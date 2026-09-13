# Wolfmed port analysis — `wounds-a`

**Scope:** dependency and API-gap analysis for four Onyx files under `C:/tmp/onyx/Content.Shared/_Onyx/Wounds/`:
`WoundDamageRoutingSystem.cs` (1015 lines), `WoundDamageComponents.cs` (304), `WoundDamageProjectionSystem.cs` (253), `WoundEvents.cs` (151).

**Onyx pin:** `2f5bab9` (sparse checkout at `C:/tmp/onyx`).
**Wolfgate worktree (`WG`):** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`, RobustToolbox 277 at `WG/RobustToolbox`.
Everything below was verified by reading the actual files. Nothing is inferred from upstream.

---

## 0. Executive summary — the three structural walls

Before the per-file tables, three findings dominate everything else. They are not "one symbol is missing"; they change how the port has to be built.

### 0.1 Wolfgate's `DamageableSystem` is the OLD damage model. There is no `DamageDealtEvent` and no `InjurableComponent`.

Onyx runs the *new* upstream damage pipeline, split across
`C:/tmp/onyx/Content.Shared/Damage/Systems/DamageableSystem.API.cs` and `DamageableSystem.Events.cs`:

```csharp
// onyx DamageableSystem.API.cs:120
public DamageSpecifier ChangeDamage(
    Entity<DamageableComponent?> ent, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)
{
    ...
    var evt = new DamageDealtEvent(damage, origin, interruptsDoAfters);
    RaiseLocalEvent(ent, ref evt);          // <-- damage is NOT written here
    return damage;
}

// onyx DamageableSystem.Events.cs:293
[ByRefEvent]
public readonly record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters);

// onyx DamageableSystem.Events.cs:25 + :218
SubscribeLocalEvent<InjurableComponent, DamageDealtEvent>(OnDamageDealt);
private void OnDamageDealt(Entity<InjurableComponent> ent, ref DamageDealtEvent args) { /* writes damageable.Damage */ }
```

The whole routing design depends on this: `WoundDamageRoutingSystem.OnDamageDealt`
(`WoundDamageRoutingSystem.cs:64-72`) subscribes `before: [typeof(DamageableSystem)]`, **clones the damage and then
clears `args.Damage.DamageDict`**, so `DamageableSystem`'s own `InjurableComponent` handler writes nothing to the mob.
The damage is then re-applied to a body part instead.

Wolfgate has none of this. `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:190`:

```csharp
public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool ignoreGlobalModifiers = false,
    float armorPenetration = 0f,
    // Shitmed Change
    bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null,
    // Mono: arg to ID indirect damage sources
    DamageOriginFlag? originFlag = null)
```

and it writes `damageable.Damage.DamageDict` inline (`DamageableSystem.cs:250-269`) then calls `DamageChanged(...)`.
`grep -rn "DamageDealtEvent" WG/Content.Shared WG/Content.Server WG/Content.Client` returns only
`HitscanDamageDealtEvent` (`Content.Shared/Weapons/Hitscan/Events/HitscanEvents.cs:85`) — unrelated.
`class InjurableComponent` does not exist anywhere in WG.

**Consequence:** the "cancel and re-route" half of the design (`BeforeDamageChangedEvent`) works in Wolfgate, but the
"intercept the applied damage before the component writes it" half (`DamageDealtEvent`) has no seam at all. See §1.3-A.

### 0.2 Wolfgate's `BodyPartType` has no `Chest` and no `Groin`; `HumanoidVisualLayers` has no `Groin`.

`WG/Content.Shared/Body/Part/BodyPartType.cs:10-20`:

```csharp
public enum BodyPartType
{
    Other = 0, Torso, Head, Arm, Hand, Leg, Foot, Tail
}
```

Onyx (`C:/tmp/onyx/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:15-27`):

```csharp
public enum BodyPartType : ushort
{
    Other = 0, Torso = 1, Head = 2, Arm = 3, Hand = 4, Leg = 5, Foot = 6, Tail = 7, Chest = 8, Groin = 9
}
```

Onyx split `Torso` into `Chest` + `Groin` (their `<Onyx-ChestGroin>` change). `WoundDamageComponents.cs` references
`BodyPartType.Chest` at lines 20, 47(via `TargetBodyPart.Chest`), 52 and `BodyPartType.Groin` at lines 21, 53;
`WoundDamageProjectionSystem.cs:239-240` maps both to `HumanoidVisualLayers.Chest` / `.Groin`;
`WoundDamageRoutingSystem.cs:481, 613, 883` use `TargetBodyPart.Chest` and `BodyPartType.Chest`.

`WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs:7-37` has `Chest` (line 15) but **no `Groin`**.
Onyx's has both (`Chest`, then `// <Onyx-ChestGroin>` `Groin`).

Wolfgate's Shitmed `TargetBodyPart` (`WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:12-31`) likewise uses
`Torso = 1 << 1` and `Groin = 1 << 2` — so it does have a `Groin` *target*, but no `Groin` *part type* and no `Groin`
*visual layer*.

### 0.3 `HealOnBuckleComponent` is server-only in Wolfgate, but the vendored shared file references it.

`WoundDamageRoutingSystem.cs:3` `using Content.Shared.Bed.Components;` and `:952`
`else if (origin is { } bed && HasComp<HealOnBuckleComponent>(bed))`.
Onyx: `Content.Shared/Bed/Components/HealOnBuckleComponent.cs`.
Wolfgate: `WG/Content.Server/Bed/Components/HealOnBuckleComponent.cs:3` → `namespace Content.Server.Bed.Components`.
`WG/Content.Shared/Bed/` contains only `Cryostorage`, `Sleep`, `StasisBedVisuals.cs`. A `Content.Shared` file
**cannot reference it at all**. This is a hard compile blocker, not a behaviour difference.

---

## 1. `WoundDamageRoutingSystem.cs`

### 1.1 Purpose and Onyx-internal dependencies

