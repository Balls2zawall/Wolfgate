using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.PlanetCracker.Planets;

/// <summary>One-shot biome wildlife spawner governed by the planet population budget.</summary>
[RegisterComponent]
public sealed partial class WFPlanetFaunaSpawnerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Table;
}