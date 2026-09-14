# WP12-7 verification — Analyzer client UI and locale (P4-4b)

**Verdict: PASS.** No blockers, no majors. One minor (documentation-only marker convention gap).

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)
```

Both green, re-run independently in this pass (600000 ms timeout each, both finished well inside it).

## 2. Upstream discipline

`git diff HEAD --stat` over Content.Shared/Content.Server/Content.Client/Resources/Content.IntegrationTests
shows 18 modified tracked upstream files across all phase-4 work packages so far. Exactly three belong to
WP12-7:

- `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` — **HOOK 26**, 3 marked code changes (each
  preceded by its own `<!-- WOLFGATE: HOOK 26 -->` comment, +3 comment lines since XML forbids an inline
  comment inside an attribute list): the `xmlns:wolfmed` namespace on the `FancyWindow` root, `Name=
  "WolfmedDamageGroupsPanel"` added to the pre-existing un-named damage-groups `PanelContainer` at the
  original `:288`, and `<wolfmed:WolfmedDiagnosticPanel Name="WolfmedPanel" Visible="False" />` mounted as
  the last child of `RootContainer`. Matches PLAN4 §3.1 HOOK 26 (a)/(b)/(c) verbatim.
- `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml.cs` — **HOOK 26**, 2 marked lines:
  `PopulateWolfmed(msg); // WOLFGATE: HOOK 26` as the literal first statement of `Populate`, and
  `HideWolfmed(); // WOLFGATE: HOOK 26` inside the `_target == null` / no-`DamageableComponent` early-return
  block, immediately before `return;`. **Deviation, disclosed and justified (report §3 item 4):** PLAN4's
  hook table literally specifies `WolfmedPanel.Visible = false;` at site (e); the shipped code calls
  `HideWolfmed()` instead, which also calls `Clear()` and `ReleaseDamageSection()`. Same site, same line
  count (1 marked line), and it is the only way to satisfy the ground rule that hook bodies live in `_WF`
  partials — a literal `WolfmedPanel.Visible = false;` would also leave the damage section permanently
  hidden if the early return fires while the Wounds/Organs/Chemicals tab was selected. Accepted as a
  compliant, better-justified substitute for the plan's literal snippet, not a scope violation.
- `Resources/Locale/en-US/medical/components/health-analyzer-component.ftl` — **LOC A**, 19 keys appended
  in one `# WOLFGATE (P4-4)` block, purely additive, no existing key touched. Counted by hand: 19 exactly,
  matching PLAN4 §3.1 LOC A and the report.

All three are inside `_Onyx`/`_WF`-adjacent upstream sites PLAN4 §3 explicitly authorises for WP12-7,
correctly `// WOLFGATE`/`<!-- WOLFGATE -->` marked, one/two-line hooks with all logic in `_WF` partials.
**D2:** `PopulateWolfmed` (in the new `_WF` partial, verified §3 below) hides the panel outright whenever
`msg.WoundDiagnostics == null && msg.Organs == null` — exactly PLAN4's stated D2 gate — so a non-wound-host
scan renders the window byte-for-byte as it does today; the two HOOK 26 code-behind lines only ever touch
the new `WolfmedPanel`/`WolfmedDamageGroupsPanel` names, never any pre-existing rendering path.

The other 15 modified upstream files (`EvenHealthChange.cs`, `HealthChange.cs`, `HealthAnalyzerSystem.cs`,
`GibbingSystem.cs`, `HealthAnalyzerScannedUserMessage.cs`, `SurgeryWoundedConditionComponent.cs`,
`SharedSurgerySystem.cs`, five reagent YAML files, two surgery YAML files, `firstaidkits.yml`, `healing.yml`)
belong to WP12-0 through WP12-6 and already carry their own PASS verify docs
(`C:/tmp/wolfmed-plan/p4/wp/WP12-{0..6}-verify.md`); spot-checked here for a `// WOLFGATE` marker on at least
one added line per file (all 15 have one or more) and out of this WP's scope otherwise.
`Content.Shared/Gibbing/Systems/GibbingSystem.cs`'s two `.ToArray()` snapshots are the DECISIONS-pre-
authorised phase-4 gibbing fix, correctly marked, not part of WP12-7's own edit set.

## 3. Vendoring fidelity

Diffed against `git -C C:/tmp/onyx show HEAD:<path>` with `--strip-trailing-cr`:

| File | Result |
|---|---|
| `Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` | **0 diff** — byte-verbatim (only the "no newline at end of file" diff marker on both sides, i.e. identical content). Matches the report's claim exactly. |
| `Resources/Locale/en-US/_Onyx/medical/health-analyzer-component.ftl` | **2 hunks.** (1) The 5 lines of Onyx's disease keys + the dead `health-analyzer-wound-pain` key are dropped, replaced by a 2-line header comment explaining the omission. (2) 4 fracture keys appended under `# WOLFGATE (P4-D25): ...`. The 11 wound keys in between (`part-summary` through `clotting-mixed`) are verified **byte-identical** to Onyx, line for line. |
| `Resources/Locale/en-US/_Onyx/targeting/targeting.ftl` | **2 hunks** (large deletion of the 19 unrelated Onyx keys this file also carries — `ui-options-*`, `targeting-ui-*`, `targeting-status-*`, `repair-mode-*`, none of which have a Wolfgate consumer — plus the `chest`→`torso` rename and `groin` drop for D9). Header carries `# WOLFGATE (D9): ...`, correctly marked. |