The routing system is the entry point of the entire wound model: it cancels damage aimed at a `WoundHostComponent`
mob, picks a body part (explicit request → attacker's targeting snapshot → active hand → weighted random), splits the
damage into *systemic* (stored on `SystemicDamageComponent`) and *localized* (applied to the part's own
`DamageableComponent`), and raises `PartDamageAppliedEvent` / `PartDamageOverflowedEvent` for the wound, fracture,
bleeding, pain and amputation systems to consume. It also exposes the public damage API the rest of the content calls
(`TryApplyDamage`, `TryApplyPartDamage`, `TryApplyDistributedDamage`, `TryApplyTargetedDamage`, `TryApplyCarrierDamage`,
`TryApplyLethalDamage`, `TryRoute*` variants).

Depends on these other Onyx files:

| Onyx file | What it needs |
|---|---|
| `_Onyx/Wounds/WoundDamageComponents.cs` | `WoundHostComponent`, `WoundableComponent`, `SystemicDamageComponent` |
| `_Onyx/Wounds/WoundEvents.cs` | `PartDamageAppliedEvent`, `PartDamageOverflowedEvent`, `PartDamageModifyEvent` |
| `_Onyx/Wounds/WoundDamageProjectionSystem.cs` | `_projection.RefreshBodyDamage(body)` |
| `_Onyx/Wounds/WoundPrototype.cs` | `BodyPartProfilePrototype` (`.AcceptedDamageTypes`, `.TreatmentCapabilities`, `.PassiveRecoveryMultiplier`, `.BedRecoveryMultiplier`), `TreatmentCapability` (`:203`) |
| `_Onyx/Wounds/PainSystem.cs` | `_pain.ApplyDamage(...)` |
| `_Onyx/Targeting/TargetResolverSystem.cs` | `TryResolve` ×2, `TryResolveAvailable`, `TryResolveExact`, `GetMatchingParts` |
| `_Onyx/Targeting/TargetingSnapshotComponent.cs` | `.RequestedTarget`, `.Shooter` |
| `_Onyx/Targeting/TargetBodyPart.cs`, `DamageDistribution.cs` | enums |
| `_Onyx/Body/Part/BodyPartComponent.cs` | `.MaxDamage`, `.AmputationThresholds`, `.Parent`, `.PartType`, `.Symmetry` |
| `_Onyx/Mobs/Systems/MobThresholdSystem.cs` | `CheckVitalDamage(EntityUid, DamageableComponent)` — **this file is outside `_Onyx/Wounds` and is a hard dependency of `TryApplyLethalDamage`** |

### 1.2 External symbol table

Legend: **SAME** = call-compatible in Wolfgate; **DIFFERENT** = exists but signature/semantics differ; **MISSING** = absent.

| # | Symbol | Namespace | Exact Onyx usage | Wolfgate status |
|---|---|---|---|---|
| R1 | `DamageableSystem` (type) | `Content.Shared.Damage.Systems` (Onyx) / `Content.Shared.Damage` (WG) | `[Dependency] private DamageableSystem _damage` | **SAME (type resolves)** — `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:25` `public sealed partial class DamageableSystem : EntitySystem`, namespace `Content.Shared.Damage` (`:23`). Onyx's `using Content.Shared.Damage.Systems;` still compiles because WG *does* have that namespace (e.g. `PassiveDamageSystem.cs`). |
| R2 | `DamageableSystem.ChangeDamage` | — | `_damage.ChangeDamage(body.Owner, damage, ignoreResistances, interruptsDoAfters, origin);` (`:621`)<br>Onyx sig: `public DamageSpecifier ChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)` (API.cs:120) | **MISSING** |
| R3 | `DamageableSystem.TryChangeDamage` (out-overload) | — | `_damage.TryChangeDamage(target, localized, out var appliedDamage, ignoreResistances: true, interruptsDoAfters: interruptsDoAfters, origin: origin, ignoreGlobalModifiers: true)` (`:704-710`, `:921-927`)<br>Onyx sig: `public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, out DamageSpecifier newDamage, bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)` (API.cs:93) | **DIFFERENT** — WG `DamageableSystem.cs:190`: `public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool ignoreGlobalModifiers = false, float armorPenetration = 0f, bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null, DamageOriginFlag? originFlag = null)`. No `out` param; returns the *delta*, not a bool; parameter **5 is `DamageableComponent?`, not `origin`**. See the overload trap in §1.3-T. |
| R4 | `DamageableSystem.GetAllDamage` | — | `_damage.GetAllDamage(body).Clone()` (`:158`, `:520`, `:534`, `:161`)<br>Onyx: `[Obsolete] public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent)` (API.cs:422) | **MISSING** |
| R5 | `DamageableSystem.GetPositiveDamage` | — | `_damage.GetPositiveDamage((part, partDamageable))` (`:373`, `:380`, `:734`, `:805`, `:814`, `:818`)<br>Onyx: `public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent)` (API.cs:327) | **MISSING** |
| R6 | `DamageableSystem.CanBeDamagedBy` | — | `_damage.CanBeDamagedBy(body, type)` (`:985`)<br>Onyx: `[Obsolete] public bool CanBeDamagedBy(Entity<InjurableComponent?> ent, ProtoId<DamageTypePrototype> type)` (API.cs:458) | **MISSING** (and `InjurableComponent` is missing too — see R7) |
| R7 | `InjurableComponent` | `Content.Shared.Damage.Components` | (reached transitively through `CanBeDamagedBy`; directly used by `WoundDamageProjectionSystem`) | **MISSING** — no `class InjurableComponent` in WG. Wolfgate stores the container id on `DamageableComponent.DamageContainerID` (`WG/Content.Shared/Damage/Components/DamageableComponent.cs:28`). |
| R8 | `BeforeDamageChangedEvent` | `Content.Shared.Damage` | `SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(...)`; handler reads `args.Damage`, `args.Origin`, sets `args.Cancelled` (`:55-62`, `:87-96`) | **DIFFERENT but source-compatible.** Onyx (`DamageableSystem.Events.cs:251`): `public record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, bool Cancelled = false);` — WG (`DamageableSystem.cs:470-476`): `public record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, TargetBodyPart? TargetPart = null, bool Cancelled = false, DamageOriginFlag? OriginFlag = null);`. All three members Onyx touches exist with the same names/types, and both are `[ByRefEvent]`. **Semantic difference:** WG raises it from inside `TryChangeDamage` *before* Shitmed's `TryChangePartDamageEvent` (raise at `DamageableSystem.cs:210-212`; the Shitmed event at `:217-222`), and it carries a Shitmed `TargetPart` that Onyx will silently ignore. |
| R9 | `DamageDealtEvent` | `Content.Shared.Damage.Systems` | `SubscribeLocalEvent<WoundHostComponent, DamageDealtEvent>(OnDamageDealt, before: [typeof(DamageableSystem)])` (`:51`); handler clears `args.Damage.DamageDict` (`:70`) | **MISSING** — see §0.1. |
| R10 | `SharedBodySystem` (type + DI) | `Content.Shared.Body.Systems` | `[Dependency] private SharedBodySystem _body` | **SAME** — WG `Content.Shared/Body/Systems/SharedBodySystem.cs:12` `public abstract partial class SharedBodySystem : EntitySystem`. It is *abstract*, but RT resolves base types: `WG/RobustToolbox/Robust.Shared/GameObjects/EntitySystemManager.cs:177` registers `foreach (var baseType in GetBaseTypes(type))`. WG itself does exactly this at `Content.Shared/Damage/Systems/DamageableSystem.cs:30`. |
| R11 | `SharedBodySystem.GetBodyChildren` | — | `_body.GetBodyChildren(body)` (`:423`, `:793`) | **SAME (call-compatible)** — WG `SharedBodySystem.Body.cs:256`: `public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid? id, BodyComponent? body = null, BodyPartComponent? rootPart = null)`. Onyx `_Onyx/Body/Systems/SharedBodySystem.cs:133`: `GetBodyChildren(EntityUid body)`. Tuple element names `Id`/`Component` match. |
| R12 | `SharedBodySystem.GetBodyChildrenOfType` | — | `_body.GetBodyChildrenOfType(body, BodyPartType.Hand)` (`:562`), reads `candidate.Component.Symmetry`, `candidate.Id` | **SAME (call-compatible)** — WG `SharedBodySystem.Parts.cs:991`: `GetBodyChildrenOfType(EntityUid bodyId, BodyPartType type, BodyComponent? body = null, BodyPartSymmetry? symmetry = null)` |
| R13 | `SharedBodySystem.BodyHasChild` | — | `_body.BodyHasChild(body, target)` (`:698`, `:1013`) | **SAME (call-compatible)** — WG `SharedBodySystem.Parts.cs:978`: `public bool BodyHasChild(EntityUid bodyId, EntityUid partId, BodyComponent? body = null, BodyPartComponent? part = null)` |
| R14 | `SharedHandsSystem.GetActiveHand` | `Content.Shared.Hands.EntitySystems` | `_hands.GetActiveHand((body, hands)) is not { } activeHand` then `_hands.TryGetHand((body, hands), activeHand, out var hand)` (`:549-550`)<br>Onyx sig: `public string? GetActiveHand(Entity<HandsComponent?> entity)` | **DIFFERENT** — WG `SharedHandsSystem.cs:177`: `public Hand? GetActiveHand(Entity<HandsComponent?> entity)`. Returns the hand object, not its id. |
| R15 | `SharedHandsSystem.TryGetHand` | same | `_hands.TryGetHand((body, hands), activeHand, out var hand)`; then `hand.Value.Location`<br>Onyx sig: `public bool TryGetHand(Entity<HandsComponent?> ent, [NotNullWhen(true)] string? handId, [NotNullWhen(true)] out Hand? hand)` | **DIFFERENT** — WG `SharedHandsSystem.cs:306`: `public bool TryGetHand(EntityUid handsUid, string handId, [NotNullWhen(true)] out Hand? hand, HandsComponent? hands = null)`. First param is `EntityUid` (an `Entity<HandsComponent?>` converts, so that part is fine) but `handId` is non-nullable. |
| R16 | `Hand` (type) | `Content.Shared.Hands.Components` | `hand.Value.Location` (`:553`) | **DIFFERENT — compile break.** WG `HandsComponent.cs:107`: `public sealed class Hand //TODO: This should definitely be a struct - Jezi`. It is a **class**, so `Hand?` is a plain nullable reference and `.Value` does not exist. Onyx's `Hand` is a struct. |
| R17 | `HandLocation.FunctionalLeft` / `.FunctionalRight` | `Content.Shared.Hands.Components` | `HandLocation.Left or HandLocation.FunctionalLeft => BodyPartSymmetry.Left` etc. (`:555-556`) | **MISSING** — WG `HandsComponent.cs:156-161`: `public enum HandLocation : byte { Left, Middle, Right }`. Onyx's (their `<Onyx-FunctionalHands>` change): `{ Right, Middle, Left, Functional, FunctionalRight, FunctionalLeft }`. Note the *ordering* also differs, which matters for any serialized value but not for named use. |
| R18 | `HandsComponent` | same | `TryComp(body, out HandsComponent? hands)` | **SAME** |
| R19 | `InventorySystem.RelayEvent` | `Content.Shared.Inventory` | `_inventory.RelayEvent((body, inventory), modify)` (`:681`) | **SAME** — WG `Content.Shared/Inventory/InventorySystem.Relay.cs:118`: `public void RelayEvent<T>(Entity<InventoryComponent> inventory, T args) where T : IInventoryRelayEvent` |
| R20 | `InventoryComponent` | `Content.Shared.Inventory` | `TryComp(body, out InventoryComponent? inventory)` | **SAME** |
| R21 | `TargetResolverSystem` | `Content.Shared._Onyx.Targeting` | 5 methods, see §1.1 | Onyx-internal — port with the wounds. Not in WG. |
| R22 | `TargetingSnapshotComponent` | `Content.Shared._Onyx.Targeting` | `snapshot.RequestedTarget`, `snapshot.Shooter` | Onyx-internal — port. |
| R23 | `TargetBodyPart` (Onyx) | `Content.Shared._Onyx.Targeting` | `TargetBodyPart.Chest` (`:481`, `:613`) | Onyx-internal — port verbatim. **Do not alias to WG's `Content.Shared._Shitmed.Targeting.TargetBodyPart`**: that one has `Torso = 1 << 1` where Onyx has `Chest = 1 << 1`, and lacks Onyx's `FullArms/FullLegs/BodyMiddle/FullLegsGroin/Vital` composites (`WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:12-31`). The two enums can coexist in different namespaces. |
| R24 | `DamageDistribution` | `Content.Shared._Onyx.Targeting` | `DamageDistribution.SplitEvenly` etc. | Onyx-internal — port. |
| R25 | `MobThresholdSystem` | `Content.Shared.Mobs.Systems` | `[Dependency] private MobThresholdSystem _mobThreshold` | **SAME** — WG `Content.Shared/Mobs/Systems/MobThresholdSystem.cs:12`: `public sealed partial class MobThresholdSystem : EntitySystem`. Being `partial` is what lets Onyx's own partial extension compile. |
| R26 | `MobThresholdSystem.CheckVitalDamage` | same | `_mobThreshold.CheckVitalDamage(body, damageable)` (`:466`)<br>Onyx def: `public FixedPoint2 CheckVitalDamage(EntityUid target, DamageableComponent damageableComponent)` in `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:22` | **MISSING in WG** — but it lives in an `_Onyx` file, so it ports with the rest. Its body needs `GetTotalDamage(Entity<DamageableComponent?>)` (also missing, §1.3-A) and `BodyPartType.Chest`/`.Groin` (§0.2). |
| R27 | `MobThresholdsComponent.Thresholds` | `Content.Shared.Mobs.Components` | `thresholds.Thresholds.Count`, `thresholds.Thresholds.Keys.Last()` (`:463`, `:466`) | **SAME** — WG `MobThresholdsComponent.cs:15`: `public SortedDictionary<FixedPoint2, MobState> Thresholds = new();` — identical to Onyx's `:15`. |
| R28 | `PoweredLightComponent` | `Content.Shared.Light.Components` | `HasComp<PoweredLightComponent>(light)` (`:607`) | **SAME** — WG `Content.Shared/Light/Components/PoweredLightComponent.cs:9` `namespace Content.Shared.Light.Components` (identical namespace in Onyx). |
| R29 | `DefibrillatorComponent` | `Content.Shared.Medical` | `HasComp<DefibrillatorComponent>(defibrillator)` (`:612`) | **SAME** — WG `Content.Shared/Medical/DefibrillatorComponent.cs:8` `namespace Content.Shared.Medical;` (identical in Onyx). |
| R30 | `HealOnBuckleComponent` | `Content.Shared.Bed.Components` | `HasComp<HealOnBuckleComponent>(bed)` (`:952`) | **MISSING from `Content.Shared`** — WG has it at `Content.Server/Bed/Components/HealOnBuckleComponent.cs:3`, `namespace Content.Server.Bed.Components`. **Hard blocker.** See §0.3. |
| R31 | `PassiveDamageComponent` | `Content.Shared.Damage.Components` | `HasComp<PassiveDamageComponent>(source)` (`:950`) | **SAME** — WG `Content.Shared/Damage/Components/PassiveDamageComponent.cs:6` `namespace Content.Shared.Damage.Components;` |
| R32 | `DamageableComponent` | `Content.Shared.Damage` (WG) / also reachable via `Content.Shared.Damage.Components` (Onyx) | `TryComp(part, out DamageableComponent? partDamageable)` | **SAME type, DIFFERENT namespace** — WG `Content.Shared/Damage/Components/DamageableComponent.cs:9`: `namespace Content.Shared.Damage`. Onyx: `Content.Shared.Damage.Components`. Because the vendored file has **both** `using Content.Shared.Damage;` and `using Content.Shared.Damage.Components;` (lines 4-5), the type still resolves. No action needed. |
| R33 | `DamageSpecifier.DamageDict` | `Content.Shared.Damage` | `damage.DamageDict[type]`, `foreach (var (type, amount) in damage.DamageDict)` (throughout) | **DIFFERENT, but source-compatible.** Onyx (`DamageSpecifier.cs:27`): `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>`. WG (`DamageSpecifier.cs:44`): `Dictionary<string, FixedPoint2>`. Loop variables become `string` instead of `ProtoId<DamageTypePrototype>`; the implicit conversions on `ProtoId<T>` (both directions) keep `HashSet<ProtoId<…>>.Contains(type)`, `CanPartReceiveDamage(part, type)` and dictionary indexing compiling. **Watch for:** anywhere a `var type` is passed to a generic that infers `string` (none in these four files). |
| R34 | `DamageSpecifier.Clone()` | `Content.Shared.Damage` | `args.Damage.Clone()` (`:69`), `_damage.GetAllDamage(body).Clone()` (`:158`, `:520`), `CompOrNull<SystemicDamageComponent>(body)?.Damage.Clone()` (`:374`), `damage.Clone()` (`:942`) | **MISSING** — WG's `DamageSpecifier` does not implement `IRobustCloneable<DamageSpecifier>` (Onyx: `DamageSpecifier.cs:21` `: IEquatable<DamageSpecifier>, IRobustCloneable<DamageSpecifier>`; WG `:20` `: IEquatable<DamageSpecifier>`). WG has the copy constructor `public DamageSpecifier(DamageSpecifier damageSpec)` at `DamageSpecifier.cs:94`. `IRobustCloneable<T>` does exist in the engine: `WG/RobustToolbox/Robust.Shared/Serialization/IRobustCloneable.cs:12`. |
| R35 | `DamageSpecifier.Empty`, `.GetTotal()`, `operator +`, `operator -`, ctor `(DamageSpecifier)` | `Content.Shared.Damage` | `localized.Empty`, `scaled.GetTotal()`, `new DamageSpecifier(damage)` | **SAME** — WG `DamageSpecifier.cs:83` (`Empty`), `:53` (`GetTotal`), `:401`/`:420` (`operator +`/`-`), `:94` (copy ctor). Both are `[DataDefinition, Serializable, NetSerializable]`. |
| R36 | `FixedPoint2` (`.Zero`, `.Min`, `.Max`, `.FromHundredths`, `.Float()`, `.Value`) | `Content.Shared.FixedPoint` | `FixedPoint2.FromHundredths(value)` (`:247`, `:823`), `shares[i].GetTotal().Float()` (`:888`), `amount.Value` (`:241`, `:811`) | **SAME** — WG `FixedPoint2.cs:51` `FromHundredths(int)`, `:186` `public readonly float Float()`, `:15` `public int Value { get; private set; }`, `:215`/`:220` `Min`/`Max`. |
| R37 | `IRobustRandom.NextFloat()` | `Robust.Shared.Random` | `_random.NextFloat()` (`:230`, `:439`, `:899`) | **SAME** (engine) |
| R38 | `INetManager.IsServer` | `Robust.Shared.Network` | `_net.IsServer` | **SAME** (engine) |
| R39 | `IPrototypeManager.TryIndex<T>(ProtoId<T>, out T?)` | `Robust.Shared.Prototypes` | `_prototypes.TryIndex(woundable.Profile, out var profile)` (`:870`, `:939`, `:973`) | **SAME** — `WG/RobustToolbox/Robust.Shared/Prototypes/IPrototypeManager.cs:273` |
| R40 | `EntitySystem.Subs` ordering args | `Robust.Shared.GameObjects` | `before: [typeof(DamageableSystem)]` (`:51`) | **SAME shape** — `WG/RobustToolbox/.../EntitySystem.Subscriptions.cs:192-224`: `SubscribeLocalEvent<TComp,TEvent>(handler, Type[]? before = null, Type[]? after = null)`. Unknown ordering types do **not** throw: `EntityEventBus.Ordering.cs:100` passes `allowMissing: true`. |
| R41 | `Entity<T>` → `EntityUid` implicit conversion | `Robust.Shared.GameObjects` | relied on throughout the shim design | **SAME** — `WG/RobustToolbox/Robust.Shared/GameObjects/Entity.cs:44` `public static implicit operator EntityUid(Entity<T> ent)`; `:39` `public static implicit operator Entity<T?>(EntityUid owner)`. Both directions exist — this is precisely what creates the overload trap in §1.3-T. |
| R42 | `TreatmentCapability` | `Content.Shared._Onyx.Wounds` | `IReadOnlySet<TreatmentCapability>` (`:45`, `:74`) | Onyx-internal (`WoundPrototype.cs:203`) — ports with the wounds. |

**No CCVars and no Loc keys are referenced by this file.** No prototypes are referenced by string id except through
`WoundHostComponent`'s data fields (`"DismembermentWound"`, `"AmputationConsequenceWound"`, damage type ids) which live
in `WoundDamageComponents.cs`.

### 1.3 Adaptations

#### A. The damage-API gap (R2–R7, R34) — **preferred: a compat partial class, not extension methods**

Wolfgate's `DamageableSystem` is declared `public sealed partial class DamageableSystem : EntitySystem`
(`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:25`). That is the single most useful fact in this report:
the entire missing new-style API can be added as **real instance methods in a second partial file**, which keeps all
four vendored Onyx files byte-identical and avoids extension-method resolution problems entirely.

Create `Content.Shared/_WF/Wolfmed/Compat/DamageableSystem.Wolfmed.cs`:

```csharp
namespace Content.Shared.Damage;

public sealed partial class DamageableSystem
{
    /// <summary>Onyx-shaped damage application: routes through the Wolfgate pipeline and returns the applied delta.</summary>
    public DamageSpecifier ChangeDamage(
        Entity<DamageableComponent?> ent,
        DamageSpecifier damage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        EntityUid? origin = null,
        bool ignoreGlobalModifiers = false);

    /// <summary>Onyx-shaped bool-returning overload with the applied delta.</summary>
    public bool TryChangeDamage(
        Entity<DamageableComponent?> ent,
        DamageSpecifier damage,
        out DamageSpecifier newDamage,
        bool ignoreResistances = false,
        bool interruptsDoAfters = true,
        EntityUid? origin = null,
        bool ignoreGlobalModifiers = false);

    /// <summary>Copy of the entity's damage, positive entries only.</summary>
    public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent);

    /// <summary>Copy of the entity's whole damage specifier.</summary>
    public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent);

    /// <summary>Total damage currently on the entity.</summary>
    public FixedPoint2 GetTotalDamage(Entity<DamageableComponent?> ent);

    /// <summary>Replaces the entity's damage wholesale, dropping types absent from <paramref name="damage"/>.</summary>
    public void SetDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage);

    /// <summary>Zeroes every damage type on the entity.</summary>
    public void ClearAllDamage(Entity<DamageableComponent?> ent);

    /// <summary>Whether the entity's damage container supports this damage type.</summary>
    public bool CanBeDamagedBy(Entity<DamageableComponent?> ent, ProtoId<DamageTypePrototype> type);
}
```

Implementation notes that must not be glossed over:

- `ChangeDamage` / `TryChangeDamage` must forward to the existing `TryChangeDamage(EntityUid?, …)` with
  `canSever: false, canEvade: false, partMultiplier: 1f, targetPart: null` so Shitmed's in-`DamageableSystem` spreading
  (`DamageableSystem.cs:217-222`, the `TryChangePartDamageEvent`) does not fire a second time for wound hosts — this is
  decision **D2** made concrete. `ChangeDamage` returns `delta ?? new DamageSpecifier()`.
- `SetDamage` must **not** be `damageable.Damage = damage;` (that is what WG's 3-arg `SetDamage` at
  `DamageableSystem.cs:143` does). Onyx's version (API.cs:38-54) *removes* types missing from the argument and *writes*
  the rest into the existing dict. Copy Onyx's loop or `WoundDamageProjectionSystem.RefreshBodyDamage` will slowly
  accumulate stale keys.
- `CanBeDamagedBy` should read `DamageableComponent.DamageContainerID` (`DamageableComponent.cs:28`) and index the
  `DamageContainerPrototype`, mirroring the seeding loop at `DamageableSystem.cs:103-121`. Note the parameter type
  differs from Onyx's `Entity<InjurableComponent?>`; the only call in these files is
  `_damage.CanBeDamagedBy(body, type)` with an `EntityUid`, so the implicit `EntityUid → Entity<DamageableComponent?>`
  conversion (`Entity.cs:39`) binds correctly.
- **Semantic trap inside `TryChangeDamage`:** WG skips damage types that are not already keyed in the dict
  (`DamageableSystem.cs:256-257`: `if (!dict.TryGetValue(type, out var oldValue)) continue;`), whereas Onyx's
  `OnDamageDealt` uses `dict.GetValueOrDefault(type)` and *adds* new keys (`DamageableSystem.Events.cs:233-239`).
  For body parts created by `EnsureComp<DamageableComponent>` with no `DamageContainerID` this is harmless (WG's
  `DamageableInit` seeds every damage type, `:124-128`), but for parts whose container is set to `"Biological"` it
  silently drops e.g. `Structural`. Document it in the manifest.

For `Clone()` (R34), a second tiny partial keeps every vendored file verbatim —
`Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Clone.cs`:

```csharp
namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    /// <summary>Onyx-compatible deep copy.</summary>
    public DamageSpecifier Clone() => new(this);
}
```

(`DamageSpecifier` is `public sealed partial class` at `WG/Content.Shared/Damage/DamageSpecifier.cs:20`, so this
compiles. Adding `IRobustCloneable<DamageSpecifier>` to the interface list is optional and not needed by these files.)

*Option (b), a `// WOLFGATE` edit in the vendored file:* replace each `X.Clone()` with `new DamageSpecifier(X)` and
each `_damage.GetPositiveDamage((p, d))` with a local helper. Roughly 14 edits across the two systems — rejected,
because it defeats re-sync.

*Option (c), an upstream hook:* add the methods directly to `DamageableSystem.cs` with `// WOLFGATE` markers. Rejected
under D5/D6 — the partial file in `_WF/Wolfmed/Compat` is the same code with zero upstream diff.

#### B. `DamageDealtEvent` (R9) — **no shim keeps the file verbatim; this needs a `// WOLFGATE` edit or an upstream hook**

There is no seam in Wolfgate between "damage has passed the modifiers" and "damage has been written to the component".
Three options, in preference order:

1. **Upstream hook (preferred here, exceptionally).** Add to `WG/Content.Shared/Damage/Systems/DamageableSystem.cs`,
   immediately after `damage = ApplyUniversalAllModifiers(damage);` (`DamageableSystem.cs:248`) and before the `var delta = …` (`:250`)
   block:

   ```csharp
   // WOLFGATE: Wolfmed damage routing seam, mirrors upstream DamageDealtEvent.
   var dealt = new DamageDealtEvent(damage, origin, interruptsDoAfters);
   RaiseLocalEvent(uid.Value, ref dealt);
   damage = dealt.Damage;
   if (damage.Empty)
       return damage;
   ```

   with the event declared in `Content.Shared/_WF/Wolfmed/Compat/DamageDealtEvent.cs`:

   ```csharp
   namespace Content.Shared.Damage.Systems;

   /// <summary>Raised after modifiers, before the damage is written. Wolfmed routing steals the damage here.</summary>
   [ByRefEvent]
   public record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters);
   ```

   **Deviation from Onyx that must be recorded:** Onyx's is `readonly record struct`; the vendored routing code mutates
   it (`args.Damage.DamageDict.Clear()` at `:70`). That mutation goes through the `DamageSpecifier` *reference*, so a
   `readonly` struct works — but WG's `TryChangeDamage` has to re-read `dealt.Damage` (the same object) after the raise.
   Keeping it non-readonly is harmless and more forgiving. Cost: 5 lines of upstream diff, within D5's "one- or two-line
   hooks" spirit but slightly over budget — flag it to the orchestrator.

2. **`// WOLFGATE` edit inside `WoundDamageRoutingSystem.cs`.** Replace the subscription at `:51` and the handler at
   `:64-72` with a subscription to Wolfgate's `TryChangePartDamageEvent` (`DamageableSystem.cs:481-491`, already raised
   for every damage on every entity and already cancellable via `args.Cancelled`). The handler becomes:

   ```csharp
   // WOLFGATE: Wolfgate has no DamageDealtEvent; Shitmed's TryChangePartDamageEvent is the equivalent seam.
   SubscribeLocalEvent<WoundHostComponent, TryChangePartDamageEvent>(OnDamageDealt);
   ...
   private void OnDamageDealt(Entity<WoundHostComponent> ent, ref TryChangePartDamageEvent args)
   {
       if (!_net.IsServer || !_routing.Contains(ent))
           return;

       var damage = args.Damage.Clone();
       args.Cancelled = true;                      // WOLFGATE: replaces DamageDict.Clear()
       RouteAppliedDamage(ent, damage, args.Origin, true);
   }
   ```

   Cheaper upstream, but it moves the interception point *before* resistances and `DamageModifyEvent` (armor) are
   applied — `TryChangePartDamageEvent` is raised at `DamageableSystem.cs:212`, armor's `DamageModifyEvent` at `:238`. That silently removes
   all armor from wound hosts. **Do not take this option without re-ordering.**

