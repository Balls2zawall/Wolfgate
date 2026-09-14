using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._WF.PlanetCracker.Cracker;

/// <summary>Marks a grid as a planet cracker hull and carries the state of its current crack.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class WFPlanetCrackerComponent : Component
{
    /// <summary>Current stage of the crack, per design section 3.</summary>
    [DataField, AutoNetworkedField]
    public WFCrackState State = WFCrackState.Idle;

    /// <summary>The mapper-placed berth marker on this hull, resolved at map init.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? Berth;

    /// <summary>The first half of the targeted pair.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? AnchorA;

    /// <summary>The second half of the targeted pair.</summary>
    [DataField, AutoNetworkedField]
    public NetEntity? AnchorB;

    /// <summary>
    /// Berth centre in grid-local coordinates, rewritten whenever the berth resolves.
    /// The grid entity is force-sent to any client who sees any chunk of it, while the berth marker 26 tiles out on a
    /// MarkerBase prototype routinely falls outside net.pvs_range, so this is how the berth pose reaches a client that
    /// has no BUI state to read - the radar ghost.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 BerthLocalPos;

    /// <summary>Berth rotation relative to the grid, rewritten alongside <see cref="BerthLocalPos"/>.</summary>
    [DataField, AutoNetworkedField]
    public Angle BerthLocalRot;

    /// <summary>Berth rectangle in tiles, copied off the marker.</summary>
    [DataField, AutoNetworkedField]
    public Vector2i BerthSize;

    /// <summary>
    /// When the running crack finishes; authoritative while the crack is not damage-paused.
    /// Two fields rather than one deadline: the map pause and the damage pause are different mechanisms and coexist,
    /// so the remainder is banked in <see cref="CrackRemaining"/> while a damaged anchor holds the cut.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CrackEnd;

    /// <summary>Time left on the crack, banked while the damage pause holds.</summary>
    [DataField]
    public TimeSpan CrackRemaining;

    /// <summary>Full duration the current crack was begun with, for the console progress bar.</summary>
    [DataField]
    public TimeSpan CrackDuration;

    /// <summary>True while a damaged targeted anchor is holding the crack.</summary>
    [DataField]
    public bool CrackPaused;

    /// <summary>When the grace countdown runs out and the hull drops; meaningless unless GraceRunning.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan GraceEnd;

    /// <summary>True while the grace countdown is running.</summary>
    [DataField]
    public bool GraceRunning;

    /// <summary>How long the crew has to fix a failing precondition before the hull drops.</summary>
    [DataField]
    public TimeSpan GraceDuration = TimeSpan.FromMinutes(5);

    /// <summary>Which preconditions are failing; recomputed every sweep while Cracking or Cracked.</summary>
    [DataField]
    public WFCrackFailure Failing;

    /// <summary>Why BEGIN CRACK is refused right now; the console hover list names one locale key per flag.</summary>
    [DataField]
    public WFCrackBlocker Blockers;

    /// <summary>When the abort spin-down finishes and the state falls back to PendingAbort.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan AbortEnd;

    /// <summary>State the abort spin-down is heading for; null when no abort is running.</summary>
    [DataField, AutoNetworkedField]
    public WFCrackState? PendingAbort;

    /// <summary>True only while the crack applied its own ForceAnchor, so the release never steals a mapper's anchor.</summary>
    [DataField]
    public bool Locked;

    /// <summary>Tiles the berth centre may sit from the cut circle centre and still be targetable.</summary>
    [DataField]
    public float AlignTolerance = 8f;

    /// <summary>Crack time at ReferenceDistance with tier 1 parts.</summary>
    [DataField]
    public TimeSpan BaseCrackTime = TimeSpan.FromMinutes(12);

    /// <summary>Pair distance BaseCrackTime is quoted for, in tiles.</summary>
    [DataField]
    public float ReferenceDistance = 24f;

    /// <summary>How long the hull keeps its lock after a targeted anchor is broken or destroyed.</summary>
    [DataField]
    public TimeSpan AbortSpinDown = TimeSpan.FromSeconds(30);

    /// <summary>Healthy projectors the hull needs to keep cutting.</summary>
    [DataField]
    public int RequiredProjectors = 2;

    /// <summary>Looped while the cut runs.</summary>
    [DataField]
    public SoundSpecifier RumbleSound = new SoundPathSpecifier("/Audio/Ambience/Objects/crushing.ogg");

    /// <summary>Looped while the grace countdown runs.</summary>
    [DataField]
    public SoundSpecifier KlaxonSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

    /// <summary>Looped once the hull is falling.</summary>
    [DataField]
    public SoundSpecifier FallSound = new SoundPathSpecifier("/Audio/Misc/redalert.ogg");

    /// <summary>One-shot thunk as the hull snaps onto the circle and locks.</summary>
    [DataField]
    public SoundSpecifier LockSound = new SoundCollectionSpecifier("MetalThud");

    /// <summary>Live rumble loop; server-only, never networked, stopped on every state edge.</summary>
    [ViewVariables]
    public EntityUid? RumbleStream;

    /// <summary>Live grace klaxon loop; server-only, never networked.</summary>
    [ViewVariables]
    public EntityUid? KlaxonStream;

    /// <summary>Live fall alarm loop; server-only, never networked.</summary>
    [ViewVariables]
    public EntityUid? FallStream;
}
