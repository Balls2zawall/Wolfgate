using Content.Shared._WF.ShipPa;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server._WF.ShipPa;

/// <summary>
/// Looping ship-wide alarms. One alarm is a single sound running on every working speaker at once;
/// speakers that come online later join the loop in phase rather than starting from the top.
/// </summary>
public sealed partial class ShipPaSystem
{
    /// <summary>How often running alarms are matched back up against the ship's working speakers.</summary>
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>How often a running alarm repeats its text over the speakers, so latecomers learn what it is.</summary>
    private static readonly TimeSpan BubbleInterval = TimeSpan.FromSeconds(12);

    private readonly Dictionary<EntityUid, Dictionary<string, ActiveAlarm>> _alarms = new();

    /// <summary>Reused by the reconcile so the common case of nothing changing allocates nothing.</summary>
    private readonly List<EntityUid> _staleStreams = new();

    private readonly List<EntityUid> _deadGrids = new();

    /// <summary>Speakers carrying the PA of the grid being reconciled.</summary>
    private readonly HashSet<EntityUid> _activeSpeakers = new();

    private TimeSpan _nextReconcile;

    /// <summary>
    /// Starts a synchronised loop on every working speaker; speakers that come online later join in phase.
    /// With a message, the speakers repeat it in a bubble every <see cref="BubbleInterval"/> while it runs.
    /// </summary>
    public void StartAlarm(EntityUid grid, string key, SoundSpecifier sound, AudioParams? audioParams = null, string? message = null, Color? color = null)
    {
        if (!Exists(grid) || TerminatingOrDeleted(grid))
            return;

        var resolved = _audio.ResolveSound(sound);

        if (string.IsNullOrEmpty(_audio.GetAudioPath(resolved)))
            return;

        var length = (float) _audio.GetAudioLength(resolved).TotalSeconds;

        if (length <= 0f)
            return;

        // Restarting an alarm replaces it outright, so the ship never runs two copies of one key.
        StopAlarm(grid, key);

        var alarm = new ActiveAlarm
        {
            Resolved = resolved,
            Params = (audioParams ?? sound.Params).WithLoop(true),
            Start = _timing.CurTime,
            Length = length,
            BroadcastId = NextBroadcastId(),
            Message = message,
            Color = color,
            // The caller announces the start itself; the repeats begin one interval in.
            NextBubble = _timing.CurTime + BubbleInterval,
        };

        _alarms.GetOrNew(grid)[key] = alarm;
        ReconcileAlarm(grid, alarm);
    }

    public void StopAlarm(EntityUid grid, string key)
    {
        if (!_alarms.TryGetValue(grid, out var alarms) || !alarms.Remove(key, out var alarm))
            return;

        foreach (var stream in alarm.Streams.Values)
        {
            StopStream(stream);
        }

        alarm.Streams.Clear();

        if (alarms.Count == 0)
            _alarms.Remove(grid);
    }

    public bool IsAlarmActive(EntityUid grid, string key)
    {
        return _alarms.TryGetValue(grid, out var alarms) && alarms.ContainsKey(key);
    }

    /// <summary>
    /// Matches every running alarm back up against its ship's working speakers. Only grids with an
    /// alarm are touched.
    /// </summary>
    private void UpdateAlarms()
    {
        if (_alarms.Count == 0 || _timing.CurTime < _nextReconcile)
            return;

        _nextReconcile = _timing.CurTime + ReconcileInterval;
        _deadGrids.Clear();

        foreach (var (grid, alarms) in _alarms)
        {
            if (!Exists(grid) || TerminatingOrDeleted(grid))
            {
                _deadGrids.Add(grid);
                continue;
            }

            foreach (var alarm in alarms.Values)
            {
                ReconcileAlarm(grid, alarm);
            }
        }

        // The streams died with the grid; just forget the bookkeeping.
        foreach (var grid in _deadGrids)
        {
            _alarms.Remove(grid);
        }

        _deadGrids.Clear();
    }

