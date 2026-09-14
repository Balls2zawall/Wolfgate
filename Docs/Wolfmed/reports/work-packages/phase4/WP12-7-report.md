# WP12-7 — Analyzer client UI and locale (PLAN4 §4 / P4-4b)

Worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**),
Onyx pin `2f5bab9`. No commits. `WG/RobustToolbox` untouched.

---

## 1. Files created / modified

### New — vendored (`_Onyx`)

| File | Status |
|---|---|
| `WG/Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` | **new, byte-verbatim from ONYX, 0 edits**, 133 lines |

### New — Wolfgate glue (`_WF`)

| File | Status |
|---|---|
| `WG/Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml` | **new**, 70 lines — root `BoxContainer`, 4-tab strip + three tab bodies |
| `WG/Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml.cs` | **new**, 422 lines — `Populate` / `Clear` / `ReleaseDamageSection` / `DamageSection` / the three draw methods |
| `WG/Content.Client/_WF/Wolfmed/Medical/HealthAnalyzerWindow.Wolfmed.cs` | **new**, 40 lines — the HOOK 26 bodies `PopulateWolfmed` and `HideWolfmed` |

### New — locale

| File | Status |
|---|---|
| `WG/Resources/Locale/en-US/_Onyx/medical/health-analyzer-component.ftl` | **new**, 15 keys (11 Onyx wound keys verbatim + 4 fracture keys, P4-D25) |
| `WG/Resources/Locale/en-US/_Onyx/targeting/targeting.ftl` | **new**, 10 `targeting-part-*` keys (D9: `chest`→`torso`, `groin` omitted) |

### Modified — upstream

| File | Status |
|---|---|
| `WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` | **HOOK 26**, 3 marked lines (+3 marker comment lines; XML forbids inline comments in an attribute list) |
| `WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml.cs` | **HOOK 26**, 2 marked lines |
| `WG/Resources/Locale/en-US/medical/components/health-analyzer-component.ftl` | **LOC A**, 19 keys appended in one `# WOLFGATE (P4-4)` block |
| `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | `### WP12-7` section appended |

No other file in the tree was touched. **No subscription, no component, no BUI message, no prototype.**

---

## 2. Every WOLFGATE edit and its reason

**Upstream — `HealthAnalyzerWindow.xaml` (HOOK 26 a/b/c):**

1. `xmlns:wolfmed="clr-namespace:Content.Client._WF.Wolfmed.Medical"` on the `FancyWindow` root.
   Marker comment placed on its own line **above** the root element, because a comment cannot sit inside an
   element's attribute list in XML. Onyx marks its own copy of this exact file the same way.
2. `Name="WolfmedDamageGroupsPanel"` on the previously un-named damage-groups `PanelContainer` at `:288`.
   On the **outer** panel, not the inner `GroupsContainer`: hiding only the inner container would leave an
   empty expanded black panel when the Damage tab is not selected.
3. `<wolfmed:WolfmedDiagnosticPanel Name="WolfmedPanel" Visible="False" />` as the last child of
   `RootContainer`. Starts hidden, so any client that never scans a wound host renders as it does today.

**Upstream — `HealthAnalyzerWindow.xaml.cs` (HOOK 26 d/e):**

4. `PopulateWolfmed(msg);` as the **first** statement of `Populate` (CRITIQUE4): `Populate` early-returns on
   `_target == null` or a missing `DamageableComponent`, and a trailing call would leave the panel rendering the
   previous patient's findings beside "No patient data" — reachable once a second just by walking out of range.
5. `HideWolfmed();` inside that early-return block, because a target outside client PVS can still carry
   non-null diagnostics in the message.

**Upstream — `medical/components/health-analyzer-component.ftl` (LOC A):** 19 keys appended under one marked
comment block. Purely additive; no existing key touched or shadowed.

**Vendored file edits:** **none.** `EllipsisLabel.cs` is byte-verbatim.

