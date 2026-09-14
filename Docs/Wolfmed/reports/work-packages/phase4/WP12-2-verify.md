# WP12-2 verify — Reagent content, Tier A (P4-1b)

Verifier pass over `WG` (`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`),
Onyx pin `2f5bab9946539cbe083010c9ae6fbc59b47ae377` at `C:/tmp/onyx`. No file touched except this one and the
two snapshot files below.

## 1. Build — PASS

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

## 2. Upstream discipline — PASS

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 7 modified tracked files (plus the manifest doc). Every one not under `_Onyx`/`_WF` was read in full:

| File | Marking | Authorised by | Verdict |
|---|---|---|---|
| `Content.Server/EntityEffects/Effects/HealthChange.cs` | 3 `// WOLFGATE: HOOK 9` blocks (using, `TreatmentCapabilities` datafield, delegate + guard) | PLAN4 §3.1 HOOK 9, WP12-1 | matches the authorised text verbatim, including the untouched `partMultiplier: 1.00f, // Mono, 0.5f->1.00f` line |
| `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` | 3 `// WOLFGATE: HOOK 9` blocks, incl. the required `using System.Linq;` | PLAN4 §3.1 HOOK 9(b), WP12-1 | matches; `var final = dspec * scale` used (not `Damage * scale`) as the plan specifies for this file |
| `Content.Shared/Gibbing/Systems/GibbingSystem.cs` | 2× `.ToArray()` + `using System.Linq; // WOLFGATE` | DECISIONS "Gibbing fix", pre-authorised | matches exactly: two loops snapshotted, nothing else changed |
| `Resources/Prototypes/_Goobstation/Reagents/medicine.yml` | PROTO H, 2-line comment + block | PLAN4 §3.2 PROTO H | `Stasizium` gains `!type:MendFractures {amount:10, wounds:[], minimumGrade:Hairline, maximumGrade:Comminuted}` in `Medicine:`, placed after the −20 heal and before the overdose block, as specified |
| `Resources/Prototypes/Reagents/medicine.yml` | PROTO I | PLAN4 §3.2 PROTO I | `Bicaridine` gains `SuppressPain {0.75/18s/Bicaridine/×1.75}` in `Medicine:`, its only group |
| `Resources/Prototypes/Reagents/narcotics.yml` | PROTO J ×2 | PLAN4 §3.2 PROTO J | `Desoxyephedrine` gets its block in the `Narcotic:` group (not `Poison:`, correctly); `Happiness` gets its block in `Narcotic:` |
| `Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml` | PROTO K | PLAN4 §3.2 PROTO K | `Cognac` gets its block in the `Drink:` group (its only group; Onyx's `Digestion:` does not exist in WG) |

All values (amounts, decay durations, identifiers, recovery multipliers) match PLAN4's table in §3.2 and §8.1
exactly. No line outside these marked blocks was altered in any of the four upstream YAML files (diff shows
insertions only, 0 deletions). `Docs/Wolfmed/DECISIONS.md` and `Docs/Wolfmed/WOLFMED_MANIFEST.md` are docs,
appended-only, out of scope for the marker rule.

**D2** — the two HOOK 9 sites gate the new capability-scoped path on
`args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity)`; the `else Apply()` branch runs the
original unmodified `TryChangeDamage` call for every other entity, byte-for-byte identical to pre-phase-4
behaviour. `SuppressPain.Effect` no-ops without `PainComponent`; `MendFractures.Effect` no-ops without
`WoundHostComponent`. No behaviour change for non-wound-hosts. **PASS.**

No file outside WP12-2's own scope (the pre-existing HOOK 9 / Gibbing edits from WP12-1 and the Gibbing fix)
was touched by this package.

## 3. Vendoring fidelity — PASS

Three new files under `_Onyx/` are this WP's own vendored content; each diffed against
`git -C C:/tmp/onyx show HEAD:<path>` with CRLF-insensitive comparison:

- `Resources/Prototypes/_Onyx/Reagents/Medicine/medicine.yml` — Osteogen, Ibuprofen, Ketorolac, Tramadol,
  Oxycodone blocks compared field-by-field against Onyx's `_Onyx/Reagents/Medicine/medicine.yml` rows
  130-145, 191-242, 243-278, 413-442, 443-472. Every amount, `metabolismRate`, `decayDuration`, `identifier`,
  `recoveryMultiplier`, colour and threshold is Onyx's verbatim. Every differing token is a marked
  translation: `Bloodstream:` → `Medicine: # WOLFGATE: Onyx \`Bloodstream\`` (×5), `TemperatureCondition` →
  `Temperature # WOLFGATE: Onyx \`TemperatureCondition\`` (×2), `ReagentCondition` → `ReagentThreshold #
  WOLFGATE: Onyx \`ReagentCondition\`` (×5), `ModifyBleed` → `ModifyBleedAmount # WOLFGATE: Onyx
  \`ModifyBleed\`` (×1). No unmarked hunk differs from Onyx.
- `Resources/Prototypes/_Onyx/Recipes/Reactions/medicine.yml` — Osteogen, Ibuprofen, Tramadol, Ketorolac
  reactions are byte-identical to Onyx's rows 13-24, 73-100, 130-156 (module reordering, which does not
  change content). Oxycodone's block differs only in `Heroin` → `Ethanol`, carrying a
  `# WOLFGATE (P4-D2)` marker and matching the pre-authorised deviation.
