# WP7 verification — Prototypes, mob wiring, locale, textures

WG: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
ONYX pin checked: `git -C C:/tmp/onyx rev-parse HEAD` = `2f5bab9946539cbe083010c9ae6fbc59b47ae377` — matches DECISIONS.md.

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  Build succeeded. 0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  Build succeeded. 0 Error(s)
```

Both green. **Pass.**

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests` lists 12 modified files, none new (all new work is under `_Onyx`/`_WF`/`Docs`, reported separately as untracked):

- 8 `.cs` files carried over from WP4/WP5/WP6 (`BloodstreamSystem.cs`, `ChatSystem.Emote.cs`, `HealingComponent.cs`, `HealingSystem.cs`, `HemophiliaSystem.cs`, `DamageableSystem.cs`, `SharedBodySystem.Targeting.cs`, `SharedTargetingSystem.cs`) — untouched by WP7. Diffed all 8: every added/changed line carries a `// WOLFGATE` marker (GUARD E, GUARD E3, GUARD E4, HOOK 6, HOOK 7/D14, HOOK 8, GUARD D/D2, GUARDs A/B/C, HOOK 5) and each corresponds one-to-one to an entry in PLAN §3's authorised hook list. No stray unmarked lines found.
- 4 `.yml` files are WP7's own upstream edits:
  - `Resources/Prototypes/Body/Parts/base.yml` — 9 one-line `parent:` edits, each with an inline `# WOLFGATE (WP7, D8)` comment.
  - `Resources/Prototypes/_Shitmed/Body/Parts/base.yml` — 1 one-line `parent:` edit, same marker.
  - `Resources/Prototypes/Entities/Mobs/Species/base.yml` — `# WOLFGATE` block adding `WoundHost`/`Destructible` and in-place-editing `PassiveDamage.damage` to `{}`; matches D21/D22/D29 and the plan's own WP7 block modulo the documented PassiveDamage deviation (below).
  - `Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml` — one documentation-only `# WOLFGATE` comment, no functional change.

  These four are YAML prototype edits authorised by WP7's own file table and by D8/D9/D20–D22/D29/D32, not by PLAN §3 (which enumerates C# hooks only) — there is no PLAN §3 entry for YAML files, so "corresponds to an authorised hook" is read here as "corresponds to a decision/WP7-table entry", which all four do.

No upstream file outside this list of 12 was touched. **Pass**, no unauthorised upstream edits.

## 3. Vendoring fidelity (`_Onyx` files touched in WP7)

Diffed against `git -C C:/tmp/onyx show HEAD:<path>`, `diff --strip-trailing-cr`:

| File | Result |
|---|---|
| `Resources/Prototypes/_Onyx/Wounds/wounds.yml` | Differs only in: (a) dropped Ipc/Slime/Cybernetic/Plant profiles and their 11 wounds, covered by one top-of-file `# WOLFGATE (WP7)` comment; (b) `Caustic` line gets an inline `# WOLFGATE: D20 reversed` comment; (c) `Chest/Groin` fold to `Torso` with an inline `# WOLFGATE (D9)` comment. No unmarked drift. |
| `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | Byte-identical for all 43 original keys; 4 keys appended after a `# WOLFGATE (WP7)` comment explaining the `health-examinable.ftl` borrow. No unmarked drift. |
| `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | Byte-identical (0 diff). Verbatim as claimed. |
| `Resources/Prototypes/_Onyx/Alerts/alerts.yml` | Only difference is the 6 dropped alert blocks (`ModsuitPower`/`Centered`/`HierophantBeat`/`DragonPower`/`SneakAttack`/`LossOfSurprise`), covered by one `# WOLFGATE (WP7)` comment; `BrokenBones` entry and SPDX header untouched. |
| `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/meta.json` | Byte-identical (0 diff). |
| `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/brokenbones.png` | `cmp` reports identical. |

**Pass**, no unmarked drift.

## 4. Subscriptions

WP7 adds exactly one new `SubscribeLocalEvent`: `WolfmedWoundHostExclusionSystem` → `SubscribeLocalEvent<WoundHostComponent, ComponentInit>(OnWoundHostInit)`.

Grepped all of `Content.Shared`, `Content.Server`, `Content.Client` for `SubscribeLocalEvent<WoundHostComponent, ComponentInit>` — the only hit is this new file. No duplicate.

