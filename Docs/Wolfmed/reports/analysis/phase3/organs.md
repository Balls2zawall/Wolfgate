# WOLFMED PHASE 3 — analyst report: **organs** (P3-2, organ-damage consequences)

Scope: DECISIONS.md §"Phase 3" item **P3-2** — "what Onyx does when organs take damage
(`OrganDamageSystem` caps, `OrganConsequenceComponents`, `FunctionalOrganComponent`, server
`OrganEffectSystem` if it is wound-driven) mapped onto Wolfgate's organs … Only pieces with a consumer at
the pin."

Read-only pass. Every claim below is cited `file:line` against the real files.
`WG` = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`,
`ONYX` = `C:/tmp/onyx` @ `2f5bab9`.

---

## 0. Verdict in one paragraph

The organ half of the wound port is **completely inert today**: `OrganDamageSystem`'s organ branch and the
whole of `OrganHealthSystem` are live, compiled, subscribed — and match **zero entities**, because *no
prototype in Wolfgate carries `WolfmedOrgan` or `OrganDamage`* (verified: `grep -rn "type: OrganDamage\|type:
WolfmedOrgan" WG/Resources/Prototypes` → **zero hits**). Switching organ damage on is therefore almost
entirely a **prototype** job, not a C# job. On the consequence side the news is better than the plan
assumed: Onyx's organ-consequence layer (`OrganConsequenceComponents.cs`, `OrganEffectSystem.cs`,
`MissingHeartComponent`, `BodyStasis.cs`) is **Nubody anatomy bookkeeping that Shitmed already implements a
different way** — Onyx destroys an organ at 0 health and its consequences then flow from *organ removal*,
which in Wolfgate already produces heart→`DelayedDeath`, brain→`Debrained`, eyes→`TemporaryBlindness`,
lungs→suffocation. **7 of Onyx's 9 organ-consequence pieces should be skipped, 1 ported verbatim
(nothing — see §4), and the rest mapped onto Shitmed.** The two genuine gaps are (a) **there is no way to
heal organ damage in Wolfgate at all** (Onyx's only healer is its own surgery, excluded by D7) and (b) at
Onyx's own numbers organ destruction is a **~170–400-hit** event and will essentially never be seen — both
are user decisions, §8.

---

## 1. `WG/Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` — what it does today

Ported file, 115 lines, namespace `Content.Shared._Onyx.Wounds`, **located in `Content.Server`** (D13).
Manifest row: `WOLFMED_MANIFEST.md:85`.

### 1.1 What it does

| Step | Lines | Behaviour |
|---|---|---|
| Single subscription | `:31` | `SubscribeLocalEvent<WoundableComponent, PartDamageAppliedEvent>(OnPartDamageApplied)` — **the only subscriber of that pair in the whole tree** (§7). It is the fan-out dispatcher for the wound pipeline. |
| Dispatch, fixed order | `:36`,`:37`,`:40` | `_wounds.HandlePartDamageApplied` → `_fractures.HandlePartDamageApplied` → `_bleeding.HandlePartDamageApplied`. Each of those is a plain public/internal method, **not** its own subscription (`WoundSystem.cs:66`, `WoundFractureSystem.cs:27`, `WoundBleedingSystem.cs:89`). |
| Amputation slot | `:38-39` | `// WOLFGATE: D26, amputation is phase 3.` + `// TODO: phase 3 - _amputation.HandlePartDamageApplied(part, ref args);` — **commented out**, matching the disabled `[Dependency]` at `:25-26`. |
| Gate | `:42-44` | Server only; part must have `BodyPartComponent` with a non-null `Body`; the part's `WoundableComponent.Profile` must resolve to a `bodyPartProfile`. |
| Part-type roll | `:46-49` | `profile.OrganDamage.Chances[bodyPart.PartType]`, `_random.Prob(clamp01(chance))`. Data lives in `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:27-36`. |
| Candidate set | `:52-59` | `_body.GetPartOrgans(part)` (`SharedBodySystem.Parts.cs:825`) filtered to organs that have **both** `WolfmedOrganComponent` with `Health > 0` **and** `OrganDamageComponent`. |
| Weighted pick, `MaxAffected` times | `:61-79` | `PickOrgan` (`:95-113`) draws without replacement by `OrganDamageComponent.SelectionWeight`; then `HitChance` roll; then `GetOrganDamage`; then the `MaxDamageFraction` cap; then `_organHealth.ChangeHealth(organ, -applied)`. |
| Damage formula | `:82-92` | `Σ over DamageDict of amount × max(0, DamageMultipliers[type])`. **WP10-2 note:** `damage.DamageDict` is keyed by `string` in Wolfgate while `DamageMultipliers` is `Dictionary<ProtoId<DamageTypePrototype>, float>`; this compiles only because RT defines `implicit operator ProtoId<T>(string)`. It works, but it is a silent trap for anyone who "fixes" the dictionary type. |
| Cap | `:75-76` | `applied = min(applied, MaxHealth × MaxDamageFraction)`. `MaxDamageFraction` defaults to **0.3** (`OrganDamageComponent.cs:22`) and **no Onyx prototype overrides it** → a hard cap of `0.3 × 15 = 4.5` organ HP per damage application. |

### 1.2 Divergences from ONYX `Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs` (107 lines)

Onyx's file is **shared**, and health lives on Onyx's own `OrganComponent` (`ONYX
Content.Shared/Body/OrganComponent.cs:19-24`). The port is byte-for-byte identical except:

| Onyx | WG | Why |
|---|---|---|
| `:2 using Content.Shared.Body;` | `:2-3 using Content.Shared.Body.Organ;` + `Content.Shared._WF.Wolfmed.Body;` | D8 — Wolfgate keeps Shitmed's `OrganComponent` in a different namespace and has no health on it |
| `:24 [Dependency] AmputationSystem _amputation` | `:25-26` commented | D26 → **P3-1 re-enables this** |
| `:36 _amputation.HandlePartDamageApplied(...)` | `:38-39` commented | same |
| `:48-50` filter on `organ.Component.Health` | `:52-57` re-projects onto `WolfmedOrganComponent` | D8 |
| `:87-88 PickOrgan(List<(EntityUid, OrganComponent)>)` | `:95-96 PickOrgan(List<(EntityUid, WolfmedOrganComponent)>)` | D8 |

Behaviourally equivalent. **No further change is needed in this file for P3-2.**
**Sequencing hazard:** P3-1 (amputation) and P3-2 both want to touch `:25-26` / `:38-39` — but only P3-1
does. P3-2 must not re-edit those lines; sequential packages in one worktree mean the second package would
otherwise clobber the first.

### 1.3 What it **cannot** do today (missing consumers)

1. **Nothing is ever damaged.** `grep -rn "type: OrganDamage\|type: WolfmedOrgan" WG/Resources/Prototypes`
   → **zero hits**. The manifest states this explicitly: *"No prototype carries it until WP11"*
   (`WOLFMED_MANIFEST.md:91`). `:58-59` returns on `organs.Count == 0` for every hit in the game today.
2. **No amputation consequence.** `:38-39` — the `PartDamageOverflowedEvent` / finishing-hit path does not
   run (P3-1).
3. **`OrganHealthSystem` matches nothing.** `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:39`
   queries `EntityQueryEnumerator<WolfmedOrganComponent, OrganComponent>()` — empty set, every tick.
4. **`OrganFunctionChangedEvent` has zero subscribers.**
   `grep -rn "OrganFunctionChangedEvent" --include=*.cs WG` → only the declaration
   (`OrganHealthSystem.cs:21`) and the raise (`:71-72`). Onyx's only consumer is
   `OrganEffectSystem.cs:57`, which is not ported.
5. **`WolfmedOrganComponent.DestructionWound` / `DestructionWoundSeverity` are unreachable data**
   (`WolfmedOrganComponent.cs:19-22`) — nothing sets them, so `OrganHealthSystem.cs:85-88` never creates an
   internal-bleeding wound.
6. **No healing path whatsoever.** Onyx's *only* caller of `OrganHealthSystem.ChangeHealth` with a positive
   amount is `ONYX Content.Shared/_Onyx/Medical/Surgery/SharedSurgerySystem.Organs.cs:107`
   (`SurgeryOrganHealEffectComponent`, `amount: 1` per step, `ONYX
   Resources/Prototypes/_Onyx/Entities/Surgery/surgery_steps.yml:862-940`). **D7 excludes Onyx surgery.**
   There is no passive organ regen, no chem, no `PassiveDamage` equivalent — grepped
   (`git grep -n "_organHealth\." HEAD` in ONYX returns exactly two call sites: the surgery one and
   `OrganDamageSystem.cs:71`).
7. **No player-visible organ readout.** Onyx's health-analyzer organ list
   (`ONYX Content.Shared/_Onyx/Medical/HealthAnalyzerOrganInfo.cs`, `ONYX
   Content.Server/Medical/HealthAnalyzerSystem.cs:443-470`, `ONYX
   Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs:446-460`) is **not ported**
   (`grep -rn "HealthAnalyzerOrganInfo\|HealthAnalyzerWoundDiagnostic" --include=*.cs WG` → zero hits).
   Phase-2's part-status examine (`_Onyx/HealthExaminable`) shows *part* status, never organs.

---

## 2. ONYX source survey

### 2.1 `Content.Shared/Body/OrganComponent.cs` (Nubody) vs `WG/Content.Shared/Body/Organ/OrganComponent.cs` (Shitmed)

| Field | ONYX (Nubody, 49 lines) | WG (Shitmed, 78 lines) | Verdict |
|---|---|---|---|
| `Health` | `:19-20 [DataField, AutoNetworkedField] public FixedPoint2 Health = FixedPoint2.New(15);` | **absent** | **MISSING** → lives on `WolfmedOrganComponent.Health` (`:13`), same default `FixedPoint2.New(15)` |
| `MaxHealth` | `:22-23 … MaxHealth = FixedPoint2.New(15);` | **absent** | **MISSING** → `WolfmedOrganComponent.MaxHealth` (`:16`), same default |
| `DestructionWound` | `:30-31 public ProtoId<_Onyx.Wounds.WoundPrototype>? DestructionWound;` | absent | **MISSING** → `WolfmedOrganComponent.DestructionWound` (`:19`) |
| `DestructionWoundSeverity` | `:33-34 public FixedPoint2 DestructionWoundSeverity;` | absent | **MISSING** → `WolfmedOrganComponent.DestructionWoundSeverity` (`:22`) |
| `Body` | `:40-41 public EntityUid? Body;` | `:17-18` same | **SAME** |
| `Category` | `:46-47 public ProtoId<OrganCategoryPrototype>? Category;` | **absent**; Shitmed uses `:33-34 public string SlotId = "";` | **DIFFERENT** — `OrganCategoryPrototype` does not exist in WG (`grep -rn "OrganCategoryPrototype" --include=*.cs WG` → **zero hits**). Every Onyx "missing organ X" check is keyed on `Category`; WG's nearest key is the body-graph slot id. |
| `Enabled` / `CanEnable` | absent | `:63-64`, `:69-70` | WG-only — Shitmed's organ-condition switch (§3.3) |
| `OnAdd` / `OnRemove` | absent | `:51-52`, `:57-58` (`ComponentRegistry?`) | WG-only — **Shitmed's exact equivalent of Onyx `FunctionalOrganComponent.Components`** |
| `Removable`, `Used`, `ToolName`, `Speed`, `OriginalBody` | absent | `:25,:37,:40,:46,:76` | WG-only, Shitmed surgery plumbing |

**No Onyx organ prototype overrides `health`/`maxHealth`** — `git grep -n "maxHealth" HEAD -- Resources/Prototypes/Body Resources/Prototypes/_Onyx/Body Resources/Prototypes/Entities/Mobs` → zero hits. Every organ in
Onyx is 15/15.

### 2.2 `Content.Shared/_Onyx/Body/OrganDamageComponent.cs`

**Ported verbatim** (WP6). ONYX `:1-23` and `WG Content.Shared/_Onyx/Body/OrganDamageComponent.cs:1-23`
are byte-identical (diffed). Registers as `OrganDamage`. Fields: `HitChance = 1f`, `SelectionWeight = 1f`,
`DamageMultipliers`, `MaxDamageFraction = 0.3f`.

### 2.3 `Content.Shared/_Onyx/Body/FunctionalOrganComponent.cs` (19 lines)

```csharp
// ONYX :8-13
[RegisterComponent]
public sealed partial class FunctionalOrganComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();
}
// ONYX :17-19
[ByRefEvent]
public readonly record struct OrganFunctionChangedEvent(EntityUid Body, bool Functional);
```

Status in WG: **skipped** (`WOLFMED_MANIFEST.md:90`), with only the event relocated into
`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:20-21` inside `namespace Content.Shared._Onyx.Body`.

**Consumers at the pin (grepped):** exactly one — `ONYX Content.Server/_Onyx/Body/OrganEffectSystem.cs:165`
(`TryComp(organId, out FunctionalOrganComponent? functional)`), gated on `organ.Health > 0` at `:161`.
**Prototype consumers:** exactly two files —
`ONYX Resources/Prototypes/_Onyx/Body/Organs/dubious.yml` (exotic surgical implants: `OrganDubiousHealth`
grants `PassiveDamage`, `OrganDubiousAA` grants `Access`/`AccessReader`, `OrganDubiousShock` grants
`Insulated` + `LightningArcShooter`, …) and
`ONYX Resources/Prototypes/_Onyx/Entities/Mobs/Customization/Parts/cybernetic.yml`. **No vanilla organ uses
it.**

### 2.4 `Content.Shared/_Onyx/Body/OrganConsequenceComponents.cs` (47 lines)

| Declaration | ONYX line | Who consumes it at the pin |
|---|---|---|
| `BodyOrgansChangedEvent(EntityUid Body)` | `:12-13` | **Nobody.** `git grep -n "BodyOrgansChangedEvent" HEAD` → the declaration and one raise (`OrganEffectSystem.cs:129`). **Zero subscribers.** |
| `BodyAnatomyComponent` (`RequiredParts`, `RequiredOrgans`, `AnatomyInitialized`) | `:15-26` | `OrganEffectSystem.cs:74,144,217,226`; `_Onyx/Body/Systems/BodyInventorySlotSystem.cs:28,57,68,72` (stand-up gate); populated by `_Onyx/Body/Systems/SharedBodySystem.cs:160-182`. **Requires `OrganCategoryPrototype`.** |
| `MissingEarsComponent` | `:29` | `OrganEffectSystem.cs:176` (set); `ONYX Content.Server/_Onyx/Chat/ChatSystem.Hearing.cs:9` (can't hear); `ONYX Content.Client/_Onyx/Medical/Surgery/OrganHearingSystem.cs:28` |
| `MissingEyesComponent` | `:32` | `OrganEffectSystem.cs:60,133,175` — cancels `CanSeeAttemptEvent` |
| `BreathingImmunityComponent` | `:38` | `ONYX Content.Server/Body/Systems/RespiratorSystem.cs:92`; `ONYX Content.Server/_Onyx/Swimming/OceanSwimmingStaminaSystem.cs:122` |
| `MissingHeadComponent` (+`Elapsed`) | `:41-44` | `OrganEffectSystem.cs:174,295-307` (timed death, 15 s / 300 s in stasis); `ONYX Content.Server/Body/Systems/RespiratorSystem.cs:140`; `ONYX Content.Shared/Speech/EntitySystems/VocalSystem.cs:56`; `ONYX Content.Shared/Speech/SpeechSystem.cs:31` |
| `InitiallyLungedComponent` | `:47` | `ONYX Content.Server/Body/Systems/RespiratorSystem.cs:103,108,248,277`; set by `_Onyx/Body/Systems/SharedBodySystem.cs:180-182` |

### 2.5 `Content.Shared/_Onyx/Body/TaggedOrganComponent.cs` + `TaggedOrganSystem.cs`

`TaggedOrganComponent` (`AddTags`/`RemoveTags`) + `OrganTagOwnershipComponent` (refcounted per-tag
ownership so two organs granting the same tag don't fight). `TaggedOrganSystem` subscribes
`<TaggedOrganComponent, OrganGotInsertedEvent>` / `<…, OrganGotRemovedEvent>` (`:14-15`).

**Consumers at the pin: NONE.** `git grep -n "TaggedOrgan" HEAD` returns **only those two files** — no
prototype anywhere in Onyx declares `- type: TaggedOrgan`. It is dead code at the pin. **Skip.**

### 2.6 `Content.Shared/_Onyx/Body/ProfileOrgansComponent.cs`

`Organs: Dictionary<ProtoId<OrganCategoryPrototype>, ProfileOrganData{Prototype, Parent, PresenceLayers}>`
plus marker `ProfileGeneratedOrganComponent`. Consumers: `ONYX
Content.Shared/Humanoid/Markings/MarkingManager.cs:281` and
`ONYX Content.Shared/_Onyx/Body/SharedVisualBodySystem.ProfileOrgans.cs:16,23,52,62,69-72`. This is
**character-profile-driven organ spawning and marking visuals** — Nubody body-generation, nothing to do with
wounds or damage. **Skip (D8).**

### 2.7 `Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs` (92 lines) vs the WG port (94 lines)

Ported, adapted (`WOLFMED_MANIFEST.md:89`). Differences, all already recorded:

| Onyx | WG | Note |
|---|---|---|
| `:27-28` query `OrganComponent` alone | `:39-40` query `<WolfmedOrganComponent, OrganComponent>` | D8 |
| `:33 HasComp<BrainComponent>` from `Content.Shared.Body.Components` | `:45` same call, `using Content.Server.Body.Components` (`:1`) | D13 — WG's `BrainComponent` is server-only |
| `:65-91 DestroyOrgan` walks `part.Organs` with `TryGetOrganInSlot`/`TryRemoveOrgan` | `:78-92` uses `SharedBodySystem.RemoveOrgan(organId, organ)` (`SharedBodySystem.Organs.cs:172`) | neither Onyx helper exists in WG; behaviour equivalent |
| `OrganFunctionChangedEvent` declared in `FunctionalOrganComponent.cs` | declared at `OrganHealthSystem.cs:20-21` | file not ported |

Semantics, unchanged: every tick, on the server, any organ at `Health <= 0` is either
(a) **a brain** → the body is killed via `_mobState.ChangeMobState(body, MobState.Dead, mobState, uid)` if it
is not already dead and *has* a Dead threshold (`:45-54`), and the brain **is never destroyed**; or
(b) **anything else** → removed from its containing part and `QueueDel`'d, leaving
`DestructionWound` at `DestructionWoundSeverity` on the parent part if both are set and the parent is
`Woundable` (`:78-92`).

`SetHealth` (`:60-73`) clamps, `Dirty`s, and raises `OrganFunctionChangedEvent` **only on a
`Health > 0` ⇄ `Health <= 0` transition**.

### 2.8 `Content.Server/_Onyx/Body/OrganEffectSystem.cs` (321 lines)

Not ported. It is one system doing **five unrelated jobs**:

1. **Eye-damage marshalling between body and eye organ** (`:63-71`, `:84-88`, `:91-99`, `:101-116`) — keeps
   `EyesComponent.Damage`/`MinDamage` on the organ in sync with the body's `BlindableComponent` so eye damage
   travels with a transplanted eye.
2. **Anatomy bookkeeping** (`:72-79`, `:143-144`, `:174-176`, `:184`, `:216-232`) — builds
   `BodyAnatomyComponent.RequiredParts`/`RequiredOrgans` lazily from the body's initial state, then
   `Missing*` = *current count < required count*.
3. **`FunctionalOrganComponent` grant/revoke** (`:157-172`, `:185-186`, `:244-281`) — a
   refcounted `ComponentRegistry` reconciler on the body, **filtered by `organ.Health <= 0` at `:161`**.
   *This is the only wound-driven part of the file.*
4. **Timed death** (`:295-319`) — `MissingHeadComponent` 15 s (300 s in stasis), `MissingHeartComponent`
   `NormalDuration` 30 s / `StasisDuration` 300 s (`:36-39`, `:211-212`).
5. **Vocal-cord / neuro-interface / surgical-mute glue** (`:47`, `:58`, `:121-125`, `:177-183`) — Onyx
   surgery, D7.

Only job 3 is keyed on organ health. Jobs 1, 2, 4, 5 are keyed on organ **presence**.

### 2.9 `Content.Shared/_Onyx/Medical/Surgery/MissingHeartComponent.cs` + `Content.Server/_Onyx/Body/BodyStasis.cs`

```csharp
// ONYX MissingHeartComponent.cs:5-10
[RegisterComponent, NetworkedComponent]
public sealed partial class MissingHeartComponent : Component
{
    [DataField] public float Progress;
    [DataField] public float NormalDuration;
    [DataField] public float StasisDuration;
}
```
Consumers: **only** `OrganEffectSystem.cs:203,207,210,309`. Nothing else in Onyx reads it.

```csharp
// ONYX BodyStasis.cs:16-27 — a static helper, not a system
public static bool IsActive(IEntityManager entMan, EntityUid body)
// true if buckled to an OperatingTableComponent, or to a powered StasisBedComponent
```
Consumers: **only** `OrganEffectSystem.cs:302,315` (the two timed-death loops).

**Both are needed only if `MissingHead`/`MissingHeart` are ported. They are not (§4). Answer to the
DECISIONS question: NO — neither is needed.**

---

## 3. Consequence trace: what happens per organ

### 3.1 In Onyx, when an organ takes wound damage

`PartDamageAppliedEvent` → `OrganDamageSystem` → `OrganHealthSystem.ChangeHealth(-x)` → **and then nothing
until health reaches 0**. Onyx has **no graded organ-damage effects** — an organ is fully functional at
1/15 HP and gone at 0/15. Everything below is the *destruction* consequence.

| Organ | Onyx `OrganDamage` policy (`ONYX Resources/Prototypes/Body/base_organs.yml`) | On `Health == 0` |
|---|---|---|
| **Brain** | `:481-491` hit 0.8, weight 0.75, `Blunt .115 / Slash .25 / Piercing .42 / Heat .15 / Cold .05 / Shock .3125`, no destruction wound (comment `:491`: "handled through mob state") | `OrganHealthSystem.cs:33-41` — mob → `MobState.Dead`. Organ **not** destroyed. |
| **Eyes** | `:537-547` hit 0.7, weight 0.2275, `Blunt .115 / Slash .3 / Piercing .4375 / Heat .15 / Cold .05 / Shock .25`, no destruction wound | organ destroyed → `OrganEffectSystem.cs:175` sets `MissingEyesComponent` → `:133-136` cancels `CanSeeAttemptEvent` = blind |
| **Lungs** | `:668-676` hit **1.0** (default), weight **1.38** (highest), `Blunt .1 / Slash .25 / Piercing .42 / Heat .165 / Cold .05 / Shock .25`; `:665-666` `destructionWound: InternalBleedingWound`, severity **35** | destroyed → no `LungComponent` → suffocation via the normal respirator path, **plus** internal bleeding sev 35 on the torso |
| **Heart** | `:721-730` hit 0.8, weight 0.64, `Blunt .1 / Slash .25 / Piercing .455 / Heat .15 / Cold .05 / Shock .3375`; `:718-719` destruction wound sev **45** | destroyed → `OrganEffectSystem.cs:184,199-214` sets `MissingHeartComponent` → death in 30 s (300 s in stasis); **plus** internal bleeding sev 45 |
| **Liver** | `:811-819` hit 1.0, weight **1.1**, `Blunt .1 / Slash .3 / Piercing .4375 / Heat .15 / Cold .05 / Shock .25`; `:808-809` sev **40** | destroyed → its `Metabolizer` (Metabolites stage) is gone; **no dedicated Missing\* consequence**; internal bleeding sev 40 |
| **Kidneys** | `:849-858` hit 0.9, weight 0.51, `Blunt .1 / Slash .25 / Piercing .4025 / Heat .15 / Cold .05 / Shock .25`; `:846-847` sev **30** | as liver — metabolizer loss + internal bleeding sev 30 |
| **Stomach** | `:758-767` hit 0.85, weight 0.56, `Blunt .1 / Slash .275 / Piercing .4025 / Heat .15 / Cold .05 / Shock .25`; `:755-756` sev **25** | as liver — digestion loss + internal bleeding sev 25 |
| **Tongue** | `:581-590` hit 0.5, weight 0.08, anchor `&organicOrganDamage` = `Blunt .1 / Slash .25 / Piercing .35 / Heat .15 / Cold .05 / Shock .25`, no destruction wound | destroyed → `OrganEffectSystem.cs:177` `TonguelessAccentComponent` |
| **Ears** | `:636-639` hit 0.45, weight 0.0875, `*organicOrganDamage`, no destruction wound | destroyed → `MissingEarsComponent` → deaf |
| **Appendix** | `:612-615` hit 0.35, weight 0.0375, `*organicOrganDamage`, no destruction wound | destroyed → nothing |

Part-type roll chance (`ONYX Resources/Prototypes/_Onyx/Wounds/wounds.yml:26-35`, `OrganicBodyPartProfile`):
`Head 0.05, Chest 0.04, Groin 0.04, Arm 0.02, Hand 0.01, Leg 0.02, Foot 0.01`, `maxAffected: 2`.
The Wolfgate port folds Chest+Groin into one `Torso: 0.04` (D9, `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:30-31`).

### 3.2 The key structural observation

**Every Onyx organ consequence except brain-death fires on organ *removal*, not on organ health.**
`OrganHealthSystem.DestroyOrgan` removes the organ from the part and deletes it; `OrganEffectSystem`'s
`OrganGotRemovedEvent` handler then recomputes `Missing*`. Health only decides *when* removal happens.

### 3.3 What Wolfgate already does on organ removal

`SharedBodySystem.Organs.cs:56-79 RemoveOrgan` runs on every container removal
(`SharedBodySystem.Parts.cs:274`, `SharedBodySystem.Body.cs:111`) and raises, in order:
`OrganRemovedEvent` → `OrganEnableChangedEvent(false)` (`:65-66`) → `OrganRemovedFromBodyEvent` (`:68-69`),
and `OnOrganEnableChanged` (`:273-289`) then raises `OrganComponentsModifyEvent(body, false)` (`:286`) and
`OrganDisabledEvent` for eyes (`:304-315`).

| Organ removed | Wolfgate consequence today | Evidence |
|---|---|---|
| **Brain** | `DebrainedComponent` on the body → `DelayedDeathComponent` (60 s to crit→dead), `StunnedComponent`, forced down, speech blocked, can't stand, defib refused | `Content.Server/Body/Systems/BrainSystem.cs:26,30-42`; `Content.Server/_Shitmed/Body/Systems/DebrainedSystem.cs:25-61`; `Content.Server/_Shitmed/DelayedDeath/DelayedDeathSystem.cs:38-63`; `DelayedDeathComponent.cs:10` `DeathTime = 60` |
| **Heart** | `DelayedDeathComponent` on the old body (60 s) | `Content.Server/_Shitmed/Body/Organ/HeartSystem.cs:17,20-27` |
| **Eyes** | `TemporaryBlindnessComponent` on the body + blindness transfer | `Content.Server/_Shitmed/Body/Systems/EyesSystem.cs:21,77-85` |
| **Lungs** | no `LungComponent` in `GetBodyOrganEntityComps<LungComponent>` → suffocation | `Content.Server/Body/Systems/RespiratorSystem.cs:122,152,209,295,311` |
| **Liver / kidneys / stomach** | their `Metabolizer` leaves the body | prototype-level (`Resources/Prototypes/Body/Organs/human.yml:239-244,261-266,284-287`) |
| **Any organ with `OnAdd`** | granted components removed from the body | `Content.Shared/_Shitmed/BodyEffects/OrganEffectSystem.cs:47-68` |
| **Ears** | **nothing** — `EarsComponent.cs:7` is literally `// TODO: Use this for deafening.` | — |

**This is the whole of Onyx's consequence set, already present, by a different route.**

---

## 4. Mapping table — port verbatim / map onto Shitmed / skip

| Onyx piece | Decision | Evidence / reasoning |
|---|---|---|
| `OrganDamageComponent.cs` | **already ported verbatim** (WP6) | byte-identical; needs only prototype data |
| `OrganHealthSystem.cs` | **already ported, adapted** (WP6); **one small addition** in P3-2 (§5.2) | it is the whole "organ takes damage → organ dies" engine |
| `OrganDamageSystem.cs` | **already ported**; no P3-2 change | §1.2 |
| `FunctionalOrganComponent` | **SKIP the component; map onto Shitmed `OrganComponent.OnAdd`** | `OrganComponent.cs:51-52` `public ComponentRegistry? OnAdd;` + `_Shitmed/BodyEffects/OrganEffectSystem.cs:53-59` is a 1:1 functional equivalent, already wired to enable/disable via `OrganComponentsModifyEvent` (`SharedBodySystem.Organs.cs:286`). Onyx's only prototype users are exotic implants (dubious.yml) that Wolfgate does not have. |
| `OrganFunctionChangedEvent` | **KEEP** (already declared) and **give it its first consumer** | §5.2 — map `Functional == false` onto Shitmed's `OrganEnableChangedEvent(false)` so a 0-HP organ stops granting `OnAdd` components and blinds on eyes, in the tick before destruction |
| `OrganEffectSystem.cs` (job 1, eye-damage marshalling) | **SKIP** | `EyesSystem.HandleSight` (`Content.Server/_Shitmed/Body/Systems/EyesSystem.cs:24-65`) already transfers blindness between body and eye organ via `BlindableSystem.TransferBlindness`. Also `BlindableSystem.GetEyeDamage` is **MISSING** in WG (§6). |
| `OrganEffectSystem.cs` (job 2, `BodyAnatomyComponent`) | **SKIP** | needs `OrganCategoryPrototype` (**MISSING**, zero hits in WG) and Onyx's `InitializeAnatomy` (**MISSING**). Its only purpose is to decide `Missing*`, all of which are skipped below. |
| `OrganEffectSystem.cs` (job 3, functional grants) | **map onto Shitmed** | as `FunctionalOrganComponent` above |
| `OrganEffectSystem.cs` (job 4, timed death) | **SKIP** | Shitmed `DelayedDeathComponent` already covers heart and brain, §3.3 |
| `OrganEffectSystem.cs` (job 5, vocal cords / neuro) | **SKIP** | D7; `TongueComponent`, `NeuroInterfaceRuntimeComponent`, `TonguelessAccentComponent`, `StatusEffectSurgicallyMuted` all **MISSING** in WG (§6) |
| `BodyOrgansChangedEvent` | **SKIP** | zero subscribers in Onyx at the pin |
| `BodyAnatomyComponent` | **SKIP** | above |
| `MissingEyesComponent` | **SKIP** | Shitmed's `EyesSystem` → `TemporaryBlindnessComponent` on eye removal is the equivalent consumer |
| `MissingEarsComponent` | **SKIP (record)** | WG's `EarsComponent` has no consumer at all (`_Shitmed/Body/Organ/EarsComponent.cs:7`). Porting it would need a new `ChatSystem` hearing hook — out of P3 scope, and ears are not even a slot in any Wolfgate body graph (§5.1). |
| `MissingHeadComponent` | **SKIP** | head removal takes the brain with it → `DebrainedComponent` gives speech-block, stand-block and a 60 s death timer, i.e. Onyx's three consequences by another route |
| `MissingHeartComponent` (+ `BodyStasis.cs`) | **SKIP — answer to the DECISIONS question is NO for both** | `HeartSystem.cs:26` already ensures `DelayedDeathComponent`. Only delta: Onyx's 30 s vs Shitmed's 60 s, and Onyx's 300 s stasis extension. If the stasis extension is wanted later it is a 3-line read of `BuckleComponent` + `OperatingTableComponent` (`Content.Shared/_Shitmed/Surgery/OperatingTableComponent.cs:6`) + `StasisBedComponent` + `ApcPowerReceiverComponent` inside `DelayedDeathSystem.Update` — **not** a reason to port either file. |
| `BreathingImmunityComponent` | **SKIP — already exists** | `Content.Shared/_Shitmed/Body/Components/BreathingImmunityComponent.cs:8`, consumed at `Content.Server/Body/Systems/RespiratorSystem.cs:79` |
| `InitiallyLungedComponent` | **SKIP** | Onyx's respirator rework; WG's `RespiratorSystem` is upstream and keys on `GetBodyOrganEntityComps<LungComponent>` instead. Manifest already records `RespiratorSystem.cs` as never-touched (`WOLFMED_MANIFEST.md:99`). |
| `TaggedOrganComponent` + `TaggedOrganSystem` | **SKIP** | dead code at the pin — zero prototype users, zero other references (§2.5) |
| `ProfileOrgansComponent` | **SKIP** | Nubody character-profile organ generation, not wound-related (D8) |
| Onyx health-analyzer organ readout | **SKIP for P3, record for phase 4** | the whole `HealthAnalyzerWoundDiagnostic`/`HealthAnalyzerOrganInfo` surface is unported; organ damage will be **completely invisible to players** until then (§8 R3) |

**Net C# for P3-2: one new `_WF` system of ~40 lines, one ~6-line addition to the already-ported
`OrganHealthSystem`, and (optionally) one new prototype + one data-driven ensure system. Nothing is ported
verbatim from Onyx that is not already in the tree.**

---

## 5. Prototype work

### 5.1 What Wolfgate's organs look like

`Resources/Prototypes/Body/Prototypes/human.yml:11-27` — the **only** organ slots on a human are
`head: {brain, eyes}` and `torso: {heart, lungs, stomach, liver, kidneys}`. **Seven organs.**
`OrganHumanTongue` (`human.yml:119`), `OrganHumanAppendix` (`:128`) and `OrganHumanEars` (`:139`) exist as
entities but **no body graph in the repo slots them** — enumerating every `Organ*` id across
`Resources/Prototypes/Body/Prototypes/*.yml` and `Resources/Prototypes/_*/Body/Prototypes/*.yml` yields
only brain/eyes/heart/kidneys/liver/lungs/stomach variants (plus `OrganIPCEyes`/`OrganIPCPump`, phase 5).
**So Onyx's tongue / ears / appendix policies have no target and should be recorded as skipped.**

No Wolfgate organ prototype carries `WolfmedOrgan` or `OrganDamage` (zero hits). The four fields on
`WolfmedOrganComponent` default to `Health = MaxHealth = 15` (`:13,:16`), exactly Onyx's C# defaults, so
**the YAML only needs `destructionWound`/`destructionWoundSeverity` and the `OrganDamage` block.**

### 5.2 Two viable shapes — recommend **A for P3-2, B recorded as the follow-up**

**Option A (Onyx-faithful, prototype-driven).** Follow WP7's established pattern
(`WOLFMED_MANIFEST.md:114` + Deviations at `:377`): a new file of abstract ids in
`Resources/Prototypes/_WF/Wolfmed/Body/organs.yml`, each added to the corresponding upstream organ's
`parent:` list with a one-line `# WOLFGATE` edit — because redeclaring an existing `id:` in a second file
throws RT's `Duplicate ID`.

*Cost, measured.* The 17 wound-host species (`WOLFMED_MANIFEST.md:169-172`) reach their organs through
**five different inheritance roots**, so the 7 `OrganHuman*` edits do **not** cover everything:

| Root | Covers | Edits |
|---|---|---|
| `OrganHuman{Brain,Eyes,Heart,Lungs,Liver,Stomach,Kidneys}` | human, gingerbread, dwarf (`dwarf.yml:3,11,19` parent `OrganHuman*`), vox (`vox.yml:3,19,36,60,71,83,95`), yowie (`_Goobstation/…/yowie.yml:3,26,34,42`), chitinid (partly), feroxi (partly), rodentia/tajaran/vulpkanin/moth/reptilian (partly), asakim (`_Mono/…/asakim.yml:3,13,46`), hydrakin (`_Mono/…/hydra.yml:53,85`) | **7** |
| `OrganAnimal{Heart,Liver,Lungs,Kidneys,Stomach}` (all `parent: BaseAnimalOrgan`, `Body/Organs/Animal/animal.yml:36,71,115,141,169`) | moth, reptilian, rodentia, tajaran, vulpkanin, harpy, arachnid lungs, chitinid stomach, feroxi liver, asakim heart | **5** |
| `Body/Organs/arachnid.yml` — `OrganArachnidEyes:170`, `…Heart:91`, `…Liver:120`, `…Kidneys:148` (`BaseArachnidOrgan` / `BaseHumanOrgan`) | arachnid | **4** |
| `Body/Organs/diona.yml` — `OrganDionaBrain:31`, `…Eyes:61`, `…Stomach:75`, `…Lungs:107` (`BaseDionaOrgan`) | diona (+ the three `*Nymph` children) | **4** |
| `Body/Organs/slime.yml:47 OrganSlimeLungs`, `_Mono/…/hydra.yml:29,64,93` (`OrganHydrakin{Stomach,Liver,Lungs}`), `_DV/…/feroxi.yml` (`OrganFeroxi{Lungs,Stomach}`), `_DV/…/chitinid.yml:31 OrganChitinidLiver` | slime, hydrakin, feroxi, chitinid | **~7** |

≈ **27 one-line parent edits across ~10 files in 6 fork namespaces** (`_DV`, `_Mono`, `_Goobstation`,
`_Shitmed`, base, `_NF`). Every one of them is a permanent upstream-merge conflict surface for a downstream
fork. Correct and Onyx-shaped, but expensive.

**Option B (Wolfgate-shaped, data-driven).** One `WolfmedOrganProfilePrototype` keyed by
`OrganComponent.SlotId` (`OrganComponent.cs:33-34`), and a small `_WF` server system that, on
`<OrganComponent, OrganAddedToBodyEvent>` (**free pair**, §7) into a `WoundHostComponent` body, `EnsureComp`s
`WolfmedOrganComponent` + `OrganDamageComponent` from the profile. `SlotId` is set on essentially every
organ prototype (grepped: `brain`×4, `eyes`×6, `heart`×5, `lungs`×12, `liver`×7, `stomach`×6,
`kidneys`×4, plus `pump`/`core` for IPC), and where it is empty the fallback is
`SharedBodySystem.GetOrganContainerId(slotId)` (`SharedBodySystem.cs:81`, `public static`) or simply "no
organ damage", which is safe. **Zero upstream edits, automatic coverage of every current and future
species and every fork's organs.** Deviation from Onyx's prototype-data model; per-prototype overrides need
the component declared explicitly (which still works, because `EnsureComp` does not overwrite).

**Recommendation:** ship **Option A limited to the seven `OrganHuman*` ids** in P3-2 (it covers human,
gingerbread, dwarf, vox, yowie and the human-lineage organs of eight more species — the overwhelming
majority of play), record the remaining ~20 roots in the manifest as "organ damage does not fire on these
organs yet", and put **Option B** on the phase-4 list as the general fix. This keeps P3-2 small, testable
and reversible, and the "does not fire" state is a silent no-op, never a crash
(`OrganDamageSystem.cs:58-59` just returns).

### 5.3 Proposed YAML — `Resources/Prototypes/_WF/Wolfmed/Body/organs.yml`

Numbers are Onyx's, verbatim, from `ONYX Resources/Prototypes/Body/base_organs.yml` (line cites per block).
`health`/`maxHealth` are deliberately omitted: `WolfmedOrganComponent`'s C# defaults are already 15/15,
identical to Onyx's `OrganComponent` defaults, and no Onyx prototype overrides them.

```yaml
# Wolfmed organ data. Values are Onyx's (Resources/Prototypes/Body/base_organs.yml @ 2f5bab9).
# health/maxHealth are omitted - WolfmedOrganComponent already defaults to 15/15, which is Onyx's
# OrganComponent C# default, and no Onyx organ prototype overrides it.
#
# Same shape as _WF/Wolfmed/Body/parts.yml: separate abstract ids, not re-declarations of the upstream
# OrganHuman* ids, because RT's ComponentRegistrySerializer throws "Duplicate ID" if a second file
# redeclares an existing id. Each is added to the upstream organ's `parent:` list with a one-line
# `# WOLFGATE` edit in Resources/Prototypes/Body/Organs/human.yml.

# Brain: no destruction wound - OrganHealthSystem kills the mob instead of destroying the organ.
- type: entity
  id: WolfmedOrganBrain
  abstract: true
  components:
  - type: WolfmedOrgan
  - type: OrganDamage
    hitChance: 0.8
    selectionWeight: 0.75
    damageMultipliers:
      Blunt: 0.115
      Slash: 0.25
      Piercing: 0.42
      Heat: 0.15
      Cold: 0.05
      Shock: 0.3125

# Eyes: paired organ, no internal bleeding on destruction (Onyx base_organs.yml:547).
- type: entity
  id: WolfmedOrganEyes
  abstract: true
  components:
  - type: WolfmedOrgan
  - type: OrganDamage
    hitChance: 0.7
    selectionWeight: 0.2275
    damageMultipliers:
      Blunt: 0.115
      Slash: 0.3
      Piercing: 0.4375
      Heat: 0.15
      Cold: 0.05
      Shock: 0.25

- type: entity
  id: WolfmedOrganHeart
  abstract: true
  components:
  - type: WolfmedOrgan
    destructionWound: InternalBleedingWound
    destructionWoundSeverity: 45
  - type: OrganDamage
    hitChance: 0.8
    selectionWeight: 0.64
    damageMultipliers:
      Blunt: 0.1
      Slash: 0.25
      Piercing: 0.455
      Heat: 0.15
      Cold: 0.05
      Shock: 0.3375

- type: entity
  id: WolfmedOrganLungs
  abstract: true
  components:
  - type: WolfmedOrgan
    destructionWound: InternalBleedingWound
    destructionWoundSeverity: 35
  - type: OrganDamage
    selectionWeight: 1.38          # highest weight in the torso; hitChance left at the 1.0 default
    damageMultipliers:
      Blunt: 0.1
      Slash: 0.25
      Piercing: 0.42
      Heat: 0.165
      Cold: 0.05
      Shock: 0.25

- type: entity
  id: WolfmedOrganLiver
  abstract: true
  components:
  - type: WolfmedOrgan
    destructionWound: InternalBleedingWound
    destructionWoundSeverity: 40
  - type: OrganDamage
    selectionWeight: 1.1           # hitChance left at the 1.0 default
    damageMultipliers:
      Blunt: 0.1
      Slash: 0.3
      Piercing: 0.4375
      Heat: 0.15
      Cold: 0.05
      Shock: 0.25

- type: entity
  id: WolfmedOrganStomach
  abstract: true
  components:
  - type: WolfmedOrgan
    destructionWound: InternalBleedingWound
    destructionWoundSeverity: 25
  - type: OrganDamage
    hitChance: 0.85
    selectionWeight: 0.56
    damageMultipliers:
      Blunt: 0.1
      Slash: 0.275
      Piercing: 0.4025
      Heat: 0.15
      Cold: 0.05
      Shock: 0.25

- type: entity
  id: WolfmedOrganKidneys
  abstract: true
  components:
  - type: WolfmedOrgan
    destructionWound: InternalBleedingWound
    destructionWoundSeverity: 30
  - type: OrganDamage
    hitChance: 0.9
    selectionWeight: 0.51
    damageMultipliers:
      Blunt: 0.1
      Slash: 0.25
      Piercing: 0.4025
      Heat: 0.15
      Cold: 0.05
      Shock: 0.25
```

### 5.4 The seven upstream one-line edits (`Resources/Prototypes/Body/Organs/human.yml`)

| Line | Current | Becomes |
|---|---|---|
| `:53` (`OrganHumanBrain`) | `parent: BaseHumanOrganUnGibbable` | `parent: [BaseHumanOrganUnGibbable, WolfmedOrganBrain] # WOLFGATE (WP11, D8): Wolfmed organ health + organ-damage policy` |
| `:103` (`OrganHumanEyes`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganEyes] # WOLFGATE (WP11, D8)` |
| `:150` (`OrganHumanLungs`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganLungs] # WOLFGATE (WP11, D8)` |
| `:189` (`OrganHumanHeart`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganHeart] # WOLFGATE (WP11, D8)` |
| `:215` (`OrganHumanStomach`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganStomach] # WOLFGATE (WP11, D8)` |
| `:249` (`OrganHumanLiver`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganLiver] # WOLFGATE (WP11, D8)` |
| `:270` (`OrganHumanKidneys`) | `parent: BaseHumanOrgan` | `parent: [BaseHumanOrgan, WolfmedOrganKidneys] # WOLFGATE (WP11, D8)` |

**Inheritance side effects to check at implementation time (all verified to exist):**
- `Resources/Prototypes/_Shitmed/Body/Organs/cybernetic.yml:2` `BaseCyberneticEyes` and
  `:20`/`:31`-style `BasicCyberneticEyes`/`SecurityCyberneticEyes`/`MedicalCyberneticEyes` all parent
  `OrganHumanEyes` → **cybernetic eyes would inherit organic organ-damage multipliers.**
- `Resources/Prototypes/_Shitmed/Body/Organs/generic.yml:2,8,14,20` parent
  `OrganHuman{Heart,Liver,Lungs,Eyes}`; `Resources/Prototypes/_Mono/Body/Organs/cybernetics.yml:2,18,120`
  parent `OrganHumanHeart`.
- **Decision needed (§8 D-ORG-3):** leave them inheriting (Onyx *does* route organ damage through
  `CyberneticBodyPartProfile`, `ONYX .../wounds.yml:147-151`), or add
  `- type: OrganDamage\n  hitChance: 0` to the cybernetic bases. Recommend leaving them for P3-2 and
  handling cybernetics properly in phase 5, since phase 5 owns the whole cybernetic/IPC profile.
- `Resources/Prototypes/Body/Organs/rat.yml:3` (`OrganRatLungs`) and `_NF/Body/Organs/goblin_organs.yml`
  parent `OrganHuman*`; those mobs are not wound hosts, so the components sit inert. Harmless.

### 5.5 `wounds.yml` — nothing to change

`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:27-36` already ships `organDamage.chances`
(`Head 0.05 / Torso 0.04 / Arm 0.02 / Hand 0.01 / Leg 0.02 / Foot 0.01`) and `maxAffected: 2`, with the
D9 Chest+Groin→Torso fold documented in place at `:30-31`. No P3-2 edit.

---

## 6. Every external symbol a P3-2 file needs — SAME / DIFFERENT / MISSING

| Symbol | Onyx form | Wolfgate | Verdict |
|---|---|---|---|
| `OrganComponent.Health` / `.MaxHealth` | `ONYX Content.Shared/Body/OrganComponent.cs:19-23`, `FixedPoint2` 15/15 | absent on `WG Content.Shared/Body/Organ/OrganComponent.cs` | **MISSING** → compat: `WolfmedOrganComponent.Health/.MaxHealth` (`:13,:16`), same defaults, **already exists** |
| `OrganComponent.DestructionWound(Severity)` | `ONYX …/OrganComponent.cs:30-34` | absent | **MISSING** → `WolfmedOrganComponent:19,22`, **already exists** |
| `OrganComponent.Body` | `ONYX …:40-41 EntityUid? Body` | `WG …/OrganComponent.cs:17-18 EntityUid? Body` | **SAME** |
| `OrganComponent.Category` | `ONYX …:46-47 ProtoId<OrganCategoryPrototype>?` | `WG …:33-34 string SlotId = ""` | **DIFFERENT** |
| `OrganCategoryPrototype` | `ONYX Content.Shared/Body/OrganCategoryPrototype.cs` | — | **MISSING** (zero hits) |
| `OrganDamageComponent` | `ONYX …/OrganDamageComponent.cs` | `WG Content.Shared/_Onyx/Body/OrganDamageComponent.cs` | **SAME** (verbatim port) |
| `OrganFunctionChangedEvent(EntityUid Body, bool Functional)` | `ONYX FunctionalOrganComponent.cs:18-19` | `WG Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:20-21`, `namespace Content.Shared._Onyx.Body` | **SAME shape, DIFFERENT assembly** — it is declared in `Content.Server`; a shared consumer would need the declaration moved to `Content.Shared/_WF/Wolfmed/Compat/` (`WOLFMED_MANIFEST.md:327-332`). P3-2's consumer is server-side, so **no move needed**. |
| `OrganHealthSystem.SetHealth/ChangeHealth(Entity<OrganComponent>, …)` | `ONYX OrganHealthSystem.cs:48,62` | `WG …/OrganHealthSystem.cs:60,75` take `Entity<WolfmedOrganComponent>` | **DIFFERENT signature**, deliberate (D8) |
| `SharedBodySystem.GetPartOrgans` | `ONYX _Onyx/Body/Systems/SharedBodySystem.cs` (Nubody walk) | `WG Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:825 public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)` | **SAME shape** — already used by the ported file |
| `SharedBodySystem.GetBodyOrgans` | `ONYX …/SharedBodySystem.cs:142` | `WG Content.Shared/Body/Systems/SharedBodySystem.Body.cs:277` | **SAME shape** |
| `SharedBodySystem.RemoveOrgan(EntityUid, OrganComponent?)` | — | `WG SharedBodySystem.Organs.cs:172` | **WG-only**, already used |
| `SharedBodySystem.TryGetOrganInSlot` / `TryRemoveOrgan` / `TryGetOrgan` | `ONYX` yes | — | **MISSING** (zero hits) — already worked around |
| `SharedBodySystem.InitializeAnatomy` | `ONYX _Onyx/Body/Systems/SharedBodySystem.cs:160-182` | — | **MISSING** — only needed by the skipped `BodyAnatomyComponent` path |
| `SharedBodySystem.GetOrganContainerId(string)` | — | `WG Content.Shared/Body/Systems/SharedBodySystem.cs:81 public static string` | **WG-only**, useful for Option B |
| `OrganGotInsertedEvent(EntityUid Target)` / `OrganGotRemovedEvent` | `ONYX` Nubody, raised on parts **and** organs | `WG Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs:5,9`, raised **only on parts** by `WolfmedBodyPartLifecycleSystem.cs:42,62` | **DIFFERENT scope** — a `TaggedOrganSystem`-style organ subscriber would never fire. Another reason TaggedOrgan is skipped. |
| `OrganAddedToBodyEvent(EntityUid Body, EntityUid Part)` | — | `WG Content.Shared/Body/Events/MechanismBodyEvents.cs:16` `[ByRefEvent]` | **WG-only** |
| `OrganRemovedFromBodyEvent(EntityUid OldBody, EntityUid OldPart)` | — | `WG …/MechanismBodyEvents.cs:28` `[ByRefEvent]` | **WG-only** |
| `OrganEnableChangedEvent(bool Enabled)` | — | `WG Content.Shared/_Shitmed/Body/Organ/OrganEvents.cs:7` `[ByRefEvent]` | **WG-only** — the Shitmed organ-condition switch |
| `OrganComponentsModifyEvent(EntityUid Body, bool Add)` | — | `WG …/OrganEvents.cs:4` (**not** `[ByRefEvent]`) | **WG-only** |
| `OrganEnabledEvent` / `OrganDisabledEvent` | — | `WG …/OrganEvents.cs:10,13` | **WG-only**, raised **only for `EyesComponent`** (`SharedBodySystem.Organs.cs:296-301,309-314` — "I hate having to hardcode these checks") |
| `_Shitmed OrganEffectSystem` | — | `WG Content.Shared/_Shitmed/BodyEffects/OrganEffectSystem.cs:13` | **WG-only**, the `FunctionalOrgan` equivalent |
| `FunctionalOrganComponent` | `ONYX` yes | — | **MISSING**, intentionally (maps to `OrganComponent.OnAdd`) |
| `MissingHeartComponent` / `MissingHeadComponent` / `MissingEyesComponent` / `MissingEarsComponent` / `BodyAnatomyComponent` / `InitiallyLungedComponent` / `BodyOrgansChangedEvent` / `OrganEffectOwnershipComponent` / `TaggedOrganComponent` / `OrganTagOwnershipComponent` / `ProfileOrgansComponent` | `ONYX` yes | — | **MISSING**, all deliberately skipped (§4) |
| `BreathingImmunityComponent` | `ONYX OrganConsequenceComponents.cs:38` | `WG Content.Shared/_Shitmed/Body/Components/BreathingImmunityComponent.cs:8` | **SAME name, DIFFERENT namespace** — already present and consumed (`RespiratorSystem.cs:79`). **Do not port Onyx's**: two `[RegisterComponent]` classes with the same registered name is a start-up crash. |
| `BrainComponent` | `ONYX Content.Shared/Body/Components/BrainComponent.cs` (shared) | `WG Content.Server/Body/Components/` (server) | **DIFFERENT assembly**, already handled (`OrganHealthSystem.cs:1`) |
| `HeartComponent` / `LiverComponent` / `EyesComponent` / `EarsComponent` / `DebrainedComponent` | — | `WG Content.Shared/_Shitmed/Body/Organ/{Heart,Liver,Eyes,Ears,Debrained}Component.cs` | **WG-only** |
| `DelayedDeathComponent` (`DeathTime = 60`) | — | `WG Content.Server/_Shitmed/DelayedDeath/DelayedDeathComponent.cs:10` | **WG-only** — the `MissingHeart`/`MissingHead` equivalent |
| `LungComponent` | `ONYX Content.Shared/Body/Components/LungComponent.cs` | `WG Content.Server/Body/Components/LungComponent.cs` (used at `RespiratorSystem.cs:122`) | **DIFFERENT assembly** — not needed by P3-2 |
| `MobStateSystem.IsDead / HasState / ChangeMobState(uid, state, comp, origin)` | `ONYX` | already compiled against in `OrganHealthSystem.cs:49-51` | **SAME** |
| `WoundSystem.CreateOrMergeWound(Entity<WoundableComponent?>, ProtoId<WoundPrototype>, FixedPoint2)` | `ONYX` | `WG Content.Shared/_Onyx/Wounds/WoundSystem.cs:166-170` | **SAME** |
| `InternalBleedingWound` prototype | `ONYX .../wounds.yml` | `WG Resources/Prototypes/_Onyx/Wounds/wounds.yml:405-413` (`WoundInternalBleedingBehavior rate: 0.02, chance: 1, maximumSeverity: 200`) | **SAME** |
| `BlindableSystem.GetEyeDamage` | `ONYX`, used at `OrganEffectSystem.cs:67,110` | `WG Content.Shared/Eye/Blinding/Systems/BlindableSystem.cs` has `UpdateIsBlind:32`, `AdjustEyeDamage:59`, `SetMinDamage:82` but **no `GetEyeDamage`** | **MISSING** — and `BlindableComponent.EyeDamage` is `[Access(typeof(BlindableSystem))]` (`BlindableComponent.cs:8`), so a `_WF` reader would need an accessor. Only the skipped job-1 needs it. |
| `TongueComponent` / `TonguelessAccentComponent` / `NeuroInterfaceRuntimeComponent` / `NeuroInterfaceEnabledChangedEvent` / `StatusEffectSurgicallyMuted` / `EyeDamageChangedEvent` on `BodyComponent` | `ONYX` yes | `EyeDamageChangedEvent` exists (`BlindableSystem.cs:17`), the rest are **zero hits** | **MISSING** — all in the skipped jobs |
| `StatusEffectsSystem.TrySetStatusEffectDuration` / `TryRemoveStatusEffect` | `ONYX` | `WG Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs:63` etc. (ported WP1) | **SAME** — not needed by P3-2 |
| `OperatingTableComponent` / `StasisBedComponent` / `ApcPowerReceiverComponent` / `BuckleComponent` | `ONYX BodyStasis.cs:1-4` | `WG Content.Shared/_Shitmed/Surgery/OperatingTableComponent.cs:6`, `Content.Server/Bed/BedSystem.cs:37`, present | **SAME** — all four exist, so `BodyStasis` *could* be ported; it just has no consumer (§4) |
| `BodyPartType` | `ONYX` `{Other, Torso, Head, Arm, Hand, Leg, Foot, Tail}` + `Chest`/`Groin` | `WG Content.Shared/Body/Part/BodyPartType.cs:12-19` `{Other, Torso, Head, Arm, Hand, Leg, Foot, Tail}` | **DIFFERENT** — no `Chest`/`Groin`; already handled by D9 in `wounds.yml:30-31` |
| `DamageSpecifier.DamageDict` | `ONYX` keyed by `ProtoId<DamageTypePrototype>` | `WG` keyed by **`string`** (WP10-2) | **DIFFERENT** — works in `OrganDamageSystem.cs:87-89` only via RT's `implicit operator ProtoId<T>(string)`. Any P3-2 code that reads `DamageMultipliers` must use the same pattern. |

---

## 7. Duplicate directed-subscription audit

RT stores **one** registration per `(component, event)` pair for the whole bus.

### 7.1 Pairs P3-2 would register

| # | Pair | Existing WG owner | Free? |
|---|---|---|---|
| 1 | `<WolfmedOrganComponent, OrganFunctionChangedEvent>` | none — `OrganFunctionChangedEvent` has **zero subscribers** in WG (only `OrganHealthSystem.cs:71-72` raises it) | **YES** — recommended registrant for §5.2's consequence mapping |
| 2 | `<OrganComponent, OrganAddedToBodyEvent>` | **none.** Full grep of `SubscribeLocalEvent<*, OrganAddedToBodyEvent>` in WG → `BrainComponent` (`BrainSystem.cs:24`) and `HeartComponent` (`_Shitmed/Body/Organ/HeartSystem.cs:16`) only | **YES** — needed only for Option B |
| 3 | `<OrganComponent, OrganRemovedFromBodyEvent>` | **none.** Subscribers are `BrainComponent` (`BrainSystem.cs:26`), `NymphComponent` (`Content.Server/Species/Systems/NymphSystem.cs:24`), `HeartComponent` (`HeartSystem.cs:17`) | **YES** |
| 4 | `<WolfmedOrganComponent, OrganAddedToBodyEvent>` | none | **YES** (safer Option-B variant if the component is pre-declared) |

> **Correction to PLAN2.md §5.2.** That table lists
> `<OrganComponent, OrganAddedToBodyEvent/RemovedFromBodyEvent>` under *"Already registered by phase 1. Do
> not re-register."*, echoing PLAN.md §5.2. **That is stale.** Phase 1 never registered them — the manifest
> says so explicitly (`WOLFMED_MANIFEST.md:291-295`: *"`WolfmedBodyPartLifecycleSystem` does not subscribe
> `<OrganComponent, OrganAddedToBodyEvent>` / `<OrganComponent, OrganRemovedFromBodyEvent>` although PLAN
> 5.2 assigns those pairs to it"*), and the grep above confirms it. **Both pairs are free.**

### 7.2 Pairs P3-2 must **not** register

| Pair | Owner | Consequence |
|---|---|---|
| `<OrganComponent, MapInitEvent>` | `Content.Shared/Body/Systems/SharedBodySystem.Organs.cs:22` | server-start crash |
| `<OrganComponent, OrganEnableChangedEvent>` | `SharedBodySystem.Organs.cs:23` | server-start crash — **raise the event instead** (precedent: `Content.Server/_Shitmed/Cybernetics/CyberneticsSystem.cs:27-28,45-46`) |
| `<OrganComponent, OrganComponentsModifyEvent>` | `Content.Shared/_Shitmed/BodyEffects/OrganEffectSystem.cs:23` | server-start crash |
| `<WoundableComponent, PartDamageAppliedEvent>` | `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs:31` | already claimed — new handlers must be `HandlePartDamageApplied`-style methods called from there, never their own subscription (PLAN.md §5.2) |
| `<WoundableComponent, OrganGotInsertedEvent>` / `<…, OrganGotRemovedEvent>` | `Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs:34-35` (phase 2) | server-start crash |
| `<BrainComponent, OrganAddedToBodyEvent/RemovedFromBodyEvent>`, `<HeartComponent, …>`, `<EyesComponent, OrganEnabledEvent/OrganDisabledEvent>` | `BrainSystem.cs:24,26`; `HeartSystem.cs:16-17`; `EyesSystem.cs:20-21` | already claimed |
| `<BlindableComponent, EyeDamageChangedEvent>` | `Content.Shared/Eye/Blinding/Systems/BlindableSystem.cs:17` | `<BodyComponent, EyeDamageChangedEvent>` (Onyx's job-1 pair) would be *free*, but job 1 is skipped |

### 7.3 Component-registration names

`WolfmedOrgan` and `OrganDamage` are **already registered** by phase 1 (`WolfmedOrganComponent.cs:9`,
`OrganDamageComponent.cs:9`). P3-2 adds **no new component name** under Option A. Under Option B it adds one
prototype kind (`wolfmedOrganProfile`) — grep before committing to the name.

### 7.4 Ordering constraints

- `OrganHealthSystem.Update` and `OrganDamageSystem` never run in the same frame ordering-sensitive way:
  damage is applied from an event handler, destruction happens on the next `Update`. So an organ can sit at
  0 HP for up to one tick. That one-tick window is the **only** state in which
  `OrganFunctionChangedEvent(false)` is observable for a non-brain organ.
- **EMP race (low, record it):** `CyberneticsSystem.OnEmpDisabledRemoved` (`:45-46`) raises
  `OrganEnableChangedEvent(true)` unconditionally. If §5.2 maps functional→Enabled, an EMP recovery inside
  that one-tick window could re-enable a doomed organ. Harmless (it is deleted the next tick) and only
  reachable on `Cybernetics` organs. Do not add `before:`/`after:` for it.

---

## 8. P3-6 — the numbers the user must see

All from shipped data: `OrganDamageComponent.MaxDamageFraction = 0.3` (`:22`),
`WolfmedOrganComponent.MaxHealth = 15` (`:16`), `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:27-36`,
`ONYX Resources/Prototypes/Body/base_organs.yml` multipliers.

### 8.1 The per-hit cap dominates everything

Cap = `0.3 × 15 = **4.5 organ HP per damage application**`, i.e. **30 % of an organ, four applications to
destroy it**, no matter how hard the hit.

Raw damage at which the cap starts binding (`4.5 / multiplier`):

| Organ | Piercing | Slash | Blunt | Shock | Heat |
|---|---|---|---|---|---|
| Heart | **9.9** | 18.0 | 45.0 | 13.3 | 30.0 |
| Lungs | 10.7 | 18.0 | 45.0 | 18.0 | 27.3 |
| Liver | 10.3 | 15.0 | 45.0 | 18.0 | 30.0 |
| Kidneys | 11.2 | 18.0 | 45.0 | 18.0 | 30.0 |
| Stomach | 11.2 | 16.4 | 45.0 | 18.0 | 30.0 |
| Brain | 10.7 | 18.0 | 39.1 | 14.4 | 30.0 |
| Eyes | 10.3 | 15.0 | 39.1 | 18.0 | 30.0 |

**Wolfgate's base bullet is `Piercing: 14`** (`Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml:104-106`).
Every organ's Piercing threshold is below 14, so **every ordinary gunshot that triggers organ damage does
exactly the capped 4.5.** Weapon damage above ~11 Piercing is irrelevant to organ damage — this is worth
saying out loud before any balance pass.

### 8.2 How often it actually fires

Roll chance is **per `PartDamageAppliedEvent`** (one per damage application to that part).
Torso 0.04, head 0.05. `maxAffected: 2`.

Wolfgate human torso organ set and weights (`heart 0.64, lungs 1.38, stomach 0.56, liver 1.10,
kidneys 0.51`; total **4.19**); head set (`brain 0.75, eyes 0.2275`; total 0.9775, and with only 2 organs in
the head **both are always drawn**).

| Organ | P(drawn in the 2 picks) | × `hitChance` | × part chance | **P(damaged per hit on that part)** | **Expected hits to destroy** (4 × 4.5) |
|---|---|---|---|---|---|
| Lungs | 0.602 | 1.00 | 0.04 | **2.41 %** | **≈ 166 torso hits** |
| Liver | 0.512 | 1.00 | 0.04 | **2.05 %** | **≈ 195 torso hits** |
| Heart | 0.327 | 0.80 | 0.04 | **1.05 %** | **≈ 382 torso hits** |
| Stomach | 0.288 | 0.85 | 0.04 | **0.98 %** | **≈ 408 torso hits** |
| Kidneys | 0.264 | 0.90 | 0.04 | **0.95 %** | **≈ 421 torso hits** |
| Brain | 1.000 | 0.80 | 0.05 | **4.0 %** | **≈ 100 head hits** |
| Eyes | 1.000 | 0.70 | 0.05 | **3.5 %** | **≈ 114 head hits** |

**Read this as: at Onyx's own numbers, organ destruction is a ~100–400-hit event.** A Wolfgate gunfight
does not last 166 torso hits. Organ damage as shipped is **flavour that almost nobody will ever see** — it
will accumulate slowly across a shift on a surviving character and, because nothing heals it (§8.4), it is
a one-way ratchet.

### 8.3 What destruction costs, when it does happen

| Organ | Immediate effect | Internal bleeding |
|---|---|---|
| Brain | **instant death** (`OrganHealthSystem.cs:45-51`) | — |
| Heart | `DelayedDeathComponent` → **crit then dead in 60 s**, defib refused (`DelayedDeathSystem.cs:47-63`) | sev **45** → `0.02 × 45 = **0.9 blood units/s**`. Human `BloodMaxVolume = 300`, `BloodlossThreshold = 0.9` (`BloodstreamComponent.cs:134,63`) → bloodloss damage starts after **≈ 33 s** and the blood is gone in ≈ 5.5 min. |
| Lungs | suffocation | sev **35** → 0.7 u/s → threshold in ≈ 43 s |
| Liver | metabolite processing lost | sev **40** → 0.8 u/s → ≈ 37 s |
| Kidneys | catch-all metabolizer lost | sev **30** → 0.6 u/s → ≈ 50 s |
| Stomach | digestion lost | sev **25** → 0.5 u/s → ≈ 60 s |
| Eyes | `TemporaryBlindnessComponent` on the body (`EyesSystem.cs:83`) | — |

**So: destruction is rare, but when it lands it is lethal within a minute and unrecoverable without a
transplant.** That is a sharp cliff — worth the user's explicit sign-off.

### 8.4 The irreversibility number

**Zero.** There is no path in Wolfgate that raises organ health. Onyx's single healer is
`SurgeryOrganHealEffectComponent`, `amount: 1` per repeatable surgery step
(`ONYX surgery_steps.yml:862-940`), and D7 excludes Onyx surgery. Wolfgate's Shitmed surgery can **replace**
an organ but never **repair** one.

---

## 9. Ordered file list and difficulty

### 9.1 Recommended P3-2 (WP11-organs) file order

| # | File | Action | Lines | Difficulty | Notes |
|---|---|---|---|---|---|
| 1 | `Resources/Prototypes/_WF/Wolfmed/Body/organs.yml` | **new** | ≈ 105 | **Low** | §5.3 verbatim. Lint in Release; the ErrorNode trap applies. |
| 2 | `Resources/Prototypes/Body/Organs/human.yml` | **modify**, 7 one-line `parent:` edits at `:53,:103,:150,:189,:215,:249,:270` | 7 | **Low** | §5.4. Owned solely by this package. |
| 3 | `Content.Server/_WF/Wolfmed/WolfmedOrganConsequenceSystem.cs` | **new** | ≈ 45 | **Medium** | Subscribes `<WolfmedOrganComponent, OrganFunctionChangedEvent>` (free, §7.1). On `Functional == false` raises Shitmed `OrganEnableChangedEvent(false)` on the organ (precedent: `CyberneticsSystem.cs:27-28`), giving the one-tick "organ is dead but still installed" state the same behaviour Onyx's `FunctionalOrgan` filter gives — `OnAdd` components revoked, eyes blinded. On `Functional == true` (surgical repair, later) raises `(true)`. `TerminatingOrDeleted` guards on both. |
| 4 | `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` | **modify**, ≈ 4 lines | 4 | **Low-Medium** | Add `TerminatingOrDeleted(uid)` / `TerminatingOrDeleted(parent)` guards in `Update`/`DestroyOrgan`. Phase-1's `WolfmedBodyPartLifecycleSystem.cs:31-33,52-54` had to add exactly this guard for the same reason (mob deletion detaches everything mid-termination) and `DestroyOrgan` calls `_wounds.CreateOrMergeWound` on the parent. **Mark `// WOLFGATE`.** |
| 5 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundOrganDamageTest.cs` | **new** | ≈ 200 | **Medium-High** | §10 |
| 6 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **append** | ≈ 8 rows | **Low** | rows for files 1–5 + a Deviations entry for the un-covered ~20 organ roots (§5.2) and for the skip list (§4) |
| 7 | `Docs/Wolfmed/WOLFMED_STATUS.md` | **modify** | ≈ 6 | **Low** | organ damage now live; record §8's numbers and the "no organ healing" gap |

**Total new C#: ≈ 45 lines. Total modified upstream: 11 lines across 2 files.**
**Overall package difficulty: LOW-MEDIUM** — dominated by the tests, not the code.

### 9.2 Explicitly out of scope for P3-2 (record in the manifest)

`FunctionalOrganComponent`, `OrganConsequenceComponents.cs` (all 7 declarations),
`Content.Server/_Onyx/Body/OrganEffectSystem.cs`, `MissingHeartComponent.cs`, `BodyStasis.cs`,
`TaggedOrganComponent`/`TaggedOrganSystem`, `ProfileOrgansComponent`,
`SharedVisualBodySystem.ProfileOrgans.cs`, `_Onyx/Body/Systems/BodyInventorySlotSystem.cs`, the
health-analyzer organ readout, the ~20 non-`OrganHuman*` organ roots, and Option B's
`WolfmedOrganProfilePrototype`.

---

## 10. Tests (feeds P3-5's "organ-damage consequence assertion")

`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundOrganDamageTest.cs`. Onyx has **no** organ-damage test at
the pin (`git grep -l "OrganDamage" HEAD -- Content.IntegrationTests` → nothing), so every assertion is new.
Standing traps from PLAN2 §6.1 apply (db.ef sqlite warnings, `[TestPrototypes]` hygiene).

| Id | Assertion | Method |
|---|---|---|
| T-ORG-DATA | Every human organ prototype resolves with `WolfmedOrgan` (15/15) and `OrganDamage` at the §5.3 numbers | prototype-level, no mob spawn — cheap and catches a mis-typed `parent:` |
| T-ORG-CAP | `OrganDamageSystem` never applies more than `MaxHealth × MaxDamageFraction` in one event | drive `_organHealth.ChangeHealth` indirectly by applying a 200-Piercing torso hit with a seeded `IRobustRandom`; assert organ health ≥ 10.5 |
| T-ORG-DESTROY | Driving an organ to 0 removes it from the part, deletes it, and leaves `InternalBleedingWound` at the prototype severity on the torso | call `OrganHealthSystem.SetHealth(organ, 0)` directly, tick once, assert `GetPartOrgans` no longer contains it and `GetWounds(torso)` contains the wound |
| T-ORG-HEART | Destroying the heart puts `DelayedDeathComponent` on the body | proves the §3.3 Shitmed chain actually fires from `OrganHealthSystem.DestroyOrgan` — **the single most important assertion in this package** |
| T-ORG-BRAIN | Destroying the brain's health kills the mob and does **not** delete the organ | `OrganHealthSystem.cs:45-54` |
| T-ORG-EYES | Destroying the eyes puts `TemporaryBlindnessComponent` on the body | `EyesSystem.cs:83` |
| T-ORG-FUNC | `OrganFunctionChangedEvent(false)` revokes an `OnAdd` component from the body before destruction | needs a `[TestPrototypes]` organ with `- type: Organ\n  onAdd:` — validates file #3 |
| T-ORG-INERT | A non-wound-host mob takes torso damage and no organ loses health | regression guard for D3/D32 |

---

## 11. Risks, blockers and the decisions the user owes

| Id | Kind | Item |
|---|---|---|
| **D-ORG-1** | **user decision** | **Organ damage is irreversible (§8.4).** Ship it that way (Onyx defaults, D4 — a damaged organ is permanent until transplanted), or add a Wolfgate-only slow organ regen / make a chem or a Shitmed surgery step call `OrganHealthSystem.ChangeHealth(+x)`? The latter is ~15 lines but is a **deliberate divergence from Onyx**, so it needs a decision, not an analyst's judgement. |
| **D-ORG-2** | **user decision** | **The §8.2 rarity.** At Onyx's numbers a specific torso organ needs ~166–420 hits. Accept as flavour (D4), or scale `organDamage.chances` up? Note the `MaxDamageFraction` cap (§8.1) means *chance*, not weapon damage, is the only lever. |
| **D-ORG-3** | decision | **Cybernetic organs inherit organic organ-damage policy** through `OrganHumanEyes`/`OrganHumanHeart` (§5.4). Leave for phase 5, or zero `hitChance` on the cybernetic bases now? |
| **D-ORG-4** | decision | **Option A (7 edits, human lineage only) vs Option B (data-driven, full coverage)** — §5.2. Recommendation: A now, B in phase 4. |
| R1 | risk | **`OrganFunctionChangedEvent` is declared in `Content.Server`** (`OrganHealthSystem.cs:20-21`). File #3 is server-side so this is fine, but any future *shared* consumer forces the declaration into `Content.Shared/_WF/Wolfmed/Compat/` (already flagged, `WOLFMED_MANIFEST.md:327-332`). |
| R2 | risk | **`OrganDamageSystem.cs:25-26,38-39` is P3-1's edit site.** P3-2 must not touch those lines; sequential packages in one worktree will otherwise clobber. |
| R3 | risk | **Organ damage is invisible to players.** No health-analyzer organ readout, no examine text, no alert. Until phase 4 a player will suddenly have a destroyed organ with no warning and no diagnosis. Record in `WOLFMED_STATUS.md`. |
| R4 | risk | **`BreathingImmunityComponent` name collision.** Onyx's and Shitmed's both register as `BreathingImmunity`. Porting `OrganConsequenceComponents.cs` wholesale is a **server-start crash**. §4 skips it; this note exists so nobody re-adds the file. |
| R5 | risk | **`OrganHealthSystem.DestroyOrgan` runs during mob deletion.** `RecursiveDeleteEntity` detaches parts while the mob terminates and `CreateOrMergeWound`/`RemoveOrgan` on a terminating entity is exactly the failure phase 1 hit at `WolfmedBodyPartLifecycleSystem.cs:31-33`. File #4 exists for this. |
| R6 | note | **PLAN.md §5.2 / PLAN2.md §5.2 are stale** about `<OrganComponent, OrganAddedToBodyEvent/RemovedFromBodyEvent>` (§7.1). Both pairs are free. Correct the phase-3 plan so a future WP does not skip a pair it could use. |
| **No blocker.** | | Nothing in P3-2 is blocked on missing Wolfgate API. The compat layer built in phases 1–2 is sufficient; the only genuinely missing symbol (`BlindableSystem.GetEyeDamage`) belongs to a skipped job. |
