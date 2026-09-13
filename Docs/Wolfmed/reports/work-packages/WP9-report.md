# WP9 — tests (PLAN.md §4 WP9 / §6)

**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
**Date:** 2026-09-13. No commits made; tree left uncommitted per DECISIONS.md.

---

## 1. Files created / modified

| # | Path | Status | What |
|---|---|---|---|
| 1 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | new (adapted port) | 9 of Onyx's 12 tests |
| 2 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | new (adapted port) | 4 of Onyx's 6 tests |
| 3 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundScarTest.cs` | new (adapted port) | 1 test |
| 4 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` | new (adapted port) | 2 of Onyx's 3 tests |
| 5 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundHealingTest.cs` | new (adapted port) | 4 of Onyx's 5 tests |
| 6 | `Content.IntegrationTests/Tests/_Onyx/Body/BodyConsequencesTest.cs` | new (rewritten port) | 2 tests |
| 7 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | new (`_WF`) | 7 tests — T-SETUP, T-RESULT, T-PIERCE, T-CAUSTIC, T12, non-wound-host control, no-double-application |
| 8 | `Content.Shared/_Onyx/Wounds/PainSystem.cs` | modified (vendored `_Onyx`) | **blocker fix** — `new ModifyPainGainEvent()` zeroed the multiplier, so all pain was multiplied by 0 |
| 9 | `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | modified (`_WF`, WP5 file) | **2 fixes** — `TerminatingOrDeleted` guards; drive `WoundBleedingSystem`'s part-lifecycle entry points |
| 10 | `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` | modified (`_WF`, WP7 file) | **blocker fix** — `RemCompDeferred` → `RemComp`; every `MobProtogen` spawn was throwing |
| 11 | `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | modified (`_WF`, WP2 file) | **fix** — `ComponentStartup` → `MapInitEvent`; beds were gaining a component on spawn |
| 12 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified | 10 WP9 rows + a WP9 Deviations section; `PainSystem.cs` row flipped `verbatim` → `modified` |

**Zero upstream (non-`_Onyx`, non-`_WF`) files touched.** Verified:
`git diff HEAD --stat -- Content.Server Content.Shared Content.Client Resources` is empty for WP9's own changes
(files 8–11 are themselves untracked additions from WP2/WP5/WP7, which the user has not committed yet), and
`git ls-files --others` over `Content.IntegrationTests`/`Docs` lists exactly files 1–7 and 12.

---

## 2. Every `// WOLFGATE` edit and its reason

### 2.1 Production-code fixes (4 sites in 4 files)