3. Rewrite `RouteThroughBodyModifiers` to not need the event at all (call the modifier pipeline manually). Largest
   divergence; not recommended.

#### C. Hands (R14–R17)

Option (a), a compat partial on `SharedHandsSystem` is *not* available in the same clean way: WG's
`SharedHandsSystem` is abstract with client/server subclasses, and adding an Onyx-shaped
`string? GetActiveHand(Entity<HandsComponent?>)` would collide with the existing `Hand? GetActiveHand(...)` on return
type alone (not a legal overload). So this one must be a **`// WOLFGATE` edit** inside the vendored file.

Replace `WoundDamageRoutingSystem.cs:545-572` body of `TryGetActiveHandPart` — exact replacement for lines 548-558:

```csharp
        // WOLFGATE: Wolfgate's GetActiveHand returns the Hand itself (a class), and HandLocation has no
        // Functional* members.
        if (!TryComp(body, out HandsComponent? hands) ||
            _hands.GetActiveHand((body, hands)) is not { } hand)
            return false;

        var symmetry = hand.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
```

(Everything from `if (symmetry == BodyPartSymmetry.None)` at `:559` down is unchanged.)
Behavioural note: Onyx's `Functional*` locations come from their prosthetic-hand feature; Wolfgate has no equivalent,
so dropping them loses nothing today.

