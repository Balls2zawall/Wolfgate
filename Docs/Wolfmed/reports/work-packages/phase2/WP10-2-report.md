# WP10-2 — HealthExaminable part status + pain examine (report)

**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG), branch `clanker/wolfmed-port-orchestration-454c3d`, **not committed**.
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (read via `git -C C:/tmp/onyx show HEAD:<path>`).
**Status:** complete. Content.Server, Content.Client and Content.IntegrationTests all build with 0 errors; headless server starts clean (no Fluent duplicate id, no `[ERRO]`/`[FATL]`/`Exception`); a throwaway integration test proved the readout end-to-end and was then deleted.

---

## 1. Files created / modified

| # | Path (under WG) | Status | Notes |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs` | **new, vendored — modified** | 5 `// WOLFGATE` edits (PLAN2 predicted 4; see §2) |
| 2 | `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.Pain.cs` | **new, vendored — verbatim** | byte-identical to Onyx apart from CRLF |
| 3 | `Content.Client/_Onyx/HealthExaminable/ExamineSystem.PartStatus.cs` | **new, vendored — verbatim** | `Content.Client/_Onyx/` did not exist; created |
| 4 | `Content.Client/_Onyx/HealthExaminable/PartStatusTag.cs` | **new, vendored — verbatim** | |
| 5 | `Content.Shared/_WF/Wolfmed/Compat/PartStatusSeverity.cs` | **new** | PLAN2 §2.1 shim, body as written there |
| 6 | `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` | **modified (hook)** | GUARD F, 5 marked sites |
| 7 | `Content.Client/Examine/ExamineSystem.cs` | **modified (hook)** | HOOK 14, 2 marked sites |
| 8 | `Content.Server/Body/Systems/BloodstreamSystem.cs` | **modified (hook)** | GUARD E2, 1 marked line |
| 9 | `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl` | **new, vendored — verbatim** | 36 message ids |
| 10 | `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | **modified — 8 lines deleted** | P2-D12; the whole `# WOLFGATE (WP7)` stopgap block (`:45-52`) |
| 11 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | 10 new rows, 2 amended rows, WP7 deviation marked resolved, new `### WP10-2` deviation section |
| — | `Resources/Locale/en-US/_Onyx/targeting/part-status.ftl` | **skipped** | orphaned at the pin (P2-D12) |

All C# / FTL files written CRLF (matching the working tree); `WOLFMED_MANIFEST.md` kept LF (that is how it already was). `RobustToolbox/` untouched; no `git stash`/`clean`/`checkout --`/`reset`/`commit` was run.

---

## 2. Every `// WOLFGATE` edit and its reason

### File 1 — `_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs` (5 edits)

1. `:6` `using Content.Shared.Damage.Components;` → `using Content.Shared.Damage; // WOLFGATE: DamageableComponent lives here in Wolfgate, not in .Damage.Components.`
   `WG/Content.Shared/Damage/Components/DamageableComponent.cs:9` declares `namespace Content.Shared.Damage`; the `.Components` namespace exists, so without this the `using` compiles and `DamageableComponent` is CS0246. Same class of fix as `WoundFractureSystem.cs:4` from phase 1.
2. `:7` **added** `using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12 damage facade.`
3. `:19` **added** `[Dependency] private WolfmedDamageableSystem _damageable = default!; // WOLFGATE: D12, Onyx-shaped damage API.`
   An addition, not a swap: Onyx declares `_damageable` on the **base** partial (`ONYX HealthExaminableSystem.cs:15`) and GUARD F deliberately leaves WG's base file facade-free. Binds `WolfmedDamageableSystem.GetPositiveDamage(Entity<DamageableComponent>)` (`Compat/WolfmedDamageableSystem.cs:89`).
