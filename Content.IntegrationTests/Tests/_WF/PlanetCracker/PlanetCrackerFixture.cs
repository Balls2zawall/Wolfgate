#nullable enable
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Pair;
using Content.Server._WF.PlanetCracker.Anchors;
using Content.Server._WF.PlanetCracker.Cracker;
using Content.Server._WF.PlanetCracker.Planets;
using Content.Server._WF.PlanetCracker.Testing;
using Content.Server.Power.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.PlanetCracker.Anchors;
using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared._WF.PlanetCracker.Cracker.BUI;
using Content.Shared._WF.PlanetCracker.Planets;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

/// <summary>
/// The scaffolding every planet cracker fixture shares, lifted out of the three that had grown their own verbatim
/// copies. Every fixture in this namespace pulls it in with <c>using static</c>, so the call sites read the same as
/// the private helpers they replaced.
/// PlanetNetworkTest keeps its own BuildStandalone and Teardown: they wrap the layers in a Stack record and tear down
/// through the network uid rather than the ground layer, so they are a different shape rather than a fourth copy.
/// </summary>
public static class PlanetCrackerFixture
{
    /// <summary>The crackable world every fixture builds its stack from.</summary>
    public const string SurfaceProto = "WFSurfaceAsclepiu";

    /// <summary>The deployable gravity anchor.</summary>
    public const string AnchorProto = "WFGravityAnchor";

    /// <summary>Deck plating, used for both hull decks and hand-laid ground.</summary>
    public const string FloorTile = "FloorSteel";

    /// <summary>Turns the feature on for this pair; TestPair reverts the change when the pair is returned.</summary>
    public static async Task EnableFeature(TestPair pair)
    {
        await pair.Server.WaitPost(() => pair.Server.CfgMan.SetCVar(PlanetCrackerCVars.PlanetNetworks, true));
    }

