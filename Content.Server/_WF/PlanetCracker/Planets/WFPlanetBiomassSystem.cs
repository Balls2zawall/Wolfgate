using Content.Server.Spreader;
using Content.Shared._WF.PlanetCracker.Chunk;
using Content.Shared._WF.PlanetCracker.Planets;

namespace Content.Server._WF.PlanetCracker.Planets;

/// <summary>Chimera biomass must not colonize streamed planets or extracted chunks.</summary>
[RegisterComponent]
public sealed partial class WFPlanetBiomassRestrictionComponent : Component;

public sealed partial class WFPlanetBiomassSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<WFPlanetBiomassRestrictionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WFPlanetBiomassRestrictionComponent, EntParentChangedMessage>(OnParentChanged);
    }

    public bool IsPlanet(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return false;
        return HasComp<WFPlanetLayerComponent>(uid) || HasComp<WFPlanetChunkComponent>(uid) ||
            HasComp<WFPlanetLayerComponent>(xform.MapUid) || HasComp<WFPlanetChunkComponent>(xform.GridUid);
    }

    private void OnMapInit(Entity<WFPlanetBiomassRestrictionComponent> ent, ref MapInitEvent args) => Suppress(ent);
    private void OnParentChanged(Entity<WFPlanetBiomassRestrictionComponent> ent, ref EntParentChangedMessage args) => Suppress(ent);

    private void Suppress(EntityUid uid)
    {
        if (!IsPlanet(uid))
            return;
        // Queue deletion before the next spread tick; no extra fixtures or persistent floor objects.
        QueueDel(uid);
    }
}