| File | Site | Marked edit | Reason |
|---|---|---|---|
| `Content.Shared/_Onyx/Wounds/PainSystem.cs` | `:111` (`RefreshWoundPain`) and `:233` (`ChangePain`) | `new ModifyPainGainEvent()` → `new ModifyPainGainEvent(1f)`, with a 3-line `// WOLFGATE` comment at each | `ModifyPainGainEvent` is a `record struct` whose primary constructor declares `float Multiplier = 1f`, but `new T()` on a struct binds to the **implicit parameterless constructor**, which zeroes the field. Every pain gain and every wound-pain floor was multiplied by 0, so `GetRawPain` stayed at 0 on every wound host — pain, pain shock, the pain stun and all pain-driven emotes were inert. Onyx's source is byte-identical here, so Onyx is presumably affected too. |
| `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | `OnPartAdded`, `OnPartRemoved` | `if (TerminatingOrDeleted(body) \|\| TerminatingOrDeleted(args.Part.Owner)) return;` | `RecursiveDeleteEntity` detaches every part while a mob terminates, so `WoundDamageProjectionSystem.RefreshDetachedDamage`'s `EnsureComp<PartDamageVisualsComponent>` threw `DebugAssertException` on **every mob deletion**. That poisoned the pooled test pair and failed the teardown of every test in the repo that had spawned a `MobHuman`. Onyx has exactly this guard in `BodyInventorySlotSystem.cs:32,45`. |
| same | `OnPartAdded`, `OnPartRemoved` | `_bleeding.OnPartInserted(part, body)` / `_bleeding.OnPartChanged(body)` + a `WoundBleedingSystem` dependency | `WoundBleedingSystem.OnPartInserted`/`OnPartChanged` had **no caller** in the port. Onyx drives them from `BodyInventorySlotSystem.cs:39,49`, which D8 does not port. Symptom: a detached limb's wounds kept bleeding into the body's `BloodstreamComponent.BleedAmount` and a re-attached limb's never rejoined it. Same class as PLAN §8.3 trap 2, extended to bleeding. |
| `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` | `OnWoundHostInit` | `RemCompDeferred<WoundHostComponent>` → `RemComp<WoundHostComponent>` | `RemCompDeferred` leaves the component in `EntityManager._deleteSet` at life stage `Initialized`; `StartComponents` then trips `DebugTools.Assert(!_deleteSet.Contains(component))` (`EntityManager.LifeCycle.cs:52`). **Every `MobProtogen` spawn threw**, failing `EntityTest.SpawnAndDeleteAllEntities*`. A straight `RemComp` moves it to `Deleted`, which `StartComponents` skips. |
| `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | `Initialize` | `<HealOnBuckleComponent, ComponentStartup>` → `<HealOnBuckleComponent, MapInitEvent>` | Every healing bed gained `WolfmedBedHealMarker` the moment it spawned, which is exactly what `PrototypeSaveTest.UninitializedSaveTest` forbids (`NFBedrollStained`, `NFBedrollStainedFolded`). That test's map is never initialised, so MapInit does not run there; for a live bed MapInit runs in the same spawn call, so gameplay is unchanged. Pair verified free (`BedSystem` takes only `Strapped`/`Unstrapped` on that component). |

### 2.2 Test-file `// WOLFGATE` notes

Every adaptation is marked at its site. Grouped by kind:

- **Fixture shape** (all 6 ported files): Onyx's Nubody `- type: InitialBody` + `organs:` becomes a Shitmed
  `- type: body` prototype plus `- type: Body / prototype:`; `- type: Injurable` dropped (D19);
  `partType: Chest` → `Torso` (D9); Onyx's bare `BodyPart` entities become Wolfgate's real `TorsoHuman` /
  `HeadHuman` / `LeftArmHuman` / `RightArmHuman` / `LeftLegHuman` so the parts carry `Damageable`
  (`OrganicPart`) and WP7's `WolfmedBodyPart` data.
- **API swaps:** `DamageableSystem` → `WolfmedDamageableSystem` (D12); `SharedBodySystem.TryDetachPart` →
  `WolfmedBodySystem.TryDetachPart` (§2.7); `TryAttachPart(parent, part)` →
  `SharedBodySystem.AttachPart(parent, slotId, part)`; `TargetResolverSystem` → `WoundTargetResolver` (D10);
  `Content.Shared._Onyx.Targeting.TargetBodyPart` → `Content.Shared._Shitmed.Targeting.TargetBodyPart`;
  `TargetBodyPart.FullArms` → `Arms`, `TargetBodyPart.Vital` → `Head | Torso` (neither exists in Wolfgate).
- **Server-side moves (D13/D14):** `WoundBleedingSystem`, `WoundHealingSystem` and `BloodstreamComponent` /
  `BloodstreamSystem` / `HealingComponent` resolve from `Content.Server.*`. The vendored wound systems keep
  Onyx's `Content.Shared._Onyx.Wounds` namespace even though the files live in `Content.Server`, so **no**
  `using Content.Server._Onyx.Wounds;` is needed (that was the first build error and is now a marked comment).
- **`- type: StatusEffects` on the foundation test body:** Wolfgate's `SharedStunSystem.TryParalyze` — what
  PLAN §2.8's shim maps Onyx's `TryUpdateParalyzeDuration` onto — refuses any entity without the **old**
  `StatusEffectsComponent`. Onyx's stun runs on StatusEffectNew and needs none.
- **Skipped tests**, each with a `// WOLFGATE` note in place of the test (§3.2 below).

---

## 3. Deviations from PLAN.md, with justification

### 3.1 PLAN §6.1's port list, narrowed

