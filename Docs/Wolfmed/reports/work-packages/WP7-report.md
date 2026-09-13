# WP7 report — Prototypes, mob wiring, locale, textures

WG worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
Onyx pin: `2f5bab9946539cbe083010c9ae6fbc59b47ae377`

## 1. Files created / modified

New (vendored `_Onyx`, verbatim or lightly edited):

| File | Status |
|---|---|
| `Resources/Prototypes/_Onyx/Wounds/wounds.yml` | modified — organic subset only |
| `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | modified — 43 Onyx keys + 4 added (see §3) |
| `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | verbatim |
| `Resources/Prototypes/_Onyx/Alerts/alerts.yml` | modified — `BrokenBones` only |
| `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{meta.json,brokenbones.png}` | verbatim (CC-BY-SA-3.0, re-checked) |

New (`_WF` Wolfmed code, no license header):

| File | Status |
|---|---|
| `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` | new — 10 `WolfmedBase<Part>` abstracts |
| `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` | new — synthetic-species opt-out (see §3) |

Upstream (`// WOLFGATE`-marked edits only):

| File | Edit |
|---|---|
| `Resources/Prototypes/Body/Parts/base.yml` | 9 one-line `parent:` edits — `BaseHead`, `BaseLeftArm`, `BaseRightArm`, `BaseLeftHand`, `BaseRightHand`, `BaseLeftLeg`, `BaseRightLeg`, `BaseLeftFoot`, `BaseRightFoot` each gain the matching `WolfmedBase<Part>` parent |
| `Resources/Prototypes/_Shitmed/Body/Parts/base.yml` | 1 one-line `parent:` edit — `BaseTorso` gains `WolfmedBaseTorso` |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | `# WOLFGATE` block on `BaseMobSpeciesOrganic`: `- type: WoundHost` (D21), `- type: Destructible` Blunt 400→1500 (D22), existing `- type: PassiveDamage`'s `damage:` zeroed in place (D29) |
| `Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml` | documentation-only `# WOLFGATE` comment on `BaseMobProtogen`; no functional change |

No other upstream files touched. `git status --porcelain` for this WP's touched dirs (Content.Shared/Server/Client, Resources, Docs, Content.IntegrationTests) shows exactly the 4 upstream `.yml` modifications above plus the 8 upstream `.cs` files already modified by WP1–WP6 (untouched by WP7).

`Docs/Wolfmed/WOLFMED_MANIFEST.md` updated: 12 new rows, a species inclusion/exclusion list, a "### WP7" Deviations subsection, and two new Hazards entries. `Last re-sync` bumped to WP7.

## 2. Every `// WOLFGATE` edit and its reason

- **`Resources/Prototypes/Entities/Mobs/Species/base.yml`** — `BaseMobSpeciesOrganic` gains `WoundHost`
  (D21: organic species become wound hosts), a `Destructible` override raising the Blunt gib threshold
  from 400 to 1500 (D22: part damage now projects back onto the mob, so 400 is reachable from routine limb
  damage), and the pre-existing `PassiveDamage`'s `damage:` is zeroed to `{}` in place (D29: Onyx's
  per-part-profile recovery is the only passive heal on wound hosts; Wolfgate's body-level regen on top
  would double-heal and re-interpret `damageCap: 20` against the projected total).
- **`Resources/Prototypes/Body/Parts/base.yml`** (9 lines) / **`Resources/Prototypes/_Shitmed/Body/Parts/base.yml`**
  (1 line) — each limb-typed abstract's `parent:` gains the matching `WolfmedBase<Part>` id so it picks up
  `WolfmedBodyPart` data (fracture profile, max damage, amputation thresholds) at Onyx's numbers. Reason:
  D8 keeps Wolfgate's Shitmed `BodyPartComponent`; Onyx's extra fields live in a separate `_WF` component
  that has to reach every organic limb type somehow, and re-declaring the existing ids directly is not
  possible (see §3).
