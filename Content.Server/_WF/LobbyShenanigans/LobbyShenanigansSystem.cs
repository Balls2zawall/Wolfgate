using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._WF.LobbyShenanigans;
using Content.Shared.GameTicking;
using Robust.Server.Player;

namespace Content.Server._WF.LobbyShenanigans;

/// <summary>
/// Sets a lobby shenanigan off for every client. Called only by the SSymphony panel's endpoint, behind the admin API
/// token; clients have no way to ask for one.
/// </summary>
public sealed class LobbyShenanigansSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private GameTicker _ticker = default!;

    /// <summary>Sets <paramref name="id"/> off for every client and counts those in the lobby who see it; false for an unknown ID.</summary>
    public bool Trigger(string id, out int inLobby)
    {
        inLobby = 0;
        if (!LobbyShenaniganIds.All.Contains(id))
            return false;
        inLobby = _players.Sessions.Count(s => !_ticker.PlayerGameStatuses.TryGetValue(s.UserId, out var status) || status != PlayerGameStatus.JoinedGame);
        RaiseNetworkEvent(new LobbyShenaniganTriggerEvent(id));
        return true;
    }
}
