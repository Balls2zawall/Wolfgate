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

    /// <summary>
    /// Surface gravity of the world this layer belongs to, in gees, copied off its wfPlanetSurface at build time. It
    /// divides a hull's landing-thruster lift, so a heavy world needs proportionally more of it to fly at all.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Gravity = 1f;
}
