# WP12-2 — Reagent content, Tier A (P4-1b)

Worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**),
Onyx pin `2f5bab9` at `C:/tmp/onyx`. Nothing committed; `WG/RobustToolbox` untouched; no
`git stash/clean/checkout --/reset` at any point.

## 1. Files created / modified

| # | Path (WG-relative) | Status |
|---|---|---|
| 1 | `Resources/Prototypes/_Onyx/Reagents/Medicine/medicine.yml` | **new** (163 lines) — Tier-A subset of ONYX `_Onyx/Reagents/Medicine/medicine.yml`: `Osteogen`, `Ibuprofen`, `Ketorolac`, `Tramadol`, `Oxycodone` |
| 2 | `Resources/Prototypes/_Onyx/Recipes/Reactions/medicine.yml` | **new** (69 lines) — 5 reactions, Ketorolac's precursors ordered above it, Oxycodone re-authored |
| 3 | `Resources/Locale/en-US/_Onyx/reagents/medicine.ftl` | **new** (10 keys + 2 `# WOLFGATE` header lines) |
| 4 | `Resources/Prototypes/_Goobstation/Reagents/medicine.yml` | **modified — PROTO H**, +7 −0 (Stasizium `MendFractures`) |
| 5 | `Resources/Prototypes/Reagents/medicine.yml` | **modified — PROTO I**, +7 −0 (Bicaridine `SuppressPain`) |
| 6 | `Resources/Prototypes/Reagents/narcotics.yml` | **modified — PROTO J**, +14 −0 (Desoxyephedrine + Happiness `SuppressPain`) |
| 7 | `Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml` | **modified — PROTO K**, +8 −0 (Cognac `SuppressPain`) |
| 8 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — `### WP12-2` appended (7 rows + notes), **+150 −0, purely additive** |

No C#, XAML, RSI or test file was touched. All four upstream YAML edits are **insert-only** (0 deleted lines
across the whole package, manifest included). Encodings preserved: `_Goobstation/Reagents/medicine.yml` is
still UTF-8-with-BOM/CRLF; the three other upstream YAML files are still **ASCII/CRLF** (em dashes in my
comment lines were normalised to `-` afterwards so the files did not silently change encoding class); the
three new files are UTF-8/CRLF (the `.ftl` and the recipes file are ASCII).

## 2. Every WOLFGATE edit and its reason

**New vendored files (in-file `# WOLFGATE` markers, not upstream hooks)**

