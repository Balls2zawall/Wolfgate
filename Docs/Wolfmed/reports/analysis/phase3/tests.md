# Phase 3 — test plan (analyst report: "tests")

Read-only pass. Every claim below is verified against the real files (`git show HEAD:<path>` for ONYX,
direct `Read`/`grep` for WG) — no claim is carried over from PLAN.md/DECISIONS.md without re-checking it
against source, per DECISIONS.md's "every Onyx literal is suspect" instruction. ONYX = `C:/tmp/onyx` @
`2f5bab9946539cbe083010c9ae6fbc59b47ae377` (sparse). WG = `.../worktrees/rules-motd-updates-11c89c` @
`1171e02fb6` (phase 2 committed).

Two sibling analyst reports already exist in `C:/tmp/wolfmed-plan/p3/`: `armour.md` (P3-3, per-part armour)
and `visuals.md` (P3-4, limb damage sprites). Both are deep, independently-verified, and this report defers
to them for implementation detail, incorporating their test tables directly (with attribution) rather than
re-deriving. This report's own original contribution is P3-1 (amputation) and P3-2 (organ consequences),
which have no sibling report yet, plus the unified cross-cutting test table, run commands, and the P3-6
balance numbers.

---

## 0. Executive summary — read this first

1. **Two of the four "locational-armour tests" and the literal "overflow" amputation mechanism are
   dead code in Onyx itself, not Wolfgate porting artifacts.** `ArmorComponent.Coverage`/`CoverageSymmetry`
   have zero C# consumers anywhere in Onyx (confirmed independently by `armour.md` and by this report).
   Separately and independently, **`WolfmedBodyPartComponent.MaxDamage`/`AccumulateAmputationOverflow`/
   `PartDamageOverflowedEvent`" — the mechanism literally named "overflow" — is functionally dead for every
   human limb**: in Onyx's own `Resources/Prototypes/Body/base_organs.yml`, `maxDamage` is never set on
   Head/Arm/Hand/Leg/Foot (only Chest/Groin get it, at 250/250), and `AmputationSystem` explicitly excludes
   `BodyPartType.Chest` from amputation. WG's `_WF/Wolfmed/Body/parts.yml` faithfully reproduces this (only
   `WolfmedBaseTorso` has `MaxDamage`, and Torso is excluded from severing). **All amputation that can ever
   happen to a human in Onyx — and therefore in Wolfgate — goes through the *threshold*-based path
   (`WolfmedBodyPartComponent.AmputationThresholds`), not the overflow path.** Onyx's own canonical test,
   `WoundBleedingTest.TraumaticAmputationCreatesSevereStumpBleedingTest`, independently confirms this: its
   two-hit (200 then 15 Slash) construction only makes sense against the threshold path.
   The test plan below is built around this finding, not around the task brief's "overflow" framing —
   see Finding F1.

2. **`AmputationSystem.cs` cannot be vendored with only the two D26 dependency lines re-enabled.** It reads
   `bodyPart.MaxDamage`, `.AmputationThresholds`, `.DismembermentFinishingDamage`,
   `.AmputationConsequenceSeverity`, `.DismembermentSeverity` and `.Parent` as members of the **same**
   `BodyPartComponent` it resolves — none of which exist on Wolfgate's `Content.Shared.Body.Part.BodyPartComponent`
   (D8 moved them to `WolfmedBodyPartComponent`, and `.Parent` doesn't exist on Wolfgate's `BodyPartComponent`
   at all). It also calls `_body.TryDetachPart(part)` — a method that exists on Onyx's own `SharedBodySystem`
   but not on Wolfgate's. **All of this already has a precedent, already merged**: `WoundDamageRoutingSystem.cs`
   (shipped in phase 1) already redirects the identical `MaxDamage`/`AmputationThresholds` reads through
   `_wfPart.Get(part).X` (`WoundDamageRoutingSystem.cs:799,804,954`). `AmputationSystem.cs` needs the same
   treatment applied to itself — see Finding F2 for the exact line-by-line list. This is an implementation
   note for the phase-3 WP, but it directly gates every T-AMP-* test: none of them can run until these edits
   land, so they belong in the same work package as the port, not a later "tests" package.

