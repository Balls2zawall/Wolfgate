# Wolfmed port — Body & Organ layer (decision D8)

Scope: which parts of `ONYX/Content.Shared/_Onyx/Body` and `ONYX/Content.Server/_Onyx/Body` the wound port actually needs,
given D8 (no Nubody glue, stay on Wolfgate's Shitmed `BodyPartComponent`).

Pins:
- Onyx = `C:/tmp/onyx` @ `2f5bab9` (sparse).
- Wolfgate = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (referred to as `WG` below).

Everything below was read out of the actual files. Line numbers are from those files at the pinned state.

---

## 0. Executive summary

1. **Onyx's `BodyPartComponent` is a Nubody *replacement*, not an addition.** `ONYX/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs`
   declares `namespace Content.Shared.Body.Part;` and redefines `BodyPartType`, `BodyPartSymmetry` and `BodyPartSlot` as well.
   So the wound files' `using Content.Shared.Body.Part;` resolves to *Onyx's* type, not upstream's. That is why D8 is the right call,
   and it is also why the port needs a shim component: the 6 extra fields are not "extra", they are part of a class Wolfgate cannot adopt.
2. **Only 6 `BodyPartComponent` fields are wound-relevant, across exactly 15 use sites in 3 files**
   (`AmputationSystem.cs`, `WoundDamageRoutingSystem.cs`, `WoundFractureSystem.cs`, `FractureAlertSystem.cs`).
   One component (`WolfmedBodyPartComponent`) plus one lookup helper covers all of them.
3. **`BodyPartComponent.Parent` (an `EntityUid?`) does not exist in Wolfgate.** Three wound use sites read it. Wolfgate's
   equivalent is `SharedBodySystem.GetParentPartOrNull(EntityUid)` / `GetParentPartAndSlotOrNull(EntityUid)` (container walk, not a field).
4. **`BodyPartType.Chest` and `BodyPartType.Groin` do not exist in Wolfgate** — Wolfgate has `Torso` and no groin part at all.
   14 wound-file use sites depend on them, plus `HumanoidVisualLayers.Groin` which is also absent from Wolfgate.
5. **`SharedBodySystem.TryDetachPart` is MISSING in Wolfgate**; everything else the wounds call
   (`GetBodyChildren`, `GetBodyPartChildren`, `GetBodyChildrenOfType`, `GetPartOrgans`, `BodyHasChild`) exists with
   call-compatible signatures and equivalent semantics.
6. **Of the 24 `_Onyx/Body` files, the wounds reference exactly two:** `OrganDamageComponent.cs` and `Systems/OrganHealthSystem.cs`
   (both only from `_Onyx/Wounds/OrganDamageSystem.cs`). Everything else is Nubody/surgery/visual glue → skip.
7. **Organ health lives on Onyx's `OrganComponent`, which is also a Nubody replacement**
   (`ONYX/Content.Shared/Body/OrganComponent.cs`, `namespace Content.Shared.Body;`) carrying `Health`, `MaxHealth`,
   `DestructionWound`, `DestructionWoundSeverity`. Wolfgate's `OrganComponent` has none of these → a second shim component
   (`WolfmedOrganComponent`) is required for organ damage (phase 3).

**Blocker-grade item:** none in this area that stops phase 1. The organ-damage subsystem (phase 3) needs the second shim
component and a `// WOLFGATE`-edited `OrganDamageSystem.cs`; the part-field shim (phase 1/3) is mechanical.

---

## 1. Every reference from the wound set into `Content.Shared._Onyx.Body.*` and `Content.Shared.Body.*`

Files grepped (the task's "wound set"):
`ONYX/Content.Shared/_Onyx/Wounds/*.cs` (22 files),
`ONYX/Content.Shared/_Onyx/Medical/Tourniquet/*.cs`,
`ONYX/Content.Shared/_Onyx/HealthExaminable/*.cs`,
`ONYX/Content.Shared/_Onyx/Chemistry/Circulation/*.cs`,
`ONYX/Content.Client/_Onyx/HealthExaminable/*.cs`.

### 1.1 `Content.Shared._Onyx.Body.*` — two hits, one file

```
ONYX/Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs:5:using Content.Shared._Onyx.Body.Systems;
ONYX/Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs:6:using Content.Shared._Onyx.Body;
```

Types actually consumed from those namespaces:

| Type | Onyx file | Consumed at | Purpose |
|---|---|---|---|
| `OrganDamageComponent` | `_Onyx/Body/OrganDamageComponent.cs` | `OrganDamageSystem.cs:49, 60, 78, 90, 95` | per-organ hit chance / weight / damage multipliers / cap |
| `OrganHealthSystem` | `_Onyx/Body/Systems/OrganHealthSystem.cs` | `OrganDamageSystem.cs:25` (`[Dependency]`), `:72` (`ChangeHealth`) | applies the damage to organ HP |

**No other `_Onyx/Body` type is referenced by the wound set.** Verified negative for:
`FunctionalOrganComponent`, `OrganConsequenceComponents` (and its `BodyAnatomyComponent`, `BodyOrgansChangedEvent`,
`MissingEarsComponent`, `MissingEyesComponent`, `BreathingImmunityComponent`, `MissingHeadComponent`, `InitiallyLungedComponent`),
`TaggedOrganComponent`/`TaggedOrganSystem`, `ProfileOrgansComponent`, `OrganEffectSystem`, `StorageOrgan*`, `OrganAction*`,
`BodyPartReplacementComponent`, `TransplantCompatibility*`, `DeclarativeBodySystem`, `BodyInventorySlotSystem`,
`VisualOrganActivity*`, `DirectionalLimbLayer*`, `BodyGraphPrototype`.

### 1.2 `Content.Shared.Body.*` — the `using` lines

```
WoundSystem.cs:2                using Content.Shared.Body;              (BodyComponent)
WoundSystem.cs:3                using Content.Shared.Body.Systems;
WoundStatusEffectSystem.cs:1-3  Body, Body.Part, Body.Systems
WoundScarSystem.cs:1            Body.Part
WoundPrototype.cs:3-4           Body, Body.Part
WoundInternalBleedingSystem.cs:1-3  Body.Components, Body.Part, Body.Systems
WoundHealingSystem.cs:2-3       Body.Components, Body.Systems
WoundFractureSystem.cs:1        Body.Part
WoundEvents.cs:2                Body.Part
WoundDamageRoutingSystem.cs:2,9 Body.Systems, Body.Part
WoundDamageProjectionSystem.cs:2-4  Body, Body.Part, Body.Systems
WoundDamageComponents.cs:3      Body.Part
WoundBleedingSystem.cs:3-6      Body, Body.Components, Body.Part, Body.Systems
ReagentTreatmentSystems.cs:5    Body.Systems
PainSystem.cs:2                 Body.Part
OrganDamageSystem.cs:2-4        Body, Body.Part, Body.Systems
FractureEffectsSystem.cs:1-3    Body, Body.Part, Body.Systems
FractureAlertSystem.cs:2-4      Body, Body.Part, Body.Systems
BodyPartFunctionalitySystem.cs:1-3  Body, Body.Part, Body.Systems
AmputationSystem.cs:2-3         Body.Part, Body.Systems
Medical/Tourniquet/TourniquetSystem.cs:4         Body.Systems
HealthExaminable/...PartStatus.cs:4-5            Body.Part, Body.Systems
HealthExaminable/...Pain.cs:2                    Body.Systems
Chemistry/Circulation/CirculatoryStreamSystem.cs:1-5  Body.Components, Body.Events, Body.Part, Body.Systems, Body
```

Component types actually named in the wound set (occurrence counts):

| Type | Count | Wolfgate status |
|---|---|---|
| `BodyPartComponent` | 39 | exists, **different shape** (see §2) |
| `BloodstreamComponent` | 10 | **SERVER-ONLY** in Wolfgate — `WG/Content.Server/Body/Components/BloodstreamComponent.cs`. Onyx's is shared. Not this report's scope (bleeding agent), but it breaks compilation of shared `WoundBleedingSystem.cs` / `WoundInternalBleedingSystem.cs` / `CirculatoryStreamSystem.cs`. |
| `MobStateComponent` | 4 | same (`Content.Shared.Mobs.Components`) |
| `BodyPartFunctionalityComponent` | 3 | Onyx-local (`_Onyx/Wounds/WoundDamageComponents.cs`), ports with wounds |
| `OrganComponent` | 2 | exists, **different shape**, only in `OrganDamageSystem.PickOrgan` |
| `BodyComponent` | 2 | exists. **Namespace differs**: Onyx `Content.Shared.Body`, Wolfgate `Content.Shared.Body.Components` (`WG/Content.Shared/Body/Components/BodyComponent.cs:8`). Used at `WoundSystem.cs:34, :122` (`RejuvenateEvent`). One-line `using` fix. |

`Content.Shared.Body.Events` is imported by `CirculatoryStreamSystem.cs:2`; Wolfgate has no `Content.Shared/Body/Events`
directory (`WG/Content.Shared/Body/` contains only `Components/`, `Organ/`, `Part/`, `Systems/`, plus loose files) — flagged
for the circulation agent, out of scope here.

---

## 2. The Onyx `BodyPartComponent` fields the wounds need, and the Wolfgate shim

### 2.1 Side-by-side of the two components

Onyx — `ONYX/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs` (namespace `Content.Shared.Body.Part`, so it *is* the game's body-part component there):

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BodyPartComponent : Component
{
    public const string PartSlotPrefix = "body_part_slot_";
    public const string OrganSlotPrefix = "body_organ_slot_";
    [DataField, AutoNetworkedField] public EntityUid? Body;
    [DataField, AutoNetworkedField] public EntityUid? Parent;
    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartType> Children = new();
    [DataField, AutoNetworkedField] public Dictionary<string, BodyPartSlot> ChildSlots = new();
    [DataField, AutoNetworkedField] public HashSet<string> Organs = new();
    [DataField, AutoNetworkedField] public BodyPartType PartType = BodyPartType.Other;
    [DataField, AutoNetworkedField] public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;
    [DataField("vital"), AutoNetworkedField] public bool IsVital;
    [DataField, AutoNetworkedField] public ProtoId<SpeciesPrototype>? Species;
    [DataField, AutoNetworkedField] public ProtoId<OrganCategoryPrototype>? Category;
    [DataField] public HashSet<DirtExposure> DirtExposures = [...];
    [DataField] public List<SlotFlags> DirtCoverageLayers = [];

    /// <summary>Fracture profile for this part. Null = no fractures.</summary>
    [DataField] public ProtoId<FractureProfilePrototype>? FractureProfile;
    [DataField] public FixedPoint2 MaxDamage;
    [DataField] public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> AmputationThresholds = new();
    [DataField] public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DismembermentFinishingDamage = new();
    [DataField] public FixedPoint2 AmputationConsequenceSeverity = 35;
    [DataField] public FixedPoint2? DismembermentSeverity;
}
```

Wolfgate — `WG/Content.Shared/Body/Part/BodyPartComponent.cs:18-225`:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyPartComponent : Component, ISurgeryToolComponent // Shitmed Change
```

Relevant shape differences (all verified):

| Concept | Onyx | Wolfgate | Consequence |
|---|---|---|---|
| parent link | `EntityUid? Parent` | `BodyPartSlot? ParentSlot` (`:31-32`) — id+type only, **no entity** | must use `GetParentPartOrNull` / `GetParentPartAndSlotOrNull` |
| children | `Dictionary<string, BodyPartType> Children` + `Dictionary<string, BodyPartSlot> ChildSlots` | `Dictionary<string, BodyPartSlot> Children` (`:180-181`) | internal to the body system; wounds never touch it |
| organs | `HashSet<string> Organs` | `Dictionary<string, OrganSlot> Organs` (`:186-187`) | internal; wounds only call `GetPartOrgans` |
| slot container prefix | `BodyPartComponent.PartSlotPrefix`/`OrganSlotPrefix` consts | `SharedBodySystem.GetPartSlotContainerId(slotId)` / `GetOrganContainerId(slotId)` (`WG/Content.Shared/Body/Systems/SharedBodySystem.cs:73, :81`, both `public static`) | adapter, only needed if porting `OrganHealthSystem` |
| the 6 wound fields | present | **absent** | → `WolfmedBodyPartComponent` |
| Shitmed-only | — | `SeverIntegrity = 130` (`:124`), `IntegrityThresholds` (`:138-147`), `Enabled`/`CanEnable`, `HealingTime`/`SelfHealingAmount`, `VitalDamage`, `ItemInsertionSlot`, `Species` as `string` | D2 must disable `SeverIntegrity` and self-heal for wound hosts |

### 2.2 `BodyPartType` / visual-layer enum gap (affects far more sites than the 6 fields)

Onyx `BodyPartType` (`ONYX/.../Part/BodyPartComponent.cs:15-28`):
`Other=0, Torso=1, Head=2, Arm=3, Hand=4, Leg=5, Foot=6, Tail=7, Chest=8, Groin=9`

Wolfgate `BodyPartType` (`WG/Content.Shared/Body/Part/BodyPartType.cs:10-20`):
`Other=0, Torso, Head, Arm, Hand, Leg, Foot, Tail` — **no `Chest`, no `Groin`.**

Wolfgate's Shitmed already treats groin as torso:
`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:395`
```csharp
TargetBodyPart.Groin => (BodyPartType.Torso, BodyPartSymmetry.None), // TODO: Groin is not a part type yet
```
and groin is not even a selectable target (`WG/Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs:13` has
`//TargetBodyPart.Groin,` commented out).

`WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs` has `Chest` (`:15`) but **no `Groin`** layer.

Mapping rule for the whole port: **`BodyPartType.Chest` → `BodyPartType.Torso`; `BodyPartType.Groin` → delete the entry/case.**
`TargetBodyPart.Chest` (used at `WoundDamageComponents.cs` `SystemicPainTarget`) → `TargetBodyPart.Torso`
(`WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:15` — Wolfgate's flags enum has `Torso` and `Groin`, no `Chest`).

Full list of affected wound-set lines:

| File:line | Onyx text | `// WOLFGATE` edit |
|---|---|---|
| `AmputationSystem.cs:35` | `bodyPart.PartType == BodyPartType.Chest \|\|` | `BodyPartType.Torso` |
| `AmputationSystem.cs:73` | `bodyPart.PartType is BodyPartType.Chest \|\| ...` | `BodyPartType.Torso` |
| `AmputationSystem.cs:114` | `bodyPart.PartType == BodyPartType.Chest)` | `BodyPartType.Torso` |
| `WoundDamageRoutingSystem.cs:883` | `part.PartType == BodyPartType.Chest \|\|` | `BodyPartType.Torso` |
| `WoundDamageComponents.cs:20-21` | `[BodyPartType.Chest] = 2.5f,` / `[BodyPartType.Groin] = 1.5f,` | `[BodyPartType.Torso] = 2.5f,` ; drop the Groin entry |
| `WoundDamageComponents.cs:53` | `[BodyPartType.Groin] = 160,` | drop |
| `WoundDamageProjectionSystem.cs:239` | `case (BodyPartType.Chest, _): layer = HumanoidVisualLayers.Chest;` | `case (BodyPartType.Torso, _):` |
| `WoundDamageProjectionSystem.cs:240` | `case (BodyPartType.Groin, _): layer = HumanoidVisualLayers.Groin;` | delete (no such layer in Wolfgate) |
| `HealthExaminableSystem.PartStatus.cs:135-136` | `BodyPartType.Chest => 1,` / `BodyPartType.Groin => 2,` | `BodyPartType.Torso => 1,` ; drop Groin |

### 2.3 Every use site of the six wound fields

Verified by grepping `\.<Field>\b` across the wound set. There are **15 field use sites** in **4 files**.

#### `FractureProfile` (2)

| Site | Code |
|---|---|
| `FractureAlertSystem.cs:24` | `if (bodyPart.FractureProfile is not { } profileId \|\|` |
| `WoundFractureSystem.cs:146` | `var profileId = bodyPart.FractureProfile;` |

#### `MaxDamage` (4)

| Site | Code |
|---|---|
| `AmputationSystem.cs:36` | `bodyPart.MaxDamage <= FixedPoint2.Zero)` |
| `AmputationSystem.cs:44` | `if (part.Comp.AmputationOverflow >= bodyPart.MaxDamage)` |
| `WoundDamageRoutingSystem.cs:730` | `bodyPart.MaxDamage <= FixedPoint2.Zero \|\|` |
| `WoundDamageRoutingSystem.cs:735` | `var remaining = bodyPart.MaxDamage - current;` |

#### `AmputationThresholds` (7)

| Site | Code |
|---|---|
| `AmputationSystem.cs:74` | `bodyPart.AmputationThresholds.Count == 0 \|\|` |
| `AmputationSystem.cs:84` | `if (ReachedThreshold(damage, bodyPart.AmputationThresholds))` |
| `AmputationSystem.cs:91` | `if (GetThresholdProgress(damage, bodyPart.AmputationThresholds) < GetResetRatio(args.Body))` |
| `AmputationSystem.cs:103` | `if (ReachedThreshold(damageBeforeHit, bodyPart.AmputationThresholds) &&` |
| `AmputationSystem.cs:158` | `if (amount <= FixedPoint2.Zero \|\| !part.AmputationThresholds.ContainsKey(type))` |
| `AmputationSystem.cs:188` | `var chance = Math.Clamp(GetThresholdProgress(totalDamage, bodyPart.AmputationThresholds) * 0.5f, 0f, 1f);` |
| `WoundDamageRoutingSystem.cs:885` | `part.AmputationThresholds.Count == 0)` |

#### `DismembermentFinishingDamage` (1)

| Site | Code |
|---|---|
| `AmputationSystem.cs:161` | `var minimum = part.DismembermentFinishingDamage.GetValueOrDefault(type, host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type));` |

#### `AmputationConsequenceSeverity` (1)

| Site | Code |
|---|---|
| `AmputationSystem.cs:64` | `_wounds.CreateOrMergeWound(parent, host.AmputationConsequenceWound, parentPart.AmputationConsequenceSeverity);` |

#### `DismembermentSeverity` (1)

| Site | Code |
|---|---|
| `AmputationSystem.cs:123` | `bodyPart.DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType));` |

#### `Parent` — not in the task list, but it is the same class of problem (3 sites)

| Site | Code | Wolfgate equivalent |
|---|---|---|
| `AmputationSystem.cs:73` | `bodyPart.Parent == null \|\|` | `_body.GetParentPartOrNull(part) is null` |
| `AmputationSystem.cs:117` | `var parent = bodyPart.Parent ?? part;` | `var parent = _body.GetParentPartOrNull(part) ?? part;` |
| `WoundDamageProjectionSystem.cs:115` | `while (CompOrNull<BodyPartComponent>(root)?.Parent is { } parent)` | `while (_body.GetParentPartOrNull(root) is { } parent)` |
| `WoundDamageRoutingSystem.cs:884` | `part.Parent == null \|\|` | `_body.GetParentPartOrNull(parts[i]) is null` |

Fields of Onyx's `BodyPartComponent` the wound set does **not** touch (confirmed zero hits):
`DirtExposures`, `DirtCoverageLayers`, `Category`, `IsVital`, `Species`, `ChildSlots`, `Children`, `Organs`,
`PartSlotPrefix`/`OrganSlotPrefix`. (`Organs` and the prefixes *are* used by `OrganHealthSystem` — see §3.)

### 2.4 Proposed `WolfmedBodyPartComponent`

`WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs` (new; `_WF` style — no licence header, one-line `<summary>`):

```csharp
using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.Body;

/// <summary>
/// Wound-system data Onyx keeps on its own BodyPartComponent. Wolfgate stays on Shitmed's part component,
/// so the fields live here instead.
/// </summary>
[RegisterComponent]
public sealed partial class WolfmedBodyPartComponent : Component
{
    /// <summary>Fracture profile for this part. Null = no fractures.</summary>
    [DataField]
    public ProtoId<FractureProfilePrototype>? FractureProfile;

    /// <summary>Structural damage cap; damage past it becomes tear-off pressure instead.</summary>
    [DataField]
    public FixedPoint2 MaxDamage;

    /// <summary>Per-damage-type totals at which the part becomes severable.</summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> AmputationThresholds = new();

    /// <summary>Minimum follow-up hit per damage type needed to detach a ruined part.</summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DismembermentFinishingDamage = new();

    /// <summary>Severity of the consequence wound left on the parent when this part is torn off.</summary>
    [DataField]
    public FixedPoint2 AmputationConsequenceSeverity = 35;

    /// <summary>Overrides the host's per-part-type dismemberment severity.</summary>
    [DataField]
    public FixedPoint2? DismembermentSeverity;
}
```

Networking note: Onyx marks none of these six `[AutoNetworkedField]` (only `Body`/`Parent`/`PartType`/… are auto-networked),
and nothing mutates them at runtime, so plain `[RegisterComponent]` with `[DataField]`s is sufficient — the client gets the
values from the prototype when it spawns the part. Two sites read them outside an `IsServer` guard
(`FractureAlertSystem.Refresh`, `WoundFractureSystem.TryGetProfile`); both only read prototype-sourced data, so this holds.

### 2.5 The lookup helper (so vendored files stay near-verbatim)

`WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartSystem.cs`:

```csharp
namespace Content.Shared._WF.Wolfmed.Body;

/// <summary>Reads Wolfmed part data off Shitmed body parts, with a no-wounds default.</summary>
public sealed class WolfmedBodyPartSystem : EntitySystem
{
    private static readonly WolfmedBodyPartComponent None = new();

    /// <summary>Wolfmed data for a part; a zeroed default when the part has none.</summary>
    public WolfmedBodyPartComponent Get(EntityUid part) =>
        CompOrNull<WolfmedBodyPartComponent>(part) ?? None;
}
```

The shared `None` instance makes every call site a straight textual swap with no added null handling, and it defaults
`MaxDamage = 0` / empty threshold dicts, which is exactly the "this part does not participate" branch Onyx already codes for
(`AmputationSystem.cs:36`, `:74`; `WoundDamageRoutingSystem.cs:730`, `:885`).

### 2.6 Exact `// WOLFGATE` edit for every use site

`AmputationSystem` gains two dependencies at the top of the class:

```csharp
    [Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE
```

**`_Onyx/Wounds/AmputationSystem.cs`**

| Line | before | after |
|---|---|---|
| 34-37 | `!TryComp(part, out BodyPartComponent? bodyPart) \|\| bodyPart.Body == null \|\|`<br>`bodyPart.PartType == BodyPartType.Chest \|\|`<br>`bodyPart.MaxDamage <= FixedPoint2.Zero)` | `!TryComp(part, out BodyPartComponent? bodyPart) \|\| bodyPart.Body == null \|\|`<br>`bodyPart.PartType == BodyPartType.Torso \|\| // WOLFGATE: Wolfgate has no Chest part type`<br>`_wfPart.Get(part).MaxDamage <= FixedPoint2.Zero) // WOLFGATE` |
| 39 | `TryExplosionAmputate(args.Body, part, bodyPart, args.Damage)` | unchanged call; change the helper's signature (see line 170 below) |
| 44 | `if (part.Comp.AmputationOverflow >= bodyPart.MaxDamage)` | `if (part.Comp.AmputationOverflow >= _wfPart.Get(part).MaxDamage) // WOLFGATE` |
| 57-64 | `!TryComp(parent, out BodyPartComponent? parentPart))` … `parentPart.AmputationConsequenceSeverity` | keep the `TryComp` (it still guards "is a body part"), replace the read: `_wfPart.Get(parent).AmputationConsequenceSeverity // WOLFGATE` |
| 72-75 | `bodyPart.PartType is BodyPartType.Chest \|\| bodyPart.Parent == null \|\|`<br>`bodyPart.AmputationThresholds.Count == 0 \|\|` | `bodyPart.PartType is BodyPartType.Torso \|\| _body.GetParentPartOrNull(part) is null \|\| // WOLFGATE`<br>`_wfPart.Get(part).AmputationThresholds.Count == 0 \|\| // WOLFGATE` |
| 84 | `ReachedThreshold(damage, bodyPart.AmputationThresholds)` | `ReachedThreshold(damage, _wfPart.Get(part).AmputationThresholds) // WOLFGATE` |
| 91 | `GetThresholdProgress(damage, bodyPart.AmputationThresholds)` | `GetThresholdProgress(damage, _wfPart.Get(part).AmputationThresholds) // WOLFGATE` |
| 103 | `ReachedThreshold(damageBeforeHit, bodyPart.AmputationThresholds) &&`<br>`IsFinishingHit(args.Body, bodyPart, args.Damage)` | `ReachedThreshold(damageBeforeHit, _wfPart.Get(part).AmputationThresholds) && // WOLFGATE`<br>`IsFinishingHit(args.Body, part.Owner, args.Damage) // WOLFGATE` |
| 113-118 | `bodyPart.PartType == BodyPartType.Chest)` … `var parent = bodyPart.Parent ?? part;` … `if (!_body.TryDetachPart(part))` | `bodyPart.PartType == BodyPartType.Torso) // WOLFGATE`<br>`var parent = _body.GetParentPartOrNull(part) ?? part; // WOLFGATE`<br>`if (!_wfBody.TryDetachPart(part)) // WOLFGATE: see §4.6` |
| 123 | `bodyPart.DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType)` | `_wfPart.Get(part).DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType) // WOLFGATE` |
| 151 | `private bool IsFinishingHit(EntityUid body, BodyPartComponent part, DamageSpecifier damage)` | `private bool IsFinishingHit(EntityUid body, EntityUid part, DamageSpecifier damage) // WOLFGATE: resolve Wolfmed data from the uid` + `var wf = _wfPart.Get(part);` as the first line |
| 158 | `!part.AmputationThresholds.ContainsKey(type)` | `!wf.AmputationThresholds.ContainsKey(type)` |
| 161 | `part.DismembermentFinishingDamage.GetValueOrDefault(...)` | `wf.DismembermentFinishingDamage.GetValueOrDefault(...)` |
| 170-177 | `private bool TryExplosionAmputate(EntityUid body, Entity<WoundableComponent> part, BodyPartComponent bodyPart, …)` … `if (!IsFinishingHit(body, bodyPart, hit))` | drop the `BodyPartComponent bodyPart` parameter; `if (!IsFinishingHit(body, part.Owner, hit))` — callers at 39 and 79 lose the `bodyPart` argument. `// WOLFGATE` |
| 188 | `GetThresholdProgress(totalDamage, bodyPart.AmputationThresholds)` | `GetThresholdProgress(totalDamage, _wfPart.Get(part.Owner).AmputationThresholds) // WOLFGATE` |

**`_Onyx/Wounds/WoundDamageRoutingSystem.cs`**

| Line | before | after |
|---|---|---|
| 729-730 | `!TryComp(part, out BodyPartComponent? bodyPart) \|\|`<br>`bodyPart.MaxDamage <= FixedPoint2.Zero \|\|` | keep `TryComp` (still gates "is a part"); `_wfPart.Get(part).MaxDamage <= FixedPoint2.Zero \|\| // WOLFGATE` |
| 735 | `var remaining = bodyPart.MaxDamage - current;` | `var remaining = _wfPart.Get(part).MaxDamage - current; // WOLFGATE` |
| 883-885 | `part.PartType == BodyPartType.Chest \|\|`<br>`part.Parent == null \|\|`<br>`part.AmputationThresholds.Count == 0)` | `part.PartType == BodyPartType.Torso \|\| // WOLFGATE`<br>`_body.GetParentPartOrNull(parts[i]) is null \|\| // WOLFGATE`<br>`_wfPart.Get(parts[i]).AmputationThresholds.Count == 0) // WOLFGATE` |

**`_Onyx/Wounds/FractureAlertSystem.cs`**

| Line | before | after |
|---|---|---|
| 22-24 | `foreach (var (part, bodyPart) in _body.GetBodyChildren(uid))`<br>`if (bodyPart.FractureProfile is not { } profileId \|\|` | `foreach (var (part, _) in _body.GetBodyChildren(uid)) // WOLFGATE`<br>`if (_wfPart.Get(part).FractureProfile is not { } profileId \|\| // WOLFGATE` |

**`_Onyx/Wounds/WoundFractureSystem.cs`**

| Line | before | after |
|---|---|---|
| 143-146 | `if (!Resolve(part, ref part.Comp, false) \|\| !TryComp(part, out BodyPartComponent? bodyPart)) return false;`<br>`var profileId = bodyPart.FractureProfile;` | keep the `TryComp` guard; `var profileId = _wfPart.Get(part).FractureProfile; // WOLFGATE` |

**`_Onyx/Wounds/WoundDamageProjectionSystem.cs`**

| Line | before | after |
|---|---|---|
| 115 | `while (CompOrNull<BodyPartComponent>(root)?.Parent is { } parent)` | `while (_body.GetParentPartOrNull(root) is { } parent) // WOLFGATE` |
| 239-240 | `case (BodyPartType.Chest, _): layer = HumanoidVisualLayers.Chest; return true;`<br>`case (BodyPartType.Groin, _): layer = HumanoidVisualLayers.Groin; return true;` | `case (BodyPartType.Torso, _): layer = HumanoidVisualLayers.Chest; return true; // WOLFGATE`<br>*(delete the Groin case — Wolfgate has no such layer)* |

**Alternative considered and rejected:** putting `FractureProfile` on Onyx's own `BodyPartProfilePrototype`
(`WoundPrototype.cs:135`, referenced by `WoundableComponent.Profile`, `WoundDamageComponents.cs:161`). That would remove
two of the fifteen edits, but it is an Onyx-content change (a re-sync would fight it) and it cannot carry per-part
`AmputationThresholds`, which differ per limb. Rejected: one shim component is the smaller and more re-sync-friendly delta.

---

## 3. `_Onyx/Body` file-by-file verdict

All 24 shared files + 3 server files. "Needed by wounds" = referenced from the wound set defined in §1.

| Onyx file | Needed by wounds? | Depends on | Verdict |
|---|---|---|---|
| `Body/OrganDamageComponent.cs` | **Yes** — `OrganDamageSystem.cs:49,60,78,90,95` | nothing but `DamageTypePrototype`; pure data | **PORT verbatim** into `Content.Shared/_Onyx/Body/OrganDamageComponent.cs`. Compiles against Wolfgate as-is. |
| `Body/Systems/OrganHealthSystem.cs` | **Yes** — `OrganDamageSystem.cs:25,72` | Nubody `OrganComponent.Health/MaxHealth/DestructionWound/DestructionWoundSeverity`; `_body.TryGetOrganInSlot`, `_body.TryRemoveOrgan`; `BodyPartComponent.Organs` as `HashSet<string>` | **ADAPT** — see §3.1. Needs `WolfmedOrganComponent` + Wolfgate's `RemoveOrgan`. |
| `Body/FunctionalOrganComponent.cs` | No | declares `OrganFunctionChangedEvent`, raised by `OrganHealthSystem.SetHealth` (`:58`) and consumed only by `Content.Server/_Onyx/Body/OrganEffectSystem.cs` | **PORT the event, SKIP the component.** `OrganFunctionChangedEvent` must exist for `OrganHealthSystem` to compile; move it into the Wolfmed organ file (or port the file and leave the component unused/unattached). |
| `Body/OrganConsequenceComponents.cs` | No — zero hits for `BodyAnatomyComponent`, `BodyOrgansChangedEvent`, `MissingEars/Eyes/Head`, `BreathingImmunity`, `InitiallyLunged` | `BodyAnatomyComponent` is written only by Nubody `InitializeAnatomy` (`_Onyx SharedBodySystem.cs:160-181`); the Missing* markers are consumed by `OrganEffectSystem` | **SKIP** (phase 1-3). Revisit in a later "missing organ consequences" phase; it needs `OrganEffectSystem`, which needs Nubody + `StatusEffectNew` + Onyx surgery. |
| `Body/TaggedOrganComponent.cs`, `Body/TaggedOrganSystem.cs` | No — zero hits | tags, organ insert/remove events | **SKIP.** |
| `Body/ProfileOrgansComponent.cs`, `Body/SharedVisualBodySystem.ProfileOrgans.cs` | No | `OrganCategoryPrototype`, `HumanoidVisualLayers`, Nubody visual body | **SKIP** (pure Nubody character-creation glue). |
| `Content.Server/_Onyx/Body/OrganEffectSystem.cs` | No | `using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;`, `Content.Shared._Onyx.Medical.Surgery`, `Content.Shared._Onyx.Surgery.Organs`, `Content.Shared.StatusEffectNew`, `Content.Shared._Onyx.Speech`, Nubody `OrganCategoryPrototype` | **SKIP.** Violates D7 (no Onyx surgery) and pulls the whole Nubody organ graph. |
| `Content.Server/_Onyx/Body/BodyStasis.cs`, `RespiratorComponent.cs` | No | respirator/stasis, Nubody | **SKIP.** |
| `Body/Part/BodyPartComponent.cs` | — | *is* the Nubody part component | **SKIP** (D8). Replaced by §2.4. |
| `Body/Systems/SharedBodySystem.cs` | — | *is* Nubody | **SKIP** (D8). See §4. |
| `Body/Systems/DeclarativeBodySystem.cs`, `Body/Systems/BodyInventorySlotSystem.cs` | No | Nubody graph / inventory slots from organs | **SKIP.** |
| `Body/Prototypes/BodyGraphPrototype.cs`, `Body/Prototypes/TransplantCompatibilityPrototype.cs`, `Body/TransplantCompatibilityComponent.cs` | No | Nubody / Onyx surgery | **SKIP.** |
| `Body/StorageOrganComponent.cs`, `StorageOrganSystem.cs` | No | Nubody | **SKIP.** |
| `Body/OrganActionComponent.cs`, `OrganActionSystem.cs` | No | Nubody; Wolfgate already has `_Shitmed/Body/Actions/organactions.yml` | **SKIP.** |
| `Body/BodyPartReplacementComponent.cs` | No | Nubody | **SKIP.** |
| `Body/DirectionalLimbLayerComponent.cs` + `Content.Client/_Onyx/Body/DirectionalLimbLayerSystem.cs` | No | Nubody visuals | **SKIP.** |
| `Body/VisualOrganActivityComponent.cs`, `VisualOrganActivitySystem.cs`, `SharedVisualBodySystem.Sex.cs` | No | Nubody visuals | **SKIP.** |

Net: **1 file ported verbatim, 1 file adapted, 1 event relocated, 25 files skipped.**

### 3.1 `OrganHealthSystem` — what it needs and how to adapt it

Onyx's version (`ONYX/Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs`, 92 lines) reads four fields off the
**Nubody** `OrganComponent` (`ONYX/Content.Shared/Body/OrganComponent.cs`, namespace `Content.Shared.Body`):

```csharp
    // <Onyx-OrganHealth>
    [DataField, AutoNetworkedField] public FixedPoint2 Health = FixedPoint2.New(15);
    [DataField, AutoNetworkedField] public FixedPoint2 MaxHealth = FixedPoint2.New(15);
    // </Onyx-OrganHealth>
    // <Onyx-OrganDamage>
    [DataField] public ProtoId<_Onyx.Wounds.WoundPrototype>? DestructionWound;
    [DataField] public FixedPoint2 DestructionWoundSeverity;
    // </Onyx-OrganDamage>
```

Wolfgate's `OrganComponent` (`WG/Content.Shared/Body/Organ/OrganComponent.cs:12-78`) has none of them. It does have
`Body`, `Enabled`, `CanEnable`, `SlotId`, `OnAdd`/`OnRemove`, `Removable`, `Used`, `ToolName`, `Speed`, `OriginalBody`.

Proposed `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs`:

```csharp
using Content.Shared._Onyx.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.Body;

/// <summary>Organ hit points and destruction wound, which Onyx keeps on its own OrganComponent.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WolfmedOrganComponent : Component
{
    /// <summary>Current organ health; at zero the organ stops working and is destroyed.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Health = FixedPoint2.New(15);

    [DataField, AutoNetworkedField]
    public FixedPoint2 MaxHealth = FixedPoint2.New(15);

    /// <summary>Wound left on the containing part when this organ is destroyed.</summary>
    [DataField]
    public ProtoId<WoundPrototype>? DestructionWound;

    [DataField]
    public FixedPoint2 DestructionWoundSeverity;
}
```

(`Health` *is* mutated at runtime, so unlike the part component this one genuinely needs `AutoNetworkedField` —
the health analyzer and examine text in phase 4 read it client-side.)

`// WOLFGATE` edits inside the vendored `OrganHealthSystem.cs`:

| Line | before | after |
|---|---|---|
| 27-28 | `var query = EntityQueryEnumerator<OrganComponent>();`<br>`while (query.MoveNext(out var uid, out var organ))` | `EntityQueryEnumerator<WolfmedOrganComponent>()` — iterate the Wolfmed component instead |
| 30 | `if (organ.Health > FixedPoint2.Zero) continue;` | unchanged (now on the Wolfmed comp) |
| 33-41 | `HasComp<BrainComponent>(uid)` … `organ.Body` | `organ.Body` is not on the Wolfmed comp → `CompOrNull<OrganComponent>(uid)?.Body`. `BrainComponent` exists in Wolfgate at `WG/Content.Shared/Body/Components/` (via `Content.Shared.Body.Components`) — namespace already matches Onyx's `using Content.Shared.Body.Components;` |
| 48-63 | `SetHealth(Entity<OrganComponent> organ, …)` / `ChangeHealth` | retype to `Entity<WolfmedOrganComponent>`; `organ.Comp.Body` → `CompOrNull<OrganComponent>(organ)?.Body` |
| 65-90 | `DestroyOrgan`: `foreach (var slot in part.Organs)` (a `HashSet<string>`), `_body.TryGetOrganInSlot(parent, slot, out var slotted)`, `_body.TryRemoveOrgan(parent, slot, out var removed)` | Wolfgate's `part.Organs` is `Dictionary<string, OrganSlot>` and there is **no** `TryGetOrganInSlot`/`TryRemoveOrgan`. Replace the whole loop with `_body.RemoveOrgan(organ.Owner)` (`WG/Content.Shared/Body/Systems/SharedBodySystem.Organs.cs:172`), which finds the containing part itself: <br>`var parent = Transform(organ).ParentUid;`<br>`if (_body.RemoveOrgan(organ.Owner)) { /* create the destruction wound on parent */ QueueDel(organ); return; }` |

`_body.RemoveOrgan` signature, for the record — `WG/Content.Shared/Body/Systems/SharedBodySystem.Organs.cs:172-181`:

```csharp
    public bool RemoveOrgan(EntityUid organId, OrganComponent? organ = null)
    {
        if (!Containers.TryGetContainingContainer((organId, null, null), out var container))
            return false;
        var parent = container.Owner;
        return HasComp<BodyPartComponent>(parent) && Containers.Remove(organId, container);
    }
```

`// WOLFGATE` edits in the vendored `OrganDamageSystem.cs`:

| Line | before | after |
|---|---|---|
| 48-50 | `_body.GetPartOrgans(part).Where(organ => organ.Component.Health > FixedPoint2.Zero && HasComp<OrganDamageComponent>(organ.Id))` | `.Where(organ => CompOrNull<WolfmedOrganComponent>(organ.Id) is { Health: var h } && h > FixedPoint2.Zero && HasComp<OrganDamageComponent>(organ.Id))` |
| 69 | `applied = FixedPoint2.Min(applied, organ.Component.MaxHealth * policy.MaxDamageFraction);` | `… Comp<WolfmedOrganComponent>(organ.Id).MaxHealth * …` |
| 72 | `_organHealth.ChangeHealth((organ.Id, organ.Component), -applied);` | `_organHealth.ChangeHealth((organ.Id, Comp<WolfmedOrganComponent>(organ.Id)), -applied);` |
| 39 | `bodyPart.Body == null` | unchanged — Wolfgate's `BodyPartComponent.Body` exists (`:26-27`) |

Note that `OrganDamageSystem.OnPartDamageApplied` (`:32-37`) is also the **single dispatch point** for
`WoundSystem`, `WoundFractureSystem`, `AmputationSystem` and `WoundBleedingSystem` `HandlePartDamageApplied`.
If organ damage is deferred to phase 3, the file still has to be ported in phase 1 with the organ half
`#if`-free but guarded — simplest is to port the whole file and just not put `OrganDamageComponent` /
`WolfmedOrganComponent` on any prototype until phase 3, at which point `organs.Count == 0` (`:51-52`) short-circuits it.

---

## 4. Body API: Onyx vs Wolfgate, method by method

### 4.0 Structural finding

`ONYX/Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:12` declares:

```csharp
public sealed partial class SharedBodySystem : EntitySystem
```

and `git grep -l "partial class SharedBodySystem" HEAD` over the whole Onyx tree returns **exactly that one file**.
`ONYX/Content.Shared/Body/Systems/` contains only `BloodstreamSystem.cs`, `BrainSystem.cs`, `LungSystem.cs`,
`SharedInternalsSystem.cs`, `StomachSystem.cs` — there is **no** `SharedBodySystem.Body/Parts/Organs` there.

So it is not "an Onyx partial on top of upstream": Onyx **replaced** the upstream body system with a single 578-line
Nubody file. Wolfgate's equivalent is spread over
`WG/Content.Shared/Body/Systems/SharedBodySystem.cs` (+`.Body.cs`, `.Parts.cs`, `.Organs.cs`) plus
`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`.
Nothing about the Onyx file is portable; only the *call surface* matters.

### 4.1 `GetBodyChildren` — **SAME**

Onyx `SharedBodySystem.cs:133`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body)
```
Wolfgate `WG/Content.Shared/Body/Systems/SharedBodySystem.Body.cs:256-259`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(
        EntityUid? id,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
```
Call-compatible (`EntityUid` → `EntityUid?` implicit; extra params defaulted). Both resolve
`BodyComponent.RootContainer.ContainedEntity` then delegate to `GetBodyPartChildren(root)`, which yields the root itself.
**Same element set, same order (depth-first from root).** 12 call sites in the wound set — no edit.

### 4.2 `GetBodyPartChildren` — **SAME**

Onyx `SharedBodySystem.cs:551`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(EntityUid part)
```
Wolfgate `SharedBodySystem.Parts.cs:880-882`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(
        EntityUid partId,
        BodyPartComponent? part = null)
```
Both `yield return (part, self)` first, then recurse into each child slot's container. Only difference is the container-id
scheme (`BodyPartComponent.PartSlotPrefix + slot` vs `GetPartSlotContainerId(slotId)`), which is internal.
Called at `WoundDamageProjectionSystem.cs:128` — no edit.

### 4.3 `GetBodyChildrenOfType` — **SAME**

Onyx `SharedBodySystem.cs:184`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(EntityUid body, BodyPartType type)
```
Wolfgate `SharedBodySystem.Parts.cs:991-996`:
```csharp
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        // Shitmed Change
        BodyPartSymmetry? symmetry = null)
```
Call-compatible. Called once, at `WoundDamageRoutingSystem.cs:562`:
`foreach (var candidate in _body.GetBodyChildrenOfType(body, BodyPartType.Hand))` — no edit.
(Wolfgate's extra `symmetry` param could actually simplify the surrounding hand-symmetry filter, but leave the vendored file alone.)

### 4.4 `BodyHasChild` — **SAME**

Onyx `SharedBodySystem.cs:191`:
```csharp
    public bool BodyHasChild(EntityUid body, EntityUid part)
    {
        return GetBodyChildren(body).Any(child => child.Id == part);
    }
```
Wolfgate `SharedBodySystem.Parts.cs:978-990`:
```csharp
    public bool BodyHasChild(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body, logMissing: false)
            && body.RootContainer.ContainedEntity is not null
            && Resolve(partId, ref part, logMissing: false)
            && TryComp(body.RootContainer.ContainedEntity, out BodyPartComponent? rootPart)
            && PartHasChild(body.RootContainer.ContainedEntity.Value, partId, rootPart, part);
    }
```
`PartHasChild` (`:954-971`) scans `GetBodyPartChildren(parentId)`, which includes the root, so the root part counts as a
child in both implementations. **Semantically identical.** 5 call sites
(`WoundDamageRoutingSystem.cs:698, :1013`; `WoundHealingSystem.cs:157`; `TourniquetSystem.cs:115`;
`CirculatoryStreamSystem.cs:394`) — no edit.

### 4.5 `GetPartOrgans` — **SAME**

Onyx `SharedBodySystem.cs:424`:
```csharp
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid part)
```
Wolfgate `SharedBodySystem.Parts.cs:825`:
```csharp
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)
```
Call-compatible. Note the `OrganComponent` in the tuple is a **different class** (Onyx's Nubody one vs Wolfgate's Shitmed one) —
that is the §3.1 problem, not an API problem. One call site, `OrganDamageSystem.cs:48` (which passes an
`Entity<WoundableComponent>`, implicitly converted to `EntityUid`) — no signature edit, but the `.Where` clause changes (§3.1).

### 4.6 `TryDetachPart` — **MISSING**

Onyx `SharedBodySystem.cs:225-247`:
```csharp
    public bool TryDetachPart(EntityUid part, bool reparent = true)
    {
        if (!TryGetParentBodyPart(part, out var parent, out var parentPart) || parent == null || parentPart == null)
            return false;
        var body = Comp<BodyPartComponent>(part).Body;
        foreach (var slot in parentPart.Children.Keys.ToList())
        {
            if (!_containers.TryGetContainer(parent.Value, BodyPartComponent.PartSlotPrefix + slot, out var container)
                || container is not ContainerSlot { ContainedEntity: { } child } || child != part)
                continue;
            if (!_containers.Remove(part, container, reparent: reparent))
                return false;
            if (!parentPart.ChildSlots.ContainsKey(slot))
                parentPart.Children.Remove(slot);
            Dirty(parent.Value, parentPart);
            return true;
        }
        return false;
    }
