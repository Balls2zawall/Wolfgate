using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._WF.Wolfmed.Compat;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.Wolfmed;

/// <summary>
/// The Wolfmed damage bridge: damage aimed at a wound host lands on a body part and projects back onto the mob,
/// nothing double-applies, and entities without WoundHostComponent behave exactly as they did before the port.
/// </summary>
/// <remarks>
/// PLAN §6.2. Covers T-SETUP, T-RESULT, T-PIERCE, T-CAUSTIC, T12, the non-wound-host control and no-double-apply.
/// </remarks>
[TestFixture]
[TestOf(typeof(WoundDamageRoutingSystem))]
public sealed class WolfmedDamageBridgeTest : GameTest
{
    /// <summary>Two mobs on the same Shitmed body graph; the control is the bridge body minus WoundHost.</summary>
    [TestPrototypes]
    private const string Prototypes = @"
- type: body
  id: WolfmedBridgeBodyGraph
  name: ""wolfmed bridge body""
  root: torso
  slots:
    torso:
      part: TorsoHuman
      connections:
      - head
      - left arm
      - right arm
    head:
      part: HeadHuman
    left arm:
      part: LeftArmHuman
    right arm:
      part: RightArmHuman

- type: entity
  id: WolfmedBridgeBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Targeting
  - type: WoundHost

- type: entity
  id: WolfmedControlBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      100: Critical
      200: Dead
  - type: Targeting

- type: entity
  id: WolfmedBridgeHitscan
  components:
  - type: HitscanBasicDamage
    damage:
      types:
        Blunt: 10
";