PLAN §6.1 said to port `WoundBleedingTest` "minus the tourniquet test" and `WoundFractureTest` with only the
`FractureEffectSystem` assertions split out. Two more tests had to go:

1. **`WoundBleedingTest.TraumaticAmputationCreatesSevereStumpBleedingTest` — dropped to phase 3.** It asserts
   `WoundableComponent.Severable`, and `Severable` is set **only** by `AmputationSystem`
   (ONYX `AmputationSystem.cs:46,86`; the port contains no other writer — verified by grep). D26 defers
   amputation to phase 3, so nothing in phase 1 can make a part severable. PLAN §6.1 did not notice this.
2. **`WoundFractureTest.EffectsRefreshOnTreatmentHealingAndDetachTest` — dropped to phase 2**, as PLAN §6.1
   anticipated ("`FractureEffectSystem.GetDurationMultiplier` assertions are phase 2 — split that test out").
   The whole test is `FractureEffectSystem`, so it is skipped rather than split.
3. **Onyx's three locational-armour tests** (`AppliesLocationalArmorExactlyOnceTest`,
   `EmptyCoverageAndSymmetryTest`, `LocationalModifierOverridesAndFallbackTest`) collapse into one
   `AppliesArmorExactlyOnceTest`. Wolfgate's `ArmorComponent` has no `coverage`, `coverageSymmetry` or
   `partModifiers` (WP8 left per-part armour as a phase-3 TODO), so two thirds of what those tests assert does
   not exist. The contract the bridge can actually break — armour applies **exactly once** per routed hit — is
   kept.
4. **`WoundDamageFoundationTest.TargetingContractAndRoutingTest`'s Onyx-targeting assertions dropped.**
   `SharedTargetingSystem.TryConvert` and `TargetingComponent.DefaultOdds()` belong to the Targeting stack D10
   declines to vendor, and phase 1 resolves the requested part exactly with no anatomical-odds scatter
   (§2.13). What remains: `IsSelectable` (HOOK 5), and resolver behaviour including D9's Groin→Torso fold.
5. **`BodyConsequencesTest` is a rewrite, not a port.** PLAN §6.1 called it a "near-direct port, zero wound
   dependencies". It is not: all three Onyx tests assert Nubody inventory-slot coupling (`shoes`, `socks`,
   `underwearb` disappearing with the groin) that **no Wolfgate system implements** — nothing under
   `Content.{Shared,Server}/Inventory` subscribes `BodyPartRemovedEvent`/`BodyPartDroppedEvent` (verified by
   grep) — and `BodyPartType.Groin` and `StandUpAttemptEvent` do not exist here. The two surviving tests
   assert the contract the wound bridge can break: detaching a part still cascades to its children, and a mob
   with no legs is still put down (Wolfgate goes down at **zero** legs, not at one —
   `SharedBodySystem.Parts.cs:390`). They run on a real `MobHuman`, i.e. a real wound host with GUARDs A/B/C
   live, so they double as the signature-compatibility check PLAN wanted.

### 3.2 Onyx test literals that are stale against Onyx's own pinned prototypes

Four assertions could not be satisfied by any Wolfgate behaviour because the Onyx test and the Onyx prototype
disagree **at the pin**. In every case the vendored C# and YAML are byte-identical to Onyx's, so this is not a
port defect. Each is corrected with the reasoning at the assertion; **re-check all four on an Onyx re-sync.**

| Test | Onyx literal | Actual | Why |
|---|---|---|---|
| `WoundFractureTest.GradeBoundariesAreDeterministicTest` | grades at 15/30/50/75 | 20/35/50/60 | `OrganicFractureProfile` declares `Hairline 20, Simple 35, Displaced 50, Comminuted 60` |
| `WoundBleedingTest.AutomaticClotting{Deadline,Disabled}Test` | wound severities 1 and 3 | raised to 10 and 30 | `SlashWound`'s bleeding behaviour has `minimumSeverity: 9`, so at 1 and 3 nothing ever bleeds and both tests assert nothing. The deadline test's `secondsPerSeverity` and `RunSeconds` were rescaled to keep its shape (one short deadline, one long) |
| `WoundBleedingTest.BandageReducesBleedingAndDamageReopensWoundTest` | `BleedingSeverity == 5` | 35 | `WoundSystem.SetWoundState` → `SyncRuntimeComponents` re-adds `WoundBleedingComponent` with `BleedingSeverity = wound.Comp.Severity` as soon as the wound is closed, so a fully bandaged severity-30 wound reopening with +5 lands at 35 |
| `WoundHealingTest.HealsSelectedPartDamageWithoutWoundTest` | `wound.Severity == 13.5` | 5 | 13.5 needs a wound `healingMultiplier` of 0.15; `BluntWound` sets none and `WoundPrototype.HealingMultiplier` defaults to 1 in **both** trees |

