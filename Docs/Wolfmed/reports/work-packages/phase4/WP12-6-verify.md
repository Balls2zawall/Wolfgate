# WP12-6 verification — Analyzer payload and server (P4-4a)

**Verdict: PASS.** No blockers, no majors.

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)
```

Both green, matching the report's claim (report also independently built `Content.IntegrationTests`, not
required by this checklist but consistent with a clean tree).

## 2. Upstream discipline

`git diff HEAD --stat` over the tracked worktree shows 15 modified upstream files total across all phase-4
work packages so far. Only two of them belong to WP12-6:

- `Content.Server/Medical/HealthAnalyzerSystem.cs` — **HOOK 23**, exactly one appended line:
  `BuildWoundDiagnostics(target), BuildOrganInfo(target), BuildChemicalInfo(target, bloodstream), BuildVitalDamage(target) // WOLFGATE: HOOK 23`
  after the existing `part != null ? GetNetEntity(part) : null,`. No `using`, no `[Dependency]` landed
  upstream — matches PLAN4 §3.1 HOOK 23 verbatim.
- `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` — **EXT 2**, matches PLAN4 §3.2 EXT 2
  verbatim: 2 marked `using`s, 4 marked nullable fields after `Uncloneable`, 4 appended optional trailing
  ctor parameters after `NetEntity? part = null`, one marked assignment block (`// WOLFGATE: EXT 2 start/end`).
  Purely additive; `CryoPodSystem.cs:206-221`'s nine positional arguments still compile (build confirms).

The other 13 modified files (`EvenHealthChange.cs`, `HealthChange.cs`, `SurgeryWoundedConditionComponent.cs`,
`SharedSurgerySystem.cs`, five reagent YAML files, three surgery YAML files, `firstaidkits.yml`,
`healing.yml`) belong to WP12-1/WP12-2/WP12-3/WP12-4/WP12-5 and are out of this WP's scope.
`Content.Shared/Gibbing/Systems/GibbingSystem.cs` carries the pre-phase-4 fix DECISIONS pre-authorised
(two `.ToArray()` snapshots + `using System.Linq`, both marked `// WOLFGATE`) — present and correctly marked,
not part of WP12-6's own edit set.

