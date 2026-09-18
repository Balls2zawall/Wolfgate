using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.PlanetCracker.Planets;

/// <summary>Registers a biome encounter site with the bounded planet population system.</summary>
[RegisterComponent]
public sealed partial class WFPlanetFaunaSpawnerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Table;
}