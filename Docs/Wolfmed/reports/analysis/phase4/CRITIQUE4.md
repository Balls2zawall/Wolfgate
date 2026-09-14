# CRITIQUE4 — completeness critic on PLAN4.md and the five phase-4 analyst reports

**Scope.** Read `C:/tmp/wolfmed-plan/p4/PLAN4.md` in full (§1–§9) plus `analyzer.md`, `reagents.md`,
`surgery.md`, `tests.md`, `tools.md` (TOCs + the sections PLAN4 cites), and `DECISIONS.md`. Everything below
was re-grepped in the live tree (**WG** = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`,
HEAD `6329d204e3`) or in **ONYX** (`C:/tmp/onyx`, pin `2f5bab9`) during this pass. Read-only; nothing was
changed.

**Verdict.** The plan is unusually well verified — I could not break its subscription audit, its component
and type registration audit, its NetSerializable analysis, its sandbox analysis, or its `!type:` collision
analysis, and the single hardest architectural claim (the shared/server split of the two surgery step events)
is **correct** where Shitmed's own in-file comment is stale. But there are **two blockers** and **nine majors**,
concentrated in exactly the two places the plan itself flags as the highest-risk silent failures (PROTO G's
incision chain and the reagent metabolism group) — in both cases the plan names the trap and then walks into
it.

---

## 0. Findings at a glance

| Id | Sev | One line |
|---|---|---|
| **B1** | blocker | PROTO G leaves a permanently bleeding, stacking `SurgicalIncisionWound` on every tend-wounds operation |
| **B2** | blocker | The five new Tier-A reagents keep Onyx's `Bloodstream:` metabolism group, which does not exist in Wolfgate |
| **M1** | major | §3 ("the complete authorised list") omits two upstream files two WPs must edit; §3.4's count is wrong |
| **M2** | major | WP12-2 item 8 (chem-dispenser inventory) is not implementable as written and is unnecessary |
| **M3** | major | P4-D21 says the incision effect *replaces* the flat Bloodloss; §2.7/PROTO G/trap 14 say *adds beside* |
| **M4** | major | No test covers PROTO H–K — the phase's own #1 and #2 silent-failure risks are untested |
| **M5** | major | PROTO K names no metabolism group; Cognac's only group is `Drink:`, not Onyx's `Digestion:` |
| **M6** | major | WP12-5's 26 new surgery/step prototypes carry no `name:` and no `categories:`; they render blank in the BUI |
| **M7** | major | Organ-heal surgeries skip the `SawBones` + `ClampInternalBleeders` gate every other organ surgery on the same part requires |
| **M8** | major | HOOK 26's `PopulateWolfmed` placement misses `Populate`'s early-return paths → stale panel on a failed scan |
| **M9** | major | Nothing tests HOOK 22 itself; the feature can ship dormant with every listed test green |
| m1–m8 | minor | line-number drift, counts, one unchecked API pair, one missing `!type:` row, one UX gap |

---

## 1. Blockers

### B1 — PROTO G leaves a permanently bleeding, `SeparateInstances`-stacking surgical wound on the tend-wounds path

**Severity: blocker.** This is the exact failure §8.5 trap 15 names ("a permanently bleeding,
`SeparateInstances`-stacking wound per operation"), shipped by the plan's own PROTO G spec, and no test in
§6.2 can catch it.

**Evidence.**

PROTO G (§3.2) authorises four marked additions:

* `SurgeryStepOpenIncisionScalpel` (`Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml:9`) gains `- type: WolfmedSurgeryIncisionWoundEffect { severity: 10 }`
* `SurgeryStepCarefulIncisionScalpel` (`:303`) gains the same
* `SurgeryStepClampBleeders` (`:31`) gains `- type: WolfmedSurgeryIncisionTreatmentEffect { treatment: Clamp }`
* `SurgeryStepCloseIncision` (`:168`) gains `- type: WolfmedSurgeryIncisionTreatmentEffect { treatment: Close }`

(All four line numbers verified exact: `grep -n "id: SurgeryStep…" surgery_steps.yml` → `9, 31, 168, 303`.)

But `SurgeryStepCarefulIncisionScalpel` is used **only** by the tend-wounds chain, which contains neither of
the two treatment steps:

```
Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml:284-293
- type: entity
  parent: SurgeryBase
  id: SurgeryTendWoundsBrute
  name: Tend Bruise Wounds
  components:
  - type: Surgery
    steps:
    - SurgeryStepCarefulIncisionScalpel   # :291
    - SurgeryStepRepairBruteTissue
    - SurgeryStepSealTendWound            # :293  <- NOT SurgeryStepCloseIncision
  - type: SurgeryWoundedCondition
```

`SurgeryTendWoundsBurn` is identical (`:296-307`, careful incision at `:304`, seal at `:306`). `grep -n
"SurgeryStepCarefulIncisionScalpel\|SurgeryStepSealTendWound" surgeries.yml` returns exactly those four lines
and nothing else — no surgery pairs the careful incision with `SurgeryStepClampBleeders` or
`SurgeryStepCloseIncision`. WP12-5's two new `SurgeryTendWounds*Deep` surgeries also end on
`SurgeryStepSealTendWound` (PLAN4 line ~1231).

The wound that is opened and never closed:

```
Resources/Prototypes/_Onyx/Wounds/wounds.yml:373-382
- type: wound
  id: SurgicalIncisionWound
  name: wound-name-surgical-incision
  damageTypes: {}
  mergeMode: SeparateInstances     # one NEW wound entity per operation
  maximumSeverity: 200
  behaviors:
  - !type:WoundBleedingBehavior
    rate: 0.1
    awakeMultiplier: 3
```

So each tend-wounds operation on a wound host adds a fresh severity-10 bleeder (the plan's own §8.3 computes
3.0/s raw on an awake patient) with no clamp, no cauterisation and no scar roll. Two tend operations = two
stacked instances.

**It is also not Onyx-faithful.** Onyx's own careful incision carries **no** bleed effect:

```
ONYX Resources/Prototypes/_Onyx/Entities/Surgery/surgery_steps.yml:958-970
- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepCarefulIncisionScalpel
  name: Make a careful incision
  components:
  - type: SurgeryStep
    tool: [ { type: Scalpel } ]
    duration: 3
  - type: Sprite …
  - type: SurgeryStepPainInflicter
    amount: 17
    sleepModifier: 0
```

Onyx puts `SurgeryStepBleedEffect { damage: 10 }` on `SurgeryStepOpenIncisionScalpel` only
(`ONYX surgery_steps.yml:20`), pairs it with `SurgeryClampBleedEffect` on `SurgeryStepClampBleeders` (`:67`)
and `SurgeryCloseIncisionEffect` on `SurgeryStepCloseIncision` (`:229`) — a closed three-step loop that WG's
`SurgeryOpenIncision`/`SurgeryCloseIncision` pair reproduces exactly. The careful incision sits outside that
loop in both trees.

**A second, load-bearing factual error in the same section.** §2.7 states: "`SurgeryStepOpenIncisionScalpel`
and `SurgeryStepCarefulIncisionScalpel` today carry `- type: SurgeryDamageChangeEffect { damage: { types:
{ Bloodloss: 10 } }, sleepModifier: 0.5 }`." That is false for the careful incision, which has no damage
effect at all — its full component list is `SurgeryStep` + `Sprite` + `SurgeryStepEmoteEffect`
(`surgery_steps.yml:303-316`, read in full above). `grep -n "SurgeryDamageChangeEffect" surgery_steps.yml`
returns `22, 44, 187, 231, 271, 372, 467, 480, 493, 535` — the block at `:22` belongs to
`SurgeryStepOpenIncisionScalpel`; nothing between `:303` and `:316`. The "deliberate ~1.3× increase in the
cost of opening an incision" recorded as deviation 9 (§7.3) therefore describes only one of the two steps.

**Why no test catches it.** T-SURG-SCAR (§6.2) raises `WolfmedSurgeryIncisionWoundEffect`, then the `Clamp`
treatment, then the `Close` treatment on **bare `[TestPrototypes]` effect entities**, in sequence, by hand. It
never indexes `SurgeryTendWoundsBrute` or walks a real step list, so it asserts the C# chain works and says
nothing about whether the prototypes ever reach it. `T-SURGERY-PROTOTYPE-SANITY` only iterates "the 13 new
surgery ids" — the two tend surgeries are pre-existing and not in that set.

**Fix (pick one; the first is recommended).**

1. **Drop `WolfmedSurgeryIncisionWoundEffect` from `SurgeryStepCarefulIncisionScalpel`.** PROTO G becomes
   three additions, not four. This is Onyx parity, keeps the whole incision-wound life-cycle inside the
   `SurgeryOpenIncision` → `SurgeryCloseIncision` loop that already contains all three effects, and the two
   `*Deep` surgeries still scar because they run `requirement: SurgeryOpenIncision`. Correct §2.7's false
   claim and re-word deviation 9 to cover one step.
2. If the careful incision must bleed, **also** add `- type: WolfmedSurgeryIncisionTreatmentEffect
   { treatment: Close }` to `SurgeryStepSealTendWound` (`surgery_steps.yml:359`) — making PROTO G five
   additions — and add a `Clamp` somewhere in the tend chain or accept an unclamped bleeder for the duration
   of the operation. §8.2's tend-wounds duration row and §8.3's "the incision now bleeds" bullet both need
   updating.

Either way, **extend `T-SURGERY-PROTOTYPE-SANITY`** with a prototype-level invariant that closes the class of
bug permanently: *every step prototype carrying `WolfmedSurgeryIncisionWoundEffect` must appear in a
`Surgery.steps` list that also reaches a step carrying `WolfmedSurgeryIncisionTreatmentEffect { treatment:
Close }`, directly or via `Surgery.requirement`.*

---

### B2 — The five new Tier-A reagents keep Onyx's `Bloodstream:` metabolism group, which does not exist in Wolfgate

**Severity: blocker** (prototype load / Release-lint failure for the whole WP12-2 file; or, if the key were
tolerated, five inert reagents — the entire gameplay point of P4-1).

**Evidence.**

Every Tier-A reagent in Onyx is defined under `metabolisms: Bloodstream:`:

```
ONYX Resources/Prototypes/_Onyx/Reagents/Medicine/medicine.yml
  id: Osteogen   … metabolisms:  Bloodstream:   (line 9 of the extracted block)
  id: Ibuprofen  … metabolisms:  Bloodstream:
  id: Ketorolac  … metabolisms:  Bloodstream:
  id: Tramadol   … metabolisms:  Bloodstream:   metabolismRate: 0.1
  id: Oxycodone  … metabolisms:  Bloodstream:
```

Wolfgate has no such group. The complete set:

```
Resources/Prototypes/Chemistry/metabolism_groups.yml:3,7,11,15,19,23,28,33
  Poison, Medicine, Narcotic, Alcohol, Food, Drink, Gas, PlantMetabolisms
Resources/Prototypes/_NF/Chemistry/metabolism_groups.yml:3
  Cryogenic
```

`grep -rn "^    Bloodstream:$" Resources/Prototypes/Reagents` → **0 hits**: not one Wolfgate reagent uses it.

And the key is ProtoId-validated, so this is a hard failure, not a silent one:

```
Content.Shared/Chemistry/Reagent/ReagentPrototype.cs:155
    public FrozenDictionary<ProtoId<MetabolismGroupPrototype>, ReagentEffectsEntry>? Metabolisms;
```

**Why the plan misses it.** §8.5 risk 4 states the trap correctly — "Copying Onyx's metabolism group header
(`Bloodstream:`) into a Wolfgate reagent → the effect lands in a group the target's metabolizer does not
process and **never fires**, with no error" — and then scopes the remedy to "**The four correct groups are
named in PROTO H–K**", i.e. the four *existing* Wolfgate reagents being edited in place. WP12-2 item 5 (the
five brand-new reagents, "~140 lines") carries no group instruction at all, and WP12-2's `!type:` translation
table — explicitly billed as "the single biggest source of silent breakage in this WP; every tag in a copied
block must be checked against it" — translates **effect tags only**, never the group header. A literal
execution of item 5 copies `Bloodstream:` across five reagents.

**Fix.** Add a row to WP12-2's translation table above the `!type:` rows:

> | `metabolisms: Bloodstream:` | → **`Medicine:`** — Wolfgate has no `Bloodstream` metabolism group
> (`Chemistry/metabolism_groups.yml`); all five Tier-A reagents are `group: Medicine` and their effects
> belong in the liver's `Medicine` pass. Applies to Tier B (`Probital`, `Mitogen`) too if it is taken. |

Also correct §8.5 risk 4 to read "PROTO H–K **and the five new reagents**", and add the group check to M4's
proposed prototype test so the mapping is asserted rather than trusted.

---

## 2. Majors

### M1 — §3 is declared exhaustive but omits two upstream files that two work packages must edit

Ground rule 3 (PLAN4 line 26) and §3's own preamble (line 359: "Nothing outside this section may be edited in
an upstream (non-`_Onyx`, non-`_WF`) file without escalating") make §3 the authority. Two required edits are
absent from both §3.1 (code hooks) and §3.2 (non-hook edits):

1. **`Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`.** WP12-6 item 4 (PLAN4 line 1282):
   "**modified**, 4 fields + 4 optional ctor params + 2 marked `using`s, ~10 lines", and the manifest row at
   line 1683 repeats it. The file is upstream and un-Wolfmed-touched today (verified: it carries only
   `// Shitmed Change` and `// Frontier` markers). It is also not in §3.4's arithmetic — the list of 18 added
   files there produces 45, and with this file it is **46**.
2. **The chem-dispenser inventory** — WP12-2 item 8, "**modified** — list the five so they are craftable
   in-round". The file is `Resources/Prototypes/Catalog/ReagentDispensers/chemical.yml` (the plan never names
   a path). See M2: it should be deleted rather than authorised.

