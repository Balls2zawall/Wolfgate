using Content.Shared._WF.PlanetCracker.Planets;
using Content.Shared.Shuttles.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._WF.PlanetCracker.Planets;

/// <summary>
/// The shuttle console's "enter planet orbit" / "leave orbit" control. It needs no FTL drive and never touches the
/// destination list: the server answers a BUI message with the ordinary FTL transit.
/// It reads <see cref="WFConsoleOrbitTargetComponent"/> off the console entity every frame rather than the shuttle BUI
/// state, because that state is only pushed on docking, beacon and power events and would be stale while the hull flies.
/// </summary>
public sealed partial class WFOrbitButton : Button
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly SharedUserInterfaceSystem _ui;

    private EntityUid? _console;

    public WFOrbitButton()
    {
        IoCManager.InjectDependencies(this);
        _ui = _entMan.System<SharedUserInterfaceSystem>();

        StyleClasses.Add("ButtonSquare");
        TextAlign = Label.AlignMode.Center;
        Visible = false;

        OnPressed += OnOrbitPressed;
    }

    /// <summary>Binds this button to the console whose interface it sits in.</summary>
    public void SetConsole(EntityUid? console)
    {
        _console = console;
    }

    /// <inheritdoc/>
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entMan.TryGetComponent<WFConsoleOrbitTargetComponent>(_console, out var target))
        {
            Visible = false;
            return;
        }

        Visible = true;
        Disabled = target.Busy || target.Planet == null;

        var planet = target.PlanetName;

        if (string.IsNullOrEmpty(planet))
        {
            Text = Loc.GetString("wf-shuttle-console-orbit-none");
            return;
        }

        Text = Loc.GetString(target.InOrbit ? "wf-shuttle-console-leave-orbit" : "wf-shuttle-console-enter-orbit",
            ("planet", planet));
    }

    /// <summary>Asks the server for the hop; every gate is re-checked there, so a stale button can only be refused.</summary>
    private void OnOrbitPressed(ButtonEventArgs args)
    {
        if (_console is not { } console
            || !_entMan.TryGetComponent<WFConsoleOrbitTargetComponent>(console, out var target)
            || target.Busy)
        {
            return;
        }

        var netConsole = _entMan.GetNetEntity(console);

        if (target.InOrbit)
        {
            _ui.ClientSendUiMessage(console, ShuttleConsoleUiKey.Key, new WFLeavePlanetOrbitMessage(netConsole));
            return;
        }

        if (target.Planet is not { } planet)
            return;

        _ui.ClientSendUiMessage(console, ShuttleConsoleUiKey.Key, new WFEnterPlanetOrbitMessage(netConsole, planet));
    }
}
