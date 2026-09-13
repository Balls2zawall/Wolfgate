using Content.Shared._Mono.Shipyard;
using Content.Shared._WF.PlanetCracker.Anchors;
using Content.Shared._WF.PlanetCracker.Cracker;

namespace Content.Server._WF.PlanetCracker.Cracker;

/// <summary>
/// Stamps the anchors and crates riding on a cracker hull with that hull, so two crackers can never share a pair.
/// </summary>
public sealed partial class WFCrackerOwnershipSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // ShipyardShuttlePurchaseEvent carries no [ByRefEvent] and both raise sites are broadcast, so match them exactly.
        SubscribeLocalEvent<ShipyardShuttlePurchaseEvent>(OnPurchased);
    }

    /// <summary>A freshly bought cracker owns whatever anchors shipped aboard it.</summary>
    private void OnPurchased(ShipyardShuttlePurchaseEvent args)
    {
        if (HasComp<WFPlanetCrackerComponent>(args.Shuttle))
            BindAboard(args.Shuttle);
    }

    /// <summary>Stamps every unowned anchor and crate resting on this cracker as belonging to it; returns how many were bound.</summary>
    public int BindAboard(EntityUid cracker)
    {
        var owner = GetNetEntity(cracker);
        var bound = 0;

        var crates = EntityQueryEnumerator<WFAnchorCrateComponent, TransformComponent>();
        while (crates.MoveNext(out var uid, out var crate, out var xform))
        {
            if (xform.GridUid != cracker || crate.Cracker is not null)
                continue;

            crate.Cracker = owner;
            Dirty(uid, crate);
            bound++;
        }

        var anchors = EntityQueryEnumerator<WFGravityAnchorComponent, TransformComponent>();
        while (anchors.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (xform.GridUid != cracker || anchor.Cracker is not null)
                continue;

            anchor.Cracker = owner;
            Dirty(uid, anchor);
            bound++;
        }

        return bound;
    }
}
