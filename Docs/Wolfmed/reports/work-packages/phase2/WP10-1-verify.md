# WP10-1 — Verification report

**Verifier worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (`C:/tmp/onyx`)
**Verdict: PASS.** No blockers, no majors.

---

## 1. Build

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```
Both green, 0 errors, matching the report.

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`:

```
 .../Tests/_Onyx/Wounds/WoundFractureTest.cs        | 46 ++++++++++++++++++++++
 Resources/Prototypes/_Onyx/Wounds/wounds.yml       | 12 ++++--
 2 files changed, 54 insertions(+), 4 deletions(-)
```

Full inventory (`git status --porcelain` over the same paths, plus `Docs`):

```
 M Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs
 M Docs/Wolfmed/DECISIONS.md
 M Docs/Wolfmed/WOLFMED_MANIFEST.md
 M Resources/Prototypes/_Onyx/Wounds/wounds.yml
?? Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs
?? Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs
?? Content.Shared/_WF/Wolfmed/DoAfter/
```

No upstream (non-`_Onyx`, non-`_WF`) production file is touched anywhere in the six files. Findings per file:

- `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` — modified, but only inside the
  `[TestPrototypes]` string constant (adds a `left hand` slot + `- type: Hands` to `WoundFractureBodyGraph`/
  `WoundFractureBody`, plus a new `WoundFractureHandsBodyGraph`/`WoundFractureHandsBody`). No test method
  touched; the WP10-6b skip marker is untouched. This is exactly PLAN2's Serialisation rule 0 / T-FIXTURE
  (P2-D21), explicitly assigned to WP10-1. An 8-line `// WOLFGATE (P2-D21, WP10-1)` comment sits directly above
  the block explaining the design (why exactly one hand). Not a "hook" subject to the one/two-line ceiling —
  it is a test-fixture prototype PLAN2 assigns to this WP by name.
- `Resources/Prototypes/_Onyx/Wounds/wounds.yml` — vendored (`_Onyx`), covered by check 3 below.
- `Docs/Wolfmed/DECISIONS.md` — diffed and confirmed to be the pre-existing orchestrator content (the Phase-2
  scope/answers sections already present in `C:/tmp/wolfmed-plan/DECISIONS.md` at task start), not new writes
  by this WP. Matches report item 8's claim.
- `Docs/Wolfmed/WOLFMED_MANIFEST.md` — see check 5.

No directed-subscription or upstream hook of any kind was added by WP10-1 (confirmed: `FractureAlertSystem.cs`
has no `Initialize`; `FractureEffectsSystem.cs`'s subscriptions are all on vendored `_Onyx` components/events;
`WolfmedFractureDoAfterSystem.cs`'s one subscription is on a new `_WF` file). PLAN2 §3's authorised-hooks table
lists zero hooks for WP10-1 — consistent with the report's "WP10-1 needed no upstream hook at all."

## 3. Vendoring fidelity

Diffed both new `_Onyx` files against `git -C C:/tmp/onyx show HEAD:<path>` with `--strip-trailing-cr`.

**`FractureAlertSystem.cs`** (Onyx 46 lines → WG 49 lines, +3):
```
4a5
> using Content.Shared._WF.Wolfmed.Body; // WOLFGATE: D8 keeps Onyx's part fields on WolfmedBodyPartComponent.
14a16
>     [Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8, Onyx's extra part fields.
24c26,27
<             if (bodyPart.FractureProfile is not { } profileId ||
---
>             // WOLFGATE: D8, FractureProfile lives on WolfmedBodyPartComponent, not Shitmed's BodyPartComponent.
>             if (_wfPart.Get(part).FractureProfile is not { } profileId ||
```
All 3 differing hunks carry an inline `// WOLFGATE` marker. Matches the report's "3 edits, all D8" exactly.
`using Content.Shared.Body;` kept verbatim (P2-D3), confirmed present unchanged.

**`FractureEffectsSystem.cs`** (Onyx 204 lines → WG 207 lines, +3):
```
125a126,128
>     // WOLFGATE: Wolfgate's hands API hands back Hand objects, not hand-id strings ...
132c135 / 135c138 / 139c142 / 141c144 / 144c147 / 146,147c149,150
    (TryGetUsedHandSymmetry body swapped for the WG Hand-object rewrite)
```
The single hunk (the whole `TryGetUsedHandSymmetry` replacement) is preceded by a 3-line `// WOLFGATE` comment
block, satisfying "every differing hunk carries a WOLFGATE marker" — this is one contiguous replaced region,
matching PLAN2 §1.2 P2-D2's literal replacement text verbatim (confirmed character-for-character against
PLAN2 §4/WP10-1's code block). Everything else in the file (all 8 `SubscribeLocalEvent` calls, the
`WoundStatusEffectSystem` dependency and both call sites, `RefreshTransferredPart`, the movement wiring) is
byte-identical to Onyx.

