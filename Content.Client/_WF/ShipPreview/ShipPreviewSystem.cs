using System.Numerics;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Client._WF.ShipPreview;

/// <summary>
/// A grid loaded into the preview map, with the numbers a previewer wants to show next to it.
/// </summary>
public readonly record struct ShipPreviewGrid(
    Entity<MapGridComponent> Grid,
    Box2 LocalBounds,
    Vector2i SizeInTiles,
    int TileCount);

/// <summary>
/// Loads ship grids onto a single client-side preview map so UI can render them without touching the server or the
/// player's own view. The map is created on first use and deleted once the last previewer releases it.
/// </summary>
/// <remarks>
/// The map is created without map-init and stays paused, so spawners, atmos and timers on the loaded grid never run.
/// Client map ids are negative, so the map can never collide with a server-allocated one arriving in a game state.
/// </remarks>
public sealed partial class ShipPreviewSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private MapId _mapId = MapId.Nullspace;
    private EntityUid _mapUid = EntityUid.Invalid;
    private int _users;

    private ResPath? _loadedPath;
    private ShipPreviewGrid? _loaded;

    /// <summary>
    /// The preview map, or nullspace while no previewer holds it.
    /// </summary>
    public MapId PreviewMap => _mapId;

    /// <summary>
    /// The currently previewed grid, if one is loaded and still alive.
    /// </summary>
    public ShipPreviewGrid? Current => _loaded is { } loaded && Exists(loaded.Grid.Owner) ? loaded : null;

    public override void Shutdown()
    {
        base.Shutdown();

        _users = 0;
        DestroyMap();
    }

    /// <summary>
    /// Takes a reference on the preview map. Every call must be matched by a <see cref="Release"/>.
    /// </summary>
    public void Acquire()
    {
        _users++;
    }

    /// <summary>
    /// Drops a reference taken by <see cref="Acquire"/>, deleting the map and everything on it when the last one goes.
    /// </summary>
    public void Release()
    {
        _users--;
        if (_users > 0)
            return;

        _users = 0;
        DestroyMap();
    }

    /// <summary>
    /// Loads a vessel's grid, replacing whatever was previewed before. Loading the vessel that is already shown
    /// does nothing.
    /// </summary>
    public bool TryLoad(VesselPrototype vessel, out ShipPreviewGrid preview)
    {
        return TryLoad(vessel.ShuttlePath, vessel.Name, out preview);
    }

    /// <summary>
    /// Loads a grid file, replacing whatever was previewed before. Loading the file that is already shown does nothing.
    /// </summary>
    public bool TryLoad(ResPath path, string? name, out ShipPreviewGrid preview)
    {
        preview = default;

        if (Current is { } current && _loadedPath == path)
        {
            preview = current;
            return true;
        }

        if (!EnsureMap())
            return false;

        Clear();

        Entity<MapGridComponent>? grid;
        try
        {
            if (!_loader.TryLoadGrid(_mapId, path, out grid, new DeserializationOptions
                {
                    InitializeMaps = false,
                    PauseMaps = true,
                }))
            {
                Log.Warning($"Ship preview failed to load grid {path}");
                return false;
            }
        }
        catch (Exception e)
        {
            // TryLoadGrid rethrows deserialization failures after cleaning up its own entities.
            Log.Error($"Ship preview threw while loading grid {path}: {e}");
            return false;
        }

        // Park it at the origin unrotated so the previewer can work in plain grid-local coordinates.
        _xform.SetLocalPositionRotation(grid.Value.Owner, Vector2.Zero, Angle.Zero);

        if (!string.IsNullOrEmpty(name))
            _meta.SetEntityName(grid.Value.Owner, name);

        var bounds = grid.Value.Comp.LocalAABB;
        var tileSize = MathF.Max(grid.Value.Comp.TileSize, 1f);
        var size = new Vector2i(
            (int) MathF.Round(bounds.Width / tileSize),
            (int) MathF.Round(bounds.Height / tileSize));

        // Counted once here rather than per frame; the grid never changes while it is previewed.
        var tiles = 0;
        var enumerator = _map.GetAllTilesEnumerator(grid.Value.Owner, grid.Value.Comp);
        while (enumerator.MoveNext(out _))
        {
            tiles++;
        }

        preview = new ShipPreviewGrid(grid.Value, bounds, size, tiles);
        _loaded = preview;
        _loadedPath = path;
        return true;
    }

    /// <summary>
    /// Deletes the previewed grid, leaving the map in place for the next load.
    /// </summary>
    public void Clear()
    {
        if (_loaded is { } loaded && Exists(loaded.Grid.Owner))
            Del(loaded.Grid.Owner);

        _loaded = null;
        _loadedPath = null;
    }

    /// <summary>
    /// Creates the preview map if it is missing. Also recovers from an entity flush (disconnect, round restart)
    /// silently taking the old map away.
    /// </summary>
    private bool EnsureMap()
    {
        if (_mapId != MapId.Nullspace
            && Exists(_mapUid)
            && _map.TryGetMap(_mapId, out var existing)
            && existing == _mapUid)
        {
            return true;
        }

        // Stale ids after a flush; a new map may well have taken the old id.
        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
        _loaded = null;
        _loadedPath = null;

        try
        {
            _mapUid = _map.CreateMap(out var mapId, runMapInit: false);
            _mapId = mapId;
        }
        catch (Exception e)
        {
            Log.Error($"Ship preview failed to create its map: {e}");
            _mapUid = EntityUid.Invalid;
            _mapId = MapId.Nullspace;
            return false;
        }

        _meta.SetEntityName(_mapUid, "Wolfgate ship preview");
        return true;
    }

    private void DestroyMap()
    {
        _loaded = null;
        _loadedPath = null;

        if (_mapId != MapId.Nullspace && Exists(_mapUid) && _map.TryGetMap(_mapId, out var existing) && existing == _mapUid)
            Del(_mapUid);

        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
    }
}
