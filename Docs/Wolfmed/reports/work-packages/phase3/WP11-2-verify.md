# WP11-2 verify — Organ damage (PLAN3 §4 / P3-2)

Verifier pass, 2026-09-13. Worktree `WG` = `.claude/worktrees/rules-motd-updates-11c89c`, branch
`clanker/wolfmed-port-orchestration-454c3d`. RobustToolbox untouched (not inspected/modified).

**Verdict: PASS.** No blockers, no majors.

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

Both green, matching the report's §6.

---

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 6 tracked files changed. Two of them (`WolfmedBodyPartLifecycleSystem.cs`,
`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`) are `_WF`, out of scope for the marker check; one
(`WoundDamageFoundationTest.cs`) is a test. Byte-diffed all three against the corresponding hunk in
`WP11-1.patch` (already-verified WP11-1 output) — **identical, zero bytes of new WP11-2 change** in any of
them. Confirms the report's claim that WP11-2 touched nothing WP11-1 already owns.

The two files this WP actually edits outside `_Onyx`/`_WF`:

* **`Resources/Prototypes/Body/Organs/human.yml`** — 7 one-line `parent:` edits at `:53,103,150,189,215,
  249,270`, each carrying `# WOLFGATE (WP11-2, D8)` (the brain line adds a longer reason clause). Diffed:
  every change is exactly `parent: X` → `parent: [X, WolfmedOrganY] # WOLFGATE (...)`, one line per site,
  nothing else touched in the file. This is **PROTO A**, the only hook PLAN3 §3 authorises for this WP, at
  exactly the line numbers §3/§7.2 specify. `OrganHumanTongue` (`:119`), `OrganHumanAppendix` (`:128`),
  `OrganHumanEars` (`:139`) confirmed untouched.
* **`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`** — this file is `_Onyx`, not upstream, so the
  hook-authorisation rule doesn't apply to it; it's covered under §3 vendoring fidelity below. Its 2 sites
  both carry `// WOLFGATE` (P3-D23) as PLAN3 requires.

`OrganDamageSystem.cs` — diffed HEAD; the only changes present are WP11-1's D26 lift (`_amputation`
dependency + call, both marked `// WOLFGATE`). **WP11-2 did not touch `:25-26`/`:38-39` or anywhere else in
this file** — P3-D24 honoured.

**D2 (no behaviour change for non-wound-hosts):** verified structurally, not just asserted. The only path
that ever reduces `WolfmedOrganComponent.Health` is `OrganDamageSystem.OnPartDamageApplied`, subscribed to
`<WoundableComponent, PartDamageAppliedEvent>` — `WoundableComponent` exists only on wound-host parts, so a
non-host's organs are structurally unreachable by any health-reducing code, regardless of what
`OrganHealthSystem.Update`'s query enumerates. The per-tick query does cover non-host organs (rat lungs,
`_NF` goblin organs, cybernetic organs parented to `OrganHuman*`) but the loop body for a healthy organ is a
single `FixedPoint2` comparison with no side effect — cost without behaviour, as the report states. Matches
PLAN3 §1.1 D2 and the report's §4 claim.

No unauthorised or oversized upstream edits found.

---

## 3. Vendoring fidelity (`_Onyx/`)

