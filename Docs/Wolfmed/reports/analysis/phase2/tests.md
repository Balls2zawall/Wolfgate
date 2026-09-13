# Wolfmed Phase 2 — test plan

**Scope:** PLAN.md §6.2 (T-AP, T-PASSIVE) + DECISIONS.md P2-2 (port `EffectsRefreshOnTreatmentHealingAndDetachTest`,
write T-AP/T-PASSIVE, add a fracture-alert and pain-alert assertion) + the task's explicit asks (HighPainThreshold
trait assertion). Read-only analysis; no code written. All claims below are cited to `file:line` in the worktree
(`WG` = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`) or the Onyx sparse
checkout (`ONYX` = `C:/tmp/onyx`, pin `2f5bab9946539cbe083010c9ae6fbc59b47ae377`).

---

## 0. Headline findings (read this first)

1. **Two of the six line items DECISIONS.md/task asks for cannot be written as literally specified, for reasons
   independent of test-writing skill — they are production gaps, not missing test code:**
   - **No pain or shock `AlertPrototype` exists anywhere in the Onyx pin.** `PainSystem.cs` never calls
     `AlertsSystem` (grep confirms zero `_alerts`/`ShowAlert` references in
     `WG/Content.Shared/_Onyx/Wounds/PainSystem.cs`); pain shock is expressed as a stun + jitter, not an alert.
     DECISIONS.md's own phrasing — "a pain-alert/shock assertion **once pain-hud defines the alert**" — anticipates
     this; the condition is not met at this pin. §4.5 below gives the interim assertion to write instead.
   - **`PainShockTargetComponent` is not on any shipped mob prototype.** It is required for pain shock to fire at
     all (`WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:143-155` only iterates
     `EntityQueryEnumerator<PainComponent, MobStateComponent, PainShockTargetComponent>`), nothing `EnsureComp`s it,
     and `grep -rn "PainShockTarget" Resources` returns nothing in `WG`. It is added only inside WP9's own bespoke
     `[TestPrototypes]` body (`WoundDamageFoundationTest.cs:66`). **On real mobs today, pain shock is exactly as
     dead as pain itself was before WP9's fix — nobody has fixed this yet.** This is a genuine phase-1/WP7 gap,
     not a phase-2 test-writing problem; §4.5 flags it as a blocker for WP10's production scope, and the interim
     test uses its own fixture the same way WP9 did.
2. **T-AP has a real, already-built mechanism to test.** WP8 (already landed, uncommitted) threads
   `armorPenetration` through `DamageableSystem.TryChangeDamage(..., armorPenetration:, targetPart:)` end to end
   for wound hosts (`WolfmedPartArmorSystem.cs`, `WG/Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs`).
   T-AP is listed as untested in both `WP8-report.md` §5 item 2 and `WP9-report.md` §6 item 5 — it is real test
   debt, not speculative. §4.1 gives exact numbers.
3. **T-PASSIVE's literal PLAN wording is nearly tautological against the shipped YAML** — `BaseMobSpeciesOrganic`'s
   `PassiveDamage.damage` is already `{}` (empty) (`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:266-270`),
   and `TryChangeDamage` returns immediately for an empty `DamageSpecifier`
   (`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:213-216`) — before the code even checks
   `WoundHostComponent`. A test that only re-confirms "empty stays empty" would not catch a regression where
   someone restores a real heal value to that YAML block without also code-gating `PassiveDamageSystem`. §4.2
   designs a two-part test: one against the **real production prototype** (locks the current deviation in place)
   and one against a **bespoke mechanism fixture** with non-empty `PassiveDamage` on a wound host (proves the
   routing mechanism itself would double-heal if the YAML guard were ever removed, which is the actual regression
   D29's rationale warns about).
4. **The fracture-alert test has almost nothing to build — the alert prototype and its texture already shipped in
   phase 1**, ahead of the system that will use them. `OrganicFractureProfile` already declares
   `alert: BrokenBones, alertMinimumGrade: Simple, alertHiddenTreatments: [Mended]`
   (`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:39-48`), and the `BrokenBones` `AlertPrototype` plus its
   `fracture.rsi` texture are already in the tree (`WG/Resources/Prototypes/_Onyx/Alerts/alerts.yml:14-21`,
   `WG/Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/`). Wolfgate's `AlertsSystem.IsShowingAlert` has the
   exact Onyx-shaped signature already (`WG/Content.Shared/Alert/AlertsSystem.cs:39`). The only missing piece is
   `FractureAlertSystem` itself (WP10 scope, not phase 1). §4.4 gives the test.
5. **`FractureEffectSystem`'s hand-symmetry lookup cannot compile unchanged** — Onyx's
   `_hands.IsHolding((body, hands), item, out handId)` expects an `out string? handId` overload and
   `_hands.GetActiveHand((body, hands))` expects a `string?` return; Wolfgate's equivalent signatures return
   `Hand?`, not `string?` (`WG/Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:177,438`). This is the
   "hands block rewritten" PLAN.md §4's WP10 paragraph already flags — it is a production-code adaptation, but it
   changes what the fracture-effects test can assert without modification. See §4.3.
6. **Every dependency `FractureEffectSystem`/`FractureAlertSystem` need beyond the hands block already exists with
   matching signatures** from phase 1: `BodyPartFunctionalitySystem.{GetState,Refresh,RefreshPart}`
   (`WG/Content.Shared/_Onyx/Wounds/BodyPartFunctionalitySystem.cs:18,56,65`),
   `WoundStatusEffectSystem.{HandlePartInserted,HandlePartRemoved}` (`WoundStatusEffectSystem.cs:109,119`),
   `AlertsSystem.{ShowAlert,ClearAlert}` (base class, `AlertsSystem.cs:81,153`), and the
   `OrganGotInsertedEvent`/`OrganGotRemovedEvent` compat shim, already raised on the correct target
   (`WG/Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs:36,60`, both `[ByRefEvent]` directed on the
   **part**, `Target` = body — matches Onyx's `ref` handler signature exactly). No new subscription pair this port
   wants to register is already taken (§2).

---

## 1. What already exists to build on (read before writing anything)

### 1.1 Fixture pattern — `WolfmedDamageBridgeTest.cs`

`WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` is the template for every new `_WF`
test in this plan:
- `[TestPrototypes]` is a single `const string` YAML blob with a `body` prototype (Shitmed shape: `root`, `slots`,
  each slot naming a real Wolfgate part entity like `TorsoHuman`/`LeftArmHuman`) plus one or more `entity`
  prototypes with `parent: InventoryBase`, `- type: Body prototype: <graph>`, `- type: Damageable
  damageContainer: Biological`, `- type: MobState`, `- type: MobThresholds`, `- type: Targeting`, and
  `- type: WoundHost` (or not, for a control).
- **`[TestPrototypes]` ids are global to the whole test-assembly pool** — a new file must not reuse an id another
  fixture already declared (`WoundFoundationBody*`, `WoundBleedingBody*`, `WoundHealingBody*`, `WoundScarBody*`,
  `WoundFractureBody*`, `WolfmedBridgeBody*` are all taken). Every new prototype id below is chosen to avoid this.
- Standard per-test shape:
  ```csharp
  var server = Pair.Server;
  await server.WaitIdleAsync();
  var entities = server.ResolveDependency<IEntityManager>();
  var map = await Pair.CreateTestMap();
  await server.WaitAssertion(() => { /* spawn, act, Assert.That / Assert.Multiple, all synchronous */ });
  ```
- Damage is driven directly through `entities.System<DamageableSystem>().TryChangeDamage(body, spec,
  targetPart: TargetBodyPart.X, armorPenetration: Y)` — **not** through a weapon or the `damage` console command.
  WP8 round 1 tried to add a 5-argument `HurtCommand` for exactly this and it was **reverted** on verification
  (`WP8-report.md` §F2, patch kept at `C:/tmp/wolfmed-plan/wp/WP8-hurtcommand-deferred.patch`, unauthorised
  upstream edit). Use `WoundDamageRoutingSystem.TryApplyPartDamage(body, part, spec, ignoreResistances:)` when you
  want to bypass armour/routing/AP entirely (it always carries `armorPenetration: 0` —
  `WoundDamageRoutingSystem.cs:66-67`, confirmed by WP8 — so it is unsuitable for T-AP itself, but is exactly
  right for T-PASSIVE's initial "give the part some damage" step).
- `SupportedTypeCount(prototypes, "Biological")` (bottom of `WolfmedDamageBridgeTest.cs`) is the existing helper
  for the D30 seeding-invariant check; reuse it rather than re-deriving the count.

### 1.2 CCVar pinning pattern — `WoundScarTest.cs`

`WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundScarTest.cs:56-93`:
```csharp
await server.WaitAssertion(() =>
{
    configuration.SetCVar(CCVars.SurgeryScarChance, 1f);   // pin inside the assertion, before acting
    // ... spawn, act, assert ...
});
await server.WaitPost(() =>                                 // restore OUTSIDE WaitAssertion, after it
    configuration.SetCVar(CCVars.SurgeryScarChance, CCVars.SurgeryScarChance.DefaultValue));
```
No phase-2 test in this plan needs a CCVar pin (`wounds.body_part_functionality_enabled` stays `false` per P2-3,
and none of the systems below read a probabilistic CCVar), but the pattern is documented here because
`FractureAlertSystem`/`FractureEffectSystem` share a file with `WoundScarSystem` conceptually (both are
grade/state-driven wound behaviours) and a later balance pass may add one.

### 1.3 Fracture fixture — `WoundFractureTest.cs` (phase 1, to extend)

`WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs:20-58` already declares everything the
ported `EffectsRefreshOnTreatmentHealingAndDetachTest` and the fracture-alert test need:
```yaml
- type: body
  id: WoundFractureBodyGraph
  root: torso
  slots:
    torso: {part: TorsoHuman, connections: [left arm, left leg]}
    left arm: {part: LeftArmHuman}
    left leg: {part: LeftLegHuman}
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
      coefficients: {Blunt: 0.5}
```
`GradeBoundariesAreDeterministicTest` and `PostArmorHitAndTreatmentPreconditionsTest` already pass (30/30 in
`WP9-tests.log`). The commented-out placeholder at the bottom of the file
(`WoundFractureTest.cs:116-117`, `// WOLFGATE: Onyx's EffectsRefreshOnTreatmentHealingAndDetachTest is not
ported...`) is exactly where the ported test goes.

### 1.4 Onyx's `EffectsRefreshOnTreatmentHealingAndDetachTest` (the one to port)

`ONYX Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` (full text pulled via `git show`, not
present as a file in the sparse checkout's working tree):
```csharp
[Test]
public async Task EffectsRefreshOnTreatmentHealingAndDetachTest()
{
    ... 
    var routing = entityManager.System<WoundDamageRoutingSystem>();
    var fractures = entityManager.System<WoundFractureSystem>();
    var manipulation = entityManager.System<FractureEffectSystem>();
    var leg = parts.Single(part => part.Component.PartType == BodyPartType.Leg).Id;
    var arm = parts.Single(part => part.Component.PartType == BodyPartType.Arm).Id;

    Assert.That(routing.TryApplyPartDamage(body, leg, Spec(75)));
    Assert.That(entityManager.GetComponent<MovementSpeedModifierComponent>(body).WalkSpeedModifier,
        Is.EqualTo(0.4f).Within(0.001f));

    Assert.That(routing.TryApplyPartDamage(body, arm, Spec(75)));
    Assert.That(manipulation.GetDurationMultiplier(body), Is.EqualTo(2f).Within(0.001f));
    Assert.That(fractures.TryMend(fractures.GetFracture(arm).Value.Owner));
    Assert.That(fractures.GetFracture(arm), Is.Null);
    Assert.That(manipulation.GetDurationMultiplier(body), Is.EqualTo(1f).Within(0.001f));

    Assert.That(graph.TryDetachPart(leg));
    Assert.That(entityManager.GetComponent<MovementSpeedModifierComponent>(body).WalkSpeedModifier,
        Is.EqualTo(1f).Within(0.001f));
}
```
Note: it calls `manipulation.GetDurationMultiplier(body)` **with no `used` argument** — i.e. `used: null`. Reading
Onyx's `FractureEffectSystem.TryGetUsedHandSymmetry` (`ONYX FractureEffectsSystem.cs`, quoted in §4.3 below):
when `used` is `null` it falls to `_hands.GetActiveHand((body, hands))` — the mob's currently active hand — so the
test's `arm` (`LeftArmHuman`, the only arm in this 3-part graph) must be reachable through whichever hand
`GetActiveHand` returns on a freshly spawned `WoundFractureBody`. On Wolfgate's `HandsComponent`, a freshly
initialised entity's active hand is deterministic (first hand added, typically the left) but **this must be
verified against a real spawn**, not assumed — see the adaptation note in §4.3.

---

## 2. Subscription-pair audit (every new pair this plan's tests exercise)

Checked with `grep -rn "SubscribeLocalEvent<" WG/Content.Shared WG/Content.Server WG/Content.Client` (excluding
`obj`/`bin`) against every pair `FractureEffectSystem`/`FractureAlertSystem`/`HighPainThresholdSystem` would
register. **All are free — confirmed by direct grep, not inferred from PLAN.md's §5.2 table alone** (which
already listed most of these prospectively; this re-verifies against the current tree state after WP1–WP9):

| Pair | Current owner | Free? |
|---|---|---|
| `WoundFractureComponent`, `FractureGradeChangedEvent` | none — event is raised (`WoundFractureSystem.cs:167-169`) but nothing subscribes it yet | **yes** |
| `WoundFractureComponent`, `FractureTreatmentChangedEvent` | none — raised at `WoundFractureSystem.cs:122-124,181-183`, unsubscribed | **yes** |
| `WoundFractureComponent`, `WoundRemovedEvent` | none | **yes** |
| `WoundableComponent`, `OrganGotInsertedEvent` / `OrganGotRemovedEvent` | none (WP9-verify.md §4 confirms `WolfmedBodyPartLifecycleSystem` raises them directly rather than subscribing `OrganComponent`, so nothing has claimed the receive side) | **yes** |
| `WoundableComponent`, `BodyPartFunctionalityChangedEvent` | none | **yes** |
| `WoundHostComponent`, `RefreshMovementSpeedModifiersEvent` | none | **yes** |
| `WoundHostComponent`, `GetManipulationDurationMultiplierEvent` | none (type does not exist in `WG` yet — new to this port) | **yes**, but see §4.3 — may not be the event WP10 ends up using |
| `HighPainThresholdComponent`, `ModifyPainGainEvent` | none — `ModifyPainGainEvent` is raised twice in `PainSystem.cs` (`:114`, `:239`) but has zero subscribers anywhere in `WG` today | **yes** |

No test in this plan adds a `SubscribeLocalEvent` itself (tests only call systems / raise events directly), but
every pair above is what the **production code these tests exercise** will need to register, and a duplicate
registration is a server-start crash for every test in the suite, not just the new ones — hence verifying it here.

---

## 3. Traps (apply to every test below)

1. **Pooled test pair / `TerminatingOrDeleted`.** `RecursiveDeleteEntity` detaches every part while a mob
   terminates. `WoundDamageProjectionSystem.RefreshDetachedDamage`'s `EnsureComp<PartDamageVisualsComponent>`
   throws `DebugAssertException` on a terminating entity unless the caller guards with `TerminatingOrDeleted`.
   `WolfmedBodyPartLifecycleSystem.OnPartAdded`/`OnPartRemoved` already carry this guard
   (`WolfmedBodyPartLifecycleSystem.cs:29,50`) — but **any new production code added for WP10 that reacts to
   `BodyPartAddedEvent`/`BodyPartRemovedEvent`/`OrganGotInsertedEvent`/`OrganGotRemovedEvent` must repeat it**, or
   a single test spawning and later disposing a `MobHuman`/`WoundFractureBody` poisons the shared pooled pair for
   every other test in the run (`WP9-report.md` §5 failure #1: 13 unrelated tests failed from this one bug before
   it was found). If you see `Skipped — Test was dirty-disposed.` with no assertion text on a test that never
   touched fractures/pain, suspect this class of bug in whatever new code WP10 lands, and re-run the suspect test
   alone under a narrow `--filter` to get the real exception (WP9-report.md §5, "A note on reading `dotnet test`
   output for this suite").
2. **`db.ef` / `DockTest` environmental failure mode.** Project memory: sqlite migration warnings
   (`admin_notes`, `ConnectionLogServer`, etc.) can fail every pair test in this repo. Before attributing any
   phase-2 test failure to Wolfmed code, run
   `dotnet test Content.IntegrationTests.csproj -c DebugOpt --no-build --filter "FullyQualifiedName~DockTest"`.
   If `TestDockingConfig`/`TestPlanetDock` fail too, the environment is the cause, not the port (WP9-report.md
   §4.5 and WP9-verify.md §8 both used this exact tell).
3. **`Assert.Multiple` must stay synchronous.** Every existing use in this suite
   (`WolfmedDamageBridgeTest.cs:182-186,265-272,297-301`, `WoundFractureTest.cs` via inherited pattern) passes a
   plain `Action` — no `async`/`await` inside the lambda. `Assert.Multiple` has no `Func<Task>`/async-aware
   overload here; wrapping `await`-containing code in it either fails to compile against the `TestDelegate`
   overload or, if forced through an `async void`-shaped lambda, returns before the awaited assertions run and
   silently drops any exception they would have thrown — the test reports green with assertions that never
   executed. Keep every `Assert.Multiple` body fully synchronous, nested inside the outer `await
   server.WaitAssertion(() => { ... })`, exactly as every current test in this tree does.
4. **CCVar pinning** — see §1.2. Restore the default **after** `WaitAssertion`, never inside the same block you
   changed it in without restoring, or a later test in the same fixture run (NUnit parallelises fixtures, not
   tests within one `[TestFixture]` by default, but cvars are process-global) inherits the pinned value.
5. **`[TestPrototypes]` id collisions are silent until server start** — RT does not warn you locally; it throws
   at pool creation. Grep every existing `_Onyx`/`_WF` test file's `[TestPrototypes]` block for an id before
   introducing a new one. This plan's new ids (`WolfmedBridgeArmor`, `WolfmedPassiveWoundHost`,
   `WolfmedPassiveControl`, `WolfmedPainShockBody`, `WolfmedHighPainThresholdBody`) were checked against every
   file in `Content.IntegrationTests/Tests/_Onyx/Wounds/`, `Tests/_Onyx/Body/`, and `Tests/_WF/Wolfmed/` — none
   collide today, but re-check if another phase-2 work package lands prototypes first.
6. **YAML lints in Release only.** `ErrorNode` crashes the linter on other configurations (project memory). Any
   new `AlertPrototype`/trait/component YAML this plan's production code needs (none for the tests themselves,
   since `BrokenBones` already exists) must be checked with
   `dotnet run --project Content.YAMLLinter -c Release` before the test that depends on it is trusted.
7. **Build order for `Content.IntegrationTests`.** PLAN.md ground rule 5: from WP9 onward, `dotnet build
   Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt` is a required green checkpoint alongside
   Server/Client — it was not required before WP9 and is easy to forget.

---

## 4. Test-by-test plan

### 4.1 T-AP — `ArmorPenetrationReachesWoundHostsTest`

**File:** `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` (extend the existing
`[TestPrototypes]` block and add the test method alongside the other bridge tests).

**Why this file:** T-AP is PLAN §6.2's own bridge-contract test (same fixture family as T-RESULT/T-CAUSTIC/T12),
and it is literally the acceptance gate WP8's own report names as unmet (`WP8-report.md` §5 item 2: "T-AP now has
something to measure... write it against a limb").

**New prototype needed** (append to `WolfmedDamageBridgeTest.cs`'s `Prototypes` const):
```yaml
- type: entity
  id: WolfmedBridgeArmor
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    modifiers:
      coefficients:
        Blunt: 0.5
```
(mirrors `WoundFractureArmor` exactly — Wolfgate's `ArmorComponent` has no `coverage`, so it protects every part
regardless of `PartType`, confirmed by WP9's own comment on the fracture-armour fixture.)

**Setup:**
```csharp
var body = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
var armor = entities.SpawnEntity("WolfmedBridgeArmor", map.GridCoords);
var inventory = entities.System<InventorySystem>();
var graph = entities.System<SharedBodySystem>();
var damage = entities.System<DamageableSystem>();
Assert.That(inventory.TryEquip(body, armor, "outerClothing"));
var leftArm = graph.GetBodyChildren(body)
    .Single(part => part.Component.PartType == BodyPartType.Arm &&
                    part.Component.Symmetry == BodyPartSymmetry.Left).Id;
```

**Action + assertions** (two separate bodies, one per AP value, so armour state never needs resetting between
hits — matches how `NonWoundHostUnchangedTest` et al. avoid cross-hit interference):
```csharp
// Body A: armorPenetration 0 — full coefficient applies.
var lowApBody = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
var lowApArmor = entities.SpawnEntity("WolfmedBridgeArmor", map.GridCoords);
Assert.That(inventory.TryEquip(lowApBody, lowApArmor, "outerClothing"));
var lowApArm = /* resolve left arm as above */;
Assert.That(damage.TryChangeDamage(lowApBody, Spec("Blunt", 10),
    targetPart: TargetBodyPart.LeftArm, armorPenetration: 0f), Is.Not.Null);

// Body B: armorPenetration 1.0 — PenetrateArmor returns an empty modifier set (DamageSpecifier.cs:314-315),
// so the coefficient is not applied at all.
var highApBody = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);
var highApArmor = entities.SpawnEntity("WolfmedBridgeArmor", map.GridCoords);
Assert.That(inventory.TryEquip(highApBody, highApArmor, "outerClothing"));
var highApArm = /* resolve left arm */;
Assert.That(damage.TryChangeDamage(highApBody, Spec("Blunt", 10),
    targetPart: TargetBodyPart.LeftArm, armorPenetration: 1f), Is.Not.Null);

