using Robust.Shared.Map;

namespace Content.Server._WF.PlanetCracker.Flight;

/// <summary>
/// A hull grinding out a hard landing: still an ordinary grid, still repairable, but shedding leading-edge tiles and
/// flattening whatever it slides through until the ground friction pass has taken its speed away.
/// </summary>
[RegisterComponent]
public sealed partial class WFSkidComponent : Component
{
    /// <summary>Damage accumulated on each leading-edge tile, keyed by grid index; a tile over the threshold goes.</summary>
    [DataField]
    public Dictionary<Vector2i, float> TileDamage = new();

    /// <summary>The looping scrape, stopped and cleared when the hull comes to rest.</summary>
    [DataField]
    public EntityUid? Loop;

    /// <summary>When the leading edge is next chewed on; the sweep is throttled rather than run every tick.</summary>
    [DataField]
    public TimeSpan NextBite;
}
