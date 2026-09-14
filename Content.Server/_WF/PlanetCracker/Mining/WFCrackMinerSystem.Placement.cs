using Content.Shared._WF.PlanetCracker.Chunk;
using Content.Shared._WF.PlanetCracker.Mining;
using Content.Shared._WF.PlanetCracker.Survey;
using Content.Shared.Construction.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._WF.PlanetCracker.Mining;

/// <summary>The placement gate - chunk grid plus a vein in the miner's own snap cell - and the three lookups it is built from.</summary>
public sealed partial class WFCrackMinerSystem
{
    /// <summary>Resolves the chunk grid an entity is standing on, or fails on a hull, a planet layer or a map.</summary>
    public bool TryGetChunk(
        TransformComponent xform,
        out Entity<WFPlanetChunkComponent> chunk,
        out Entity<MapGridComponent> grid)
    {
        chunk = default;
        grid = default;

        // This is the INVERSE of WFGravityAnchorSystem.TryGetPlanetGround, which demands xform.MapUid == grid because a
        // planet ground layer's map entity IS its grid. A chunk is a grid ON a map and never the map itself - which
        // CrackExtractionTest.TheChunkNeverCarriesPlanetLayer pins - so there is deliberately no MapUid test here.
        if (xform.GridUid is not { } gridUid)
            return false;

        if (!TryComp<WFPlanetChunkComponent>(gridUid, out var chunkComp))
            return false;

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return false;

        chunk = (gridUid, chunkComp);
        grid = (gridUid, mapGrid);
        return true;
    }

    /// <summary>The first deep vein anchored in one tile's snap cell, if there is one.</summary>
    public bool TryGetVeinAt(Entity<MapGridComponent> grid, Vector2i idx, out Entity<WFDeepVeinComponent> vein)
    {
        vein = default;

        // The lookup has to be tile-index based rather than an area query: F5 re-anchors a vein at the SAME tile index
        // on the chunk grid, and the vein carries no fixture at all, so only snap-cell membership finds it.
        var enumerator = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, idx);

        while (enumerator.MoveNext(out var other))
        {
            if (!TryComp<WFDeepVeinComponent>(other, out var comp))
                continue;

            vein = (other.Value, comp);
            return true;
        }

        return false;
    }

    /// <summary>The chunk and the vein under an entity's own tile; the whole of the miner's placement rule.</summary>
    /// <remarks>Public for the same reason TryGetPlanetGround is: the admin command and the tests both call it.</remarks>
    public bool TryGetVein(
        TransformComponent xform,
        out Entity<WFPlanetChunkComponent> chunk,
        out Entity<WFDeepVeinComponent> vein)
    {
        vein = default;

        if (!TryGetChunk(xform, out chunk, out var grid))
            return false;

        var idx = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);

        return TryGetVeinAt(grid, idx, out vein);
    }

    /// <summary>Refuses a wrench-down anywhere but a chunk tile with a seam under it.</summary>
    private void OnAnchorAttempt(Entity<WFCrackMinerComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var xform = Transform(ent.Owner);

        // AnchorAttemptEvent carries no reason field, so each popup has to come before its own cancel.
        if (!TryGetChunk(xform, out _, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("wf-crack-miner-not-chunk"), ent.Owner, args.User);
            args.Cancel();
            return;
        }

        var idx = _map.TileIndicesFor(grid.Owner, grid.Comp, xform.Coordinates);

        // There is deliberately no third "a miner is already here" refusal: AnchorableSystem.OnAnchorComplete already
        // calls TileFree and pops "anchorable-occupied", and TileFree tests CanCollide and Hard rather than BodyType, so
        // the dynamic body this prototype uses does not weaken it.
        if (!TryGetVeinAt(grid, idx, out _))
        {
            _popup.PopupEntity(Loc.GetString("wf-crack-miner-no-vein"), ent.Owner, args.User);
            args.Cancel();
        }
    }

    /// <summary>Anchor and unanchor alike: flush what was cut and re-read the state. It never unanchors anything.</summary>
    private void OnAnchorStateChanged(Entity<WFCrackMinerComponent> ent, ref AnchorStateChangedEvent args)
    {
        // Detaching means the entity is being sent to null-space as part of its own deletion, and spawning ore out of a
        // terminating machine is the same call already made for ComponentShutdown - the buffer is deliberately lost.
        // The in-content precedent for testing the flag is ArtifactAnchorTriggerSystem. Note that this is NOT what
        // happens when the chunk grid is deleted: EntityManager flags the whole subtree Terminating before detaching,
        // and DetachEntityInternal's anchored branch is gated on the GRID being at most MapInitialized, so a grid delete
        // raises no anchor event here at all.
        if (args.Detaching)
            return;

        if (args.Anchored)
            return;

        FlushOutput(ent, Transform(ent.Owner));

        if (ent.Comp.State != WFCrackMinerState.Broken)
            SetState(ent, WFCrackMinerState.Idle);

        // There is no post-hoc re-verify-and-Unanchor here, unlike WFGravityAnchorSystem's: that exists because the
        // anchor's nine-tile footprint is not what the engine validates and because its eight-second do-after gives the
        // world time to change. The miner's gate is exactly the one tile the engine registers, a vein cannot move and a
        // grid cannot stop carrying WFPlanetChunkComponent, so the Update gate is authoritative - and a mapper may leave
        // a miner anchored on a hull as scenery, where it simply refuses to mine.
    }
}