    /// <summary>T-SETUP: every part of a real humanoid is woundable after map-init, and the D30 seeding survives.</summary>
    [Test]
    public async Task EveryPartGetsWoundableOnMapInitTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var map = await Pair.CreateTestMap();
        var missing = new List<string>();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("MobHuman", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var projection = entities.System<WoundDamageProjectionSystem>();

            Assert.That(entities.HasComponent<WoundHostComponent>(body), Is.True,
                "MobHuman is not a wound host; WP7's BaseMobSpeciesOrganic wiring is missing.");

            var parts = graph.GetBodyChildren(body).ToList();
            Assert.That(parts, Is.Not.Empty);
            foreach (var part in parts)
            {
                if (!entities.HasComponent<WoundableComponent>(part.Id))
                    missing.Add($"{entities.ToPrettyString(part.Id)} has no WoundableComponent");
                if (!entities.HasComponent<DamageableComponent>(part.Id))
                    missing.Add($"{entities.ToPrettyString(part.Id)} has no DamageableComponent");
            }

            // D30: SetDamage zeroes types missing from the argument instead of pruning them, so the container
            // seeding DamageableInit performed must survive a projection pass.
            var expected = SupportedTypeCount(prototypes, "Biological");
            projection.RefreshBodyDamage(body);
            Assert.That(entities.GetComponent<DamageableComponent>(body).Damage.DamageDict, Has.Count.EqualTo(expected));
        });

        Assert.That(missing, Is.Empty, string.Join("\n", missing));
    }

    /// <summary>T-RESULT: TryChangeDamage must report the routed damage, not null (D27).</summary>
    [Test]
    public async Task TryChangeDamageReportsRoutedDamageTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
            var damage = entities.System<DamageableSystem>();

            var dealt = damage.TryChangeDamage(body, Spec("Blunt", 10), targetPart: TargetBodyPart.LeftArm);

            Assert.That(dealt, Is.Not.Null, "the routed pass returned null; D27's BeforeDamageChangedEvent.Applied is not wired");
            Assert.That(dealt!.Empty, Is.False);
            Assert.That(dealt.GetTotal(), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(entities.GetComponent<DamageableComponent>(body).TotalDamage, Is.EqualTo(FixedPoint2.New(10)));
        });
    }

    /// <summary>T-PIERCE: a hitscan must keep damaging entities behind a wound host.</summary>
    [Test]
    public async Task PiercingHitscanDamagesEntitiesBehindAWoundHostTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var host = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
            var behind = entities.SpawnEntity("WolfmedControlBody", map.GridCoords);
            var hitscan = entities.SpawnEntity("WolfmedBridgeHitscan", map.GridCoords);
            var gun = entities.SpawnEntity(null, map.GridCoords);

            var args = new HitscanRaycastFiredEvent
            {
                FromCoordinates = map.GridCoords,
                ShotDirection = default,
                HitEntities = new HashSet<EntityUid> { host, behind },
                Gun = gun,
                Shooter = null,
                DistanceTried = 5f,
            };
            entities.EventBus.RaiseLocalEvent(hitscan, ref args);

            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<DamageableComponent>(host).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(entities.GetComponent<DamageableComponent>(behind).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
            });
        });
    }

    /// <summary>T-CAUSTIC: Caustic is a localized type and routes to the hit part as a burn (D20 reversed).</summary>
    [Test]
    public async Task CausticRoutesToTheHitPartTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var damage = entities.System<DamageableSystem>();
            var wounds = entities.System<WoundSystem>();
            var leftArm = graph.GetBodyChildren(body)
                .Single(part => part.Component.PartType == Content.Shared.Body.Part.BodyPartType.Arm &&
                                part.Component.Symmetry == Content.Shared.Body.Part.BodyPartSymmetry.Left).Id;

            Assert.That(damage.TryChangeDamage(body, Spec("Caustic", 10), targetPart: TargetBodyPart.LeftArm), Is.Not.Null);

            var arm = entities.GetComponent<DamageableComponent>(leftArm);
            Assert.That(arm.Damage.DamageDict[new ProtoId<DamageTypePrototype>("Caustic")], Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(wounds.GetWounds((leftArm, entities.GetComponent<WoundableComponent>(leftArm)))
                .Any(wound => wound.Comp.Prototype == new ProtoId<WoundPrototype>("BurnWound")), Is.True);
            Assert.That(entities.GetComponent<SystemicDamageComponent>(body).Damage.Empty, Is.True);
        });
    }

    /// <summary>T12: DamageChangedEvent.DamageDelta must survive the projection (§8.3 trap 5).</summary>
    [Test]
    public async Task DamageChangedDeltaSurvivesProjectionTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
            var damage = entities.System<DamageableSystem>();
            var sleeping = entities.System<SleepingSystem>();

            // SleepingSystem.OnDamageChanged early-returns on a null DamageDelta, so waking up is a direct
            // observation that the projection reported a real delta. It is one of the eight systems trap 5 lists.
            Assert.That(sleeping.TrySleeping(body), Is.True);
            Assert.That(entities.HasComponent<SleepingComponent>(body), Is.True);

            Assert.That(damage.TryChangeDamage(body, Spec("Blunt", 10), targetPart: TargetBodyPart.LeftArm), Is.Not.Null);
            Assert.That(entities.HasComponent<SleepingComponent>(body), Is.False,
                "the wound host did not wake: DamageChangedEvent.DamageDelta was lost in the projection");
        });
    }

    /// <summary>D2: an entity without WoundHostComponent behaves exactly as it did before the port.</summary>
    [Test]
    public async Task NonWoundHostUnchangedTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WolfmedControlBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var damage = entities.System<DamageableSystem>();
            var leftArm = graph.GetBodyChildren(body)
                .Single(part => part.Component.PartType == Content.Shared.Body.Part.BodyPartType.Arm &&
                                part.Component.Symmetry == Content.Shared.Body.Part.BodyPartSymmetry.Left).Id;

            Assert.That(damage.TryChangeDamage(body, Spec("Blunt", 10), targetPart: TargetBodyPart.LeftArm), Is.Not.Null);

            Assert.Multiple(() =>
            {
                // Shitmed's own spreading still runs: the body takes the hit through GetPartDamageModifier(Arm).
                Assert.That(entities.GetComponent<DamageableComponent>(body).TotalDamage, Is.EqualTo(FixedPoint2.New(7)));
                Assert.That(entities.GetComponent<DamageableComponent>(leftArm).TotalDamage, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(entities.HasComponent<WoundableComponent>(leftArm), Is.False);
                Assert.That(entities.HasComponent<SystemicDamageComponent>(body), Is.False);
            });
        });
    }

    /// <summary>T4: one hit lands once — on the parts plus systemic, and once on the projected body total.</summary>
    [Test]
    public async Task NoDoubleApplicationTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var damage = entities.System<DamageableSystem>();

            Assert.That(damage.TryChangeDamage(body, Spec("Blunt", 10), targetPart: TargetBodyPart.LeftArm), Is.Not.Null);

            var partTotal = graph.GetBodyChildren(body).Aggregate(FixedPoint2.Zero,
                (total, part) => total + entities.GetComponent<DamageableComponent>(part.Id).TotalDamage);
            var systemic = entities.GetComponent<SystemicDamageComponent>(body).Damage.GetTotal();

            Assert.Multiple(() =>
            {
                Assert.That(partTotal + systemic, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(entities.GetComponent<DamageableComponent>(body).TotalDamage, Is.EqualTo(FixedPoint2.New(10)));
            });
        });
    }

    /// <summary>Number of damage types DamageableInit seeds for a container: its types plus every type of its groups.</summary>
    private static int SupportedTypeCount(IPrototypeManager prototypes, string containerId)
    {
        var container = prototypes.Index<DamageContainerPrototype>(containerId);
        var types = new HashSet<string>(container.SupportedTypes);
        foreach (var groupId in container.SupportedGroups)
            types.UnionWith(prototypes.Index<DamageGroupPrototype>(groupId).DamageTypes);

        return types.Count;
    }

    private static DamageSpecifier Spec(string type, int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
    };
}
