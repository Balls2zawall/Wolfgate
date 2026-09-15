using Content.Shared._WF.PlanetCracker.Flight;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Shuttles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.PlanetCracker.Flight;

/// <summary>
/// The landing conversion kit: used on an anchored ordinary thruster it swaps the whole unit for the landing variant
/// in place. A swap rather than a bolted-on component because a thruster only ever registers its thrust with the hull
/// at initialisation (ThrusterSystem.EnableThruster), so the replacement has to be a fresh entity either way.
/// </summary>
public sealed partial class WFLandingThrusterKitSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WFLandingThrusterKitComponent, AfterInteractEvent>(OnAfterInteract);
    }

    /// <summary>Converts the thruster the kit was used on, consuming the kit.</summary>
    private void OnAfterInteract(Entity<WFLandingThrusterKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!HasComp<ThrusterComponent>(target) || HasComp<WFLandingThrusterComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("wf-landing-kit-wrong-target"), ent, args.User);
            return;
        }

        var xform = Transform(target);

        if (!xform.Anchored)
        {
            _popup.PopupEntity(Loc.GetString("wf-landing-kit-not-anchored"), ent, args.User);
            return;
        }

        var coords = xform.Coordinates;
        var rotation = xform.LocalRotation;

        // Deleted outright rather than queued: the replacement anchors onto the same tile in this very call, and the
        // engine's snap-grid cell asserts on a second anchored entity while the old one is still queued.
        Del(target);

        var converted = Spawn(ent.Comp.Variant, coords);
        _transform.SetLocalRotation(converted, rotation);

        // The thruster prototype maps in anchored, so only a variant that somehow did not needs this; anchoring an
        // already-anchored entity asserts on the snap-grid cell it is already in.
        if (!Transform(converted).Anchored)
            _transform.AnchorEntity(converted);

        QueueDel(ent.Owner);

        _popup.PopupEntity(Loc.GetString("wf-landing-kit-converted"), converted, args.User);
    }
}