- **`Resources/Prototypes/_Onyx/Wounds/wounds.yml`** — trimmed to the organic profile/wounds (D3, phase 1
  scope); D9's `Chest`+`Groin` organ-damage-chance fold to one `Torso: 0.04` row; two `# WOLFGATE` comments
  documenting the D9 fold and the D20 reversal (`Caustic` intentionally kept).
- **`Resources/Prototypes/_Onyx/Alerts/alerts.yml`** — one `# WOLFGATE` comment recording that only
  `BrokenBones` is ported; the SPDX header block is preserved verbatim above it.
- **`Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl`** — one `# WOLFGATE` comment above 4
  appended keys (see §3).
- **`Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml`** — one `# WOLFGATE` comment on
  `BaseMobProtogen` pointing at the exclusion system; no data changed.
- **`Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs`** — new `_WF` file, not an edit to
  a vendored file; its own doc-comment explains the D21/D32 rationale.

## 3. Deviations from PLAN.md, with justification

All three deviations stem from the same root cause, verified by reading
`RobustToolbox/Robust.Shared/Serialization/TypeSerializers/Implementations/ComponentRegistrySerializer.cs`
and `PrototypeManager.YamlLoad.cs:228` end to end: **this RT version has no way to remove a component that
a prototype inherited, and re-declaring an existing entity `id:` in a second file throws
`PrototypeLoadException("Duplicate ID: ...")`.** Component lists only ever *add* (parent → child, if the
child lacks the type) or *field-merge* (if the child already declares the same type); a second
`- type: X` entry in one file for the same prototype logs `"Component of type 'X' defined twice in
prototype!"` and is silently dropped.

1. **D29's `PassiveDamage` neutralisation is an in-place edit, not PLAN's literal second `- type:
   PassiveDamage` block.** PLAN's WP7 §4 example YAML appends `- type: WoundHost`, `- type: Destructible`,
   *and* a second `- type: PassiveDamage` after the existing one already declared on
   `BaseMobSpeciesOrganic`. Because that component is already declared earlier in the very same file's
   `components:` list, the second entry would have been the "defined twice" case above: dropped silently
   (D29 never actually applies) and an error logged on every server start (failing this WP's own headless
   smoke-test gate). Fixed by editing the existing block's `damage:` field to `{}` instead. Functionally
   identical to the plan's intent; verified by a clean headless server run (no "defined twice" errors) and
   the YAML linter.
2. **Protogen's `WoundHost` exclusion (D21/D32) is a runtime system, not a prototype-level removal.** PLAN
   says "excluding protogen … by removing WoundHost in its own prototype with a `# WOLFGATE` comment", but
   there is no YAML verb for "remove an inherited component" in this engine. Implemented instead as
   `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs`: a shared `EntitySystem` that
   subscribes `<WoundHostComponent, ComponentInit>` (confirmed free — grepped the whole tree, no existing
   subscriber of that pair), walks `IPrototypeManager.EnumerateAllParents<EntityPrototype>(proto.ID,
   includeSelf: true)`, and `RemCompDeferred<WoundHostComponent>` if `BaseMobProtogen` is among the
   ancestors. Deliberately **shared**, not server-only: `WoundHostComponent` is `[NetworkedComponent]` and
   every GUARD (A/B/C/D) is component-presence-gated per PLAN 8.3 trap 3, so client and server must agree
   on whether protogen carries it or a permanent client/server mispredict results. `protogen.yml` itself
   only gets a documentation comment. The exclusion list (`ExcludedAncestors`) is a one-line addition point
   for future synthetic species — flagged in the manifest's Hazards section.
3. **`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` declares 10 new `WolfmedBase<Part>` ids, not
   re-declarations of `BaseTorso`/`BaseHead`/etc.** `body-organ.md` §5.2's draft YAML (which PLAN's WP7 #6
   cites directly) writes `id: BaseTorso`, `id: BaseHead`, … in the new file — those ids already exist in
   `Resources/Prototypes/{_Shitmed/,}Body/Parts/base.yml` and would hit the same "Duplicate ID" exception.
   PLAN's own WP7 #6 text anticipated exactly this ("if re-declaring an existing abstract id in a second
   file errors, instead declare `WolfmedBase<Part>` abstracts here and add them to each `Base<Part>`'s
   `parent:` list with a `// WOLFGATE` comment") and I took that pre-authorised fallback. All 10 abstracts
   (`WolfmedBaseTorso`, `WolfmedBaseHead`, `WolfmedBase{Left,Right}{Arm,Hand,Leg,Foot}`) carry the same
   Onyx-sourced numbers `body-organ.md` §5.2 specified.

