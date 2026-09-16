using System.Collections.Generic;
using System.Numerics;
using Content.Server._WF.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Shuttles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._WF.Shuttle;

/// <summary>
/// The collision warning only fires for ships that are actually going to hit something at speed.
/// </summary>
public sealed class CollisionWarningTest
{
    [Test]
    public async Task ClosingShipIsWarnedAndCleared()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var physicsSystem = entManager.System<SharedPhysicsSystem>();
        var xformSystem = entManager.System<SharedTransformSystem>();
        var warningSystem = entManager.System<CollisionWarningSystem>();

        var hysteresis = cfg.GetCVar(CollisionWarningCVars.Hysteresis);

        await server.WaitAssertion(() =>
        {
            // Warnings clear the moment the threat is gone, so each step can be checked on its own.
            cfg.SetCVar(CollisionWarningCVars.Hysteresis, 0f);

            entManager.DeleteEntity(map.Grid);

            var ship = MakeGrid(entManager, mapManager, mapSystem, map.MapId);
            var obstacle = MakeGrid(entManager, mapManager, mapSystem, map.MapId);

            xformSystem.SetWorldPosition(obstacle, new Vector2(60f, 0f));

            entManager.EnsureComponent<ShuttleComponent>(ship);
            entManager.SpawnEntity("ComputerShuttle", new EntityCoordinates(ship, new Vector2(0.5f, 0.5f)));

            physicsSystem.SetBodyType(ship, BodyType.Dynamic);

            // Well inside the lookahead and far above the speed anything survives.
            physicsSystem.SetLinearVelocity(ship, new Vector2(30f, 0f));

            warningSystem.Sweep();

            Assert.That(entManager.TryGetComponent<CollisionWarningComponent>(ship, out var warning), Is.True,
                "A ship closing on another grid at speed should be warned.");
            Assert.That(warning!.Level, Is.EqualTo(CollisionWarningLevel.Imminent),
                "Contact under two seconds away should be the imminent stage.");
            Assert.That(warning.Threat, Is.EqualTo(obstacle), "The warning should name the grid in the way.");
            Assert.That(warning.ClosingSpeed, Is.EqualTo(30f).Within(0.1f));

            // Far enough out to be an advisory rather than an imminent hit.
            xformSystem.SetWorldPosition(obstacle, new Vector2(300f, 0f));
            warningSystem.Sweep();

            Assert.That(entManager.TryGetComponent(ship, out warning), Is.True,
                "Traffic inside the lookahead window should still warn.");
            Assert.That(warning!.Level, Is.EqualTo(CollisionWarningLevel.Advisory),
                "Contact ten seconds out should only be an advisory.");

            // Same course, but too slowly to hurt.
            physicsSystem.SetLinearVelocity(ship, new Vector2(2f, 0f));
            warningSystem.Sweep();

            Assert.That(entManager.HasComponent<CollisionWarningComponent>(ship), Is.False,
                "A gentle approach should not warn.");

            // Closing speed is back up, but the ship is pointed away from the obstacle.
            physicsSystem.SetLinearVelocity(ship, new Vector2(-30f, 0f));
            warningSystem.Sweep();

            Assert.That(entManager.HasComponent<CollisionWarningComponent>(ship), Is.False,
                "A ship opening the range should not warn.");

            cfg.SetCVar(CollisionWarningCVars.Hysteresis, hysteresis);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A bare four by four grid of plating.
    /// </summary>
    private static EntityUid MakeGrid(IEntityManager entManager, IMapManager mapManager, SharedMapSystem mapSystem, MapId mapId)
    {
        var grid = mapManager.CreateGridEntity(mapId);
        var tiles = new List<(Vector2i GridIndices, Tile Tile)>();

        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < 4; y++)
            {
                tiles.Add((new Vector2i(x, y), new Tile(1)));
            }
        }

        mapSystem.SetTiles(grid.Owner, grid.Comp, tiles);

        return grid.Owner;
    }
}
