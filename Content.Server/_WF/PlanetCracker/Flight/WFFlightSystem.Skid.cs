using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._WF.PlanetCracker.Flight;

/// <summary>
/// The ground-out of a hard landing. The scrape itself is CE's ground friction, which already stops a hull in finite
/// time; what this adds is the price of arriving fast - the leading edge grinds itself off, anything under the hull is
/// crushed, and the obstacles CE would have bounced off are flattened instead (CEZLevelsSystem.WFFlight.cs).
/// </summary>
public sealed partial class WFFlightSystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    /// <summary>Speed (m/s) under which the hull has stopped and the skid is over.</summary>
    public const float SkidStopSpeed = 0.5f;

    /// <summary>Speed (m/s) over which the leading edge is grinding itself off rather than just sliding.</summary>
    public const float SkidRamSpeed = 4f;

    /// <summary>Damage a leading-edge tile takes per second, per metre per second of hull speed.</summary>
    public const float SkidTileDamageRate = 3f;

    /// <summary>Accumulated damage at which a leading-edge tile is torn off the hull.</summary>
    public const float SkidTileThreshold = 40f;

    /// <summary>
    /// Damage every tile of the hull takes at touchdown, at the speed a free fall would have arrived at. Under
    /// <see cref="SkidTileThreshold"/> on its own: a hard landing rattles the whole hull but tears nothing off it.
    /// </summary>
    public const float ImpactTileDamage = 12f;

    /// <summary>
    /// Extra damage the leading-edge tiles take at touchdown when the hull came in with planar speed. With the
    /// hull-wide share this passes <see cref="SkidTileThreshold"/> on a fast hard landing and not on a gentle one, so
    /// the expected price of arriving badly is the machinery along the nose.
    /// </summary>
    public const float ImpactEdgeDamage = 45f;

    /// <summary>How wide the leading edge is, in tiles of projection behind the foremost one.</summary>
    private const float SkidEdgeDepth = 1.5f;

    /// <summary>Blast left where a leading-edge tile tears off; small, and silent so one skid is not a hundred bangs.</summary>
    private const float SkidTileIntensity = 2f;

    private const float SkidTileSlope = 2f;
    private const float SkidTileMaxIntensity = 2f;

    /// <summary>Speed (m/s) the hull loses for each tile it leaves behind.</summary>
    private const float SkidTileSpeedCost = 0.5f;

    /// <summary>How often the leading edge is chewed on; the footprint walk is not a per-tick job.</summary>
    private static readonly TimeSpan SkidBiteInterval = TimeSpan.FromSeconds(0.25);

    private readonly List<Vector2i> _skidEdge = new();

    /// <summary>Grinds every skidding hull down, and lets go of the ones that have come to rest.</summary>
    private void UpdateSkids(float frameTime)
    {
        _skidScan.Clear();

        var query = EntityQueryEnumerator<WFSkidComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _skidScan.Add(uid);
        }

        foreach (var grid in _skidScan)
        {
            if (TerminatingOrDeleted(grid) || !TryComp<WFSkidComponent>(grid, out var skid))
                continue;

            if (!TryComp<PhysicsComponent>(grid, out var body) || !TryComp<MapGridComponent>(grid, out var gridComp))
            {
                EndSkid(grid, skid);
                continue;
            }

            var velocity = body.LinearVelocity;
            var speed = velocity.Length();

            if (speed <= SkidStopSpeed)
            {
                EndSkid(grid, skid);
                continue;
            }

            // Anything standing where the hull is going gets the same treatment an FTL arrival gives it.
            _shuttle.Smimsh(grid);

            if (speed <= SkidRamSpeed || _timing.CurTime < skid.NextBite)
                continue;

            var elapsed = (float) SkidBiteInterval.TotalSeconds;
            skid.NextBite = _timing.CurTime + SkidBiteInterval;

            GrindLeadingEdge((grid, gridComp), skid, body, velocity / speed, speed, elapsed);
        }
    }

    /// <summary>
    /// A touchdown the hull walks away from: the thud, the scrape, and the tiles it pays for arriving at the speed it
    /// did. <paramref name="severity"/> is the touchdown speed over the speed a free fall would have brought it in at,
    /// so a hull that nearly held itself up barely marks the deck and one that nearly crashed loses its nose.
    /// </summary>
    public void HardLanding(Entity<MapGridComponent> grid, float severity)
    {
        BeginSkid(grid.Owner);

        if (!TryComp<WFSkidComponent>(grid.Owner, out var skid))
            return;

        severity = Math.Clamp(severity, 0f, 1f);

        // The whole hull takes the landing; nothing comes off from this alone.
        BiteTiles(grid, skid, ImpactTileDamage * severity, null);

        if (!TryComp<PhysicsComponent>(grid.Owner, out var body))
            return;

        var velocity = body.LinearVelocity;
        var speed = velocity.Length();

        // Straight down onto its own footprint has no leading edge to concentrate the impact on.
        if (speed <= SkidStopSpeed)
            return;

        BiteTiles(grid, skid, ImpactEdgeDamage * severity, velocity / speed);
    }

    /// <summary>
    /// Damages the tiles actually taking the impact - the ones furthest along the direction of travel - and tears off
    /// the ones that have had enough. Measured by projection rather than by a bounding edge so a hull sliding in
    /// corner-first loses its corner, which is what it is leading with.
    /// </summary>
    private void GrindLeadingEdge(
        Entity<MapGridComponent> grid,
        WFSkidComponent skid,
        PhysicsComponent body,
        Vector2 heading,
        float speed,
        float elapsed)
    {
        var lost = BiteTiles(grid, skid, SkidTileDamageRate * speed * elapsed, heading);

        if (lost == 0)
            return;

        var cost = SkidTileSpeedCost * lost;

        _physics.SetLinearVelocity(grid.Owner,
            speed <= cost ? Vector2.Zero : heading * (speed - cost),
            body: body);
    }

    /// <summary>
    /// Puts damage on a hull's own tiles and tears off the ones that have had enough, leaving a small silent blast
    /// where each one was. With a world heading only the leading edge is touched; without one the whole footprint is,
    /// which is what an impact straight down does. Returns how many tiles went.
    /// </summary>
    private int BiteTiles(Entity<MapGridComponent> grid, WFSkidComponent skid, float damage, Vector2? heading)
    {
        if (damage <= 0f)
            return 0;

        // The hull's own frame: the footprint is indexed in it, and the hull may be sliding sideways or spinning.
        var local = heading is { } world ? (-_transform.GetWorldRotation(grid.Owner)).RotateVec(world) : Vector2.Zero;

        _skidEdge.Clear();
        var best = float.MinValue;

        var tiles = _map.GetAllTilesEnumerator(grid.Owner, grid.Comp);
        while (tiles.MoveNext(out var tileRef))
        {
            var indices = tileRef.Value.GridIndices;

            if (heading is not null)
                best = MathF.Max(best, Projection(indices, local));

            _skidEdge.Add(indices);
        }

        var lost = 0;

        foreach (var indices in _skidEdge)
        {
            if (heading is not null && Projection(indices, local) < best - SkidEdgeDepth)
                continue;

            var total = skid.TileDamage.GetValueOrDefault(indices) + damage;

            if (total < SkidTileThreshold)
            {
                skid.TileDamage[indices] = total;
                continue;
            }

            skid.TileDamage.Remove(indices);
            lost++;

            var coords = _map.GridTileToLocal(grid.Owner, grid.Comp, indices);
            _map.SetTile(grid.Owner, grid.Comp, indices, Tile.Empty);

            _explosion.QueueExplosion(coords,
                ExplosionSystem.DefaultExplosionPrototypeId,
                SkidTileIntensity,
                SkidTileSlope,
                SkidTileMaxIntensity,
                cause: grid.Owner,
                addLog: false,
                silent: true);
        }

        return lost;
    }

    /// <summary>How far along the direction of travel a tile sits; the leading edge is the highest of these.</summary>
    private static float Projection(Vector2i indices, Vector2 heading)
    {
        return Vector2.Dot(new Vector2(indices.X + 0.5f, indices.Y + 0.5f), heading);
    }

    /// <summary>Stops the scrape and lets the hull be an ordinary grid again.</summary>
    private void EndSkid(EntityUid grid, WFSkidComponent skid)
    {
        if (skid.Loop is { } loop)
            _audio.Stop(loop);

        RemComp<WFSkidComponent>(grid);
    }
}
