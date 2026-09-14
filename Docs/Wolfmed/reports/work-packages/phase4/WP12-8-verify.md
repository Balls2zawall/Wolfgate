# WP12-8 verification — Explosion amputation (P4-6 / P4-D14)

Verified against `DECISIONS.md` (Phase 4 + §8.4), `PLAN4.md` §WP12-8/§1/§3/§5/§8, and
`WP12-8-report.md`. Worktree: `.../worktrees/rules-motd-updates-11c89c`. `WG/RobustToolbox` not touched.

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Both green, 0 errors. **PASS.**

## 2. Upstream discipline

`git diff HEAD --stat` over `Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 20 changed files total — this is the cumulative sequential-worktree diff (WP12-0 through WP12-8, all
uncommitted per the phase-4 no-commits rule). Comparing against `WP12-7.patch`'s `git apply --stat`, every
file's line count is byte-identical to the WP12-7 snapshot **except** the two files WP12-8 touches:

- `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` (new hunks this WP, file itself pre-existing/vendored)
- `Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs` (first-ever Wolfmed touch, HOOK 22)

This confirms WP12-8 added exactly these two files' worth of change and touched nothing else already
verified by WP12-0–WP12-7's own verify passes. (`Docs/Wolfmed/WOLFMED_MANIFEST.md` is outside this
path filter, appended separately — checked in §5.)

**HOOK 22 (`ExplosionSystem.Processing.cs`)** — diffed directly:
```diff
+using Content.Server._WF.Wolfmed.Explosion; // WOLFGATE: HOOK 22
...
+    [Dependency] private WolfmedExplosionSystem _wolfmedExplosion = default!; // WOLFGATE: HOOK 22
...
+                if (!_wolfmedExplosion.TryApplyExplosionDamage(entity, damage)) // WOLFGATE: HOOK 22 - wound hosts split the blast across their limbs; everyone else falls through unchanged.
                 _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
                 // Mono: Explosion flag for plate protection
                 originFlag: DamageableSystem.DamageOriginFlag.Explosion);
```
3 marked lines, matches PLAN4 §3.1 HOOK 22 exactly in kind (1 using, 1 `[Dependency]`, the guard). The
existing `TryChangeDamage` call and its Mono comment are byte-for-byte unchanged. The `if` is deliberately
brace-free — the plan's own written example uses braces, but bracing here would add two unmarked lines and
re-indent the guarded (already multi-line) statement for no behavioural gain; the guarded body is a single
C# statement either way, and the build succeeds. Recorded as report deviation 1; accepted — same conclusion
PLAN4 reaches for other braceless single-marked-line-per-site hooks elsewhere in the phase.

**D2 (structural):** `TryApplyExplosionDamage` returns `false` immediately when `!HasComp<WoundHostComponent>`,
so a non-host falls straight through to the original `TryChangeDamage(...)` call with identical arguments —
same code path, same call, D2-identical. Confirmed by reading `WolfmedExplosionSystem.cs` directly.

**In-vendored-file edits (`WoundDamageRoutingSystem.cs`, 3 sites, PLAN4 §3.3 authorises this WP for exactly
this file):** all three sites diffed and match the plan (§2.11): optional trailing `originFlag` parameter on
`TryApplyDistributedDamage` and `TryRouteDistributedDamage`, and the `_routedModifiers` save/restore scope.
Every one of the WP12-8-added lines carries an inline `// WOLFGATE (P4-D14)` marker or sits directly under a
5-line `// WOLFGATE (P4-D14): ...` block comment. **PASS.**

## 3. Vendoring fidelity

`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` diffed in full against
`git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` with
`--strip-trailing-cr` (Onyx HEAD `2f5bab9946539cbe083010c9ae6fbc59b47ae377`, matches the DECISIONS.md pin).
205-line diff, 133 added lines. Every differing hunk carries either an inline `// WOLFGATE` marker or sits
under an immediately-preceding `// WOLFGATE` block comment — verified line by line. This includes both the
WP12-8-specific hunks (the `originFlag` parameter + `_routedModifiers` scope on both entry points, all
marked `P4-D14`) and the pre-existing phase-1/2/3 hunks (D8, D9, D10, D12, D23, D27 — already verified in
earlier phases, re-confirmed here as still fully marked, none regressed). No unmarked divergence anywhere
in the file. **PASS.**

No other file under `_Onyx/` was added or changed by this WP (confirmed by the untracked-file diff in §4).

## 4. Collisions

- **Subscriptions:** grepped `SubscribeLocalEvent` in all three files this WP touches
  (`WolfmedExplosionSystem.cs`, `WoundDamageRoutingSystem.cs`, `ExplosionSystem.Processing.cs`) — the only
  hits are `WoundDamageRoutingSystem.cs:64-66`, byte-identical to `HEAD` (diffed directly, confirmed
  unchanged), matching PLAN4 §5.1's row for WP12-8 ("registers nothing") and the D23 requirement not to
  disturb the two `before: [typeof(SharedArmorPlateSystem)]` orderings. `WolfmedExplosionSystem` registers
  only two `Subs.CVar` callbacks, which are not directed subscriptions. **No new pairs, no duplicates.**
