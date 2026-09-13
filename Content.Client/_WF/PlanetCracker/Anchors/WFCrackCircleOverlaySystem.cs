using Content.Shared._WF.PlanetCracker.Anchors;
using Robust.Client.Graphics;

namespace Content.Client._WF.PlanetCracker.Anchors;

/// <summary>Owns the crack circle overlay and drops its cached rings whenever an anchor's state arrives from the server.</summary>
public sealed partial class WFCrackCircleOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    /// <summary>The overlay this system keeps registered, held so its cache can be invalidated.</summary>
    private WFCrackCircleOverlay _circles = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _circles = new WFCrackCircleOverlay();
        _overlay.AddOverlay(_circles);

        SubscribeLocalEvent<WFGravityAnchorComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<WFGravityAnchorComponent, ComponentShutdown>(OnShutdown);
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<WFCrackCircleOverlay>();
    }

    /// <summary>A new state can move a pair, re-radius it or recolour it, so the cached ring goes.</summary>
    private void OnState(Entity<WFGravityAnchorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _circles.Invalidate();
    }

    /// <summary>An anchor leaving the client's view takes its cached ring with it.</summary>
    private void OnShutdown(Entity<WFGravityAnchorComponent> ent, ref ComponentShutdown args)
    {
        _circles.Invalidate(ent.Owner);
    }
}