Only `WolfmedPanel` and `WolfmedDamageGroupsPanel` enter the window's `[GenerateTypedNameReferences]` scope.
Every other name lives in the panel's own scope (the point of P4-D25 PARALLEL). No control is named
`CloseButton`, `ContentsContainer`, `TitleLabel` or `WindowHeader`, and the panel's root is a `BoxContainer`,
so `FancyWindow`'s reserved names are not even in play.

---

## 3. Deviations from PLAN4, with justification

1. **Tab labels.** §2.10's 4-tab strip is `Damage/Wounds/Organs/Chemicals`, so LOC A ships
   `health-analyzer-window-damage-tab` and `-wounds-tab` **instead of** §2.12's
   `health-analyzer-window-whole-body` and `-entity-damage-part-text`. Those two Onyx keys have no consumer in
   Wolfgate — WG's window has `ReturnButton` and `PartNameLabel` where Onyx has a whole-body button and a
   damage-scope label. LOC A is still exactly 19 keys.
2. **Four fracture keys, not two.** P4-D25 names the two summary keys; printing `FractureTreatment` needs words
   for `Reduced`/`Mended` and WG had none, so `health-analyzer-wound-fracture-treatment-reduced` / `-mended`
   were added. The grade word reuses WP12-1's `fracture-grade-*` keys rather than adding four more.
3. **`targeting.ftl` is 10 keys, not "11".** Onyx `:20-32` is 11 part keys; `groin` is dropped under D9
   (trap 18), leaving 10, and `chest` is renamed `torso` to match `PartKey(TargetBodyPart.Torso)`.
4. **HOOK 26 (e) is `HideWolfmed();` rather than the literal `WolfmedPanel.Visible = false;`.** Same one line,
   same site; the body stays in the `_WF` partial per ground rule 2/3 and additionally clears the stale rows and
   restores the damage section. Without the restore, an early return taken while the Wounds tab was selected
   would leave the window with **both** the panel and the damage section hidden.
