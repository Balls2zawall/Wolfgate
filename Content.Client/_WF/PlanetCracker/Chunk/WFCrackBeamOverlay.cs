using System.Numerics;
using Content.Client._WF.PlanetCracker.Anchors;
using Content.Client._WF.Stylesheets;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.PlanetCracker.Anchors;
using Content.Shared._WF.PlanetCracker.Cracker;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._WF.PlanetCracker.Chunk;

/// <summary>
/// Draws one beam per firing gravity projector, from the projector's emitter to the anchor it is cutting with, with the
/// far end projected onto the viewer's own layer when the anchor is several z-levels below (design D4).
/// </summary>
public sealed partial class WFCrackBeamOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly ISawmill _sawmill;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<CEZMapComponent> _zMapQuery;
    private readonly EntityQuery<WFGravityAnchorComponent> _anchorQuery;

    /// <summary>The beam texture; an RSI frame, so it is an atlas sub-region and must never be tiled by UV.</summary>
    private static readonly SpriteSpecifier BeamSprite =
        new SpriteSpecifier.Rsi(new ResPath("_WF/PlanetCracker/Effects/crack_beam.rsi"), "beam");

    /// <summary>
    /// Where the beam leaves the projector, in its own rotated frame. The placeholder art is 64x64 on a 1x1 fixture
    /// (machines.yml:103-107), so the emitter face sits one tile in front of the entity's own tile centre.
    /// </summary>
    private static readonly Vector2 EmitterOffset = new(0f, -1f);

    /// <summary>Shorter than this and the rect degenerates, so the beam is simply not drawn.</summary>
    private const float MinBeamLength = 0.05f;

    /// <summary>An overlay bail-out is indistinguishable from a swallowed exception at runtime, so each one logs once.</summary>
    private bool _loggedNoEye;

    /// <summary>As above, for a viewer or target map that carries no CEZMapComponent.</summary>
    private bool _loggedNoDepth;

    /// <inheritdoc/>
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public WFCrackBeamOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entityManager.System<SharedTransformSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _sawmill = _logManager.GetSawmill("wf.crackbeam");

        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        _zMapQuery = _entityManager.GetEntityQuery<CEZMapComponent>();
        _anchorQuery = _entityManager.GetEntityQuery<WFGravityAnchorComponent>();

        // Equal ZIndex draws in a random order (Overlay.cs:33-35), so the beam names its own slot above the ring.
        ZIndex = 10;
    }

    /// <inheritdoc/>
    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        // ONE PASS ONLY. The beam is drawn once, in the viewer's own map, and its far end is projected into that pass's
        // screen space; drawing it again per z-pass would stack several copies. `eye is ZEye` is the wrong test: at
        // altitude zero the own pass is handed the plain fallback eye (ScalingViewport.CEZLevels.cs:318).
        if (_player.LocalEntity is not { } local ||
            !_xformQuery.TryComp(local, out var localXform) ||
            localXform.MapID != args.MapId)
        {
            return;
        }

        // Every eye term comes from THIS pass's eye. The own pass is a scaled ZEye whenever the player's local altitude
        // is non-zero (ScalingViewport.CEZLevels.cs:139-140), so IEyeManager.CurrentEye or a cached fallback eye is wrong.
        if (args.Viewport.Eye is not { } eye)
        {
            if (!_loggedNoEye)
            {
                _loggedNoEye = true;
                _sawmill.Warning("Beam overlay ran on a viewport with no eye; no beams drawn.");
            }

            return;
        }

        if (!_zMapQuery.TryComp(args.MapUid, out var viewerMap))
        {
            if (!_loggedNoDepth)
            {
                _loggedNoDepth = true;
                _sawmill.Debug($"Beam overlay skipped: viewer map {args.MapUid} carries no CEZMapComponent.");
            }

            return;
        }

        var eyePos = eye.Position.Position;
        var eyeOffset = eye.Offset;
        var up = (-eye.Rotation).ToWorldVec();

        var skin = WolfgateSkins.Get(_cfg.GetCVar(WolfgateCVars.UiStyle));

        var texture = _sprite.GetFrame(BeamSprite, _timing.CurTime);
        var width = texture.Width / (float) EyeManager.PixelsPerMeter;

        var query = _entityManager
            .EntityQueryEnumerator<WFCrackBeamComponent, WFGravityProjectorComponent, TransformComponent>();
        while (query.MoveNext(out var beam, out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            // The anchors carry a global PVS override (WFGravityAnchorSystem.cs:148), so the far half always resolves.
            if (beam.Target is not { } netTarget ||
                !_entityManager.TryGetEntity(netTarget, out var target) ||
                !_xformQuery.TryComp(target, out var targetXform) ||
                !_anchorQuery.TryComp(target, out var anchor))
            {
                continue;
            }

            if (targetXform.MapUid is not { } targetMapUid || !_zMapQuery.TryComp(targetMapUid, out var targetMap))
            {
                if (!_loggedNoDepth)
                {
                    _loggedNoDepth = true;
                    _sawmill.Debug($"Beam overlay skipped a beam: target map of {target} carries no CEZMapComponent.");
                }

                continue;
            }

            // Depth counts upward (CEZLevelsSystem.Maps.cs:68-75), so n is how many layers BELOW the viewer the anchor is.
            var n = viewerMap.Depth - targetMap.Depth;
            var r = MathF.Pow(CESharedZLevelsSystem.ZLevelViewShrink, n);

            var anchorPos = _transform.GetWorldPosition(targetXform);

            // Equating the far pass's screen position with this pass's: a pass at depth d is drawn at scale
            // ZLevelViewShrink^-d and shifted by up * ZLevelOffset * (d - ownDepth), both of which cancel into one
            // affine map about the eye (ScalingViewport.CEZLevels.cs:337-345 against Eye.GetViewMatrix).
            var far = eyePos + eyeOffset
                + r * (anchorPos - eyePos - eyeOffset + up * CESharedZLevelsSystem.ZLevelOffset * n);

            var near = _transform.GetWorldPosition(xform)
                + _transform.GetWorldRotation(xform).RotateVec(EmitterOffset);

            var diff = far - near;
            var length = diff.Length();

            if (length <= MinBeamLength)
                continue;

            var partner = anchor.Partner is { } netPartner &&
                          _entityManager.TryGetEntity(netPartner, out var partnerUid) &&
                          _anchorQuery.TryComp(partnerUid, out var partnerComp)
                ? partnerComp
                : anchor;

            // DrawTextureRect writes Modulate directly, which is linear, so this takes the CONVERTED skin colour.
            var colour = Color.FromSrgb(WFCrackCircleOverlay.ColourFor(skin, anchor, partner));

            var midPoint = near + diff / 2f;
            var box = new Box2(-width / 2f, -length / 2f, width / 2f, length / 2f);
            var rotated = new Box2Rotated(box.Translated(midPoint), diff.ToWorldAngle(), midPoint);

            handle.DrawTextureRect(texture, rotated, colour);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
