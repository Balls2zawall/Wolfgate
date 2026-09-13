# WP10-5 verification — Pain sounds + mob wiring

**Verdict: PASS** (no blockers, no majors)

Worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
Onyx pin: sparse checkout at `C:/tmp/onyx` (`git show HEAD:...`)

## 1. Build (check 1)

All three checkpoints, sequential, `-c DebugOpt -v q`:

| Project | Result |
|---|---|
| `Content.Server/Content.Server.csproj` | Build succeeded, 0 Error(s) |
| `Content.Client/Content.Client.csproj` | Build succeeded, 0 Error(s) |
| `Content.IntegrationTests/Content.IntegrationTests.csproj` | Build succeeded, 0 Error(s) (first attempt hit a transient MSBuild file-lock on `RobustToolbox\Robust.Shared.CompNetworkGenerator.dll` — MSB3027/MSB3021, a stale `csc` process holding the DLL, unrelated to any WP10-5 source change and not touching `RobustToolbox` content; immediate retry built clean) |

IntegrationTests build was not in the verifier's mandatory check list but is PLAN2 ground rule 5's checkpoint for every WP; included for completeness. Pass.

## 2. Upstream discipline (check 2)

`git diff HEAD --stat` over the whole tree shows files from earlier, already-landed phase-2 work packages (WP10-1..4: `ExamineSystem.cs`, `DamageOverlay*.cs`, `HealthExaminableSystem.cs`, `BloodstreamSystem.cs`, `PainSystem.cs`, `wounds.ftl`, `wounds.yml`, the IntegrationTests fixtures) — those are out of this WP's scope (verified/owned elsewhere) and untouched by WP10-5. WP10-5's own tracked-file footprint, per its report and `git status`, is exactly:

- `Content.Server/Chat/EmoteOnDamageComponent.cs` (upstream hook, HOOK 17)
- `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` (upstream hook, HOOK 18)
- `Resources/Prototypes/Entities/Mobs/Species/base.yml` (owned by WP10-5 per PLAN2 §8.3 risk 2)
- `Docs/Wolfmed/WOLFMED_MANIFEST.md` (manifest rows, checked separately)

Diffed each:

- **EmoteOnDamageComponent.cs** — 1 added `using` line + a block of 4 purely-additive `[DataField]`s/`[ViewVariables]` fields, all under one `// WOLFGATE: HOOK 17 …` provenance comment. `Emotes` untouched. This matches PLAN2 §3 HOOK 17 verbatim (including the exact field list, types and default values). HOOK 17 is one of PLAN2's five explicitly-authorised multi-line upstream sites, so the "one or two lines per site" cap does not apply here — PLAN2 itself specifies this exact additive block as HOOK 17's authorised content, and there is no `_WF` partial for it because there is no method body to split out (pure data fields).
- **EmoteOnDamageSystem.cs** — exactly 1 line, `HandlePainDamageEmote(uid, emoteOnDamage, args); // WOLFGATE: HOOK 18, …`, inserted as the first statement of `OnDamage`, before the existing `if (!args.DamageIncreased) return;`. Matches HOOK 18 verbatim (site, wording, placement).
- **base.yml** — two blocks (`PainShockTarget`, `EmoteOnDamage`) appended inside the existing phase-1 `# WOLFGATE - Wolfmed phase 1 (D21/D32)` block on `BaseMobSpeciesOrganic`, each with its own `# WOLFGATE` provenance/rationale comment. Field values (`emotesThreshold`, `emoteChance`, `withChat`, `hiddenFromChatWindow`, `emoteCooldown`) match PLAN2 §3/§4 exactly, including the corrected key. All fields used (`EmoteChance`, `WithChat`, `HiddenFromChatWindow`, `EmoteCooldown`) were confirmed pre-existing on `EmoteOnDamageComponent` (not newly introduced), so the YAML introduces no undisclosed schema.

No unauthorised or oversized upstream edits found. Check 2: pass.

(Note: `Docs/Wolfmed/DECISIONS.md` also shows as modified in `git status`, but this is the orchestrator's phase-2 decisions text landing in-repo — not attributed to or touched by WP10-5's own work, and outside the `Content.*`/`Resources` scope this check targets.)

## 3. Vendoring fidelity (check 3)

New file `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`, diffed against `git -C C:/tmp/onyx show HEAD:Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` with `--strip-trailing-cr`:

5 differing hunks, all carrying a `// WOLFGATE` marker:

1. `+using Content.Shared._Onyx.Wounds; // WOLFGATE: P2-D22 wound-host gate.`
2. `+using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12 damage facade.`
3. `[Dependency] private DamageableSystem _damageable` → `WolfmedDamageableSystem _damageable = default!; // WOLFGATE: D12, …`
4. P2-D22 wound-host gate (3-line comment + 2-line guard, all under one `// WOLFGATE (P2-D22):` block) as the first statement of `HandlePainDamageEmote`.
5. `HasComp<PainNumbnessComponent>(uid)) // WOLFGATE: P2-D8 parity — …` appended to the bail-out chain.

No unmarked differing hunks. Namespace (`Content.Server.Chat.Systems`) and the absence of a license header both match Onyx's original exactly (verified: Onyx's file opens with `using Content.Shared.Chat;`, no header). Check 3: pass.

The report's D1 correction (PLAN2 wrongly claimed `using Content.Shared._Onyx.Wounds;` was already Onyx's line 1) is independently confirmed: Onyx's actual first line is `using Content.Shared.Chat;`, the wounds import is genuinely new.

## 4. Subscriptions (check 4)

`grep -n "SubscribeLocalEvent"` across the new file and the two upstream hook files: zero new subscriptions. The only `SubscribeLocalEvent<EmoteOnDamageComponent, DamageChangedEvent>` in the whole tree (`grep -rn` outside RobustToolbox) is the pre-existing one at `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs:22`, which HOOK 18 correctly calls into rather than duplicating. Matches PLAN2 §5.2's explicit "deliberately does not register" entry for this pair. Check 4: pass.

## 5. Manifest (check 5)

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries one row each for:
- `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` (line 159)
- `Content.Server/Chat/EmoteOnDamageComponent.cs` (line 160)
- `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` (line 161)
- `Resources/Prototypes/Entities/Mobs/Species/base.yml` (line 162)
- an explicit "no audio/locale needed" row (line 163)

plus a phase-2 sentence on the species-exclusion paragraph (line 174) and a full `### WP10-5` deviations section (lines 666-711) covering D1-D5 from the report. Every file WP10-5 touched has a row. Check 5: pass.

## 6. Plan conformance (check 6)

- All 4 WP10-5 destination files exist at the paths PLAN2's table names. No deviation from the file table.
- §8.2-3 (pain sounds ported with the corrected `emotesThreshold` key): confirmed in base.yml diff — `emotesThreshold:` is used, not Onyx's dead `emotes:`.
- §8.2-4/5/6 (HOOK 17 + HOOK 18 authorised, one/two-line upstream sites): confirmed as built (see check 2).
- P2-D22 (wound-host gate on the pain-sound path, since D32 strips only `WoundHostComponent` from Protogen): confirmed present as the guard clause in the vendored file.
- Supporting components referenced (`PainShockTargetComponent`, `PainNumbnessComponent`) both exist in the tree.
- Fracture-multiplier (1.1/1.25/1.5/2.0) and part-status-wound-hosts-only decisions are WP10-1/WP10-2 concerns; WP10-5 does not touch fracture YAML or `HealthExaminableSystem`, so they are not applicable to this WP's own diff and are not contradicted by anything WP10-5 did.
- Deviations D1-D5 in the report are all either corrections-with-no-behavioural-effect (D1, D2), explicit discretionary choices PLAN2 itself offered (D3), a manifest-clarity edit (D4), or properly escalated rather than silently fixed (D5 — the ex-zombie `emotesThreshold` loss on `RemoveEmote`'s `removeEmpty: true` default is documented, confined to ex-zombies, invisible during crit/dead, and correctly left to a future WP rather than an unauthorised upstream edit to `ZombieSystem`).

Check 6: pass.

## 7. Snapshot (check 7)

Written:
- `C:/tmp/wolfmed-plan/p2/snapshots/WP10-5.patch` (925 lines — cumulative diff of the whole uncommitted worktree across all landed phase-2 WPs to date, per the "no commits" rule)
- `C:/tmp/wolfmed-plan/p2/snapshots/WP10-5.untracked.txt` (16 new files from WP10-1 through WP10-5 combined, including this WP's `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`)

## Summary

WP10-5 is a clean, small, well-scoped package: two one/two-line upstream hooks exactly matching PLAN2 §3, one vendored file with all 5 diverging hunks WOLFGATE-marked, two additive YAML blocks with the corrected key, no new subscriptions, and full manifest coverage. No blockers or majors. One informational item worth flagging forward (already captured in the report and manifest, not a defect in this WP): the ex-zombie `EmoteOnDamage` component loss via `RemoveEmote`'s `removeEmpty: true` default (D5) is real but out of PLAN2 §3's authorised hook list for this WP, correctly escalated rather than fixed here.