#### D. `HealOnBuckleComponent` (R30) — **shim, option (a)**

Preferred: add a marker component in `Content.Shared/_WF/Wolfmed/Compat/` that the server-side bed system ensures, so
the vendored `HasComp<HealOnBuckleComponent>` test becomes a shared-safe one. But the *cheapest* shim that keeps the
file verbatim is to put a shared component with the exact Onyx name and namespace in the compat folder:

```csharp
// Content.Shared/_WF/Wolfmed/Compat/HealOnBuckleComponent.cs
namespace Content.Shared.Bed.Components;

/// <summary>Shared marker mirroring the server-only Content.Server.Bed.Components.HealOnBuckleComponent.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HealOnBuckleMarkerComponent : Component;
```

…except that the name must match for the vendored file to compile, and two `HealOnBuckleComponent` types in different
namespaces will make `Content.Server/Bed/BedSystem.cs` ambiguous the moment it gets `using Content.Shared.Bed.Components;`.
So the honest recommendation is:

- **(a) shim:** declare `Content.Shared.Bed.Components.HealOnBuckleMarkerComponent` as above, plus a one-line
  `// WOLFGATE` hook in `WG/Content.Server/Bed/BedSystem.cs` that `EnsureComp`s the marker alongside the server
  component, **and** a two-line `// WOLFGATE` edit in the vendored file:

  ```csharp
  // WOLFGATE: HealOnBuckleComponent is server-only in Wolfgate; a shared marker stands in.
  else if (origin is { } bed && HasComp<HealOnBuckleMarkerComponent>(bed))
  ```
  and delete `using Content.Shared.Bed.Components;` → replace with `using Content.Shared._WF.Wolfmed.Compat;`
  (2 changed lines).

- **(b)** move `HealOnBuckleComponent` to `Content.Shared/Bed/Components/` with a `// WOLFGATE` header. Cleaner for
  the vendored file (zero edits) but touches upstream file placement and `BedSystem`; per D5 the hook budget is one or
  two lines, and this is a file move. Offer it to the orchestrator as the alternative if verbatim matters more.

#### E. `BodyPartType.Chest` / `.Groin`, `HumanoidVisualLayers.Groin` (§0.2)

These are **enum member additions to upstream types**, so no shim is possible — a shim cannot add enum members.

- **(b) minimal `// WOLFGATE` edits to upstream enums** (preferred):
  `WG/Content.Shared/Body/Part/BodyPartType.cs:19` — append after `Tail`:
  ```csharp
        Tail,
        Chest, // WOLFGATE: Wolfmed splits Torso into Chest + Groin
        Groin, // WOLFGATE
  ```
  `WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs` — append after `Chest` (line 15):
  ```csharp
        Chest,
        Groin, // WOLFGATE: Wolfmed
  ```
  **Appending is mandatory, not stylistic:** `BodyPartType` is `[Serializable, NetSerializable]`
  (`BodyPartType.cs:9`) with implicit ordinals, and `HumanoidVisualLayers` is `: byte` with implicit ordinals — inserting
  in the middle renumbers every existing part and every existing sprite layer and will corrupt saved maps and every
  humanoid prototype that names a layer.
  Note Wolfgate ends up with `Chest = 8, Groin = 9` (after `Tail = 7`), coincidentally matching Onyx's explicit
  `Chest = 8, Groin = 9`. Worth asserting in a test.
- **(a) shim:** not possible.
- **(c) upstream hook:** this *is* the upstream hook; it is 2+1 lines, inside D5's budget.

Whether Phase-1 humans actually *use* `Chest`/`Groin` parts is a separate content decision (D3/D8): Wolfgate's human
prototypes use `Torso`. `WoundHostComponent.TargetWeights` (`WoundDamageComponents.cs:18-29`) has no `Torso` entry, so
a Wolfgate torso falls to the `GetValueOrDefault(…, 1f)` default weight and a `Torso` part hits `default: return false`
in `TryGetVisualLayer` (`WoundDamageProjectionSystem.cs:250`) — i.e. wounds work but the torso is under-weighted and
gets no damage overlay. Either add `[BodyPartType.Torso] = 2.5f` in a `_WF` prototype override of `WoundHostComponent`
(no code edit needed — it is a `[DataField]`), or split human bodies into Chest+Groin in phase 3.

#### F. `TryApplyLethalDamage` / `CheckVitalDamage` (R26)

Port `C:/tmp/onyx/Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` (47 lines) verbatim into
`Content.Shared/_Onyx/Mobs/Systems/`. It compiles against WG because `MobThresholdSystem` is `sealed partial`
(`WG/Content.Shared/Mobs/Systems/MobThresholdSystem.cs:12`) and `BodyComponent.RootContainer` exists
(`WG/Content.Shared/Body/Components/BodyComponent.cs:26`) — **but** WG's is `public ContainerSlot RootContainer = default!;`
(non-nullable) while Onyx's is `public ContainerSlot? RootContainer;` (`C:/tmp/onyx/Content.Shared/Body/BodyComponent.cs:27`).
The vendored line `body.RootContainer?.ContainedEntity is not { } rootPart` compiles either way (the `?.` on a
non-nullable reference is a warning at most), so no edit is needed. It also needs the new `GetTotalDamage` from §1.3-A
and the `Chest`/`Groin` enum members from §1.3-E.

#### T. **Overload-resolution traps — read this before writing any shim**

