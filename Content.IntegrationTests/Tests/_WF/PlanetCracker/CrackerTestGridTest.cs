#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Pair;
using Content.Server._CE.ZLevels.Core;
using Content.Server._WF.PlanetCracker.Cracker;
using Content.Server._WF.PlanetCracker.Planets;
using Content.Server._WF.PlanetCracker.Testing;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.PlanetCracker.Anchors;
using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared._WF.PlanetCracker.Planets;
using Content.Shared.Gravity;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

/// <summary>
/// The two code-built test hulls: their exact contents and layout, the masses the pooled gravgen check weighs them
/// with, the D11 virtual mass a carried anchor adds, and the ownership stamp both spawn paths have to apply.
/// </summary>
[TestFixture]
[TestOf(typeof(WFTestGridFactory))]
public sealed class CrackerTestGridTest
{
    private const string Surface = "WFSurfaceAsclepiu";
    private const string Crate = "WFAnchorCrate";
    private const string Anchor = "WFGravityAnchor";
    private const string Gravgen = "WFTransportGravgen";
    private const string HullTile = "FloorSteel";

    /// <summary>Plan F.1: fourteen entities on the tiny cracker.</summary>
    private static readonly Dictionary<string, int> CrackerContents = new()
    {
        ["ComputerShuttle"] = 1,
        ["WFCrackConsole"] = 1,
        ["DebugGyroscope"] = 1,
        ["WFCentrifuge"] = 1,
        ["WFGravityProjector"] = 2,
        ["WFChunkBerthMarker"] = 1,
        ["AirlockShuttle"] = 1,
        ["WFAnchorCrate"] = 2,
        ["DebugThruster"] = 4,
    };

    /// <summary>Plan F.2: eight entities on the micro transport.</summary>
    private static readonly Dictionary<string, int> TransportContents = new()
    {
        ["ComputerShuttle"] = 1,
        ["WFTransportGravgen"] = 1,
        ["AirlockShuttle"] = 1,
        ["WFAnchorCrate"] = 1,
        ["DebugThruster"] = 4,
    };

    /// <summary>Tiles in the tiny cracker hull, 15x15.</summary>
    private const int CrackerTiles = 225;

    /// <summary>Tiles in the micro transport hull, 7x9.</summary>
    private const int TransportTiles = 63;

    /// <summary>ShuttleSystem.TileDensityMultiplier; the tests read it back rather than trusting the constant.</summary>
    private const float TileDensity = 0.5f;

    /// <summary>WFTransportGravgen.maxHandledMass.</summary>
    private const float TransportCapacity = 40f;

    /// <summary>WFCentrifuge.maxHandledMass.</summary>
    private const float CentrifugeCapacity = 3000f;

    /// <summary>WFGravityAnchorComponent.VirtualMass and WFAnchorCrateComponent.VirtualMass.</summary>
    private const float AnchorVirtualMass = 6f;

    [Test]
    public async Task CrackerGridHasTheExpectedContents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(cracker);
            Assert.That(maps.GetAllTiles(cracker, grid).Count(), Is.EqualTo(CrackerTiles),
                "The tiny cracker should be a solid 15x15 of deck plating.");

            var contents = Contents(entMan, cracker);