**Fix.** Add the message file to §3.2 as `EXT 2` with the exact shape (4 nullable fields, 4 optional trailing
ctor params, the two `using`s for `Content.Shared._Onyx.Medical` and `Content.Shared.FixedPoint`), correct
§3.4 to **46**, and delete WP12-2 item 8 per M2. WP12-10's reconcile depends on that count being right.

### M2 — WP12-2 item 8 (chem-dispenser inventory) cannot be done as described and is not needed

```
Resources/Prototypes/Catalog/ReagentDispensers/chemical.yml:1-22
- type: reagentDispenserInventory
  id: ChemDispenserStandardInventory
  inventory:
  - JugAluminium
  - ReinforcedJugCarbon # Frontier
  … JugFluorine, JugIodine, JugIron, JugMercury, JugRadium, JugSodium, JugSulfur …
```

The inventory holds **jug entity ids for base elements**, not reagent ids: `grep -c "Bicaridine"` on that file
→ **0**. No medicine is dispensable there. Listing Osteogen/Ibuprofen/Ketorolac/Tramadol/Oxycodone would
require five new `Jug*` entity prototypes plus sprites — a content change with no Onyx precedent — and is
redundant, because WP12-2 item 6 ships five reactions whose precursors are all present in WG (verified:
`Benzene, Acetone, Inaprovaline, Phosphorus, Milk, Charcoal, Fluorine, Epinephrine, Plasma, Carbon, Ethanol`
each `grep "^  id: X$" Resources/Prototypes/Reagents` → 1 hit; Plasma 2, the gas and the material).

