# WP8 verification (round 2) — Bridge hardening: AP passthrough, armour, thresholds, execution

**Verifier run:** 2026-09-12, second pass. Worktree `WG` = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`. Onyx ref = `C:/tmp/onyx` @ `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (confirmed via `git -C onyx log -1 --format=%H`).

**Context:** round 1's `WP8-verify.md` (preserved in git history of this file / superseded here) failed the WP on one blocker (U1) and two majors (S1, V1). `WP8-report.md`'s "Fix round 1" section claims all three were resolved by reverting `HurtCommand.cs`, moving HOOK 10's part-armour subscription into a new `_WF` file, and deleting the unverifiable locale file. This round re-verifies the fixes from scratch and re-runs every check.

**Verdict: PASS.** Both builds green. All three round-1 findings (U1 blocker, S1 major, V1 major) are confirmed resolved. No unmarked upstream drift, no unmarked vendoring drift, no duplicate subscriptions, manifest and file-table coverage complete. One pre-existing minor (PLAN §5.2 documentation gap for the new `_WF` subscription) remains, disclosed by the report and not blocking.

---

## 1. Build

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```
Both green, matches the report's fix-round-1 logs (`WP8-fix1-server.log`, `WP8-fix1-client.log`). **PASS.**

---

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests` shows the cumulative uncommitted diff since HEAD (no commits per DECISIONS.md), walked file-by-file:

| File | WP (per PLAN §3) | Marker check |
|---|---|---|
| `Content.Server/Body/Systems/BloodstreamSystem.cs` | WP6 (GUARD E, E3) | every added line under `// WOLFGATE` — OK |
| `Content.Server/Chat/Systems/ChatSystem.Emote.cs` | WP4 (HOOK 6) | one-word change, marked — OK |
| `Content.Server/Medical/Components/HealingComponent.cs` | WP6 (HOOK 7) | additive block, marked start/end — OK |
| `Content.Server/Medical/DefibrillatorSystem.cs` | **WP8 (HOOK 12)** | marked, exact substitution PLAN specifies — OK |
| `Content.Server/Medical/HealingSystem.cs` | WP6 (HOOK 8, explicitly allowed >2 lines) | large additive block, all under `// WOLFGATE` start/end markers — OK |
| `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` | WP6 (GUARD E4) | marked — OK |
| `Content.Shared/Armor/SharedArmorSystem.cs` | **WP8 (HOOK 10)** | marked; **narrowed since round 1** — now carries exactly the `OnDamageModify` wound-host branch + `ApplyWoundSystemicArmor` + one `using`, nothing else — OK |
| `Content.Shared/Damage/Systems/DamageableSystem.cs` | WP5 (GUARD D, D2/D23/D27) | marked — OK |
| `Content.Shared/Execution/SharedExecutionSystem.cs` | **WP8 (HOOK 13)** | marked, exact line PLAN specifies — OK |
| `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` | **WP8 (HOOK 11)** | marked at both cited sites — OK |
| `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | WP5 (GUARD A/B/C) | marked — OK |
| `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` | WP2 (HOOK 5) | marked — OK |
| `Resources/Prototypes/Body/Parts/base.yml` | WP7 (D8) | marked — OK |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | WP7 (D20/D21/D22/D29/D32) | marked — OK |
| `Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml` | WP7 (D21/D32) | marked (comment) — OK |
| `Resources/Prototypes/_Shitmed/Body/Parts/base.yml` | WP7 (D8) | marked — OK |

**U1 resolved:** `Content.Server/Damage/Commands/HurtCommand.cs` and `Resources/Locale/en-US/damage/damage-command.ftl` are byte-identical to `HEAD` again —
```
$ git diff HEAD -- Content.Server/Damage/Commands/HurtCommand.cs Resources/Locale/en-US/damage/damage-command.ftl
warning: ... LF will be replaced by CRLF the next time Git touches it   (x2, no patch output)
```
Empty diff on both files (confirmed twice, with and without `--strip-trailing-cr`); the only output is git's autocrlf line-ending notice, not a content difference. Neither file appears in `git diff HEAD --stat` any more. The withdrawn feature is preserved at `C:/tmp/wolfmed-plan/wp/WP8-hurtcommand-deferred.patch` (verified present, 7400 bytes, starts with the expected `HurtCommand.cs` header) for later re-authorisation. **No upstream file outside PLAN §3's authorised list is edited. Blocker cleared.**

Every added/changed line in every remaining upstream file carries a `// WOLFGATE` (or `# WOLFGATE`) marker or sits inside a marked block — no unmarked upstream drift found anywhere in the diff.

---

## 3. Vendoring fidelity (`_Onyx/` and `StatusEffectNew/` files touched in WP8)

