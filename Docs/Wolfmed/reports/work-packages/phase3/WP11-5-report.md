# WP11-5 — Amputation and organ tests (PLAN3 §4 / §6, P3-5)

Worktree WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(branch `clanker/wolfmed-port-orchestration-454c3d`). ONYX = `C:/tmp/onyx` @ `2f5bab9`. No commits made;
nothing inside `WG/RobustToolbox` touched; **no production code changed by this package** (four test files
plus the manifest).

**Status: complete and green.** 17 tests, all passing. Three builds 0 errors, `DockTest` 3/3 first, the full
phase-1/2/3 wound gate **65/65**, smoke filter **Test Run Successful**.

---

## 1. Files created / modified

| # | Path (relative to WG) | Status |
|---|---|---|
| 1 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | **modified** — T-AMP-THRESHOLD restored, the phase-1 skip note deleted |
| 2 | `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` | **new** — 3 of Onyx's 5 tests, D8/D9-translated |
| 3 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAmputationTest.cs` | **new** — 5 tests |
| 4 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedOrganTest.cs` | **new** — 8 tests |
| 5 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 4 master-table rows + a `### WP11-5` deviations block |

Test count: 1 restored + 3 ported + 5 new amputation + 8 new organ = **17**, exactly PLAN3 §4/WP11-5's
"17 tests across 4 files (1 restored, 3 ported, 5 new amputation, 8 new organ)".

Files deliberately **not** touched: `WoundDamageFoundationTest.cs` (serialisation rule 2 — WP11-3 owns it),
every `_Onyx`/`_WF` production file, every `Resources/` prototype, locale and texture. **No new directed
subscription** (PLAN3 §5.1 says WP11-5 registers none, and it registers none). No `[TestPrototypes]` id
collides with §4 rule 3's pool — all 13 new ids were grepped clear across the suite before use.

New `[TestPrototypes]` ids claimed by this package:
`AmputationConsequenceTestGraph/Body/Torso/Head`; `WolfmedAmputationBodyGraph/Body/Torso`,
`WolfmedAmputationOverflowGraph/Body/Part`; `WolfmedOrganTestProfile`,
`WolfmedOrganTestGraph/Body/Torso/Organ`, `WolfmedOrganControlBody`, `WolfmedOrganFuncGraph/Body/Organ`.

Logs: `WP11-5-report-docktest.log`, `WP11-5-report-amp.log` (first run, one failure — §3 D-1),
`WP11-5-report-amp2.log`, `WP11-5-report-tests.log`, `WP11-5-report-smoke.log`.

---

## 2. Every `WOLFGATE` edit and its reason

These are test files, so the `// WOLFGATE` convention is used for (a) each deliberate divergence from the
Onyx source and (b) each derived expected value, per P2-D16 / PLAN3 §6.1 trap 6.

### `WoundBleedingTest.cs` (T-AMP-THRESHOLD)

| Site | Edit | Reason |
|---|---|---|
| the 4-line skip note at `:150-153` | deleted, replaced by the restored test | D26 is lifted; WP11-1 vendored `AmputationSystem`, so `Severable` can now be set |
| `BodyPartType.Chest` | `BodyPartType.Torso` | D9 |
| `BleedAmount Is.GreaterThanOrEqualTo(40f)` | `Is.EqualTo(bloodstream.MaxBleedAmount)` (10f) | **P3-D14.** `DismembermentWound` at Head severity 200, `rate: 0.2`, `awakeMultiplier: 1.5` → raw 60, clamped by `BloodstreamSystem` to `BloodstreamComponent.MaxBleedAmount`; `WoundBleedingBody` inherits `MobBloodstream` and never raises it. The contract asserted is "a traumatic amputation bleeds as hard as this body can" |
| — (added) | `DismembermentWound` severity **200** (`DismembermentSeverities[Head]`), `AmputationConsequenceWound` severity **35** (`WolfmedBodyPartComponent` default on `BaseTorso`), `Deleted(head) Is.False` + `Transform(head).ParentUid != body` | PLAN3 §6.2's row asks for all four; the "live entity" pair is also what distinguishes an amputation from a gib |