Assert.Multiple(() =>
{
    Assert.That(entities.GetComponent<DamageableComponent>(lowApArm).TotalDamage,
        Is.EqualTo(FixedPoint2.New(5)), "0.5 coefficient should have halved the hit");
    Assert.That(entities.GetComponent<DamageableComponent>(highApArm).TotalDamage,
        Is.EqualTo(FixedPoint2.New(10)), "armorPenetration: 1.0 should have bypassed the coefficient entirely");
    Assert.That(entities.GetComponent<DamageableComponent>(highApArm).TotalDamage,
        Is.GreaterThan(entities.GetComponent<DamageableComponent>(lowApArm).TotalDamage));
});
```

**Exact numbers, verified from source, not assumed:**
- `DamageSpecifier.PenetrateArmor(modifierSet, penetration)` (`WG/Content.Shared/Damage/DamageSpecifier.cs:306-330`):
  `penetration == 0` returns the modifier set unchanged; `penetration >= 1f` returns a **new, empty**
  `DamageModifierSet` (`:314-315`).
  `DamageSpecifier.ApplyModifierSet` (`:133-163`) leaves a value unmodified when the modifier set has no entry for
  that type (`:157`, `TryGetValue` false → no multiply) — so an empty set is a true no-op, not a zero-out.
  Therefore AP=1.0 leaves the coefficient un-applied and 10 Blunt lands as 10; AP=0.0 leaves it applied, 10 → 5.
- The armour path this actually exercises is `WolfmedPartArmorSystem.OnPartDamageModify`
  (`WG/Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:28-32`), the **sole subscriber** of
  `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` — confirmed exclusive by WP8 round 1's grep
  (`WP8-report.md` §F3). This is the code path WP8's report explicitly says was added in a second, undocumented
  half and has no test yet.

**Also add the WP8-report-recommended regression companion** (§5 item 2 of `WP8-report.md`): a non-AP hit on an
armoured wound host must land **less** on the limb than the same hit on an unarmoured one. This is subsumed by
the two-body comparison above if you add a third, unarmoured body at AP=0 and assert its arm damage (`10`) is
greater than the armoured AP=0 body's arm damage (`5`) — cheap to add in the same test, folds into the existing
`Assert.Multiple`.

**Dependencies on phase-2 WPs:** none — every symbol here (`WolfmedPartArmorSystem`, `DamageableSystem`
`armorPenetration`/`targetPart` params, `WolfmedBridgeBody`) is phase-1 (WP5/WP8) production code already in the
tree. This test can be written and should pass **today**, before any other phase-2 work lands.

---

### 4.2 T-PASSIVE — two tests: `RealWoundHostPassiveDamageIsNeutralisedTest` + `PassiveDamageMechanismStillRoutesIfReenabledTest`

**File:** `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs`.

**Test A — locks the shipped D29 deviation against regression, on the real production entity:**
```csharp
[Test]
public async Task RealWoundHostPassiveDamageIsNeutralisedTest()
{
    var server = Pair.Server;
    await server.WaitIdleAsync();
    var entities = server.ResolveDependency<IEntityManager>();
    var map = await Pair.CreateTestMap();
    EntityUid body = default, leftArm = default;

    await server.WaitAssertion(() =>
    {
        body = entities.SpawnEntity("MobHuman", map.GridCoords);
        var routing = entities.System<WoundDamageRoutingSystem>();
        var graph = entities.System<SharedBodySystem>();
        leftArm = graph.GetBodyChildren(body)
            .Single(part => part.Component.PartType == BodyPartType.Arm &&
                            part.Component.Symmetry == BodyPartSymmetry.Left).Id;

        // D29's own mechanism: PassiveDamage.damage is {} on BaseMobSpeciesOrganic. Assert this directly so a
        // future re-add of a real heal value to that YAML block fails here immediately, not three systems away.
        Assert.That(entities.GetComponent<PassiveDamageComponent>(body).Damage.Empty, Is.True,
            "BaseMobSpeciesOrganic's PassiveDamage must stay neutralised (D29) until Onyx per-part recovery lands");

        Assert.That(routing.TryApplyPartDamage(body, leftArm, Spec("Blunt", 10)));
    });

    await RunSeconds(60f);

    await server.WaitAssertion(() =>
    {
        Assert.That(entities.GetComponent<DamageableComponent>(leftArm).TotalDamage,
            Is.EqualTo(FixedPoint2.New(10)), "no passive heal should reach the part over 60s (D29)");
    });
}
```
Uses `MobHuman` (the real, shipped species prototype), not a bespoke fixture — this is the strongest form of the
literal PLAN wording ("a wound host with 10 Blunt on one arm and no treatment still has 10 Blunt on that arm
after 60s"), and it also re-verifies WP7's D21 wiring (`MobHuman` is in fact a wound host) as a side effect.

**Test B — proves the routing mechanism itself, independent of the current YAML value (the actual regression
D29's rationale describes):**
```yaml
- type: entity
  id: WolfmedPassiveWoundHost
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: WoundHost
  - type: PassiveDamage
    allowedStates: [Alive]
    damageCap: 0
    damage:
      types:
        Blunt: -5   # heals 5/s if it ever reaches the body's own DamageableComponent unrouted

