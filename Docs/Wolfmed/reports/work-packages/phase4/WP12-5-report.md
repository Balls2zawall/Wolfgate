# WP12-5 — Wound surgery content (PLAN4 §4, P4-3b)

Worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**).
No commits. RobustToolbox untouched. Pure YAML + FTL + manifest — no C# in this package.

---

## 1. Files created / modified

| File | Status | Size |
|---|---|---|
| `WG/Resources/Prototypes/_WF/Wolfmed/Surgery/surgery_steps.yml` | **new** | 13 prototypes (12 concrete steps + `SurgeryStepHealOrganBase`), 190 lines |
| `WG/Resources/Prototypes/_WF/Wolfmed/Surgery/surgeries.yml` | **new** | 13 surgeries, 208 lines |
| `WG/Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` | **modified — PROTO F** | +3 marked lines |
| `WG/Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml` | **modified — PROTO G** | +8 lines (4 marked component additions) |
| `WG/Resources/Locale/en-US/_WF/wolfmed/surgery-popup.ftl` | **new** | 12 keys |
| `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | `### WP12-5` section appended |

All new files written CRLF to match the working tree.

**The 26 new prototypes.** Steps: `SurgeryStepSutureBleeding`, `…StopInternalBleeding`, `…SetBone`,
`…MendFracture`, `…HealAmputationConsequence`, `SurgeryStepHealOrganBase` (abstract) and
`SurgeryStepHeal{Brain,Eyes,Heart,Lungs,Liver,Stomach,Kidneys}`. Surgeries: `SurgeryStopBleeding`,
`SurgeryStopInternalBleeding`, `SurgeryMendFracture`, `SurgeryHealAmputationConsequence`,
`SurgeryTendWoundsBruteDeep`, `SurgeryTendWoundsBurnDeep`, and
`SurgeryHeal{Heart,Lungs,Liver,Stomach,Kidneys,Brain,Eyes}`. All 26 ids were grepped repo-wide before
creation (0 hits each), and **all 26 carry both `name:` and `categories: [ HideSpawnMenu ]`** as CRITIQUE4 M6
requires — including the seven organ-step children, which do not rely on inheriting `categories` from the
abstract base. No new tool prototype, no new sprite: every step reuses `Hemostat` / `Tending` / `BoneSetter` /
`BoneGel` and Wolfgate's shipped `_Shitmed/…/Surgery/*.rsi` states.

---

## 2. Every `// WOLFGATE` edit and its reason

Seven marked lines across the two upstream files this WP owns. Nothing else upstream was touched.

**PROTO F — `_Shitmed/Entities/Surgery/surgeries.yml` (2 sites, 3 lines):**

1. `SurgeryTendWoundsBrute`'s `- type: SurgeryWoundedCondition` (`:294`) gains
   `maxWoundSeverity: 99.99 # WOLFGATE (PROTO F, P4-D19): deep wounds get SurgeryTendWoundsBruteDeep instead`.
2. `SurgeryTendWoundsBurn`'s (`:307`) gains **two** lines:
   `woundGroup: Burn # WOLFGATE (PROTO F, P4-D19): GetGroupSeverity must read the Burn group here` and the
   matching `maxWoundSeverity: 99.99`. The `woundGroup` line is mandatory, not cosmetic — EXT 1's datafield
   defaults to `Brute`, so a burn surgery with only the bound would measure the wrong damage group.
   Reason: without an upper bound a badly wounded limb lists **four** overlapping tend surgeries instead of
   two. D2-safe: `WolfmedWoundWindowFails` returns `false` for any body without `WoundHostComponent`, and no
   other prototype in the tree declares either bound.

**PROTO G — `_Shitmed/Entities/Surgery/surgery_steps.yml` (4 sites, 8 lines), the complete P4-D21 chain:**

3. `SurgeryStepOpenIncisionScalpel` (`:9`) gains `- type: WolfmedSurgeryIncisionWoundEffect` / `severity: 10`,
   **added beside** its existing `SurgeryDamageChangeEffect { Bloodloss: 10 }` and explicitly not replacing it
   — replacing would strip the incision cost from non-wound-hosts, a D2 breach (§8.5 trap 14). Comment:
   *a real incision wound beside the flat charge, so it can bleed, be clamped and scar*.
4. `SurgeryStepClampBleeders` (`:31`) gains `- type: WolfmedSurgeryIncisionTreatmentEffect` /
   `treatment: Clamp`.
