#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server._WF.Encounters.Components;
using Content.Server._WF.Encounters.Systems;
using Content.Server._WF.NpcCrew.Components;
using Content.Server._WF.NpcCrew.Systems;
using Content.Server.Shuttles.Events;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Encounters;
using Content.Shared._WF.NpcCrew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.Encounters;

public sealed partial class WFEncounterTest
{
    [TestPrototypes]
    private const string SimulationPrototypes = @"
- type: wfEncounter
  id: WFTestSimWanderer
  name: wf-encounter-name-convoy
  start: Manual
  ships:
  - key: ship
    vessel: WFDredger
    side: wanderers
    wander: 4
    wanderRadius: 1500

- type: wfEncounter
  id: WFTestSimRaider
  name: wf-encounter-name-pirates
  start: Manual
  ships:
  - key: ship
    vessel: WFDredger
    side: reavers
    faction: PirateNF
    company: WFAshfallReavers
    objectives:
    - kind: Hold

- type: wfEncounter
  id: WFTestSimHauler
  name: wf-encounter-name-convoy
  start: Manual
  ships:
  - key: ship
    vessel: WFDredger
    side: freight
    company: PDV
    objectives:
    - kind: Hold
";

    /// <summary>Turns simulation on for a test, with quick spawning and going back, and restores it after.</summary>
    private async Task WithSimulation(Func<Task> body)
    {
        var config = Server.ResolveDependency<IConfigurationManager>();
        await Server.WaitPost(() =>
        {
            config.SetCVar(EncountersCVars.Simulate, true);
            config.SetCVar(EncountersCVars.DematerializeDelay, 1f);
            config.SetCVar(EncountersCVars.MaterializeInterval, 0.1f);
        });
        try
        {
            await body();
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                config.SetCVar(EncountersCVars.Simulate, false);
                config.SetCVar(EncountersCVars.DematerializeDelay, EncountersCVars.DematerializeDelay.DefaultValue);
                config.SetCVar(EncountersCVars.MaterializeInterval, EncountersCVars.MaterializeInterval.DefaultValue);
                config.SetCVar(EncountersCVars.DuelMeanSeconds, EncountersCVars.DuelMeanSeconds.DefaultValue);
            });
        }
    }

    /// <summary>Runs ticks a second at a time until a condition holds on the server, failing past a limit.</summary>
    private async Task Until(Func<bool> condition, int seconds, string what)
    {
        var met = false;
        for (var i = 0; i < seconds && !met; i++)
        {
            await RunTicks(60);
            await Server.WaitPost(() => met = condition());
        }

        Assert.That(met, Is.True, what);
    }

    private WFEncounterShipState Ship(EntityUid encounter) => SEntMan.GetComponent<WFEncounterComponent>(encounter).Ships["ship"];

    private void MovePlayer(Vector2 to)
    {
        Server.System<SharedTransformSystem>().SetMapCoordinates(SPlayer, new MapCoordinates(to, MapData.MapId));
    }

    /// <summary>
    /// A ship far from everyone starts simulated and flies its orders as a point; a player coming near spawns its hull,
    /// crewed and still on its orders; once the player has gone, the untouched ship goes back to simulation.
    /// </summary>
    [Test]
    public async Task SimulatedShipsSpawnWhenAPlayerComesNearAndGoBackWhenLeft()
    {
        await WithSimulation(async () =>
        {
            EntityUid encounter = default, proxy = default;
            var start = new Vector2(30000, 30000);
            await Server.WaitAssertion(() =>
            {
                var prototype = Server.ResolveDependency<IPrototypeManager>().Index<WFEncounterPrototype>("WFTestSimWanderer");
                Assert.That(Server.System<WFEncounterSystem>().TrySpawn(prototype, new MapCoordinates(start, MapData.MapId), out encounter), Is.True);
                var ship = Ship(encounter);
                proxy = ship.Grid;
                Assert.That(ship.Virtual, Is.True, "Nobody near: the ship starts simulated.");
                Assert.That(SEntMan.HasComponent<MapGridComponent>(proxy), Is.False, "Its stand-in is no grid.");
                Assert.That(SEntMan.HasComponent<WFCrewVirtualShipComponent>(proxy), Is.True);
                Assert.That(Server.System<WFCrewObjectiveSystem>().QueueStatus(proxy, ship.Group), Is.Not.Null, "Its orders are on the stand-in.");
            });

            await Until(() => (Server.System<SharedTransformSystem>().GetWorldPosition(proxy) - start).Length() > 50f, 30,
                "The simulated ship flies its wander legs.");

            await Server.WaitPost(() => MovePlayer(Server.System<SharedTransformSystem>().GetWorldPosition(proxy) + new Vector2(0f, 1500f)));
            await Until(() => !Ship(encounter).Virtual, 30, "A player within range spawns the hull.");
            await Server.WaitAssertion(() =>
            {
                var ship = Ship(encounter);
                Assert.That(SEntMan.HasComponent<MapGridComponent>(ship.Grid), Is.True);
                Assert.That(SEntMan.EntityExists(proxy), Is.False, "The stand-in is gone.");
                Assert.That(Server.System<WFEncounterSystem>().HasLivingCrew(ship, anywhere: true), Is.True, "The hull is crewed.");
                Assert.That(Server.System<WFCrewObjectiveSystem>().QueueStatus(ship.Grid, ship.Group), Is.Not.Null, "Its orders came with it.");
                Assert.That(SEntMan.GetComponent<WFEncounterComponent>(encounter).Resolution, Is.Null, "The encounter runs on.");
            });

            EntityUid hull = default;
            await Server.WaitPost(() =>
            {
                hull = Ship(encounter).Grid;
                MovePlayer(Vector2.Zero);
            });
            await Until(() => Ship(encounter).Virtual, 30, "Left alone, the untouched ship goes back to simulation.");
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.EntityExists(hull), Is.False, "Its hull is gone.");
                Assert.That(Server.System<WFEncounterSystem>().InFight(Ship(encounter)), Is.True, "And it is still in the encounter.");
                Server.System<WFEncounterSystem>().End(encounter);
            });
        });
    }

    /// <summary>A jump plotted to a point near a simulated ship spawns its hull while the jumper is in transit.</summary>
    [Test]
    public async Task AJumpPlottedNearASimulatedShipSpawnsIt()
    {
        await WithSimulation(async () =>
        {
            EntityUid encounter = default;
            var at = new Vector2(-30000, 30000);
            await Server.WaitAssertion(() =>
            {
                var prototype = Server.ResolveDependency<IPrototypeManager>().Index<WFEncounterPrototype>("WFTestSimHauler");
                Assert.That(Server.System<WFEncounterSystem>().TrySpawn(prototype, new MapCoordinates(at, MapData.MapId), out encounter), Is.True);
                Assert.That(Ship(encounter).Virtual, Is.True);
                var ev = new FTLStartedEvent(MapData.Grid, new EntityCoordinates(MapData.MapUid, at + new Vector2(500f, 0f)), MapData.MapUid,
                    Matrix3x2.Identity, Angle.Zero);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
            });

            await Until(() => !Ship(encounter).Virtual, 30, "The ship near the jump's exit is spawned.");
            await Server.WaitPost(() => Server.System<WFEncounterSystem>().End(encounter));
        });
    }

    /// <summary>Two simulated ships ordered to attack each other fight it out by a roll, nobody watching: one is lost and the other's side has it.</summary>
    [Test]
    public async Task UnwatchedSimulatedShipsFightItOutByARoll()
    {
        await WithSimulation(async () =>
        {
            EntityUid raider = default, hauler = default;
            await Server.WaitAssertion(() =>
            {
                Server.ResolveDependency<IConfigurationManager>().SetCVar(EncountersCVars.DuelMeanSeconds, 1f);
                var prototypes = Server.ResolveDependency<IPrototypeManager>();
                var encounters = Server.System<WFEncounterSystem>();
                Assert.That(encounters.TrySpawn(prototypes.Index<WFEncounterPrototype>("WFTestSimRaider"),
                    new MapCoordinates(new Vector2(30000, -30000), MapData.MapId), out raider), Is.True);
                Assert.That(encounters.TrySpawn(prototypes.Index<WFEncounterPrototype>("WFTestSimHauler"),
                    new MapCoordinates(new Vector2(30800, -30000), MapData.MapId), out hauler), Is.True);
                var attack = new List<WFCrewObjective>
                {
                    new() { Kind = WFCrewObjectiveKind.Attack, Target = SEntMan.GetNetEntity(Ship(hauler).Grid), Range = 600f },
                };
                Assert.That(encounters.SetOrders(raider, "ship", attack), Is.True);
            });

            await Until(() => SEntMan.Deleted(Ship(raider).Grid) || SEntMan.Deleted(Ship(hauler).Grid), 30,
                "One of the two is lost in the simulated fight.");
            await Until(() => SEntMan.Deleted(raider) || SEntMan.GetComponent<WFEncounterComponent>(raider).Resolution != null
                              || SEntMan.Deleted(hauler) || SEntMan.GetComponent<WFEncounterComponent>(hauler).Resolution != null, 15,
                "The beaten ship's encounter is over.");
            await Server.WaitPost(() =>
            {
                foreach (var uid in new[] { raider, hauler })
                {
                    if (!SEntMan.Deleted(uid))
                        Server.System<WFEncounterSystem>().End(uid);
                }
            });
        });
    }

    /// <summary>
    /// A simulated ship sent to dock with another counts as docked once it reaches it, and keeps counting as docked
    /// when both are spawned, while it comes alongside for real.
    /// </summary>
    [Test]
    public async Task SimulatedDockingCarriesOverWhenTheShipsSpawn()
    {
        await WithSimulation(async () =>
        {
            EntityUid host = default, visitor = default;
            var at = new Vector2(-30000, -30000);
            await Server.WaitAssertion(() =>
            {
                var prototypes = Server.ResolveDependency<IPrototypeManager>();
                var encounters = Server.System<WFEncounterSystem>();
                Assert.That(encounters.TrySpawn(prototypes.Index<WFEncounterPrototype>("WFTestSimHauler"),
                    new MapCoordinates(at, MapData.MapId), out host), Is.True);
                Assert.That(encounters.TrySpawn(prototypes.Index<WFEncounterPrototype>("WFTestSimHauler"),
                    new MapCoordinates(at + new Vector2(400f, 0f), MapData.MapId), out visitor), Is.True);
                var dock = new List<WFCrewObjective>
                {
                    new() { Kind = WFCrewObjectiveKind.Dock, Target = SEntMan.GetNetEntity(Ship(host).Grid) },
                    new() { Kind = WFCrewObjectiveKind.Hold },
                };
                Assert.That(encounters.SetOrders(visitor, "ship", dock), Is.True);
            });

            await Until(() => Server.System<WFEncounterSystem>().AreDocked(Ship(visitor), Ship(host)), 40,
                "The simulated visitor reaches the host and counts as docked.");

            await Server.WaitPost(() => MovePlayer(at + new Vector2(0f, 1500f)));
            await Until(() => !Ship(host).Virtual && !Ship(visitor).Virtual, 30, "Both hulls are spawned.");
            await Server.WaitAssertion(() =>
            {
                var encounters = Server.System<WFEncounterSystem>();
                Assert.That(encounters.AreDocked(Ship(visitor), Ship(host)), Is.True, "Still docked while it comes alongside.");
                Assert.That(Ship(visitor).RedockTo, Is.EqualTo(Ship(host).Grid), "It is docking with the host's hull.");
                var bounds = Server.System<EntityLookupSystem>();
                Assert.That(bounds.GetWorldAABB(Ship(visitor).Grid).Intersects(bounds.GetWorldAABB(Ship(host).Grid)), Is.False,
                    "The visitor's hull is spawned clear of the host's.");
                encounters.End(host);
                encounters.End(visitor);
                MovePlayer(Vector2.Zero);
            });
        });
    }
}
