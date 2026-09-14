# Wolfmed Phase 4 — test plan (P4-8)

**Scope:** plan the phase-4 integration tests only. No production code is touched by this report. Every
claim below was verified directly against the current tree (`WG` = the worktree at
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` — note there is **no**
`WG/` subfolder; that worktree root *is* `WG`) or against the Onyx pin (`C:/tmp/onyx`, commit
`2f5bab9946539cbe083010c9ae6fbc59b47ae377`) via `git show`/`git grep`. All phase-4 production code (P4-1
through P4-7) is **unimplemented as of this report** — this is a pre-implementation test plan. Every test
below therefore depends on a not-yet-written work package; §6 states exactly which one, and §8 flags every
point where the test's shape constrains an implementation choice that is properly the WP's to make.

---

## 1. Existing suites — fixtures, helpers, patterns, traps (recap, re-verified)

Six files in `Content.IntegrationTests/Tests/_Onyx/Wounds/` (`WoundDamageFoundationTest`,
`WoundBleedingTest`, `WoundHealingTest`, `WoundFractureTest`, `WoundScarTest`, `AmputationConsequenceTest`)
and six in `Tests/_WF/Wolfmed/` (`WolfmedDamageBridgeTest`, `WolfmedAmputationTest`, `WolfmedOrganTest`,
`WolfmedPainTest`, `WolfmedReattachTest`, `WolfmedVisualsTest`) exist today; **65/65 pass** per
`WOLFMED_STATUS.md`. All six standing traps from PLAN2 §6.1 / PLAN3 §6.1 still apply and I did not find any
new ones specific to surgery/reagent testing beyond what's noted inline below:

1. **`TerminatingOrDeleted`** — any new reactor to `BodyPartAdded/Removed`/`OrganGot*`/organ destruction must
   guard before `EnsureComp`/`CreateOrMergeWound`, or one unguarded spawn/dispose poisons the whole pooled
   pair run (cost WP9 13 unrelated failures once).
2. **`DockTest` first**, always, before blaming a red test on Wolfmed.
3. **`Assert.Multiple` bodies must be fully synchronous** (no `async`/`await` inside the lambda).
4. **`[TestPrototypes]` ids are a global pool.** Phase 4 must pick new ids clear of everything phases 1-3
   already registered (full list: `WoundFoundationBody*`, `WoundBleedingBody*`, `WoundHealingBody*`,
   `WoundScarBody*`, `WoundFractureBody*`/`WoundFractureArmor`/`WoundFractureHandsBody*`,
   `WolfmedBridgeBody*`, `WolfmedAmputationBody*`/`WolfmedAmputationOverflow*`,
   `AmputationConsequenceTest*`, `WolfmedOrganTest*`/`WolfmedOrganControlBody`/`WolfmedOrganFunc*`,
   `WolfmedReattachBody*`, `WolfmedPain*` (not yet read in full but present)). I use a `WolfmedSurgery*` /
   `WolfmedTreatment*` prefix throughout §5 to stay clear.
5. **YAML lints in Release only.**
6. **Every literal is a prediction until measured** — every derivation in §5 is shown; none may be assumed
   from Onyx's own numbers without re-checking Onyx's own YAML/C# defaults, several of which were already
   found stale in phases 1-3 (fracture grades, bleeding minimums, healing multiplier, coverage tests).
7. **New trap found in this pass — `_treatmentCapabilities` is opt-in, not ambient.**
   `WoundDamageRoutingSystem.CanTreatPart` (`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:933-940`)
   returns `true` unconditionally unless the calling code first wraps the operation in
   `WithTreatmentCapabilities(body, capabilities, action)` (`:136-147`), which stashes the set in a
   per-body dictionary for the duration of `action()` only. **A test (or a reagent effect) that calls
   `_routing.TryApplyPartDamage`/`TryApplyDamage` directly, without that wrapper, gets zero capability
   gating** — every part is "treatable" by default. T-REAGENT's negative case (§5) only means something if
   the code path under test actually opens that wrapper; I flag this as a P4-1 implementation dependency,
   not something the test can paper over.
8. **New trap found in this pass — reagent-effect testing needs no chemistry.** Every ported Onyx entity
   effect (`SuppressPain`, `MendFractures`, and the extended `HealthChange`/`EvenHealthChange`) can be
   exercised by constructing an `EntityEffectReagentArgs` directly and calling `.Effect(args)` — this is the
   existing Wolfgate pattern (see `Content.Server/EntityEffects/Effects/EvenHealthChange.cs:83`, which itself
   resolves `args.EntityManager.System<DamageableSystem>()` and calls `TryChangeDamage` — no reagent
   solution, metabolism, or `Content.IntegrationTests` reagent-drinking harness is needed). Constructor:
   `new EntityEffectReagentArgs(target, entityManager, organEntity: null, source: null, quantity: FixedPoint2.New(1), reagent: null, method: null, scale: FixedPoint2.New(1))`
   (`Content.Shared/EntityEffects/EntityEffect.cs:109-124`, verified).

---

## 2. Onyx tests phase 4 unlocks — read in full, analysed

### 2.1 `WoundSurgeryTest.cs` (ONYX, 2 tests) — **NOT PORTABLE as written; its *pattern* is exactly what phase 4 needs**

Read via `git show HEAD:Content.IntegrationTests/Tests/_Onyx/Wounds/WoundSurgeryTest.cs`. Both tests target
`Content.Shared._Onyx.Medical.Surgery.WoundSurgerySystem` — Onyx's own surgery framework, excluded outright
by D7/D8. It is unportable for the same reason every Onyx-surgery file is. But its **driving technique is
directly reusable on Shitmed's surgery system**, and is in fact the technique I specify for every new test in
§5:

```csharp
private static bool IsValid(EntityUid condition, EntityUid part, IEntityManager entities)
{
    var ev = new SurgeryValidEvent(EntityUid.Invalid, part);
    entities.EventBus.RaiseLocalEvent(condition, ref ev);
    return !ev.Cancelled;
}

private static void RaiseStep(EntityUid effect, EntityUid part, IEntityManager entities)
{
    var ev = new SurgeryStepEvent(EntityUid.Invalid, EntityUid.Invalid, part, []);
    entities.EventBus.RaiseLocalEvent(effect, ref ev);
}
```

Onyx spawns a **bare entity carrying only the condition/effect component** and raises the event directly on
it — no do-after, no UI, no `SurgeryComponent`/`SurgeryStepComponent` wrapper at all. `FractureMendAndStaleNoOpTest`
proves the pattern end-to-end: it applies real wound damage through the router, creates a real fracture, then
raises `SurgeryStepEvent` on a bare `SurgeryMendFractureEffect`-carrying entity and asserts
`fractures.GetFracture(arm)` is null afterwards, then raises it again and asserts idempotence (still null).
This is precisely the "drive the effect directly, assert on wound-system state" shape §5 uses.

### 2.2 `WoundSurgeryScarTest.cs` (ONYX, 1 test) — **NOT PORTABLE**, same reason (Onyx's `SurgeryCloseIncisionEffect`)

Confirms `CCVars.SurgeryScarChance` exists in Onyx and is consumed by `SurgeryCloseIncisionEffect`'s handler
(`Content.Server._Onyx.Medical.Surgery.SurgerySystem.WoundEffects.cs:24-37`, read in full — see §3). Not
relevant to Wolfgate's phase 4 since D7/P4-3 says **reuse Wolfgate's own `SurgeryOpenIncision`/
`SurgeryCloseIncision`**, which carry no scar-chance concept today and are out of P4-3's stated scope
(P4-3 lists `SurgeryStopBleeding`, tend-deep, stop-internal-bleeding, mend-fracture, heal-amputation-consequence,
heal-organ — not a scar mechanic). **No scar test is specified in §5**; if a future WP ports Onyx's
`SurgicalIncisionWound`-scarring-on-close behaviour onto Wolfgate's `SurgeryCloseIncisionConditionComponent`
path, it would need its own CCVar and its own test — out of scope here, flagged for the record.

### 2.3 `WoundBleedingTest.TourniquetStopsOnlySelectedPartTest` (ONYX) — **portable, cheap, near-verbatim**

Read via `git show`. Full Onyx source for `TourniquetComponent`/`TourniquetSystem` also read in full
(`Content.Shared/_Onyx/Medical/Tourniquet/{TourniquetComponent,TourniquetSystem}.cs`). What it asserts:

- `TourniquetComponent` exists and is **not** a `HealingComponent` (it's its own device, not chemistry).
- `TourniquetSystem.Apply(body, part)` (public) returns `true` and afterwards `bleeding.GetPartRate(head) == 0`
  while `bleeding.GetPartRate(torso) > 0` (untouched) — the "only selected part" contract.

`TourniquetSystem.Apply` internals (verified): iterates `_wounds.GetWounds(part)`, and for every wound with
`WoundBleedingComponent.CurrentRate > 0` calls `_bleeding.SetTreatment(wound.Owner, BleedingTreatment.Clamped)`.
Both `WoundSystem.GetWounds` and `WoundBleedingSystem.SetTreatment` are already vendored, byte-identical, in
WG (confirmed used exactly this way in `WoundBleedingTest.cs:78,81` today). **Everything Apply needs already
exists in WG.** The only non-portable piece is target resolution: Onyx's `TryStart` uses
`Content.Shared._Onyx.Targeting.TargetResolverSystem` + Onyx's own `TargetingComponent` (D10-excluded) — the
Wolfgate port must use `WoundTargetResolver.TryResolveExact`/`TryResolveAvailable` (already shipped,
`Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs`) against Shitmed's `TargetingComponent`
instead. `Apply(body, part)` itself is target-agnostic (takes a resolved part), so **T-TOURNIQUET in §5 tests
`Apply` directly and does not need to exercise the do-after/UseInHand/AfterInteract layer at all** — same
"skip the interaction layer, test the logic" pattern PLAN.md's testing philosophy already uses everywhere
else (project memory: prefer logic tests when the user is present).

### 2.4 `HealthAnalyzerPartDamageTest.cs` (ONYX, 2 tests) — **not directly portable (targets Onyx's own analyzer additions), but fully re-derivable — see §5 T-ANALYZER**

Read in full via `git show`. Both tests target methods Onyx itself added to the **upstream**
`Content.Server.Medical.HealthAnalyzerSystem` (same class Wolfgate has, just extended) — `BuildPartDamage`
and `BuildWoundDiagnostics`, plus a client-side static helper `HealthAnalyzerControl.IsDangerousBloodLevel`.
This is the load-bearing discovery for T-ANALYZER: **Onyx does not test the network message directly — it
factors the message's content into a plain, directly-callable builder method and tests *that*.**
`BuildWoundDiagnostics(body)` returns an object with `.Parts[TargetBodyPart]` entries carrying
`.BleedingRate` (float), `.Fracture` (`FractureGrade`), `.FractureTreatment` (`FractureTreatment`),
`.ScarCount` (int); it returns `null` for a non-wound-host. `BuildPartDamage(body)` returns
`Dictionary<TargetBodyPart, DamageSpecifier>?`, also null for non-wound-hosts, and folds `Groin` onto
`Chest`'s exact entry (`snapshot[TargetBodyPart.Groin] == snapshot[TargetBodyPart.Chest]`, asserted by
reference-equal dictionary value in Onyx's own test).

Wolfgate specifics that change the fixture (verified): WG's `HealthAnalyzerScannedUserMessage`
(`Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`) already carries a Shitmed
`Dictionary<TargetBodyPart, TargetIntegrity>? Body` field, populated by
`SharedBodySystem.GetBodyPartStatus` — but **no wound-diagnostic field of any kind exists yet.** WG's
`HealthAnalyzerSystem.UpdateScannedUser` (`Content.Server/Medical/HealthAnalyzerSystem.cs:198-247`, read in
full) is `public`, gated only on `HasComp<DamageableComponent>(target)`, and calls
`_uiSystem.ServerSendUiMessage(...)` at the end — there is no headless capture point for what actually goes
over the wire without a client harness. **P4-4's implementation must add a `BuildWoundDiagnostics`-shaped
public method to mirror Onyx's factoring**, exactly the way Onyx did it, so the test can call it directly
instead of intercepting a network message. §8 escalates this as a decision (with this as the recommendation).
D9 applies: `TargetBodyPart.Groin` must fold to `.Torso`'s entry, not `.Chest`'s (`BodyPartType.Chest` does
not exist in WG).

### 2.5 `SurgeryStepSequencePrototypeTest.cs` (ONYX, 3 tests) — **not portable as written; a Shitmed-shaped substitute is proposed in §5**

Read in full via `git show`. All three tests validate `Content.Shared._Onyx.Medical.Surgery.SurgeryComponent`,
whose `Steps` field is `Dictionary<string, Section>` (fallback + conditional sections, each with a
`Required` component set) — Onyx's own richer surgery-selection model. **Shitmed's `SurgeryComponent.Steps`
is a flat `List<EntProtoId>`** (`Content.Shared/_Shitmed/Surgery/SurgeryComponent.cs:16`, verified) with no
section/fallback concept at all, so none of the three assertions (`EverySurgeryHasOneFallbackAndNonEmptySections`,
`CyberneticAttachmentsHaveFallbackAndConditionalSequences`, `OrdinarySurgeryHasNamedFallbackSection`) transfers.
The two Onyx-only fixture attributes it uses, `[SidedDependency(Side.Server)]` and `[RunOnSide(Side.Server)]`
(`Content.IntegrationTests.Fixtures.Attributes`), are **not confirmed present in WG** — I did not find them
in a grep of `Content.IntegrationTests/Fixtures/Attributes`; PLAN.md §6's claim that "the harness pattern
needs no change" was verified only for `GameTest`/`[TestPrototypes]`, not for these two attributes
specifically, so treat their availability as unverified until the implementing WP checks.

What **is** worth keeping from the spirit of this test: a cheap, prototype-only sanity check that every new
phase-4 wound-surgery prototype (`SurgeryStopBleeding`, `SurgeryStopInternalBleeding`, `SurgeryMendFracture`,
`SurgeryHealAmputationConsequence`, per-organ heal surgeries) resolves, has a non-empty `Steps` list, and
every step id in it actually carries `Content.Shared._Shitmed.Medical.Surgery.Steps.SurgeryStepComponent`.
This catches a typo'd step id for the price of an `EnumeratePrototypes` loop with no mob spawn. Specified as
**T-SURGERY-PROTOTYPE-SANITY** in §5.

### 2.6 Grep for MedicalPatch / SuppressPain / TreatmentCapabilit in Onyx's own tests

```
git grep -lni "medicalpatch\|suppresspain\|treatmentcapabilit" HEAD -- 'Content.IntegrationTests/*'
  -> WoundDamageFoundationTest.cs, WoundHealingTest.cs   (both just use the TreatmentCapability *parameter*
                                                            already ported in phase 1 — no new test found)
```

**Onyx itself has zero dedicated tests for `MedicalPatchSystem` or `SuppressPainEntityEffectSystem`.** T-PATCH
and the SuppressPain half of T-REAGENT in §5 are therefore **new coverage with no Onyx test to port or even
adapt** — designed from the production source alone (`Content.Server/_Onyx/Medical/MedicalPatchComponent.cs`,
`MedicalPatchSystem.cs`, and `Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs`, all read in full).
`MedicalPatchSystem` is orthogonal to wounds at the mechanism level — it is a `StickyComponent`-driven
solution-transfer loop (`EntityStuckEvent`/`EntityUnstuckEvent`, periodic `TryInject` into the target's
injectable solution) with **no `TreatmentCapability`, wound, or `WoundHostComponent` reference anywhere in
its source**. Its only wound-relevant surface is that P4-2 will presumably use it as the delivery vehicle for
a wound-treating reagent, so T-PATCH tests the delivery mechanic itself (does it inject on a schedule, does
it stop on unstick, does it respect `SingleUse`) on a `WoundHostComponent` body as a D2-adjacent regression
guard, not the wound-healing logic itself (that's T-REAGENT's job, exercised on the reagent effect it
delivers). `StickyComponent`/`EntityStuckEvent`/`EntityUnstuckEvent` and
`SharedSolutionContainerSystem.TryGetSolution`/`TryGetInjectableSolution` all confirmed present in WG.

---

## 3. Driving a Shitmed surgery step headlessly — the entry point

**There are no existing Shitmed surgery tests in WG** (`find Content.IntegrationTests -iname "*surger*"` →
empty). `SharedSurgerySystem`'s real entry points are `protected`/require a live do-after
(`IsSurgeryValid` is `protected`; the do-after path goes through `OnTargetDoAfter` → `IsSurgeryValid` →
`PreviousStepsComplete` → `CanPerformStep` → raises `SurgeryStepEvent` on the step singleton). None of that
is reachable from a different-assembly test class directly.

**The verified, minimal, production-code-exercising entry point** — identical in spirit to Onyx's own test
pattern (§2.1) and requiring zero new test infrastructure:

```csharp
var surgery = entities.System<SharedSurgerySystem>(); // or SurgerySystem on the server, same public surface
var stepEnt = surgery.GetSingleton("SurgeryStepClampWoundBleeding")!.Value; // public, spawns/reuses a singleton
var ev = new SurgeryStepEvent(user, body, part, new List<EntityUid>(), surgeryEnt);
entities.EventBus.RaiseLocalEvent(stepEnt, ref ev);
```

`GetSingleton(EntProtoId)` is `public` (`SharedSurgerySystem.cs:349-364`, verified) — it spawns the step/
surgery entity at `MapCoordinates.Nullspace` once and caches it, exactly the object every real step-perform
raises the event on. Raising `SurgeryStepEvent` directly on that singleton invokes **every** handler
subscribed via `SubSurgery<TComp>`/`SubscribeLocalEvent<TComp, SurgeryStepEvent>` for whatever components
that step's prototype actually carries — i.e. the *real* production handler for `SurgeryTendWoundsEffect`,
`SurgeryClampBleedingEffect` (once vendored), etc. This **bypasses**: `IsSurgeryValid`'s condition checks
(`SurgeryValidEvent`), `PreviousStepsComplete`, `CanPerformStep`'s tool/hand checks, and the do-after delay.
It does **not** bypass anything the step's own effect handler itself queries (e.g. `OnTendWoundsStep`'s
`HasDamageGroup` guard, or a vendored `OnClampBleeding`'s `FindWound` call) — the real wound-system state
change is exercised, only the "can I start this surgery" gate and the click-through UI are skipped. This is
consistent with the project's standing "prefer logic tests when the user is present" guidance and with every
prior phase's testing style (drive the system API directly, not the UI/do-after).

**To also test a condition component** (needed for T-REATTACH-BLOCKED's surgery gate and for
`SurgeryHasWoundConditionComponent`/`SurgeryFractureGradeConditionComponent` once vendored), raise
`SurgeryValidEvent` the same way, on the *step or surgery* singleton that carries the condition:

```csharp
var ev = new SurgeryValidEvent(body, part);
entities.EventBus.RaiseLocalEvent(conditionEnt, ref ev);
Assert.That(ev.Cancelled, Is.False); // or True, for the negative case
```

**Fixture pattern.** New wound-surgery step/surgery prototypes for the tests in §5 follow the exact shape
already shipped in `Resources/Prototypes/_Shitmed/Entities/Surgery/{surgeries,surgery_steps}.yml` (read in
full, verified — e.g. `SurgeryStepRepairBruteTissue` is `parent: SurgeryStepBase`, `- type: SurgeryStep`
with a `tool:` requirement and `duration`, plus the effect component; `SurgeryOpenIncision` is
`parent: SurgeryBase`, `- type: Surgery` with a flat `steps:` list, plus a condition component). Test
`[TestPrototypes]` blocks in §5 declare bespoke, phase-4-scoped surgery/step ids so they do not collide with
or depend on the real cargo/loadout-facing prototypes P4-2/P4-3 will also add.

---

## 4. Reference implementation for the new step/condition components (Onyx source, read in full)

`Content.Server/_Onyx/Medical/Surgery/WoundSurgerySystem.cs` (Onyx, read in full via `git show`) is the
production logic that P4-3's implementer will most likely vendor. **Component-name collision check, re-run
in this pass** (`grep -rn "class <Name>Component\b\|id: <Name>\b" Content.Shared Content.Server
Resources/Prototypes`, binaries excluded):

| Onyx type | WG collision? | Disposition |
|---|---|---|
| `SurgeryHasWoundConditionComponent` | **none found** | vendorable verbatim |
| `SurgeryClampBleedingEffectComponent` | **none found** | vendorable verbatim — this is `SurgeryStopBleeding`'s effect |
| `SurgeryFractureGradeConditionComponent` | **none found** | vendorable verbatim |
| `SurgeryMendFractureEffectComponent` | **none found** | vendorable verbatim — calls `WoundFractureSystem.TryMend` |
| `SurgeryTreatWoundEffectComponent` | **none found** | vendorable verbatim — used for **both** stop-internal-bleeding (`internalBleeding: true`) and heal-amputation-consequence (`woundPrototype: AmputationConsequenceWound`) |
| `SurgeryWoundedConditionComponent` | **COLLIDES** — `Content.Shared._Shitmed.Medical.Surgery.Conditions.SurgeryWoundedConditionComponent` already registers this exact name (`OnWoundedValid` at `SharedSurgerySystem.cs:120-128` checks flat `TotalDamage`) | **extend Wolfgate's in place** (DECISIONS.md P4-3), not vendor Onyx's |
| `SurgeryTendWoundsEffectComponent` | **COLLIDES** — `Content.Shared._Shitmed.Medical.Surgery.Effects.Step.SurgeryTendWoundsEffectComponent` already registers this name and is live in shipped content (`SurgeryStepRepairBruteTissue`/`BurnTissue`, `Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml:319-353`) | **extend Wolfgate's `OnTendWoundsStep`/`OnTendWoundsCheck` in place**, gated `if (HasComp<WoundHostComponent>(args.Body)) { /* Onyx's severity-aware GetGroupSeverity + TryHealWounds path */ } else { /* existing flat-damage path, byte-identical */ }` |

Onyx's handler bodies (all read in full, `WoundSurgerySystem.cs`), summarised for the assertions in §5:

- **`SurgeryClampBleedingEffectComponent{ Amount, WoundPrototype? }`** → on step: finds the highest-severity
  wound on the part matching `WoundPrototype` (or any, if null) with `CurrentRate > 0`, calls
  `_bleeding.ReduceBleeding(wound, Amount)`. Complete-check: cancelled (step not done) while any matching
  bleeding wound remains.
- **`SurgeryMendFractureEffectComponent`** (no fields) → on step: `_fractures.TryMend(fracture.Owner)` if
  `GetFracture(part)` is non-null. Complete-check: cancelled while `GetFracture(part)` is still non-null.
- **`SurgeryTreatWoundEffectComponent{ WoundPrototype?, InternalBleeding, Amount = MaxValue, Damage }`** → on
  step: finds the matching wound via the shared `FindWound` helper (by prototype id, or by
  `WoundInternalBleedingComponent.Severity > 0 && state == Open` when `InternalBleeding` is set), calls
  `_wounds.TreatWound(wound, Amount)`. **`TreatWound` with `amount >= severity` drives `ChangeSeverity` to
  zero, which `RemoveWound`s the wound entity outright** (`WoundSystem.cs:284-314`, read in full — confirmed:
  `if (severity == FixedPoint2.Zero) { ...; return RemoveWound(wound); }`). This is the mechanism
  T-SURG-AMPCONSEQUENCE and T-REATTACH-BLOCKED rely on: after treatment, the `AmputationConsequenceWound`
  entity is **gone**, not merely reduced.
- **Extended `SurgeryTendWoundsEffectComponent` (Onyx half)** → `GetGroupSeverity(part, group)` sums the
  severity of every non-scar wound on the part whose prototype's `damageTypes` overlap the group; if > 0, it
  computes a heal bonus (`severity * HealMultiplier * (dead ? 0.2f : 1f)`), builds an adjusted
  `DamageSpecifier`, and — the part that matters for "tend **deep**" — calls
  **`_wounds.TryHealWounds(part, treatment)`** (already public, `WoundSystem.cs:400`) in addition to (or
  instead of) reducing flat part damage. This is what distinguishes "deep" tending (heals the *wound entity's
  severity*, closing/removing it below its minimum-severity floor) from Wolfgate's existing shallow tend
  (which only ever reduces `DamageableComponent`, leaving the wound entity itself untouched — confirmed by
  reading `SharedSurgerySystem.Steps.cs:343-365`, `OnTendWoundsStep`, which never calls anything on
  `WoundSystem`).

**This table and the handler summary are a recommendation for whoever implements P4-3, not a mandate** — the
tests in §5 are written to assert on the *wound-system-visible outcome* (a wound's severity/presence, a
fracture's grade, a bleeding wound's rate), which is stable regardless of whether the implementer names the
new components exactly as above. Where a test's setup needs a concrete component/prototype name to exist
(because it constructs a `[TestPrototypes]` step entity), I use the names in this table; if the implementing
WP picks different names, only the fixture's component list needs updating, not the assertions.

---

## 5. New tests — the deliverable

All new tests target `Content.IntegrationTests/Tests/_WF/Wolfmed/` (new phase-4 mechanics, `_WF`-style,
matching the project's convention that Onyx-shaped-but-Wolfgate-authored logic lives under `_WF`) **except**
where a test is a direct, minimal-adaptation port of an Onyx test, which goes to
`Tests/_Onyx/Wounds/` or `Tests/_Onyx/Medical/` alongside its siblings (matching phases 1-3's placement rule).

| # | Test | File | Setup | Key assertion(s) | Depends on (P4 WP) |
|---|---|---|---|---|---|
| **T-TOURNIQUET** | `TourniquetStopsOnlySelectedPartTest` (ported, target-resolution adapted) | `Tests/_Onyx/Wounds/WoundBleedingTest.cs` (new method, alongside the existing skip-note at line 147-148, which this replaces) | `WoundBleedingBody` (existing fixture) with a fresh `Slash 10` wound on head and torso (`routing.TryApplyPartDamage`, both bleed); spawn `Tourniquet` prototype (ported P4-2). Adapt Onyx's `TryStart`'s target lookup: call `entities.System<TourniquetSystem>().Apply(body, head)` directly (§2.3 — `Apply` is target-agnostic; the do-after/targeting-component layer is skipped as a logic test) | `Apply` returns `true`; `bleeding.GetPartRate(head) == 0`; `bleeding.GetPartRate(torso) > 0` (unaffected); a **second** `Apply(body, head)` call returns `false` (`CanApply` requires `GetPartRate(part) > 0`, already zero) — the "does not double-apply" guard Onyx's own test never checked, added here for completeness | P4-2 (Tourniquet vendored, server-side per D13) |
| **T-PATCH** | `MedicalPatchInjectsOnScheduleAndStopsOnUnstickTest` | `Tests/_WF/Wolfmed/WolfmedMedicalPatchTest.cs` (new) | Bespoke `[TestPrototypes]`: a `WolfmedPatchTestReagent` solution-bearing item carrying `- type: MedicalPatch { solutionName: patch, transferAmount: 5, updateTime: 1 }` and a `- type: SolutionContainerManager` with a `patch` solution pre-filled with a **new, inert test reagent** (`WolfmedPatchTestChem`, no metabolism/entity effects — keeps the test independent of P4-1's reagent work); a `WoundHostBody` target (reuse `WoundBleedingBody`'s shape) with an injectable `chemstream`/`bloodstream` solution. Stick the patch via `EntityManager.EventBus.RaiseLocalEvent(patch, new EntityStuckEvent(target, user))` directly (mirrors §2.6's finding that the mechanic is a plain `StickyComponent` consumer, not a wound-specific one) | (a) immediately after stick, if `InjectAmmountOnAttatch`/`InjectPercentageOnAttatch` are set on the fixture, the target's solution gains that amount instantly; (b) after `RunSeconds(1.1f)`, the target's solution has gained `transferAmount` (5u) of the test reagent via periodic `TryInject`; (c) raise `EntityUnstuckEvent` — `RunSeconds(1.1f)` again and confirm **no further transfer** (`sticky.StuckTo == null` short-circuits `Update`); (d) with `singleUse: true` and a `trashObject`, confirm the patch is deleted (`entities.Deleted(patch)`) and the trash prototype was spawned. **This test is wound-portable-regression only** (confirms the delivery device works correctly on a `WoundHostComponent` body — D2 sanity) — it intentionally does **not** assert wound healing; that is T-REAGENT's job, once the delivered reagent itself carries a wound-treating entity effect | P4-2 (MedicalPatch ported, server-side) |
| **T-REAGENT-CAP-YES** | `TreatmentCapabilityMatchHealsWoundTest` | `Tests/_WF/Wolfmed/WolfmedReagentTreatmentTest.cs` (new) | `WoundHealingBody`-shaped fixture (reuse the existing WP4 fixture: `TorsoHuman`+`HeadHuman`, `MobBloodstream`, `WoundHost`). Create a real wound: `routing.TryApplyPartDamage(body, head, Spec("Blunt", 20))` → a `BluntWound` at severity ≈17.4 (per phase-1's own derivation pattern: `severityMultiplier` on `BluntWound` — **re-derive from the shipped `wounds.yml` value at implementation time, do not assume 20**). Build `new EntityEffectReagentArgs(head, entities, null, null, FixedPoint2.New(1), null, null, FixedPoint2.New(1))` and call the extended `HealthChange` (or `EvenHealthChange`) effect's `.Effect(args)` directly with a `Damage: { Blunt: -30 }`, `TreatmentCapabilities: [Biological]` instance (matches `OrganicBodyPartProfile.treatmentCapabilities: [Biological]`, `Resources/Prototypes/_Onyx/Wounds/wounds.yml:4`, verified) | after one `.Effect()` call: `damage.GetAllDamage(head).GetTotal()` has dropped (flat damage healed) **and** the wound's `Severity` has also dropped (or the wound is gone if fully healed) — i.e. **wound severity moved, not just the raw damage number**, proving the effect actually reached `WoundSystem`/`_routing.TryApplyPartDamage(..., healWounds: true)` rather than a bare `TryChangeDamage` that only the *projection* would show | P4-1 (`HealthChange`/`EvenHealthChange` gain `TreatmentCapabilities` + wound-routing branch, HOOK 9) |
| **T-REAGENT-CAP-NO** | `TreatmentCapabilityMismatchDoesNotHealTest` | same file | Same fixture and wound as above, but the effect instance declares `TreatmentCapabilities: [Mechanical]` (no overlap with the part profile's `[Biological]`) | **After `.Effect()`, both the raw damage total and the wound's severity are unchanged** (within FixedPoint2 equality). This is the test that actually exercises trap 7 (§1): the implementation under test **must** wrap its `TryChangeDamage`/`_routing.TryApplyPartDamage` call in `_routing.WithTreatmentCapabilities(body, TreatmentCapabilities, () => ...)` (`WoundDamageRoutingSystem.cs:136-147`, already shipped) for this to fail-safe correctly — if the implementer forgets that wrapper, this test is the one that catches it, since `CanTreatPart` defaults to `true` with no wrapper open (trap 7) | P4-1 |
| **T-REAGENT-SUPPRESSPAIN** | `SuppressPainEntityEffectLowersPainTest` | same file | `MobHuman` (real wound host with `PainComponent`); drive pain up via a real hit (`routing.TryApplyPartDamage(body, head, Spec("Blunt", 15))` — matches `WoundHealingTest.cs`'s own measured baseline, `GetRawPain == 13.05`, re-derive if the profile changes). Author `Content.Shared._WF.Wolfmed.EntityEffects.SuppressPain : EntityEffect` per D16 wrapping `PainSystem.SuppressPain(entity, identifier, amount, decayDuration, recoveryMultiplier)` (already public and unchanged since phase 1, `PainSystem.cs:340-359`, verified); call `.Effect(new EntityEffectReagentArgs(body, ...))` with `Amount: 20, DecayDuration: 30s` | `pain.GetPain(body)` (the *effective*, suppression-adjusted value the HUD reads — `GetPain` at `PainSystem.cs:165` is distinct from `GetRawPain` at `:198`) drops relative to the control (no effect applied) by up to the full `Amount`, clamped at zero; `entity.Comp.SuppressionModifiers` contains the identifier (`"PainSuppressant"` default) with a positive `Amount`. A **second** call with the same identifier **accumulates** rather than resetting (`SuppressPain`'s own merge logic, `PainSystem.cs:346-350`, reads `SuppressionModifiers.TryGetValue` and adds) — assert the modifier's stored `Amount` increased | P4-1 (`SuppressPain` entity effect authored per D16; `PainSystem` itself needs no change) |
| **T-SURG-BLEED** | `SurgeryStopBleedingClampsTheBleedingWoundTest` | `Tests/_WF/Wolfmed/WolfmedWoundSurgeryTest.cs` (new) | Bespoke bare-entity fixture (Onyx pattern, §2.1/§3): `WolfmedSurgeryTestPart` (`BodyPart Arm` + `Woundable`); create a wound with active bleed (`wounds.CreateOrMergeWound(part, "SlashWound", 15)` — `SlashWound` bleeds per phase-1's `WoundBleedingTest`). Spawn a bare `SurgeryClampBleedingEffectComponent{ Amount: 10 }`-carrying entity; raise `SurgeryStepEvent(user: Invalid, body: Invalid, part, tools: [], surgery: Invalid)` on it directly | `WoundBleedingComponent.CurrentRate` (or the wound's own bleed contribution — assert via `WoundBleedingSystem.GetPartRate(part)`) is lower after the first raise; raising the equivalent `SurgeryStepCompleteCheckEvent` on the same entity is `Cancelled == true` while the wound still bleeds, and `Cancelled == false` once enough applications have zeroed it (repeat the step raise until `GetPartRate(part) == 0`, bounded by the wound's `Severity`/`Amount`) | P4-3 (`SurgeryClampBleedingEffectComponent` vendored — table in §4) |
| **T-SURG-TEND-BRUTE-DEEP** | `SurgeryTendWoundsDeepHealsWoundSeverityOnWoundHostTest` | same file | `WolfmedSurgeryTestPart` on a `WoundHostBody` (needs `WoundHost` for the branch to engage — see §4's extension design); create a `BluntWound` via `routing.TryApplyPartDamage`. Raise `SurgeryStepEvent` on the **existing, extended** `SurgeryTendWoundsEffectComponent`-carrying step (reuse `SurgeryStepRepairBruteTissue`'s real prototype id via `GetSingleton`, §3) | On a wound host, the wound's `Severity` decreases (not just `DamageableComponent`'s total) — this is the "deep" contract; **repeat the identical setup and step-raise on a body WITHOUT `WoundHost`** and confirm the wound-severity branch does **not** engage (D2) — the non-host control must still work exactly as it does today (flat damage heals, no `WoundSystem` call, since non-hosts have no `WoundableComponent`/wound entities to begin with — assert `HasComponent<WoundableComponent>(part)` is false for the control and that the existing `SurgeryStepDamageEvent`/flat-heal path still reduces `DamageableComponent.TotalDamage`) | P4-3 (extend Wolfgate's `SurgeryTendWoundsEffectComponent` handlers, §4) |
| **T-SURG-TEND-BURN-DEEP** | `SurgeryTendWoundsBurnDeepHealsWoundSeverityTest` | same file | Identical shape to T-SURG-TEND-BRUTE-DEEP but with `SurgeryStepRepairBurnTissue` (`mainGroup: Burn`) and a `BurnWound` from `Heat` damage | Same contract as brute, on the Burn group — kept as a **separate** test rather than a `[TestCase]` parameterisation of the brute one because `BurnWound`'s bleed/severity numbers and `HealMultiplier` context (`OnTendWoundsStep`'s `damageable.DamagePerGroup["Burn"]` bonus) are derived from different shipped values that must each be checked, not assumed symmetric with Brute | P4-3 |
| **T-SURG-INTERNAL** | `SurgeryStopInternalBleedingRemovesTheWoundTest` | same file | Torso part with an `InternalBleedingWound` (create it the same way T-ORG-DESTROY does today: `entities.System<OrganHealthSystem>().SetHealth(organ, 0)` on a torso organ, then `RunTicksSync`, OR directly `wounds.CreateOrMergeWound(torso, "InternalBleedingWound", severity)` if that prototype is spawnable standalone — check `WoundInternalBleedingComponent`'s `ComponentInit` wiring first). Spawn a bare `SurgeryTreatWoundEffectComponent{ InternalBleeding: true }` entity and raise `SurgeryStepEvent` on it | Before: `WoundInternalBleedingComponent.Severity > 0` and `WoundSystem.GetWounds(torso)` contains the `InternalBleedingWound`. After **one** raise (Onyx's `Amount` defaults to `FixedPoint2.MaxValue`, i.e. always fully treats in one application — confirm this default is kept, or note the deviation if the phase-4 WP tunes it lower): the wound entity is **gone** (`RemoveWound` fired via `TreatWound`'s severity-to-zero path, §4) and `WoundInternalBleedingSystem`'s per-tick systemic bleed contribution from that wound stops (advance one tick, assert no further systemic `Bloodloss` accrual attributable to it) | P4-3 (`SurgeryTreatWoundEffectComponent` vendored) |
| **T-SURG-FRACTURE** | `SurgeryMendFractureClearsTheFractureTest` | same file | Arm part on a body with `Hands` (reuse `WoundFractureTest`'s extended fixture, `WoundFractureBody`, already carries a hand — P2-D21); `routing.TryApplyPartDamage(body, arm, Spec("Blunt", 75))` with `creationChance: 1` guaranteed only at Comminuted grade (P2-D23 — **use the same 75-Blunt hit `WoundFractureTest` already uses to reliably reach Comminuted**, do not invent a new damage value). Spawn a bare `SurgeryMendFractureEffectComponent` entity, raise `SurgeryStepEvent` on it | `WoundFractureSystem.GetFracture(arm)` is non-null and `Severable`/`Grade == Comminuted` before; **`TryMend` sets `FractureTreatment.Mended`, and if `removeWoundWhenMended: true` (the shipped profile flag — confirmed in P2-D16's derivation of T-FRACT-EFFECTS) the fracture wound itself is removed**, so `GetFracture(arm)` is null after one raise and the movement/manipulation multiplier (`FractureEffectSystem.GetDurationMultiplier`) returns to `1f` (mirrors T-FRACT-EFFECTS' own post-mend assertion, phase 2). A second raise is a no-op (`GetFracture` already null, matching Onyx's own `FractureMendAndStaleNoOpTest` idempotence check, §2.1) — assert no exception and the state is unchanged | P4-3 (`SurgeryMendFractureEffectComponent` vendored; BoneGel/BoneSetter are **tool** requirements on the step prototype, not part of the effect logic under test — the test bypasses the tool-check layer per §3, so BoneGel possession is not asserted here, only the effect) |
| **T-SURG-AMPCONSEQUENCE** | `SurgeryHealAmputationConsequenceRemovesTheWoundTest` | same file | Reuse `WolfmedAmputationTest`'s `WolfmedAmputationBody` fixture pattern: amputate an arm (`routing.TryApplyPartDamage` past the Slash threshold + finishing hit, exact numbers from `WolfmedAmputationTest.cs` §already-shipped, e.g. Slash 130 then Slash 15) so the torso gains an `AmputationConsequenceWound`. Spawn a bare `SurgeryTreatWoundEffectComponent{ WoundPrototype: AmputationConsequenceWound }` entity, raise `SurgeryStepEvent` targeting the **torso** (the consequence wound lives on the parent stump, not the severed part — confirmed by every phase-3 amputation test) | Before: `wounds.GetWounds(torso)` contains exactly one `AmputationConsequenceWound`. After one raise: **zero** — `TreatWound(wound, MaxValue)` removes it (§4's derivation). This is also the **setup precondition** T-REATTACH-BLOCKED's "after treatment" half reuses | P4-3 |
| **T-SURG-ORGAN** | `SurgeryHealOrganRestoresHealthTest` | same file | Reuse `WolfmedOrganTest`'s `WolfmedOrganTestBody`/`WolfmedOrganTestOrgan` fixture; damage the organ via `OrganHealthSystem.SetHealth(organ, someValueBelowMax)` directly (deterministic, avoids the ~1-4% per-hit organ roll — same rationale `WolfmedOrganTest.cs` already documents for every one of its tests). Per DECISIONS.md P4-3, the healing surgery calls `OrganHealthSystem.ChangeHealth(organ, amount)`, already `public` (`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`, confirmed: `public void ChangeHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 amount) => SetHealth(organ, organ.Comp.Health + amount)`) — spawn a bare effect entity for whatever component the WP wires to call it (recommend `SurgeryHealOrganEffectComponent{ Amount }`, new, no collision — verified) and raise `SurgeryStepEvent` targeting the organ's containing part | `WolfmedOrganComponent.Health` increases by exactly `Amount`, clamped at `MaxHealth` (`SetHealth`'s own `FixedPoint2.Clamp`, `OrganHealthSystem.cs`, verified); if healing crosses the organ back above 0 from exactly 0 (a previously non-functional-but-not-yet-destroyed organ, the one-tick window T-ORG-FUNC also exercises), `OrganFunctionChangedEvent(true)` fires and — via `WolfmedOrganConsequenceSystem` (already shipped, phase 3) — the organ's `OnAdd` grants are restored (reuse T-ORG-FUNC's `MutedComponent` fixture organ to make this observable: heal it back above zero and assert `MutedComponent` is gone) | P4-3 (this is the "organ-healing gap" DECISIONS.md/STATUS.md flag as currently missing — new step-effect component, name TBD by the WP) |
| **T-REATTACH-BLOCKED** | `AmputatedLimbCannotBeReattachedUntilConsequenceTreatedTest` | `Tests/_WF/Wolfmed/WolfmedReattachTest.cs` (new method, alongside the existing `ReattachedPartRejoinsWoundTrackingTest`) | Amputate an arm on a `WolfmedAmputationBody`-shaped fixture exactly as `WolfmedAmputationTest` does (creates `AmputationConsequenceWound` on the torso, phase 3, already shipped and unconditional). **Attempt reattachment**: `graph.AttachPart(torso, "left arm", severedArm)` (or `graph.CanAttachPart(torso, slot, severedArm)`, whichever the P4-3 hook gates) | **Before the P4-3 hook, this must FAIL** where phase 3 let it silently succeed (P3-D2's documented gap — `AmputationConsequenceWound` ships inert). This is the regression test that *proves the gap is closed*: assert `CanAttachPart`/`AttachPart` returns `false` while the consequence wound is present. **Then** raise the T-SURG-AMPCONSEQUENCE step on the torso to remove the wound (§4's `TreatWound` mechanism), and assert the **same** `AttachPart` call now **succeeds**, and the reattached arm passes the existing `ReattachedPartRejoinsWoundTrackingTest` contract (rejoins `WoundableComponent` tracking, takes fresh damage, bleeds) | P4-3 (the `CanAttachPart` hook on `SharedBodySystem.Parts.cs:606` that lifts P3-D2 — **this is a new upstream hook, not yet authorised in any DECISIONS.md section**; §8 escalates it explicitly since it touches a core upstream file outside `_Onyx`/`_WF`) |
| **T-ANALYZER** | `HealthAnalyzerWoundDiagnosticsIncludesWoundedPartTest` | `Tests/_Onyx/Medical/HealthAnalyzerPartDamageTest.cs` (new file, Wolfgate-authored — Onyx's own file is not portable per §2.4, but the placement mirrors Onyx's path since the *behaviour under test* is a wound-system extension of an upstream system, matching D6's spirit) | Fixture per §2.4's Shitmed adaptation: a `WoundHost` body (reuse `WoundBleedingBody`'s shape) plus a **plain**, non-wound-host control body (`Damageable` only, no `Body`). Create a real bleeding `SlashWound` on the head and a fracture on an arm (reuse `WoundFractureBody`'s extended fixture). Call the analyzer's builder method directly — **this assumes P4-4 adds a `BuildWoundDiagnostics(EntityUid body)`-shaped public method to `HealthAnalyzerSystem`, mirroring Onyx's own factoring (§2.4's finding and recommendation)** | `analyzer.BuildWoundDiagnostics(woundHost)` is non-null and its entry for the wounded part carries a positive bleeding rate and (for the fractured arm) the correct `FractureGrade`/`FractureTreatment`; `analyzer.BuildWoundDiagnostics(plainBody)` is **null** (D2: no diagnostic surface for non-wound-hosts); `TargetBodyPart.Groin`'s entry (if present) equals `TargetBodyPart.Torso`'s (D9 fold, not `.Chest`, which does not exist in WG) | P4-4 (health analyzer wound/organ/pain diagnostics — **the UI-shape decision in DECISIONS.md P4-4 does not block this test**, since it targets the builder method, not the BUI window; only the "does a builder method exist to test" choice matters, which is my recommendation in §8) |
| **T-SURGERY-PROTOTYPE-SANITY** | `EveryWoundSurgeryStepReferencesARealSurgeryStepTest` | `Tests/_Onyx/Medical/SurgeryStepSequencePrototypeTest.cs` (new file, Wolfgate-shaped substitute per §2.5) | No mob spawn — prototype-only, server-side. `foreach (var surgery in _prototypes.EnumeratePrototypes<EntityPrototype>()) if (surgery.TryComp<SurgeryComponent>(...))` for the six new phase-4 surgery ids (`SurgeryStopBleeding`, `SurgeryStopInternalBleeding`, `SurgeryMendFracture`, `SurgeryHealAmputationConsequence`, and whatever the per-organ heal surgeries are named) | `Steps` is non-empty for each; every step id in it resolves via `_prototypes.Index<EntityPrototype>(step)` and carries `Content.Shared._Shitmed.Medical.Surgery.Steps.SurgeryStepComponent`; each surgery has at least one condition component from §4's table (a `SurgeryHasWoundConditionComponent`, `SurgeryFractureGradeConditionComponent`, or the extended `SurgeryWoundedConditionComponent`) so it cannot be started on an unwounded part. **Availability of `[SidedDependency(Side.Server)]`/`[RunOnSide(Side.Server)]` must be re-checked before use (§2.5) — if absent, fall back to the existing pattern every other prototype-only test in this repo uses** (a plain `[Test]` resolving `IPrototypeManager`/`IComponentFactory` via `server.ResolveDependency<T>()` inside `WaitAssertion`, exactly as every test in §5 above already does) | P4-3 (needs the six surgery prototypes to exist) |

### 5.1 Notes on scope deliberately excluded from this table

- **No sprite/UI/BUI-message-over-the-wire test** is specified anywhere (T-ANALYZER tests the builder method,
  not the network message) — consistent with `WolfmedVisualsTest`'s own precedent (phase 3) of asserting on
  server-side component state rather than attempting a client-rendered assertion, and with the project's
  "prefer logic tests" standing guidance.
- **No cargo/loadout/medkit-fill test** (P4-2's "minimal marked edits to existing medkit fills") — that is
  prototype-content placement, not logic, and has no behaviour to assert beyond "the fill list contains the
  id", which the existing `PrototypeSaveTest`/`EntityTest` smoke tests already cover incidentally (per PLAN.md
  §6.3's run commands, which include those filters).
- **No `SurgeryOpenIncision`/`SurgeryCloseIncision` regression test** — P4-3 reuses them unmodified; the
  existing Shitmed surgery content (whatever exercises them today, even if untested) is out of this port's
  blast radius by construction (no `// WOLFGATE` edit is authorised on either prototype).