`Content.Shared/_WF/Wolfmed/DoAfter/WolfmedFractureDoAfterSystem.cs` is new `_WF` code (no Onyx equivalent to
diff against) — compared line-for-line against PLAN2 §2.2's code block and found identical, including the
`ref` handler shape and the "no `before:`/`after:` edge" omission.

**`wounds.yml`** (vendored `_Onyx` prototype, not a new file — diffed via `git diff HEAD`): four
`manipulationModifier` values change 0.92/0.84/0.75/0.75 → 1.1/1.25/1.5/2.0, immediately preceded by a 4-line
`# WOLFGATE (DECISIONS §8.2-1)` comment block. This is DECISIONS.md §8.2-1's exact prescribed fix, applied to
exactly the field named and nothing else (`movementModifier`, `threshold`, `creationChance` all confirmed
untouched in the diff).

## 4. Subscriptions

Pairs added in this WP (from `FractureEffectsSystem.cs` + `WolfmedFractureDoAfterSystem.cs`):

| Component | Event | Registrant |
|---|---|---|
| `WoundHostComponent` | `RefreshMovementSpeedModifiersEvent` | `FractureEffectSystem` |
| `WoundHostComponent` | `GetManipulationDurationMultiplierEvent` | `FractureEffectSystem` |
| `WoundFractureComponent` | `FractureGradeChangedEvent` | `FractureEffectSystem` |
| `WoundFractureComponent` | `FractureTreatmentChangedEvent` | `FractureEffectSystem` |
| `WoundFractureComponent` | `WoundRemovedEvent` | `FractureEffectSystem` |
| `WoundableComponent` | `OrganGotInsertedEvent` | `FractureEffectSystem` |
| `WoundableComponent` | `OrganGotRemovedEvent` | `FractureEffectSystem` |
| `WoundableComponent` | `BodyPartFunctionalityChangedEvent` | `FractureEffectSystem` |
| `WoundHostComponent` | `GetDoAfterDelayMultiplierEvent` | `WolfmedFractureDoAfterSystem` |

