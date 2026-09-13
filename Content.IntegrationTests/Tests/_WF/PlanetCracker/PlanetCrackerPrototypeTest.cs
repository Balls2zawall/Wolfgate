#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Gravity;
using Content.IntegrationTests.Pair;
using Content.Server.Construction.Components;
using Content.Shared.Cargo.Components;
using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared.Repairable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

/// <summary>
/// Everything the planet cracker prototypes promise that only a loaded server can check: that each one indexes and
/// spawns, that every sprite layer names a state its RSI actually has, and the handful of numbers the C# mirrors.
/// </summary>
[TestFixture]
public sealed class PlanetCrackerPrototypeTest
{
    /// <summary>Every prototype this feature adds, in the order the plan lists them.</summary>
    private static readonly string[] Prototypes =
    {
        "WFGravityAnchor",
        "WFAnchorCrate",
        "WFAnchorCrateReplacement",
        "WFCentrifuge",
        "WFGravityProjector",
        "WFTransportGravgen",
        "WFCrackConsole",
        "WFSectorSurveyConsole",
        "WFChunkBerthMarker",
        "WFCentrifugeCircuitboard",
        "WFGravityProjectorCircuitboard",
    };

    /// <summary>The prototypes that carry a Repairable block design D9's welder loop has to work on.</summary>
    private static readonly string[] Repairables =
    {
        "WFGravityAnchor",
        "WFCentrifuge",
        "WFGravityProjector",
    };

    private const string Projector = "WFGravityProjector";
    private const string Centrifuge = "WFCentrifuge";
    private const string Crate = "WFAnchorCrate";
    private const string CrateReplacement = "WFAnchorCrateReplacement";
    private const string HullTile = "FloorSteel";

    /// <summary>Design section 5 / D17: the cargo-purchasable replacement crate.</summary>
    private const float ReplacementPrice = 400000f;

