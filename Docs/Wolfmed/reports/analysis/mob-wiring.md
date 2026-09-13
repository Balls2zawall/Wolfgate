# Wolfmed mob-wiring report

Onyx pinned commit: `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (reference copy `C:\tmp\onyx`, sparse checkout).
Wolfgate worktree: `C:\Users\jzo12\Documents\GitHub\Wolfgate\.claude\worktrees\rules-motd-updates-11c89c` (= `WG` below).
All Onyx file citations are `file:line` against the sparse checkout; anything not in the sparse set is called out explicitly. All Wolfgate citations are against the worktree above (read-only; nothing was modified).

## 0. Summary of the 5 things that matter most

1. **`WoundHostComponent` attaches to the whole mob, not to parts.** Onyx puts `- type: WoundHost` on the abstract mob entity `BaseSpeciesMobOrganic` (`Resources/Prototypes/Body/species_base.yml:265`). Wolfgate's equivalent abstract mob is `BaseMobSpeciesOrganic` (`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:247`), which is already the parent of all 9 of Wolfgate's organic species (arachnid, diona, dwarf, gingerbread, human, moth, reptilian, slime, vox — `skeleton` is the one species base that stays on plain `BaseMobSpecies`). One `// WOLFGATE` line on that single prototype turns every phase-1 species into a wound host simultaneously; no per-species file needs to change.
2. **Per-part wound support (`WoundableComponent`) is not declared in Onyx's prototypes for organic parts at all** — it is added at runtime. `WoundDamageProjectionSystem.SetupPart` (`Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs:206-221`) runs `EnsureComp<WoundableComponent>(part)` + `EnsureComp<DamageableComponent>(part)` + conditionally `PainComponent` + `EnsureComp<BodyPartFunctionalityComponent>(part)` for every child of a `WoundHostComponent` body on `MapInitEvent`. Onyx's YAML only declares `- type: Woundable` explicitly for **non-default** species (diona, slime, IPC, cybernetic, xenomorph) to override the profile; organic humans get the default (`OrganicBodyPartProfile`) purely from the component's own field default. Wolfgate can copy this pattern almost exactly, because Wolfgate's Shitmed parts already carry a static per-part `Damageable` (`damageContainer: OrganicPart`) declared in `_Shitmed/Body/Parts/base.yml:7-8` — so the Wolfgate glue system doesn't even need to `EnsureComp<DamageableComponent>`, only `EnsureComp` the new Woundable-equivalent.
3. **Blocker (must resolve before any code lands): four C# type names collide outright.** Onyx's own `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs` declares `namespace Content.Shared.Body.Part;` and defines `BodyPartComponent`, `BodyPartType` (10 members: adds `Chest`/`Groin` that Wolfgate's enum doesn't have), `BodyPartSymmetry`, and `BodyPartSlot` — **the exact same namespace and exact same four type names Wolfgate's Shitmed already uses** (`WG/Content.Shared/Body/Part/BodyPartComponent.cs`, `BodyPartSymmetry.cs`). Vendoring that Onyx file verbatim will not compile. This is exactly what D8 already anticipated ("put Onyx's extra part fields in a separate Wolfmed component instead of editing the upstream one") — the report below treats this as confirmed and mandatory, not optional. Separately, `Content.Shared/_Onyx/Targeting/TargetingComponent.cs` registers a component literally named `Targeting` (RT registers by class name), which is the same registered name as Wolfgate's existing `Content.Shared._Shitmed.Targeting.TargetingComponent` (also `Targeting`) — a second, independent collision if `_Onyx/Targeting` is vendored as-is. Both collisions are avoidable (skip vendoring those two files; adapt to Wolfgate's existing `BodyPartComponent`/`TargetingComponent` instead), but they must be decided now, not discovered mid-port.
4. **Wolfgate's mob-level gib threshold will fire too early once part damage is projected back onto the mob.** Onyx had to raise `BaseMobDestructible`'s `Destructible` Blunt/Heat threshold from its old value to **1500**, marked `# <Onyx-WoundDamageProjection-edited>` (`Resources/Prototypes/Entities/Mobs/base.yml`, `git show HEAD:...` — not in the sparse checkout, read via `git show`). This is because `WoundDamageProjectionSystem.RefreshBodyDamage` sums every part's damage back onto the mob's own `DamageableComponent` (`_damage.SetDamage(body, total)`), so the old "gib the whole mob on total damage" logic would trigger far too soon once part damage is being tallied that way. Wolfgate's `MobDamageable` currently gibs at **Blunt: 400** (`WG/Resources/Prototypes/Entities/Mobs/base.yml`) — the same fix will be needed for `BaseMobSpeciesOrganic`/whatever inherits `MobDamageable`, and it is a `// WOLFGATE` edit, not a new prototype.
5. **`Caustic` damage cannot currently reach a Wolfgate body part.** Onyx's `OrganicBodyPartProfile.acceptedDamageTypes` includes `Caustic`, and `WoundHostComponent.LocalizedDamageTypes` routes it to parts by default. Wolfgate's part-level damage container `OrganicPart` (`WG/Resources/Prototypes/Damage/containers.yml:79-82`) only supports the `Brute`/`Burn` groups (Blunt/Slash/Piercing/Heat/Shock/Cold) — `Caustic` isn't in any damage group and isn't listed as a `supportedType`, so a Caustic hit would currently be dropped by `DamageableComponent` on a part. This needs either a `// WOLFGATE` addition of `Caustic` to `OrganicPart`'s `supportedTypes`, or dropping Caustic from the routed set for phase 1 (acid/chem damage is rare in Wolfgate's ship/gun PvP anyway).

## 1. How Onyx attaches wounds to mobs

