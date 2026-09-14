# WP12-5 verify — Wound surgery content (P4-3b)

**Verdict: PASS.** No blockers, no majors.

## 1. Build

Both builds run sequentially in the worktree, 0 errors each.

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat` (path-limited to Content.Shared/Server/Client, Resources, Content.IntegrationTests) shows
13 modified tracked files, cumulative across all of phase 4 so far (no commits since phase 3). Filtering to
files **not** under `_Onyx`/`_WF`:

| File | Owning WP | Marked? | Authorised by |
|---|---|---|---|
| `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` | WP12-1 | yes, every added line | HOOK 9(b) |
| `Content.Server/EntityEffects/Effects/HealthChange.cs` | WP12-1 | yes, every added line | HOOK 9(a) |
| `Content.Shared/Gibbing/Systems/GibbingSystem.cs` | pre-phase-4 | yes | DECISIONS "Gibbing fix" |
| `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` | WP12-4 | yes | EXT 1 |
| `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` | WP12-4 | yes | HOOK 24 + HOOK 25 |
| `Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` | WP12-3 | yes | PROTO E |
| `Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml` | WP12-3 | yes | PROTO D |
| `Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml` | WP12-2 | yes | PROTO K |
| `Resources/Prototypes/Reagents/medicine.yml` | WP12-2 | yes | PROTO I |
| `Resources/Prototypes/Reagents/narcotics.yml` | WP12-2 | yes | PROTO J |
| `Resources/Prototypes/_Goobstation/Reagents/medicine.yml` | WP12-2 | yes | PROTO H |
| `Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` | **WP12-5** | yes | **PROTO F, P4-D19** |
| `Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml` | **WP12-5** | yes | **PROTO G, P4-D21** |

Every changed line in every file is `// WOLFGATE` / `# WOLFGATE`-marked and traces to a hook PLAN4 §3
authorises for this or an earlier phase-4 WP (all pre-verified in their own WPs; re-inspected here for
completeness since the diff is cumulative). `Docs/Wolfmed/DECISIONS.md` was also touched (a mirror copy of the
orchestrator decisions appended by an earlier WP) — outside the five source roots this check scopes to, not
inspected further.

**WP12-5's own two files, re-verified line by line:**

- **PROTO F** (`surgeries.yml`): 3 lines, exactly `SurgeryTendWoundsBrute`'s `maxWoundSeverity: 99.99` and
  `SurgeryTendWoundsBurn`'s `woundGroup: Burn` + `maxWoundSeverity: 99.99`, each `# WOLFGATE (PROTO F, P4-D19)`.
  Matches PLAN4 §3 exactly (report's 3-vs-plan's-"2" note is a table-count typo, not a deviation — the plan's
  own §3 prose prescribes all three lines).
- **PROTO G** (`surgery_steps.yml`): 4 sites, each one `- type: Wolfmed…` component addition beside the
  step's existing effects, each `# WOLFGATE (PROTO G, P4-D21)`:
  `SurgeryStepOpenIncisionScalpel` → `WolfmedSurgeryIncisionWoundEffect { severity: 10 }` (added beside
  `SurgeryDamageChangeEffect { Bloodloss: 10 }`, not replacing it — confirmed both components present);
  `SurgeryStepClampBleeders` → `WolfmedSurgeryIncisionTreatmentEffect { treatment: Clamp }`;
  `SurgeryStepCloseIncision` → same component, `treatment: Close`;
  `SurgeryStepSealTendWound` → same component, `treatment: Close`.
  `SurgeryStepCarefulIncisionScalpel` confirmed untouched (component list is still
  `SurgeryStep` + `Sprite` + `SurgeryStepEmoteEffect`, nothing else).

**D2 check.** `WolfmedWoundWindowFails` (HOOK 24 body, `SharedSurgerySystem.Wolfmed.cs`) returns `false`
immediately when both bounds are null or the body lacks `WoundHostComponent`, so PROTO F is inert for every
non-host and for every other prototype (both bounds stay null everywhere else — grepped). PROTO G's new step
effects are handled by `WolfmedWoundSurgerySystem`: `OnOpenIncision` is explicitly
`HasComp<WoundHostComponent>(args.Body)`-gated (a non-host keeps only the pre-existing flat
`SurgeryDamageChangeEffect`); `OnTreatIncision`'s `Close`/`Clamp` branches require `WoundableComponent` on the
part (present only on wound-host body parts per D32), so a non-host step invocation is a no-op. No behaviour
change for entities without `WoundHostComponent` is confirmed on both upstream files.

## 3. Vendoring fidelity

No files under `_Onyx/` were added or changed by this WP (`git status` shows all existing `_Onyx` entries
belong to WP12-0/WP12-2/WP12-3). N/A for this package — consistent with the report's "no C# in this package"
claim.

## 4. Collisions

- **Subscriptions:** none registered by this WP (pure YAML/locale; the step-effect handlers it wires prototypes
  to were registered by WP12-4's `WolfmedWoundSurgerySystem`/`WolfmedSurgeryConditionSystem`, already audited
  there).
- **Components:** none new; this WP only attaches WP12-4's existing `WolfmedSurgery*` components to prototypes.
- **Prototype ids:** all 26 new ids (13 surgeries + 13 steps, incl. the abstract `SurgeryStepHealOrganBase`)
  grepped for `^  id: <id>$` across `Resources/Prototypes` — each resolves to exactly one hit.