Two vendored files are modified per the report's table (items 5–6); diffed against `git -C C:/tmp/onyx show HEAD:<path>` with `diff --strip-trailing-cr`:

- **`Content.Shared/_Onyx/Wounds/WoundEvents.cs`** — one hunk differs from Onyx (the `PartDamageModifyEvent` primary constructor gains `float armorPenetration = 0f` + a readonly `ArmorPenetration` field). Both differing lines carry `// WOLFGATE: D23...`. **OK.**
- **`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs`** — large accumulated diff (all WPs to date; file was never committed). Every differing hunk — usings, `[Dependency]` swaps, the `_routedModifiers`/`_appliedDelta` side tables, the `before:` ordering args, the `TargetBodyPart.Chest`→`Torso` folds (D9), the hand-symmetry rewrite, the `_wfPart.Get(...)` indirection (D8), the `HealOnBuckleComponent`→`WolfmedBedHealMarkerComponent` swap, and the WP8-specific `_routedModifiers.GetValueOrDefault(body).ArmorPenetration` passed into the `PartDamageModifyEvent` construction — carries an inline `// WOLFGATE` marker. **No unmarked drift. OK.**

**V1 resolved:** `Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` — the file whose Onyx provenance could not be verified — no longer exists:
```
$ ls Resources/Locale/en-US/_Onyx/commands/
No such file or directory
$ find Resources/Locale/en-US/_Onyx -type d
Resources/Locale/en-US/_Onyx
Resources/Locale/en-US/_Onyx/medical
Resources/Locale/en-US/_Onyx/prototypes
Resources/Locale/en-US/_Onyx/prototypes/wounds
```
The empty `commands/` directory was removed along with the file. The manifest's WP8 Deviations section carries the disclosure (verified at manifest line ~394). **Cleared.**

No other `_Onyx` or `StatusEffectNew` file shows a diff attributable to WP8 beyond the two above.

---

## 4. Subscriptions

```
$ grep -rn "PartDamageModifyEvent" Content.Shared Content.Server Content.Client   (excluding obj/bin)
Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:741:            var modify = new PartDamageModifyEvent(
Content.Shared/_Onyx/Wounds/WoundEvents.cs:138:public sealed class PartDamageModifyEvent(
Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:25:        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
```

| Pair | Registrant | Duplicate check | Matches PLAN §5? |
|---|---|---|---|
| `ArmorComponent`, `InventoryRelayedEvent<PartDamageModifyEvent>` | `Content.Shared._WF.Wolfmed.Armor.WolfmedPartArmorSystem` (**S1 fix: moved out of upstream `SharedArmorSystem.cs`**) | Exactly one declaration, one raise site, one `SubscribeLocalEvent`, one handler in the whole tree. **No duplicate.** | **No** — PLAN §5.2's pair-audit table does not list this pair. This is a pre-existing documentation gap, not a code defect: `ArmorComponent.Modifiers` is a public `[DataField]` so the `_WF` system needs no upstream access grant, and the port's own ground rules explicitly permit new `_WF` systems and new subscriptions on new/relayed event types without upstream edits. **Minor, disclosed by the report, does not block** — needs an orchestrator bookkeeping entry in PLAN §5.2 (WP8 agent cannot edit PLAN.md). |

**S1 resolved:** the subscription no longer lives in the upstream file. `Content.Shared/Armor/SharedArmorSystem.cs`'s diff (§2 above) now contains **only** the `OnDamageModify` wound-host branch + `ApplyWoundSystemicArmor` helper + one `using` — i.e. exactly PLAN §3 HOOK 10's authorised text, confirmed by direct inspection. The relocated handler in `WolfmedPartArmorSystem.cs` is bit-identical in behaviour (same pair, same maths, same single registration site) to what shipped in round 1.

No other new subscriptions were introduced in this WP (`MobThresholdSystem.cs`, `DefibrillatorSystem.cs`, `SharedExecutionSystem.cs` add dependencies/calls, not subscriptions).

