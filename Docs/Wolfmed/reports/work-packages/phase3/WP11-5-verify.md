# WP11-5 verify — Amputation and organ tests (P3-5)

**Verdict: PASS.** No blockers, no majors. Two trivial documentation nits noted (not blocking). Checks are
against the live worktree state, which also carries WP11-1/2/3/4's uncommitted work per the sequential
no-commit model (each already independently PASSed its own verify — not re-audited here beyond a
consistency spot-check and a full DECISIONS.md §8.6 sweep, since this WP's own report leans on those
packages' balance data).

---

## 1. Build (0 errors each)

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo -> Build succeeded. 0 Error(s)
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo -> Build succeeded. 0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows the cumulative phase-3 diff (13 tracked files). Attributing each to its owning WP:

| File | Owning WP |
|---|---|
| `Content.Client/Damage/DamageVisualsSystem.cs` | WP11-4 (HOOK 20) |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | **WP11-5** |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | WP11-3 |
| `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` | WP11-2 |
| `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` | WP11-1 |
| `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | WP11-1 (P3-D1) |
| `Content.Shared/Body/Part/BodyPartComponent.cs` | WP11-4 (HOOK 21) |
| `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | WP11-3 |
| `Resources/Prototypes/Body/Organs/human.yml` | WP11-2 (PROTO A) |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | WP11-4 (PROTO C) |
| `.../bulletproof_helmets.yml`, `.../bulletproof_vests.yml` | WP11-3 (PROTO B) |
| `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` | WP11-1 (balance data) |

**WP11-5's own tracked edit is exactly one file: `WoundBleedingTest.cs`**, under `Content.IntegrationTests/Tests/_Onyx/Wounds`
— a test file, not upstream production code, and not in the "not under `_Onyx`/`_WF`" category the
upstream-discipline rule targets. Diffed line-for-line against the report's §2 table: the `:150-153` skip
note is deleted and replaced by the restored `TraumaticAmputationCreatesSevereStumpBleedingTest`; every
divergence from Onyx's own version (`ONYX Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs:174-199`,
read in full) carries a `// WOLFGATE` comment: `BodyPartType.Chest`→`Torso` (D9), the `BleedAmount`
assertion rewritten from `Is.GreaterThanOrEqualTo(40f)` to `Is.EqualTo(bloodstream.MaxBleedAmount)` (P3-D14,
confirmed `BloodstreamComponent.MaxBleedAmount = 10.0f` at `Content.Server/Body/Components/BloodstreamComponent.cs:57`,
clamped at `BloodstreamSystem.cs:432`), and the added `DismembermentWound`/`AmputationConsequenceWound`/live-entity
assertions.

**No production code was touched by this package** — confirmed by the table above: none of the 12 other
changed tracked files, and none of the 3 new untracked files (`AmputationConsequenceTest.cs`,
`WolfmedAmputationTest.cs`, `WolfmedOrganTest.cs`, all under `Content.IntegrationTests`), fall outside test
scope. **No unauthorised or oversized upstream edits found** in this WP's own contribution.

