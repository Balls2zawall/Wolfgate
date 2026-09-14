using System.Numerics;
using Content.Shared._WF.PlanetCracker.Anchors;
using Content.Shared._WF.PlanetCracker.Cracker;
using Content.Shared.Camera;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._WF.PlanetCracker.Cracker;

/// <summary>
/// The cutting beams and the site's own shaking, reconciled every sweep rather than written on edges. No subscriptions.
/// A projector's beam is a networked target the client overlay draws; the surface half of the beam is a sprite child of
/// the anchor, because look-up rendering draws exactly one map and no overlay can reach across layers.
/// </summary>
public sealed partial class WFCrackerSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;

    /// <summary>The pillar of light standing on a targeted anchor while the cut runs.</summary>
    private const string SkyBeamProto = "WFCrackSkyBeam";

    /// <summary>One sky beam per targeted anchor; keyed on the anchor so a dead anchor's beam is still findable.</summary>
    private readonly Dictionary<EntityUid, EntityUid> _skyBeams = new();

    /// <summary>Next site camera kick per cutting hull; server-only bookkeeping.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _nextSiteKick = new();

    /// <summary>Sky beam entries this pass decided to drop; collected first so the dictionary is not edited mid-walk.</summary>
    private readonly List<EntityUid> _staleBeams = new();

    /// <summary>
    /// Points every projector at one half of the pair and stands a beam on each anchor, or takes both away.
    /// Beams exist only while the cut is actually running: WFProjectorState is a display value the projector's own
    /// power and repair handlers overwrite, so it is never read as "this hull is holding a chunk".
    /// </summary>
    private void ReconcileBeams(Entity<WFPlanetCrackerComponent> ent)
    {
        var cutting = ent.Comp.State == WFCrackState.Cracking && ent.Comp.PendingAbort is null;
        var targeted = TryGetTargetedPair(ent, out var a, out var b);
        var firing = cutting && targeted;

        GetProjectors(ent.Owner, _projectorBuffer);

        for (var i = 0; i < _projectorBuffer.Count; i++)
        {
            var projector = _projectorBuffer[i];

            if (!firing)
            {
                RemComp<WFCrackBeamComponent>(projector.Owner);
                continue;
            }

            // The list is sorted by grid-local X, so the split is stable from one sweep to the next.
            var target = GetNetEntity((i & 1) == 0 ? a.Owner : b.Owner);
            var beam = EnsureComp<WFCrackBeamComponent>(projector.Owner);

            if (beam.Target == target)
                continue;

            beam.Target = target;
            Dirty(projector.Owner, beam);
        }

        if (firing)
        {
            EnsureSkyBeam(a.Owner);
            EnsureSkyBeam(b.Owner);
        }

        PruneSkyBeams(ent, firing, a.Owner, b.Owner);
    }

    /// <summary>Parents one sky beam to an anchor, whose own global PVS override is what replicates it to every layer.</summary>
    private void EnsureSkyBeam(EntityUid anchor)
    {
        if (_skyBeams.TryGetValue(anchor, out var existing) && !TerminatingOrDeleted(existing))
            return;

        _skyBeams[anchor] = Spawn(SkyBeamProto, new EntityCoordinates(anchor, Vector2.Zero));
    }

    /// <summary>Deletes a sky beam whose anchor died, stopped being targeted or whose cut has ended.</summary>
    private void PruneSkyBeams(Entity<WFPlanetCrackerComponent> ent, bool firing, EntityUid a, EntityUid b)
    {
        _staleBeams.Clear();

        foreach (var (anchor, _) in _skyBeams)
        {
            if (TerminatingOrDeleted(anchor) || !HasComp<WFGravityAnchorComponent>(anchor))
            {
                _staleBeams.Add(anchor);
                continue;
            }

            // Another hull's anchor is that hull's own sweep to reconcile, never this one's.
            if (!TryGetOwner(anchor, out var owner) || owner.Owner != ent.Owner)
                continue;

            if (firing && (anchor == a || anchor == b))
                continue;

            _staleBeams.Add(anchor);
        }

        foreach (var anchor in _staleBeams)
        {
            if (_skyBeams.Remove(anchor, out var beam))
                QueueDel(beam);
        }
    }

    /// <summary>
    /// Kicks the cameras of anyone standing near the cut, every SiteKickInterval seconds.
    /// This is the only shaking mechanism with a falloff: the engine's grid shake has no radius at all, which is why
    /// the site is never shaken and the hull is.
    /// </summary>
    private void UpdateSiteEffects(Entity<WFPlanetCrackerComponent> ent)
    {
        if (ent.Comp.State != WFCrackState.Cracking || ent.Comp.PendingAbort is not null)
        {
            _nextSiteKick.Remove(ent.Owner);
            return;
        }

        if (!TryGetTargetedPair(ent, out var a, out var b) || !TryGetCircle(a.Owner, b.Owner, out var centre, out var radius))
            return;

        if (_nextSiteKick.TryGetValue(ent.Owner, out var next) && _timing.CurTime < next)
            return;

        _nextSiteKick[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.SiteKickInterval);

        KickCamerasInRange(
            new MapCoordinates(centre, Transform(a.Owner).MapID),
            radius + ent.Comp.SiteKickPadding,
            1f);
    }

    /// <summary>
    /// The explosion system's camera shake, copied rather than called: its own is private.
    /// Public because the chunk extraction throws one hard kick of its own at the same site.
    /// </summary>
    public void KickCamerasInRange(MapCoordinates epicentre, float range, float strength)
    {
        if (range <= 0f)
            return;

        var players = Filter.Empty();
        players.AddInRange(epicentre, range, _playerManager, EntityManager);

        foreach (var player in players.Recipients)
        {
            if (player.AttachedEntity is not { } uid)
                continue;

            var delta = epicentre.Position - TransformSystem.GetWorldPosition(uid);

            if (delta.EqualsApprox(Vector2.Zero))
                delta = new Vector2(0.01f, 0f);

            var distance = delta.Length();

            if (distance > range)
                continue;

            _recoil.KickCamera(uid, -delta.Normalized() * strength * (1f - distance / range));
        }
    }
}
