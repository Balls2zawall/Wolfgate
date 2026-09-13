using System.Linq;
using System.Numerics;
using Content.Server._WF.PlanetCracker.Testing;
using Content.Server.Administration;
using Content.Shared._WF.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._WF.PlanetCracker.Commands;

/// <summary>
/// Spawns the code-built planet cracker test grids next to the calling admin.
/// </summary>
[AdminCommand(AdminFlags.Spawn | AdminFlags.Mapping)]
public sealed partial class WFCrackerCommand : LocalizedEntityCommands
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WFTestGridFactory _factory = default!;

    private const string SubSpawn = "spawn";

    private const string KindCracker = "cracker";
    private const string KindTransport = "transport";

    private static readonly string[] Subcommands = { SubSpawn };

    private static readonly string[] Kinds = { KindCracker, KindTransport };

    /// <summary>Offset from the caller to the cracker hull, so it does not land on their head.</summary>
    private static readonly Vector2 CrackerOffset = new(8f, 8f);

    /// <summary>Offset from the caller to the transport hull, clear of the cracker.</summary>
    private static readonly Vector2 TransportOffset = new(8f, -12f);

    /// <inheritdoc/>
    public override string Command => WolfgateAdminCommands.Cracker;

    /// <inheritdoc/>
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !Subcommands.Contains(args[0]))
        {
            shell.WriteError(Loc.GetString("cmd-wfcracker-invalid-args"));
            shell.WriteLine(Help);
            return;
        }

        if (!TryGetPlayerPosition(shell, out var map, out var position))
            return;

        EntityUid grid;

        switch (args[1])
        {
            case KindCracker:
                grid = _factory.BuildCracker(map, position + CrackerOffset);
                break;
            case KindTransport:
                grid = _factory.BuildTransport(map, position + TransportOffset);
                break;
            default:
                shell.WriteError(Loc.GetString("cmd-wfcracker-unknown-kind", ("kind", args[1])));
                return;
        }

        shell.WriteLine(Loc.GetString("cmd-wfcracker-spawned",
            ("kind", args[1]),
            ("grid", EntityManager.ToPrettyString(grid).ToString()),
            ("map", map.ToString())));
    }

    /// <summary>Resolves the map and world position the calling player is standing at.</summary>
    private bool TryGetPlayerPosition(IConsoleShell shell, out MapId map, out Vector2 position)
    {
        map = MapId.Nullspace;
        position = Vector2.Zero;

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError(Loc.GetString("cmd-wfcracker-no-map"));
            return false;
        }

        var xform = EntityManager.GetComponent<TransformComponent>(player);

        if (xform.MapID == MapId.Nullspace)
        {
            shell.WriteError(Loc.GetString("cmd-wfcracker-no-map"));
            return false;
        }

        map = xform.MapID;
        position = _transform.GetWorldPosition(xform);
        return true;
    }

    /// <inheritdoc/>
    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(Subcommands, Loc.GetString("cmd-wfcracker-hint-sub"));
            case 2:
                return args[0] == SubSpawn
                    ? CompletionResult.FromHintOptions(Kinds, Loc.GetString("cmd-wfcracker-hint-kind"))
                    : CompletionResult.Empty;
            default:
                return CompletionResult.Empty;
        }
    }
}
