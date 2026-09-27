using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.Wounds;
using Content.Shared.Body.Part;

namespace Content.Server._WF.Wolfmed.Wounds;

/// <summary>
/// Tags each dismemberment wound with the part it is the stump of (<see cref="WolfmedStumpComponent"/>), off the
/// amputation event, so the artery overlay can tell a neck from a shoulder on the same torso.
/// </summary>
public sealed class WolfmedStumpTagSystem : EntitySystem
{
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WolfmedPartAmputatedEvent>(OnAmputated);
    }

    private void OnAmputated(ref WolfmedPartAmputatedEvent args)
    {
        if (!TryComp(args.Part, out BodyPartComponent? part) || !TryComp(args.Body, out WoundHostComponent? host))
            return;

        // The stump wound is a separate instance per amputation and the newest untagged one on the parent is this one.
        EntityUid? newest = null;
        foreach (var wound in _wounds.GetWounds(args.Parent))
        {
            if (wound.Comp.Prototype == host.DismembermentWound && !HasComp<WolfmedStumpComponent>(wound))
                newest = wound;
        }

        if (newest is not { } stump)
            return;

        var tag = EnsureComp<WolfmedStumpComponent>(stump);
        tag.PartType = part.PartType;
        tag.Symmetry = part.Symmetry;
    }
}
