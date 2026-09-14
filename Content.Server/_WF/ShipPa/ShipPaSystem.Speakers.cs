using Content.Server.Power.Components;
using Content.Shared._WF.ShipPa;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Power;

namespace Content.Server._WF.ShipPa;

/// <summary>
/// Speaker condition: where a speaker sits, whether it has power, and how wrecked it sounds.
/// </summary>
public sealed partial class ShipPaSystem
{
    /// <summary>Distortion at which an alarm gets a static overlay on top of it.</summary>
    private const float StaticThreshold = 0.2f;

    /// <summary>Distortion at which the speaker visibly reads as damaged.</summary>
    private const float DamagedThreshold = 0.25f;

    private void InitializeSpeakers()
    {
        SubscribeLocalEvent<ShipPaSpeakerComponent, MapInitEvent>(OnSpeakerMapInit);
        SubscribeLocalEvent<ShipPaSpeakerComponent, ComponentShutdown>(OnSpeakerShutdown);
        SubscribeLocalEvent<ShipPaSpeakerComponent, AnchorStateChangedEvent>(OnSpeakerAnchorChanged);
        SubscribeLocalEvent<ShipPaSpeakerComponent, ReAnchorEvent>(OnSpeakerReAnchor);
        SubscribeLocalEvent<ShipPaSpeakerComponent, EntParentChangedMessage>(OnSpeakerParentChanged);
        SubscribeLocalEvent<ShipPaSpeakerComponent, PowerChangedEvent>(OnSpeakerPowerChanged);
        SubscribeLocalEvent<ShipPaSpeakerComponent, DamageChangedEvent>(OnSpeakerDamageChanged);
        SubscribeLocalEvent<ShipPaSpeakerComponent, BreakageEventArgs>(OnSpeakerBreakage);
        SubscribeLocalEvent<ShipPaSpeakerComponent, ExaminedEvent>(OnSpeakerExamined);
    }

    private void OnSpeakerMapInit(Entity<ShipPaSpeakerComponent> ent, ref MapInitEvent args)
    {
        UpdateDamage(ent);
        UpdateAppearance(ent);
        QueueRefresh(GetSpeakerGrid(ent));
    }

    private void OnSpeakerShutdown(Entity<ShipPaSpeakerComponent> ent, ref ComponentShutdown args)
    {
        StopSpeakerStreams(ent);
        QueueRefresh(GetSpeakerGrid(ent));
    }

    private void OnSpeakerAnchorChanged(Entity<ShipPaSpeakerComponent> ent, ref AnchorStateChangedEvent args)
    {
        // An unanchored speaker is off the network, so nothing of its ship's may keep playing.
        if (!args.Anchored)
            StopSpeakerStreams(ent);

        UpdateAppearance(ent);
        QueueRefresh(args.Transform.GridUid);
    }

    private void OnSpeakerReAnchor(Entity<ShipPaSpeakerComponent> ent, ref ReAnchorEvent args)
    {
        StopSpeakerStreams(ent);
        QueueRefresh(args.OldGrid);
        QueueRefresh(args.Grid);
    }

    private void OnSpeakerParentChanged(Entity<ShipPaSpeakerComponent> ent, ref EntParentChangedMessage args)
    {
        StopSpeakerStreams(ent);
        QueueRefresh(args.OldParent);
        QueueRefresh(args.Transform.GridUid);
    }

    private void OnSpeakerPowerChanged(Entity<ShipPaSpeakerComponent> ent, ref PowerChangedEvent args)
    {
        // Coming back on is handled by the alarm reconcile, which slots it into whatever is running.
        if (!args.Powered)
            StopSpeakerStreams(ent);

        UpdateAppearance(ent);
        QueueRefresh(GetSpeakerGrid(ent));
    }

    private void OnSpeakerDamageChanged(EntityUid uid, ShipPaSpeakerComponent comp, DamageChangedEvent args)
    {
        UpdateDamage((uid, comp));
    }

