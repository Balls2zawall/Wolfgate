using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.Shared._WF.Wolfmed.Compat;

/// <summary>Onyx-shaped body helpers mapped onto Wolfgate's Shitmed body system.</summary>
public sealed class WolfmedBodySystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>Detaches a part from its parent slot and drops it, mirroring Onyx's TryDetachPart. `reparent` is ignored.</summary>
    public bool TryDetachPart(EntityUid part, bool reparent = true)
    {
        if (_body.GetParentPartAndSlotOrNull(part) is not { } parentSlot
            || !HasComp<BodyPartComponent>(part)
            || !_body.CanDetachPart(parentSlot.Parent, parentSlot.Slot, part))
            return false;

        // DropPart is protected; the amputate event is the public door to it, and it also drops held
        // items and raises BodyPartDroppedEvent, which Wolfgate appearance/cybernetics/targeting listen for.
        var ev = new AmputateAttemptEvent(part);
        RaiseLocalEvent(part, ref ev);
        return _body.GetParentPartOrNull(part) is null;
    }
}
