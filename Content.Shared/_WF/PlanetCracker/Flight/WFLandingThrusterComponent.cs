using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Flight;

/// <summary>
/// A thruster that also holds its hull up against a planet's gravity. On a planet layer a gravity generator is no
/// lift at all, so this is the only thing that keeps a ship in the air there; the planar thrust is unchanged.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WFLandingThrusterComponent : Component
{
    /// <summary>
    /// Lift added to the hull's pooled capacity while the thruster is enabled, powered and intact, in the same units
    /// as a gravity generator's MaxHandledMass - a hull's FixturesMass plus whatever cargo weighs against it.
    /// </summary>
    [DataField]
    public float LiftThrust = 50f;
}
