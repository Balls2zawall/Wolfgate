using Content.Shared.Maps;
using Content.Shared.Mining;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.PlanetCracker.Survey;

/// <summary>
/// One hidden deep vein, spawned by the ground biome's flat marker layer and stamped deterministically at MapInit.
/// The sprite starts invisible and is flipped on client-side per player from <see cref="WFSurveyedComponent"/>, so the
/// vein is networked to everyone in range but drawn and examinable only for players who have pulsed it.
/// </summary>
/// <remarks>
/// Plain AutoGenerateComponentState with no argument is correct here: nothing subscribes AfterAutoHandleStateEvent on
/// this component - the client reacts to ComponentStartup instead - and the analyzer only errors when such a
/// subscription exists.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class WFDeepVeinComponent : Component
{
    /// <summary>The ore this vein rolled, picked by weight from the world's vein table.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<OrePrototype> Ore;

    /// <summary>Total units in the vein when it was stamped, before any extraction.</summary>
    [DataField, AutoNetworkedField]
    public int TotalYield;

    /// <summary>True in the top quarter of the table's yield range; drives the richer sprite state.</summary>
    [DataField, AutoNetworkedField]
    public bool Rich;

    /// <summary>Units per extraction tick, copied from the table. F6's; unused by F2.</summary>
    [DataField]
    public float Rate = 150f;

    /// <summary>Units left to extract. F6's; unused by F2.</summary>
    [DataField]
    public int Remaining;

    /// <summary>
    /// Tiles this vein is allowed to sit on; it deletes itself at MapInit on anything else.
    /// This is the only tile filter available: BiomeMarkerLayerPrototype has exactly eight fields in this fork
    /// (Content.Shared/Parallax/Biomes/Markers/BiomeMarkerLayerPrototype.cs:12-52) and none of them is a whitelist -
    /// tile whitelists live on biome LAYERS (IBiomeWorldLayer.AllowedTiles), which marker layers never consult.
    /// The two defaults are Grasslands' fill tiles (Resources/Prototypes/Procedural/biome_templates.yml:124-130) and
    /// deliberately exclude MonoOcean and Snow, the other two templates WFBiomeAsclepiu stacks.
    /// </summary>
    [DataField]
    public List<ProtoId<ContentTileDefinition>> AllowedTiles = new() { "FloorPlanetGrass", "FloorPlanetDirt" };
}
