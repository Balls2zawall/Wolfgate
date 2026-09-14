using System.Numerics;

namespace Content.Shared._WF.PlanetCracker.Cracker;

/// <summary>Raised broadcast by WFCrackerSystem.SetState after the component is dirtied; never fires on a no-op.</summary>
[ByRefEvent]
public readonly record struct WFCrackStateChangedEvent(EntityUid Cracker, WFCrackState Old, WFCrackState New);

/// <summary>Raised when a crack finishes cutting; the F5 extraction hook, unsubscribed in F4.</summary>
[ByRefEvent]
public readonly record struct WFCrackCompletedEvent(
    EntityUid Cracker,
    EntityUid AnchorA,
    EntityUid AnchorB,
    Vector2 CentreXY,
    float Radius,
    EntityUid GroundMap);

/// <summary>Raised as the hull is pushed into transit, so F5's chunk joins the same fall at a distinct progress (D-K).</summary>
[ByRefEvent]
public readonly record struct WFCrackerFallingEvent(EntityUid Cracker);
