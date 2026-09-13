using System.Linq;
using Content.IntegrationTests.Fixtures;
// WOLFGATE: D7, Onyx's Content.Shared._Onyx.Medical.Surgery is not ported, so SurgeryStepEvent and the
// SurgicalHealRemovesConsequenceAndUnblocks test that needs it are absent (see the skip notes below).
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12, GetAllDamage/SetDamage live on the compat facade.
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

/// <summary>
/// Onyx's AmputationConsequenceTest, reduced to the three cases that survive the port.
/// </summary>
/// <remarks>
/// PLAN3 §6.2 T-AMP-CONSEQUENCE-1/-2/-3 (WP11-5). Two of Onyx's five tests are deliberately absent:
/// <list type="bullet">
/// <item>SurgicalHealRemovesConsequenceAndUnblocksTest - needs SurgeryStepEvent and
/// SurgeryTreatWoundEffect from Onyx's own surgery, which D7 leaves out (phase 4).</item>
/// <item>HealingDamageKeepsConsequenceBlockedTest - its entire payload is
/// `graph.TryAttachPart(torso, spare) Is.False`, and B-2/P3-D2 ships AmputationConsequenceWound inert:
/// Shitmed's CanAttachPart has no consequence gate and nothing in phase 3 can clear the wound, so
/// hooking it would make re-attachment permanently impossible. The wound is a marker for examine and
/// phase-4 surgery only.</item>
/// </list>
/// For the same reason the surviving test drops Onyx's `graph.HasAmputationConsequence(torso)` and
/// `graph.TryAttachPart(torso, spare) Is.False` assertions - neither mechanism exists in Wolfgate.
/// </remarks>
[TestFixture]
[TestOf(typeof(AmputationSystem))]
public sealed class AmputationConsequenceTest : GameTest
{
    // WOLFGATE: Onyx's fixture is Nubody-shaped (`InitialBody`, `TransplantCompatibility`, a bespoke
    // `bodyPartProfile`, `partType: Chest`). Rebuilt on a Shitmed body graph like WoundBleedingTest's, with
    // the two Onyx part-level datafields moved onto `- type: WolfmedBodyPart` (D8). Both parts inherit the
    // real human parts so they pick up Woundable/Damageable/vital exactly as a live limb does.
    //
    // The torso's `amputationConsequenceSeverity: 50` is load-bearing and must not be dropped (PLAN3
    // §8.7 hazard 7 / CRITIQUE3 M3-2): WolfmedBodyPartComponent defaults the field to 35 AND
    // WolfmedBodyPartSystem.Get's zeroed fallback singleton also reads 35, so on a stock body an
    // implementation that wrongly read the severity off the SEVERED PART instead of the parent stump
    // would still assert green. 50 is the only value that can tell the two apart.
    [TestPrototypes]
    private const string Prototypes = @"
- type: body
  id: AmputationConsequenceTestGraph
  name: ""amputation consequence test body""
  root: torso
  slots:
    torso:
      part: AmputationConsequenceTestTorso
      connections:
      - head
    head:
      part: AmputationConsequenceTestHead

- type: entity
  id: AmputationConsequenceTestBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: AmputationConsequenceTestGraph
  - type: Damageable
    damageContainer: Biological
  - type: WoundHost

- type: entity
  id: AmputationConsequenceTestTorso
  parent: TorsoHuman
  components:
  - type: WolfmedBodyPart
    amputationConsequenceSeverity: 50

- type: entity
  id: AmputationConsequenceTestHead
  parent: HeadHuman
  components:
  - type: WolfmedBodyPart
    amputationThresholds:
      Slash: 70
";

    /// <summary>PLAN3 §6.2 T-AMP-CONSEQUENCE-1.</summary>
    [Test]
    public async Task TraumaticAmputationCreatesConsequenceTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var damage = entities.System<WolfmedDamageableSystem>(); // WOLFGATE: D12
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id; // WOLFGATE: D9