1. **`TryChangeDamage` is the dangerous one.** After the compat partial adds
   `bool TryChangeDamage(Entity<DamageableComponent?>, DamageSpecifier, out DamageSpecifier, …)` next to the existing
   `DamageSpecifier? TryChangeDamage(EntityUid?, DamageSpecifier, …, DamageableComponent? damageable = null, EntityUid? origin = null, …)`:
   - A call **with `out`** (both call sites in this file, `:704` and `:921`) can only bind to the new one. Safe.
   - A call **without `out` and with an `EntityUid` first argument** — e.g. anything in the *other* Onyx wound files
     written as `_damage.TryChangeDamage(uid, dmg, ignoreResistances: true, interruptsDoAfters: false, origin: o)` —
     binds to the **old** overload, because `EntityUid → EntityUid?` is a standard nullable conversion and
     `EntityUid → Entity<DamageableComponent?>` is a user-defined one, and standard beats user-defined in C#'s
     betterness rules. The old overload returns `DamageSpecifier?`, which is not implicitly `bool`, so
     `if (_damage.TryChangeDamage(...))` **fails to compile** (loud, good) — but a **bare statement call**
     `_damage.TryChangeDamage(...);` compiles silently and takes the Shitmed path, bypassing wound routing entirely.
     Note the parameter-name overlap is total: `ignoreResistances`, `interruptsDoAfters`, `origin`,
     `ignoreGlobalModifiers` all exist on the old signature with the same types.
   - **Mitigation:** after the compat layer lands, grep every vendored `_Onyx` file for
     `TryChangeDamage(` without a following `out ` and assert each one is intentional. Alternatively name the
     Onyx-shaped bool overload `TryChangeDamageEnt` and add a one-word `// WOLFGATE` edit at the two call sites —
     less elegant but removes the trap.
2. **Do not use extension methods for any of this.** C# only considers extension methods when no instance method is
   applicable. Because `Entity<DamageableComponent?>` implicitly converts to `EntityUid` (`Entity.cs:44`), the old
   instance `TryChangeDamage` *is* applicable for most Onyx call shapes, so an extension would simply never be chosen.
   The partial-class approach in §1.3-A does not have this problem.
3. **`SetDamage` is safe.** WG's is `SetDamage(EntityUid, DamageableComponent, DamageSpecifier)` — three required
   parameters, so the 2-argument Onyx call `_damage.SetDamage(body, total)` cannot bind to it.
4. **`GetActiveHand` cannot be overloaded** — differs only by return type; that is why §1.3-C uses a file edit.

### 1.4 Directed subscriptions registered

| Component | Event | Ordering | Conflict in Wolfgate? |
|---|---|---|---|
| `WoundHostComponent` | `BeforeDamageChangedEvent` | — | **None.** `grep -rn "SubscribeLocalEvent<.*BeforeDamageChangedEvent>"` over `Content.Shared/`, `Content.Server/`, `Content.Client/` returns exactly three, all on other components: `SharedGodmodeSystem.cs:19` (`GodmodeComponent`), `_Goobstation/ChronoLegionnaire/EntitySystems/SharedStasisSystem.cs:52` (`InsideStasisComponent`), `_Mono/ArmorPlate/SharedArmorPlateSystem.cs:46` (`ArmorPlateProtectedComponent`). `WoundHostComponent` does not exist in WG. |
| `WoundHostComponent` | `DamageDealtEvent` | `before: [typeof(DamageableSystem)]` | Event does not exist yet (§1.3-B). Once added, `DamageableSystem` will not subscribe to it in Wolfgate (the write happens inline), so the ordering constraint is inert — `allowMissing: true` at `EntityEventBus.Ordering.cs:100` means no crash, but **the `before:` gives you nothing**; ordering is instead guaranteed by where the raise sits in `TryChangeDamage`. |
| `WoundableComponent` | `BeforeDamageChangedEvent` | — | None (new component). |

RT throws `"Duplicate Subscriptions for comp={…}, event={…}"` at
`WG/RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Directed.cs:407` and `:419`, so any future clash surfaces at
server start, as the project memory notes.

### 1.5 Networking / prediction

`WoundDamageRoutingSystem` is nominally a **shared** system, but it is **not predicted**: every public entry point and
both event handlers bail on `!_net.IsServer` (`:57`, `:66`, `:106`, `:155`, `:201`, `:366`, `:460`, `:516`, `:847`).
Only `OnBeforePartDamageChanged` (`:87`) and the pure helpers run on the client. In practice it is a server system that
happens to live in `Content.Shared` so that types and the public API are visible to shared callers. **That is good news
for Wolfgate:** the server-only-ness of `BloodstreamSystem`, `HealingSystem` and `BedSystem` in Wolfgate costs nothing
here.

Server-only dependencies in Wolfgate that this file reaches:

- `HealOnBuckleComponent` — server-only **type**, which is a *compile* problem, not a runtime one (§0.3, §1.3-D).
- `PainSystem._pain.ApplyDamage` — Onyx's `PainSystem` is shared and uses `Content.Shared.StatusEffectNew`
  (`PainSystem.cs:11`), which Wolfgate does not have (decision **D1**: port `StatusEffectNew` verbatim). Nothing in
  the routing file itself touches statuses.
- `WoundDamageProjectionSystem.RefreshBodyDamage` — server-gated (`:145`), fine.

Client-visible state produced here is entirely component state: `SystemicDamageComponent.Damage` and
`WoundableComponent.AmputationOverflow` (`Dirty` at `:740`, `:778`, `:1004`). `SystemicDamageComponent` is
`[NetworkedComponent, AutoGenerateComponentState]` with an `[AutoNetworkedField] DamageSpecifier` — `DamageSpecifier` is
`[Serializable, NetSerializable]` in Wolfgate too (`DamageSpecifier.cs:19`), so that networks fine.
`WoundableComponent.AmputationOverflow` is `[ViewVariables]` only and is **not** networked in Onyx either — the client
will not see amputation pressure. Not a Wolfgate regression; just note it.

---

## 2. `WoundDamageComponents.cs`

### 2.1 Purpose and Onyx-internal dependencies

Pure data: the component definitions for the whole wound model — the host (`WoundHostComponent`: part-targeting weights,
which damage types localize, dismemberment thresholds), the per-part state (`WoundableComponent`,
`BodyPartFunctionalityComponent`, `PainComponent`), the wound entity itself (`WoundComponent` plus the
bleeding/internal-bleeding/fracture/scar facets), the body-wide `SystemicDamageComponent` and
`PartDamageVisualsComponent`, and the four enums (`WoundState`, `BleedingTreatment`, `FractureGrade`,
`FractureTreatment`). It declares no systems and no logic.

Depends on `_Onyx/Wounds/WoundPrototype.cs` (`WoundPrototype`, `BodyPartProfilePrototype`),
`_Onyx/Wounds/WoundEvents.cs` (`BodyPartFunctionalityState`) and `_Onyx/Targeting/TargetBodyPart.cs`.

### 2.2 External symbol table

| # | Symbol | Namespace | Exact Onyx usage | Wolfgate status |
|---|---|---|---|---|
| C1 | `BodyPartType` | `Content.Shared.Body.Part` | `Dictionary<BodyPartType, float> TargetWeights` (`:18`) etc. | **SAME type, DIFFERENT members** — see §0.2 / §1.3-E. |
| C2 | `BodyPartType.Chest` | same | `[BodyPartType.Chest] = 2.5f` (`:20`) | **MISSING** |
| C3 | `BodyPartType.Groin` | same | `[BodyPartType.Groin] = 1.5f` (`:21`), `= 160` (`:53`) | **MISSING** |
| C4 | `BodyPartType.{Head,Arm,Hand,Leg,Foot,Other,Tail}` | same | `:22-28`, `:54-57`, `:81`, `:84`, `:88-91` | **SAME** — `WG/Content.Shared/Body/Part/BodyPartType.cs:12-19` |
| C5 | `DamageSpecifier` | `Content.Shared.Damage` | `Dictionary<HumanoidVisualLayers, DamageSpecifier> Damage` (`:99`), `DamageSpecifier Damage = new()` (`:303`) | **SAME** — and `[Serializable, NetSerializable]` in both, so `[AutoNetworkedField]` works. |
| C6 | `DamageTypePrototype` | `Content.Shared.Damage.Prototypes` | `HashSet<ProtoId<DamageTypePrototype>> LocalizedDamageTypes` (`:35`), `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>` (`:73`), `Dictionary<ProtoId<DamageTypePrototype>, float> DamageMultipliers` (`:119`) | **SAME** — `WG/Content.Shared/Damage/Prototypes/DamageTypePrototype.cs:3` |
| C7 | Damage type ids `"Blunt" "Slash" "Piercing" "Heat" "Cold" "Shock" "Caustic" "Cellular" "Radiation" "Poison"` | prototypes | `:37-43`, `:75-77`, `:121-130` | **Verify at YAML lint.** These are `ProtoId<DamageTypePrototype>` string literals; all ten exist in standard SS14 damage prototypes, but confirm against `WG/Resources/Prototypes/Damage/` during the port. Not verified in this pass. |
| C8 | `FixedPoint2` | `Content.Shared.FixedPoint` | `FixedPoint2.New(1f / 9f)` (`:134`), implicit int→FixedPoint2 (`:52-57`, `:61`, `:137`, `:161` via ProtoId) | **SAME** |
| C9 | `HumanoidVisualLayers` | `Content.Shared.Humanoid` | `Dictionary<HumanoidVisualLayers, DamageSpecifier>` (`:99`) | **SAME type** (`WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs:7`), member gap only shows up in the projection system. |
| C10 | `TargetBodyPart` (Onyx) | `Content.Shared._Onyx.Targeting` | `public TargetBodyPart SystemicPainTarget = TargetBodyPart.Chest;` (`:47`) | Onyx-internal — port. Do **not** alias to `_Shitmed`'s (`Torso`, not `Chest`). |
| C11 | `Container` | `Robust.Shared.Containers` | `public Container WoundsContainer = default!;` (`:164`) | **SAME** (engine) |
| C12 | `RegisterComponent`, `NetworkedComponent` | `Robust.Shared.GameStates` | on every component | **SAME** (engine) |
| C13 | `AutoGenerateComponentState(raiseAfterAutoHandleState: true)` | `Robust.Shared.Analyzers` (re-exported) | `:95`, `:102` | **SAME** — `WG/RobustToolbox/Robust.Shared/Analyzers/ComponentNetworkGeneratorAuxiliary.cs:54`: `public AutoGenerateComponentStateAttribute(bool raiseAfterAutoHandleState = false, bool fieldDeltas = false)` |
| C14 | `AutoNetworkedField` | same | throughout | **SAME** — `ComponentNetworkGeneratorAuxiliary.cs:66` |
| C15 | `Serializable`, `NetSerializable` | `System` / `Robust.Shared.Serialization` | on the four enums | **SAME** |
| C16 | `ProtoId<T>` | `Robust.Shared.Prototypes` | throughout | **SAME** |
| C17 | `BodyPartProfilePrototype`, `WoundPrototype` | `Content.Shared._Onyx.Wounds` | `ProtoId<BodyPartProfilePrototype> Profile = "OrganicBodyPartProfile"` (`:161`), `ProtoId<WoundPrototype> DismembermentWound = "DismembermentWound"` (`:64`) | Onyx-internal — port `WoundPrototype.cs` and `Resources/Prototypes/_Onyx/Wounds/wounds.yml`. |
| C18 | `using Content.Shared.Humanoid;` | — | line 5 | **SAME** namespace exists |

