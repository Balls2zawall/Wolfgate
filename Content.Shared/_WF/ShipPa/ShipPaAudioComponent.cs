using Robust.Shared.GameStates;

namespace Content.Shared._WF.ShipPa;

/// <summary>
/// Added by the server to every audio entity a PA speaker plays, so the client mesh can tell the
/// copies of one broadcast apart and duck all but the nearest.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipPaAudioComponent : Component
{
    /// <summary>Streams sharing an id are copies of the same broadcast; the client keeps only the nearest audible.</summary>
    [DataField, AutoNetworkedField] public int BroadcastId;

    /// <summary>The speaker this stream is playing out of.</summary>
    [DataField, AutoNetworkedField] public EntityUid? Speaker;

    /// <summary>Copy of the speaker's distortion when the stream started.</summary>
    [DataField, AutoNetworkedField] public float Distortion;

    /// <summary>Static overlays are never ducked by the mesh (they're already quiet) and never muffled further.</summary>
    [DataField, AutoNetworkedField] public bool IsOverlay;
}