```

Wolfgate has no `TryDetachPart`. The closest API, `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs`:

```csharp
:414    public EntityUid? GetParentPartOrNull(EntityUid uid)
:430    public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)
:702    public bool DetachPart(EntityUid parentPartId, string slotId, EntityUid partId,
                               BodyPartComponent? parentPart = null, BodyPartComponent? part = null)
:718    public bool CanDetachPart(EntityUid parentId, BodyPartSlot slot, EntityUid partId, …)
:733    public bool CanDetachPart(EntityUid parentId, string slotId, EntityUid partId, …)
:751    public bool DetachPart(EntityUid parentPartId, BodyPartSlot slot, EntityUid partId, …)
:201    protected virtual void DropPart(Entity<BodyPartComponent> partEnt)   // NOT public
:219    private void OnAmputateAttempt(Entity<BodyPartComponent> partEnt, ref AmputateAttemptEvent args) => DropPart(partEnt);
```

`DetachPart` alone only does `Containers.Remove(partId, container)` (`:782`). Shitmed's own sever path
(`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:213-238`) instead calls `DropPart`, which also
`DropSlotContents`, raises `BodyPartEnableChangedEvent(false)` and `BodyPartDroppedEvent`, and
`AttachToGridOrMap`s the part (`:201-217`). Wolfgate's surgery calls it through the event
(`WG/Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.Steps.cs:523-531`):

```csharp
        var ev = new AmputateAttemptEvent(args.Part);
        RaiseLocalEvent(args.Part, ref ev);
