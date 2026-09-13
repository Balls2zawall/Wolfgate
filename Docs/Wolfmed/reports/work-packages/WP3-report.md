# WP3 report — Circulation data layer + CCVars + targeting snapshot

## 1. Files created / modified

| # | Onyx source | Wolfgate path | Status |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamComponent.cs` | same | verbatim |
| 2 | `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs` | same | **modified** |
| 3 | `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` | same | verbatim |
| 4 | `Content.Shared/_Onyx/CCVar/CCVars.Surgery.cs` | same | verbatim |
| 5 | `Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs` | same | verbatim |
| 6 | `Content.Shared/_Onyx/Targeting/DamageDistribution.cs` | same | verbatim |
| 7 | `Content.Shared/_Onyx/Targeting/TargetingSnapshotComponent.cs` | same | **modified** |
| 8 | `Content.Shared/_Onyx/Targeting/TargetingSnapshotSystem.cs` | same | **modified** |
| 9 | `Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml` | same | verbatim |

9 files, matching PLAN.md §4 WP3's file count exactly. `Docs/Wolfmed/WOLFMED_MANIFEST.md` was updated (9 new rows + a "### WP3" Deviations subsection); no other file was touched.

## 2. `// WOLFGATE` edits and reasons

**`CirculatoryStreamPrototype.cs`** (D15, PLAN.md §4 WP3#2):
- `using Content.Shared.Metabolism;` → commented out with a `// WOLFGATE (D15/WP3#2)` reason. Wolfgate has **no** `Content.Shared.Metabolism` namespace at all (verified by grep — only `Content.Shared.Body.Prototypes.MetabolismGroupPrototype` and the chemistry metabolism classes live elsewhere), so leaving the `using` in place is a build error (`CS0246`) independent of whether the two fields below are commented. This is a deviation from the plan's literal instruction ("comment out `MetabolismStage`/`MetabolitesStage`") — the `using` line also had to go, or the fields' comment-out alone would not have compiled. Recorded in the manifest.
- `MetabolismStage` / `MetabolitesStage` `[DataField]`s commented out, same reason tag. `MetabolismStagePrototype` does not exist in Wolfgate and nothing in the phase-1 wound path reads either field (D15).

**`TargetingSnapshotComponent.cs`** (D9, D10, PLAN.md §4 WP3#7):
- Added `using Content.Shared._Shitmed.Targeting;` — Onyx's file has no explicit `using` for its own `Content.Shared._Onyx.Targeting` namespace (it's declared inside it), so with D10 skipping Onyx's own `TargetBodyPart`, an addition (not a literal replace) is what resolves the type. Recorded as a manifest deviation-note since the plan's wording ("using swap") could be read as a literal find/replace that isn't present in the source.
- `RequestedTarget = TargetBodyPart.Chest` → `TargetBodyPart.Torso`, tagged `// WOLFGATE (D9)`. `TargetBodyPart.Chest` does not exist in Shitmed's enum; D9 maps Chest→Torso.

**`TargetingSnapshotSystem.cs`** (D10, PLAN.md §4 WP3#8):
- Added `using Content.Shared._Shitmed.Targeting;` — same reasoning as above; resolves `TargetingComponent`, `TargetBodyPart`, and `SharedTargetingSystem.IsSelectable` (HOOK 5, already landed in WP2). No other line changed — Shitmed's `TargetingComponent.Target` field name and type already match what the vendored code reads (`targeting.Target`), verified by reading `Content.Shared/_Shitmed/Targeting/TargetingComponent.cs`.

No upstream (non-`_Onyx`, non-`_WF`) files were touched by WP3. HOOK 5 (`IsSelectable`) was already present from WP2's work, confirmed by grep — no double-add.

## 3. Deviations from PLAN.md, with justification

1. **`using Content.Shared.Metabolism;` removed, not just the two fields** — see above; the plan's WP3 table entry #2 only mentions the two data fields, but the `using` alone is a compile error in Wolfgate. Justified and recorded in the manifest's "### WP3" deviations subsection.
2. **The two Targeting files' "using swap" is an addition** — Onyx's files carry no explicit `using Content.Shared._Onyx.Targeting;` (own-namespace resolution), so nothing was literally "swapped"; one `using Content.Shared._Shitmed.Targeting;` line was added to each. Net effect matches the plan's intent (§2's WP3 table prose already describes it this way); flagged so a future re-sync doesn't look for a `using` line that was never there to replace.

Both are cosmetic/mechanical clarifications of the plan's wording, not behavioural departures from what §4 WP3 specifies. Zero CCVar name or cvar-string collisions were found (re-verified directly against `Content.Shared/CCVar/*.cs` and `Content.Shared/_WF/CCVar/*.cs`, matching the plan's claim). `CCVars` in Wolfgate is confirmed `public sealed partial class CCVars : CVars` in namespace `Content.Shared.CCVar` — identical to Onyx's own file headers, so no namespace adaptation was needed anywhere in WP3.

## 4. Build checkpoint (exact tail)

**Content.Server:**
```
Build succeeded.
    0 Error(s)
```

**Content.Client:**
```
Build succeeded.
    0 Error(s)
```

Both green on the first attempt after the edits above (no iteration needed). YAML prototype (`circulatoryStream`/`Organic`) was not separately smoke-tested against a running headless server in this WP — the file has only `id: Organic` plus all-default fields, and no other WP yet references `CirculatoryStreamPrototype`, so there is nothing at this point that would exercise prototype loading beyond what `dotnet build` already validates (schema types resolve, `[Prototype]` attribute compiles). Recommend WP6/WP9 include it in their existing prototype-load assertions.

## 5. Notes for later WPs

- **New symbols available:** `Content.Shared._Onyx.Chemistry.Circulation.{CirculatoryStreamComponent, CirculatoryStreamPrototype}` (with `MetabolismStage`/`MetabolitesStage` absent — do not reference them until a phase-5 metabolizer rewrite adds `MetabolismStagePrototype`); `Content.Shared.CCVar.CCVars.{Wounds*, Surgery*, Targeting*}` cvars (all verified collision-free); `Content.Shared._Onyx.Targeting.DamageDistribution` enum; `Content.Shared._Onyx.Targeting.{TargetingSnapshotComponent, TargetingSnapshotSystem}` (system not yet `Initialize()`d by anything but its own `ThrownEvent` subscription — that's Onyx's own behaviour, verbatim).
- **`CirculatoryStreamSystem.cs` (server-side, D15) is explicitly out of scope for WP3** — it's WP6's job, trimmed to `GetPartStream` + primary-stream branches per D15. `SharedSolutionContainerSystem.CirculatoryStreams.cs` stays permanently skipped.
- **Open TODO carried forward, not introduced by WP3:** none. WP3's file set is inert data/config with zero behaviour, exactly as its goal states — nothing here blocks or changes WP4's plan.
- WP1 and WP2 were already present in the tree (uncommitted) when WP3 started; this report only covers WP3's own 9 files plus the manifest edit.

## Summary (for the calling script)

Both builds green (Content.Server and Content.Client, 0 errors each). 9 files created/modified exactly as PLAN.md §4 WP3 specifies (3 verbatim CCVar files, verbatim CirculatoryStreamComponent/DamageDistribution/YAML, 3 files with small `// WOLFGATE` edits). Two minor deviations from the plan's literal wording, both mechanical (the Metabolism `using` line also had to go, not just the two fields; the Targeting files needed an *added* `using` since no literal one existed to swap) — both recorded in `Docs/Wolfmed/WOLFMED_MANIFEST.md`. Zero CCVar/cvar-string collisions confirmed. No upstream files touched. No blockers for WP4.
