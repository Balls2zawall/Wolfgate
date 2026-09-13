# WP11-1 verification — Amputation (PLAN3 §4 / P3-1)

Verifier run against WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(branch `clanker/wolfmed-port-orchestration-454c3d`), ONYX = `C:/tmp/onyx` @ `2f5bab9946539cbe083010c9ae6fbc59b47ae377`.
No file modified except this one and the two snapshot files below.

**Verdict: PASS.** No blockers, no majors.

---

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo   → Build succeeded. 0 Error(s)
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo   → Build succeeded. 0 Error(s)
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo → Build succeeded. 0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`:

```
 .../_Onyx/Wounds/WoundDamageFoundationTest.cs      | 10 +++++--
 Content.Server/_Onyx/Wounds/OrganDamageSystem.cs   |  6 ++--
 .../_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs  | 29 ++++++++++++++++++++
 Resources/Prototypes/_WF/Wolfmed/Body/parts.yml    | 32 ++++++++++++++++++++++
 4 files changed, 71 insertions(+), 6 deletions(-)
```

Plus one untracked file: `Content.Shared/_Onyx/Wounds/AmputationSystem.cs` (new, vendored).

**All four modified/added tracked files are under `_Onyx/` or `_WF/`. Zero upstream (non-`_Onyx`, non-`_WF`)
files touched** — confirmed against the full `git status --porcelain` (also shows the two WP11-0 test files
and the pre-existing `Docs/Wolfmed/DECISIONS.md`/`WOLFMED_MANIFEST.md` edits; DECISIONS.md's diff is exactly
the phase-3 scope/§8.6 sections already on record in `C:/tmp/wolfmed-plan/DECISIONS.md`, not a WP11-1 edit).
This matches PLAN3 §3's authorised list, which grants WP11-1 **no** upstream hook (HOOK 19 withdrawn, nothing
else assigned to this WP) — none was taken.

Per-file check:
* `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` — 2 sites, both carry an inline `// WOLFGATE: D26 lifted
  in phase 3 (WP11-1)…` comment. Call order `_wounds → _fractures → _amputation → _bleeding` verified intact
  in the live file.
* `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` — `_WF` file, no marker convention required;
  the added call and method are commented and cite P3-D1. Ordering verified in the live file: `_projection.OnPartRemoved`
  → `_bleeding.OnPartChanged` → `ChargeVitalPartLoss`, exactly as PLAN3 §4/File 3 requires (do-not-reorder trap).
  `ChargeVitalPartLoss`'s only entry point is the existing `SubscribeLocalEvent<WoundHostComponent, BodyPartRemovedEvent>`
  — structurally unreachable for a non-host by construction, not just by convention (RT will not raise a
  directed event on an entity lacking the subscribed component).
* `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` — every changed/added line carries `# WOLFGATE (P3 balance)`
  or is inside the marked header block.
* `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` — the 2 changed literals carry a
  `// WOLFGATE (WP11-1, PLAN3 P3-D1)` comment with the full derivation (13 + 100 = 113; 6 + 113 = 119, matches
  a live re-run, §6 below).

**D2 (no behaviour change for non-wound-hosts):** verified structurally, not just by report claim.
`ChargeVitalPartLoss` extends the body of a handler subscribed on `Entity<WoundHostComponent>`; a directed
`SubscribeLocalEvent<WoundHostComponent, BodyPartRemovedEvent>` cannot fire for an entity without that
component, so the new charge is unreachable for non-hosts regardless of species/prototype. `WoundDamageComponents.cs`
(`DefaultDismembermentFinishingDamage` = Slash 15 / Piercing 40 / Blunt 50) is untouched — confirmed by empty
diff — so melee finishing behaviour for every part that does not carry the new `_WF` YAML overrides is
unchanged. No blockers on D2.

**Deviations D-1 through D-5 in the report** (parts.yml edited despite PLAN3 listing it "not touched";
T-AMP-NOGUN superseded; `WoundDamageFoundationTest.cs` edited despite being nominally WP11-3's; YAML lint run
as a headless-server-startup check instead of the Release linter; D2 check done via a deleted throwaway test)
are all correctly justified against DECISIONS.md §8.6-1, which is later and authoritative over PLAN3's
original P3-D4/P3-D13/§3 text, and are honestly disclosed rather than hidden. None is a blocker.

## 3. Vendoring fidelity

