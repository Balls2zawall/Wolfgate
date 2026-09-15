#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server._WF.PlanetCracker.Flight;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WF.PlanetCracker.Flight;
using Content.Shared._WF.ShipPa;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using static Content.IntegrationTests.Tests._WF.PlanetCracker.PlanetCrackerFixture;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

/// <summary>
/// F10, atmospheric flight: what lifts a hull over a planet, what happens when it stops lifting, and what it costs to
/// hit the ground. Everything here runs on the standalone Asclepiu stack, which is the only place a planet layer and
/// its gravity exist at all.
/// </summary>
[TestFixture]
[TestOf(typeof(WFFlightSystem))]
public sealed class FlightTest
{
    /// <summary>The landing thruster; WFThrusterLanding's own liftThrust is 50.</summary>
    private const string LandingThruster = "WFThrusterLanding";

    /// <summary>The conversion kit item.</summary>
    private const string Kit = "WFLandingThrusterKit";

    /// <summary>The stock thruster the kit converts.</summary>
    private const string PlainThruster = "DebugThruster";

    /// <summary>The tiny cracker's hull mass: 225 tiles at TileDensityMultiplier 0.5.</summary>
    private const float CrackerHullMass = 112.5f;

    /// <summary>WFAnchorCrateComponent.VirtualMass, times the cracker's two crates.</summary>
    private const float CrackerCargoMass = 12f;

    /// <summary>
    /// A gravity generator lifts nothing over a planet and landing thrusters lift everything. The cracker's own
    /// centrifuge is rated at 3000 against a 124.5 load, so without the exclusion the hull would read as flying.
    /// </summary>
    [Test]
    public async Task ThrustersLiftAndGravgensDoNotOnAPlanet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var zLevels = server.System<CEZLevelsSystem>();

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var topAir = layers[^2];
        var airMapId = await MapIdOf(pair, topAir);