One additional deviation not caused by the component-removal limitation:

4. **`wounds.ftl` needed 4 keys PLAN did not list.** PLAN's WP1/WP7 tables both call `wounds.ftl`
   "verbatim (43 keys, complete — nothing missing)". It is verbatim for the 43 keys it names, but
   `BoneFractureWound`'s four `examineDescription` fields (`wound-examine-fracture-{hairline,simple,
   displaced,comminuted}`) reference keys that live in Onyx's `_Onyx/medical/health-examinable.ftl` — a
   file that belongs to the unported `HealthExaminable` system (WP10) and was never in scope for WP7. The
   Release YAML linter failed with `No localization message found` for all four until I appended them
   (copied verbatim from `health-examinable.ftl`) to the end of `wounds.ftl` with a `# WOLFGATE` comment
   explaining the source and telling WP10 to delete the block (and re-check for a duplicate-key clash) if
   `health-examinable.ftl` is ported later.

No other deviations. D9, D20, D21, D22, D29, D32 are otherwise applied exactly as PLAN specifies.

## 4. Build and validation output

**Gate 1 — duplicate (kind,id) scan**, `python` + PyYAML permissive multi-constructor, 4818 `.yml` files
under `Resources/Prototypes`: **0 duplicate (kind,id) pairs.** 4 pre-existing parse errors, all unrelated
to Wolfmed (tab characters PyYAML rejects but YamlDotNet tolerates): `_EinsteinEngines/Language/Standard/
taucetibasic.yml`, `_NF/Datasets/Names/goblin_names_{female,last,male}.yml`.

**Gate 2 — permissive parse of every touched file:** included in the gate-1 script's run (same loader);
all WP7 files parsed without error.

**Gate 3 — component-name check:** manually verified every `type:` used in touched YAML resolves to a
registered component: `WolfmedBodyPart` → `Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs`;
`WoundHost` → `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:15`; `Destructible`/`PassiveDamage`/
`Barotrauma` → pre-existing engine/content components; the five `!type:Wound*Behavior` tags in `wounds.yml`
→ `Content.Shared/_Onyx/Wounds/WoundBehaviors.cs` (WP4, verbatim). Confirmed a second, stronger way: both
the headless server run and the Release YAML linter load every prototype and report zero "Unknown
component" / prototype-load errors.

**Gate 4 — build + headless server (150 s):**

```
dotnet build Content.Server -c DebugOpt:  Build succeeded. 0 Error(s).
dotnet build Content.Client -c DebugOpt:  Build succeeded. 0 Error(s).
```

Headless server log (`C:/tmp/wolfmed-plan/wp/WP7-server-final.log`, final run after the `wounds.ftl` fix):
`grep -E "\[ERRO\]|\[FATL\]|Exception"` → **0 matches.** Only pre-existing, unrelated `[WARN]` lines appear
elsewhere in the run (duplicate emote-word warnings, present before WP7). Tail:

```
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
[INFO] net: "::": "Network thread started"
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
[INFO] net: "0.0.0.0": "Network thread started"
```

**Gate 5 — YAML linter, Release** (`dotnet run --project Content.YAMLLinter -c Release`): first run found 4
real errors (the missing `wound-examine-fracture-*` keys, §3.4); after the `wounds.ftl` fix, a second run
produced:

```
No errors found in 75293 ms.
```

No `ErrorNode`/`"An item with the same key has already been added"` crash was hit, so the engine's
`mapping.Add()`→`TryAdd()` debug trick was never needed.
`git -C RobustToolbox diff --stat` / `git -C RobustToolbox status --porcelain` → **empty**, confirmed clean
before finishing.

## 5. Notes for later WPs