            using (Assert.EnterMultipleScope())
            {
                foreach (var (proto, count) in CrackerContents)
                {
                    Assert.That(contents.GetValueOrDefault(proto), Is.EqualTo(count),
                        $"The cracker carries the wrong number of {proto}.");
                }

                Assert.That(contents.Values.Sum(), Is.EqualTo(CrackerContents.Values.Sum()),
                    $"The cracker carries entities the layout does not list: {string.Join(", ", contents.Keys)}");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>The layout-typo guard: a tile index off the hull would leave an entity over empty space.</summary>
    [Test]
    public async Task EverythingSitsOnASolidTile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(cracker);
            var children = Children(entMan, cracker).ToList();

            Assert.That(children, Has.Count.EqualTo(CrackerContents.Values.Sum()),
                "Precondition: the hull carries its fourteen entities.");

            using (Assert.EnterMultipleScope())
            {
                foreach (var uid in children)
                {
                    var xform = entMan.GetComponent<TransformComponent>(uid);
                    var index = maps.TileIndicesFor(cracker, grid, xform.Coordinates);

                    Assert.That(maps.TryGetTileRef(cracker, grid, index, out var tile) && !tile.Tile.IsEmpty, Is.True,
                        $"{entMan.ToPrettyString(uid)} at {index} is not standing on deck plating.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Anchored per role, not across the board: the crates carry Anchorable flags: None, which AnchorableSystem
    /// refuses outright, so a blanket assertion would be asserting something the prototype cannot do.
    /// </summary>
    [Test]
    public async Task OnlyTheMachinesAreAnchored()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var uid in Children(entMan, cracker))
                {
                    var proto = entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
                    var anchored = entMan.GetComponent<TransformComponent>(uid).Anchored;

                    if (proto == Crate)
                        Assert.That(anchored, Is.False, "An anchor crate is anchored; it is meant to be dragged.");
                    else
                        Assert.That(anchored, Is.True, $"{proto} should be wrenched down on the hull.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TransportGridHasTheExpectedContents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();

        var map = await pair.CreateTestMap();
        var transport = await BuildTransport(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(transport);
            Assert.That(maps.GetAllTiles(transport, grid).Count(), Is.EqualTo(TransportTiles),
                "The micro transport should be a solid 7x9 of deck plating.");

            var contents = Contents(entMan, transport);

            using (Assert.EnterMultipleScope())
            {
                foreach (var (proto, count) in TransportContents)
                {
                    Assert.That(contents.GetValueOrDefault(proto), Is.EqualTo(count),
                        $"The transport carries the wrong number of {proto}.");
                }

                Assert.That(contents.Values.Sum(), Is.EqualTo(TransportContents.Values.Sum()),
                    $"The transport carries entities the layout does not list: {string.Join(", ", contents.Keys)}");

                var crate = Children(entMan, transport)
                    .First(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == Crate);

                Assert.That(entMan.GetComponent<TransformComponent>(crate).Anchored, Is.False,
                    "The transport's crate is anchored; it is cargo.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Both hulls have to actually fly. A thruster only adds to ShuttleComponent.LinearThrust if the grid already
    /// carries that component when the thruster initialises (ThrusterSystem.EnableThruster returns early otherwise,
    /// and its IsOn guard means nothing re-registers it later), so this is the guard on the build order.
    /// </summary>
    [Test]
    public async Task BothHullsRegisterTheirThrust()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);
        var transport = await BuildTransport(pair, map.MapId, new Vector2(200f, 0f));

        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            var crackerShuttle = entMan.GetComponent<ShuttleComponent>(cracker);
            var transportShuttle = entMan.GetComponent<ShuttleComponent>(transport);

            using (Assert.EnterMultipleScope())
            {
                // Thruster facing picks the index: South 0, East 1, North 2, West 3 (ThrusterSystem.cs:342).
                for (var dir = 0; dir < 4; dir++)
                {
                    Assert.That(crackerShuttle.LinearThrust[dir], Is.GreaterThan(0f),
                        $"The cracker registered no thrust in direction {dir}.");
                    Assert.That(transportShuttle.LinearThrust[dir], Is.GreaterThan(0f),
                        $"The transport registered no thrust in direction {dir}.");
                }

                Assert.That(crackerShuttle.AngularThrust, Is.GreaterThan(0f),
                    "The cracker's gyroscope registered no angular thrust.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>The pooled lift check weighs a hull as tiles x TileDensityMultiplier and nothing else.</summary>
    [Test]
    public async Task HullMassMatchesTileCount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);
        var transport = await BuildTransport(pair, map.MapId, new Vector2(200f, 0f));

        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                // Not exact: grid chunk fixtures are polygons with a physics skin, so a hull measures fractionally
                // UNDER tile count times the density multiplier - the 15x15 cracker reads 112.2002 against 112.5.
                // Hence a half-open band exactly one tile's 0.5 contribution wide instead of a tolerance: a hull
                // one tile too small falls below it and one tile too large rises above the exact figure.
                Assert.That(entMan.GetComponent<PhysicsComponent>(cracker).FixturesMass,
                    Is.GreaterThan(CrackerTiles * TileDensity - TileDensity)
                        .And.LessThanOrEqualTo(CrackerTiles * TileDensity),
                    "The cracker hull's mass no longer matches its tile count.");

                Assert.That(entMan.GetComponent<PhysicsComponent>(transport).FixturesMass,
                    Is.GreaterThan(TransportTiles * TileDensity - TileDensity)
                        .And.LessThanOrEqualTo(TransportTiles * TileDensity),
                    "The transport hull's mass no longer matches its tile count.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Hull 31.5 plus one crated anchor's 6 virtual mass is 37.5, inside the 40 the mini gravgen is rated for.</summary>
    [Test]
    public async Task TransportGravgenIsRatedForTheHullPlusOneAnchor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var zLevels = server.System<CEZLevelsSystem>();

        var map = await pair.CreateTestMap();
        var transport = await BuildTransport(pair, map.MapId);
        await Energise(pair, transport);

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.TryGetGravgenLoad(transport, out var mass, out var capacity), Is.True,
                "The transport reports no gravgen load at all.");

            var hull = entMan.GetComponent<PhysicsComponent>(transport).FixturesMass;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(capacity, Is.EqualTo(TransportCapacity).Within(0.01f),
                    "The transport gravgen is not rated at 40.");
                Assert.That(mass - hull, Is.EqualTo(AnchorVirtualMass).Within(0.01f),
                    "The load is not the hull plus exactly one crated anchor's virtual mass.");
                Assert.That(mass, Is.LessThanOrEqualTo(capacity), "The transport cannot lift its own single anchor.");
                Assert.That(capacity, Is.LessThan(50f), "The rating is not a downgrade from the stock mini gravgen.");
                Assert.That(capacity, Is.LessThan(TransportTiles), "The rating lets the transport lift twice its hull.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The test that proves D11 bites: without the two marked lines in CEZLevelsSystem.Gravity.cs the reported mass
    /// would sit at the bare hull's 31.5 however much cargo was aboard.
    /// </summary>
    [Test]
    public async Task SecondAnchorOverloadsTheTransport()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var zLevels = server.System<CEZLevelsSystem>();

        var map = await pair.CreateTestMap();
        var transport = await BuildTransport(pair, map.MapId);
        await Energise(pair, transport);

        var before = 0f;

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.TryGetGravgenLoad(transport, out before, out var capacity), Is.True,
                "The transport reports no gravgen load at all.");
            Assert.That(before, Is.LessThanOrEqualTo(capacity), "Precondition: one anchor still flies.");
        });

        await server.WaitPost(() => entMan.SpawnEntity(Crate, new EntityCoordinates(transport, new Vector2(1.5f, 7.5f))));
        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.TryGetGravgenLoad(transport, out var mass, out var capacity), Is.True,
                "The transport reports no gravgen load at all.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mass - before, Is.EqualTo(AnchorVirtualMass).Within(0.01f),
                    "The second crate did not add its virtual mass to the load.");
                Assert.That(mass, Is.GreaterThan(capacity), "Two anchors should put the transport over its rating.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Both exclusions in the capacity sweep at once: a planet ground layer is itself a grid, and a wrenched-down
    /// anchor is terrain. Either one leaking would put cargo mass on a whole planet network's pooled lift.
    /// </summary>
    [Test]
    public async Task DeployedAnchorsDoNotLoadAPlanetLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var transform = server.System<SharedTransformSystem>();

        await EnableFeature(pair);
        var stack = await BuildStandalone(pair);
        var ground = stack[0];

        await LayTiles(pair, ground, new Vector2i(-4, -4), new Vector2i(28, 8));

        var anchors = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            anchors.Add(entMan.SpawnEntity(Anchor, new EntityCoordinates(ground, new Vector2(0.5f, 0.5f))));
            anchors.Add(entMan.SpawnEntity(Anchor, new EntityCoordinates(ground, new Vector2(24.5f, 0.5f))));
        });

        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
            Assert.That(entMan.HasComponent<WFGridAnchorLoadComponent>(ground), Is.False,
                "Loose anchors resting on a planet ground layer added cargo mass to the layer itself."));

        await server.WaitPost(() =>
        {
            foreach (var anchor in anchors)
            {
                transform.AnchorEntity(anchor);
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var anchor in anchors)
                {
                    Assert.That(entMan.GetComponent<TransformComponent>(anchor).Anchored, Is.True,
                        "Precondition: the anchor is wrenched down on the ground layer.");
                    Assert.That(entMan.GetComponent<WFGravityAnchorComponent>(anchor).State,
                        Is.Not.EqualTo(WFAnchorState.Loose), "Precondition: the anchor deployed.");
                }

                Assert.That(entMan.HasComponent<WFGridAnchorLoadComponent>(ground), Is.False,
                    "Deployed anchors added cargo mass to the planet ground layer.");
            }
        });

        await Teardown(pair, stack);
        await pair.CleanReturnAsync();
    }

    /// <summary>The player-facing half of D11: the rating stays at one while the count climbs past it.</summary>
    [Test]
    public async Task AnchorCapacityCountsWhatIsAboard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var transport = await BuildTransport(pair, map.MapId);

        await server.WaitPost(() => entMan.SpawnEntity(Crate, new EntityCoordinates(transport, new Vector2(1.5f, 7.5f))));
        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            var gravgen = Children(entMan, transport)
                .First(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == Gravgen);

            var capacity = entMan.GetComponent<WFAnchorCapacityComponent>(gravgen);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(capacity.Aboard, Is.EqualTo(2), "The capacity sweep did not count both crates.");
                Assert.That(capacity.Capacity, Is.EqualTo(1), "The transport is rated for more than one anchor.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>The centrifuge's placeholder rating has to clear the hull and must never read as the infinite-lift zero.</summary>
    [Test]
    public async Task CentrifugeIsRatedAboveTheHull()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var zLevels = server.System<CEZLevelsSystem>();

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);
        await Energise(pair, cracker);

        await server.WaitAssertion(() =>
        {
            Assert.That(zLevels.TryGetGravgenLoad(cracker, out var mass, out var capacity), Is.True,
                "The cracker reports no gravgen load at all.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(capacity, Is.EqualTo(CentrifugeCapacity).Within(0.01f),
                    "The centrifuge is not rated at 3000.");
                Assert.That(capacity, Is.GreaterThan(0f),
                    "A rating of zero or less reads as infinite lift in HasPooledGravgenSupport.");
                Assert.That(capacity, Is.GreaterThan(CrackerTiles * TileDensity),
                    "The centrifuge cannot lift its own hull.");
                Assert.That(mass - entMan.GetComponent<PhysicsComponent>(cracker).FixturesMass,
                    Is.EqualTo(2f * AnchorVirtualMass).Within(0.01f),
                    "The load is not the hull plus exactly its two crated anchors' virtual mass.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Proves the factory's SetNeedsPower(false) route gets the centrifuge all the way to gravity on the grid.</summary>
    [Test]
    public async Task CentrifugeActivatesWithoutCabling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);
        await Energise(pair, cracker);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(cracker, out GravityComponent? gravity), Is.True,
                "The cracker hull never got a gravity component.");
            Assert.That(gravity!.Enabled, Is.True, "The centrifuge charged but never switched the grid's gravity on.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>The berth hangs eight tiles beyond the north hull edge, clear of the 15x15 deck.</summary>
    [Test]
    public async Task BerthCentreSitsOffTheHull()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var crackers = server.System<WFCrackerSystem>();

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<WFPlanetCrackerComponent>(cracker);
            Assert.That(comp.Berth, Is.Not.Null, "The cracker never resolved its berth marker.");

            Assert.That(crackers.TryGetBerthCentre((cracker, comp), out var centre), Is.True,
                "The berth centre could not be computed.");

            var grid = entMan.GetComponent<MapGridComponent>(cracker);
            var local = maps.WorldToLocal(cracker, grid, centre.Position);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(local.X, Is.EqualTo(7.5f).Within(0.01f), "The berth is not centred on the hull.");
                Assert.That(local.Y, Is.EqualTo(22.5f).Within(0.01f), "The berth is not eight tiles off the north edge.");

                var index = maps.LocalToTile(cracker, grid, new EntityCoordinates(cracker, local));
                Assert.That(maps.TryGetTileRef(cracker, grid, index, out var tile) && !tile.Tile.IsEmpty, Is.False,
                    "The berth centre sits on the hull instead of off it.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Ownership is what keeps two crackers from sharing a pair, so an unowned crate must stay unowned.</summary>
    [Test]
    public async Task CratesAreBoundToTheCracker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();
        var cracker = await BuildCracker(pair, map.MapId);

        var loose = EntityUid.Invalid;
        await server.WaitPost(() => loose = entMan.SpawnEntity(Crate, map.GridCoords));
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var owner = entMan.GetNetEntity(cracker);
            var crates = Children(entMan, cracker)
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == Crate)
                .ToList();

            Assert.That(crates, Has.Count.EqualTo(2), "Precondition: two crates shipped with the cracker.");

            using (Assert.EnterMultipleScope())
            {
                foreach (var crate in crates)
                {
                    Assert.That(entMan.GetComponent<WFAnchorCrateComponent>(crate).Cracker, Is.EqualTo(owner),
                        "A crate aboard the cracker was never stamped with its hull.");
                }

                Assert.That(entMan.GetComponent<WFAnchorCrateComponent>(loose).Cracker, Is.Null,
                    "A crate on an unrelated grid was stamped with the cracker.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The admin and ERT path raises no ShipyardShuttlePurchaseEvent, so it calls BindAboard directly; this is that
    /// call, on a hand-built hull the shipyard never touched.
    /// </summary>
    [Test]
    public async Task AdminSpawnBindsAnchorsToo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var ownership = server.System<WFCrackerOwnershipSystem>();

        var map = await pair.CreateTestMap();
        var hull = EntityUid.Invalid;
        var crates = new List<EntityUid>();
        var bound = 0;

        await server.WaitPost(() =>
        {
            var grid = mapMan.CreateGridEntity(map.MapId);
            var floor = new Tile(tileDefs[HullTile].TileId);
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();

            for (var x = 0; x < 4; x++)
            for (var y = 0; y < 4; y++)
            {
                tiles.Add((new Vector2i(x, y), floor));
            }

            maps.SetTiles(grid.Owner, grid.Comp, tiles);
            hull = grid.Owner;

            entMan.EnsureComponent<WFPlanetCrackerComponent>(hull);
            crates.Add(entMan.SpawnEntity(Crate, new EntityCoordinates(hull, new Vector2(1.5f, 1.5f))));
            crates.Add(entMan.SpawnEntity(Crate, new EntityCoordinates(hull, new Vector2(2.5f, 2.5f))));
        });

        await server.WaitRunTicks(1);

        await server.WaitPost(() => bound = ownership.BindAboard(hull));

        await server.WaitAssertion(() =>
        {
            var owner = entMan.GetNetEntity(hull);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bound, Is.EqualTo(2), "BindAboard did not stamp both crates.");

                foreach (var crate in crates)
                {
                    Assert.That(entMan.GetComponent<WFAnchorCrateComponent>(crate).Cracker, Is.EqualTo(owner),
                        "A crate on the admin-spawned hull was left unowned.");
                }
            }
        });

        await server.WaitPost(() => entMan.DeleteEntity(hull));
        await pair.CleanReturnAsync();
    }

    /// <summary>Builds the tiny cracker through the factory and lets its fixtures settle.</summary>
    private static async Task<EntityUid> BuildCracker(TestPair pair, MapId map, Vector2? offset = null)
    {
        var server = pair.Server;
        var factory = server.System<WFTestGridFactory>();
        var grid = EntityUid.Invalid;

        await server.WaitPost(() => grid = factory.BuildCracker(map, offset ?? Vector2.Zero));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
        return grid;
    }

    /// <summary>Builds the micro transport through the factory and lets its fixtures settle.</summary>
    private static async Task<EntityUid> BuildTransport(TestPair pair, MapId map, Vector2? offset = null)
    {
        var server = pair.Server;
        var factory = server.System<WFTestGridFactory>();
        var grid = EntityUid.Invalid;

        await server.WaitPost(() => grid = factory.BuildTransport(map, offset ?? new Vector2(100f, 0f)));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
        return grid;
    }

    /// <summary>
    /// Winds every PowerCharge machine on a hull up to full so its gravity generator activates. The shipped charge
    /// rates are 100 s for the mini gravgen and 240 s for the centrifuge, which no integration test can tick through;
    /// PowerChargeComponent is [Access(typeof(PowerChargeSystem))], so the rate is raised by reflection and the
    /// machine still has to charge, activate and light the grid on its own.
    /// </summary>
    private static async Task Energise(TestPair pair, EntityUid grid)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        var rate = typeof(PowerChargeComponent).GetProperty("ChargeRate",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(rate, Is.Not.Null, "PowerChargeComponent.ChargeRate was not found.");

        await server.WaitPost(() =>
        {
            foreach (var uid in Children(entMan, grid))
            {
                if (entMan.TryGetComponent(uid, out PowerChargeComponent? charge))
                    rate!.SetValue(charge, 10f);
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(2f));
    }

    /// <summary>Turns the feature on for this pair; TestPair reverts the change when the pair is returned.</summary>
    private static async Task EnableFeature(TestPair pair)
    {
        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(PlanetCrackerCVars.PlanetNetworks, true));
    }

    /// <summary>Builds an unowned Asclepiu stack at the origin and returns its layers, ground first.</summary>
    private static async Task<List<EntityUid>> BuildStandalone(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var networks = server.System<WFPlanetNetworkSystem>();
        var layers = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var surface = proto.Index<WFPlanetSurfacePrototype>(Surface);
            var built = networks.BuildNetwork(surface, Vector2.Zero, "Asclepiu", null);

            Assert.That(built, Is.Not.Null, "The planet network failed to build.");
            layers.AddRange(entMan.GetComponent<WFPlanetNetworkComponent>(built!.Value).Layers);
        });

        await server.WaitRunTicks(1);
        return layers;
    }

    /// <summary>
    /// Materialises a rectangle of ground so the footprint checks have solid tiles to find. SetTiles is deliberate:
    /// unlike BiomeSystem.ReserveTiles it leaves BiomeComponent.ModifiedTiles alone, so a reservation test can still
    /// tell what the anchor pinned.
    /// </summary>
    private static async Task LayTiles(TestPair pair, EntityUid ground, Vector2i from, Vector2i to)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitPost(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(ground);
            var floor = new Tile(tileDefs[HullTile].TileId);
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();

            for (var x = from.X; x <= to.X; x++)
            for (var y = from.Y; y <= to.Y; y++)
            {
                tiles.Add((new Vector2i(x, y), floor));
            }

            maps.SetTiles(ground, grid, tiles);
        });

