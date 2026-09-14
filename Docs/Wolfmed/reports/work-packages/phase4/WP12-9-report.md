# WP12-9 — Tests (PLAN4 §4 WP12-9 / §6, P4-8)

Worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**),
Onyx pin `2f5bab9` at `C:/tmp/onyx`. Nothing committed. `WG/RobustToolbox` untouched. No
`git stash/clean/checkout --/reset/commit` at any point.

---

## 1. Files created / modified

| # | Path (WG-relative) | Status | Size |
|---|---|---|---|
| 1 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedMedicalPatchTest.cs` | **new** | 2 tests, 215 lines |
| 2 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReagentTreatmentTest.cs` | **new** | 7 tests, 470 lines |
| 3 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedWoundSurgeryTest.cs` | **new** | 9 tests, 620 lines |
| 4 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAnalyzerTest.cs` | **new** | 8 tests, 435 lines |
| 5 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedExplosionTest.cs` | **new** | 3 tests, 350 lines |
| 6 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | **modified** | +1 test (T-TOURNIQUET restored over the phase-1 skip note) |
| 7 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs` | **modified** | +2 tests (T-REATTACH-BLOCKED, T-SURG-AMP-CLEAN) |
| 8 | `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` | **modified** | +1 test (Onyx's `SurgicalHealRemovesConsequenceAndUnblocksTest` restored) + rewritten `<remarks>` |
| 9 | `Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` | **modified — PROTO E withdrawn** | −1 fill line, +7 marked comment lines |
| 10 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | `### WP12-9` appended, **+749 / −0, purely additive** |

**33 new tests** (31 new methods + 2 restored skips). The phase-4 wound suite goes from **65 to 98**.

Nothing else was touched. No new C# outside `Content.IntegrationTests`. No new locale key, sprite, RSI or
XAML. All test files are UTF-8/LF, matching the existing `_WF/Wolfmed` and `_Onyx/Wounds` test files;
`firstaidkits.yml` stays ASCII/CRLF.

**Test-to-plan mapping** (PLAN4 §6.2 ids, all 30 rows covered):

| PLAN4 id | Method | File |
|---|---|---|
| T-PATCH | `MedicalPatchInjectsOnScheduleAndStopsOnUnstickTest` + `SingleUseMedicalPatchSpawnsTrashAndDeletesItselfTest` | `WolfmedMedicalPatchTest` |
| T-REAGENT-CAP-YES / -CAP-NO / -SYSTEMIC-BYPASS / -SUPPRESS / -MEND / -STAM / -PROTOTYPE-SANITY | 7 methods | `WolfmedReagentTreatmentTest` |
| T-TOURNIQUET | `TourniquetStopsOnlySelectedPartTest` | `WoundBleedingTest` |
| T-SURG-BLEED / -FRACTURE / -INTERNAL / -AMPCONSEQ / -ORGAN / -WINDOW / -SCAR / -PAIN | 9 methods (organ is 2) | `WolfmedWoundSurgeryTest` |
| T-REATTACH-BLOCKED / T-SURG-AMP-CLEAN | 2 methods | `WolfmedReattachTest` |
| (Onyx) `SurgicalHealRemovesConsequenceAndUnblocks` | restored | `AmputationConsequenceTest` |
| T-AN-GATE / -FINDINGS / -CLEARS / -CLOT / -PAIN / -ORGANS / -CHEM / -VITAL | 8 methods | `WolfmedAnalyzerTest` |
| T-EXPLOSION-PLATE / T-EXPLOSION-WRAPPER / T-SURGERY-PROTOTYPE-SANITY | 3 methods | `WolfmedExplosionTest` |

---

## 2. Every WOLFGATE edit and its reason

**Outside the test project — exactly one site.**

1. **`Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml`, `MedkitAdvancedFilled` — PROTO E withdrawn.**
   WP12-3's single added line `- id: Tourniquet` is replaced by a 7-line `# WOLFGATE (PROTO E, P4-2, WITHDRAWN
   in WP12-9)` comment that records the failure, the cause, what still works, and what would have to change to
   re-add it. Reason: a **real production failure** (§3 deviation 2 below). No other line in the file was
   altered; this is the file WP12-3 already owned under §3.2, and the edit removes an edit rather than adding
   one.

**Inside the test project**, `// WOLFGATE` marks every place a ported/planned assertion had to change shape.
The load-bearing ones:

* `WoundBleedingTest.cs` — the restored tourniquet test carries `// WOLFGATE` on (a) `Apply` being called
  directly per §6.1 trap 9, (b) the D9 `Chest -> Torso` substitution, (c) severity 10 rather than Onyx's 1/3
  (SlashWound's bleeding behaviour has `minimumSeverity: 9`, the same correction the neighbouring clotting tests
  already carry), and (d) the extra second-`Apply`-returns-false assertion Onyx's own test never made.
