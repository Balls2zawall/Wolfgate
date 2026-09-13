# WP11-2 — Organ damage (PLAN3 §4 / P3-2)

Worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG).
Onyx pin `2f5bab9`. No commits made. RobustToolbox untouched.

---

## 1. Files created / modified

| # | Path (relative to WG) | Status | Notes |
|---|---|---|---|
| 1 | `Resources/Prototypes/_WF/Wolfmed/Body/organs.yml` | **new**, 133 lines | PROTO A half 1. Seven abstracts, Onyx values verbatim. |
| 2 | `Resources/Prototypes/Body/Organs/human.yml` | **modified**, 7 lines | PROTO A half 2 (upstream hook, authorised by PLAN3 §3). |
| 3 | `Content.Server/_WF/Wolfmed/Body/WolfmedOrganConsequenceSystem.cs` | **new**, 37 lines | PLAN3 §2.2, P3-D8. New `_WF` folder `Content.Server/_WF/Wolfmed/Body/`. |
| 4 | `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` | **modified**, 2 sites | P3-D23 `TerminatingOrDeleted` guards. |
| 5 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | 4 file rows before `## Deviations`; `### WP11-2 (phase 3 - organ damage)` block at `:904`, inside the Deviations section, after WP11-1. |

Scratch/verification artefacts (created and **deleted** before finishing):
`Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedTempOrganProtoCheck.cs` — prototype-resolution and
per-tick-query measurement scaffold. The tree carries no residue; `Content.IntegrationTests` was rebuilt
after its deletion and re-run.

Logs: `WP11-2-report-server.log` (headless), `WP11-2-report-docktest.log`, `WP11-2-report-tests.log`,
`WP11-2-report-protocheck.log` (the throwaway check's output, kept as the evidence for §4's numbers).

Files this WP deliberately did **not** touch: `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs`
(P3-D24 — WP11-1 owns `:25-26`/`:38-39`), `Resources/Prototypes/_Onyx/Wounds/wounds.yml`,
`Content.Shared/Body/Organ/OrganComponent.cs` (P3-D9), every non-`OrganHuman*` organ root.

---

## 2. Every `WOLFGATE` edit and its reason

### `Resources/Prototypes/Body/Organs/human.yml` — PROTO A (7 one-line edits)

| Line | Now reads |
|---|---|
| 53 | `  parent: [BaseHumanOrganUnGibbable, WolfmedOrganBrain] # WOLFGATE (WP11-2, D8): Wolfmed organ health + organ-damage policy` |
| 103 | `  parent: [BaseHumanOrgan, WolfmedOrganEyes] # WOLFGATE (WP11-2, D8)` |
| 150 | `  parent: [BaseHumanOrgan, WolfmedOrganLungs] # WOLFGATE (WP11-2, D8)` |
| 189 | `  parent: [BaseHumanOrgan, WolfmedOrganHeart] # WOLFGATE (WP11-2, D8)` |
| 215 | `  parent: [BaseHumanOrgan, WolfmedOrganStomach] # WOLFGATE (WP11-2, D8)` |
| 249 | `  parent: [BaseHumanOrgan, WolfmedOrganLiver] # WOLFGATE (WP11-2, D8)` |
| 270 | `  parent: [BaseHumanOrgan, WolfmedOrganKidneys] # WOLFGATE (WP11-2, D8)` |

Reason: D8 keeps Wolfgate on Shitmed's `OrganComponent`, so Onyx's organ-health and organ-damage data must
arrive from a separate `_WF` abstract; RT's `ComponentRegistrySerializer` throws `Duplicate ID` if a second
file redeclares `OrganHuman*`, so the only way in is the `parent:` list. Same pattern WP7 used for
`parts.yml` (`Body/Parts/base.yml:81,137,156,…`). Line numbers matched PLAN3 §3 PROTO A exactly; each line
was asserted to be `parent: BaseHumanOrgan[UnGibbable]` before rewriting.

`OrganHumanTongue` (`:119`), `OrganHumanAppendix` (`:128`) and `OrganHumanEars` (`:139`) were **not**
edited — no body graph in the repo slots them (PLAN3 §3, organs.md §5.1).

