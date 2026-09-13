# WP10-6b verify — Fracture + pain tests

**Verdict: PASS.** No blockers, no majors.

---

## 1. Build (check 1)

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```
Both ran sequentially, both clean. (Content.IntegrationTests also builds — proven by the test run in §6, which
requires it.)

---

## 2. Upstream discipline (check 2)

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests` shows
13 changed tracked files. WP10-6b's own report says it touches **zero** upstream/production files — verified:
its two files (`WoundFractureTest.cs`, `WolfmedPainTest.cs`) are both under `_Onyx`/`_WF` and are excluded from
this check. The remaining non-`_Onyx`/`_WF` files in the diff are carried over uncommitted from earlier
phase-2 WPs (no commits between packages, per DECISIONS.md); re-verified here since the check asks for "this or
an earlier phase-2 WP":

| File | Hook | Marked | Authorised (PLAN2 §3) | Size |
|---|---|---|---|---|
| `Content.Client/Examine/ExamineSystem.cs` | HOOK 14 (a),(b) | yes, both sites | yes | 2 sites, ~1-3 lines each |
| `Content.Client/.../DamageOverlayUiController.cs` | HOOK 16 | yes | yes | 1 line |
| `Content.Client/.../Overlays/DamageOverlay.cs` | HOOK 15 (a),(b) | yes, both sites | yes | 2 one-line sites |
| `Content.Server/Body/Systems/BloodstreamSystem.cs` | GUARD E2 | yes | yes | 1 line |
| `Content.Server/Chat/EmoteOnDamageComponent.cs` | HOOK 17 | yes | yes | additive datafields only, `Emotes` untouched |
| `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` | HOOK 18 | yes | yes | 1 line |
| `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` | GUARD F (a)-(e) | yes, all 5 sites | yes (explicitly authorised as a 5-site exception, §8.2 item 4) | matches §3's exact prescribed diff, including the `else AddPartStatusMarkup` placement (P2-D20) |
| `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | P2-D12 deletion | commented at removal site (pre-existing `# WOLFGATE (WP7)` block deleted whole) | yes | removes the 4 `wound-examine-fracture-*` keys now owned by `health-examinable.ftl` |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | P2-D7 / P2-D9 | yes, block comment | yes (WP10-5 sole owner, same `# WOLFGATE` block as phase 1) | |

All hooks match their PLAN2 §3 text verbatim (line-by-line diffed). No edit outside the authorised list.
`Docs/Wolfmed/DECISIONS.md`'s diff is the orchestrator's own phase-2 section (not a WP edit) and is out of this
check's file scope.

---

## 3. Vendoring fidelity (check 3)

Only one `_Onyx` file was changed **by WP10-6b itself**:
`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs`. Diffed against
`git -C C:/tmp/onyx show HEAD:Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` with CRLF
stripped. Every differing hunk carries a `// WOLFGATE` marker (or is inside a block already headed by one):

- New `using`s: the added `Content.Shared._WF.Wolfmed.Compat` line carries its own `// WOLFGATE` comment; the
  rest of that hunk (`Alert`, `Hands.Components`, `Hands.EntitySystems`) is covered by the same hunk.
- `[TestPrototypes]` rewrite: headed by a multi-line `// WOLFGATE (P2-D21, WP10-1)` block comment (WP10-1's
  fixture work, not WP10-6b's, but present and marked); the two WP10-6b-owned additions (`- type: Alerts`,
  `WoundFractureHeldItem`) each carry their own `// WOLFGATE (WP10-6b)` comment.
