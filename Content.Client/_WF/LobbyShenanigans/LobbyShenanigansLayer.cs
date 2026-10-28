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
/// Covers the lobby and runs every <see cref="LobbyShenanigan"/> over it. Never takes a click.
/// </summary>
public sealed class LobbyShenanigansLayer : Control
{
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private IDynamicTypeFactory _factory = default!;

    // A long hitch would otherwise be simulated all at once.
    private const float MaxFrame = 0.1f;

    public readonly List<LobbyShenanigan> Shenanigans;

    public LobbyShenanigansLayer()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        Shenanigans = _reflection.GetAllChildren<LobbyShenanigan>()
            .Select(t => _factory.CreateInstance<LobbyShenanigan>(t))
            .OrderBy(s => s.Order)
            .ToList();
    }

    public void Trigger(LobbyShenanigan shenanigan) => shenanigan.Trigger(PixelSize, UIScale);

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