    private void ReconcileAlarm(EntityUid grid, ActiveAlarm alarm)
    {
        var now = _timing.CurTime;

        // The active set can change under a running alarm, e.g. the first dedicated speaker going up
        // takes the ship off its air alarms, so streams on units that dropped out stop too. A looping alarm is the
        // worst of the fan-out - nothing despawns it - so it is held to the same carrier cap as a one-shot.
        _speakerBuffer.Clear();
        GatherSpeakers(grid, _speakerBuffer);
        SelectCarriers(_speakerBuffer);
        _activeSpeakers.Clear();

        foreach (var speaker in _speakerBuffer)
        {
            _activeSpeakers.Add(speaker.Owner);
        }

        _staleStreams.Clear();

        foreach (var (speaker, stream) in alarm.Streams)
        {
            if (Exists(stream.Audio) && _activeSpeakers.Contains(speaker))
                continue;

            _staleStreams.Add(speaker);
        }

        foreach (var speaker in _staleStreams)
        {
            if (!alarm.Streams.Remove(speaker, out var stream))
                continue;

            StopStream(stream);
        }

        _staleStreams.Clear();

        foreach (var entity in _speakerBuffer)
        {
            if (!alarm.Streams.TryGetValue(entity.Owner, out var stream))
            {
                if (PlayAlarmStream(entity, alarm, now) is not { } audio)
                    continue;

                stream = new AlarmStream { Audio = audio };
            }

            UpdateStatic(entity, alarm, ref stream);
            alarm.Streams[entity.Owner] = stream;
        }

        _speakerBuffer.Clear();

        if (alarm.Message == null || now < alarm.NextBubble)
            return;

        alarm.NextBubble = now + BubbleInterval;
        var wrapped = WrapBubble(grid, alarm.Message, alarm.Color);

        foreach (var speaker in alarm.Streams.Keys)
        {
            if (TryComp(speaker, out ShipPaSpeakerComponent? comp))
                SpeakerBubble((speaker, comp), alarm.Message, wrapped);
        }
    }

    /// <summary>
    /// Starts one copy of the alarm on a speaker, wound forward to wherever the rest of the ship is.
    /// </summary>
    private EntityUid? PlayAlarmStream(Entity<ShipPaSpeakerComponent> speaker, ActiveAlarm alarm, TimeSpan now)
    {
        var offset = (float) ((now - alarm.Start).TotalSeconds % alarm.Length);
        var stream = _audio.PlayPvs(alarm.Resolved, speaker.Owner, BuildParams(speaker, alarm.Params).WithPlayOffset(offset));

        if (stream == null)
            return null;

        // PlayOffsetSeconds is only read when a client creates its own audio. A networked stream takes
        // its position from AudioStart, so back-date that instead to put a late joiner in phase.
        if (offset > 0f)
            _audio.SetPlaybackPosition(stream.Value.Entity, offset);

        TagStream(stream.Value.Entity, alarm.BroadcastId, speaker.Owner, speaker.Comp.Distortion, false);

        return stream.Value.Entity;
    }

    /// <summary>
    /// Runs a quiet crackle under the alarm while the speaker is damaged, and drops it once repaired.
    /// </summary>
    private void UpdateStatic(Entity<ShipPaSpeakerComponent> speaker, ActiveAlarm alarm, ref AlarmStream stream)
    {
        var distortion = speaker.Comp.Distortion;
        var wanted = distortion >= StaticThreshold && speaker.Comp.StaticSound != null;
        var running = stream.Static is { } existing && Exists(existing);

        if (wanted == running)
            return;

        if (!wanted)
        {
            _audio.Stop(stream.Static);
            stream.Static = null;
            return;
        }

        var staticParams = AudioParams.Default
            .WithLoop(true)
            .WithVolume(-12f + 6f * distortion)
            .WithMaxDistance(speaker.Comp.Range)
            .WithReferenceDistance(ReferenceDistance)
            .WithRolloffFactor(RolloffFactor);

        var overlay = _audio.PlayPvs(speaker.Comp.StaticSound, speaker.Owner, staticParams);

        if (overlay == null)
        {
            stream.Static = null;
            return;
        }

        TagStream(overlay.Value.Entity, alarm.BroadcastId, speaker.Owner, distortion, true);
        stream.Static = overlay.Value.Entity;
    }

    /// <summary>
    /// Drops every alarm stream running on one speaker, wherever it is.
    /// </summary>
    private void StopSpeakerStreams(EntityUid speaker)
    {
        if (_alarms.Count == 0)
            return;

        foreach (var alarms in _alarms.Values)
        {
            foreach (var alarm in alarms.Values)
            {
                if (!alarm.Streams.Remove(speaker, out var stream))
                    continue;

                StopStream(stream);
            }
        }
    }

    private void StopStream(AlarmStream stream)
    {
        if (Exists(stream.Audio))
            _audio.Stop(stream.Audio);

        if (stream.Static is { } overlay && Exists(overlay))
            _audio.Stop(overlay);
    }

    /// <summary>
    /// One looping sound running ship-wide, and the per-speaker copies carrying it.
    /// </summary>
    private sealed class ActiveAlarm
    {
        public ResolvedSoundSpecifier Resolved = default!;
        public AudioParams Params;

        /// <summary>When the alarm started, so a new copy can be wound forward to match.</summary>
        public TimeSpan Start;

        public float Length;
        public int BroadcastId;
        public Dictionary<EntityUid, AlarmStream> Streams = new();

        /// <summary>Text repeated over the speakers while the alarm runs, if any.</summary>
        public string? Message;
        public Color? Color;
        public TimeSpan NextBubble;
    }

    /// <summary>
    /// One speaker's copy of an alarm, plus the damage crackle layered over it.
    /// </summary>
    private struct AlarmStream
    {
        public EntityUid Audio;
        public EntityUid? Static;
    }
}
