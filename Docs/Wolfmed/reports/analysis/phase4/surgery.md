# P4-3 — Wound surgeries on Shitmed's step system (D7)

Analyst report. Everything below was read in the live tree at HEAD `6329d204e3`
(`WG = C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`) and at the
Onyx pin `2f5bab9` (`git -C C:/tmp/onyx show HEAD:<path>`). Every signature is quoted verbatim from the file
named. Nothing was written to the tree — this analysis is read-only.

---

## 0. Executive summary

1. **Wolfgate's Shitmed surgery already reaches the Wolfmed wound layer for free.** `SurgeryStepDamageEvent`
   → `SurgerySystem.SetDamage` → `DamageableSystem.TryChangeDamage(body, …, targetPart:)`, and the phase-1
   routing seam (`DamageableSystem.cs:255-266`, `WoundDamageRoutingSystem.OnBeforeDamageChanged`) already
   takes that call over for `WoundHostComponent` entities and heals wounds on negative damage. So
   `SurgeryTendWoundsBrute`/`Burn` and every `SurgeryDamageChangeEffect` step (clamp bleeders −5 Bloodloss,
   cautery −5 Heat, …) already act on wounds today. **P4-3 is additive, not a rewrite.**
2. **Six things Shitmed cannot express** and that P4-3 must add: a wound-severity window (deep vs. shallow
   tending), per-wound bleeding suture, internal-bleeding treatment, fracture mending, amputation-consequence
   treatment, and organ healing. Each is a new `_WF` step-effect/condition component plus one surgery + one
   step prototype.
3. **Two colliding component names** confirmed (`SurgeryWoundedConditionComponent`,
   `SurgeryTendWoundsEffectComponent` — §3.1). Per D7 they are **extended in place** with `// WOLFGATE`
   datafields, never vendored. All eight new components use a `WolfmedSurgery…` prefix; all eight names were
   grepped and are free (§6.2).
4. **The `CanAttachPart` gap has a cheaper and safer fix than DECISIONS assumes.** A guard in
   `SharedBodySystem.CanAttachPart` would also silently break Mono's `PrybarProstheticsSystem`,
   `BionicLegsSystem`, Goob's `AutoSurgeonSystem` and Shitmed's `GenerateChildPartSystem`, all of which call
   `AttachPart`. A single 2-line marked hook inside `SharedSurgerySystem.OnPartRemovedConditionValid`
   (`SharedSurgerySystem.cs:255-274`) blocks **both** the UI listing and the completed do-after for all 11
   `SurgeryAttach*` surgeries, and touches nothing else. **User decision U-1 (§7).**
5. **No new tool is needed.** Onyx's `Stitches` tool maps onto Wolfgate's existing `Hemostat` / `Tending`;
   `BoneGel` and the otherwise-unused `BoneSetter` already exist on `Bonesetter`, `BoneGel` and the `omnimed`
   item prototypes. **User decision U-3 (§7)** is whether to give bone setting a distinct role
   (`BoneSetter` → reduce, `BoneGel` → mend) as Onyx's `FractureTreatment` enum invites.
6. **Difficulty: medium.** ~450 lines of C# in five new `_WF` files, ~330 lines of YAML, 4 marked upstream
   edits (2 C# call sites, 2 datafield blocks), 1 locale file, 6 new tests. No new upstream *systems*, no new
   subscriptions to upstream components, zero registration collisions.

---

## 1. Wolfgate's Shitmed surgery — the exact step lifecycle

### 1.1 Namespace vs. folder (a trap)

The folder is `Content.Shared/_Shitmed/Surgery/…` but the namespace is
`Content.Shared._Shitmed.Medical.Surgery` (`SharedSurgerySystem.cs:31`). The server half is at
`Content.Server/_Shitmed/Medical/Surgery/SurgerySystem.cs` (namespace `Content.Server._Shitmed.Medical.Surgery`)
— **there is no `Content.Server/_Shitmed/Surgery`**. The client half is
`Content.Client/_Shitmed/Medical/Surgery/{SurgerySystem,SurgeryBui}.cs`.

### 1.2 The three step entities

Surgeries and steps are **entity prototypes spawned once into nullspace and cached**:

```csharp
// Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs:349-364
public EntityUid? GetSingleton(EntProtoId surgeryOrStep)
{
    if (!_prototypes.HasIndex(surgeryOrStep))
        return null;
    if (!_surgeries.TryGetValue(surgeryOrStep, out var ent) || TerminatingOrDeleted(ent))
    {
        ent = Spawn(surgeryOrStep, MapCoordinates.Nullspace);
        _surgeries[surgeryOrStep] = ent;
    }
    return ent;
}
```

Every effect/condition component therefore lives **on the singleton surgery or step entity**, not on the
patient. `args.Body` / `args.Part` in the events carry the patient.

Surgeries are discovered automatically — no registry edit is needed for a new surgery prototype:

```csharp
// Content.Server/_Shitmed/Medical/Surgery/SurgerySystem.cs:185-191
private void LoadPrototypes()
{
    _surgeries.Clear();
    foreach (var entity in _prototypes.EnumeratePrototypes<EntityPrototype>())
        if (entity.HasComponent<SurgeryComponent>())
            _surgeries.Add(new EntProtoId(entity.ID));
}
```

### 1.3 The four events (all `[ByRefEvent]` record structs)

| Event | Raised on | Signature | File |
|---|---|---|---|
| `SurgeryValidEvent` | the **surgery** singleton **and** the **step** singleton | `record struct SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false, BodyPartType PartType = default, BodyPartSymmetry? Symmetry = default)` | `Conditions/SurgeryValidEvent.cs:9` |
| `SurgeryCanPerformStepEvent` | the **step** singleton, then relayed to **`args.Body`** | `record struct SurgeryCanPerformStepEvent(EntityUid User, EntityUid Body, List<EntityUid> Tools, SlotFlags TargetSlots, string? Popup = null, StepInvalidReason Invalid = StepInvalidReason.None, Dictionary<EntityUid, float>? ValidTools = null) : IInventoryRelayEvent` | `Steps/SurgeryCanPerformStepEvent.cs:6` |
| `SurgeryStepEvent` | the **step** singleton | `record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools, EntityUid Surgery)` | `SurgeryStepEvent.cs:7` |
| `SurgeryStepCompleteCheckEvent` | the **step** singleton | `record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, EntityUid Surgery, bool Cancelled = false)` | `Steps/SurgeryStepCompleteCheckEvent.cs:4` |

Two more, both raised **on the patient body**, server-handled:

```csharp
// SurgeryStepDamageEvent.cs:9
public record struct SurgeryStepDamageEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Surgery, DamageSpecifier Damage, float PartMultiplier);
// SurgeryStepDamageChangeEvent.cs:9
public record struct SurgeryStepDamageChangeEvent(EntityUid User, EntityUid Body, EntityUid Part, EntityUid Step);
```

`SurgeryCompletedEvent` (`Effects/Complete/SurgeryCompletedEvent.cs:7`) is an **empty** `record struct` and is
**never raised anywhere in the tree** (the only subscriber, `OnRemoveLarva`, is commented out at
`SharedSurgerySystem.cs:76`). **Do not build anything on surgery completion.**

### 1.4 The lifecycle, start to finish

1. **Verb.** `SurgerySystem.OnUtilityVerb` (`SurgerySystem.cs:122`) on `SurgeryToolComponent` →
   `AttemptStartSurgery` → `_ui.OpenUi(target, SurgeryUIKey.Key, user)` + `RefreshUI(target)`.
2. **Listing.** `SurgerySystem.RefreshUI` (server only, `SurgerySystem.cs:61-88`) walks every cached surgery ×
   every body child part and raises `SurgeryValidEvent` **on the surgery singleton only**:
   ```csharp
   foreach (var part in _body.GetBodyChildren(body))
   {
       var ev = new SurgeryValidEvent(body, part.Id);
       RaiseLocalEvent(surgeryEnt, ref ev);
       if (ev.Cancelled) continue;
       surgeries.GetOrNew(GetNetEntity(part.Id)).Add(surgery);
   }
   ```
   **Consequence:** a condition component on a *step* prototype does **not** hide the surgery from the list —
   it only fires later, inside `IsSurgeryValid`. Conditions that must gate visibility belong on the **surgery**
   prototype.
3. **Next-step highlight (client).** `SurgeryBui.cs:281` calls `_system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent)`
   → `IsStepComplete` → `SurgeryStepCompleteCheckEvent`. **This runs on the client**, so any
   `SurgeryStepCompleteCheckEvent` handler must be **shared**, or the client will mark the step permanently
   complete and highlight the wrong one. (`SurgeryBui.cs:310` likewise calls `CanPerformStep` client-side.)
4. **Tool/armour gate.** `CanPerformStep` (`SharedSurgerySystem.Steps.cs:345-384`) builds
   `SurgeryCanPerformStepEvent` with a `SlotFlags` derived from `BodyPartType`, raises it on the step, and
   `OnToolCanPerform` (`Steps.cs:270-319`) checks operating table, worn armour over the slot, then relays
   the event to `args.Body`, then resolves `ent.Comp.Tool` via `AnyHaveComp`.
5. **Do-after.** `OnSurgeryTargetStepChosen` (`Steps.cs:415-497`) validates, raises `SurgeryToolUsedEvent` per
   tool, computes `duration = stepComp.Duration / speed`, ×4 if self-surgery, and starts a
   `SurgeryDoAfterEvent(surgery, step)` with `BreakOnMove`, `NeedHand`, `BreakOnHandChange`.
6. **Completion.**
   ```csharp
   // SharedSurgerySystem.cs:86-106
   private void OnTargetDoAfter(Entity<SurgeryTargetComponent> ent, ref SurgeryDoAfterEvent args)
   {
       if (!_timing.IsFirstTimePredicted) return;
       if (args.Cancelled || args.Handled || args.Target is not { } target
           || !IsSurgeryValid(ent, target, args.Surgery, args.Step, args.User, out var surgery, out var part, out var step)
           || !PreviousStepsComplete(ent, part, surgery, args.Step)
           || !CanPerformStep(args.User, ent, part, step, false))
       { Log.Warning(...); return; }

       args.Repeat = (HasComp<SurgeryRepeatableStepComponent>(step) && !IsStepComplete(ent, part, args.Step, surgery));
       var ev = new SurgeryStepEvent(args.User, ent, part, GetTools(args.User), surgery);
       RaiseLocalEvent(step, ref ev);
       RefreshUI(ent);
   }
   ```
   `IsSurgeryValid` (`SharedSurgerySystem.cs:315-347`) raises `SurgeryValidEvent` on **both** the step and the
   surgery singleton — so a step-level condition *is* authoritative at completion time, just invisible in the
   list.
7. **Effects.** `OnToolStep` (`Steps.cs:74-184`) plays the tool's `EndSound`, applies `add`/`remove` (on the
   part), `bodyAdd`/`bodyRemove` (on the body), `addOrganOnAdd`/`removeOrganOnAdd`, then charges 15 Poison
   sepsis if the surgeon has no gloves **or** no mask and no `SanitizedComponent`. **Every other
   `SubscribeLocalEvent<TComp, SurgeryStepEvent>` handler for the same step entity also fires** — that is the
   extension point.
8. **Damage.** Server-side `SurgerySystem.OnSurgeryStepDamage` (`SurgerySystem.cs:144`) →
   ```csharp
   // SurgerySystem.cs:89-105
   private void SetDamage(EntityUid body, DamageSpecifier damage, float partMultiplier, EntityUid user, EntityUid part)
   {
       if (!TryComp<BodyPartComponent>(part, out var partComp)) return;
       _damageable.TryChangeDamage(body, damage, true, origin: user, canSever: false,
           partMultiplier: partMultiplier, targetPart: _body.GetTargetBodyPart(partComp));
   }
   ```
9. **Repeat.** `args.Repeat` re-runs the do-after while `SurgeryRepeatableStepComponent` is present and
   `IsStepComplete` is false.

### 1.5 How an effect reaches Wolfmed today (verified)

`SetDamage` → `TryChangeDamage` → `BeforeDamageChangedEvent` →

```csharp
// Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:69-72
SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged, before: [typeof(SharedArmorPlateSystem)]);
```

