using Content.Shared._DV.Planet;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._WF.PlanetCracker.Planets;

/// <summary>
/// Describes the whole z-stack of one sector planet: its ground biome, how many air layers sit above it, and its orbit layer.
/// </summary>
/// <remarks>
/// The prototype kind string is declared explicitly. Robust derives an unqualified kind by lowercasing only index 0,
/// which would register this type as "wFPlanetSurface" and break every "- type: wfPlanetSurface" document.
/// </remarks>
[Prototype("wfPlanetSurface")]
public sealed partial class WFPlanetSurfacePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The sector body type this surface belongs to; the registry matches spawned planets by this id.</summary>
    [DataField(required: true)]
    public ProtoId<PlanetTypePrototype> PlanetType;

    /// <summary>The planet prototype the ground layer is generated from.</summary>
    [DataField(required: true)]
    public ProtoId<PlanetPrototype> Ground;

    /// <summary>Optional hand-made grid loaded onto the ground layer at the planet centre.</summary>
    [DataField]
    public ResPath? GroundGrid;

    /// <summary>How many bare fall-through air layers sit between the ground and the cloud layer.</summary>
    [DataField]
    public int AirLayers = 2;

    /// <summary>Whether a cloud layer sits between the topmost air layer and orbit.</summary>
    [DataField]
    public bool CloudLayer = true;

    /// <summary>How close to the sector planet a shuttle must be to enter orbit.</summary>
    [DataField]
    public float OrbitRange = 2000f;

    /// <summary>Whether the network is built eagerly when the sector body spawns at round start.</summary>
    [DataField]
    public bool BuildAtRoundStart;

    /// <summary>Components stamped on every member map by the z-network registry, transit maps included.</summary>
    [DataField]
    public ComponentRegistry? NetworkComponents;

    /// <summary>Components added to the ground layer after the network is initialised.</summary>
    [DataField]
    public ComponentRegistry? GroundComponents;

    /// <summary>Components added to each air layer after the network is initialised.</summary>
    [DataField]
    public ComponentRegistry? AirComponents;

    /// <summary>Components added to the cloud layer after the network is initialised.</summary>
    [DataField]
    public ComponentRegistry? CloudComponents;

    /// <summary>Components added to the orbit layer after the network is initialised.</summary>
    [DataField]
    public ComponentRegistry? OrbitComponents;

    /// <summary>Name given to the orbit map entity; the shuttle console uses it as the destination heading.</summary>
    [DataField]
    public LocId OrbitMapName = "wf-planet-orbit-map-name";

    /// <summary>Name given to the orbit beacon entity.</summary>
    [DataField]
    public LocId OrbitBeaconName = "wf-planet-orbit-beacon-name";
}
