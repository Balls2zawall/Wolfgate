using Content.Shared._WF.ShipPa;
using Robust.Client.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Client._WF.ShipPa;

/// <summary>
/// Keeps overlapping PA speakers from piling up: of every copy of a broadcast only the nearest audible one is
/// heard and the rest crossfade out. Streams from damaged speakers are also muffled and drop in and out.
/// </summary>
public sealed partial class ShipPaMeshSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Duck levels crossed per second, i.e. a 250 ms fade when the nearest speaker changes.
    /// </summary>
    private const float CrossfadeRate = 4f;

    /// <summary>
    /// How often a damaged stream can cut in or out.
    /// </summary>
    private const float FlutterRate = 12f;

    /// <summary>
    /// Level a dropout falls to. Not silence, so the speaker still sounds like it's struggling.
    /// </summary>
    private const float FlutterLevel = 0.15f;

    /// <summary>
    /// Distortion at or below this counts as an undamaged speaker.
    /// </summary>
    private const float DistortionFloor = 0.05f;

    /// <summary>
    /// Occlusion added on top of the engine's at full distortion.
    /// </summary>
    private const float MuffleStrength = 3f;

    private readonly Dictionary<EntityUid, StreamState> _states = new();

    /// <summary>
    /// Nearest in-range stream of each broadcast, rebuilt every frame.
    /// </summary>
    private readonly Dictionary<int, (EntityUid Entity, float Distance)> _nearest = new();

    private readonly List<StreamInfo> _streams = new();

    public override void Initialize()
    {
        base.Initialize();

        // The engine rewrites gain and occlusion on its own audio frames, so we have to run after it.
        UpdatesAfter.Add(typeof(AudioSystem));

        SubscribeLocalEvent<ShipPaAudioComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, ShipPaAudioComponent component, ComponentShutdown args)
    {
        _states.Remove(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        Collect();

        if (_streams.Count == 0)
            return;

        // Audio keeps running while the game is paused, so the dropouts are driven off real time.
        var time = (float) _timing.RealTime.TotalSeconds;

        foreach (var stream in _streams)
        {
            Apply(stream, frameTime, time);
        }
    }

    /// <summary>
    /// Measures every managed stream against the listener and picks the nearest one of each broadcast.
    /// </summary>
    private void Collect()
    {
        _streams.Clear();
        _nearest.Clear();

        var listener = _audio.GetListenerCoordinates();
        var query = EntityQueryEnumerator<ShipPaAudioComponent, AudioComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var pa, out var audio, out var xform))
        {
            // Static overlays are already quiet, so the engine keeps them.
            if (pa.IsOverlay || !audio.Started || xform.MapID != listener.MapId)
                continue;

            var distance = (_xform.GetWorldPosition(uid) - listener.Position).Length();

            // The same test ProcessStream runs, so a stream the engine muted stays muted.
            var inRange = _audio.GetAudioDistance(distance) <= audio.MaxDistance;

            _streams.Add(new StreamInfo(uid, audio, pa.BroadcastId, pa.Distortion, inRange));

            if (!inRange || pa.BroadcastId <= 0)
                continue;

            // Ties break on uid so the winner doesn't flicker between two equidistant speakers.
            if (_nearest.TryGetValue(pa.BroadcastId, out var best) &&
                (best.Distance < distance || (best.Distance.Equals(distance) && best.Entity.Id <= uid.Id)))
            {
                continue;
            }

            _nearest[pa.BroadcastId] = (uid, distance);
        }
    }

    /// <summary>
    /// Writes this frame's gain and occlusion for one stream, on top of whatever the engine last set.
    /// </summary>
    private void Apply(StreamInfo stream, float frameTime, float time)
    {
        // A stream with no broadcast id isn't part of a mesh, so nothing ducks it.
        var nearest = stream.BroadcastId <= 0
                      || (_nearest.TryGetValue(stream.BroadcastId, out var best) && best.Entity == stream.Entity);
        var target = nearest ? 1f : 0f;

        // A stream we've never seen starts where it wants to be, so the first speaker isn't faded in.
        if (!_states.TryGetValue(stream.Entity, out var state))
            state = new StreamState { Duck = target };

        state.Duck = Approach(state.Duck, target, CrossfadeRate * frameTime);

        if (stream.InRange)
        {
            var flutter = Flutter(stream.Entity, stream.Distortion, time);
            stream.Audio.Gain = SharedAudioSystem.VolumeToGain(stream.Audio.Params.Volume) * state.Duck * flutter;

            if (stream.Distortion > DistortionFloor)
            {
                // Anything other than our own last value means the engine wrote a fresh base to muffle.
                if (!state.Muffled || !stream.Audio.Occlusion.Equals(state.Applied))
                {
                    state.Base = stream.Audio.Occlusion;
                    state.Muffled = true;
                }

                state.Applied = state.Base + MuffleStrength * stream.Distortion;
                stream.Audio.Occlusion = state.Applied;
            }
            else
            {
                state.Muffled = false;
            }
        }

        _states[stream.Entity] = state;
    }

    /// <summary>
    /// Moves a value toward a target by at most delta.
    /// </summary>
    private static float Approach(float value, float target, float delta)
    {
        if (value < target)
            return MathF.Min(target, value + delta);

        return MathF.Max(target, value - delta);
    }

    /// <summary>
    /// Deterministic on/off dropout for a damaged stream, resampled at <see cref="FlutterRate"/>.
    /// </summary>
    private static float Flutter(EntityUid uid, float distortion, float time)
    {
        if (distortion <= DistortionFloor)
            return 1f;

        // Cheap hash noise: the fractional part of a big sine, held for one flutter step.
        var tick = MathF.Floor(time * FlutterRate);
        var noise = MathF.Sin(tick * 12.9898f + uid.Id * 78.233f) * 43758.5453f;
        noise -= MathF.Floor(noise);

        return noise > 1f - 0.8f * distortion ? FlutterLevel : 1f;
    }

    /// <summary>
    /// One managed stream as measured this frame.
    /// </summary>
    private readonly record struct StreamInfo(
        EntityUid Entity,
        AudioComponent Audio,
        int BroadcastId,
        float Distortion,
        bool InRange);

    /// <summary>
    /// What we last did to a stream, so the crossfade and the muffle survive the engine rewriting both.
    /// </summary>
    private struct StreamState
    {
        /// <summary>Current crossfade level, 0 silent to 1 full.</summary>
        public float Duck;

        /// <summary>Whether <see cref="Base"/> and <see cref="Applied"/> are meaningful.</summary>
        public bool Muffled;

        /// <summary>Engine occlusion our muffle is added to.</summary>
        public float Base;

        /// <summary>Occlusion we last wrote, so a fresh engine value is recognisable.</summary>
        public float Applied;
    }
}
