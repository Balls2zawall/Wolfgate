using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server._WF.PlanetCracker.Planets;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Content.Shared._WF.PlanetCracker.Planets;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.IntegrationTests.Tests._WF.PlanetCracker.PlanetCrackerFixture;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

[TestFixture]
public sealed class PlanetPopulationTest
{
    [Test]
    public async Task IdleWorldsStayUnloadedAndAmbientPopulationIsBounded()
    {
        await using var pair = await PoolManager.GetServerClient();
        await EnableFeature(pair);
        var server = pair.Server;
        var em = server.EntMan;
        var allLayers = new List<EntityUid>();
        var grounds = new List<EntityUid>();
        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var systemId = "SystemKyphrus";
            var system = proto.Index<StarSystemPrototype>(systemId);
            Assert.That(system.Planets.Exists(p => p.Planet.Id == "PlanetCarcinoma"), Is.True);
            var stopwatch = Stopwatch.StartNew();
            foreach (var name in new[] { "Asclepiu", "Fervidus", "Merak", "Aerumna", "Thrascias", "Carcinoma" })
            {
                var surface = proto.Index<WFPlanetSurfacePrototype>("WFSurface" + name);
                Assert.That(surface.Sanctioned, Is.EqualTo(name != "Carcinoma"));
                var network = server.System<WFPlanetNetworkSystem>().BuildNetwork(surface, Vector2.Zero, name, null);
                Assert.That(network, Is.Not.Null);
                var layers = em.GetComponent<WFPlanetNetworkComponent>(network!.Value).Layers;
                allLayers.AddRange(layers);
                grounds.Add(layers[0]);
                var biome = em.GetComponent<BiomeComponent>(layers[0]);
                Assert.That(biome.LoadedChunks, Is.Empty, "An unvisited planet generated terrain eagerly.");
                Assert.That(biome.LoadedEntities, Is.Empty, "An unvisited planet spawned scenery or wildlife eagerly.");
            }
            TestContext.Out.WriteLine($"Six empty planet networks: {allLayers.Count} maps, {stopwatch.ElapsedMilliseconds} ms construction, zero loaded terrain chunks.");
            Assert.That(allLayers, Has.Count.EqualTo(30));

            List<EntityUid> Animals(EntityUid? ground = null)
            {
                var result = new List<EntityUid>();
                var query = em.EntityQueryEnumerator<MobStateComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var xform))
                {
                    if (xform.MapUid is { } map && grounds.Contains(map) && (ground == null || map == ground))
                        result.Add(uid);
                }
                return result;
            }

            // Saturate each planet, then the global limit. No ticks/AI are needed to test admission.
            foreach (var ground in grounds)
            {
                for (var i = 0; i < WFPlanetFaunaSystem.MaxPerPlanet + 5; i++)
                    em.SpawnEntity("WFFaunaAsclepiu", new EntityCoordinates(ground, new Vector2(i * 3, 0)));
                Assert.That(Animals(ground).Count, Is.LessThanOrEqualTo(WFPlanetFaunaSystem.MaxPerPlanet));
            }
            var animals = Animals();
            Assert.That(animals, Has.Count.EqualTo(WFPlanetFaunaSystem.MaxTotal));

            // Moving wildlife off its birth planet must not open another global slot.
            server.System<SharedTransformSystem>().SetCoordinates(animals[0],
                new EntityCoordinates(grounds[^1], new Vector2(200, 0)));
            em.SpawnEntity("WFFaunaAsclepiu", new EntityCoordinates(grounds[0], new Vector2(201, 0)));
            Assert.That(Animals(), Has.Count.EqualTo(WFPlanetFaunaSystem.MaxTotal));

            // Deleted animals release budget; blocked attempts must not leak it.
            em.DeleteEntity(animals[0]);
            em.SpawnEntity("WFFaunaAsclepiu", new EntityCoordinates(grounds[0], new Vector2(204, 0)));
            Assert.That(Animals(), Has.Count.EqualTo(WFPlanetFaunaSystem.MaxTotal));

            var body = em.SpawnEntity(null, new EntityCoordinates(grounds[^1], Vector2.Zero));
            var carcinomaId = "WFSurfaceCarcinoma";
            var registered = server.System<WFPlanetRegistrySystem>().ApplySurface(body,
                proto.Index<WFPlanetSurfacePrototype>(carcinomaId));
            Assert.That(registered.Comp.Sanctioned, Is.False, "Survey and sanction notices read this flag.");
        });
        await Teardown(pair, allLayers);
        await pair.CleanReturnAsync();
    }
}