# WP12-1 verification — Reagent effect classes and HOOK 9 (P4-1a)

**Verdict: PASS** — no blockers, no majors, no minors.

Worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG),
Onyx pin `2f5bab9946539cbe083010c9ae6fbc59b47ae377` at `C:/tmp/onyx`. `WG/RobustToolbox` not touched
(not in any diff below). Nothing committed.

## 1. Build (re-run independently)

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Both green, matching the report's tails.

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`:

```
 .../EntityEffects/Effects/EvenHealthChange.cs        | 20 ++++++++++++++++++--
 Content.Server/EntityEffects/Effects/HealthChange.cs | 19 +++++++++++++++++--
 Content.Shared/Gibbing/Systems/GibbingSystem.cs      |  5 +++--
 3 files changed, 38 insertions(+), 6 deletions(-)
```

Only three tracked non-`_Onyx`/`_WF` files are touched in the whole tree (`Docs/` is excluded from this
stat by the task's own command and is checked separately below).

- **`GibbingSystem.cs`** — the pre-phase-4 `.ToArray()` snapshot fix. Diff is 2 hunks, both marked
  `// WOLFGATE` / `// WOLFGATE: snapshot, DropEntity/GibEntity mutate the container`. Pre-authorised by
  `DECISIONS.md`'s Phase 4 preamble ("The `GibbingSystem.cs` container-mutation crash is fixed with a marked
  `.ToArray()` (two loops) before phase 4 starts") and by this task's own instructions. Not part of WP12-1's
  own claimed edits (the report doesn't list it) — correctly so, it predates the package. No action needed.
- **`HealthChange.cs`** — HOOK 9(a), PLAN4 §3.1 row 1. Diff verified line-for-line against the plan's exact
  quoted patch: `using Content.Shared._Onyx.Wounds; // WOLFGATE: HOOK 9 - treatment-capability scope`; a
  marked `HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]` datafield;
  the single `TryChangeDamage` call rewritten as `var change = Damage * scale; void Apply() => …` with the
  Shitmed/Mono argument block (`targetPart: TargetBodyPart.All`, `partMultiplier: 1.00f, // Mono,
  0.5f->1.00f`, `canSever: false`) surviving byte-for-byte, followed by a marked
  `if (change.DamageDict.Values.Any(amount => amount < 0) && HasComponent<WoundHostComponent>(...))` branch
  into `WoundDamageRoutingSystem.WithTreatmentCapabilities(...)`, else `Apply()`. `change = Damage * scale`
  is kept (not `damageSpec * scale`) — trap T4 (the discarded-universal-modifier bug) correctly NOT fixed.
  Every added/changed line sits under one of the three `// WOLFGATE: HOOK 9` site comments; this is the
  established one-marker-per-site convention (matches how HOOK 22–26 are specified in PLAN4 §3.1), and
  PLAN4 explicitly exempts HOOK 9 from the one-/two-line limit ("A pure two-line hook is impossible here
  because the call must become a delegate").
- **`EvenHealthChange.cs`** — HOOK 9(b), PLAN4 §3.1 row 2. Same three additions, plus the required
  `using System.Linq; // WOLFGATE: HOOK 9 - Any() on the healing test` (T5/trap 21 — this file had no
  `System.Linq` before). The delegate local is `var final = dspec * scale;` (the modifier-adjusted spec, per
  the plan's explicit note that this file differs from HOOK 9(a) in which expression it captures), and the
  healing test is `Damage.Values.Any(amount => amount < 0)` — matches PLAN4 §3.1 verbatim.
- **D2 (no behaviour change for non-wound-hosts):** both hooks gate the new branch on
  `HasComponent<WoundHostComponent>(...)`; every entity without the component falls through to the
  unconditional `Apply()`, i.e. exactly today's call with exactly today's arguments. Confirmed by inspection
  and corroborated by the report's unchanged 65/65 `_Onyx.Wounds|_Onyx.Body|_Onyx.Medical|Wolfmed` test run.
- **`WithTreatmentCapabilities` nesting (trap 3):** each hook calls it exactly once, at top level, never
  inside another such scope. No nesting found.

`Docs/Wolfmed/DECISIONS.md` also shows as modified in `git status`, but its diff is the Phase 4 section
already present verbatim in the `DECISIONS.md` supplied for this review (orchestrator-authored, predates
WP12-1) — not a WP12-1 edit, and outside the audited path set (`Docs` is excluded from the task's stat
command). `Docs/Wolfmed/WOLFMED_MANIFEST.md` (also outside that path set) carries the report's appended
`### WP12-1` section, checked under §5 below.

Five untracked paths outside `_WF`/`_Onyx`-owned trees appear in `git status`
(`Content.Server/_Onyx/Medical/`, `Resources/Locale/en-US/_Onyx/medical/medical_patch.ftl`,
`Resources/Prototypes/_Onyx/Entities/Objects/`, `Resources/Prototypes/_Onyx/Tags/`,
`Resources/Textures/_Onyx/Objects/`) — these are all WP12-0's (medical patch) uncommitted output, already
PASS-verified in `WP12-0-verify.md`, and are not claimed or touched by the WP12-1 report. Correctly out of
scope for this verification.

## 3. Vendoring fidelity

Only one file under `_Onyx/` is new/changed in this WP:
`Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl`. Diffed against
`git -C C:/tmp/onyx show HEAD:Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` with
`--strip-trailing-cr`:

- 3 hunks differ: (1) header gains a 2-line `# WOLFGATE` comment plus the `entity-effect-guidebook-*` →
  `reagent-effect-guidebook-*` key rename; (2) same rename on the `mend-fractures` key; (3) same rename on
  `all-fractures`, plus an appended `reagent-effect-guidebook-take-stamina-damage` key under its own
  `# WOLFGATE` comment recording it has no Onyx source. Every differing hunk carries a WOLFGATE marker. The
  4 `fracture-grade-*` keys are untouched/unrenamed, matching the report's claim.

The four new `.cs` files live under `_WF/Wolfmed/EntityEffects/`, not `_Onyx/`, so they are new Wolfgate code
rather than vendored Onyx code and are not subject to this diff-fidelity check (per D6's layout rule); they
are old-style re-authorings of Onyx's ECS-based originals, which is what PLAN4 §2.1 specifies for all four.

## 4. Collisions

- **Subscriptions:** `grep -rn "SubscribeLocalEvent"` over the four new files and the two hooked files
  returns 0 hits. Matches PLAN4 §5.1 ("WP12-1 … registers nothing") and the report. No new pair to check for
  duplication.
- **New EntityEffect class bare names** — `SuppressPain`, `MendFractures`, `TakeStaminaDamage`,
  `StaminaDamageCondition` — each greps to exactly one hit (`class <Name>`), its own new file, across
  `Content.Shared`, `Content.Server`, `Content.Client`. No collision.
- **New components:** none registered by this WP.
- **New prototype ids:** none — this WP ships no YAML/prototype file.
- **New locale ids:** all 8 keys in `entity-effects.ftl` (`reagent-effect-guidebook-suppress-pain`,
  `-mend-fractures`, `-all-fractures`, `-take-stamina-damage`, `fracture-grade-{hairline,simple,displaced,
  comminuted}`) grep to exactly one defining file each across `Resources/Locale/en-US/`.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` diff adds a `### WP12-1 (phase 4 — reagent effect classes and HOOK 9,
P4-1a)` section with a 7-row table covering every file the report's own table lists except the manifest
file itself (the usual convention — the manifest does not carry a self-row): the four new `EntityEffects`
files, `entity-effects.ftl`, and the two hooked files (`HealthChange.cs`, `EvenHealthChange.cs`), plus a
notes block covering P4-D1/D5/D6/D7, the subscription/component audit, the name audit, the two cosmetic
deviations, and the build/test checkpoint. Consistent with the report.

## 6. Plan conformance

- All 8 rows in PLAN4 §4 WP12-1's file table exist: the 4 new `EntityEffects/*.cs`, the new
  `entity-effects.ftl`, the two hooked files, and the manifest append. No deviation to justify.
- The four class bodies match PLAN4 §2.1's quoted code, modulo the two disclosed cosmetic differences
  (per-field `/// <summary>` one-liners; `StaminaDamageCondition.Condition` using a plain local instead of
  the `is var damage &&` pattern) — both confirmed present on inspection, both behaviourally identical.
- **T2** (`ReagentEffectGuidebookText` is abstract): all four classes implement it; build is green.
- **T1** (class-name load-bearing): confirmed via the 0-hit greps above, both before-creation (per report)
  and now.
- **DECISIONS §8.4-7 honoured:** `grep -rn "treatmentCapabilities" Resources/Prototypes` finds exactly one
  hit, `Resources/Prototypes/_Onyx/Wounds/wounds.yml:4` (`OrganicBodyPartProfile`, phase-3 vendored, pinned
  before this WP) — WP12-1 adds the datafield to `HealthChange`/`EvenHealthChange` but writes it onto no
  reagent or item, exactly as decided.
- **P4-D6** (`DistributedHealthChange` not authored) and **P4-D1** (HOOK 9 inert until a non-Biological
  profile exists) are both recorded in the manifest and consistent with the tree (only
  `OrganicBodyPartProfile` exists, confirmed by the same grep).
- No subscription or component registered, matching PLAN4 §5.1's row-by-row accounting for this WP.

## 7. Snapshot

```
$ git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
    > C:/tmp/wolfmed-plan/p4/snapshots/WP12-1.patch
$ git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
    > C:/tmp/wolfmed-plan/p4/snapshots/WP12-1.untracked.txt
```

Written: `WP12-1.patch` (274 lines) and `WP12-1.untracked.txt` (32 lines, the WP12-0 medical-patch files plus
the four new WP12-1 `.cs` files and the new `.ftl`).

## Summary

WP12-1 does exactly what PLAN4 §4/§2.1/§3.1 specify: four re-authored old-style `EntityEffect`/
`EntityEffectCondition` classes under `_WF/Wolfmed/EntityEffects`, HOOK 9(a)/(b) on `HealthChange.cs` and
`EvenHealthChange.cs` with every changed line under a `// WOLFGATE: HOOK 9` marker, D2 preserved for
non-wound-hosts, no new subscriptions/components/prototype ids, and a correctly filled-in manifest section.
Both requested builds are green. No blockers, majors, or minors found.
