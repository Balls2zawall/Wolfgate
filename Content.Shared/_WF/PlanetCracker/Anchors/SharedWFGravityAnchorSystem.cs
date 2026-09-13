namespace Content.Shared._WF.PlanetCracker.Anchors;

/// <summary>Shared maths for gravity anchor pairs; declares no subscriptions, the server half owns the behaviour.</summary>
public abstract partial class SharedWFGravityAnchorSystem : EntitySystem
{
    /// <summary>Cut radius for a pair, per design D21: half the centre distance plus the padding.</summary>
    public static float GetCutRadius(float distance, float padding) => distance / 2f + padding;

    /// <summary>True when the two centres are inside the pairing band.</summary>
    public static bool InBand(float distance, float min, float max) => distance >= min && distance <= max;

    /// <summary>True for states where the anchor is drilled in and must not be unwrenched.</summary>
    // Off is armed and has no outgoing player transition in F3, so an Off anchor is stuck until it breaks or is
    // destroyed; wf-anchor-locked-unwrench tells such a player to switch it off, which it already is. F7's re-arm
    // or cut-out verb is what recovers it, and is where that refusal line should get its own state-aware wording.
    public static bool IsArmed(WFAnchorState s) => s is WFAnchorState.Drilling or WFAnchorState.Locked or WFAnchorState.Off;
}