which cancels the vanilla pass, honours the explicit `targetPart`
(`WoundDamageRoutingSystem.cs:85-96`), and lands the damage as
`PartDamageAppliedEvent(body, target, appliedDamage, !_skipWoundHealing.Contains(body), …)`
(`WoundDamageRoutingSystem.cs:782-787`). `WoundSystem.HandlePartDamageApplied`
(`WoundSystem.cs:66`) creates or heals wounds from it. **Negative `DamageSpecifier`s therefore heal wounds
today, unprompted.** Part damage is projected back onto the body's `DamageableComponent` by
`WoundDamageProjectionSystem.OnPartDamageDealt` (`WoundDamageProjectionSystem.cs:38`), so Shitmed's
`damageable.DamagePerGroup[…]` reads on the body are meaningful on wound hosts.

### 1.6 Tools

`ISurgeryToolComponent` (`Tools/ISurgeryToolComponent.cs:3`):

```csharp
public interface ISurgeryToolComponent
{
    public string ToolName { get; }
    public bool? Used { get; set; }
    public float Speed { get; set; }
}
```

Implementations in `Content.Shared/_Shitmed/Surgery/Tools/`: `Scalpel`, `Hemostat`, `Retractor`, `BoneSaw`,
`Cautery`, `Drill`, `Tweezers`, `Tending`, **`BoneGel`**, **`BoneSetter`**. Plus — this is why
`tool: - type: BodyPart` and `- type: Organ` work — `BodyPartComponent` and `OrganComponent` **also**
implement it (`Content.Shared/Body/Part/BodyPartComponent.cs:20`,
`Content.Shared/Body/Organ/OrganComponent.cs:12`, both marked `// Shitmed Change`).

```csharp
// Tools/BoneGelComponent.cs:6-14
public sealed partial class BoneGelComponent : Component, ISurgeryToolComponent
{ public string ToolName => "bone gel"; public bool? Used { get; set; } = null; [DataField] public float Speed { get; set; } = 1f; }
// Tools/BoneSetterComponent.cs:6-12
public sealed partial class BoneSetterComponent : Component, ISurgeryToolComponent
{ public string ToolName => "a bone setter"; public bool? Used { get; set; } = null; [DataField] public float Speed { get; set; } = 1f; }
```

Item prototypes — `Resources/Prototypes/Entities/Objects/Specific/Medical/surgery.yml`:
`Bonesetter` (`- type: BoneSetter`, line 229), `BoneGel` (`- type: BoneGel`, line 241), and `Omnimed`
(lines 464-489) which carries **all ten** tool components at `speed: 2`, `BoneSetter` and `Tending` included.
`Hemostat` also carries `- type: Tweezers` and `- type: Tending`.

**`BoneSetterComponent` is declared, shipped on two items, and referenced by no step prototype in the entire
tree** (`grep -rn "type: BoneSetter" Resources/Prototypes` → 3 item hits, 0 step hits). It is a free slot for
P4-3.

### 1.7 `SurgeryTendWoundsEffectComponent` and `SurgeryWoundedConditionComponent` as they exist

```csharp
// Content.Shared/_Shitmed/Surgery/Effects/Step/SurgeryTendWoundsEffectComponent.cs:6-20
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField, AutoNetworkedField] public string MainGroup = "Brute";
    [DataField, AutoNetworkedField] public bool IsAutoRepeatable = true;
    [DataField, AutoNetworkedField] public DamageSpecifier Damage = default!;
    [DataField, AutoNetworkedField] public float HealMultiplier = 0.07f;
}

// Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs:6-7
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component;
```

Handlers:

```csharp
// SharedSurgerySystem.Steps.cs:120-152 (shared, both sides)
private void OnTendWoundsStep(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
{
    var group = ent.Comp.MainGroup == "Brute" ? BruteDamageTypes : BurnDamageTypes;
    if (!HasDamageGroup(args.Body, group, out var damageable) && !HasDamageGroup(args.Part, group, out var _) || damageable == null) return;
    var bonus = ent.Comp.HealMultiplier * damageable.DamagePerGroup[ent.Comp.MainGroup];
    if (_mobState.IsDead(args.Body)) bonus *= 0.2;
    var adjustedDamage = new DamageSpecifier(ent.Comp.Damage);
    foreach (var type in group) adjustedDamage.DamageDict[type] -= bonus;
    var ev = new SurgeryStepDamageEvent(args.User, args.Body, args.Part, args.Surgery, adjustedDamage, 2.5f);
    RaiseLocalEvent(args.Body, ref ev);
}
private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
{
    var group = ent.Comp.MainGroup == "Brute" ? BruteDamageTypes : BurnDamageTypes;
    if (HasDamageGroup(args.Body, group, out var _) || HasDamageGroup(args.Part, group, out var _)) args.Cancelled = true;
}

// SharedSurgerySystem.cs:120-128
private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
{
    if (!TryComp(args.Body, out DamageableComponent? damageable) || !TryComp(args.Part, out DamageableComponent? partDamageable)
        || damageable.TotalDamage <= 0 && partDamageable.TotalDamage <= 0 && !HasComp<IncisionOpenComponent>(args.Part))
        args.Cancelled = true;
    }
```

Notes that matter for P4-3:

* `BruteDamageTypes = { "Slash", "Blunt", "Piercing" }`, `BurnDamageTypes = { "Heat", "Shock", "Cold", "Caustic" }`
  (`Steps.cs:35-36`) — string-keyed, matching WG's `DamageSpecifier.DamageDict`.
* `IsAutoRepeatable` is **dead**: the only repeat driver is `SurgeryRepeatableStepComponent`
  (`SharedSurgerySystem.cs:102`). `grep -rn "IsAutoRepeatable"` → 1 hit, the declaration.
* `OnTendWoundsCheck` cancels while **any** Brute/Burn remains on the **body** — so on a wound host a tend
  step keeps repeating until the whole body is clean, not just the part. That is pre-existing Shitmed
  behaviour; P4-3 does not have to change it, but it must be recorded (§8 hazard 4).