```
with `[ByRefEvent] public readonly record struct AmputateAttemptEvent(EntityUid Part);`
(`WG/Content.Shared/_Shitmed/Body/Events/BodyPartEvents.cs:9-10`).

**Adaptation** — add to `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodySystem.cs`:

```csharp
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;

namespace Content.Shared._WF.Wolfmed.Body;

/// <summary>Onyx-shaped body helpers mapped onto Wolfgate's Shitmed body system.</summary>
public sealed class WolfmedBodySystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>Detaches a part from its parent slot and drops it, mirroring Onyx's TryDetachPart.</summary>
    public bool TryDetachPart(EntityUid part, bool reparent = true)
    {
        if (_body.GetParentPartAndSlotOrNull(part) is not { } parentSlot
            || !HasComp<BodyPartComponent>(part)
            || !_body.CanDetachPart(parentSlot.Parent, parentSlot.Slot, part))
            return false;

        // DropPart is protected; the amputate event is the public door to it.
        var ev = new AmputateAttemptEvent(part);
        RaiseLocalEvent(part, ref ev);
        return _body.GetParentPartOrNull(part) is null;
    }
}
```

Behavioural deltas to accept, and why they are the right ones:
- Onyx's `reparent: false` path is never used by the wound set (`AmputationSystem.cs:118` calls `_body.TryDetachPart(part)`,
  i.e. the default `true`). The `reparent` parameter is kept for signature compatibility and ignored.
- Going through `AmputateAttemptEvent` gains Shitmed's held-item drop and `BodyPartDroppedEvent`, which Wolfgate content
  (appearance, cybernetics, targeting doll) already listens for. Calling `DetachPart` directly would silently skip all of it.
- `DropPart` is gated on `_timing.IsFirstTimePredicted`; `AmputationSystem.TryAmputate` is `_net.IsServer`-guarded
  (`AmputationSystem.cs:113`), so that gate is always satisfied.
- The return value must be re-checked (`GetParentPartOrNull(part) is null`) because the event handler returns `void`.

### 4.7 `TryGetRootPart` — **MISSING (name only)**

Onyx has no `TryGetRootPart`; the wound set never calls one. Onyx's `GetBodyChildren` inlines the root lookup
(`SharedBodySystem.cs:135-136`). Wolfgate's equivalent is
`WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:576`:
```csharp
    public (EntityUid Entity, BodyPartComponent BodyPart)? GetRootPartOrNull(EntityUid bodyId, BodyComponent? body = null)
