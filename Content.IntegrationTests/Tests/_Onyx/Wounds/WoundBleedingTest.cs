using System.Linq;
using Content.IntegrationTests.Fixtures;
// WOLFGATE: D13 moves WoundBleedingSystem to Content.Server but keeps its Onyx namespace, so no extra using.
using Content.Server.Body.Components; // WOLFGATE: BloodstreamComponent is server-only here.
using Content.Server.Body.Systems; // WOLFGATE: BloodstreamSystem is server-only here.
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: §2.7 TryDetachPart.
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Onyx.Wounds;

[TestFixture]
[TestOf(typeof(WoundBleedingSystem))]
public sealed class WoundBleedingTest : GameTest
{
    // WOLFGATE: Shitmed body graph instead of Onyx's Nubody `InitialBody`; `Injurable` dropped (D19); Chest → Torso (D9).
    [TestPrototypes]
    private const string Prototypes = @"
- type: body
  id: WoundBleedingBodyGraph
  name: ""wound bleeding body""
  root: torso
  slots:
    torso:
      part: TorsoHuman
      connections:
      - head
    head:
      part: HeadHuman

- type: entity
  id: WoundBleedingBody
  parent: [InventoryBase, MobBloodstream]
  components:
  - type: Body
    prototype: WoundBleedingBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: WoundHost
";

    [Test]
    public async Task ProjectsTreatsAndTracksAttachmentTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var graph = entityManager.System<SharedBodySystem>();
            var wfBody = entityManager.System<WolfmedBodySystem>(); // WOLFGATE
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var parts = graph.GetBodyChildren(body).ToList();
            var head = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var torso = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id;
            var bloodstream = entityManager.GetComponent<BloodstreamComponent>(body);