### 1.1 The mob-level component: `WoundHostComponent`

`Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:14-93`:

```csharp
[RegisterComponent, NetworkedComponent]
public sealed partial class WoundHostComponent : Component
{
    [DataField] public Dictionary<BodyPartType, float> TargetWeights = new() { ... };
    [DataField] public HashSet<ProtoId<DamageTypePrototype>> LocalizedDamageTypes =
        [ "Blunt", "Slash", "Piercing", "Heat", "Cold", "Shock", "Caustic" ];
    [DataField] public TargetBodyPart SystemicPainTarget = TargetBodyPart.Chest;
    [DataField] public Dictionary<BodyPartType, FixedPoint2> DismembermentSeverities = new() { ... };
    [DataField] public FixedPoint2 DefaultDismembermentSeverity = 100;
    [DataField] public ProtoId<WoundPrototype> DismembermentWound = "DismembermentWound";
    [DataField] public ProtoId<WoundPrototype> AmputationConsequenceWound = "AmputationConsequenceWound";
    [DataField] public float SeverableResetRatio = 0.8f;
    [DataField] public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DefaultDismembermentFinishingDamage = new() { ... };
    [DataField] public HashSet<BodyPartType> MobilityParts = [BodyPartType.Leg, BodyPartType.Foot];
    [DataField] public HashSet<BodyPartType> ManipulationParts = [BodyPartType.Arm, BodyPartType.Hand];
    [DataField] public Dictionary<BodyPartType, float> PartEffectScales = new() { ... };
}
```

It carries **all its tuning inline as field defaults** — nothing else needs to be set in YAML for a mob to become a fully-configured wound host; a bare `- type: WoundHost` is enough.

It is declared exactly once, on the abstract mob template, `Resources/Prototypes/Body/species_base.yml:211-266`:

```yaml
- type: entity
  abstract: true
  parent:
  - MobBloodstream
  - MobRespirator
  - MobAtmosStandard
  - MobFlammable
  - BaseSpeciesMob
  - BaseMobDestructible
  - BaseHungerThirstSlowdown
  id: BaseSpeciesMobOrganic
  save: false
  components:
  ...
  # <Onyx-Surgery>
  - type: SurgeryTarget
  # </Onyx-Surgery>
  # <Onyx-WoundSystem>
  - type: WoundHost
  # </Onyx-WoundSystem>
  # <Onyx-Targeting>
  - type: Targeting
  - type: PartStatus
  # </Onyx-Targeting>
  ...
```

Every organic humanoid species prototype (`MobHuman`, `MobIpc` is the exception — see 1.4) parents `BaseSpeciesMobOrganic`, so it is the single choke point. The non-organic sibling `BaseSpeciesMob` (`species_base.yml:1-210`) does **not** get `WoundHost`, but does get `PainShockTargetComponent` at line 54 (`- type: PainShockTarget # <Onyx-PainShock>`) — pain-shock is wired one level higher than wounds themselves, because IPCs (which skip `BaseSpeciesMobOrganic` entirely, see 1.4) can still take pain-adjacent stun effects. Not relevant to phase 1 (fractures/pain/shock are phase 2 per the handoff), but worth knowing when that phase starts: the insertion points for `WoundHost` and `PainShockTarget` are **not the same prototype**.

`BaseSpeciesMobOrganic`'s trigger for actually building the wound machinery is `MapInitEvent`, subscribed in `WoundDamageProjectionSystem.cs:29` (`after: [typeof(InitialBodySystem)]`) — i.e. it deliberately runs after the body's organs/parts have been spawned in, then walks them.

### 1.2 The per-part component: `WoundableComponent`, and how parts get it

`Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:155-171`:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundableComponent : Component
{
    public const string ContainerId = "wounds";
    [DataField, AutoNetworkedField] public ProtoId<BodyPartProfilePrototype> Profile = "OrganicBodyPartProfile";
    [ViewVariables] public Container WoundsContainer = default!;
    [ViewVariables] public FixedPoint2 AmputationOverflow;
    [AutoNetworkedField] public bool Severable;
}
```

This is **not** present anywhere in `base_organs.yml` (the file that defines the default human/organic torso, head, arms, hands, legs, feet). It is added dynamically, once, by `WoundDamageProjectionSystem.SetupPart` (`Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs:206-221`):

```csharp
private void SetupPart(EntityUid part)
{
    EnsureComp<WoundableComponent>(part);
    EnsureComp<DamageableComponent>(part);
    if (_pain.CanFeelPain(part))
        EnsureComp<PainComponent>(part);
    else
        RemComp<PainComponent>(part);
    EnsureComp<BodyPartFunctionalityComponent>(part);
    var injurable = EnsureComp<InjurableComponent>(part);
    if (injurable.DamageContainer != null)
        return;
    injurable.DamageContainer = "Biological";
    Dirty(part, injurable);
}
```

called from `SetupBody` (`:192-204`), which is called from `OnMapInit` (`:34-38`) on `WoundHostComponent` + `MapInitEvent`, iterating `_body.GetBodyChildren(body)` (Onyx's own rewritten `SharedBodySystem`, `Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs`).

`- type: Woundable` **is** declared explicitly in YAML only where a species needs a non-default profile:

| File:line | Entity | Purpose |
|---|---|---|
| `Resources/Prototypes/Body/Species/diona.yml:230-233` | `OrganDionaExternal` | `profile: PlantBodyPartProfile`, `- type: BodyPart / fractureProfile: null` |
| `Resources/Prototypes/Body/Species/slime.yml:236-239` | `OrganSlimePersonExternal` | `profile: SlimeBodyPartProfile`, `fractureProfile: null` |
| `Resources/Prototypes/_Onyx/Body/Parts/xenomorph.yml:19-20` | (xenomorph part) | `profile: OrganicBodyPartProfile` (explicit even though it's the default — animal, not species) |
| `Resources/Prototypes/_Onyx/Entities/Mobs/Customization/Parts/cybernetic.yml:7-10` (and 8 more repeats through line 192) | cybernetic prosthetic parts | `profile: CyberneticBodyPartProfile`, `fractureProfile: CyberneticFractureProfile` |
| `Resources/Prototypes/Corvax/Body/Species/ipc.yml:327-329` (via `git show`, not in sparse checkout as a working-tree file but present at `HEAD`) | `OrganIpcExternal` | `profile: IpcBodyPartProfile`, `fractureProfile: null` |

Note diona/slime/IPC are all **outside phase 1** (D3), so for phase 1 the port needs zero explicit `Woundable` YAML at all — the runtime-ensure pattern handles every organic human-shaped species for free.

### 1.3 Where `fractureProfile` and other per-part fields actually live: Onyx's own `BodyPartComponent`

The `fractureProfile`, `amputationThresholds`, `maxDamage`, `dismembermentFinishingDamage`, `amputationConsequenceSeverity`, `dismembermentSeverity` fields the handoff doc flagged are **not** on `WoundableComponent` — they are on Onyx's own from-scratch `BodyPartComponent`, at `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:12-98`:

```csharp
namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
public enum BodyPartType : ushort
{ Other = 0, Torso = 1, Head = 2, Arm = 3, Hand = 4, Leg = 5, Foot = 6, Tail = 7, Chest = 8, Groin = 9 }

