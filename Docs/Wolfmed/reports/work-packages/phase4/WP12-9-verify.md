# WP12-9 — Verification (Tests, P4-8)

Verifier pass over `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(**WG**) against `PLAN4.md` §WP12-9/§1/§3/§5/§8, `DECISIONS.md` phase-4 sections (incl. §8.4), and
`WP12-9-report.md`. No file touched except this one and the two snapshot files.

**Verdict: PASS.**

---

## 1. Builds

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  -> Build succeeded. 0 Error(s)
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  -> Build succeeded. 0 Error(s)
```

Both green, matching the report.

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 23 modified tracked files (the full accumulated phase-4 diff — WP12-0 through WP12-9, nothing
committed between packages). Every file not under `_Onyx`/`_WF` was read in full diff and checked against
PLAN4 §3's authorised hook list:

| File | Hook | Matches §3? |
|---|---|---|
| `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` (+`.xaml.cs`) | HOOK 26 | Yes — xmlns, `Name="WolfmedDamageGroupsPanel"`, panel element, `PopulateWolfmed(msg)` as first statement of `Populate`, `HideWolfmed()` in the early-return block. Bodies live in `Content.Client/_WF/Wolfmed/Medical/HealthAnalyzerWindow.Wolfmed.cs` (confirmed present, defines both methods) |
| `Content.Server/EntityEffects/Effects/HealthChange.cs` | HOOK 9(a) | Yes — `using`, `TreatmentCapabilities` field, call converted to `Apply()` delegate + `WithTreatmentCapabilities` branch; the Shitmed/Mono `partMultiplier: 1.00f, // Mono, 0.5f->1.00f` block survives byte-for-byte; no redundant `System.Linq` (already imported, per plan) |
| `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` | HOOK 9(b) | Yes — same shape, plus the required new `using System.Linq;` |
| `Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs` | HOOK 22 | Yes — 1 `using`, 1 `[Dependency]`, 1 `if (!_wolfmedExplosion.TryApplyExplosionDamage(...))` guarding the unchanged existing call |
| `Content.Server/Medical/HealthAnalyzerSystem.cs` | HOOK 23 | Yes — 1 appended line, no new `using`/`[Dependency]` upstream |
| `Content.Shared/Gibbing/Systems/GibbingSystem.cs` | pre-authorised Gibbing fix | Yes — `using System.Linq;` + two `.ContainedEntities.ToArray()` snapshots, both marked `// WOLFGATE` |
| `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` | EXT 2 | Yes — 4 fields, 4 trailing optional ctor params, 2 `using`s, all marked |
| `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` | EXT 1 | Yes — 3 datafields, purely additive |
| `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` | HOOK 24 + HOOK 25 | Yes — 2 lines each, at the specified sites, bodies confirmed in `Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs` |
| `Resources/.../healing.yml` (Tourniquet) | PROTO D | Yes — `Healing` block replaced in place with `Tourniquet`; no `Tourniquet` tag added (matches the D9 "leave `tags:` alone" instruction) |
| `Resources/.../firstaidkits.yml` | PROTO E, **withdrawn by WP12-9** | Yes — net diff is a 7-line marked comment, no fill line added (deviation 2, matches report) |
| `Resources/.../alcohol.yml`, `medicine.yml`, `narcotics.yml`, `_Goobstation/.../medicine.yml` | PROTO K/I/J/H | Yes — one `SuppressPain`/`MendFractures` block each, each in the correct metabolism group (spot-checked Desoxyephedrine's block is under `Narcotic:`, not `Poison:`, as PLAN4 requires) |
| `Resources/.../surgeries.yml` | PROTO F | Yes — `maxWoundSeverity: 99.99` on both tend-wounds surgeries, `woundGroup: Burn` on the Burn one |
| `Resources/.../surgery_steps.yml` | PROTO G | Yes — exactly the 4 authorised sites (`SurgeryStepOpenIncisionScalpel`, `SurgeryStepClampBleeders`, `SurgeryStepCloseIncision`, `SurgeryStepSealTendWound`); confirmed `SurgeryStepCarefulIncisionScalpel` (line 309) is untouched (§8.5 risk 15 avoided) |
| `Resources/Locale/.../health-analyzer-component.ftl` | LOC A | Yes — one marked block, 22 lines |

Every changed line outside `_Onyx`/`_WF` carries a `// WOLFGATE` (or `<!-- WOLFGATE -->`) marker citing a hook
id, and every hook body is one or two lines with the implementation living in an `_WF` partial or a new `_WF`
file. No edit outside PLAN4 §3's list was found.

**D2 spot check:** HOOK 9's guard is `if (change...Any(<0) && HasComponent<WoundHostComponent>(target)) WithTreatmentCapabilities(...) else Apply()` — non-hosts and non-healing calls fall straight to unmodified `Apply()`. HOOK 22 falls through to the unchanged `_damageableSystem.TryChangeDamage` call for non-hosts. Both are exercised directly by this WP's own tests (T-REAGENT-CAP-NO, T-EXPLOSION-WRAPPER's "otherwise" half, T-EXPLOSION-PLATE's control body) and all pass.

## 3. Vendoring fidelity

WP12-9 itself changes no file under `_Onyx/` outside the test project. Its two modified `_Onyx`-pathed test
files were diffed with `--strip-trailing-cr` against `git -C C:/tmp/onyx show HEAD:<path>` (Onyx pin
`2f5bab9`, confirmed via `git -C C:/tmp/onyx log -1`):

- `WoundBleedingTest.cs` — the WP12-9-added hunk (restored `TourniquetStopsOnlySelectedPartTest`, replacing
  the phase-1 skip note) carries `// WOLFGATE` on every point of departure from Onyx: `Apply` called directly
  (trap 9), the D9 Chest→Torso substitution, severity 10 vs Onyx's 1/3 (`SlashWound.minimumSeverity: 9`), and
  the added second-`Apply`-returns-`false` assertion. Matches the report exactly. (The rest of the file's diff
  against Onyx is inherited from phases 1–3's D9/D13/D8 rework, out of this WP's scope.)
- `AmputationConsequenceTest.cs` — the WP12-9-added hunk (restored `SurgicalHealRemovesConsequenceAndUnblocksTest`
  + rewritten `<remarks>`) is fully marked, and correctly substitutes a `SurgeryValidEvent`-on-the-surgery-singleton
  assertion for Onyx's `TryAttachPart Is.False` per P4-D18/HOOK 25, with an explicit comment explaining why.

`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` (touched by WP12-8, not WP12-9) was inspected for
completeness: its P4-D14 hunk (new `TryApplyDistributedDamage` wrapper + `ApplyDistributedDamageCore` split,
the `originFlag` parameter threaded through `TryRouteDistributedDamage`) is entirely `// WOLFGATE`-commented
and consistent with §3.3's authorisation for that file.

## 4. Collisions

- **Subscriptions:** grepped all 8 new/modified WP12-9 test files for `SubscribeLocalEvent`/`RegisterComponent`
  — zero hits. Matches the report's "zero directed subscriptions and zero components registered by this
  package."
- **`[TestPrototypes]` ids:** extracted all 25 new ids across the 5 new test files (5 + 3 + 14 + 2 + 1) and
  grepped each (`id: <name>$`) across the whole tree excluding RobustToolbox — every one returns exactly 1 hit
  (its own definition). `WolfmedReattachTest.cs`'s two new tests add no new ids; they reuse
  `WolfmedAmputationBody` and `WolfmedStepHealAmputation` from the global pool, as the report claims.
- **Fixture class names:** grepped `WolfmedMedicalPatchTest`, `WolfmedReagentTreatmentTest`,
  `WolfmedWoundSurgeryTest`, `WolfmedAnalyzerTest`, `WolfmedExplosionTest` repo-wide — each appears exactly
  once (its own file). Minor note: the report says "six new fixture class names," but only 5 new `[TestFixture]`
  classes exist; not a defect (no collision either way), just a report miscount — **minor**.
- **Component/entity-effect names:** WP12-9 registers no new `[RegisterComponent]` types and defines no new
  `EntityEffect` classes; all `- type:` component usages inside its `[TestPrototypes]` blocks resolve to
  types already registered by earlier phase-4 packages (WP12-0's `MedicalPatch`, WP12-4's
  `WolfmedSurgery*Effect` family, `WoundHost`, etc.) — none are new.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-9 (phase 4 — tests, P4-8)` section (line 1844) with one
row per file in the report's table 1 (all 10 files: the 5 new test files, the 3 modified test files, the
withdrawn-PROTO-E `firstaidkits.yml` row, and the manifest's own row), plus subscription/component/type-name
summary lines ("none" / "none" as expected) and the run-command results. Diff stat confirms +749/−0
(purely additive), matching the report.

## 6. Plan conformance

All 8 files in PLAN4's WP12-9 table exist with the right status (new/modified) and test coverage. Spot-checked
DECISIONS §8.4 answers are honoured tree-wide (not just by WP12-9, since packages run sequentially
uncommitted):

- §8.4-1 Explosion amputation shipped (WP12-8's `WolfmedExplosionSystem` + HOOK 22), gated by this WP's
  T-EXPLOSION-PLATE/-WRAPPER, both passing.
- §8.4-2 Organ heal `amount: 3` — confirmed on all 7 `WolfmedSurgeryOrganHealEffect` steps in
  `Resources/Prototypes/_WF/Wolfmed/Surgery/surgery_steps.yml`, each with a `// WOLFGATE (P4 balance)` comment.
- §8.4-3 Four-prototype scarring chain shipped exactly as revised (PROTO G, checked above); T-SURG-SCAR passes.
- §8.4-4 Tier A reagents present (`Osteogen`/`Ibuprofen`/`Ketorolac`/`Tramadol`/`Oxycodone` all resolve in
  `_Onyx/Reagents/Medicine/medicine.yml`); T-REAGENT-PROTOTYPE-SANITY passes.
- §8.4-5 PARALLEL analyzer UI shipped (`WolfmedDiagnosticPanel` + HOOK 26, 5 marked upstream lines as
  specified).
- §8.4-6/-7/-8: no organ-examine line added (`Content.Server/HealthExaminable` has no organ reference), no
  `treatmentCapabilities` on the cable coil, no `PainNumbness`/`ModifyStatusEffect` chain added — all three
  "no" answers honoured.
- Gibbing fix present and marked (checked in §2 above).

Deviations 1–10 and the "three corrected assertions" / "two findings recorded" in the report were all
spot-checked against the actual test code (T-PATCH split in two, T-REAGENT-SYSTEMIC-BYPASS uses `Poison`,
T-SURGERY-PROTOTYPE-SANITY asserts clampability with an explicit in-code comment explaining the measured
correction against PLAN4's literal wording) and are accurately described.

## 7. Snapshot

```
git -C WG diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p4/snapshots/WP12-9.patch      (1755 lines)
git -C WG ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p4/snapshots/WP12-9.untracked.txt   (61 lines)
```

Both written. Untracked list matches the expected accumulation of new files from WP12-0/1/4/5/6/7/8/9 (medical
patch, tourniquet, entity effects, surgery components, analyzer payload/client, explosion system, surgery
prototypes/textures/locale) — nothing unexplained.

## 8. Test log verification

Read `WP12-9-report-tests.log`, `-smoke.log`, `-lint.log`, `-server.log` (the four logs the report cites).

- **Combined gate filter:** `NUnit3TestExecutor discovered 98 of 98 NUnit test cases`, ends
  `Test Run Successful. Total tests: 98 / Passed: 98`. Zero `Failed`/`Skipped` lines anywhere in the file.
  Extracted all 98 `Passed <name>` lines; every one of the 33 test methods the report claims as new/restored
  for this WP appears in that list, including `T-AN-CLEARS`'s `TreatmentDisappearsFromTheReadoutTest` (not
  `[Ignore]`d — correct, since WP12-5 has landed) and both `T-SURG-ORGAN` methods
  (`SurgeryHealOrganRestoresHealthTest` + `SurgeryHealOrganRestoresGrantedComponentsTest`).
- **Smoke filter:** `Total tests: 11 / Passed: 9 / Skipped: 2`; the two skips are
  `SpawnAndDeleteEntityCountTest` and `SpawnAndDirtyAllEntities` — exactly the two permanently-`[Ignore]`d
  tests DECISIONS/manifest record as legitimate, matching the report's "9 passed, 0 failed" claim.
- **Release lint:** exactly 1 `::error`, on `medical_patch.yml`'s missing `icon:` — the pre-existing,
  already-recorded WP12-0/2 hazard, unowned by WP12-9. No new lint error.
- **Headless server (120 s, port 1299):** reaches `Server Version 277.0.0.0 -> Ready`; 0 case-insensitive hits
  for `ERRO`/`FATL`/`Exception`.

No failure to report — every test the report lists as passing did pass, with matching assertion-free logs.

---

## Issues found

- **Minor:** the report's prose says "six new fixture class names grepped repo-wide (0 hits)"; only 5 new
  `[TestFixture]` classes were added (`WolfmedMedicalPatchTest`, `WolfmedReagentTreatmentTest`,
  `WolfmedWoundSurgeryTest`, `WolfmedAnalyzerTest`, `WolfmedExplosionTest`). No collision either way — cosmetic
  miscount in the report text, not a code defect. Does not affect the verdict.

No blockers or majors found. Build, upstream-hook discipline, D2 gating, vendoring fidelity, prototype/name
collision freedom, manifest completeness, plan conformance, and every logged test result all check out.