### `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` — 2 sites (P3-D23)

```
// WOLFGATE: P3-D23, DestroyOrgan detaches and wounds; RecursiveDeleteEntity reaches here while a
// mob terminates, which is the DebugAssertException WP9 fixed in WolfmedBodyPartLifecycleSystem.
if (TerminatingOrDeleted(uid))
    continue;
```
inserted in `Update`'s loop immediately before `DestroyOrgan((uid, organ, slotted));`, and

```
// WOLFGATE: P3-D23, never wound a part that is already terminating (see the Update guard).
… && !TerminatingOrDeleted(parent) && HasComp<WoundableComponent>(parent))
```
added to `DestroyOrgan`'s `CreateOrMergeWound` condition. Reason: PROTO A makes `DestroyOrgan` reachable
for the first time; it calls `_body.RemoveOrgan` and `_wounds.CreateOrMergeWound`, both of which are unsafe
during `RecursiveDeleteEntity`. This is byte-for-byte the failure WP9 fixed in
`WolfmedBodyPartLifecycleSystem` (13 unrelated pooled-pair failures).

### `_WF` files — no marker convention applies

`WolfmedOrganConsequenceSystem.cs` and `organs.yml` are new Wolfgate files (`_WF`), so per PLAN3 §3's
in-vendored-file table they carry explanatory comments rather than `// WOLFGATE` markers. `_WF` style
respected: no licence header, `/// <summary>` one-liners, `[Dependency]` without `readonly` (the system has
no dependencies at all).

---

## 3. Subscription audit (PLAN3 §5)

| Pair | Registrant | Audited |
|---|---|---|
| `<WolfmedOrganComponent, OrganFunctionChangedEvent>` | `WolfmedOrganConsequenceSystem.Initialize()` | **FREE.** Re-grepped: `OrganFunctionChangedEvent` appears only at its declaration (`OrganHealthSystem.cs:21`) and its raise (`:71`). |

Pairs deliberately **not** registered, each re-grepped in the tree today:

* `<OrganComponent, OrganEnableChangedEvent>` — owned by `SharedBodySystem.Organs.cs:23` (sole subscriber).
  The new system **raises** it instead (precedent `_Shitmed/Cybernetics/CyberneticsSystem.cs:27-28,45-46`).
* `<OrganComponent, MapInitEvent>` — `SharedBodySystem.Organs.cs:22`.
* `<OrganComponent, OrganComponentsModifyEvent>` — `_Shitmed/BodyEffects/OrganEffectSystem.cs:23`.
* `<WoundableComponent, PartDamageAppliedEvent>` — `OrganDamageSystem.cs:31` (the single fan-out point).
* `<OrganComponent, OrganAddedToBodyEvent/OrganRemovedFromBodyEvent>` — free (P3-D21) but unused; they
  belong to the deferred data-driven Option B.

Type name `WolfmedOrganConsequenceSystem` re-grepped clear across `Content.{Shared,Server,Client}` and
`Content.IntegrationTests`. No new component registered. No `before:`/`after:` ordering added (PLAN3 §5.4;
in particular none for the accepted EMP race).

---

## 4. Measurements this WP owed

**Prototype resolution (the real gate on PROTO A).** Not inferred from the YAML — all seven ids were
spawned in a throwaway integration test and their resolved components printed:

```
OrganHumanBrain:   health=15/15 wound=-               sev=0  hitChance=0.8 weight=0.75   Blunt .115 Slash .25  Piercing .42   Heat .15  Cold .05 Shock .3125
OrganHumanEyes:    health=15/15 wound=-               sev=0  hitChance=0.7 weight=0.2275 Blunt .115 Slash .3   Piercing .4375 Heat .15  Cold .05 Shock .25
OrganHumanLungs:   health=15/15 wound=InternalBleedingWound sev=35 hitChance=1   weight=1.38 Blunt .1 Slash .25 Piercing .42   Heat .165 Cold .05 Shock .25
OrganHumanHeart:   health=15/15 wound=InternalBleedingWound sev=45 hitChance=0.8 weight=0.64 Blunt .1 Slash .25 Piercing .455  Heat .15  Cold .05 Shock .3375
OrganHumanStomach: health=15/15 wound=InternalBleedingWound sev=25 hitChance=0.85 weight=0.56 Blunt .1 Slash .275 Piercing .4025 Heat .15 Cold .05 Shock .25
OrganHumanLiver:   health=15/15 wound=InternalBleedingWound sev=40 hitChance=1   weight=1.1  Blunt .1 Slash .3  Piercing .4375 Heat .15  Cold .05 Shock .25
OrganHumanKidneys: health=15/15 wound=InternalBleedingWound sev=30 hitChance=0.9 weight=0.51 Blunt .1 Slash .25 Piercing .4025 Heat .15  Cold .05 Shock .25
```

Every value matches `ONYX Resources/Prototypes/Body/base_organs.yml:481-491, 537-547, 665-676, 718-730,
755-767, 808-819, 846-858`, which were re-read via `git -C C:/tmp/onyx show HEAD:…` for this package.
Multi-parent inheritance resolves cleanly: the Wolfmed abstracts declare only components the upstream
organs lack, so nothing is lost or overwritten.

**The per-tick organ query (PLAN3 WP11-2 checkpoint, "one measurement this WP owes").**
`OrganHealthSystem.Update`'s `EntityQueryEnumerator<WolfmedOrganComponent, OrganComponent>()` enumerated
**0** entities before PROTO A and **7 per spawned `MobHuman`** after (`ORGANQUERY before=0 after=7
perMobHuman=7`). The loop body for a healthy organ is a single `FixedPoint2` comparison
(`organ.Health > Zero` → `continue`), so a 50-human round is ~350 comparisons per tick. The query also
covers organs on non-wound-hosts (rat lungs, `_NF` goblin organs, `_Shitmed`/`_Mono` cybernetics parented to
`OrganHuman*`): cost without behaviour, because health only ever drops through `OrganDamageSystem`, which
runs off `<WoundableComponent, PartDamageAppliedEvent>` and `WoundableComponent` exists only on wound-host
parts. **D2 holds structurally, not by an `IsServer` check.**

**Balance, restated as shipped (D4 / DECISIONS §8.6-4).** Cap `15 × 0.3 = 4.5` HP per application → exactly
**4 applications** destroy any organ at any hit size; raw damage above ~10-11 Piercing is irrelevant to
organs. Per-hit probabilities: lungs 2.41 %, liver 2.05 %, heart 1.05 %, stomach 0.98 %, kidneys 0.95 % per
torso hit; brain 4.0 %, eyes 3.5 % per head hit → **~166 to ~421 torso hits, ~100 head hits for the brain**.
Irreversible: nothing in Wolfgate raises organ health (`ChangeHealth(+x)` exists and is public, so a
phase-4 chem/surgery step is ~15 lines).

---

## 5. Deviations from PLAN3

**None material.** Three notes:

1. **PLAN3 §2.2's sketch is shipped essentially verbatim**, with one addition: a `<remarks>` block on the
   class stating the one-tick window, the intentional double disable and P3-D8's argument, so nobody
   "fixes" the double `OrganEnableChangedEvent` with a guard that also suppresses the first pass. Also
   `base.Initialize()` is called, matching `_WF` convention.
2. **`organs.yml` carries three extra header-comment lines** beyond `organs.md` §5.3's text, recording
   §8.6-4 (irreversible), §8.6-7 (human lineage only) and where `destructionWound` lives under D8. Comment
   only; no data change. The two inline comments organs.md put on `selectionWeight` were kept.
3. **`_WF` folder created:** `Content.Server/_WF/Wolfmed/Body/`. PLAN3 §2.2 specifies that path; the folder
   did not exist (the server `_WF/Wolfmed` tree had only `Compat/`, `Medical/` and the lifecycle system).

