using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Planets;

/// <summary>
/// The top map of a planet network: vacuum, no terrain, the only FTL door in or out, and grids parked here never fall.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class WFOrbitLayerComponent : Component
{
    /// <summary>The sector body this layer orbits, used by the inbound FTL range gate.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Planet;

    /// <summary>How close to the sector body a shuttle must be to FTL into this layer.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 2000f;

    /// <summary>The z-network entity this layer belongs to.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Network;
}