        await server.WaitRunTicks(1);
    }

    /// <summary>Tears a stack down through its own network entity.</summary>
    private static async Task Teardown(TestPair pair, List<EntityUid> layers)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var networks = server.System<WFPlanetNetworkSystem>();

        await server.WaitPost(() =>
        {
            if (entMan.TryGetComponent(layers[0], out Content.Shared._CE.ZLevels.Core.Components.CEZMapComponent? zMap) &&
                zMap.NetworkUid is { } network)
            {
                networks.DeleteNetwork(network);
            }
        });

        await server.WaitRunTicks(5);
    }

    /// <summary>Every entity parented straight to a grid; machine parts live inside their machine, not here.</summary>
    private static IEnumerable<EntityUid> Children(IEntityManager entMan, EntityUid grid)
    {
        var found = new List<EntityUid>();
        var query = entMan.AllEntityQueryEnumerator<TransformComponent>();

        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.ParentUid == grid)
                found.Add(uid);
        }

        return found;
    }

    /// <summary>Prototype id to count for everything sitting on a grid.</summary>
    private static Dictionary<string, int> Contents(IEntityManager entMan, EntityUid grid)
    {
        var counts = new Dictionary<string, int>();

        foreach (var uid in Children(entMan, grid))
        {
            if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID is not { } id)
                continue;

            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        return counts;
    }
}