- type: entity
  id: WolfmedPassiveControl
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: PassiveDamage
    allowedStates: [Alive]
    damageCap: 0
    damage:
      types:
        Blunt: -5
```
```csharp
[Test]
public async Task PassiveDamageMechanismStillRoutesIfReenabledTest()
{
    var server = Pair.Server;
    await server.WaitIdleAsync();
    var entities = server.ResolveDependency<IEntityManager>();
    var map = await Pair.CreateTestMap();
    var routing = entities.System<WoundDamageRoutingSystem>();
    var graph = entities.System<SharedBodySystem>();

    EntityUid woundHost = default, control = default, whArm = default, controlArm = default;
    await server.WaitAssertion(() =>
    {
        woundHost = entities.SpawnEntity("WolfmedPassiveWoundHost", map.GridCoords);
        control = entities.SpawnEntity("WolfmedPassiveControl", map.GridCoords);
        whArm = graph.GetBodyChildren(woundHost).Single(p => p.Component.PartType == BodyPartType.Arm).Id;
        controlArm = graph.GetBodyChildren(control).Single(p => p.Component.PartType == BodyPartType.Arm).Id;

        Assert.That(routing.TryApplyPartDamage(woundHost, whArm, Spec("Blunt", 10)));
        Assert.That(entities.System<DamageableSystem>().TryChangeDamage(control, Spec("Blunt", 10)), Is.Not.Null);
    });

    await RunSeconds(2f); // damageCap: 0 disables the cap check; 2 ticks of PassiveDamageSystem.Update at -5/s

    await server.WaitAssertion(() =>
    {
        // The control heals normally through vanilla PassiveDamageSystem -> DamageableSystem (no routing).
        Assert.That(entities.GetComponent<DamageableComponent>(control).TotalDamage,
            Is.LessThan(FixedPoint2.New(10)), "the control's own PassiveDamage should have healed it");

        // If PassiveDamage were ever re-enabled on a wound host, TryChangeDamage's WoundHostComponent check
        // (DamageableSystem.cs's GUARD D seam) routes it through WoundDamageRoutingSystem exactly like any other
        // damage call, including a negative (healing) DamageSpecifier -- it does NOT silently no-op. This test
        // exists to document/detect that: if you see this fail because whArm now shows LESS than 10, someone
        // restored a real PassiveDamage value to a wound host's prototype and it is being routed as a heal.
        Assert.That(entities.GetComponent<DamageableComponent>(whArm).TotalDamage,
            Is.EqualTo(FixedPoint2.New(10)),
            "this wound-host fixture carries the same PassiveDamage the control does; if this ever heals it " +
            "means D29's YAML guard was removed without a code-level replacement -- see D29's rationale");
    });
}
```
This second test is **not** asking "does D29 currently work" (Test A already answers that) — it is a canary that
fails loudly and specifically (not just "damage changed") if a future WP re-adds real `PassiveDamage` values to
`BaseMobSpeciesOrganic` without adding the code-level guard D29's own rationale says is necessary. Frame it in the
report/manifest as **intentionally documenting a known non-guarded mechanism**, not as "passing today proves
safety forever."

**Dependencies on phase-2 WPs:** none. Both tests exercise phase-1 production code
(`PassiveDamageSystem`, `WoundDamageRoutingSystem`, the D29 YAML). Write and land these now.

---

### 4.3 Fracture-effects port — `EffectsRefreshOnTreatmentHealingAndDetachTest`

**File:** `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` (replace the `// WOLFGATE` comment
at `:116-117` with the real test).