A fifth is a flakiness fix rather than a stale literal: `WoundScarTest` depends on `CCVars.SurgeryScarChance`
(ships at **0.35**) multiplied into the wound's own scar chance, so Onyx's version passes roughly a third of
the time. The test now pins the cvar to 1 for its duration and restores it.

### 3.3 Real Wolfgate behavioural differences, asserted as such

Three numbers in `WoundDamageFoundationTest` differ because Wolfgate genuinely behaves differently. Each is
explained at the assertion:

- **Detaching a vital part adds 100 `Bloodloss`.** Wolfgate's `HeadHuman` is vital, so Shitmed's
  `PartRemoveDamage` (`SharedBodySystem.Parts.cs:405`) applies 100 Bloodloss on removal; the projection then
  includes it. Onyx's bare test part had no vitality, hence its flat `6`. The test now asserts the 100
  Bloodloss lands in `SystemicDamageComponent` **and** that the projected total is 106 — which also makes this
  test cover PLAN §6.2's T14 (`VitalPartRemovalStillKills`) mechanism.
- **Damage dealt to a *detached* limb still goes through Shitmed's `<BodyPartComponent, DamageModifyEvent>`**
  (the `PartDamage` modifier set plus `GetPartDamageModifier(Head) = 0.5`), so 5 Blunt lands as 2 and pain
  goes 8.70 → 10.44 where Onyx expects 13.05. Routed hits on an *attached* part bypass this because the routed
  write uses `ignoreResistances: true` (PLAN §5.4).
- **`PainComponent.RecoveryPerSecond` is `FixedPoint2.New(1f / 9f)` = 0.11 at two decimals here**, so one
  second of recovery gives 8.59, not Onyx's 8.62.

A fourth is an ordering artefact rather than a difference: Onyx's pain-shock figures assume no residual
suppression, but its own decay block leaves 4.5 on the body and nothing clears it before the shock block. The
test clears suppression explicitly rather than re-deriving every figure with a 4.5 offset.

### 3.4 `DamageSpecifier.Empty` is not a "nothing landed" assertion here

`BodyPartProfileContractsTest` asserts `GetTotal() == 0` instead of `.Empty`. `DamageableInit` seeds every
supported type of the container to zero (D30 / §8.3 trap 1), so a part's `DamageSpecifier` is never `Empty`.

### 3.5 T-RESULT and T-PIERCE are driven at the seam, not through melee

PLAN §6.2's T-RESULT says "one melee light attack … and a `Blunt` melee hit must still produce stamina
damage". WP9 asserts the D27 contract directly — `DamageableSystem.TryChangeDamage` on a wound host returns a
non-null, non-empty `DamageSpecifier` whose total equals what landed — rather than driving
`SharedMeleeWeaponSystem`. Driving a real light attack needs a wielded weapon, a player session and an
interaction pipeline, and the stamina branch it would exercise is downstream of exactly the return value the
test already pins. T-PIERCE **is** driven through the real code path: it raises `HitscanRaycastFiredEvent` on a
`HitscanBasicDamage` entity whose `HitEntities` holds the wound host and a second body, which is the loop with
the `if (damageDealt == null) return;` at `HitscanBasicDamageSystem.cs:33-34`.

### 3.6 T12 is observed through `SleepingSystem`

Nothing in the test assembly can subscribe a directed `DamageChangedEvent` (RT raises it with
`broadcast: false`), so T12 puts the wound host to sleep and asserts it wakes: `SleepingSystem.OnDamageChanged`
early-returns on `args.DamageDelta == null` and is one of the eight systems PLAN §8.3 trap 5 names.