            // The fixture head's Slash threshold is 70, so progress = 70/70 = 1.0 arms the limb without
            // detaching it (AmputationSystem.HandlePartDamageApplied's `!Severable` branch returns).
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 70)));
            Assert.Multiple(() =>
            {
                Assert.That(graph.BodyHasChild(body, head), Is.True);
                Assert.That(entities.GetComponent<WoundableComponent>(head).Severable, Is.True);
            });

            // Slash 15 is exactly DefaultDismembermentFinishingDamage["Slash"], and damageBeforeHit (70)
            // is still at the threshold, so this hit detaches.
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 15)));

            var wounds = entities.System<WoundSystem>()
                .GetWounds((torso, entities.GetComponent<WoundableComponent>(torso)))
                .ToList();
            var consequence = wounds
                .Single(wound => wound.Comp.Prototype == new ProtoId<WoundPrototype>("AmputationConsequenceWound"));

            Assert.Multiple(() =>
            {
                Assert.That(graph.BodyHasChild(body, head), Is.False);
                // WOLFGATE: 50 comes from the fixture TORSO's amputationConsequenceSeverity, i.e. the
                // parent the stump is left on - not from the severed head, which carries the 35 default.
                Assert.That(consequence.Comp.Severity, Is.EqualTo(FixedPoint2.New(50)),
                    "the consequence severity must be read off the parent stump, not the severed part.");
                // WOLFGATE: DismembermentSeverities[Head] = 200; also lands on the parent.
                Assert.That(wounds.Count(wound =>
                        wound.Comp.Prototype == new ProtoId<WoundPrototype>("DismembermentWound")),
                    Is.EqualTo(1));
                // Onyx's assertion, kept: AmputationConsequenceWound has `damageTypes: {}`, so creating it
                // must not put any damage on the stump.
                Assert.That(damage.GetAllDamage(torso).GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    /// <summary>PLAN3 §6.2 T-AMP-CONSEQUENCE-2. A heal is never a finishing hit.</summary>
    [Test]
    public async Task HealingPartAboveThresholdDoesNotAmputateTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var damage = entities.System<WolfmedDamageableSystem>(); // WOLFGATE: D12
            var head = graph.GetBodyChildren(body)
                .Single(part => part.Component.PartType == BodyPartType.Head).Id;

            // SetDamage bypasses routing, so the part sits above its 70 threshold without ever having been
            // marked Severable - exactly Onyx's setup.
            damage.SetDamage(head, Spec("Slash", 80));
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", -1)));

            Assert.Multiple(() =>
            {
                Assert.That(graph.BodyHasChild(body, head), Is.True,
                    "a negative damage application must never detach a limb.");
                // 80 - 1 = 79. IsFinishingHit skips every entry with amount <= 0, so the heal cannot
                // finish the limb no matter how far past the threshold the part is.
                Assert.That(damage.GetAllDamage(head).DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(79)));
            });
        });
    }

    /// <summary>PLAN3 §6.2 T-AMP-CONSEQUENCE-3. The full Severable set/reset cycle.</summary>
    [Test]
    public async Task HealingBelowResetRatioClearsSeverableTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entities.SpawnEntity("AmputationConsequenceTestBody", map.GridCoords);
            var graph = entities.System<SharedBodySystem>();
            var routing = entities.System<WoundDamageRoutingSystem>();
            var head = graph.GetBodyChildren(body)
                .Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var woundable = entities.GetComponent<WoundableComponent>(head);

            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 70)));
            Assert.That(woundable.Severable, Is.True);

            // 70 - 15 = 55; progress 55/70 = 0.786 < WoundHostComponent.SeverableResetRatio (0.8), so the
            // limb is disarmed again.
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", -15)));
            Assert.That(woundable.Severable, Is.False);

            // Back to 70 - but damageBeforeHit is 55, whose progress (0.786) is below 1, so ReachedThreshold
            // is false and this hit only re-arms the limb instead of taking it off.
            Assert.That(routing.TryApplyPartDamage(body, head, Spec("Slash", 15)));
            Assert.Multiple(() =>
            {
                Assert.That(graph.BodyHasChild(body, head), Is.True);
                Assert.That(woundable.Severable, Is.True);
            });
        });
    }

    private static DamageSpecifier Spec(string type, int amount) => new()
    {
        DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
    };
}