**Minor finding (not a blocker):** hunk (1) of `health-analyzer-component.ftl`'s diff (the disease-key
omission) is documented with a plain comment — `# Wound findings shown by the Wolfmed diagnostic panel.
The four disease keys and the dead health-analyzer-wound-pain key from Onyx's file have no consumer in
Wolfgate and are not ported.` — that does **not** contain the literal token `WOLFGATE`, unlike every other
divergence-marking header in this WP and in the rest of `_Onyx/` (`targeting.ftl`'s own header two lines
below it, `entity-categories.ftl`, `entity-effects.ftl`, `reagents/medicine.ftl`, `quirks.ftl` — all grepped,
all say `# WOLFGATE`). The intent is unambiguous and the omission is correct per the report, but the literal
marker convention established elsewhere in `_Onyx/` is not followed on this one hunk. Cosmetic; recommend a
one-word fix (`# WOLFGATE: ...`) whenever the file is next touched, no functional or build impact.

## 4. Collisions

- **`SubscribeLocalEvent` / `RegisterComponent` pairs:** none. Grepped
  `Content.Client/_WF/Wolfmed/Medical/` and `Content.Client/_Onyx/Medical/HealthAnalyzer/` for
  `SubscribeLocalEvent`, `RegisterComponent`, `[DataDefinition]`, `class.*Component` — zero hits. Matches
  PLAN4 §5.1's statement that WP12-7 registers nothing (no new BUI message either — HOOK 26 reads the
  existing `HealthAnalyzerScannedUserMessage` only).
- **New class names:** `WolfmedDiagnosticPanel` (declared once, `WolfmedDiagnosticPanel.xaml.cs:26`),
  `EllipsisLabel` (declared once, `EllipsisLabel.cs:15`), `WolfmedDiagnosticTab` (new enum, declared once).
  Grepped `class WolfmedDiagnosticPanel` / `class EllipsisLabel` across the whole tree (excluding
  RobustToolbox): zero hits outside the two new files. `HealthAnalyzerWindow` itself is extended via
  `public sealed partial class HealthAnalyzerWindow` in the new `_WF` file, which is a partial-class
  extension of the existing upstream class (already `public sealed partial` at
  `HealthAnalyzerWindow.xaml.cs:33`), not a duplicate declaration.
- **Control names:** the panel's 13 `Name=` attributes (`DamageButton`, `WoundsButton`, `OrgansButton`,
  `ChemicalsButton`, `TabBody`, `WoundsTab`, `VitalDamageRow`, `VitalDamageLabel`, `WoundStateLabel`,
  `WoundFindingsContainer`, `OrgansTab`, `OrgansContainer`, `ChemicalsTab`, `ChemicalsContainer`) live in the
  panel's own `[GenerateTypedNameReferences]` scope (root is `BoxContainer`, not a window), so
  `FancyWindow`'s `WindowTitle`/`HelpButton`/`CloseButton`/`ContentsContainer` and `DefaultWindow`'s
  `TitleLabel`/`WindowHeader` are not even reachable. None of the 13 collide with any reserved name in any
  case. Only `WolfmedPanel` and `WolfmedDamageGroupsPanel` enter the window's own scope, and neither
  collides with any of that window's pre-existing 27+ `Name=` attributes (spot-checked: no prior
  `WolfmedPanel`/`WolfmedDamageGroupsPanel` existed before this WP's diff).
- **New prototype ids:** none registered by this WP.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-7` section (line 1755) with one row for every file
this WP touches — `EllipsisLabel.cs`, `WolfmedDiagnosticPanel.xaml`, `WolfmedDiagnosticPanel.xaml.cs`,
`HealthAnalyzerWindow.Wolfmed.cs`, `HealthAnalyzerWindow.xaml`, `HealthAnalyzerWindow.xaml.cs`, the two new
`_Onyx` locale files, the modified LOC A file, and the manifest section itself — 9 rows total, matching the
report's 8-file table plus the manifest's own self-row. The section also states the subscription/component/
BUI-message tally ("none"/"none"/"none") and the build/test checkpoint, matching PLAN4 §7.2's WP12-7 rows.

## 6. Plan conformance

All 7 items in PLAN4 §WP12-7's table exist:

| # | File | Present |
|---|---|---|
| 1 | `Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` | yes, byte-verbatim |
| 2 | `Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml` | yes |
| 3 | `Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml.cs` | yes |
| 4 | `Content.Client/_WF/Wolfmed/Medical/HealthAnalyzerWindow.Wolfmed.cs` | yes |
| 5 | `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` + `.xaml.cs` | yes (modified, HOOK 26) |
| 6 | 2 new `_Onyx` locale files + 1 modified LOC A file | yes, 15 + 10 + 19 keys respectively |
| 7 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` append | yes |

