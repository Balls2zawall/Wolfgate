using Content.Shared._Onyx.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.Server._WF.Wolfmed;

/// <summary>Drives Onyx's part-lifecycle entry points from Wolfgate's body-scoped part events.</summary>
public sealed class WolfmedBodyPartLifecycleSystem : EntitySystem
{
    [Dependency] private WoundDamageProjectionSystem _projection = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private SharedBodySystem _body = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        // WOLFGATE (D28): <BodyComponent, BodyPart*Event> is owned by Shitmed's
        // SharedBodySystem.PartAppearance.cs:25-26. WoundHostComponent sits on the same entity
        // and scopes the handler to wound hosts, which is what we want anyway.
        SubscribeLocalEvent<WoundHostComponent, BodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<WoundHostComponent, BodyPartRemovedEvent>(OnPartRemoved);
    }

    /// <summary>Initialises wounds on a newly attached limb and its whole subtree.</summary>
    private void OnPartAdded(Entity<WoundHostComponent> body, ref BodyPartAddedEvent args)
    {
        // Deleting a mob detaches every part on the way down, so this fires mid-termination. Onyx has no
        // caller for these entry points at all; the guard belongs here rather than in the vendored file.
        if (TerminatingOrDeleted(body) || TerminatingOrDeleted(args.Part.Owner))
            return;

        // Onyx initialises every part of the attached subtree, not just the root it was handed.
        foreach (var (part, _) in _body.GetBodyPartChildren(args.Part.Owner, args.Part.Comp))
        {
            _projection.OnPartInserted(part, body);
            // Onyx's BodyInventorySlotSystem:37-39 drives both of these; that system is Nubody glue D8 skips,
            // so without this line a re-attached limb's wounds never rejoin the body's bleed total.
            _bleeding.OnPartInserted(part, body);

            var inserted = new OrganGotInsertedEvent(body);
            RaiseLocalEvent(part, ref inserted);
        }
    }

    /// <summary>Re-projects the body and the detached limb when a limb comes off.</summary>
    private void OnPartRemoved(Entity<WoundHostComponent> body, ref BodyPartRemovedEvent args)
    {
        // Same guard as OnPartAdded: RecursiveDeleteEntity detaches every part while the mob terminates, and
        // RefreshDetachedDamage's EnsureComp<PartDamageVisualsComponent> throws on a terminating entity.
        if (TerminatingOrDeleted(body) || TerminatingOrDeleted(args.Part.Owner))
            return;

        _projection.OnPartRemoved(args.Part.Owner, body);
        // Onyx's BodyInventorySlotSystem:49 does the same: the detached limb's wounds must leave the body's
        // bleed total, or BloodstreamComponent.BleedAmount keeps bleeding for a limb that is on the floor.
        _bleeding.OnPartChanged(body);

        foreach (var (part, _) in _body.GetBodyPartChildren(args.Part.Owner, args.Part.Comp))
        {
            var removed = new OrganGotRemovedEvent(body);
            RaiseLocalEvent(part, ref removed);
        }
    }
}