5. `SurgeryStepCloseIncision` (`:168`) gains the same component with `treatment: Close` — cauterise, close,
   roll `surgery.scar_chance`.
6. `SurgeryStepSealTendWound` (`:359`) gains `treatment: Close` as well. This is the Wolfgate adaptation Onyx
   has no counterpart for: the wound surgeries end on the seal step, which removes only `IncisionOpen`, so
   without it the incision wound would survive clamped-but-open until the medic separately ran
   `SurgeryCloseIncision`.

**`SurgeryStepCarefulIncisionScalpel` (`:303`) was deliberately NOT touched** (CRITIQUE4 B1 / §8.5 trap 15).
Its only two consumers — `SurgeryTendWoundsBrute` and `…Burn` — contain neither a clamp nor a close step, so a
wound effect there would leak one permanent `mergeMode: SeparateInstances` bleeder per tend operation, on the
commonest surgery in the game. Onyx's careful incision carries no bleed effect either.

**The chain shipped all-four-or-none**, as P4-D21 and §8.4 decision 3 require.

**New `_WF` files carry no `// WOLFGATE` markers** — they are new Wolfgate content under the authorised
`_WF/Wolfmed` layout (D6). The only `# WOLFGATE` comments inside them are the seven balance markers on the
organ-heal `amount: 3` lines (P4-D23).

---

## 3. Deviations from PLAN4

None material. Four notes, all inside the plan's own latitude:

1. **PROTO F is 3 lines, not the plan's "2".** §3.2 names `maxWoundSeverity` on both surgeries and adds
   "Plus `woundGroup: Burn` on the Burn one" in the same cell, so the third line is prescribed; the "+2 lines"
   count in WP12-5's file table simply did not include it.
2. **`categories: [ HideSpawnMenu ]` is written explicitly on the seven organ-step children** rather than
   inherited from `SurgeryStepHealOrganBase`. The plan's "every one of the 26 new prototypes carries `name:`
   and `categories:`" is satisfied literally, matching how every shipped Shitmed child re-states both even
   though its base sets `categories`.
3. **`SurgeryStepSetBone` carries `WolfmedSurgeryPainEffect { amount: 12 }`, the same as
   `SurgeryStepMendFracture`.** P4-D22 gives Onyx's amounts as "12 on mend-fracture, 24/sleepModifier 0 on the
   organ-heal base, 5 default elsewhere"; Onyx has no separate set-bone step (the ladder is Wolfgate's, P4-D20),
   so its half of the ladder takes the fracture amount rather than the generic 5.
4. **`SurgeryStepSutureBleeding` and `SurgeryStepStopInternalBleeding` carry `SurgeryStepEmoteEffect`** the way
   every other new step does; `surgery.md` §5.1's sketch omitted it on the suture step only. Cosmetic parity
   with the shipped Shitmed steps.

Explicitly **not** done, each per an existing decision: no `SurgeryStepSawBones` on the five torso organ heals
(`SurgeryOpenRibcage` already sawed — CRITIQUE4 M7); no `SurgeryStepClampInternalBleeders` on any organ heal
(deviation 25); no `SurgeryOrganCondition` on the heal surgeries (it would wake `SurgeryAffixOrganStep`); no
`Groin` part anywhere (D9); no step-level visibility conditions (§8.5 trap 9); no new tool, sprite, component
or subscription.

---

## 4. Build / test output

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

**Headless server** (`--cvar net.port=1299`, ~130 s) — `C:/tmp/wolfmed-plan/p4/wp/WP12-5-report-server.log`:

```
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
57:[INFO] root: Server Version 277.0.0.0 -> Ready
```

Zero unknown-component, missing-prototype, missing-parent, missing-`!type:` or duplicate-Fluent-id errors —
i.e. all 26 new prototypes and all seven marked component additions loaded. Only pre-existing `[WARN]` lines
(duplicate emote words, the `PullingSystem` bind warning).

**Release YAML lint** (`dotnet run --project Content.YAMLLinter -c Release`, run twice, before and after the
final edit):

```
::error file=/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml,line=-1,col=-1::
        /Prototypes/_Onyx/…/medical_patch.yml(-1,-1)  File not found. (/Textures)
1 errors found in 73740 ms.
```