- **Component names:** none registered by this WP (`WolfmedExplosionSystem.cs` defines no component).
- **Type names:** `grep -rn "class WolfmedExplosionSystem"` → exactly one hit, the new file itself.
  `grep -rn "ApplyDistributedDamageCore"` → two hits, both inside `WoundDamageRoutingSystem.cs` (the
  definition and its one call site) — no collision.
- **Prototype ids:** none added by this WP.

Untracked-file diff against `WP12-7.untracked.txt` confirms exactly one new file added by this WP:
`Content.Server/_WF/Wolfmed/Explosion/WolfmedExplosionSystem.cs`. **PASS.**

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-8` section (line 1806) with a 4-row table covering
every file this WP touched (`WoundDamageRoutingSystem.cs`, `WolfmedExplosionSystem.cs`,
`ExplosionSystem.Processing.cs`, the manifest itself), a full WOLFGATE-edit-by-edit breakdown for both the
upstream hook and the vendored file, subscription/component/prototype audit results, CVar notes, all four
deviations from the report, and the build/test checkpoint. **PASS.**

## 6. Plan conformance

All four files in PLAN4's WP12-8 table exist with the documented status (modified / new / modified / append).
Order-matters constraint (#1 before #2 before #3) is satisfied by construction (the file diffs are consistent
with that order — the `_WF` system only compiles because the vendored `originFlag` parameter already exists).

Cited decisions — D2, D23, P4-D14, §8.4-1 — checked against the diff:
- **D2:** honoured structurally (see §2 above) — non-hosts take the identical original code path.
- **D23:** honoured — the two `before:` orderings are untouched, confirmed byte-identical to HEAD.
- **P4-D14:** the `originFlag` plumbing and save/restore shape match the plan's sketch; the report's
  documented deviation (wrapper + renamed core instead of an in-place `try`/`finally`) is a reasonable,
  disclosed implementation choice that preserves identical semantics (verified: the core keeps its own
  `_routing.Contains` re-entrancy guard, and the wrapper's `finally` always restores the prior entry).
- **§8.4-1** ("SHIP (WP12-8), gated on T-EXPLOSION-PLATE + T-EXPLOSION-WRAPPER"): the *ship* half is done.
  The *gate* half is **not yet satisfied** — `grep -rln "ArmorPlate\|Plate" Content.IntegrationTests/Tests/`
  returns nothing, confirming the report's own claim that no armour-plate test exists anywhere in the tree.
  PLAN4's WP12-8 section itself states "T-EXPLOSION-PLATE and T-AMP-EXPLOSION both green is the gate" for
  its own build checkpoint, which is not literally met by this package alone. However: PLAN4 §4 sequences
  WP12-9 ("Tests (P4-8)") immediately after WP12-8 for exactly this purpose, §5.1 confirms WP12-8 was never
  expected to add tests, and the report is fully transparent about the gap (§5 of the report: "WP12-9 owns
  the gate for this package", with the exact assertions WP12-9 must write spelled out). Treated as a
  **disclosed, plan-sequenced deferral, not a silent gap** — noted below as a follow-up item rather than a
  blocker on WP12-8 itself.

`ExplosionAmputatesDeterministicallyTest` (phase 3's T-AMP-EXPLOSION) confirmed present and unmodified in
`WolfmedAmputationTest.cs`; its stale `<remarks>` (still says D24/P3-D3 keep the path unreachable) is
correctly left alone per the report, since `Content.IntegrationTests` is out of WP12-8's file scope
(confirmed: `git diff HEAD --stat -- Content.IntegrationTests` is empty).

## 7. Snapshot

```
git -C .../rules-motd-updates-11c89c diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p4/snapshots/WP12-8.patch
git -C .../rules-motd-updates-11c89c ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p4/snapshots/WP12-8.untracked.txt
```
Written: `WP12-8.patch` (1250 lines), `WP12-8.untracked.txt` (56 lines). **Done.**

## Verdict

**PASS.** Both builds green with 0 errors. HOOK 22 is 3 marked lines, non-hook call preserved byte-for-byte,
D2 holds structurally. `WoundDamageRoutingSystem.cs`'s entire diff against Onyx HEAD — old and new hunks
alike — is fully WOLFGATE-marked. No new subscription pairs, no component/type/prototype-id collisions. The
manifest carries a complete WP12-8 section. Every file in PLAN4's WP12-8 table exists and matches its
declared status.

One follow-up, not a blocker: **T-EXPLOSION-PLATE and T-EXPLOSION-WRAPPER (DECISIONS §8.4-1's stated gate)
do not exist yet.** They are correctly scoped to WP12-9 by PLAN4's own work breakdown, and the report
documents exactly what each must assert (armour-plate durability loss/absorption via
`WolfmedExplosionSystem.TryApplyExplosionDamage`, the wrapper's true/false return contract, and the
`_routedModifiers` restore-on-refusal behaviour). WP12-9 must land these before phase 4 as a whole can be
considered to honour §8.4-1.
