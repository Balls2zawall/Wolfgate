# P4-4 — Diagnostics (health analyzer) — analyst report `analyzer`

Read-only analysis for DECISIONS.md **P4-4**. Every claim below was re-verified in the live tree at
`WG = C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(phases 1–3 committed, HEAD `6329d204e3`) and against `ONYX = C:/tmp/onyx` @ `2f5bab9`.
Line numbers are current as of this pass. The phase-1 reports `entityeffects-gap.md` and
`medical-extras.md` were checked; `hooks-a.md` §1/§2 and `hooks-b.md` §7/§9/S13 were re-verified and are
**correct** except where noted in §10.

---

## 0. Executive summary

| | |
|---|---|
| **Onyx and Wolfgate rebuilt the same file two different ways.** | Onyx replaced the flat `HealthAnalyzerScannedUserMessage` with a `HealthAnalyzerUiState` struct and a split `HealthAnalyzerControl`; Wolfgate/Shitmed kept the flat message and added a per-part selection round-trip (`HealthAnalyzerPartMessage`) plus a part doll. **Neither the Onyx message file, the Onyx window, nor the Onyx control can be vendored.** They must be re-expressed. |
| **The server half ports almost verbatim.** | `BuildWoundDiagnostics` needs 3 small `// WOLFGATE` edits; `BuildPartDamage` is redundant (Shitmed already ships the equivalent); `BuildOrganInfo` needs `Category` → `SlotId`; `BuildChemicalInfo` needs `MetabolitesSolutionName` → `ChemicalSolutionName`. |
| **The three `_Onyx/Medical` payload types port byte-identically** apart from `TargetBodyPart.Chest` → `Torso` (D9). All 11 new type names are collision-free. |
| **Zero new components, zero new directed subscription pairs.** Nothing here can crash the server at start. |
| **UI decision:** recommend **(b) PARALLEL**, implemented as a `_WF` control mounted by a 2-line XAML edit + 1-line code-behind call, with all logic in a `_WF` **partial of `HealthAnalyzerWindow`** (it is `sealed partial`). Upstream footprint: 3 lines client + ~8 lines server + 4 appended message fields. |
| **Crew monitor / suit sensors: confirmed NO wound data in Onyx.** The only Onyx markers there are `<Onyx-CommandTrackingImplant>`, an unrelated feature. |
| **Examine: confirmed Onyx has NO organ-damage examine line.** Phase 2 already shipped everything Onyx's examine has. Any organ examine would be a Wolfgate invention. |
| **Blocker:** none. One genuine user decision (§7) plus two sub-decisions (§7.2, §7.3). |

---

## 1. What Wolfgate's analyzer is today

### 1.1 Server — `WG/Content.Server/Medical/HealthAnalyzerSystem.cs`

```csharp
:31  public sealed partial class HealthAnalyzerSystem : EntitySystem      // sealed *partial* — _WF partials attach
:37  [Dependency] private SharedBodySystem _bodySystem = default!; // Shitmed Change
```

Subscriptions (`Initialize()`, `:46-56`):

| Pair | Line |
|---|---|
| `<HealthAnalyzerComponent, AfterInteractEvent>` | `:46` |
| `<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>` | `:47` |
| `<HealthAnalyzerComponent, EntGotInsertedIntoContainerMessage>` | `:48` |
| `<HealthAnalyzerComponent, ItemToggledEvent>` | `:49` |
| `<HealthAnalyzerComponent, DroppedEvent>` | `:50` |
| `<HealthAnalyzerComponent, HealthAnalyzerPartMessage>` | `:52-55` via `Subs.BuiEvents<HealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, …)` |

Public API (all Shitmed-extended with a trailing `EntityUid? part`):

```csharp
:180 public void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target, EntityUid? part = null)
:196 public void StopAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
:238 public void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode, EntityUid? part = null)
```

`UpdateScannedUser` (`:238-288`) is the single payload-assembly point. It gathers:

* `TemperatureComponent.CurrentTemperature` (`:248-249`)
* blood `FillFraction` + `bloodstream.BleedAmount > 0` (`:256-262`)
* **Shitmed:** `body = _bodySystem.GetBodyPartStatus(target)` when the target has `TargetingComponent` (`:264-268`) — a `Dictionary<TargetBodyPart, TargetIntegrity>`, i.e. an 8-bucket severity per part, *not* damage numbers
* `UnrevivableComponent` (`:270-271`), **Frontier** `UncloneableComponent` (`:273-274`)
* `part != null ? GetNetEntity(part) : null` (`:286`) — the *selected* part only

The Shitmed part round-trip (`:213-228`):

```csharp
private void OnHealthAnalyzerPartSelected(Entity<HealthAnalyzerComponent> healthAnalyzer, ref HealthAnalyzerPartMessage args)
{
    if (!TryGetEntity(args.Owner, out var owner)) return;
    if (args.BodyPart == null) { BeginAnalyzingEntity(healthAnalyzer, owner.Value, null); }
    else
    {
        var (targetType, targetSymmetry) = _bodySystem.ConvertTargetBodyPart(args.BodyPart.Value);
        if (_bodySystem.GetBodyChildrenOfType(owner.Value, targetType, symmetry: targetSymmetry) is { } part)
            BeginAnalyzingEntity(healthAnalyzer, owner.Value, part.FirstOrDefault().Id);
    }
}
```

`Update()` (`:77-86`) auto-resets the selection to whole-body when the selected part is deleted or detached.

**Other callers of the same message / key** (matters for any signature change):

* `WG/Content.Server/Medical/CryoPodSystem.cs:206-221` — a **second construction site**, passing `null` for the last five parameters. It does *not* call `UpdateScannedUser`.
* `WG/Content.Server/_Mono/CorticalBorer/CorticalBorerSystem.cs:276,291` — calls `BeginAnalyzingEntity`/`StopAnalyzingEntity` (2-arg overload).
* WG has **no** `CryoPodWindow.xaml` (`Content.Client/Medical/Cryogenics/` holds only `CryoPodSystem.cs`), so unlike Onyx there is no second client consumer of the analyzer UI. One fewer surface than Onyx.

### 1.2 Shared — `WG/Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`

```csharp
:9  [Serializable, NetSerializable]
:10 public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
:12     public readonly NetEntity? TargetEntity;
:13     public float Temperature;
:14     public float BloodLevel;
:15     public bool? ScanMode;
:16     public bool? Bleeding;
:17     public Dictionary<TargetBodyPart, TargetIntegrity>? Body;   // Shitmed Change
:18     public NetEntity? Part;                                     // Shitmed Change
:19     public bool? Unrevivable;
:20     public bool? Uncloneable;                                   // Frontier
:22     public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel,
:22         bool? scanMode, bool? bleeding, bool? unrevivable, bool? uncloneable,
:22         Dictionary<TargetBodyPart, TargetIntegrity>? body, NetEntity? part = null)

:37 [Serializable, NetSerializable]
:38 public sealed class HealthAnalyzerPartMessage(NetEntity? owner, TargetBodyPart? bodyPart) : BoundUserInterfaceMessage
:40     public readonly NetEntity? Owner = owner;
:41     public readonly TargetBodyPart? BodyPart = bodyPart;
```

`HealthAnalyzerUiKey.Key` is the only key (`HealthAnalyzerUiKey.cs:6-9`).

### 1.3 Client — BUI and window

`WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerBoundUserInterface.cs`

```csharp
:22 _window = this.CreateWindow<HealthAnalyzerWindow>();
:23 _window.OnBodyPartSelected += SendBodyPartMessage;             // Shitmed Change
:36 _window.Populate(cast);
:40 private void SendBodyPartMessage(TargetBodyPart? part, EntityUid target)
:40     => SendMessage(new HealthAnalyzerPartMessage(EntMan.GetNetEntity(target), part ?? null));
```

`WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` — **312 lines**, a `controls:FancyWindow`
with `SetHeight="650" SetWidth="350" Resizable="False"` (`:5-7`). Structure:

```
FancyWindow (350×650, fixed)
└ PanelContainer (outer bevel) → PanelContainer (inner bevel)
  └ BoxContainer Name="RootContainer" (Vertical)              :16-20
    ├ Label  Name="NoPatientDataText"                         :21-23
    ├ Button Name="ReturnButton"    "< Return" (Shitmed)      :29-34
    ├ BoxContainer Name="PatientDataContainer"                :37-40
    │  ├ TextureRect Name="NoDataTex"  (out-of-range icon)    :57-62
    │  ├ SpriteView  Name="SpriteView" Access="Public" 96×96  :63-67
    │  ├ PanelContainer Name="PartView" — the Shitmed doll    :68-203
    │  │   11 TextureButtons: Head/Chest/Groin/Left+RightArm/
    │  │   Hand/Leg/Foot, StyleClasses="TargetDollButton*"
    │  ├ RichTextLabel Name="NameLabel" / Label SpeciesLabel
    │  │   / Label PartNameLabel / Label ScanModeLabel        :209-225
    │  └ GridContainer(2): Status / Temperature / Blood /
    │      Total-Damage value labels                          :240-254
    ├ PanelContainer Name="AlertsDivider"                     :259-261
    ├ ScrollContainer → BoxContainer Name="AlertsContainer"   :271-282  (fixed SetHeight="62")
    └ ScrollContainer → BoxContainer Name="GroupsContainer"   :296-305  (VerticalExpand)
```

`WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml.cs`

```csharp
:32 [GenerateTypedNameReferences]
:33 public sealed partial class HealthAnalyzerWindow : FancyWindow      // sealed *partial*
:41 public event Action<TargetBodyPart?, EntityUid>? OnBodyPartSelected;   // Shitmed
:45 private readonly EntProtoId _bodyView = "AlertSpriteView";
:47 private readonly Dictionary<TargetBodyPart, TextureButton> _bodyPartControls;
:86 public void SetActiveBodyPart(TargetBodyPart part, TextureButton button)
:92     OnBodyPartSelected?.Invoke(part == TargetBodyPart.Groin ? TargetBodyPart.Torso : part, _target.Value);
:110 public void Populate(HealthAnalyzerScannedUserMessage msg)
:335 private EntityUid? SetupIcon(Dictionary<TargetBodyPart, TargetIntegrity>? body)
```

**Where per-part data comes from, exactly:**

1. **Per-part severity for the doll** — `msg.Body` (`Dictionary<TargetBodyPart, TargetIntegrity>`), consumed by
   `SetupIcon` (`:335-364`), which spawns an `AlertSpriteView` entity and stacks 11 sprite layers from
   `/Textures/_Shitmed/Interface/Targeting/Status/{part}.rsi` state `{part}_{(int)integrity}`.
   Those RSIs exist in WG: `groin, head, leftarm, leftfoot, lefthand, leftleg, rightarm, rightfoot,
   righthand, rightleg, torso` (11). `TargetIntegrity` runs `Healthy=0 … Disabled=8`
   (`WG/Content.Shared/_Shitmed/Targeting/TargetIntegrity.cs:2-13`).
2. **Damage numbers for the selected part** — **not from the message at all.** `:118` does
   `_entityManager.TryGetComponent<DamageableComponent>(isPart ? part : _target, out var damageable)`,
   i.e. the client reads the part entity's own networked `DamageableComponent`. `:183` prints
   `damageable.TotalDamage`; `:224-227` feeds `damageable.DamagePerGroup` / `damageable.Damage.DamageDict`
   into `DrawDiagnosticGroups` (`:243-287`). The message only carries the part's `NetEntity`.
3. **Everything else** (temperature, blood %, status, alerts) is message-driven.

**What a Wolfgate medic sees today:** a doll with 11 coloured severity silhouettes, a name/species line,
Status / Temperature / Blood Level / Total Damage, up to three alert lines (unrevivable, uncloneable,
"Patient is bleeding!"), and a damage-group breakdown for whichever part is selected (or the whole body).
**No wounds, no fractures, no bleeding rate, no pain, no organs, no chemicals.**

Locale: `WG/Resources/Locale/en-US/medical/components/health-analyzer-component.ftl` — 27 lines, includes
`health-analyzer-window-entity-bleeding-text` (`:19`, WG-only) and `health-analyzer-window-return-button-text`
(`:27`, Shitmed). It does **not** have Onyx's tab, organ, chemical, vital-damage, whole-body,
damage-part or wound-diagnostic keys.

---

## 2. What Onyx's analyzer is

### 2.1 Server — `ONYX Content.Server/Medical/HealthAnalyzerSystem.cs` (518 lines)

Onyx-tagged feature families: `<Onyx-HealthAnalyzer-StatusDoll>` (outer), `<Onyx-BodyScanner>`,
`<Onyx-VitalDamage>`, `<Onyx-HealthAnalyzerPain>`, `<Onyx-HealthAnalyzerOrgans-edited>`,
`<Onyx-HealthAnalyzerChemicals>`.

Extra dependencies (`:50-59`):

```csharp
[Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
[Dependency] private SharedBodySystem _body = default!;
[Dependency] private DamageableSystem _damageable = default!;
[Dependency] private WoundSystem _wounds = default!;
[Dependency] private PainSystem _pain = default!;                       // <Onyx-HealthAnalyzerPain>
[Dependency] private BodyPartFunctionalitySystem _functionality = default!;
[Dependency] private IPrototypeManager _prototypes = default!;
[Dependency] private MobThresholdSystem _mobThreshold = default!;       // <Onyx-VitalDamage>
```

Payload assembly is refactored into `public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)`
(`:259-313`), which appends:

```csharp
:302  BuildPartDamage(entity),
:303  BuildWoundDiagnostics(entity),
:305  vitalDamage,                     // _mobThreshold.CheckVitalDamage(entity, damageable) when HasComp<WoundHostComponent>
:308  BuildOrganInfo(entity),
:310  BuildChemicalInfo(entity, bloodstream)
```

The four builders (all `SurgeryTargetComponent`-gated, returning `null` for non-bodies):

* **`BuildPartDamage` (`:316-335`)** — `Dictionary<TargetBodyPart, DamageSpecifier>` over `_body.GetBodyChildren`,
  using `SharedTargetingSystem.TryConvert(bodyPart.PartType, bodyPart.Symmetry, out var target)` and
  `_damageable.GetAllDamage((part, damageable))`; `Chest` is copied to `Groin` (`:330-331`).
* **`BuildWoundDiagnostics` (`:337-440`)** — the heart of the feature. Per part, over `_wounds.GetWounds((part, woundable))`:
  * worst untreated fracture: `foundFracture.Treatment != FractureTreatment.Mended && foundFracture.Grade > fracture`
  * `bleedingRate += foundBleeding.CurrentRate`; `bleedingTreatment` from the **highest-rate** bleeding wound
  * `scarCount++` per `WoundScarComponent`
  * `internalBleedingRate += internalBleeding.Rate * internalBleeding.Severity.Float()` for **open** wounds
  * clotting phase per open bleeding wound: `AutomaticClottingAt != null` → `InProgress`; else
    `NaturalClotting > 0f && CurrentRate <= 0f` → `Complete`; else `None`. 0 phases → `NotApplicable`,
    1 → that one, >1 → `Mixed`.
  * visible wounds: skip unless `prototype.Visibility == WoundVisibility.Visible` and state is not
    `Healed`/`Scarred`; key is `(prototype.Name, GetStageDefinition(severity)?.Name)`, counted.
  * pain: `_pain.GetPain((part, painComponent))`
  * functionality: `_functionality.GetState((part, woundable))`
  * the part is emitted **only if `diagnostic.HasFindings`**.
* **`BuildOrganInfo` (`:443-475`)** — `List<HealthAnalyzerOrganInfo>` from `_body.GetBodyOrgans(body)`
  (`NetEntity`, `component.Health`, `component.MaxHealth`, `OrganOrder(component.Category?.Id)`), sorted by Order.
  `OrganOrder`: Brain 0, Eyes 1, Ears 2, Tongue 3, Lungs 4, Heart 5, Liver 6, Stomach 7, Appendix 8, Kidneys 9, else 10.
* **`BuildChemicalInfo` (`:479-515`)** — bloodstream solution + metabolites solution + every organ's stomach and
  lung solution, each as `HealthAnalyzerChemicalInfo(type, List<HealthAnalyzerReagentInfo>)`.

Onyx also renamed `MaxScanRange` to nullable + added `IsAnalyzerActive` and `PauseAnalyzingEntity`/
`ClearAnalyzedEntity` — **upstream Wizden drift, unrelated to wounds, out of scope.**

### 2.2 Shared payload types (port targets)

`ONYX Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` (48 lines):

```csharp
[Serializable, NetSerializable]
public readonly record struct HealthAnalyzerWoundDiagnostic(
    FractureGrade Fracture, FractureTreatment FractureTreatment,
    float BleedingRate, BleedingTreatment BleedingTreatment,
    ushort ScarCount, FixedPoint2 Pain,
    List<HealthAnalyzerVisibleWound> VisibleWounds,
    BodyPartFunctionalityState Functionality,
    float InternalBleedingRate, HealthAnalyzerClottingPhase ClottingPhase)
{
    public bool HasFindings => Fracture != FractureGrade.None || BleedingRate > 0f || ScarCount > 0 ||
        Pain > FixedPoint2.Zero || VisibleWounds.Count > 0 ||
        Functionality != BodyPartFunctionalityState.Functional || InternalBleedingRate > 0f;
}

[Serializable, NetSerializable]
public readonly record struct HealthAnalyzerVisibleWound(LocId Name, LocId? StageName, int Count);

[Serializable, NetSerializable]
public enum HealthAnalyzerClottingPhase : byte { NotApplicable, None, InProgress, Complete, Mixed }

[Serializable, NetSerializable]
public sealed class HealthAnalyzerWoundDiagnostics
{
    public readonly Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic> Parts;
    public HealthAnalyzerWoundDiagnostics(Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic> parts) { Parts = parts; }
}
```

`HealthAnalyzerOrganInfo.cs` (11 lines): `readonly record struct HealthAnalyzerOrganInfo(NetEntity Entity, FixedPoint2 Health, FixedPoint2 MaxHealth, int Order);`

