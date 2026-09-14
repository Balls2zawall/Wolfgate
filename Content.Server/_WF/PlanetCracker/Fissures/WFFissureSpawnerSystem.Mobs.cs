using System.Numerics;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.HTN;
using Content.Shared._WF.PlanetCracker.Fissures;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Storage;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._WF.PlanetCracker.Fissures;

/// <summary>
/// What climbs out of a fissure: the cumulative cap, the faction roll and the site-threat stamp. No subscriptions.
/// </summary>
public sealed partial class WFFissureSpawnerSystem
{
    /// <summary>The HTN compound a stamped threat is re-rooted on, so it goes for the anchor before it goes for people.</summary>
    private const string ThreatCompound = "WFFissureThreatCompound";

    /// <summary>One mob per fissure up to the roll, the ring's own tile count and whatever is left of the cap.</summary>
    private void SpawnRingMobs(
        Entity<WFFissureSpawnerComponent> ent,
        Entity<MapGridComponent> ground,
        EntityUid anchor,
        List<Vector2i> chosen)
    {
        var comp = ent.Comp;

        // A world with no faction still gets its decals, its burst and its crack sound - it just has nothing to send up.
        if (comp.Faction is not { } id || !_proto.TryIndex(id, out var faction))
            return;

        var want = _random.Next(comp.MinMobs, comp.MaxMobs + 1);

        if (!comp.Sanctioned)
            want = (int)MathF.Ceiling(want * comp.UnsanctionedMultiplier);

        want = Math.Min(want, chosen.Count);

        // CUMULATIVE: SpawnedTotal never comes back down, so killing what is already out cannot re-open the budget.
        want = Math.Min(want, comp.Cap - comp.SpawnedTotal);

        if (want <= 0)
            return;

        for (var i = 0; i < want; i++)
        {
            if (TrySpawnThreat(ent, ground, anchor, faction, chosen[i]))
                comp.SpawnedTotal++;
        }
    }

    /// <summary>Rolls one mob out of the faction and puts it on a tile, returning false only if nothing was spawned.</summary>
    private bool TrySpawnThreat(
        Entity<WFFissureSpawnerComponent> ent,
        Entity<MapGridComponent> ground,
        EntityUid anchor,
        SalvageFactionPrototype faction,
        Vector2i index)
    {
        if (faction.MobGroups.Count == 0)
            return false;

        string? proto = null;

        // BOUNDED RE-ROLL. EntitySpawnCollection.GetSpawns can legitimately come back EMPTY:
        // Resources/Prototypes/Procedural/salvage_factions.yml:17-21 is a Xenos group whose only entry is
        // `NFMobXenoDrone amount: 0 maxAmount: 2`, and GetAmount (Content.Shared/Storage/EntitySpawnEntry.cs:252-265)
        // returns random.Next(0, 2), so it yields nothing about half the time that group is drawn. A granted slot
        // re-rolls rather than being silently lost, and SpawnedTotal only counts an actual spawn, so an abandoned slot
        // never consumes the cap either.
        for (var attempt = 0; attempt < ent.Comp.MobRollRetries && proto is null; attempt++)
        {
            var group = RollGroup(faction);
            var spawns = EntitySpawnCollection.GetSpawns(group.Entries, _random);

            if (spawns.Count == 0)
                continue;

            // ONE prototype per fissure: the design counts MOBS, not groups.
            proto = _random.Pick(spawns);
        }

        if (proto is null)
            return false;

        var coords = new EntityCoordinates(ground.Owner, (Vector2)index + ground.Comp.TileSizeHalfVector);

        // The SpawnSalvageMissionJob.cs:525-530 sequence. The ghost-role strip is mandatory: several faction entries
        // ship GhostRole blocks and would otherwise flood the ghost-role panel every ring.
        var uid = EntityManager.CreateEntityUninitialized(proto, coords);
        RemComp<GhostTakeoverAvailableComponent>(uid);
        RemComp<GhostRoleComponent>(uid);
        EntityManager.InitializeAndStartEntity(uid);

        // STAMP BY LOOKUP, never by assuming `uid` is the mob. Argocytes, Flesh and Dinosaurs list RandomSpawner MARKER
        // entities (Resources/Prototypes/_NF/Entities/Markers/Spawners/Random/mobs_hostile_argocyte.yml:4-16), and
        // ConditionalSpawnerSystem.OnRandSpawnMapInit (Content.Server/Spawners/EntitySystems/ConditionalSpawnerSystem.cs:34-39)
        // fires synchronously inside InitializeAndStartEntity and QueueDels the marker, leaving the real mob within
        // RandomSpawnerComponent.Offset of the tile centre.
        _lookupBuffer.Clear();
        _lookup.GetEntitiesInRange(coords, 1.0f, _lookupBuffer, LookupFlags.Uncontained);

        foreach (var candidate in _lookupBuffer)
        {
            if (!TryComp<HTNComponent>(candidate, out var htn) || ent.Comp.Live.Contains(candidate))
                continue;

            StampThreat(ent, candidate, htn, anchor);
        }

        return true;
    }

    /// <summary>
    /// Makes one spawned mob a site threat: the anchor-first HTN root, the faction exception against the anchor and the
    /// emerge effect and sound. An entry with no HTN (WeaponTurretXeno, salvage_factions.yml:22-25) is left unstamped -
    /// it still spawns and is still hostile to players, it just never joins <see cref="WFFissureSpawnerComponent.Live"/>.
    /// </summary>
    private void StampThreat(Entity<WFFissureSpawnerComponent> ent, EntityUid mob, HTNComponent htn, EntityUid anchor)
    {
        htn.RootTask = new HTNCompoundTask { Task = ThreatCompound };

        // Force an immediate replan rather than waiting out whatever the stock root had already queued.
        htn.Plan = null;
        htn.PlanAccumulator = 0f;

        // Half one of the aggro: the anchor joins FactionExceptionComponent.Hostiles, which GetNearbyHostiles unions in
        // unfiltered by range. Half two is WFFissureTargets' TargetIsAliveOrNACon; either half alone is inert.
        // Do NOT widen the blackboard MeleeRange or MeleeWeaponComponent.Range to "reach" the 3x3 anchor - the damage
        // lands through NPCSteeringSystem's NavSmash obstacle branch (Obstacles.cs:162-194), which has no range check,
        // and widening either would break these mobs against players.
        _npcFaction.AggroEntity(mob, anchor);

        // Parented to the MOB, the WhistleSystem.cs:25 idiom. Transform(mob).Coordinates would parent it to the ground
        // GRID instead, and the effect would not follow a mob that moves during its 0.48 s.
        SpawnAttachedTo(ent.Comp.EmergeEffect, new EntityCoordinates(mob, Vector2.Zero));
        _audio.PlayPvs(ent.Comp.EmergeSound, mob);

        ent.Comp.Live.Add(mob);
    }

    /// <summary>
    /// One cumulative weighted draw over the faction's mob groups.
    /// Deliberately NOT the upstream shape at Content.Server/Salvage/SpawnSalvageMissionJob.cs:496-504, which walks the
    /// cumulative sum and then throws the result away with a flat random.Next over the group list.
    /// </summary>
    private SalvageMobGroup RollGroup(SalvageFactionPrototype faction)
    {
        var sum = 0f;

        foreach (var group in faction.MobGroups)
        {
            sum += group.Prob;
        }

        var roll = _random.NextFloat() * sum;
        var running = 0f;

        foreach (var group in faction.MobGroups)
        {
            running += group.Prob;

            if (running >= roll)
                return group;
        }

        return faction.MobGroups[^1];
    }
}