    private void OnSpeakerBreakage(EntityUid uid, ShipPaSpeakerComponent comp, BreakageEventArgs args)
    {
        if (comp.Broken)
            return;

        comp.Broken = true;
        Dirty(uid, comp);

        StopSpeakerStreams(uid);
        UpdateAppearance((uid, comp));
        QueueRefresh(GetSpeakerGrid(uid));
    }

    private void OnSpeakerExamined(Entity<ShipPaSpeakerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Fallback)
            return;

        var key = ent.Comp.Broken
            ? "ship-pa-speaker-examine-broken"
            : !IsSpeakerPowered(ent)
                ? "ship-pa-speaker-examine-unpowered"
                : ent.Comp.Distortion > 0f
                    ? "ship-pa-speaker-examine-damaged"
                    : "ship-pa-speaker-examine-ok";

        args.PushMarkup(Loc.GetString(key));
    }

    /// <summary>
    /// True when the speaker is getting power, or doesn't want any.
    /// </summary>
    private bool IsSpeakerPowered(EntityUid uid)
    {
        return !TryComp(uid, out ApcPowerReceiverComponent? receiver) || receiver.Powered;
    }

    /// <summary>
    /// Recomputes distortion from damage and clears breakage once the speaker is whole again.
    /// Any repair path ends with the damage set to zero, so this covers welding and field repair alike.
    /// </summary>
    private void UpdateDamage(Entity<ShipPaSpeakerComponent> ent)
    {
        var total = TryComp(ent.Owner, out DamageableComponent? damageable) ? damageable.TotalDamage : FixedPoint2.Zero;
        var limit = ent.Comp.DistortionDamage;
        var distortion = limit <= FixedPoint2.Zero ? 0f : Math.Clamp(total.Float() / limit.Float(), 0f, 1f);

        var changed = false;

        if (Math.Abs(ent.Comp.Distortion - distortion) > 0.001f)
        {
            ent.Comp.Distortion = distortion;
            changed = true;
        }

        if (ent.Comp.Broken && total <= FixedPoint2.Zero)
        {
            ent.Comp.Broken = false;
            changed = true;
            QueueRefresh(GetSpeakerGrid(ent));
        }

        if (!changed)
            return;

        Dirty(ent);
        UpdateAppearance(ent);
    }

    /// <summary>
    /// Pushes the speaker's current state to its sprite.
    /// </summary>
    private void UpdateAppearance(Entity<ShipPaSpeakerComponent> ent)
    {
        if (!TryComp(ent.Owner, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(ent.Owner, ShipPaSpeakerVisuals.State, GetVisualState(ent), appearance);
    }

    private ShipPaSpeakerState GetVisualState(Entity<ShipPaSpeakerComponent> ent)
    {
        if (ent.Comp.Broken)
            return ShipPaSpeakerState.Broken;

        if (!IsFunctional(ent))
            return ShipPaSpeakerState.Unpowered;

        if (ent.Comp.BroadcastingUntil is { } until && _timing.CurTime < until)
            return ShipPaSpeakerState.Broadcasting;

        if (ent.Comp.Distortion >= DamagedThreshold)
            return ShipPaSpeakerState.Damaged;

        return ShipPaSpeakerState.Idle;
    }

    /// <summary>
    /// Drops the broadcasting light once a one-shot has finished playing.
    /// </summary>
    private void ClearExpiredBroadcasts()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShipPaSpeakerComponent>();

        while (query.MoveNext(out var uid, out var speaker))
        {
            if (speaker.BroadcastingUntil is not { } until || now < until)
                continue;

            speaker.BroadcastingUntil = null;
            UpdateAppearance((uid, speaker));
        }
    }

    private EntityUid? GetSpeakerGrid(EntityUid uid)
    {
        return TryComp(uid, out TransformComponent? xform) ? xform.GridUid : null;
    }
}
