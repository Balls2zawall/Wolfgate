# Wolfmed content & licensing inventory

Scope: non-C# content (prototypes, textures, locale, tests-as-spec, CCVars) the wound port needs, and what already
exists in Wolfgate vs. must be ported. Onyx pinned at `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. All paths below are
relative to `C:\tmp\onyx` (ONYX) or the Wolfgate worktree (WG) unless stated otherwise.

**Note on the sparse checkout**: the actual `git sparse-checkout list` on disk is materially wider than the example
command quoted in `WOLFMED_HANDOFF.md` — it separately includes `Content.Shared/_Onyx/{StatusEffects,Traits,Surgery,
Cybernetics,EntityEffects}`, `Resources/Prototypes/{Alerts,Body,Entities,Reagents}` (top-level, not just `_Onyx`), and
several `Resources/Locale/en-US/_Onyx/*` leaf folders. Everything the checklist below asks for was present on disk;
nothing had to be invented. Two small dependencies (`Content.Shared/_Onyx/Repairable`, 4 files) fell outside even that
wider set — these were read with `git show HEAD:<path>` (git can resolve trees from a blobless partial clone even
outside the sparse set) and are noted as such below.

---

## 1. `Resources/Prototypes/_Onyx/Wounds/wounds.yml` (1065 lines)

30 prototypes total: 5 `bodyPartProfile`, 2 `fractureProfile`, 23 `wound`.

### `bodyPartProfile` (5)
`OrganicBodyPartProfile`, `IpcBodyPartProfile`, `SlimeBodyPartProfile`, `CyberneticBodyPartProfile`,
`PlantBodyPartProfile`. Fields: `treatmentCapabilities`, `bleedingMultiplier`, `scarrable`, `canFeelPain`,
`passiveRecoveryMultiplier`, `bedRecoveryMultiplier`, `acceptedDamageTypes`, `supportedWounds`, `organDamage.chances`
(per `BodyPartType`) / `organDamage.maxAffected`. Phase 1 (D3, organic humanoids) only needs `OrganicBodyPartProfile`;
the other 4 are phase-5 species work.

### `fractureProfile` (2)
`OrganicFractureProfile`, `CyberneticFractureProfile`. Both point at `wound: BoneFractureWound` /
`CyberneticFrameFractureWound` respectively and both set `alert: BrokenBones` (default field value — not overridden).

### `wound` (23)
`BluntWound`, `BoneFractureWound`, `CyberneticFrameFractureWound`, `SlashWound`, `SystemicBleedingWound`,
`PiercingWound`, `BurnWound`, `ElectricalWound`, `SurgicalIncisionWound`, `DismembermentWound`,
`IpcMechanicalDamageWound`, `CyberneticMechanicalDamageWound`, `SlimeBluntWound`, `SlimeSlashWound`,
`SlimePiercingWound`, `SlimeBurnWound`, `PlantBluntWound`, `PlantSlashWound`, `PlantPiercingWound`, `PlantBurnWound`,
`AmputationConsequenceWound`, `InternalBleedingWound`, `MedicalScarWound`. Phase 1 organic-only subset (per
`OrganicBodyPartProfile.supportedWounds`): `BluntWound`, `SlashWound`, `PiercingWound`, `BurnWound`, `ElectricalWound`,
`BoneFractureWound`, `SurgicalIncisionWound`, `DismembermentWound`, `AmputationConsequenceWound`,
`InternalBleedingWound`, `MedicalScarWound`, `SystemicBleedingWound` — 12 of the 23.

### Behavior `!type:` tags actually used in the data (5, from `Content.Shared/_Onyx/Wounds/WoundBehaviors.cs`)
`WoundBleedingBehavior`, `WoundPainBehavior`, `WoundScarBehavior`, `WoundFunctionalityBehavior`,
`WoundInternalBleedingBehavior`. A sixth type, **`WoundStatusEffectBehavior`**, is defined in code
(`WoundBehaviors.cs:98`) and consumed by `WoundStatusEffectSystem.cs`, but at this pinned commit **no wound prototype
in `wounds.yml` actually uses it** — grep for `!type:WoundStatusEffectBehavior` across the whole file returns zero
hits. It appears to be reserved for wiring up `StatusEffectBurnSlowdown`/`StatusEffectWoundImpairment` (see §2) but
that wiring isn't present yet in this snapshot.

### Cross-references from `wounds.yml` and whether Wolfgate has them

| Reference | Onyx value(s) | In WG? | Evidence |
|---|---|---|---|
| Damage types (`acceptedDamageTypes`, `damageTypes`) | `Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic` | **Yes, all 7** | `WG/Resources/Prototypes/Damage/types.yml` defines all 7 with identical ids. |
| Alert (`FractureProfilePrototype.Alert`) | `BrokenBones` | **No** | `grep -rn "BrokenBones" WG/Resources/Prototypes` → no hits. Must be ported (prototype + `fracture.rsi` texture, §3). |
| Status-effect entities referenced by wound behaviors | none (see above — `WoundStatusEffectBehavior` unused in data) | n/a | No `EntProtoId<StatusEffectComponent>` reference exists anywhere in `wounds.yml` to check. |
| Reagent ids | none — `wounds.yml` is referenced *by* reagents (§6), not the reverse | n/a | — |
| Sprites/textures | **none** | n/a | `WoundPrototype` (`WoundPrototype.cs:13-119`) and `WoundStageDefinition` (`WoundBehaviors.cs:132-152`) have no icon/sprite field at all. Wound severity has no per-wound icon; the only wound-adjacent art is the fracture alert icon (§2/§3) and the species damage-overlay re-skin (§3), neither of which `wounds.yml` itself references. |
| Loc keys (`name`, stage `name`, stage `examineDescription`) | `wound-name-*` (23), `wound-stage-*` (20), `wound-examine-fracture-*`/`wound-examine-frame-*` (8) | **Yes, all present** | See §4 — all resolve inside the checked-out `_Onyx` locale tree, none missing. |

**Verdict for item 1**: every prototype and damage-type reference in `wounds.yml` itself resolves against Wolfgate
content except the single `BrokenBones` alert, which must be ported (small: 1 prototype + 1 texture + 2 Loc lines,
all under permissive license). `wounds.yml` carries zero texture/sprite references directly.

---

## 2. StatusEffects and Alerts (wound/pain/fracture only)

### `Resources/Prototypes/_Onyx/StatusEffects/wounds.yml` (8 lines, 2 prototypes)
```
- type: entity
  parent: StatusEffectSlowdown
  id: StatusEffectBurnSlowdown
  name: burn slowdown

- type: entity
  parent: StatusEffectSlowdown
  id: StatusEffectWoundImpairment
  name: wound impairment
```
Both are bare — no components beyond what `StatusEffectSlowdown` supplies (`StatusEffect` + `MovementModStatusEffect`),
no alert, no Loc key (name is an inline string, not a `LocId`). **Both ids are unreferenced anywhere else in the
sparse checkout** (`grep -rn "StatusEffectBurnSlowdown\|StatusEffectWoundImpairment" .` inside ONYX returns only these
two declaration lines) — they are dead/orphaned prototypes at this commit, presumably reserved for a future
`WoundStatusEffectBehavior` wiring (§1).

`StatusEffectSlowdown` itself is **not** an `_Onyx` prototype — it's upstream, in
`Resources/Prototypes/Entities/StatusEffects/movement.yml:3`, parented from `MobStatusEffectBase`
(`Resources/Prototypes/Entities/StatusEffects/misc.yml:14`). Both depend on the `StatusEffect` and
`MovementModStatusEffect` components from the **StatusEffectNew** system. None of this chain exists in Wolfgate
(confirmed: `WG/Content.Shared/StatusEffectNew` doesn't exist; no entity prototype named `StatusEffectSlowdown` or
`MobStatusEffectBase` in `WG/Resources/Prototypes`). **Porting these 2 prototypes means also porting their upstream
prototype ancestor chain** (`Resources/Prototypes/Entities/StatusEffects/{misc,movement}.yml` — at minimum
`MobStatusEffectBase`, `MobStatusEffectDebuff`, `StatusEffectSlowdown`), not just the two `_Onyx` leaves. This is on
top of D1's StatusEffectNew C# port (13 files, shared-only, confirmed no server/client counterpart exists upstream
either).

### `Resources/Prototypes/_Onyx/StatusEffects/surgery.yml` (5 lines, 1 prototype)
```
- type: entity
  parent: MobStatusEffectBase
  id: StatusEffectSurgicallyMuted
  name: raspy voice
  components:
  - type: RaspyAccent
```
Used by `Content.Shared/_Onyx/Medical/Surgery/SharedSurgerySystem.cs:55` and `.StatusEffects.cs` (Onyx's own surgery
system — **not ported** per D7, phase 1 keeps Shitmed surgery). If this status effect is needed later for Shitmed-step
surgery muting, note `RaspyAccentComponent` **does not exist anywhere in Wolfgate** (`grep -rln "RaspyAccent"
WG/Content.Shared WG/Content.Server` → no hits) and is also outside every sparse-checkout path in ONYX, so its
definition could not be verified here at all — flagged as absent from sparse checkout, source unconfirmed.

### `Resources/Prototypes/_Onyx/Alerts/alerts.yml` (85 lines) — wound/pain/fracture entries only
Only **one** entry is wound-relevant: `BrokenBones` (`alerts.yml:34-40`):
```
- type: alert
  id: BrokenBones
  icons:
  - sprite: /Textures/_Onyx/Interface/Alerts/fracture.rsi
    state: brokenbones
  name: alerts-broken-bones-name
  description: alerts-broken-bones-desc
  # no `category:` — uses the default grouping
```
The other 5 entries in this file (`ModsuitPower`, `Centered`, `HierophantBeat`, `DragonPower`, `SneakAttack`,
`LossOfSurprise`) are modsuit/ninjutsu/tile-movement content, unrelated to wounds.

**No alert exists for pain.** `PainSystem.cs` never calls `AlertsSystem` — pain shock (§ handoff "Pain") manifests
as a forced scream emote + jitter + stun + a `PainShockTargetComponent.Armed`/`AdrenalineEnds` state, not a
UI alert icon. Grepping every `Resources/Prototypes/Alerts/*.yml` and `_Onyx/Alerts/*.yml` for `Pain` or `Shock` in an
`id:` field returns nothing.

Wolfgate has **no** `BrokenBones` alert and **no** `AlertCategory` collision risk (the categories `TileMovement` and
`Ninjutsu` used by the other entries in this file aren't wound-related, so item 2's "alertHiddenTreatments"/category
question is moot for wounds — `FractureProfilePrototype` doesn't set a category on `BrokenBones`).

---

## 3. Textures

### `Resources/Textures/_Onyx/Wounds/**` — 156 files, 2 RSI folders
Both are **damage-overlay re-skins of the standard humanoid sprite layer**, not wound-specific icons (confirmed:
`WoundPrototype`/`WoundStageDefinition` have no icon field, §1). They're wired in via
`Resources/Prototypes/Body/species_base.yml:26-35`:
```yaml
damageOverlayGroups:
  Brute:
    sprite: _Onyx/Wounds/brute_damage.rsi   # <Onyx-PartDamageVisuals-edited>
    color: "#FF0000"
  Burn:
    sprite: _Onyx/Wounds/burn_damage.rsi    # <Onyx-PartDamageVisuals-edited>
```
i.e. Onyx repointed the vanilla `damageOverlayGroups` sprite path away from the stock
`Textures/Mobs/Effects/{brute,burn}_damage.rsi` to their own re-drawn copies. **Wolfgate already ships the stock
assets** at `WG/Resources/Textures/Mobs/Effects/brute_damage.rsi` and `burn_damage.rsi` (37 states vs. Onyx's 78 —
Onyx's set is a 4-directional, finer-grained redraw).

| RSI | States | License | Copyright | Flag |
|---|---|---|---|---|
| `brute_damage.rsi` | 77 (11 part names × 7 damage-% steps) + meta | `CC-BY-SA-3.0` | "Drawn by Ubaser." | Clean — same artist/license as WG's existing copy. |
| `burn_damage.rsi` | 77 + meta | `CC-BY-SA-3.0` | "Taken from Citadel-Station-13 …, 100 drawn by Ubaser." | Clean — attribution present, permissive share-alike, not NC. |

**Neither RSI is CC-BY-NC-SA or unlicensed — nothing here needs to be skipped.** Porting them is optional/cosmetic:
they only matter if Wolfmed wants Onyx's finer 4-direction damage-overlay art instead of Wolfgate's existing
non-directional version; the wound *system* has no functional dependency on them.

### Alert icon: `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi`
Single state `brokenbones.png`. `meta.json`: license `CC-BY-SA-3.0`, copyright "Taken from tgstation, redrawn by
darkrell". Clean, no NC restriction — **must be ported** (it's the only art the `BrokenBones` alert from §2 needs).

The other RSIs under `_Onyx/Interface/Alerts/` (`bloodcounter`, `bloodrite`, `centered`, `dragonpower`,
`eternaldarkness`, `hierophantbeat`, `item_offer`, `modpower`, `plasma_counter`, `queen_finder`, `xenomorph_*`) back
the non-wound alerts from §2 and are out of scope.

**Licensing summary: zero blocked assets.** Every wound-adjacent texture found (158 files across 3 RSIs) is
`CC-BY-SA-3.0` with clear attribution. No `CC-BY-NC-SA` or missing-license file was found anywhere under
`_Onyx/Wounds` or the fracture alert icon.

---

## 4. Locale

All paths under `Resources/Locale/en-US/_Onyx/`. Key counts are top-level Fluent message ids (multi-line
plural/attribute blocks count once).

| File | Lines | Top-level keys | Relevance |
|---|---|---|---|
| `prototypes/wounds/wounds.ftl` | 44 | 43 | `wound-name-*` (23) + `wound-stage-*` (20) — every name/stage-name `wounds.yml` needs. **Complete, nothing missing.** |
| `medical/health-examinable.ftl` | 49 | 36 | Pain-color spans, part summary/injury/severity/bleeding/scar strings **and** the 8 `wound-examine-fracture-*`/`wound-examine-frame-*` keys `WoundStageDefinition.ExamineDescription` needs (lines 38-45). |
| `medical/fractures.ftl` | 2 | 2 | Just `alerts-broken-bones-name`/`-desc` — the `BrokenBones` alert's Loc keys live here, **not** in `alerts/alerts.ftl` despite the prototype being declared there. |
| `medical/surgery.ftl` | 285 | 277 | Onyx's own surgery-system strings (step/effect descriptions for ~70+ surgery prototypes) — almost entirely out of scope under D7 (Shitmed surgery kept); only relevant if a specific wound-surgery string is reused verbatim later. |
| `medical/tourniquet.ftl` | 3 | 3 | `tourniquet-selected-part-missing`, `tourniquet-no-bleeding`, `tourniquet-applied` — needed for phase-4 Tourniquet port. |
| `medical/health-analyzer-component.ftl` | 16 | 16 | Health analyzer UI strings (wound diagnostics display, phase 4). |
| `medical/medical_patch.ftl` | 8 | 6 | Makeshift patch entity + do-after strings (phase 4 treatment). |
| `medical/autosurgeon.ftl` | 2 | 2 | Autosurgeon ready/used strings — Shitmed already has an Autosurgeon per the handoff baseline; check for overlap before porting. |
| `targeting/part-status.ftl` | 1 | 1 | `part-status-self-examine-title` only. |
| `targeting/targeting.ftl` | 49 | 45 | Targeting-doll/body-part UI strings. |
| `alerts/alerts.ftl` | 5 | 4 | `alerts-plasma-*`, `alerts-queen-finder-*` — xenomorph strings, **not** wound-related despite the filename (the wound-relevant `BrokenBones` keys are in `fractures.ftl`, above). |

**StatusEffectNew's own locale need**: neither `StatusEffectBurnSlowdown` nor `StatusEffectWoundImpairment` carries a
`StatusEffectAlertComponent` (§2), so in this snapshot **no upstream `alerts/status-effects.ftl`-style key is
actually consumed** by the wound status effects — the handoff's expectation that StatusEffectNew needs such keys
doesn't materialize for these two specific prototypes. (The generic StatusEffectNew alert-display keys would only
matter once/if `WoundStatusEffectBehavior` is actually wired up in `wounds.yml`, or for other, non-wound StatusEffectNew
consumers ported later.)

**Total relevant to phase 1 (wounds/fractures/pain/health-examinable/targeting) and phase-4 add-ons (tourniquet,
medical patch, surgery-adjacent, health-analyzer): ~11 files, ~440 keys**, all present in the sparse checkout, nothing
missing.

---

## 5. Tests — `Content.IntegrationTests/Tests/_Onyx/{Wounds(8), Medical(2), Body(2)}`

All 12 files use the same harness Wolfgate already has: `GameTest` base class (`WG/Content.IntegrationTests/Fixtures/
GameTest.cs` exists with matching `Pair`/`WaitAssertion`/`WaitIdleAsync`/`CreateTestMap`/`[TestPrototypes]` shape —
confirmed by direct comparison), so the *harness* pattern ports with no changes needed. Portability issues below are
about **prototype/API dependencies**, not test infrastructure.

### Wounds (8 files)

**`WoundDamageFoundationTest.cs`** (718 lines, largest file) — `[TestOf(WoundDamageRoutingSystem)]`. Defines 10
`[TestPrototypes]` entities (a `WoundFoundationBody` with `Body/Damageable/MobState/PainShockTarget/Injurable/
WoundHost/InitialBody`, 4 body-part entities, 4 armor variants, a non-`WoundHost` `WoundFoundationVanillaBody`, an
attacker with `Targeting`). 10 `[Test]` methods: targeting-to-bodypart conversion + `TargetResolverSystem` resolution;
`TargetingSnapshotSystem` capture/stability across thrown items; direct/carrier/targeted/distributed damage routing
through `WoundDamageRoutingSystem` (`TryApplyDamage`, `TryApplyPartDamage`, `TryApplyCarrierDamage`,
`TryApplyTargetedDamage`, `TryApplyDistributedDamage`); systemic-vs-localized damage split via
`SystemicDamageComponent`; wound creation/merge/heal/reattach via `WoundSystem.CreateOrMergeWound`/`ChangeSeverity`;
restricted `bodyPartProfile` gating (`acceptedDamageTypes`/`supportedWounds`); locational/symmetric armor coverage
applied exactly once; non-`WoundHost` entities fall back to vanilla `DamageableSystem.TryChangeDamage` +
`SharedArmorSystem`; and a full pain lifecycle (`PainSystem.GetPain/SetPain/ChangePain/RecoverPain/SuppressPain/
ClearPainSuppression/DecayPainSuppression`, pain-shock stun/adrenaline via `PainShockTargetComponent`, rejuvenate
reset). **Portability: high.** Every dependency (`SharedBodySystem`, `DamageableSystem`, `InventorySystem`,
`SharedArmorSystem`, `TargetingComponent`, `SystemicDamageComponent`) is Wolfgate/Shitmed-native or part of the wound
port itself; only `#pragma warning disable CS0618` markers hint at the legacy `Damageable` projection the routing
system maintains for interop, which is exactly D2's damage-bridge concern.

**`WoundBleedingTest.cs`** (316 lines) — `[TestOf(WoundBleedingSystem)]`. 6 tests: bleeding-rate projection onto
`BloodstreamComponent.BleedAmount`, treatment via `WoundBleedingSystem.SetTreatment(BleedingTreatment.Bandaged)`,
`ReduceBleeding`, wound reopening at damage thresholds, a `Tourniquet` entity stopping only its targeted part
(`TourniquetSystem.Apply`), traumatic-amputation stump bleeding, and CVar-gated automatic clotting
(`WoundsBleedingAutoStop*`). **Portability: medium.** Imports `using Content.Shared.Medical.Healing;` and calls
`entityManager.GetComponent<BloodstreamComponent>`/`System<BloodstreamSystem>()` as if shared — **but in Wolfgate,
`BloodstreamComponent`/`BloodstreamSystem` live in `Content.Server.Body.{Components,Systems}`, not
`Content.Shared`** (confirmed: `WG/Content.Server/Body/Components/BloodstreamComponent.cs`,
`WG/Content.Server/Body/Systems/BloodstreamSystem.cs`; no `Content.Shared` counterpart exists). The `using` line
won't compile against Wolfgate and needs a `// WOLFGATE` namespace fix; the calls themselves are fine since tests run
server-side anyway. Also spawns a `Tourniquet` entity prototype, which **does not exist in Wolfgate**
(`grep -rln "id: Tourniquet$" WG/Resources/Prototypes` → no hits) — needs the phase-4 Tourniquet port first.

**`WoundFractureTest.cs`** (162 lines) — `[TestOf(WoundFractureSystem)]`. 3 tests: deterministic grade boundaries
from `FractureProfilePrototype` thresholds (`WoundFractureSystem.GetGrade`, a static method), post-armor fracture
creation + `TryMend`, and effect refresh (`MovementSpeedModifierComponent.WalkSpeedModifier`,
`FractureEffectSystem.GetDurationMultiplier`) on treat/heal/detach. **Portability: high** — only touches
`SharedBodySystem`, `InventorySystem`, `MovementSpeedModifierComponent`, all present in Wolfgate.

**`WoundHealingTest.cs`** (258 lines) — `[TestOf(WoundHealingSystem)]`. 5 tests: healing item
(`Content.Shared.Medical.Healing.HealingComponent` + `damageContainers`) resolving to the correct part via
`WoundHealingSystem.ResolveHealingPart`/`TryApplyHealing`, cross-body/incompatible-container rejection, untargeted
healing spread across all parts, exact-target resolution (`TargetResolverSystem.TryResolveExact`), and a
targeted-repair event pair (`ResolveRepairPartEvent`/`ValidateRepairPartEvent`) from
**`Content.Shared._Onyx.Repairable`** — a 4-file dependency (`RepairableComponent.TreatmentCapabilities.cs`,
`TargetedRepairSystem.cs`, `WelderComponent.RepairModes.cs`, `WelderRepairSystem.cs`) that sits **outside every
sparse-checkout path** and wasn't in the handoff's dependency map; read via `git show HEAD:<path>` since git can
resolve tree objects from the blobless clone even outside the sparse set. It's small and self-contained (extends
upstream `Content.Shared.Repairable` with per-part targeting via `TargetResolverSystem`/`SharedBodySystem`).
**Portability: medium** — same `Content.Shared.Medical.Healing` namespace-mismatch issue as `WoundBleedingTest.cs`
(Wolfgate's `HealingComponent`/`HealingSystem` are `Content.Server.Medical.{Components,}`, confirmed), plus the
previously-unlisted `_Onyx/Repairable` dependency.

**`WoundScarTest.cs`** (85 lines) — `[TestOf(WoundScarSystem)]`. 1 test: scar threshold (19 vs 20 severity),
`WoundState.Scarred` blocking `TreatWound`/`RemoveWound`, scar surviving detach/reattach, rejuvenate clearing scars.
**Portability: high** — pure wound-system API, no Onyx-surgery or server-only-namespace dependencies.

**`WoundSurgeryScarTest.cs`** (81 lines) — no `[TestOf]`. 1 test: `CCVars.SurgeryScarChance` gating scar creation on
`SurgicalIncisionWound` close, driven by raising a raw `SurgeryStepEvent` at a `SurgeryCloseIncisionEffect` component.
**Portability: low.** `SurgeryStepEvent`/`SurgeryCloseIncisionEffect` are Onyx's **own** surgery system
(`Content.Shared._Onyx.Medical.Surgery`) — explicitly excluded by D7. Cannot be ported as-is; the *scenario*
(scar-on-close, gated by `surgery.scar_chance`) would need to be re-expressed once wound surgeries land on Shitmed's
step system (handoff phase 4).

**`WoundSurgeryTest.cs`** (147 lines) — `[TestOf(WoundSurgerySystem)]` (server-side class,
`Content.Server/_Onyx/Medical/Surgery/WoundSurgerySystem.cs`, not listed among the 22 `Content.Shared/_Onyx/Wounds`
files). 2 tests: selecting the highest-severity wound matching a `SurgeryHasWoundCondition`, applying
`SurgeryClampBleedingEffect`/`SurgeryMendFractureEffect` via raw `SurgeryStepEvent`. **Portability: low** — same D7
conflict as above; every fixture and API surface here is Onyx's own surgery, not Shitmed's.

**Wounds test summary**: 5 of 8 (`WoundDamageFoundationTest`, `WoundFractureTest`, `WoundScarTest`, plus the bulk of
`WoundBleedingTest`/`WoundHealingTest` once namespace fixes land) are high-to-medium portability and validate exactly
the phase-1/2/3 mechanics (routing, bleeding, fractures, scars, amputation). 2 of 8
(`WoundSurgeryTest`, `WoundSurgeryScarTest`) are low-portability because they test Onyx's excluded surgery system —
their *scenarios* are worth re-deriving against Shitmed steps later, but the files themselves can't be vendored.

### Medical (2 files)

**`HealthAnalyzerPartDamageTest.cs`** (163 lines) — `[TestOf(HealthAnalyzerSystem)]`. A `[TestCase]`-based unit test
for `HealthAnalyzerControl.IsDangerousBloodLevel` (client-side static, no server needed) plus 2 integration tests:
`HealthAnalyzerSystem.BuildPartDamage` (per-part damage snapshot, Groin mirrors Chest, detached parts excluded) and
`BuildWoundDiagnostics` (bleeding rate, fracture grade/treatment, scar count per part, returns `null` for a
non-`WoundHost` body). **Portability: high** for the `BuildPartDamage` half (touches only `SharedBodySystem`/
`DamageableSystem`); `BuildWoundDiagnostics` depends on wound-system types directly and ports once the core wound
system is in. Wolfgate already has `Content.Server/Medical/HealthAnalyzerSystem.cs` (37 Onyx-marker lines per the
handoff) as the hook point.

**`SurgeryStepSequencePrototypeTest.cs`** (91 lines) — no `[TestOf]`, pure prototype-validation (no entity spawns):
every `SurgeryComponent`-bearing `EntityPrototype` must have exactly one unconditional step section and no empty
sections; the 8 cybernetic-limb-attachment surgeries must each have an `organic` and a `cybernetic` conditional
section gated by `CyberneticsComponent`. **Portability: none for phase 1.** This validates **Onyx's own** ~70+
surgery prototype set (`SurgeryComponent`, `Content.Shared._Onyx.Medical.Surgery`), which D7 excludes entirely. Not
applicable until/unless Onyx surgery itself is later reconsidered.

### Body (2 files)

**`BodyConsequencesTest.cs`** (88 lines) — no `[TestOf]`, spawns Wolfgate's own `MobHuman`. 3 tests: detaching the
Groin cascades to remove Leg/Foot children and their inventory slots (shoes/socks/underwear); detaching both Feet
still leaves the Socks slot (legs, not feet, gate it); detaching one Leg forces `StandingStateSystem.IsDown` and
cancels a `StandUpAttemptEvent`. **Portability: very high — this test has zero Onyx-wound dependencies.** It only
calls `SharedBodySystem.{TryDetachPart, BodyHasPartType, GetBodyChildrenOfType}`, `InventorySystem.HasSlot`, and
`StandingStateSystem`, all of which the handoff's dependency map already claims exist in Wolfgate's
`SharedBodySystem.Parts.cs`/`.Body.cs` under the same names — this test is close to a direct, mechanical port and
doubles as a good signature-compatibility check for that claim.

**`TransplantCompatibilityPrototypeTest.cs`** (74 lines) — no `[TestOf]`, pure prototype validation. Every concrete
`BodyPartComponent`/`DetachableOrganComponent` prototype must carry a `TransplantCompatibilityComponent`; `Mechanical`
recipients must reject `Biosynthetic` transplants; 8 named cybernetic-limb prototypes must resolve to the
`Cybernetic` compatibility profile despite inheriting from organic parts. **Portability: low for phase 1.** This is
Onyx's own organ/limb-transplant framework (`Content.Shared._Onyx.Body`), explicitly out of scope per D8 (only
organ-damage/functional-organ pieces are wanted, not the Nubody-adjacent transplant-compatibility glue) — relevant
again only in phase 5 (species/cybernetics) if that framework is ever adopted.

---

## 6. Reagents with wound-treatment effects

### Onyx reagent files with `TreatmentCapabilities`/`SuppressPain`/`MendFractures`/`ModifyBleed`
`Resources/Prototypes/_Onyx/Reagents/Medicine/first_aid.yml`, `.../Medicine/medicine.yml`,
`.../Narcotics/opioids.yml`, and 3 upstream files (`Reagents/medicine.yml`, `Reagents/narcotics.yml`,
`Reagents/Consumable/Drink/alcohol.yml`) carry Onyx-added `!type:ModifyBleed`/`!type:SuppressPain` metabolism steps.

Confirmed reagents (from `first_aid.yml` and `medicine.yml`, read in full):

| Reagent | Wound-relevant effects |
|---|---|
| `Stasizium` | `ModifyBleed -2`, `EvenHealthChange` (5 groups, -20 each), `MendFractures amount:10` (Hairline–Comminuted) |
| `SilverSulfadiazine` | Burn-type `HealthChange` (Heat/Cold/Shock/Caustic) |
| `StypticPowder` | `HealthChange` (Brute types) + `ModifyBleed -2` |
| `MinersSalve` | `HealthChange` (Burn/Brute groups, Bloodloss) |
| `Osteogen` | `MendFractures amount:1`, `wounds:[BoneFractureWound]`, up to `Simple` grade |
| `Mitotrophin` | `HealthChange` + `ModifyBleed -0.5` |
| `Ibuprofen` | `SuppressPain amount:0.5 decay:27s identifier:Ibuprofen recoveryMultiplier:2.5` |
| `Ketorolac` | `SuppressPain amount:0.9 decay:50s` + small `ModifyBleed` (worsening, conditional) |
| `Atropine` | `ModifyBleed -1` (among many other crit-recovery effects) |
| `Tramadol` | `SuppressPain amount:1.25 decay:45s` |
| `Oxycodone` | `SuppressPain amount:2 decay:60s` |

`ModifyBleed` is **not** one of the wound-treatment files in `Content.Shared/_Onyx/Wounds/` — its `EntityEffectSystem`
override lives at `Content.Shared/EntityEffects/Effects/Body/ModifyBleedEntityEffectSystem.cs:12-27`, guarded
`// <Onyx-WoundTreatment-edited>`: it calls `WoundBleedingSystem.ModifyBodyBleeding` for `WoundHostComponent` entities
and falls back to vanilla `BloodstreamSystem.TryModifyBleedAmount` otherwise — i.e. it's an upstream-file edit, not a
new `_Onyx` file, consistent with D5's "shim inside a vendored/upstream file" strategy.

Damage-group names used (`Brute`, `Burn`, `Airloss`, `Toxin`, `Genetic`) all **already exist** in Wolfgate's
`WG/Resources/Prototypes/Damage/groups.yml` with identical membership — **no group-name mismatch**, despite these
being legacy/pre-fork vanilla SS14 group names; Wolfgate kept them.

### The real gap: `HealthChange`/`EvenHealthChange`/`MendFractures`/`SuppressPain` are ECS entity effects
`Content.Shared/_Onyx/Wounds/ReagentTreatmentSystems.cs` and `ReagentTreatmentEffects.cs` extend `HealthChange`,
`EvenHealthChange`, `DistributedHealthChange` via **partial classes** assuming they're
`EntityEffectSystem<TComponent,TEffect>`-based ECS effects living in `Content.Shared.EntityEffects.Effects.Damage`.
Confirmed in Wolfgate: **`HealthChange`/`EvenHealthChange` are old-style, class-based `EntityEffect` subclasses in
`Content.Server/EntityEffects/Effects/{HealthChange,EvenHealthChange}.cs`** — different base class, different
namespace, server-only. There is no `Content.Shared/EntityEffects/EntityEffectSystem<T,U>` generic in Wolfgate at all
(`grep -rln "class EntityEffectSystem" WG/Content.Shared` → no hits) and no `DistributedHealthChange`/
`HealDistributed` counterpart either (matches D5). **The Onyx reagent-treatment partial classes cannot be vendored
verbatim** — this is the concrete instance of D5's "compat layer" need for reagents specifically: either write new
`Content.Server`-side, old-style `EntityEffect` subclasses that call `WoundDamageRoutingSystem.WithTreatmentCapabilities`
(mirroring the logic in `ReagentTreatmentSystems.cs:14-30`), or shim a minimal ECS entity-effect dispatch layer.
`SuppressPainEntityEffect.cs` is simpler — it's its own effect type (`EntityEffectSystem<PainComponent,
SuppressPain>`), not a partial-class extension of an existing vanilla effect, so it only needs the generic
`EntityEffectSystem<T,U>` base itself, not a rewrite of an existing Wolfgate class.

### Mapping targets in Wolfgate
- `WG/Resources/Prototypes/Reagents/medicine.yml` and `WG/Resources/Prototypes/Reagents/narcotics.yml` are the
  existing upstream reagent files these Onyx reagents would sit alongside (Onyx's own layout mirrors this: top-level
  `medicine.yml`/`narcotics.yml` plus a `Medicine/`, `Narcotics/` subfolder split).
- **Two reagent id collisions found** if Onyx's `_Onyx/Reagents/Medicine/first_aid.yml`/`chemicals` content is
  vendored verbatim: `Stasizium` already exists at `WG/Resources/Prototypes/_Goobstation/Reagents/medicine.yml:2`
  (different definition: `metabolisms: Medicine:` not `Bloodstream:`, `ModifyBleedAmount` not `ModifyBleed`) and
  `SalicylicAcid` already exists at `WG/Resources/Prototypes/_NF/Reagents/chemicals.yml:2` (different definition,
  no wound effects). Both `_Goobstation` and `_NF` are active prototype directories in this Wolfgate build (per
  project memory, `_NF` medical bounties are already a live Wolfgate feature), so a duplicate `- type: reagent / id:
  Stasizium` (or `SalicylicAcid`) **will fail prototype loading** — this needs a rename or a "skip, reuse Wolfgate's
  existing reagent" decision before porting, not a silent vendor.

---

## 7. CCVars

### `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` (full contents, 27 lines)
```csharp
public static readonly CVarDef<bool> WoundsBodyPartFunctionalityEnabled =
    CVarDef.Create("wounds.body_part_functionality_enabled", false, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<bool> WoundsBleedingAutoStopEnabled =
    CVarDef.Create("wounds.bleeding_auto_stop_enabled", true, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<float> WoundsBleedingAutoStopSecondsPerSeverity =
    CVarDef.Create("wounds.bleeding_auto_stop_seconds_per_severity", 2f, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<float> WoundsBleedingAutoStopMinSeconds =
    CVarDef.Create("wounds.bleeding_auto_stop_min_seconds", 5f, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<float> WoundsBleedingAutoStopMaxSeconds =
    CVarDef.Create("wounds.bleeding_auto_stop_max_seconds", 120f, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<float> ExplosionLimbDamageVariation =
    CVarDef.Create("explosion.damage_variation", 2f, CVar.SERVERONLY);
public static readonly CVarDef<float> ExplosionWoundMultiplier =
    CVarDef.Create("explosion.wounding_multiplier", 4f, CVar.SERVERONLY);
```

### `Content.Shared/_Onyx/CCVar/CCVars.Surgery.cs` (full contents, 15 lines)
```csharp
public static readonly CVarDef<float> SurgeryScarChance =
    CVarDef.Create("surgery.scar_chance", 0.35f, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<bool> SurgerySelfEnabled =
    CVarDef.Create("surgery.self_enabled", false, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<float> SurgerySelfMultiplier =
    CVarDef.Create("surgery.self_multiplier", 3f, CVar.SERVER | CVar.ARCHIVE | CVar.REPLICATED);
```

### Collision check against `WG/Content.Shared/CCVar/*.cs` (49 files) and `WG/Content.Shared/_WF/CCVar/*.cs` (2 files)
Checked all 10 field names (`WoundsBodyPartFunctionalityEnabled` … `SurgerySelfMultiplier`) and all 10 string keys
(`"wounds.body_part_functionality_enabled"` … `"surgery.self_multiplier"`) against every existing WG CCVar file.
**Zero collisions on either axis.** `CCVars.Explosion.cs` exists in Wolfgate already (for unrelated explosion
mechanics) but doesn't define `damage_variation` or `wounding_multiplier` under the `explosion.*` namespace, so no
conflict there either — these two would need to either land in a new `CCVars.Wounds.cs`/`CCVars.Surgery.cs` (matching
Onyx's file split, vendored under `_Onyx/CCVar` per D6) or be folded into the existing `_WF` convention; either way,
**no renames are forced by naming collisions.**

---

## Top 5 findings (see summary below) and full blocker list

1. **Zero texture-licensing blockers.** All 158 wound-adjacent texture files (`brute_damage.rsi`, `burn_damage.rsi`,
   `fracture.rsi`) are `CC-BY-SA-3.0` with clear attribution (Ubaser / Citadel-Station-13 / tgstation-via-darkrell).
   Nothing needs to be skipped or replaced.
2. **Reagent id collisions are real and will break prototype loading if not handled**: `Stasizium`
   (`_Goobstation/Reagents/medicine.yml`) and `SalicylicAcid` (`_NF/Reagents/chemicals.yml`) already exist in
   Wolfgate with different definitions than Onyx's versions.
3. **The reagent-treatment C# layer cannot be vendored verbatim**: Onyx's `ReagentTreatmentSystems.cs` assumes an ECS
   `EntityEffectSystem<T,U>`-based `HealthChange`/`EvenHealthChange` that doesn't exist in Wolfgate — Wolfgate's
   versions are old-style, class-based, and server-only, at `Content.Server/EntityEffects/Effects/{HealthChange,
   EvenHealthChange}.cs`. This confirms and locates precisely the compat-layer work D5 anticipated.
4. **2 of 8 wound tests and 1 of 2 medical tests are not portable at all**: `WoundSurgeryTest.cs`,
   `WoundSurgeryScarTest.cs`, and `SurgeryStepSequencePrototypeTest.cs` exercise Onyx's own excluded surgery system
   (D7). The other 9 of 12 test files range from directly portable (`BodyConsequencesTest.cs`, zero wound
   dependencies) to needing a small namespace fix (`WoundBleedingTest.cs`/`WoundHealingTest.cs`'s
   `Content.Shared.Medical.Healing` import, which is `Content.Server` in Wolfgate).
5. **`wounds.yml` itself is nearly self-sufficient**: of everything it references, only the `BrokenBones` alert
   (1 prototype + 1 already-licensed texture + 2 Loc lines) is missing from Wolfgate; every damage type it uses
   already exists, and it carries no direct texture/sprite references at all.

Secondary items worth flagging: `WoundStatusEffectBehavior` is unused dead code in this pinned `wounds.yml` (no
prototype invokes it); the two wound-status-effect prototypes it would drive depend on an upstream `StatusEffectSlowdown`
→ `MobStatusEffectBase` prototype chain that also needs porting alongside the StatusEffectNew C#; `RaspyAccentComponent`
(used by `surgery.yml`'s `StatusEffectSurgicallyMuted`) exists in neither Wolfgate nor the ONYX sparse checkout and
its source could not be verified; and `Content.Shared._Onyx.Repairable` (4 files) is a previously-unlisted dependency
of `WoundHealingTest.cs`'s repair-targeting scenario, found outside the sparse checkout via `git show`.
