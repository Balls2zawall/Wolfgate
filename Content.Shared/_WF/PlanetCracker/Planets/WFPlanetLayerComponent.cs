using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Planets;

/// <summary>
/// Marks a map as one layer of a planet network, so the outbound FTL gate only applies to planets and never to a station z-network.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class WFPlanetLayerComponent : Component
{
    /// <summary>The z-network entity this layer belongs to.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Network;
}