**CCVars: none. Loc keys: none.** Prototype ids referenced as literals: `"OrganicBodyPartProfile"`,
`"DismembermentWound"`, `"AmputationConsequenceWound"` plus the ten damage types — all supplied by
`Resources/Prototypes/_Onyx/Wounds/wounds.yml`, which ports alongside.

### 2.3 Adaptations

Only C2/C3 need anything, and they are covered by §1.3-E (append `Chest`, `Groin` to `BodyPartType`). With that one
upstream enum edit, **this file ports verbatim** — no shim, no `// WOLFGATE` marker inside it.

Two content-level follow-ups (not code):
- `WoundHostComponent.TargetWeights` has no `Torso` key. On a Wolfgate human (which has `Torso`, not `Chest`+`Groin`)
  the torso silently falls to weight `1f` via `GetValueOrDefault(component.PartType, 1f)`
  (`WoundDamageRoutingSystem.cs:428`) instead of Onyx's `2.5f`. Fix in a `_WF/Wolfmed` prototype override, not in code.
- `DismembermentSeverities` likewise has no `Torso` key, so a Wolfgate torso uses
  `DefaultDismembermentSeverity = 100` (`:61`). Probably fine; note it for D4 tuning.

### 2.4 Directed subscriptions

None — this file declares no system.

### 2.5 Networking / prediction

All eleven components are `[NetworkedComponent]`; nine use `[AutoGenerateComponentState]`. Two use
`raiseAfterAutoHandleState: true` (`PartDamageVisualsComponent` `:95`, `PainComponent` `:102`) which means whichever
Onyx system subscribes `AfterAutoHandleStateEvent` for them (client-side pain overlay / damage overlay) must be ported
too, or RT's `AfterAutoHandleStateAnalyzer` will be satisfied but nothing will react.

`WoundableComponent.WoundsContainer` is `[ViewVariables]` `Container` — containers replicate through the engine's own
container state, so the wound entities themselves reach the client. Everything here is server-authoritative
component state; nothing is predicted.

No dependency in this file lives server-only in Wolfgate.

---

## 3. `WoundDamageProjectionSystem.cs`

### 3.1 Purpose and Onyx-internal dependencies

Keeps the mob's own `DamageableComponent` a read-only *projection* of the sum of its parts: on map-init it ensures the
wound components on the body and every part, on every part damage it recomputes the body's total
(`RefreshBodyDamage`) and per-layer visual damage, and on rejuvenate it clears parts, pain and amputation pressure.
It is the piece that lets Wolfgate's existing health/threshold/HUD code keep reading `DamageableComponent` on the mob
while wounds do the real bookkeeping on the parts.

Depends on: `_Onyx/Wounds/WoundDamageComponents.cs` (all of `WoundHost`/`Woundable`/`Pain`/`SystemicDamage`/
`PartDamageVisuals`), `_Onyx/Wounds/PainSystem.cs` (`SetPain`, `ClearPainSuppression`, `GetRawPain`, `ApplyDamage`,
`CanFeelPain`), `_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` (`SynchronizeStreams`),
`_Onyx/Body/Part/BodyPartComponent.cs` (`.Parent`, `.Body`, `.PartType`, `.Symmetry`).

### 3.2 External symbol table

| # | Symbol | Namespace | Exact Onyx usage | Wolfgate status |
|---|---|---|---|---|
| P1 | `InitialBodySystem` | `Content.Shared.Body` | `SubscribeLocalEvent<WoundHostComponent, MapInitEvent>(OnMapInit, after: [typeof(InitialBodySystem)])` (`:29`) | **MISSING** — this is Nubody glue (`C:/tmp/onyx/Content.Shared/Body/InitialBodySystem.cs`), explicitly out of scope per **D8**. Wolfgate has no such type; `grep -rn "class InitialBodySystem"` over `Content.Shared/` + `Content.Server/` returns nothing. |
| P2 | `DamageDealtEvent` | `Content.Shared.Damage.Systems` | `SubscribeLocalEvent<WoundableComponent, DamageDealtEvent>(OnPartDamageDealt, after: [typeof(DamageableSystem)])` (`:31`); handler reads `args.Damage` | **MISSING** — §0.1 / §1.3-B. Note this subscription wants to run *after* the damage is written, the opposite of the routing system's. |
| P3 | `DamageableSystem.ClearAllDamage` | — | `_damage.ClearAllDamage((part, damageable))` (`:56`)<br>Onyx: `public void ClearAllDamage(Entity<DamageableComponent?> ent)` (API.cs:374) | **MISSING** — WG has `SetAllDamage(EntityUid uid, DamageableComponent component, FixedPoint2 newValue)` (`DamageableSystem.cs:311`), three required params. Covered by §1.3-A. |
| P4 | `DamageableSystem.GetPositiveDamage` | — | `_damage.GetPositiveDamage((part, damageable))` (`:133`, `:171`) | **MISSING** — §1.3-A |
| P5 | `DamageableSystem.SetDamage` | — | `_damage.SetDamage(body, total)` (`:183`)<br>Onyx: `public void SetDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage)` (API.cs:38) | **DIFFERENT** — WG `DamageableSystem.cs:143`: `public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)`. Three required params, and it replaces the whole `DamageSpecifier` *object* (`damageable.Damage = damage;`) rather than merging into the existing dict. The 2-arg Onyx call cannot bind to it, so no silent trap — but the shim must reproduce Onyx's *merge-and-prune* semantics (§1.3-A). |
| P6 | `DamageableSystem.CanBeDamagedBy` | — | `_damage.CanBeDamagedBy(body, type)` (`:156`) | **MISSING** — §1.3-A |
| P7 | `InjurableComponent` | `Content.Shared.Damage.Components` | `var injurable = EnsureComp<InjurableComponent>(part); if (injurable.DamageContainer != null) return; injurable.DamageContainer = "Biological"; Dirty(part, injurable);` (`:215-220`) | **MISSING** — the field is `ProtoId<DamageContainerPrototype>? DamageContainer` (`C:/tmp/onyx/Content.Shared/Damage/Components/InjurableComponent.cs:21`). Wolfgate's equivalent is `DamageableComponent.DamageContainerID` (`WG/Content.Shared/Damage/Components/DamageableComponent.cs:27-28`, `[DataField("damageContainer")] public ProtoId<DamageContainerPrototype>? DamageContainerID;`). |
| P8 | `DamageableComponent` | `Content.Shared.Damage` | `EnsureComp<DamageableComponent>(part)` (`:209`), `TryComp(part, out DamageableComponent? damageable)` | **SAME** (namespace note as R32) |
| P9 | `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(body)` (`:53`, `:167`, `:200`, `:226`) | **SAME** — `SharedBodySystem.Body.cs:256` |
| P10 | `SharedBodySystem.GetBodyPartChildren` | same | `_body.GetBodyPartChildren(root)` (`:128`) | **SAME** — `SharedBodySystem.Parts.cs:880`: `public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(EntityUid partId, BodyPartComponent? part = null)` |
| P11 | `BodyPartComponent.Parent` | `Content.Shared.Body.Part` | `while (CompOrNull<BodyPartComponent>(root)?.Parent is { } parent) root = parent;` (`:115-116`) | **MISSING** — WG's `BodyPartComponent` has no `Parent` field. It has `[DataField, AutoNetworkedField] public BodyPartSlot? ParentSlot;` (`WG/Content.Shared/Body/Part/BodyPartComponent.cs:31-32`), and `BodyPartSlot` is `{ string Id; BodyPartType Type; }` (`:232-241`) — no parent uid. The uid comes from the container instead: `SharedBodySystem.Parts.cs:430` `public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)` and `:453` `public bool TryGetParentBodyPart(EntityUid partUid, out EntityUid? parentUid, out BodyPartComponent? parentComponent)`. |
| P12 | `BodyPartComponent.Body` | same | `if (component.Body is { } body)` (`:89`) | **SAME** — WG `BodyPartComponent.cs:26-27`: `[DataField, AutoNetworkedField] public EntityUid? Body;` |
| P13 | `BodyPartComponent.PartType` / `.Symmetry` | same | `switch (component.PartType, component.Symmetry)` (`:237`) | **SAME** — WG `BodyPartComponent.cs:150`, `:161` |
| P14 | `HumanoidVisualLayers.Chest/Head/LArm/RArm/LHand/RHand/LLeg/RLeg/LFoot/RFoot` | `Content.Shared.Humanoid` | `:239`, `:241-249` | **SAME** — all present in `WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs:15-29` |
| P15 | `HumanoidVisualLayers.Groin` | same | `case (BodyPartType.Groin, _): layer = HumanoidVisualLayers.Groin;` (`:240`) | **MISSING** — §0.2 / §1.3-E |
| P16 | `BodyPartType.Chest` / `.Groin` | `Content.Shared.Body.Part` | `:239`, `:240` | **MISSING** — §0.2 |
| P17 | `RejuvenateEvent` | `Content.Shared.Rejuvenate` | `SubscribeLocalEvent<WoundHostComponent, RejuvenateEvent>(OnRejuvenate)` with `ref RejuvenateEvent args` (`:30`, `:40`) | **SAME** — WG `Content.Shared/Rejuvenate/RejuvenateEvent.cs:3`: `public sealed class RejuvenateEvent : EntityEventArgs { }`; Onyx's is the identical class. Subscribing a class event by `ref` is already done in Wolfgate (`Content.Shared/Emp/SharedEmpSystem.cs:151`, `Content.Shared/Eye/Blinding/Systems/BlindableSystem.cs:20`) and RT 277 only enforces the by-ref/by-value match for *broadcast* subs (`EntityEventBus.Broadcast.cs:217-222`), not directed ones. |
| P18 | `MapInitEvent` | `Robust.Shared.GameObjects` | `:29`, `:34` | **SAME** (engine) |
| P19 | `INetManager.IsServer` | `Robust.Shared.Network` | `:42`, `:84`, `:123`, `:145`, `:194` | **SAME** |
| P20 | `FixedPoint2.Zero` | `Content.Shared.FixedPoint` | `:59`, `:64`, `:66`, `:225` | **SAME** |
| P21 | `CirculatoryStreamSystem.SynchronizeStreams` | `Content.Shared._Onyx.Chemistry.Circulation` | `_circulation.SynchronizeStreams(body)` (`:37`)<br>Def: `public void SynchronizeStreams(EntityUid body, EntityUid? insertedPart = null)` (`CirculatoryStreamSystem.cs:121`) | Onyx-internal — port the 4 Circulation files. |
| P22 | `System.Linq` (`.ToArray()`) | — | `systemic.Damage.DamageDict.ToArray()` (`:153`) | **SAME** |

