using Content.Shared._WF.PlanetCracker.Anchors;

namespace Content.Server._WF.PlanetCracker.Anchors;

/// <summary>
/// The public surface the admin command drives the anchors through, so it reaches the private state writer without
/// duplicating its guards or its side effects. No subscriptions.
/// </summary>
public sealed partial class WFGravityAnchorSystem
{
    /// <summary>
    /// Finishes a drill at once: the same lock, thunk and event the 1 Hz sweep would have raised when the timer ran out.
    /// </summary>
    public bool CompleteDrill(Entity<WFGravityAnchorComponent> ent)
    {
        if (ent.Comp.State != WFAnchorState.Drilling && ent.Comp.State != WFAnchorState.Paired)
            return false;

        // Pulled forward so the sweep cannot fire a second WFAnchorDrillFinishedEvent for the same drill.
        ent.Comp.DrillEnd = _timing.CurTime;

        SetState(ent, WFAnchorState.Locked);
        _audio.PlayPvs(LockSound, ent.Owner);

        var ev = new WFAnchorDrillFinishedEvent(ent.Owner);
        RaiseLocalEvent(ref ev);
        return true;
    }

    /// <summary>
    /// Switches a locked anchor off without raising the cancellable attempt, so a later veto cannot refuse an admin.
    /// </summary>
    public bool ForceSwitchOff(Entity<WFGravityAnchorComponent> ent)
    {
        if (ent.Comp.State != WFAnchorState.Locked)
            return false;

        SetState(ent, WFAnchorState.Off);

        var ev = new WFAnchorSwitchedOffEvent(ent.Owner);
        RaiseLocalEvent(ref ev);
        return true;
    }
}