4. `:138` `PartOrder`'s `BodyPartType.Chest => 1` / `Groin => 2` folded to `BodyPartType.Torso => 1, // WOLFGATE (D9): Chest and Groin folded to Torso; Wolfgate's enum has neither.` Non-contiguous literals left as-is (cosmetic).
5. **NOT PREDICTED BY PLAN2 — see §3 deviation 1.** `WG`'s `DamageSpecifier.DamageDict` is `Dictionary<string, FixedPoint2>` (`Content.Shared/Damage/DamageSpecifier.cs:44`), not `ProtoId<DamageTypePrototype>`-keyed as in Onyx, so two `.Id` accesses are CS1061:
   - `:47` `.OrderBy(entry => entry.Key.Id)` → `.OrderBy(entry => entry.Key)) // WOLFGATE: DamageSpecifier.DamageDict is keyed by string here, not ProtoId<DamageTypePrototype>.`
   - `:53-54` `$"health-examinable-part-damage-{type.Id.ToLowerInvariant()}"` → `{type.ToLowerInvariant()}` with `// WOLFGATE: string key, so no .Id — see the OrderBy above.`
   Behaviour is identical (`ProtoId<T>.Id` *is* that string), so both the sort order and the produced loc key are unchanged.

Files 2, 3, 4 and 9 needed **zero** edits, as PLAN2 predicted.

### GUARD F — `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` (5 sites, +6/−2 lines)

- `:1` `+ using Content.Shared._Onyx.Wounds; // WOLFGATE: GUARD F`
- `:33` `var markup = CreateMarkup(uid, args.User, component, damage); // WOLFGATE: GUARD F, examiner param for self-vs-other pain visibility`
- `:46` signature → `public FormattedMessage CreateMarkup(EntityUid uid, EntityUid examiner, HealthExaminableComponent component, DamageableComponent damage) // WOLFGATE: GUARD F` (the only caller in the repo is the one above — verified by grep)
- `:50` opens `if (!HasComp<WoundHostComponent>(uid)) { // WOLFGATE: GUARD F — legacy threshold text is for non-wound-hosts only; body left un-reindented to keep the upstream diff minimal.` and closes after the `msg.IsEmpty` fallback
- `:100` `else AddPartStatusMarkup(uid, examiner, msg); // WOLFGATE: GUARD F — wound hosts only (P2-D20/D2); Onyx calls this unconditionally because every bodied entity there is a wound host.`

Onyx's `_damageable.GetAllDamage` swap inside the legacy branch was **deliberately skipped** per §3, so the file needs no facade dependency. `HealthExaminableComponent.cs` untouched. **No subscription added.**

### HOOK 14 — `Content.Client/Examine/ExamineSystem.cs` (2 sites)

- `:202` `_examineTooltipOpen = new Popup { MaxWidth = 560 }; // WOLFGATE: HOOK 14 — was 400; Onyx's part-status boxes are 520 wide`
- `:279-284` the three `richLabel` lines wrapped in `if (!TryAddPartStatusMessage(vBox, message)) { … } // WOLFGATE: HOOK 14 — Onyx's part-status boxes replace the plain label when the markup carries them.`

No new `using` (the callee is a member of the same partial class). **No subscription added.**

### GUARD E2 — `Content.Server/Body/Systems/BloodstreamSystem.cs:280` (1 line)

`if (!HasComp<WoundHostComponent>(ent) && GetBloodLevelPercentage(ent, ent) < ent.Comp.BloodlossThreshold) // WOLFGATE: GUARD E2 — Onyx's HealthExaminable covers pallor per-part for wound hosts`

No new `using` (GUARD E added `Content.Shared._Onyx.Wounds` in WP6). The two `BleedAmount` messages above are untouched, per §3.

### Locale (P2-D12)

`Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` lines 45-52 deleted (blank line + the 3-line `# WOLFGATE (WP7)` comment + the 4 `wound-examine-fracture-*` keys). `health-examinable.ftl` now owns those ids. Verified: `grep -rn "wound-examine-fracture" Resources/` shows each key defined exactly once, still referenced by `_Onyx/Wounds/wounds.yml:143,154,165,176`; a duplicate-id scan over all of `Resources/Locale/en-US/_Onyx/` returns nothing.