```
**No adaptation needed** — nothing in the wound set calls it. Listed here only to close the task's checklist item.

### 4.8 `GetBodyPartCount` — **Wolfgate-only, unused by wounds**

Wolfgate `SharedBodySystem.Parts.cs:1130`:
```csharp
    public int GetBodyPartCount(EntityUid bodyId, BodyPartType partType, BodyComponent? body = null)
```
Onyx has no such method (it has `BodyHasPartType` at `:196`). Zero wound-set call sites either way. **No action.**

### 4.9 Nubody-only methods the wound set does *not* call

For completeness, the Onyx-only body API that stays behind with Nubody:
`GetBodyOrgans(:142)`, `InitializeAnatomy(:160)`, `BodyHasPartType(:196)`, `TryGetParentBodyPart(:198)`,
`HasAmputationConsequence(:209)`, `TryAttachPart(:249, :382)`, `TryRemoveOrgan(:290)`, `TryInsertOrgan(:307)`,
`TryInsertOrganIgnoringCompatibility(:312)`, `CanInsertOrgan(:335)`, `AreTransplantsCompatible(:355)`,
`TryCreatePartSlot(:366)`, `TryCreateOrganSlot(:407)`, `ActivateDeclarativeGraph(:418)`, `TryGetOrganInSlot(:442)`,
`HasOrganSlot(:455)`, `HasOrgan(:469)`, `TryGetOrgan(:483)`, `CountOrgans(:501)`, `HasPartChild(:513)`.

Two of them deserve a note for later phases:
- `TryGetOrganInSlot` / `TryRemoveOrgan` are used by `OrganHealthSystem.DestroyOrgan` → replaced by
  Wolfgate's `RemoveOrgan` (§3.1).
- `HasAmputationConsequence(:209)` is Onyx's gate that stops re-attaching a limb while the stump still carries an
  `AmputationConsequenceWound` (called from Onyx's `TryAttachPart(:249)`). Wolfgate's reattach path is Shitmed surgery
  (`SharedSurgerySystem.Steps.cs`), so if phase 3 ships amputation, add the same check as a `// WOLFGATE` two-line guard in
  Shitmed's attach step, or as a `SurgeryStepCompleteCheckEvent` subscriber in `_WF/Wolfmed`. **Flagged, not in scope.**