That single error is **WP12-0's standing missing-texture hazard**, unchanged since WP12-3 and still owned by
WP12-10 (or a WP12-0 re-run). **Zero lint errors from this package's files.**

**Prototype-resolution smoke check** (WP12-5's named extra gate, because a typo'd step id is a runtime
`GetSingleton` null rather than a lint error): all 17 distinct ids referenced from `Surgery.steps` in the new
`surgeries.yml` and both `requirement:` ids resolve to exactly one `id:` in `Resources/Prototypes` each —
`SurgeryStep{SutureBleeding, StopInternalBleeding, SetBone, MendFracture, HealAmputationConsequence,
Heal{Brain,Eyes,Heart,Lungs,Liver,Stomach,Kidneys}, SawBones, RepairBruteTissue, RepairBurnTissue,
SealTendWound, SealOrganWound}`, `SurgeryOpenIncision`, `SurgeryOpenRibcage`.

No integration tests were run: WP12-9 owns every test file, and this package adds no C#.

---

## 5. What later packages must know

**A. WP12-4's HANDOFF is closed.** `SurgeryHealAmputationConsequence` exists, so HOOK 25's block is now
curable: open incision → `SurgeryStepHealAmputationConsequence` (`Tending`, 4 s) → `SurgeryStepSealTendWound`.
Traumatic amputation is no longer permanently un-reattachable. **Consequence for WP12-10:** P4-D15's guidebook
amputation-consequence paragraph ships as planned; the conditional that would have dropped it is resolved.

**B. Surgery can scar for the first time.** `surgery.scar_chance` (0.35, dormant since phase 1) is live via
`SurgeryStepCloseIncision` and `SurgeryStepSealTendWound`. **WP12-9's T-SURG-SCAR is now reachable**, and so
is the closed-loop invariant in `T-SURGERY-PROTOTYPE-SANITY`: every surgery whose step list can open a
`SurgicalIncisionWound` must also contain, or require, a step carrying
`WolfmedSurgeryIncisionTreatmentEffect { treatment: Close }`. In the shipped set that holds because the four
wound surgeries with `requirement: SurgeryOpenIncision` all end on `SurgeryStepSealTendWound`, and the seven
organ heals end on `SurgeryStepSealOrganWound` — **note that `SurgeryStepSealOrganWound` has NO `Close`
effect**, so an organ-heal operation's incision is closed only by a later `SurgeryCloseIncision`. That is the
same position `SurgeryRemove/InsertHeart` etc. have been in since Shitmed, and it is deliberate (PROTO G is
four sites, not five), but a test asserting "every incision is auto-closed" would fail on the organ heals.

**C. Balance numbers WP12-10 must record in `WOLFMED_STATUS.md`:** organ heal `amount: 3` (P4-D23, ~10 s per
organ, five 2 s repeats); surgery pain 5 / 12 / 24 (P4-D22, `sleepModifier` inert); incision cost on a wound
host is now flat `Bloodloss: 10` **plus** a severity-10 `SurgicalIncisionWound` (~1.3×, deviation 9).

**D. For WP12-9's fixtures.** `SurgeryStopBleeding`'s tight `bleeding: true` condition is on the **surgery**,
so a repeat loop ends with a `"tried to start invalid surgery"` warning line — expected, not a failure; do not
assert a clean log for that surgery. `SurgeryTendWounds*Deep` inherits Shitmed's **body-scoped**
`OnTendWoundsCheck`, so its repeat runs until the whole patient is clean, not just the operated limb.
`SurgeryHeal{Brain,Eyes}` need `SurgeryStepSawBones` completed first (a `BoneSaw`), the torso five do not (the
saw is spent inside `SurgeryOpenRibcage`). Organ-heal surgeries list only while `0 < Health < MaxHealth`
(WP12-4's S3), so a fixture must damage the organ without destroying it.

**E. Upstream-file count for WP12-10's §3.4 arithmetic: +2.** Both
`Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` and `surgery_steps.yml` are **first-time
Wolfmed touches** — verified: `git show HEAD:<file> | grep -c WOLFGATE` → 0 for each. They are the two files
PLAN4 §3.4 lists as `_Shitmed/…/surgeries.yml` and `_Shitmed/…/surgery_steps.yml` in the 27 → 46 arithmetic.

**F. File ownership released.** `_Shitmed/Entities/Surgery/surgeries.yml` and `surgery_steps.yml` are done
with; no later phase-4 package is scheduled to touch them.