    [Test]
    public async Task EveryPrototypeSpawns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var spawned = await SpawnAll(pair);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var (id, uid) in spawned)
                {
                    Assert.That(entMan.EntityExists(uid), Is.True, $"{id} did not survive being spawned.");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID, Is.EqualTo(id),
                        $"{id} spawned as something else.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The regression guard on "the prototypes reference the placeholder RSIs verbatim". A state name that is not in
    /// the generated meta.json only shows up as a missing sprite at runtime.
    /// </summary>
    [Test]
    public async Task EverySpriteStateExists()
    {
        // Connected, because the sprite layers only exist on the client half.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var clientEntMan = client.EntMan;

        var spawned = await SpawnAll(pair);
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var (id, serverUid) in spawned)
                {
                    var uid = pair.ToClientUid(serverUid);

                    Assert.That(clientEntMan.EntityExists(uid), Is.True, $"{id} never reached the client.");

                    if (!clientEntMan.TryGetComponent(uid, out SpriteComponent? sprite))
                        continue;

                    var layer = 0;

                    foreach (var spriteLayer in sprite.AllLayers)
                    {
                        layer++;

                        if (spriteLayer.RsiState.Name is not { } state)
                            continue;

                        Assert.That(spriteLayer.ActualRsi, Is.Not.Null,
                            $"{id} layer {layer} names state '{state}' but resolves no RSI.");
                        Assert.That(spriteLayer.ActualRsi!.TryGetState(state, out _), Is.True,
                            $"{id} layer {layer} names state '{state}', which its RSI does not have.");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The client's gravity visualiser does an unconditional layer lookup for Core whenever the charge changes, so a
    /// centrifuge missing that layer throws on every client the moment it starts spinning up.
    /// </summary>
    [Test]
    public async Task CentrifugeMapsBothGravgenLayers()
    {
        // Connected, because the sprite layers only exist on the client half.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var clientEntMan = client.EntMan;

        var spawned = await SpawnAll(pair);
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var uid = pair.ToClientUid(spawned[Centrifuge]);
            var sprite = clientEntMan.GetComponent<SpriteComponent>(uid);
            var sprites = clientEntMan.System<SpriteSystem>();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sprites.LayerMapTryGet((uid, sprite), GravityGeneratorVisualLayers.Base, out _, false), Is.True,
                    "The centrifuge does not map the gravity generator's Base layer.");
                Assert.That(sprites.LayerMapTryGet((uid, sprite), GravityGeneratorVisualLayers.Core, out _, false), Is.True,
                    "The centrifuge does not map the gravity generator's Core layer, which the client looks up unconditionally.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// BaseStructure narrows the Repairable default to Applicating only, so every one of these has to spell the welder
    /// out again or design D9's repair loop silently does nothing.
    /// </summary>
    [Test]
    public async Task EveryRepairableAcceptsAWelder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var spawned = await SpawnAll(pair);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var id in Repairables)
                {
                    Assert.That(entMan.TryGetComponent(spawned[id], out RepairableComponent? repairable), Is.True,
                        $"{id} has no Repairable block at all.");
                    Assert.That(repairable!.Qualities, Does.Contain("Welding"),
                        $"{id} cannot be repaired with a welder; it kept BaseStructure's Applicating-only narrowing.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>A spawned machine is stocked with tier-one parts at map init, which is the multiplier's 1.0 baseline.</summary>
    [Test]
    public async Task ProjectorStartsAtTierOne()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var spawned = await SpawnAll(pair);

        await server.WaitAssertion(() =>
        {
            var uid = spawned[Projector];
            var containers = entMan.System<SharedContainerSystem>();

            Assert.That(containers.TryGetContainer(uid, MachineFrameComponent.PartContainerName, out var parts), Is.True,
                "The projector has no machine parts container.");
            Assert.That(parts!.ContainedEntities, Is.Not.Empty, "The projector was not stocked with any parts.");

            Assert.That(entMan.GetComponent<WFGravityProjectorComponent>(uid).CrackTimeMultiplier,
                Is.EqualTo(1f).Within(0.001f), "A tier-one projector does not sit at the full crack time.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The crates that ship with a hull must not inflate the appraisal the shipyard test measures; only the cargo
    /// replacement carries design section 5's price.
    /// </summary>
    [Test]
    public async Task ShippedCratesAreFree()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var spawned = await SpawnAll(pair);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Price(entMan, spawned[Crate]), Is.EqualTo(0f).Within(0.001f),
                    "The shipped anchor crate is not free.");
                Assert.That(Price(entMan, spawned[CrateReplacement]), Is.EqualTo(ReplacementPrice).Within(0.001f),
                    "The replacement anchor crate is not priced at the design's 400000.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Spawns one of every new prototype, each on its own tile of a throwaway grid.</summary>
    private static async Task<Dictionary<string, EntityUid>> SpawnAll(TestPair pair)
    {
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();

        var map = await pair.CreateTestMap();
        var spawned = new Dictionary<string, EntityUid>();

        await server.WaitPost(() =>
        {
            var grid = map.Grid;
            var floor = new Tile(tileDefs[HullTile].TileId);
            var tiles = new List<(Vector2i GridIndices, Tile Tile)>();

            // Three tiles apart, so nothing lands inside a neighbour's footprint or snap cell.
            for (var x = 0; x < Prototypes.Length * 3; x++)
            for (var y = 0; y < 3; y++)
            {
                tiles.Add((new Vector2i(x, y), floor));
            }

            maps.SetTiles(grid.Owner, grid.Comp, tiles);

            for (var i = 0; i < Prototypes.Length; i++)
            {
                var coords = new EntityCoordinates(grid.Owner, new Vector2(i * 3 + 1.5f, 1.5f));
                spawned[Prototypes[i]] = entMan.SpawnEntity(Prototypes[i], coords);
            }
        });

        await server.WaitRunTicks(5);
        return spawned;
    }

    /// <summary>The entity's static price, or zero when it carries none.</summary>
    private static float Price(IEntityManager entMan, EntityUid uid)
    {
        return entMan.TryGetComponent(uid, out StaticPriceComponent? price) ? (float)price.Price : 0f;
    }
}
