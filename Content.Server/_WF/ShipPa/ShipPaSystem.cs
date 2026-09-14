using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._WF.ShipPa;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._WF.ShipPa;

/// <summary>
/// Runs a ship's public address network: every speaker anchored to a grid carries that grid's
/// broadcasts, announcements and alarms. Membership is the grid the speaker sits on, so there is
/// nothing to wire up.
/// </summary>
public sealed partial class ShipPaSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;

    /// <summary>Distance at which a stream is still at full volume.</summary>
    private const float ReferenceDistance = 3f;

    /// <summary>How sharply a stream falls off past <see cref="ReferenceDistance"/>.</summary>
    private const float RolloffFactor = 1.5f;

    /// <summary>How often expired broadcasts and queued speaker counts are cleaned up.</summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    private static readonly SoundSpecifier DefaultChime = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    /// <summary>Grids whose speaker counts changed this tick, refreshed in bulk so power flicker is cheap.</summary>
    private readonly HashSet<EntityUid> _pendingCounts = new();

    /// <summary>Reused by <see cref="Broadcast"/> so a one-shot doesn't allocate.</summary>
    private readonly List<Entity<ShipPaSpeakerComponent>> _speakerBuffer = new();

    private TimeSpan _nextUpdate;
    private int _nextBroadcastId = 1;

    public override void Initialize()
    {
        base.Initialize();

        InitializeSpeakers();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        ClearExpiredBroadcasts();
        FlushPendingCounts();
        UpdateAlarms();
    }

    /// <summary>
    /// Plays a one-shot through every working speaker on the grid. Null when nothing could play.
    /// </summary>
    public int? Broadcast(EntityUid grid, SoundSpecifier sound, AudioParams? audioParams = null)
    {
        if (!Exists(grid))
            return null;

        var resolved = _audio.ResolveSound(sound);

        if (string.IsNullOrEmpty(_audio.GetAudioPath(resolved)))
            return null;

        var baseParams = audioParams ?? sound.Params;
        var id = NextBroadcastId();
        var now = _timing.CurTime;
        var played = false;
        TimeSpan? length = null;

        // Every copy goes out in the same tick, which is what keeps them in phase.
        _speakerBuffer.Clear();
        GatherSpeakers(grid, _speakerBuffer);

        foreach (var speaker in _speakerBuffer)
        {
            if (!IsFunctional(speaker))
                continue;

            var stream = _audio.PlayPvs(resolved, speaker.Owner, BuildParams(speaker, baseParams));

            if (stream == null)
                continue;

            TagStream(stream.Value.Entity, id, speaker.Owner, speaker.Comp.Distortion, false);

            length ??= _audio.GetAudioLength(resolved);
            speaker.Comp.BroadcastingUntil = now + length.Value;
            UpdateAppearance(speaker);
            played = true;
        }

        _speakerBuffer.Clear();

        return played ? id : null;
    }

    /// <summary>
    /// Tone through the speakers, the text in a bubble over each one, and one chat line for everyone in
    /// earshot of a working speaker. False when no speaker could carry it.
    /// </summary>
    public bool Announce(EntityUid grid, string message, SoundSpecifier? sound = null, string? sender = null, Color? color = null)
    {
        if (!Exists(grid))
            return false;

        var chime = sound ?? CompOrNull<ShipAlertComponent>(grid)?.AnnouncementChime ?? DefaultChime;

        if (Broadcast(grid, chime) == null)
            return false;

        Bubble(grid, message, color);

        // DispatchFilteredAnnouncement escapes the text itself when it builds the wrapped message.
        _chat.DispatchFilteredAnnouncement(
            GetListeners(grid),
            message,
            source: grid,
            sender: sender ?? GetShipName(grid),
            playSound: false,
            announcementSound: null,
            colorOverride: color);

        return true;
    }

    /// <summary>
    /// Puts the text in a speech bubble over every working speaker on the grid, for players within its
    /// range. Hidden from the chat log, so several speakers in earshot don't repeat the line there.
    /// </summary>
    public void Bubble(EntityUid grid, string message, Color? color = null)
    {
        if (!Exists(grid))
            return;

        var wrapped = WrapBubble(grid, message, color);

        _speakerBuffer.Clear();
        GatherSpeakers(grid, _speakerBuffer);

        foreach (var speaker in _speakerBuffer)
        {
            if (IsFunctional(speaker))
                SpeakerBubble(speaker, message, wrapped);
        }

        _speakerBuffer.Clear();
    }

    private string WrapBubble(EntityUid grid, string message, Color? color)
    {
        return Loc.GetString("ship-pa-bubble",
            ("sender", Loc.GetString("ship-pa-sender", ("ship", GetShipName(grid)))),
            ("color", (color ?? Color.White).ToHex()),
            ("message", FormattedMessage.EscapeText(message)));
    }

    /// <summary>
    /// One speaker's bubble. Local channel gives the client a say bubble; hideChat keeps it out of the log.
    /// </summary>
    private void SpeakerBubble(Entity<ShipPaSpeakerComponent> speaker, string message, string wrapped)
    {
        var filter = Filter.Empty().AddInRange(_xform.GetMapCoordinates(speaker.Owner), speaker.Comp.Range);

        if (filter.Count == 0)
            return;

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Local, message, wrapped, speaker.Owner, hideChat: true, recordReplay: false, colorOverride: null);
    }

    /// <summary>
    /// Players whose attached entity is within Range of a working speaker on the grid (same map).
    /// </summary>
    public Filter GetListeners(EntityUid grid)
    {
        var filter = Filter.Empty();

        foreach (var speaker in GetSpeakers(grid))
        {
            if (IsFunctional(speaker))
                filter.AddInRange(_xform.GetMapCoordinates(speaker.Owner), speaker.Comp.Range);
        }

        return filter;
    }

    /// <summary>
    /// Every speaker anchored to the grid, working or not.
    /// </summary>
    public List<Entity<ShipPaSpeakerComponent>> GetSpeakers(EntityUid grid)
    {
        var speakers = new List<Entity<ShipPaSpeakerComponent>>();
        GatherSpeakers(grid, speakers);
        return speakers;
    }

    /// <summary>
    /// Anchored, on a grid, not broken, and powered (or has no ApcPowerReceiver).
    /// </summary>
    public bool IsFunctional(Entity<ShipPaSpeakerComponent> speaker)
    {
        if (speaker.Comp.Broken || TerminatingOrDeleted(speaker.Owner))
            return false;

        if (!TryComp(speaker.Owner, out TransformComponent? xform) || !xform.Anchored || xform.GridUid == null)
            return false;

        return IsSpeakerPowered(speaker.Owner);
    }

    /// <summary>
    /// Counts the speakers carrying the ship's PA. Fallback is set when those are air alarms standing in.
    /// </summary>
    public (int Online, int Total, bool Fallback) CountSpeakers(EntityUid grid)
    {
        var online = 0;
        var total = 0;
        var fallback = false;

        foreach (var speaker in GetSpeakers(grid))
        {
            total++;
            fallback |= speaker.Comp.Fallback;

            if (IsFunctional(speaker))
                online++;
        }

        return (online, total, fallback);
    }

    /// <summary>
    /// Recounts speakers into the grid's ShipAlertComponent and dirties it if changed.
    /// </summary>
    public void RefreshCounts(EntityUid grid)
    {
        if (!Exists(grid) || TerminatingOrDeleted(grid))
            return;

        var (online, total, fallback) = CountSpeakers(grid);

        if (!TryComp(grid, out ShipAlertComponent? alert))
        {
            // Every station has air alarms; only a dedicated speaker makes a grid worth tracking on its
            // own. Ships with just air alarms get the component when their console is first opened.
            if (total == 0 || fallback)
                return;

            alert = EnsureComp<ShipAlertComponent>(grid);
        }

        if (alert.SpeakersOnline == online && alert.SpeakersTotal == total && alert.SpeakersFallback == fallback)
            return;

        alert.SpeakersOnline = online;
        alert.SpeakersTotal = total;
        alert.SpeakersFallback = fallback;
        Dirty(grid, alert);
    }

    /// <summary>
    /// The ship's name for announcements, or a stand-in for unnamed hulls.
    /// </summary>
    public string GetShipName(EntityUid grid)
    {
        if (TryComp(grid, out MetaDataComponent? meta) && !string.IsNullOrWhiteSpace(meta.EntityName))
            return meta.EntityName;

        return Loc.GetString("ship-pa-unknown-ship");
    }

    /// <summary>
    /// Fills the list with the speakers carrying the grid's PA: every dedicated speaker anchored to it,
    /// or, when there is none at all, its fallback units (air alarms). No grid-indexed query exists, so
    /// this sweeps the speaker query like ShuttleConsoleSystem does.
    /// </summary>
    private void GatherSpeakers(EntityUid grid, List<Entity<ShipPaSpeakerComponent>> into)
    {
        if (!Exists(grid))
            return;

        var first = into.Count;
        var dedicated = false;
        var query = EntityQueryEnumerator<ShipPaSpeakerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var speaker, out var xform))
        {
            if (!xform.Anchored || xform.GridUid != grid)
                continue;

            dedicated |= !speaker.Fallback;
            into.Add((uid, speaker));
        }

        if (!dedicated)
            return;

        for (var i = into.Count - 1; i >= first; i--)
        {
            if (into[i].Comp.Fallback)
                into.RemoveAt(i);
        }
    }

    /// <summary>
    /// Speaker range, trim and damage distortion folded into the caller's params.
    /// </summary>
    private AudioParams BuildParams(Entity<ShipPaSpeakerComponent> speaker, AudioParams baseParams)
    {
        var result = baseParams
            .WithMaxDistance(speaker.Comp.Range)
            .WithReferenceDistance(ReferenceDistance)
            .WithRolloffFactor(RolloffFactor)
            .AddVolume(speaker.Comp.Volume);

        var distortion = speaker.Comp.Distortion;

        if (distortion > 0f)
            result = result.WithPitchScale(result.Pitch * (1f - 0.15f * distortion)).AddVolume(-6f * distortion);

        return result;
    }

    /// <summary>
    /// Marks an audio entity as one copy of a broadcast so the client mesh can duck the rest.
    /// </summary>
    private void TagStream(EntityUid audio, int broadcastId, EntityUid speaker, float distortion, bool overlay)
    {
        var comp = EnsureComp<ShipPaAudioComponent>(audio);
        comp.BroadcastId = broadcastId;
        comp.Speaker = speaker;
        comp.Distortion = distortion;
        comp.IsOverlay = overlay;
        Dirty(audio, comp);
    }

    private int NextBroadcastId()
    {
        return _nextBroadcastId++;
    }

    /// <summary>
    /// Queues a speaker recount so a burst of power or damage events only costs one sweep.
    /// </summary>
    private void QueueRefresh(EntityUid? grid)
    {
        if (grid is { } uid && uid.IsValid())
            _pendingCounts.Add(uid);
    }

    private void FlushPendingCounts()
    {
        if (_pendingCounts.Count == 0)
            return;

        foreach (var grid in _pendingCounts)
        {
            RefreshCounts(grid);
        }

        _pendingCounts.Clear();
    }
}
