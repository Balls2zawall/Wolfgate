# WP12-0 report — Medical patch (P4-2a)

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.Server/_Onyx/Medical/MedicalPatchComponent.cs` | created, vendored verbatim from ONYX (0 edits) |
| `Content.Server/_Onyx/Medical/MedicalPatchSystem.cs` | created, vendored verbatim from ONYX (0 edits) |
| `Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml` | created, verbatim |
| `Resources/Prototypes/_Onyx/Tags/medical_patch.yml` | created, verbatim |
| `Resources/Textures/_Onyx/Objects/Medical/medical_patch.rsi/*` (22 files: 21 PNG + `meta.json`) | created, byte-copied |
| `Resources/Locale/en-US/_Onyx/medical/medical_patch.ftl` | created, verbatim (8 keys) |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified — appended 7 table rows + `## Phase 4` / `### WP12-0` deviations section |

No other file was touched. `Content.Shared/Gibbing/Systems/GibbingSystem.cs` and `Docs/Wolfmed/DECISIONS.md`
were already modified in the tree before this package started (pre-existing phase-4 setup, not this WP's
work) and were left untouched.

## 2. WOLFGATE edits

**None.** Zero upstream edits, per PLAN4 WP12-0's own goal statement. Both vendored `.cs` files compiled
with 0 changes against `git -C C:/tmp/onyx show HEAD:<path>` — verified by direct diff during authoring.
Every dependency (`IGameTiming`, `SharedSolutionContainerSystem`, `ReactiveSystem`, `StickySystem`,
`SharedHandsSystem`, `ISharedAdminLogManager`, `UnremoveableComponent`, `BaseHealingItem`, `StickyVisualizer`,
`MixableSolution`, `Trash`/`SpiderCraft` tags, `Cloth`/`WebSilk` materials, `construction-category-tools`)
was confirmed present in WG before vendoring, and `MedicalPatch` (component name and tag) was confirmed
free by grep before creation.

## 3. Deviations from PLAN4

**None.** This package matches PLAN4 §4 WP12-0 exactly:
- `GroupHealSpecifier` was NOT vendored — PLAN4 P4-D12 rules it out entirely (its only consumers are the
  unrelated Vampire feature), and `MedicalPatchComponent.cs`/`MedicalPatchSystem.cs` were read in full and
  reference it nowhere. The task brief's conditional ("if PLAN4 vendors it here") resolves to no.
- Zero fill/vending/cargo/loadout placement, mirroring Onyx exactly (P4-D13) — `MedicalPatchMakeshift` is a
  field-craft item only (1 Cloth or 4 WebSilk, 5 s do-after), placed nowhere in Onyx's own prototypes.
- File count for the RSI independently re-confirmed at 22 (21 PNG + `meta.json`) via
  `git -C C:/tmp/onyx ls-tree -r --name-only HEAD` on the pinned commit, matching PLAN4's corrected count.

## 4. Build / test output

**Content.Server** (`-c DebugOpt`):
```
Build succeeded.
    0 Error(s)
```

**Content.Client** (`-c DebugOpt`):
```
Build succeeded.
    0 Error(s)
```

**Content.IntegrationTests** (`-c DebugOpt`, build only per WP12-0's checkpoint — no test filter specified
for this package):
```
Build succeeded.
    0 Error(s)
```

**Headless server** (~120 s, port 1299; full log at `C:/tmp/wolfmed-plan/p4/wp/WP12-0-report-server.log`):
```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
```
No `[ERRO]`, `[FATL]`, or `Exception` lines. No `Duplicate Subscriptions` throw. The only `[WARN]` lines are
pre-existing and unrelated to Wolfmed (`PullingSystem` command-bind notice, emote-word duplicates across
species, `MainLoop: Cannot keep up!` under the headless smoke-test CPU load).

**DockTest** (project-memory pre-check; log at `C:/tmp/wolfmed-plan/p4/wp/WP12-0-report-tests.log`):
```
Total tests: 3
     Passed: 3
Test Run Successful.
```

## 5. What later packages must know

- `Content.Server/_Onyx/Medical/` now exists and holds only the medical patch pair — WP12-3 (Tourniquet)
  creates a sibling `Content.Server/_Onyx/Medical/Tourniquet/` subdirectory; no collision.
- The `MedicalPatch` component and tag names, and all five new prototype ids
  (`BaseMedicalPatch`, `MedicalPatchMakeshift`, `UsedMedicalPatch`, `UsedMedicalPatchMakeshift`, plus the two
  construction graph/recipe id pairs `MedicalPatchMakeshift`/`SilkPatchMakeshift`), are now taken — re-grep
  before reusing any of them.
- Two subscription pairs are now live: `<MedicalPatchComponent, EntityStuckEvent>` and
  `<MedicalPatchComponent, EntityUnstuckEvent>`, both server-side in `MedicalPatchSystem`. No later WP may
  re-register either pair.
- A new artist/licence attribution entered the tree: `@jorgun  inspired by Studenterhue of Goonstation`,
  CC-BY-SA-3.0, recorded in the manifest. Distinct from phase 3's Ubaser wound-visuals attribution.
- The manifest's Phase 4 section now exists (`## Phase 4 (2026-09-13)` with a `### WP12-0` subsection) —
  WP12-1 onward should append their own `### WP12-N` subsections after it, and WP12-10 reconciles at the end
  per serialisation rule 4.
