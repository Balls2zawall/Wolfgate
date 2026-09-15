using Content.Server._WF.PlanetCracker.Planets;
using Content.Server.Shuttles.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._WF.Shuttles;

/// <summary>
/// The thrust loop: while any pilot is holding a movement or rotation key, the hull's crew and anyone hovering over it
/// hear the engines. One stream per hull, to the grid audience rather than a point source at the grid origin, which
/// on a capital hull would be engines in one corridor. Re-cut on an interval so somebody who boarded mid-burn is in.
/// </summary>
public sealed partial class WFThrustAmbienceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private WFGridAudienceSystem _audience = default!;

    public static readonly SoundSpecifier ThrustLoop = new SoundPathSpecifier("/Audio/_WF/Shuttle/thrust_loop.ogg");

    /// <summary>
    /// Keys that mean the thrusters are firing: the strafes and the brake. Rotation is the gyroscope's and the
    /// vertical keys are the landing thrusters', so neither counts.
    /// </summary>
    private const ShuttleButtons Thrusting = ShuttleButtons.StrafeUp | ShuttleButtons.StrafeDown
        | ShuttleButtons.StrafeLeft | ShuttleButtons.StrafeRight | ShuttleButtons.Brake;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan RecutInterval = TimeSpan.FromSeconds(10);

    private TimeSpan _nextSweep;
    private readonly HashSet<EntityUid> _thrusting = new();
    private readonly List<(EntityUid Grid, WFThrustAmbienceComponent Comp)> _playing = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextSweep)
            return;

        _nextSweep = _timing.CurTime + SweepInterval;
        _thrusting.Clear();

        var pilots = EntityQueryEnumerator<PilotComponent>();
        while (pilots.MoveNext(out _, out var pilot))
        {
            if ((pilot.HeldButtons & Thrusting) == 0 || pilot.Console is not { } console || TerminatingOrDeleted(console))
                continue;

            if (Transform(console).GridUid is { } grid && HasComp<ShuttleComponent>(grid))
                _thrusting.Add(grid);
        }

        // Collected first: the loop below removes components, which an enumerator will not survive.
        _playing.Clear();
        var playing = EntityQueryEnumerator<WFThrustAmbienceComponent>();
        while (playing.MoveNext(out var uid, out var comp))
        {
            _playing.Add((uid, comp));
        }

        foreach (var (grid, comp) in _playing)
        {
            if (_thrusting.Contains(grid))
                continue;

            comp.Stream = _audio.Stop(comp.Stream);
            RemComp<WFThrustAmbienceComponent>(grid);
        }

        foreach (var grid in _thrusting)
        {
            var comp = EnsureComp<WFThrustAmbienceComponent>(grid);

            if (comp.Stream != null && _timing.CurTime < comp.NextRecut)
                continue;

            comp.Stream = _audio.Stop(comp.Stream);
            comp.Stream = _audio.PlayGlobal(ThrustLoop, _audience.Aboard(grid), true, AudioParams.Default.WithLoop(true))?.Entity;
            comp.NextRecut = _timing.CurTime + RecutInterval;
        }
    }
}

/// <summary>Marks a hull whose thrust loop is playing, and holds the stream.</summary>
[RegisterComponent]
public sealed partial class WFThrustAmbienceComponent : Component
{
    [ViewVariables]
    public EntityUid? Stream;

    [ViewVariables]
    public TimeSpan NextRecut;
}