---

## 4. Build and test output (exact tails)

### 4.1 `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 \
    | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

### 4.2 `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`

```
$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 \
    | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

### 4.3 `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt`

```
$ dotnet build .../Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo 2>&1 \
    | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -40
Build succeeded.
    0 Error(s)
```

### 4.4 Wound + bridge test run

```
$ dotnet test .../Content.IntegrationTests.csproj -c DebugOpt --no-build     --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"     --logger "console;verbosity=detailed"
...
Total tests: 30
     Passed: 30
```

Full log: `C:/tmp/wolfmed-plan/wp/WP9-tests.log`.

### 4.5 Smoke run — `PrototypeSaveTest | EntityTest | DockTest`

```
$ dotnet test ... --filter "FullyQualifiedName~PrototypeSaveTest|FullyQualifiedName~EntityTest|FullyQualifiedName~DockTest"
  Passed AllComponentsOneToOneDeleteTest [39 s]
  Passed SpawnAndDeleteAllEntitiesInTheSameSpot [1 m 22 s]
  Skipped SpawnAndDeleteEntityCountTest [< 1 ms]
  Skipped SpawnAndDirtyAllEntities [< 1 ms]
  Passed TestDockingConfig(<0.5, 0.5>,<0.5, 0.5>,0 rad,0 rad,True) [45 ms]
  Passed TestDockingConfig(<0.5, 1.5>,<0.5, 1.5>,0 rad,0 rad,False) [13 ms]
  Passed TestPlanetDock [156 ms]
  Passed EntityEntityTest [25 ms]
  Passed UninitializedSaveTest [15 s]
  Passed AllItemsHaveSpritesTest [1 s]
  Passed SpawnAndDeleteAllEntitiesOnDifferentMaps [1 m 6 s]
Total tests: 11
     Passed: 9
    Skipped: 2
