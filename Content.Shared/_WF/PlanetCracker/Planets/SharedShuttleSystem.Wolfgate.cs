using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WF.PlanetCracker.Planets;

namespace Content.Shared.Shuttles.Systems;

public abstract partial class SharedShuttleSystem
{
    /// <summary>The orbit layer is the only FTL door in or out of a planet network, and only from within range of the sector planet.</summary>
    protected bool WfAllowFTL(EntityUid shuttleUid, EntityUid targetMapUid)
    {
        var shuttleXform = _xformQuery.GetComponent(shuttleUid);

        // Outbound: never from mid-transit, and never off a surface, air or cloud layer - you climb to orbit first.
        if (shuttleXform.MapUid is { } shuttleMapUid)
        {
            if (HasComp<CEZTransitMapComponent>(shuttleMapUid))
                return false;

            if (HasComp<WFPlanetLayerComponent>(shuttleMapUid) && !HasComp<WFOrbitLayerComponent>(shuttleMapUid))
                return false;
        }

        // Inbound: an orbit layer is only reachable from within range of its own sector planet.
        if (!TryComp<WFOrbitLayerComponent>(targetMapUid, out var orbit))
            return true;

        // A network with no sector body (a dev spawn) has nothing to measure against, so it stays open.
        if (orbit.Planet is not { } netPlanet)
            return true;

        if (!TryGetEntity(netPlanet, out var planet))
            return false;

        var planetXform = _xformQuery.GetComponent(planet.Value);

        if (planetXform.MapUid != shuttleXform.MapUid)
            return false;

        var delta = XformSystem.GetWorldPosition(planetXform) - XformSystem.GetWorldPosition(shuttleXform);
        return delta.LengthSquared() <= orbit.Range * orbit.Range;
    }
}
