# WP10-6b — Fracture + pain tests (PLAN2 §4 / §6, group F3)

**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG), branch `clanker/wolfmed-port-orchestration-454c3d`, uncommitted.
**Result:** Server, Client and IntegrationTests all build with **0 errors**. The whole wound suite is green: **39/39 passed**, including all six new tests. Smoke filter `EntityTest|PrototypeSaveTest`: **6 passed, 2 skipped** (both `[Ignore]`d upstream, pre-existing). No re-run needed — no `db.ef` / `admin_notes` flake occurred, so `DockTest` was not required.

---

## 1. Files created / modified

| # | Path | Status | Notes |
|---|---|---|---|
| 1 | `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` | **modified** | 4 new `[Test]` methods (T-FRACT-EFFECTS, T-FRACT-HANDS, T-FRACT-ALERT, T-FRACT-ALERT-NEG), 2 `[TestPrototypes]` additions, 6 new `using`s, the WP10-6b skip marker removed. CRLF preserved. |
| 2 | `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedPainTest.cs` | **new** | T-PAIN-OVERLAY, T-PAIN-SHOCK, T-HIGH-PAIN, T-PAIN-NUMB + 4 `[TestPrototypes]` on one new body graph. LF, matching its sibling `WolfmedDamageBridgeTest.cs`. |
| 3 | `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | `WoundFractureTest.cs` row extended, new `WolfmedPainTest.cs` row, new `### WP10-6b` deviations subsection before `## Hazards`. Appended directly per the orchestrator's override of PLAN2's `manifest-rows-WP10-N.md` indirection. |

**No production code changed. No upstream or vendored `_Onyx` file touched. No new subscription registered** — nothing to add to PLAN2 §5's audit. `RobustToolbox/` untouched; no git write operations.

New pool-global `[TestPrototypes]` ids (all grepped clear beforehand): `WoundFractureHeldItem`, `WolfmedPainBodyGraph`, `WolfmedPainControlBody`, `WolfmedHighPainThresholdBody`, `WolfmedPainShockBody`, `WolfmedPainNumbBody`.

---

## 2. Every `// WOLFGATE` edit and its reason

All edits are in test files; each measured literal carries its derivation at the assertion, as P2-D16 requires.

### `WoundFractureTest.cs`