**Fix.** Delete item 8; WP12-2 drops from 10 files to 9 in §9's table. Record in the manifest that the five
reagents are reaction-only (chemist-craftable), matching Onyx.

### M3 — P4-D21 contradicts §2.7, PROTO G and §8.5 trap 14 on whether the flat `Bloodloss: 10` is replaced

* **P4-D21** (line 96): "`WolfmedSurgeryIncisionWoundEffect` on the two incision steps **replaces** their flat
  `Bloodloss: 10`" … "replacing WG's `SurgeryDamageChangeEffect { Bloodloss: 10 }` rather than stacking on top
  of it is the Onyx-faithful shape".
* **§2.7** (line 130): "The authorised edit therefore **keeps** the `SurgeryDamageChangeEffect` block and adds
  `- type: WolfmedSurgeryIncisionWoundEffect` beside it."
* **PROTO G** (§3.2): "**beside** their existing `SurgeryDamageChangeEffect` (§2.7 — do not remove it, that
  would change non-host behaviour)."
* **§8.5 trap 14**: "Replacing `SurgeryDamageChangeEffect`'s flat Bloodloss on the incision steps → non-wound-hosts
  lose the incision cost entirely, **a D2 breach**."

The decision table is the first thing an implementing agent reads and is the one that says "replaces". This is
a one-word error with a D2 breach as its payload.

**Fix.** Re-word P4-D21's decision cell to "**adds beside** the existing flat `Bloodloss: 10` (the replacement
shape is Onyx's but would be a D2 breach here — see §2.7)". Keep the Onyx comparison in the rationale column.

### M4 — the phase's two highest-rated silent-failure risks have zero test coverage

§8.5 ranks as risks 4 and 5: a wrong metabolism group ("the effect … **never fires**, with no error") and a
mistranslated `!type:` ("a silently different effect at worst"). Every reagent test in §6.2 —
T-REAGENT-CAP-YES/NO, T-REAGENT-SYSTEMIC-BYPASS, T-REAGENT-SUPPRESS, T-REAGENT-MEND, T-REAGENT-STAM —
constructs the effect object in C# (`new SuppressPain { … }.Effect(args)`) and **never indexes a shipped
`ReagentPrototype`**. So PROTO H, I, J and K can all be written into the wrong group, or with a mistranslated
tag that happens to parse, and the whole suite stays green. The only backstop is the Release YAML lint, which
catches an unknown `!type:` and an invalid `ProtoId` group key but **not** a valid-but-wrong group (e.g.
`SuppressPain` landed in Desoxyephedrine's `Poison:` block instead of `Narcotic:`, which the plan explicitly
warns about).