### 4.10 Summary table

| Method wounds call | Onyx | Wolfgate | Verdict |
|---|---|---|---|
| `GetBodyChildren(body)` | `SharedBodySystem.cs:133` | `SharedBodySystem.Body.cs:256` | **SAME** (extra defaulted params) |
| `GetBodyPartChildren(part)` | `:551` | `SharedBodySystem.Parts.cs:880` | **SAME** |
| `GetBodyChildrenOfType(body, type)` | `:184` | `Parts.cs:991` | **SAME** |
| `BodyHasChild(body, part)` | `:191` | `Parts.cs:978` | **SAME** |
| `GetPartOrgans(part)` | `:424` | `Parts.cs:825` | **SAME** signature; tuple's `OrganComponent` is a different class → §3.1 |
| `TryDetachPart(part, reparent)` | `:225` | — | **MISSING** → `WolfmedBodySystem.TryDetachPart` (§4.6) |
| `TryGetRootPart` | — | `GetRootPartOrNull` `Parts.cs:576` | n/a, uncalled |
| `GetBodyPartCount` | — | `Parts.cs:1130` | n/a, uncalled |
| `BodyPartComponent.Parent` (field) | field | `GetParentPartOrNull` `Parts.cs:414` | **DIFFERENT** → 4 edits (§2.3) |
| `TryGetOrganInSlot` / `TryRemoveOrgan` | `:442` / `:290` | `RemoveOrgan` `Organs.cs:172` | **DIFFERENT**, only for `OrganHealthSystem` (§3.1) |

