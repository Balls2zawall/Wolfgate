using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Cracker;

/// <summary>Marks a grid as a planet cracker hull and carries the state of its current crack.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class WFPlanetCrackerComponent : Component
{
    /// <summary>Current stage of the crack, per design section 3.</summary>
    [DataField, AutoNetworkedField]
    public WFCrackState State = WFCrackState.Idle;

    /// <summary>The mapper-placed berth marker on this hull, resolved at map init.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Berth;

    /// <summary>The first half of the targeted pair, once F4 exists.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? AnchorA;

    /// <summary>The second half of the targeted pair, once F4 exists.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? AnchorB;
}