```

The two skips are upstream `[Ignore]`s, unrelated to Wolfmed. **`DockTest` passes**, so the `db.ef`
`admin_notes` environmental failure mode described in the task prompt did not occur in this session — every
failure reported below was a real assertion or a real exception, not the environmental one.

Before the §2.1 fixes this same filter reported **3 failures**: `SpawnAndDeleteAllEntitiesOnDifferentMaps` and
`SpawnAndDeleteAllEntitiesInTheSameSpot` (both `Failed to spawn entity MobProtogen` →
`DebugAssertException` in `EntityManager.LifeStartup`) and `UninitializedSaveTest`
(`Prototype NFBedrollStained gains a component on spawn: WolfmedBedHealMarker`). Log:
`C:/tmp/wolfmed-plan/wp/WP9-smoke.log`.

---

## 5. Per-test results

All 30 pass. No failures, so there is no assertion text to report.

| Fixture | Test | Result |
|---|---|---|
| `_Onyx.Wounds.WoundDamageFoundationTest` | `TargetingContractAndRoutingTest` | Passed |
| | `CombatSnapshotsRemainStableTest` | Passed |
| | `RoutesAndProjectsDamageTest` | Passed |
| | `NonTargetingOriginUsesWeightedFallbackTest` | Passed |
| | `DistributedDamageMasksAndRoundingTest` | Passed |
| | `CreatesMergesHealsAndPreservesWoundsTest` | Passed |
| | `BodyPartProfileContractsTest` | Passed |
| | `AppliesArmorExactlyOnceTest` | Passed |
| | `NonWoundHostUsesVanillaArmorTest` | Passed |
| | `PainApiAndProjectionTest` | Passed |
| `_Onyx.Wounds.WoundBleedingTest` | `ProjectsTreatsAndTracksAttachmentTest` | Passed |
| | `BandageReducesBleedingAndDamageReopensWoundTest` | Passed |
| | `AutomaticClottingDeadlineTest` | Passed |
| | `AutomaticClottingDisabledTest` | Passed |
| `_Onyx.Wounds.WoundScarTest` | `ThresholdTreatmentAttachmentAndRejuvenateTest` | Passed |
| `_Onyx.Wounds.WoundFractureTest` | `GradeBoundariesAreDeterministicTest` | Passed |
| | `PostArmorHitAndTreatmentPreconditionsTest` | Passed |
| `_Onyx.Wounds.WoundHealingTest` | `HealsSelectedPartDamageWithoutWoundTest` | Passed |
| | `LegacySelectionBleedingIsolationAndValidationTest` | Passed |
| | `UntargetedHealingTreatsDamageAcrossAllPartsTest` | Passed |
| | `ExactTargetSelectionAndMissingRejectionTest` | Passed |
| `_Onyx.Body.BodyConsequencesTest` | `DetachingLegRemovesChildFootTest` | Passed |
| | `DetachingEveryLegPreventsStandingTest` | Passed |
| `_WF.Wolfmed.WolfmedDamageBridgeTest` | `EveryPartGetsWoundableOnMapInitTest` (T-SETUP) | Passed |
| | `TryChangeDamageReportsRoutedDamageTest` (T-RESULT) | Passed |
| | `PiercingHitscanDamagesEntitiesBehindAWoundHostTest` (T-PIERCE) | Passed |
| | `CausticRoutesToTheHitPartTest` (T-CAUSTIC) | Passed |
| | `DamageChangedDeltaSurvivesProjectionTest` (T12) | Passed |
| | `NonWoundHostUnchangedTest` (D2 control) | Passed |
| | `NoDoubleApplicationTest` (T4) | Passed |

### Failure history (all fixed)

The first run of the ported suite was **2 passed / 28 failed / 1 skipped**
(`C:/tmp/wolfmed-plan/wp/WP9-tests-run1.log`). Root causes, in the order they were found:

| # | Symptom | Tests hit | Cause | Fix |
|---|---|---|---|---|
| 1 | `TearDown : DebugAssertException` from `TestPair.CleanReturnAsync` | 13 | `RefreshDetachedDamage`'s `EnsureComp<PartDamageVisualsComponent>` on a terminating entity during mob deletion | §2.1 `TerminatingOrDeleted` guards |
| 2 | `BleedAmount` 1.875 instead of 1.5 after a detach; re-attached limb never bleeds again | 2 | `WoundBleedingSystem.OnPartInserted`/`OnPartChanged` had no caller | §2.1 bleeding lifecycle wiring |
| 3 | `GetRawPain` == 0 everywhere | 2 | `new ModifyPainGainEvent()` zeroes `Multiplier` | §2.1 `PainSystem` fix |
| 4 | `HasComponent<StunnedComponent>` false | 1 | `TryParalyze` needs the old `StatusEffectsComponent` | `- type: StatusEffects` on the test body |
| 5 | stale Onyx literals | 5 | §3.2 | assertions corrected with reasoning |
| 6 | real Wolfgate differences | 3 | §3.3 | assertions corrected with reasoning |
| 7 | `Is.EqualTo(HashSet)` compares positionally | 1 | NUnit | `Is.EquivalentTo` |
| 8 | `.Empty` on a seeded `DamageSpecifier` | 1 | D30 seeding | `GetTotal() == 0` |

**A note on reading `dotnet test` output for this suite.** NUnit runs these fixtures in parallel and
`TestPair.OnDirtyDispose` calls `Assert.Warn` on whatever NUnit context is current, so a pair poisoned by one
test can surface as `Skipped — Test was dirty-disposed.` on a *different* test that never ran. Three tests
showed that in the second run; re-running them under a narrow `--filter` produced their real assertion
failures immediately. When a test in this suite reports `Skipped` with no assertion text, re-run it alone.

---

## 6. What a later WP must know

### New symbols and files

- **`Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs`** declares `[TestPrototypes]`
  `WolfmedBridgeBodyGraph` (a `body` prototype), `WolfmedBridgeBody`, `WolfmedControlBody` and
  `WolfmedBridgeHitscan`. `WolfmedControlBody` is `WolfmedBridgeBody` **minus `WoundHost`**, declared
  separately because RT has no YAML-level component removal — keep them in step by hand.
- Every ported fixture declares its own `body` prototype and mob ids (`WoundFoundationBody*`,
  `WoundBleedingBody*`, `WoundHealingBody*`, `WoundScarBody*`, `WoundFractureBody*`). `[TestPrototypes]` are
  global to the pool, so a new test file must not reuse those ids.
- **`WolfmedBodyPartLifecycleSystem` now depends on `WoundBleedingSystem`** (server). Anything else that needs
  a part-lifecycle hook should be added to that system's two handlers rather than taking its own subscription —
  `<WoundHostComponent, BodyPartAddedEvent>` / `<…, BodyPartRemovedEvent>` remain claimed exclusively by it.
- **`<HealOnBuckleComponent, MapInitEvent>` is now claimed** by `WolfmedBedHealMarkerSystem`
  (it released `<HealOnBuckleComponent, ComponentStartup>`).
- **`<WoundHostComponent, MapInitEvent>` is claimed by `WoundDamageProjectionSystem`** and cannot be taken by
  anything else — worth knowing because the obvious "strip `WoundHost` at MapInit" fix for the exclusion
  system is therefore unavailable; it stays on `ComponentInit` with a plain `RemComp`.

### Open items handed forward

1. **Pain was dead until WP9.** Everything downstream of `PainSystem` — pain shock, the pain stun, pain
   emotes, `PainNumbness`, the phase-2 pain HUD — has therefore **never been exercised** on a real mob. WP10
   should re-validate its own balance assumptions rather than trusting anything measured before this fix.
2. **Balance, now measurable:** with pain live, a wound host that reaches `SoftPainCap` (135) gets paralysed
   through `StunSystemOnyxCompat.TryParalyze`, which re-triggers stun VFX on every call (a recorded §8.2
   deviation). Watch for stun spam in the first playtest.
3. **`WoundPrototype.HealingMultiplier` is 1 for every ported wound**, so any topical heals a wound's severity
   one-for-one with the damage it heals. Onyx's tests imply 0.15 was intended. A D4 tuning item.
4. **Phase-3 test debt:** `TraumaticAmputationCreatesSevereStumpBleedingTest` (needs `AmputationSystem`),
   `TourniquetStopsOnlySelectedPartTest` (needs the `Tourniquet` prototype), the three locational-armour tests
   (need `ArmorComponent.PartModifiers`), `EffectsRefreshOnTreatmentHealingAndDetachTest` (needs
   `FractureEffectSystem`), `RepairSelectionAndSnapshotValidationTest` (needs `_Onyx.Repairable`), plus the
   surgery-attach assertion PLAN §8.3 trap 2 asks for.
5. **PLAN §6.2 tests not written in WP9** (the task scoped WP9 to T-SETUP, T-RESULT, T-PIERCE, T-CAUSTIC, T12,
   the control and no-double-application): T1–T3, T7–T11, T13–T16, T-AP, T-PASSIVE. Of these, **T-AP is WP8's
   own stated gate** and **T-PASSIVE is D29's gate** — both are still untested. WP8's report also asks for a
   T-EXEC test of `TryApplyLethalDamage`. `NoDoubleApplicationTest` covers T4, `NonWoundHostUnchangedTest`
   covers T5, `RoutesAndProjectsDamageTest` covers most of T1/T6/T14 and `PainApiAndProjectionTest` covers
   T15's pain half.
6. **No commits were made.** `Content.Shared/_Onyx/Wounds/PainSystem.cs`,
   `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`,
   `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` and
   `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` now differ from what WP4/WP5/WP7/WP2
   reported.

### Logs

| File | What |
|---|---|
| `C:/tmp/wolfmed-plan/wp/WP9-tests.log` | final wound + bridge run |
| `C:/tmp/wolfmed-plan/wp/WP9-tests-run1.log` | first run (2 passed / 28 failed), kept for the failure analysis |
| `C:/tmp/wolfmed-plan/wp/WP9-tests-pain.log` | narrow re-runs used to isolate the pain blocker |
| `C:/tmp/wolfmed-plan/wp/WP9-smoke.log` | `PrototypeSaveTest \| EntityTest \| DockTest` |
| `C:/tmp/wolfmed-plan/wp/WP9-{server,client,tests}-build.txt` | the three build checkpoints |