Cited decisions honoured:
- **P4-D25 (§8.4 decision 5, Analyzer UI shape):** PARALLEL taken as directed — self-contained `_WF` panel,
  4-tab strip inside the panel (not the window), no window-geometry change, 5 upstream lines total (3 XAML +
  2 code). `SetWidth="350"` is untouched.
- **D2 gate:** verified in `PopulateWolfmed` (§2 above) — exact `WoundDiagnostics == null && Organs == null`
  test PLAN4 specifies.
- **D9 (chest→torso, groin omitted):** `targeting.ftl` ships `targeting-part-torso` (not `-chest`) and no
  `-groin` key; `SharedTargetingSystem.GetValidParts()` (pre-existing, unmodified by this WP) already has
  `TargetBodyPart.Groin` commented out, so the panel's `PartKey()` lookup can never be asked to resolve a
  `groin` key. Confirmed by reading both the enum and `GetValidParts()`.
- **Reserved-name trap:** confirmed clear (§4 above) — only `WolfmedPanel`/`WolfmedDamageGroupsPanel` enter
  the window's own scope, matching "under P4-D25 only WolfmedPanel enters that scope."
- **Style:** no license header in any new `_WF` file; `EllipsisLabel.cs` carries no header, matching Onyx's
  own headerless original — no header invented either side.

The report's 8 disclosed deviations from PLAN4 (§3 of the report — tab labels, four fracture keys instead of
two, 10-not-11 targeting keys, `HideWolfmed()` instead of the literal one-liner, no `VerticalExpand="True"`
on the mount line, `OopsConcat` relocated into the panel, no `MaxWidth="430"`, ASCII glyphs instead of Onyx's
Unicode ones) were each checked against the actual diff/source and are all real, all correctly justified by
either a hard constraint (WG's narrower window, `EllipsisLabel.cs` having no `OopsConcat` at this pin) or a
D2/consistency requirement, and none silently changes plan-mandated behaviour. The one open observation
(§3.1's mount position at `:308`, putting the tab strip at the bottom of the window on the Damage tab) is
correctly flagged as unresolved and deferred to WP12-10/a UI pass, exactly as the report states — not a
defect in this WP.

Locale audit re-checked by spot sample rather than full re-grep of all 50 keys: `health-analyzer-window-
entity-unknown-value-text`, `chem-master-window-unknown-reagent-text`, `health-analyzer-window-damage-tab`,
`health-analyzer-wound-fracture-treatment-reduced`, `targeting-part-torso` each resolve **exactly once**.
The four `fracture-grade-*` keys the panel reuses (from WP12-1's guidebook ftl) exist and match
`FractureGrade`'s four non-`None` values exactly. `FractureTreatment.Reduced`/`.Mended`,
`BodyPartFunctionalityState.Impaired`/`.Disabled`/`.Unavailable`, and `HealthAnalyzerClottingPhase.InProgress`/
`.Complete`/`.Mixed` were all read from their enum definitions and match the corresponding
`Loc.GetString($"...-{value.ToString().ToLowerInvariant()}")` keys exactly — no runtime missing-key path for
any reachable enum value.

## 7. Snapshot

Written:
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-7.patch` (1115 lines — cumulative worktree diff across
  Content.Shared/Server/Client/Resources/Docs/Content.IntegrationTests, not WP12-7-only, per the task's own
  scope for this command)
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-7.untracked.txt` (55 lines — cumulative untracked file list;
  confirmed it lists all 5 of WP12-7's own new files: `EllipsisLabel.cs`, `HealthAnalyzerWindow.Wolfmed.cs`,
  `WolfmedDiagnosticPanel.xaml`, `WolfmedDiagnosticPanel.xaml.cs`, the two new `_Onyx` locale files)

## Server / test log (re-checked)

`C:/tmp/wolfmed-plan/p4/wp/WP12-7-report-server.log`: reaches `Server Version 277.0.0.0 -> Ready`,
`grep -cE "\[ERRO\]|\[FATL\]|Exception"` → 0, re-verified in this pass.
`C:/tmp/wolfmed-plan/p4/wp/WP12-7-report-tests.log`: `UiControlTest.TestWindows` — Passed, 1/1, re-verified.

## Summary

No blockers, no majors. Both builds green with 0 errors. The three upstream files WP12-7 touches carry
exactly the marked hooks PLAN4 §3 authorises (one disclosed, justified deviation at HOOK 26(e), same site,
same line count). `EllipsisLabel.cs` is byte-verbatim against Onyx; the two new `_Onyx` locale files match
Onyx line-for-line wherever they overlap, with every divergence except one bearing an explicit `# WOLFGATE`
marker (the one exception — the disease-key omission comment in `health-analyzer-component.ftl` — states
the same rationale without the literal token, a cosmetic gap, not a scope or behaviour problem). No new
subscriptions, components, or prototypes were registered; no class-name or control-name collisions found.
The manifest carries a complete WP12-7 section with a row per file, and all 7 planned deliverables exist.
D2, D9, and P4-D25 (§8.4 decision 5) are all honoured and independently verified against source, not just
asserted by the report.
