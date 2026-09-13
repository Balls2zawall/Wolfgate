# WP10-6a — Verify: Bridge tests T-AP, T-PASSIVE-A, T-PASSIVE-B

Verified against `DECISIONS.md` (Phase 2 sections), `PLAN2.md` §WP10-6a/§1/§3/§5/§8.3, and
`WP10-6a-report.md`. WG = `.claude/worktrees/rules-motd-updates-11c89c`. No files modified except this
report and the snapshot pair.

## 1. Build (0 errors both)

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  Build succeeded.  0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  Build succeeded.  0 Error(s)
```
Also built `Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt` (required to re-run the
test filter): `Build succeeded. 0 Error(s)`.

**PASS.**

## 2. Upstream discipline

`git diff HEAD --stat` on the tracked content dirs shows 10 files changed (the tree is cumulative and
uncommitted across all F1 work packages executed so far, not just WP10-6a). Non-`_Onyx`/non-`_WF` files
modified, each checked line-by-line against PLAN2 §3's authorised hook table:

| File | Hook | Lines | Verdict |
|---|---|---|---|
| `Content.Client/Examine/ExamineSystem.cs` | HOOK 14 | `MaxWidth = 560` (was 400) + wrap of `richLabel` construction, both `// WOLFGATE`-marked | Matches §3 exactly (WP10-2) |
| `Content.Client/.../DamageOverlayUiController.cs` | HOOK 16 | one added `&& !WolfmedPainOwnsVignette(entity)` clause, `// WOLFGATE`-marked | Matches §3 exactly (WP10-4) |
| `Content.Client/.../Overlays/DamageOverlay.cs` | HOOK 15 | one added `TryApplyWolfmedPain();` call + `0.8f` → `0.8f * level`, both `// WOLFGATE`-marked; body lives in `_WF` partial | Matches §3 exactly (WP10-4) |
| `Content.Server/Body/Systems/BloodstreamSystem.cs` | GUARD E2 | one added `!HasComp<WoundHostComponent>(ent) &&` clause, `// WOLFGATE`-marked | Matches §3 exactly (WP10-2) |
| `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` | GUARD F | 5 sites (using, `args.User` param, signature, if-wrap, else-`AddPartStatusMarkup`), all `// WOLFGATE`-marked, `else` branch confirms **P2-D20** (wound-hosts-only, not unconditional as Onyx does it) | Matches §3 exactly, including the pre-authorised aggregate-exceeds-2-lines shape (WP10-2) |

`Content.Shared/_Onyx/Wounds/PainSystem.cs` is a vendored `_Onyx` file (excluded from this check; its one
`// WOLFGATE`(P2-D8) edit was landed by WP10-3, out of WP10-6a's scope) — noted, not re-audited here.

**None of these five files were touched by WP10-6a itself** — its own diff is confined to
`Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` (a `_WF` test file) and
`Docs/Wolfmed/WOLFMED_MANIFEST.md`, both outside this check's scope by definition. Confirmed by reading the
full diff of `WolfmedDamageBridgeTest.cs`: only additive `[TestPrototypes]` YAML and three new `[Test]`
methods, matching the report's description exactly (three-body T-AP; real-`MobHuman` T-PASSIVE-A;
bespoke-pair T-PASSIVE-B). No upstream file, no vendored file, touched by this package.

**PASS — no unauthorised or oversized upstream edits, in this WP or in the four other F1 packages already
landed in the tree.**

## 3. Vendoring fidelity

WP10-6a adds/changes **zero** files under `_Onyx/`. Confirmed by the WP's own file list (report §1) and by
`git diff HEAD --stat` showing only the `_WF` test file and the manifest as this package's contribution.
**N/A for this WP — no files to check.**

## 4. Subscriptions

`grep -n "SubscribeLocalEvent" Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` — zero
hits. WP10-6a adds three `[Test]` methods and three `[TestPrototypes]` entries, no systems, no
`Subscribe*` calls. Matches PLAN2 §5.1's table (WP10-6a registers no `(Component, Event)` pair) and the
report's own claim ("No new subscriptions").

**PASS — nothing to audit for duplicates.**

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries:
- The file-list row for `WolfmedDamageBridgeTest.cs` (line ~134), updated in place to record WP10-6a's
  additions (T-AP, T-PASSIVE-A/B, the three new `[TestPrototypes]`).
- A full `### WP10-6a (phase 2 — bridge tests...)` deviations subsection recording both corrected
  predictions (T-PASSIVE-A's lower-bound fix, T-PASSIVE-B's 300-tick/exact-zero fix) with the same
  derivations as the report, plus the one-off `IOException` note and the "no production code changed" line.

**PASS — every file this WP touched has a manifest row.**

## 6. Plan conformance

- WP10-6a's one planned file (`WolfmedDamageBridgeTest.cs`, PLAN2 §9) exists at its destination and was
  extended, not replaced.
