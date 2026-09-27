#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Shared.Gravity;
using Content.Shared.Humanoid.Prototypes;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.Wolfmed.Range;

/// <summary>
/// Builds the Wolfmed range (playtest 4): one walled, lit room with every playable species lined up, the medical
/// supplies, two powered pods, and a row of guns, melee and armour. Run by hand with WOLFMED_RANGE_OUT set to the
/// file to write, then commit the file at <see cref="WolfmedRangeMap.Path"/>. The species come from the species
/// prototypes, so a new species only needs a rerun. In game: `wolfmedrange`.
/// </summary>
[TestFixture]
[Explicit("Regenerates the Wolfmed range map; set WOLFMED_RANGE_OUT to the file to write.")]
public sealed class WolfmedRangeMapGenerator
{
    /// <summary>Tiles a side, walls included; 32 floor tiles across.</summary>
    private const int Size = 34;

    private const int PerRow = 16;

    private static readonly string[] Medical =
    [
        "MedkitFilled", "MedkitBruteFilled", "MedkitBurnFilled", "MedkitToxinFilled", "MedkitAdvancedFilled",
        "MedkitCombatFilled", "HandheldHealthAnalyzer", "HandheldHealthAnalyzer", "Defibrillator", "Bloodpack",
        "Bloodpack", "Bloodpack", "Tourniquet", "Tourniquet", "WFWolfmedSplint", "WFWolfmedSplint", "Gauze", "Gauze",
        "Brutepack", "Brutepack", "Ointment", "Ointment", "MedicatedSuture", "RegenerativeMesh", "SyringeEphedrine",
        "WFWolfmedHydraulicFluidPack", "WFWolfmedHydraulicFluidPack", "Welder", "CableApcStack", "BodyBag",
        "Scalpel", "Retractor", "Hemostat", "Cautery", "Saw", "Drill", "BoneGel",
    ];

    private static readonly string[] Arms =
    [
        "WeaponRifleAk", "BoxMagazine762x39mmFMJ", "WeaponPistolMk58", "BoxMagazine45_ACPFMJ",
        "WeaponShotgunKammerer", "WeaponLaserCarbine", "WeaponLaserCarbine", "WeaponSniperMosin",
        "CombatKnife", "Machete", "FireAxe", "Spear", "EnergySword", "Crowbar",
    ];

    private static readonly string[] Armour =
    [
        "ClothingOuterArmorBasic", "ClothingHeadHelmetBasic", "ClothingOuterArmorRiot", "ClothingHeadHelmetRiot",
        "ClothingOuterArmorHeavy", "ClothingOuterArmorBulletproof", "ClothingHeadHelmetSwat",
        "ClothingOuterHardsuitBasic", "ClothingHeadHelmetHardsuitBasic", "ClothingOuterHardsuitSecurity",
        "ClothingHeadHelmetHardsuitSecurity",
    ];

    [Test]
    public async Task GenerateTest()
    {
        var outPath = Environment.GetEnvironmentVariable("WOLFMED_RANGE_OUT");
        Assert.That(outPath, Is.Not.Null.And.Not.Empty, "set WOLFMED_RANGE_OUT to the file to write.");

        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var protos = server.ResolveDependency<IPrototypeManager>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var mapMan = server.ResolveDependency<IMapManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var skipped = new List<string>();
        var saved = new ResPath("/wolfmed_range.yml");

        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out var mapId, runMapInit: false);
            var grid = mapMan.CreateGridEntity(mapId);
            var floor = new Tile(tileDefs["FloorSteel"].TileId);
            for (var x = 0; x < Size; x++)
            for (var y = 0; y < Size; y++)
                mapSys.SetTile(grid, new Vector2i(x, y), floor);

