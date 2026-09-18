using Content.Server.Ghost.Roles.Components;
using Content.Shared.EntityTable;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.PlanetCracker.Planets;

/// <summary>Bounds ambient wildlife independently of the existing fissure-wave budgets.</summary>
public sealed partial class WFPlanetFaunaSystem : EntitySystem
{
    public const int MaxPerPlanet = 32;
    public const int MaxTotal = 128;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityTableSystem _tables = default!;

    // Count captured/off-world animals too: carrying animals away must not bypass the global cap.
    private readonly Dictionary<EntityUid, EntityUid> _living = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WFPlanetFaunaSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<WFPlanetFaunaSpawnerComponent> ent, ref MapInitEvent args)
    {
        // Always consume the site, including when capped. Revisiting terrain must not roll again.
        QueueDel(ent);
        if (Transform(ent).MapUid is not { } ground || !HasComp<BiomeComponent>(ground))
            return;

        _expired.Clear();
        foreach (var (uid, _) in _living)
        {
            if (TerminatingOrDeleted(uid) ||
                !TryComp<MobStateComponent>(uid, out var state) || state.CurrentState == MobState.Dead)
                _expired.Add(uid);
        }
        foreach (var uid in _expired)
            _living.Remove(uid);

        var local = 0;
        foreach (var origin in _living.Values)
        {
            if (origin == ground)
                local++;
        }
        if (local >= MaxPerPlanet || _living.Count >= MaxTotal)
            return;

        foreach (var prototype in _tables.GetSpawns(_proto.Index(ent.Comp.Table).Table))
        {
            if (local >= MaxPerPlanet || _living.Count >= MaxTotal)
                break;
            var uid = EntityManager.CreateEntityUninitialized(prototype, Transform(ent).Coordinates);
            // Ambient generation must not populate the ghost-role menu.
            RemComp<GhostRoleComponent>(uid);
            RemComp<GhostTakeoverAvailableComponent>(uid);
            EntityManager.InitializeAndStartEntity(uid);
            if (!HasComp<MobStateComponent>(uid))
            {
                QueueDel(uid);
                continue;
            }
            _living.Add(uid, ground);
            local++;
        }
    }
}