* `args.PartMultiplier = 2.5f` (Mono's buff; upstream Shitmed was `0.5f`) means the part takes 2.5× the
  healing of the body figure.

### 1.8 Existing prototypes P4-3 reuses

`Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml`:

```yaml
- type: entity                       # :7
  parent: SurgeryBase
  id: SurgeryOpenIncision
  components:
  - type: Surgery
    steps: [ SurgeryStepOpenIncisionScalpel, SurgeryStepRetractSkin, SurgeryStepClampBleeders ]
  - type: SurgeryPartPresentCondition

- type: entity                       # :20
  id: SurgeryCloseIncision
  components:
  - type: Surgery
    priority: 1
    steps: [ SurgeryStepCloseBones, SurgeryStepMendRibcage, SurgeryStepCloseIncision ]
  - type: SurgeryPartPresentCondition

- type: entity                       # :285
  id: SurgeryTendWoundsBrute
  components:
  - type: Surgery
    steps: [ SurgeryStepCarefulIncisionScalpel, SurgeryStepRepairBruteTissue, SurgeryStepSealTendWound ]
  - type: SurgeryWoundedCondition
```

`SurgeryComponent` (`SurgeryComponent.cs:6-18`) is `{ int Priority; EntProtoId? Requirement; List<EntProtoId> Steps }` —
a **flat list plus one `requirement:` chain**, unlike Onyx's `Dictionary<string, variant>` (§2.1).

`SurgeryStepComponent` (`Steps/SurgeryStepComponent.cs:6-46`) is
`[Prototype("SurgerySteps")]`, `{ ComponentRegistry? Tool, Add, BodyAdd, Remove, BodyRemove; Dictionary<string, ComponentRegistry>? AddOrganOnAdd, RemoveOrganOnAdd; float Duration = 2f }`.

`StepInvalidReason` (`StepInvalidReason.cs:3-10`) = `{ None, MissingSkills, NeedsOperatingTable, Armor, MissingTool }`
— **no `AmputationConsequence` member** (Onyx has one, `Content.Client/_Onyx/Medical/Surgery/SurgeryBoundUserInterface.cs:300`).

`SurgeryPartRemovedConditionComponent` (`Conditions/SurgeryPartRemovedConditionComponent.cs:7-20`):
`{ string Connection (required); BodyPartType Part; BodyPartSymmetry? Symmetry }`.

Popup locale keys resolve as `surgery-popup-procedure-{surgery}-step-{step}` then
`surgery-popup-step-{step}` (`Steps.cs:481-489`); existing file
`Resources/Locale/en-US/_Shitmed/surgery/surgery-popup.ftl`. Entity **names** are inline `name:` in YAML
(no ftl), unlike Onyx.

---

## 2. Onyx's wound surgeries, surgery by surgery

### 2.1 Why Onyx's surgery system cannot be vendored (confirms D7)

| | Onyx | Wolfgate |
|---|---|---|
| `Surgery.steps` | `Dictionary<string, {required: ComponentRegistry, steps: […]}>` variant map (`surgeries.yml:22-32`) | `List<EntProtoId>` + one `requirement:` |
| step markers | `addMarkers: [ IncisionOpen ]` + `SurgeryMarkerCondition` | `add:`/`remove:` `ComponentRegistry` |
| tool | `tool: [ { type: Scalpel } ]`, plain marker components | same shape, but components must implement `ISurgeryToolComponent` |
| repeat | `RepeatSurgeryStep` | `SurgeryRepeatableStep` |
| organ addressing | `ProtoId<OrganCategoryPrototype> Slot` + `_body.TryGetOrganInSlot` | `ComponentRegistry Organ` + `_body.TryGetBodyPartOrgans` |
| part type names | `Chest`, `Groin` | `Torso` (no `Groin`) |
| `SurgeryStepEvent` | 4-tuple `(User, Body, Part, Tools)` | 5-tuple, adds `Surgery` |

Onyx's `SharedSurgerySystem` is 11 partial files plus 4 server files; porting it would duplicate the BUI,
the step registry, the tool system and 22 component names. **D7's call stands.**

### 2.2 Onyx's wound-surgery C#

`Content.Shared/_Onyx/Medical/Surgery/WoundSurgeryComponents.cs` (113 lines) declares:

| Onyx component | Registered name | Collides with WG? |
|---|---|---|
| `SurgeryHasWoundConditionComponent` (`:10`) | `SurgeryHasWoundCondition` | no |
| `SurgeryClampBleedingEffectComponent` (`:29`) | `SurgeryClampBleedingEffect` | no |
| `SurgeryFractureGradeConditionComponent` (`:39`) | `SurgeryFractureGradeCondition` | no |
| `SurgeryMendFractureEffectComponent` (`:52`) | `SurgeryMendFractureEffect` | no |
| `SurgeryTreatWoundEffectComponent` (`:55`) | `SurgeryTreatWoundEffect` | no |
| **`SurgeryWoundedConditionComponent`** (`:74`) | `SurgeryWoundedCondition` | **YES** — `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs:7` |
| **`SurgeryTendWoundsEffectComponent`** (`:87`) | `SurgeryTendWoundsEffect` | **YES** — `Content.Shared/_Shitmed/Surgery/Effects/Step/SurgeryTendWoundsEffectComponent.cs:7` |

Plus `SurgeryOrganHealEffectComponent` in `SurgeryEffects.cs:54`
(`{ ProtoId<OrganCategoryPrototype> Slot; FixedPoint2 Amount }`).

`Content.Server/_Onyx/Medical/Surgery/WoundSurgerySystem.cs` subscribes **11 pairs**
(`WoundSurgerySystem.cs:27-37`), all of which a Wolfgate port must re-home onto `_WF` component names.

### 2.3 Per-surgery table

Columns: Onyx surgery → what it calls → Wolfgate equivalent.

#### a) `SurgeryStopBleeding` (`_Onyx/…/surgeries.yml:1083`)

```yaml
- type: entity
  id: SurgeryStopBleeding
  name: Stop bleeding
  components:
  - type: Surgery
    icon: { sprite: Interface/Alerts/bleed.rsi, state: bleed5 }
    steps: { default: { steps: [ SurgeryStepClampWoundBleeding ] } }
  - type: SurgeryHasWoundCondition
    state: Open
    bleeding: true
```
Step `SurgeryStepClampWoundBleeding` (`surgery_steps.yml:943`): `tool: [ { type: Stitches } ]`,
`SurgeryHasWoundCondition state: Open`, `SurgeryClampBleedingEffect amount: 10`, `RepeatSurgeryStep`.

**API:** `WoundBleedingSystem.ReduceBleeding(wound, 10)` on the *highest-severity bleeding wound on the
selected part* (`WoundSurgerySystem.cs:47-51`, `FindWound(… bleeding: true)`).

**Wolfgate:** new `WolfmedSurgeryClampBleedingEffectComponent`; `WoundBleedingSystem.ReduceBleeding` is
**SAME** (`Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs:142`:
`public bool ReduceBleeding(Entity<WoundComponent?> wound, FixedPoint2 amount)`), but the system is
**server-only** → the `SurgeryStepEvent` handler must live in `Content.Server`. The completion check reads
`WoundBleedingComponent.CurrentRate` directly (networked, `WoundDamageComponents.cs:209-226`) and stays
shared. Tool: `Hemostat` (see U-3).

#### b) `SurgeryTendWoundsBrute` / `Burn` (`surgeries.yml:1098`, `:1113`)

```yaml
  - type: SurgeryWoundedCondition
    damageGroup: Brute
    minSeverity: 50
    maxSeverity: 99.99
```
single step `SurgeryStepRepairBruteTissue` (`surgery_steps.yml:974`, `tool: Hemostat`, `duration: 2`,
`SurgeryTendWoundsEffect { healDamage: true, healWounds: true, damage: { groups: { Brute: -5 } } }`,
`RepeatSurgeryStep`). **No incision.** `Burn` uses `tool: Tending` and `Burn: -10`.

#### c) `SurgeryTendWoundsBruteDeep` / `BurnDeep` (`surgeries.yml:1128`, `:1143`)

Identical, but `steps: [ SurgeryOpenIncision, SurgeryStepRepairBruteTissue ]` and
`minSeverity: 100` with no upper bound.

**API:** `WoundSurgerySystem.OnTendWounds` (`WoundSurgerySystem.cs:86-142`) — computes
`GetGroupSeverity(part, group)` (sum of non-scar wound severities whose prototype's `DamageTypes` intersect
the group, `:167-185`), applies a `HealMultiplier` bonus, then
`_damageRouting.TryApplyPartDamage(args.Body, args.Part, damage, args.User, healWounds: false)` **and**
`_wounds.TryHealWounds(args.Part, treatment)` separately.

**Wolfgate:** `TryApplyPartDamage` and `TryHealWounds` are both **SAME**:
`WoundDamageRoutingSystem.cs:240-249` `public bool TryApplyPartDamage(EntityUid body, EntityUid part, DamageSpecifier damage, EntityUid? origin = null, bool ignoreResistances = false, bool healWounds = true)`;
`WoundSystem.cs:400-401` `public bool TryHealWounds(Entity<WoundableComponent?> part, DamageSpecifier healing, IReadOnlySet<string>? allowedStages = null)`.
But Wolfgate does **not need** the split: routing already heals wounds on a negative
`SurgeryStepDamageEvent`. **Recommendation:** keep Wolfgate's existing
`SurgeryTendWoundsEffect` handler untouched and add only the severity window to
`SurgeryWoundedConditionComponent` (§3.1), so `Deep` is a second surgery with the same repair step but an
`SurgeryOpenIncision` requirement. This is the single biggest scope saving in P4-3.

#### d) `SurgeryStopInternalBleeding` (`surgeries.yml:1173`)

```yaml
  - type: Surgery
    useTargetPartIcon: true
    steps: { default: { steps: [ SurgeryOpenIncision, SurgeryStepStopInternalBleeding ] } }
  - type: SurgeryHasWoundCondition
    internalBleeding: true
```
Step `SurgeryStepStopInternalBleeding` (`surgery_steps.yml:1123-1135`): `duration: 4`,
`tool: [ { type: Hemostat } ]`, `SurgeryTreatWoundEffect internalBleeding: true`.

**API:** `WoundSystem.TreatWound(wound, ent.Comp.Amount)` with `Amount = FixedPoint2.MaxValue` default
(`WoundSurgeryComponents.cs:67`), on a wound carrying `WoundInternalBleedingComponent` with `Severity > 0`
and `State == Open` (`WoundSurgerySystem.cs:220-222`).

**Wolfgate:** `WoundSystem.TreatWound` is **SAME** (`WoundSystem.cs:316`:
`public bool TreatWound(Entity<WoundComponent?> wound, FixedPoint2 amount)`).
`WoundInternalBleedingComponent` is **SAME** (`WoundDamageComponents.cs:236-243`, `{ float Rate; FixedPoint2 Severity }`, networked).
`InternalBleedingWound` prototype exists (`Resources/Prototypes/_Onyx/Wounds/wounds.yml:405`) and is
produced by organ destruction (`_WF/Wolfmed/Body/organs.yml`, heart 45 / liver 40 / lungs 35 / kidneys 30 /
stomach 25). **This surgery is currently the only cure for a destroyed organ's internal bleed in Wolfgate.**

#### e) `SurgeryMendFracture` (`surgeries.yml:1188`)

```yaml
  - type: Surgery
    icon: { sprite: _Onyx/Interface/Alerts/fracture.rsi, state: brokenbones }
    steps:
      default: { steps: [ SurgeryOpenIncision, SurgeryStepMendFracture ] }
      cybernetic: { required: [ { type: Cybernetics } ], steps: [ SurgeryOpenIncision, SurgeryStepMendFrameFracture ] }
  - type: SurgeryFractureGradeCondition
```
Step `SurgeryStepMendFracture` (`surgery_steps.yml:1035-1046`): `tool: [ { type: BoneGel } ]`,
`SurgeryTargetPartContext`, `SurgeryMendFractureEffect`, `SurgeryStepPainInflicter amount: 12`.

**API:** `WoundFractureSystem.GetFracture(part)` then `TryMend(fracture.Owner)`
(`WoundSurgerySystem.cs:67-71`).

**Wolfgate:** both **SAME** and, unlike Onyx, both in **shared** code:
`Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:88` `public Entity<WoundComponent, WoundFractureComponent>? GetFracture(Entity<WoundableComponent?> part)`;
`:100` `public bool TryMend(Entity<WoundComponent?> wound)`;
`:99` `public bool TryReduce(Entity<WoundComponent?> wound) => TrySetTreatment(wound, FractureTreatment.Reduced);`;
`:111` `public bool TrySetTreatment(Entity<WoundComponent?> wound, FractureTreatment treatment)`
(server-gated internally by `if (!_net.IsServer …) return false`).
`TryMend` respects `profile.RemoveWoundWhenMended` and removes the wound. The `cybernetic:` variant is
dropped (no `CyberneticsComponent` scope in phase 4). `SurgeryFractureGradeConditionComponent` maps to a new
`WolfmedSurgeryFractureConditionComponent` with `minGrade` / `grade` / `treatment`.

DECISIONS says "BoneGel/BoneSetter drive reduction → mended". Onyx has **one** step (BoneGel → mended); the
two-step reduction ladder is a Wolfgate design addition — **U-3 in §7**.

#### f) `SurgeryHealAmputationConsequence` (`surgeries.yml:1158`)

```yaml
  - type: Surgery
    useTargetPartIcon: true
    steps: { default: { steps: [ SurgeryOpenIncision, SurgeryStepHealAmputationConsequence ] } }
  - type: SurgeryHasWoundCondition
    woundPrototype: AmputationConsequenceWound
```
Step (`surgery_steps.yml:1013-1024`): `tool: [ { type: Tending } ]`, `duration: 2`,
`SurgeryTreatWoundEffect woundPrototype: AmputationConsequenceWound` (so `Amount = MaxValue` → one step
clears it).

**Wolfgate:** the wound already ships —
```yaml
# Resources/Prototypes/_Onyx/Wounds/wounds.yml:397-401
- type: wound
  id: AmputationConsequenceWound
  name: wound-name-amputation-consequence
  damageTypes: {}
  mergeMode: SeparateInstances
  maximumSeverity: 200
```
created by `AmputationSystem.ApplyAmputationConsequences` (`Content.Shared/_Onyx/Wounds/AmputationSystem.cs:59-69`):
`_wounds.CreateOrMergeWound(parent, host.AmputationConsequenceWound, _wfPart.Get(parent).AmputationConsequenceSeverity)`,
default severity 35 (`WoundHostComponent.AmputationConsequenceWound = "AmputationConsequenceWound"`,
`WoundDamageComponents.cs:67`). Because `damageTypes: {}`, nothing in `TryHealWounds` or passive recovery can
touch it — **only `TreatWound` clears it**, which is exactly Onyx's design.

#### g) `SurgeryHeal<Organ>` (`surgeries.yml:908-1080`)

Ten surgeries (Brain, Eyes, Tongue, Ears, Heart, Lungs, Liver, Stomach, Kidneys, Appendix), each
`steps: [ <SurgeryOpenIncision | SurgeryOpenRibcage | SurgeryOpenSkull>, SurgeryStepHeal<Organ> ]` with
`SurgeryOrganCondition { slot: <Slot>, damaged: true, part: <Head|Chest|Groin> }`.

Step base (`surgery_steps.yml:845-857`):
```yaml
- type: entity
  id: SurgeryStepHealOrganBase
  abstract: true
  components:
  - type: SurgeryStep
    duration: 2
    tool: [ { type: Tending } ]
  - type: RepeatSurgeryStep
  - type: SurgeryStepPainInflicter { amount: 24, sleepModifier: 0 }
```
each concrete step adding `SurgeryOrganHealEffect { slot: X, amount: 1 }`.

**API** (`Content.Shared/_Onyx/Medical/Surgery/SharedSurgerySystem.Organs.cs:95-120`):
`_organHealth.ChangeHealth(organ, ent.Comp.Amount)`; valid when `organ.Comp.Health < organ.Comp.MaxHealth`;
complete when `Health >= MaxHealth`. With `amount: 1` and `MaxHealth 15` that is up to 15 repeats.

**Wolfgate — DIFFERENT, in three ways:**
1. `ChangeHealth` is **SAME in signature but the entity type differs**:
   ```csharp
   // Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:80-81  (namespace Content.Shared._Onyx.Body.Systems)
   public void ChangeHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 amount) => SetHealth(organ, organ.Comp.Health + amount);
   // :65
   public void SetHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 health)
   ```
   Onyx takes `Entity<OrganComponent>`; Wolfgate takes `Entity<WolfmedOrganComponent>` (D8 — health lives on
   `Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs:10-22`,
   `{ FixedPoint2 Health = 15; FixedPoint2 MaxHealth = 15; ProtoId<WoundPrototype>? DestructionWound; FixedPoint2 DestructionWoundSeverity }`,
   `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]` with `Health`/`MaxHealth`
   `[AutoNetworkedField]` → readable client-side).
2. `OrganHealthSystem` **lives in `Content.Server`** despite its `Content.Shared._Onyx.Body.Systems`
   namespace. The `SurgeryStepEvent` handler must be server-side; `SurgeryValidEvent` and
   `SurgeryStepCompleteCheckEvent` handlers read the networked component directly and stay shared.
3. `SharedBodySystem.TryGetOrganInSlot` is **MISSING** in Wolfgate (grepped; the phase-3 comment at
   `OrganHealthSystem.cs:86-87` says so explicitly: *"Wolfgate's SharedBodySystem has no
   TryGetOrganInSlot/TryRemoveOrgan"*). The available API is
   `_body.GetPartOrgans(args.Part)` returning `(EntityUid, OrganComponent)` pairs whose
   `OrganComponent.SlotId` is the slot string — the pattern Shitmed itself uses at
   `SharedSurgerySystem.Steps.cs:133`:
   `_body.GetPartOrgans(args.Part).ToDictionary(o => o.Item2.SlotId, o => o)`.

Wolfgate's organ set (human lineage only, §8.6-7) is **7**, not 10 — no Tongue, Ears or Appendix.
`Resources/Prototypes/Body/Organs/human.yml` carries the phase-3 marked parents:
`OrganHumanBrain :53`, `OrganHumanEyes :103`, `OrganHumanLungs :150`, `OrganHumanHeart :189`,
`OrganHumanStomach :215`, `OrganHumanLiver :249`, `OrganHumanKidneys :270`.
Slot ids from `Resources/Prototypes/Body/Prototypes/human.yml:11-27`: head → `brain`, `eyes`;
torso → `heart`, `lungs`, `stomach`, `liver`, `kidneys`. **There is no `Groin` part and no kidneys surgery in
Wolfgate today** — `SurgeryHealKidneys` would be the first kidney surgery in the fork (torso, ribcage).

Onyx's `SurgeryOrganCondition` addresses organs by `ProtoId<OrganCategoryPrototype> slot`; Wolfgate's
`SurgeryOrganConditionComponent` (`Conditions/SurgeryOrganConditionComponent.cs:8-18`) addresses them by
`ComponentRegistry Organ`, and **several of those marker components are server-only** — Shitmed itself
admits this at `surgeries.yml:64-66` (*"Most of the organs are only defined on the server"*):
`Content.Server/Body/Components/StomachComponent.cs:11` is server-only, while
`Content.Shared/_Shitmed/Body/Organ/LiverComponent.cs:6` is shared. **A shared condition keyed on a
component registry would behave differently on client and server.** The new `_WF` condition therefore keys
on the **slot string**, matching Onyx's intent while using only `GetPartOrgans`.

#### h) Scars — already done, no port needed

Onyx's `SurgerySystem.WoundEffects.cs:25-43` `OnCloseIncisionComplete` rolls
`CCVars.SurgeryScarChance` when the close-incision step runs. **Wolfgate already does this automatically**,
driven by the wound's own state change rather than the surgery:

```csharp
// Content.Shared/_Onyx/Wounds/WoundScarSystem.cs:23
SubscribeLocalEvent<WoundComponent, WoundStateChangedEvent>(OnWoundStateChanged);
// :48-56
var globalChance = Math.Clamp(_cfg.GetCVar(CCVars.SurgeryScarChance), 0f, 1f);
var finalChance = baseChance * globalChance;
if (finalChance < 1f && !_random.Prob(finalChance)) return;
CreateScar((wound.Owner, wound.Comp));
```
`CCVars.SurgeryScarChance` is ported (`Content.Shared/_Onyx/CCVar/CCVars.Surgery.cs:7-8`,
`"surgery.scar_chance"`, default `0.35f`), `SurgicalIncisionWound` is ported
(`wounds.yml:373-383`, `damageTypes: {}`, `WoundBleedingBehavior rate 0.1 awakeMultiplier 3`), and
`WoundScarSystem.CreateScar(Entity<WoundComponent?>)` is **SAME** (`:64`).

**Gap:** nothing in Wolfgate ever *creates* a `SurgicalIncisionWound` — Onyx's `SurgeryStepBleedEffect`
(`SurgerySystem.WoundEffects.cs:15-18`, `_wounds.CreateOrMergeWound(args.Part, SurgicalIncision, ent.Comp.Damage)`)
has no Wolfgate counterpart; `SurgeryStepOpenIncisionScalpel` instead charges 10 flat `Bloodloss`
(`surgery_steps.yml:22-26`). So today, **surgery in Wolfgate never scars**. This is the only piece of
Onyx's `SurgerySystem.WoundEffects.cs` worth re-expressing — **U-4 in §7**.

#### i) Pain during surgery — not in Wolfgate

Onyx attaches `SurgeryStepPainInflicter` to 8 of the wound/organ steps
(`SurgeryEffects.cs:10-13`, `{ FixedPoint2 Amount = 5; FixedPoint2 SleepModifier = 1 }`;
handler `SharedSurgerySystem.Tools.cs:53-63` → `_pain.ChangePain((args.Part, null), amount)`).
Wolfgate has no surgery pain effect. `PainSystem.ChangePain` is **SAME**
(`Content.Shared/_Onyx/Wounds/PainSystem.cs:225`: `public bool ChangePain(Entity<PainComponent?> entity, FixedPoint2 delta)`)
and `PainComponent` lives on the **part** (`PainSystem.cs:69` `EnsureComp<PainComponent>(part)`), so a
`WolfmedSurgeryPainEffectComponent` is ~12 lines. Optional; **U-5 in §7**.

### 2.4 Locale (`Resources/Locale/en-US/_Onyx/medical/surgery.ftl`)

```
:65 surgery-popup-step-SurgeryStepClampWoundBleeding = { $user } is suturing the bleeding wound on { $target }'s { $part }.
:66 surgery-popup-step-SurgeryStepMendFracture = { $user } is mending the fracture on { $target }'s { $part }.
:68 surgery-popup-step-SurgeryStepHealAmputationConsequence = { $user } is repairing the amputation damage on { $target }'s { $part }.
:69 surgery-popup-step-SurgeryStepStopInternalBleeding = { $user } is stopping internal bleeding in { $target }'s { $part }.
:153-157 surgery-popup-step-SurgeryStepHeal{Heart,Lungs,Liver,Stomach,Kidneys} = { $user } is healing { $target }'s <organ>.
```
Onyx uses `{ $user }` with spaces; Wolfgate's file uses `{$user}` without. Both parse; match Wolfgate's
style. Onyx's entity-name ftl (`_Onyx/prototypes/entities/surgery/surgery.ftl`) is **not** needed — Wolfgate
declares `name:` inline.

---

## 3. The Wolfgate design

### 3.1 Two marked extensions of Wolfgate's colliding components (D7)

**E-1 — `SurgeryWoundedConditionComponent`** (`Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs`):

```csharp
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component
{
    // WOLFGATE (P4-3, D7): Wolfmed wound-severity window, so deep and shallow tending are distinct
    // surgeries as in Onyx. Null bounds keep the pre-Wolfmed behaviour exactly.
    [DataField] public ProtoId<DamageGroupPrototype> WoundGroup = "Brute";
    [DataField] public FixedPoint2? MinWoundSeverity;
    [DataField] public FixedPoint2? MaxWoundSeverity;
}
```
(the file currently ends `public sealed partial class SurgeryWoundedConditionComponent : Component;` at
line 7, so the change is `;` → a body — 6 added lines.)

**HOOK A — 2 marked lines** in `SharedSurgerySystem.OnWoundedValid` (`SharedSurgerySystem.cs:120-128`),
appended after the existing check:
```csharp
        if (WolfmedWoundWindowFails(ent, args.Body, args.Part)) // WOLFGATE (P4-3)
            args.Cancelled = true;
```
body in `Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs`
(`namespace Content.Shared._Shitmed.Medical.Surgery; public abstract partial class SharedSurgerySystem`),
precedent: `Content.Server/_WF/Wolfmed/Medical/HealingSystem.Wolfmed.cs:19-21`
(`namespace Content.Server.Medical; public sealed partial class HealingSystem`). The helper returns
`false` immediately when both bounds are null **and** when the body has no `WoundHostComponent`, so
non-wound-hosts and the two existing prototypes are provably unchanged.

**E-2 — `SurgeryTendWoundsEffectComponent`: recommend NO change.** Its handler already reaches wounds
through routing (§1.5). The only Onyx feature it lacks — computing the bonus from *wound severity* rather
than *projected group damage* — is a balance nuance, not a capability. If the user wants it, it is one
marked `[DataField] public bool UseWoundSeverity;` plus a 3-line branch; recorded as a deviation either way.

### 3.2 New `_WF` components (8) and their subscription pairs

All under `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryComponents.cs`
(`namespace Content.Shared._WF.Wolfmed.Surgery`), one file, Onyx-shaped but renamed:

| Component | Registered name | Fields |
|---|---|---|
| `WolfmedSurgeryWoundConditionComponent` | `WolfmedSurgeryWoundCondition` | `ProtoId<WoundPrototype>? WoundPrototype; WoundVisibility? Visibility; WoundState? State; bool Bleeding; bool InternalBleeding; bool Inverse` |
| `WolfmedSurgeryClampBleedingEffectComponent` | `WolfmedSurgeryClampBleedingEffect` | `FixedPoint2 Amount (required); ProtoId<WoundPrototype>? WoundPrototype` |
| `WolfmedSurgeryTreatWoundEffectComponent` | `WolfmedSurgeryTreatWoundEffect` | `ProtoId<WoundPrototype>? WoundPrototype; ProtoId<DamageGroupPrototype>? DamageGroup; bool InternalBleeding; FixedPoint2 Amount = FixedPoint2.MaxValue; DamageSpecifier Damage = new()` |
| `WolfmedSurgeryFractureConditionComponent` | `WolfmedSurgeryFractureCondition` | `FractureGrade MinGrade = FractureGrade.Hairline; FractureGrade? Grade; FractureTreatment? Treatment` |
| `WolfmedSurgeryMendFractureEffectComponent` | `WolfmedSurgeryMendFractureEffect` | `FractureTreatment Treatment = FractureTreatment.Mended` |
| `WolfmedSurgeryOrganDamagedConditionComponent` | `WolfmedSurgeryOrganDamagedCondition` | `string Slot (required); bool Inverse` |
| `WolfmedSurgeryOrganHealEffectComponent` | `WolfmedSurgeryOrganHealEffect` | `string Slot (required); FixedPoint2 Amount = 1` |
| `WolfmedSurgeryPainEffectComponent` *(optional, U-5)* | `WolfmedSurgeryPainEffect` | `FixedPoint2 Amount = 5; FixedPoint2 SleepModifier = 1` |

`Inverse` on the wound and organ conditions is the addition that lets the same component gate the attach
surgeries (§4).

**Collision check — all eight registered names were grepped against the whole tree and return 0 hits**
(`grep -rl "class Wolfmed…Component" --include=*.cs .` excluding `RobustToolbox`). See §6.2 for the full
audit including the systems.

**Subscription pairs** — every `(Component, Event)` below was grepped against every existing subscriber in
`Content.Shared`, `Content.Server`, `Content.Client` (§6.1). All are new; none duplicates.

*Shared* — `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryConditionSystem.cs`
(`public sealed class WolfmedSurgeryConditionSystem : EntitySystem`):

| # | Subscription |
|---|---|
| S1 | `SubscribeLocalEvent<WolfmedSurgeryWoundConditionComponent, SurgeryValidEvent>` |
| S2 | `SubscribeLocalEvent<WolfmedSurgeryFractureConditionComponent, SurgeryValidEvent>` |
| S3 | `SubscribeLocalEvent<WolfmedSurgeryOrganDamagedConditionComponent, SurgeryValidEvent>` |
| S4 | `SubscribeLocalEvent<WolfmedSurgeryClampBleedingEffectComponent, SurgeryStepCompleteCheckEvent>` |
| S5 | `SubscribeLocalEvent<WolfmedSurgeryTreatWoundEffectComponent, SurgeryStepCompleteCheckEvent>` |
| S6 | `SubscribeLocalEvent<WolfmedSurgeryMendFractureEffectComponent, SurgeryStepCompleteCheckEvent>` |
| S7 | `SubscribeLocalEvent<WolfmedSurgeryOrganHealEffectComponent, SurgeryStepCompleteCheckEvent>` |

*Server* — `Content.Server/_WF/Wolfmed/Surgery/WolfmedWoundSurgerySystem.cs`
(`public sealed class WolfmedWoundSurgerySystem : EntitySystem`):

| # | Subscription |
|---|---|
| V1 | `SubscribeLocalEvent<WolfmedSurgeryClampBleedingEffectComponent, SurgeryStepEvent>` |
| V2 | `SubscribeLocalEvent<WolfmedSurgeryTreatWoundEffectComponent, SurgeryStepEvent>` |
| V3 | `SubscribeLocalEvent<WolfmedSurgeryMendFractureEffectComponent, SurgeryStepEvent>` |
| V4 | `SubscribeLocalEvent<WolfmedSurgeryOrganHealEffectComponent, SurgeryStepEvent>` |
| V5 *(optional)* | `SubscribeLocalEvent<WolfmedSurgeryPainEffectComponent, SurgeryStepEvent>` |

**Why the split is mandatory, not stylistic.** The *check* events must be shared because the client calls
`GetNextStep` → `IsStepComplete` (`SurgeryBui.cs:281`) — a server-only check handler makes the client treat
the step as permanently complete and highlight the wrong row. The *step* events must be server-side
because `WoundBleedingSystem` (`Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs`) and
`OrganHealthSystem` (`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`) are server-only assemblies.
Because the event *types* differ, one component being handled by two systems is legal — the crash rule is
one system per `(component, event)` pair, and every pair above is unique.

All the data the shared conditions read is networked: `WoundComponent`
(`WoundDamageComponents.cs:180`, `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]`,
`Prototype`/`Severity`/`State` all `[AutoNetworkedField]`), `WoundBleedingComponent` (`:209`),
`WoundInternalBleedingComponent` (`:235`), `WoundFractureComponent` (`:245`), `WoundScarComponent` (`:258`),
`WoundableComponent` (`:155`), `WolfmedOrganComponent`. The wound container is a plain
`Container` (`WoundableComponent.ContainerId = "wounds"`, `:158`), so contained wound entities replicate.

### 3.3 Dependencies each system needs (all verified present)

`WolfmedSurgeryConditionSystem` (shared): `IPrototypeManager`, `WoundSystem` (shared),
`WoundFractureSystem` (shared), `SharedBodySystem`.
`WolfmedWoundSurgerySystem` (server): `IPrototypeManager`, `WoundSystem`, `WoundBleedingSystem` (server),
`WoundFractureSystem`, `WoundDamageRoutingSystem` (shared),
`Content.Shared._Onyx.Body.Systems.OrganHealthSystem` (server), `SharedBodySystem`, `MobStateSystem`,
`PainSystem` (optional).

A shared `FindWound` helper (port of `WoundSurgerySystem.cs:187-231`) goes in the shared system and is
called from the server one via a `[Dependency]`; or duplicate ~30 lines. Recommend the dependency.

### 3.4 Symbol table — every external symbol, WG verdict

| Symbol used by Onyx's wound surgery | WG verdict | Evidence |
|---|---|---|
| `WoundSystem.GetWounds(Entity<WoundableComponent?>)` | **SAME** | `Content.Shared/_Onyx/Wounds/WoundSystem.cs:155` `public IEnumerable<Entity<WoundComponent>> GetWounds(Entity<WoundableComponent?> part)` |
| `WoundSystem.TreatWound(Entity<WoundComponent?>, FixedPoint2)` | **SAME** | `WoundSystem.cs:316` |
| `WoundSystem.ChangeSeverity(Entity<WoundComponent?>, FixedPoint2)` | **SAME** | `WoundSystem.cs:284` |
| `WoundSystem.SetWoundState(Entity<WoundComponent?>, WoundState)` | **SAME** | `WoundSystem.cs:338` |
| `WoundSystem.CloseWound` / `RemoveWound` | **SAME** | `WoundSystem.cs:354`, `:356` |
| `WoundSystem.TryHealWounds(Entity<WoundableComponent?>, DamageSpecifier, IReadOnlySet<string>?)` | **SAME** | `WoundSystem.cs:400-401` |
| `WoundSystem.CreateOrMergeWound(Entity<WoundableComponent?>, ProtoId<WoundPrototype>, FixedPoint2)` | **SAME** | `WoundSystem.cs:166-169` |
| `WoundBleedingSystem.ReduceBleeding(Entity<WoundComponent?>, FixedPoint2)` | **SAME**, server-only assembly | `Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs:142` |
| `WoundBleedingSystem.SetTreatment(Entity<WoundComponent?>, BleedingTreatment)` | **SAME**, server-only | `WoundBleedingSystem.cs:131` |
| `WoundBleedingSystem.TreatPart(Entity<WoundableComponent?>, BleedingTreatment, ProtoId<WoundPrototype>?)` | **SAME**, server-only | `WoundBleedingSystem.cs:257-258` |
| `WoundFractureSystem.GetFracture(Entity<WoundableComponent?>)` | **SAME**, and **shared** in WG (server in Onyx) | `Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:88` |
| `WoundFractureSystem.TryMend(Entity<WoundComponent?>)` | **SAME**, shared | `WoundFractureSystem.cs:100` |
| `WoundFractureSystem.TryReduce` / `TrySetTreatment` | **SAME**, shared | `WoundFractureSystem.cs:99`, `:111` |
| `WoundScarSystem.CreateScar(Entity<WoundComponent?>)` | **SAME**, shared | `Content.Shared/_Onyx/Wounds/WoundScarSystem.cs:64` |
| `WoundDamageRoutingSystem.TryApplyPartDamage(EntityUid, EntityUid, DamageSpecifier, EntityUid?, bool, bool)` | **SAME** | `WoundDamageRoutingSystem.cs:240-249` |
| `OrganHealthSystem.ChangeHealth` | **DIFFERENT** — Onyx `ChangeHealth(Entity<OrganComponent>, FixedPoint2)`; WG `public void ChangeHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 amount)` | `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:80` |
| `OrganHealthSystem.SetHealth` | **DIFFERENT** — same reason | `OrganHealthSystem.cs:65` |
| `SharedBodySystem.TryGetOrganInSlot(part, slot, out organId)` | **MISSING** — use `GetPartOrgans(part)` + `OrganComponent.SlotId` | comment at `OrganHealthSystem.cs:86-87`; usage pattern `SharedSurgerySystem.Steps.cs:133` |
| `SharedBodySystem.HasAmputationConsequence(EntityUid)` | **MISSING** — Onyx `Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:209`; must be reimplemented in `_WF` (§4) | grepped, 0 hits in WG |
| `SharedBodySystem.TryAttachPart(parentId, slot, partId)` | **MISSING** — WG has `AttachPart(EntityUid parentPartId, string slotId, EntityUid partId, BodyPartComponent? parentPart = null, BodyPartComponent? part = null)` | `Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:652-658` |
| `SharedBodySystem.CanAttachPart(EntityUid parentId, string slotId, EntityUid partId, …)` | **SAME name, WG-specific body** | `SharedBodySystem.Parts.cs:606-619` |
| `StepInvalidReason.AmputationConsequence` | **MISSING** — WG enum is `{None, MissingSkills, NeedsOperatingTable, Armor, MissingTool}` | `StepInvalidReason.cs:3-10` |
| `SurgeryTargetPartContextComponent` | **MISSING** — WG's `SurgeryStepEvent` already carries `Part`; no equivalent needed | grepped |
| `MechanicalSurgeryStepComponent` / `CyberneticsComponent` | **MISSING** — cybernetic fracture variant dropped | grepped |
| `RepeatSurgeryStepComponent` | **DIFFERENT name** — WG `SurgeryRepeatableStepComponent` (`Steps/SurgeryRepeatableStepComponent.cs:6`) | — |
| `StitchesComponent` | **MISSING** — Onyx `Content.Shared/_Onyx/Medical/Surgery/SurgeryToolComponents.cs:25`; map to `Hemostat`/`Tending` | grepped |
| `PainSystem.ChangePain(Entity<PainComponent?>, FixedPoint2)` | **SAME** | `Content.Shared/_Onyx/Wounds/PainSystem.cs:225` |
| `CCVars.SurgeryScarChance` | **SAME** | `Content.Shared/_Onyx/CCVar/CCVars.Surgery.cs:7-8` |
| `WoundVisibility`, `WoundState`, `BleedingTreatment`, `FractureGrade`, `FractureTreatment` | **SAME** | `WoundPrototype.cs:305`, `WoundDamageComponents.cs:262`, `:272`, and `FractureGrade`/`FractureTreatment` in the same files |
| `AmputationConsequenceWound`, `InternalBleedingWound`, `SurgicalIncisionWound`, `MedicalScarWound` | **SAME** | `Resources/Prototypes/_Onyx/Wounds/wounds.yml:398`, `:405`, `:374`, `:416` |

---

## 4. The two phase-3 gaps

### 4.1 `AmputationConsequenceWound` must block re-attachment

**Current state (verified).** The wound is created
(`Content.Shared/_Onyx/Wounds/AmputationSystem.cs:69`,
`_wounds.CreateOrMergeWound(parent, host.AmputationConsequenceWound, _wfPart.Get(parent).AmputationConsequenceSeverity)`)
and read by nothing. `Docs/Wolfmed/WOLFMED_STATUS.md` records it as inert:
*"`AmputationConsequenceWound` is inert — it marks a stump for examine but does not yet block Shitmed's
`CanAttachPart`, so a severed limb can still be surgically re-attached (P3-D2, by design)."*
`Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs:23-32` names the two Onyx tests
skipped for this reason.

**Onyx's mechanism.**
```csharp
// ONYX Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:209-223
public bool HasAmputationConsequence(EntityUid part)
{
    if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body is not { } body ||
        !TryComp(body, out WoundHostComponent? host) ||
        !_containers.TryGetContainer(part, WoundableComponent.ContainerId, out var container))
        return false;
    foreach (var entity in container.ContainedEntities)
        if (TryComp(entity, out WoundComponent? wound) && wound.Prototype == host.AmputationConsequenceWound)
            return true;
    return false;
}
```
used in **three** places: `TryAttachPart(parentId, partId)` (`:253`), `TryAttachPart(parentId, slot, partId)`
(`:389`), and the surgery UI gate
`SharedSurgerySystem.BodyParts.cs:148-153` (`StepInvalidReason.AmputationConsequence` + popup
`surgery-ui-reason-amputation-consequence`).

**Wolfgate's attach path.**
```csharp
// Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:606-619
public bool CanAttachPart(EntityUid parentId, string slotId, EntityUid partId, BodyPartComponent? parentPart = null, BodyPartComponent? part = null)
{
    return Resolve(partId, ref part, logMissing: false)
        && Resolve(parentId, ref parentPart, logMissing: false)
        && parentPart.Children.TryGetValue(slotId, out var parentSlotData)
        && part.PartType == parentSlotData.Type
        && Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container)
        && Containers.CanInsert(partId, container);
}
// :667-697  AttachPart(parentPartId, BodyPartSlot slot, partId, …) calls CanAttachPart(parentPartId, slot.Id, …) at :676
// :591-601  CanAttachPart(parentId, BodyPartSlot slot, …) delegates to the string overload at :600
```
Shitmed's attach surgery step:
```csharp
// SharedSurgerySystem.Steps.cs:217-238
private void OnAddPartStep(Entity<SurgeryAddPartStepComponent> ent, ref SurgeryStepEvent args)
{
    if (!TryComp(args.Surgery, out SurgeryPartRemovedConditionComponent? removedComp)) return;
    foreach (var tool in args.Tools)
        if (TryComp(tool, out BodyPartComponent? partComp) && partComp.PartType == removedComp.Part && …)
        {
            var slotName = …;
            _body.TryCreatePartSlot(args.Part, slotName, partComp.PartType, out var _);
            _body.AttachPart(args.Part, slotName, tool);
            EnsureComp<BodyPartReattachedComponent>(tool);
            …
        }
}
```
and the condition that makes those surgeries visible:
```csharp
// SharedSurgerySystem.cs:255-274
private void OnPartRemovedConditionValid(Entity<SurgeryPartRemovedConditionComponent> ent, ref SurgeryValidEvent args)
{
    if (!_body.CanAttachToSlot(args.Part, ent.Comp.Connection)) { args.Cancelled = true; return; }
    var results = _body.GetBodyChildrenOfType(args.Body, ent.Comp.Part, symmetry: ent.Comp.Symmetry);
    if (results is not { } || !results.Any()) return;
    if (!results.Any(part => HasComp<BodyPartReattachedComponent>(part.Id))) args.Cancelled = true;
}
```

**`CanAttachPart` has four non-surgery callers** — this is the finding that changes the recommendation:

| Caller | File:line |
|---|---|
| Shitmed body construction | `Content.Shared/_Shitmed/BodyEffects/Subsystems/GenerateChildPartSystem.cs:48` |
| Goob autosurgeon | `Content.Shared/_Goobstation/Autosurgeon/AutoSurgeonSystem.cs:116` |
| **Mono prybar prosthetics** | `Content.Server/_Mono/Traits/Physical/PrybarProstheticsSystem.cs:118` |
| **Mono bionic legs** | `Content.Server/_Mono/Traits/Physical/BionicLegsSystem.cs:104` |

`PrybarProstheticsSystem` (`:110-122`) spawns a replacement part and attaches it to the parent; a blanket
`CanAttachPart` guard would make it silently `QueueDel(newPart)` whenever the parent stump carries the
consequence wound — exactly the "you lost a leg, bolt on a prosthetic" case. `BionicLegsSystem` is the same
shape. Neither is in P4-3's scope.

**Recommended fix — HOOK B, one site, two lines, no `CanAttachPart` change:**

```csharp
// SharedSurgerySystem.cs, inside OnPartRemovedConditionValid, after the CanAttachToSlot guard
        if (WolfmedStumpBlocksAttachment(args.Part)) // WOLFGATE (P4-3): untreated amputation consequence
        { args.Cancelled = true; return; }
```
Body in the same `_WF` partial as HOOK A:
```csharp
// Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs
/// <summary>True while a stump still carries an untreated AmputationConsequenceWound.</summary>
private bool WolfmedStumpBlocksAttachment(EntityUid parent) =>
    TryComp(parent, out BodyPartComponent? bodyPart) && bodyPart.Body is { } body &&
    TryComp(body, out WoundHostComponent? host) &&
    _wolfmedWounds.GetWounds(parent).Any(w => w.Comp.Prototype == host.AmputationConsequenceWound);
```

Because `OnTargetDoAfter` (`SharedSurgerySystem.cs:94`) re-runs `IsSurgeryValid`, which raises
`SurgeryValidEvent` on the surgery singleton, this **both** hides all 11 `SurgeryAttach*` surgeries from the
BUI **and** aborts a do-after that somehow started. It is server- and client-consistent (wounds replicate).
It leaves `CanAttachPart` — and therefore prosthetics, autosurgeon and body construction — untouched.

**Cost of the alternative** (`CanAttachPart` guard, what DECISIONS literally names): 1 marked line at
`SharedSurgerySystem.Parts.cs:613`, but it silently breaks two Mono traits and the Goob autosurgeon, and
gives the medic no UI feedback (the surgery still lists; the step just does nothing). **U-1 in §7.**

**Escape valve.** With HOOK B, an amputee with no surgeon can never be re-limbed. The cure is
`SurgeryHealAmputationConsequence` (§5.2), which needs `SurgeryOpenIncision` + a `Tending` tool (the
`Hemostat` and `Omnimed` both have it) and clears the wound in **one** step because
`WolfmedSurgeryTreatWoundEffectComponent.Amount` defaults to `FixedPoint2.MaxValue`. Note that
`AmputationConsequenceWound` has `damageTypes: {}`, so `TryHealWounds`, topicals and passive recovery cannot
touch it — surgery is the *only* cure, by design.

### 4.2 Organ healing

**Available API** (server, `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`, namespace
`Content.Shared._Onyx.Body.Systems`):
```csharp
:65  public void SetHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 health)
:80  public void ChangeHealth(Entity<WolfmedOrganComponent> organ, FixedPoint2 amount) => SetHealth(organ, organ.Comp.Health + amount);
```
`SetHealth` clamps to `[0, MaxHealth]` and raises `OrganFunctionChangedEvent(body, functional)`
(`OrganHealthSystem.cs:21`) when the organ crosses zero — so healing a dead-but-still-present organ
re-enables it and the phase-3 consequence systems (`WolfmedOrganConsequenceSystem`) see the transition.

**Caveat worth stating to the user:** once an organ reaches 0 HP, `OrganHealthSystem.Update`
(`:33-63`) destroys it on the next tick (`DestroyOrgan` → `RemoveOrgan` + `QueueDel`), except the brain
(which kills the mob instead, `:45-53`). So `SurgeryHeal<Organ>` can only save an organ **before** it hits
zero. Healing a destroyed organ is impossible by construction; the cure there is
`SurgeryInsertHeart`/`InsertLungs`/… which already exist. Phase-3's "organ damage ships irreversible"
(§8.6-4) becomes "organ damage is reversible while the organ lives".

**Design:** `WolfmedSurgeryOrganHealEffectComponent { string Slot; FixedPoint2 Amount = 1 }`, resolved via
```csharp
private bool TryFindOrgan(EntityUid part, string slot, out Entity<WolfmedOrganComponent> organ)
{
    foreach (var (id, comp) in _body.GetPartOrgans(part))
        if (comp.SlotId == slot && TryComp(id, out WolfmedOrganComponent? health))
        { organ = (id, health); return true; }
    organ = default; return false;
}
```
`SurgeryValidEvent` (shared, on the **surgery** prototype via `WolfmedSurgeryOrganDamagedCondition`):
cancel unless `Health < MaxHealth`. `SurgeryStepCompleteCheckEvent` (shared): cancel while
`Health < MaxHealth`. `SurgeryStepEvent` (server): `_organHealth.ChangeHealth(organ, Amount)`.
With `Amount: 1` and `MaxHealth 15`, a fully wrecked-but-living organ takes up to 15 × 2 s repeats — Onyx's
number. **U-6 in §7** proposes `amount: 3` (5 repeats) for Wolfgate's faster pace.

---

## 5. Prototype plan

All new YAML under `Resources/Prototypes/_WF/Wolfmed/Surgery/` (two files, matching Shitmed's split):
`surgeries.yml` and `surgery_steps.yml`. New locale:
`Resources/Locale/en-US/_WF/wolfmed/surgery-popup.ftl`.

### 5.1 Steps (`_WF/Wolfmed/Surgery/surgery_steps.yml`)

```yaml
# Wolfmed surgery steps. Re-expresses Space Onyx's wound surgeries on Shitmed's step system (D7, P4-3).
# Sprites and tools reuse Wolfgate's existing Shitmed set - no new tool prototype is needed.

- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepSutureBleeding
  name: Suture the bleeding wound
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: Hemostat
    duration: 2
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/hemostat.rsi
    state: hemostat
  - type: WolfmedSurgeryClampBleedingEffect
    amount: 10
  - type: SurgeryRepeatableStep

- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepStopInternalBleeding
  name: Stop internal bleeding
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: Hemostat
    duration: 4
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/hemostat.rsi
    state: hemostat
  - type: WolfmedSurgeryTreatWoundEffect
    internalBleeding: true
  - type: SurgeryStepEmoteEffect

- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepSetBone
  name: Set the bone
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: BoneSetter        # the one shipped-but-unused Shitmed tool; see U-3
    duration: 3
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/bonesetter.rsi
    state: bonesetter
  - type: WolfmedSurgeryMendFractureEffect
    treatment: Reduced
  - type: SurgeryStepEmoteEffect

- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepMendFracture
  name: Mend the fracture
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: BoneGel
    duration: 3
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/bone-gel.rsi
    state: bone-gel
  - type: WolfmedSurgeryMendFractureEffect
    treatment: Mended

- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepHealAmputationConsequence
  name: Repair the amputation damage
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: Tending
    duration: 4
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/hemostat.rsi
    state: hemostat
  - type: WolfmedSurgeryTreatWoundEffect
    woundPrototype: AmputationConsequenceWound
  - type: SurgeryStepEmoteEffect

# Organ healing -----------------------------------------------------------
- type: entity
  parent: SurgeryStepBase
  id: SurgeryStepHealOrganBase
  abstract: true
  categories: [ HideSpawnMenu ]
  components:
  - type: SurgeryStep
    tool:
    - type: Tending
    duration: 2
  - type: Sprite
    sprite: _Shitmed/Objects/Specific/Medical/Surgery/hemostat.rsi
    state: hemostat
  - type: SurgeryRepeatableStep
  - type: SurgeryStepEmoteEffect

- type: entity
  parent: SurgeryStepHealOrganBase
  id: SurgeryStepHealBrain
  name: Heal the brain
  components: [ { type: WolfmedSurgeryOrganHealEffect, slot: brain, amount: 1 } ]
# …identical entries for eyes / heart / lungs / liver / stomach / kidneys
```

### 5.2 Surgeries (`_WF/Wolfmed/Surgery/surgeries.yml`)

```yaml
- type: entity
  parent: SurgeryBase
  id: SurgeryStopBleeding
  name: Stop Bleeding
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    steps:
    - SurgeryStepSutureBleeding
  - type: WolfmedSurgeryWoundCondition
    state: Open
    bleeding: true

- type: entity
  parent: SurgeryBase
  id: SurgeryStopInternalBleeding
  name: Stop Internal Bleeding
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenIncision          # reuses Wolfgate's existing surgery
    steps:
    - SurgeryStepStopInternalBleeding
    - SurgeryStepSealTendWound                # reuses Wolfgate's existing cautery step
  - type: WolfmedSurgeryWoundCondition
    internalBleeding: true

- type: entity
  parent: SurgeryBase
  id: SurgeryMendFracture
  name: Mend Fracture
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenIncision
    steps:
    - SurgeryStepSetBone                      # BoneSetter -> FractureTreatment.Reduced  (U-3)
    - SurgeryStepMendFracture                 # BoneGel    -> FractureTreatment.Mended
    - SurgeryStepSealTendWound
  - type: WolfmedSurgeryFractureCondition
    minGrade: Hairline

- type: entity
  parent: SurgeryBase
  id: SurgeryHealAmputationConsequence
  name: Repair Amputation Damage
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenIncision
    steps:
    - SurgeryStepHealAmputationConsequence
    - SurgeryStepSealTendWound
  - type: WolfmedSurgeryWoundCondition
    woundPrototype: AmputationConsequenceWound

- type: entity
  parent: SurgeryBase
  id: SurgeryTendWoundsBruteDeep
  name: Tend Deep Bruise Wounds
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenIncision
    steps:
    - SurgeryStepRepairBruteTissue            # reuses Wolfgate's existing repeatable tend step
    - SurgeryStepSealTendWound
  - type: SurgeryWoundedCondition
    woundGroup: Brute
    minWoundSeverity: 100                     # WOLFGATE datafield, E-1

- type: entity
  parent: SurgeryBase
  id: SurgeryTendWoundsBurnDeep
  name: Tend Deep Burn Wounds
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenIncision
    steps:
    - SurgeryStepRepairBurnTissue
    - SurgeryStepSealTendWound
  - type: SurgeryWoundedCondition
    woundGroup: Burn
    minWoundSeverity: 100

# Organ healing: 7 surgeries, one per human-lineage organ.
- type: entity
  parent: SurgeryBase
  id: SurgeryHealHeart
  name: Heal Heart
  categories: [ HideSpawnMenu ]
  components:
  - type: Surgery
    requirement: SurgeryOpenRibcage           # Wolfgate's existing ribcage surgery (Torso-gated)
    steps:
    - SurgeryStepHealHeart
    - SurgeryStepSealOrganWound               # reuses Wolfgate's existing cautery/organ step
  - type: SurgeryPartCondition
    part: Torso
  - type: WolfmedSurgeryOrganDamagedCondition
    slot: heart
# …SurgeryHealLungs / Stomach / Liver / Kidneys likewise on SurgeryOpenRibcage + part: Torso;
#   SurgeryHealBrain / SurgeryHealEyes on SurgeryOpenIncision + part: Head.
```

### 5.3 Marked edits to the two existing tend surgeries (optional, recommended)

`Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml:285` and `:298`, adding under
`- type: SurgeryWoundedCondition`:
```yaml
    maxWoundSeverity: 99.99   # WOLFGATE (P4-3): deep wounds need SurgeryTendWoundsBruteDeep instead
```
Without this, a severely wounded limb offers **four** overlapping tend surgeries instead of two.
Two one-line marked YAML edits. (Wolfgate's condition ignores the field on non-wound-hosts, so animals,
borgs and Protogen are unaffected.)

### 5.4 Locale (`Resources/Locale/en-US/_WF/wolfmed/surgery-popup.ftl`)

```
surgery-popup-step-SurgeryStepSutureBleeding = {$user} is suturing the bleeding wound on {$target}'s {$part}.
surgery-popup-step-SurgeryStepStopInternalBleeding = {$user} is stopping the internal bleeding in {$target}'s {$part}.
surgery-popup-step-SurgeryStepSetBone = {$user} is setting the bone in {$target}'s {$part}.
surgery-popup-step-SurgeryStepMendFracture = {$user} is mending the fracture in {$target}'s {$part}.
surgery-popup-step-SurgeryStepHealAmputationConsequence = {$user} is repairing the amputation damage on {$target}'s {$part}.
surgery-popup-step-SurgeryStepHealBrain = {$user} is healing {$target}'s brain.
surgery-popup-step-SurgeryStepHealEyes = {$user} is healing {$target}'s eyes.
surgery-popup-step-SurgeryStepHealHeart = {$user} is healing {$target}'s heart.
surgery-popup-step-SurgeryStepHealLungs = {$user} is healing {$target}'s lungs.
surgery-popup-step-SurgeryStepHealLiver = {$user} is healing {$target}'s liver.
surgery-popup-step-SurgeryStepHealStomach = {$user} is healing {$target}'s stomach.
surgery-popup-step-SurgeryStepHealKidneys = {$user} is healing {$target}'s kidneys.
```
(`bonesetter.rsi` state `bonesetter` and `bone-gel.rsi` state `bone-gel` both exist —
`Resources/Prototypes/Entities/Objects/Specific/Medical/surgery.yml:224-227`, `:236-241`.)

---

## 6. Audits

### 6.1 Subscription-pair audit

Every existing subscriber of the four surgery events in the tree (grep
`SurgeryValidEvent|SurgeryStepEvent>|SurgeryStepCompleteCheckEvent>|SurgeryCanPerformStepEvent>` filtered to
`Subscribe`):

| Component | Event | System |
|---|---|---|
| `SurgeryCloseIncisionCondition`, `SurgeryCorticalBorerCondition`, `SurgeryPartCondition`, `SurgeryOrganCondition`, **`SurgeryWoundedCondition`**, `SurgeryPartRemovedCondition`, `SurgeryBodyCondition`, `SurgeryOrganSlotCondition`, `SurgeryPartPresentCondition`, `SurgeryMarkingCondition`, `SurgeryBodyComponentCondition`, `SurgeryPartComponentCondition`, `SurgeryOrganOnAddCondition` | `SurgeryValidEvent` | `SharedSurgerySystem` (`SharedSurgerySystem.cs:62-75`) |
| `SurgeryStepComponent` | `SurgeryStepEvent`, `SurgeryStepCompleteCheckEvent`, `SurgeryCanPerformStepEvent` | `SharedSurgerySystem` (`Steps.cs:39-41`) |
| **`SurgeryTendWoundsEffect`**, `SurgeryStepCavityEffect`, `SurgeryStepRemoveCorticalBorer`, `SurgeryAddPartStep`, `SurgeryAffixPartStep`, `SurgeryRemovePartStep`, `SurgeryAddOrganStep`, `SurgeryRemoveOrganStep`, `SurgeryAffixOrganStep`, `SurgeryAddMarkingStep`, `SurgeryRemoveMarkingStep`, `SurgeryAddOrganSlotStep` | `SurgeryStepEvent` + `SurgeryStepCompleteCheckEvent` | `SharedSurgerySystem.SubSurgery` (`Steps.cs:49-61`) |
| `SurgeryStepEmoteEffect`, `SurgeryStepSpawnEffect` | `SurgeryStepEvent` | server `SurgerySystem` (`SurgerySystem.cs:55-56`) |
| `SurgeryTarget` | `SurgeryStepDamageEvent` | server `SurgerySystem` (`:50`) |
| `SurgerySpecialDamageChangeEffect`, `SurgeryDamageChangeEffect` | `SurgeryStepDamageChangeEvent` | server `SurgerySystem` (`:53-54`) |
| `ItemToggle`, `Gun` | `SurgeryToolUsedEvent` | `SurgeryToolConditionsSystem` (`Tools/SurgeryToolConditionsSystem.cs:21-22`) |
| `SurgeryTool` + 11 tool/part/organ components | `GetVerbsEvent<ExamineVerb>`, `SurgeryToolExaminedEvent` | `SurgeryToolExamineSystem` (`Tools/SurgeryToolExamineSystem.cs:18-31`) |

**No proposed pair (S1–S7, V1–V5) appears above.** The two bolded rows are the components P4-3 *extends*;
their existing subscriptions are left in place and reached through a `_WF` partial, never re-subscribed.

One extra note: `OnToolCanPerform` relays `SurgeryCanPerformStepEvent` to the patient
(`Steps.cs:296`, `RaiseLocalEvent(args.Body, ref args)`), so a `SubscribeLocalEvent<TBodyComp, SurgeryCanPerformStepEvent>`
on a body component is possible — but the event carries **no `Part`**, so it cannot be used for a
per-limb gate. That is why §4.1's hook goes through `SurgeryValidEvent` instead.

### 6.2 Component registration-name collision audit

`RegisterComponent` derives the YAML name by stripping the `Component` suffix. Each proposed name was
grepped across `Content.Shared`, `Content.Server`, `Content.Client` (excluding `RobustToolbox`) for both
`class <Name>Component` and `class <Name>`; all returned **0**:

`WolfmedSurgeryWoundCondition`, `WolfmedSurgeryClampBleedingEffect`, `WolfmedSurgeryTreatWoundEffect`,
`WolfmedSurgeryFractureCondition`, `WolfmedSurgeryMendFractureEffect`, `WolfmedSurgeryOrganHealEffect`,
`WolfmedSurgeryOrganDamagedCondition`, `WolfmedSurgeryPainEffect`, `WolfmedWoundSurgerySystem`,
`WolfmedSurgeryConditionSystem`.

Existing `Wolfmed*` registered components, for reference (no overlap): `WolfmedOrgan`, `WolfmedBodyPart`,
`WolfmedBedHealMarker`.

**Deliberately NOT introduced** (Onyx names that would collide with Wolfgate's Shitmed components):
`SurgeryWoundedCondition`, `SurgeryTendWoundsEffect`. Also avoided out of caution even though they are
currently free, because they read as upstream Shitmed names and a future Monolith merge could introduce
them: `SurgeryHasWoundCondition`, `SurgeryClampBleedingEffect`, `SurgeryTreatWoundEffect`,
`SurgeryMendFractureEffect`, `SurgeryFractureGradeCondition`, `SurgeryOrganHealEffect`.

**New step/surgery prototype id collisions** — checked against
`Resources/Prototypes/_Shitmed/Entities/Surgery/*.yml` and the whole `Resources/Prototypes` tree:
`SurgeryStopBleeding`, `SurgeryStopInternalBleeding`, `SurgeryMendFracture`,
`SurgeryHealAmputationConsequence`, `SurgeryTendWoundsBruteDeep`, `SurgeryTendWoundsBurnDeep`,
`SurgeryHeal{Brain,Eyes,Heart,Lungs,Liver,Stomach,Kidneys}`, `SurgeryStepSutureBleeding`,
`SurgeryStepStopInternalBleeding`, `SurgeryStepSetBone`, `SurgeryStepMendFracture`,
`SurgeryStepHealAmputationConsequence`, `SurgeryStepHealOrganBase`, `SurgeryStepHeal<Organ>` — all free.
(Wolfgate's existing ids are `SurgeryTendWoundsBrute`/`Burn`, `SurgeryRemove*`/`Insert*` organ pairs,
`SurgeryAttach*`, `SurgeryOpenIncision`/`CloseIncision`/`OpenRibcage`/`RemovePart`/`InsertItem`/
`CorticalBorerRemoval` — surgeries.yml lines 2-596.)

### 6.3 Marked upstream edits P4-3 needs (4 sites)

| # | File:line | Change | Lines |
|---|---|---|---|
| HOOK A | `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs:128` | severity-window call in `OnWoundedValid` | 2 |
| E-1 | `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs:7` | 3 datafields | 6 |
| HOOK B | `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs:258` | stump gate in `OnPartRemovedConditionValid` | 2 |
| YAML | `Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml:295`, `:308` | `maxWoundSeverity: 99.99` on the two shallow tend surgeries | 2 |

Neither of the two Shitmed surgery C# files nor `SharedBodySystem.Parts.cs` carries a `// WOLFGATE` mark
today (grepped) — P4-3 is the first phase to touch them. Under the DECISIONS rule this raises the tracked
upstream-file count from 27 to **30** (the two Shitmed surgery files + one prototype file), or 31 if U-1
option B is chosen.

---

## 7. Decisions the user must make

**U-1 — How does `AmputationConsequenceWound` block re-attachment?** *(the P3-D2 gap)*
* **(A) Surgery-layer hook only — RECOMMENDED.** 2 marked lines in `OnPartRemovedConditionValid`. Hides all
  11 attach surgeries from the BUI *and* aborts the do-after. Mono prybar prosthetics, Mono bionic legs, the
  Goob autosurgeon and Shitmed's `GenerateChildPartSystem` keep working.
* (B) `CanAttachPart` guard, as DECISIONS §P4-3 literally says. 1 marked line, but silently breaks the four
  callers above and gives the medic no UI feedback.
* (C) Both. Most faithful to Onyx (which gates `TryAttachPart` *and* the surgery UI), but inherits (B)'s
  collateral damage.
  **Recommendation: (A).** It delivers the entire player-visible behaviour with less blast radius; (C) can
  be added later behind a `WolfmedBodyPartComponent` opt-out if prosthetics should also require treatment.

**U-2 — Do the two existing shallow tend surgeries get an upper severity bound?**
* **(A) Yes — RECOMMENDED.** Two marked YAML lines (`maxWoundSeverity: 99.99`). Matches Onyx; a badly
  wounded limb offers "Tend Deep …" instead of "Tend …", two entries, not four.
* (B) No. Zero upstream YAML edits, but the BUI shows four overlapping tend surgeries on a wounded limb.

**U-3 — Does bone setting get a distinct role?**
* **(A) Two-step ladder — RECOMMENDED, and what DECISIONS §P4-3 asks for.** `BoneSetter` →
  `FractureTreatment.Reduced`, then `BoneGel` → `Mended`. Uses the one shipped-but-referenced-nowhere
  Shitmed tool (`BoneSetterComponent`), so the `Bonesetter` item finally has a purpose. Both treatments
  already exist in the enum and `WoundFractureSystem.TrySetTreatment` already validates the order via
  `CanTreat` (`WoundFractureSystem.cs:111-127`).
* (B) One step, `BoneGel` → `Mended`, exactly Onyx. Simpler; leaves `BoneSetter` dead.

**U-4 — Does Wolfgate surgery leave scars?**
Today it never can: nothing creates a `SurgicalIncisionWound`, so `WoundScarSystem` (which is fully ported
and CVar-driven) never fires on surgery. Onyx creates one in its own open-incision step.
* **(A) Add a `WolfmedSurgeryIncisionWoundEffectComponent` to `SurgeryStepOpenIncisionScalpel` and
  `SurgeryStepCarefulIncisionScalpel` — RECOMMENDED (small).** ~20 lines + 2 marked YAML lines; makes
  `surgery.scar_chance` (0.35) live, and makes Onyx's `WoundSurgeryScarTest` portable (§8).
* (B) Leave it. Surgery stays scar-free; record as a deviation and drop the scar test.
  Note this also changes surgery's cost: an incision would then bleed as a wound
  (`SurgicalIncisionWound` has `WoundBleedingBehavior rate: 0.1, awakeMultiplier: 3`) **in addition to** the
  existing flat 10 `Bloodloss`, so (A) should *replace* that flat damage, not stack with it.

**U-5 — Surgery inflicts pain?** Onyx charges 12–34 pain per wound/organ step. Wolfgate has a live
`PainSystem` since phase 2 and no surgery pain at all. **Recommendation: yes, ~12 lines
(`WolfmedSurgeryPainEffectComponent` + V5), with Onyx's amounts**, so awake surgery on a wound host is
dangerous the way phase 2's pain shock intends. Cheap to defer if the package runs long.

**U-6 — Organ heal rate.** Onyx: `amount: 1`, `duration: 2` → up to 15 repeats (~30 s) per organ.
**Recommendation: `amount: 3`** (5 repeats, ~10 s), because Wolfgate's organ damage caps at
`MaxHealth × 0.3` per hit and phase 3 measured organ loss as a shift-long ratchet, not a firefight event —
a 30-second-per-organ repair loop is dead time, not tension. Record as a balance deviation from D4.

**U-7 — Kidneys.** Wolfgate has `OrganHumanKidneys` with full Wolfmed organ data but **no kidney surgery of
any kind**. `SurgeryHealKidneys` would be the fork's first. **Recommendation: ship it** (it is one prototype
pair) and note that `SurgeryRemoveKidneys`/`InsertKidneys` remain absent, so a destroyed kidney is still
unrecoverable.

---

## 8. Tests

### 8.1 Re-expressing Onyx's two tests

**`WoundSurgeryTest`** (`ONYX Content.IntegrationTests/Tests/_Onyx/Wounds/WoundSurgeryTest.cs`) has two cases:

* `SelectedPartAndHighestSeverityTest` (`:70-103`) — spawns three bare `Woundable` parts and two *singleton
  effect entities*, then raises the events **by hand**:
  ```csharp
  var ev = new SurgeryValidEvent(EntityUid.Invalid, part);        // :131
  entities.EventBus.RaiseLocalEvent(condition, ref ev);
  var ev = new SurgeryStepEvent(EntityUid.Invalid, EntityUid.Invalid, part, []);  // :138
  entities.EventBus.RaiseLocalEvent(effect, ref ev);
  ```
  Asserts: the condition passes on a part with a `SlashWound` and fails on an empty part; two clamp steps
  reduce the **highest-severity** wound's `WoundBleedingComponent.BleedingSeverity` 20 → 10 → gone; the
  other part's wound and the lower-severity wound on the same part are untouched.
* `FractureMendAndStaleNoOpTest` (`:106-127`) — 75 Blunt to an arm via
  `WoundDamageRoutingSystem.TryApplyPartDamage`, then the mend effect twice; `GetFracture(arm)` is null both
  times (second raise is a no-op, not an exception).

**Porting verdict: directly portable, one signature change.** Wolfgate's `SurgeryStepEvent` takes five
arguments, so the helper becomes
`new SurgeryStepEvent(EntityUid.Invalid, EntityUid.Invalid, part, [], EntityUid.Invalid)`. Everything else
maps: `SurgeryHasWoundCondition` → `WolfmedSurgeryWoundCondition`,
`SurgeryClampBleedingEffect` → `WolfmedSurgeryClampBleedingEffect`,
`SurgeryMendFractureEffect` → `WolfmedSurgeryMendFractureEffect`. The Onyx fixture's Nubody prototypes
(`InitialBody`, `TransplantCompatibility`, `partType: Chest`) must be rebuilt on a Shitmed body graph
exactly as phase 3 did for `AmputationConsequenceTest.cs:38-47` (`- type: body` + real human parts +
`- type: WolfmedBodyPart`). `WoundDamageRoutingSystem.TryApplyPartDamage(body, arm, Blunt(75))` is SAME.

**`WoundSurgeryScarTest`** (`ONYX …/WoundSurgeryScarTest.cs`) asserts: with
`CCVars.SurgeryScarChance = 0`, closing a `SurgicalIncisionWound` removes it and creates **no** scar; with
chance `1`, it creates exactly **one** scar, and a second close does not create a second
(`ScarCreatedForCurrentClosure` idempotence).

**Porting verdict: depends on U-4.** Wolfgate's scar path is already live and already covered by
`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundScarTest.cs` (phase 1) — but it fires on
`WoundStateChangedEvent`, not on a surgery step. If U-4(A) is taken, the test becomes: create a
`SurgicalIncisionWound` on a part, raise the close-incision step, assert removal + scar count, with
`surgery.scar_chance` pinned in a `try/finally` exactly as Onyx does (`:39-66`). If U-4(B), **drop the test**
and record it in the manifest next to the two already-skipped `AmputationConsequenceTest` cases.

### 8.2 New tests P4-3 should add (`Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedWoundSurgeryTest.cs`)

| id | Assertion |
|---|---|
| T-SURG-BLEED | Two `WolfmedSurgeryClampBleedingEffect` steps on a bleeding part drop `BleedingSeverity` 20 → 10 → component removed; the lower-severity wound on the same part and a wound on a different part are untouched. (Onyx `SelectedPartAndHighestSeverityTest`, re-expressed.) |
| T-SURG-FRACTURE | 75 Blunt → `GetFracture(arm)` non-null; `SurgeryStepSetBone` sets `Treatment == Reduced`; `SurgeryStepMendFracture` clears the fracture; a third raise is a no-op. (Onyx `FractureMendAndStaleNoOpTest` + U-3's ladder.) |
| T-SURG-INTERNAL | An `InternalBleedingWound` with `Severity > 0` makes `WolfmedSurgeryWoundCondition internalBleeding: true` valid; one `WolfmedSurgeryTreatWoundEffect internalBleeding: true` step removes it; the condition then cancels. |
| T-SURG-AMPUTATION | Sever a limb (reuse `WolfmedAmputationTest`'s fixture), assert the stump carries `AmputationConsequenceWound`; assert `SurgeryValidEvent` on the `SurgeryAttachLeftArm` singleton is **cancelled**; run the heal step; assert the wound is gone and the same `SurgeryValidEvent` now passes. **This is the direct replacement for Onyx's skipped `SurgicalHealRemovesConsequenceAndUnblocksTest`** (named at `AmputationConsequenceTest.cs:23-25`). |
| T-SURG-ORGAN | Damage a heart to `Health < MaxHealth` but `> 0`; `WolfmedSurgeryOrganDamagedCondition slot: heart` is valid; N heal steps restore `Health == MaxHealth`; the step's complete-check then cancels; a further step is a no-op (clamped by `SetHealth`). |
| T-SURG-WINDOW | A part with ≥100 Brute wound severity makes `SurgeryTendWoundsBruteDeep` valid and (with U-2) `SurgeryTendWoundsBrute` invalid; at 60 severity the reverse; on a **non-wound-host** both bounds are ignored and the existing behaviour is byte-identical (the D2 canary). |

All six are effect-level tests raising the events directly, in the style Onyx and phase 3 already use — no
BUI driving, no do-after, consistent with the "prefer logic tests" memory.

---

## 9. Ordered file list

1. `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryComponents.cs` — the 7 (+1 optional) components. **new, ~110 lines.**
2. `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryConditionSystem.cs` — S1–S7 + the shared `FindWound`/`GetGroupSeverity`/`TryFindOrgan` helpers (ports of `WoundSurgerySystem.cs:167-231` and `SharedSurgerySystem.Organs.cs:117-125`). **new, ~190 lines.**
3. `Content.Server/_WF/Wolfmed/Surgery/WolfmedWoundSurgerySystem.cs` — V1–V5. **new, ~110 lines.**
4. `Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs` — `partial class SharedSurgerySystem` in `Content.Shared._Shitmed.Medical.Surgery`, bodies for HOOK A and HOOK B. **new, ~45 lines.**
5. `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` — **E-1, marked, +6 lines.**
6. `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` — **HOOK A (`:128`) and HOOK B (`:258`), marked, +4 lines.**
7. `Resources/Prototypes/_WF/Wolfmed/Surgery/surgery_steps.yml` — 12 steps (+1 abstract base). **new, ~180 lines.**
8. `Resources/Prototypes/_WF/Wolfmed/Surgery/surgeries.yml` — 13 surgeries. **new, ~150 lines.**
9. `Resources/Prototypes/_Shitmed/Entities/Surgery/surgeries.yml` — **U-2, marked, +2 lines.**
10. `Resources/Locale/en-US/_WF/wolfmed/surgery-popup.ftl` — 12 keys. **new.**
11. `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedWoundSurgeryTest.cs` — T-SURG-*. **new, ~330 lines.**
12. `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` — **restore** the skipped `SurgicalHealRemovesConsequenceAndUnblocksTest` and update the `<remarks>` block that documents the skip (`:20-33`). **marked, +40 lines.**
13. `Docs/Wolfmed/WOLFMED_MANIFEST.md`, `Docs/Wolfmed/WOLFMED_STATUS.md` — append rows; strike the "AmputationConsequenceWound is inert" and "no organ healing path" gaps.

If U-4(A): add `WolfmedSurgeryIncisionWoundEffectComponent` to file 1, V6 to file 3, and two marked lines in
`Resources/Prototypes/_Shitmed/Entities/Surgery/surgery_steps.yml:22` and `:303`, plus
`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundSurgeryScarTest.cs` as a new file.

---

## 10. Hazards

1. **Check handlers must be shared.** `SurgeryBui.cs:281` runs `GetNextStep` → `IsStepComplete` client-side.
   A server-only `SurgeryStepCompleteCheckEvent` handler makes the client believe the step is done: no
   highlight, and `SurgeryRepeatableStep` rows look finished. Every S4–S7 handler must be in
   `Content.Shared`.
2. **`SurgeryValidEvent` on a step prototype does not hide the surgery.** `RefreshUI`
   (`SurgerySystem.cs:69-78`) raises it on the **surgery** singleton only. Onyx also attaches
   `SurgeryHasWoundCondition` to its `SurgeryStepClampWoundBleeding` step — copying that placement into
   Wolfgate produces a condition that only bites at do-after completion. Put visibility conditions on the
   surgery.
3. **Do not build on `SurgeryCompletedEvent`.** It is an empty struct that nothing raises
   (`Effects/Complete/SurgeryCompletedEvent.cs:7`; its only subscriber is commented out at
   `SharedSurgerySystem.cs:76`).
4. **`OnTendWoundsCheck` is body-scoped, not part-scoped** (`Steps.cs:156-162`): the repeat loop continues
   while *any* Brute/Burn remains on the **body**. Adding `SurgeryTendWoundsBruteDeep` inherits that: a medic
   tending one limb keeps repeating until the whole patient is clean. Pre-existing Shitmed behaviour; record
   it, and if the user objects it is a separate marked change to an upstream handler.
5. **`SurgeryRemovePart` will still amputate a stump's neighbour.** `SurgeryStepRemoveFeature` raises
   `AmputateAttemptEvent` (`Steps.cs:295-301`) which goes through `WolfmedBodySystem.TryDetachPart`'s path
   and `WolfmedBodyPartLifecycleSystem`. P4-3 adds no consequence wound there (surgical amputation is clean);
   confirm in T-SURG-AMPUTATION that a *surgically* removed limb can be re-attached, i.e. that only
   `AmputationSystem`'s traumatic path sets the block.
6. **`args.Repeat` and `SurgeryValidEvent` interact.** `OnTargetDoAfter` re-runs `IsSurgeryValid` on every
   repeat (`SharedSurgerySystem.cs:94`). A condition like `WolfmedSurgeryWoundCondition bleeding: true` will
   therefore abort the repeat loop the instant the last bleeder is clamped and log
   `"tried to start invalid surgery"` (`:98`). Onyx has the same shape; put the tight condition on the
   **step** (where it only gates completion) and the loose one on the **surgery**, or accept the warning.
7. **`WolfmedSurgeryOrganHealEffect` is useless on a destroyed organ.** `OrganHealthSystem.Update`
   (`:33-63`) deletes any organ at `Health <= 0` on the next tick. The surgery only saves a *dying* organ.
   Word the guidebook and the analyzer readout (P4-4) accordingly.
8. **`OrganHealthSystem` is in `Content.Server` with a `Content.Shared` namespace**
   (`Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:24`). A `Content.Shared` file that `using`s
   `Content.Shared._Onyx.Body.Systems` and takes `[Dependency] OrganHealthSystem` will compile-fail on the
   client. Keep V4 in `Content.Server`.
9. **Prediction.** Wounds replicate, so shared conditions agree across the wire, but `WoundSystem` mutators
   are `_net.IsServer`-gated (`WoundSystem.cs:284`, `:338`, `:356`, `:401`). A client that runs a step effect
   sees no change until the server state arrives — the same one-tick lag phase 1 accepted under D35. No new
   prediction work.
10. **`GetSingleton` caches by `EntProtoId` in a `Dictionary` cleared on `RoundRestartCleanupEvent`**
    (`SharedSurgerySystem.cs:53`, `:81-84`). Integration tests that spawn the effect prototype themselves
    (as Onyx's do) bypass the cache entirely — which is why those tests are round-restart-safe. Keep that
    pattern.