3. **`OrganEffectSystem.cs` (the file DECISIONS.md's P3-2 names) is NOT wound-driven — it is surgery/
   anatomy-driven and pulls in exactly the machinery D7 excludes.** Read in full: it depends on
   `BodyAnatomyComponent`, `NeuroInterfaceRuntimeComponent`/`NeuroInterfaceEnabledChangedEvent` (Onyx's own
   `_Onyx.Surgery.Augments.NeuroInterface`), `_Onyx.Medical.Surgery`, and `TongueComponent.VocalCordsCut`
   (Onyx's own surgery). None of this exists in Wolfgate and none of it is a wound consequence — it is
   "what components does a body get from having a working brain/eyes/ears/tongue/heart", driven by organ
   *insertion/removal* (surgery), not organ *damage*. **Recommendation: skip `OrganEffectSystem.cs` and
   `FunctionalOrganComponent` entirely for phase 3** (P3-2's own hedge — "only pieces with a consumer at the
   pin" — is not met). The wound-driven organ-consequence chain is `OrganDamageSystem` (routes part damage
   into organ health, already vendor-ready) → `OrganHealthSystem` (health depletion → `DestroyOrgan` → merges
   a `DestructionWound` onto the parent part). That pair is what T-ORGAN targets. See Finding F4.

4. **Organ health/destruction-wound fields are on an *upstream* file Onyx patches directly, not on an
   `_Onyx`-namespaced one** — `Content.Shared/Body/OrganComponent.cs` (Onyx's fork of the vanilla organ
   component, bare `Content.Shared.Body` namespace per the already-recorded §2.17 pattern), carrying
   `Health`, `MaxHealth`, `DestructionWound`, `DestructionWoundSeverity` inside `<Onyx-OrganHealth>`/
   `<Onyx-OrganDamage>` tags. This needs a **new upstream hook** on Wolfgate's own
   `Content.Shared/Body/Organ/OrganComponent.cs` (four additive `// WOLFGATE` DataFields), the same shape as
   HOOK 7's `HealingComponent` extension — not yet authorised anywhere in DECISIONS.md/PLAN.md/PLAN2.md. See
   Finding F4.

5. **T-AMP-EXPLOSION's "cvar pin" is not really a cvar effect.** `CCVars.ExplosionLimbDamageVariation`/
   `ExplosionWoundMultiplier` are already ported (WP3) but have **zero live consumer** in the routing seam —
   the only place that reads them is `Content.Server/Explosion/EntitySystems/ExplosionSystem.CVars.cs`,
   which feeds fields that only matter if `ExplosionSystem`'s own damage-application call site is hooked to
   call `WoundDamageRoutingSystem.TryApplyDistributedDamage(..., isExplosion: true)` — and D24 explicitly
   declines to hook `ExplosionSystem` in phase 1-3 scope. **Determinism for T-AMP-EXPLOSION comes from
   calling the already-public `TryApplyDistributedDamage`/`TryRouteDistributedDamage` API directly** with
   `variation: 0f` passed as a parameter (not set via cvar) and a single-part target mask, not from any cvar
   write. See Finding F5.

6. **Reattachment already has a partial precedent that stops short of what T-REATTACH needs.**
   `WoundScarTest.ThresholdTreatmentAttachmentAndRejuvenateTest` (shipped, phase 1) already does
   `wfBody.TryDetachPart(part)` → `graph.AttachPart(torso, "head", part)` and asserts the wound survives —
   but it never asserts `WoundableComponent`/`DamageableComponent` presence or that bleeding resumes after
   reattachment, which is exactly PLAN.md §8.3 trap 2's concern (`WoundDamageProjectionSystem.OnPartInserted`
   has no caller except `WolfmedBodyPartLifecycleSystem`, and that path has never been exercised by a test
   that actually re-damages the reattached part). T-REATTACH closes this gap. See Finding F6.

7. **The numbers the user must see (P3-6):** amputation thresholds, finishing damage, and consequence
   severities are already fully populated in the shipped `_WF/Wolfmed/Body/parts.yml`/
   `WoundHostComponent` defaults — verified byte-identical to Onyx's own `base_organs.yml` for Head
   (200/200/350). Full table in §5.

---

## 1. Existing test suite audit

**Fixture:** `Content.IntegrationTests/Fixtures/GameTest.cs` + `.Pair.cs`/`.Entities.cs`/`.CVars.cs`/
`.CommonPoolSettings.cs` + `Fixtures/Attributes/[TestPrototypes]`. Unchanged since phase 1; still the
harness every phase-3 test uses. `RunSeconds(float)` (`GameTest.Pair.cs:32`) advances simulated time on both
sides of the pair.

**`[TestPrototypes]` id pool is global across the whole suite** (PLAN2 §6.1 trap 4). Taken today (verified
by listing the five `_Onyx/Wounds/*.cs` + two `_WF/Wolfmed/*.cs` files): `WoundFoundationBody*`,
`WoundBleedingBody*`, `WoundHealingBody*`, `WoundScarBody*`, `WoundFractureBody*`/`WoundFractureArmor`/
`WoundFractureHandsBody*`, `WolfmedBridgeBody*`/`WolfmedBridgeArmor`, `WolfmedPainShockBody`,
`WolfmedHighPainThresholdBody`. New phase-3 ids below are chosen clear of these — re-check if another
phase-3 package lands prototypes first (armour.md's own additions: `WoundFoundationArmorHead`,
`WoundFoundationArmorLeftArm`, `WoundFoundationArmorLocational`, `WoundFoundationArmorAllHead`).

**Standing traps that apply to every new test** (carried forward from PLAN2 §8.3, still binding):

1. **`TerminatingOrDeleted`.** Any new code reacting to `BodyPartAdded/Removed` or `OrganGot*` must guard
   `TerminatingOrDeleted(uid)` before `EnsureComp<T>` — a single unguarded spawn/dispose during test teardown
   poisons the shared pooled pair for every other test in the run (cost WP9 13 unrelated failures once).
   `WolfmedBodyPartLifecycleSystem.cs:29,50` already guards this; any *new* phase-3 subscriber to those
   events (there should be none needed for P3-1/P3-2 — see §4) must repeat it if one is added.
2. **`DockTest` first.** `db.ef` sqlite warnings fail every pair test in this repo (project memory) —
   run it before attributing a phase-3 test failure to Wolfmed.
3. **`Assert.Multiple` bodies must be fully synchronous** — no `async`/`await` inside the lambda.
4. **YAML lints in Release only** (`ErrorNode` crashes the linter elsewhere).
5. **Every Onyx literal is a prediction until measured against the *shipped* prototype**, not Onyx's. This
   report derives every expected value from WG's actual `_WF/Wolfmed/Body/parts.yml` and
   `WoundDamageComponents.cs` defaults, cross-checked against Onyx's `base_organs.yml`/`chest_groin.yml`
   sources, exactly the WP9 §3.2 / P2-D16 / P2-D23 pattern. Where a value could not be independently
   confirmed against a real prototype, it is flagged "derive at implementation time," not asserted as fact.
6. **Fracture creation / explosion amputation are dice rolls** (P2-D23's lesson generalises). Any new test
   that needs a fracture or an explosion-amputation to happen must drive the roll to `chance = 1`
   (`creationChance: 1` for fractures; `GetThresholdProgress(...) * 0.5f` clamped to `1.0` for explosion
   amputation, i.e. total damage ≥ 2× the relevant threshold) rather than relying on `_random.Prob` landing
   the right way most of the time.
7. **`[TestPrototypes]` bodies for pain-adjacent fixtures need `- type: StatusEffects` with an explicit
   `allowed: [...]` and `- type: MobState`** (P2-D24) if they touch pain shock / stun. Not needed for the
   pure damage/amputation/organ/armour tests below, but relevant if a phase-3 test also exercises pain
   shock alongside amputation (none currently do; noted for completeness).

---

## 2. Onyx tests phase 3 unlocks

| Onyx test / file | What it asserts | APIs it needs | Fixture needs | Portability |
|---|---|---|---|---|
| `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` (5 tests, read in full) | See breakdown below §2.1 | `AmputationSystem.TryAmputate`/`ApplyAmputationConsequences`, `WoundSystem.CreateOrMergeWound`, `RejuvenateEvent` clearing consequence wounds | Own bespoke `AmputationConsequenceTestBody`/`Torso`/`Head`/`Heal` prototypes (Nubody `InitialBody`+`Injurable`; needs the standard D9/D19 fixture translation to Shitmed `body`+`Damageable`) | **Yes, adapted.** Straightforward once `AmputationSystem` compiles (Finding F2). Chest→Torso fold (D9), drop `Injurable` (D19), `graph.TryDetachPart`→`WolfmedBodySystem.TryDetachPart` (§2.7 precedent, already used by 3 other ported tests). |
| `WoundBleedingTest.TraumaticAmputationCreatesSevereStumpBleedingTest` (`:173-199` in Onyx, dropped from WG's phase-1 port) | 200 Slash to head → `Severable=true`, still attached; +15 Slash → head detaches; torso gets a `DismembermentWound`; body `BloodstreamComponent.BleedAmount >= 40` | `WoundDamageRoutingSystem.TryApplyPartDamage`, `WoundSystem.GetWounds`, `BloodstreamComponent` | Uses the **already-shipped** `WoundBleedingBody` fixture (WG's `_Onyx/Wounds/WoundBleedingTest.cs`, ported phase 1) — no new prototype needed | **Yes, near-verbatim.** This is Onyx's own canonical amputation test and (per Finding F1) exercises the live threshold path, not the dead overflow path — portable once `AmputationSystem` lands. Values (200 Slash / +15 Slash on Head) match WG's shipped `parts.yml` thresholds (Head Slash=200) and host default finishing damage (Slash=15) exactly — see §5. |
| Three "locational-armour" tests in `WoundDamageFoundationTest.cs`: `AppliesLocationalArmorExactlyOnceTest` (`:480-506`), `EmptyCoverageAndSymmetryTest` (`:508-543`), `LocationalModifierOverridesAndFallbackTest` (`:545-580`) | Coverage/coverageSymmetry/partModifiers gate which parts an armour's modifiers apply to | `ArmorComponent.Coverage/CoverageSymmetry/PartModifiers` (**do not exist in WG at all** — `armour.md` §7 confirms MISSING), `WolfmedPartArmorSystem.OnPartDamageModify` | Extends the existing `WoundFoundationBody`/`WoundFoundationBodyGraph` fixture already in WG's `WoundDamageFoundationTest.cs`; three new armour `[TestPrototypes]` entries (`armour.md` §6.1 gives exact YAML) | **Two of three are RED against Onyx's own shipped code** — see Finding F3 / `armour.md` §3.5. `LocationalModifierOverridesAndFallbackTest` alone is portable unconditionally. |
| `Content.IntegrationTests/Tests/_Onyx/Body/BodyConsequencesTest.cs` — parts WP9 dropped: `SocksSlotDependsOnLegsNotFeetTest` (inventory-slot coupling to body parts), `StandUpAttemptEvent` assertion in `DetachingOneLegPreventsStandingTest`, and the whole Groin-specific framing | Shoes/socks/underwear inventory slots reacting to `BodyPartRemovedEvent`; a cancellable `StandUpAttemptEvent` | `InventorySystem.HasSlot` reacting to part removal (no Wolfgate system subscribes `BodyPartRemovedEvent`/`BodyPartDroppedEvent` in `Content.{Shared,Server}/Inventory` — confirmed absent by grep, this is a **real missing feature**, not a naming difference); `StandUpAttemptEvent` (**does not exist in Wolfgate** — confirmed absent, `grep -rn "StandUpAttemptEvent" WG` → 0 hits) | N/A — no such mechanism exists to test | **No — correctly already excluded**, and phase 3 does not reopen this. WG's ported `BodyConsequencesTest.cs` already documents why in its class-level `<remarks>`. Nothing in P3-1/P3-2/P3-3/P3-4 adds an inventory-slot↔bodypart coupling or a `StandUpAttemptEvent`, so there is no new phase-3 reason to revisit this gap. Recorded here only because the task asked to check it explicitly. |
| Organ-damage test | **Does not exist as a standalone file.** `git -C ONYX grep -iln "organ" -- Content.IntegrationTests` and a direct `OrganDamage` grep both return no dedicated organ-damage test file anywhere in the Onyx test tree (the only "organ" hits are body/gib/respirator/lung tests unrelated to Onyx's wound-organ-damage feature, plus `HealthAnalyzerPartDamageTest`/`SurgeryStepSequencePrototypeTest`/`TransplantCompatibilityPrototypeTest`, none of which exercise `OrganDamageSystem`/`OrganHealthSystem`). **Onyx itself ships `OrganDamageSystem`/`OrganHealthSystem`/`OrganDamageComponent` with zero test coverage.** | — | — | **No test to port — T-ORGAN (§4) is wholly new coverage**, not a port. This should be flagged to the user: it means the organ-consequence path is being tested for the first time anywhere in either codebase. |

### 2.1 `AmputationConsequenceTest.cs` breakdown (read in full, ONYX)

Five tests, all against a bespoke two-part body (`AmputationConsequenceTestBody`: torso + head, torso and
head both carry `WolfmedBridgeArmor`-style profiles with `amputationThresholds`/`dismembermentFinishingDamage`
set directly in the test's own `[TestPrototypes]` block — not the production human body):

1. Amputating the head merges `AmputationConsequenceWound` onto the torso at the configured severity.
2. Amputating twice (a spare head swapped in) **merges** severity rather than duplicating the wound entity
   (`WoundSystem.CreateOrMergeWound` semantics).
3. A treatment (`AmputationConsequenceTestHeal`) reduces the consequence wound's severity without removing
   it outright (below `removeWoundWhenMended` threshold, presumably).
4. `RejuvenateEvent` clears the consequence wound along with everything else (already covered generically by
   WG's shipped `RejuvenateClearsEverythingTest`-equivalent, `WoundDamageProjectionSystem.OnRejuvenate` — but
   this test proves the *specific* consequence-wound path clears too, which is worth keeping as its own
   assertion since consequence wounds are created by a different code path — `AmputationSystem.
   ApplyAmputationConsequences` — than the routing seam's own wound creation).
5. A test asserting the consequence wound does **not** appear when amputation itself is disabled/refused
   (exact condition to be read in full at implementation time — the file was read only far enough to confirm
   structure and portability, not transcribed assertion-by-assertion here to respect the copyright-quoting
   limit).

All five are portable with the standard D8/D9/D19 translation. Recommend porting as
`Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs`, same path, same class name.

---

## 3. Load-bearing findings

### F1 — The overflow-based amputation path is dead for every human limb, in Onyx itself

`AmputationSystem.cs` has **two** independent amputation entry points:

- **Overflow path**: `OnPartDamageOverflowed` (subscribed `<WoundableComponent, PartDamageOverflowedEvent>`),
  fed by `WoundDamageRoutingSystem.AccumulateAmputationOverflow`, gated on
  `bodyPart.MaxDamage <= FixedPoint2.Zero` early-return.
- **Threshold path**: `HandlePartDamageApplied` (called from `OrganDamageSystem.OnPartDamageApplied` in a
  fixed order alongside `WoundSystem`/`WoundFractureSystem`/`WoundBleedingSystem`), gated on
  `bodyPart.AmputationThresholds.Count == 0` early-return, using `ReachedThreshold`/`GetThresholdProgress`.

Checked every source of `maxDamage` in Onyx (`git grep -n maxDamage HEAD -- Resources/Prototypes`): it is set
**only** on `Chest` (250) and `Groin` (250, `chest_groin.yml:19`) and on two unrelated animal parts
(`_Onyx/Body/Parts/animal.yml:11,88`). It is **never** set on `OrganBaseHead`/`ArmLeft`/`ArmRight`/
`HandLeft`/`HandRight`/`LegLeft`/`LegRight`/`FootLeft`/`FootRight` in `Resources/Prototypes/Body/
base_organs.yml` — the file that actually defines every human limb's base stats. And both
`OnPartDamageOverflowed` and `TryAmputate` early-return unconditionally when `bodyPart.PartType ==
BodyPartType.Chest`. So in Onyx: the only part with a non-zero `MaxDamage` (Chest) is the one part
explicitly forbidden from amputating, and every part that *can* amputate has `MaxDamage == 0`, which makes
`AccumulateAmputationOverflow` early-return before ever raising `PartDamageOverflowedEvent`. **The overflow
mechanism cannot fire on a human in Onyx, at this pin, full stop** — not a Wolfgate gap.

WG's `_WF/Wolfmed/Body/parts.yml` (WP7) faithfully reproduces exactly this shape: only `WolfmedBaseTorso` has
`MaxDamage: 250`; Head/Arm/Hand/Leg/Foot all have `AmputationThresholds` populated and no `MaxDamage` at all.
This is not a WP7 oversight — it is a correct, verified port of Onyx's own (functionally inert-for-limbs)
data. PLAN.md §2.12's warning ("some limbs can never be severed... invisible until phase 3") undersells the
finding slightly: it's not that WP7 might have missed populating `MaxDamage` for limbs — Onyx never populates
it either, for any organic species, anywhere in the sparse checkout.

**Consequence for the test plan:** name the primary, always-exercised amputation test **T-AMP-THRESHOLD** and
build it on `AmputationThresholds`/`HandlePartDamageApplied` (this is what actually happens when a player
shoots someone's arm off). Keep a test literally named **T-AMP-OVERFLOW**, per the task brief, but design it
as a documentation canary (P2-D-style, precedent: T-PASSIVE-B) proving the overflow mechanism is present,
wired, and correctly inert on the shipped human body — i.e. it protects against a future WP7 edit that
accidentally sets `MaxDamage` on a limb and creates silent double-amputation-eligibility, rather than testing
a mechanism that fires in play today.

### F2 — `AmputationSystem.cs` needs the `_wfPart.Get()` treatment already precedented in `WoundDamageRoutingSystem.cs`

Every field `AmputationSystem.cs` reads off `BodyPartComponent` beyond `.Body` and `.PartType` (which **do**
exist, unchanged, on Wolfgate's `Content.Shared.Body.Part.BodyPartComponent`) does **not** exist there:

| Onyx `bodyPart.X` | WG `BodyPartComponent` | Fix | Precedent |
|---|---|---|---|
| `.MaxDamage` | MISSING | `_wfPart.Get(part).MaxDamage` | `WoundDamageRoutingSystem.cs:799,804` (already shipped) |
| `.AmputationThresholds` | MISSING | `_wfPart.Get(part).AmputationThresholds` | `WoundDamageRoutingSystem.cs:954` (already shipped) |
| `.DismembermentFinishingDamage` | MISSING | `_wfPart.Get(part).DismembermentFinishingDamage` | same pattern, new site |
| `.AmputationConsequenceSeverity`* | MISSING (this one is read off the **parent's** `BodyPartComponent` in `ApplyAmputationConsequences`, via `parentPart.AmputationConsequenceSeverity`) | `_wfPart.Get(parent).AmputationConsequenceSeverity` | same pattern, new site |
| `.DismembermentSeverity` | MISSING | `_wfPart.Get(part).DismembermentSeverity` | same pattern, new site |
| `.Parent` | MISSING (Wolfgate's `BodyPartComponent` has no `Parent` field at all) | `_body.GetParentPartOrNull(part)` (verified at `SharedBodySystem.Parts.cs:414`, already used by `WolfmedBodySystem.TryDetachPart`) | §2.7 compat shim |
| `_body.TryDetachPart(part)` | MISSING on Wolfgate's `SharedBodySystem` (confirmed by grep: zero hits for `TryDetachPart` outside the Wolfmed compat file) | `_wfBody.TryDetachPart(part)` via the **already-shipped** `WolfmedBodySystem` compat system (§2.7) | Used already by `BodyConsequencesTest.cs`, `WoundScarTest.cs`, `WoundBleedingTest.cs` |
| `bodyPart.PartType == BodyPartType.Chest` (×2, `OnPartDamageOverflowed`/`HandlePartDamageApplied`/`TryAmputate`) | `BodyPartType.Chest` does not exist (D9) | `== BodyPartType.Torso` | D9, applied identically elsewhere |

\* Read carefully: Onyx's `ApplyAmputationConsequences(EntityUid body, EntityUid parent)` reads
`parentPart.AmputationConsequenceSeverity` — i.e. off the **parent** part's component, not the severed part's.
This must carry through the `_wfPart.Get()` redirect unchanged (it already does structurally; just don't
redirect it to `part` by mistake).

This is **not new scope** in the sense that it invents anything — it is the identical `D8` pattern the phase-3
WP will need to apply anyway to compile `AmputationSystem.cs` at all. It is listed here because every T-AMP-*
test in §4 is gated on these edits landing correctly, and a test failure that is actually "forgot one of these
seven redirects" should not be mistaken for a balance or logic bug.

### F3 — Locational armour: two of three ported tests assert behaviour Onyx's own code does not perform

Fully corroborated by `armour.md` §0 item 2 and §3.5, independently re-derived here from
`Content.Shared/Armor/SharedArmorSystem.cs`'s `OnPartDamageModify` (ONYX): the method loops
`component.PartModifiers` (checking `Parts`/`Symmetry` per-entry) and, if none match (including when
`PartModifiers` is empty), falls through to applying `component.Modifiers` **unconditionally**, under a
comment literally marked `<Onyx-ArmorGlobalProtection-edited>` stating "never skipped for an individual body
part that is not listed in @Coverage/@CoverageSymmetry." `Coverage`/`CoverageSymmetry` are declared DataFields
with **zero C# consumers anywhere in the Onyx tree** (`git grep -n "\.Coverage\b\|\.CoverageSymmetry\b" -- '*.cs'`
returns only unrelated Solar/IdentityManagement/Vampire hits).

Practical effect: `AppliesLocationalArmorExactlyOnceTest`'s `WoundFoundationArmorHead` (`coverage: [Head]`,
no `partModifiers`) would, under Onyx's actual code, protect **both** the head and the torso equally (global
fallback), making the test's `torso == 10` (unreduced) assertion **fail** against a faithful port.
`EmptyCoverageAndSymmetryTest`'s second half (`coverage: [Arm], coverageSymmetry: [Left]`) has the identical
problem for the right arm. Only `LocationalModifierOverridesAndFallbackTest` (which supplies real
`partModifiers` entries and therefore never reaches the dead fallback-with-empty-PartModifiers branch) is
consistent with Onyx's shipped behaviour.

This is the same defect class as P2-D9 (Onyx's `EmotesThreshold` YAML key never binding) and P2-D13 (the
manipulation-modifier sign inversion): a documented, intended mechanic that the shipped Onyx code does not
actually implement. `armour.md` §4.1 already frames this as a required user decision (ship Onyx's actual
behaviour vs. ship Onyx's *documented intent*, i.e. actually gate on Coverage/CoverageSymmetry) and provides
the implementation for both options. **This report does not re-litigate that decision** — it adopts
`armour.md`'s recommended "Option B" (gate active) for the test table in §4, since Option B is the only one
under which all three tests, plus P3-3's own stated goal ("this is the piece that makes helmets and vests
matter per limb"), are simultaneously true. If the user picks Option A instead, `AppliesLocationalArmorExactlyOnceTest`
and `EmptyCoverageAndSymmetryTest` must be rewritten to assert the (documented-as-a-deviation) global-fallback
behaviour rather than skipped — recommend rewrite over skip, since coverage-gating is exactly the contract a
regression here would silently break.

### F4 — Organ consequences: architecture gap, and a scope correction on `OrganEffectSystem`

Traced the full dependency chain of `OrganDamageSystem.OnPartDamageApplied` (the wound-driven entry point):

```
OrganDamageSystem.OnPartDamageApplied
  → BodyPartProfilePrototype.OrganDamage (Chances/MaxAffected)   [ALREADY VENDORED — WoundPrototype.cs:184-199, phase 1, unused until now]
  → SharedBodySystem.GetPartOrgans(part)                         [SAME signature in WG — SharedBodySystem.Parts.cs:825]
  → OrganDamageComponent (HitChance/SelectionWeight/DamageMultipliers/MaxDamageFraction) [MISSING — new _Onyx/Body file, no WG collision]
  → OrganHealthSystem.ChangeHealth(organ, -applied)               [MISSING — new _Onyx/Body/Systems file]
       → OrganComponent.Health/MaxHealth                          [MISSING as fields on WG's OrganComponent — see below]
       → OrganFunctionChangedEvent                                [new event, free pair]
       → DestroyOrgan(organ)
            → BodyPartComponent.Organs (slot list)                [need to verify shape — SAME/DIFFERENT to check at implementation time]
            → SharedBodySystem.TryRemoveOrgan(part, slot, out organ)  [MISSING — Onyx-only signature]
                 → WG has SharedBodySystem.RemoveOrgan(EntityUid organId, OrganComponent? organ = null)  [DIFFERENT signature, simpler — takes the organ directly, no slot lookup needed]
            → OrganComponent.DestructionWound / .DestructionWoundSeverity  [MISSING as fields — see below]
            → WoundSystem.CreateOrMergeWound(parent, wound, severity)  [SAME — already used throughout]
```

**`OrganComponent.Health`/`MaxHealth`/`DestructionWound`/`DestructionWoundSeverity` live on an *upstream*
file** — `Content.Shared/Body/OrganComponent.cs` in Onyx (bare `Content.Shared.Body` namespace, **not**
`_Onyx`), inside `<Onyx-OrganHealth>`/`<Onyx-OrganDamage>` comment-tagged blocks — i.e. Onyx patches the
vanilla organ component directly, the same pattern already recorded for `Content.Shared/Body/OrganComponent.cs`
generally (see the existing D2.17 note about the bare-namespace difference). Wolfgate's own
`Content.Shared/Body/Organ/OrganComponent.cs` has neither field. **This needs a new phase-3 upstream hook**
(four additive `// WOLFGATE` DataFields, no behavioural change to existing consumers — same shape and risk
level as the already-authorised HOOK 7 on `HealingComponent`), not yet named in any decisions document. This
is a phase-3 planning gap that should be raised alongside this report, not something the test plan can route
around: T-ORGAN's fixture needs `Health`/`DestructionWound` to exist as real fields to spawn a test organ
against.

**`Content.Server/_Onyx/Body/OrganEffectSystem.cs` (the file DECISIONS.md's P3-2 explicitly names as a
maybe) is confirmed, by reading it in full, to be surgery/anatomy-driven, not wound-driven**: its
`Initialize()` subscribes `OrganGotRemovedEvent`/`OrganGotInsertedEvent`/`OrganFunctionChangedEvent`/
`NeuroInterfaceEnabledChangedEvent` and its body (`RefreshBody`) computes "what components should this body
have given its current organs" (missing-head/eyes/ears/tongue/heart status effects, blindness sync, a cut
vocal cords → `StatusEffectSurgicallyMuted` status effect) — none of which is a consequence *of organ
damage*, and several of its dependencies (`_Onyx.Surgery.Augments.NeuroInterface`, `_Onyx.Medical.Surgery`,
`BodyAnatomyComponent`) are D7/D8-excluded surgery/anatomy machinery that does not exist in Wolfgate at all.
**Recommendation: mark `OrganEffectSystem.cs` and `FunctionalOrganComponent` `skipped` for phase 3** with
this finding as the reason, and scope P3-2/T-ORGAN to `OrganDamageSystem` + `OrganHealthSystem` (trimmed of
the `BrainComponent`/mob-death branch in `OrganHealthSystem.Update`, which needs its own scoping decision —
see the open question in §6).

### F5 — T-AMP-EXPLOSION's determinism does not come from a cvar

`WoundDamageRoutingSystem.TryApplyDistributedDamage`/`TryRouteDistributedDamage` are already-shipped **public**
methods (`isExplosion: bool = false`, `variation: float = 0f` parameters) — confirmed present and unchanged
since phase 1 (`WoundDamageRoutingSystem.cs:177-282,835-861`), including the explosion-candidate weighted-pick
logic (`PickExplosionAmputationCandidate`, `:874-...`) which already reads `_wfPart.Get(parts[i]).AmputationThresholds`
(§F2's redirect pattern, already applied here in the already-shipped file). `CCVars.ExplosionLimbDamageVariation`/
`ExplosionWoundMultiplier` (`explosion.damage_variation` / `explosion.wounding_multiplier`) are ported but
**read nowhere in the routing seam** — their only reader anywhere in the tree is
`Content.Server/Explosion/EntitySystems/ExplosionSystem.CVars.cs`, an upstream file whose `LimbDamageVariation`/
`WoundMultiplier` fields are themselves only meaningful if `ExplosionSystem`'s damage-application call site is
hooked into the wound router — explicitly on D24's "not hooked" list. **So there is currently no live wiring
between a real in-game explosion and `TryApplyDistributedDamage(isExplosion: true, ...)` at all** — this is
expected and correct for phase 1-3 scope (D24), but it means T-AMP-EXPLOSION must call the routing API
directly rather than spawning an actual `Explosion` prototype and detonating it.

Determinism recipe (no RNG dependency at all, not just a "usually passes" cvar pin):
1. Restrict the target mask to a **single** part with `AmputationThresholds.Count > 0` (e.g.
   `TargetBodyPart.LeftArm`) — `PickExplosionAmputationCandidate`'s weighted-random pick then has exactly one
   candidate, so `_random.NextFloat()`'s exact value cannot change the outcome.
2. Size total accumulated damage so `GetThresholdProgress(totalDamage, thresholds) >= 2.0` (i.e. ≥ 2× the sum
   of per-type threshold ratios) — `TryExplosionAmputate`'s chance is `Math.Clamp(progress * 0.5f, 0, 1)`,
   which saturates to exactly `1.0` at `progress == 2.0`, making `_random.Prob(chance)` always `true`.
3. Pass `variation: 0f` explicitly as a call parameter (documents intent and guards a future regression where
   someone wires the CVar through; it is not load-bearing today since a single-part mask has no cross-part
   weight jitter to begin with).

This should be flagged to whoever owns the P3-1 implementation: the test is fully deterministic, but it is
testing the **routing API's** explosion-amputation branch in isolation, not an end-to-end "grenade goes off,
arm falls off" path — that end-to-end wiring is out of scope per D24 and is not silently gained by this test
passing.

### F6 — Reattachment: what's already covered vs. what T-REATTACH adds

`WoundScarTest.ThresholdTreatmentAttachmentAndRejuvenateTest` (shipped) already proves: `wfBody.TryDetachPart`
detaches a wounded part; `entities.System<SharedBodySystem>().AttachPart(EntityUid parentPart, string slotId,
EntityUid part)` (confirmed signature, already called at that test's line) reattaches it to a named slot; the
wound entity survives both operations unchanged. What it does **not** check, and what PLAN.md §8.3 trap 2 is
actually worried about: whether `WolfmedBodyPartLifecycleSystem.OnPartAdded` (the sole caller of
`WoundDamageProjectionSystem.OnPartInserted`) actually re-establishes `WoundableComponent`/`DamageableComponent`
on the part such that **new** damage/bleeding on the reattached part behaves normally — i.e. whether reattaching
resets the part into a fully-live wound-tracking state, not merely whether pre-existing wound *data* survives
the round trip. T-REATTACH closes exactly this gap by re-damaging the part post-reattachment and asserting
bleeding resumes.

---

## 4. New Wolfgate test spec

All new tests target `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/` (ported-test extensions) and
`WG/Content.IntegrationTests/Tests/_WF/Wolfmed/` (new bridge-style tests), per the existing split.

| # | Test | File | Setup | Key assertion(s) | Depends on | Notes |
|---|---|---|---|---|---|---|
| **T-AMP-THRESHOLD** | `AmputationThresholdSeversLimbTest` (near-port of `WoundBleedingTest.TraumaticAmputationCreatesSevereStumpBleedingTest`) | `_Onyx/Wounds/WoundBleedingTest.cs` (restore the dropped test) | Existing `WoundBleedingBody` fixture. `routing.TryApplyPartDamage(body, head, Spec("Slash", 200))`, then `+15` more. | After 200: head still attached, `WoundableComponent.Severable == true`. After +15: head detached (`!graph.BodyHasChild(body, head)`); torso holds a wound `DismembermentWound` (severity = host `DismembermentSeverities[Head] = 200`, §5) **and** a wound `AmputationConsequenceWound` (severity = `WolfmedBodyPartComponent.AmputationConsequenceSeverity`, default 35, §5); `BloodstreamComponent.BleedAmount >= 40` on the body; severed head entity is a real, un-deleted entity in the world (thrown, `_throwing.TryThrow`) — assert `!Deleted(head)` and `Transform(head).ParentUid != body`. | AmputationSystem port + F2's 7 redirects; F1's threshold-path framing | Matches Onyx's own test construction exactly (200 then 15 Slash on Head) — both hit values are already load-bearing in shipped `parts.yml`/`WoundHostComponent` defaults, not invented for the test. **This is P3-5's `TraumaticAmputationCreatesSevereStumpBleedingTest` requirement, satisfied.** |
| **T-AMP-OVERFLOW** | `OverflowAmputationMechanismIsInertOnShippedLimbsTest` | `_WF/Wolfmed/WolfmedAmputationTest.cs` (new file) | Real `MobHuman`. Drive an arm to 500 Blunt (`routing.TryApplyPartDamage`), i.e. far past its 250 `AmputationThresholds.Blunt`. | `WoundableComponent.AmputationOverflow == 0` throughout (the overflow accumulator never engages because `_wfPart.Get(arm).MaxDamage == 0`); the arm still amputates (via the *threshold* path, not overflow) once a qualifying finishing hit lands — assert it does, to distinguish "inert" from "broken." Then, as a canary for a future data regression: spawn a **bespoke** `[TestPrototypes]` part with `maxDamage: 50` set explicitly, drive it past 50, and assert `AmputationOverflow > 0` **does** accumulate there — proving the mechanism itself works and is data-gated, not code-dead. | AmputationSystem port | Named to match the task brief's "T-AMP-OVERFLOW," but scoped as the F1 canary rather than a "normal play" test, since the literal overflow path cannot fire on any shipped human part. Re-run this test on every future `parts.yml` edit — if it starts failing because a limb now has `MaxDamage > 0`, that's a deliberate design change, not a bug, but it changes this test's assumptions. |
| **T-AMP-EXPLOSION** | `ExplosionAmputatesDeterministicallyTest` | `_WF/Wolfmed/WolfmedAmputationTest.cs` | Real `MobHuman` (or the bridge fixture). `routing.TryRouteDistributedDamage(body, Spec("Slash", X), TargetBodyPart.LeftArm, DamageDistribution.SplitEvenly, variation: 0f, isExplosion: true)` with `X` sized so `GetThresholdProgress(X, {Slash:130}) >= 2.0` i.e. `X >= 260` Slash (derive exact value against the shipped `AmputationThresholds.Slash = 130` for Arm, §5) and `X >= DismembermentFinishingDamage[Slash] = 15` for `IsFinishingHit`. | Left arm detaches in this single call (no retry loop, no flaky assertion); torso gets both consequence wounds as in T-AMP-THRESHOLD; **assert the *other* arm is unaffected** (proves the single-eligible-part mask, not luck, drove the deterministic candidate pick). | AmputationSystem port + F5's recipe | Deliberately does **not** spawn an `Explosion` entity or go through `ExplosionSystem` — see F5. Document this explicitly in the test's own doc-comment so nobody "fixes" it into a slower, still-not-more-representative end-to-end test. |
| **T-AMP-CONSEQUENCE-MERGE** | `RepeatedAmputationMergesConsequenceWoundTest` | same file, or port `AmputationConsequenceTest.cs` #2 directly (§2.1) | Amputate two different parts whose parent is the same torso (e.g. left arm then right arm, if both map to the same consequence-wound prototype id) | Torso holds **one** `AmputationConsequenceWound` entity with combined severity, not two separate wound entities (`WoundSystem.CreateOrMergeWound` semantics) | AmputationSystem port | Direct port target from `AmputationConsequenceTest.cs`; listed separately here because it is easy to accidentally under-test (asserting "a wound exists" without asserting "exactly one"). |
| **T-REATTACH** | `ReattachedPartRejoinsWoundTrackingTest` | `_Onyx/Wounds/WoundBleedingTest.cs` or a new `_WF/Wolfmed/WolfmedReattachTest.cs` | Real wound host; detach an arm via `wfBody.TryDetachPart`; `graph.AttachPart(torso, "left arm", arm)` (Shitmed slot-id reattach, precedent: `WoundScarTest.cs`); **then deal new damage to the reattached arm.** | Post-reattach: `HasComp<WoundableComponent>(arm)` and `HasComp<DamageableComponent>(arm)` (both re-established, not stale references to deleted components); `_wfPart.Get(arm)` still returns the correct `WolfmedBodyPartComponent` data (fracture profile / amputation thresholds unchanged from before detachment — proves `WolfmedBodyPartComponent` isn't networked/re-initialized incorrectly, per its own doc comment "nothing mutates these at runtime"); a fresh hit creates a **new** wound on the arm and the arm's `WoundBleedingComponent`/`BloodstreamComponent` projection updates (bleeding "rejoins"), not silently absorbed as systemic damage. | `WolfmedBodyPartLifecycleSystem`'s existing `<WoundHostComponent, BodyPartAddedEvent>` subscription (already shipped, phase 1) — this test is what actually exercises the surgery-attach half of PLAN §8.3 trap 2 for the first time. | No new C# needed — this is pure test debt closure, testing already-shipped phase-1 code from a new angle. Can be written and run **before** any P3-1/P3-2/P3-3 code lands; recommend scheduling it first as a cheap, high-value, zero-dependency test. |
| **T-ORGAN** | `OrganDestructionMergesConsequenceWoundTest` | `_WF/Wolfmed/WolfmedOrganTest.cs` (new) | Bespoke `[TestPrototypes]`: a wound-host body with one organ-bearing part; the organ prototype sets `health`/`maxHealth` low (e.g. 10) and `destructionWound`/`destructionWoundSeverity` (e.g. a generic `SlashWound` at severity 20, or a dedicated test wound id); the part's `bodyPartProfile.organDamage` sets `chances: {Torso: 1.0}` (or whichever part type is used), `maxAffected: 1`; the organ carries `- type: OrganDamage` with `hitChance: 1, selectionWeight: 1, damageMultipliers: {Blunt: 1}, maxDamageFraction: 1` so a single hit both always rolls and always deals its full multiplied damage. | `routing.TryApplyPartDamage(body, part, Spec("Blunt", 10))` once → organ `Health <= 0`; organ entity is destroyed/removed from its slot (`!HasComp<OrganComponent>` on the old organ id, or the id is `Deleted`); part gains a wound matching `destructionWound` at `destructionWoundSeverity`. **Negative control:** repeat with `chances: {}` (or the relevant part type omitted) → organ `Health` unchanged after the same hit, proving the roll is real and not a hard-coded pass. | `OrganDamageSystem` + `OrganHealthSystem` (trimmed, no `OrganEffectSystem` — F4) port; the new upstream `OrganComponent` hook (F4) | **First-ever test of this mechanism in either codebase** (§2, no Onyx test exists to port). Design the fixture to be self-contained (own organ/part/wound prototypes) rather than depending on a real human organ having `destructionWound` populated, since that YAML population is itself new phase-3 content, not yet written. |
| **T-ARMOUR-COVERAGE-1** | `AppliesLocationalArmorExactlyOnceTest` (ported, per `armour.md` §6.2) | `_Onyx/Wounds/WoundDamageFoundationTest.cs` | `armour.md`'s `WoundFoundationArmorHead` (`coverage: [Head]`) equipped `outerClothing`; hit head and torso 10 Blunt each | head **5**, torso **10** (unreduced — proves Coverage actually gates) | `armour.md`'s Option B (Coverage/CoverageSymmetry gate implemented) | Red against Onyx's own shipped (Option-A) behaviour — see F3. Adopt only if Option B is chosen. |
| **T-ARMOUR-COVERAGE-2** | `EmptyCoverageAndSymmetryTest` (ported, per `armour.md` §6.2) | same | `WoundFoundationArmorAllHead` (no coverage) in `head` slot, hit torso → **5** (empty coverage protects everywhere, slot-independent); then `WoundFoundationArmorLeftArm` (`coverage:[Arm], coverageSymmetry:[Left]`) in `outerClothing`, hit both arms | leftArm **5**, rightArm **10** (unreduced) | same | Second half is the one that is red under Option A. |
| **T-ARMOUR-COVERAGE-3** | `LocationalModifierOverridesAndFallbackTest` (ported, per `armour.md` §6.2) | same | `WoundFoundationArmorLocational` (`partModifiers` for Head/LeftArm/RightArm, `coverage:[Torso]` global fallback), 20 Blunt to all four parts | head **5**, torso **16**, leftArm **10**, rightArm **15** | none — green under both options | Port unconditionally regardless of the Option A/B decision. |
| **T-ARMOUR-AP** *(recommended addition, from `armour.md` §6.2)* | `PartModifiersRouteThroughArmorPenetrationTest` | same | Locational armour as above, `armorPenetration: 1f` via the facade/`targetPart` overload | head takes **20** (full, AP defeats the locational modifier too) not 5 | Option B | Not in Onyx; guards the new `partModifiers` branch from silently breaking AP. |
| **T-VISUALS** | `PartDamageProjectsToVisualsComponentTest` | `_WF/Wolfmed/WolfmedVisualsTest.cs` (new) or extend `WolfmedDamageBridgeTest.cs` | Real wound host; deal targeted Blunt damage to the left arm only | `Comp<PartDamageVisualsComponent>(body).Damage[HumanoidVisualLayers.LArm]` carries a **positive** `DamageSpecifier` matching the dealt amount (via `GetPositiveDamage`/`GetDamagePerGroup`, whichever `TryGetVisualLayer` maps to); the sibling `.RArm` entry is **absent or zero**. Run the identical check against `Pair.Client`'s mirrored entity (same `EntityUid`, since it's `[NetworkedComponent]`+`AutoNetworkedField`) to prove the data reaches the client, not just the server. | None beyond what's already shipped (`PartDamageVisualsComponent`/`WoundDamageProjectionSystem.RefreshBodyDamage`/`TryGetVisualLayer` all live since phase 1, per `visuals.md` §1) | **This test can be written today, independent of P3-4 landing** — it tests already-shipped server data plumbing for a client consumer that doesn't exist yet. Recommend writing it *before* P3-4's client `DamageVisualsSystem` hook, as a safety net proving the data side was never the risk. The actual sprite-state rendering (`Content.Client/Damage/DamageVisualsSystem.cs`'s hook) is **not** headlessly assertable per `visuals.md` §10/§12 and per this project's "prefer logic tests" memory — do not attempt a screenshot-diff or sprite-state test; the server-side `PartDamageVisualsComponent` assertion is the correct and sufficient test. |

---

## 5. Balance numbers (P3-6) — what the user must see

All values read directly from the shipped `_WF/Wolfmed/Body/parts.yml` and `WoundHostComponent`'s C#
defaults (`Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs`), cross-checked against Onyx's own
`Resources/Prototypes/Body/base_organs.yml` (Head matches exactly: 200/200/350 — high confidence the rest do
too, since WP7's file header cites the same Onyx source family for the sibling Torso value and the shape is
identical across all five limb abstracts).

| Part | Slash threshold | Piercing threshold | Blunt threshold | Finishing damage (host default, no per-part override) | Dismemberment (consequence-on-parent) severity | `MaxDamage` (overflow cap) |
|---|---|---|---|---|---|---|
| Head | 200 | 200 | 350 | Slash 15 / Piercing 40 / Blunt 50 | 200 | 0 (overflow inert, F1) |
| Arm (L/R, shared) | 130 | 250 | 250 | same | 120 | 0 |
| Hand (L/R, shared) | 70 | 200 | 150 | same | 80 | 0 |
| Leg (L/R, shared) | 150 | 250 | 300 | same | 120 | 0 |
| Foot (L/R, shared) | 80 | 220 | 170 | same | 80 | 0 |
| Torso | n/a (`AmputationThresholds` empty — excluded from amputation regardless) | | | n/a | 100 (`DefaultDismembermentSeverity`, unused since Torso never amputates) | **250** (only nonzero `MaxDamage` in the game; inert per F1) |

Additional fixed values (component defaults, not per-part):
- `AmputationConsequenceSeverity` (the `AmputationConsequenceWound` severity added to the **parent** part on
  every amputation, regardless of which limb was severed): **35** (`WolfmedBodyPartComponent` default, no
  per-part override in shipped `parts.yml`).
- `SeverableResetRatio`: **0.8** — once a part becomes `Severable`, its cumulative threshold-damage ratio must
  drop below 80% (via healing) to un-flag it; otherwise the *next* hit that also independently clears
  `IsFinishingHit`'s minimum will amputate.
- Explosion-amputation chance formula: `clamp(thresholdProgress * 0.5, 0, 1)` — i.e. a single explosive hit
  that deals exactly 1× a limb's threshold total has a 50% amputation chance; 2×+ is a guaranteed amputation
  (used for T-AMP-EXPLOSION's determinism, F5).

**What the user should sanity-check against Wolfgate gun damage before playtest** (not computed here — no
verified live weapon-damage figures were pulled in this pass, and P3-6 asks only that the amputation numbers
themselves be surfaced): whether a single well-aimed hit from a standard sidearm/rifle can plausibly clear a
limb's Slash/Piercing/Blunt threshold *and* separately clear that damage type's finishing-damage minimum in
the same or a following hit. Given phase-1's own §8.1 item 3 finding that "limb damage against armour roughly
doubles" post-bridge relative to pre-bridge Shitmed, and that these thresholds are Onyx's own unmodified
numbers (D4), the same "needs a playtest measurement, not an assumption" caveat phase 1 already recorded for
general limb damage applies here with equal force to amputation specifically. Recommend a dedicated
Wolfgate-weapon-vs-threshold comparison pass alongside (not instead of) these tests, using real weapon
prototypes rather than the tests' synthetic `Spec(...)` hits.

---

## 6. Open questions / decisions needed before implementation

1. **Armour Coverage semantics (F3, `armour.md` §4.1's decision, restated because it directly picks which of
   T-ARMOUR-COVERAGE-1/2 can be written as green tests):** ship Onyx's actual (Coverage-inert) behaviour, or
   implement Coverage/CoverageSymmetry as documented. This report's test table assumes the latter.
2. **New upstream hook needed for organ Health/DestructionWound (F4):** not yet authorised anywhere. Needs
   the same one-paragraph sign-off pattern as HOOK 7 before T-ORGAN's fixture can compile against a real
   `OrganComponent`.
3. **`OrganEffectSystem.cs`/`FunctionalOrganComponent` scope (F4):** this report recommends `skipped`, but
   that is a scoping call, not purely a test-planning one — flagging for confirmation rather than assuming.
4. **`OrganHealthSystem.Update`'s brain/death branch:** the vendored `Update` loop also contains a
   `BrainComponent`→death-if-missing-and-not-already-dead branch unrelated to the generic destroy-organ path.
   Decide whether phase 3 wants this (it would be Wolfmed's first "missing vital organ kills you over time"
   mechanic) or whether it should be `// WOLFGATE`-disabled alongside `OrganEffectSystem` until a later phase
   — affects whether T-ORGAN needs a companion brain-specific test.
5. **`BodyConsequencesTest`'s dropped inventory-slot coupling:** confirmed correctly out of scope (§2), no
   action needed, listed only because the task asked it be checked explicitly.

---

## 7. Run commands

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
dotnet run --project Content.YAMLLinter -c Release
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build --filter "FullyQualifiedName~DockTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
  --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed" \
  --logger "console;verbosity=detailed"
```

Run the amputation/organ/armour/visuals suites (`AmputationConsequenceTest`, the restored
`TraumaticAmputationCreatesSevereStumpBleedingTest`, `WolfmedAmputationTest`, `WolfmedOrganTest`,
`WoundDamageFoundationTest`'s three armour tests, `WolfmedVisualsTest`) individually first during
development — the filter above is for the final gate.