[Serializable, NetSerializable]
public enum BodyPartSymmetry { None, Left, Right }

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BodyPartComponent : Component
{
    ... Body, Parent, Children, ChildSlots, Organs, PartType, Symmetry, IsVital, Species, Category,
    DirtExposures, DirtCoverageLayers,
    /// <summary>Fracture profile for this part. Null = no fractures.</summary>
    [DataField] public ProtoId<FractureProfilePrototype>? FractureProfile;
    [DataField] public FixedPoint2 MaxDamage;
    [DataField] public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> AmputationThresholds = new();
    [DataField] public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DismembermentFinishingDamage = new();
    [DataField] public FixedPoint2 AmputationConsequenceSeverity = 35;
    [DataField] public FixedPoint2? DismembermentSeverity;
}

[DataDefinition][Serializable, NetSerializable]
public partial record struct BodyPartSlot { [DataField(required: true)] public BodyPartType Type; [DataField] public BodyPartSymmetry Symmetry; ... }
```

**This is the file described under §0.3 above: it collides on all four type names with Wolfgate's existing `Content.Shared.Body.Part.BodyPartComponent`/`BodyPartType`/`BodyPartSymmetry`/`BodyPartSlot`.** It cannot be vendored verbatim per D6; only its wound-relevant *fields* (`FractureProfile`, `MaxDamage`, `AmputationThresholds`, `DismembermentFinishingDamage`, `AmputationConsequenceSeverity`, `DismembermentSeverity`) should be lifted into the new `_WF/Wolfmed` part-extras component that D8 already calls for.

`fractureProfile` is set per default organic part in `Resources/Prototypes/Body/base_organs.yml`, on the `- type: BodyPart` block of each abstract part:

| Part (abstract id) | Line | `fractureProfile` | `amputationThresholds` |
|---|---|---|---|
| `OrganBaseTorso` | 46-57 | *(none set — torso has no fracture in Onyx's default set)* | *(none)* |
| `OrganBaseHead` | 84-100 | `OrganicFractureProfile` (line 95) | Slash 200 / Piercing 200 / Blunt 350 |
| `OrganBaseArmLeft` | 134-152 | `OrganicFractureProfile` (145) | Slash 130 / Piercing 250 / Blunt 250 |
| `OrganBaseArmRight` | 172-195 | `OrganicFractureProfile` (188) | Slash 130 / Piercing 250 / Blunt 250 |
| `OrganBaseHandLeft` | 219-235 | `OrganicFractureProfile` (230) | Slash 70 / Piercing 200 / Blunt 150 |
| `OrganBaseHandRight` | 263-279 | `OrganicFractureProfile` (274) | Slash 70 / Piercing 200 / Blunt 150 |
| `OrganBaseLegLeft` | 308-326 | `OrganicFractureProfile` (319) | Slash 150 / Piercing 250 / Blunt 300 |
| `OrganBaseLegRight` | 351-369 | `OrganicFractureProfile` (362) | Slash 150 / Piercing 250 / Blunt 300 |
| `OrganBaseFootLeft` | 393-409 | `OrganicFractureProfile` (404) | Slash 80 / Piercing 220 / Blunt 170 |
| `OrganBaseFootRight` | 433-449 | `OrganicFractureProfile` (444) | Slash 80 / Piercing 220 / Blunt 170 |

Also note: `category:` on each (`Torso`, `Head`, `ArmLeft`, `ArmRight`, `HandLeft`, `HandRight`, `LegLeft`, `LegRight`, `FootLeft`, `FootRight`) is a `ProtoId<OrganCategoryPrototype>` — a **Nubody** concept (each organ/part has a unique named "slot category") that has no Wolfgate/Shitmed equivalent (Shitmed identifies parts by `PartType` + `Symmetry` + slot id string, not a `category` prototype). Not needed for phase 1 — the wound routing code that consumes `category` is Nubody-only glue Onyx uses for its own slot lookups; Wolfgate's Shitmed slot system already does the equivalent job differently, so this field is simply dropped when the fields above are extracted into the new component.

`chest_groin.yml` and `Parts/animal.yml`/`Parts/xenomorph.yml`/`Entities/Mobs/Customization/Parts/cybernetic.yml` repeat the same `fractureProfile`/`profile` pattern for chest/groin organs, animals, xenomorphs and cybernetic prosthetics respectively (all outside phase 1's "organic humanoid" scope except chest/groin, which **is** in scope — see 1.5).

### 1.4 Organ-level damage: `OrganDamageComponent` and `FunctionalOrganComponent`

`Content.Shared/_Onyx/Body/OrganDamageComponent.cs:10` (fields: `hitChance`, `selectionWeight`, `damageMultipliers`) is attached directly in `base_organs.yml` to every **internal** organ (brain, eyes, tongue, appendix, ears, lungs, heart, stomach, liver, kidneys — lines 480-859, all under `# <Onyx-OrganDamage>` markers), e.g. brain at `base_organs.yml:480-492`:

```yaml
  - type: OrganDamage
    hitChance: 0.8
    selectionWeight: 0.75
    damageMultipliers: { Blunt: 0.115, Slash: 0.25, Piercing: 0.42, Heat: 0.15, Cold: 0.05, Shock: 0.3125 }
```

This is a **separate** mechanic from part wounds: organs (brain/heart/lungs/etc.) accumulate their own damage pool with a chance-per-hit and a per-organ damage-type multiplier, independent of the part's own `WoundableComponent`/wound severity. `Organ` component itself (vanilla Nubody, extended by Onyx) gains `destructionWound`/`destructionWoundSeverity` fields (e.g. lungs `destructionWound: InternalBleedingWound`, `destructionWoundSeverity: 35`, `base_organs.yml:665-666`) — destroying an organ creates a wound on the parent part.

`FunctionalOrganComponent` (`Content.Shared/_Onyx/Body/FunctionalOrganComponent.cs:9`) is used on prosthetic/cybernetic organs (`_Onyx/Body/Organs/dubious.yml`, `cybernetic.yml`) to track functional state vs. the classic wizden Organ — not relevant to organic phase 1.

**Wolfgate currently has no organ-level damage at all** — `WG/Resources/Prototypes/Body/Organs/human.yml` organs (`BaseHumanOrganUnGibbable`, `OrganHumanBrain`, etc.) carry `Organ`, `Food`, `SolutionContainerManager`, `FlavorProfile`, `Tag` — no `Damageable`. This matches the handoff's phase plan exactly: organ damage is phase 3 ("Amputation, organ damage, scars"), not phase 1. Phase 1 only needs part-level wounds.

### 1.5 The wound-content prototypes (`bodyPartProfile` / `fractureProfile`)

`Resources/Prototypes/_Onyx/Wounds/wounds.yml` registers three prototype kinds, all defined in one C# file, `Content.Shared/_Onyx/Wounds/WoundPrototype.cs`:

- `[Prototype] class WoundPrototype : IPrototype` (`WoundPrototype.cs:12-13`) → YAML `type: wound`
- `[Prototype] class BodyPartProfilePrototype : IPrototype` (`WoundPrototype.cs:134-135`) → YAML `type: bodyPartProfile`
- `[Prototype] class FractureProfilePrototype : IPrototype` (`WoundPrototype.cs:210-211`) → YAML `type: fractureProfile`

Five `bodyPartProfile`s exist (`wounds.yml:1-152`): `OrganicBodyPartProfile`, `IpcBodyPartProfile`, `SlimeBodyPartProfile`, `CyberneticBodyPartProfile`, `PlantBodyPartProfile`. Phase 1 needs only `OrganicBodyPartProfile`:

```yaml
- type: bodyPartProfile
  id: OrganicBodyPartProfile
  treatmentCapabilities: [Biological]
  bleedingMultiplier: 1.0
  acceptedDamageTypes: [Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic]
  supportedWounds: [BluntWound, SlashWound, PiercingWound, BurnWound, ElectricalWound,
    BoneFractureWound, SurgicalIncisionWound, DismembermentWound, AmputationConsequenceWound,
    InternalBleedingWound, MedicalScarWound, SystemicBleedingWound]
  organDamage:
    chances: { Head: 0.05, Chest: 0.04, Groin: 0.04, Arm: 0.02, Hand: 0.01, Leg: 0.02, Foot: 0.01 }
    maxAffected: 2
```

Two `fractureProfile`s exist (`wounds.yml:153-230`): `OrganicFractureProfile` and `CyberneticFractureProfile`. Phase 1 (before fractures ship — phase 2) doesn't strictly need `OrganicFractureProfile` to *do* anything yet, but the `BodyPart.FractureProfile` field on the new Wolfmed component will point at it, so it should be ported alongside the bodyPartProfile as inert data.

### 1.6 Species-specific overrides (confirms D3's scope)