Grepped all of `Content.Shared`/`Content.Server`/`Content.Client` for each exact pair, excluding the two new
files — **zero hits for every pair**. No duplicates. Matches PLAN2 §5.1 rows 1-9 exactly (registrant names,
free-status, and the reference lists of `WoundHostComponent`'s/`WoundableComponent`'s existing subscriptions
line up with what's actually in the tree).

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for every one of the six touched files:
`FractureAlertSystem.cs` (:79), `FractureEffectsSystem.cs` (:78), `WolfmedFractureDoAfterSystem.cs` (:138),
`wounds.yml` — two rows, the WP7 base row (:106) and a WP10-1 addendum row (:139) documenting the
§8.2-1 fix, `WoundFractureTest.cs` (:131, reconciled with the pre-existing WP9 row). A `### WP10-1` deviations
subsection (after line ~479) records the manipulationModifier fix, P2-D2, P2-D4's two losses, P2-D18,
the `WoundStatusEffectSystem` first-caller note, and P2-D21's fixture rationale. The manifest itself is also
correctly listed as a modified file. Per the orchestrator's override of PLAN2's manifest-rows-WP10-N.md
indirection (confirmed in DECISIONS.md's Phase 2 execution note: "each package appends its rows directly to
`Docs/Wolfmed/WOLFMED_MANIFEST.md`"), this direct-append approach is correct, not a deviation from the binding
instructions.

## 6. Plan conformance

- All 3 files in PLAN2 §4/WP10-1's table exist at their stated destinations, in the correct order dependency
  (`FractureAlertSystem.cs` before `FractureEffectsSystem.cs` before `WolfmedFractureDoAfterSystem.cs`,
  confirmed by content: `FractureEffectSystem` holds `[Dependency] private FractureAlertSystem _fractureAlerts`).
- T-FIXTURE (Serialisation rule 0 / P2-D21) landed as the plan requires: `left hand` slot on
  `WoundFractureBodyGraph`, `- type: Hands` on `WoundFractureBody`, and the symmetric `WoundFractureHandsBody`/
  `WoundFractureHandsBodyGraph` pair, with no test-method assertions added (WP10-6b's job).
- DECISIONS.md §8.2-1 ("FIX... 1.1/1.25/1.5/2.0... `# WOLFGATE` balance comment") is honoured exactly —
  verified above. This correctly **overrides** PLAN2's own P2-D13 ("ship unchanged, escalate"), which the
  report calls out explicitly as an invalidation of P2-D16/§6.2's stale `0.75` prediction — correct and
  properly flagged for WP10-6b's benefit (§5 item 1 of the report).
- The other two DECISIONS §8.2 answers cited in the verification brief (pain sounds with corrected key;
  part-status wound-hosts-only) belong to WP10-5 and WP10-2 respectively, not WP10-1's scope — confirmed WP10-1
  touches neither `EmoteOnDamage*` nor `HealthExaminable*` files. Nothing to check against those two answers
  here beyond confirming WP10-1 stayed out of that scope, which it did.
- P2-D19 (land `FractureAlertSystem` + `FractureEffectSystem` together, in that order, one WP) — honoured.
- The "3 files → 6 touched" deviation is justified: the 3 extra are the T-FIXTURE block (assigned to WP10-1 by
  name in Serialisation rule 0), the wounds.yml §8.2-1 fix (the only WP that consumes
  `manipulationModifier`, and DECISIONS.md's answer post-dates PLAN2's static file table), and the manifest
  update (required by ground rule 6 / the orchestrator's direct-append instruction). No scope creep found
  beyond what's justified.

## 7. Snapshot

```
$ git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p2/snapshots/WP10-1.patch
$ git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p2/snapshots/WP10-1.untracked.txt
```
Written: `WP10-1.patch` (212 lines), `WP10-1.untracked.txt` (3 lines: the two vendored files + the new
`_WF/Wolfmed/DoAfter/` directory entry).

## 8. Fracture tests (run by verifier)

```
$ dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
    --filter "FullyQualifiedName~WoundFractureTest" --logger "console;verbosity=detailed"
...
Test Run Successful.
Total tests: 2
     Passed: 2
 Total time: 1.8452 Minutes
```
Passed on the first run (the `db.ef` sqlite migration warnings were present in the log but did not fail the
run — no re-run was needed). `GradeBoundariesAreDeterministicTest` and
`PostArmorHitAndTreatmentPreconditionsTest` both green, matching the report. Note per report and PLAN2
Serialisation rule 0: `EffectsRefreshOnTreatmentHealingAndDetachTest` (T-FRACT-EFFECTS), the hand-symmetry
test (T-FRACT-HANDS) and the alert tests (T-FRACT-ALERT/-NEG) are correctly still deferred to WP10-6b — WP10-1
was only responsible for the fixture prototypes those tests will need, not the assertions themselves.

---

## Verdict

**PASS.** Both builds green with 0 errors. No unauthorised or oversized upstream edits — the only two modified
tracked files are a vendored `_Onyx` prototype (fully WOLFGATE-marked, matching DECISIONS §8.2-1 verbatim) and
the integration-test fixture block PLAN2 explicitly assigns to this WP. Both new vendored `_Onyx` C# files
diff cleanly against the Onyx pin with every differing hunk WOLFGATE-marked, and match PLAN2's prescribed edit
text verbatim. No duplicate subscriptions among the 9 pairs registered. The manifest carries a row for every
touched file. All 3 planned files plus the assigned fixture and YAML fix exist at their correct destinations.
The two fracture tests pass under my own run. No blockers or majors found.