### `AmputationConsequenceTest.cs` (T-AMP-CONSEQUENCE-1/-2/-3)

| Site | Edit | Reason |
|---|---|---|
| fixture | Onyx's `InitialBody` + bespoke part entities → a `- type: body` graph with parts inheriting `TorsoHuman`/`HeadHuman` | PLAN3 §6.1 trap 9 — WG is Shitmed-shaped, and inheriting the real parts gives the fixture real `Woundable`/`Damageable`/`vital` data |
| fixture | `- type: transplantCompatibility`, `- type: bodyPartProfile`, `TransplantCompatibility`, `AmputationConsequenceTestHeal` dropped | Nubody / Onyx-surgery only (D7, D19) |
| fixture | `partType: Chest` → a `TorsoHuman`-derived part | D9 |
| fixture | part-level `amputationThresholds: {Slash: 70}` → `- type: WolfmedBodyPart` | D8 |
| fixture | **added** `amputationConsequenceSeverity: 50` on the torso | PLAN3 §4/WP11-5's mandatory line; CRITIQUE3 M3-2 / §8.7 hazard 7 |
| test 1 | asserts severity **50**, not Onyx's 35 | the fixture value above; 35 cannot tell the parent from the severed part |
| test 1 | `graph.HasAmputationConsequence(torso)` and `graph.TryAttachPart(torso, spare) Is.False` **dropped** | B-2/P3-D2 — Shitmed's `CanAttachPart` has no consequence gate and phase 3 deliberately does not add one |
| file | `SurgicalHealRemovesConsequenceAndUnblocks` and `HealingDamageKeepsConsequenceBlocked` recorded as skips in the class `<remarks>` | D7 (needs `SurgeryStepEvent`/`SurgeryTreatWoundEffect`) and B-2/P3-D2 respectively |
| tests 2 and 3 | ported verbatim in shape; `DamageableSystem` reads → `WolfmedDamageableSystem` | D12 (`GetAllDamage`/`SetDamage` live on the compat facade) |

### `WolfmedAmputationTest.cs` and `WolfmedOrganTest.cs`

New Wolfgate test files, so no vendored-file marker convention applies; every expected value nevertheless
carries its derivation at the assertion. The two structural notes worth calling out:

* `WolfmedAmputationTorso` carries `amputationConsequenceSeverity: 50` for the same reason as above, so
  T-AMP-CONSEQUENCE-SEPARATE also proves the parent-vs-part reading (CRITIQUE3 M3-2 asks for both).
* `WolfmedOrganTestTorso` declares `- type: Woundable  profile: WolfmedOrganTestProfile` **statically**.
  `WoundableComponent` is normally `EnsureComp`'d by `WoundDamageProjectionSystem.SetupPart` with
  `Profile = "OrganicBodyPartProfile"`; declaring it on the prototype is the only way to attach a bespoke
  profile, because `EnsureComp` leaves an existing component alone. That profile forces
  `organDamage.chances.Torso: 1` and `maxAffected: 1`, which is what makes T-ORG-CAP deterministic against a
  shipped 4 % roll.

---

## 3. Deviations from PLAN3