            Assert.That(wounds.CreateOrMergeWound(head, "SlashWound", 15), Is.Not.Null);
            Assert.That(wounds.CreateOrMergeWound(torso, "PiercingWound", 10), Is.Not.Null);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(3f).Within(0.001f));
            Assert.That(entityManager.System<BloodstreamSystem>().TryModifyBleedAmount(body, 5f), Is.False);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(3f).Within(0.001f));

            var headWound = wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))).Single();
            Assert.That(bleeding.SetTreatment(headWound.Owner, BleedingTreatment.Bandaged));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(headWound).CurrentRate,
                Is.EqualTo(0.375f).Within(0.001f));
            Assert.That(bleeding.SetTreatment(headWound.Owner, BleedingTreatment.Bandaged));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(headWound).CurrentRate,
                Is.EqualTo(0.375f).Within(0.001f));
            Assert.That(bleeding.GetPartRate(torso), Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.875f).Within(0.001f));

            Assert.That(wfBody.TryDetachPart(head)); // WOLFGATE
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(graph.AttachPart(torso, "head", head)); // WOLFGATE
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1.875f).Within(0.001f));

            Assert.That(bleeding.ModifyBodyBleeding(body, -20f));
            Assert.That(bleeding.GetPartRate(head), Is.Zero);
            Assert.That(bleeding.GetPartRate(torso), Is.Zero);
            Assert.That(bloodstream.BleedAmount, Is.Zero);

            entityManager.EventBus.RaiseLocalEvent(body, new RejuvenateEvent());
            Assert.That(bloodstream.BleedAmount, Is.Zero);
            Assert.That(wounds.GetWounds((head, entityManager.GetComponent<WoundableComponent>(head))), Is.Empty);
            Assert.That(bleeding.ModifyBodyBleeding(body, 1f));
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(1f).Within(0.001f));
            Assert.That(graph.GetBodyChildren(body).SelectMany(part =>
                    wounds.GetWounds((part.Id, entityManager.GetComponent<WoundableComponent>(part.Id))))
                .Single().Comp.Prototype, Is.EqualTo(new ProtoId<WoundPrototype>("SystemicBleedingWound")));
        });
    }

    [Test]
    public async Task BandageReducesBleedingAndDamageReopensWoundTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var map = await Pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
            var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();
            var part = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
            var wounds = entityManager.System<WoundSystem>();
            var bleeding = entityManager.System<WoundBleedingSystem>();
            var wound = wounds.CreateOrMergeWound(part, "SlashWound", 30)!.Value;

            Assert.That(bleeding.ReduceBleeding(wound, 10));
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(wound).BleedingSeverity,
                Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(wounds.GetWounds((part, entityManager.GetComponent<WoundableComponent>(part))).Count(), Is.EqualTo(1));

            Assert.That(bleeding.ReduceBleeding(wound, 20));
            Assert.That(bleeding.GetPartRate(part), Is.Zero);
            Assert.That(entityManager.HasComponent<WoundBleedingComponent>(wound), Is.False);
            Assert.That(wounds.CloseWound(wound));
            Assert.That(wounds.CreateOrMergeWound(part, "SlashWound", 5), Is.EqualTo(wound));
            Assert.That(entityManager.GetComponent<WoundComponent>(wound).State, Is.EqualTo(WoundState.Open));
            Assert.That(entityManager.HasComponent<WoundBleedingComponent>(wound), Is.True);
            // WOLFGATE: Onyx's literal 5 is stale against its own code. WoundSystem.SetWoundState -> and
            // WoundSystem.cs (both byte-identical to Onyx) re-add WoundBleedingComponent with
            // `BleedingSeverity = wound.Comp.Severity` the moment CloseWound re-syncs, so a fully bandaged
            // severity-30 wound reopening with +5 lands at 35, not 5. Same stale-literal class as the
            // fracture-grade thresholds in WoundFractureTest. Contract kept: reopening restores bleeding.
            Assert.That(entityManager.GetComponent<WoundBleedingComponent>(wound).BleedingSeverity,
                Is.EqualTo(FixedPoint2.New(35)));
            Assert.That(bleeding.GetPartRate(part), Is.GreaterThan(0f));
        });
    }

    // WOLFGATE: Onyx's TourniquetStopsOnlySelectedPartTest is not ported — the `Tourniquet` prototype and
    // TourniquetSystem are phase 4 (PLAN §6.1, WP11).

    // WOLFGATE: Onyx's TraumaticAmputationCreatesSevereStumpBleedingTest is not ported - it asserts
    // WoundableComponent.Severable, which only AmputationSystem ever sets (ONYX AmputationSystem.cs:46,86).
    // D26 defers amputation to phase 3, so nothing in phase 1 can make a part severable. Port it with
    // AmputationSystem in WP11.

    [Test]
    public async Task AutomaticClottingDeadlineTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();
        EntityUid body = default;
        EntityUid light = default;
        EntityUid heavy = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled, true);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity, 0.05f);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMinSeconds, 0f);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds, 10f);

                body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
                var parts = entityManager.System<SharedBodySystem>().GetBodyChildren(body).ToList();
                light = parts.Single(part => part.Component.PartType == BodyPartType.Torso).Id; // WOLFGATE: D9
                heavy = parts.Single(part => part.Component.PartType == BodyPartType.Head).Id;
                var wounds = entityManager.System<WoundSystem>();
                // WOLFGATE: Onyx's severities (1 and 3) are below SlashWound's own `minimumSeverity: 9`
                // bleeding threshold in its pinned wounds.yml, so neither wound ever bleeds and the test
                // measures nothing. 10 and 30 keep the same shape (a short deadline and a long one) above it.
                wounds.CreateOrMergeWound(light, new ProtoId<WoundPrototype>("SlashWound"), 10);
                wounds.CreateOrMergeWound(heavy, new ProtoId<WoundPrototype>("SlashWound"), 30);
            });

            await RunSeconds(0.8f);
            await server.WaitAssertion(() =>
            {
                var bleeding = entityManager.System<WoundBleedingSystem>();
                Assert.That(bleeding.GetPartRate(light), Is.Zero);
                Assert.That(bleeding.GetPartRate(heavy), Is.GreaterThan(0f));
            });

            await server.WaitAssertion(() =>
            {
                var wfBody = entityManager.System<WolfmedBodySystem>(); // WOLFGATE
                Assert.That(wfBody.TryDetachPart(heavy));
            });
            await RunSeconds(1.2f);
            await server.WaitAssertion(() =>
            {
                var graph = entityManager.System<SharedBodySystem>();
                var bleeding = entityManager.System<WoundBleedingSystem>();
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);
                Assert.That(graph.AttachPart(light, "head", heavy)); // WOLFGATE
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);

                var wound = entityManager.System<WoundSystem>()
                    .GetWounds((heavy, entityManager.GetComponent<WoundableComponent>(heavy))).Single();
                Assert.That(bleeding.GetPartRate(heavy), Is.Zero);

                Assert.That(entityManager.System<WoundSystem>().ChangeSeverity(wound.Owner, 1));
                Assert.That(bleeding.GetPartRate(heavy), Is.GreaterThan(0f));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled,
                    CCVars.WoundsBleedingAutoStopEnabled.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopSecondsPerSeverity,
                    CCVars.WoundsBleedingAutoStopSecondsPerSeverity.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMinSeconds,
                    CCVars.WoundsBleedingAutoStopMinSeconds.DefaultValue);
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopMaxSeconds,
                    CCVars.WoundsBleedingAutoStopMaxSeconds.DefaultValue);
            });
        }
    }

    [Test]
    public async Task AutomaticClottingDisabledTest()
    {
        var server = Pair.Server;
        await server.WaitIdleAsync();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var map = await Pair.CreateTestMap();
        EntityUid part = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled, false);
                var body = entityManager.SpawnEntity("WoundBleedingBody", map.GridCoords);
                part = entityManager.System<SharedBodySystem>().GetBodyChildren(body).First().Id;
                // WOLFGATE: as in AutomaticClottingDeadlineTest, Onyx's severity 1 is below SlashWound's
                // `minimumSeverity: 9`, so the wound never bleeds and "still bleeding after a second" is vacuous.
                entityManager.System<WoundSystem>()
                    .CreateOrMergeWound(part, new ProtoId<WoundPrototype>("SlashWound"), 10);
            });
            await RunSeconds(1f);
            await server.WaitAssertion(() =>
                Assert.That(entityManager.System<WoundBleedingSystem>().GetPartRate(part), Is.GreaterThan(0f)));
        }
        finally
        {
            await server.WaitPost(() => configuration.SetCVar(CCVars.WoundsBleedingAutoStopEnabled,
                CCVars.WoundsBleedingAutoStopEnabled.DefaultValue));
        }
    }

    private static DamageSpecifier Spec(string type, int amount)
    {
        return new DamageSpecifier
        {
            DamageDict = { [new ProtoId<DamageTypePrototype>(type)] = FixedPoint2.New(amount) },
        };
    }
}
