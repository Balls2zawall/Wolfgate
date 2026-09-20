using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._WF.Explosion;

/// <summary>
/// Draws every live explosion shockwave as a screen space displacement ring.
/// </summary>
public sealed class ExplosionShockwaveOverlay : Overlay
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>Waves that can distort the screen at once. The shader's array lengths match this.</summary>
    public const int MaxCount = 4;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ExplosionShockwaveSystem _system;
    private readonly ShaderInstance _shader;

    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _radii = new float[MaxCount];
    private readonly float[] _widths = new float[MaxCount];
    private readonly float[] _strengths = new float[MaxCount];
    private int _count;

    public ExplosionShockwaveOverlay(ExplosionShockwaveSystem system)
    {
        IoCManager.InjectDependencies(this);

        _system = system;
        _shader = _proto.Index<ShaderPrototype>("WfExplosionShockwave").Instance().Duplicate();
        ZIndex = 102; // After the singularity's own distortion, which sits at 101.
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _count = 0;

        if (args.Viewport.Eye == null)
            return false;

        foreach (var wave in _system.Shockwaves)
        {
            if (wave.Position.MapId != args.MapId || wave.Strength <= 0f)
                continue;

            // Skip waves whose band is nowhere near the visible area.
            var centre = wave.Position.Position;
            var reach = wave.Radius + wave.Width;

            if ((centre - args.WorldAABB.ClosestPoint(centre)).LengthSquared() > reach * reach)
                continue;

            // Inside-viewport pixels, so specifically not IViewportControl.WorldToScreen.
            var coords = args.Viewport.WorldToLocal(centre);
            coords.Y = args.Viewport.Size.Y - coords.Y; // Local space to fragment space.

            // Metres to fragment pixels.
            var scale = EyeManager.PixelsPerMeter * (args.Viewport.RenderScale * args.Viewport.Eye.Scale).X;

            _positions[_count] = coords;
            _radii[_count] = wave.Radius * scale;
            _widths[_count] = MathF.Max(wave.Width * scale, 1f);
            _strengths[_count] = wave.Strength * scale;
            _count++;

            if (_count == MaxCount)
                break;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || _count == 0)
            return;

        _shader.SetParameter("count", _count);
        _shader.SetParameter("position", _positions);
        _shader.SetParameter("radius", _radii);
        _shader.SetParameter("width", _widths);
        _shader.SetParameter("strength", _strengths);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
