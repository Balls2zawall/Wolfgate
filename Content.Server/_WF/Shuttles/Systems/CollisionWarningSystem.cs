using System.Numerics;
using Content.Server._WF.ShipPa;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Shuttles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._WF.Shuttles.Systems;

/// <summary>
/// Ship collision warning. Sweeps every piloted ship's hull along its velocity, finds what it is going
/// to hit and how soon, and puts a <see cref="CollisionWarningComponent"/> on the grid plus an alarm on
/// the ship's PA. Only closing speeds hard enough to hurt count, so ordinary station approaches are quiet.
/// </summary>
public sealed class CollisionWarningSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private ShipPaSystem _pa = default!;

    /// <summary>Alarm loop key for the traffic advisory klaxon.</summary>
    public const string AdvisoryAlarm = "tcas-advisory";

    /// <summary>Alarm loop key for the imminent collision klaxon.</summary>
    public const string ImminentAlarm = "tcas-imminent";

    /// <summary>Alarm loop key for the spoken callout, which runs under both klaxons.</summary>
    public const string VoiceAlarm = "tcas-voice";

    private static readonly SoundSpecifier AdvisorySound =
        new SoundPathSpecifier("/Audio/_WF/Shuttles/Tcas/traffic_alarm.ogg");

    private static readonly SoundSpecifier ImminentSound =
        new SoundPathSpecifier("/Audio/_WF/Shuttles/Tcas/collision_alarm.ogg");

    private static readonly SoundSpecifier VoiceSound =
        new SoundPathSpecifier("/Audio/_WF/Shuttles/Tcas/traffic.ogg");

    /// <summary>Speed below which a grid is not going anywhere worth predicting.</summary>
    private const float MinimumSpeed = 0.5f;

    private bool _enabled;
    private float _lookahead;
    private float _imminentTime;
    private float _minimumClosingSpeed;
    private float _margin;
    private float _hysteresis;
    private float _updateInterval;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    /// <summary>Grids carrying a shuttle console, rebuilt each sweep; ships nobody flies are not warned.</summary>
    private readonly HashSet<EntityUid> _pilotedGrids = new();

    /// <summary>Reused per ship so a quiet sweep allocates nothing.</summary>
    private List<Entity<MapGridComponent>> _candidates = new();
    private readonly HashSet<EntityUid> _docked = new();
    private readonly List<EntityUid> _stale = new();

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_cfg, CollisionWarningCVars.Enabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.Lookahead, value => _lookahead = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.ImminentTime, value => _imminentTime = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.MinimumClosingSpeed, value => _minimumClosingSpeed = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.Margin, value => _margin = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.Hysteresis, value => _hysteresis = value, true);
        Subs.CVar(_cfg, CollisionWarningCVars.UpdateInterval, value => _updateInterval = value, true);

        SubscribeLocalEvent<CollisionWarningComponent, ComponentShutdown>(OnWarningShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(_updateInterval);

        Sweep();
    }

    /// <summary>
    /// One pass over every piloted ship. Exposed so tests can drive a sweep without waiting on the tick.
    /// </summary>
    public void Sweep()
    {
        if (!_enabled)
        {
            ClearAll();
            return;
        }

        CollectPilotedGrids();

        var shuttles = EntityQueryEnumerator<ShuttleComponent, TransformComponent, PhysicsComponent>();

        while (shuttles.MoveNext(out var uid, out _, out var xform, out var physics))
        {
            if (!_pilotedGrids.Contains(uid) || !_gridQuery.TryComp(uid, out var grid))
                continue;

            if (FindThreat((uid, grid, xform, physics)) is { } threat)
                Warn(uid, threat);
            else
                Clear(uid);
        }

        ExpireHeldWarnings();
    }

    /// <summary>
    /// The first thing this ship is going to hit inside the lookahead window, if any.
    /// </summary>
    private Threat? FindThreat(Entity<MapGridComponent, TransformComponent, PhysicsComponent> ship)
    {
        var (uid, _, xform, physics) = ship;

        // Ships in FTL cannot hit anything, and the hyperspace map holds no traffic.
        if (HasComp<FTLComponent>(uid) || xform.MapUid == null || HasComp<FTLMapComponent>(xform.MapUid))
            return null;

        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();

        if (speed < MinimumSpeed)
            return null;

        // A hull is taken as its bounding box. Rotation is left out of the prediction, so a ship that
        // only spins into another is not warned about.
        var ourBounds = _lookup.GetWorldAABB(uid, xform).Enlarged(_margin);

        _candidates.Clear();
        _mapManager.FindGridsIntersecting(xform.MapID,
            ourBounds.Enlarged(speed * _lookahead),
            ref _candidates,
            approx: true,
            includeMap: false);

        if (_candidates.Count == 0)
            return null;

        // Docking is not a collision, and neither is a ship already tied to ours.
        _docked.Clear();
        _shuttle.GetAllDockedShuttles(uid, _docked);

        var ourCentre = ourBounds.Center;
        Threat? best = null;

        foreach (var candidate in _candidates)
        {
            var other = candidate.Owner;

            if (other == uid || _docked.Contains(other))
                continue;

            if (!_xformQuery.TryComp(other, out var otherXform))
                continue;

            var otherVelocity = _physicsQuery.TryComp(other, out var otherPhysics)
                ? otherPhysics.LinearVelocity
                : Vector2.Zero;

            // How the other hull moves as this ship sees it.
            var approach = otherVelocity - velocity;
            var closingSpeed = approach.Length();

            if (closingSpeed < _minimumClosingSpeed)
                continue;

            var otherBounds = _lookup.GetWorldAABB(other, otherXform);

            if (TimeToContact(ourBounds, otherBounds, approach) is not { } time)
                continue;

            if (best != null && time >= best.Value.Time)
                continue;

            best = new Threat(other, time, closingSpeed, otherBounds.Center - ourCentre);
        }

        return best;
    }

    /// <summary>
    /// Seconds until two boxes overlap, given how the second moves relative to the first, or null if
    /// they never do inside the lookahead window. Each axis gives the window it overlaps in, and
    /// contact is where those windows meet.
    /// </summary>
    private float? TimeToContact(Box2 ours, Box2 other, Vector2 approach)
    {
        var entry = float.NegativeInfinity;
        var exit = float.PositiveInfinity;

        for (var axis = 0; axis < 2; axis++)
        {
            var velocity = axis == 0 ? approach.X : approach.Y;
            var ourMin = axis == 0 ? ours.Left : ours.Bottom;
            var ourMax = axis == 0 ? ours.Right : ours.Top;
            var otherMin = axis == 0 ? other.Left : other.Bottom;
            var otherMax = axis == 0 ? other.Right : other.Top;

            float axisEntry;
            float axisExit;

            if (MathF.Abs(velocity) <= float.Epsilon)
            {
                // Nothing closes on this axis, so the boxes either already share it or never will.
                if (otherMax < ourMin || otherMin > ourMax)
                    return null;

                axisEntry = float.NegativeInfinity;
                axisExit = float.PositiveInfinity;
            }
            else if (velocity > 0f)
            {
                axisEntry = (ourMin - otherMax) / velocity;
                axisExit = (ourMax - otherMin) / velocity;
            }
            else
            {
                axisEntry = (ourMax - otherMin) / velocity;
                axisExit = (ourMin - otherMax) / velocity;
            }

            entry = MathF.Max(entry, axisEntry);
            exit = MathF.Min(exit, axisExit);
        }

        // Passing clear, already past, or further out than anyone needs warning of.
        if (entry > exit || exit < 0f || entry > _lookahead)
            return null;

        return MathF.Max(entry, 0f);
    }

    /// <summary>
    /// Raises or refreshes the warning on a ship, and matches the PA alarm to its stage.
    /// </summary>
    private void Warn(EntityUid uid, Threat threat)
    {
        var level = threat.Time <= _imminentTime ? CollisionWarningLevel.Imminent : CollisionWarningLevel.Advisory;
        var warning = EnsureComp<CollisionWarningComponent>(uid);
        var escalated = warning.Level != level;

        warning.Level = level;
        warning.ImpactTime = _timing.CurTime + TimeSpan.FromSeconds(threat.Time);
        warning.ThreatName = _shuttle.GetIFFLabel(threat.Grid);
        warning.Bearing = GetBearing(uid, threat.Offset);
        warning.ClosingSpeed = threat.ClosingSpeed;
        warning.ClearTime = _timing.CurTime + TimeSpan.FromSeconds(_hysteresis);
        warning.Threat = threat.Grid;
        Dirty(uid, warning);

        // The callout runs through both stages, so escalating does not restart it.
        if (!_pa.IsAlarmActive(uid, VoiceAlarm))
            _pa.StartAlarm(uid, VoiceAlarm, VoiceSound);

        // Klaxons loop until stopped, so only a change of stage touches the PA.
        if (!escalated && (_pa.IsAlarmActive(uid, AdvisoryAlarm) || _pa.IsAlarmActive(uid, ImminentAlarm)))
            return;

        if (level == CollisionWarningLevel.Imminent)
        {
            _pa.StopAlarm(uid, AdvisoryAlarm);
            _pa.StartAlarm(uid, ImminentAlarm, ImminentSound,
                message: Loc.GetString("collision-warning-pa-imminent"),
                color: Color.Red);
        }
        else
        {
            _pa.StopAlarm(uid, ImminentAlarm);
            _pa.StartAlarm(uid, AdvisoryAlarm, AdvisorySound,
                message: Loc.GetString("collision-warning-pa-advisory"),
                color: Color.Orange);
        }
    }

    /// <summary>
    /// Marks a ship's warning as no longer renewed. It is dropped once the hold expires.
    /// </summary>
    private void Clear(EntityUid uid)
    {
        if (!TryComp<CollisionWarningComponent>(uid, out var warning))
            return;

        warning.Threat = null;

        if (_timing.CurTime >= warning.ClearTime)
            RemComp<CollisionWarningComponent>(uid);
    }

    private void ExpireHeldWarnings()
    {
        _stale.Clear();

        var warnings = EntityQueryEnumerator<CollisionWarningComponent>();

        while (warnings.MoveNext(out var uid, out var warning))
        {
            if (warning.Threat == null && _timing.CurTime >= warning.ClearTime)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            RemComp<CollisionWarningComponent>(uid);
        }
    }

    private void ClearAll()
    {
        _stale.Clear();

        var warnings = EntityQueryEnumerator<CollisionWarningComponent>();

        while (warnings.MoveNext(out var uid, out _))
        {
            _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            RemComp<CollisionWarningComponent>(uid);
        }
    }

    /// <summary>
    /// The alarm belongs to the warning, so it stops with it however the warning ends.
    /// </summary>
    private void OnWarningShutdown(Entity<CollisionWarningComponent> ent, ref ComponentShutdown args)
    {
        _pa.StopAlarm(ent, AdvisoryAlarm);
        _pa.StopAlarm(ent, ImminentAlarm);
        _pa.StopAlarm(ent, VoiceAlarm);
    }

    private void CollectPilotedGrids()
    {
        _pilotedGrids.Clear();

        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();

        while (consoles.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is { } grid)
                _pilotedGrids.Add(grid);
        }
    }

    /// <summary>
    /// Bearing to the threat in degrees clockwise from the ship's nose.
    /// </summary>
    private float GetBearing(EntityUid uid, Vector2 offset)
    {
        var bearing = (new Angle(offset) - _xform.GetWorldRotation(uid)).Degrees;

        return (float) ((bearing % 360d + 360d) % 360d);
    }

    private readonly record struct Threat(EntityUid Grid, float Time, float ClosingSpeed, Vector2 Offset);
}