    /// <summary>Builds an unowned Asclepiu stack at the origin and returns its layers, ground first.</summary>
    public static async Task<List<EntityUid>> BuildStandalone(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var networks = server.System<WFPlanetNetworkSystem>();
        var layers = new List<EntityUid>();

        await server.WaitPost(() =>
        {
            var surface = proto.Index<WFPlanetSurfacePrototype>(SurfaceProto);
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
    /// tell what the anchor itself pinned.
    /// </summary>
    public static async Task LayTiles(TestPair pair, EntityUid ground, Vector2i from, Vector2i to)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitPost(() =>
        {
            var grid = entMan.GetComponent<MapGridComponent>(ground);
            var floor = new Tile(tileDefs[FloorTile].TileId);
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
    public static async Task Teardown(TestPair pair, List<EntityUid> layers)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var networks = server.System<WFPlanetNetworkSystem>();

        await server.WaitPost(() =>
        {
            if (entMan.TryGetComponent(layers[0], out CEZMapComponent? zMap) && zMap.NetworkUid is { } network)
                networks.DeleteNetwork(network);
        });

        await server.WaitRunTicks(5);
    }

    /// <summary>Every entity parented straight to a grid; machine parts live inside their machine, not here.</summary>
    public static IEnumerable<EntityUid> Children(IEntityManager entMan, EntityUid grid)
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
    public static Dictionary<string, int> Contents(IEntityManager entMan, EntityUid grid)
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

    /// <summary>Builds the tiny cracker through the factory and lets its fixtures settle.</summary>
    public static async Task<EntityUid> BuildCracker(TestPair pair, MapId map, Vector2? offset = null)
    {
        var server = pair.Server;
        var factory = server.System<WFTestGridFactory>();
        var grid = EntityUid.Invalid;

        await server.WaitPost(() => grid = factory.BuildCracker(map, offset ?? Vector2.Zero));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
        return grid;
    }

    /// <summary>Builds the micro transport through the factory and lets its fixtures settle.</summary>
    public static async Task<EntityUid> BuildTransport(TestPair pair, MapId map, Vector2? offset = null)
    {
        var server = pair.Server;
        var factory = server.System<WFTestGridFactory>();
        var grid = EntityUid.Invalid;

        await server.WaitPost(() => grid = factory.BuildTransport(map, offset ?? new Vector2(100f, 0f)));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
        return grid;
    }

    /// <summary>
    /// Map-initialises a code-built hull.
    /// MapManager's grid creation deliberately leaves a new grid un-map-initialised even on a live map, and adding a
    /// component only re-raises MapInitEvent on an entity that IS map-initialised. Without this the crack's
    /// AddComp&lt;ForceAnchorComponent&gt; never reaches ForceAnchorSystem's handler, so the hull would never actually
    /// go static and every lock assertion would be testing nothing.
    /// </summary>
    public static async Task MapInitHull(TestPair pair, EntityUid grid)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitPost(() => entMan.RunMapInit(grid, entMan.GetComponent<MetaDataComponent>(grid)));
        await server.WaitRunTicks(1);
    }

    /// <summary>
    /// Winds every PowerCharge machine on a hull up to full so its gravity generator activates. The shipped charge
    /// rates are 100 s for the mini gravgen and 240 s for the centrifuge, which no integration test can tick through;
    /// PowerChargeComponent is [Access(typeof(PowerChargeSystem))], so the rate is raised by reflection and the
    /// machine still has to charge, activate and light the grid on its own.
    /// </summary>
    public static async Task Energise(TestPair pair, EntityUid grid)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitPost(() =>
        {
            foreach (var uid in Children(entMan, grid))
            {
                if (entMan.TryGetComponent(uid, out PowerChargeComponent? charge))
                    ChargeRateProperty.SetValue(charge, 10f);
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(2f));
    }

    /// <summary>
    /// Pins one charging machine's ramp so a parked charge stays parked. Without it the shipped rate keeps creeping the
    /// charge back up under every threshold assertion.
    /// </summary>
    public static async Task FreezeCharge(TestPair pair, EntityUid uid)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitPost(() => ChargeRateProperty.SetValue(entMan.GetComponent<PowerChargeComponent>(uid), 0f));
        await server.WaitRunTicks(1);
    }

    /// <summary>
    /// Parks one charging machine at an exact charge and lets the centrifuge sweep read it. PowerChargeComponent is
    /// [Access(typeof(PowerChargeSystem))] and the shipped ramp is 240 s, so the level is set by reflection the same
    /// way Energise sets the rate; a full second covers the sweep's own 0.25 s gate.
    /// </summary>
    public static async Task SetCharge(TestPair pair, EntityUid uid, float charge)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        await server.WaitPost(() => ChargeProperty.SetValue(entMan.GetComponent<PowerChargeComponent>(uid), charge));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
    }

    /// <summary>Builds a full crack site: an Asclepiu stack, laid ground, and a surveying cracker hull in orbit.</summary>
    public static async Task<CrackerSite> BuildCrackerInOrbit(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;

        await EnableFeature(pair);

        var site = new CrackerSite { Layers = await BuildStandalone(pair) };

        await LayTiles(pair, site.Ground, new Vector2i(-8, -8), new Vector2i(32, 8));

        var orbitMap = MapId.Nullspace;

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<WFOrbitLayerComponent>(site.Orbit), Is.True,
                "Precondition: the top layer of the stack is the orbit layer.");

            orbitMap = entMan.GetComponent<MapComponent>(site.Orbit).MapId;
        });

        // The hull has to sit on the orbit layer: that is the survey edge, and it is what the fall has to leave.
        site.Cracker = await BuildCracker(pair, orbitMap);
        await MapInitHull(pair, site.Cracker);

