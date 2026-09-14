using System.Numerics;
using Content.Shared._WF.PlanetCracker.Anchors;
using Robust.Shared.Map;

namespace Content.Shared._WF.PlanetCracker.Cracker;

/// <summary>
/// The one derivation of cracker geometry, shared so the server state builder, the client diagrams, the radar ghost
/// and F5's chunk placement all agree. Declares no subscriptions; the server half owns the behaviour.
/// This type is abstract, so ReflectionManager.GetAllChildren skips it and it is resolvable only through a concrete
/// subclass in each assembly - hence a WFCrackerSystem on the server AND on the client.
/// </summary>
public abstract partial class SharedWFCrackerSystem : EntitySystem
{
    [Dependency] protected SharedTransformSystem TransformSystem = default!;

    /// <summary>World centre of the hull's chunk berth, Distance tiles out along the marker's own facing.</summary>
    public bool TryGetBerthCentre(Entity<WFPlanetCrackerComponent> ent, out MapCoordinates centre)
    {
        centre = MapCoordinates.Nullspace;

        if (ent.Comp.Berth is not { } netBerth || !TryGetEntity(netBerth, out var berth))
            return false;

        if (!TryComp<WFChunkBerthComponent>(berth, out var berthComp))
            return false;

        var xform = Transform(berth.Value);

        if (xform.MapID == MapId.Nullspace)
            return false;

        var (position, rotation) = TransformSystem.GetWorldPositionRotation(xform);

        // Angle.Zero is Direction.South in Robust, so a marker facing north sits at 180 degrees.
        centre = new MapCoordinates(position + rotation.ToWorldVec() * berthComp.Distance, xform.MapID);
        return true;
    }

    /// <summary>World rectangle of the hull's chunk berth: the berth centre, the marker's Size and its world rotation.</summary>
    public bool TryGetBerthRect(Entity<WFPlanetCrackerComponent> ent, out Box2Rotated rect)
    {
        rect = default;

        if (!TryGetBerthCentre(ent, out var centre))
            return false;

        if (ent.Comp.Berth is not { } netBerth || !TryGetEntity(netBerth, out var berth))
            return false;

        if (!TryComp<WFChunkBerthComponent>(berth, out var berthComp))
            return false;

        var rotation = TransformSystem.GetWorldRotation(berth.Value);

        rect = new Box2Rotated(Box2.CenteredAround(centre.Position, berthComp.Size), rotation, centre.Position);
        return true;
    }

    /// <summary>Cut circle of a pair: the midpoint as a raw world XY and the design D21 radius.</summary>
    public bool TryGetCircle(EntityUid a, EntityUid b, out Vector2 centreXY, out float radius)
    {
        centreXY = Vector2.Zero;
        radius = 0f;

        if (!TryComp<WFGravityAnchorComponent>(a, out var anchorA) || !HasComp<WFGravityAnchorComponent>(b))
            return false;

        var posA = TransformSystem.GetWorldPosition(a);
        var posB = TransformSystem.GetWorldPosition(b);

        centreXY = (posA + posB) / 2f;
        radius = SharedWFGravityAnchorSystem.GetCutRadius((posA - posB).Length(), anchorA.CutPadding);
        return true;
    }

    /// <summary>
    /// How far the berth centre is from the cut circle centre, as a raw XY delta.
    /// The berth carries the orbit map id and the circle the ground one, and MapCoordinates.InRange returns false for
    /// differing MapIds before any distance maths, so plain subtraction of the positions is the only correct route.
    /// CE z-layers share world XY, which is what makes it correct.
    /// </summary>
    public bool TryGetBerthOffset(Entity<WFPlanetCrackerComponent> ent, EntityUid a, EntityUid b, out Vector2 offset)
    {
        offset = Vector2.Zero;

        if (!TryGetBerthCentre(ent, out var centre))
            return false;

        if (!TryGetCircle(a, b, out var circleCentre, out _))
            return false;

        offset = circleCentre - centre.Position;
        return true;
    }

    /// <summary>
    /// Crack duration: the base time scaled by pair distance against the reference distance, then by the part
    /// multiplier. The multiplier is the arithmetic MEAN of both projectors' CrackTimeMultiplier, not the minimum
    /// (which would make a single-projector upgrade worth nothing) and not the product (which undershoots the design's
    /// 5.6 minute floor). Mean and minimum agree at every uniform part tier and differ only on a mixed pair.
    /// </summary>
    public static TimeSpan GetCrackDuration(float distance, float partMultiplier, TimeSpan baseTime, float referenceDistance)
    {
        if (referenceDistance <= 0f)
            return baseTime * partMultiplier;

        return baseTime * (distance / referenceDistance) * partMultiplier;
    }
}