        var hull = await BuildCracker(pair, airMapId);
        await MapInitHull(pair, hull);
        await Energise(pair, hull);

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.WfTryGetLiftRatio(hull, out var ratio), Is.True,
                "A hull on a planet air layer reports no lift ratio at all.");
            Assert.That(ratio, Is.EqualTo(0f).Within(0.001f),
                "The powered centrifuge is still counting as lift on a planet layer.");
        });

        await AddLandingThrusters(pair, hull, 2);

        await server.WaitAssertion(() =>
        {
            zLevels.WfTryGetLiftRatio(hull, out var ratio);

            Assert.That(ratio, Is.EqualTo(100f / (CrackerHullMass + CrackerCargoMass)).Within(0.01f),
                "Two 50-rated landing thrusters do not give the cracker the lift their ratings add up to.");
        });

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A hull below orbit whose lift is short sinks and is in lift lost. One thruster on the cracker is a ratio of
    /// 0.40 - under the half-lift floor - so it falls at the full rate and the alarms take the ship over.
    /// </summary>
    [Test]
    public async Task PartialLiftSinksAndEntersLiftLost()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var topAir = layers[^2];
        var airMapId = await MapIdOf(pair, topAir);

        var hull = await BuildCracker(pair, airMapId);
        await MapInitHull(pair, hull);
        await AddLandingThrusters(pair, hull, 1);

        // The fall gate sweeps at 2 Hz and the grace period is three seconds.
        await server.WaitRunTicks(pair.SecondsToTicks(5f));

        await server.WaitAssertion(() =>
        {
            var mapUid = entMan.GetComponent<TransformComponent>(hull).MapUid;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.HasComponent<CEZTransitMapComponent>(mapUid), Is.True,
                    $"The under-lifted hull is sitting on {entMan.ToPrettyString(mapUid)} instead of sinking.");
                Assert.That(entMan.HasComponent<WFLiftLostComponent>(hull), Is.True,
                    "A hull sinking under its own weight is not in lift lost.");
                Assert.That(entMan.GetComponent<WFLiftLostComponent>(hull).Ratio, Is.LessThan(1f),
                    "The lift-lost state recorded a ratio that would have flown.");
            }
        });

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The callouts escalate orbit-to-ground in order and the ship's own situation code comes back afterwards. The
    /// hull starts on yellow, so a restore to green would be the state machine forgetting rather than restoring.
    /// </summary>
    [Test]
    public async Task AlarmsEscalateAndRestoreThePriorCode()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var alerts = server.System<Content.Server._WF.ShipPa.ShipAlertSystem>();

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var orbit = layers[^1];
        var orbitMapId = await MapIdOf(pair, orbit);

        var hull = await BuildCracker(pair, orbitMapId);
        await MapInitHull(pair, hull);

        await server.WaitPost(() => alerts.SetCode(hull, "ShipCodeYellow", announce: false));

        var seen = new List<string>();

        await server.WaitPost(() => seen.Add(entMan.GetComponent<ShipAlertComponent>(hull).Code.Id));

        // No settle: the caution chime is set inside the call, and one tick of the flight sweep is already past it.
        var refusal = await EnterAtmosphere(pair, hull, settle: 0f);
        Assert.That(refusal, Is.Null, $"The confirmed descent was refused: {refusal}");

        await server.WaitPost(() => seen.Add(entMan.GetComponent<ShipAlertComponent>(hull).Code.Id));

        // A full plummet is four gaps at CE's 0.15 levels/s² up to a 1.2 terminal; sample the code as it goes.
        for (var i = 0; i < 120; i++)
        {
            await server.WaitRunTicks(pair.SecondsToTicks(0.25f));

            await server.WaitPost(() =>
            {
                if (!entMan.TryGetComponent<ShipAlertComponent>(hull, out var alert))
                    return;

                var code = alert.Code.Id;

                if (seen.Count == 0 || seen[^1] != code)
                    seen.Add(code);
            });

            if (seen.Count > 0 && seen[^1] == "ShipCodeYellow" && seen.Count > 1)
                break;
        }

        var expected = new[]
        {
            "ShipCodeYellow",
            WFFlightSystem.AlertLiftLost,
            WFFlightSystem.AlertDontSink,
            WFFlightSystem.AlertSinkRate,
            WFFlightSystem.AlertTerrain,
            WFFlightSystem.AlertTooLowTerrain,
            WFFlightSystem.AlertPullUp,
            "ShipCodeYellow",
        };

        Assert.That(seen, Is.EqualTo(expected),
            $"The flight alarms did not walk orbit to ground and back: {string.Join(" -> ", seen)}");

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>A gliding hull gains speed along its own heading each layer; a hull with none drops straight.</summary>
    [Test]
    public async Task GlideGainsSpeedPerLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var physics = server.System<SharedPhysicsSystem>();

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var orbit = layers[^1];
        var orbitMapId = await MapIdOf(pair, orbit);

        var hull = await BuildCracker(pair, orbitMapId);
        await MapInitHull(pair, hull);

        var refusal = await EnterAtmosphere(pair, hull);
        Assert.That(refusal, Is.Null, $"The confirmed descent was refused: {refusal}");

        await server.WaitPost(() => physics.SetLinearVelocity(hull, new Vector2(4f, 0f)));

        var start = 0f;
        var startDepth = int.MaxValue;

        await server.WaitAssertion(() =>
        {
            start = entMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(hull).LinearVelocity.Length();
            startDepth = entMan.GetComponent<WFLiftLostComponent>(hull).LastDepth;
        });

        // Two layer crossings is enough to tell a per-layer gain from a one-off.
        var crossed = 0;
        var speed = start;

        for (var i = 0; i < 200 && crossed < 2; i++)
        {
            await server.WaitRunTicks(pair.SecondsToTicks(0.25f));

            await server.WaitPost(() =>
            {
                if (!entMan.TryGetComponent<WFLiftLostComponent>(hull, out var lost))
                    return;

                if (lost.LastDepth >= startDepth)
                    return;

                crossed = startDepth - lost.LastDepth;
                speed = entMan.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(hull).LinearVelocity.Length();
            });
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(crossed, Is.GreaterThanOrEqualTo(2), "The hull never fell through two layers.");

            // Two 25% boosts are 1.5625x, and the hull's ordinary airborne damping eats into that over the seconds
            // the fall takes; without the glide the same seconds would have left it BELOW the speed it started at.
            Assert.That(speed, Is.GreaterThan(start * 1.15f),
                $"Two layers of a 25% glide gain left {start} at {speed}, which is not a per-layer gain at all.");
        }

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A slow touchdown out of lift lost is a hard landing: the hull survives, keeps its planar speed and skids. A
    /// fast one is still CE's crash.
    /// </summary>
    [Test]
    public async Task SlowTouchdownSkidsAndFastOneCrashes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var zLevels = server.System<CEZLevelsSystem>();

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var ground = layers[0];
        var groundMapId = await MapIdOf(pair, ground);

        await LayTiles(pair, ground, new Vector2i(-24, -24), new Vector2i(48, 48));

        var hull = await BuildCracker(pair, groundMapId);
        await MapInitHull(pair, hull);

        // The landing decision is taken purely on the state and the touchdown speed, so both are set by hand rather
        // than flown: a real plummet lands at terminal velocity every time and would only ever test one branch.
        await server.WaitPost(() =>
        {
            entMan.EnsureComponent<CEZGridFallerComponent>(hull);
            var lost = entMan.EnsureComponent<WFLiftLostComponent>(hull);
            lost.Ratio = 0.2f;
        });

        await server.WaitAssertion(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(hull);
            var faller = entMan.GetComponent<CEZGridFallerComponent>(hull);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(zLevels.WfTryHardLanding((hull, grid, faller), CEZLevelsSystem.WFHardLandingSpeed + 0.1f),
                    Is.False, "A touchdown above the hard-landing speed was not left to the crash path.");
                Assert.That(zLevels.WfTryHardLanding((hull, grid, faller), CEZLevelsSystem.WFHardLandingSpeed - 0.1f),
                    Is.True, "A touchdown under the hard-landing speed did not become a hard landing.");
            }
        });

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.EntityExists(hull), Is.True, "The hard-landed hull was destroyed.");
                Assert.That(entMan.HasComponent<WFSkidComponent>(hull), Is.True, "The hard-landed hull is not skidding.");
                Assert.That(entMan.HasComponent<WFLiftLostComponent>(hull), Is.False,
                    "A hull that is on the ground is still in lift lost.");
            }
        });

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>The kit swaps an anchored ordinary thruster for the landing variant, in place, and is consumed.</summary>
    [Test]
    public async Task KitConvertsAThruster()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var interaction = server.System<SharedInteractionSystem>();
        var receiver = server.System<SharedPowerReceiverSystem>();

        var map = await pair.CreateTestMap();
        var transport = await BuildTransport(pair, map.MapId);

        var thruster = EntityUid.Invalid;
        var user = EntityUid.Invalid;
        var kit = EntityUid.Invalid;
        var coords = default(EntityCoordinates);

        await server.WaitPost(() =>
        {
            foreach (var child in Children(entMan, transport))
            {
                if (entMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID == PlainThruster)
                    thruster = child;
            }

            Assert.That(thruster, Is.Not.EqualTo(EntityUid.Invalid), "The transport carries no ordinary thruster.");

            coords = entMan.GetComponent<TransformComponent>(thruster).Coordinates;

            user = entMan.SpawnEntity(ViewerProto, new EntityCoordinates(transport, new Vector2(3.5f, 3.5f)));
            kit = entMan.SpawnEntity(Kit, new EntityCoordinates(transport, new Vector2(3.5f, 3.5f)));
        });

        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitPost(() => interaction.InteractUsing(user, kit, thruster, coords));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            var landing = EntityUid.Invalid;
            var plain = 0;

            foreach (var child in Children(entMan, transport))
            {
                var proto = entMan.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID;

                if (proto == LandingThruster && entMan.GetComponent<TransformComponent>(child).Coordinates.Position == coords.Position)
                    landing = child;

                if (proto == PlainThruster)
                    plain++;
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(landing, Is.Not.EqualTo(EntityUid.Invalid), "The kit left no landing thruster where the old one stood.");
                Assert.That(entMan.GetComponent<TransformComponent>(landing).Anchored, Is.True,
                    "The converted thruster is not anchored.");
                Assert.That(plain, Is.EqualTo(3), "The old thruster was not consumed by the conversion.");
                Assert.That(entMan.EntityExists(kit), Is.False, "The kit survived the conversion.");
            }

            // Silences the unused-variable warning on a helper the test keeps for symmetry with the factory.
            Assert.That(receiver, Is.Not.Null);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The confirm gate: an under-lifted hull is refused the descent until it says yes, and a raw descend input out
    /// of orbit is refused outright so the gate cannot be walked around.
    /// </summary>
    [Test]
    public async Task DescentNeedsConfirmAndRawInputIsRefused()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var orbit = layers[^1];
        var orbitMapId = await MapIdOf(pair, orbit);

        var hull = await BuildCracker(pair, orbitMapId);
        await MapInitHull(pair, hull);

        // A held descend key is the old way down; it now does nothing at all from orbit.
        await HoldDescend(pair, hull);
        await server.WaitRunTicks(pair.SecondsToTicks(3f));

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<TransformComponent>(hull).MapUid, Is.EqualTo(orbit),
                "A raw descend input took the hull out of orbit, bypassing the confirm.");
        });

        var refused = await EnterAtmosphere(pair, hull, confirmed: false);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(refused, Is.Not.Null, "An under-lifted hull was let out of orbit without confirming.");
                Assert.That(entMan.GetComponent<TransformComponent>(hull).MapUid, Is.EqualTo(orbit),
                    "The refused descent moved the hull anyway.");
            }
        });

        var accepted = await EnterAtmosphere(pair, hull, confirmed: true);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(accepted, Is.Null, $"The confirmed descent was refused: {accepted}");
                Assert.That(entMan.HasComponent<CEZTransitMapComponent>(entMan.GetComponent<TransformComponent>(hull).MapUid),
                    Is.True, "The confirmed descent did not put the hull into the gap below orbit.");
            }
        });

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }

    /// <summary>The map id of a z-layer, for the spawners that want one.</summary>
    private static async Task<MapId> MapIdOf(TestPair pair, EntityUid layer)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapId = MapId.Nullspace;

        await server.WaitPost(() => mapId = entMan.GetComponent<MapComponent>(layer).MapId);
        return mapId;
    }
}