**Blocking production gap to resolve first (not a test-writing problem):** Onyx's hand-symmetry lookup
(`ONYX Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs`, `TryGetUsedHandSymmetry`) is:
```csharp
private bool TryGetUsedHandSymmetry(EntityUid body, EntityUid? used, out BodyPartSymmetry symmetry)
{
    ...
    if (used is { } item)
    {
        if (!_hands.IsHolding((body, hands), item, out handId))   // expects out string? handId
            return false;
    }
    else
        handId = _hands.GetActiveHand((body, hands));              // expects string? return

    if (!_hands.TryGetHand((body, hands), handId, out var hand))
        return false;
    symmetry = hand.Value.Location switch { ... };
}
```
Wolfgate's `SharedHandsSystem`:
- `GetActiveHand(Entity<HandsComponent?> entity)` returns **`Hand?`**, not `string?`
  (`WG/Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:177`).
- `IsHolding(Entity<HandsComponent?> ent, EntityUid? entity, out string? inHand)` **does** exist with a
  string-out overload (`SharedHandsSystem.cs:438`) — this half needs no change.
- `TryGetHand(EntityUid handsUid, string handId, out Hand? hand, ...)` (`SharedHandsSystem.cs:306`) takes a bare
  `EntityUid`, not `Entity<HandsComponent?>`, and Onyx's 3-arg tuple-target call form does not match it directly.

