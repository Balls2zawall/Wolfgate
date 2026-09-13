using Robust.Shared.GameStates;

namespace Content.Shared._WF.PlanetCracker.Cracker;

/// <summary>Marks the ship's gravitic centrifuge so F4 finds it without a gravity generator query.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WFCentrifugeComponent : Component;