**Fix.** Add `T-REAGENT-PROTOTYPE-SANITY` to WP12-9, modelled on the already-planned
`T-SURGERY-PROTOTYPE-SANITY` (prototype-only, no mob, ~30 lines): index `Cognac`, `Bicaridine`,
`Desoxyephedrine`, `Happiness`, `Stasizium` and the five new reagents; assert each carries a `SuppressPain`
(or `MendFractures`) instance under the expected `ProtoId<MetabolismGroupPrototype>` key — `Drink`, `Medicine`,
`Narcotic`, `Narcotic`, `Medicine` and `Medicine`×5 respectively — with the amounts §8.1 lists. This is also
the natural home for B2's group assertion.

### M5 — PROTO K names no metabolism group, and Cognac's only group is `Drink:`

§8.5 trap 4 claims "The four correct groups are named in PROTO H–K". PROTO H names `Medicine`, PROTO I names
`Medicine`, PROTO J names `Narcotic`. **PROTO K (line 386) names none.**

```
Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml:105-127
- type: reagent
  id: Cognac
  parent: BaseAlcohol
  …
  metabolisms:
    Drink:
      effects:
      - !type:SatiateThirst
        factor: 2
      - !type:AdjustReagent
        reagent: Ethanol
        amount: 0.2
```

`Drink` is the only group Cognac has. Onyx puts its `SuppressPain` block in a `Digestion:` group that does not
exist in WG (`ONYX alcohol.yml:110-121`, under `<Onyx-PartPain>`). `Drink` is processed by the stomach
metabolizer at a different rate from the liver's `Medicine`/`Narcotic` pass, so the effective suppression
cadence differs from Onyx's — small at `amount: 0.25`, but it is a deviation, not parity.

**Fix.** PROTO K must read "in the **`Drink:`** group (Cognac's only group); Onyx uses its own `Digestion`
group, which WG does not have — record the differing metabolizer and rate as a deviation in §7.3."

### M6 — WP12-5's 26 new surgery and step prototypes carry no `name:` and no `categories:`

```
Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml:1-3
- type: entity
  id: SurgeryBase
  categories: [ HideSpawnMenu ]        # no name:

Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml:1-5
- type: entity
  id: SurgeryStepBase
  categories: [ HideSpawnMenu ]        # no name:
  components:
  - type: SurgeryStep
```

Every shipped surgery and step sets its own `name:` (`SurgeryOpenIncision` → `name: Open Incision` at
`surgeries.yml:8`; `SurgeryStepOpenIncisionScalpel` → `name: Cut with a scalpel` at `surgery_steps.yml:10`)
**and** repeats `categories: [ HideSpawnMenu ]`. None of WP12-5's 13 abridged surgery entries (PLAN4 lines
1182-1234) or its 13 step entries set either. Unnamed prototypes render with an empty label in the surgery
BUI's surgery list and step rows — a shipped-broken UI, not a crash.