`AmputationSystem.cs` diffed against `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/AmputationSystem.cs`
with `diff --strip-trailing-cr`: 18 differing regions, one exactly matching PLAN3's 26-row/18-site edit table.
Every differing region carries a `// WOLFGATE` marker either inline or on the line immediately introducing the
substitution (e.g. `var wf = _wfPart.Get(part); // WOLFGATE: D8` covers the two unmarked-but-adjacent
`wf.AmputationThresholds`/`wf.DismembermentFinishingDamage` renames three and six lines below it — this matches
PLAN3's own edit-table rows 22/23, which likewise carry no separate inline tag). No license header exists in
Onyx's original (confirmed by reading both file heads) and none was invented. Namespace (`Content.Shared._Onyx.Wounds`)
preserved.

## 4. Subscriptions

`grep -rn "PartDamageOverflowedEvent" Content.Shared Content.Server Content.Client` (source only, binaries
excluded): declaration (`WoundEvents.cs:62`), raise (`WoundDamageRoutingSystem.cs:761`), and exactly one
`SubscribeLocalEvent<WoundableComponent, PartDamageOverflowedEvent>` in the new `AmputationSystem.cs`. No
duplicate. `WolfmedBodyPartLifecycleSystem.cs` and `OrganDamageSystem.cs` register no new subscriptions — both
files' `SubscribeLocalEvent` calls are the pre-existing ones. Matches PLAN3 §5.1 row 1 exactly. Server-start
smoke via the full DockTest + wound-suite run (§6) produced no `Duplicate Subscriptions` throw.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries one row for each of the 5 files WP11-1 touched
(`AmputationSystem.cs`, `OrganDamageSystem.cs`, `WolfmedBodyPartLifecycleSystem.cs`, `parts.yml`,
`WoundDamageFoundationTest.cs`) plus a `### WP11-1 (phase 3 - amputation)` narrative section covering the
threshold mechanics, the Heat routing proof, measured hit counts, the P3-D1 neutrality argument and the D2
empirical check, the double-apply audit, and the still-inert `AmputationConsequenceWound`/explosion items.
Complete.

## 6. Plan conformance

Every file in PLAN3's WP11-1 table (§4) exists: `AmputationSystem.cs` (new), `OrganDamageSystem.cs` (modified),
`WolfmedBodyPartLifecycleSystem.cs` (modified), manifest (appended). The one extra file
(`WoundDamageFoundationTest.cs`) is a disclosed, justified deviation (D-3), not an omission.

DECISIONS.md §8.6-1 is honoured: guns/lasers can sever, expressed only in `_WF` YAML, `# WOLFGATE (P3 balance)`-marked.
§8.6-3 (P3-D1 host-gated vital charge, no HOOK 19) is honoured — verified in code, not just asserted. Vests/
helmet annotation (§8.6-2) and hand/foot visual folding (§8.6-8) are out of this WP's scope (WP11-3/WP11-4) and
correctly not touched here (confirmed: no diff under `Resources/Prototypes/_Mono` or organ/base.yml paths).

Re-ran the wound-suite filter (`DockTest` first, per project convention):

```
DockTest:                                    Passed: 3,  Total: 3
_Onyx.Wounds|_Onyx.Body|Wolfmed filter:       Passed: 43, Total: 43
```

Matches the report's claimed 43/43 exactly, including the corrected `RoutesAndProjectsDamageTest` literals
(113/119).

## 8. §8.6-1 YAML values

Confirmed present in `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`, each line `# WOLFGATE (P3 balance)`-marked:
* `dismembermentFinishingDamage: {Piercing: 12, Heat: 15}` (YAML-anchored, applied to all ten limb parts).
* `Heat` row added to `amputationThresholds` on every limb, equal to that part's Piercing threshold: Head 200,
  Arm 250, Hand 200, Leg 250, Foot 220.
* Slash/Blunt intentionally absent from the per-part `dismembermentFinishingDamage` dict, confirmed still
  falling back to the untouched C# defaults (Slash 15, Blunt 50) in `WoundDamageComponents.cs`.

The report documents the threshold mechanics in both the report body (§4) and the manifest narrative, and
every claim was independently re-verified against the live source in this pass:
`GetThresholdProgress`/`IsFinishingHit`/`ReachedThreshold` in `AmputationSystem.cs` (progress is a sum of
per-type ratios over the part's own threshold dict; a type absent from the dict contributes nothing and can
never be a finishing hit); `Heat`'s presence in `WoundHostComponent.LocalizedDamageTypes`,
`OrganicBodyPartProfile.acceptedDamageTypes`, and the `Burn` damage group under the `OrganicPart` container
(so laser damage lands on the part's own `DamageableComponent` as `Heat` with no wound-type mapping needed).
All confirmed byte-for-byte against the tree, not taken on faith.

## 7. Snapshot

```
git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests
  > C:/tmp/wolfmed-plan/p3/snapshots/WP11-1.patch          (351 lines — matches the report's stated size)
git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests
  > C:/tmp/wolfmed-plan/p3/snapshots/WP11-1.untracked.txt  (3 files: WolfmedReattachTest.cs, WolfmedVisualsTest.cs — WP11-0 —
                                                             and AmputationSystem.cs — WP11-1)
```

RobustToolbox confirmed untouched (`git -C RobustToolbox status --porcelain` → empty).

---

## Summary

WP11-1 does exactly what PLAN3 §4/WP11-1 and DECISIONS.md §8.6-1/§8.6-3 specify: `AmputationSystem.cs` vendored
verbatim except the 18 authorised `// WOLFGATE` sites, `OrganDamageSystem`'s D26 comment-outs restored in the
correct fan-out order, `WolfmedBodyPartLifecycleSystem` extended (not re-subscribed) with a structurally
host-gated vital-part Bloodloss charge that is neutral by construction, and the §8.6-1 gun/laser-sever balance
data added only to `_WF` YAML with every line marked. Zero upstream files touched, zero duplicate subscriptions,
zero unauthorised edits, all three builds green, the full wound/body/Wolfmed test filter green at 43/43,
DockTest green, and the report's numeric claims (build output, test counts, patch line count, threshold
mechanics) all independently reproduced rather than taken on faith. The deviations from PLAN3's original text
(D-1 through D-5) are correctly grounded in the later, authoritative DECISIONS.md §8.6-1 user decision and are
disclosed, not hidden.

**No blockers or majors found. PASS.**