- `Resources/Locale/en-US/_Onyx/reagents/medicine.ftl` — all 10 `reagent-{name,desc}-*` bodies are
  byte-identical to Onyx's `medicine.ftl`.

No unmarked hunk differs from Onyx in any of the three files.

## 4. Collisions — PASS

- Subscriptions / components: none registered by this WP (YAML + FTL only, confirmed by reading the diff —
  no `.cs` file is touched or added by WP12-2 itself).
- New reagent/reaction ids `Osteogen, Ibuprofen, Ketorolac, Tramadol, Oxycodone`: `grep -rn "^  id: X$"
  Resources/Prototypes` → 0 hits outside the two new WP12-2 files.
- New locale keys (10 `reagent-{name,desc}-*` keys): 0 hits outside the one new `.ftl` file.
- No new `EntityEffect`/`EntityEffectCondition` class introduced by WP12-2 (`SuppressPain`, `MendFractures`,
  `TakeStaminaDamage`, `StaminaDamageCondition` are WP12-1's, already registered once; WP12-2 only
  references their `!type:` tags in YAML).

## 5. Manifest — PASS

`Docs/Wolfmed/WOLFMED_MANIFEST.md` §"WP12-2" (line 1341) carries one row per file in the plan's table
(items 1-7; item 8 is the manifest row itself; item 9, Tier B, is correctly recorded as not taken with a
concrete technical reason — Probital's `immediate: true` self-heal inverts into a re-crit under WG's
`StaminaSystem.TakeStaminaDamage`).

## 6. Plan conformance — PASS

Every file in PLAN4's WP12-2 table exists at its destination path. §8.4-4 (Tier A; Tier B only if green with
time to spare) is honoured — Tier A shipped, Tier B explicitly and correctly declined for cause. §8.4-7
(no `treatmentCapabilities` annotation now) is honoured — none added. DECISIONS' P4-D4 (`SalicylicAcid` not
ported, not renamed, not extended) is honoured. The `!type:` translation table (§3, "the metabolism-group
trap") is applied correctly in every one of the 4 upstream edits and the 1 new vendored reagent file.

## 7. Snapshot — DONE

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p4/snapshots/WP12-2.patch          (416 lines)
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs \
  Content.IntegrationTests > C:/tmp/wolfmed-plan/p4/snapshots/WP12-2.untracked.txt   (35 files)
```

Note: both files necessarily also carry WP12-0's and WP12-1's still-uncommitted work (medical patch,
tourniquet-adjacent reagent effect classes, HOOK 9), since phase-4 packages run sequentially in one worktree
without intermediate commits. That is expected and out of WP12-2's own scope; it does not affect this
verdict.

## 8. Non-blocking carry-forward item

The report's own §5 flags that the Release YAML lint is red on
`Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml` (missing `icon:` on two
`construction` prototypes). Confirmed: that file has no `icon:` key anywhere and belongs to WP12-0, not
WP12-2 — `git ls-files --others` places it outside this WP's own table, and WP12-2's own YAML lints clean
per the report's log excerpt. Not a WP12-2 blocker; carried forward to WP12-0's owner / WP12-10 as the report
states.

## Verdict

No blocker or major found. WP12-2 is vendored, marked, non-colliding, plan-conformant, and both builds are
green. **PASS.**