So the `used == null` branch (`GetActiveHand`) needs one line changed from `handId = _hands.GetActiveHand(...)`
to `handId = _hands.GetActiveHand((body, hands))?.Location switch { ... }` **or**, simpler, resolve the hand id via
`Hand.Name`/whatever field a `Hand` struct exposes for its id — **read `WG/Content.Shared/Hands/Components/Hand.cs`
before implementing this**, it was not in this analysis's required-reading set and its exact field for the id
string was not confirmed here. This is a one-line `// WOLFGATE` production fix inside a vendored file (D5), not
an upstream hook — flag it to whoever implements FractureEffectSystem, do not let it block test-writing: the test
itself only calls the public `GetDurationMultiplier(EntityUid, EntityUid?)` and does not care how the hand lookup
is implemented internally, provided it resolves the correct arm's symmetry on `WoundFractureBody` (which has only
one arm, so any correct implementation gives the same answer as Onyx's regardless of this signature fix).

**Adaptations to the ported test itself** (beyond the hands-block risk above, all following the same pattern as
the two already-ported tests in this file):
- No `Chest`/`Groin` in this fixture already (phase-1 fixture is Torso-rooted) — no change needed here.
- `manipulation.GetDurationMultiplier(body)` with `used: null` relies on `GetActiveHand` picking the fixture's
  only arm's hand. **Before trusting the literal port, spawn `WoundFractureBody` in isolation and assert
  `entities.System<SharedHandsSystem>().GetActiveHand((body, Comp<HandsComponent>(body)))` is non-null and belongs
  to the left arm** — if `WoundFractureBody`'s `LeftArmHuman` part doesn't come with hand slots pre-populated (it
  should, being a real Wolfgate part entity, but this fixture has never exercised hands before), the multiplier
  test silently measures "no hand found" (multiplier stays `1f`) rather than the fracture's `2f`. Add this as a
  guard assertion immediately before the `GetDurationMultiplier` call, not as a separate test, so a failure here
  points straight at hand setup instead of masquerading as "fracture doesn't affect manipulation."