### 3.3 Adaptations

#### P1 — `InitialBodySystem` ordering

- **(b) `// WOLFGATE` edit, preferred.** One line. `WoundDamageProjectionSystem.cs:29` becomes:
  ```csharp
        // WOLFGATE: InitialBodySystem is Nubody-only (D8); Wolfgate builds bodies in BodySystem's MapInit.
        SubscribeLocalEvent<WoundHostComponent, MapInitEvent>(OnMapInit, after: [typeof(SharedBodySystem)]);
  ```
  `SharedBodySystem` is where Wolfgate assembles the body from `BodyPrototype` — check with the body agent which
  concrete system subscribes `MapInitEvent` for `BodyComponent` in Wolfgate and name that type; if the sub is on a
  derived `BodySystem`, use `typeof(BodySystem)` from the server assembly, which a shared file cannot reference — in
  that case use `after: [typeof(SharedBodySystem)]` anyway (the subscription is registered by the derived instance but
  ordering keys on the *registering system's* type, so this may be wrong; verify empirically with an integration test
  that asserts `WoundableComponent` lands on every part after map-init).
- **(a) shim:** a no-op `InitialBodySystem : EntitySystem` in `_WF/Wolfmed/Compat` under namespace
  `Content.Shared.Body` would keep the file verbatim and compile, but since it subscribes to nothing the ordering is
  inert (`allowMissing: true`) — it buys verbatim-ness and nothing else. Acceptable if re-sync cost dominates; be
  explicit in the manifest that it is a placebo.
- **(c)** none needed.

#### P7 — `InjurableComponent`

- **(a) shim, preferred.** `Content.Shared/_WF/Wolfmed/Compat/InjurableComponent.cs`:
  ```csharp
  namespace Content.Shared.Damage.Components;

  /// <summary>Wolfgate stand-in for upstream InjurableComponent; mirrors its container onto DamageableComponent.</summary>
  [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
  public sealed partial class InjurableComponent : Component
  {
      /// <summary>Damage container this part supports. Copied to DamageableComponent.DamageContainerID.</summary>
      [DataField, AutoNetworkedField]
      public ProtoId<DamageContainerPrototype>? DamageContainer;
  }
  ```
  plus `Content.Shared/_WF/Wolfmed/InjurableBridgeSystem.cs` that, on `AfterAutoHandleStateEvent` /
  `ComponentStartup` and whenever `DamageContainer` changes, writes it to `DamageableComponent.DamageContainerID`
  **and re-seeds `Damage.DamageDict`** the way `DamageableSystem.DamageableInit` does
  (`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:103-133`). Without the re-seed, WG's
  `TryChangeDamage` skips every type (`:256-257`: `if (!dict.TryGetValue(type, out var oldValue)) continue;`) and
  parts take zero damage — a silent, total failure mode. **This is the single easiest way to get a "wounds do nothing"
  bug.**
  Note the ordering in `SetupPart` (`:208-221`): `EnsureComp<DamageableComponent>` runs *first* (line 209), so it
  initialises with `DamageContainerID == null` → all damage types seeded (`DamageableSystem.cs:124-129`) → then the
  bridge narrows it to `Biological` and must prune/re-seed.
- **(b)** a `// WOLFGATE` edit replacing lines 215-220 with direct manipulation of `DamageableComponent.DamageContainerID`
  is 5 changed lines and skips the bridge system. Simpler, but `InjurableComponent` appears in other Onyx wound files
  and in `CanBeDamagedBy`, so the shim pays for itself.

#### P11 — `BodyPartComponent.Parent`

- **(b) `// WOLFGATE` edit, preferred** (a shim cannot add a field to a sealed upstream component, and adding one
  upstream would need it kept in sync by `SharedBodySystem` — far more than a two-line hook).
  Replace `WoundDamageProjectionSystem.cs:112-119`:
  ```csharp
    private EntityUid GetDetachedRoot(EntityUid part)
    {
        // WOLFGATE: Wolfgate's BodyPartComponent has no Parent uid; walk the part containers instead.
        var root = part;
        while (_body.TryGetParentBodyPart(root, out var parent, out _) && parent is { } parentUid)
            root = parentUid;

        return root;
    }
  ```
  `TryGetParentBodyPart` is `SharedBodySystem.Parts.cs:453`:
  `public bool TryGetParentBodyPart(EntityUid partUid, [NotNullWhen(true)] out EntityUid? parentUid, [NotNullWhen(true)] out BodyPartComponent? parentComponent)`.
  Caution: it `DebugTools.Assert(HasComp<BodyPartComponent>(partUid))` on entry (`:458`) — the loop above only ever
  passes parts, so that holds, but the *routing* system's use of `.Parent` (`WoundDamageRoutingSystem.cs:884`,
  `part.Parent == null`) is a different shape and should become
  `!_body.TryGetParentBodyPart(parts[i], out _, out _)`.
- **(a) shim alternative worth considering:** add `public EntityUid? Parent => …` cannot be done on a sealed class from
  outside — but `BodyPartComponent` in Wolfgate **is** `public sealed partial class` (`BodyPartComponent.cs:20`), so a
  `_WF` partial *could* add a computed property. It would need `IoCManager` inside a component to walk containers,
  which the codebase already does for the VV helpers (`BodyPartComponent.cs:198`) but is bad practice for hot paths
  (`GetDetachedRoot` runs per part-damage). **Recommend the file edit, not the partial property.**

#### P3/P4/P5/P6 — covered entirely by the compat partial in §1.3-A.

#### P15/P16 — covered by §1.3-E.

### 3.4 Directed subscriptions

| Component | Event | Ordering | Conflict in Wolfgate? |
|---|---|---|---|
| `WoundHostComponent` | `MapInitEvent` | `after: [typeof(InitialBodySystem)]` | None (new component). Ordering type missing — §3.3/P1. |
| `WoundHostComponent` | `RejuvenateEvent` | — | None (new component). Existing WG `RejuvenateEvent` subscribers are on other components (`SharedEmpSystem.cs:151` / `EmpDisabledComponent`, `BlindableSystem.cs:20` / `BlindableComponent`, and `DamageableComponent` inside `DamageableSystem`). |
| `WoundableComponent` | `DamageDealtEvent` | `after: [typeof(DamageableSystem)]` | Event missing. Once the §1.3-B hook exists, `DamageableSystem` still will not subscribe, so this `after:` is inert; correctness depends on the raise being placed *before* the dict write in `TryChangeDamage` for the routing sub, and this projection sub then needs to run **after** the write. **That is contradictory with a single raise point.** Resolution: raise `DamageDealtEvent` before the write for the routing system, and have the projection system instead subscribe to Wolfgate's existing `DamageChangedEvent` (`DamageableSystem.cs:522`), which is raised after the write (`DamageableSystem.cs:266` → `DamageChanged(...)`). That is a `// WOLFGATE` edit of 2 lines in the projection file and must be flagged to the orchestrator as a real design decision, not a mechanical translation. |

### 3.5 Networking / prediction

Shared file, **server-only in practice**: `OnRejuvenate` (`:42`), `OnPartDamageDealt` (`:84`), `RefreshDetachedDamage`
(`:123`), `RefreshBodyDamage` (`:145`) and `SetupBody` (`:194`) all early-return on `!_net.IsServer`. `SetupPart`
(`:206`) and `RefreshBodyPain` (`:223`) do not check, but they are only reached from server-gated callers or from the
public `OnPartInserted`/`OnPartRemoved` hooks, which the body system calls server-side. So no prediction to preserve.

Server-only Wolfgate dependencies reached: none directly. `PainSystem` (shared, needs `StatusEffectNew` per D1) and
`CirculatoryStreamSystem` (ports as shared) are the only non-engine systems it touches.

Client-visible output is `PartDamageVisualsComponent` (`[AutoNetworkedField] Dictionary<HumanoidVisualLayers, DamageSpecifier>`,
`raiseAfterAutoHandleState: true`) — the client damage-overlay system that consumes it is **not** in these four files
and must be located in `Content.Client/_Onyx/` before phase 1 ships, or bodies will show no wound sprites.

---

## 4. `WoundEvents.cs`

### 4.1 Purpose and Onyx-internal dependencies

The event vocabulary of the wound model: 15 `[ByRefEvent]` records (wound lifecycle, bleeding/pain changes, part damage
applied/overflowed, fracture grade/treatment, scars, functionality), the `BodyPartFunctionalityState` enum, and one
class event, `PartDamageModifyEvent`, which is the armor hook (`IInventoryRelayEvent` relayed to every non-pocket slot
after the struck part is resolved). It contains no logic.

Depends on `_Onyx/Wounds/WoundPrototype.cs` (`WoundPrototype`, `TreatmentCapability`) and
`_Onyx/Wounds/WoundDamageComponents.cs` (`WoundState`, `FractureGrade`, `FractureTreatment`).

### 4.2 External symbol table