            EntityUid? Spawn(string id, int x, int y, Angle rotation = default)
            {
                if (!protos.TryIndex<EntityPrototype>(id, out var proto) || proto.Categories.Any(c => c.ID == "DoNotMap"))
                {
                    skipped.Add(id);
                    return null;
                }

                var uid = entMan.SpawnEntity(id, new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f)));
                if (rotation != Angle.Zero)
                    transform.SetLocalRotation(entMan.GetComponent<TransformComponent>(uid), rotation);
                return uid;
            }

            for (var i = 0; i < Size; i++)
            {
                Spawn("WallSolid", i, 0);
                Spawn("WallSolid", i, Size - 1);
                Spawn("WallSolid", 0, i);
                Spawn("WallSolid", Size - 1, i);
            }

            // Wall lights every six tiles, each turned to face into the room.
            for (var i = 4; i < Size - 1; i += 6)
            {
                Spawn("AlwaysPoweredWallLight", i, Size - 1);
                Spawn("AlwaysPoweredWallLight", i, 0, Angle.FromDegrees(180));
                Spawn("AlwaysPoweredWallLight", 0, i, Angle.FromDegrees(-90));
                Spawn("AlwaysPoweredWallLight", Size - 1, i, Angle.FromDegrees(90));
            }

            var gravity = entMan.EnsureComponent<GravityComponent>(grid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            entMan.EnsureComponent<MapLightComponent>(mapUid).AmbientLightColor = Color.FromHex("#303030");
            entMan.System<AtmosphereSystem>().SetMapAtmosphere(mapUid, false, Scenarios.WolfmedScenario.Air());

            // Every species with a humanoid player mob, in rows along the north wall. Player mobs are save: false, so
            // the map carries a marker that spawns the species on map init.
            var species = protos.EnumeratePrototypes<SpeciesPrototype>()
                .Where(s => protos.TryIndex<EntityPrototype>(s.Prototype, out var p) && p.Components.ContainsKey("HumanoidAppearance"))
                .OrderBy(s => s.ID, StringComparer.Ordinal)
                .ToList();
            for (var i = 0; i < species.Count; i++)
            {
                var marker = Spawn("WFWolfmedRangeSpawner", 2 + i % PerRow * 2, Size - 4 - i / PerRow * 2);
                if (marker != null)
                    entMan.GetComponent<RandomSpawnerComponent>(marker.Value).Prototypes = [species[i].Prototype];
            }

            // Power: an RTG into a substation into an APC, low-voltage cable under the pods.
            Spawn("CableHV", 5, 4);
            Spawn("CableHV", 6, 4);
            Spawn("GeneratorRTG", 5, 4);
            Spawn("CableMV", 6, 4);
            Spawn("CableMV", 7, 4);
            Spawn("SubstationBasic", 6, 4);
            Spawn("APCBasic", 7, 4);
            for (var y = 4; y <= 8; y++)
                Spawn("CableApcExtension", 7, y);
            for (var x = 8; x <= 10; x++)
                Spawn("CableApcExtension", x, 8);
            Spawn("WFMachineAutodoc", 7, 8);
            Spawn("WFMachineAutodoc", 10, 8);
            Spawn("MedicalBed", 5, 14);
            Spawn("MedicalBed", 5, 16);
            Spawn("OperatingTable", 5, 18);
            Spawn("WFCrateWolfmedDebug", 2, 15);
            Spawn("WFCrateWolfmedDebug", 2, 16);

            // Supplies on tables along the west wall, four to a table.
            for (var i = 0; i < Medical.Length; i++)
            {
                var y = 4 + i / 4;
                if (i % 4 == 0)
                    Spawn("Table", 2, y);
                Spawn(Medical[i], 2, y);
            }

            // Guns and melee on tables along the south wall, armour along the east wall.
            for (var i = 0; i < Arms.Length; i++)
            {
                Spawn("Table", 6 + i * 2, 2);
                Spawn(Arms[i], 6 + i * 2, 2);
            }

            for (var i = 0; i < Armour.Length; i++)
            {
                Spawn("Table", Size - 3, 4 + i * 2);
                Spawn(Armour[i], Size - 3, 4 + i * 2);
            }

            Assert.That(loader.TrySaveMap(mapId, saved), "the map did not save.");
        });

        string yaml;
        await using (var stream = server.ResolveDependency<IResourceManager>().UserData.Open(saved, FileMode.Open))
        using (var reader = new StreamReader(stream))
            yaml = await reader.ReadToEndAsync();

        File.WriteAllText(outPath!, yaml);
        TestContext.Out.WriteLine($"wrote {outPath} ({yaml.Length} chars); skipped: {(skipped.Count == 0 ? "none" : string.Join(", ", skipped))}");
        await pair.CleanReturnAsync();
    }
}