| # | Deviation | Justification |
|---|---|---|
| **D-1** | **T-AMP-NOGUN is not written; T-AMP-GUN replaces it.** | DECISIONS.md §8.6-1 (later and authoritative) and the task brief. WP11-1 already shipped the balance data; `BulletsNeverAmputateTest` would fail by construction. T-AMP-GUN asserts the four cases the decision names: a hand over its Piercing threshold severed by **one** 14-Piercing round (hit 16), over its Heat threshold by **one** 16-Heat shot (hit 14), a below-threshold foot severed by neither (5 × 14 Piercing = 70/220; 3 × 16 Heat = 48/220), and the deliberately untouched melee case (6 × Slash 32 on an arm, matching PLAN3 §8.2's machete row). Counts agree with WP11-1's independent live measurement. |
| **D-2** | **T-AMP-OVERFLOW (a) drives the HEAD, not an arm** (16 × Blunt 25, peak 450). | PLAN3/CRITIQUE3 M3-4 assume a limb can be driven far past its amputation threshold. It cannot. Shitmed's `Destructible` **gibs** a part from `DamageChangedEvent` — inside `TryChangeDamage`, before `PartDamageAppliedEvent` reaches `AmputationSystem`. `Resources/Prototypes/Body/Parts/base.yml`: `MajorLimb` (arms/legs) Blunt **190** / Slash **210** / Heat 250 (`:280-297`); `MinorLimb` (hands/feet) Blunt **150** / Slash **180** / Heat 230 (`:312-338`); `BaseHead` Blunt **500** / Slash **600** / Heat **700** (`:105-122`). The arm's Blunt amputation threshold is 250 but it gibs at 190, so PLAN3's setup is unreachable — the first run failed there with a real exception (§5). CRITIQUE3's load-bearing 25/50 chunk sizes are kept; only the part moved. |
| **D-3** | **T-AMP-EXPLOSION uses `Spec("Piercing", 500)`, not PLAN3's `Spec("Slash", 260)`.** | Same cause. Slash 260 exceeds `MajorLimb`'s Slash gib at 210, so the arm would be **destroyed** and the test would have passed for the wrong reason (it did pass on the first run — that is exactly the silent-green failure mode this deviation removes). **Piercing has no `Destructible` trigger on any body part in the game**, and 500 against the arm's Piercing threshold of 250 preserves PLAN3's intended `clamp(progress × 0.5, 0, 1) = 1.0` saturation and `IsFinishingHit` (500 ≥ 12). The test now also asserts the arm is **not deleted** and that a `DismembermentWound` at severity **120** landed on the stump; T-AMP-GUN carries the same not-gibbed assertion on all three of its severed parts. |
| **D-4** | **T-AMP-VITAL (a) asserts `after == before + 115`, where PLAN3's row says "+100 exactly".** | Both halves of PLAN3's own derivation are asserted, and the row's own worked example ("Head at 215 Slash → systemic 215 + 100 = 315 where `CheckVitalDamage` read 215") is what is measured. The `+100` phrasing omits the finishing hit itself: `before` is read *before* the decapitating hit, so the movement is that hit's 15 plus Shitmed's flat `VitalDamage` 100. The contract PLAN3 actually wants — `after >= before`, i.e. decapitation never reduces the readout — is asserted separately and first, and systemic `Bloodloss == 315` pins the two charges exactly. |
| **D-5** | **No headless server run and no Release YAML lint.** | This package adds no file under `Resources/`. Its prototypes are `[TestPrototypes]` strings compiled into the test assembly, which neither `Content.Server.exe` nor `Content.YAMLLinter` ever loads — the integration-test run *is* their validation, and it loaded and spawned every one of them. `git diff --stat` confirms WP11-5 touched only the four test files and the manifest. |

Everything else follows PLAN3 literally: P3-D10 (SeparateInstances, replacing `tests.md`'s merge test),
P3-D14 (the clamped bleed literal), P3-D22 (the brain survives), P3-D23's guards are exercised rather than
weakened, D2 regression guards in both T-AMP-VITAL (b) and T-ORG-INERT, `Assert.Multiple` bodies fully
synchronous (trap 3), `DockTest` first (trap 1), and destruction driven through `SetHealth` rather than the
~1–4 % damage roll (PLAN3 §6.2's own instruction).

---

## 4. Real failure found and fixed, plus one upstream bug flagged

**The failure (fixed):** `OverflowAmputationMechanismIsInertOnShippedLimbsTest` failed on the first run with

```
System.InvalidOperationException : Collection was modified; enumeration operation may not execute.
  at Content.Shared.Gibbing.Systems.GibbingSystem.TryGibEntityWithRef(...) GibbingSystem.cs:line 141
  at Content.Shared.Body.Systems.SharedBodySystem.GibPart(...) SharedBodySystem.Body.cs:line 404
  at Content.Server.Destructible.Thresholds.Behaviors.GibPartBehavior.Execute(...)
  at Content.Server.Destructible.DestructibleSystem.Execute(...)
  at Content.Shared.Damage.DamageableSystem.DamageChanged(...)
  at Content.Shared._WF.Wolfmed.Compat.WolfmedDamageableSystem.ChangeDamage(...)
  at Content.Shared._Onyx.Wounds.WoundDamageRoutingSystem.RouteAppliedDamage(...) :772
```

Driving a `MobHuman` arm to 200 Blunt crossed `MajorLimb`'s `Destructible` gib threshold (Blunt 190), and
gibbing an arm that still contains its hand throws. Fixed in the test by D-2/D-3 above (the mechanism under
test is amputation, not destruction) and documented in-file so nobody re-writes the unreachable setup.

**The upstream bug (NOT fixed, flagged):** `Content.Shared/Gibbing/Systems/GibbingSystem.cs:135-146` —
`TryGibEntityWithRef`'s `GibContentsOption.Drop` and `.Gib` branches iterate
`foreach (var ent in container.ContainedEntities)` while `DropEntity`/`GibEntity` remove entities from that
same container. Any body part that holds something (an arm holds its hand; a torso holds organs) throws when
it crosses a `Destructible` gib threshold. This is pre-existing upstream Wolfgate/Wizden code that phase 3
does not touch and PLAN3 §3 does not authorise editing; Wolfmed only makes it *more reachable*, because
routing concentrates damage on a single part instead of spreading it. **The fix is one `.ToArray()` (or a
copied list) on each of the two `foreach`es.** Left to the user / a later package rather than patched from a
test work package.

---

## 5. Build / test output tails

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

`DockTest` first (project memory — `db.ef` sqlite warnings fail pair tests), `WP11-5-report-docktest.log`:

```
Test Run Successful.
Total tests: 3
     Passed: 3
```

Full phase-1/2/3 wound gate,
`--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"`
(a superset of the task's `_Onyx.Wounds|Wolfmed`), `WP11-5-report-tests.log`:

```
  Passed PartDamageProjectsToVisualsComponentTest [123 ms]

Test Run Successful.
Total tests: 65
     Passed: 65
 Total time: 2.4085 Minutes
```

65 = WP11-3's 48 + this package's 17. The 22 `db.ef … admin_notes` lines in the log are the environmental
warning project memory documents; no re-run was needed.

Smoke filter `--filter "FullyQualifiedName~EntityTest|FullyQualifiedName~PrototypeSaveTest|FullyQualifiedName~DockTest"`,
`WP11-5-report-smoke.log`:

```
Test Run Successful.
Total tests: 11
     Passed: 9
    Skipped: 2
 Total time: 7.0608 Minutes
```

The two skips are `SpawnAndDeleteEntityCountTest` and `SpawnAndDirtyAllEntities`, both pre-existing
`[Ignore]`d tests unrelated to Wolfmed. `AllComponentsOneToOneDeleteTest`,
`SpawnAndDeleteAllEntitiesInTheSameSpot`, `SpawnAndDeleteAllEntitiesOnDifferentMaps`, `UninitializedSaveTest`
and `AllItemsHaveSpritesTest` all passed, which is the strongest available check that this package's 13 new
`[TestPrototypes]` are well-formed.

Per-test roll-up of the 17 (from `WP11-5-report-amp2.log` and `WP11-5-report-tests.log`):

```
Passed TraumaticAmputationCreatesSevereStumpBleedingTest
Passed TraumaticAmputationCreatesConsequenceTest
Passed HealingPartAboveThresholdDoesNotAmputateTest
Passed HealingBelowResetRatioClearsSeverableTest
Passed DecapitationNeverReducesVitalDamageTest
Passed GunsAndLasersAmputateOverThresholdLimbsTest
Passed OverflowAmputationMechanismIsInertOnShippedLimbsTest
Passed ExplosionAmputatesDeterministicallyTest
Passed RepeatedAmputationCreatesSeparateConsequenceWoundsTest
Passed OrganPrototypesCarryWolfmedDataTest
Passed OrganDamageIsCappedPerApplicationTest
Passed OrganDestructionMergesConsequenceWoundTest
Passed DestroyedHeartAppliesDelayedDeathTest
Passed DestroyedBrainKillsWithoutDeletingOrganTest
Passed DestroyedEyesBlindTest
Passed ZeroHealthOrganRevokesGrantedComponentsTest
Passed NonWoundHostTakesNoOrganDamageTest
```

All eight organ tests passed on the first run — WP11-2's measured prototype table (`OrganHuman*` health
15/15, hit chances, selection weights, multipliers, destruction wounds and severities) is reproduced exactly
by T-ORG-DATA, and P3-D8's whole "Shitmed already does the consequences" argument is now pinned by
T-ORG-HEART (`DelayedDeathComponent`), T-ORG-EYES (`TemporaryBlindnessComponent`) and T-ORG-BRAIN.

---

## 6. What later packages must know

1. **Shitmed's `Destructible` gib thresholds are now a first-class constraint on every amputation number,
   and PLAN3 never accounted for them.** Gibbing fires from `DamageChangedEvent`, i.e. *before*
   `AmputationSystem` sees the hit, so the part is destroyed rather than severed and no dismemberment or
   consequence wound is ever created. The live picture on shipped parts:

   | Part family | Blunt gib | Slash gib | Heat gib | Piercing gib | Amputation thresholds |
   |---|---|---|---|---|---|
   | `MajorLimb` (arm, leg) | **190** | **210** | 250 | none | Slash 130/150, Piercing 250, Blunt 250/300, Heat 250 |
   | `MinorLimb` (hand, foot) | **150** | **180** | 230 | none | Slash 70/80, Piercing 200/220, Blunt 150/170, Heat 200/220 |
   | `BaseHead` | 500 | 600 | 700 | none | Slash 200, Piercing 200, Blunt 350, Heat 200 |

   Reading straight off that table: **Blunt can only ever amputate the head** (every limb gibs first);
   **Slash amputation works on every part** (thresholds sit below the gib triggers); **Heat amputation is
   tight on hands and feet** (threshold 200/220 against a 230 gib — a laser stream severs at 224 with six
   points of headroom, which this package's T-AMP-GUN measures) and comfortable elsewhere; **Piercing is
   unconstrained** because no body part declares a Piercing `Destructible` trigger. Any future change to
   `_WF/Wolfmed/Body/parts.yml` thresholds, to `dismembermentFinishingDamage`, or to `Body/Parts/base.yml`'s
   gib numbers can silently turn an amputation into a gib. The balance pass should see this table.
2. **`GibbingSystem.cs:141` is a live crash** (§4). Worth a one-line fix in whatever package is allowed to
   touch `Content.Shared/Gibbing`.
3. **The two `amputationConsequenceSeverity: 50` fixture lines are load-bearing** (`AmputationConsequenceTestTorso`
   and `WolfmedAmputationTorso`). Remove either and the parent-vs-part bug PLAN3 §8.7 hazard 7 describes ships
   silently green.
4. **`WoundBleedingTest.cs` is no longer skip-noted for amputation**, and its `WoundBleedingBody` fixture is now
   exercised by a test that detaches the head. A package that changes `BloodstreamComponent.MaxBleedAmount`,
   `DismembermentWound`'s rate, or `DismembermentSeverities[Head]` changes that test.
5. **Test-file ownership after this package:** `WoundBleedingTest.cs`, `AmputationConsequenceTest.cs`,
   `WolfmedAmputationTest.cs` and `WolfmedOrganTest.cs` are WP11-5's. `WoundDamageFoundationTest.cs` is still
   WP11-3's and was not touched.
6. **WP11-6** has 4 new master-table rows and a `### WP11-5` deviations block to reconcile, and should fold the
   gib-threshold table above plus the `GibbingSystem` bug into `WOLFMED_STATUS.md`'s known-gaps section. PLAN3
   §7.1's stale-row corrections (`:80`, `:85`, `:89`, `:91`) are still outstanding from WP11-1/WP11-2.
7. **The wound suite is 65 tests and takes ~2.4 minutes** with a warm pool; the smoke filter takes ~7.