| # | Symbol | Namespace | Exact Onyx usage | Wolfgate status |
|---|---|---|---|---|
| E1 | `FixedPoint2` | `Content.Shared.FixedPoint` | `FixedPoint2 OldSeverity` (`:18`) etc. | **SAME** |
| E2 | `DamageSpecifier` | `Content.Shared.Damage` | `DamageSpecifier Damage` (`:50`, `:65`, `:125`, `:142`, `:149`) | **SAME** |
| E3 | `DamageContainerPrototype` | `Content.Shared.Damage.Prototypes` | `IReadOnlyList<ProtoId<DamageContainerPrototype>>? DamageContainers` (`:125`) | **SAME** — `WG/Content.Shared/Damage/Prototypes/DamageContainerPrototype.cs:5` |
| E4 | `BodyPartType` | `Content.Shared.Body.Part` | `BodyPartType partType` (`:141`), `public readonly BodyPartType PartType` (`:146`) | **SAME** (no `Chest`/`Groin` literal is used in *this* file) |
| E5 | `BodyPartSymmetry` | `Content.Shared.Body.Part` | `BodyPartSymmetry symmetry` (`:142`), `:147` | **SAME** — WG `Content.Shared/Body/Part/BodyPartSymmetry.cs`: `public enum BodyPartSymmetry { None = 0, Left, Right }`; Onyx's `_Onyx/Body/Part/BodyPartComponent.cs:30`: `public enum BodyPartSymmetry { None, Left, Right }`. Identical members and ordinals. |
| E6 | `IInventoryRelayEvent` | `Content.Shared.Inventory` | `public sealed class PartDamageModifyEvent(...) : EntityEventArgs, IInventoryRelayEvent` (`:143`) | **SAME** — WG `Content.Shared/Inventory/InventorySystem.Relay.cs:172` |
| E7 | `SlotFlags.WITHOUT_POCKET` | `Content.Shared.Inventory` | `public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;` (`:150`) | **SAME** — WG `Content.Shared/Inventory/SlotFlags.cs:37`: `WITHOUT_POCKET = All & ~POCKET`. (Wolfgate's own `DamageModifyEvent` writes it as `~SlotFlags.POCKET` at `DamageableSystem.cs:503`; the named constant exists and is what Onyx uses.) |
| E8 | `EntityEventArgs` | `Robust.Shared.GameObjects` | `:143` | **SAME** |
| E9 | `ByRefEvent` | `Robust.Shared.GameObjects` | 15 uses | **SAME** — `WG/RobustToolbox/Robust.Shared/GameObjects/EventBusAttributes.cs:6` |
| E10 | `Serializable`, `NetSerializable` | `System` / `Robust.Shared.Serialization` | `:88` on `BodyPartFunctionalityState` | **SAME** |
| E11 | `ProtoId<T>` | `Robust.Shared.Prototypes` | `:12`, `:29`, `:125` | **SAME** |
| E12 | `IReadOnlySet<T>` / `IReadOnlyList<T>` | `System.Collections.Generic` (implicit usings) | `:126`, `:127`, `:125` | **SAME** — verify `ImplicitUsings` is on in `Content.Shared.csproj`; Onyx's file has no `using System.Collections.Generic;`. |
| E13 | `WoundPrototype`, `TreatmentCapability`, `WoundState`, `FractureGrade`, `FractureTreatment` | `Content.Shared._Onyx.Wounds` | throughout | Onyx-internal — port with the wounds. |

**No CCVars, no Loc keys, no prototype string literals, no systems, no engine APIs beyond attributes.**

### 4.3 Adaptations

**None required.** Every external symbol is SAME. This file ports byte-for-byte.

One forward-looking note that is not a gap but will bite the armor work: `PartDamageModifyEvent` is a *new, separate*
relay event. Wolfgate's armor (`Content.Shared/Armor/SharedArmorSystem.cs`, and `_Mono/ArmorPlate`) subscribes
Wolfgate's `DamageModifyEvent` (`DamageableSystem.cs:496-520`, which already carries a Shitmed `TargetPart`). With
Onyx routing in place, the body's `DamageModifyEvent` still fires inside `TryChangeDamage` on the mob, and *then*
`PartDamageModifyEvent` fires again on the resolved part (`WoundDamageRoutingSystem.cs:674-681`) — so armor would apply
**twice** unless one of the two is suppressed for wound hosts. That is a phase-1 balance/correctness item for whoever
owns the armor bridge; it is caused by these files but is not a compile gap.

### 4.4 Directed subscriptions

None — this file declares no system.

### 4.5 Networking / prediction

`BodyPartFunctionalityState` is `[Serializable, NetSerializable]` because it is replicated through
`BodyPartFunctionalityComponent` / `WoundFunctionalityComponent`. The events themselves are local-only and never
networked. `PartDamageModifyEvent` is raised on the server inside the routing path (which is `_net.IsServer`-gated), so
no prediction concerns.

No dependency here is server-only in Wolfgate.

---

## 5. Port difficulty

| File | Rating | Why |
|---|---|---|
| `WoundEvents.cs` | **verbatim** | Every external symbol is SAME. Zero edits, zero shims. Only needs `WoundPrototype.cs` + `WoundDamageComponents.cs` alongside it. |
| `WoundDamageComponents.cs` | **verbatim** *(conditional)* | Verbatim **once `BodyPartType.Chest` and `BodyPartType.Groin` are appended upstream** (§1.3-E). Without that it does not compile at all. No shims, no in-file markers. |
| `WoundDamageProjectionSystem.cs` | **light edits** | 3 `// WOLFGATE` edits (`InitialBodySystem` ordering at `:29`, `GetDetachedRoot` at `:112-119`, and — if the §3.4 resolution is taken — the `DamageDealtEvent` → `DamageChangedEvent` subscription at `:31`), plus it consumes the compat partial (`ClearAllDamage`, `GetPositiveDamage`, `SetDamage`, `CanBeDamagedBy`) and the `InjurableComponent` shim. |
| `WoundDamageRoutingSystem.cs` | **heavy adaptation** | Needs the entire new-style `DamageableSystem` API recreated (8 methods), a brand-new `DamageDealtEvent` seam inside upstream `TryChangeDamage`, a hands rewrite (`:545-572`), a `HealOnBuckleComponent` workaround (`:952`), `DamageSpecifier.Clone()`, `BodyPartComponent.MaxDamage`/`.AmputationThresholds`/`.Parent` (which live in a `_WF` part component per **D8** — every access at `:729-735`, `:882-885` must be redirected), and the whole `_Onyx/Targeting` + `_Onyx/Mobs` sub-ports. It is also the file where the `TryChangeDamage` overload trap lives. |

## 6. Wolfgate files that need hooks

Ordered by how invasive the hook is. "Lines" is the estimated `// WOLFGATE` diff.

| Wolfgate file | Hook | Lines | Driven by |
|---|---|---|---|
| `Content.Shared/Damage/Systems/DamageableSystem.cs` | Raise `DamageDealtEvent` after `ApplyUniversalAllModifiers` (`:248`), before the dict write (`:250-269`). **The one upstream change the port cannot avoid.** | ~5 | §0.1, §1.3-B |
| `Content.Shared/Body/Part/BodyPartType.cs` | Append `Chest, Groin` after `Tail` (`:19`) | 2 | §0.2, §1.3-E |
| `Content.Shared/Humanoid/HumanoidVisualLayers.cs` | Append `Groin` after `Chest` (`:15`) | 1 | §0.2, P15 |
| `Content.Server/Bed/BedSystem.cs` (+ `Content.Server/Bed/Components/HealOnBuckleComponent.cs`) | `EnsureComp` the shared marker, **or** move the component to `Content.Shared/Bed/Components/` | 1–2 (or a file move) | §0.3, §1.3-D |
| `Content.Shared/Body/Systems/SharedBodySystem.Parts.cs` | Call `WoundDamageProjectionSystem.OnPartInserted` / `.OnPartRemoved` (`WoundDamageProjectionSystem.cs:95`, `:105`) from wherever Wolfgate attaches/detaches parts (`AttachPart` `:652`/`:667`, `DetachPart` `:702`/`:751`). **Nothing calls these two public methods in the four files analysed** — they are pure hook points and will silently never fire if forgotten. | 2–4 | P-hooks |
| `Content.Shared/Damage/Systems/DamageableSystem.cs` (second hook) | Bypass Shitmed's `TryChangePartDamageEvent` spreading + sever-at-130 for `WoundHostComponent` entities (**D2**) | 2–3 | §1.3-A |

Plus these **new** `_WF/Wolfmed/Compat` files (no upstream diff):
`DamageableSystem.Wolfmed.cs` (partial, 8 methods), `DamageSpecifier.Clone.cs` (partial, 1 method),
`DamageDealtEvent.cs`, `InjurableComponent.cs` + `InjurableBridgeSystem.cs`,
`HealOnBuckleMarkerComponent.cs`, and (if option (a) is taken for P1) a no-op `InitialBodySystem.cs`.

And these **additional Onyx files** that these four hard-depend on and that are outside `_Onyx/Wounds`:
`_Onyx/Targeting/` (`TargetResolverSystem.cs`, `TargetBodyPart.cs`, `TargetingSnapshotComponent.cs`,
`DamageDistribution.cs`, and whatever `TargetingSnapshotSystem.cs` needs),
`_Onyx/Mobs/Systems/MobThresholdSystem.cs`,
`_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` (+3 siblings),
and a `_WF/Wolfmed` part-data component carrying Onyx's `BodyPartComponent` extras
(`MaxDamage`, `AmputationThresholds`, `DismembermentFinishingDamage`, `FractureProfile`,
`AmputationConsequenceSeverity`, `DismembermentSeverity` — `C:/tmp/onyx/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:69-97`).

## 7. The blocker

**There is no `DamageDealtEvent` seam in Wolfgate, and the two subscriptions that need it want opposite orderings.**

`WoundDamageRoutingSystem` needs to intercept damage *before* it is written to the component
(`WoundDamageRoutingSystem.cs:51`, `before: [typeof(DamageableSystem)]`, handler clears the dict at `:70`), while
`WoundDamageProjectionSystem` needs to react *after* it is written
(`WoundDamageProjectionSystem.cs:31`, `after: [typeof(DamageableSystem)]`). In Onyx both hang off one event because
`DamageableSystem` is itself just another subscriber. In Wolfgate the write is inline inside `TryChangeDamage`
(`DamageableSystem.cs:250-269`), so a single injected raise point cannot serve both. The port must split them:
new `DamageDealtEvent` (raised pre-write) for the routing system, and Wolfgate's existing post-write
`DamageChangedEvent` (`DamageableSystem.cs:522`, raised via `DamageChanged` at `:269`) for the projection system.
That is a design decision, not a mechanical translation, and it needs the orchestrator's sign-off before either file is
vendored.
