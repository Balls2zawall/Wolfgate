using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;

namespace Content.Client._WF.LobbyShenanigans;

/// <summary>
/// Covers the lobby and runs every <see cref="LobbyShenanigan"/> over it when the server sets one off. Never takes
/// a click.
/// </summary>
public sealed class LobbyShenanigansLayer : Control
{
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private IDynamicTypeFactory _factory = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    // A long hitch would otherwise be simulated all at once.
    private const float MaxFrame = 0.1f;

    public readonly List<LobbyShenanigan> Shenanigans;

    public LobbyShenanigansLayer()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        Shenanigans = _reflection.GetAllChildren<LobbyShenanigan>()
            .Select(t => _factory.CreateInstance<LobbyShenanigan>(t))
            .ToList();
    }

    private void OnTriggered(string id)
    {
        if (Shenanigans.FirstOrDefault(s => s.Id == id) is { } shenanigan)
            shenanigan.Trigger(PixelSize, UIScale);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _systems.GetEntitySystem<LobbyShenanigansSystem>().Triggered += OnTriggered;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _systems.GetEntitySystem<LobbyShenanigansSystem>().Triggered -= OnTriggered;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!VisibleInTree)
            return;
        var dt = MathF.Min(args.DeltaSeconds, MaxFrame);
        foreach (var shenanigan in Shenanigans)
            shenanigan.Update(dt, PixelSize, UIScale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        foreach (var shenanigan in Shenanigans)
            shenanigan.Draw(handle, UIScale);
    }
}