Manifest-row corrections PLAN3 §7.1 assigns to **WP11-6** were left alone rather than edited in place:
the stale `:89` `OrganHealthSystem.cs` row and the `:91` `WolfmedOrganComponent.cs` row ("No prototype
carries it until WP11") still stand; my four appended rows supersede them and WP11-6 reconciles.

---

## 6. Build / test output

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

Headless server, 120 s, `--cvar net.port=1299` → `WP11-2-report-server.log`:

```
grep -cE "\[ERRO\]|\[FATL\]|Exception"  →  0
…
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
```

Zero errors; the only warnings are the repo's pre-existing `system.chat` duplicate-emote and
`PullingSystem` command-bind noise. This run is what proves PROTO A has no unknown component or mistyped
field — every prototype in the game was loaded.

`DockTest` first (project memory trap):

```
Test Run Successful.  Total tests: 3   Passed: 3
```

Wound suite (`--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"`)
→ `WP11-2-report-tests.log`:

```
NUnit Adapter 5.2.0.0: Test execution complete
  Passed PartDamageProjectsToVisualsComponentTest [83 ms]
  Passed RealWoundHostPassiveDamageIsNeutralisedTest [673 ms]

Test Run Successful.
Total tests: 43
     Passed: 43
 Total time: 49.4187 Seconds
```

43/43, identical to WP11-1's count — no regression, no `db.ef` re-run needed.

Release YAML lint was **not** run separately; the full headless server start loads and validates every
prototype the linter would, and a Release build of the whole solution is far more expensive than the
brief's stated checkpoint. Flagged here so WP11-3 (which adds `coverage:` lists where a stray `Chest`/
`Groin` is a lint-only failure mode) runs it.

---

## 7. What later packages must know

* **`Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` was not touched.** WP11-1's D26 lift stands intact
  (`:25-26`, `:38-39` restored). P3-D24 honoured.
* **`OrganHealthSystem.cs` now has two `TerminatingOrDeleted` guards. Do not weaken them** — the same class
  of bug cost WP9 13 unrelated pooled-pair failures, and the path only became reachable in this package.
* **WP11-5 (`WolfmedOrganTest.cs`) can rely on:**
  - T-ORG-DATA's seven ids resolving exactly as printed in §4 — those literals are measured, not predicted.
  - `OrganHealthSystem.SetHealth(Entity<WolfmedOrganComponent>, FixedPoint2)` and `ChangeHealth` are
    **public**, so drive T-ORG-DESTROY / -HEART / -BRAIN / -EYES through `SetHealth(organ, 0)` + one tick,
    as PLAN3 §6.2 specifies, rather than through the ~1 %/hit damage roll.
  - `SetHealth` raises `OrganFunctionChangedEvent` **only on a transition**, so a test that sets 0 twice
    sees one raise. T-ORG-FUNC's "drop to 0 without ticking, assert, then tick and re-assert" shape is
    required to cover both passes of the disable.
  - The brain branch (`OrganHealthSystem.cs:45-54`) `continue`s before the new `TerminatingOrDeleted`
    guard, so T-ORG-BRAIN's "organ is NOT deleted" assertion is unaffected by this package's edits.
  - T-ORG-INERT's non-wound-host mob **will** appear in `OrganHealthSystem.Update`'s query (7 organs) — the
    assertion must be "no organ loses health", not "the query is empty".
* **WP11-4** is unaffected by this package; nothing here touches visuals or `Species/base.yml`.
* **Manifest:** WP11-2's rows sit immediately before `## Deviations`; its narrative block is
  `### WP11-2 (phase 3 - organ damage)` at `:904`, after `### WP11-1`. WP11-6 still owes the §7.1
  corrections to the WP6-era `:89`/`:91` rows and the `FunctionalOrganComponent` row
  (→ `skipped` permanently, P3-D8).
* **Open, recorded, not bugs:** organ damage is irreversible and invisible to players (no analyzer readout,
  no examine text) — §7.3 deviations 12 and 13, both phase-4 items; cybernetic organs inherit the organic
  policy (phase 5 owns it); ~20 non-`OrganHuman*` organ roots remain silent no-ops.