- `GetGrade` boundary literals (20/35/50/60): `// WOLFGATE` comment above (WP10-1, pre-existing in tree).
- `EffectsRefreshOnTreatmentHealingAndDetachTest`: every changed assertion carries a `// WOLFGATE` derivation
  comment (P2-D16, P2-D21, P2-D23, and the corrected 2.0 vs. PLAN2's stale 0.75 — see §6 below).
- Three new tests (`FractureManipulationUsesHeldHandSymmetryTest`, `FractureAlertTracksGradeAndTreatmentTest`,
  `FractureAlertRespectsMinimumGradeTest`): each carries `// WOLFGATE` derivation comments at every measured
  literal, plus an XML-doc `<summary>` citing the PLAN2 test id.

No unmarked hunk found. CRLF line endings preserved (confirmed with `file`: still "CRLF line terminators").

`WolfmedPainTest.cs` is new, entirely under `_WF`, and has no Onyx original — vendoring fidelity does not
apply; it follows `_WF` style (no license header, `///` one-liners) and its `Level()` helper was checked
line-for-line against `Content.Client/_WF/Wolfmed/Overlays/DamageOverlay.Wolfmed.cs`'s `TryApplyWolfmedPain()`
— identical formula (`Min(1, GetPain/SoftPainCap)`, floor to 0 below 0.05).

---

## 4. Subscriptions (check 4)

`grep -n "SubscribeLocalEvent" Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs
Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedPainTest.cs` → **zero hits**. WP10-6b registers no directed
subscription, consistent with the report and with PLAN2 §5 (all ten phase-2 pairs are registered by WP10-1/
WP10-3/§2.2, none by the test WPs). No duplicate-subscription risk introduced by this package.

---

## 5. Manifest (check 5)

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries:
- line 131: the extended `WoundFractureTest.cs` row, attributing WP10-1's fixture work and WP10-6b's four test
  methods and two prototype additions separately and accurately.
- line 135: the new `WolfmedPainTest.cs` row, WP10-6b, listing all four tests and all four fixtures.
- a `### WP10-6b` subsection (line 718) with the full deviation list, placed before `## Hazards` as the report
  states.

Both files WP10-6b touched have rows. Manifest check passes.

---

## 6. Plan conformance (check 6)

Both files in PLAN2 §9's WP10-6b row (2 files: extend `WoundFractureTest.cs`, new `WolfmedPainTest.cs`) exist
at their destinations. §6.2's test table items are all present: T-FRACT-EFFECTS, T-FRACT-HANDS,
T-FRACT-ALERT, T-FRACT-ALERT-NEG, T-PAIN-OVERLAY, T-PAIN-SHOCK, T-HIGH-PAIN, T-PAIN-NUMB (T-CYBER-FALLBACK,
marked optional, is honestly reported as not written).

**DECISIONS.md §8.2 items, spot-checked directly against the tree (not taken on the report's word):**

- **§8.2-1 fracture multipliers fixed to 1.1/1.25/1.5/2.0:** confirmed in
  `Resources/Prototypes/_Onyx/Wounds/wounds.yml:65,70,75,80` — Hairline/Simple/Displaced/Comminuted =
  1.1/1.25/1.5/2.0 exactly, with a `# WOLFGATE (DECISIONS §8.2-1)` comment. The WP10-6b report's deviation 1
  correctly identifies that PLAN2 §6.2's own interim prediction of "manipulation 0.75" for T-FRACT-EFFECTS is
  now **stale** against this later, binding decision, and re-derives the honoured value as **2.0**
  (Comminuted `manipulationModifier` 2.0 × partScale 1 × treatmentScale 1). The test asserts `2.0` and passed.
  This is not a plan violation — DECISIONS.md is explicitly senior to PLAN2's per-test literal predictions
  (P2-D16 flags every literal as "prediction until measured"), and the report documents the override with its
  derivation, as required.
- **Pain sounds ported with the corrected key:** `emotesThreshold` confirmed in `base.yml`'s WP10-5 block
  (out of WP10-6b's own scope, but present and intact in the tree WP10-6b built against).
- **Part status wound-hosts-only (P2-D20):** confirmed via GUARD F's `else AddPartStatusMarkup(...)` placement
  in `HealthExaminableSystem.cs` (§2 above) — also pre-existing, intact.

Cross-checked every numeric literal the report claims was "measured": `DamageMultipliers["Blunt"] = 0.87f`,
`SoftPainCap = 135`, `HighPainThresholdComponent.PainMultiplier = 0.75f`, `PainShockThreshold = 130`,
`PainShockRearmThreshold = 110`, `PainShockAdrenalineMultiplier = 0.7f`, `PainShockAdrenalineTime = 30s`,
`BoneFractureWound` Comminuted `painPerSeverity: 0.8`/`oneTime: true`, `PartEffectScales[Leg] = 0.5f` — all
read directly from source and consistent with every assertion in both test files. `FixedPoint2`'s multiply/
divide operators do use an `(int)` cast (truncation, not rounding), consistent with the report's 6.52-not-6.53
claim.

---

## 7. Snapshot (check 7)

```
git -C <WG> diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p2/snapshots/WP10-6b.patch          (1235 lines)
git -C <WG> ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources \
  Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p2/snapshots/WP10-6b.untracked.txt  (17 files)
```
Both written. Per DECISIONS.md, this snapshot is the accumulated tree through WP10-6b (no commits between
packages), consistent with the WP10-1 through WP10-6a snapshots already on disk.

---

## 8. Tests (check 8)

`C:/tmp/wolfmed-plan/p2/wp/WP10-6b-tests.log`: **39 total, 39 passed, 0 failed.** Every test the report lists
as passing is present and green in the log, confirmed by line number:

- `EffectsRefreshOnTreatmentHealingAndDetachTest` — Passed [176 ms] (:439)
- `FractureManipulationUsesHeldHandSymmetryTest` — Passed [119 ms] (:530)
- `FractureAlertTracksGradeAndTreatmentTest` — Passed [133 ms] (:441)
- `FractureAlertRespectsMinimumGradeTest` — Passed [144 ms] (:440)
- `PainOverlayLevelTracksPainTest` — Passed [231 ms] (:695)
- `PainShockStunsAtThresholdTest` — Passed [89 ms] (:696)
- `HighPainThresholdReducesWoundPainGainTest` — Passed [101 ms] (:693)
- `PainNumbnessSuppressesWoundPainTest` — Passed [92 ms] (:694)

No failures anywhere in the log. `Total tests: 39 / Passed: 39 / Total time: 2.7555 Minutes` matches the
report verbatim. Smoke log (`WP10-6b-smoke.log`): 8 total, 6 passed, 2 skipped — both skips are pre-existing
`[Ignore]` attributes in `EntityTest.cs` (`SpawnAndDeleteEntityCountTest`, `SpawnAndDirtyAllEntities`), not
caused by this package; matches the report.

---

## Deviations reviewed and accepted

All nine deviations in the report's §3 were checked against source, not taken on trust:

1. **Manipulation 2.0 vs. PLAN2's stale 0.75** — verified correct and properly derived (§6 above).
2. **Two `[TestPrototypes]` additions in WP10-1's serialisation-rule-0 block** (`- type: Alerts`,
   `WoundFractureHeldItem`) — both are hard prerequisites for WP10-6b's own assertions, don't touch production
   data, and are individually `// WOLFGATE (WP10-6b)`-marked. Accepted as a minor, disclosed scope note.
3. **T-FRACT-HANDS asserts both directions** — stronger than PLAN2's literal ask, for a documented reason
   (distinguishing a real `1f` from the "no hand found" `1f` failure mode). Accepted.
4. **T-HIGH-PAIN uses its own control fixture** rather than `WolfmedBridgeBody` — avoids cross-file test
   coupling; `[TestPrototypes]` id `WolfmedPainControlBody` does not collide with anything (grepped clear).
   Accepted.
5. **T-PAIN-SHOCK keeps the bespoke fixture and adds a real-mob check inside T-PAIN-OVERLAY** — both paths
   verified present and passing. Accepted.
6. **T-PAIN-NUMB asserts "no pain shock at all" rather than literally "no emote"** — correct given
   `UpdatePainShock` returns before the `Scream` call, verified in `PainSystem.cs`. Accepted.
7. **T-CYBER-FALLBACK not written** — explicitly optional in PLAN2 §6.2, honestly flagged for WP11, not a gate.
8. **No headless-server/YAML-linter run** — correct call: the only YAML this package touches lives inside
   `[TestPrototypes]` string literals, parsed by the test pair itself (which did run, 39/39 green). No
   `Resources/**` file was modified by WP10-6b.
9. **Manifest indirection skipped, no commit, no snapshot patch** — consistent with DECISIONS.md's execution
   note (direct manifest appends) and with the plan (verify stage owns the snapshot, done in §7 above).

None of the nine rise above a documented, justified deviation; none touch upstream files or add subscriptions.

---

## Summary

Both builds are clean. WP10-6b touches exactly the two files its own report claims (`WoundFractureTest.cs`
extended, `WolfmedPainTest.cs` new) plus the manifest; every upstream/production file elsewhere in the tree
belongs to an earlier, already-marked phase-2 WP and matches PLAN2 §3 verbatim. The one `_Onyx` file WP10-6b
changed is fully WOLFGATE-marked at every differing hunk against the Onyx original. No new subscriptions. The
manifest has both rows. Every DECISIONS.md §8.2 item spot-checked (fracture multipliers, pain-sound key, part
status gating) is honoured in the actual source, not just claimed. All 39 wound-suite tests pass, including
the 8 this package owns; the smoke suite's 2 skips are pre-existing and unrelated. The one substantive
plan deviation (manipulation 2.0 instead of PLAN2's own interim 0.75 prediction) is the *correct* resolution
of a genuine conflict between PLAN2's stale interim math and DECISIONS.md's later, binding balance decision,
and is documented with a full derivation at the assertion.

**No blockers. No majors. PASS.**