1. **`- type: Alerts` on `WoundFractureBody`** — `// WOLFGATE (WP10-6b)`. `AlertsSystem.ShowAlert` early-returns when the entity has no `AlertsComponent` (`AlertsSystem.cs:87`), so T-FRACT-ALERT would have read `false` for a reason unrelated to `FractureAlertSystem`.
2. **New `WoundFractureHeldItem` prototype** (bare `- type: Item`) — `// WOLFGATE (WP10-6b)`. T-FRACT-HANDS drives `TryGetUsedHandSymmetry`'s `used` branch through `SharedHandsSystem.IsHolding`, which only resolves a hand for a real item.
3. **T-FRACT-EFFECTS derivations**: walk **0.5** (Comminuted leg → `movementModifier 0`, `PartEffectScales[Leg] 0.5`, `TreatmentEffectScales[None] 1` → `ModifySpeed(1 − (1−0)·0.5·1)`; Onyx's stale literal is `0.4f`); manipulation **2.0** (see §3 item 1); **1.0** after `TryMend` (`removeWoundWhenMended: true` → `Functional` fallback); walk **1.0** after detach. Also a P2-D21 guard assertion that `GetActiveHand` is non-null *before* any multiplier assertion, with a failure message saying the fixture is at fault.
4. **P2-D23 notes** at each fracture creation: every fracture in this package is created with a ≥ 60 hit (`creationChance: 1`) and re-graded with `WoundSystem.ChangeSeverity`, which runs through `OnWoundChanged` with no roll.

### `WolfmedPainTest.cs`

5. **P2-D24 fixture comment** on the `[TestPrototypes]` block: why `StatusEffects allowed: [Stun, KnockedDown, Jitter]` **and** `MobState` are both mandatory.
6. **Overlay-level mirror** (`Level(...)`) documented as a mirror of `Content.Client/_WF/Wolfmed/Overlays/DamageOverlay.Wolfmed.cs`, with the warning that nothing in the compiler couples them.
7. **Measured pain numbers** annotated: 100.2 after one 60-Blunt hit (52.2 damage pain at `DamageMultipliers["Blunt"] 0.87` + 48 one-time fracture pain from `BoneFractureWound`'s Comminuted `WoundPainBehavior` at `painPerSeverity 0.8`), 135 soft cap, 94.5 after adrenaline, 8.7 vs 6.52 for `HighPainThreshold`, and the `FixedPoint2` truncation that makes it 6.52 rather than 6.53.

---

## 3. Deviations from PLAN2, with justification

1. **PLAN2 P2-D16 / §6.2's manipulation prediction of `0.75` is stale and was not used; the measured and asserted value is `2.0`.** P2-D16 was written against Onyx's shipped `manipulationModifier` values, but DECISIONS.md §8.2-1 (the binding answer) had WP10-1 restore the C# defaults 1.1/1.25/1.5/2.0. Derivation at the assertion: Comminuted arm `manipulationModifier 2.0` × `partScale 1` (`Arm` is absent from `PartEffectScales`) × `treatmentScale 1` → `1 + (2.0−1)·1·1 = 2.0`; the intact left hand falls through to `BodyPartFunctionalitySystem.GetState` = `Functional` (P2-3's cvar is false) → `1`. Product **2.0** — by coincidence Onyx's own original literal. Verified by test.
2. **Two `[TestPrototypes]` additions in a block serialisation rule 0 assigned to WP10-1** (`- type: Alerts`, `WoundFractureHeldItem`). WP10-1 is finished and released the file; both are hard prerequisites of assertions WP10-6b owns, and neither touches production data.
3. **T-FRACT-HANDS asserts both directions.** PLAN2 asks only for "item in the right hand → 1f". A lone `1f` is indistinguishable from the "no hand found" failure mode P2-D21 warns about, so the test also holds a second item in the left hand and asserts `2.0`.
4. **T-HIGH-PAIN uses its own control fixture** (`WolfmedPainControlBody`) rather than PLAN2's `WolfmedBridgeBody`, which is defined in `WolfmedDamageBridgeTest.cs`. `[TestPrototypes]` are pool-global so PLAN2's version would work, but it would make one test file's fixture load-bearing for another file's assertions.
5. **T-PAIN-SHOCK ships the bespoke-fixture version *and* a real-mob check.** PLAN2 says to switch to a real `MobHuman` once WP10-5 lands (it has). The bespoke `WolfmedPainShockBody` is kept because it is the only thing that exercises P2-D24 explicitly; the real-mob half lives in T-PAIN-OVERLAY's last block, which asserts `StunnedComponent` on a `MobHuman` — i.e. WP10-5's `PainShockTarget` wiring is gated by a test.
6. **T-PAIN-NUMB's "no pain-shock emote" is asserted as "no pain shock at all"** (`StunnedComponent` absent, `Armed` still true, `AdrenalineEnds` null) after the same damage that paralyses `WolfmedPainShockBody`. The emote itself is not observable from a headless pair; `UpdatePainShock` returns before the `Scream` call, so the negative is exact, not approximate.
7. **T-CYBER-FALLBACK (PLAN2 §6.2, marked optional) was not written.** Out of the task's stated scope; P2-D14's live cybernetics path remains untested. Flagged for WP11.
8. **No headless-server run.** The only YAML this package touches is inside `[TestPrototypes]` string literals, which the test pair itself parses — and did, 39/39 green. No `Resources/**` YAML or FTL was modified.
9. **Manifest indirection skipped** per the orchestrator's instruction (rows appended directly to `WOLFMED_MANIFEST.md`; no `manifest-rows-WP10-6b.md` written). **No commit, no snapshot patch.**

---

## 4. Build / test output

```
$ dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Wound suite — `C:/tmp/wolfmed-plan/p2/wp/WP10-6b-tests.log` (filter `_Onyx.Wounds|Wolfmed`), first run, no re-run needed:

```
  Passed EffectsRefreshOnTreatmentHealingAndDetachTest [176 ms]
  Passed FractureManipulationUsesHeldHandSymmetryTest [119 ms]
  Passed FractureAlertTracksGradeAndTreatmentTest [133 ms]
  Passed FractureAlertRespectsMinimumGradeTest [144 ms]
  Passed PainOverlayLevelTracksPainTest [231 ms]
  Passed PainShockStunsAtThresholdTest [89 ms]
  Passed HighPainThresholdReducesWoundPainGainTest [101 ms]
  Passed PainNumbnessSuppressesWoundPainTest [92 ms]
  ... (31 pre-existing wound/bridge tests, all passed)
Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 2.7555 Minutes
```

Smoke — `C:/tmp/wolfmed-plan/p2/wp/WP10-6b-smoke.log` (filter `EntityTest|PrototypeSaveTest`):

```
  Passed EntityEntityTest [1 m 45 s]
  Passed AllComponentsOneToOneDeleteTest [1 m 45 s]
  Passed SpawnAndDeleteAllEntitiesInTheSameSpot [1 m 33 s]
  Passed SpawnAndDeleteAllEntitiesOnDifferentMaps [2 m 30 s]
  Passed UninitializedSaveTest [34 s]
  Passed AllItemsHaveSpritesTest [5 s]
  Skipped SpawnAndDeleteEntityCountTest    (EntityTest.cs:238 [Ignore("Even wizden calls this test ass")])
  Skipped SpawnAndDirtyAllEntities         (EntityTest.cs:150 [Ignore("Preventing CI tests from failing")])
Test Run Successful.
Total tests: 8
     Passed: 6
```

Both skips are pre-existing `[Ignore]` attributes, not caused by this package. No `Skipped — Test was dirty-disposed.` anywhere: the new fixtures do not poison the shared pool (PLAN2 §6.1 trap 1).

---

## 5. What later packages must know

1. **Measured constants now pinned by tests, all first-ever measurements on a real build (P2-4):** walk 0.5 / manipulation 2.0 / mend 1.0 / detach 1.0 (fractures); pain 100.2 per 60-Blunt hit on a fracture-capable part, 135 soft cap, 94.5 under adrenaline, shock at 130, rearm below 110; 8.7 head pain per 10 Blunt, 6.52 with `HighPainThreshold`; overlay 0 / 0 / 0.05 / 0.5 / 0.7 at pain 0 / 6 / 6.75 / 67.5 / 200. **Any balance change to `OrganicFractureProfile`, `PainComponent.DamageMultipliers`, `SoftPainCap`, `HighPainThresholdComponent.PainMultiplier` or `BoneFractureWound`'s pain behaviours breaks a named assertion** — that is deliberate, and each one names the profile field it reads.
2. **The pain vignette eases as pain shock lands.** At raw pain 135 the overlay reads `GetPain`, which the 30 s adrenaline window multiplies by 0.7, so the level drops from a would-be 1.0 to 0.7 exactly when the player is paralysed. Onyx's design, now pinned by T-PAIN-OVERLAY. Worth a look in the balance pass.
3. **`WolfmedPainTest.Level` is a hand-maintained mirror of HOOK 15's formula** in `Content.Client/_WF/Wolfmed/Overlays/DamageOverlay.Wolfmed.cs`. Change one, change the other; the compiler will not tell you.
4. **`FixedPoint2` truncates, it does not round** (`FixedPoint2.cs:101,116`). Any derived expectation must be truncated to two decimals — 6.525 is 6.52, 6/135 is 0.04.
5. **P2-D23 and P2-D21 are now enforced by tests, not just by convention.** Every fracture is created at ≥ 60 and re-graded with `ChangeSeverity`; `WoundFractureBody` keeps exactly one hand, and `GetActiveHand` is asserted non-null before any multiplier assertion. A later package that adds a second hand to `WoundFractureBody` will silently change which arm T-FRACT-EFFECTS measures.
6. **`WoundFractureBody` now carries `- type: Alerts`.** Any future assertion about alerts on the other fracture fixture (`WoundFractureHandsBody`) needs the same component added there first.
7. **T-CYBER-FALLBACK (P2-D14) is still unwritten** — the one `GetEffect` fallback path (`CyberneticsComponent.Disabled` → mobility 0 / manipulation 2.5) that stays live despite `wounds.body_part_functionality_enabled: false` has no coverage. Cheap to add on `WoundFractureBody`; recommended for WP11.
8. **WP10-7 (docs):** the manifest already carries WP10-6b's two rows (the extended `WoundFractureTest.cs` row and the new `WolfmedPainTest.cs` row) and a `### WP10-6b` deviations subsection immediately before `## Hazards`. Reconcile rather than re-add. The manifest working copy is **LF** and UTF-8 — `WoundFractureTest.cs` is **CRLF**; do not normalise either.