- Everything else — `routing.TryApplyPartDamage(body, leg, Spec(75))`, `MovementSpeedModifierComponent
  .WalkSpeedModifier`, `fractures.TryMend(...)`, `graph.TryDetachPart(leg)` — ports with only the namespace/API
  swaps WP9 already established for this file (`WolfmedDamageableSystem` for damage reads if any are added;
  `WoundFractureSystem`/`WoundDamageRoutingSystem` resolve identically to Onyx's).
- `graph.TryDetachPart(leg)` in Onyx's original calls `SharedBodySystem.TryDetachPart` directly; **this file's own
  two existing tests never call `TryDetachPart`**, so there is no established precedent in *this* file — but
  `WoundScarTest.cs:83` and `BodyConsequencesTest.cs` both use `entities.System<WolfmedBodySystem>().TryDetachPart`
  (the D8/§2.7 compat shim), not `SharedBodySystem.TryDetachPart`. Use `WolfmedBodySystem.TryDetachPart` here too
  for consistency — `SharedBodySystem` has no `TryDetachPart` of that shape in Wolfgate (confirmed absent; D10's
  whole point is that Onyx's targeting/body-graph helpers of this kind are re-based onto the compat shim).

**Grade-boundary numbers already established for this fixture (reuse, do not re-derive):**
`OrganicFractureProfile`: Hairline 20, Simple 35, Displaced 50, Comminuted 60
(`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:39-68`, already asserted by `GradeBoundariesAreDeterministicTest`).
75 damage through the leg (no armour in this test, unlike `PostArmorHitAndTreatmentPreconditionsTest`) exceeds
the Comminuted threshold (60) directly, matching the movement modifier Onyx's own literal `0.4f` expects
(`Comminuted: movementModifier: 0` in the profile plus Wolfgate's own base walk multiplier — **do not assume
0.4f survives verbatim**; recompute it against `MovementSpeedModifierComponent`'s Wolfgate defaults the way
WP9 recomputed the grade thresholds, since `WalkSpeedModifier` composes Wolfgate's baseline with the fracture's
effect and the profile's numbers are pinned but Wolfgate's baseline speed modifier stack may not be 1.0).

**Dependencies on phase-2 WPs:** blocks on `FractureEffectSystem`/`FractureAlertSystem` (or at minimum
`FractureEffectSystem` alone — `FractureAlertSystem` is a separate dependency of `FractureEffectSystem`'s
constructor but this specific test never reads an alert) being ported, and on the hands-block fix above.

---

### 4.4 Fracture-alert assertion — `FractureAlertTracksGradeAndTreatmentTest`

**File:** `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` (new test, same file — this is
vendored-Onyx-system behaviour, not a Wolfgate bridging concern, so it belongs alongside the other two tests
already there rather than in `_WF`).

**Nothing new to port for the prototype/texture side** — confirmed already shipped in phase 1 (§0.4 above):
`OrganicFractureProfile.alert: BrokenBones`, `alertMinimumGrade: Simple`, `alertHiddenTreatments: [Mended]`
(`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:39-48`); `BrokenBones` `AlertPrototype`
(`WG/Resources/Prototypes/_Onyx/Alerts/alerts.yml:14-21`) with its texture
(`WG/Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{brokenbones.png,meta.json}`).

```csharp
[Test]
public async Task FractureAlertTracksGradeAndTreatmentTest()
{
    var server = Pair.Server;
    await server.WaitIdleAsync();
    var entityManager = server.ResolveDependency<IEntityManager>();
    var map = await Pair.CreateTestMap();

    await server.WaitAssertion(() =>
    {
        var body = entityManager.SpawnEntity("WoundFractureBody", map.GridCoords);
        var graph = entityManager.System<SharedBodySystem>();
        var routing = entityManager.System<WoundDamageRoutingSystem>();
        var fractures = entityManager.System<WoundFractureSystem>();
        var alerts = entityManager.System<AlertsSystem>();
        var leg = graph.GetBodyChildren(body).Single(p => p.Component.PartType == BodyPartType.Leg).Id;
        ProtoId<AlertPrototype> brokenBones = "BrokenBones";

        Assert.That(alerts.IsShowingAlert(body, brokenBones), Is.False,
            "an undamaged wound host must not show the fracture alert");

        // 35 clears the Simple threshold (alertMinimumGrade), staying below Comminuted (60) so this exercises
        // the alert's *minimum grade* gate specifically, not just "any fracture at all".
        Assert.That(routing.TryApplyPartDamage(body, leg, Spec(35)));
        Assert.That(alerts.IsShowingAlert(body, brokenBones), Is.True,
            "grade >= Simple (alertMinimumGrade) must show BrokenBones");

        var fracture = fractures.GetFracture(leg)!.Value;
        Assert.That(fractures.TryMend(fracture.Owner));
        Assert.That(alerts.IsShowingAlert(body, brokenBones), Is.False,
            "Mended is in alertHiddenTreatments -- the alert must clear on treatment, not just on healing");
    });
}
```
**Below-threshold negative case** (worth a second `[Test]` or an `Assert.Multiple` branch in the same test):
damage a *different* freshly spawned body's leg to exactly 19 (`< Hairline`'s 20 per the already-established
boundary table) and assert the alert never shows — proves `alertMinimumGrade: Simple` actually gates at Simple
(35) and not merely at "any fracture exists" (Hairline, 20-34, should **not** show `BrokenBones` per
`alertMinimumGrade: Simple`).

**Dependencies on phase-2 WPs:** `FractureAlertSystem` (needs `AlertsSystem`, `SharedBodySystem`,
`WoundFractureSystem`, `IPrototypeManager` — all already available, confirmed in §0.6/§1.6 of PLAN.md's own
Dependency list, all four exist in `WG` today) and something that calls `FractureAlertSystem.Refresh(body)` on
grade/treatment change — i.e. this test is really gating **`FractureEffectSystem`'s** `OnChanged`/`OnRemoved`
handlers (§4.3), since `FractureAlertSystem.Refresh` has no other caller in the vendored set
(`ONYX FractureAlertSystem.cs` exposes only `Refresh`, called exclusively from `FractureEffectSystem`). **This
test cannot pass with `FractureAlertSystem` alone ported and `FractureEffectSystem` skipped** — note this
explicitly in whatever WP10 scoping happens, since DECISIONS.md's P2-1 list has them as separate bullet items and
someone could plausibly port one without the other.

---

### 4.5 Pain-alert/shock assertion — blocked as literally specified; interim test given

**Status: cannot write "a pain-alert assertion" today — no alert exists.** Confirmed by direct inspection
(§0.1): `grep -rn "_alerts\|ShowAlert\|ClearAlert\|Alert" WG/Content.Shared/_Onyx/Wounds/PainSystem.cs` returns
zero hits related to alerts, and no `AlertPrototype` for pain or shock exists anywhere in the Onyx pin
(`grep -rln "PainAlert\|ShockAlert" ONYX/Resources/Prototypes` returns nothing). DECISIONS.md's own wording
("once pain-hud defines the alert") anticipates exactly this — the condition is unmet. **Recommendation: do not
invent an alert prototype to satisfy this line item.** The mechanism Onyx actually ships for "the player notices
high pain" is the client-side pain damage overlay (a separate P2-1 bullet, screen-tint driven directly off
`PainComponent`/`GetPain`, not the Alerts system) plus the pain-shock stun. Test the latter; the former is a
client visual and outside this integration-test harness's reach (server-only `GameTest` pair, per `PainSystem.cs`
being `_net.IsServer`-gated almost everywhere).

**Second blocker found and required to be closed before ANY real-mob pain-shock test is meaningful:**
`PainShockTargetComponent` is not on `BaseMobSpeciesOrganic` or any other shipped species prototype (§0.1). Pain
shock is entirely inert on real mobs today. This should be raised to whoever scopes WP10's production work as a
**required addition**, in the same `// WOLFGATE` block as `WoundHost`/`Destructible`/`PassiveDamage` on
`BaseMobSpeciesOrganic` (`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:250-270`):
```yaml
  - type: PainShockTarget   # WOLFGATE (P2, gap found during test planning): without this, PainSystem's
                             # UpdatePainShock loop (EntityQueryEnumerator<PainComponent, MobStateComponent,
                             # PainShockTargetComponent>) never iterates a real mob at all.
```
Component fields: `Armed` (bool, defaults presumably `true`/`false` — check `WG/Content.Shared/_Onyx/Wounds
/WoundDamageComponents.cs:146` for the exact default before writing this line) and `AdrenalineEnds`
(`TimeSpan?`). No YAML fields are required at the default.