- **Reagent id collisions (`Stasizium`, `SalicylicAcid`)** are a P4-1 content decision, not a test-plan item;
  no test in this table depends on which reagent id ships, since T-REAGENT-CAP-YES/NO exercise the **entity
  effect class**, not a named reagent prototype.

---

## 6. Dependencies and sequencing

```
P4-1 (reagents/HOOK 9)  ──► T-REAGENT-CAP-YES, T-REAGENT-CAP-NO, T-REAGENT-SUPPRESSPAIN
P4-2 (tourniquet+patch) ──► T-TOURNIQUET, T-PATCH
P4-3 (surgeries)        ──► T-SURG-BLEED, T-SURG-TEND-BRUTE-DEEP, T-SURG-TEND-BURN-DEEP,
                            T-SURG-INTERNAL, T-SURG-FRACTURE, T-SURG-AMPCONSEQUENCE, T-SURG-ORGAN,
                            T-SURGERY-PROTOTYPE-SANITY
P4-3 + new CanAttachPart hook (§8) ──► T-REATTACH-BLOCKED
P4-4 (analyzer + BuildWoundDiagnostics recommendation) ──► T-ANALYZER
```

None of the eleven new tests depend on each other's fixtures except T-REATTACH-BLOCKED, which reuses
T-SURG-AMPCONSEQUENCE's mechanism (not its fixture) for its "treat, then retry" half — write
T-SURG-AMPCONSEQUENCE first so the exact `TreatWound` call shape is proven before T-REATTACH-BLOCKED depends
on it. Otherwise all eleven can be written in parallel once their respective P4-N package lands, following
the same "packages run sequentially in one worktree, tests append after their package" rule PLAN2/PLAN3
established.