- **Locale ids:** all 12 new `surgery-popup-step-*` Fluent keys grepped across `Resources/Locale` — each
  resolves to exactly one hit. Key format matches the consumer
  (`SharedSurgerySystem.Steps.cs:792`, `Loc.GetString($"surgery-popup-step-{args.Step}", …)`).

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-5 (phase 4 — wound surgery content, P4-3b)` section
(lines 1539–1647) with a row for every file this WP touched: the two new `_WF` YAML files, PROTO F, PROTO G,
the new locale file, and the manifest's own row. Content matches the report and the tree.

## 6. Plan conformance

Every file in PLAN4 §WP12-5's table exists:

1. `Resources/Prototypes/_WF/Wolfmed/Surgery/surgery_steps.yml` — new, 12 concrete steps + 1 abstract base. ✓
2. `Resources/Prototypes/_WF/Wolfmed/Surgery/surgeries.yml` — new, 13 surgeries. ✓
3. `Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` — PROTO F. ✓ (3 lines, not the table's "2";
   justified above and in the report — the plan's own §3 prose prescribes 3, so this is a plan-table
   transcription slip, not a WP deviation.)
4. `Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml` — PROTO G, all four sites `:9`/`:31`/
   `:168`/`:359`; `:303` (`SurgeryStepCarefulIncisionScalpel`) confirmed not touched. ✓
5. `Resources/Locale/en-US/_WF/wolfmed/surgery-popup.ftl` — new, 12 keys. ✓
6. `Docs/Wolfmed/WOLFMED_MANIFEST.md` — `### WP12-5` appended. ✓

Decisions honoured, re-checked directly in the tree:

- **P4-D19 / P4-D21** shipped exactly as specified (see §2 above).
- **P4-D20** (fracture ladder): `SurgeryStepSetBone` uses `BoneSetter` → `Reduced`; `SurgeryStepMendFracture`
  uses `BoneGel` → `Mended`. ✓
- **P4-D22** (surgery pain): `WolfmedSurgeryPainEffect` present on all 12 new steps; `12` on both fracture
  steps, `24`/`sleepModifier: 0` on `SurgeryStepHealOrganBase`, component default `5` elsewhere. ✓
- **P4-D23** (organ heal `amount: 3`): present on all seven organ-heal steps with the required
  `# WOLFGATE (P4 balance)` comment. ✓
- **CRITIQUE4 M6** (`name:` + `categories: [ HideSpawnMenu ]` on all 26 new prototypes): verified — no
  prototype block in either new `_WF` file is missing either field, including the seven organ-step children
  that re-state `categories` rather than inherit it (report deviation 2, cosmetic, matches shipped-Shitmed
  convention).
- **CRITIQUE4 M7** (organ bone gate): five torso heals require no `SurgeryStepSawBones` (already spent inside
  `SurgeryOpenRibcage`); `SurgeryHealBrain`/`SurgeryHealEyes` both prefix `SurgeryStepSawBones`. ✓
- **§8.4 decision 2** (organ heal rate `amount: 3`) and **decision 3** (ship the four-prototype scar chain
  complete, careful incision untouched) both honoured. ✓
- Organ slot ids match `Body/Prototypes/human.yml` (`brain`, `eyes`, `heart`, `lungs`, `liver`, `stomach`,
  `kidneys` — seven, not Onyx's ten, per D9/D3). ✓
- `SurgeryOrganCondition` deliberately not added to any organ-heal surgery (grepped 0 hits in the new files),
  matching the plan's explicit "do not add" instruction (the affix-step early-return argument). ✓
- Subscription pairs / components registered: correctly none (pure YAML + locale). ✓

No unauthorised deviations found. The report's four "deviations" (PROTO F's 3rd line, explicit `categories` on
organ children, `SetBone`'s pain amount matching `MendFracture`, and the emote effect on the two bleeding
steps) are all cosmetic/latitude items already covered by the plan's own text or its silence, as the report
argues; none change shipped behaviour outside plan intent.

## 8. CRITIQUE4 B1 spot check

- `SurgeryStepCarefulIncisionScalpel` (surgery_steps.yml:309-321): component list is `SurgeryStep` + `Sprite` +
  `SurgeryStepEmoteEffect` only — **no** `WolfmedSurgeryIncisionWoundEffect` and no other damage/wound effect.
  Confirmed its only two consumers (`SurgeryTendWoundsBrute`, `SurgeryTendWoundsBurn`) reference it and neither
  surgery's step list contains a clamp or close step.
- `SurgeryStepSealTendWound` (surgery_steps.yml:365-384): carries
  `WolfmedSurgeryIncisionTreatmentEffect { treatment: Close }` alongside its existing
  `SurgeryDamageChangeEffect`/`SurgeryStepEmoteEffect`. Confirmed present.

Both halves of check 8 pass.

## 7. Snapshot

Written:

- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-5.patch` (825 lines) —
  `git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests`
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-5.untracked.txt` (45 lines) —
  `git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests`

Both are cumulative (phase 4 has made no commits), consistent with WP12-0 through WP12-4's snapshots in the
same directory.

## Notes / non-blocking observations

- The Release YAML lint's one standing error (`_Onyx/…/medical_patch.yml` missing-texture) is WP12-0's hazard,
  unchanged, owned by WP12-10 — not this WP's to fix, and not a WP12-5 regression (re-confirmed via the
  report's lint log; not re-run here since check list items 1–8 don't call for it and the file this WP owns is
  untouched by that hazard).
- `Docs/Wolfmed/DECISIONS.md`'s modification (an appended mirror of the orchestrator's phase-4 decisions) is
  outside the five roots §2 scopes to, and outside WP12-5's file table; not flagged as a WP12-5 issue.
