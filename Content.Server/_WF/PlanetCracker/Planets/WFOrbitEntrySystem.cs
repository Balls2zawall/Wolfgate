using System.Diagnostics.CodeAnalysis;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.PlanetCracker.Planets;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._WF.PlanetCracker.Planets;

/// <summary>
/// Entering and leaving planet orbit from the shuttle console, which needs no FTL drive: the orbit layer is not an FTL
/// destination at all any more. This system owns both gates and both hops; the transit itself is the ordinary FTL
/// machinery, reached through <see cref="ShuttleSystem.WfFTLToLayer"/>.
/// </summary>
public sealed partial class WFOrbitEntrySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    /// <summary>How often a console's orbit readout is recomputed; a hull crosses the range band over minutes.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>Sector bodies with a live orbit layer, rebuilt once per sweep rather than per console.</summary>
    private readonly List<(EntityUid Body, EntityUid Orbit, float Range)> _bodies = new();

    private TimeSpan _nextRefresh;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<WFEnterPlanetOrbitMessage>(OnEnterOrbitMessage);
            subs.Event<WFLeavePlanetOrbitMessage>(OnLeaveOrbitMessage);
        });
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        Refresh();
    }

    /// <summary>Recomputes what every shuttle console offers, so the button tracks the hull instead of the BUI state.</summary>
    private void Refresh()
    {
        _bodies.Clear();

        if (_cfg.GetCVar(PlanetCrackerCVars.PlanetNetworks))
        {
            var bodies = AllEntityQuery<WFSectorPlanetComponent>();

            while (bodies.MoveNext(out var uid, out var comp))
            {
                if (comp.OrbitMap is not { } orbitNet
                    || !TryGetEntity(orbitNet, out var orbitUid)
                    || !TryComp<WFOrbitLayerComponent>(orbitUid, out var orbit))
                {
                    continue;
                }

                _bodies.Add((uid, orbitUid.Value, orbit.Range));
            }
        }

        // Nothing to offer anywhere: sweep only what is already stamped rather than every console in the sector.
        // A standalone dev network has no sector body, so its orbit layer offers no climb out either.
        if (_bodies.Count == 0)
        {
            var stale = AllEntityQuery<WFConsoleOrbitTargetComponent>();

            while (stale.MoveNext(out var uid, out _))
            {
                RemCompDeferred<WFConsoleOrbitTargetComponent>(uid);
            }

            return;
        }

        var consoles = AllEntityQuery<ShuttleConsoleComponent, TransformComponent>();

        while (consoles.MoveNext(out var uid, out _, out var xform))
        {
            RefreshConsole(uid, xform);
        }
    }

    /// <summary>Writes one console's offer, adding or dropping the component so idle hulls carry nothing networked.</summary>
    private void RefreshConsole(EntityUid console, TransformComponent xform)
    {
        NetEntity? planet = null;
        var planetName = string.Empty;
        var inOrbit = false;
        var busy = false;

        if (xform.GridUid is { } grid && HasComp<ShuttleComponent>(grid) && Transform(grid).MapUid is { } mapUid)
        {
            busy = HasComp<FTLComponent>(grid);

            if (TryComp<WFOrbitLayerComponent>(mapUid, out var orbit))
            {
                if (orbit.Planet is { } netPlanet
                    && TryGetEntity(netPlanet, out var body)
                    && HasComp<WFSectorPlanetComponent>(body))
                {
                    inOrbit = true;
                    planet = netPlanet;
                    planetName = Name(body.Value);
                }
            }
            else if (TryGetNearestBody(grid, mapUid, out var nearest))
            {
                planet = GetNetEntity(nearest.Value);
                planetName = Name(nearest.Value);
            }
        }

        if (planet is null && !inOrbit)
        {
            RemCompDeferred<WFConsoleOrbitTargetComponent>(console);
            return;
        }

        var comp = EnsureComp<WFConsoleOrbitTargetComponent>(console);

        if (comp.Planet == planet && comp.PlanetName == planetName && comp.InOrbit == inOrbit && comp.Busy == busy)
            return;

        comp.Planet = planet;
        comp.PlanetName = planetName;
        comp.InOrbit = inOrbit;
        comp.Busy = busy;
        Dirty(console, comp);
    }

    /// <summary>
    /// The closest registered sector body on this map whose orbit the hull is inside range of. A hull already on a
    /// planet layer or riding a transit map is offered nothing: it climbs, or it leaves orbit.
    /// </summary>
    /// <param name="grid">The hull.</param>
    /// <param name="mapUid">The map the hull is on.</param>
    /// <param name="body">The nearest body in range.</param>
    private bool TryGetNearestBody(EntityUid grid, EntityUid mapUid, [NotNullWhen(true)] out EntityUid? body)
    {
        body = null;

        if (HasComp<WFPlanetLayerComponent>(mapUid) || HasComp<CEZTransitMapComponent>(mapUid))
            return false;

        var gridPos = _transform.GetWorldPosition(grid);
        var best = float.MaxValue;

        foreach (var candidate in _bodies)
        {
            var bodyXform = Transform(candidate.Body);

            if (bodyXform.MapUid != mapUid)
                continue;

            var distance = (_transform.GetWorldPosition(bodyXform) - gridPos).LengthSquared();

            if (distance > candidate.Range * candidate.Range || distance >= best)
                continue;

            best = distance;
            body = candidate.Body;
        }

        return body != null;
    }

    /// <summary>
    /// Drops the console's hull onto a sector body's orbit layer, at the same world spot it occupied in the sector.
    /// Every condition is re-checked here: the client's button is a readout, never the authority.
    /// </summary>
    /// <param name="console">The shuttle console the request came from.</param>
    /// <param name="planetUid">The sector body to orbit.</param>
    /// <param name="reason">Why the hop was refused, already localised.</param>
    public bool TryEnterOrbit(EntityUid console, EntityUid planetUid, [NotNullWhen(false)] out string? reason)
    {
        if (!TryGetHull(console, out var hull, out reason))
            return false;

        if (!TryComp<WFSectorPlanetComponent>(planetUid, out var sector)
            || sector.OrbitMap is not { } orbitNet
            || !TryGetEntity(orbitNet, out var orbitUid)
            || !TryComp<WFOrbitLayerComponent>(orbitUid, out var orbit))
        {
            reason = Loc.GetString("wf-orbit-no-network", ("planet", Name(planetUid)));
            return false;
        }

        var mapUid = Transform(hull.Value.Owner).MapUid;

        if (mapUid is null
            || mapUid != Transform(planetUid).MapUid
            || HasComp<WFPlanetLayerComponent>(mapUid.Value)
            || HasComp<CEZTransitMapComponent>(mapUid.Value))
        {
            reason = Loc.GetString("wf-orbit-not-in-sector", ("planet", Name(planetUid)));
            return false;
        }

        var worldPos = _transform.GetWorldPosition(hull.Value.Owner);

        if ((_transform.GetWorldPosition(planetUid) - worldPos).LengthSquared() > orbit.Range * orbit.Range)
        {
            reason = Loc.GetString("wf-orbit-out-of-range", ("planet", Name(planetUid)));
            return false;
        }

        if (!_shuttle.CanFTL(hull.Value.Owner, out reason))
            return false;

        if (!_shuttle.WfFTLToLayer(hull.Value, orbitUid.Value, worldPos))
        {
            reason = Loc.GetString("wf-orbit-refused");
            return false;
        }

        _nextRefresh = TimeSpan.Zero;
        return true;
    }

    /// <summary>
    /// Climbs the console's hull out of orbit onto the sector map, at the same world spot - which is beside the body,
    /// because an orbit layer shares the sector's frame with the body at its centre.
    /// </summary>
    /// <param name="console">The shuttle console the request came from.</param>
    /// <param name="reason">Why the hop was refused, already localised.</param>
    public bool TryLeaveOrbit(EntityUid console, [NotNullWhen(false)] out string? reason)
    {
        if (!TryGetHull(console, out var hull, out reason))
            return false;

        if (Transform(hull.Value.Owner).MapUid is not { } mapUid || !TryComp<WFOrbitLayerComponent>(mapUid, out var orbit))
        {
            reason = Loc.GetString("wf-orbit-not-in-orbit");
            return false;
        }

        if (orbit.Planet is not { } netPlanet
            || !TryGetEntity(netPlanet, out var body)
            || Transform(body.Value).MapUid is not { } sectorMap)
        {
            reason = Loc.GetString("wf-orbit-no-sector");
            return false;
        }

        if (!_shuttle.CanFTL(hull.Value.Owner, out reason))
            return false;

        if (!_shuttle.WfFTLToLayer(hull.Value, sectorMap, _transform.GetWorldPosition(hull.Value.Owner)))
        {
            reason = Loc.GetString("wf-orbit-refused");
            return false;
        }

        _nextRefresh = TimeSpan.Zero;
        return true;
    }

    /// <summary>The flyable hull a console sits on.</summary>
    private bool TryGetHull(EntityUid console, [NotNullWhen(true)] out Entity<ShuttleComponent>? hull, [NotNullWhen(false)] out string? reason)
    {
        hull = null;
        reason = null;

        if (Transform(console).GridUid is not { } grid || !TryComp<ShuttleComponent>(grid, out var shuttle))
        {
            reason = Loc.GetString("wf-orbit-no-hull");
            return false;
        }

        hull = (grid, shuttle);
        return true;
    }

    /// <summary>Console request to enter orbit; the actor has to be the pilot of this very console.</summary>
    private void OnEnterOrbitMessage(EntityUid uid, ShuttleConsoleComponent component, WFEnterPlanetOrbitMessage args)
    {
        if (GetEntity(args.Console) != uid || !IsPilot(args.Actor, uid) || !TryGetEntity(args.Planet, out var planet))
            return;

        if (!TryEnterOrbit(uid, planet.Value, out var reason))
            _popup.PopupEntity(reason, uid, args.Actor);
    }

    /// <summary>Console request to leave orbit; the actor has to be the pilot of this very console.</summary>
    private void OnLeaveOrbitMessage(EntityUid uid, ShuttleConsoleComponent component, WFLeavePlanetOrbitMessage args)
    {
        if (GetEntity(args.Console) != uid || !IsPilot(args.Actor, uid))
            return;

        if (!TryLeaveOrbit(uid, out var reason))
            _popup.PopupEntity(reason, uid, args.Actor);
    }

    /// <summary>True when this actor is currently piloting this console.</summary>
    private bool IsPilot(EntityUid actor, EntityUid console)
    {
        return TryComp<PilotComponent>(actor, out var pilot) && pilot.Console == console;
    }
}