Both WP12-6 hunks carry a `// WOLFGATE` marker, are one/few lines at the authorised site, with all logic in
`_WF`/`_Onyx` files. **D2:** every new builder call is additive to the wire message only — no gameplay
behaviour changes. `BuildWoundDiagnostics`, `BuildOrganInfo` and `BuildVitalDamage` all gate on
`HasComp<WoundHostComponent>` and return `null` for non-hosts (verified in the builder file, §3 below);
`BuildChemicalInfo` is deliberately not wound-gated per PLAN4 §2.9 (chemicals are not a wound feature), so a
non-host's message now also carries reagent-contents data it didn't before — this is the documented,
plan-authorised design (§5.2: "four appended nullable fields... are strictly cheaper than a second round
trip"), not a gameplay behaviour change.

## 3. Vendoring fidelity

Diffed against `git -C C:/tmp/onyx show HEAD:<path>` with `--strip-trailing-cr`:

| File | Result |
|---|---|
| `Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` | **1 hunk**, exactly the claimed one: `using Content.Shared._Onyx.Targeting;` → `using Content.Shared._Shitmed.Targeting; // WOLFGATE: D10 ...`. Marked. |
| `Content.Shared/_Onyx/Medical/HealthAnalyzerOrganInfo.cs` | **0 diff** — byte-verbatim (after CRLF normalisation), as claimed. |
| `Content.Shared/_Onyx/Medical/HealthAnalyzerChemicalInfo.cs` | **0 diff** — byte-verbatim, as claimed. |

`HealthAnalyzerSystem.Wolfmed.cs` is a `_WF` file, not `_Onyx`-vendored, so it is not subject to the
byte-diff check, but its logic was compared line-by-line against Onyx's
`Content.Server/Medical/HealthAnalyzerSystem.cs:337-515` (`BuildWoundDiagnostics`/`BuildOrganInfo`/
`BuildChemicalInfo`): every deviation claimed in the report is real and is the only deviation present —
the `WoundHostComponent` gate (D2), the `GetTargetBodyPart` substitution for `TryConvert` (missing API),
`WolfmedOrganComponent.Health`/`OrganComponent.SlotId` for D8, and `ChemicalSolutionName` for
`MetabolitesSolutionName`. Everything else (fracture/bleeding/scar/internal-bleeding/clotting-phase/
visible-wound accumulation, `HasFindings` gate, `AddChemicalInfo` helper) is copied line-for-line.
`BuildVitalDamage` has no direct Onyx counterpart method (Onyx inlines the `CheckVitalDamage` call at its
call site); the `_WF` wrapper is a faithful 5-line extraction, as the report describes.

## 4. Collisions

- **`SubscribeLocalEvent` pairs:** none added by this WP (grepped the new file and the three vendored
  files — zero hits for `SubscribeLocalEvent`/`RegisterComponent`/`EntityEffect`). Matches PLAN4 §5.1's
  explicit statement that WP12-6 registers nothing.
- **New type names** (`HealthAnalyzerWoundDiagnostic(s)`, `HealthAnalyzerVisibleWound`,
  `HealthAnalyzerClottingPhase`, `HealthAnalyzerOrganInfo`, `HealthAnalyzerChemicalInfo`,
  `HealthAnalyzerSolutionType`, `HealthAnalyzerReagentInfo`) — grepped for `class`/`record`/`record struct`/
  `enum` definitions anywhere else in `Content.Shared`/`Content.Server`/`Content.Client`: **zero hits**
  outside the new files. No collision.
- **New components:** none registered by this WP.
- **New prototype ids:** none registered by this WP.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-6` section (line 1649) with a row for every one of
the 7 files in the WP table, the full WOLFGATE-edit list (HOOK 23, EXT 2, the D10 vendored-file edit, the
three builder deviations), the new-type-name grep record, the deviations-from-PLAN4 note, and the build/
server checkpoint. Matches PLAN4 §7.2's WP12-6 rows.

## 6. Plan conformance

All 7 files in PLAN4 §WP12-6's table exist:

| # | File | Present |
|---|---|---|
| 1 | `Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` | yes |
| 2 | `Content.Shared/_Onyx/Medical/HealthAnalyzerOrganInfo.cs` | yes |
| 3 | `Content.Shared/_Onyx/Medical/HealthAnalyzerChemicalInfo.cs` | yes |
| 4 | `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` | yes (modified) |
| 5 | `Content.Server/_WF/Wolfmed/Medical/HealthAnalyzerSystem.Wolfmed.cs` | yes |
| 6 | `Content.Server/Medical/HealthAnalyzerSystem.cs` | yes (modified) |
| 7 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | yes (appended) |

Cited decisions honoured: **D2** (WoundHostComponent gate, verified §2/§3 above), **D8** (WolfmedOrganComponent
+ OrganComponent.SlotId, verified), **D9** (no `Groin` key possible — `GetTargetBodyPart` returns `null` for
unmappable parts, which the builder `continue`s past), **D10** (the one marked `using` swap in
`HealthAnalyzerWoundDiagnostic.cs`, verified byte-exact), **D26** (all four builders are `public`, verified
in the source), **D27** (`BuildPartDamage` is absent from the new file — confirmed not ported).
DECISIONS.md §8.4 answer **"§8.4-5 Analyzer UI: PARALLEL"** is not this WP's concern (WP12-7); WP12-6's own
scope has no §8.4 item directly gating it beyond the general phase-4 execution rules, which are honoured
(vendored files marked, glue in `_WF`, hooks one line, no commits, RobustToolbox untouched).

Two textual (non-behavioural) deviations from PLAN4 are disclosed in the report and are legitimate compile
requirements, not scope drift: `BuildChemicalInfo` returns `List<HealthAnalyzerChemicalInfo>?` (nullable,
matching PLAN4 §2.9's own signature — never actually returns null) and `OrganOrder(string? slotId)` instead
of `string` (WG's `OrganComponent.SlotId` is nullable; a null slot falls to the `_ => 10` arm like an
unknown id would).

## 7. Snapshot

Written:
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-6.patch` (981 lines — full worktree diff across Content.Shared/
  Server/Client/Resources/Docs/Content.IntegrationTests, i.e. cumulative phase-4-to-date, not WP12-6-only)
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-6.untracked.txt` (49 lines — cumulative untracked file list)

## Server log

`C:/tmp/wolfmed-plan/p4/wp/WP12-6-report-server.log`: reached `Server Version 277.0.0.0 -> Ready`,
`grep -cE "\[ERRO\]|\[FATL\]|Exception"` → 0. Re-verified in this pass.

## Summary

No blockers, no majors, no minors. Both builds green with 0 errors, the two upstream hooks are exactly the
single/appended-line changes PLAN4 §3 authorises for WP12-6 and are WOLFGATE-marked, the three vendored
`_Onyx` files diff byte-exact against ONYX HEAD apart from the one documented D10 `using` swap, the `_WF`
builder file's every deviation from Onyx's logic is accounted for by a cited decision (D2/D8/D9), no new
subscriptions/components/prototypes were registered (matching PLAN4 §5.1's "registers nothing"), no
collisions were found on any of the 8 new type names, the manifest has a complete WP12-6 section, and all 7
planned files exist.