5. **The mount line carries no `VerticalExpand="True"`** (§3.1's snippet sets it). `RootContainer` is a vertical
   `BoxContainer` and `WolfmedDamageGroupsPanel` is already a `VerticalExpand` child; a second permanently
   expanding sibling would halve the damage section on the Damage tab. `ApplyTab()` sets
   `VerticalExpand = TabBody.Visible`, so the panel expands only while it owns the area.
6. **The `OopsConcat` trick is in the panel, not in `EllipsisLabel`.** §2.10 says to keep it in the vendored
   file; at the pin `EllipsisLabel.cs` has no such helper — it lives at `HealthAnalyzerControl.xaml.cs:516`
   beside `Capitalize`. Both were carried into the panel verbatim, so the sandbox protection is preserved.
7. **No `MaxWidth="430"` on finding labels.** Onyx's window is 790 px wide; WG's is `SetWidth="350"`. Labels use
   `HorizontalExpand` inside an `HScrollEnabled="False"` `ScrollContainer` and wrap to the real width instead.
8. **ASCII glyphs** (`- ` bullet, ` - ` separator, `x2` count) instead of Onyx's `•`, `·`, `×`, matching the
   rest of `_WF`.

**Observation, not changed:** §3.1 puts the mount at `:308`, i.e. **below** the damage-groups section, so on the
Damage tab the tab strip sits at the bottom of the window and on the other three tabs it sits at the top of the
swapped area. Mounting it at `:288` instead would keep the strip in one place. Followed PLAN4 literally; flagged
for WP12-10 or a UI pass to decide.

---

## 4. Build / test output

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

**Headless server**, 120 s, port 1299 → `C:/tmp/wolfmed-plan/p4/wp/WP12-7-report-server.log`:

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
```
`grep -cE "\[ERRO\]|\[FATL\]|Exception"` → **0**. This is the gate for the three FTL files: a duplicate or
malformed Fluent id is logged at startup.

**Headless BUI exercise** → `C:/tmp/wolfmed-plan/p4/wp/WP12-7-report-tests.log`:

```
Passed TestWindows [26 s]
Test Run Successful.
Total tests: 1
     Passed: 1
```
`Content.IntegrationTests/Tests/UserInterface/UiControlTest.TestWindows` instantiates every content `BaseWindow`
with an empty constructor inside a connected client pair. It therefore loads `HealthAnalyzerWindow.xaml`,
resolves the new `wolfmed:` xmlns, constructs `WolfmedDiagnosticPanel` from its own XAML, and runs both
constructors including `ApplyTab()`. That is the headless gate PLAN4 makes available for this package; there is
no headless capture point for `Populate` itself (the payload arrives over the wire — P4-D26).

**Locale audit (static, 50 keys):** every `Loc.GetString` literal the panel can reach — the 44 new keys plus the
reused `fracture-grade-*`, `health-analyzer-window-entity-unknown-value-text` and
`chem-master-window-unknown-reagent-text` — was grepped across `Resources/Locale/en-US`. Each resolves
**exactly once**: 0 missing, 0 duplicates.

**Manual check still owed (cannot be automated here):** scan a wounded human (panel appears; Wounds tab lists
per-part findings; Organs tab shows 7 rows; Chemicals tab lists bloodstream/chemicals/stomach/lungs; Damage tab
gives the section back) and scan an animal or a borg (panel absent, window identical to today). No screenshot or
sprite-pixel test was run — project memory: prefer logic tests, and the user may be working.

---

## 5. What later packages must know

- **The panel is entirely message-driven and stateless between scans except for two things**: the selected tab
  (`_tab`, defaults to `Damage`, survives across patients) and the organ-row cache (`_organRows`, keyed by
  `NetEntity`, cleared by `Clear()`). Anything that wants to add a row should add it inside the panel, not the
  window — the whole point of P4-D25 is that the window's name scope gained exactly two names.
- **`WolfmedDiagnosticPanel.DamageSection`** is a `Control?` the window hands over on the first `Populate`. The
  panel owns that control's `Visible` from then on; nothing else may write it, or the Damage tab breaks.
- **`HideWolfmed()` is the single "nothing to show" path.** Any future early return added to
  `HealthAnalyzerWindow.Populate` must call it, or the panel goes stale. The `Clear()` + `ReleaseDamageSection()`
  pair inside it is what keeps the window looking exactly like today for a non-wound-host (D2).
- **WP12-8 (explosion):** touches nothing this package owns.
- **WP12-9 (tests):** `WolfmedDiagnosticPanel.IsDangerousBloodLevel(float)` is `internal static` and directly
  assertable. Everything else in the panel needs a client harness; the four server builders (WP12-6, P4-D26) are
  the intended assertion point for analyzer content. `UiControlTest` already covers construction, so a new test
  should target payload → text, not XAML loading. Note the fracture detail is rendered from
  `fracture-grade-*` + `health-analyzer-wound-fracture-treatment-*`, so a text assertion must expect e.g.
  `fracture: simple (reduced)`.
- **WP12-10 (reconcile):** upstream files newly touched here are
  `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` and `.xaml.cs` (HOOK 26, one hook number, two
  sites — both first-time Wolfmed touches) and
  `Resources/Locale/en-US/medical/components/health-analyzer-component.ftl` (LOC A, first-time touch). All three
  are already inside §3.4's revised 46/19 arithmetic. **No second licence to record** — `EllipsisLabel.cs`
  carries no header in Onyx and none was invented. The `:308` mount-position observation in §3 above is the one
  open UI question.
- **Phase 5:** the Organs tab reads `WolfmedOrganComponent`, so it shows 7 rows for a human and
  "Organ data unavailable." for every other species until more organs are annotated. The client needs no change
  when that happens.