- **New symbol:** `Content.Shared._WF.Wolfmed.Body.WolfmedWoundHostExclusionSystem` — add a species id to
  its `ExcludedAncestors` set (currently `{"BaseMobProtogen"}`) whenever a fork adds another synthetic
  `BaseMobSpeciesOrganic` descendant. Subscribes `<WoundHostComponent, ComponentInit>` (free pair —
  extending §5 of PLAN.md if anything else ever wants that pair).
- **New prototypes:** `WolfmedBaseTorso`, `WolfmedBaseHead`, `WolfmedBaseLeftArm`, `WolfmedBaseRightArm`,
  `WolfmedBaseLeftHand`, `WolfmedBaseRightHand`, `WolfmedBaseLeftLeg`, `WolfmedBaseRightLeg`,
  `WolfmedBaseLeftFoot`, `WolfmedBaseRightFoot` in `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`. These
  are the mixin point for any future per-limb Wolfmed data — do not also try to attach `WolfmedBodyPart` on
  `BaseTorso`/`BaseHead`/etc directly in a new file; it will collide.
- **Body part coverage confirmed:** all 10 organic limb-type abstracts carry `WolfmedBodyPart` with a
  non-zero `MaxDamage` or `AmputationThresholds`. `BasePartInorganic`, `BaseTorsoInorganic`, `BasePart` do
  not and are not meant to — they're generic bases, not limb types, and only organic species become wound
  hosts in phase 1.
- **Species roster is final for phase 1 pending playtest feedback:** 17 wound hosts, 1 exclusion
  (protogen). Diona and slime run on `OrganicBodyPartProfile` until a phase-5 profile exists — this is
  Onyx-consistent, flagged in the manifest, not a WP7 bug.
- **`wounds.ftl` now carries 4 keys that logically belong to WP10's `health-examinable.ftl`.** When WP10
  ports that file, check for a duplicate-key error and delete the block this WP added to `wounds.ftl`
  (marked with a `# WOLFGATE` comment for easy removal).
- **Open TODOs:** none blocking. `Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` remain
  unported per PLAN (cosmetic, zero YAML references, revisit only if wanted later).
- No commits were made; the tree is left uncommitted per orchestrator policy (DECISIONS.md).

## Summary (< 250 words)

Both builds green (Content.Server, Content.Client — `DebugOpt`, 0 errors each). Headless server ran 150 s
clean, zero `[ERRO]`/`[FATL]`/exceptions, only pre-existing unrelated warnings. Release YAML linter: 0
errors after one fix. Duplicate (kind,id) scan over all 4818 prototype YAML files: 0 duplicates.
RobustToolbox junction confirmed untouched.

Delivered: organic-only `wounds.yml` (D9 Chest+Groin→Torso fold, D20 Caustic kept) and its locale/alert/
texture files; `BaseMobSpeciesOrganic` gains `WoundHost` + raised gib threshold + neutralised
`PassiveDamage` (D21/D22/D29); `WolfmedBodyPart` data wired onto all 10 organic limb-type abstracts; 17 of
18 `BaseMobSpeciesOrganic` descendants ship as wound hosts, protogen excluded as synthetic.

3 deviations, all with the same root cause (this RT build has no YAML mechanism to remove an inherited
component, and re-declaring an existing prototype id throws): PassiveDamage neutralisation edits the
existing block in place instead of appending a second one (PLAN's literal example would have silently
no-opped and logged an error); protogen's exclusion is a small new shared system
(`WolfmedWoundHostExclusionSystem`) instead of a prototype edit; the new per-limb data lives on 10
distinctly-named `WolfmedBase<Part>` abstracts mixed into the existing ones via one-line upstream `parent:`
edits, per PLAN's own pre-authorised fallback for exactly this case. A 4th, unrelated deviation: `wounds.ftl`
needed 4 keys borrowed from Onyx's unported `health-examinable.ftl` to satisfy `BoneFractureWound`'s
examine text — PLAN's claim that the file was "complete" was wrong.

No blockers. Full detail in `Docs/Wolfmed/WOLFMED_MANIFEST.md`.