**Fix.** Require `name:` and `categories: [ HideSpawnMenu ]` on all 26 new prototypes; add
`Assert.That(proto.Name, Is.Not.Empty)` to `T-SURGERY-PROTOTYPE-SANITY` for the 13 surgeries and their steps.
(WP12-5 already budgets a `surgery-popup.ftl`, so the popup keys are covered; the entity `name:` is separate
and inline per Shitmed's convention — the plan notes this for popups at §2.12 but not for names.)

### M7 — the organ-heal surgeries skip the bone gate every other organ surgery on the same part requires

Shitmed's own organ surgeries all open bone and clamp internal bleeders before touching an organ:

```
Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml:327-343   SurgeryRemoveBrain
    requirement: SurgeryOpenIncision
    steps: [ SurgeryStepSawBones, SurgeryStepClampInternalBleeders, SurgeryStepRemoveOrgan ]
    SurgeryPartCondition part: Head

:396-412   SurgeryRemoveHeart
    requirement: SurgeryOpenRibcage
    steps: [ SurgeryStepSawBones, SurgeryStepClampInternalBleeders, SurgeryStepRemoveOrgan ]

:556-571   SurgeryRemoveEyes   — identical shape on the Head
```

WP12-5's organ-heal surgeries (PLAN4 lines 1234-1236) are `requirement: SurgeryOpenRibcage` (torso) or
`SurgeryOpenIncision` (head) plus `[ SurgeryStepHeal<Organ>, SurgeryStepSealOrganWound ]` — **no
`SurgeryStepSawBones`, no `SurgeryStepClampInternalBleeders`**. A surgeon reaches and repairs a brain with
scalpel + retractor + hemostat + cautery, while removing that same brain requires a circular saw. §8.2's
"~20 s from a fully wrecked organ" is computed on the short chain.

This is not flagged anywhere in PLAN4, `surgery.md` or `tests.md`.

**Fix.** Either prefix `SurgeryStepSawBones` + `SurgeryStepClampInternalBleeders` to the seven organ-heal step
lists (and recompute §8.2's organ row: +4 s, and the tool list gains `Saw` — already in a standard kit), or
record it in §7.3 as a deliberate simplification with the reason ("healing does not breach the organ, only
the cavity"). Silence is the one option that should not ship, because a reviewer will read it as an oversight.

### M8 — HOOK 26's `PopulateWolfmed(msg)` never runs on `Populate`'s early-return paths

```
Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml.cs:110-127
        public void Populate(HealthAnalyzerScannedUserMessage msg)
        {
            _target = _entityManager.GetEntity(msg.TargetEntity);
            EntityUid? part = msg.Part != null ? _entityManager.GetEntity(msg.Part.Value) : null;
            var isPart = part != null;

            if (_target == null
                || !_entityManager.TryGetComponent<DamageableComponent>(isPart ? part : _target, out var damageable))
            {
                NoPatientDataText.Visible = true;
                …
```

HOOK 26 (§3.1) places `PopulateWolfmed(msg); // WOLFGATE: HOOK 26` as the **last statement of `Populate`,
after `:229`** (`DrawDiagnosticGroups(...)`, the final call before the closing brace at `:230`). On a scan
that hits the early return — target gone, target has no `DamageableComponent`, or the selected part was
detached mid-scan — the `_WF` panel is never touched and keeps rendering the **previous** patient's wound
rows, organ rows and chemical list next to "No patient data". The analyzer re-sends once per second, so this
is reachable simply by walking out of range.

**Fix (one marked line either way).** Make HOOK 26's line the **first** statement of `Populate` — the panel
already hides itself when `msg.WoundDiagnostics == null && msg.Organs == null` (§2.10) and null-gates
internally, so nothing else is needed — or keep it last and add `WolfmedPanel.Visible = false;` inside the
early-return block, making HOOK 26 four marked lines instead of three.

### M9 — nothing tests HOOK 22; the explosion feature can ship dormant with every test green

T-EXPLOSION-PLATE (§6.2) calls `routing.TryRouteDistributedDamage(body, dmg, TargetBodyPart.All,
SplitWithVariation, variation: 0f, isExplosion: true, originFlag: DamageOriginFlag.Explosion)` **directly**,
"routing-API level, same style as phase 3's T-AMP-EXPLOSION — no live grenade". That exercises §2.11's
`originFlag` passthrough and the plate gate, which is the stated purpose, but it never touches
`ExplosionSystem.Processing.cs:471`. An agent that lands `WolfmedExplosionSystem.cs` and the vendored-file
edits but forgets the three-line HOOK 22 produces a green build, a green lint, a green suite — and a dormant
mechanism. That is precisely the P3-D3 shape the package exists to fix (§8.5 and P4-D14 both describe
"ported and unreachable" as the phase-3 failure mode).

The call site is verified present and unambiguous:

```
Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs:470-473
                // TODO EXPLOSIONS turn explosions into entities, and pass the the entity in as the damage origin.
                _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
                // Mono: Explosion flag for plate protection
                originFlag: DamageableSystem.DamageOriginFlag.Explosion);
```

**Fix.** Add two assertions to `WolfmedExplosionTest`: `entities.System<WolfmedExplosionSystem>()
.TryApplyExplosionDamage(host, dmg)` returns **`true`** and spreads damage across ≥2 attached parts; the same
call on a non-host returns **`false`** (the D2 fall-through). That pins the wrapper's contract; pair it with a
one-line grep assertion in the WP12-8 checklist that `ExplosionSystem.Processing.cs` contains
`_wolfmedExplosion.TryApplyExplosionDamage`.

---

## 3. Minors

* **m1 — PROTO F line numbers are wrong.** §3.2 cites `- type: SurgeryWoundedCondition` on
  `SurgeryTendWoundsBrute` at `:285` and `SurgeryTendWoundsBurn` at `:298`. Actual:
  `grep -n "SurgeryWoundedCondition" Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` → **`294`**
  and **`307`** (a consistent +9 drift). PROTO G's four numbers are exact, and HOOK 24/25's `:127`/`:263` are
  within a line of `OnWoundedValid` (`:120`) and `OnPartRemovedConditionValid`'s `CanAttachToSlot` block
  (`:257-262`), so the drift is isolated to PROTO F.
* **m2 — there are 10 `SurgeryAttach*` surgeries, not 11.** P4-D18 and §3.1 HOOK 25 both say "all 11".
  `grep -c "id: SurgeryAttach" Resources/Prototypes` → **10** (Head, LeftArm, RightArm, LeftLeg, RightLeg,
  Hands, LeftHand, RightHand, LeftFoot, RightFoot), matching the 10 `SurgeryPartRemovedCondition` uses.
* **m3 — `medical_patch.rsi` has 24 files, not 18.** WP12-0 item 5 says "(18 files)". `git ls-tree` on the pin
  returns 23 PNGs + `meta.json`. The licence/copyright note (`CC-BY-SA-3.0`, `@jorgun`) is correct and
  correctly flagged as new to Wolfmed.
* **m4 — PROTO L's line citation.** §3.2 cites `Guidebook/medical.yml:5-9` for the `Medical` entry's
  `children:` list; the list is `:6-12`, with `MedicalDoctor` at `:7` and `Chemist` at `:8`. The two new ids
  are free (`grep "^  id: Wounds$\|^  id: WoundTreatment$"` → 0 hits) and `id: Surgery` is genuinely taken
  (`:60`), so P4-D15's reasoning holds.
* **m5 — `MedicalPatchSystem` is not verified "verbatim, 0 edits" for two hands APIs.** Onyx's
  `OnUnstuck` runs `_hands.IsHolding(args.User, patch, out var hand)` then `_hands.TryPickup(args.User, used,
  hand)`. The out-parameter type differs between trees:
  * ONYX `Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:438` — `IsHolding(Entity<HandsComponent?>
    ent, EntityUid? entity, out string? inHand)`
  * WG `Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:285` — `IsHolding(EntityUid uid, EntityUid?
    entity, out Hand? inHand, HandsComponent? handsComp = null)`

  It still compiles — `Hand` is a class (`Content.Shared/Hands/Components/HandsComponent.cs:107`), so `Hand?`
  binds to WG's `TryPickup(EntityUid, EntityUid, Hand hand, …)` overload
  (`SharedHandsSystem.Pickup.cs:91`) with a CS8604 possible-null warning rather than an error, and the
  behaviour is equivalent. But neither `tools.md` §2.1's "full symbol table" nor PLAN4 §2.3's "every
  dependency is vanilla and present" list mentions `SharedHandsSystem.IsHolding`/`TryPickup` at all
  (`grep -n "IsHolding\|TryPickup" tools.md` → 0 hits). Add both to §2.3's symbol list marked **DIFFERENT
  (compatible)** so WP12-0's agent is not surprised by a warning in a file promised as byte-identical.
  Everything else in that file checks out: `EntityStuckEvent(EntityUid Target, EntityUid User)` /
  `EntityUnstuckEvent` (`Content.Shared/Sticky/EntityStuckEvent.cs:20,26`),
  `StickySystem.UnstickFromEntity(Entity<StickyComponent>, EntityUid)` (`:182`),
  `TryGetInjectableSolution` (`SharedSolutionContainerSystem.Capabilities.cs:71`), static `ToPrettyString(Solution)`
  (`:152`), `UnremoveableComponent`, `ReactionMethod.Injection`, `LogType.ForceFeed`.
* **m6 — WP12-2's `!type:` table omits two tags used by a Tier-A reagent.** Ibuprofen carries a top-level
  `plantMetabolism:` block with `!type:PlantAdjustWeeds { amount: -5 }` and `!type:PlantAdjustHealth
  { amount: -10 }` (ONYX `_Onyx/Reagents/Medicine/medicine.yml`). Both are **SAME** in WG
  (`Content.Server/EntityEffects/Effects/PlantMetabolism/PlantAdjustWeeds.cs:7`, `PlantAdjustHealth.cs:6`) and
  `ReagentPrototype` has the field (`:163-164`, `[DataField("plantMetabolism", serverOnly: true)]`), so
  nothing breaks — but the table is billed as the checklist for every tag in a copied block. Add both as SAME.
* **m7 — the re-attachment block gives the medic no feedback, and is coarser than it reads.** HOOK 25 keys on
  the **parent** part (`WolfmedStumpBlocksAttachment(args.Part)`), and `ApplyAmputationConsequences` puts the
  wound on the parent (`Content.Shared/_Onyx/Wounds/AmputationSystem.cs:59-70`, severity from
  `WolfmedBodyPartComponent.AmputationConsequenceSeverity`). In WG's human graph every arm, leg and the head
  attach to the torso (`Resources/Prototypes/Body/Prototypes/human.yml:5-27`), so **one** untreated stump
  hides **all ten** `SurgeryAttach*` surgeries on the torso until it is treated. This matches Onyx
  (`graph.HasAmputationConsequence(torso)`, ONYX `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs:104,133,138`),
  so it is not a defect — but §8.7 drops `StepInvalidReason.AmputationConsequence` and its popup ("the surgery
  is hidden, not greyed"), so the surgeries simply vanish with no explanation. P4-D15 already ships the
  amputation-consequence guidebook paragraph; make sure it says *all* attachments to that part are blocked,
  and add a line to `WOLFMED_STATUS.md`.
* **m8 — §8.2's durations depend on B1's resolution.** The `SurgeryTendWounds*Deep` row ("incision (6) +
  repeats + seal") and §8.3's "the incision now bleeds" bullet are written assuming the careful incision
  bleeds. If B1 is fixed by option 1, both need a one-line correction.

---

## 4. What I checked and could **not** break

Recording these so the coverage of this critique is legible, and so the user knows which of the plan's
load-bearing claims survived an independent pass.

**Entity-effect `!type:` name collisions — clean.** `grep -rn "class X\b"` over `Content.{Shared,Server,Client}`
for `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition` → **0 class declarations
each**. The only `SuppressPain` symbol in the tree is a method, `Content.Shared/_Onyx/Wounds/PainSystem.cs:340
public bool SuppressPain(Entity<PainComponent?> entity, string identifier, FixedPoint2 amount, …)`, which a
same-named class does not shadow at the member-access site the plan uses. `TakeStaminaDamage`'s 17 hits are
all the `StaminaSystem` method. I also enumerated **all 65** `class X : EntityEffect` declarations and all
`class X : EntityEffectCondition` declarations and found **no duplicate bare names anywhere in the tree** —
so the plan's claim that `!type:` resolution is unambiguous today is verified, not assumed.

**Component registration names — clean.** All 12 new names (`MedicalPatch`, `Tourniquet`, and the ten
`WolfmedSurgery*`) → 0 hits for `class <Name>Component`. The two names P4-D17 avoids are genuinely taken:
`SurgeryWoundedConditionComponent` (`Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs:7`,
`[RegisterComponent, NetworkedComponent] public sealed partial class SurgeryWoundedConditionComponent : Component;`)
and `SurgeryTendWoundsEffectComponent`. The 17 new non-component type names → 0 hits each.

**The `Tourniquet` prototype collision is real and PROTO D's in-place swap is safe.**
`Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml:266-296` declares `id: Tourniquet`,
`parent: BaseHealingItem`, with `- type: Healing` carrying `groups: Brute: 5`, `types: Asphyxiation: 5`,
`bloodlossModifier: -10`, `delay: 0.5` and the two `brutepack_{begin,end}.ogg` paths — matching the plan
exactly. Critically, **`BaseHealingItem` (`:1-17`) carries no `- type: Healing` block** (only `Sprite`,
`Item`, `StaticPrice`, `ItemTax`), so removing the child's block genuinely removes the component; there is no
inherited-component trap and no dual-system `UseInHandEvent` race. Onyx's own edit is byte-comparable
(`ONYX healing.yml:262-291`, `<Onyx-TargetedTourniquet-edited>`, `damage: types: Blunt: 5 / Asphyxiation: 5`)
— so the plan's `types: { Blunt: 5, Asphyxiation: 5 }` is Onyx's number, not a conversion. P4-D9's tag call is
also right: `grep -rn "id: Tourniquet" Resources/Prototypes` → only the entity and two fill references; Onyx
declares the tag at its own `tags.yml:1460` as a `ClothingBeltMedical` storage whitelist entry, which WG has
no equivalent of.

**Every subscription-pair claim in §5 — verified.** All 20 new pairs are on brand-new components. Every row in
§5.2's "must not register" table is a real existing registration: `SharedSurgerySystem.cs:67`
(`<SurgeryWoundedConditionComponent, SurgeryValidEvent>`) and `:68`
(`<SurgeryPartRemovedConditionComponent, SurgeryValidEvent>`) are exactly where HOOK 24 and HOOK 25 extend
handler bodies rather than re-subscribing; `SubSurgery<SurgeryTendWoundsEffectComponent>` is at
`SharedSurgerySystem.Steps.cs:49` and registers both step events at once via the helper at `:67-72`, which is
why §5.1's "do not use `SubSurgery<T>`" note is correct.

**The shared/server split of the two surgery step events is correct — and Shitmed's own comment is wrong.**
`SharedSurgerySystem.Steps.cs:47` claims "Check DOES only run on the server side". It does not:
`Content.Client/_Shitmed/Medical/Surgery/SurgeryBui.cs:281` calls `_system.GetNextStep(...)`, which calls
`IsStepComplete` (`Steps.cs:831`), which raises `SurgeryStepCompleteCheckEvent`. So §2.5's requirement that
every S4–S8 handler be shared (and §8.5 trap 10) is right and the stale upstream comment is a trap for the
implementer. Worth quoting in WP12-4's brief. All seven components those handlers read are networked:
`WoundComponent` (`WoundDamageComponents.cs:180-181`), `WoundableComponent` (`:155-156`),
`WoundBleedingComponent` (`:209-210`), `WoundInternalBleedingComponent` (`:235-236`), `WoundFractureComponent`
(`:245-246`), `WoundScarComponent` (`:258-259`) and `WolfmedOrganComponent`
(`Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs:9-10`), every one
`[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]`. `Cancelled = true` means "not
complete" (`OnToolCheck`, `Steps.cs:186-196`). Event shapes carry `Body` and `Part` as the handlers assume:
`SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false, …)`,
`SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, EntityUid Surgery, bool Cancelled = false)`,
`SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools, EntityUid Surgery)`.

**NetSerializable wire change — safe.** `HealthAnalyzerScannedUserMessage` has exactly **two** construction
sites (`Content.Server/Medical/HealthAnalyzerSystem.cs:276` and `Content.Server/Medical/CryoPodSystem.cs:209`),
both positional, so four trailing optional parameters keep CryoPod compiling as §3.4 claims. `bloodstream` is
genuinely in scope at the send site (declared by `TryComp<BloodstreamComponent>(target, out var bloodstream)`
at `:256` inside an `if` condition, so block-scoped). `LocId` is `[Serializable, NetSerializable]`
(`RobustToolbox/Robust.Shared/Localization/LocId.cs:14-15`) and `[NetSerializable] readonly record struct` has
20+ precedents in `Content.Shared`. `HealthAnalyzerSystem` really is `public sealed partial class` (`:31`), so
the `_WF` partial is legal, and `_bodySystem`/`_solutionContainerSystem` are private fields of that class.

**Client sandbox — clean.** `EllipsisLabel`'s three risky APIs are all whitelisted:
`RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml:896` and `:1326` (`Rune: { All: True }`), `:994`
(`StringRuneEnumerator: { All: True }`), `:1509` (`"System.Text.StringRuneEnumerator EnumerateRunes()"`).

**XAML names — clean.** `FancyWindow.xaml` reserves `WindowTitle` (`:12`), `HelpButton` (`:14`), `CloseButton`
(`:15`) and `ContentsContainer` (`:20`); `HealthAnalyzerWindow.xaml` already declares 27 names (`RootContainer`
at `:17` — so HOOK 26's insertion target exists — through `GroupsContainer` at `:301`). `WolfmedPanel` collides
with none of them, and P4-D25's whole point (every other new name lives in the panel's own
`[GenerateTypedNameReferences]` scope) holds. **One gap:** §2.10 never states `WolfmedDiagnosticPanel`'s base
class; if it is ever made a `DefaultWindow` descendant rather than a `BoxContainer`/`Control`, the project's
reserved-name rule (`CloseButton`/`ContentsContainer`/`TitleLabel`/`WindowHeader`) applies to its seven new
names. Specify the base class in WP12-7's brief.

**`EvenHealthChange.cs` really lacks `using System.Linq;` — and adding it is safe.** The file's usings end at
`:8` with no Linq, confirmed against `Content.Server/GlobalUsings.cs` (System, System.Collections.Generic and
seven Robust namespaces; no Linq). The existing `groupDamage.Values.Sum()` at `:112` resolves to the
FixedPoint2 extension `Content.Shared/FixedPoint/FixedPoint2.cs:313 public static FixedPoint2 Sum(this
IEnumerable<FixedPoint2> source)`, which `System.Linq.Enumerable.Sum` has no applicable overload for — so
HOOK 9(b)'s new `using` creates **no** ambiguity at `:112`. `HealthChange.cs`'s call site is at `:167-176`
with the `partMultiplier: 1.00f, // Mono, 0.5f->1.00f` comment intact, exactly as §3.1 describes, and
`System.Linq` is already imported there.

**Explosion path — the plan's mechanism holds under scrutiny.** CVar names are exact
(`Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs:22-26`: `ExplosionLimbDamageVariation` = `explosion.damage_variation`
2f, `ExplosionWoundMultiplier` = `explosion.wounding_multiplier` 4f; `CCVars.Surgery.cs:7-8`:
`SurgeryScarChance` = `surgery.scar_chance` 0.35f). Explosion damage types are all localized —
`Resources/Prototypes/explosion.yml:3-10` gives `Default` `Heat: 5, Blunt: 5, Piercing: 5, Structural: 20`,
and `WoundHostComponent.LocalizedDamageTypes` (`WoundDamageComponents.cs:34-43`) is
`[Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic]` — so the split actually splits and `Structural` falls
to the systemic branch. I specifically probed the fall-through for a double-application bug and it is **not**
there: `TryRouteDistributedDamage` (`WoundDamageRoutingSystem.cs:903-928`) returns **`true`
unconditionally** once its four guards pass, discarding `TryApplyDistributedDamage`'s
"did-anything-land" boolean — so HOOK 22's `if (!TryApplyExplosionDamage(...)) { existing call }` has correct
"I handled it" semantics even when armour absorbs everything. The `_routedModifiers` save/restore is also
sound: it is written at `:79` and removed at `:105` only inside `OnBeforeDamageChanged`, which early-returns
on `_routing.Contains(ent)` before its own `try`, so a value pre-seeded by the distributed path survives to
the read at `:686-688` and `:747`. The `before: [typeof(SharedArmorPlateSystem)]` orderings at `:65-67` are
exactly where §5.4 says.

**Reagent groundwork — clean apart from B2/M5.** All five new reagent ids free
(`grep "^  id: X$" Resources/Prototypes` → 0 for Osteogen, Ibuprofen, Ketorolac, Tramadol, Oxycodone, plus
Probital, Mitogen and Heroin). Group headers for the three named PROTOs verified in the tree:
`Stasizium` → `group: Medicine` with a `Medicine:` metabolism block (`_Goobstation/Reagents/medicine.yml:1-41`,
and its existing effect list matches P4-D3's side-by-side to the letter — `ModifyBloodLevel 10`,
`ModifyBleedAmount -2`, `ReduceRotting 30` gated on Dead, the five-group `-20`, a `Blunt: 100` overdose at
`ReagentThreshold min: 21`, and the sub-263.15 K `AdjustTemperature -50000`); `Bicaridine` → `Medicine:`
(`Reagents/medicine.yml:145-153`); `Desoxyephedrine` → `Narcotic:` block present and distinct from its
`Poison:` block (`Reagents/narcotics.yml:1-45`), confirming PROTO J's warning; `Happiness` → `Narcotic:`
(`:565-575`). Tramadol's and Oxycodone's `GenericStatusEffect { key: Adrenaline, component:
IgnoreSlowOnDamage }` resolves in WG — `Resources/Prototypes/status_effects.yml:74 id: Adrenaline`,
`Content.Shared/Damage/Components/IgnoreSlowOnDamageComponent.cs:9`, and three existing WG reagents already
use `key: Adrenaline`. All reaction precursors present (listed in M2).

**Medical patch content — clean.** `StickyVisualizerComponent`, `MixableSolutionComponent`,
`ExaminableSolutionComponent`, `PhysicalCompositionComponent` all exist; the two construction graphs' material
ids resolve (`Cloth` and `WebSilk` both have stack prototypes at `Resources/Prototypes/Stacks/Materials/materials.yml:23,72`);
the `SilkPatchMakeshift` recipe's `entityWhitelist: tags: [SpiderCraft]` resolves (`Resources/Prototypes/tags.yml:1184`);
`construction-category-tools` exists (`Resources/Locale/en-US/construction/construction-categories.ftl:4`);
and the `MedicalPatch` tag id is free (0 hits).

**Surgery content references — clean.** `SurgeryBase`, `SurgeryOpenIncision`, `SurgeryOpenRibcage`,
`SurgeryStepSealTendWound`, `SurgeryStepRepairBruteTissue`, `SurgeryStepRepairBurnTissue`,
`SurgeryStepSealOrganWound`, `SurgeryTendWoundsBrute`, `SurgeryTendWoundsBurn` → 1 hit each.
`SurgeryPartConditionComponent`, `BoneSetterComponent`, `BoneGelComponent`, `HemostatComponent`,
`TendingComponent`, `SurgeryRepeatableStepComponent` all exist. Organ slot ids match P4-D24's seven exactly
(`human.yml:11-27`: head → `brain`, `eyes`; torso → `heart`, `lungs`, `stomach`, `liver`, `kidneys`).

**The two phase-3 gap closures are correctly targeted.** `WoundHostComponent.AmputationConsequenceWound` is a
real datafield (`WoundDamageComponents.cs:67`, default `"AmputationConsequenceWound"`), the prototype exists
with `damageTypes: {}` and `mergeMode: SeparateInstances` (`wounds.yml:397-401`) so P4-D24's "surgery is the
only cure" is literally true, and it is created **only** on the traumatic path —
`ApplyAmputationConsequences` has exactly one internal caller, `TryAmputate` (`AmputationSystem.cs:130`), so
T-SURG-AMP-CLEAN's premise (a surgically removed limb still re-attaches) is correct. `OrganHealthSystem.SetHealth`
clamps to `[0, MaxHealth]` and raises `OrganFunctionChangedEvent` on a zero crossing exactly as P4-D24 describes
(`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:64-80`), and `ChangeHealth` is the one-line wrapper
the plan's V4 handler calls. The `WoundFractureSystem` surface is as §2.0 lists (`GetFracture:88` returning
`Entity<WoundComponent, WoundFractureComponent>?`, `TryReduce:99`, `TryMend:100`, `TrySetTreatment:111`,
`GetGrade:128`), `WoundBleedingSystem.TreatPart:257` does return `int` (§2.0's correction to `surgery.md` is
right), and `BleedingTreatment` really has `Cauterized` (`WoundDamageComponents.cs:272-279`).

---

## 5. Recommended ordering of the fixes

1. **B2** and **M5** before WP12-2 starts — both are one table row and one sentence, and WP12-2 is otherwise
   ten files of YAML with no way to notice.
2. **B1** and **M3** before WP12-5 starts — decide option 1 or 2, then correct §2.7, P4-D21, PROTO G, §7.3
   deviation 9, §8.2 and §8.3 together, and extend `T-SURGERY-PROTOTYPE-SANITY`.
3. **M1** before WP12-6 (the message file must be authorised or WP12-6 stalls on ground rule 3) and **M2**
   before WP12-2.
4. **M6**, **M7** with WP12-5's content brief; **M8** with WP12-7's; **M9** and **M4** with WP12-9's.
5. Minors m1–m4 and m6 are documentation corrections for WP12-10's reconcile; m5 belongs in §2.3 before
   WP12-0; m7 belongs in the guidebook text and `WOLFMED_STATUS.md`.

None of these changes the phase's shape, the work-package order, the model assignments, or any decision in
`DECISIONS.md`. **The §8.4 user decisions are unaffected**, except that decision 3 (surgery scarring, default
"ship all three effects") should be re-presented with B1's option 1 as the shape being shipped — "the incision
that bleeds is the `SurgeryOpenIncision` one only; tend-wounds keeps its clean careful incision" — because as
currently written that decision approves a mechanic that leaks a bleeding wound on the commonest surgery in
the game.
