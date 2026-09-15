using Robust.Shared.Prototypes;

namespace Content.Server._WF.PlanetCracker.Flight;

/// <summary>Marks an item as a landing-thruster conversion kit and names what it converts a thruster into.</summary>
[RegisterComponent]
public sealed partial class WFLandingThrusterKitComponent : Component
{
    /// <summary>The landing thruster spawned in the old one's place.</summary>
    [DataField]
    public EntProtoId Variant = "WFThrusterLanding";
}