This pair is **not literally enumerated in PLAN §5** (§5 was written before the protogen-exclusion runtime-system deviation was conceived; §5.2 lists `WoundHostComponent` against `BeforeDamageChangedEvent`, `DamageDealtEvent`, `MapInitEvent`, `RejuvenateEvent`, `BodyPartAddedEvent`/`BodyPartRemovedEvent`, `SleepStateChangedEvent`, `ResolveHealingPartEvent` — no `ComponentInit`). It is, however, a direct and necessary consequence of a documented, justified deviation (WP7-report §3 item 2 / manifest "WP7 Deviations": RT has no YAML-level component-removal verb, so the protogen exclusion PLAN's WP7 section anticipated ("a `- type: WoundHost` removal — or a per-species profile override") had to become a runtime system instead). Independently verified free of collision. Not a blocker; noted as a minor documentation gap in PLAN §5 itself, not in the implementation.

All other subscriptions from earlier WPs re-checked in passing while diffing (WolfmedBodyPartLifecycleSystem's `<WoundHostComponent, BodyPartAddedEvent/BodyPartRemovedEvent>`, WolfmedBedHealMarkerSystem's `<HealOnBuckleComponent, ComponentStartup>`) match §5 exactly and are untouched by WP7.

**Pass** (one pair outside the literal §5 table, but justified and verified collision-free — see minor note above).

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for every file WP7 touched: `wounds.yml`, `wounds.ftl`, `fractures.ftl`, `alerts.yml`, `fracture.rsi` (meta.json+png), `parts.yml`, `Body/Parts/base.yml`, `_Shitmed/Body/Parts/base.yml`, `Entities/Mobs/Species/base.yml`, `protogen.yml`, `WolfmedWoundHostExclusionSystem.cs` — 11 rows tagged `WP7` (report's summary says "12 new rows"; actual count is 11 — trivial off-by-one in the report's own tally, not a missing-row problem). A `### WP7` Deviations subsection and a species inclusion/exclusion list (17 wound hosts + 1 exclusion, with rationale) are both present, plus two new Hazards entries. `Last re-sync` bumped to WP7. **Pass.**

## 6. Plan conformance

All 7 planned WP7 file-table rows exist at their planned destinations, with three explained deviations (all pre-authorised or well-justified by an engine limitation verified against `RobustToolbox` source):

1. `PassiveDamage` neutralisation is an in-place edit of the existing block, not a second appended `- type: PassiveDamage` block as PLAN's literal example text shows. Verified: RT's `ComponentRegistrySerializer` silently drops (logs "defined twice") a second same-type component entry in one file's `components:` list — confirmed by reading the referenced source. A literal second block would have silently no-opped D29 and logged an error on every server start. The in-place edit is functionally identical to D29's intent.
2. Protogen's `WoundHost` exclusion is `WolfmedWoundHostExclusionSystem.cs` (a shared runtime system), not a prototype-level `- type: WoundHost` removal — RT has no such YAML verb. This is the pre-authorised fallback PLAN's own WP7 text anticipated for exactly this failure mode ("or a per-species profile override").
3. `parts.yml` declares 10 new `WolfmedBase<Part>` abstract ids mixed in via `parent:` edits, rather than re-declaring `BaseTorso`/`BaseHead`/etc. directly as `body-organ.md` §5.2's draft literally wrote — this is PLAN's own explicitly pre-authorised "duplicate-id caveat" fallback in WP7 file-table row 6.
4. `wounds.ftl` needed 4 extra keys borrowed from Onyx's unported `health-examinable.ftl` because `BoneFractureWound`'s `examineDescription` fields reference them; PLAN's claim that the file was "complete" was incorrect. Marked with a removal note for WP10.

Independently re-verified the `BaseMobSpeciesOrganic` descendant enumeration: `grep`-found 18 direct children across 5 fork directories (`arachnid, diona, dwarf, gingerbread, human, moth, reptilian, slime, vox` · `chitinid, feroxi, rodentia, vulpkanin` (`_DV`) · `tajaran, yowie` (`_Goobstation`) · `asakim, protogen` (`_Mono`) · `hydrakin` (`_Obelisk`)) — matches PLAN §8.1 item 1 and the manifest exactly. Grepped all 17 non-protogen species files for `Silicon`/`Deathgasp` markers — none found, confirming protogen is the only synthetic in the set.

Decisions cited by WP7 (D3, D8, D9, D20, D21, D22, D29, D32) are all honoured as described above.

**Pass.**

## 7. Snapshot

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP7.patch   (621 lines)
git ls-files --others --exclude-standard -- ... > C:/tmp/wolfmed-plan/snapshots/WP7.untracked.txt   (75 files)
```

Both written. Untracked-file list is exactly WP1–WP7's cumulative new files (StatusEffectNew, `_Onyx` wound/circulation/targeting/CCVar files, `_WF/Wolfmed` compat/body/targeting files, WP7's `_Onyx` locale/prototype/texture files, `_WF/Wolfmed/Body/parts.yml`, `WolfmedWoundHostExclusionSystem.cs`, `Docs/Wolfmed/*`) — nothing unexpected.

## 8. RobustToolbox untouched

```
git -C WG/RobustToolbox diff --stat  → (empty)
git -C WG/RobustToolbox status --porcelain → (empty)
```

**Pass.**

## 9. Duplicate (kind,id) scan

Independent Python/PyYAML permissive-loader scan of all `.yml`/`.yaml` under `Resources/Prototypes`:

```
Files scanned: 4818
Duplicate (kind,id) pairs: 0
Parse errors: 4 (pre-existing, tab characters: _EinsteinEngines/Language/Standard/taucetibasic.yml,
                 _NF/Datasets/Names/goblin_names_{female,last,male}.yml)
```

Matches the report's own gate-1 numbers exactly. **Pass, 0 duplicates.**

## Verdict

No blockers, no majors. One minor: the new `<WoundHostComponent, ComponentInit>` subscription (necessitated by the protogen-exclusion deviation) is not literally listed in PLAN §5's table — independently verified collision-free, and the deviation that required it is well-documented and justified. Another trivial minor: the WP7 report's summary says "12 new rows" in the manifest where 11 are actually present; all required files are documented, just an off-by-one in the report's own count.

**pass = true**
