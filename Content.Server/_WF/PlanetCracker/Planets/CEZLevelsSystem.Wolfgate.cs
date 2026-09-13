using Content.Shared._WF.PlanetCracker.Planets;

namespace Content.Server._CE.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>True when this map is a planet orbit layer, where parked grids never fall.</summary>
    private bool WfIsOrbitLayer(EntityUid mapUid) => HasComp<WFOrbitLayerComponent>(mapUid);
}