Only one `_Onyx` file changed in this WP: `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` (already
diverged from Onyx's `Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs` by prior-phase relocation/
rewrites — out of this WP's scope). Isolated this WP's own delta via `git diff HEAD` on the file (shown
above): exactly two hunks, both carrying `// WOLFGATE: P3-D23 …` comments describing the reason. No
unmarked hunk.

`Resources/Prototypes/_WF/Wolfmed/Body/organs.yml` and `WolfmedOrganConsequenceSystem.cs` are `_WF`, not
`_Onyx` — no marker convention applies (PLAN3 §3's in-vendored-file table), consistent with report §2.
Cross-checked `organs.yml`'s seven `damageMultipliers`/`hitChance`/`selectionWeight`/`destructionWound*`
blocks byte-for-byte against `git -C C:/tmp/onyx show HEAD:Resources/Prototypes/Body/base_organs.yml` at
lines 481-491, 537-547, 665-676, 718-730, 755-767, 808-819, 846-858 — **all seven blocks match exactly**,
including the two organs (Brain, Eyes) that carry no `destructionWound`.

---

## 4. Subscriptions

```
grep -rn "OrganFunctionChangedEvent" --include=*.cs .   (excluding RobustToolbox)
```
→ declared + raised only in `OrganHealthSystem.cs:21,71`; subscribed only once, in
`WolfmedOrganConsequenceSystem.cs:23`. No duplicate.

```
grep -rn "OrganEnableChangedEvent" --include=*.cs .
```
→ subscribed only once, `SharedBodySystem.Organs.cs:23`. `WolfmedOrganConsequenceSystem` **raises** it
(`:35`), matching PLAN3 §2.2's explicit "raise, don't subscribe" design and the `CyberneticsSystem.cs:27-28`
precedent it cites. No duplicate directed subscription; matches PLAN3 §5.1 row 2 exactly.

---

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` diff: 4 new rows appended immediately before `## Deviations` — one each
for `organs.yml`, `Body/Organs/human.yml`, `WolfmedOrganConsequenceSystem.cs`, `OrganHealthSystem.cs` — plus
the `### WP11-2 (phase 3 - organ damage)` narrative block, confirmed at line `:904`, directly after
`### WP11-1`. Every file this WP touched (report table rows 1-4) has a manifest row; row 5 is the manifest
itself. WP11-1's prior rows and narrative are untouched. Matches report §1/§7 and PLAN3 §7.2.

---

## 6. Plan conformance

* All 5 files in the WP table exist as described; line counts are close to (within ~2 lines of) the
  report's claims — immaterial rounding, not a deviation.
* **DECISIONS.md §8.6-4** (organ damage ships irreversible): confirmed — no code path calls
  `OrganHealthSystem.ChangeHealth`/`SetHealth` with a positive delta anywhere in the tree; `ChangeHealth` is
  public but has zero callers today.
* **§8.6-7** (human-lineage organs only): confirmed — PROTO A edits only the 7 `OrganHuman*` ids in
  `Body/Organs/human.yml`; no other organ root under `Resources/Prototypes/**/Organs/**` was touched.
* **§8.6-1** (guns/lasers sever via `_WF` YAML, Piercing finishing 12, Heat thresholds) and **§8.6-2**
  (vest/helmet coverage annotation) belong to WP11-1/WP11-3 respectively, not this WP; confirmed WP11-2 made
  no edit to `parts.yml` or the armour YAML (§2 above) — correctly out of scope here, not a WP11-2 gate.
* **§8.6-3** (host-gated vital charge) is WP11-1's `WolfmedBodyPartLifecycleSystem.cs` edit, confirmed
  present and unmodified by WP11-2 (§2).
* Prototype resolution measurement (report §4) matches Onyx source exactly (§3 above) — this WP's one
  required measurement is satisfied and independently re-verified against the vendored source rather than
  just re-trusting the throwaway test's printed numbers.
* Test evidence: `WP11-2-report-tests.log` tail confirms **43/43** passed (Wound/Onyx.Body/Wolfmed filter,
  49.4 s); `WP11-2-report-docktest.log` confirms **DockTest 3/3** passed. No scratch-test residue —
  `WolfmedTempOrganProtoCheck.cs` is absent from the tree, matching the report's claim it was deleted before
  finishing.
* No commits made (git log unchanged at `1171e02fb6 phase 2` as HEAD for content purposes); consistent with
  the no-commits rule.

No deviation found that isn't already disclosed and justified in the report's own §5.

---

## 7. Snapshot

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
    > C:/tmp/wolfmed-plan/p3/snapshots/WP11-2.patch
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs \
    Content.IntegrationTests > C:/tmp/wolfmed-plan/p3/snapshots/WP11-2.untracked.txt
```
Written (520-line patch — cumulative with WP11-0/WP11-1's still-uncommitted changes, as expected for
sequential packages in one worktree; 5-line untracked list matching the report's new/untracked files).

---

## Summary

WP11-2 does exactly what PLAN3 §4/§7.2 and the report describe: PROTO A (organ prototypes + 7 upstream
`parent:` hooks), the P3-D23 `OrganHealthSystem` guards, and the new `WolfmedOrganConsequenceSystem` bridge
— nothing more, nothing less. Both required builds are green, the one new subscription is free and matches
PLAN3 §5.1, vendored values match Onyx byte-for-byte, D2/D8/§8.6-4/§8.6-7 are all honoured, the manifest is
complete, and WP11-1's exclusive ownership of `OrganDamageSystem.cs` was respected. Pass.