---

## 5. Prototypes

### 5.1 How Onyx sets the fields

Onyx has no `Resources/Prototypes/Body/Parts/` at all — under Nubody, body parts *are* organs, defined as
`OrganBase*` entities in `Resources/Prototypes/Body/base_organs.yml` (928 lines) plus
`Resources/Prototypes/_Onyx/Body/chest_groin.yml`. (`git ls-tree -r HEAD Resources/Prototypes/Body/` shows
`Animals/`, `Satiation/`, `Species/`, `base_organs.yml`, `detached.yml`, `organ_categories.yml`,
`species_appearance.yml`, `species_base.yml` — no `Parts/`, no `Organs/`.)

`species_base.yml` sets **none** of the six fields (grep for all six returns nothing).

**Part values — `Resources/Prototypes/Body/base_organs.yml`** (all on `- type: BodyPart`, under `# <Onyx-Surgery>` markers):

| Prototype | line | `fractureProfile` | `maxDamage` | `amputationThresholds` |
|---|---|---|---|---|
| `OrganBaseTorso` | 46 | — (none) | — | — (torso is never severed) |
| `OrganBaseHead` | 84 | `OrganicFractureProfile` | — | Slash 200, Piercing 200, Blunt 350 |
| `OrganBaseArmLeft` | 134 | `OrganicFractureProfile` | — | Slash 130, Piercing 250, Blunt 250 |
| `OrganBaseArmRight` | 177 | `OrganicFractureProfile` | — | Slash 130, Piercing 250, Blunt 250 |
| `OrganBaseHandLeft` | 219 | `OrganicFractureProfile` | — | Slash 70, Piercing 200, Blunt 150 |
| `OrganBaseHandRight` | 263 | `OrganicFractureProfile` | — | Slash 70, Piercing 200, Blunt 150 |
| `OrganBaseLegLeft` | 308 | `OrganicFractureProfile` | — | Slash 150, Piercing 250, Blunt 300 |
| `OrganBaseLegRight` | 351 | `OrganicFractureProfile` | — | Slash 150, Piercing 250, Blunt 300 |
| `OrganBaseFootLeft` | 393 | `OrganicFractureProfile` | — | Slash 80, Piercing 220, Blunt 170 |
| `OrganBaseFootRight` | 433 | `OrganicFractureProfile` | — | Slash 80, Piercing 220, Blunt 170 |

