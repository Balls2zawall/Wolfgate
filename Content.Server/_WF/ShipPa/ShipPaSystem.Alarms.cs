using Content.Shared._WF.ShipPa;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server._WF.ShipPa;

/// <summary>
/// Ship-wide sounds that run for a while: looping alarms, and one-off tracks that play to the end. Either is
/// a single sound running on every working speaker at once, and speakers that come online later join it in
/// phase rather than starting from the top.
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

    /// <summary>Tracks that played out this reconcile, stopped after the sweep rather than during it.</summary>
    private readonly List<(EntityUid Grid, string Key)> _finishedTracks = new();

    /// <summary>Speakers carrying the PA of the grid being reconciled.</summary>
    private readonly HashSet<EntityUid> _activeSpeakers = new();

    private TimeSpan _nextReconcile;

    /// <summary>
    /// Starts a synchronised loop on every working speaker; speakers that come online later join in phase.
    /// With a message, the speakers repeat it in a bubble every <see cref="BubbleInterval"/> while it runs.
    /// </summary>
    public void StartAlarm(EntityUid grid, string key, SoundSpecifier sound, AudioParams? audioParams = null, string? message = null, Color? color = null)
    {
        Start(grid, key, sound, loop: true, audioParams, message, color);
    }

    /// <summary>
    /// Starts a one-off sound ship-wide that plays through to its end and then clears itself. Speakers that
    /// come online part-way through join wherever the rest of the ship has got to, rather than replaying it.
    /// Returns false when the sound couldn't be resolved.
    /// </summary>
    public bool StartTrack(EntityUid grid, string key, SoundSpecifier sound, AudioParams? audioParams = null, string? message = null, Color? color = null)
    {
        return Start(grid, key, sound, loop: false, audioParams, message, color);
    }

    private bool Start(EntityUid grid, string key, SoundSpecifier sound, bool loop, AudioParams? audioParams, string? message, Color? color)
    {
        if (!Exists(grid) || TerminatingOrDeleted(grid))
            return false;

        var resolved = _audio.ResolveSound(sound);

        if (string.IsNullOrEmpty(_audio.GetAudioPath(resolved)))
            return false;

        float length;
        try
        {
            length = (float) _audio.GetAudioLength(resolved).TotalSeconds;
        }
        catch (Exception e)
        {
            // Sounds mounted at runtime can be missing or malformed in ways a shipped one never is.
            Log.Warning($"Couldn't read the length of {_audio.GetAudioPath(resolved)} for the PA: {e.Message}");
            return false;
        }

        if (length <= 0f)
            return false;

        // Restarting replaces the old one outright, so the ship never runs two copies of one key.
        StopAlarm(grid, key);

        var alarm = new ActiveAlarm
        {
            Resolved = resolved,
            Params = (audioParams ?? sound.Params).WithLoop(loop),
            Loop = loop,
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
        return true;
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
        _finishedTracks.Clear();

        foreach (var (grid, alarms) in _alarms)
        {
            if (!Exists(grid) || TerminatingOrDeleted(grid))
            {
                _deadGrids.Add(grid);
                continue;
            }

            foreach (var (key, alarm) in alarms)
            {
                // A track that has played out clears itself; an alarm runs until something stops it.
                if (!alarm.Loop && Elapsed(alarm) >= alarm.Length)
                {
                    _finishedTracks.Add((grid, key));
                    continue;
                }

                ReconcileAlarm(grid, alarm);
            }
        }

        // Stopped outside the loop above, which is iterating the dictionaries they're removed from.
        foreach (var (grid, key) in _finishedTracks)
        {
            StopAlarm(grid, key);
            var ev = new ShipPaTrackFinishedEvent(grid, key);
            RaiseLocalEvent(ref ev);
        }

        _finishedTracks.Clear();

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
        // takes the ship off its air alarms, so streams on units that dropped out stop too.
        _speakerBuffer.Clear();
        GatherSpeakers(grid, _speakerBuffer);
        _speakerBuffer.RemoveAll(speaker => !IsFunctional(speaker));

        // A long hull carries more speakers than one client will give sources to, so a running alarm only
        // keeps streams on the ones nearest the crew. They're re-picked every reconcile, and a new copy is
        // wound forward to match, so walking the ship hands the alarm along rather than dropping it.
        TrimToBudget(_speakerBuffer);

        _activeSpeakers.Clear();

        foreach (var speaker in _speakerBuffer)
        {
            _activeSpeakers.Add(speaker.Owner);
        }

        _staleStreams.Clear();

        foreach (var (speaker, stream) in alarm.Streams)
        {
            // Dropping a stream nobody is near frees a source for one they can actually hear; they walk
            // back into range and the reconcile starts it again, wound forward to match the ship.
            if (Exists(stream.Audio)
                && _activeSpeakers.Contains(speaker)
                && TryComp(speaker, out ShipPaSpeakerComponent? comp)
                && IsFunctional((speaker, comp)))
            {
                continue;
            }

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

        // Captions go to the whole ship; only the audio is budgeted.
        Bubble(grid, alarm.Message, alarm.Color);
    }

    /// <summary>
    /// Starts one copy of the alarm on a speaker, wound forward to wherever the rest of the ship is.
    /// </summary>
    private EntityUid? PlayAlarmStream(Entity<ShipPaSpeakerComponent> speaker, ActiveAlarm alarm, TimeSpan now)
    {
        var elapsed = (float) (now - alarm.Start).TotalSeconds;

        // A loop wraps; a track is simply over, and a speaker joining now has nothing left to catch up to.
        if (!alarm.Loop && elapsed >= alarm.Length)
            return null;

        var offset = alarm.Loop ? elapsed % alarm.Length : elapsed;
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

    private float Elapsed(ActiveAlarm alarm)
    {
        return (float) (_timing.CurTime - alarm.Start).TotalSeconds;
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

        /// <summary>False for a one-off track, which ends on its own instead of running until stopped.</summary>
        public bool Loop = true;

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
