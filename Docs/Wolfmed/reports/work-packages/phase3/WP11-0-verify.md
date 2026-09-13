# WP11-0 verification — Zero-dependency test debt (T-REATTACH + T-VISUALS)

Verified against WG worktree
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`, branch
`clanker/wolfmed-port-orchestration-454c3d`, phase 2 committed (`1171e02fb6`). ONYX = `C:/tmp/onyx`.

## 1. Build — PASS

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

All three targets green, matching the report's §4 tails.

## 2. Upstream discipline — PASS (with one report-completeness note)

```
git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
```
→ **empty**. No tracked file under those five trees is modified.

```
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
```
→ only the two new files:
- `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs`
- `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedVisualsTest.cs`

Both live under a `_WF` path, so no `// WOLFGATE` marker is required on them (new-code rule, not
upstream-hook rule); both files use `// WOLFGATE:` inline comments anyway purely to explain *why* a
compat/`_WF` symbol is used at each call site (e.g. `WolfmedBodySystem`/`WolfmedBodyPartSystem`/D8/D12
citations) — good practice, not a requirement here since the files themselves are new, not vendored/upstream.

**Zero upstream files touched → D2 trivially holds**: there is no production-code change of any kind in this
WP, so entities without `WoundHostComponent` cannot be affected. Confirmed by direct inspection of both new
test files (Read in full): no upstream symbol is edited, only called.

**Report-completeness note (not a code defect):** `git status` also shows
`Docs/Wolfmed/DECISIONS.md` modified (phase-3 scope + §8.6 answers appended, byte-identical to
`C:/tmp/wolfmed-plan/DECISIONS.md`'s phase-3 section). The WP11-0 report does not mention this file at all —
its file table (§1) lists only the two new tests and `WOLFMED_MANIFEST.md`. File mtimes place this edit
*before* the two new test files (`DECISIONS.md` 09:27:04 vs. `WolfmedReattachTest.cs` 09:34:38 /
`WolfmedVisualsTest.cs` 09:35:35 vs. `WOLFMED_MANIFEST.md` 09:40:37), consistent with an orchestrator-level
doc-seeding step that predates WP11-0's own edits rather than production/test work done by this package. It
is outside the `Content.Shared/Content.Server/Content.Client/Resources/Content.IntegrationTests` scope this
check audits, contains no code, matches the authoritative decisions doc word-for-word, and does not affect
build, tests, subscriptions or D2. Flagged as **minor**: the report's "no uncommitted phase-3 work was
present in the tree at start" framing is imprecise, and `WOLFMED_MANIFEST.md` carries no row for
`Docs/Wolfmed/DECISIONS.md`. Recommend a one-line mention in a future WP11-0 report revision; no rework
needed.

## 3. Vendoring fidelity — N/A (PASS by absence)

No file under `_Onyx/` was added or changed in this WP (confirmed by the diff/untracked listings above).
Nothing to diff against `git -C C:/tmp/onyx show HEAD:<path>`.

## 4. Subscriptions — N/A (PASS by absence)

```
grep -n "SubscribeLocalEvent" Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedVisualsTest.cs
```
→ no output. No `SubscribeLocalEvent<X,Y>` pair added in this WP. Matches PLAN3 §5.1 ("WP11-0 … register
nothing") and the report's own claim of zero production code touched.

## 5. Manifest — PASS

`Docs/Wolfmed/WOLFMED_MANIFEST.md` diff carries a row for each of the two new files:

```
| — | Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs | new | WP11-0 | T-REATTACH; closes PLAN §8.3 trap 2 … |
| — | Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedVisualsTest.cs | new | WP11-0 | T-VISUALS; first-ever coverage of PartDamageVisualsComponent … |
```

plus a `### WP11-0` narrative section matching the report's §3 content closely (mechanism description, test
counts, WP11-4 pointer). One gap: no row for `Docs/Wolfmed/DECISIONS.md` (see §2 note above) — minor,
docs-only.

## 6. Plan conformance — PASS

PLAN3 §WP11-0 table:

| # | Wolfgate destination | Status | Verified |
|---|---|---|---|
| 1 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs` | new | exists, builds, passes |
| 2 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedVisualsTest.cs` | new | exists, builds, passes |

Both files were read in full:
- **T-REATTACH** matches PLAN3 §6.2's row exactly: bespoke one-arm `WolfmedReattachBody`/
  `WolfmedReattachBodyGraph` fixture (Shitmed graph + `MobBloodstream` + `WoundHost`); detach via
  `WolfmedBodySystem.TryDetachPart`; reattach via `SharedBodySystem.AttachPart(torso, "left arm", arm)`;
  post-reattach assertions on `WoundableComponent`/`DamageableComponent` presence, `WolfmedBodyPartSystem
  .Get(arm).AmputationThresholds` reading the prototype's `{Slash 130, Piercing 250, Blunt 250}`
  (`WolfmedBaseLeftArm`, P3-D13), then a fresh `Slash 15` hit creating exactly one new `SlashWound` and
  raising `BleedAmount` above zero. `[TestPrototypes]` ids `WolfmedReattachBody`/`WolfmedReattachBodyGraph`
  are free (re-grepped: no collision outside this file).
- **T-VISUALS** matches PLAN3 §6.2's row exactly: real `MobHuman`, targeted Blunt hits on left arm and left
  hand via `TryApplyPartDamage`; asserts `PartDamageVisualsComponent.Damage[LArm]`/`[LHand]` equal the dealt
  amounts, `RArm`/`RHand` absent-or-zero, and the identical reads succeed against `Pair.Client`'s networked
  mirror after `Pair.RunTicksSync(10)`. No sprite/screenshot assertion, per PLAN3's own instruction for this
  row. The `LHand` assertion is exactly the P3-D25 fold anchor PLAN3 calls for.

No production code changed, so none of DECISIONS.md's §8.6 answers (guns/lasers severing, vest+helmet
annotation, host-gated vital charge, hand/foot fold) are implicated by this WP — WP11-0 is upstream of all of
them (P3-1/P3-3/P3-4 work) and correctly does not attempt any of it. Nothing in the two new test files
contradicts any decision.

## 8. Wound-suite re-run — PASS, matches report exactly

```
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --no-build \
  --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed" -v n
```

Result: **41 total, 41 passed**, including
`Passed PartDamageProjectsToVisualsComponentTest [80 ms]` and
`Passed ReattachedPartRejoinsWoundTrackingTest [41 ms]` — both green, same pass/fail outcome as the report's
`WP11-0-tests.log` (39 pre-existing + 2 new = 41, all green). Timings differ slightly run-to-run (expected,
not a discrepancy).

`DockTest` re-run separately: 3/3 passed, clean, no `db.ef`/`admin_notes` warning noise — matches
`WP11-0-docktest.log`.

## 7. Snapshot — done

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p3/snapshots/WP11-0.patch
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/p3/snapshots/WP11-0.untracked.txt
```

`WP11-0.patch` (88 lines) contains the `Docs/Wolfmed/DECISIONS.md` and `Docs/Wolfmed/WOLFMED_MANIFEST.md`
hunks. `WP11-0.untracked.txt` lists the two new test files.

## Verdict

No blockers, no majors. One minor: the report omits mention of the (harmless, docs-only, pre-existing at
WP11-0's own start-time) `Docs/Wolfmed/DECISIONS.md` change, and the manifest has no row for it. Build,
subscriptions, vendoring (N/A), manifest rows for the WP's own deliverables, plan conformance, and the
wound-suite re-run all check out exactly against the report.

**pass = true**