        // Two seconds covers the cracker sweep's 1 Hz gate, which is what walks Idle to Surveying.
        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            site.Console = FindConsole(entMan, site.Cracker);
            site.Centrifuge = FindCentrifuge(entMan, site.Cracker);
            site.Projectors = FindProjectors(entMan, site.Cracker);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(site.Console, Is.Not.EqualTo(EntityUid.Invalid), "The hull carries no crack console.");
                Assert.That(site.Centrifuge, Is.Not.EqualTo(EntityUid.Invalid), "The hull carries no centrifuge.");
                Assert.That(site.Projectors, Has.Count.EqualTo(2), "The hull does not carry its two projectors.");
                Assert.That(entMan.GetComponent<WFPlanetCrackerComponent>(site.Cracker).State,
                    Is.EqualTo(WFCrackState.Surveying), "A hull parked on the orbit layer should be surveying.");
            }
        });

        return site;
    }

    /// <summary>
    /// Spawns an owned anchor pair on the ground layer, wrenches both down and optionally finishes both drills, which
    /// is what walks the hull Surveying to AnchorsPlaced to AnchorsLocked.
    /// </summary>
    public static async Task DeployPair(TestPair pair, CrackerSite site, float ax, float bx, bool drill)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var transform = server.System<SharedTransformSystem>();
        var anchors = server.System<WFGravityAnchorSystem>();

        await server.WaitPost(() =>
        {
            // Hand-spawned anchors belong to nobody, and an unowned pair is invisible to the hull's state machine.
            foreach (var x in new[] { ax, bx })
            {
                var anchor = entMan.SpawnEntity(AnchorProto, new EntityCoordinates(site.Ground, new Vector2(x + 0.5f, 0.5f)));
                entMan.GetComponent<WFGravityAnchorComponent>(anchor).Cracker = entMan.GetNetEntity(site.Cracker);
                site.Anchors.Add(anchor);
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(1f));

        await server.WaitPost(() =>
        {
            foreach (var anchor in site.Anchors)
            {
                transform.AnchorEntity(anchor);
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(2f));

        if (!drill)
            return;

        // The shipped drill is five minutes; the admin path finishes it with the same lock, thunk and event.
        await server.WaitPost(() =>
        {
            foreach (var anchor in site.Anchors)
            {
                anchors.CompleteDrill((anchor, entMan.GetComponent<WFGravityAnchorComponent>(anchor)));
            }
        });

        await server.WaitRunTicks(pair.SecondsToTicks(1f));
    }

    /// <summary>
    /// A hull in orbit with a drilled, owned pair, flown onto the cut circle and a rotor wound to full: everything a
    /// begin-crack needs, with nothing blocking.
    /// </summary>
    public static async Task<CrackerSite> BuildReadyToCut(TestPair pair, Vector2? offset = null)
    {
        var site = await BuildCrackerInOrbit(pair);
        await DeployPair(pair, site, 0f, 24f, true);
        await AlignHull(pair, site, offset ?? Vector2.Zero);
        await Energise(pair, site.Cracker);
        return site;
    }

    /// <summary>Targets the hull's pair and begins the cut, failing loudly on either refusal.</summary>
    public static async Task BeginCut(TestPair pair, CrackerSite site)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var crackers = server.System<WFCrackerSystem>();

        await server.WaitPost(() =>
        {
            var cracker = (site.Cracker, entMan.GetComponent<WFPlanetCrackerComponent>(site.Cracker));

            Assert.That(crackers.TryTarget(cracker, out var targeting), Is.True,
                $"Precondition: the pair targets: {targeting}");
            Assert.That(crackers.TryBegin(cracker, out var reason), Is.True,
                $"Precondition: the cut begins: {reason}");
        });

        await server.WaitRunTicks(pair.SecondsToTicks(1f));
    }

    /// <summary>Parks a hull at a world position on its current map, the way a pilot would fly it there.</summary>
    public static async Task MoveHullTo(TestPair pair, EntityUid hull, Vector2 position)
    {
        var server = pair.Server;
        var transform = server.System<SharedTransformSystem>();

        await server.WaitPost(() => transform.SetWorldPosition(hull, position));
        await server.WaitRunTicks(pair.SecondsToTicks(1f));
    }

    /// <summary>
    /// Flies the hull so its berth centre lands exactly the given raw XY delta short of the cut circle centre, which is
    /// what TryGetBerthOffset then reports. The berth centre is read back off the marker rather than assumed, so the
    /// hull layout can change without every alignment test moving with it.
    /// </summary>
    public static async Task AlignHull(TestPair pair, CrackerSite site, Vector2 offset)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var crackers = server.System<WFCrackerSystem>();
        var transform = server.System<SharedTransformSystem>();

        await server.WaitPost(() =>
        {
            var cracker = (site.Cracker, entMan.GetComponent<WFPlanetCrackerComponent>(site.Cracker));

            Assert.That(crackers.TryGetBerthCentre(cracker, out var centre), Is.True,
                "Precondition: the hull resolved its berth centre.");
            Assert.That(crackers.TryGetOwnedPair(cracker, out var a, out var b, false), Is.True,
                "Precondition: the hull owns a pair to aim at.");
            Assert.That(crackers.TryGetCircle(a.Owner, b.Owner, out var circle, out _), Is.True,
                "Precondition: the pair has a cut circle.");

            // The berth sits at a fixed grid-local place, so the hull pose that puts it where we want is arithmetic.
            var local = centre.Position - transform.GetWorldPosition(site.Cracker);
            transform.SetWorldPosition(site.Cracker, circle - offset - local);
        });

        await server.WaitRunTicks(pair.SecondsToTicks(1f));
    }

    /// <summary>
    /// The exact state object a client would receive, built server-side with no window standing up.
    /// Must be called from inside a server thread callback.
    /// </summary>
    public static WFCrackConsoleState ReadConsoleState(TestPair pair, CrackerSite site)
    {
        return pair.Server.System<WFCrackConsoleSystem>().BuildState(site.Console);
    }

    /// <summary>The crack console resting on a hull, or Invalid.</summary>
    public static EntityUid FindConsole(IEntityManager entMan, EntityUid cracker)
    {
        foreach (var uid in Children(entMan, cracker))
        {
            if (entMan.HasComponent<WFCrackConsoleComponent>(uid))
                return uid;
        }

        return EntityUid.Invalid;
    }

    /// <summary>The centrifuge resting on a hull, or Invalid.</summary>
    public static EntityUid FindCentrifuge(IEntityManager entMan, EntityUid cracker)
    {
        foreach (var uid in Children(entMan, cracker))
        {
            if (entMan.HasComponent<WFCentrifugeComponent>(uid))
                return uid;
        }

        return EntityUid.Invalid;
    }

    /// <summary>Every gravity projector resting on a hull, ordered by grid-local X the way the console rows are.</summary>
    public static List<EntityUid> FindProjectors(IEntityManager entMan, EntityUid cracker)
    {
        var found = new List<EntityUid>();

        foreach (var uid in Children(entMan, cracker))
        {
            if (entMan.HasComponent<WFGravityProjectorComponent>(uid))
                found.Add(uid);
        }

        found.Sort((x, y) => entMan.GetComponent<TransformComponent>(x).LocalPosition.X
            .CompareTo(entMan.GetComponent<TransformComponent>(y).LocalPosition.X));

        return found;
    }

    /// <summary>PowerChargeComponent.ChargeRate, which is access-locked to its own system.</summary>
    private static readonly PropertyInfo ChargeRateProperty = Resolve("ChargeRate");

    /// <summary>PowerChargeComponent.Charge, which is access-locked to its own system.</summary>
    private static readonly PropertyInfo ChargeProperty = Resolve("Charge");

    /// <summary>One access-locked charge property, failing loudly here rather than as a null deref in a test.</summary>
    private static PropertyInfo Resolve(string name)
    {
        var property = typeof(PowerChargeComponent).GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(property, Is.Not.Null, $"PowerChargeComponent.{name} was not found.");
        return property!;
    }
}

/// <summary>One built crack site: the planet stack, the hull in orbit and everything on it the tests reach for.</summary>
public sealed class CrackerSite
{
    /// <summary>The stack's layers, ground first, orbit last.</summary>
    public List<EntityUid> Layers = new();

    /// <summary>The biome ground layer the anchors are wrenched down on.</summary>
    public EntityUid Ground => Layers[0];

    /// <summary>The vacuum orbit layer the hull parks on.</summary>
    public EntityUid Orbit => Layers[^1];

    /// <summary>The cracker hull grid.</summary>
    public EntityUid Cracker;

    /// <summary>The hull's crack control console.</summary>
    public EntityUid Console;

    /// <summary>The hull's gravitic centrifuge.</summary>
    public EntityUid Centrifuge;

    /// <summary>The hull's gravity projectors, ordered by grid-local X.</summary>
    public List<EntityUid> Projectors = new();

    /// <summary>Anchors deployed on the ground layer for this site.</summary>
    public List<EntityUid> Anchors = new();
}