---

## 7. Run commands

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
dotnet run --project Content.YAMLLinter -c Release
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build --filter "FullyQualifiedName~DockTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
  --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~_Onyx.Medical|FullyQualifiedName~Wolfmed" \
  --logger "console;verbosity=detailed"
```

Run each new test file individually during development (`--filter "FullyQualifiedName~WolfmedWoundSurgeryTest"`
etc.); the combined filter above is the final phase-4 gate, matching phases 1-3's own `WOLFMED_STATUS.md`
convention of reporting one pass/fail count for the whole `_Onyx.Wounds|_Onyx.Body|Wolfmed` filter (extended
here with `_Onyx.Medical` for the new analyzer/surgery-prototype tests).

---

## 8. Decisions for the user (with recommendation)

1. **Health analyzer needs a directly-callable builder method, mirroring Onyx's own `BuildWoundDiagnostics`/
   `BuildPartDamage` factoring, independent of the UI-shape decision DECISIONS.md P4-4 already poses.**
   Without it, T-ANALYZER has no headless assertion point (WG's `UpdateScannedUser` ends in a network send,
   verified). **Recommendation: yes, add it** — it costs nothing extra (P4-4 has to compute this data to put
   it in the message regardless of whether the UI grafts onto Shitmed's window or ships a parallel tab), and
   it is exactly the pattern Onyx itself validated as testable. This does not decide P4-4's UI question; it
   only decides that the *data assembly* is a separate, testable unit from the *message send*.
2. **A new upstream hook on `Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:606 CanAttachPart`** is
   required to close P3-D2 and make T-REATTACH-BLOCKED meaningful. This is **not yet authorised** in
   DECISIONS.md's phase-4 section (P4-3 only says "make `AmputationConsequenceWound` block `CanAttachPart`
   until treated" as prose, without the file/line/authorisation ceremony every other upstream edit in this
   port has received). **Recommendation: authorise a one-line guard** at `CanAttachPart`'s entry —
   `if (HasWound(partId, "AmputationConsequenceWound")) return false;` gated behind a small `_WF` helper
   (e.g. `WolfmedBodySystem.HasAmputationConsequence(EntityUid part)` wrapping `WoundSystem.GetWounds`) —
   consistent with D5's "one- or two-line hook, body in `_WF`" pattern used everywhere else. The alternative
   (skip the hook, ship T-REATTACH-BLOCKED as a documented-red/skipped test) simply extends P3-D2's gap
   through phase 4, which DECISIONS.md's own P4-3 line already says should not happen.
3. **`SurgeryStepSequencePrototypeTest`'s two Onyx-only fixture attributes
   (`[SidedDependency(Side.Server)]`, `[RunOnSide(Side.Server)]`) were not confirmed present in WG's
   `Content.IntegrationTests/Fixtures/Attributes`** in this pass (I searched but found no definition file for
   either under that path with the tools available to me in this session — this should be re-verified by
   whoever implements T-SURGERY-PROTOTYPE-SANITY, since if they are absent the test needs the plain
   `server.ResolveDependency<T>()` pattern instead, which every other prototype-only test in this repo
   already uses safely). **Recommendation: do not block on this** — the fallback pattern is zero-risk and
   already proven; treat the attributes as a nice-to-have, not a dependency.
4. **`WoundHealMultiplier`/`removeWoundWhenMended` and other "which shipped constant does the mend/heal land
   on" questions in T-SURG-FRACTURE, T-SURG-TEND-*-DEEP** are **not this report's to answer** — every number
   in §5 is explicitly marked "re-derive from the shipped value," continuing the phase 1-3 discipline that
   caught four stale Onyx literals already. **Recommendation: the implementing WP computes and records each
   number in a `// WOLFGATE` comment at the assertion, exactly as WP9/WP10-6b/WP11-5 did.**

---

## 9. Blockers

**None that block writing this plan.** Every API this report relies on (`WoundSystem.TreatWound`,
`WoundFractureSystem.TryMend`/`GetFracture`, `WoundBleedingSystem.SetTreatment`/`ReduceBleeding`,
`OrganHealthSystem.ChangeHealth`/`SetHealth`, `PainSystem.SuppressPain`/`GetPain`,
`SharedSurgerySystem.GetSingleton`, `WoundDamageRoutingSystem.WithTreatmentCapabilities`,
`SharedBodySystem.CanAttachPart`) already exists in the tree today, verified by direct read — none is
speculative. **Every test in §5 is currently un-runnable** because the P4-1 through P4-4 production code it
targets has not been written yet; this is expected for a test-planning-ahead-of-implementation deliverable,
not a blocker in the sense the phase 1-3 reports used the word. The one item that needs a decision **before**
a test can be written at all (not just before it can pass) is item 2 in §8 — the `CanAttachPart` hook — since
without it T-REATTACH-BLOCKED has no seam to call.
