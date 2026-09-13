# WP1 — StatusEffectNew framework — verification report

**Verifier date:** 2026-09-12
**WG:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (branch `clanker/wolfmed-port-orchestration-454c3d`)
**ONYX:** `C:/tmp/onyx`, pinned at `2f5bab9946539cbe083010c9ae6fbc59b47ae377` — confirmed (`git -C C:/tmp/onyx rev-parse HEAD` matches).

**Verdict: PASS.** No blockers, no majors. One informational note (sparse-checkout gap, see §3).

---

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Both green, matching the WP1 report's claim.

---

## 2. Upstream discipline

```
git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
```
→ **empty output.** `git status --porcelain` over the same paths shows only untracked additions:

```
?? Content.Shared/StatusEffectNew/
?? Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs
?? Content.Shared/_WF/Wolfmed/
?? Docs/
?? Resources/Locale/en-US/_Onyx/
?? Resources/Prototypes/Entities/StatusEffects/
?? Resources/Prototypes/_Onyx/
```

I also swept every other top-level directory (`BuildChecker`, `BuildFiles`, `Content.Benchmarks`, `Content.Docfx`, `Content.MapRenderer`, `Content.Packaging`, `Content.PatreonParser`, `Content.Replay`, `Content.Server.Database`, `Content.Shared.Database`, `Content.Tests`, `Content.Tools`, `Content.YAMLLinter`, `LICENSES`, `MSBuild`, `Pow3r`, `Scripts`, `Tools` — `RobustToolbox` excluded per instructions, it's a junction) with `git status --porcelain`: no output, nothing touched.

**Conclusion: zero pre-existing (tracked) files were modified anywhere in the repo.** Every file WP1 touched is a brand-new untracked file. There is therefore no upstream-discipline violation to find — the check's premise (modified upstream files without WOLFGATE markers) doesn't arise. `Content.Shared/StatusEffectNew/` is a new directory, not a modification to an existing upstream file, so its internal `// WOLFGATE` edits are assessed under vendoring fidelity (§3) instead, consistent with the checklist's own routing of that path there.

Also confirmed no unauthorised upstream `// WOLFGATE` hooks were added: none of PLAN.md §3's GUARD/HOOK list (all scheduled for WP2/WP4/WP5/WP6/WP8/WP10/WP11) appear anywhere in this diff, correctly — WP1 owns none of them.

---

## 3. Vendoring fidelity

Diffed every file the WP1 report claims as "verbatim" against `git -C C:/tmp/onyx show`/direct file read at the pinned commit, `diff --strip-trailing-cr`:

| File | Result |
|---|---|
| `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/Components/StatusEffectContainerComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/Components/StatusEffectAlertComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/Components/CloneableStatusEffectComponent.cs` | byte-identical (BOM preserved) |
| `Content.Shared/StatusEffectNew/Components/ExaminableStatusEffectComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/Components/PermanentStatusEffectsComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/Components/RejuvenateRemovedStatusEffectComponent.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/StatusEffectsSystem.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs` | byte-identical |
| `Content.Shared/StatusEffectNew/StatusEffectAlertSystem.cs` | byte-identical |
| `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` | byte-identical |

All 11 verbatim claims confirmed true.

For the three files claimed "modified", every differing hunk was inspected and carries a `// WOLFGATE` marker (either directly on the changed line, or covering a whole deleted block):

- **`StatusEffectSystem.Relay.cs`** — 4 hunks: MartialArts `using` swap, added `using Content.Shared.Damage;`, and two runs of deleted subscription lines (11 lines total across the run at `:47-49/:52` and `:61-71`), each replaced by a `// WOLFGATE:` comment naming the missing type(s). Matches PLAN.md's exact edit list line-for-line.
- **`ExaminableStatusEffectSystem.cs`** — 1 hunk: `[SubscribeLocalEvent]` replaced by an explicit `// WOLFGATE`-commented `Initialize()` with the identical subscription. Matches plan.
- **`PermanentStatusEffectsSystem.cs`** — 1 hunk (plus 3 now-dead attribute-line deletions folded into it): explicit `// WOLFGATE`-commented `Initialize()` added, three `[SubscribeLocalEvent]` attributes removed. The `// <Onyx-OrganEffects>` block is untouched. Matches plan.

**Resource files:**

- `Resources/Prototypes/Entities/StatusEffects/misc.yml` — diffed against the full Onyx file (present in the sparse checkout). One hunk: everything after the three abstract bases (`MobStandStatusEffectBase` plus 14 concrete status-effect entities) is deleted and replaced by a single `# WOLFGATE:` comment explaining the drop. Matches the report's described deviation.
- `Resources/Prototypes/_Onyx/Entities/categories.yml` and `Resources/Locale/en-US/_Onyx/entity-categories.ftl` — **cannot be diffed against Onyx directly**: their Onyx sources (`Resources/Prototypes/Entities/categories.yml`, `Resources/Locale/en-US/entity-categories.ftl`) are vanilla/upstream paths outside `C:/tmp/onyx`'s sparse-checkout set (confirmed via `git -C C:/tmp/onyx sparse-checkout list` — neither path nor its parent tree is included). Per DECISIONS.md's own rule ("if a path is absent say so, never guess file contents"), I am reporting this as an inspection gap rather than guessing a match. **Not a blocker**: both files are two and one lines of actual content respectively, both are entirely `# WOLFGATE`-commented as adapted extracts, and their content matches PLAN.md §WP1's literal specification (the `StatusEffects` entityCategory block and the one locale key) verbatim. Recommend the orchestrator widen the Onyx sparse checkout to include `Resources/Prototypes/Entities/categories.yml` and `Resources/Locale/en-US/entity-categories.ftl` (both tiny, upstream/vanilla) before the next WP that touches adapted-from-vanilla files, so this class of check doesn't recur.

No unmarked drift found anywhere.

---

## 4. Subscriptions

Every `SubscribeLocalEvent<X, Y>` pair added in WP1's new files:

```
ExaminableStatusEffectComponent, StatusEffectRelayedEvent<ExaminedEvent>
PermanentStatusEffectsComponent, ComponentInit
PermanentStatusEffectsComponent, MapInitEvent
PermanentStatusEffectsComponent, ComponentRemove
StatusEffectAlertComponent, StatusEffectAppliedEvent
StatusEffectAlertComponent, StatusEffectRemovedEvent
StatusEffectAlertComponent, StatusEffectEndTimeUpdatedEvent
StatusEffectContainerComponent, LocalPlayerAttachedEvent / LocalPlayerDetachedEvent / RejuvenateEvent /
  RefreshMovementSpeedModifiersEvent / ModifySlowOnDamageSpeedEvent / UpdateCanMoveEvent /
  MobStateChangedEvent / RefreshFrictionModifiersEvent / TileFrictionEvent / CanSeeAttemptEvent /
  GetBlurEvent / FlashAttemptEvent / SelfBeforeClimbEvent / BeforeForceSayEvent /
  BeforeAlertSeverityCheckEvent / SpeakAttemptEvent / ExaminedEvent / DamageModifyEvent /
  SelfBeforeDefibrillatorZapsEvent / SelfBeforeGunShotEvent
StatusEffectContainerComponent, ComponentInit / ComponentShutdown /
  EntInsertedIntoContainerMessage / EntRemovedFromContainerMessage
RejuvenateRemovedStatusEffectComponent, StatusEffectRelayedEvent<RejuvenateEvent>
```

Grepped the rest of WG (`Content.Shared`, `Content.Server`, `Content.Client`, excluding `Content.Shared/StatusEffectNew/`) for `SubscribeLocalEvent<` on any of the five component types WP1 introduces (`StatusEffectContainerComponent`, `ExaminableStatusEffectComponent`, `PermanentStatusEffectsComponent`, `StatusEffectAlertComponent`, `RejuvenateRemovedStatusEffectComponent`): **zero hits**. All are brand-new component types with no other subscriber anywhere, so no `(component, event)` pair can collide by construction. Also checked for internal duplicates within the new files themselves: none (`sort | uniq -c` on every pair — all count 1).

This matches PLAN.md §5.2's last row exactly ("all seven components are new... yes"), and §5.3's confirmation that all the WP1-relevant registered names (`StatusEffect`, `StatusEffectContainer`, `StatusEffectAlert`, `CloneableStatusEffect`, `ExaminableStatusEffect`, `PermanentStatusEffects`, `RejuvenateRemovedStatusEffect`, `PainNumbnessStatusEffect`) are free.

Headless-server-start evidence in the WP1 report (`Server Version 277.0.0.0 -> Ready`, no `Duplicate Subscriptions` throw) is consistent with this static check.

---

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` exists with a populated table, Deviations section, and Hazards section.

Cross-checked `git ls-files --others --exclude-standard` over the WP's scope against the manifest table:

| File | Manifest row? |
|---|---|
| `Content.Shared/StatusEffectNew/Components/*.cs` ×7 | yes, one row each |
| `Content.Shared/StatusEffectNew/StatusEffectsSystem.cs` | yes |
| `Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs` | yes |
| `Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs` | yes |
| `Content.Shared/StatusEffectNew/StatusEffectAlertSystem.cs` | yes |
| `Content.Shared/StatusEffectNew/ExaminableStatusEffectSystem.cs` | yes |
| `Content.Shared/StatusEffectNew/PermanentStatusEffectsSystem.cs` | yes |
| `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` | yes |
| `Content.Shared/_WF/Wolfmed/Compat/StatusEffectsSystem.Wolfgate.cs` | yes |
| `Content.Shared/_WF/Wolfmed/Compat/EntityPrototypeCompatExtensions.cs` | yes |
| `Content.Shared/_WF/Wolfmed/Compat/AlertsSystem.UpdateAlert.cs` | yes |
| `Resources/Prototypes/_Onyx/Entities/categories.yml` | yes |
| `Resources/Locale/en-US/_Onyx/entity-categories.ftl` | yes |
| `Resources/Prototypes/Entities/StatusEffects/misc.yml` | yes |
| `Docs/Wolfmed/{DECISIONS,WOLFMED_HANDOFF,WOLFMED_PLAN}.md` | not rows — correctly excluded: these are the orchestrator's own planning docs, untracked before WP1 started, not WP1 output |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | the manifest itself, no self-row needed |

Every file WP1 actually produced has a manifest row. Deliberately-skipped Onyx source files (movement/body/clumsy/damage/speech/traits/weather status-effect prototypes, MartialArts, the Immunities systems, the never-ported StatusEffects yml files) are also listed with `skipped` status and a reason, going beyond the minimum bar.

---

## 6. Plan conformance

All 17 code files + 3 resource files from PLAN.md's WP1 table (§4, "WP1 — StatusEffectNew framework") exist at the planned destination — verified by direct listing, matching 1:1 (same Onyx-relative path per D1, as the plan specifies "all identical paths"). The three new `_WF/Wolfmed/Compat` files were read in full and match PLAN.md §2.4/§2.5/§2.6's specified signatures, namespaces, and logic exactly (down to `.Item1`/`.Item2` on the unnamed tuple, and the `IPrototypeManager ProtoMan` field name/type).

**Decision cited:** D1 only (WP1's header: "StatusEffectNew framework (D1)"). D1 requires: port verbatim from Onyx's `Content.Shared/StatusEffectNew`, treat as vendored upstream with `// WOLFGATE`-marked edits, and leave the old `Content.Shared.StatusEffect` system untouched serving existing content. Confirmed: the old system's directory was not touched (absent from the diff), and every edit in the new tree is marked, per §3 above. D1 honoured.

**Deviation reported:** WP1's own report documents one deviation — `MobStandStatusEffectBase` not ported despite the plan's "(and optionally MobStandStatusEffectBase)" — with a concrete reason (its blacklist references the `KnockdownImmune` tag, which returns zero hits in Wolfgate; a missing tag id is a YAML-linter error). This is within the plan's own stated latitude ("optionally") and is recorded in the manifest's Deviations section. Accepted, not a deviation requiring escalation.

File count matches: 17 code files + 3 resource files, as the plan specifies.

---

## 7. Snapshot

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP1.patch
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP1.untracked.txt
```

`WP1.patch` is **0 bytes** — expected and correct, since every WP1 file is a new untracked addition rather than a modification to a tracked file, so `git diff HEAD` (tracked-file diff) has nothing to show. `WP1.untracked.txt` (24 lines) is the actual record of WP1's changes: the 21 files WP1 produced plus the 3 pre-existing orchestrator docs under `Docs/Wolfmed/` that were already untracked before WP1 started (`DECISIONS.md`, `WOLFMED_HANDOFF.md`, `WOLFMED_PLAN.md`).

**Note for the orchestrator:** because WP1 adds only new files, the per-WP patch mechanism captures nothing for it in `WP1.patch`; `WP1.untracked.txt` is the file to consult (or a future WP's committer should `git add` these paths before diffing, if the snapshot format is expected to always be a non-empty unified diff).

---

## Summary

| # | Check | Result |
|---|---|---|
| 1 | Build (Server + Client) | PASS — both 0 errors |
| 2 | Upstream discipline | PASS — zero tracked files modified anywhere in the repo |
| 3 | Vendoring fidelity | PASS — all verbatim claims byte-identical; all edits WOLFGATE-marked; one informational sparse-checkout gap (not a defect in WP1) |
| 4 | Subscriptions | PASS — all pairs on brand-new components, zero collisions, matches PLAN.md §5.2 |
| 5 | Manifest | PASS — every touched file has a row |
| 6 | Plan conformance | PASS — every planned file exists at destination; D1 honoured; one plan-sanctioned deviation recorded |
| 7 | Snapshot | Done — `WP1.patch` (empty, expected) + `WP1.untracked.txt` (24 files) written |

**pass = true.**