- **Fracture multipliers (DECISIONS §8.2-1):** `Resources/Prototypes/_Onyx/Wounds/wounds.yml` shows
  `manipulationModifier: 1.1 / 1.25 / 1.5 / 2.0` for Hairline/Simple/Displaced/Comminuted behind a
  `# WOLFGATE (DECISIONS §8.2-1)` comment. Honoured (landed by WP10-1, unaffected by this WP).
- **Part status wound-hosts-only (DECISIONS §8.2-8 / P2-D20):** `HealthExaminableSystem.cs`'s GUARD F wrap
  calls `AddPartStatusMarkup` only in the `else` of `!HasComp<WoundHostComponent>(uid)` — confirmed by
  direct diff read. Honoured (landed by WP10-2, unaffected by this WP).
- **Pain sounds ported with corrected key (DECISIONS §8.2-3):** not yet landed anywhere in the tree —
  `EmoteOnDamageSystem.PainSounds.cs` does not exist yet. This is expected: pain sounds is WP10-5 (group F2,
  scheduled strictly after all of group F1 including WP10-6a finishes) per PLAN2's build-order DAG (§4).
  Not a WP10-6a defect; nothing in this WP contradicts the decision, it simply predates its own
  implementation. Flagged here for the record, not as a blocker.
- T-AP's exercised subscriber, `WolfmedPartArmorSystem` (`Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs`),
  exists in the tree (landed pre-phase-2), so the test's "armour penetration survives the wound-host
  detour" claim is against real code, not a stub.

**PASS** (pain-sounds item is a scheduling non-issue, not a conformance failure).

## 8. Test re-run

`dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build --filter
"FullyQualifiedName~WolfmedDamageBridgeTest"`:

```
Passed ArmorPenetrationReachesWoundHostsTest [1 m 35 s]
Passed CausticRoutesToTheHitPartTest [1 m 35 s]
Passed DamageChangedDeltaSurvivesProjectionTest [182 ms]
Passed NoDoubleApplicationTest [95 ms]
Passed NonWoundHostUnchangedTest [121 ms]
Passed PassiveDamageMechanismStillRoutesIfReenabledTest [448 ms]
Passed PiercingHitscanDamagesEntitiesBehindAWoundHostTest [124 ms]
Passed EveryPartGetsWoundableOnMapInitTest [1 s]
Passed TryChangeDamageReportsRoutedDamageTest [117 ms]
Passed RealWoundHostPassiveDamageIsNeutralisedTest [1 s]

Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

Matches the report's table exactly: same 10 tests, same pass/fail outcome for each (all pass), including
the 3 new tests (T-AP, T-PASSIVE-A, T-PASSIVE-B) and the 7 pre-existing ones (no regression). Per-test
wall-clock differs slightly run to run (expected for a headless integration-test pool), but that is not a
result the report claimed to reproduce exactly.

Independently re-derived the report's two documented prediction corrections by reading
`WoundDamageRoutingSystem.cs` myself:
- `OnBeforeDamageChanged` intercepts any `TryChangeDamage` on `WoundHostComponent` regardless of sign.
- `RouteThroughBodyModifiers`'s `localizedDamage` gate is `amount > FixedPoint2.Zero`, so an untargeted
  negative amount sets no `_requestedParts` entry.
- The change still reaches `OnDamageDealt → RouteAppliedDamage`, which buckets a negative localized-type
  delta as healing and calls `ApplyLocalizedHealing`, spreading it across parts carrying positive damage of
  that type — with no `WoundHostComponent`-specific block anywhere in that path.

This confirms the report's root-cause claim ("no code-level barrier, only the D29 YAML convention") rather
than merely trusting the prose.

**PASS.**

## 7. Snapshot

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p2/snapshots/WP10-6a.patch          (774 lines)
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs \
  Content.IntegrationTests > C:/tmp/wolfmed-plan/p2/snapshots/WP10-6a.untracked.txt   (15 lines)
```
Both written. Note: as with checks 2/6, these snapshots capture the *cumulative* uncommitted phase-2 diff
(WP10-1 through WP10-6a), not a WP10-6a-only diff, because the tree carries all completed F1 packages
uncommitted per the phase-2 no-commits rule. This matches every prior WP's snapshot in
`C:/tmp/wolfmed-plan/p2/snapshots/` (WP10-1 through WP10-4 are all similarly cumulative).

## Other observations (non-blocking)

- `Docs/Wolfmed/DECISIONS.md` is also modified in the tree (mirrors the phase-2 additions from
  `C:/tmp/wolfmed-plan/DECISIONS.md`), but this predates WP10-6a, is outside its file list, and outside
  `WOLFMED_MANIFEST.md`'s ownership boundary for this WP — not this package's concern.

## Verdict

All eight checks pass. No unauthorised upstream edits, no unmarked vendoring changes (none present), no
new duplicate subscriptions (none added), manifest rows present, plan decisions honoured (fracture
multipliers, part-status wound-hosts-only; pain-sounds decision awaits its own later WP by design), both
builds green, and the test re-run reproduces the report's 10/10 pass result including the two documented,
well-evidenced prediction corrections (T-PASSIVE-A's lower bound, T-PASSIVE-B's 300-tick/exact-zero fix).

**PASS.**
