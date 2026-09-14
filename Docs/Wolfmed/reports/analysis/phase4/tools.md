# Wolfmed Phase 4 — Tourniquet, Medical Patch, Explosion Amputation, Guidebook

**Scope:** P4-2 (tourniquet, medical patch), P4-6 (explosion amputation hook), P4-7 (guidebook).
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`, sparse checkout at `C:\tmp\onyx`.
**Wolfgate tree (WG):** `C:\Users\jzo12\Documents\GitHub\Wolfgate\.claude\worktrees\rules-motd-updates-11c89c`, HEAD `6329d204e3` (phase 3 committed, phases 1–3 all in tree).
**Method:** every symbol below was read directly from the pinned Onyx tree and the current WG tree in this session; nothing is carried over from the phase‑1 evidence reports (`entityeddfects-gap.md`, `medical-extras.md`) without being re‑verified, and two of that evidence's conclusions are corrected below (§1.4, §2.3).

## 0. Summary of findings and decisions needed

1. **Tourniquet has an entity-prototype collision the phase‑1 evidence report missed.** WG already ships an entity `id: Tourniquet` (`Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml:263`) using the generic `Healing` component as a weak stand‑in for the real mechanic. Onyx's own repository independently reached the identical conclusion — its `healing.yml` patches the **same upstream entity, same id**, swapping in `- type: Tourniquet` under an `<Onyx-TargetedTourniquet-edited>` tag. **Recommendation: don't vendor a new entity; replace WG's `Tourniquet`'s `- type: Healing` block with Onyx's `- type: Tourniquet` in place**, matching what Onyx itself did line‑for‑line. See §1.4.
2. **`TourniquetSystem` must move to `Content.Server`**, not stay in `Content.Shared` as Onyx has it — it calls `WoundBleedingSystem` directly, and D13 already put that class in `Content.Server`. `TourniquetComponent` (data + the DoAfter event) stays shared, matching the split D13 already established for `WoundHealingSystem`/`HealingComponent`. See §1.3.
3. **`GroupHealSpecifier` is not a Medical Patch dependency.** It has zero consumers in `MedicalPatchComponent`/`MedicalPatchSystem`; every consumer in the whole Onyx tree is the Vampire antagonist feature (`_Onyx/Vampire/**`), which is out of Wolfmed's scope entirely. **Recommendation: do not port it in P4-2**; the WP11 line in `PLAN.md` bundled it in without a real dependency edge. See §2.3.
4. **Explosion amputation is not fully "out" today — it's partially live already**, and the plate‑protection gap identified in `PLAN3.md` P3-D3 is fixable with a small, low‑risk addition entirely inside the already‑vendored `WoundDamageRoutingSystem.cs`. I recommend **proceeding in phase 4** (reversing the phase‑3 "stays phase 6" default) with a one‑hook, ~10‑line change plus a regression test. This is the item most worth a deliberate user go/no‑go given it touches live‑fire combat balance. See §3.
5. **Onyx's guidebook `Surgery` entry collides by id with WG's own `Surgery` guide entry** (Shitmed's, already shipping). Skip it — Onyx's copy documents Onyx's own unported surgery system anyway (D7). Two of Onyx's five referenced guide XML files (`BodyPartDamage.xml`, `Surgery.xml`, `Virology.xml` — three, not two) are outside the sparse checkout or out of scope; only `Wounds.xml` and `WoundTreatment.xml` exist and are relevant. See §4.

No blockers. All four items are buildable now on top of the phases 1–3 tree with no further phase‑1..3 gaps.

---

## 1. Tourniquet

### 1.1 Onyx source, verbatim

`Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs` (no license header):

```csharp
namespace Content.Shared._Onyx.Medical.Tourniquet;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TourniquetComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public SoundSpecifier? BeginSound;

    [DataField]
    public SoundSpecifier? EndSound;
}

[Serializable, NetSerializable]
public sealed partial class TourniquetDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public TourniquetDoAfterEvent(NetEntity part) { Part = part; }
}
```

`Content.Shared/_Onyx/Medical/Tourniquet/TourniquetSystem.cs` (no license header) — full symbol table:

| Symbol used | Onyx type | WG status |
|---|---|---|
| `[Dependency] SharedAudioSystem _audio` | vanilla | present, unchanged |
| `[Dependency] SharedBodySystem _body` | vanilla | present; `BodyHasChild` used at `:~119` — **SAME**, `Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:978 public bool BodyHasChild(...)`, already called by the phase‑1 `WoundHealingSystem.IsCompatiblePart` |
| `[Dependency] SharedDoAfterSystem _doAfter` | vanilla | present, unchanged |
| `[Dependency] INetManager _net` | vanilla | present; becomes **dead** once the system moves server‑only (§1.3) |
| `[Dependency] SharedPopupSystem _popup` | vanilla | present, unchanged |
| `[Dependency] TargetResolverSystem _targeting` | `Content.Shared._Onyx.Targeting` | **MISSING by design (D10)** — Onyx's own Targeting/resolver stack is not ported. Map to `Content.Shared._WF.Wolfmed.Targeting.WoundTargetResolver`, which the phase‑1 plan built for exactly this (`PLAN.md` §2.13). `WoundTargetResolver.TryResolveExact(EntityUid body, TargetBodyPart target, out EntityUid part)` — **method name and signature match Onyx's call site verbatim**: `WG/Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs:60`. One-line dependency swap. |
| `[Dependency] WoundBleedingSystem _bleeding` | `Content.Shared._Onyx.Wounds` in Onyx | **DIFFERENT namespace of residence**: WG's copy lives at `Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs` (class still declares `namespace Content.Shared._Onyx.Wounds;`, per D6) — **server‑only per D13**. This is the reason the whole system must relocate; see §1.3. `SetTreatment(Entity<WoundComponent?>, BleedingTreatment)` at `:131`, `GetPartRate(Entity<WoundableComponent?>)` at `:273` — **SAME** signatures Onyx's calls expect. |
| `[Dependency] WoundDamageRoutingSystem _damage` | `Content.Shared._Onyx.Wounds` | **SAME**, shared, unmodified call: `TryApplyPartDamage(EntityUid body, EntityUid part, DamageSpecifier damage, EntityUid? origin = null, bool ignoreResistances = false, bool healWounds = true)` at `WG/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:240` — exact match to `_damage.TryApplyPartDamage(body, part, tourniquet.Comp.Damage, args.Args.User)`. |
| `[Dependency] WoundSystem _wounds` | `Content.Shared._Onyx.Wounds` | **SAME**, shared, `GetWounds(Entity<WoundableComponent?> part)` at `WG/Content.Shared/_Onyx/Wounds/WoundSystem.cs:155` — exact match. |
| `TargetingComponent`/`.Target` | `Content.Shared._Onyx.Targeting.TargetingComponent` | **MISSING by design (D10)** — that type registers as `"Targeting"`, the exact name Shitmed's own component already uses, and porting it would crash `ComponentFactory` at boot. Use WG's own `Content.Shared._Shitmed.Targeting.TargetingComponent` (`WG/Content.Shared/_Shitmed/Targeting/TargetingComponent.cs:11`), which has the **identical field name and type**, `public TargetBodyPart Target = TargetBodyPart.Torso;` (`:15`). One `using` swap, zero logic changes — `TryComp(user, out TargetingComponent? targeting)` and `.Target` read unchanged. |
| `WoundBleedingComponent.CurrentRate` | data field | **SAME** — `WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:216 public float CurrentRate;` |
| `BleedingTreatment.Clamped` | enum | **SAME** — `WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:272 public enum BleedingTreatment`, `Clamped` maps to a `0f` bleed multiplier at `WoundBleedingSystem.cs:469` |

Behaviour (`TryStart`→`CanApply`→do‑after→`OnDoAfter`→`Apply`) needs **zero logic changes**, only the dependency/using swaps above, all markable `// WOLFGATE` inside the vendored file.

### 1.2 D13: where must `TourniquetSystem` live, and what changes

D13 put `WoundBleedingSystem`, `WoundInternalBleedingSystem`, `OrganDamageSystem`, `WoundHealingSystem` in `Content.Server` because all four mutate state that's server‑authoritative and are internally `!_net.IsServer`‑gated in Onyx's own source. `TourniquetSystem.CanApply` calls `_bleeding.GetPartRate(part)` — a **compile‑time** dependency on a type that, post‑port, only exists in the `Content.Server` assembly. `Content.Shared` cannot reference `Content.Server` types, so **`TourniquetSystem.cs` cannot compile in `Content.Shared` as Onyx has it.**

`CanApply` is called from `TryStart`, which is itself called from the `UseInHandEvent`/`AfterInteractEvent` handlers — i.e. the *entire* class needs the server‑only dependency, not just `Apply`. There is no way to split this one system as was done for `HealingComponent`/`HealingSystem` (data stays shared, logic moves server) without moving the whole `TourniquetSystem` class.

**This has direct precedent already in the tree**: `Content.Server/Medical/HealingSystem.cs` is a `Content.Server`-only class that itself subscribes directly to `UseInHandEvent`/`AfterInteractEvent` on a shared component (`HealingComponent`) — this is completely normal in this codebase, not a special case. Consequence: the item's "use" input has no client‑side prediction (the click round‑trips to the server before anything visible happens), same as every existing healing item today.

**Placement (confirmed against D6 layout convention, matching WoundHealingSystem/HealingComponent's existing split):**

| File | Path | Notes |
|---|---|---|
| `TourniquetComponent.cs` (+ `TourniquetDoAfterEvent`) | `Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs` | verbatim, unmodified — needs to stay shared so the DoAfter event networks normally |
| `TourniquetSystem.cs` | **`Content.Server/_Onyx/Medical/Tourniquet/TourniquetSystem.cs`** (relocated from Onyx's `Content.Shared/_Onyx/Medical/Tourniquet/`) | namespace stays `Content.Shared._Onyx.Medical.Tourniquet` per D6 (location moves, Onyx's namespace doesn't); `// WOLFGATE` edits: (a) `using Content.Shared._Onyx.Targeting;` → `using Content.Shared._Shitmed.Targeting;`, (b) `[Dependency] private TargetResolverSystem _targeting` → `WoundTargetResolver _targeting`, (c) drop the now‑dead `INetManager _net` field and its one `if (_net.IsServer) QueueDel(tourniquet);` guard (harmless if kept, but it's always true now — recommend deleting with a one‑line comment) |

Minor deviation to record: audio uses `_audio.PlayPredicted(...)`, which in Onyx's shared‑system version gets genuine client prediction; moved server‑only, it degrades to ordinary server‑fired PVS audio (a few‑tick delay on the begin/end sound, no functional loss). Same class of deviation already recorded elsewhere in the manifest (e.g. stun VFX re‑trigger).

### 1.3 Prototypes — the collision (re‑verifies and corrects `medical-extras.md` §1)

`medical-extras.md` (phase‑1 evidence) said: *"Vendor verbatim under `_Onyx`; no name collision (`git grep -i tourniquet` in WG returns nothing)"* — that check only covered **C# symbols**, not entity prototypes, and it is wrong for prototypes.

**Onyx ships zero entity prototype anywhere in its own tree using `- type: Tourniquet`** (`git grep -n "type: Tourniquet" HEAD` across the whole Onyx repository returns exactly one hit) — and that one hit is Onyx's own edit to the **shared upstream file both forks inherited**:

`ONYX Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml:261-289`:
```yaml
- type: entity
  parent: BaseHealingItem
  id: Tourniquet
  name: tourniquet
  description: Stops bleeding! Hopefully.
  components:
    - type: Tag
      tags:
        - SecBeltEquip
        - Tourniquet
    - type: Sprite
      state: tourniquet
    - type: Item
      heldPrefix: tourniquet
    # <Onyx-TargetedTourniquet-edited>
    - type: Tourniquet
      damage:
        types:
          Blunt: 5 # Tourniquets HURT
          Asphyxiation: 5
      delay: 0.5
      beginSound: {path: "/Audio/Items/Medical/brutepack_begin.ogg", ...}
      endSound: {path: "/Audio/Items/Medical/brutepack_end.ogg", ...}
    # </Onyx-TargetedTourniquet-edited>
```

**WG's own `Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml:262-292`** has the **same `id: Tourniquet`, same `parent: BaseHealingItem`, same name/description/sprite/heldPrefix, same `delay: 0.5`, and the same two sound file paths**, but with `- type: Healing` (`bloodlossModifier: -10`, `damage: {groups: {Brute: 5}, types: {Asphyxiation: 5}}`) instead of `- type: Tourniquet`. This is unmistakably a shared ancestor — WG's item is the pre‑Wolfmed placeholder for the exact same design intent, using the only mechanic available before the wound system existed.

**This is a hard collision if Onyx's file is vendored as a second `id: Tourniquet` entity — RT rejects/overwrites on duplicate prototype IDs of the same type.** It is also functionally redundant: the item already exists, is already stocked in `sec.yml`/`gib.yml` vending, security spawners, `job.yml` belts, `cmo_webbing.yml`, `_NF`/`_Goobstation` fills — 9 files reference the id today.

**Recommendation: edit WG's existing `Tourniquet` entity in place** (same pattern Onyx itself used on the same upstream file), replacing the `- type: Healing` block with Onyx's `- type: Tourniquet` block, one `# WOLFGATE (P4-2)` marked change to WG's own (non‑Onyx, non‑`_WF`) content, zero new entity ids, zero touched fill/vending/spawner files:

```yaml
- type: entity
  parent: BaseHealingItem
  id: Tourniquet
  name: tourniquet
  description: Stops bleeding! Hopefully.
  components:
    - type: Tag
      tags:
        - SecBeltEquip
        - Tourniquet    # WOLFGATE (P4-2): tag Onyx also carries, for storage whitelists
    - type: Sprite
      state: tourniquet
    - type: Item
      heldPrefix: tourniquet
    # WOLFGATE (P4-2): replaces the generic bloodloss-reduction mechanic (Healing.bloodlossModifier)
    # with Onyx's real per-limb bleeding clamp now that Wolfmed's wound system exists.
    - type: Tourniquet
      damage:
        types:
          Blunt: 5
          Asphyxiation: 5
      delay: 0.5
      beginSound: {path: "/Audio/Items/Medical/brutepack_begin.ogg", params: {volume: 1.0, variation: 0.125}}
      endSound: {path: "/Audio/Items/Medical/brutepack_end.ogg", params: {volume: 1.0, variation: 0.125}}
```

Both sound files (`brutepack_begin.ogg`, `brutepack_end.ogg`) already exist in WG at `Resources/Audio/Items/Medical/` — no new audio assets. Both sprite states (`tourniquet`, `tourniquet-inhand-left/right`) already exist at `Resources/Textures/Objects/Specific/Medical/medical.rsi/` — **no new textures at all**, no license work needed here.

**Accepted regression, recorded not engineered around:** Onyx's `TourniquetSystem.CanApply` requires `HasComp<WoundableComponent>(part)`, which only exists on wound‑host body parts. A mob that is **not** a wound host (only Protogen, per the D21 exclusion — every other organic species is a wound host) loses the ability to use a tourniquet at all, where today it gets a weak generic bloodloss reduction via `Healing.BloodlossModifier`. I evaluated and reject dual‑component composition (attaching both `Healing` and `Tourniquet` to the same entity, gated by `HasComp<WoundHostComponent>`) as unjustified complexity: it creates an event‑dispatch‑order dependency between two systems reacting to the same `UseInHandEvent` on the same entity (whichever's handler runs first "wins" the `args.Handled` race, which is not something this codebase makes deterministic by design), for a population of exactly one excluded species. Recommend accepting the narrow loss and recording it, consistent with the project's established pattern for phase‑scoped balance deviations (e.g. §8.6‑7's organ‑damage scope note).

### 1.4 Locale

`Resources/Locale/en-US/_Onyx/medical/tourniquet.ftl` — 3 keys, no locale collisions (checked against WG's existing `medical-item-*` keys, which are namespaced differently):
```
tourniquet-selected-part-missing = The selected body part is missing.
tourniquet-no-bleeding = The selected body part is not bleeding.
tourniquet-applied = The tourniquet stops the bleeding in the selected body part.
```
Port verbatim to the same path. (Onyx also ships a `ru-RU` copy; WG's project convention elsewhere is en‑US only for Wolfmed strings — skip ru‑RU unless the user wants it.)

### 1.5 Medkit / cargo placement

WG's `MedkitAdvancedFilled` and `MedkitCombatFilled` (`Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml:77-97`) currently do **not** include a Tourniquet. Onyx's own equivalent fill file adds it to exactly one kit:

`ONYX Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` (`MedkitAdvancedFilled`, marked `<Onyx-MedkitContents>`):
```yaml
        - id: MedicatedSuture
        - id: RegenerativeMesh
        - id: Bloodpack
          amount: 2
        - id: Tourniquet # <Onyx-MedkitContents>
```
WG's current `MedkitAdvancedFilled` has the first three lines and is missing the fourth. **Recommendation: add `- id: Tourniquet` to `MedkitAdvancedFilled`** (one line, `# WOLFGATE (P4-2, matches Onyx's own MedkitContents edit)`), matching Onyx's own choice exactly. I found no evidence Onyx also seeds `MedkitCombatFilled` with it (that kit's Onyx contents list was not shown adding a Tourniquet in the slice I could reach); leave `MedkitCombatFilled` untouched rather than guess. All the security/vending/belt placements WG already has (`sec.yml`, `gib.yml`, `security.yml` spawners, `job.yml`, `cmo_webbing.yml`) need no changes — the swapped component keeps the exact same id, so every existing reference keeps working unchanged.

### 1.6 Ordered file list, difficulty

| # | File | Action | Difficulty |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs` | new, vendored verbatim | trivial |
| 2 | `Content.Server/_Onyx/Medical/Tourniquet/TourniquetSystem.cs` | new, vendored + 3 `// WOLFGATE` edits (namespace of using, dependency type, drop dead `_net`) | small — mechanical swap, no design work |
| 3 | `Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml` | modify existing `Tourniquet` entity in place | small, one block swap |
| 4 | `Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` | add one line to `MedkitAdvancedFilled` | trivial |
| 5 | `Resources/Locale/en-US/_Onyx/medical/tourniquet.ftl` | new, verbatim | trivial |
| 6 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | un‑skip the previously‑skipped Tourniquet test (`PLAN.md:957`, `PLAN3.md:1001` both flag it "skipped — no such prototype until WP11/phase 4") | small, port Onyx's `TourniquetStopsOnlySelectedPartTest` against the new server‑side system |

Overall difficulty: **small**. No new upstream hooks, no new subscriptions beyond what's already free‑checked in `PLAN.md` §5 (Onyx's own `TourniquetSystem` doesn't add any `SubscribeLocalEvent` pair that isn't already scoped to its own new `TourniquetComponent`, which has zero prior registrations in WG).

---

## 2. Medical Patch

### 2.1 Onyx source — full symbol table

`Content.Server/_Onyx/Medical/MedicalPatchComponent.cs` (no license header, `namespace Content.Server._Onyx.Medical;`):
```csharp
[RegisterComponent]
public sealed partial class MedicalPatchComponent : Component
{
    [DataField] public string SolutionName = "drink";
    [DataField] public FixedPoint2 TransferAmount = FixedPoint2.New(1);
    [DataField] public bool SingleUse;
    [DataField] public string? TrashObject = "UsedMedicalPatch";
    [DataField] public float UpdateTime = 1f;
    [DataField] public TimeSpan NextUpdate;
    [DataField] public FixedPoint2 InjectAmmountOnAttatch;
    [DataField] public FixedPoint2 InjectPercentageOnAttatch;
}
```

`Content.Server/_Onyx/Medical/MedicalPatchSystem.cs` — dependencies and status:

| Symbol | WG status |
|---|---|
| `IGameTiming` | vanilla, present |
| `SharedSolutionContainerSystem` | **SAME** — `TryGetSolution`, `TryGetInjectableSolution`, `SplitSolution`, `TryAddSolution` all standard vanilla API, present unchanged |
| `ReactiveSystem` | **SAME** — `Content.Shared/Chemistry/ReactiveSystem.cs`, present |
| `StickySystem` / `StickyComponent` / `EntityStuckEvent` / `EntityUnstuckEvent` | **SAME** — `Content.Shared/Sticky/{Systems/StickySystem.cs, Components/StickyComponent.cs, EntityStuckEvent.cs}` all present verbatim (vanilla SS14 feature, unrelated to any fork) |
| `SharedHandsSystem` | **SAME**, present |
| `ISharedAdminLogManager` | **SAME**, present |
| `UnremoveableComponent` | **SAME** — `Content.Shared/Interaction/Components/UnremoveableComponent.cs` |

**No wound‑system dependency of any kind.** `MedicalPatchSystem` never touches `WoundHostComponent`, `WoundableComponent`, `WoundBleedingSystem`, or any `_Onyx/Wounds` type — it is a pure chemistry/interaction feature (a sticky injector that periodically transfers a solution into whatever it's stuck to). This matches and confirms `medical-extras.md`'s original phase‑1 rating ("MedicalPatch — verbatim, no wound dependency at all").

**No name collisions**: `git grep -rn "MedicalPatch"` across WG's `Content.Shared`/`Content.Server`/prototypes returns exactly one hit, a commented‑out line in `_Goobstation/Entities/Clothing/Belt/belts.yml:30` (`#    - MedicalPatch # Goobstation`) — inert, not a real reference.

Because it has zero wound dependency, this can be ported to `Content.Server/_Onyx/Medical/{MedicalPatchComponent,MedicalPatchSystem}.cs` **verbatim**, independent of everything else in phase 4 — it could equally well have shipped in phase 1.

### 2.2 Prototypes, sprites, locale

`Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml` — entities: `BaseMedicalPatch` (abstract; `Sticky` with `whitelist: components: [Bloodstream]`, `SolutionContainerManager` 20u `drink` solution, `MixableSolution`, `ExaminableSolution`, `- type: MedicalPatch`), `UsedMedicalPatch` (trash), `UsedMedicalPatchMakeshift`, and the one **concrete, spawnable** item — `MedicalPatchMakeshift` (`parent: BaseMedicalPatch`, `updateTime: 2`, `singleUse: true`) plus its `constructionGraph`/`construction` (crafted from 1 Cloth, 5s do‑after) and a second graph, `SilkPatchMakeshift` (crafted from 4 WebSilk, for `SpiderCraft`‑tagged entities). **Onyx ships no "stocked"/pre‑filled medical‑patch item** — the only spawnable entity is the craftable makeshift one.

`Resources/Prototypes/_Onyx/Tags/medical_patch.yml`: one tag, `MedicalPatch` — no collision (`git grep` in WG returns nothing for this tag).

Sprites: `Resources/Textures/_Onyx/Objects/Medical/medical_patch.rsi/` (18 files, `meta.json`):
```json
"license": "CC-BY-SA-3.0",
"copyright": "@jorgun  inspired by Studenterhue of Goonstation"
```
Record this license/copyright line in the manifest exactly as‑is (a new artist/license pair not already recorded elsewhere in the Wolfmed manifest).

Locale: `Resources/Locale/en-US/_Onyx/medical/medical_patch.ftl` (8 keys: 2 `ent-*` entity names + 4 `medical-patch-{stick,remove}-{start,success}` sticky‑action popups) — no collisions, port verbatim.

### 2.3 Dependencies — correcting the `GroupHealSpecifier` note

The task brief and `PLAN.md`'s WP11 line both mention `GroupHealSpecifier` (`Content.Shared/_Onyx/Damage/GroupHealSpecifier.cs`) as if it were something Medical Patch (or Tourniquet) needs. **I read `MedicalPatchComponent.cs`/`MedicalPatchSystem.cs` in full above and grepped `GroupHealSpecifier` across the type's every consumer in the whole Onyx tree — Medical Patch does not use it, at all.**

`GroupHealSpecifier`'s actual consumers, confirmed by `git grep`:
```
Content.Server/_Onyx/Vampire/Abilities/VampireSystem.Abilities.cs:149
Content.Shared/_Onyx/Damage/Components/DamageInContainerComponent.cs:23
Content.Shared/_Onyx/Damage/Components/LeechMeleeWeaponComponent.cs:15
Content.Shared/_Onyx/Damage/Systems/DamageableSystem.API.cs:13,22,72,82
Content.Shared/_Onyx/Vampire/Events/VampireActionEvents.cs:50,141,341
```
Every single one belongs to Onyx's **Vampire antagonist** feature — a completely separate system, never mentioned as in‑scope anywhere in `DECISIONS.md`/`PLAN.md`/`PLAN2.md`/`PLAN3.md`.

The type itself does carry a second license header worth recording *if it is ever ported for something that actually needs it* (Vampire, some future phase):
```csharp
// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
```
layered under Onyx's own AGPL‑3.0‑or‑later project license — a genuinely unusual second‑license file, correctly flagged by the phase‑1 evidence report. But **it has no place in P4‑2's file list**. Recommendation: **do not port `GroupHealSpecifier` in phase 4**; note in the manifest that WP11's mention of it alongside Tourniquet/Medical Patch was a bundling error with no real dependency edge, to be revisited only if/when Vampire (or another actual consumer) is ever ported.

(The other Tourniquet/Medical Patch symbols the task brief asks about — `DamageSpecifier.ArmorPenetration` — belong to the compat layer shipped in phase 1 (D5/D23) and are unrelated to these two items; neither file references it.)

### 2.4 Cargo/loadout/medkit placement

Onyx's own prototype set does **not** place `MedicalPatchMakeshift` in any starter medkit fill, vending inventory, loadout, or cargo bounty — `git grep -i "medicalpatch"` across `Resources/Prototypes` in the whole Onyx tree returns only the definition file, the tag file, and one `Surgerykit` storage **whitelist** entry (`medkits.yml:36`, allowing a patch to be *stored* in the surgery kit, not spawning one). This is consistent with its own flavour text ("This doesn't look hygienic. Hopefully it does the job.") and its two construction graphs (Cloth or WebSilk) — it's designed as an emergency field‑craft item, not a stocked supply.

**Recommendation: mirror Onyx exactly — add no fill/vending/cargo entries.** Add `MedicalPatch` to WG's `Surgerykit`‑equivalent storage whitelist only if/when Wolfgate ships an equivalent surgery‑kit storage prototype with a similar whitelist (not confirmed to exist under that name in WG; not required for the item to function, since a plain backpack/pocket already holds it).

### 2.5 Ordered file list, difficulty

| # | File | Action | Difficulty |
|---|---|---|---|
| 1 | `Content.Server/_Onyx/Medical/MedicalPatchComponent.cs` | new, vendored verbatim | trivial |
| 2 | `Content.Server/_Onyx/Medical/MedicalPatchSystem.cs` | new, vendored verbatim, zero edits | trivial |
| 3 | `Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml` | new, verbatim | trivial |
| 4 | `Resources/Prototypes/_Onyx/Tags/medical_patch.yml` | new, verbatim | trivial |
| 5 | `Resources/Textures/_Onyx/Objects/Medical/medical_patch.rsi/*` (18 files) | new, verbatim; record CC‑BY‑SA‑3.0 / @jorgun (Studenterhue/Goonstation) in the manifest | trivial |
| 6 | `Resources/Locale/en-US/_Onyx/medical/medical_patch.ftl` | new, verbatim | trivial |

Overall difficulty: **trivial**. This is the lowest‑risk item in the whole phase — no wound coupling, no collisions, no upstream edits, no new subscriptions (`EntityStuckEvent`/`EntityUnstuckEvent` on `MedicalPatchComponent` is obviously free — it's a brand‑new component). Could genuinely land in an hour of implementation time.

---

## 3. Explosion amputation (P4-6)

### 3.1 What "explosion amputation" currently means, precisely — and a correction to the phase‑3 framing

`WOLFMED_STATUS.md` and `PLAN3.md` P3‑D3 both describe explosion amputation as fully "out" / "inert" in phase 3. Having re‑traced the whole call path in the current tree, that overstates it:

**Explosion damage against a wound host already gets routed through the wound system today, with zero explosion‑specific code.** WG's `ExplosionSystem.Processing.cs:471` calls:
```csharp
_damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
    // Mono: Explosion flag for plate protection
    originFlag: DamageableSystem.DamageOriginFlag.Explosion);
```
`TryChangeDamage` is the **universal** entry point GUARD D/D2 hooks (`Content.Shared/Damage/Systems/DamageableSystem.cs:214-220`) — it raises `BeforeDamageChangedEvent` *unconditionally*, before any `ignoreResistances`/`ignoreGlobalModifiers` short‑circuit, and `WoundDamageRoutingSystem.OnBeforeDamageChanged` (`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:69-108`) subscribes `<WoundHostComponent, BeforeDamageChangedEvent>` with no origin‑type filter. So for a wound host, an explosion's damage is **already** cancelled here and routed through `RouteThroughBodyModifiers` to a single, randomly‑weighted part (`ResolveDamagePart`) — including the ordinary amputation‑threshold/finishing‑hit machinery, and **already correctly threading the `DamageOriginFlag.Explosion` flag to armour plates**, because `OnBeforeDamageChanged` stashes `args.OriginFlag` into `_routedModifiers` (`:79`) before the re‑entrant write reads it back (`:686-688`) for the `SharedArmorPlateSystem` check (`Content.Shared/_Mono/ArmorPlate/SharedArmorPlateSystem.cs:60`).

**What is actually missing is Onyx's *distinctive* explosion mechanic** — spreading the blast across every attached part at once (with per‑part random variation) instead of one random part, plus a separate, more permissive chance‑based amputation roll. That mechanism is **already fully ported and unit‑tested, just unreachable**:

- `WoundDamageRoutingSystem.TryApplyDistributedDamage`/`TryRouteDistributedDamage` (`:251-330`, `:904-929`) already implement the multi‑part split, the `isExplosion` bookkeeping (`_explosionDamage`, `_explosionAmputationCandidates`, `PickExplosionAmputationCandidate` at `:943`), and feed a candidate flag into the same `PartDamageAppliedEvent` plumbing `AmputationSystem.TryExplosionAmputate` reads (`:762`, `:783-784`).
- `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAmputationTest.cs`'s `T-AMP-EXPLOSION` already calls `TryRouteDistributedDamage(..., isExplosion: true)` directly and asserts a limb detaches deterministically (per `PLAN3.md:940`) — "routing‑API branch coverage, not end‑to‑end" is the test's own doc comment.
- The two dormant CVars, `CCVars.ExplosionLimbDamageVariation` (`explosion.damage_variation`, default `2f`) and `CCVars.ExplosionWoundMultiplier` (`explosion.wounding_multiplier`, default `4f`) — both already ported verbatim in phase 1 (`Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs:22-26`) — have zero consumers today.

**The genuine, still‑open gap is exactly what `PLAN3.md` P3‑D3 identified**: `TryApplyDistributedDamage`/`TryRouteDistributedDamage` call `RouteThroughBodyModifiers` directly, **bypassing** `OnBeforeDamageChanged` entirely — so they never populate `_routedModifiers`, so `args.OriginFlag` reaching `SharedArmorPlateSystem` on the re‑entrant write would be `null` even for an explosion, and `SharedArmorPlateSystem.OnBeforeDamageChanged`'s gate (`if (args.Origin == null && args.OriginFlag != DamageOriginFlag.Explosion) return;`) would then treat the hit as neither "has an attacker" nor "is an explosion" and **refuse to apply plate protection at all**. I re‑verified this is still true in the current (post‑phase‑3) tree — nothing since P3‑D3 was written touches this.

### 3.2 The fix — closes the P3-D3 gap instead of reproducing it

This gap is fixable with a **small, self‑contained addition entirely inside the already‑vendored `WoundDamageRoutingSystem.cs`** (which already carries ~25 `// WOLFGATE` edits — one more is low‑incremental‑risk), not an upstream change:

```csharp
// WOLFGATE (P4-6): thread the caller's origin flag through the distributed-damage entry point, which
// bypasses OnBeforeDamageChanged's _routedModifiers capture (the only other writer of that dictionary).
// Without this, an explosion hooked through TryRouteDistributedDamage reaches the re-entrant armour-plate
// check with OriginFlag == null, and SharedArmorPlateSystem.OnBeforeDamageChanged (Origin == null &&
// OriginFlag != Explosion) refuses to protect at all — plates stop working against explosions specifically.
public bool TryApplyDistributedDamage(
    EntityUid body, DamageSpecifier damage, TargetBodyPart mask, DamageDistribution mode,
    EntityUid? origin = null, bool ignoreResistances = false, bool interruptsDoAfters = true,
    float variation = 0f, bool isExplosion = false, float woundSeverityMultiplier = 1f,
    DamageableSystem.DamageOriginFlag? originFlag = null) // WOLFGATE (P4-6)
{
    ...
    _routedModifiers[body] = (0f, null, originFlag); // WOLFGATE (P4-6)
    try
    {
        if (!systemic.Empty)
            applied |= RouteThroughBodyModifiers((body, host), systemic, origin, ignoreResistances, interruptsDoAfters);
        ... // unchanged
    }
    finally
    {
        _routedModifiers.Remove(body); // WOLFGATE (P4-6)
    }
}
```
(`TryRouteDistributedDamage` gets the same optional parameter threaded through its one‑line forward to `TryApplyDistributedDamage`.) This is additive — every existing caller (including the phase‑3 `T-AMP-EXPLOSION` test) keeps compiling with the new parameter defaulting to `null`, identical behaviour to today.

**The upstream hook itself — HOOK 22** (next free number; `HOOK 19` was withdrawn in phase 3, `HOOK 20`/`21` are the last used, per `PLAN3.md` §3):

`Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs:471`, replacing the single call with an if/fallback in exactly the shape Onyx's own `<Onyx-Wounds-edited>` diff used at the same call site:

```csharp
// WOLFGATE: HOOK 22 - wound hosts spread explosion damage across every attached part instead of one flat
// body hit; body in Content.Server/_WF/Wolfmed/Explosion/WolfmedExplosionSystem.cs.
if (!_wolfmedExplosion.TryApplyExplosionDamage(entity, damage))
{
    _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
        // Mono: Explosion flag for plate protection
        originFlag: DamageableSystem.DamageOriginFlag.Explosion);
}
```
plus one `[Dependency] private WolfmedExplosionSystem _wolfmedExplosion = default!;` field and one `using` — **HOOK 22 is 3 marked lines across 2 sites**, matching the project's established "one or two line hook, body in `_WF`" shape (e.g. HOOK 8).

New `_WF` file, `Content.Server/_WF/Wolfmed/Explosion/WolfmedExplosionSystem.cs` (holds the CVar reads too, so **zero** edits are needed to `ExplosionSystem.CVars.cs` — unlike Onyx, which reads its two cvars in that file):
```csharp
namespace Content.Server._WF.Wolfmed.Explosion;

/// <summary>Routes explosion damage on wound hosts through the distributed per-part split instead of one flat body hit.</summary>
public sealed class WolfmedExplosionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private WoundDamageRoutingSystem _routing = default!;

    private float _variation;
    private float _multiplier;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.ExplosionLimbDamageVariation, v => _variation = v, true);
        Subs.CVar(_cfg, CCVars.ExplosionWoundMultiplier, v => _multiplier = v, true);
    }

    public bool TryApplyExplosionDamage(EntityUid entity, DamageSpecifier damage)
    {
        if (!HasComp<WoundHostComponent>(entity))
            return false;

        return _routing.TryRouteDistributedDamage(entity, damage, TargetBodyPart.All,
            DamageDistribution.SplitWithVariation, ignoreResistances: true, interruptsDoAfters: false,
            variation: _variation, isExplosion: true, woundSeverityMultiplier: _multiplier,
            originFlag: DamageableSystem.DamageOriginFlag.Explosion);
    }
}
```
Non‑wound‑hosts fall straight through to the unchanged vanilla call, D2‑consistent, identical to today.

### 3.3 Regression test (required, not optional, given the P3-D3 history)

**T‑EXPLOSION‑PLATE**: a wound host wearing `ArmorPlateHolderComponent` gear with an inserted plate takes `WoundDamageRoutingSystem.TryRouteDistributedDamage(body, dmg, TargetBodyPart.All, SplitWithVariation, isExplosion: true, originFlag: DamageOriginFlag.Explosion)` directly (routing‑API level, same style as `T‑AMP‑EXPLOSION` — no live grenade needed) and asserts the applied damage is reduced versus the same call with no plate, proving the flag survived the distributed path. Pair with a **non‑host regression** assertion (a mob without `WoundHostComponent` still takes flat vanilla damage and plate protection unchanged) to close the loop both directions.

### 3.4 Recommendation

**Proceed in phase 4**, reversing the "stays phase 6" default recorded in `WOLFMED_STATUS.md`/DECISIONS.md's phase‑4 scope line. The total footprint is one upstream hook (HOOK 22, 3 marked lines) + one ~10‑line addition inside the already heavily‑modified vendored routing file + one new small `_WF` system + the CVar plumbing (already ported, zero‑cost to wire) + the regression test. Everything Onyx's own distinctive mechanic needs (multi‑part split, chance‑based amputation, both CVars) is **already sitting in the tree from phase 3**, tested via the routing API — this is wiring, not new mechanism design.

**This is the one item in this report I'd flag for explicit user sign‑off before implementation**, not because the code is risky, but because it changes live combat balance on a gun‑PvP server (grenades/ordnance start being able to sever limbs the way gunfire already can per §8.6‑1, and blast damage against a wound host changes shape from "one random part eats it all" to "spread across the body, tunable via two cvars already defaulted by Onyx"). If the user would rather hold this for a dedicated balance pass, deferring to phase 6 remains a one‑line decision (skip the hook, leave the dormant mechanism as‑is) with no other consequence — nothing else in phase 4 depends on it.

---

## 4. Guidebook (P4-7)

### 4.1 Onyx source

`Resources/Prototypes/_Onyx/Guidebook/medical.yml` — 5 `guideEntry` prototypes: `Virology`, `BodyPartDamage`, `Wounds`, `WoundTreatment`, `Surgery`. Of the five referenced `Resources/ServerInfo/_Onyx/Guidebook/Medical/*.xml` files, **only two exist in the sparse checkout**: `Wounds.xml` and `WoundTreatment.xml`. `Virology.xml`, `BodyPartDamage.xml`, and `Surgery.xml` are **absent from the sparse set** — per the task's own rule, their contents are not guessed here; they may not exist upstream at all, or the sparse path list simply never included them. This isn't a blocker: `Virology` is unrelated to wounds, `Surgery` collides and is out of scope anyway (below), and `BodyPartDamage`'s locale keys (`guide-entry-body-part-damage`, `guidebook-onyx-body-part-damage-{content,examination,difference}`) *are* present in `wounds.ftl` even though its XML isn't reachable — I fold its three short paragraphs into the adapted `Wounds` entry rather than trying to reconstruct a fourth guide page from a file I cannot read.

`Wounds.xml` content (by `FTLTextpart` key, `Resources/Locale/en-US/_Onyx/guidebook/wounds.ftl`, 155 lines) walks: wound stages/open‑stabilized‑closed states → common trauma (blunt/slash/piercing/burn/electrical) → special wounds (fracture, surgical incision, dismemberment wound, amputation consequence, internal bleeding, medical scar) → **part material** (slime/plant, IPC mechanical, cybernetic mechanical, electrical‑by‑material) → dismemberment mechanics, embedding `Gauze`/`Bonesetter`. `WoundTreatment.xml` walks a treatment checklist → biological tissue (embeds `Brutepack`/`Ointment`/`Gauze`/`MedicatedSuture`/`RegenerativeMesh`) → medicine → fractures (embeds `Bonesetter`/`BoneGel`) → **IPC/cybernetic repair** (embeds `Welder`/`SyntheticRepairTool`/`CableApcStack`).

### 4.2 Entity‑embed and locale‑key collision check

| `GuideEntityEmbed` target | WG status |
|---|---|
| `Gauze`, `Brutepack`, `Ointment`, `MedicatedSuture`, `RegenerativeMesh`, `Bonesetter`, `BoneGel`, `HandheldHealthAnalyzer`, `ChemDispenser`, `Syringe`, `Welder`, `CableApcStack` | **present**, all resolvable |
| `SyntheticRepairTool` | **MISSING** — must be dropped |

The `SyntheticRepairTool` gap and the entire "IPC mechanical damage" / "Cybernetic mechanical damage" / "IPC/cybernetics repair" sections align exactly with the task's own instruction — **drop all IPC/cybernetic/slime‑plant material content**, matching D3 (phase 1 species scope) and the still‑true phase‑5 gap (organic‑only wound/organ coverage, §8.6‑7).

`git grep` for `guide-entry-wounds`, `guide-entry-wound-treatment`, `guide-entry-body-part-damage` and `id: Wounds`/`id: WoundTreatment`/`id: BodyPartDamage` (as `guideEntry` ids) across every WG `Resources/Prototypes/Guidebook/*.yml` and `Resources/Locale/en-US/**` returns **zero hits** — no collision for the two new guide entries.

**`id: Surgery` is already taken** — `WG/Resources/Prototypes/Guidebook/medical.yml:60`, Shitmed's own surgery guide entry (with children `PartManipulation`/`OrganManipulation`/`UtilitySurgeries`), already wired as a child of the top‑level `Medical` entry. Vendoring Onyx's `Surgery` guideEntry would collide on id **and** would document Onyx's own unported surgery system (D7 excludes it) — **skip it entirely**, it needs neither a rename nor a merge; WG's existing Surgery entry is already correct for what actually shipped, and phase 4's own P4‑3 wound‑surgery work (`SurgeryStopBleeding` etc., a separate work item) is the right place to extend it later, not this guidebook item.

### 4.3 WG's guidebook tree and where the new entries hang

`Resources/Prototypes/Guidebook/medical.yml:1-9`:
```yaml
- type: guideEntry
  id: Medical
  name: guide-entry-medical
  text: "/ServerInfo/Guidebook/Medical/Medical.xml"
  children:
  - DirectorOfCare
  - MedicalDoctor
  - Chemist
  - Cloning
  - Cryogenics
  - Surgery
  - MedicalBounties
```
Add `Wounds` and `WoundTreatment` to this `children` list (after `MedicalDoctor`, before `Chemist` reads naturally: basic care → wounds → wound treatment → chemistry → surgery).

### 4.4 Proposed adapted content — what to reword

Given the substance changes required (not a find/replace port), I recommend authoring this as **new Wolfgate content under `_WF/Wolfmed`** (status `new` in the manifest, using Onyx's structure/prose as a starting draft), not `_Onyx` verbatim:

- **Drop entirely**: the "Part material" section (slime/plant, IPC mechanical, cybernetic mechanical, electrical‑by‑material) and the IPC/cybernetic‑repair paragraph in `WoundTreatment` — nothing in phases 1–4 supports non‑organic wounds (§8.6‑7).
- **Reword "Dismemberment"**: Onyx's copy says a strong blunt/slash/piercing hit completes an amputation — silently melee‑only phrasing. Correct it per the shipped §8.6‑1 deviation: **guns and lasers can finish an amputation too** (Piercing finishing minimum 12, a Heat finishing‑minimum‑15 row added per part), not just melee.
- **Reword "Fracture"**: keep the mechanic description (reduction weakens effects, mending removes it) but drop any implied numbers — the shipped `manipulationModifier` values are the corrected C# defaults (`1.1/1.25/1.5/2.0`, DECISIONS.md §8.2‑1), not whatever Onyx's guide text originally implied; state qualitatively ("a fractured arm slows hand work; a fractured leg slows movement") rather than quoting Onyx's numbers, since none were quoted in the source text I read.
- **Reword "Wound treatment" checklist and "Biological tissue"**: keep as‑is (bruise packs/ointment remove damage; gauze reduces bleeding; medicated suture/regenerative mesh treat wounds; moderate/severe need surgery) — this matches what phase‑1/2 already shipped, and P4‑1's `treatmentCapabilities` additions to `HealthChange`/`EvenHealthChange` (separate work item) make it stay true. Add the tourniquet from §1 and, once P4‑1 lands, the treatment‑capable topicals, as a new short paragraph or `GuideEntityEmbed` box: `<GuideEntityEmbed Entity="Tourniquet"/>`.
- **Drop "Surgery" cross‑references** that assume Onyx's own surgery system (procedure steps, tool list) — P4‑3's wound surgeries reuse Shitmed's existing surgery guide entry, which already documents the step system; do not duplicate or contradict it here.
- **Amputation consequence**: keep the mechanic description (untreated stump blocks safe reattachment) but note in‑repo that this is currently **not yet enforced** (`AmputationConsequenceWound` is inert per §8.6, P3‑D2) until P4‑3 lands the `CanAttachPart` gate — either hold this paragraph until P4‑3 ships, or phrase it forward‑looking. Recommend holding it out of the guide until P4‑3 actually enforces it, to avoid documenting a mechanic that doesn't yet work in‑game.
- **Internal bleeding / medical scar / dismemberment wound**: port as‑is, these already work exactly as described (phases 1–3).

### 4.5 Ordered file list, difficulty

| # | File | Action | Difficulty |
|---|---|---|---|
| 1 | `Resources/Prototypes/_WF/Wolfmed/Guidebook/medical.yml` | new — 2 `guideEntry` rows (`Wounds`, `WoundTreatment`) | trivial |
| 2 | `Resources/Prototypes/Guidebook/medical.yml` | modify — add `Wounds`/`WoundTreatment` to `Medical`'s `children` list | trivial, 2 lines, marked `# WOLFGATE (P4-7)` |
| 3 | `Resources/ServerInfo/_WF/Wolfmed/Guidebook/Medical/Wounds.xml` | new — adapted from Onyx's `Wounds.xml`, IPC/slime/cybernetic sections removed, dismemberment reworded for guns/lasers | small — content editing, not engineering |
| 4 | `Resources/ServerInfo/_WF/Wolfmed/Guidebook/Medical/WoundTreatment.xml` | new — adapted from Onyx's `WoundTreatment.xml`, IPC/cybernetic repair section removed, Tourniquet embed added | small |
| 5 | `Resources/Locale/en-US/_WF/Wolfmed/guidebook/wounds.ftl` | new — adapted subset of Onyx's 30 keys (drop ~9 IPC/slime/cybernetic keys, reword ~3 for guns/fractures/amputation‑consequence status) | small |

Overall difficulty: **small**, entirely content work, zero C# or upstream engineering. The only real judgment calls are exactly the ones flagged in §4.4 (what to reword) and whether to hold the amputation‑consequence paragraph until P4‑3 ships (recommended: hold it).

---

## 5. Consolidated decision list for the user

| # | Decision | Recommendation |
|---|---|---|
| 1 | Replace WG's existing `Tourniquet` entity's `Healing` mechanic with Onyx's real `Tourniquet` component, in place (same id) | **Yes** — matches Onyx's own edit to the same shared upstream file; zero new ids, zero touched fills |
| 2 | Accept that non‑wound‑host mobs (Protogen only) lose tourniquet function entirely, vs. engineering dual‑component compatibility | **Accept the narrow loss**; dual‑component composition creates an undesirable event‑order race for one excluded species |
| 3 | Do not port `GroupHealSpecifier` in phase 4 (it has no real dependency edge to Tourniquet/Medical Patch — Vampire‑only) | **Confirmed correction** — skip, revisit only alongside Vampire |
| 4 | Proceed with the explosion‑amputation hook (P4‑6) in phase 4, closing the P3‑D3 plate‑protection gap with a ~10‑line vendored addition, rather than deferring to phase 6 | **Recommend proceeding**, gated on the T‑EXPLOSION‑PLATE regression test; flagged for explicit sign‑off since it changes live combat balance (grenades can now sever limbs, spread damage across the body) |
| 5 | Hold the "amputation consequence" guidebook paragraph until P4‑3 actually enforces the reattachment block | **Hold** — don't document an inert mechanic |
| 6 | Add `Tourniquet` to `MedkitAdvancedFilled` only (not `MedkitCombatFilled`), matching Onyx's own confirmed placement | **Yes** |

## 6. Blockers

None. All four items build on the phases 1–3 tree as it stands; no missing compat‑layer piece, no unresolved D‑decision, no absent sparse‑checkout file blocks any of the recommended work (the three unreachable guidebook XML files are worked around, not blocking, per §4.1).
