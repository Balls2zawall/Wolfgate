# WP12-0 verification report — Medical patch (P4-2a)

**Verdict: PASS** — no blockers, no majors.

## 1. Build

Both required builds run sequentially, `-c DebugOpt`, 0 errors:

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Extra corroboration (not in the mandated command list, run anyway since PLAN4 ground rule 5 requires all
three green): `Content.IntegrationTests/Content.IntegrationTests.csproj` also built with **0 Error(s)**,
matching the report's claim.

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows exactly **one** tracked file touched:

```
Content.Shared/Gibbing/Systems/GibbingSystem.cs | 5 +++--
```

That file is not under `_Onyx`/`_WF`. Read in full:

```diff
+using System.Linq; // WOLFGATE
...
-foreach (var ent in container.ContainedEntities)
+foreach (var ent in container.ContainedEntities.ToArray()) // WOLFGATE: snapshot, DropEntity/GibEntity mutate the container
...
-foreach (var ent in container.ContainedEntities)
+foreach (var ent in container.ContainedEntities.ToArray()) // WOLFGATE: snapshot, DropEntity/GibEntity mutate the container
```

Every changed line carries a `// WOLFGATE` marker. This is the pre-phase-4 Gibbing fix DECISIONS.md
explicitly pre-authorises ("The `GibbingSystem.cs` container-mutation crash is fixed with a marked
`.ToArray()` (two loops) before phase 4 starts" / Phase 4 answers: "Gibbing fix:
`Content.Shared/Gibbing/Systems/GibbingSystem.cs` two `.ToArray()` snapshots + `using System.Linq`, all
marked `// WOLFGATE`"). It predates WP12-0 (confirmed already present before this package started) and the
report correctly discloses it as not this WP's own work. No other upstream file was touched — WP12-0's own
goal statement of "zero upstream edits" holds.

**D2:** WP12-0 adds no wound-related code of any kind (`MedicalPatchComponent`/`MedicalPatchSystem` have no
dependency on `WoundHostComponent`, `WoundDamageRoutingSystem`, or any Onyx wounds type — confirmed by
reading both files in full). Trivially D2-compliant: no entity's behaviour — host or non-host — changes as a
side effect of this package.

## 3. Vendoring fidelity

Both `.cs` files, both `.yml` files and the `.ftl` file diffed with `--strip-trailing-cr` against
`git -C C:/tmp/onyx show HEAD:<path>`: **all five are byte-identical, 0 differing lines.**

All 22 RSI files (`GenericPatch{,.1-9}.png`, `GenericPatchBorder.png`, `GenericPatchSmall{,-1..5}.png`,
`GenericPatchSmallCornerLayer.png`, `GenericPatchSmallUsed.png`, `MakeshiftPatch{,Used}.png`, `meta.json`)
extracted from the Onyx pin via `git show` and byte-compared (`cmp`) against the working tree: **all 21 PNGs
identical; `meta.json` identical after `--strip-trailing-cr`** (line-ending only, no content difference).
File count independently reconfirmed at 22.

Zero differing hunks anywhere in `_Onyx/` for this WP, so the "every differing hunk carries a WOLFGATE
marker" rule is vacuously satisfied — there is nothing to mark, consistent with the report's "0 edits" claim
and PLAN4 §2.3/§WP12-0's "verbatim" / "zero edits" specification.

## 4. Collisions

**Subscription pairs** — `<MedicalPatchComponent, EntityStuckEvent>` and
`<MedicalPatchComponent, EntityUnstuckEvent>`, both registered in `MedicalPatchSystem.cs`. Grepped
`SubscribeLocalEvent<MedicalPatchComponent` across all of `Content.Server`/`Content.Shared`/`Content.Client`:
only the two hits inside the new file itself. No duplicate registration — matches PLAN4 §5.1 rows 1–2
("Free? YES").

**Component/tag name** — `MedicalPatch` grepped across `Content.Server`, `Content.Shared`, `Content.Client`,
`Resources` (source only, excluding build output under `obj/`): no pre-existing `[RegisterComponent]` or tag
prototype by that name; the only unrelated hit is a commented-out `# - MedicalPatch # Goobstation` line in
`Resources/Prototypes/_Goobstation/Entities/Clothing/Belt/belts.yml` (dead YAML comment, not a live tag
declaration). Matches PLAN4 §5.3 ("`MedicalPatch` ... 0 hits").

**Prototype ids** — `BaseMedicalPatch`, `UsedMedicalPatch`, `UsedMedicalPatchMakeshift` each appear exactly
once in the repo. `MedicalPatchMakeshift` appears 3 times and `SilkPatchMakeshift` 2 times, but all
occurrences are inside the one new `medical_patch.yml` file, across three different prototype *types*
(`entity`, `constructionGraph`, `construction`), which are separate id namespaces in Robust Toolbox — not a
collision. No other file in the tree declares any of these five ids. The `MedicalPatch` tag id is declared
exactly once. No duplicate-id blocker.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` diff carries one row per file/group touched in this WP:

| Row | Covers |
|---|---|
| `MedicalPatchComponent.cs` | item 1 |
| `MedicalPatchSystem.cs` | item 2 |
| `medical_patch.yml` (entities) | item 3 |
| `medical_patch.yml` (tags) | item 4 |
| `medical_patch.rsi` (22 files) | item 5 |
| `medical_patch.ftl` | item 6 |

All six WP-table items are covered (the RSI's 22 files are one aggregate row, consistent with how earlier
phases record RSI directories). A `## Phase 4` section with a `### WP12-0` subsection was also appended,
recording the licence/attribution, the subscription pairs, the "no deviations" statement and the checkpoint
result — satisfies PLAN4's manifest-row and deviations-block requirements (§7).

## 6. Plan conformance

All 6 files/groups in PLAN4's WP12-0 table exist at the specified paths (verified directly, not just via the
report). No deviation from the table — the report's "None" claim under §3 (Deviations) holds:

- `GroupHealSpecifier` correctly not vendored (P4-D12, confirmed on PLAN4's own "Explicitly NOT touched"
  list in §3.4).
- Zero fill/vending/cargo/loadout placement (P4-D13) — confirmed no `MedicalPatch`/`MedicalPatchMakeshift`
  reference exists anywhere in `Resources/Prototypes/Catalog` or any fill/loadout file.
- RSI file count of 22 independently reconfirmed against the Onyx pin (matches PLAN4's corrected count, not
  the earlier wrong "18"/"24" figures the plan itself flags as superseded).

No DECISIONS.md §8.4 answer names WP12-0 specifically (the eight §8.4 items concern explosion amputation,
organ heal rate, surgery scarring, reagent scope, analyzer UI, organ examine, `treatmentCapabilities`
annotation and pain numbness — all later WPs); none is contradicted by this package. The only phase-4
DECISIONS.md item touching WP12-0's file set is the pre-authorised Gibbing fix, addressed in §2 above.

## 7. Snapshot

Written:

- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-0.patch` (121 lines) — `git diff HEAD -- Content.Shared
  Content.Server Content.Client Resources Docs Content.IntegrationTests`. Contains three file diffs:
  `Content.Shared/Gibbing/Systems/GibbingSystem.cs` (pre-existing, marked, pre-authorised),
  `Docs/Wolfmed/DECISIONS.md` (pre-existing tracked edit from before phase 4 started, untouched by this WP),
  `Docs/Wolfmed/WOLFMED_MANIFEST.md` (this WP's manifest rows). New `_Onyx` files do not appear here because
  they are untracked, not modified-tracked.
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-0.untracked.txt` (27 lines) — the 6 new `_Onyx` files/dirs (2 `.cs`,
  2 `.yml`, 1 `.ftl`, 22 RSI files), i.e. every new file this WP added.

## Summary

WP12-0 is a clean, zero-risk vendoring package: two verbatim server files, three verbatim data files, and a
byte-identical RSI, with no upstream edits of its own and no subscription/name/id collisions. The only
tracked-file change in scope (`GibbingSystem.cs`) is a pre-existing, correctly marked, DECISIONS-authorised
fix that predates this package. Both mandated builds are green with 0 errors; the report's other build/test
claims (IntegrationTests, headless server, DockTest) were spot-corroborated where cheap to do so (the
IntegrationTests build) and found consistent. No blockers, no majors, no minors worth recording.
