using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared.Examine;
using Robust.Shared.Map;

namespace Content.Server._WF.PlanetCracker.Cracker;

/// <summary>
/// F1 skeleton of the cracker hull: it holds the crack state and resolves the mapper-placed chunk berth.
/// </summary>
public sealed partial class WFCrackerSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WFPlanetCrackerComponent, MapInitEvent>(OnCrackerMapInit);
        SubscribeLocalEvent<WFChunkBerthComponent, MapInitEvent>(OnBerthMapInit);
        SubscribeLocalEvent<WFChunkBerthComponent, ExaminedEvent>(OnBerthExamined);
    }

    /// <summary>A freshly loaded hull starts idle and looks for the berth marker its mapper placed on it.</summary>
    private void OnCrackerMapInit(Entity<WFPlanetCrackerComponent> ent, ref MapInitEvent args)
    {
        SetState(ent, WFCrackState.Idle);

        if (ent.Comp.Berth is null)
        {
            var query = EntityQueryEnumerator<WFChunkBerthComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.GridUid != ent.Owner)
                    continue;

                ent.Comp.Berth = GetNetEntity(uid);
                break;
            }
        }

        Dirty(ent);
    }

    /// <summary>Back-link for the other map-init order: a berth that initialises after its hull still registers itself.</summary>
    private void OnBerthMapInit(Entity<WFChunkBerthComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent.Owner).GridUid is not { } grid)
            return;

        if (!TryComp<WFPlanetCrackerComponent>(grid, out var cracker) || cracker.Berth is not null)
            return;

        cracker.Berth = GetNetEntity(ent.Owner);
        Dirty(grid, cracker);
    }

    /// <summary>Tells a mapper how big the berth is and how far out it sits.</summary>
    private void OnBerthExamined(Entity<WFChunkBerthComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("wf-berth-examine",
            ("width", ent.Comp.Size.X),
            ("height", ent.Comp.Size.Y),
            ("distance", MathF.Round(ent.Comp.Distance, 1))));
    }

    /// <summary>Moves a cracker to a new crack stage; the only writer of the state field.</summary>
    public void SetState(Entity<WFPlanetCrackerComponent> ent, WFCrackState state)
    {
        if (ent.Comp.State == state)
            return;

        ent.Comp.State = state;
        Dirty(ent);
    }

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

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);

        // Angle.Zero is Direction.South in Robust, so a marker facing north sits at 180 degrees.
        centre = new MapCoordinates(position + rotation.ToWorldVec() * berthComp.Distance, xform.MapID);
        return true;
    }
}
