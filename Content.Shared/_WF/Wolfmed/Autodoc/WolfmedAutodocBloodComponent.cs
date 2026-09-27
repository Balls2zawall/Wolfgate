namespace Content.Shared._WF.Wolfmed.Autodoc;

/// <summary>
/// The pod's blood reservoir: one slot for a Bloodpack stack, which the planner's transfusion step feeds into a low
/// occupant as their own blood at a steady rate, the way an IV drip does.
/// </summary>
[RegisterComponent]
public sealed partial class WolfmedAutodocBloodComponent : Component
{
    public const string SlotId = "autodoc_blood";

    // Runtime state, written on the server only.

    /// <summary>A transfusion out of the reservoir is under way.</summary>
    [ViewVariables]
    public bool Active;

    /// <summary>The occupant the transfusion or the fault belongs to; a new one starts from nothing.</summary>
    [ViewVariables]
    public EntityUid? Patient;

    /// <summary>The occupant needs blood and there is none loaded: "NO BLOOD LOADED".</summary>
    [ViewVariables]
    public bool NoBlood;

    /// <summary>The NO BLOOD LOADED line has been said for this occupant and this empty slot.</summary>
    [ViewVariables]
    public bool NoBloodSaid;

    /// <summary>The stack <see cref="PackUsed"/> belongs to; another stack starts from a fresh pack.</summary>
    [ViewVariables]
    public EntityUid? Pack;

    /// <summary>Units already given from the top pack of the loaded stack.</summary>
    [ViewVariables]
    public float PackUsed;

    [ViewVariables]
    public TimeSpan NextUpdate;
}
