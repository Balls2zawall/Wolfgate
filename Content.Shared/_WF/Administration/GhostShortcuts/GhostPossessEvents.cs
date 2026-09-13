using Robust.Shared.Serialization;

namespace Content.Shared._WF.Administration.GhostShortcuts;

/// <summary>
/// Admin asks the server to put a ghost's player into a body. Sent by the occupied-body prompt; the first attempt
/// arrives as a normal drag-drop. Admin-only; ignored otherwise.
/// </summary>
[Serializable, NetSerializable]
public sealed class GhostPossessRequestEvent : EntityEventArgs
{
    public NetEntity Ghost;
    public NetEntity Body;

    /// <summary>
    /// Ghost whoever is in the body first instead of aborting.
    /// </summary>
    public bool Replace;

    public GhostPossessRequestEvent(NetEntity ghost, NetEntity body, bool replace)
    {
        Ghost = ghost;
        Body = body;
        Replace = replace;
    }
}

/// <summary>
/// Server tells the admin the body already has a player so the client can ask whether to replace them.
/// </summary>
[Serializable, NetSerializable]
public sealed class GhostPossessOccupiedEvent : EntityEventArgs
{
    public NetEntity Ghost;
    public NetEntity Body;
    public string BodyName = string.Empty;
    public string GhostPlayer = string.Empty;
    public string OccupantCharacter = string.Empty;
    public string OccupantPlayer = string.Empty;

    public GhostPossessOccupiedEvent(NetEntity ghost, NetEntity body)
    {
        Ghost = ghost;
        Body = body;
    }
}
