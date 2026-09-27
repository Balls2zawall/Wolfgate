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
/// Builds the Wolfmed range (playtest 4): one walled room with every playable species lined up, the medical supplies,
/// two powered pods, and a row of guns, melee and armour. Run by hand with WOLFMED_RANGE_OUT set to the file to
/// write, then commit the file at <see cref="WolfmedRangeMap.Path"/>. The species come from the species prototypes,
/// so a new species only needs a rerun.
/// </summary>
[TestFixture]
[Explicit("Regenerates the Wolfmed range map; set WOLFMED_RANGE_OUT to the file to write.")]
public sealed class WolfmedRangeMapGenerator
{
    private const int Size = 48;

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

            EntityUid? Spawn(string id, int x, int y)
            {
                if (!protos.TryIndex<EntityPrototype>(id, out var proto) || proto.Categories.Any(c => c.ID == "DoNotMap"))
                {
                    skipped.Add(id);
                    return null;
                }

                return entMan.SpawnEntity(id, new EntityCoordinates(grid, new Vector2(x + 0.5f, y + 0.5f)));
            }

            for (var i = 0; i < Size; i++)
            {
                Spawn("WallSolid", i, 0);
                Spawn("WallSolid", i, Size - 1);
                Spawn("WallSolid", 0, i);
                Spawn("WallSolid", Size - 1, i);
            }

            var gravity = entMan.EnsureComponent<GravityComponent>(grid);
            gravity.Enabled = true;
            gravity.Inherent = true;
            entMan.EnsureComponent<MapLightComponent>(mapUid).AmbientLightColor = Color.White;
            entMan.System<AtmosphereSystem>().SetMapAtmosphere(mapUid, false, Scenarios.WolfmedScenario.Air());

            // Every species with a humanoid player mob, in rows along the north wall.
            var species = protos.EnumeratePrototypes<SpeciesPrototype>()
                .Where(s => protos.TryIndex<EntityPrototype>(s.Prototype, out var p) && p.Components.ContainsKey("HumanoidAppearance"))
                .OrderBy(s => s.ID, StringComparer.Ordinal)
                .ToList();
            for (var i = 0; i < species.Count; i++)
            {
                // Player mobs are save: false, so the map carries a marker that spawns the species on map init.
                var marker = Spawn("WFWolfmedRangeSpawner", 3 + i % 22 * 2, Size - 4 - i / 22 * 3);
                if (marker != null)
                    entMan.GetComponent<RandomSpawnerComponent>(marker.Value).Prototypes = [species[i].Prototype];
            }

            // Power: an RTG into a substation into an APC, low-voltage cable to the pods.
            Spawn("CableHV", 6, 6);
            Spawn("CableHV", 7, 6);
            Spawn("GeneratorRTG", 6, 6);
            Spawn("CableMV", 7, 6);
            Spawn("CableMV", 8, 6);
            Spawn("SubstationBasic", 7, 6);
            Spawn("APCBasic", 8, 6);
            for (var y = 6; y <= 10; y++)
                Spawn("CableApcExtension", 8, y);
            for (var x = 9; x <= 11; x++)
                Spawn("CableApcExtension", x, 10);
            Spawn("WFMachineAutodoc", 8, 10);
            Spawn("WFMachineAutodoc", 11, 10);
            Spawn("MedicalBed", 6, 12);
            Spawn("MedicalBed", 6, 14);
            Spawn("OperatingTable", 6, 16);
            Spawn("WFCrateWolfmedDebug", 5, 18);
            Spawn("WFCrateWolfmedDebug", 5, 19);

            // Supplies on tables along the west wall, four to a table.
            for (var i = 0; i < Medical.Length; i++)
            {
                var y = 6 + i / 4;
                if (i % 4 == 0)
                    Spawn("Table", 3, y);
                Spawn(Medical[i], 3, y);
            }

            // Guns and melee on tables along the south wall, armour along the east wall.
            for (var i = 0; i < Arms.Length; i++)
            {
                Spawn("Table", 12 + i * 2, 3);
                Spawn(Arms[i], 12 + i * 2, 3);
            }

            for (var i = 0; i < Armour.Length; i++)
            {
                Spawn("Table", Size - 4, 6 + i * 2);
                Spawn(Armour[i], Size - 4, 6 + i * 2);
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
