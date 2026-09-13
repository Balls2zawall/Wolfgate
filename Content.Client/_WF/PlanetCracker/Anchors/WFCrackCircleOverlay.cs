using System.Numerics;
using Content.Client._WF.Stylesheets;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.PlanetCracker.Anchors;
using Robust.Client.Graphics;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;

namespace Content.Client._WF.PlanetCracker.Anchors;

/// <summary>
/// Draws the cut circle a paired anchor rig would carve: one ring per pair, plus the chord and the two anchor marks.
/// </summary>
public sealed partial class WFCrackCircleOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _xformQuery;

    /// <summary>One cached ring per pair, keyed by the lower-uid half that draws it.</summary>
    private readonly Dictionary<EntityUid, Ring> _rings = new();

    /// <summary>Rings whose owning anchor is gone, collected so the sweep does not remove mid-enumeration.</summary>
    private readonly List<EntityUid> _stale = new();

    /// <summary>How far the centre or radius may drift before the ring is rebuilt, in tiles.</summary>
    private const float RebuildTolerance = 0.01f;

    /// <summary>Segments per tile of radius, clamped to the range below.</summary>
    private const float SegmentsPerTile = 8f;

    /// <summary>Fewest segments any ring is drawn with.</summary>
    private const int MinSegments = 64;

    /// <summary>Most segments any ring is drawn with; a 22-tile cut sits well inside this.</summary>
    private const int MaxSegments = 256;

    /// <summary>Radius of the mark drawn on each half of the pair, in tiles.</summary>
    private const float AnchorMarkRadius = 0.6f;

    /// <summary>Alpha of the chord joining the two halves.</summary>
    private const float ChordAlpha = 0.35f;

    /// <inheritdoc/>
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public WFCrackCircleOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entityManager.System<SharedTransformSystem>();
        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
    }

    /// <summary>Drops every cached ring, so the next frame rebuilds them from the new component state.</summary>
    public void Invalidate()
    {
        _rings.Clear();
    }

    /// <summary>Drops one pair's cached ring.</summary>
    public void Invalidate(EntityUid anchor)
    {
        _rings.Remove(anchor);
    }

    /// <inheritdoc/>
    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var skin = WolfgateSkins.Get(_cfg.GetCVar(WolfgateCVars.UiStyle));

        _stale.Clear();

        var query = _entityManager.EntityQueryEnumerator<WFGravityAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // During a z-pass args.MapId is the map being rendered, so this is the whole layer filter needed.
            if (xform.MapID != args.MapId)
                continue;

            if (comp.Partner is not { } netPartner)
                continue;

            // A 40-tile pair can have its far half outside PVS; that is expected, not an error.
            if (!_entityManager.TryGetEntity(netPartner, out var partner))
                continue;

            // Both halves see the same pair, so only the lower uid draws it.
            if (uid.Id > partner.Value.Id)
                continue;

            if (!_xformQuery.TryComp(partner, out var otherXform) || otherXform.MapID != args.MapId)
                continue;

            if (!_entityManager.TryGetComponent<WFGravityAnchorComponent>(partner, out var otherComp))
                continue;

            var a = _transform.GetWorldPosition(xform);
            var b = _transform.GetWorldPosition(otherXform);
            var centre = (a + b) / 2f;
            var radius = SharedWFGravityAnchorSystem.GetCutRadius((a - b).Length(), comp.CutPadding);
            var colour = ColourFor(skin, comp, otherComp);

            // The two paths want the colour in different spaces, and the skin stores sRGB hex. The Vector2-span
            // DrawPrimitives overload converts for us (DrawingHandleBase.PadVerticesV2 calls Color.FromSrgb), so the
            // ring takes the raw skin colour; DrawLine and an unfilled DrawCircle (which is DrawLine per segment)
            // write straight into Vertex2D.Modulate, which is linear, so those take the converted colour. Getting
            // this wrong draws the chord and the marks a visibly different shade from their own ring.
            var linear = Color.FromSrgb(colour);

            var ring = GetRing(uid, centre, radius);
            handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, ring.Vertices.Span, colour);

            handle.DrawLine(a, b, linear.WithAlpha(ChordAlpha));
            handle.DrawCircle(a, AnchorMarkRadius, linear, false);
            handle.DrawCircle(b, AnchorMarkRadius, linear, false);
        }

        foreach (var (uid, _) in _rings)
        {
            if (!_entityManager.EntityExists(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _rings.Remove(uid);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// The cached vertices for one pair's ring, rebuilt only when the pair actually moved or resized. DrawCircle would
    /// emit one GL line per segment every frame; this is one DrawPrimitives call instead.
    /// </summary>
    private Ring GetRing(EntityUid uid, Vector2 centre, float radius)
    {
        if (_rings.TryGetValue(uid, out var ring) &&
            (ring.Centre - centre).Length() <= RebuildTolerance &&
            MathF.Abs(ring.Radius - radius) <= RebuildTolerance)
        {
            return ring;
        }

        ring = new Ring { Centre = centre, Radius = radius };

        var segments = Math.Clamp((int)(radius * SegmentsPerTile), MinSegments, MaxSegments);

        for (var i = 0; i <= segments; i++)
        {
            // The last vertex repeats the first so the line strip closes.
            var angle = MathF.Tau * i / segments;
            ring.Vertices.Add(centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        _rings[uid] = ring;
        return ring;
    }

    /// <summary>Ring colour from the worse of the two halves, taken from the active skin rather than a literal.</summary>
    private static Color ColourFor(WolfgateSkin skin, WFGravityAnchorComponent a, WFGravityAnchorComponent b)
    {
        if (a.State == WFAnchorState.Broken || b.State == WFAnchorState.Broken || a.Damaged || b.Damaged)
            return skin.Danger;

        if (a.State is WFAnchorState.Locked or WFAnchorState.Off &&
            b.State is WFAnchorState.Locked or WFAnchorState.Off)
            return skin.Good;

        if (a.State == WFAnchorState.Drilling || b.State == WFAnchorState.Drilling)
            return skin.Caution;

        return skin.AccentDim;
    }

    /// <summary>One pair's cached ring geometry.</summary>
    private sealed class Ring
    {
        /// <summary>Midpoint the vertices were built around.</summary>
        public Vector2 Centre;

        /// <summary>Cut radius the vertices were built with.</summary>
        public float Radius;

        /// <summary>The closed line strip, first vertex repeated at the end.</summary>
        public ValueList<Vector2> Vertices;
    }
}