### Subscription audit

**Zero directed subscriptions added by this package.** Neither `_Onyx` shared partial declares `Initialize()`; the client partial's `TryAddPartStatusMessage` is called synchronously from `UpdateTooltipInfo`; `PartStatusSeverity.cs` is a `static class` plus an enum. `HealthExaminableSystem`'s existing `<HealthExaminableComponent, GetVerbsEvent<ExamineVerb>>` is untouched. Nothing near `<EmoteOnDamageComponent, DamageChangedEvent>` was gone near.

---

## 3. Deviations from PLAN2

1. **A fifth vendored-file edit, not in PLAN2's "four `// WOLFGATE` edits".** `DamageSpecifier.DamageDict` is `Dictionary<string, FixedPoint2>` in Wolfgate. PLAN2 §4/WP10-2 and §7 both say four edits; the file does not compile with four. The fifth edit (§2, item 5) is mechanical and behaviour-preserving. **Later packages: any vendored `_Onyx` file that reads `DamageDict` keys as `ProtoId<DamageTypePrototype>` will hit exactly this.** Phase 1 never did — no `_Onyx` file touches `.Key.Id` today.
2. **GUARD F's wrapped body is not re-indented.** PLAN2 §3's illustrative `<pre>` shows Onyx's indented block. I kept the wrapped statements at their original indentation so the upstream diff is 4 added lines instead of ~45 changed ones — Wolfgate merges this file from upstream, and a re-indented block conflicts on every upstream touch. Marked in the comment on the `if`. Behaviour identical.
3. **`PartDamageSeverity` ships without Onyx's `[Serializable, NetSerializable]`.** PLAN2 §2.1 gives the shim's exact body and it has no attributes; Onyx's enum (in the D10-excluded `PartStatusComponent.cs`) does. It is never networked here — only `ToString().ToLowerInvariant()`-ed into markup — so this is harmless, but a later package that networks a part-status snapshot must restore them.
4. **Recorded, not introduced (from PLAN2 §7):** deviation 15 (P2-D20 — `AddPartStatusMarkup` in the `else`, Onyx calls it unconditionally) and deviation 16 (HOOK 14 (a) widens **every** examine popup in the game 400→560 px, not just wound hosts'). Both are in the manifest's new `### WP10-2` section.
5. **A throwaway integration test was written, run and deleted** (`Content.IntegrationTests/Tests/_WF/Wolfmed/TmpExamineSmokeTest.cs`). WP10-6 owns the permanent tests; this was a verification aid only. The tree contains no trace of it (`git status` confirmed).

Nothing in PLAN2 was contradicted otherwise. `part-status.ftl` skipped as instructed; `HealthExaminableComponent.cs`, `Content.Shared/Movement/**`, `SharedDoAfterSystem.cs` and `Traits/disabilities.yml` untouched.

---

## 4. Build / test output

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt
Build succeeded.
    0 Error(s)

dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt
Build succeeded.
    0 Error(s)
```

**Headless server** (`--cvar net.port=1299`, 120 s, log at `C:/tmp/wolfmed-plan/p2/WP10-2-report-server.log`):

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
```

No Fluent duplicate-id error, no unknown component, no locale warning. (Only pre-existing `[WARN]`s: `PullingSystem` command binds and the emote-word duplicates.)

**Throwaway end-to-end check** (deleted afterwards) — a `WoundHost` body with `HealthExaminable` and 40 Blunt routed to its left leg, plus an identical non-host control:

```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 29 s

HOST:
[font size=11][color=darkgray]You check yourself for injuries.[/color][/font][partstatus summary="\[bold]Human torso\[/bold]: \[color=green]fine\[/color]" severity="none" details=""][/partstatus]
[font size=10][bold]Human torso[/bold]: [color=green]fine[/color][/font][partstatusend][/partstatusend][partstatus summary="\[bold]Left human leg\[/bold]: \[color=red]badly damaged\[/color], \[color=red]hurts terribly\[/color]" severity="severe" details="\[color=#B8B8B8]Injuries:\[/color] bruises"][/partstatus]
[font size=10][bold]Left human leg[/bold]: [color=red]badly damaged[/color], [color=red]hurts terribly[/color]
    [color=gray][color=#B8B8B8FF]Injuries:[/color] bruises[/color][/font][partstatusend][/partstatusend]

PLAIN:
There are no obvious wounds to be seen.
```

What this proves: every one of the 36 loc ids resolves (no raw key leaked into the output); `PartStatusSystem.GetSeverity(40) == Severe` matches the shim's buckets; `PartOrder` sorts torso before leg; the self-examine pain word (`health-examinable-pain-terrible`) fires through `HealthExaminableSystem.Pain.cs`; `FormattedMessage.EscapeStringParameter` round-trips the nested markup without throwing from `AddMarkupOrThrow`; and **P2-D20 holds** — the non-wound-host control got the legacy threshold text and **no** `partstatus` node.

Release YAML lint was not run: this package touched no YAML. The `examineDescription` loc references that made WP7 add the stopgap keys still resolve (same ids, now in `health-examinable.ftl`), which the clean prototype-loading server start exercises.

---

## 5. What later packages must know

1. **`HealthExaminableSystem.CreateMarkup` now takes four arguments** (`uid, examiner, component, damage`). It had exactly one caller in the repo and still does; any new caller must pass the examiner.
2. **`AddPartStatusMarkup` runs only in the `else` of `!HasComp<WoundHostComponent>(uid)`** (P2-D20). If §8.2 item 8 is ever revisited and silicons/animals should get part status too, that is a one-line move — but it also needs a decision about whether they keep the legacy threshold text as well.
3. **"Looks pale" is gone for wound hosts** (GUARD E2). The two `BleedAmount` messages above it still fire for wound hosts, deliberately (GUARD E3 projects wound bleeding onto the body's `BleedAmount`). WP11's health-analyzer readout should not duplicate them.
4. **Every examine popup in the game is now 560 px wide.** If anyone reports examine tooltips got wider, that is HOOK 14 (a), not a bug.
5. **`Content.Shared._Onyx.Targeting` now contains a `_WF`-authored `PartStatusSystem` + `PartDamageSeverity`** (`Content.Shared/_WF/Wolfmed/Compat/PartStatusSeverity.cs`). A later package that vendors Onyx's real `_Onyx/Targeting/PartStatusSystem.cs` (D10 currently forbids it) will collide on both names and must delete the shim in the same change.
6. **`Content.Client/_Onyx/` exists for the first time.** Onyx client partials attach to Wolfgate's classes with no `_WF` indirection because the class declarations are identical — the same trick will work for WP10-4's overlay work only if the class is `partial` (it is: `DamageOverlay` at `:11`, `DamageOverlayUiController` at `:17`).
7. **`wounds.ftl` no longer carries the four `wound-examine-fracture-*` keys.** They live in `_Onyx/medical/health-examinable.ftl`. Do not re-add them anywhere; Fluent throws on a duplicate id.
8. **The 4 `wound-examine-frame-*` keys in `health-examinable.ftl` have no consumer at the pin** — dead weight, not a collision. They belong to Onyx's synthetic-frame wounds (D3-deferred).
9. **`Docs/Wolfmed/WOLFMED_MANIFEST.md` now has a `### WP10-2` deviation section** between `### WP10-1` and `## Hazards`. The WP7 `wounds.ftl` stopgap deviation is marked **RESOLVED**. WP10-7 should reconcile, not merge.