**`Resources/Prototypes/_Onyx/Body/chest_groin.yml`:**

| Prototype | line | fields |
|---|---|---|
| `OrganBaseChest` | 9 | `fractureProfile: OrganicFractureProfile`, `maxDamage: 250` (no thresholds — the chest is unsevereable) |
| `OrganBaseGroin` | 36 | `fractureProfile: OrganicFractureProfile`, `amputationThresholds: {Slash: 220, Piercing: 250, Blunt: 400}` |
| `OrganSlimePersonChest` / `Groin` | 158 / 172 | `fractureProfile: null`, `amputationThresholds: {}` |
| `OrganDionaChest` / `Groin` | 188 / 202 | `fractureProfile: null`, `amputationThresholds: {}` |
| `OrganIpcChest` | 351 | `fractureProfile: null` |
| `OrganIpcGroin` | 368 | `fractureProfile: null`, thresholds Slash 480 / Piercing 450 / Blunt 960 |

Nothing anywhere in `Resources/Prototypes/_Onyx/` sets `dismembermentFinishingDamage`,
`amputationConsequenceSeverity` or `dismembermentSeverity` — those stay on their C# defaults
(`AmputationConsequenceSeverity = 35`, `DismembermentSeverity = null`, empty finishing-damage dict → falls back to
`WoundHostComponent.DefaultDismembermentFinishingDamage` = Slash 15, Piercing 40, Blunt 50,
`WoundDamageComponents.cs:72-76`). Confirmed by grepping all six names across `Resources/Prototypes/_Onyx/`:
only `chest_groin.yml`, `Parts/animal.yml`, `Entities/Mobs/Customization/Parts/cybernetic.yml` and `Wounds/wounds.yml` match.

**Organ values — `base_organs.yml`, under `# <Onyx-OrganDamage>` markers:**

| Prototype | line | `hitChance` | `selectionWeight` | `damageMultipliers` | on `- type: Organ` |
|---|---|---|---|---|---|
| `OrganBaseBrain` | 473 | 0.8 | 0.75 | Blunt .115, Slash .25, Piercing .42, Heat .15, Cold .05, Shock .3125 | — (death is via mob state) |
| `OrganBaseEyes` | 529 | 0.7 | 0.2275 | Blunt .115, Slash .3, Piercing .4375, Heat .15, Cold .05, Shock .25 | — |
| `OrganBaseTongue` | 574 | 0.5 | 0.08 | `&organicOrganDamage` = Blunt .1, Slash .25, Piercing .35, Heat .15, Cold .05, Shock .25 | — |
| `OrganBaseAppendix` | 605 | 0.35 | 0.0375 | `*organicOrganDamage` | — |
| `OrganBaseEars` | 629 | 0.45 | 0.0875 | `*organicOrganDamage` | — |
| `OrganBaseLungs` | 658 | *(default 1.0)* | 1.38 | Blunt .1, Slash .25, Piercing .42, Heat .165, Cold .05, Shock .25 | `destructionWound: InternalBleedingWound`, `destructionWoundSeverity: 35` |
| `OrganBaseHeart` | 711 | 0.8 | 0.64 | Blunt .1, Slash .25, Piercing .455, Heat .15, Cold .05, Shock .3375 | `destructionWound: InternalBleedingWound`, severity 45 |
| `OrganBaseStomach` | 748 | 0.85 | 0.56 | Blunt .1, Slash .275, Piercing .4025, Heat .15, Cold .05, Shock .25 | `destructionWound: InternalBleedingWound`, severity 25 |
| `OrganBaseLiver` | 801 | *(default 1.0)* | 1.1 | Blunt .1, Slash .3, Piercing .4375, Heat .15, Cold .05, Shock .25 | `destructionWound: InternalBleedingWound`, severity 40 |
| `OrganBaseKidneys` | 839 | 0.9 | 0.51 | Blunt .1, Slash .25, Piercing .4025, Heat .15, Cold .05, Shock .25 | `destructionWound: InternalBleedingWound`, severity 30 |

No prototype sets `health`/`maxHealth` — every organ uses the C# default of 15/15
(`ONYX/Content.Shared/Body/OrganComponent.cs:19-23`). `maxDamageFraction` is never set either → 0.3 everywhere
(`OrganDamageComponent.cs:22`), i.e. a single hit can take at most 4.5 HP off a 15 HP organ.

### 5.2 Which Wolfgate prototypes carry the new components

Wolfgate's part hierarchy (`WG/Resources/Prototypes/Body/Parts/base.yml`):

```
BasePartInorganic (:9)  ── BasePart (_Shitmed/Body/Parts/base.yml:5, sets damageContainer: OrganicPart)
BaseTorsoInorganic (:35) ── BaseTorso (_Shitmed/Body/Parts/base.yml:51)
BaseHead (:81)           (vital: true, severIntegrity: 400, organs: brain, eyes)
MajorLimb (:275)  → BaseLeftArm (:137), BaseRightArm (:156), BaseLeftLeg (:206), BaseRightLeg (:226)
MinorLimb (:312)  → BaseLeftHand (:176), BaseRightHand (:191), BaseLeftFoot (:246), BaseRightFoot (:261)
```
and per species `WG/Resources/Prototypes/Body/Parts/human.yml`:
`PartHuman (:4)`, `TorsoHuman (:22)`, `HeadHuman (:38)`, `LeftArmHuman (:54)`, `RightArmHuman (:63)`,
`LeftHandHuman (:72)`, `RightHandHuman (:83)`, `LeftLegHuman (:94)`, `RightLegHuman (:103)`,
`LeftFootHuman (:112)`, `RightFootHuman (:120)`.

**Recommendation: put `WolfmedBodyPart` on the abstract `Base*` parts in a new
`WG/Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`, not on the species entities.**
Reasons: (a) one entry per limb type covers every organic species that inherits it (D3 phase 1 = organic humanoids);
(b) `_Shitmed/Body/Parts/generic.yml` (BioSynth, Pizza… all `parent: <X>Human`) and
`_Shitmed/Body/Parts/cybernetic.yml` inherit it automatically and can override with `fractureProfile: null` in phase 5;
(c) it keeps upstream `Resources/Prototypes/Body/Parts/*.yml` untouched, matching modularity rule 3.