`HealthAnalyzerChemicalInfo.cs` (20 lines): `enum HealthAnalyzerSolutionType : byte { Bloodstream, Metabolites, Stomach, Lung }`,
`readonly record struct HealthAnalyzerReagentInfo(string Prototype, FixedPoint2 Quantity)`,
`sealed record HealthAnalyzerChemicalInfo(HealthAnalyzerSolutionType Type, List<HealthAnalyzerReagentInfo> Reagents)`.

### 2.3 Onyx's message — **cannot be ported**

`ONYX Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` wraps everything in a
`HealthAnalyzerUiState` struct (`:29-85`) with **no** `Uncloneable`, **no** `Part`, and **no**
`Dictionary<TargetBodyPart, TargetIntegrity> Body`. Adopting it would mean rewriting
`CryoPodSystem.cs:206-221`, the Shitmed selection round-trip, `SetupIcon`, and the whole window.
**Verdict: extend WG's message, do not replace it.** (hooks-a.md §2's "either unify or append" framing is
correct; this report resolves it to *append*.)

### 2.4 Onyx client

* `HealthAnalyzerWindow.xaml` (22 lines) — a thin `FancyWindow` `MaxHeight="650" MinSize="790 600"` holding a
  3-button tab strip (`BodyButton` / `OrgansButton` / `ChemicalsButton`, `StyleClasses` `OpenRight`/`OpenBoth`/`OpenLeft`)
  over a single `ui:HealthAnalyzerControl Name="HealthAnalyzer"`.
* `HealthAnalyzerWindow.xaml.cs` (25 lines) — wires the three buttons to
  `HealthAnalyzer.SelectBodyTab()/SelectOrgansTab()/SelectChemicalsTab()` and forwards `Populate(msg.State)`.
* `HealthAnalyzerControl.xaml` (89 lines) — the real layout: a left column (`DiagnosticTabs`, MinWidth 220)
  holding `PatientDataContainer` + `BodyTab` / `OrgansTab` / `ChemicalsTab`, a `cc:VSeparator`, and a right
  column `WoundDiagnosticColumn` (MinWidth **430**) with `WoundDiagnosticStateLabel` + `WoundFindingsContainer`.
* `HealthAnalyzerControl.xaml.cs` (622 lines) — `Populate`, `SelectPart`, `DrawDamage`, `DrawWoundDiagnostics`,
  `DrawOrgans`, `DrawChemicals`, `DrawDiseases` (a separate `_Onyx/Disease` partial), `IsDangerousBloodLevel`
  (`DangerousBloodLevel = 0.65f`, `:38`, `:318`).
* `Content.Client/_Onyx/Targeting/UI/HealthAnalyzerStatusDoll.xaml(.cs)` — Onyx's own clickable doll
  (`UIWidget`, 11 `TextureRect` + 11 `TextureButton`), textures at `/Textures/_Onyx/Interface/Targeting/{Status,Doll}/`.
  **WG already has an equivalent doll inside `HealthAnalyzerWindow.xaml:68-203`. Do not port this.**
* `Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` (133 lines) — a single-line `Control` that
  truncates with `…`. Used only for organ-row names.

### 2.5 Exactly what an Onyx medic sees

**Left column, always:** sprite or clickable status doll, `Whole body` button, name, species, Scan Mode
(green ACTIVE / red INACTIVE), Status, Temperature (°C + K), Blood Level %.

**Body tab:** `Damage — {part}:` or `Total Damage:` + the value; `Vital damage:` + value (wound hosts only);
`Unrevivable` alert; damage groups with per-type breakdown and a 30×30 group icon; disease block.

**Organs tab:** one row per organ — 26×26 `SpriteView`, capitalised organ name (ellipsised), and
`{percent} %` health, ordered brain → eyes → ears → tongue → lungs → heart → liver → stomach → appendix → kidneys.
Organs with `MaxHealth == 0` are hidden. Rows are diffed in place (`_organRows`), not rebuilt.

**Chemicals tab:** one group per vessel (Bloodstream / Metabolites / Stomach / Lungs), each listing
`{reagent}: {quantity} u` sorted descending, or `No reagents detected`.

**Right column ("Status", always visible, 430 px):** a bulleted findings list, one bullet per part with findings,
in `SharedTargetingSystem.SelectableParts` order, formatted
`health-analyzer-wound-part-summary = { $part }: { $details }` with `details` joined by ` · `:

| Order | Detail | Source |
|---|---|---|
| 1 | visible wounds, e.g. `Laceration (deep) ×2`, comma-joined | `VisibleWounds` |
| 2 | `external bleeding` | `BleedingRate > 0` |
| 3 | `internal bleeding` | `InternalBleedingRate > 0` |
| 4 | `clotting in progress` / `bleeding stopped` / `partial hemostasis` | `ClottingPhase` |
| 5 | `scars: {count}` | `ScarCount > 0` |
| 6 | `pain: {pain}` | `Pain > 0` |
| 7 | `reduced function` / `function lost` / `part absent` | `Functionality != Functional` |

Plus, above the list, `The patient has a [color=red]dangerously low[/color] blood level.` when
`BloodLevel < 0.65`. When scan mode is off: `No contact with patient.`; when the target has no
`SurgeryTargetComponent`: `Diagnostics unavailable for this patient.`

**Note the asymmetry:** `FractureGrade`/`FractureTreatment` are carried in the payload and used by
`HasFindings`, but **Onyx's UI never prints them** — there is no `health-analyzer-wound-fracture*` call in
`HealthAnalyzerControl.xaml.cs` (the `health-analyzer-wound-fracture` / `-hairline` / `-bleeding-active` /
`-scar-single` / `-scar-multiple` / `*-genitive` keys at
`ONYX Resources/Locale/en-US/medical/components/health-analyzer-component.ftl:54-72` are **dead at the pin**).
A fractured part shows up as a bullet with no details unless it also bleeds/hurts. **This is an upstream Onyx
gap; Wolfgate should fix it** (see §7.3).

### 2.6 Locale inventory

`ONYX Resources/Locale/en-US/_Onyx/medical/health-analyzer-component.ftl` (16 lines) — 4 disease keys
(`:1-4`, out of scope) plus the 12 wound keys (`:5-16`):
`health-analyzer-wound-pain`, `-part-summary`, `-bleeding-short`, `-internal-bleeding-short`, `-scars-short`,
`-pain-short`, `-functionality-impaired`, `-functionality-disabled`, `-functionality-unavailable`,
`-clotting-inprogress`, `-clotting-complete`, `-clotting-mixed`.
(`health-analyzer-wound-pain` at `:5` is **unused** by the control — dead.)

The rest live in the upstream file `ONYX .../medical/components/health-analyzer-component.ftl`:
`:15` vital-damage, `:18-21` body/organs tab + organ health, `:24-32` chemicals tab + 4 solution names +
empty + reagent, `:46-47` whole-body + damage-part, `:51-53` wound-diagnostics title/inactive/unavailable,
`:70` blood-level-dangerous. **19 keys to add to WG.**

`ONYX Resources/Locale/en-US/_Onyx/targeting/targeting.ftl:20-32` holds `targeting-part-{head,chest,groin,
left-arm,left-hand,right-arm,right-hand,left-leg,left-foot,right-leg,right-foot}`. **WG has zero
`targeting-part-*` keys** (`grep -rn "targeting-part-" WG/Resources/Locale/en-US/` → 0 hits), so these 11
must be added too (with `chest` → `torso`, D9).

---

## 3. Symbol-by-symbol verification (Onyx → Wolfgate)

Compat layer (`Content.Shared/_WF/Wolfmed/Compat`) checked first in every row.

### 3.1 Server-side

