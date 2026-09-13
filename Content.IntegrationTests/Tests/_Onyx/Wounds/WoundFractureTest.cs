using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundFractureSystem))]
public sealed class WoundFractureTest : GameTest
{
    // WOLFGATE: Shitmed body graph instead of Onyx's Nubody `InitialBody`; Chest → Torso (D9); the armour has no
    // `coverage` in Wolfgate, so it protects every part — the leg reduction the test measures is unchanged.
    [TestPrototypes]
    private const string Prototypes = @"
- type: body
  id: WoundFractureBodyGraph
  name: ""wound fracture body""
  root: torso
  slots:
    torso:
      part: TorsoHuman
      connections:
      - left arm
      - left leg
    left arm:
      part: LeftArmHuman
    left leg:
      part: LeftLegHuman

- type: entity
  id: WoundFractureBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: WoundFractureBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MovementSpeedModifier
  - type: WoundHost

- type: entity
  id: WoundFractureArmor
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 0.5
";

    [Test]
    public async Task GradeBoundariesAreDeterministicTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            ProtoId<FractureProfilePrototype> profileId = "OrganicFractureProfile";
            var profile = prototypes.Index(profileId);
            // WOLFGATE: Onyx's literals (15/30/50/75) are stale against its own pinned prototype. The vendored
            // OrganicFractureProfile is byte-identical to Onyx's and declares 20/35/50/60, so the boundaries
            // below are the profile's, not a Wolfgate behaviour change. Re-check on any Onyx re-sync.
            Assert.Multiple(() =>
            {
                Assert.That(WoundFractureSystem.GetGrade(profile, 19), Is.EqualTo(FractureGrade.None));
                Assert.That(WoundFractureSystem.GetGrade(profile, 20), Is.EqualTo(FractureGrade.Hairline));
                Assert.That(WoundFractureSystem.GetGrade(profile, 34), Is.EqualTo(FractureGrade.Hairline));
                Assert.That(WoundFractureSystem.GetGrade(profile, 35), Is.EqualTo(FractureGrade.Simple));
                Assert.That(WoundFractureSystem.GetGrade(profile, 49), Is.EqualTo(FractureGrade.Simple));
                Assert.That(WoundFractureSystem.GetGrade(profile, 50), Is.EqualTo(FractureGrade.Displaced));
                Assert.That(WoundFractureSystem.GetGrade(profile, 59), Is.EqualTo(FractureGrade.Displaced));
                Assert.That(WoundFractureSystem.GetGrade(profile, 60), Is.EqualTo(FractureGrade.Comminuted));
            });
        });
    }

    [Test]
    public async Task PostArmorHitAndTreatmentPreconditionsTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundFractureBody", map.GridCoords);
            var armor = entityManager.SpawnEntity("WoundFractureArmor", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var inventory = entityManager.System<InventorySystem>();
            var routing = entityManager.System<WoundDamageRoutingSystem>();
            var fractures = entityManager.System<WoundFractureSystem>();
            var leg = graph.GetBodyChildren(body).Single(part => part.Component.PartType == BodyPartType.Leg).Id;

            Assert.That(inventory.TryEquip(body, armor, "outerClothing"));
            Assert.That(routing.TryApplyPartDamage(body, leg, Spec(150)));
            var fracture = fractures.GetFracture(leg).Value;
            Assert.That(fracture.Comp1.Severity, Is.EqualTo(FixedPoint2.New(75)));
            Assert.That(fracture.Comp2.Grade, Is.EqualTo(FractureGrade.Comminuted));
            Assert.That(fractures.TryMend(fracture.Owner));
            Assert.That(fractures.GetFracture(leg), Is.Null);
        });
    }

    // WOLFGATE: Onyx's EffectsRefreshOnTreatmentHealingAndDetachTest is not ported — it asserts against
    // FractureEffectSystem (movement modifier, GetDurationMultiplier), which PLAN §6.1 and WP10 defer to phase 2.

    private static DamageSpecifier Spec(int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>("Blunt")] = FixedPoint2.New(amount) },
    };
}