Torso mapping: Wolfgate's torso is a **merge** of Onyx's Chest and Groin, and it is where the heart/lungs/stomach/liver/
kidneys live (`WG/Resources/Prototypes/Body/Parts/base.yml:63-73`). Take Onyx's **Chest** row for it
(`maxDamage: 250`, `fractureProfile: OrganicFractureProfile`, no thresholds) and drop Onyx's groin row entirely.
Wolfgate's head holds brain + eyes (`:95-99`); Onyx's head does too, plus ears/tongue. Wolfgate's `Body/Organs/human.yml`
defines `OrganHumanEars (:139)`, `OrganHumanTongue (:119)`, `OrganHumanAppendix (:128)` — check which slot they sit in
before assigning `OrganDamage`, since `OrganDamageSystem` picks organs *within the damaged part*.

**Proposed `WG/Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`:**

```yaml
# Wolfmed part data. Values are Onyx's (base_organs.yml, _Onyx/Body/chest_groin.yml @ 2f5bab9).
# Onyx's Chest maps to Wolfgate's Torso; Onyx's Groin has no Wolfgate equivalent and is dropped.

- type: entity
  id: BaseTorso
  parent: BaseTorsoInorganic
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    maxDamage: 250

- type: entity
  id: BaseHead
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds:
      Slash: 200
      Piercing: 200
      Blunt: 350

- type: entity
  id: BaseLeftArm
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: &wolfmedArmThresholds
      Slash: 130
      Piercing: 250
      Blunt: 250

- type: entity
  id: BaseRightArm
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: *wolfmedArmThresholds

- type: entity
  id: BaseLeftHand
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: &wolfmedHandThresholds
      Slash: 70
      Piercing: 200
      Blunt: 150

- type: entity
  id: BaseRightHand
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: *wolfmedHandThresholds

- type: entity
  id: BaseLeftLeg
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: &wolfmedLegThresholds
      Slash: 150
      Piercing: 250
      Blunt: 300

- type: entity
  id: BaseRightLeg
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: *wolfmedLegThresholds

- type: entity
  id: BaseLeftFoot
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: &wolfmedFootThresholds
      Slash: 80
      Piercing: 220
      Blunt: 170

- type: entity
  id: BaseRightFoot
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: *wolfmedFootThresholds
```

YAML caveats to check when this is actually written:
- Re-declaring an existing abstract `id:` in a second file is how SS14 prototype *merging* works only if the engine allows
  duplicate ids; in this codebase the safe pattern is a **separate abstract parent** plus a `// WOLFGATE` one-line
  `parent:` edit in `Body/Parts/base.yml`. Decide at implementation time; if duplicate-id merging errors, use:
  `- type: entity / id: WolfmedBaseHead / abstract: true / components: [WolfmedBodyPart …]` and add
  `WolfmedBaseHead` to `BaseHead`'s parent list with a `// WOLFGATE` comment.
- YAML anchors (`&`/`*`) must be defined before use **within the same file** — the layout above satisfies that.
- Lint in Release; `ErrorNode` crashes the linter (project memory).

**Proposed `WG/Resources/Prototypes/_WF/Wolfmed/Body/organs.yml`** (phase 3, when organ damage lands).
Wolfgate's organ prototypes are `WG/Resources/Prototypes/Body/Organs/human.yml`:
`BaseHumanOrganUnGibbable (:2)`, `BaseHumanOrgan (:43)`, `OrganHumanBrain (:52)`, `OrganHumanEyes (:102)`,
`OrganHumanTongue (:119)`, `OrganHumanAppendix (:128)`, `OrganHumanEars (:139)`, `OrganHumanLungs (:149)`,
`OrganHumanHeart (:188)`, `OrganHumanStomach (:214)`, `OrganHumanLiver (:248)`, `OrganHumanKidneys (:269)` —
a 1:1 match with Onyx's `OrganBase*` list, so the table in §5.1 transfers directly:

```yaml
- type: entity
  id: OrganHumanHeart
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
```

…repeated for the other nine, with the §5.1 numbers. Same duplicate-id caveat applies; there are no convenient abstract
per-organ parents in Wolfgate (only `BaseHumanOrgan`), so these must either merge onto the concrete ids or be added via
`// WOLFGATE` lines in `Body/Organs/human.yml`. The latter is 12 small upstream edits and is probably cleaner than fighting
the prototype loader.

Species coverage for phase 1: `WG/Resources/Prototypes/Body/Parts/` has
`animal, arachnid, base, diona, gingerbread, human, moth, rat, reptilian, silicon, skeleton, slime, vox`.
Putting the component on the `Base*` abstracts gives it to **all** of them, including `slime.yml`, `diona.yml` and
`silicon.yml`, which Onyx explicitly opts *out* of (`fractureProfile: null`, `amputationThresholds: {}`).
Phase 1 (D3, organic humanoids only) must therefore also ship the opt-outs:

```yaml
# Non-organic bodies do not fracture or tear off on the organic curve (phase 5 gives them their own profiles).
- type: entity
  id: <PartSlimeX / PartDionaX / silicon parts>
  components:
  - type: WolfmedBodyPart
    fractureProfile: null
    amputationThresholds: {}
    maxDamage: 0
```

or, cheaper and safer for phase 1: **do not** attach `WoundHostComponent` to those species' mobs, so the whole wound path
(and therefore every field read) is inert for them regardless of what the parts carry.

---

## 6. Conflicts with Shitmed to hand back to the D2 (damage bridge) agent

- `BodyPartComponent.SeverIntegrity = 130` (`WG/Content.Shared/Body/Part/BodyPartComponent.cs:124`) and
  `SharedBodySystem.Targeting.cs:213-238` sever a part on `DamageChangedEvent` when
  `damageable.TotalDamage >= SeverIntegrity` and the part is `!Enabled`. Onyx's `AmputationSystem` owns severing for
  wound hosts. Both will fire. The guard belongs at `Targeting.cs:223` —
  `&& !HasComp<WoundHostComponent>(partEnt.Comp.Body) // WOLFGATE`.
- `BodyPartComponent.HealingTime` / `SelfHealingAmount` (`:87`, `:98`) regenerate part damage; `WoundHealingSystem` owns
  that for wound hosts. Same gating.
- `IntegrityThresholds` (`:138-147`) drives the Shitmed targeting doll's colour. Onyx's `BodyPartFunctionalitySystem`
  computes a parallel `BodyPartFunctionalityState`. Leave both; they are cosmetic and independent.
- `DamageableSystem.GetAllDamage` and `GetPositiveDamage` — used by `AmputationSystem.cs:78, :182` and
  `WoundDamageRoutingSystem.cs:734`, `WoundDamageProjectionSystem.cs` — **do not exist** in
  `WG/Content.Shared/Damage/Systems/DamageableSystem.cs` (grep returns nothing). D5's compat layer must supply them.
  Not this report's scope, but the body/organ code cannot compile without them.

---

## 7. Deliverables checklist for the implementer

New files (all `_WF` style: no licence header, one-line `/// <summary>`, `[Dependency] private X _x = default!;`):

1. `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs` — §2.4
2. `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartSystem.cs` — §2.5 (the `Get` lookup)
3. `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodySystem.cs` — §4.6 (`TryDetachPart`)
4. `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs` — §3.1 (phase 3)
5. `WG/Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` — §5.2
6. `WG/Resources/Prototypes/_WF/Wolfmed/Body/organs.yml` — §5.2 (phase 3)

Vendored from Onyx:

7. `WG/Content.Shared/_Onyx/Body/OrganDamageComponent.cs` — verbatim
8. `WG/Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs` — with the §3.1 `// WOLFGATE` edits
9. `OrganFunctionChangedEvent` (from `_Onyx/Body/FunctionalOrganComponent.cs`) — relocate into 8 or into a stub file;
   `FunctionalOrganComponent` itself is not ported

`// WOLFGATE` edits inside vendored wound files: **15 field sites (§2.3/§2.6) + 9 enum sites (§2.2) + 4 `Parent` sites +
1 `TryDetachPart` site = 29 edits across 7 files** (`AmputationSystem.cs`, `WoundDamageRoutingSystem.cs`,
`WoundDamageProjectionSystem.cs`, `WoundFractureSystem.cs`, `FractureAlertSystem.cs`, `WoundDamageComponents.cs`,
`HealthExaminableSystem.PartStatus.cs`), plus the §3.1 edits in `OrganHealthSystem.cs` / `OrganDamageSystem.cs`
and one `using` fix in `WoundSystem.cs` (`Content.Shared.Body` → `Content.Shared.Body.Components`).

Upstream Wolfgate edits requested by this report: **zero** in phase 1 (the Shitmed sever/regen guards belong to D2;
the 12 organ-prototype lines in phase 3 are the only likely exception).