* `AmputationConsequenceTest.cs` — `// WOLFGATE` on asserting the block through `SurgeryValidEvent` instead of
  Onyx's `TryAttachPart Is.False`, because P4-D18 deliberately leaves `SharedBodySystem.CanAttachPart` ungated.
* `WolfmedReattachTest.cs` — `CanAttachPart` is asserted to stay **true** throughout T-REATTACH-BLOCKED. That is
  the guard proving P4-D18 was not quietly reverted and that the two Mono prosthetics traits were not collateral
  damage.
* `WolfmedWoundSurgeryTest.cs` — `// WOLFGATE` on the 5-tuple `SurgeryStepEvent` (Onyx's is a 4-tuple), on the
  two-prototype bleeder fixture, and on the P4-D20 Hairline regression block.
* `WolfmedAnalyzerTest.cs` — `// WOLFGATE` on the T-AN-CLEARS scope correction and on creating all wounds before
  mutating any (`CreateOrMergeWound` ends in `WoundBleedingSystem.RefreshBody`, which recomputes `CurrentRate`
  for every bleeder on the body and would undo an earlier row).
* `WolfmedExplosionTest.cs` — `// WOLFGATE` on the clampability invariant replacing PLAN4's closability one, and
  on using Blunt rather than Piercing so a ~4-per-limb share can never reach the per-part finishing minimum and
  make the explosion-amputation roll flaky.
* `WolfmedMedicalPatchTest.cs` — a `<remarks>` block recording the un-unstickable-patch finding, plus
  `PoolSettings => PsDisconnected` with its reason.

**Audits run before writing:** six new fixture class names grepped repo-wide (0 hits); all 25 new
`[TestPrototypes]` ids grepped repo-wide (0 hits each) — they are a global pool, since
`PoolManager.Prototypes.cs` loads every `[TestPrototypes]` string in the assembly into every pair, which is also
why this package **reuses** `WolfmedAmputationBody`, `WolfmedOrganFuncBody`/`WolfmedOrganFuncOrgan` and
`AmputationConsequenceTestBody` instead of cloning them. **Zero directed subscriptions and zero components
registered** by this package.

---

## 3. Deviations from PLAN4, with justification

1. **T-PATCH is two tests.** `singleUse: true` deletes the patch inside `EntityUnstuckEvent`, making PLAN4's
   assertion (c) ("no further transfer after unsticking") vacuously true. A non-single-use prototype measures the
   Update loop actually stopping; a single-use one covers the trash swap.
2. **PROTO E withdrawn (a real production failure, fixed).** `MedkitAdvanced` inherits `Medkit`'s
   `grid: [0,0,3,1]` — 8 cells at `maxItemSize: Small` — and `MedkitAdvancedFilled`'s four existing entries fill
   it. Adding the tourniquet made every spawn log
   `[ERRO] system.storage: Tried to StorageFill tourniquet (…, Tourniquet) inside advanced first aid kit (…,
   MedkitAdvancedFilled) but can't. reason: No room!`, which failed
   `EntityTest.SpawnAndDeleteAllEntitiesInTheSameSpot` and `EntityTest.SpawnAndDeleteAllEntitiesOnDifferentMaps`.
   **This has been red since WP12-3**, which ran no integration tests. The line is withdrawn with a marked
   comment; the `Tourniquet` id itself is unchanged and still reaches players through its eight existing
   references. **Escalated, not decided here:** if the kit placement matters, the fix is a larger `grid:` on
   `MedkitAdvanced` in `Resources/Prototypes/Entities/Objects/Specific/Medical/medkits.yml` — a file no phase-4
   WP owns, so WP12-10 or the user should take it.
3. **T-PATCH runs on a disconnected pair.** Sticking then unsticking moves the patch between the target's
   `stickers_container` and the user's hands in one tick, and RT's **client** `ContainerSystem.HandleComponentState`
   trips `DebugTools.Assert(container.Contains(entity))` replicating it
   (`RobustToolbox/Robust.Client/GameObjects/EntitySystems/ContainerSystem.cs:206`). Vanilla `StickySystem`
   behaviour, shared by every sticky item, untouched by phase 4. `MedicalPatchSystem` is server-only (P4-D13) and
   the test reads no client state, so `PoolSettings => PsDisconnected` is the honest fix.
4. **T-AN-CLEARS asserts the fracture leaves the row, not the row leaving the dictionary.** The 75-Blunt hit that
   breaks the bone also leaves a `BluntWound` and part pain — both genuine findings, so `HasFindings` correctly
   keeps the row. The row-disappearance half is asserted where it really holds: detaching the head.
5. **T-SURGERY-PROTOTYPE-SANITY asserts clampability, not closability** (full reasoning in the manifest). PLAN4's
   wording fails on ~30 shipped Shitmed surgeries, WP12-5's own head organ heals included, because
   `SurgeryCloseIncision` is a separate surgery. The test instead asserts: every surgery that can open a
   `SurgicalIncisionWound` reaches a `Clamp` step; `SurgeryCloseIncision` carries `Close`; WP12-5's five
   incision-based wound surgeries close their own incision on `SurgeryStepSealTendWound`; and
   `SurgeryTendWoundsBrute`/`Burn` reach neither an opening nor a clamp step (CRITIQUE4 B1's exact shape).
6. **T-SURG-ORGAN is two tests** (health ladder, then the function-restore half in the one-tick window).
7. **T-REAGENT-SYSTEMIC-BYPASS uses `Poison`, not `Toxin`.** `Toxin` is a damage *group* in Wolfgate, not a type.
8. **T-SURG-BLEED uses `SlashWound` + `PiercingWound`.** Two `SlashWound`s would merge (`MergeByPrototype`).
9. **Bare single-component step prototypes** for the surgery effects, exactly as PLAN4 §6.2 asks; the shipped
   prototypes are covered by T-SURGERY-PROTOTYPE-SANITY instead.
10. **Not ported:** `HealthAnalyzerPartDamageTest.BuildsIsolatedPartSnapshotTest` (P4-D27),
    `ClassifiesDangerousBloodLevel` (client-side), Onyx's `WoundSurgeryTest`/`WoundSurgeryScarTest` as written.

**Three assertions corrected against measurement rather than prediction** (P2-D16), each documented inline:

* `PainSystem.SuppressPain`'s `DecayPerSecond` is a `FixedPoint2` — 40 over 30 s is stored as `1.33`, so decaying
  for the nominal 30 s leaves `0.1` behind. T-REAGENT-SUPPRESS decays 60 s and then asserts the original pain is
  back exactly.
* `wounds.bleeding_auto_stop_enabled` defaults to **true**, so a fresh bleeder already has an
  `AutomaticClottingAt` deadline and reads as `InProgress`; the `None` phase needs that field cleared explicitly.
* A stuck patch transfers `injectAmmountOnAttatch` **and** a full `transferAmount` before `updateTime` elapses at
  all, because `NextUpdate` starts at `TimeSpan.Zero` and `OnStuck` never seeds it (2u + 5u, not 2u).

**Two findings recorded, not fixed (neither is a port defect):**

* **A stuck medical patch cannot be unstuck, in Wolfgate or Onyx.** `OnStuck` adds `UnremoveableComponent`;
  `SharedInteractionSystem` cancels `ContainerGettingRemovedAttemptEvent` for it unconditionally
  (`Content.Shared/Interaction/SharedInteractionSystem.cs:213-216`), so `StickySystem.UnstickFromEntity` always
  fails — **including the call `MedicalPatchSystem.Update` makes itself when the patch empties**, leaving
  `singleUse`/`trashObject` unreachable in play. Onyx's copy cancels identically at the pin (`:216`), so WP12-0's
  vendoring is faithful. Balance-pass item.
* **A failing test in a `GameTest` fixture can be reported as `Skipped` with the run summary still reading
  `Test Run Successful`.** Hit three times here (dirty-disposed pair). **Treat any non-zero `Skipped:` count in a
  phase-4 log as a failure** until each is re-run individually; the only legitimate skips in this repo are
  `EntityTest.SpawnAndDeleteEntityCountTest` and `EntityTest.SpawnAndDirtyAllEntities`.

---

## 4. Build / test output tails

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

`DockTest` first (project memory):

```
Test Run Successful.
Total tests: 3
     Passed: 3
```

Phase-4 gate filter (`WP12-9-report-tests.log`):

```
$ dotnet test ... --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~_Onyx.Medical|FullyQualifiedName~Wolfmed"
Test Run Successful.
Total tests: 98
     Passed: 98
 Total time: 48.2274 Seconds
```

Smoke filter (`WP12-9-report-smoke.log`):

```
$ dotnet test ... --filter "FullyQualifiedName~EntityTest|FullyQualifiedName~PrototypeSaveTest|FullyQualifiedName~DockTest"
Test Run Successful.
Total tests: 11
     Passed: 9
    Skipped: 2       <- EntityTest.SpawnAndDeleteEntityCountTest / SpawnAndDirtyAllEntities,
                        both permanently [Ignore]d upstream ("Even wizden calls this test ass").
```

Headless server, 120 s, port 1299 (`WP12-9-report-server.log`) — run because this package edits a prototype file:

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
```

Release YAML lint (`WP12-9-report-lint.log`):

```
::error file=/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml,line=-1,col=-1::
  /Prototypes/_Onyx/.../medical_patch.yml(-1,-1)  File not found. (/Textures)
1 errors found in 82482 ms.
```

**Zero lint errors from this package.** The one error is WP12-0's standing missing-`icon:` hazard on the two
`- type: construction` prototypes in `medical_patch.yml`, on record since WP12-2 and still unowned.

HOOK 22 checklist grep (PLAN4 §6.2 pairs this with T-EXPLOSION-WRAPPER):

```
$ grep -n "_wolfmedExplosion.TryApplyExplosionDamage" Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs
474:  if (!_wolfmedExplosion.TryApplyExplosionDamage(entity, damage)) // WOLFGATE: HOOK 22 - ...
```

---

## 5. What later packages must know

**A. WP12-10 has two escalations from this package.**
1. **`MedkitAdvanced`'s storage grid.** PROTO E is withdrawn (§3 deviation 2) and the advanced medkit ships with
   no tourniquet. Re-adding it needs `grid: [0,0,3,2]` (or larger) on `MedkitAdvanced` in
   `Resources/Prototypes/Entities/Objects/Specific/Medical/medkits.yml`, which is outside §3's authorised file
   list — a user/orchestrator call, not mine. **If the guidebook's treatment checklist says the tourniquet is in
   the advanced medkit, that sentence must be corrected or the grid enlarged.** The tourniquet is still in the
   sec/gib vendors, security spawners, `job.yml` belts, `cmo_webbing.yml`, two `_NF` loot fills and `nfsdtec.yml`.
2. **The `medical_patch.yml` Release-lint error is still red** and now blocks a clean phase-4 lint on its own.
   One `icon: { sprite: _Onyx/Objects/Medical/medical_patch.rsi, state: MakeshiftPatch }` per construction
   prototype fixes it.

**B. Read phase-4 test logs for `Skipped:`, not just for `Failed:`.** A dirty-disposed pair turns a genuine
assertion failure into a `Skipped` line under a green `Test Run Successful` summary. Three of this package's
tests hid real failures that way before being re-run individually.

**C. The suite is 98 tests and the whole of it is the gate.** `dotnet test --filter
"FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~_Onyx.Medical|FullyQualifiedName~Wolfmed"`,
**98/98, 0 skipped**, plus `EntityTest|PrototypeSaveTest|DockTest` at **9/9 + 2 permanent ignores**. Report both
numbers in `WOLFMED_STATUS.md` as phases 1-3 did.

**D. `[TestPrototypes]` is one global pool.** Every `[TestPrototypes]` string in the assembly is loaded into
every pair (`PoolManager.Prototypes.cs`), so a new id anywhere must be grepped repo-wide, and fixtures from other
files can be reused directly — this package spawns `WolfmedAmputationBody`, `WolfmedOrganFuncBody` and
`AmputationConsequenceTestBody` from other fixtures on purpose. 25 new ids were added; they are listed in the
manifest.

**E. What is now pinned and will break loudly if changed.** HOOK 9's capability scope (T-REAGENT-CAP-NO is the
only thing that can detect its loss); HOOK 22's wrapper contract in both directions (T-EXPLOSION-WRAPPER);
HOOK 24's severity window and its D2 fall-through (T-SURG-WINDOW); HOOK 25's block *and* the deliberate
non-hooking of `SharedBodySystem.CanAttachPart` (T-REATTACH-BLOCKED asserts `CanAttachPart` stays `true`);
P4-D20's Hairline reachability rule (T-SURG-FRACTURE); P4-D21's four-prototype chain end to end (T-SURG-SCAR);
P4-D23's `amount: 3` (T-SURG-ORGAN); the reagent group mapping including Desoxyephedrine's `Narcotic` placement
(T-REAGENT-PROTOTYPE-SANITY); and WP12-8's origin-flag passthrough (T-EXPLOSION-PLATE, which lands **exactly
zero** damage on a plated wound host and 40 on an unplated one).

**F. Phase 5 hooks to remember.** `BuildOrganInfo` returns an **empty list** for a wound host with no
`WolfmedOrgan` organs and **null** for a non-host — T-AN-ORGANS asserts both, so widening organ annotation in
phase 5 must keep that distinction. `WolfmedDiagnosticPanel.IsDangerousBloodLevel` is still untested (client
harness); the four server builders remain the intended assertion point.

**G. No file under `Content.IntegrationTests` is claimed by any later phase-4 package**, and the only upstream
file this package touched (`firstaidkits.yml`) is released.