**Interim test — write this now, against a bespoke fixture, exactly as WP9 did for
`PainApiAndProjectionTest`'s canary, so pain-shock has *some* coverage before the YAML gap above is closed:**

```yaml
- type: entity
  id: WolfmedPainShockBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: MobState
  - type: WoundHost
  - type: PainShockTarget
```
```csharp
[Test]
public async Task PainShockStunsAtThresholdAndRearmsBelowItTest()
{
    var server = Pair.Server;
    await server.WaitIdleAsync();
    var entities = server.ResolveDependency<IEntityManager>();
    var map = await Pair.CreateTestMap();

    await server.WaitAssertion(() =>
    {
        var body = entities.SpawnEntity("WolfmedPainShockBody", map.GridCoords);
        var graph = entities.System<SharedBodySystem>();
        var routing = entities.System<WoundDamageRoutingSystem>();
        var head = graph.GetBodyChildren(body).Single(p => p.Component.PartType == BodyPartType.Head).Id;

        // PainShockThreshold is 130 (PainSystem.cs:32, private -- recompute via enough Blunt damage rather
        // than referencing the constant, since it is not part of the public API surface).
        // Blunt's DamageMultipliers entry is 0.87 (WoundDamageComponents.cs:121); enough raw damage across
        // several parts to push GetPain(body) over 130 is needed since pain is summed across the whole body,
        // not just the hit part -- do not assume one single hit's number without recomputing GetPain(body).
        Assert.That(routing.TryApplyPartDamage(body, head, Spec("Blunt", 160))); // 160*0.87 ~= 139 > 130

        Assert.That(entities.HasComponent<StunnedComponent>(body), Is.True,
            "pain shock should have paralysed the body once GetPain(body) crossed PainShockThreshold (130)");
    });
}
```
Note the `- type: StatusEffects` requirement WP9 discovered
(`WP9-report.md` §2.2: "Wolfgate's `SharedStunSystem.TryParalyze` ... refuses any entity without the **old**
`StatusEffectsComponent`") — add `- type: StatusEffects` to `WolfmedPainShockBody` or the stun call
(`StunSystemOnyxCompat.TryUpdateParalyzeDuration`, §2.8) silently no-ops and this test fails with "no
`StunnedComponent`" for a reason that has nothing to do with pain shock itself. This is the same trap WP9 hit
once already; do not rediscover it the hard way.

**Dependencies on phase-2 WPs:** none for the interim bespoke-fixture version (all of `PainSystem`,
`StunSystemOnyxCompat`, `WoundDamageRoutingSystem` are phase-1). The **real**, production-relevant version of
this test additionally depends on the `PainShockTarget` YAML addition above landing on `BaseMobSpeciesOrganic` —
write both: the interim one now, and re-target it at `MobHuman` once that YAML lands (mirroring how
`RealWoundHostPassiveDamageIsNeutralisedTest` in §4.2 uses the real species).

---

### 4.6 HighPainThreshold trait assertion — `HighPainThresholdReducesWoundPainGainTest`

**File:** new file, `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedPainTest.cs` (no existing Onyx test
file to extend — confirmed by `git ls-tree` over the sparse checkout, `HighPainThreshold` has no test at this pin
in Onyx itself; house it under `_WF` since it is new coverage authored for this port, not a literal port).

**Exact mechanism, traced through source (not assumed):**
- `HighPainThresholdComponent.PainMultiplier` defaults `0.75f` (`ONYX Content.Shared/_Onyx/Traits
  /HighPainThresholdComponent.cs`).
- `HighPainThresholdSystem.OnModifyPainGain` does `args.Multiplier *= ent.Comp.PainMultiplier;`
  (`ONYX Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs`).
- `ModifyPainGainEvent` is raised **directed on the body** (not the part) from two call sites:
  `PainSystem.RefreshWoundPain` (`WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:109-117`, wound-floor
  recomputation) and `PainSystem.ChangePain` (`:232-241`, the actual per-hit pain gain path, called from
  `PainSystem.ApplyDamage` at `:395-396`, which is itself invoked on every routed hit —
  `WoundDamageProjectionSystem.cs:99`, `WoundDamageRoutingSystem.cs:1078`). **So `HighPainThresholdComponent`
  must be on the body/mob entity, not a part**, matching how a trait's granted component always lands on the
  player's mob.
- `PainComponent.DamageMultipliers["Blunt"] = 0.87f` by default
  (`WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:119-129`), so 10 Blunt damage through `ApplyDamage`
  → `CalculatePain` gives `10 * 0.87 = 8.7` pain **before** the trait multiplier, exactly matching the existing
  `PainApiAndProjectionTest`'s baseline (`WG/Content.IntegrationTests/Tests/_Onyx/Wounds
  /WoundDamageFoundationTest.cs:508`, `Is.EqualTo(FixedPoint2.New(8.7))`) — reuse that number as the control.
- With the trait: `8.7 * 0.75 = 6.525`.

**Prototype:**
```yaml
- type: entity
  id: WolfmedHighPainThresholdBody
  parent: InventoryBase
  components:
  - type: Body
    prototype: WolfmedBridgeBodyGraph
  - type: Damageable
    damageContainer: Biological
  - type: WoundHost
  - type: HighPainThreshold
```

**Test:**
```csharp
[Test]
public async Task HighPainThresholdReducesWoundPainGainTest()
{
    var server = Pair.Server;
    await server.WaitIdleAsync();
    var entities = server.ResolveDependency<IEntityManager>();
    var map = await Pair.CreateTestMap();

    await server.WaitAssertion(() =>
    {
        var control = entities.SpawnEntity("WolfmedBridgeBody", map.GridCoords);   // no HighPainThreshold
        var traited = entities.SpawnEntity("WolfmedHighPainThresholdBody", map.GridCoords);
        var graph = entities.System<SharedBodySystem>();
        var routing = entities.System<WoundDamageRoutingSystem>();
        var pain = entities.System<PainSystem>();

        var controlHead = graph.GetBodyChildren(control).Single(p => p.Component.PartType == BodyPartType.Head).Id;
        var traitedHead = graph.GetBodyChildren(traited).Single(p => p.Component.PartType == BodyPartType.Head).Id;

        Assert.That(routing.TryApplyPartDamage(control, controlHead, Spec("Blunt", 10)));
        Assert.That(routing.TryApplyPartDamage(traited, traitedHead, Spec("Blunt", 10)));

        Assert.Multiple(() =>
        {
            Assert.That(pain.GetRawPain(controlHead), Is.EqualTo(FixedPoint2.New(8.7)));
            Assert.That(pain.GetRawPain(traitedHead), Is.EqualTo(FixedPoint2.New(6.525)));
            Assert.That(pain.GetRawPain(traitedHead), Is.LessThan(pain.GetRawPain(controlHead)));
        });
    });
}
```
Also port the **trait prototype + locale** alongside the component/system, since the test's realism depends on
the trait actually being selectable, not just the component working in isolation:
`ONYX Resources/Prototypes/_Onyx/Traits/quirks.yml` (`HighPainThreshold` trait block, `conflicts: [PainNumbness]`
— Wolfgate already has a trait id `PainNumbness` at `WG/Resources/Prototypes/Traits/disabilities.yml:67-73`, so
the conflict reference resolves with no rename needed) and
`ONYX Resources/Locale/en-US/_Onyx/traits/quirks.ftl:13-14` (`trait-high-pain-threshold-name/desc`, confirmed
present in the sparse checkout).

**Dependencies on phase-2 WPs:** `HighPainThresholdComponent`/`HighPainThresholdSystem` port (trivial — 2 files,
~30 lines total, zero non-existent dependencies per §2's subscription-pair check). No dependency on
`FractureEffectSystem`/`FractureAlertSystem`/pain-shock wiring. **This is the cheapest test in this entire plan
to land** and has no blockers — write it first.

---

## 5. Test-by-test summary table

| # | Test | File | Setup | Key assertion | Depends on |
|---|---|---|---|---|---|
| 1 | `ArmorPenetrationReachesWoundHostsTest` (T-AP) | `_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | Two `WolfmedBridgeBody`s + `WolfmedBridgeArmor` (Blunt 0.5 coeff) equipped | AP=0 → 5 dmg on arm; AP=1.0 → 10 dmg; high > low | Nothing — phase-1 code (WP5/WP8) only |
| 2 | `RealWoundHostPassiveDamageIsNeutralisedTest` (T-PASSIVE part A) | `_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | Real `MobHuman`, 10 Blunt on left arm | `PassiveDamageComponent.Damage.Empty`; arm damage unchanged after `RunSeconds(60f)` | Nothing — phase-1 D29 YAML |
| 3 | `PassiveDamageMechanismStillRoutesIfReenabledTest` (T-PASSIVE part B) | `_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | Bespoke wound-host + control, both with real (healing) `PassiveDamage` | Control heals; wound-host fixture does **not** (canary for the ungated mechanism) | Nothing — phase-1 `PassiveDamageSystem`/routing |
| 4 | `EffectsRefreshOnTreatmentHealingAndDetachTest` | `_Onyx/Wounds/WoundFractureTest.cs` | Existing `WoundFractureBody` fixture | Movement modifier 0.4 on leg fracture; manipulation multiplier 2x on arm fracture, 1x after mend; movement back to 1x after detach | `FractureEffectSystem` port (WP10) + hands-block fix (§4.3) |
| 5 | `FractureAlertTracksGradeAndTreatmentTest` | `_Onyx/Wounds/WoundFractureTest.cs` | Existing `WoundFractureBody` fixture | `BrokenBones` alert off → on at grade ≥ Simple → off after `TryMend` | `FractureEffectSystem` **and** `FractureAlertSystem` (WP10) |
| 6 | Below-Hairline-threshold negative case | `_Onyx/Wounds/WoundFractureTest.cs` (same test or a second `[Test]`) | Fresh body, 19 damage (< Hairline) | `IsShowingAlert` stays false | same as #5 |
| 7 | `PainShockStunsAtThresholdAndRearmsBelowItTest` (interim) | `_WF/Wolfmed/WolfmedPainTest.cs` | Bespoke `WolfmedPainShockBody` with `PainShockTarget` + `StatusEffects` | 160 Blunt to head → `StunnedComponent` present | Nothing new to write, but exposes the missing `PainShockTarget` YAML wiring — real version depends on that landing |
| 8 | `HighPainThresholdReducesWoundPainGainTest` | `_WF/Wolfmed/WolfmedPainTest.cs` | Control `WolfmedBridgeBody` vs `WolfmedHighPainThresholdBody`, both 10 Blunt to head | Control 8.7 raw pain; traited 6.525; traited < control | `HighPainThresholdComponent`/`System` port only — cheapest, no blockers |

---

## 6. File placement and run commands

**New/extended files:**
- `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` — extend (tests 1-3): add
  `WolfmedBridgeArmor`, `WolfmedPassiveWoundHost`, `WolfmedPassiveControl` to `[TestPrototypes]`; add the three
  test methods.
- `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` — extend (tests 4-6): replace the
  `// WOLFGATE` placeholder comment with the ported test; add the fracture-alert test(s).
- `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedPainTest.cs` — new file (tests 7-8).

**Build checkpoints** (PLAN.md ground rule 5, all three required from WP9 onward):
```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
```

**YAML lint** (Release only — `ErrorNode` crashes the linter in other configs; needed if `HighPainThreshold`
trait/`PainShockTarget` YAML is added):
```
dotnet run --project Content.YAMLLinter -c Release
```

**Targeted test run** (mirrors WP9's own filter):
```
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
  --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed" \
  --logger "console;verbosity=detailed"
```

**Environmental sanity check before trusting any failure** (§3 trap 2):
```
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
  --filter "FullyQualifiedName~DockTest"
```

**If a test reports bare `Skipped` with no assertion text**, re-run it alone under a narrow `--filter` — it was
likely dirty-disposed by an unrelated pooled-pair failure (§3 trap 1), not actually exercised.

---

## 7. Open items for whoever scopes/implements WP10 (found during this test-planning pass, not resolved here)

1. **`PainShockTargetComponent` is missing from every shipped mob prototype.** Add `- type: PainShockTarget` to
   `BaseMobSpeciesOrganic`'s existing `// WOLFGATE` block (`base.yml:250-270`) or pain shock stays permanently
   inert on real mobs. Not currently in DECISIONS.md's P2-1 bullet list by name — flag explicitly.
2. **No pain/shock `AlertPrototype` exists in the Onyx pin.** Do not invent one to satisfy the literal wording of
   a "pain-alert assertion" — the mechanism Onyx actually ships is a client overlay + a stun, not an alert.
3. **`FractureEffectSystem`'s hand-symmetry lookup needs a real adaptation**, not a mechanical rename: Onyx's
   `IsHolding`/`GetActiveHand` calls expect a `string?` hand id; Wolfgate's return `Hand?`. Read
   `WG/Content.Shared/Hands/Components/Hand.cs` for the correct field before implementing (not read in this
   analysis).
4. **`GetManipulationDurationMultiplierEvent` vs Wolfgate's existing `GetDoAfterDelayMultiplierEvent`** — PLAN.md
   §4's WP10 paragraph says to subscribe the latter instead of adding Onyx's own event type, but they have
   different shapes (Onyx: `ref struct` directed on the wound-host body, keyed by which hand is used; Wolfgate:
   plain class raised on the do-after's user and relayed to body parts via `IBodyPartRelayEvent`/
   `DoAfterDelayMultiplierComponent`). Confirm before implementation whether `FractureEffectSystem.
   GetDurationMultiplier(EntityUid, EntityUid?)` stays as a public method the ported test calls directly
   (recommended — keeps §4.3's test a near-verbatim port) or gets replaced by driving `GetDoAfterDelayMultiplierEvent`
   directly, which would require rewriting §4.3's test to raise that event instead.
5. **Test 5/6 (fracture alert) cannot pass with only one of `FractureEffectSystem`/`FractureAlertSystem` ported** —
   `FractureAlertSystem.Refresh` has no caller except `FractureEffectSystem`'s event handlers. If WP10 is split
   across sub-work-packages, keep these two together or land `FractureEffectSystem` first.