| Onyx symbol | Status in WG | Evidence / substitution |
|---|---|---|
| `SurgeryTargetComponent` | **SAME** | `WG/Content.Shared/_Shitmed/Surgery/SurgeryTargetComponent.cs:6` |
| `WoundHostComponent` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:14-15` |
| `SharedBodySystem.GetBodyChildren(EntityUid)` | **SAME** | used at `WG/.../HealthExaminableSystem.PartStatus.cs:25` |
| `SharedBodySystem.GetBodyOrgans(body)` | **SAME** | `WG/Content.Shared/Body/Systems/SharedBodySystem.Body.cs:277` → `IEnumerable<(EntityUid Id, OrganComponent Component)>` |
| `SharedTargetingSystem.TryConvert(BodyPartType, BodyPartSymmetry, out TargetBodyPart)` | **MISSING** | Substitute `_body.GetTargetBodyPart(BodyPartType type, BodyPartSymmetry symmetry)` → `TargetBodyPart?` (`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:378-394`). Also `GetTargetBodyPart(BodyPartComponent)` at `:373`. |
| `SharedTargetingSystem.SelectableParts` | **MISSING** | Substitute `SharedTargetingSystem.GetValidParts()` → `TargetBodyPart[]` (`WG/Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs:7-25`). **10 entries; `Groin` is commented out at `:13`.** `IsSelectable` (added by phase-1 HOOK 5) is at `:28-29`. |
| `TargetBodyPart.Chest` | **MISSING / DIFFERENT** | WG's enum has `Torso = 1 << 1` and `Groin = 1 << 2` (`TargetBodyPart.cs:14-30`). D9: fold `Chest` → `Torso`; do **not** emit a Groin row (`ConvertTargetBodyPart` maps Groin → Torso at `:405`). |
| `DamageableSystem.GetAllDamage(Entity<DamageableComponent>)` | **MISSING on `DamageableSystem`; present on compat** | `WolfmedDamageableSystem.GetAllDamage(Entity<DamageableComponent?> ent)` (`WG/Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs:125`). Also `GetTotalDamage` `:134`, `GetPositiveDamage` `:89/:104`. **No `GetDamagePerGroup`** on the compat class — use `DamageableComponent.DamagePerGroup` or `DamageSpecifier.GetDamagePerGroup(IPrototypeManager)` (`WG/Content.Shared/Damage/DamageSpecifier.cs:298`). |
| `WoundSystem.GetWounds(Entity<WoundableComponent?>)` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundSystem.cs:155` → `IEnumerable<Entity<WoundComponent>>` |
| `WoundComponent.{Prototype,Severity,State}` | **SAME** | `WoundDamageComponents.cs:187,190,196` |
| `WoundState` | **SAME** (`Open,Stabilized,Closed,Healed,Scarred`) | `WoundDamageComponents.cs:262-269` |
| `WoundFractureComponent.{Grade,Treatment}` | **SAME** | `WoundDamageComponents.cs:246-256` |
| `FractureGrade` / `FractureTreatment` | **SAME**, both `[Serializable, NetSerializable]` | `:281-289` / `:291-297` |
| `WoundBleedingComponent.{CurrentRate,Treatment,NaturalClotting,AutomaticClottingAt}` | **SAME** | `:216, :222, :225, :231` |
| `BleedingTreatment` | **SAME**, NetSerializable | `:271-279` |
| `WoundScarComponent` | **SAME** | `:258-259` |
| `WoundInternalBleedingComponent.{Rate,Severity}` | **SAME** | `:236-242` |
| `WoundPrototype.{Name,Visibility,GetStageDefinition}` | **SAME** | `WoundPrototype.cs:19` (`LocId Name`), `:38` (`WoundVisibility Visibility`), `:76` |
| `WoundStageDefinition.Name` | **SAME** (`LocId`) | `WoundBehaviors.cs:132-135` |
| `PainSystem.GetPain(Entity<PainComponent?>)` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:165` |
| `PainComponent` on parts | **SAME** — `WoundDamageProjectionSystem.cs:224` `EnsureComp<PainComponent>(part)` | per-part pain is live |
| `BodyPartFunctionalitySystem.GetState(Entity<WoundableComponent?>)` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/BodyPartFunctionalitySystem.cs:18` |
| `BodyPartFunctionalityState` | **SAME**, NetSerializable | `WoundEvents.cs:88-103` (`Functional, Impaired, Disabled, Unavailable`) |
| `MobThresholdSystem.CheckVitalDamage(EntityUid, DamageableComponent)` | **SAME** | `WG/Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:25` (phase-1 HOOK 11) |
| `OrganComponent.Health` / `.MaxHealth` | **MISSING** on `OrganComponent` | Live on `WolfmedOrganComponent.Health/.MaxHealth` (`WG/Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs:13,16`), D8. |
| `OrganComponent.Category` (`ProtoId<OrganCategoryPrototype>?`) | **MISSING** (whole `OrganCategoryPrototype` is Nubody, not ported) | Substitute `OrganComponent.SlotId` (`WG/Content.Shared/Body/Organ/OrganComponent.cs:33-34`). WG's human organs already carry exactly the right slot ids: `brain, eyes, lungs, heart, stomach, liver, kidneys` (`WG/Resources/Prototypes/Body/Organs/human.yml:68,108,155,195,220,255,275`). `OrganOrder` becomes a lower-cased `SlotId` switch. |
| `OrganHealthSystem.{SetHealth,ChangeHealth}` | **SAME** (phase 3) | `WG/Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:65,80`, both `Entity<WolfmedOrganComponent>` |
| `BloodstreamComponent.MetabolitesSolutionName` | **MISSING** | Substitute `ChemicalSolutionName` (`WG/Content.Server/Body/Components/BloodstreamComponent.cs:151`, default `"chemicals"` `:18`). |
| `BloodstreamSystem.GetBloodLevel(entity)` | **DIFFERENT** | WG: `GetBloodLevelPercentage(EntityUid uid, BloodstreamComponent? component = null)` (`BloodstreamSystem.cs:346`). WG's analyzer already uses `bloodSolution.FillFraction` (`HealthAnalyzerSystem.cs:260`) — **keep it, change nothing**. |
| `StomachSystem.DefaultSolutionName` | **SAME** (`"stomach"`) | `WG/Content.Server/Body/Systems/StomachSystem.cs:16` |
| `LungComponent.SolutionName` | **SAME** | `WG/Content.Shared/Body/Components/LungComponent.cs:25` (note: **Shared** in WG, `Content.Server` in Onyx) |
| `Solution.Contents` / `ReagentQuantity.Reagent.Prototype` / `.Quantity` | **SAME** | `WG/Content.Shared/Chemistry/Components/Solution.cs:23`; `ReagentQuantity.cs:18`; `ReagentId.cs:18` |
| `TemperatureComponent.Temperature` | **DIFFERENT** | WG uses `CurrentTemperature` (`HealthAnalyzerSystem.cs:249`). Unchanged — the port does not touch this line. |
| `LocId` NetSerializable | **SAME** | `WG/RobustToolbox/Robust.Shared/Localization/LocId.cs:14-15` |

### 3.2 Client-side