- `_Onyx/Reagents/Medicine/medicine.yml` — a 6-line `# WOLFGATE` header recording the provenance, the Tier-A
  scope and the two translation classes; then per-site markers:
  - `Medicine: # WOLFGATE: Onyx \`Bloodstream\`` on all five reagents. **Wolfgate has no `Bloodstream`
    metabolism group** (`Poison, Medicine, Narcotic, Alcohol, Food, Drink, Gas, PlantMetabolisms` +
    `_NF`'s `Cryogenic`) and the key is `ProtoId<MetabolismGroupPrototype>`-validated, so a copied header is a
    whole-file prototype-load failure (§8.5 trap 4). All five are `group: Medicine` in Onyx.
  - `!type:ReagentThreshold # WOLFGATE: Onyx \`ReagentCondition\`` ×5 (same `min`/`max`/`reagent` fields).
  - `!type:ModifyBleedAmount # WOLFGATE: Onyx \`ModifyBleed\`` ×1 (Ketorolac).
  - `!type:Temperature # WOLFGATE: Onyx \`TemperatureCondition\`` ×2 (Ibuprofen's two `AdjustTemperature`).
  - Everything else is SAME and unmarked: `HealthChange`, `AdjustTemperature`, `GenericStatusEffect`,
    `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `PlantAdjustWeeds`, `PlantAdjustHealth`.
    Every amount, `metabolismRate`, `decayDuration`, `identifier`, `recoveryMultiplier`, colour, `flavor` and
    `physicalDesc` is Onyx's verbatim.
- `_Onyx/Recipes/Reactions/medicine.yml` — a 2-line provenance header plus **one** marked deviation, the
  Oxycodone recipe (§3 item 1).
- `_Onyx/reagents/medicine.ftl` — a 2-line `# WOLFGATE` header saying the bodies are Onyx's verbatim and that
  the keys for unported reagents are omitted.

**Upstream edits (PROTO H–K, four files, one marked block each)**

| Site | Block | Group chosen, and why |
|---|---|---|
| **PROTO H** `_Goobstation/Reagents/medicine.yml`, `Stasizium` | `!type:MendFractures { amount: 10, wounds: [], minimumGrade: Hairline, maximumGrade: Comminuted }` | `Medicine:` — Stasizium's only group in WG. Inserted after the five-group −20 heal and before the overdose block, matching Onyx's ordering. **P4-D3: extended in place, no `OnyxStasizium`.** WG's `HealthChange`-vs-`EvenHealthChange` and `-50000`-vs-`-1000000` temperature were left alone (Wolfgate balance, D4 does not reach it) |
| **PROTO I** `Reagents/medicine.yml`, `Bicaridine` | `!type:SuppressPain { 0.75 / 18 s / Bicaridine / ×1.75 }` | `Medicine:` — Bicaridine's only group in WG; Onyx's is `Bloodstream:` |
| **PROTO J** `Reagents/narcotics.yml`, `Desoxyephedrine` | `!type:SuppressPain { 0.75 / 9 s / Desoxyephedrine / ×1.75 }` | **`Narcotic:`, not `Poison:`** — WG splits this reagent across `Poison`/`Narcotic`/`Medicine` and every drug effect (movespeed, Stutter, Jitter, the two Adrenaline statuses) lives in `Narcotic`. A `Poison:` placement lints clean and is silently wrong |
| **PROTO J** `Reagents/narcotics.yml`, `Happiness` | `!type:SuppressPain { 0.4 / 9 s / Happiness / ×1.5 }` | `Narcotic:` — Happiness's only group in WG |
| **PROTO K** `Reagents/Consumable/Drink/alcohol.yml`, `Cognac` | `!type:SuppressPain { 0.25 / 9 s / Painkiller / ×1.1 }` | **`Drink:`** — Onyx uses a `Digestion:` group Wolfgate does not have. Cognac redeclares the whole `Drink:` block rather than inheriting `BaseAlcohol`'s, so the block goes in Cognac's own. Deviation 24: stomach metabolizer, not liver |

Each block carries a two-line `# WOLFGATE (PROTO x)` comment naming the Onyx source and the group
translation. No existing line in any of the four files was altered or removed.

**Pre-flight audits (all run before writing)**

- Reagent ids: `grep -rn "^  id: X$" Resources/Prototypes` → **0 hits** for `Osteogen`, `Ibuprofen`,
  `Ketorolac`, `Tramadol`, `Oxycodone`. Same grep covers reaction ids (same shape), so all five reaction ids
  are free too.
- Locale ids: all 10 `reagent-{name,desc}-*` keys → 0 hits in `Resources/Locale`. The referenced
  `reagent-physical-desc-{opaque,thick,pungent}` keys exist (`reagents/meta/physical-desc.ftl:39,45,75`).
- Effect classes: every `!type:` in the shipped YAML resolves to a class that exists in WG after WP12-1 —
  `SuppressPain`, `MendFractures`, `TakeStaminaDamage` (`Content.Shared/_WF/Wolfmed/EntityEffects/*`),
  `HealthChange`, `AdjustTemperature`, `ModifyBleedAmount`, `GenericStatusEffect`, `PlantAdjustWeeds`,
  `PlantAdjustHealth`, `ReagentThreshold`, `Temperature` (all `Content.Server/EntityEffects/**`).
- Datafield names checked against the classes, not assumed: `ReagentThreshold{Min,Max,Reagent}`,
  `Temperature{Min,Max}`, `ModifyBleedAmount{Amount,Scaled}`, `GenericStatusEffect{Key,Component,Time}`,
  `SuppressPain{Amount,DecayDuration,Identifier,RecoveryMultiplier}`,
  `MendFractures{Wounds,MinimumGrade,MaximumGrade,Amount}`, `TakeStaminaDamage{Amount,Immediate}`.
  `FractureGrade` has `Simple`/`Comminuted`; `BoneFractureWound` exists (`_Onyx/Wounds/wounds.yml:136`).
- `key: Adrenaline` + `component: IgnoreSlowOnDamage` is established WG usage (`Reagents/medicine.yml:441`,
  `narcotics.yml:76,152,238`) and `IgnoreSlowOnDamageComponent` exists.
- Reaction reactants are **plain strings, not `ProtoId`** (`ReactionPrototype.cs:28`), so the linter cannot
  catch a typo — each was grepped by hand: `Bicaridine, Milk, Phosphorus, Charcoal, Benzene, Fluorine,
  Acetone, Inaprovaline, Ethanol, Carbon, Plasma, Epinephrine` all present, `Heroin` absent (0 hits).
- **Subscription pairs registered: none. Components registered: none.** Package is YAML + FTL only.

## 3. Deviations from PLAN4

1. **Oxycodone's recipe is re-authored (this is PLAN4's own P4-D2 instruction, recorded as the deviation it
   is).** PLAN4 offers "Tramadol + Acetone + Plasma or Tramadol + Ethanol + Plasma; pick one". I substituted
   **`Heroin` → `Ethanol`** and kept Onyx's other three entries (`Tramadol`, `Epinephrine`, `Plasma` catalyst)
   and the 1u yield, because that is a strictly smaller departure from Onyx than dropping `Epinephrine` as
   well — `Epinephrine` exists in WG (`Reagents/medicine.yml:362`). Marked `# WOLFGATE (P4-D2)` in the file.
2. **Tier B (`Probital` + `Mitogen`) was NOT taken**, and not for time. Probital's payload is
   `!type:TakeStaminaDamage { amount: -100, immediate: true }` gated on `StaminaDamageCondition { min: 100 }`
   — a stamina *heal* fired at a target already in stamina crit. Under P4-D5, WG's
   `StaminaSystem.TakeStaminaDamage` (`:288-292`) does `if (component.Critical && immediate) {
   EnterStamCrit(uid, component, true); return; }`, i.e. it **returns without applying the value**. Onyx's
   self-rescue branch therefore inverts into a re-crit in Wolfgate. That needs a balance decision (drop
   `immediate`, or hook the crit branch), which is outside WP12-2. `Mitogen` is a Probital by-product with no
   standalone recipe, so it was dropped with it. Re-entry cost: 2 reagents, 4 reactions (`Probital` +
   3× `Mitotrophin*`), 4 locale keys, 1 decision.
3. **No medkit / chem-dispenser / cargo / vending placement**, per PLAN4's deleted item 8 and serialisation
   rule 2 — the five Tier-A reagents are reaction-only (chemist-craftable), matching Onyx. `firstaidkits.yml`
   is WP12-3's file and was not touched.
4. **Cosmetic:** in the four upstream files the `# WOLFGATE` comment text uses an ASCII `-` rather than an em
   dash, so three previously-ASCII files stay ASCII. `_Goobstation/Reagents/medicine.yml` was already
   UTF-8-with-BOM and keeps its em dash.

Everything else — every amount, duration, identifier, recovery multiplier, metabolism rate, colour, flavour,
plant metabolism, reaction yield and `minTemp` — is Onyx's value unchanged. P4-D4 was honoured:
`SalicylicAcid` is not ported, not renamed, not extended.

## 4. Build / test output tails

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
    # (first attempt failed with CS2012 "Robust.Shared.dll ... used by another process" — a leftover csc
    #  build-server lock from the immediately preceding Content.Server build, not a code error. Re-run clean.)

$ dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Headless server, ~120 s on port 1299 (`WP12-2-report-server.log`, 112 lines):

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
```

`grep -cE "\[ERRO\]|\[FATL\]|Exception"` → **0**. No unknown `!type:`, no duplicate reagent or reaction id, no
missing metabolism group, no Fluent duplicate id, no `Duplicate Subscriptions` throw. The only `[WARN]` lines
are the pre-existing set (`PullingSystem` command-bind notice, emote-word duplicates, `MainLoop: Cannot keep
up!`).

Release YAML lint — **the real gate for this WP** (`dotnet run --project Content.YAMLLinter -c Release`):

```
::error file=/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml,line=-1,col=-1::
  /Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml(-1,-1)  File not found. (/Textures)
1 errors found in 194826 ms.
```

**Zero errors attributable to WP12-2.** The one error is in WP12-0's file — see §5.

Test filter `_Onyx.Wounds|_Onyx.Body|_Onyx.Medical|Wolfmed` (`WP12-2-report-tests.log`):

```
Test Run Successful.
Total tests: 65
     Passed: 65
 Total time: 4.3754 Minutes
```

No `db.ef` re-run was needed.

## 5. What later packages must know

1. **BLOCKING, handed to WP12-0's owner / WP12-10: the Release YAML lint is red on
   `Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml`.** The two
   `- type: construction` prototypes there (`MedicalPatchMakeshift`, `SilkPatchMakeshift`) carry no `icon:`,
   and WG's `ConstructionPrototype.Icon` defaults to `SpriteSpecifier.Invalid`
   (`Content.Shared/Construction/Prototypes/ConstructionPrototype.cs:53-54`), which the Release linter
   resolves as the empty texture path `/Textures`. Onyx's copy has no `icon:` either, so it is a strict-lint
   divergence rather than a transcription error — the headless server starts clean and the item works in
   game. Fix: one `icon: { sprite: _Onyx/Objects/Medical/medical_patch.rsi, state: MakeshiftPatch }` per
   construction prototype (the RSI and both states exist). **I did not edit it: PLAN4 §4 gives that file to
   WP12-0.** The phase cannot go green in CI until someone does.
2. **The metabolism-group mapping is now shipped fact and must not be "restored" to Onyx's headers in any
   later re-sync:** `Osteogen/Ibuprofen/Ketorolac/Tramadol/Oxycodone/Stasizium/Bicaridine` → `Medicine:`;
   `Desoxyephedrine/Happiness` → `Narcotic:`; `Cognac` → `Drink:`.
3. **WP12-9's reagent tests can rely on these ids existing**: reagents `Osteogen, Ibuprofen, Ketorolac,
   Tramadol, Oxycodone`; reactions of the same five names; locale `reagent-{name,desc}-{osteogen, ibuprofen,
   ketorolac, tramadol, oxycodone}`. `T-REAGENT-PROTOTYPE-SANITY` should assert the group mapping in item 2
   (the lint cannot catch a valid-but-wrong group). A good `SuppressPain` fixture reagent is `Bicaridine`
   (Medicine group, `0.75 / 18 s`), and `Stasizium` is the only `MendFractures` source that reaches a
   Comminuted fracture (`wounds: []`, `amount: 10`); `Osteogen` is capped at `maximumGrade: Simple`.
4. **Balance note for §8.3:** `Stasizium` is now a universal fracture cure and is stocked in
   `MedkitCombatStasiziumFilled` and `StasiziumAutoInjector`. The knob is `maximumGrade` on PROTO H's block.
5. **Tier B is open**, blocked on the `immediate`-mode decision in §3 item 2, not on effort.
6. `firstaidkits.yml`, `healing.yml`, both `_Shitmed` surgery YAMLs and every `Content.IntegrationTests` file
   were untouched and remain free for their owners.