---

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` re-sync line (`:4`) now reads "by WP8". Rows verified present for every file WP8 currently touches:

- `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` — `:74` (running WP4/WP5/WP8 row) and `:120` (WP8-specific line)
- `Content.Shared/Armor/SharedArmorSystem.cs` — `:114`, narrowed description matching the fix
- `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` — `:115` (new row, added in fix round 1)
- `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` — `:116`
- `Content.Server/Medical/DefibrillatorSystem.cs` — `:117`
- `Content.Shared/Execution/SharedExecutionSystem.cs` — `:118`
- `Content.Shared/_Onyx/Wounds/WoundEvents.cs` — `:119`
- `Content.Server/Damage/Commands/HurtCommand.cs` — `:121`, status flipped to `skipped`
- `Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` — `:122`, status `skipped`, sourcing-gap disclosure present
- `Resources/Locale/en-US/damage/damage-command.ftl` — `:123`, status `skipped`

A `### WP8` Deviations section (manifest `:353`–`:394`) documents the HOOK 10 two-subscription split (with the fix-round-1 revision noted inline), the `Transform(uid).ParentUid` wearer lookup, the unpredicted-armour note, the `CheckThresholds` hoist, the `HurtCommand.cs` revert with reasoning, and the V1 sourcing-gap disclosure. **PASS** — manifest coverage is complete and every deviation, including the round-1 findings and their resolutions, is transparently recorded.

---

## 6. Plan conformance

All 5 files in PLAN §4 WP8's file table exist at the planned destination:

- `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` — present, D23 side-table read extended to the part pass (table itself landed in WP5, as the report explains and the manifest corroborates).
- `Content.Shared/Armor/SharedArmorSystem.cs` — present, HOOK 10, now exactly as PLAN §3 describes it (S1 fixed).
- `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` — present, HOOK 11 at both sites (`CheckThresholds` hoisted per §3.3 of the report, `UpdateAlerts`).
- `Content.Server/Medical/DefibrillatorSystem.cs` — present, HOOK 12.
- `Content.Shared/Execution/SharedExecutionSystem.cs` — present, HOOK 13.

Decisions the WP8 table cites are honoured:
- **D23** (AP/tool/origin-flag threading): `PartDamageModifyEvent` carries `ArmorPenetration`; `WoundDamageRoutingSystem` reads `_routedModifiers.GetValueOrDefault(body).ArmorPenetration` at the construction site; `SharedArmorSystem.OnDamageModify` calls `DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)` before applying, exactly as PLAN §3's HOOK 10 mandates ("must route through Wolfgate's existing `PenetrateArmor`... not bypass it"). Confirmed by direct inspection.
- **HOOK 11**: `CheckVitalDamage` substituted at both cited call sites; `CheckVitalDamage` is defined in `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` (WP5) and falls back to `TotalDamage` for non-wound-hosts.
- **HOOK 12**: same substitution in `DefibrillatorSystem.Zap`. Confirmed.
- **HOOK 13**: `_woundRouting.TryApplyLethalDamage(victim, meleeWeaponComp.Damage, attacker)` added after `AttemptLightAttack`, self-guarded on `_net.IsServer`, `HasComp<WoundHostComponent>`, and a non-empty threshold set (verified directly at `WoundDamageRoutingSystem.cs:516-547`). Confirmed.

No extra files beyond the file table remain undisclosed: `WoundEvents.cs`'s WP8 edit is documented; the withdrawn `HurtCommand.cs`/locale files are documented as reverted; `WolfmedPartArmorSystem.cs` is documented as HOOK 10's relocated other half. **PASS.**

---

## 7. Snapshot

```
$ git -C WG diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP8.patch
$ git -C WG ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP8.untracked.txt
```
Written: `WP8.patch` (764 lines — the full cumulative tracked diff since HEAD, all WPs, per the "no commits" rule; shorter than round 1's 938 lines because `HurtCommand.cs` and the `damage-command.ftl` usage-string edit are gone), `WP8.untracked.txt` (76 lines — one fewer path than round 1, the deleted `_Onyx/commands/damage-command.ftl`). **Done.**

---

## 8. Summary of findings

| # | Severity | Finding |
|---|---|---|
| — | (resolved) | **U1** (round 1 blocker): `HurtCommand.cs` unauthorised upstream edit — reverted to byte-identical `HEAD`. Confirmed clear. |
| — | (resolved) | **S1** (round 1 major): part-armour subscription scope-crept the upstream `SharedArmorSystem.cs` beyond HOOK 10's text — moved to new `_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs`. Upstream file now carries exactly HOOK 10. Confirmed clear. |
| — | (resolved) | **V1** (round 1 major): unverifiable-provenance locale file — deleted along with the command it served. Confirmed clear. |
| P1 | Minor | `(ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>)` is a real, necessary, crash-free subscription, but PLAN §5.2's pair-audit table does not yet list it. Disclosed by the report as an open item the WP8 agent cannot self-resolve (cannot edit PLAN.md). Recommend the orchestrator add the row; does not block this WP. |

Build is green both sides, no unmarked upstream drift, no unmarked vendoring drift, no duplicate subscriptions, manifest and file-table coverage complete, every cited decision honoured. All three round-1 findings are verified fixed, not merely claimed fixed. The remaining item (P1) is a documentation-only gap the report already surfaced.

**pass = true.**