| Onyx symbol | Status in WG | Evidence / substitution |
|---|---|---|
| `HumanoidProfileComponent` | **MISSING** | WG uses `HumanoidAppearanceComponent` + `_prototypes.Index<SpeciesPrototype>(c.Species)` (`HealthAnalyzerWindow.xaml.cs:160-164`). Unchanged. |
| `DamageableSystem.GetTotalDamage/GetDamagePerGroup/GetAllDamage` (client) | **MISSING on `DamageableSystem`** | Not needed: WG's window reads `damageable.TotalDamage` / `.DamagePerGroup` / `.Damage.DamageDict` directly (`:183,224,227`). |
| `DamageSpecifier.DamageDict` key type | **DIFFERENT** — `Dictionary<string, FixedPoint2>` (`DamageSpecifier.cs:44`), Onyx uses `ProtoId<DamageTypePrototype>` | Any ported loop must drop the `ProtoId<>` generic argument (same treatment as `HealthExaminableSystem.PartStatus.cs:28`'s marked comment). |
| `cc:VSeparator` (`Content.Client.Administration.UI.CustomControls`) | **SAME** | `WG/Content.Client/Administration/UI/CustomControls/VSeparator.cs:8` |
| `StyleClasses` `OpenRight`/`OpenBoth`/`OpenLeft` | **SAME** | `WG/Content.Client/Stylesheets/StyleBase.cs:21,23,24` |
| `StyleClasses="LabelHeading"` | **SAME** | `WG/Content.Client/Stylesheets/StyleBase.cs:15` |
| `/Textures/Objects/Devices/health_analyzer.rsi` state `metaphysical` (chemicals group icon) | **SAME** | `WG/Resources/Textures/Objects/Devices/health_analyzer.rsi/metaphysical.png` |
| `/Textures/_Onyx/Interface/Targeting/{Status,Doll}/**` | **MISSING** | Not needed — WG's doll + `/Textures/_Shitmed/Interface/Targeting/Status/*.rsi` (11 RSIs, verified) already serve. Do not port `HealthAnalyzerStatusDoll`. |
| `PartStatusSystem.GetSeverity(float)` | **SAME** (phase-1 compat) | `WG/Content.Shared/_WF/Wolfmed/Compat/PartStatusSeverity.cs:8-15`, in namespace `Content.Shared._Onyx.Targeting`. Only needed if the Onyx doll is ported — it is not. |
| `targeting-part-*` loc keys | **MISSING** | 11 keys to add (§2.6). |
| `chem-master-window-unknown-reagent-text` | **check at implementation**; WG has a ChemMaster UI, key expected present | fallback: add to the new `_WF` ftl |
| `EllipsisLabel` deps: `System.Text.StringBuilder`, `System.Text.Rune`, `string.EnumerateRunes()`, `Font.GetCharMetrics(Rune, float)` | **ALL sandbox-whitelisted** | `WG/RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml:896` (`Rune: All`), `:897` (`StringBuilder`), `:994` (`StringRuneEnumerator: All`), `:1509` (`System.Text.StringRuneEnumerator EnumerateRunes()`). `Font.GetCharMetrics` exists (`RobustToolbox/Robust.Client/Graphics/Font.cs`, used at `DrawingHandleScreen.cs:197`). Precedent in WG client code: `Content.Client/Administration/UI/Bwoink/BwoinkControl.xaml.cs:59,70`. Keep Onyx's `OopsConcat` trick (`HealthAnalyzerControl.xaml.cs:516-520`) if `Capitalize` is ported — it exists precisely to stop Roslyn emitting span code the sandbox rejects. |
| `FancyWindow` base | **DIFFERENT from DefaultWindow** — `public partial class FancyWindow : BaseWindow` (`WG/Content.Client/UserInterface/Controls/FancyWindow.xaml.cs:14`) | **Reserved names in this scope: `WindowTitle`, `HelpButton`, `CloseButton`, `ContentsContainer`** (`FancyWindow.xaml`). `WindowHeader`/`TitleLabel` are DefaultWindow's (`RobustToolbox/Robust.Client/UserInterface/CustomControls/DefaultWindow.xaml`) and would apply to a `DefaultWindow`-derived parallel window. Already-taken names in `HealthAnalyzerWindow.xaml`: `RootContainer, NoPatientDataText, ReturnButton, PatientDataContainer, NoDataTex, SpriteView, PartView, HeadButton, ChestButton, GroinButton, Left/RightArmButton, Left/RightHandButton, Left/RightLegButton, Left/RightFootButton, NameLabel, SpeciesLabel, PartNameLabel, ScanModeLabel, StatusLabel, TemperatureLabel, BloodLabel, DamageLabel, AlertsDivider, AlertsContainer, GroupsContainer`. |

### 3.3 New type names — collision check

`grep -rn "\bX\b" WG/Content.{Shared,Server,Client} --include=*.cs` → **0 hits** for every one of
`HealthAnalyzerWoundDiagnostic`, `HealthAnalyzerWoundDiagnostics`, `HealthAnalyzerVisibleWound`,
`HealthAnalyzerClottingPhase`, `HealthAnalyzerOrganInfo`, `HealthAnalyzerChemicalInfo`,
`HealthAnalyzerSolutionType`, `HealthAnalyzerReagentInfo`, `EllipsisLabel`, `HealthAnalyzerUiState`,
`WolfmedDiagnostic*`.

**Component registration names: P4-4 adds NO component.** Every new type is a `record struct`, `class`,
`enum` or `Control` — none carries `[RegisterComponent]`. There is therefore **no registration-collision
crash surface in this package at all**.

---

## 4. Subscription-pair audit

### 4.1 Pairs P4-4 registers

**Zero, in the GRAFT and in the recommended PARALLEL shape.** The server work lives entirely inside
`UpdateScannedUser` (already reached from the five existing handlers) and the existing
`OnHealthAnalyzerPartSelected`.

### 4.2 Pairs P4-4 must NOT register

| Pair | Existing owner | Consequence |
|---|---|---|
| `<HealthAnalyzerComponent, HealthAnalyzerPartMessage>` | `HealthAnalyzerSystem.cs:52-55` | server-start crash. Extend the existing handler. |
| `<HealthAnalyzerComponent, AfterInteractEvent / HealthAnalyzerDoAfterEvent / EntGotInsertedIntoContainerMessage / ItemToggledEvent / DroppedEvent>` | `HealthAnalyzerSystem.cs:46-50` | server-start crash. |
| `<WoundableComponent, PartDamageAppliedEvent>` | `OrganDamageSystem.cs:31` | server-start crash (phase-1 §5.2 / phase-3 §5.2 still binding). |
| `<BodyPartComponent, DamageChangedEvent>` | `SharedBodySystem.Targeting.cs:70` | server-start crash. Do not "push" analyzer refreshes from damage. |

### 4.3 If a *new* BUI message is added anyway

`Subs.BuiEvents<TComp>(key, subs => subs.Event<TEvent>(h))` expands to
`SubscribeLocalEvent<TComp, TEvent>(…)` with a UI-key filter
(`WG/RobustToolbox/Robust.Shared/GameObjects/Components/UserInterface/BoundUserInterfaceRegisterExt.cs:62-72`).
So a new message type `M` gives a **new, free** pair `<HealthAnalyzerComponent, M>` — no crash, and it may be
registered either from `HealthAnalyzerSystem`'s own `Initialize` or from a separate `_WF` system.
**But note `[Access(typeof(HealthAnalyzerSystem), typeof(CryoPodSystem))]` on `HealthAnalyzerComponent`
(`WG/Content.Server/Medical/Components/HealthAnalyzerComponent.cs:13`)** — a standalone `_WF` system cannot
touch its fields. Use a `_WF` **partial of `HealthAnalyzerSystem`** (precedent:
`WG/Content.Server/_WF/Wolfmed/Medical/HealingSystem.Wolfmed.cs:19-24`, `namespace Content.Server.Medical`,
`public sealed partial class HealingSystem`). **Recommendation: add no new message.** The existing one already
updates once per second and carries the target; four appended nullable fields are strictly cheaper.

### 4.4 Ordering

Nothing. No `before:`/`after:` anywhere in this package.

---

## 5. Message / BUI-state shape change

All four additions are `[Serializable, NetSerializable]`-clean (`FixedPoint2`, `LocId`, the four wound enums,
`TargetBodyPart`, `NetEntity` — all verified NetSerializable in §3).

```csharp
// WG/Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs
public HealthAnalyzerWoundDiagnostics? WoundDiagnostics;  // WOLFGATE: P4-4
public List<HealthAnalyzerOrganInfo>?   Organs;           // WOLFGATE: P4-4
public List<HealthAnalyzerChemicalInfo>? Chemicals;       // WOLFGATE: P4-4
public FixedPoint2?                     VitalDamage;      // WOLFGATE: P4-4
```

and four **appended optional** constructor parameters, after the existing `NetEntity? part = null`:

```csharp
public HealthAnalyzerScannedUserMessage(
    NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding,
    bool? unrevivable, bool? uncloneable, Dictionary<TargetBodyPart, TargetIntegrity>? body,
    NetEntity? part = null,
    HealthAnalyzerWoundDiagnostics? woundDiagnostics = null,   // WOLFGATE: P4-4
    List<HealthAnalyzerOrganInfo>? organs = null,              // WOLFGATE: P4-4
    List<HealthAnalyzerChemicalInfo>? chemicals = null,        // WOLFGATE: P4-4
    FixedPoint2? vitalDamage = null)                           // WOLFGATE: P4-4
```

**Why this is safe:** the only other construction site is `CryoPodSystem.cs:209-221`, which passes nine
positional arguments and keeps compiling unchanged (its four new fields stay `null`, and the cryo pod has no
client window in WG anyway). NetSerializer emits members in declaration order for both peers from the same
build, so appending fields is a clean wire change.

**Two `using`s needed** in the message file: `Content.Shared._Onyx.Medical;` and `Content.Shared.FixedPoint;`,
both marked `// WOLFGATE`.

**Rejected alternative — per-part diagnostics riding the selection round-trip.** It is technically possible to
send only `WoundDiagnostics.Parts[selectedPart]` and reuse `HealthAnalyzerPartMessage` to drive it, but it is
worse on every axis: a medic triaging wants the *overview* (which limb is bleeding), not one limb at a time;
`BuildWoundDiagnostics` already iterates every part, so the server saving is nil; and the whole dict for a
human is ~10 small structs, well under any PVS/bandwidth concern at a 1 Hz update. **Send the whole dict,
independent of the Shitmed part selection.**

**`BuildPartDamage` is NOT ported.** WG's client already gets exact per-part damage by reading the selected
part's networked `DamageableComponent` (`HealthAnalyzerWindow.xaml.cs:118,183,224-227`) and gets 11-part
severity from `msg.Body`. Porting `Dictionary<TargetBodyPart, DamageSpecifier>` would duplicate both.
(Consequence: Onyx's `HealthAnalyzerPartDamageTest.BuildsIsolatedPartSnapshotTest` has no target in WG —
see §9.)

---

## 6. Server-side port — the three builders, with exact edits

New file: **`WG/Content.Server/_WF/Wolfmed/Medical/HealthAnalyzerSystem.Wolfmed.cs`**,
`namespace Content.Server.Medical`, `public sealed partial class HealthAnalyzerSystem`.
It holds the extra `[Dependency]` fields and all three builders, so the upstream file gains **one line**.

Upstream hook (call it **HOOK 22**), one line, at `WG/Content.Server/Medical/HealthAnalyzerSystem.cs:276-287`
— the `ServerSendUiMessage` argument list gains four arguments:

```csharp
part != null ? GetNetEntity(part) : null,
BuildWoundDiagnostics(target), BuildOrganInfo(target), BuildChemicalInfo(target, bloodstream), BuildVitalDamage(target) // WOLFGATE: P4-4
```

### 6.1 `BuildWoundDiagnostics` — 3 marked edits from Onyx `:337-440`

| # | Onyx | Wolfgate |
|---|---|---|
| 1 | `if (!HasComp<SurgeryTargetComponent>(body)) return null;` | `if (!HasComp<WoundHostComponent>(body)) return null;` — **D2**: a Shitmed `SurgeryTarget` without `WoundHost` (a borg, a Protogen) has no `WoundableComponent` anywhere and would return an always-empty dict, which the client would render as "no findings" instead of "unavailable". `WoundHost` is the correct gate. |
| 2 | `SharedTargetingSystem.TryConvert(bodyPart.PartType, bodyPart.Symmetry, out var target)` | `_body.GetTargetBodyPart(bodyPart.PartType, bodyPart.Symmetry) is not { } target` (`SharedBodySystem.Targeting.cs:378`) |
| 3 | (implicit `Chest`) | D9: WG's mapping already yields `Torso`; **delete nothing, add nothing** — Onyx has no Chest→Groin copy in this builder (only in `BuildPartDamage`). |

Everything else — the fracture/bleeding/scar/internal-bleeding/clotting/visible-wound loop, `HasFindings`
gating, the `OrderBy(Name).ThenBy(StageName)` sort — ports **verbatim**.

### 6.2 `BuildOrganInfo` — rewritten (D8), ~20 lines

```csharp
private List<HealthAnalyzerOrganInfo>? BuildOrganInfo(EntityUid body)
{
    if (!HasComp<WoundHostComponent>(body)) return null;
    var result = new List<HealthAnalyzerOrganInfo>();
    foreach (var (organ, component) in _body.GetBodyOrgans(body))
    {
        if (!TryComp(organ, out WolfmedOrganComponent? health))   // WOLFGATE (D8): health lives here, not on OrganComponent
            continue;
        result.Add(new HealthAnalyzerOrganInfo(GetNetEntity(organ), health.Health, health.MaxHealth,
            OrganOrder(component.SlotId)));                        // WOLFGATE: Onyx's Category is Nubody; SlotId carries the same ids
    }
    result.Sort((l, r) => l.Order.CompareTo(r.Order));
    return result;
}

private static int OrganOrder(string slotId) => slotId.ToLowerInvariant() switch
{
    "brain" => 0, "eyes" => 1, "ears" => 2, "tongue" => 3, "lungs" => 4,
    "heart" => 5, "liver" => 6, "stomach" => 7, "appendix" => 8, "kidneys" => 9, _ => 10,
};
```

**Coverage note:** only the seven organs PROTO A annotated in phase 3 carry `WolfmedOrganComponent`
(`WG/Resources/Prototypes/_WF/Wolfmed/Body/organs.yml`, ids `WolfmedOrgan{Brain,Eyes,Lungs,Heart,Stomach,
Liver,Kidneys}`; `OrganHumanTongue`/`Appendix`/`Ears` were deliberately not edited, PLAN3 §3 PROTO A). So the
Organs tab lists 7 rows for a human and **is empty for non-human-lineage species** (P3-D7/§8.6-7). That is the
correct reading of what phase 3 shipped; record it as a known limitation. Onyx hides `MaxHealth == 0` rows;
the WG version instead hides organs with no `WolfmedOrganComponent`, which is the same idea.

### 6.3 `BuildChemicalInfo` — 1 marked edit, ~35 lines

Ports verbatim except `bloodstream.MetabolitesSolutionName` → `bloodstream.ChemicalSolutionName`
(`BloodstreamComponent.cs:151`), with `HealthAnalyzerSolutionType.Metabolites` kept as the wire value and the
locale string retitled ("Metabolites" → "Chemicals" is a one-word choice; recommend keeping Onyx's
`HealthAnalyzerSolutionType.Metabolites` name and localising it as **"Chemicals"** so the wire type does not
churn). `StomachSystem.DefaultSolutionName` and `LungComponent.SolutionName` are SAME; note `LungComponent`
lives in `Content.Shared.Body.Components` in WG, not `Content.Server`.

Gate: Onyx gates on `SurgeryTargetComponent`; WG should gate on **`BloodstreamComponent != null || has organs`**
— i.e. keep Onyx's shape but drop the `SurgeryTarget` requirement, so a non-wound-host mob with a bloodstream
still shows chemicals. (Chemicals are not a wound feature; D2 does not apply.) Cheap and strictly additive.

### 6.4 `BuildVitalDamage` — 5 lines

```csharp
private FixedPoint2? BuildVitalDamage(EntityUid body) =>
    HasComp<WoundHostComponent>(body) && TryComp(body, out DamageableComponent? damageable)
        ? _mobThreshold.CheckVitalDamage(body, damageable)
        : null;
```

`CheckVitalDamage` is already public in WG (`Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:25`).
This is the number that actually decides crit/death for wound hosts, so surfacing it fixes a real
phase-1..3 usability gap: today the analyzer's "Total Damage" is the *projection* sum, which diverges from
what kills the patient.

### 6.5 New `[Dependency]` fields (all in the `_WF` partial, zero in the upstream file)

```csharp
[Dependency] private WoundSystem _wounds = default!;
[Dependency] private PainSystem _pain = default!;
[Dependency] private BodyPartFunctionalitySystem _functionality = default!;
[Dependency] private MobThresholdSystem _mobThreshold = default!;
[Dependency] private IPrototypeManager _prototypes = default!;
```

`_bodySystem` (`:37`) and `_solutionContainerSystem` (`:39`) already exist upstream and are `private` fields of
the same partial class — reachable from the `_WF` partial.

---

## 7. The UI decision (P4-4's user decision)

### 7.1 The two options

#### (a) GRAFT — Wolfmed section inline in `HealthAnalyzerWindow.xaml`

*What it is:* add the wound-findings list, organ rows and chemical rows as new markup inside WG's existing
`HealthAnalyzerWindow.xaml`, with the `Populate` code paths added to `HealthAnalyzerWindow.xaml.cs` as marked
hooks whose bodies live in a `_WF` partial.

*Upstream footprint:* ~40–70 lines of new XAML inside `HealthAnalyzerWindow.xaml` (a tab strip, three
containers, a findings column) plus ~4 call lines in `HealthAnalyzerWindow.xaml.cs`. Every new `Name="…"`
enters the window's `[GenerateTypedNameReferences]` scope, which is where the reserved-name trap bites
(`WindowTitle`, `HelpButton`, `CloseButton`, `ContentsContainer` — plus the 27 names already used, §3.2).

*Layout problem:* WG's window is `SetWidth="350" SetHeight="650" Resizable="False"`
(`HealthAnalyzerWindow.xaml:5-7`) and is already vertically full — `AlertsContainer` is pinned at
`SetHeight="62"` and `GroupsContainer` is the only `VerticalExpand` region. Onyx's readout needs a 430 px
findings column plus a 220 px data column (`HealthAnalyzerControl.xaml:15,80`), i.e. its window is
`MinSize="790 600"`. Grafting means either a cramped 350 px column or **changing the window geometry
upstream**, which changes the analyzer for every non-wound-host scan too.

*Cost:* **medium-high.** ~1.5–2 days including the geometry rework. Every future Shitmed/upstream analyzer
change lands as a conflict in the middle of Wolfmed markup.

*Advantage:* one visual surface, no mounting indirection; per-part selection and wound findings sit in the
same column.

#### (b) PARALLEL — a `_WF` control mounted by one hook *(RECOMMENDED)*

*What it is:* a self-contained `Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml(.cs)` owning the
whole Wolfmed readout (findings list + Organs + Chemicals + Vital damage), mounted into the existing window by
**two XAML lines and one code line**, with all wiring in a `_WF` **partial of `HealthAnalyzerWindow`** —
legal because the class is `public sealed partial class HealthAnalyzerWindow : FancyWindow`
(`HealthAnalyzerWindow.xaml.cs:32-33`), so the partial can read the generated private fields
(`GroupsContainer`, `AlertsContainer`, …) that a standalone control cannot.

*Upstream diff, in full:*

```xml
<!-- HealthAnalyzerWindow.xaml, header -->
xmlns:wolfmed="clr-namespace:Content.Client._WF.Wolfmed.Medical"   <!-- WOLFGATE: P4-4 -->
<!-- inside RootContainer, after GroupsContainer's PanelContainer (:307) -->
<wolfmed:WolfmedDiagnosticPanel Name="WolfmedPanel" Visible="False" VerticalExpand="True" /> <!-- WOLFGATE: P4-4 -->
```

```csharp
// HealthAnalyzerWindow.xaml.cs, last statement of Populate (after :229)
PopulateWolfmed(msg); // WOLFGATE: P4-4 — body in _WF/Wolfmed/Medical/HealthAnalyzerWindow.Wolfmed.cs
```

`PopulateWolfmed` (in the `_WF` partial) does: hide the panel entirely when `msg.WoundDiagnostics == null &&
msg.Organs == null` (so non-wound-hosts see today's window byte-for-byte), otherwise feed the panel and, if
the tab-strip sub-decision is taken, flip `GroupsContainer`'s visibility.

*Cost:* **low-medium.** ~1 day. `WolfmedDiagnosticPanel.xaml.cs` is roughly 200 lines — Onyx's
`DrawWoundDiagnostics` (`:252-331`), `DrawOrgans` (`:413-511`) and `DrawChemicals` (`:357-409`) lifted almost
verbatim, plus `CreateDiagnosticGroupTitle`/`CreateDiagnosticItemLabel`/`GetTexture` (small, and duplicating
them beats making the upstream ones public).

*Advantages:* upstream footprint is 3 lines, all marked; zero reserved-name risk (the panel's names live in the
panel's own `[GenerateTypedNameReferences]` scope, and only `WolfmedPanel` enters the window's); a Shitmed
re-sync of `HealthAnalyzerWindow.xaml` is a 3-line re-apply; Onyx's control can be cited nearly line-for-line,
which makes a future Onyx re-sync a real diff; `EllipsisLabel` lands at its verbatim Onyx path
`Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` (D6).

*Disadvantage:* two visual idioms in one window unless the panel matches WG's bevelled `StyleBoxFlat` look —
a styling task, not a structural one.

### 7.2 Sub-decision — window geometry (needed under either option)

| Choice | Effect | Cost |
|---|---|---|
| **G1 — no geometry change (RECOMMENDED)** | Panel stacks below `GroupsContainer` inside the 350 px column. Findings lines wrap; roughly 3–4 lines per injured part. Add a `ScrollContainer` inside the panel so it never pushes the window. Non-wound-host scans look **identical to today**. | 0 upstream lines |
| **G2 — tab strip** | Add three buttons ("Damage" / "Wounds" / "Organs" / "Chemicals") above the bottom scroll area and swap `GroupsContainer` against the panel. Fits Onyx's mental model; uses the already-present `OpenRight`/`OpenBoth`/`OpenLeft` style classes. The `_WF` window partial can flip `GroupsContainer.Visible` because it is the same class. | ~6 upstream XAML lines (the buttons) or 0 if the buttons live inside `WolfmedDiagnosticPanel` and the partial does the swap |
| **G3 — widen** | `SetWidth="350"` → `"560"` (or `Resizable="True"` + `MinSize`). Two-column layout like Onyx. **Changes the analyzer for every scan in the game**, including non-wound-hosts — the same class of behaviour-leak as phase-2 HOOK 14(a)'s popup widening, which was accepted and recorded as deviation 16. | 1–2 upstream attribute edits, recorded as a deviation |

**Recommendation: G2 with the buttons inside the panel (0 extra upstream lines), falling back to G1.**
G3 only if the user wants the Onyx two-column look.

### 7.3 Sub-decision — print the fracture

Onyx carries `Fracture`/`FractureTreatment` in the payload and **never renders them** (§2.5). Wolfgate has
shipped fractures since phase 2 with a live alert, a movement penalty and a manipulation penalty — a medic who
cannot see the grade on the analyzer cannot decide between BoneGel and a splint. **Recommend adding two
details to the bullet list** (a corrected-upstream-gap deviation, same category as §8.2-1 and §8.2-3):

```
health-analyzer-wound-fracture-short = fracture: { $grade }
health-analyzer-wound-fracture-treated = fracture: { $grade } ({ $treatment })
```

driven off the existing `FractureGrade`/`FractureTreatment` enums, inserted between detail 1 (visible wounds)
and detail 2 (external bleeding). Zero server change — the data is already in the payload.

### 7.4 Recommendation, stated plainly

**Take (b) PARALLEL, with G2 (tabs inside the panel) and §7.3 (print the fracture).** It is the cheapest, the
smallest upstream diff, the safest against both the reserved-name trap and a Shitmed re-sync, and it is the
only shape in which Onyx's 622-line control can be cited nearly verbatim. Take (a) GRAFT only if the user
explicitly wants one seamless panel and accepts G3's geometry change plus the permanent conflict surface.

---

## 8. Ordered file list

**Group A — shared payload (no dependencies).**

| # | New / edited file | Source | Notes |
|---|---|---|---|
| A1 | `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` | ONYX same path | verbatim except `using Content.Shared._Shitmed.Targeting;` for `TargetBodyPart` (D10) — 1 `// WOLFGATE` line |
| A2 | `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerOrganInfo.cs` | ONYX same path | verbatim |
| A3 | `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerChemicalInfo.cs` | ONYX same path | verbatim |
| A4 | `WG/Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` | **upstream edit** | 4 fields + 4 optional ctor params + 2 `using`s, all `// WOLFGATE` (§5) |

**Group B — server (depends on A).**

| # | File | Notes |
|---|---|---|
| B1 | `WG/Content.Server/_WF/Wolfmed/Medical/HealthAnalyzerSystem.Wolfmed.cs` | **new**, `namespace Content.Server.Medical`, `sealed partial`: 5 `[Dependency]`s + `BuildWoundDiagnostics` + `BuildOrganInfo` + `BuildChemicalInfo` + `BuildVitalDamage` (§6) |
| B2 | `WG/Content.Server/Medical/HealthAnalyzerSystem.cs` | **upstream edit, HOOK 22** — one line: 4 extra arguments in the `ServerSendUiMessage` call at `:276-287` |

**Group C — locale (independent of B, needed before C/D render).**

| # | File | Notes |
|---|---|---|
| C1 | `WG/Resources/Locale/en-US/medical/components/health-analyzer-component.ftl` | **upstream edit**: 19 keys from ONYX `:15,18-21,24-32,46-47,51-53,70` (§2.6), each behind a `# WOLFGATE` block |
| C2 | `WG/Resources/Locale/en-US/_Onyx/medical/health-analyzer-component.ftl` | **new**, the 11 live wound keys from ONYX `:6-16` (skip the 4 disease keys and the dead `health-analyzer-wound-pain` at `:5`) + the 2 fracture keys from §7.3 |
| C3 | `WG/Resources/Locale/en-US/_Onyx/targeting/targeting.ftl` | **new**, the 11 `targeting-part-*` keys from ONYX `:20-32`, `chest` renamed `torso` (D9), `groin` omitted |

**Group D — client (depends on A + C).**

| # | File | Notes |
|---|---|---|
| D1 | `WG/Content.Client/_Onyx/Medical/HealthAnalyzer/EllipsisLabel.cs` | **new**, verbatim from ONYX same path (133 lines). Sandbox-clean (§3.2). Only needed if organ rows use it; a plain `Label` with `ClipText` is the fallback. |
| D2 | `WG/Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml` | **new**, ~40 lines: optional tab buttons + `WoundFindingsContainer` + `OrgansContainer` + `ChemicalsContainer` + `VitalDamage` labels, styled to match WG's bevel |
| D3 | `WG/Content.Client/_WF/Wolfmed/Medical/WolfmedDiagnosticPanel.xaml.cs` | **new**, ~200 lines: `DrawWoundDiagnostics` / `DrawOrgans` / `DrawChemicals` from ONYX `HealthAnalyzerControl.xaml.cs:252-331, 357-409, 413-520` |
| D4 | `WG/Content.Client/_WF/Wolfmed/Medical/HealthAnalyzerWindow.Wolfmed.cs` | **new**, `namespace Content.Client.HealthAnalyzer.UI`, `sealed partial class HealthAnalyzerWindow`: `PopulateWolfmed(msg)` body + the `GroupsContainer` visibility swap |
| D5 | `WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml` | **upstream edit**: 2 lines (`xmlns:wolfmed` + the `<wolfmed:WolfmedDiagnosticPanel Name="WolfmedPanel" …/>` element) |
| D6 | `WG/Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml.cs` | **upstream edit**: 1 line — `PopulateWolfmed(msg);` at the end of `Populate` (after `:229`) |

**Group E — tests (depends on B).** `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAnalyzerTest.cs` (§9).

**Total upstream footprint: 4 files, 8 marked lines** (A4's ~10 lines, B2's 1, C1's 19 locale lines, D5's 2,
D6's 1). Under GRAFT it would be 4 files and ~70 lines, 40+ of them XAML.

---

## 9. Headless test strategy (P4-8: "analyzer message contents")

Fixture: `GameTest` from `Content.IntegrationTests.Fixtures`, pattern copied from
`WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedOrganTest.cs:33-70`. **Everything worth asserting is
server-side and reachable without a client**, because the builders are public methods on
`HealthAnalyzerSystem`. No UI driving (memory: prefer logic tests when the user is present).

New file `WG/Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedAnalyzerTest.cs`,
`[TestOf(typeof(HealthAnalyzerSystem))]`:

| id | Test | Asserts |
|---|---|---|
| **T-AN-GATE** | `BuildWoundDiagnostics` / `BuildOrganInfo` on a plain `Damageable` entity and on a `SurgeryTarget`-without-`WoundHost` mob | both return `null` — the D2 gate (§6.1 edit 1) |
| **T-AN-FINDINGS** | spawn a human, `wounds.CreateOrMergeWound(head, "SlashWound", 10)`, `routing.TryApplyPartDamage(body, arm, Blunt 25)`, `wounds.CloseWound(wounds.CreateOrMergeWound(torso, "BluntWound", 20)!.Value)` (Onyx `HealthAnalyzerPartDamageTest.cs:133-137`, adapted) | `Parts[Head].BleedingRate > 0`; `Parts[LeftArm].Fracture == FractureGrade.Displaced` and `.FractureTreatment == FractureTreatment.None`; `Parts[Torso].ScarCount == 1`; **`Parts.ContainsKey(Groin) == false`** (D9 — WG never emits a Groin row); an uninjured part is absent (`HasFindings` gating) |
| **T-AN-CLEARS** | `fractures.TryMend(fracture.Owner)` then rebuild; then `graph.TryDetachPart(head)` then rebuild | `Parts` loses `LeftArm`, then loses `Head` — the "treatment shows up in the readout" contract, and the P4-3 surgery steps' acceptance criterion |
| **T-AN-CLOT** | a bleeding wound with `AutomaticClottingAt` set vs one with `NaturalClotting > 0 && CurrentRate <= 0` vs neither; then two of different phase on one part | `ClottingPhase` is `InProgress` / `Complete` / `None` / `Mixed` respectively (Onyx `:391-398` + `:415-420`) |
| **T-AN-PAIN** | apply pain to a part | `Parts[part].Pain` equals `PainSystem.GetPain((part, pain))` — the phase-2 pain numbers, now readable |
| **T-AN-ORGANS** | a human | exactly **7** rows; order is brain(0) → eyes(1) → lungs(4) → heart(5) → liver(6) → stomach(7) → kidneys(9); after `OrganHealthSystem.SetHealth(heart, 0)` the heart row reads `Health == 0` |
| **T-AN-ORGAN-HEAL** | `OrganHealthSystem.ChangeHealth(organ, +5)` (the P4-3 organ-healing path) | the row's `Health` rises and clamps at `MaxHealth` — the gate the status doc lists as a known gap |
| **T-AN-CHEM** | inject a reagent into the bloodstream, feed the stomach | a `Bloodstream` entry contains the reagent with the right quantity; a `Stomach` entry appears; a mob with no bloodstream yields an empty list, not `null` |
| **T-AN-VITAL** | damage a wound host, compare | `VitalDamage == _mobThreshold.CheckVitalDamage(body, damageable)` and it differs from `damageable.TotalDamage` once damage is routed to parts |
| **T-AN-MESSAGE** | call `UpdateScannedUser` against a real analyzer with an open UI and capture the sent message | the four new fields are non-null for a wound host and all four null for a non-host — the full round trip including HOOK 22 |

**Not portable from Onyx:**

* `HealthAnalyzerPartDamageTest.BuildsIsolatedPartSnapshotTest` (`ONYX :71-109`) — asserts `BuildPartDamage`,
  which WG does not port (§5). Drop it; the Groin-copy assertion at `:99` is actively wrong for WG (D9).
* `ClassifiesDangerousBloodLevel` (`ONYX :22-28`) — references the **client** type
  `HealthAnalyzerControl.IsDangerousBloodLevel`. Port the constant (`0.65f`) into
  `WolfmedDiagnosticPanel` as an `internal static bool IsDangerousBloodLevel(float)` and keep the three
  `TestCase`s only if `Content.IntegrationTests` already references `Content.Client` (it does — WG's
  integration fixture builds a client pair). Low value; optional.
* Onyx's `[TestPrototypes]` body uses `- type: InitialBody` with `organs: { Chest: …, Head: …, ArmLeft: … }`
  and `partType: Chest` — **all three are Nubody/Onyx-only**. Use WG's `- type: body` graph + `slots:` shape
  from `WolfmedOrganTest.cs:57-66`, and `partType: Torso`.
* `DamageSpecifier` literals must drop `ProtoId<DamageTypePrototype>` keys for WG's `string` dict
  (`DamageSpecifier.cs:44`).

---

## 10. Answers to the numbered questions, and re-verification of prior reports

**(4) Crew monitor / suit sensors — CONFIRMED: Onyx adds no wound data.**
`git -C C:/tmp/onyx grep -n "Onyx" HEAD -- Content.Server/Medical/CrewMonitoring/** Content.Shared/Medical/SuitSensors/**`
returns exactly three markers in `CrewMonitoringConsoleSystem.cs` (`:4, :73, :78`) and eight in the three
`SuitSensors` files (`SharedSuitSensor.cs:27,66`, `SharedSuitSensorSystem.cs:406,458,498,502`,
`SuitSensorComponent.cs:28,34`) — **all `<Onyx-CommandTrackingImplant>`**, a command-staff sensor filter with
no connection to wounds. `Content.Server/Medical/SuitSensors/SuitSensorSystem.cs` has no markers at all.
`hooks-b.md`'s headline row and items 7/9 are correct. **Nothing to port; leave both on PLAN §3's
"explicitly NOT touched" list.**

**(5) Organ-damage examine — CONFIRMED: Onyx has none.**
`git -C C:/tmp/onyx grep -ln "Organ" HEAD -- Content.Shared/_Onyx/HealthExaminable/** Content.Shared/HealthExaminable/**`
→ no files. No `ExaminedEvent` subscription exists in `Content.Server/_Onyx/Body/**`, `OrganHealthSystem.cs`
has no `Examine` reference, and `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl` has no organ key.
Onyx's only organ readout is the analyzer's Organs tab. **Phase 2 already shipped everything Onyx's examine
has; P4-4's "examine gains organ-damage status only if Onyx has it" resolves to NO.** If the user wants one
anyway it is a Wolfgate invention (~10 lines in the existing
`HealthExaminableSystem.PartStatus.cs` detail list, gated to self-examine or to a surgeon), and should be
recorded as such rather than as a port.

**Re-verification of `entityeffects-gap.md` and `medical-extras.md`:** both were re-read for analyzer content.
Neither makes an analyzer-specific claim that this report contradicts; `entityeffects-gap.md` is about
`HealthChange`/`EvenHealthChange` (P4-1, HOOK 9) and `medical-extras.md` about tourniquet/patch (P4-2). The
only analyzer statement in the phase-1 set is `hooks-a.md` §1/§2, re-verified above and correct, with one
refinement: hooks-a.md §1's "WG insertion point `:33–42`" for the dependency block is right, but this report
puts those dependencies in a `_WF` partial instead, so the upstream file's dependency block is **not** edited.
`hooks-b.md` S13's guess that "WG's version likely has the equivalent markup inlined in
`HealthAnalyzerWindow.xaml`" is **confirmed correct** — WG has no `HealthAnalyzerControl.xaml`, and the
equivalent markup is inline at `HealthAnalyzerWindow.xaml:8-311`.

---

## 11. Difficulty and risk

| Piece | Difficulty | Risk |
|---|---|---|
| A1–A3 payload types | **trivial** — 3 files, ~80 lines, 1 marked `using` | none; no components, no registrations |
| A4 message extension | **easy** — 10 lines | low. Only risk is forgetting `CryoPodSystem.cs:209` compiles (it does, all new params optional) |
| B1 `BuildWoundDiagnostics` | **easy** — verbatim + 2 marked edits | low. Runs once per second per active analyzer over every part's wound container; cost is O(parts × wounds), same as Onyx |
| B1 `BuildOrganInfo` | **easy** — rewritten, ~20 lines | low. Known limitation: 7 organs, human-lineage only (P3-D7) |
| B1 `BuildChemicalInfo` | **easy** — 1 marked edit | low |
| B2 HOOK 22 | **trivial** — 1 line | none |
| C1–C3 locale | **trivial** — 42 keys | YAML/FTL lint only |
| D1 `EllipsisLabel` | **trivial** — verbatim, sandbox-verified | none |
| D2–D4 the panel | **medium** — ~240 lines, the bulk of the package | medium: styling to match WG's bevel; the reserved-name trap (mitigated by the panel's own name scope); `FormattedMessage.FromMarkupPermissive` on loc-derived bullets |
| D5–D6 mounting | **trivial** — 3 lines | none under PARALLEL; **high conflict surface under GRAFT** |
| E tests | **easy-medium** — 10 tests, all server-side | low; the wound-creation helpers are already exercised by 6 existing `_Onyx/Wounds` test files |

**Overall: 1 to 1.5 days under (b), 2+ days under (a).** No blocker. The package is safely separable from
P4-1/P4-2/P4-3 and can run last in the phase-4 sequence; T-AN-CLEARS and T-AN-ORGAN-HEAL are the two tests
that *depend* on P4-3 having landed (they assert that a treated fracture and a healed organ show up in the
readout), so schedule P4-4 **after** P4-3 or mark those two `[Ignore]` until it lands.

---

## 12. Open items for the plan author

1. **§7.4 — (a) GRAFT vs (b) PARALLEL.** Recommend (b). *User decision.*
2. **§7.2 — window geometry G1 / G2 / G3.** Recommend G2 with the tab buttons inside the `_WF` panel (0 extra
   upstream lines), fallback G1. G3 is the only one that changes the analyzer for non-wound-hosts. *User decision.*
3. **§7.3 — print the fracture grade/treatment** that Onyx carries but never renders. Recommend YES, recorded
   as a corrected-upstream-gap deviation. *User decision.*
4. **§6.3 — `HealthAnalyzerSolutionType.Metabolites` label.** Recommend keeping the wire name and localising
   it as "Chemicals" to match WG's `ChemicalSolutionName`. *Plan-author call.*
5. **§10(5) — an organ examine line** would be a Wolfgate invention, not a port. Recommend NO for phase 4.
   *User decision if wanted.*
6. **Record as a limitation:** the Organs tab lists 7 human-lineage organs and is empty for every other
   species until phase 5 (P3-D7 / §8.6-7).
