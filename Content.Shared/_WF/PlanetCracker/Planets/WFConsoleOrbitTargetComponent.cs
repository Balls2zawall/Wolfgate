using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Planets;

/// <summary>
/// What a shuttle console currently offers for planet orbit, refreshed server-side on its own sweep.
/// It rides the console entity rather than the shuttle BUI state because the BUI state is only pushed on docking,
/// beacon and power events, so a button keyed to it would go stale the moment the hull started moving.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class WFConsoleOrbitTargetComponent : Component
{
    /// <summary>The sector body whose orbit this hull can enter right now, or null when none is in range.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Planet;

    /// <summary>Display name of <see cref="Planet"/>, so the button labels itself without resolving the body.</summary>
    [DataField, AutoNetworkedField]
    public string PlanetName = string.Empty;

    /// <summary>True when the hull is parked on a planet orbit layer, which is what offers "leave orbit" instead.</summary>
    [DataField, AutoNetworkedField]
    public bool InOrbit;

    /// <summary>True while the hull cannot make the hop at all: already in FTL, in cooldown or riding a transit map.</summary>
    [DataField, AutoNetworkedField]
    public bool Busy;

    /// <summary>
    /// Pooled lift over weight for this hull on the planet below, computed server-side; 1 is level flight. Only
    /// meaningful while <see cref="InOrbit"/>, which is the one place the descent decision is taken.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LiftRatio;

    /// <summary>
    /// Seconds left before this hull's orbit decays and it is dropped into the atmosphere, or -1 while it is holding
    /// station. Only meaningful while <see cref="InOrbit"/>; nothing else in the stack can decay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DecaySeconds = -1f;
}