**D2 (no behaviour change for entities without `WoundHostComponent`):** trivially satisfied — WP11-5 adds
zero production code, so it cannot introduce a D2 breach on its own. At the test level, D2 is actively
exercised: `DecapitationNeverReducesVitalDamageTest` part (b) spawns a non-host `MobMonkey`, asserts
`HasComponent<WoundHostComponent>(monkey) == false`, and confirms only Shitmed's flat 100 `VitalDamage` is
charged (not P3-D1's extra charge); `NonWoundHostTakesNoOrganDamageTest` spawns the identical organ-bearing
graph minus `WoundHostComponent`, confirms `TryApplyPartDamage` is refused while ordinary damage still lands,
and asserts organ health is untouched. Both pass (§8 below).

## 3. Vendoring fidelity

WP11-5 touches zero files under `_Onyx/` or `_WF/` — confirmed by the file table in §2 (its only tracked
edit is a test file outside those trees, and its three new files are also test files). No vendoring-fidelity
diff applies.

The two *ported* test files (not vendored production code, but adapted from Onyx's own tests, per
DECISIONS.md's "port Onyx's wound tests" instruction) were compared against `git -C C:/tmp/onyx show
HEAD:Content.IntegrationTests/Tests/_Onyx/Wounds/{WoundBleedingTest.cs,AmputationConsequenceTest.cs}`:
- `WoundBleedingTest.TraumaticAmputationCreatesSevereStumpBleedingTest`: every divergence from Onyx's
  version is `// WOLFGATE`-commented (§2 above).
- `AmputationConsequenceTest.cs`: Onyx ships 5 tests on a Nubody-shaped fixture
  (`InitialBody`/`TransplantCompatibility`/`bodyPartProfile`, `partType: Chest`). The port rebuilds the
  fixture Shitmed-shaped (`- type: body` graph, `- type: WolfmedBodyPart`, D8/D9), drops
  `HasAmputationConsequence`/`TryAttachPart` assertions (B-2/P3-D2, no such gate in Shitmed), and ports 3 of
  5: `TraumaticAmputationCreatesConsequenceTest` (renamed from Onyx's
  `TraumaticAmputationCreatesBlockingConsequenceTest`, "Blocking" dropped since the block mechanism isn't
  ported, asserting severity **50** off the fixture's non-default `amputationConsequenceSeverity` instead of
  Onyx's 35), `HealingPartAboveThresholdDoesNotAmputateTest` and `HealingBelowResetRatioClearsSeverableTest`
  (both structurally identical to Onyx's, only the damage-system call swapped for the D12
  `WolfmedDamageableSystem` facade and the fixture D8/D9-translated). `SurgicalHealRemovesConsequenceAndUnblocksTest`
  (needs `SurgeryStepEvent`, D7) and `HealingDamageKeepsConsequenceBlockedTest` (payload is the dropped
  attach-gate assertion) are correctly left out and recorded as skips in the file's `<remarks>`.

## 4. Subscriptions

`grep -n "SubscribeLocalEvent" Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs
Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAmputationTest.cs
Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedOrganTest.cs
Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` → **zero hits**. Matches PLAN3 §5.1's "WP11-5
registers nothing" exactly. No duplicate-subscription risk from this package.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for each of the 4 test files (matching the report's §2
descriptions almost verbatim — hit counts, derived severities, and the T-AMP-NOGUN→T-AMP-GUN swap all
called out), plus a `### WP11-5` deviations section covering: the DECISIONS.md §8.6-1 override and its
measured hit counts; the load-bearing `amputationConsequenceSeverity: 50` fixture lines; the T-AMP-OVERFLOW/
T-AMP-EXPLOSION deviations (D-2/D-3) with the full gib-threshold table; the flagged-not-fixed
`GibbingSystem.cs:141` bug; an explicit "no production code changed" statement; and the build/test
checkpoint numbers. All 5 files in the WP table (§6 below) have manifest coverage — file 5 *is* the
manifest, satisfied by this section's own existence.

## 6. Plan conformance

All 5 files in PLAN3's "WP11-5 — Amputation and organ tests" table exist and match:

1. `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` — modified, restored test, verified in §2/§3.
2. `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` — new, adapted, verified in §3.
3. `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAmputationTest.cs` — new, 5 tests: T-AMP-VITAL,
   **T-AMP-GUN** (replacing T-AMP-NOGUN per DECISIONS.md §8.6-1, the later authority), T-AMP-OVERFLOW,
   T-AMP-EXPLOSION, T-AMP-CONSEQUENCE-SEPARATE. Read in full; every hand-derived expected value was
   independently re-derived against the live `AmputationSystem.GetThresholdProgress`/`IsFinishingHit`
   implementation (`Content.Shared/_Onyx/Wounds/AmputationSystem.cs:143-172`) and the live
   `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` data, and matches in every case:
   - **T-AMP-GUN** (`GunsAndLasersAmputateOverThresholdLimbsTest`): hand Piercing threshold 200 + Heat
     threshold 200 (both confirmed in `parts.yml`'s diff, `# WOLFGATE (P3 balance)`-marked), Piercing/Heat
     finishing minimums 12/15 (confirmed same diff). 14 hits of Piercing 14 → 196 (not severable); hit 15 →
     210, arms; hit 16 → progress 210/200 pre-hit ≥ 1 and 14 ≥ 12 → detach. Same shape for Heat (12/13/14
     hits). Below-threshold foot (5×14 Piercing = 70/220, 3×16 Heat = 48/220) stays attached. Melee arm case
     (4/5/6 × Slash 32 against threshold 130, finishing minimum falls back to the host default 15) matches
     PLAN3 §8.2's machete row (6 hits) exactly, and is left untouched by the §8.6-1 change (Slash/Blunt
     deliberately absent from the per-part `dismembermentFinishingDamage` dict). Every severed part is
     asserted `Is.Not.Deleted` (amputated, not gibbed) — correctly distinguishing this from Shitmed's
     `Destructible` gib thresholds (Hand Heat gib 230 vs. the test's peak 224; Arm Slash gib 210 vs. peak 192
     — both headroom checks verified against `Resources/Prototypes/Body/Parts/base.yml`).
   - **T-AMP-VITAL**: `ChargeVitalPartLoss` (`Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`)
     is called *after* `_projection.OnPartRemoved` (confirmed in the file, avoiding risk 8.7.16), charges the
     detached part's own total damage (215) as systemic Bloodloss, and Shitmed's flat `VitalDamage` (100)
     adds on top → 315 systemic, `after - before == 115` exactly as asserted.
   - **T-AMP-OVERFLOW**: `AccumulateAmputationOverflow` (`WoundDamageRoutingSystem.cs:794-848`) traced by
     hand: first 40 fits entirely under `maxDamage: 50` (no overflow); second 40 finds 10 room, applies 10,
     overflows 30 — matches the test's asserted `AmputationOverflow == 30` exactly. The head-not-arm and
     Piercing-500-not-Slash-260 deviations (D-2/D-3) are correctly justified by `Destructible`'s gib
     thresholds, cross-checked against `base.yml`.
4. `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedOrganTest.cs` — new, 8 tests. `AssertOrgan`'s 7 literal
   rows were checked against the live `Resources/Prototypes/_WF/Wolfmed/Body/organs.yml` (WP11-2's file,
   unmodified by this WP) field-by-field (hitChance, selectionWeight, the named multiplier,
   destructionWound/severity) — all match. `OrganDamageComponent.MaxDamageFraction` confirmed `0.3f`
   (`Content.Shared/_Onyx/Body/OrganDamageComponent.cs:22`) and the cap applied at
   `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs:73-74` — T-ORG-CAP's 15→10.5→6 sequence is exact.
5. `Docs/Wolfmed/WOLFMED_MANIFEST.md` — appended, verified in §5.

**DECISIONS.md §8.6 answers, full sweep (per the verify brief):**
- **§8.6-1 (guns/lasers sever via Piercing-12/Heat thresholds):** confirmed present in
  `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` (WP11-1's file, not touched by WP11-5) — per-part `Heat`
  row equal to the `Piercing` row (Head/Arm/Hand/Leg/Foot 200/250/200/250/220) and
  `dismembermentFinishingDamage: {Piercing: 12, Heat: 15}`, all `# WOLFGATE (P3 balance)`-marked. **This is
  the WP11-5's own test target** (T-AMP-GUN) and is honoured, verified above.
- **§8.6-2 (vests + BP helmet annotated):** confirmed present, 5 vests `coverage: [Torso, Arm, Leg]` and 1
  helmet `coverage: [Head]`, `# WOLFGATE (WP11-3, P3-D6)`-marked (WP11-3's files, not touched by WP11-5).
- **§8.6-3 (host-gated vital Bloodloss charge):** confirmed `ChargeVitalPartLoss` present in
  `WolfmedBodyPartLifecycleSystem.cs` (WP11-1's file), called in the correct order, tested by T-AMP-VITAL.
- **§8.6-8 (hand/foot visuals fold, P3-D25):** WP11-4's scope, not re-audited here beyond the WP11-4 verify's
  own PASS; nothing in WP11-5 touches this area.

## 7. Test log cross-check

`WP11-5-tests.log` as named does not exist; the report's saved logs use the `WP11-5-report-*.log` prefix
(the same convention every prior WP's report/verify pair uses — see `WP11-4-report-tests.log`,
`WP11-3-report-tests.log`, etc.). Treated `WP11-5-report-tests.log` (full 65-test gate),
`WP11-5-report-amp2.log` (13-test targeted re-run) and `WP11-5-report-amp.log` (first, failing run) as the
intended files. All 17 tests the report lists as passing were located and confirmed **Passed** in these
logs, with matching per-test timings:

```
TraumaticAmputationCreatesSevereStumpBleedingTest   Passed (tests.log)
TraumaticAmputationCreatesConsequenceTest           Passed (tests.log)
HealingPartAboveThresholdDoesNotAmputateTest        Passed (tests.log)
HealingBelowResetRatioClearsSeverableTest           Passed (tests.log)
DecapitationNeverReducesVitalDamageTest             Passed (amp2.log)
GunsAndLasersAmputateOverThresholdLimbsTest         Passed (amp2.log)   <- T-AMP-GUN, confirmed present & green
OverflowAmputationMechanismIsInertOnShippedLimbsTest Passed (amp2.log)
ExplosionAmputatesDeterministicallyTest             Passed (amp2.log)
RepeatedAmputationCreatesSeparateConsequenceWoundsTest Passed (amp2.log)
OrganPrototypesCarryWolfmedDataTest                 Passed (amp2.log)
OrganDamageIsCappedPerApplicationTest               Passed (amp2.log)
OrganDestructionMergesConsequenceWoundTest          Passed (amp2.log)
DestroyedHeartAppliesDelayedDeathTest                Passed (amp2.log)
DestroyedBrainKillsWithoutDeletingOrganTest          Passed (amp2.log)
DestroyedEyesBlindTest                               Passed (amp2.log)
ZeroHealthOrganRevokesGrantedComponentsTest          Passed (amp2.log)
NonWoundHostTakesNoOrganDamageTest                   Passed (amp2.log)
```

`WP11-5-report-tests.log` tail: `Test Run Successful. Total tests: 65. Passed: 65.` (48 pre-existing +
this package's 17). `WP11-5-report-amp2.log` tail: `Total tests: 13. Passed: 13.`
`WP11-5-report-docktest.log`: `Total tests: 3. Passed: 3.` `WP11-5-report-smoke.log`: `Total tests: 11.
Passed: 9. Skipped: 2` (the 2 skips are pre-existing `[Ignore]`d tests, confirmed unrelated to Wolfmed).

**No failure found in any log that the report does not disclose.** The one real failure
(`OverflowAmputationMechanismIsInertOnShippedLimbsTest` in `WP11-5-report-amp.log`, first run) is exactly
the `GibbingSystem.TryGibEntityWithRef` `InvalidOperationException: Collection was modified` stack trace the
report's §4 describes verbatim (`GibbingSystem.cs:141` → `SharedBodySystem.GibPart:404` →
`GibPartBehavior.Execute:18` → `DamageThreshold.Reached:105` → `DestructibleSystem.Execute:93` →
`DamageableSystem.DamageChanged:172` → `WolfmedDamageableSystem.ChangeDamage:43` →
`WoundDamageRoutingSystem.RouteAppliedDamage:772`), fixed by moving the test setup off the reachable gib
threshold (D-2/D-3), re-run green in `amp2.log`. **Minor:** the report's §1 log index labels this failure
"§3 D-1" — D-1 is actually the T-AMP-NOGUN/T-AMP-GUN swap; the failure is D-2/D-3's subject and is correctly
described in §4. A labelling slip, not a substantive error — noted, not a blocker.

## 8. Snapshot

Written:
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-5.patch` (1312 lines — cumulative diff vs. the phase-2 commit,
  includes prior uncommitted phase-3 packages per the sequential no-commit model)
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-5.untracked.txt` (166 paths, including this WP's own 3 new test
  files, alongside earlier WPs' untracked files)

---

## Summary of findings

No blockers. No majors. Two trivial, non-blocking documentation notes: (1) the report's §1 log index
mislabels the first-run failure as "§3 D-1" when it is properly D-2/D-3's subject (correctly described
in §4 itself); (2) the verify brief's named log file `WP11-5-tests.log` does not exist under that exact
name — the report's `WP11-5-report-*.log` naming (consistent with every prior WP) was used instead. Both
builds green, zero production code touched (only one test file modified plus three new test files),
every `// WOLFGATE` divergence from Onyx's original tests is disclosed and correct, zero new subscriptions,
manifest complete, every DECISIONS.md §8.6 answer this WP depends on (§8.6-1 above all — T-AMP-GUN) is
honoured and independently re-derived against the live code and prototype data rather than taken on faith,
all 17 tests pass with logs matching the report exactly, and T-AMP-GUN
(`GunsAndLasersAmputateOverThresholdLimbsTest`) exists and passes.