- **IPC** (`Resources/Prototypes/Corvax/Body/Species/ipc.yml`, read via `git show HEAD:...` — path not in the sparse checkout as a plain file) parents `[AppearanceIpc, BaseSpeciesMob, MobBloodstream]` — **not** `BaseSpeciesMobOrganic** — and adds `- type: WoundHost` directly on `MobIpc` itself (line 101), plus its own `Bloodstream` with an Oil reagent. Its parts (`OrganIpcExternal`) set `profile: IpcBodyPartProfile`, `fractureProfile: null`. IPC is wired **per-mob**, not inherited for free — confirms it's a distinct, deliberate phase-5 task, not something that falls out of the phase-1 `BaseSpeciesMobOrganic` edit.
- **Slime** (`Body/Species/slime.yml:236-239`) and **Diona** (`Body/Species/diona.yml:230-233`) both *do* parent `BaseSpeciesMobOrganic` (so they'd get `WoundHost` "for free" the moment phase 1 lands), but override their parts' `profile`/`fractureProfile` to Slime/Plant. Since Wolfgate's `slime.yml`/`diona.yml` species also parent `BaseMobSpeciesOrganic`, **the phase-1 edit will silently turn Wolfgate's Slime and Diona mobs into (incompletely-tuned) wound hosts too**, unless the orchestrator explicitly restricts scope (e.g. by not porting `SlimeBodyPartProfile`/`PlantBodyPartProfile` and instead giving those species' parts an explicit `profile: null`-equivalent opt-out, or accepting Organic defaults for them until their real phase-5 profiles land). This should be a deliberate decision, not an oversight — see §2.3.
- **Cybernetic prosthetics** (`_Onyx/Entities/Mobs/Customization/Parts/cybernetic.yml`) are attached as replacement limbs on any species (not species-locked) — out of scope for phase 1 per D3/D8.
- **Xenomorph / animal parts** (`_Onyx/Body/Parts/xenomorph.yml`, `_Onyx/Body/Parts/animal.yml`, `Body/Animals/animal.yml:51` for `WoundHost` on the animal mob base) are NPC-only, out of scope.

## 2. Wolfgate mapping

### 2.1 Wolfgate's species inventory

`WG/Resources/Prototypes/Entities/Mobs/Species/*.yml`:

| File | Abstract species-base id | Parent | Body prototype (`WG/Resources/Prototypes/Body/Prototypes/*.yml`) | Part file (`WG/Resources/Prototypes/Body/Parts/*.yml`) |
|---|---|---|---|---|
| `arachnid.yml` | `BaseMobArachnid` | `BaseMobSpeciesOrganic` | `Arachnid` | `arachnid.yml` (`PartArachnid` → `[BaseItem, BasePart]`) |
| `diona.yml` | `BaseMobDiona` | `BaseMobSpeciesOrganic` | `Diona` | `diona.yml` (`PartDiona` → `[BaseItem, BasePart]`) |
| `dwarf.yml` | `BaseMobDwarf` | `BaseMobSpeciesOrganic` | `Dwarf` | *(none — `Dwarf` body reuses `TorsoHuman`/`HeadHuman`/etc. directly)* |
| `gingerbread.yml` | `BaseMobGingerbread` | `BaseMobSpeciesOrganic` | `Gingerbread` | `gingerbread.yml` (`PartGingerbread` → `[BaseItem, BasePart]`) |
| `human.yml` | `BaseMobHuman` | `BaseMobSpeciesOrganic` | `Human` (inherited, no override) | `human.yml` (`PartHuman` → `[BaseItem, BasePart]`) |
| `moth.yml` | `BaseMobMoth` | `BaseMobSpeciesOrganic` | `Moth` | `moth.yml` (`PartMoth` → `[BaseItem, BasePart]`) |
| `reptilian.yml` | `BaseMobReptilian` | `[BaseMobSpeciesOrganic, ReptilianClaws]` | `Reptilian` | `reptilian.yml` (`PartReptilian` → `[BaseItem, BasePart]`) |
| `skeleton.yml` | `BaseMobSkeletonPerson` | `BaseMobSpecies` **(not Organic)** | `Skeleton` | `skeleton.yml` (`PartSkeletonBase` → `BasePartInorganic`) |
| `slime.yml` | `BaseMobSlimePerson` | `BaseMobSpeciesOrganic` | `Slime` | `slime.yml` (`PartSlime` → `[BaseItem, BasePart]`) |
| `vox.yml` | `BaseMobVox` | `BaseMobSpeciesOrganic` | `Vox` | `vox.yml` (`PartVoxBase` → `[BaseItem, BasePart]`) |

There is no Silicon/IPC playable species prototype under `Entities/Mobs/Species` in Wolfgate today (`Body/Parts/silicon.yml` exists and parents `BasePartInorganic`, but no species-base mob file references it under `Entities/Mobs/Species`) — consistent with D3 deferring IPC/cybernetic to a later phase; there's simply nothing to wire yet.

**8 of 9 organic species' concrete parts all inherit from the one abstract `BasePart`** (`WG/Resources/Prototypes/_Shitmed/Body/Parts/base.yml:1-8`, itself `parent: BasePartInorganic`, adding `- type: Damageable / damageContainer: OrganicPart`) — human, arachnid, diona, gingerbread, moth, reptilian, slime, vox. Dwarf needs nothing extra (reuses human parts). Skeleton stays on `BasePartInorganic` (no `Damageable` override to `OrganicPart` — bone parts, correctly excluded).

### 2.2 Exact match confirmed: `BaseMobSpeciesOrganic` ↔ `BaseSpeciesMobOrganic`

`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:239-247`:

```yaml
- type: entity
  save: false
  parent:
  - MobBloodstream
  - MobRespirator
  - MobAtmosStandard
  - MobFlammable
  - BaseMobSpecies
  id: BaseMobSpeciesOrganic
  abstract: true
  components: ...
```

This is structurally identical to Onyx's `BaseSpeciesMobOrganic` (§1.1) — same parent list shape (`MobBloodstream`, `MobRespirator`, `MobAtmosStandard`, `MobFlammable`, + species base). Wolfgate's `BaseMobSpecies` (the non-organic parent, `base.yml:1-238`) **already has** `- type: Targeting # Shitmed Change` and `- type: SurgeryTarget # Shitmed Change` (`base.yml:232-233`) — i.e. Wolfgate already ships the Shitmed equivalent of Onyx's `<Onyx-Targeting>`/`<Onyx-Surgery>` blocks, one level higher (on `BaseMobSpecies` rather than only `-Organic`). Nothing to add there. The **only** thing genuinely missing to make Wolfgate's organic humanoids wound hosts is `WoundHost` itself.

### 2.3 Proposed changes

**A. One `// WOLFGATE` line, upstream file** (per D6 "one- or two-line hooks"; a separate `_WF/Wolfmed` file cannot add a component to an *existing* prototype id — RobustToolbox's prototype loader treats a second `- type: entity / id: BaseMobSpeciesOrganic` block as a duplicate-ID error, it does not merge component lists across files by id):

`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml`, inside `BaseMobSpeciesOrganic`'s `components:` list:

```yaml
  - type: WoundHost # WOLFGATE - Wolfmed phase 1
```

This alone covers arachnid, diona, dwarf, gingerbread, human, gingerbread, moth, reptilian, slime and vox. Per §1.6, diona and slime will pick this up too even though their species-specific wound profiles (`PlantBodyPartProfile`/`SlimeBodyPartProfile`) are out of phase-1 scope; the orchestrator should decide explicitly whether that's acceptable (they'd just get Organic-profile wounds until phase 5) or whether to gate `WoundHost` more narrowly (e.g. only on `BaseMobHuman`/`BaseMobDwarf`/`BaseMobArachnid`/`BaseMobGingerbread`/`BaseMobMoth`/`BaseMobReptilian`, six explicit one-liners instead of one, leaving `BaseMobDiona`/`BaseMobSlimePerson` for their own phase). Given D4 ("Onyx defaults, tuning later") the simplest reading is: land the single line on `BaseMobSpeciesOrganic` and accept Organic wound behavior on slime/diona until phase 5 retunes them — but this is a judgment call the report flags rather than resolves.

**B. One `// WOLFGATE` line, upstream file**, for the part-extras component (new component name TBD by the C#-writing agent, e.g. `WolfmedBodyPartComponent`) carrying only `FractureProfile`/`MaxDamage`/`AmputationThresholds`/`DismembermentFinishingDamage`/`AmputationConsequenceSeverity`/`DismembermentSeverity` (§1.3), defaulting `FractureProfile` to `OrganicFractureProfile` in the C# field default so no per-part YAML is needed for the common case:

`WG/Resources/Prototypes/_Shitmed/Body/Parts/base.yml`, inside `BasePart`'s `components:` list:

```yaml
  - type: WolfmedBodyPart # WOLFGATE - Wolfmed phase 1, carries FractureProfile/AmputationThresholds/etc. Onyx's BodyPart fields don't fit Shitmed's BodyPartComponent (see §1.3/§0.3)
```

This one line, thanks to the `BasePart` ancestry table in §2.1, reaches all 8 concrete-part species at once. Per-part *overrides* (Onyx's differing `amputationThresholds` for head/arm/hand/leg/foot, §1.3's table) are genuinely per-part data and have no single choke point in Wolfgate the way `fractureProfile`'s default does — those need either (a) small `// WOLFGATE` field blocks added to each of `BaseHead`/`BaseLeftArm`/`BaseRightArm`/`BaseLeftHand`/`BaseRightHand`/`BaseLeftLeg`/`BaseRightLeg`/`BaseLeftFoot`/`BaseRightFoot` in `WG/Resources/Prototypes/Body/Parts/base.yml` (9 small edits, mirroring Onyx's own per-part table exactly), or (b) accepting a single flat default (Wolfgate already has a single flat `SeverIntegrity` per part in its *existing* `BodyPartComponent`, e.g. `severIntegrity: 400` on `BaseHead`, `160` on `MajorLimb`/`BaseRightArm`, `WG/Resources/Prototypes/Body/Parts/base.yml:93,171,278` — the new component could mirror that existing per-part pattern instead of Onyx's dictionary-of-damage-type-thresholds, which is a balance decision for whoever owns phase 3 amputation, not phase 1).

**C. New prototypes, `_WF/Wolfmed`** (no upstream conflict — new prototype kinds, new ids):

`WG/Resources/Prototypes/_WF/Wolfmed/wounds.yml` (or split further):

```yaml
- type: bodyPartProfile
  id: OrganicBodyPartProfile
  treatmentCapabilities: [Biological]
  bleedingMultiplier: 1.0
  acceptedDamageTypes: [Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic] # NOTE: see §0.5, Caustic needs a damage-container fix first
  supportedWounds: [ ... ] # phase-appropriate subset; phase 1 only needs the bleeding-related wound ids that ship in phase 1
  organDamage:
    chances: { Head: 0.05, Chest: 0.04, Groin: 0.04, Arm: 0.02, Hand: 0.01, Leg: 0.02, Foot: 0.01 }
    maxAffected: 2

- type: fractureProfile
  id: OrganicFractureProfile
  # verbatim from Resources/Prototypes/_Onyx/Wounds/wounds.yml:153-192 — inert until phase 2
```

**D. Damage-container fix** (`// WOLFGATE`, upstream): `WG/Resources/Prototypes/Damage/containers.yml:79-82`, add `Caustic` to `OrganicPart`'s `supportedTypes` if Caustic wounds are wanted in phase 1 (see §0.5); otherwise drop `Caustic` from the `LocalizedDamageTypes`/`acceptedDamageTypes` copied into the Wolfgate wound-glue system and profile respectively, and revisit later.

**E. Mob-level gib threshold fix** (`// WOLFGATE`, upstream, see §0.4): `WG/Resources/Prototypes/Entities/Mobs/base.yml`, `MobDamageable`'s `Destructible` threshold (currently `Blunt: 400`) needs raising once part damage projects back onto the mob — exact number is a balance call for whoever writes the routing system, but Onyx's own fix (400-ish → 1500, a ~3.75x increase) is a reasonable starting ratio to carry over.

## 3. `DamageableComponent` on parts vs. body vs. organs

Three separate layers, and Wolfgate today only has two of the three:

| Layer | Onyx | Wolfgate today |
|---|---|---|
| **Mob (body)** | `DamageableComponent` (new-style, container-less; container now lives on the separate `InjurableComponent`, `Content.Shared/Damage/Components/InjurableComponent.cs:13-41`, `DamageContainer` field) + `WoundHostComponent`. `WoundDamageRoutingSystem` intercepts `BeforeDamageChangedEvent` on `WoundHostComponent`, cancels it, routes to a part (`WoundDamageRoutingSystem.cs:49-56`). `WoundDamageProjectionSystem.RefreshBodyDamage` sums part damage back onto the mob's own `Damageable` afterward (visuals/total-damage bookkeeping only — the routed damage itself never reaches the mob's container). | `DamageableComponent` (old-style: `DamageContainerID` field lives directly on `DamageableComponent`, `WG/Content.Shared/Damage/Components/DamageableComponent.cs:23-27` — **no `InjurableComponent` exists in Wolfgate at all**), container `Biological` (`MobDamageable`, `WG/Resources/Prototypes/Entities/Mobs/base.yml:65-66`). Already the interception point Shitmed uses for part-damage spreading (per D2, this is what the new `// WOLFGATE` guard must bypass for `WoundHostComponent` entities). |
| **Part** | `DamageableComponent` + `WoundableComponent`, both `EnsureComp`'d at runtime (§1.2) — not declared in prototype YAML for the default organic case. Container assigned by setting `InjurableComponent.DamageContainer = "Biological"` in code (`WoundDamageProjectionSystem.cs:219`, only if not already set). | `DamageableComponent` **already statically declared** in the prototype YAML, `damageContainer: OrganicPart` (`WG/Resources/Prototypes/_Shitmed/Body/Parts/base.yml:7-8`, inherited by every concrete part via `BasePart`). This is Shitmed's pre-existing per-part health system — it predates Wolfmed entirely and needs no new wiring for the "does the part have Damageable" question; the new Wolfmed glue system should reuse this component rather than re-declaring or re-ensuring it. |
| **Organ** | `OrganDamageComponent` (`hitChance`/`selectionWeight`/`damageMultipliers`) on internal organs only (brain/eyes/heart/lungs/liver/kidneys/stomach/tongue/appendix/ears) — a chance-per-hit accumulator, separate from Damageable entirely; no `DamageableComponent` on organs. | **No damage tracking of any kind on organs.** `WG/Resources/Prototypes/Body/Organs/human.yml`'s `BaseHumanOrganUnGibbable` has `Organ`, `Food`, `SolutionContainerManager`, `FlavorProfile`, `Tag` — nothing damage-related. This is phase-3 scope (organ damage/amputation), not phase 1; no action needed now. |

Practical takeaway for the damage-bridge author (D2): Wolfgate does **not** need an `InjurableComponent`-equivalent split for phase 1, because `DamageContainerID` already lives on `DamageableComponent` directly and every organic part already has one set to `OrganicPart` from prototype data. The Wolfgate wound-routing glue's `SetupPart`-equivalent only needs to `EnsureComp` the new Woundable-equivalent (and `BodyPartFunctionalityComponent`-equivalent, once phase 3 needs it) — the `Damageable` half of Onyx's `SetupPart` is a no-op on Wolfgate because it's already satisfied by existing Shitmed prototype data.

## 4. Every component name the port introduces (mob-wiring scope)

Registered component name = the YAML `type:` key (RT registers by class name unless overridden with `[ComponentReference]`/custom `[Prototype]` attributes — none of the components below rename themselves).

### 4.1 Directly relevant to mob wiring (phase 1)

| Registered name | Class / file | Verbatim-vendorable? |
|---|---|---|
| `WoundHost` | `WoundHostComponent`, `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:15` | Yes — no name collision found in Wolfgate. |
| `Woundable` | `WoundableComponent`, same file:156 | Yes — no collision. |
| `SystemicDamage` | `SystemicDamageComponent`, same file:300 | Yes — no collision. |
| `BodyPartFunctionality` | `BodyPartFunctionalityComponent`, same file:174 | Yes — no collision (phase 3, listed here because `SetupPart` touches it). |
| `wound` (prototype, not component) | `WoundPrototype`, `Content.Shared/_Onyx/Wounds/WoundPrototype.cs:13` | Yes — new prototype kind. |
| `bodyPartProfile` (prototype) | `BodyPartProfilePrototype`, same file:135 | Yes — new prototype kind. |
| `fractureProfile` (prototype) | `FractureProfilePrototype`, same file:211 | Yes — new prototype kind. |

### 4.2 Must NOT be vendored as-is — rename or replace (blockers, see §0.3)

| Onyx registered name | Onyx class / file | Collides with (Wolfgate) | Resolution |
|---|---|---|---|
| `BodyPart` | `Content.Shared.Body.Part.BodyPartComponent`, `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:44` | `WG/Content.Shared/Body/Part/BodyPartComponent.cs` — same namespace, same class name, same registered YAML key `BodyPart`, different fields | Do not vendor this file. Extract only `FractureProfile`/`MaxDamage`/`AmputationThresholds`/`DismembermentFinishingDamage`/`AmputationConsequenceSeverity`/`DismembermentSeverity` into a new, differently-named `_WF/Wolfmed` component (D8 already mandates this). |
| — (type, not component) `BodyPartType` | same file:15-27 (10 members incl. `Chest`, `Groin`) | `WG/Content.Shared/Body/Part/BodyPartType.cs` (8 members, no `Chest`/`Groin`) — same namespace `Content.Shared.Body.Part`, same type name | C# compile error (`CS0101`) if the file is added as-is. Any Wolfmed code that needs Onyx's `BodyPartType.Chest`/`.Groin` distinction must map onto Wolfgate's `BodyPartType.Torso` (Wolfgate doesn't distinguish chest from torso, or groin as a `BodyPartType` at all — groin is handled as its own thing only inside `_Shitmed`/`_Onyx` chest/groin extension files, needs its own check when that phase lands). |
| — `BodyPartSymmetry` | same file:29-30 | `WG/Content.Shared/Body/Part/BodyPartSymmetry.cs` — same namespace, same name | Same as above; reuse Wolfgate's existing enum, don't vendor Onyx's. |
| — `BodyPartSlot` | same file:100-112 | `WG/Content.Shared/Body/Part/BodyPartComponent.cs:230-242` — same namespace, same name (`partial record struct` vs. Wolfgate's `partial struct`) | Same; reuse Wolfgate's. |
| `Targeting` | `Content.Shared._Onyx.Targeting.TargetingComponent`, `Content.Shared/_Onyx/Targeting/TargetingComponent.cs:6` | `WG/Content.Shared/_Shitmed/Targeting/TargetingComponent.cs:11` — different namespace, but RT registers by **class name**, so both would register the YAML key `Targeting` | Do not vendor. Wound code that needs the currently-targeted part should call into Wolfgate's existing `Content.Shared._Shitmed.Targeting.TargetingComponent`/`SharedTargetingSystem` instead (already `WG`'s convention per the handoff's dependency-map row). Note the field-level mismatch too: Onyx's `TargetBodyPart` enum uses `Chest` where Wolfgate's uses `Torso` for the same slot — any adapter needs a name translation, not just a type reuse. |

### 4.3 Adjacent, not collision-checked in full (out of phase-1 scope, flagged for whoever picks up that phase)

- `PartStatus` (`Content.Shared/_Onyx/Targeting/PartStatusComponent.cs:8`) — no Wolfgate component named `PartStatus` found; likely safe, not verified beyond a name grep.
- `TargetingSnapshot` (`Content.Shared/_Onyx/Targeting/TargetingSnapshotComponent.cs:6`) — no Wolfgate collision found by name grep.
- `PainShockTarget`/`Pain` (`WoundDamageComponents.cs:103,146`) — no Wolfgate collision found; goes on `BaseSpeciesMob`, not `-Organic` (§1.1), when phase 2 lands.
- `CirculatoryStream` (`Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamComponent.cs:8`) — no Wolfgate collision found.
- Onyx's `_Onyx/HealthExaminable` folder does **not** define a new component; it partial-class-extends `HealthExaminableSystem` (`HealthExaminableSystem.Pain.cs`, `HealthExaminableSystem.PartStatus.cs`) — Wolfgate has a vanilla `Content.Shared/HealthExaminable/HealthExaminableComponent.cs` + (unverified in this pass) a `HealthExaminableSystem`; whoever ports the pain/part-status examine text needs to confirm Wolfgate's `HealthExaminableSystem` is declared `partial` and structured compatibly before assuming Onyx's partial-class trick drops in cleanly.

### 4.4 Sandbox/whitelist check (`RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml`, embedded resource, 1721 lines)

`WhitelistedNamespaces` already includes bare `Robust` and `Content` (`Sandbox.yml:24-27`), and — per a historical bug the file's own comment calls out — that also whitelists everything *prefixed* by those, e.g. `Content.Shared._Onyx.*`. So none of the new component/system classes themselves need a Sandbox.yml entry; the only thing that ever needs a whitelist entry is a **BCL/third-party type** used inside those files. Grepping every `using System...` line across `Content.Shared/_Onyx/{Wounds,Body,Targeting,Chemistry/Circulation,HealthExaminable}` found only `System.Linq` (`OrganDamageSystem.cs:1`) and `System.Numerics` (`AmputationSystem.cs:1`) beyond implicit usings. Both are already fully whitelisted: `Enumerable: { All: True }` (`Sandbox.yml:635`) and `System.Numerics` block (`Sandbox.yml:646-690`, `Vector2`/`Vector3`/etc. all `All: True`). Standard collections (`Dictionary`2`, `HashSet`1`, `Nullable`1`, `TimeSpan`) used throughout the wound components are likewise already `All: True` (`Sandbox.yml:434`, `:436`, `:1276`, `:1522` respectively). **No new Sandbox.yml entries are needed for the mob-wiring-relevant files.** This check was not re-run against the full medical/surgery/reagent-treatment tree (out of this report's remit); re-check `Content.Shared/_Onyx/{Medical,EntityEffects}` before those phases land, since reagent-effect code is more likely to touch unusual BCL surface.

## Unresolved / needs an explicit decision before coding

1. Should `WoundHost` land on `BaseMobSpeciesOrganic` (1 line, but silently reaches Diona/Slime with un-tuned Organic wounds) or on 6 explicit species-base ids, deferring Diona/Slime? (§2.3-A)
2. Per-part `amputationThresholds` overrides (§1.3/§2.3-B): copy Onyx's per-damage-type dictionary faithfully (9 small edits), or fold into Wolfgate's existing flat `severIntegrity` pattern for a smaller diff at the cost of losing per-damage-type nuance until someone rebalances? This is a phase-3 question but the component shape decided in phase 1 constrains it.
3. `Caustic` routing (§0.5/§2.3-D): fix `OrganicPart`'s damage container now, or drop Caustic from phase 1's routed/accepted set and revisit.
4. Mob-level gib threshold (§0.4/§2.3-E): exact new number for `MobDamageable`'s `Destructible` threshold once part damage projects back onto it — needs to be decided together with whoever implements `WoundDamageRoutingSystem`'s Wolfgate port, not in isolation.
