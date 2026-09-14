using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._WF.PlanetCracker.Chunk;

/// <summary>Marks a grid as a disc cut out of a planet and hung in its cracker's berth (design D24).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class WFPlanetChunkComponent : Component
{
    /// <summary>The cracker hull this chunk was cut for; the watchdog drops the chunk once this stops resolving.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Cracker;

    /// <summary>The ground layer map the disc was lifted out of.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? GroundMap;

    /// <summary>
    /// The berth's map, captured at extraction so the watchdog compares map IDENTITY rather than kind: the orbit layer
    /// is an FTL destination and every planet has one, so a hull that jumps to another planet's orbit must still drop.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? OrbitMap;

    /// <summary>Centre of the cut circle as a raw world XY on the ground layer.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 HoleCentre;

    /// <summary>Radius of the cut circle in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float Radius;

    /// <summary>How many tiles were copied onto this grid.</summary>
    [DataField, AutoNetworkedField]
    public int TileCount;

    /// <summary>True once the chunk has been pushed into transit; the watchdog skips it from then on.</summary>
    [DataField, AutoNetworkedField]
    public bool Dropped;

    /// <summary>
    /// When the chunk was cut. Paused with its map, because a plain TimeSpan would burn the whole watchdog grace the
    /// instant a paused map unpaused and drop the chunk out from under a perfectly healthy hull.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExtractedAt;

    /// <summary>How long after extraction the watchdog leaves the chunk alone.</summary>
    [DataField]
    public TimeSpan WatchdogGrace = TimeSpan.FromSeconds(5);

    /// <summary>Looped once the chunk is falling.</summary>
    [DataField]
    public SoundSpecifier DropSound = new SoundPathSpecifier("/Audio/Ambience/Objects/crushing.ogg");

    /// <summary>Ids of the rim decals stamped around the hole, kept for admin teardown; there is no bulk decal removal.</summary>
    [ViewVariables]
    public List<uint> RimDecals = new();

    /// <summary>Live drop loop; server-only, never networked.</summary>
    [ViewVariables]
    public EntityUid? DropStream;
}
