using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    /// <summary>Names of the distinct ships currently applying an active tractor beam to this shuttle.</summary>
    public string[] TractorSources;

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState,
        string[]? tractorSources = null)
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        TractorSources = tractorSources ?? Array.Empty<string>();
    }
}
