# Wolfmed port — dependency and API-gap analysis: wounds-b

Scope: six Onyx files under `C:/tmp/onyx/Content.Shared/_Onyx/Wounds/` —
`WoundSystem.cs`, `WoundPrototype.cs`, `WoundHealingSystem.cs`, `WoundBleedingSystem.cs`,
`WoundInternalBleedingSystem.cs`, `WoundScarSystem.cs`.

Onyx pinned at `2f5bab9`. Wolfgate worktree (`WG`) =
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`.
RobustToolbox 277 source at `WG/RobustToolbox`. Every claim below was verified by reading the
cited file. Read-only; nothing under `WG` was touched.

---

## 0. Shared conclusions (apply to several files)

### 0.1 These six systems are NOT predicted

Every one of them is a plain shared `EntitySystem`, but essentially every mutating entry point is
guarded by `if (!_net.IsServer) return;`:

- `WoundSystem.cs:66, 124, 142, 176, 282, 336, 354, 373, 399`
- `WoundBleedingSystem.cs:90, 113, 132, 143, 162, 215, 227, 293, 334, 346, 397`
- `WoundInternalBleedingSystem.cs:27, 36, 45, 54`
- `WoundScarSystem.cs:35, 66`
- `WoundHealingSystem.cs:104`

So the model is **server-authoritative simulation, client-replicated through networked components**
(`WoundComponent`, `WoundBleedingComponent`, … are all `[NetworkedComponent, AutoGenerateComponentState]`
in `WoundDamageComponents.cs`). There is no client-side rollback logic to preserve.

The consequence for the port is not prediction correctness — it is **compilation**. A shared system
must compile into `Content.Client` too, so any type it names has to exist in `Content.Shared`.
That is exactly where Wolfgate breaks, because `BloodstreamSystem`, `BloodstreamComponent` and
`HealingComponent` are all server-only here.

### 0.2 Engine (RT 277) — everything these six files use is present

| Engine API | Onyx usage | WG status |
|---|---|---|
| `Entity<T>.AsNullable()` | `WoundSystem.cs:87`, `WoundBleedingSystem.cs:98` | SAME — `WG/RobustToolbox/Robust.Shared/GameObjects/Entity.cs:62` `public readonly Entity<T?> AsNullable()` |
| `implicit operator Entity<T?>(EntityUid)` | pervasive (`GetPartRate(part)` with a bare `EntityUid`) | SAME — `Entity.cs:38-42` |
| `implicit operator EntityUid(Entity<T>)` | pervasive | SAME — `Entity.cs:44-47` |
| `Dirty<T>(Entity<T> ent, MetaDataComponent? meta = null) where T : IComponent?` | `WoundSystem.cs:201, 304`, etc. | SAME — `EntitySystem.Proxy.cs:204` (note the `IComponent?` constraint — `Dirty(wound)` on an `Entity<WoundComponent?>` compiles) |
| `Dirty(EntityUid uid, IComponent component, MetaDataComponent? meta = null)` | `WoundSystem.cs:135, 232, 255` | SAME — `EntitySystem.Proxy.cs:151` |
| `EntityUid Spawn(string? prototype, MapCoordinates coordinates, ComponentRegistry? overrides = null, Angle rotation = default)` | `WoundSystem.cs:195` `Spawn(null, MapCoordinates.Nullspace)` | SAME — `EntitySystem.Proxy.cs:920` |
| `T? CompOrNull<T>(EntityUid uid)` | `WoundBleedingSystem.cs:231` | SAME — `EntitySystem.Proxy.cs:474` |
| `PrototypesReloadedEventArgs.WasModified<T>()` | `WoundSystem.cs:40` | SAME — `Robust.Shared/Prototypes/IPrototypeManager.cs:643` |
| `[Prototype]` with no arguments | `WoundPrototype.cs:12, 134, 210` | SAME — `Robust.Shared/Prototypes/Attributes.cs:40` `public PrototypeAttribute(string? type = null, int loadPriority = 1)` |
| `EntProtoId<T>` | `WoundBehaviors.cs:102` (dependency of `WoundPrototype`) | SAME — `Robust.Shared/Prototypes/EntProtoId.cs:62` |
| `IPrototypeManager.TryIndex<T>(ProtoId<T>, out T?)` | pervasive | SAME — `IPrototypeManager.cs:273` (and the `ProtoId<T>?` overload at `:411`) |
| `SharedContainerSystem.EnsureContainer<T>(EntityUid, string, ContainerManagerComponent? = null)` | `WoundSystem.cs:61, 205` | SAME — `SharedContainerSystem.cs:153` |
| `SharedContainerSystem.TryGetContainer(...)` | `WoundSystem.cs:154, 362, 374` | SAME — `SharedContainerSystem.cs:175` |
| `SharedContainerSystem.Insert(Entity<TransformComponent?, MetaDataComponent?, PhysicsComponent?>, BaseContainer, TransformComponent? = null, bool force = false)` | `WoundSystem.cs:207` | SAME — `SharedContainerSystem.Insert.cs:30` |
| `SharedContainerSystem.Remove(Entity<TransformComponent?, MetaDataComponent?>, BaseContainer, bool reparent = true, ...)` | `WoundSystem.cs:363` | SAME — `SharedContainerSystem.Remove.cs:29` |
| `SharedAudioSystem.PlayPredicted(SoundSpecifier?, EntityUid, EntityUid?, AudioParams? = null)` | `WoundBleedingSystem.cs:106` | SAME — `Robust.Shared/Audio/Systems/SharedAudioSystem.cs:652` |
| `IRobustRandom.Prob(float)` | `WoundSystem.cs:228, 245`, `WoundScarSystem.cs:53` | SAME — extension in `Robust.Shared/Random/RandomExtensions.cs:171`; both files already `using Robust.Shared.Random;` |
| `INetManager.IsServer`, `IGameTiming.CurTime` | pervasive | SAME — `INetManager.cs:16`, `IGameTiming.cs:33` |
| `[Dependency]` on an *abstract* system (`SharedBodySystem`) | all six | SAME — `EntitySystemManager.cs:174-199` registers systems under their supertypes; Wolfgate already does this at `Content.Shared/Damage/Systems/DamageableSystem.cs:30` |
| Collection expressions (`= [TreatmentCapability.Biological]`) | `WoundPrototype.cs:154, 250` | SAME — `WG/MSBuild/Content.props:8` sets `LangVersion` 14 |

**No RT 277-vs-289 gap affects these six files.** The handoff's "expect some newer-API fixes" does not
materialise here.

### 0.3 Name collisions — none

Grepped `WG/Content.{Shared,Server,Client}` for `WoundPrototype`, `BodyPartProfilePrototype`,
`FractureProfilePrototype`, `CirculatoryStreamPrototype`, `WoundableComponent`, `WoundComponent`,
`WoundHostComponent`, `PainComponent`: **zero** hits. The prototype kinds (`wound`,
`bodyPartProfile`, `fractureProfile`) and the component names are free.

`WG/Content.Shared/_Onyx` does not exist yet; `WG/Content.Shared/_WF/` exists with
`Administration, Audio, CCVar, Ghost, SafetyDepositBox, Shuttles`.

### 0.4 The two enum gaps that poison everything downstream

**`BodyPartType`.** Onyx (`Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:15-27`, namespace
`Content.Shared.Body.Part`):

```csharp
public enum BodyPartType : ushort
{ Other = 0, Torso = 1, Head = 2, Arm = 3, Hand = 4, Leg = 5, Foot = 6, Tail = 7, Chest = 8, Groin = 9 }
```

Wolfgate (`Content.Shared/Body/Part/BodyPartType.cs:10-20`):

```csharp
public enum BodyPartType
{ Other = 0, Torso, Head, Arm, Hand, Leg, Foot, Tail }
```

**`Chest` and `Groin` do not exist in Wolfgate.** `WoundHostComponent` (`WoundDamageComponents.cs:20-21,
52-53, 61`) hard-codes `BodyPartType.Chest` and `BodyPartType.Groin` in field initialisers, so the
component — a dependency of four of the six files — will not compile. Onyx's `Torso` is a distinct
member from its `Chest`; Wolfgate's Shitmed torso is `Torso`.

**`TargetBodyPart`.** Onyx `Content.Shared/_Onyx/Targeting/TargetBodyPart.cs` vs Wolfgate
`Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:12`. Bit layout is *identical* except Onyx names
bit `1 << 1` **`Chest`** where Wolfgate names it **`Torso`**; Onyx additionally has
`FullArms, FullLegs, BodyMiddle, FullLegsGroin, Vital` and carries `[Serializable, NetSerializable]`
(Wolfgate's has only `[Flags]`).

These are not in my six files directly (they enter via `WoundHostComponent`), but every one of my six
files depends on `WoundHostComponent` or `WoundableComponent` compiling, so I record them here.

*Adaptation (both enums):*
- (a) **Preferred — vendor Onyx's `_Onyx/Targeting/TargetBodyPart.cs` verbatim** into
  `Content.Shared/_Onyx/Targeting/`. Different namespace from `_Shitmed.Targeting`, so no clash. Then
  a `_WF/Wolfmed/Compat/TargetBodyPartBridge.cs` with
  `public static Content.Shared._Shitmed.Targeting.TargetBodyPart ToShitmed(this Content.Shared._Onyx.Targeting.TargetBodyPart p)`
  and the inverse — a pure bit remap, since `Chest` and `Torso` occupy the same bit.
- (b) For `BodyPartType` there is **no shim** — an enum cannot be extended from outside. This needs a
  two-line `// WOLFGATE` edit to `WG/Content.Shared/Body/Part/BodyPartType.cs`:
  ```csharp
          Tail,
          Chest,  // WOLFGATE: Onyx wound profiles distinguish Chest from Torso
          Groin   // WOLFGATE
  ```
  Appending keeps all existing ordinals (`Other`=0 … `Tail`=7) stable, so no existing YAML or
  serialized state changes. This is the single unavoidable upstream enum edit.
- (c) Alternative to (b): `// WOLFGATE` edits inside `WoundDamageComponents.cs` rewriting `Chest`→`Torso`
  and dropping `Groin`. Rejected: it silently changes the target-weight and dismemberment tables and
  makes the vendored file diverge in a way that breaks future re-syncs.

### 0.5 `_Onyx` files outside `Wounds/` that these six require

| Onyx file | Required by | Notes |
|---|---|---|
| `_Onyx/Wounds/WoundDamageComponents.cs` | all six | `WoundableComponent`, `WoundComponent`, `WoundHostComponent`, `WoundBleedingComponent`, `WoundInternalBleedingComponent`, `WoundScarComponent`, `WoundState`, `BleedingTreatment` |
| `_Onyx/Wounds/WoundEvents.cs` | all six | `PartDamageAppliedEvent`, `Wound*Event`, `ResolveHealingPartEvent`, `PartBleedingChangedEvent`, `ScarCreatedEvent` |
| `_Onyx/Wounds/WoundBehaviors.cs` | `WoundSystem`, `WoundPrototype`, `WoundBleedingSystem`, `WoundScarSystem` | drags in `Content.Shared.StatusEffectNew.Components.StatusEffectComponent` (`WoundBehaviors.cs:2, 102`) — see D1 |
| `_Onyx/Wounds/WoundDamageRoutingSystem.cs` | `WoundHealingSystem` (`_routing.TryApplyPartDamage`, `_routing.TryApplyDamage`) | not analysed here (wounds-a/c scope) |
| `_Onyx/Chemistry/Circulation/*` (4 files) | `WoundPrototype` (`CirculatoryStreamPrototype`), `WoundBleedingSystem` (`CirculatoryStreamSystem`) | see §4 |
| `_Onyx/Targeting/TargetBodyPart.cs` | `WoundDamageComponents` | see §0.4 |
| `_Onyx/CCVar/CCVars.Wounds.cs`, `CCVars.Surgery.cs` | `WoundBleedingSystem`, `WoundScarSystem` | see §0.6 |
| `_Onyx/Medical/Healing/HealingComponent.Onyx.cs` | `WoundHealingSystem` | see §3 |

### 0.6 CCVars — drops in clean

Onyx declares `public sealed partial class CCVars` in `namespace Content.Shared.CCVar`
(`_Onyx/CCVar/CCVars.Wounds.cs:5`, `CCVars.Surgery.cs:5`). Wolfgate declares
`public sealed partial class CCVars : CVars` at `WG/Content.Shared/CCVar/CCVars.cs:15`. C# permits a
partial declaration that omits the base list, and Wolfgate already splits `CCVars` across ~45 files
(`CCVars.Explosion.cs`, `CCVars.Playtest.cs`, …). Grepping Wolfgate for `wounds.`, `surgery.scar_chance`,
`explosion.damage_variation`, `explosion.wounding_multiplier`, `ExplosionLimbDamageVariation`,
`ExplosionWoundMultiplier`: **zero** collisions. Both files port **verbatim**.

### 0.7 The `TryChangeDamage` overload-resolution trap (informational)

None of my six files call `TryChangeDamage` directly, but `WoundDamageRoutingSystem` (which
`WoundHealingSystem` depends on) does, and the trap is real:

- Onyx (`Content.Shared/Damage/Systems/DamageableSystem.API.cs:68`):
  ```csharp
  public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage,
      bool ignoreResistances = false, bool interruptsDoAfters = true,
      EntityUid? origin = null, bool ignoreGlobalModifiers = false)
  ```
- Wolfgate (`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:190`):
  ```csharp
  public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
      bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null,
      bool ignoreGlobalModifiers = false, float armorPenetration = 0f,
      bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f,
      TargetBodyPart? targetPart = null, EntityUid? tool = null, DamageOriginFlag? originFlag = null)
  ```

`Entity<DamageableComponent?>` converts implicitly to `EntityUid` and then to `EntityUid?`, so an
Onyx-shaped call **binds silently**. Two failure modes:
1. **Positional argument 5 changes meaning**: Onyx's 5th positional is `origin`; Wolfgate's is
   `damageable`. An Onyx call passing origin positionally would pass an `EntityUid?` where a
   `DamageableComponent?` is expected — that one is a compile error (good). But any call that passes
   *four* positionals and then relies on Onyx's defaults will silently pick up Wolfgate's Shitmed
   defaults `canSever: true, canEvade: false, partMultiplier: 1.0` — i.e. **Shitmed severing is
   re-enabled behind your back**, contradicting D2.
2. **Return type differs**: `bool` vs `DamageSpecifier?`. `if (TryChangeDamage(...))` fails to compile
   (good), but a bare statement call compiles and discards the result.

*Recommendation:* do not add a `bool`-returning `TryChangeDamage(Entity<DamageableComponent?>, ...)`
extension to the compat layer — an extension method never wins over an applicable instance method, so
the shim would be dead code while the instance overload silently absorbs every call. The only safe
route for vendored files that call `TryChangeDamage` is an explicitly named shim
(`_damage.WolfmedChangeDamage(...)`) plus a `// WOLFGATE` edit at each call site.

---

## 1. `WoundSystem.cs` (466 lines)

### 1.1 Purpose and internal dependencies

Owns the lifecycle of wound entities: it indexes every `WoundPrototype` by damage type at startup,
converts a `PartDamageAppliedEvent` into created/merged/healed wounds on the struck part, and exposes
the public severity/state/treatment API (`CreateOrMergeWound`, `ChangeSeverity`, `TreatWound`,
`SetWoundState`, `RemoveWound`, `ClearWounds`, `TryHealWounds`, `GetHealingPotential`). Wounds are
stored as nullspace entities inside a `"wounds"` container on the `WoundableComponent` part, and
severity changes re-sync the optional runtime bricks (`WoundBleedingComponent`,
`WoundInternalBleedingComponent`, `WoundFunctionalityComponent`).

Depends on Onyx files: `WoundDamageComponents.cs`, `WoundEvents.cs`, `WoundBehaviors.cs`,
`WoundPrototype.cs`.

### 1.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `SharedBodySystem` | `Content.Shared.Body.Systems` | `[Dependency] private SharedBodySystem _body` (`:19`) | **SAME** — `WG/Content.Shared/Body/Systems/SharedBodySystem.cs:11` `public abstract partial class SharedBodySystem : EntitySystem` |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | Onyx `_Onyx/Body/Systems/SharedBodySystem.cs:133` `public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body)`; call `WoundSystem.cs:127` | **SAME (call-compatible)** — `WG/.../SharedBodySystem.Body.cs:256` `public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid? id, BodyComponent? body = null, BodyPartComponent? rootPart = null)`. Extra params are optional; `EntityUid`→`EntityUid?` is implicit; identical element type. Both include the root part. |
| `BodyComponent` | Onyx `Content.Shared.Body` (`Content.Shared/Body/BodyComponent.cs:13`) / **WG `Content.Shared.Body.Components`** (`BodyComponent.cs:12`) | `SubscribeLocalEvent<BodyComponent, RejuvenateEvent>` (`:34`) | **DIFFERENT (namespace)** — the type exists but `using Content.Shared.Body;` (`:2`) will not bring it into scope in Wolfgate. Needs `using Content.Shared.Body.Components;`. |
| `RejuvenateEvent` | `Content.Shared.Rejuvenate` | `sealed class RejuvenateEvent : EntityEventArgs` | **SAME** — `WG/Content.Shared/Rejuvenate/RejuvenateEvent.cs:1,3` |
| `DamageSpecifier` / `.DamageDict` | `Content.Shared.Damage` | `:69, 403, 437` | **SAME** — `WG/Content.Shared/Damage/DamageSpecifier.cs:20` `public sealed partial class DamageSpecifier` |
| `DamageTypePrototype` | `Content.Shared.Damage.Prototypes` | `ProtoId<DamageTypePrototype>` keys (`:25, 109`) | **SAME** — `WG/Content.Shared/Damage/Prototypes/` |
| `FixedPoint2` (`.Zero`, `.Min`, `.Max`, `.MaxValue`, `.Float()`) | `Content.Shared.FixedPoint` | pervasive | **SAME** — `WG/Content.Shared/FixedPoint/FixedPoint2.cs:19, 21, 53, 186, 215, 220` |
| `SharedContainerSystem`, `Container` | `Robust.Shared.Containers` | `:20, 61, 205` | **SAME** — see §0.2 |
| `INetManager`, `IPrototypeManager`, `IRobustRandom` | `Robust.Shared.*` | `:21-23` | **SAME** |
| `MapCoordinates.Nullspace` | `Robust.Shared.Map` | `:195` | **SAME** |
| `PrototypesReloadedEventArgs` | `Robust.Shared.Prototypes` | `:32, 38, 40` | **SAME** — `IPrototypeManager.cs:643` |
| `using Content.Shared.Damage.Systems;` (`:6`) | — | unused in this file | **SAME** — namespace exists in Wolfgate (e.g. `WG/Content.Shared/Damage/Systems/StaminaSystem.cs`); harmless |
| `using Content.Shared.Body;` (`:2`) | — | | **SAME as a namespace** (C# permits importing a namespace that only contains child namespaces) — but see the `BodyComponent` row; the *using* compiles, the *type reference* does not |

### 1.3 Adaptations

**`BodyComponent` namespace (DIFFERENT).**
- (a) *Shim keeping the file verbatim*: not achievable cleanly. A file-scoped alias cannot be injected
  from outside; a project-wide `global using BodyComponent = Content.Shared.Body.Components.BodyComponent;`
  would leak into all 1000+ shared files and is a maintenance hazard. **Do not do this.**
- (b) **Preferred — one-line `// WOLFGATE` edit** in the vendored file's using block:
  ```csharp
  using Content.Shared.Body;
  using Content.Shared.Body.Components; // WOLFGATE: BodyComponent lives here, not Content.Shared.Body
  using Content.Shared.Body.Systems;
  ```
  One added line, zero behavioural change, trivially re-appliable on an Onyx re-sync.
- (c) Upstream hook: move `BodyComponent` to `Content.Shared.Body`. Rejected — it is referenced by
  hundreds of Wolfgate files.

**`SubscribeLocalEvent<BodyComponent, RejuvenateEvent>` (BLOCKER — see §1.4).**

### 1.4 Subscriptions registered

| Pair | Line | Wolfgate conflict |
|---|---|---|
| broadcast `PrototypesReloadedEventArgs` | `:32` | none — broadcast subscriptions are a list, duplicates allowed |
| `<WoundableComponent, ComponentInit>` | `:33` | none — new component |
| `<BodyComponent, RejuvenateEvent>` | `:34` | **CONFLICT** |
| `<WoundableComponent, RejuvenateEvent>` | `:35` | none — new component |

**The conflict is a hard server-start crash.** `WG/Content.Server/_Mono/Body/Systems/BodyRejuvenateSystem.cs:28`
already registers:

```csharp
SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
```

`WG/RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Directed.cs:418-419`:

```csharp
if (!_eventSubsUnfrozen[compType.Value]!.TryAdd(eventType, reg))
    throw new InvalidOperationException($"Duplicate Subscriptions for comp={compTypeObj}, event={eventType.Name}");
```

`_eventSubsUnfrozen` is keyed by component type and holds **one** registration per event type for the
whole bus — it is *not* per-system. A shared system's directed subscription registers on the server
bus too, so both handlers land on the same slot and the server throws at `Initialize`.

*Adaptation:*
- (a) **Preferred shim, vendored file stays verbatim**: change the *Wolfgate* side. Move the body of
  `BodyRejuvenateSystem.OnRejuvenate` into a method invoked from a new `_WF/Wolfmed` relay, or simpler:
  give `BodyRejuvenateSystem` a `// WOLFGATE` two-line change from a directed subscription to a
  broadcast one it filters itself. Cleanest concrete form — a single `_WF` system owns the pair and
  fans out:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/BodyRejuvenateRelaySystem.cs
  namespace Content.Shared._WF.Wolfmed.Compat;

  /// <summary>Raised on a body when it is rejuvenated. Owns the one BodyComponent+RejuvenateEvent slot.</summary>
  [ByRefEvent]
  public readonly record struct WolfmedBodyRejuvenateEvent(EntityUid Body);

  public sealed class BodyRejuvenateRelaySystem : EntitySystem
  {
      public override void Initialize()
      {
          SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
      }

      private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
      {
          var ev = new WolfmedBodyRejuvenateEvent(ent.Owner);
          RaiseLocalEvent(ent.Owner, ref ev);
      }
  }
  ```
  Then `BodyRejuvenateSystem.cs:28` becomes `SubscribeLocalEvent<BodyComponent, WolfmedBodyRejuvenateEvent>(OnRejuvenate)`
  — **but that is the same trap again**: two systems subscribing `<BodyComponent, WolfmedBodyRejuvenateEvent>`.
  So the relay must fan out to *distinct* component types. Practical version: relay to
  `<WoundHostComponent, WolfmedBodyRejuvenateEvent>` (consumed by `WoundSystem`) and leave
  `BodyRejuvenateSystem` on the original pair.
- (b) **Simplest and recommended — one-line `// WOLFGATE` edit inside the vendored `WoundSystem.cs`**:
  ```csharp
  SubscribeLocalEvent<WoundHostComponent, RejuvenateEvent>(OnRejuvenate); // WOLFGATE: BodyComponent+RejuvenateEvent is taken by _Mono BodyRejuvenateSystem
  ```
  and change the handler signature from `Entity<BodyComponent> body` to `Entity<WoundHostComponent> body`.
  This is *semantically equivalent*: `OnRejuvenate` (`:122-138`) immediately does
  `if (!_net.IsServer || !HasComp<WoundHostComponent>(body)) return;`, so the only bodies it acts on
  are exactly the `WoundHostComponent` ones. The `HasComp` guard then becomes redundant (leave it —
  it costs nothing and keeps the diff to one line). **Note:** `WoundStatusEffectSystem.cs:30` already
  subscribes `<WoundHostComponent, RejuvenateEvent>` in Onyx, so this edit would itself collide —
  the edit must instead target a third, wounds-owned component, or `WoundStatusEffectSystem`'s
  subscription must be folded in. Verify against wounds-c's findings before committing.
- (c) Upstream hook: delete `_Mono/Body/Systems/BodyRejuvenateSystem.cs` and let Onyx's rejuvenate
  path restore parts. Viable only after phase 3 (amputation) lands; out of scope for phase 1.

### 1.5 Networking / prediction

Server-only in effect (§0.1). No dependency of this file is server-only in Wolfgate — `SharedBodySystem`,
containers, prototypes and `RejuvenateEvent` are all shared. **This file compiles into the client
without a bloodstream bridge.**

---

## 2. `WoundPrototype.cs` (309 lines)

### 2.1 Purpose and internal dependencies

Pure data definitions: `WoundPrototype` (damage-type→severity conversion, merge mode, stages, behavior
bricks, with the `GetStage` / `GetStageDefinition` / `GetBehaviors` / `TryGetBehavior<T>` lookup helpers),
`BodyPartProfilePrototype` (per-species part rules: accepted damage types, supported wounds, treatment
capabilities, bleed multiplier, scarrable, pain, passive/bed recovery, organ-damage routing), and
`FractureProfilePrototype` plus the `WoundMergeMode` / `WoundVisibility` / `TreatmentCapability` /
`OrganDamageRouting` / `FractureGradeSettings` supporting types. No behaviour, no systems.

Depends on Onyx files: `WoundBehaviors.cs` (`WoundBehavior`, `WoundStageDefinition`),
`WoundDamageComponents.cs` (`FractureGrade`, `FractureTreatment`),
`_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs`.

### 2.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `DamageTypePrototype` | `Content.Shared.Damage.Prototypes` | `Dictionary<ProtoId<DamageTypePrototype>, WoundDamageTypeSettings>` (`:26`), `HashSet<ProtoId<DamageTypePrototype>>` (`:145`), `ProtoId<DamageTypePrototype> DamageType = "Blunt"` (`:217`) | **SAME** |
| `AlertPrototype` | `Content.Shared.Alert` | `ProtoId<AlertPrototype>? Alert = "BrokenBones"` (`:244`) | **SAME (type)** — `WG/Content.Shared/Alert/AlertPrototype.cs:10`. **Prototype `BrokenBones` must be checked separately** — it is an Onyx YAML asset, not a Wolfgate one. |
| `BodyPartType` | `Content.Shared.Body.Part` | `Dictionary<BodyPartType, float> Chances` (`:195`) | **DIFFERENT** — see §0.4. This file only uses it as a dictionary key type, so *this file* compiles; the YAML that populates it (`Chest`, `Groin` keys) will not deserialize. |
| `CirculatoryStreamPrototype` | `Content.Shared._Onyx.Chemistry.Circulation` | `ProtoId<CirculatoryStreamPrototype> CirculatoryStream = "Organic"` (`:157`) | **MISSING** — no Circulation package in Wolfgate |
| `FixedPoint2` | `Content.Shared.FixedPoint` | `:32, 60, 127, 143, 229, 232, 273, 289` | **SAME** |
| `LocId` | `Robust.Shared.Prototypes` | `:20, 134, 139` | **SAME** |
| `[Prototype]`, `[IdDataField]`, `[DataField]`, `[DataDefinition]` | `Robust.Shared.*` | | **SAME** — §0.2 |
| `[Serializable, NetSerializable]` | `Robust.Shared.Serialization` | `:202, 297, 304` | **SAME** |
| `using Content.Shared.Body;` (`:3`) | — | unused | **SAME as a namespace** |

Locale keys: `Name` is `LocId` on `WoundPrototype` (`:20`) and `WoundStageDefinition` (`WoundBehaviors.cs:134`),
plus `ExamineDescription` (`WoundBehaviors.cs:138`). Their values live in
`Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` (per the handoff) — **MISSING** from
Wolfgate, ports as an asset alongside `Resources/Prototypes/_Onyx/Wounds/wounds.yml`.

### 2.3 Adaptations

**`CirculatoryStreamPrototype` (MISSING).** This file needs only the *type* — one `ProtoId<>` field.
- (a) **Preferred — minimal vendored type, no behaviour.** Port only
  `_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs` verbatim into
  `Content.Shared/_Onyx/Chemistry/Circulation/`. It is an `IPrototype` with no system dependencies,
  so it costs nothing and keeps `WoundPrototype.cs` byte-identical. Only `WoundBleedingSystem` needs
  the *system*; see §4.
- (b) not needed.
- (c) not needed.

**`BodyPartType.Chest` / `.Groin`** — see §0.4(b); nothing to do inside this file.

### 2.4 Subscriptions registered

**None.** This file declares no system.

### 2.5 Networking / prediction

Prototypes only; loaded on both sides identically. No server-only dependency.

**Port difficulty: verbatim** (given `CirculatoryStreamPrototype` is vendored alongside).

---

## 3. `WoundHealingSystem.cs` (165 lines)

### 3.1 Purpose and internal dependencies

Bridges medical items to the wound layer: it answers `ResolveHealingPartEvent` by scoring every body
part against the item's healing `DamageSpecifier` (raw damage repaired + wound-healing potential +
bleeding reduction) and picking the best, then `TryApplyHealing` routes the actual change through
`WoundDamageRoutingSystem`/`WoundSystem` and reports back what was healed and whether bleeding stopped.
It also exposes the `IsCompatiblePart` treatment-capability check used by the surgery/item layers.

Depends on Onyx files: `WoundSystem.cs`, `WoundBleedingSystem.cs`, `WoundDamageRoutingSystem.cs`,
`WoundDamageComponents.cs`, `WoundEvents.cs`, `WoundPrototype.cs`,
`_Onyx/Medical/Healing/HealingComponent.Onyx.cs`.

### 3.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `:58` | **SAME** — §1.2 |
| `SharedBodySystem.BodyHasChild` | `Content.Shared.Body.Systems` | Onyx `_Onyx/Body/Systems/SharedBodySystem.cs:191` `public bool BodyHasChild(EntityUid body, EntityUid part)`; call `:157` | **SAME (call-compatible)** — `WG/.../SharedBodySystem.Parts.cs:978` `public bool BodyHasChild(EntityUid bodyId, EntityUid partId, BodyComponent? body = null, BodyPartComponent? part = null)`. Semantics differ slightly: Onyx does `GetBodyChildren(body).Any(child => child.Id == part)`; Wolfgate walks `PartHasChild` from the root. Same answer for attached parts. |
| `DamageableSystem` | Onyx `Content.Shared.Damage.Systems` (`DamageableSystem.API.cs:7`) / WG `Content.Shared.Damage` (`DamageableSystem.cs:23`) | `[Dependency] private DamageableSystem _damage` (`:18`) | **SAME (resolves)** — the file has *both* `using Content.Shared.Damage;` (`:4`) and `using Content.Shared.Damage.Systems;` (`:7`), so the type binds in Wolfgate without an edit |
| `DamageableSystem.GetPositiveDamage(Entity<DamageableComponent>)` | ↑ | `:65, 118, 135`; Onyx `DamageableSystem.API.cs:327` `public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent)` | **MISSING** — grep of `WG/Content.Shared` + `WG/Content.Server` for `GetPositiveDamage`: zero hits |
| `DamageableSystem.UniversalTopicalsHealModifier` | ↑ | `:117` | **SAME** — `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:49` `public float UniversalTopicalsHealModifier { get; private set; } = 1f;` |
| `DamageableComponent` | `Content.Shared.Damage` | `:61, 107` | **SAME** |
| `DamageSpecifier`, `operator *(DamageSpecifier, float)` | `Content.Shared.Damage` | `:102, 117` | **SAME** — `WG/Content.Shared/Damage/DamageSpecifier.cs:370` |
| `DamageContainerPrototype` | `Content.Shared.Damage.Prototypes` | `IReadOnlyList<ProtoId<DamageContainerPrototype>>?` (`:44, 154`) | **SAME (type)** — `WG/Content.Shared/Damage/Prototypes/DamageContainerPrototype.cs:17`. **DIFFERENT at the call site**: Wolfgate's `HealingComponent.DamageContainers` is `List<string>?` (see below), not `List<ProtoId<DamageContainerPrototype>>?` |
| `HealingComponent` | Onyx `Content.Shared.Medical.Healing` (`Content.Shared/Medical/Healing/HealingComponent.cs:7`) | `Entity<HealingComponent> healing` (`:96`) plus `.HealDamage`, `.HealWounds`, `.BloodlossModifier`, `.ModifyBloodLevel`, `.Damage`, `.DamageContainers`, `.TreatmentCapabilities`, `.AllowedWoundStages` | **MISSING from shared / DIFFERENT** — Wolfgate's lives at `WG/Content.Server/Medical/Components/HealingComponent.cs:12`, namespace `Content.Server.Medical.Components`, `[RegisterComponent]` only (not networked). Namespace `Content.Shared.Medical.Healing` **does not exist** in Wolfgate (0 files), so `using Content.Shared.Medical.Healing;` (`:9`) is itself CS0246. |
| `HealingComponent.HealDamage` / `.HealWounds` / `.TreatmentCapabilities` / `.AllowedWoundStages` | Onyx `_Onyx/Medical/Healing/HealingComponent.Onyx.cs:11, 15, 18, 21` | `bool HealDamage = true`, `bool HealWounds = true`, `HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]`, `HashSet<string>? AllowedWoundStages` | **MISSING** — no equivalent anywhere in Wolfgate |
| `HealingComponent.Damage` | | Onyx: `[DataField(required: true), AutoNetworkedField] public DamageSpecifier Damage` | **SAME shape** — WG `HealingComponent.cs:16` `[DataField("damage", required: true)] public DamageSpecifier Damage = default!;` |
| `HealingComponent.BloodlossModifier` / `.ModifyBloodLevel` | | `float` both | **SAME** — WG `:24, 31` |
| `HealingComponent.DamageContainers` | | Onyx `HealingComponent.cs:40` `public List<ProtoId<DamageContainerPrototype>>? DamageContainers;` | **DIFFERENT** — WG `HealingComponent.cs:39` `public List<string>? DamageContainers;` with `customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>)`. `List<string>` is *not* assignable to `IReadOnlyList<ProtoId<DamageContainerPrototype>>`. |
| `HealingComponent.Delay` | | Onyx: `TimeSpan Delay = TimeSpan.FromSeconds(3f)` | **DIFFERENT** — WG `:46` `public float Delay = 2f;`. Not used by this file, but relevant to the `HealingSystem` hook. |
| `ResolveHealingPartEvent` | `Content.Shared._Onyx.Wounds` (`WoundEvents.cs:122`) | `:28, 110` | Onyx-internal (ships with the port) |
| `WoundBleedingSystem.GetPartRate` / `.ReducePartBleeding` | `Content.Shared._Onyx.Wounds` | `:77, 89, 148` | Onyx-internal, **but see §4 — that system does not compile on the client in Wolfgate** |
| `WoundDamageRoutingSystem.TryApplyPartDamage` / `.TryApplyDamage` | `Content.Shared._Onyx.Wounds` | `:123, 128` | Onyx-internal (wounds-a/c scope) |
| `INetManager`, `IPrototypeManager` | `Robust.Shared.*` | `:22-23` | **SAME** |
| `using Content.Shared.Body.Components;` (`:2`) | | | **SAME** — namespace exists in Wolfgate |
| `using Content.Shared.Damage.Components;` (`:5`) | | unused here | **SAME** — namespace exists (`WG/Content.Shared/Damage/Components/*.cs`) |
| `System.Linq.Any` | | `:36` | **SAME** |

### 3.3 Adaptations

**`HealingComponent` (MISSING from shared).** This is the second-biggest blocker after bloodstream.
- (a) **Preferred — a shared Wolfmed treatment component + a compat facade, vendored file kept verbatim.**
  Create `Content.Shared/_WF/Wolfmed/Compat/HealingComponent.cs` declaring a *shared, networked*
  component **in namespace `Content.Shared.Medical.Healing`** with exactly Onyx's field set plus
  Onyx's partial fields, registered under a non-colliding proto name:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/HealingComponent.cs
  namespace Content.Shared.Medical.Healing;

  /// <summary>Shared mirror of the server HealingComponent, carrying the fields Onyx wound healing needs.</summary>
  [RegisterComponent, NetworkedComponent, AutoGenerateComponentState, ComponentProtoName("WolfmedHealing")]
  public sealed partial class HealingComponent : Component
  {
      [DataField(required: true), AutoNetworkedField] public DamageSpecifier Damage = default!;
      [DataField, AutoNetworkedField] public float BloodlossModifier;
      [DataField, AutoNetworkedField] public float ModifyBloodLevel;
      [DataField, AutoNetworkedField] public List<ProtoId<DamageContainerPrototype>>? DamageContainers;
      [DataField, AutoNetworkedField] public bool HealDamage = true;
      [DataField, AutoNetworkedField] public bool HealWounds = true;
      [DataField, AutoNetworkedField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
      [DataField, AutoNetworkedField] public HashSet<string>? AllowedWoundStages;
  }
  ```
  `ComponentProtoName` is available (`WG/RobustToolbox/Robust.Shared/GameObjects/ComponentAttributes.cs:20`,
  used e.g. by `ContainerManagerComponent`), which avoids the `Healing` name clash with
  `Content.Server.Medical.Components.HealingComponent`. Downside: YAML must add a second
  `- type: WolfmedHealing` block to every medical item. That is a prototype-level cost, not a code one,
  and it is *exactly* the D2 pattern (wound behaviour is opt-in per entity).
  This makes `WoundHealingSystem.cs` compile **verbatim** — the only source change is the type's
  `List<…>` vs Onyx's `IReadOnlyList<…>` parameter, which is fine because `List<T>` implements
  `IReadOnlyList<T>`.
- (b) `// WOLFGATE` edits inside `WoundHealingSystem.cs`: replace `using Content.Shared.Medical.Healing;`
  with `using Content.Shared._WF.Wolfmed;` and `Entity<HealingComponent>` with
  `Entity<WolfmedTreatmentComponent>`, i.e. 2 lines. Slightly smaller YAML footprint if you name the
  component naturally, but it makes the vendored file diverge on a public signature that Onyx's own
  `HealingSystem` also calls — worse for re-syncs. Prefer (a).
- (c) Upstream hook: move `WG/Content.Server/Medical/Components/HealingComponent.cs` to
  `Content.Shared/Medical/Healing/`, networked, and change `List<string>? DamageContainers` to
  `List<ProtoId<DamageContainerPrototype>>?`. Cleanest end state and matches Onyx exactly, but it
  touches `WG/Content.Server/Medical/HealingSystem.cs` and every `HealingComponent` YAML/serializer
  reference. This is the right phase-4 move; too large for phase 1.

**`DamageableSystem.GetPositiveDamage` (MISSING).**
- (a) **Preferred — extension-method shim, vendored file verbatim.** Extension methods on a system
  instance are found through a namespace import the vendored file already has (`using Content.Shared.Damage;`
  at `:4`), and there is **no** competing instance method to shadow them (confirmed: zero hits for
  `GetPositiveDamage` in Wolfgate):
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/DamageableSystemExtensions.cs
  namespace Content.Shared.Damage;

  /// <summary>Onyx-era DamageableSystem helpers Wolfgate's DamageableSystem does not have.</summary>
  public static class WolfmedDamageableExtensions
  {
      /// <summary>Returns only the entity's positive damage entries.</summary>
      public static DamageSpecifier GetPositiveDamage(this DamageableSystem system, Entity<DamageableComponent> ent);

      /// <summary>Returns only the entity's positive damage entries within one damage group.</summary>
      public static DamageSpecifier GetPositiveDamage(this DamageableSystem system,
          Entity<DamageableComponent> ent, ProtoId<DamageGroupPrototype> group);
  }
  ```
  Bodies copied from Onyx `DamageableSystem.API.cs:302-344`. The `group` overload needs an
  `IPrototypeManager` — take it via `IoCManager.Resolve<IPrototypeManager>()` cached in a static, or
  drop that overload (this file only uses the one-arg form).
  `DamageGroupPrototype` confirmed present at `WG/Content.Shared/Damage/Prototypes/DamageGroupPrototype.cs:16`.
- (b) not needed.
- (c) not needed.

**`HealingComponent.DamageContainers` type (DIFFERENT).** Solved by (a) above. If (c) is chosen
instead, the serializer attribute must be dropped (`ProtoId<T>` serializes natively in RT 277).

### 3.4 Subscriptions registered

| Pair | Line | Wolfgate conflict |
|---|---|---|
| `<WoundHostComponent, ResolveHealingPartEvent>` | `:28` | none — both types are new |

### 3.5 Networking / prediction

Server-gated at `:104` (`TryApplyHealing`), but `OnResolveHealingPart` (`:31-38`) and the scoring in
`ResolveHealingPart` run on both sides. **Server-only dependencies reached transitively:**

- `WoundBleedingSystem` (`:19`, calls at `:77, 89, 148`) — that system cannot compile on the client
  until the bloodstream bridge exists (§4). This makes `WoundHealingSystem` **transitively blocked**.
- `HealingComponent` — server-only today (§3.3).
- `BedSystem`/`HealOnBuckleComponent` is *not* referenced here (it is referenced only by the
  `BodyPartProfilePrototype.BedRecoveryMultiplier` field, which is inert data in this file).

**Port difficulty: heavy adaptation** — blocked on both `HealingComponent` and `WoundBleedingSystem`.

---

## 4. `WoundBleedingSystem.cs` (472 lines)

### 4.1 Purpose and internal dependencies

Runs external bleeding: it attaches/updates `WoundBleedingComponent` rates from the wound's
`WoundBleedingBehavior` and the part's `BodyPartProfilePrototype.BleedingMultiplier`, applies the
treatment multiplier (None 1.0 / Bandaged 0.25 / Clamped, Sutured, Cauterized 0), runs the CVar-driven
automatic-clotting timer, and aggregates per-part and per-circulatory-stream rates back into the
bloodstream on every change. It also converts incoming cauterising damage into bleed reduction with a
popup and sound, and exposes the body-level `ModifyBodyBleeding` / `StopBodyBleeding` /
`ReducePartBleeding` / `TreatPart` / `GetPartRate` API used by items, surgery and `WoundHealingSystem`.

Depends on Onyx files: `WoundSystem.cs`, `WoundDamageComponents.cs`, `WoundEvents.cs`,
`WoundBehaviors.cs`, `WoundPrototype.cs`, `_Onyx/Chemistry/Circulation/*`, `_Onyx/CCVar/CCVars.Wounds.cs`.

### 4.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `BloodstreamSystem` | Onyx `Content.Shared.Body.Systems` (`Content.Shared/Body/Systems/BloodstreamSystem.cs:33`) | `[Dependency] private BloodstreamSystem _bloodstream` (`:25`) — **declared but never called in this file** | **MISSING from shared** — Wolfgate's is `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:27`, namespace `Content.Server.Body.Systems`, 517 lines |
| `BloodstreamComponent` | Onyx `Content.Shared.Body.Components` (`BloodstreamComponent.cs:13`) | `TryComp(args.Body, out BloodstreamComponent? bloodstream)` (`:90`), `TryComp(body, out BloodstreamComponent? bloodstream)` (`:294`) | **MISSING from shared** — Wolfgate's is `WG/Content.Server/Body/Components/BloodstreamComponent.cs:13`, namespace `Content.Server.Body.Components` |
| `BloodstreamComponent.DamageBleedModifiers` | ↑ | Onyx `:118` `public ProtoId<DamageModifierSetPrototype> DamageBleedModifiers = "BloodlossHuman";` | **SAME field, wrong assembly** — WG `BloodstreamComponent.cs:100` is byte-identical |
| `BloodstreamComponent.BloodHealedSound` | ↑ | Onyx `:130` `public SoundSpecifier BloodHealedSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");` | **SAME field, wrong assembly** — WG `:112` identical |
| `BloodstreamComponent.BloodHealedSoundThreshold` | ↑ | Onyx `:137` `public float BloodHealedSoundThreshold = -0.1f;` | **SAME field, wrong assembly** — WG `:119` identical |
| `CirculatoryStreamSystem.GetPartStream` | `Content.Shared._Onyx.Chemistry.Circulation` | `CirculatoryStreamSystem.cs:384` `public ProtoId<CirculatoryStreamPrototype> GetPartStream(Entity<WoundableComponent> part)`; calls `:305, 318` | **MISSING** |
| `CirculatoryStreamSystem.SetBleedRates` | ↑ | `CirculatoryStreamSystem.cs:439` `public void SetBleedRates(EntityUid body, Dictionary<ProtoId<CirculatoryStreamPrototype>, float> rates)`; call `:323` | **MISSING** |
| `CirculatoryStreamPrototype` | ↑ | `:298` dictionary key | **MISSING** (see §2.3(a)) |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `:177, 436` | **SAME** — §1.2 |
| `BodyPartComponent` / `.Body` | `Content.Shared.Body.Part` | `:456` `partComp.Body is not { } attachedBody` | **SAME** — `WG/Content.Shared/Body/Part/BodyPartComponent.cs:16` (ns), `:27` `public EntityUid? Body;` |
| `SleepStateChangedEvent` | `Content.Shared.Bed.Sleep` | Onyx `Content.Shared/Bed/Sleep/SleepingSystem.cs:388` `[ByRefEvent] public record struct SleepStateChangedEvent(bool FellAsleep);`; sub at `:44` | **SAME — byte-identical** — `WG/Content.Shared/Bed/Sleep/SleepingSystem.cs:372` with the same `[ByRefEvent]` at `:371`, same namespace (`:29`) |
| `SleepingComponent` | `Content.Shared.Bed.Sleep` | `HasComp<SleepingComponent>(patient)` (`:360, 426`) | **SAME** — `WG/Content.Shared/Bed/Sleep/SleepingComponent.cs:7, 14` |
| `DamageSpecifier.ApplyModifierSet(DamageSpecifier, DamageModifierSet)` | `Content.Shared.Damage` | `:94` | **SAME** — `WG/Content.Shared/Damage/DamageSpecifier.cs:133` |
| `DamageSpecifier.GetPositive(DamageSpecifier)` | `Content.Shared.Damage` | Onyx `DamageSpecifier.cs:194` `public static DamageSpecifier GetPositive(DamageSpecifier damageSpec)`; call `:95` | **MISSING** — WG `DamageSpecifier.cs` has `Clamp`/`ClampMin`/`ClampMax` (`:208, 221, 236`) but no `GetPositive` |
| `DamageSpecifier.GetTotal()` | `Content.Shared.Damage` | `:96` | **SAME** — `WG/Content.Shared/Damage/DamageSpecifier.cs:53` |
| `DamageModifierSetPrototype` | `Content.Shared.Damage.Prototypes` | `_prototypes.TryIndex(bloodstream.DamageBleedModifiers, out var modifiers)` (`:91`) then passed as a `DamageModifierSet` | **SAME** — `WG/Content.Shared/Damage/Prototypes/DamageModifierSetPrototype.cs:13` `public sealed partial class DamageModifierSetPrototype : DamageModifierSet, IPrototype` — inheritance holds, so the `ApplyModifierSet` call type-checks |
| `SharedPopupSystem.PopupEntity(string?, EntityUid, EntityUid, PopupType)` | `Content.Shared.Popups` | `:104` | **SAME** — `WG/Content.Shared/Popups/SharedPopupSystem.cs:91` |
| `PopupType.Medium` | `Content.Shared.Popups` | `:105` | **SAME** — `WG/Content.Shared/Popups/SharedPopupSystem.cs:211` |
| `SharedAudioSystem.PlayPredicted` | `Robust.Shared.Audio.Systems` | `:106` | **SAME** — §0.2 |
| Loc `bloodstream-component-wounds-cauterized` | — | `:104` | **SAME** — `WG/Resources/Locale/en-US/bloodstream/bloodstream.ftl:5` `bloodstream-component-wounds-cauterized = You feel your wounds painfully close!` |
| `CCVars.WoundsBleedingAutoStopEnabled` / `…SecondsPerSeverity` / `…MinSeconds` / `…MaxSeconds` | `Content.Shared.CCVar` | `:402-413` | **MISSING** — ships with `_Onyx/CCVar/CCVars.Wounds.cs` verbatim (§0.6) |
| `IConfigurationManager.GetCVar` | `Robust.Shared.Configuration` | `:402` | **SAME** |
| `System.Linq` (`Where`, `OrderByDescending`, `Select`, `ToArray`) | | `:188-191, 230-234` | **SAME** |

### 4.3 Adaptations

**`BloodstreamSystem` + `BloodstreamComponent` (MISSING from shared) — the hardest problem in this file.**

Note first that `_bloodstream` (`:25`) is **declared but never used** in `WoundBleedingSystem.cs`.
Only `BloodstreamComponent` is actually read, and only for three data fields at `:90-107` and one
existence test at `:294`. That makes the shim much cheaper than it first looks.

- (a) **Preferred — shared mirror component, vendored file verbatim.** Add
  `Content.Shared/_WF/Wolfmed/Compat/BloodstreamComponent.cs` declaring, **in namespace
  `Content.Shared.Body.Components`**, a component carrying exactly the three fields the wound code
  reads, registered under a distinct proto name so it cannot clash with Wolfgate's server
  `Bloodstream`:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/BloodstreamComponent.cs
  namespace Content.Shared.Body.Components;

  /// <summary>Shared mirror of the fields Onyx wound bleeding reads off the server bloodstream.</summary>
  [RegisterComponent, NetworkedComponent, AutoGenerateComponentState, ComponentProtoName("WolfmedBloodstream")]
  public sealed partial class BloodstreamComponent : Component
  {
      [DataField, AutoNetworkedField] public ProtoId<DamageModifierSetPrototype> DamageBleedModifiers = "BloodlossHuman";
      [DataField, AutoNetworkedField] public SoundSpecifier BloodHealedSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
      [DataField, AutoNetworkedField] public float BloodHealedSoundThreshold = -0.1f;
  }
  ```
  Plus a shared stub system so the `[Dependency]` at `:25` resolves:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/BloodstreamSystem.cs
  namespace Content.Shared.Body.Systems;

  /// <summary>Shared bloodstream facade. The server subclass forwards to Content.Server BloodstreamSystem.</summary>
  public abstract class BloodstreamSystem : EntitySystem
  {
      /// <summary>Removes (negative) or restores (positive) blood on the body.</summary>
      public abstract bool TryModifyBloodLevel(Entity<BloodstreamComponent?> ent, FixedPoint2 amount);

      /// <summary>Sets the body's aggregate external bleed rate.</summary>
      public abstract bool TryModifyWoundBleedProjection(Entity<BloodstreamComponent?> ent, float delta);
  }
  ```
  with `Content.Server/_WF/Wolfmed/Compat/ServerBloodstreamSystem.cs : BloodstreamSystem` forwarding to
  `Content.Server.Body.Systems.BloodstreamSystem.TryModifyBloodLevel(EntityUid, FixedPoint2, BloodstreamComponent?)`
  (`WG/Content.Server/Body/Systems/BloodstreamSystem.cs:363`) and
  `TryModifyBleedAmount(EntityUid, float, BloodstreamComponent?)` (`:406`), and a no-op client subclass.
  **Caveat that must be verified before committing:** a class named `BloodstreamSystem` in
  `Content.Shared.Body.Systems` is a *different type* from `Content.Server.Body.Systems.BloodstreamSystem`,
  but `EntitySystemManager` registers every system under its base types
  (`EntitySystemManager.cs:174-199`) — if a second subclass of the shared facade ever appears, the
  supertype registration is dropped and the `[Dependency]` fails at startup. One subclass per side is
  required. Also `Content.Server/Body/Systems/BloodstreamSystem.cs` imports
  `Content.Shared.Body.Systems`; introducing a shared type of the same simple name there will make
  every unqualified `BloodstreamSystem` reference in that file ambiguous (CS0104). **Pick a different
  facade name** — e.g. `WolfmedBloodstreamSystem` — and take option (b) for the `[Dependency]` line.
- (b) **Recommended in practice — bridge system + three `// WOLFGATE` lines in the vendored file.**
  Keep the mirror component from (a) (it is needed either way, because the server component is not
  visible from shared), and replace the dependency:
  ```csharp
  // WOLFGATE: BloodstreamSystem is server-only in Wolfgate; go through the shared facade.
  [Dependency] private SharedWolfmedBloodstreamSystem _bloodstream = default!;
  ```
  `WoundBleedingSystem` never calls `_bloodstream`, so that single line is the entire change here.
  (`WoundInternalBleedingSystem` does call it — see §5.)
- (c) Upstream hook: promote `WG/Content.Server/Body/{Components/BloodstreamComponent.cs,Systems/BloodstreamSystem.cs}`
  (517 + ~400 lines) into `Content.Shared/Body/{Components,Systems}`, networked, matching Onyx's layout.
  This is the only route that makes the whole Onyx bleeding stack byte-identical and is what Onyx
  itself did upstream. It is a large, risky change for phase 1 — schedule it as a separate task with
  its own integration-test pass.

**`DamageSpecifier.GetPositive` (MISSING).** A `static` member cannot be added by an extension method.
- (a) **Preferred — partial-class addition, vendored file verbatim.** `WG`'s `DamageSpecifier` is
  `public sealed partial class` (`WG/Content.Shared/Damage/DamageSpecifier.cs:20`), so a *new file*
  under `_WF` adds the static without editing any upstream file:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Wolfmed.cs
  namespace Content.Shared.Damage;

  public sealed partial class DamageSpecifier
  {
      /// <summary>Returns a copy containing only the entries with a positive value.</summary>
      public static DamageSpecifier GetPositive(DamageSpecifier damageSpec);

      /// <summary>Returns a copy containing only the entries with a negative value.</summary>
      public static DamageSpecifier GetNegative(DamageSpecifier damageSpec);
  }
  ```
  Bodies copied verbatim from Onyx `Content.Shared/Damage/DamageSpecifier.cs:194-205` (and the
  matching negative helper below it). Note that the partial file must be in the same *project*
  (`Content.Shared`) and namespace — both hold.
- (b) `// WOLFGATE` edit at `WoundBleedingSystem.cs:95` replacing `DamageSpecifier.GetPositive(args.Damage)`
  with a `_WF` helper call. Strictly worse than (a).
- (c) Add `GetPositive` to upstream `DamageSpecifier.cs` with a `// WOLFGATE` marker. Equivalent to (a)
  but touches an upstream file for no benefit.

**`CirculatoryStreamSystem` (MISSING).** This file needs exactly two methods.
- (a) **Preferred — a minimal `_WF` implementation under the Onyx namespace, vendored file verbatim.**
  Port `CirculatoryStreamPrototype.cs` verbatim (§2.3) and add:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/CirculatoryStreamSystem.cs
  namespace Content.Shared._Onyx.Chemistry.Circulation;

  /// <summary>Minimal stand-in for Onyx's circulation package: single primary stream, forwards bleed to the bloodstream.</summary>
  public sealed class CirculatoryStreamSystem : EntitySystem
  {
      /// <summary>Returns the circulatory stream a part drains into.</summary>
      public ProtoId<CirculatoryStreamPrototype> GetPartStream(Entity<WoundableComponent> part);

      /// <summary>Applies aggregated per-stream bleed rates to the body.</summary>
      public void SetBleedRates(EntityUid body, Dictionary<ProtoId<CirculatoryStreamPrototype>, float> rates);
  }
  ```
  `GetPartStream` returns `_prototypes.TryIndex(part.Comp.Profile, out var profile) ? profile.CirculatoryStream : "Organic"`.
  `SetBleedRates` sums the rates for the primary stream and calls the bloodstream facade's
  `TryModifyWoundBleedProjection` (server) — matching what Onyx's real `SetBleedRates`
  (`CirculatoryStreamSystem.cs:439-473`) does for the primary stream, minus the multi-stream solution
  plumbing. Phase-1-correct for organic humanoids (D3): the Onyx wound YAML only assigns the `Organic`
  stream to organic profiles.
- (b) not preferred.
- (c) **Full port of `_Onyx/Chemistry/Circulation` (4 files) — currently impossible.**
  `CirculatoryStreamSystem.cs:1-20` imports `Content.Shared.Metabolism` (**0 files in Wolfgate**),
  `Content.Shared.Bed.Components` (**0 files**), `Content.Shared.EntityEffects.Effects.EntitySpawning`
  (**0 files**), `Content.Shared.EntityEffects.Effects.Solution` (**0 files**) — the last two are the
  ECS `EntityEffect` rework that D5 already records as absent. A verbatim port would drag in the whole
  Onyx chemistry/metabolism stack. Defer to a later phase.

### 4.4 Subscriptions registered

| Pair | Line | Wolfgate conflict |
|---|---|---|
| `<WoundBleedingComponent, ComponentInit>` | `:38` | none — new component |
| `<WoundBleedingComponent, ComponentShutdown>` | `:39` | none |
| `<WoundBleedingComponent, WoundCreatedEvent>` | `:40` | none |
| `<WoundBleedingComponent, WoundChangedEvent>` | `:41` | none |
| `<WoundBleedingComponent, WoundStateChangedEvent>` | `:42` | none |
| `<WoundBleedingComponent, WoundRemovedEvent>` | `:43` | none |
| `<WoundHostComponent, SleepStateChangedEvent>` | `:44` | none — `WoundHostComponent` is new. Wolfgate's existing `SleepStateChangedEvent` directed subs are on other components (e.g. `<MobStateComponent, SleepStateChangedEvent>` at `WG/Content.Shared/Bed/Sleep/SleepingSystem.cs:50`), so no slot clash. |

No conflicts. Note `<WoundBleedingComponent, ComponentInit>` at `:38` together with the fact that
`OnBleedingInit` → `RestartAutomaticClotting` → `RecomputeAutomaticClotting` is `_net.IsServer`-gated
at `:397`: the component add on the client is inert, as intended.

### 4.5 Networking / prediction

Server-gated everywhere (§0.1). **Server-only Wolfgate dependencies reached:**
`BloodstreamSystem` (declared, unused) and `BloodstreamComponent` (read at `:90, 294`). Both are
`Content.Server` here. Without the mirror component from §4.3 this file **does not compile into
`Content.Client`**, which in turn blocks `WoundHealingSystem` (§3.5).

`BedSystem` is not referenced. `HealingSystem` is not referenced.

**Port difficulty: heavy adaptation.**

---

## 5. `WoundInternalBleedingSystem.cs` (81 lines)

### 5.1 Purpose and internal dependencies

Drains blood straight out of the bloodstream while an open internal-bleeding wound exists — no puddles,
no external bleed rate. It mirrors the wound's severity onto `WoundInternalBleedingComponent.Severity`
(zeroing it whenever the wound is not `Open`) and its `Update` loop applies
`Rate × Severity × frameTime` blood loss per tick.

Depends on Onyx files: `WoundDamageComponents.cs` (`WoundInternalBleedingComponent`, `WoundComponent`,
`WoundState`), `WoundEvents.cs`.

### 5.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `BloodstreamSystem` | Onyx `Content.Shared.Body.Systems` | `[Dependency] private BloodstreamSystem _bloodstream` (`:15`) | **MISSING from shared** — §4.2 |
| `BloodstreamSystem.TryModifyBloodLevel` | ↑ | Onyx `Content.Shared/Body/Systems/BloodstreamSystem.cs:425` `public bool TryModifyBloodLevel(Entity<BloodstreamComponent?> ent, FixedPoint2 amount)`; call `:67` `_bloodstream.TryModifyBloodLevel((body, bloodstream), -amount)` | **DIFFERENT + wrong assembly** — WG `Content.Server/Body/Systems/BloodstreamSystem.cs:363` `public bool TryModifyBloodLevel(EntityUid uid, FixedPoint2 amount, BloodstreamComponent? component = null)`. **Overload trap:** if the server component were visible, `(body, bloodstream)` is an `Entity<BloodstreamComponent>` which converts implicitly to `EntityUid`, so the Onyx call shape **would compile** against Wolfgate's signature — it would just re-resolve the component internally. Same behaviour, but the compiler gives you no warning that the signature changed. |
| `BloodstreamComponent` | Onyx `Content.Shared.Body.Components` | `TryComp(body, out BloodstreamComponent? bloodstream)` (`:63`) | **MISSING from shared** — §4.2 |
| `BodyPartComponent` / `.Body` | `Content.Shared.Body.Part` | `:75` | **SAME** — `WG/Content.Shared/Body/Part/BodyPartComponent.cs:27` |
| `FixedPoint2.Zero`, `.New(float)`, `.Float()` | `Content.Shared.FixedPoint` | `:30, 39, 47, 60, 65, 66` | **SAME** |
| `INetManager.IsServer` | `Robust.Shared.Network` | `:27, 36, 45, 54` | **SAME** |
| `EntityQueryEnumerator<T1, T2>` | `Robust.Shared.GameObjects` | `:57` | **SAME** |
| `using Content.Shared.Body.Systems;` (`:3`) | — | for `BloodstreamSystem` | namespace exists in WG, the *type* does not |

### 5.3 Adaptations

**`BloodstreamSystem.TryModifyBloodLevel` (MISSING from shared / DIFFERENT).** Unlike §4, this file
genuinely *calls* the system, so the facade must have a real method.

- (a) **Preferred — shared facade with Onyx's exact signature, vendored file verbatim.** Building on
  §4.3(a), the shared facade must expose:
  ```csharp
  // Content.Shared/_WF/Wolfmed/Compat/WolfmedBloodstreamSystem.cs
  namespace Content.Shared.Body.Systems;

  /// <summary>Shared bloodstream facade for vendored Onyx wound code.</summary>
  public abstract class SharedWolfmedBloodstreamSystem : EntitySystem
  {
      /// <summary>Removes (negative) or restores (positive) blood. Mirrors Onyx BloodstreamSystem.</summary>
      public abstract bool TryModifyBloodLevel(Entity<BloodstreamComponent?> ent, FixedPoint2 amount);
  }
  ```
  Server subclass:
  ```csharp
  // Content.Server/_WF/Wolfmed/Compat/WolfmedBloodstreamSystem.cs
  public sealed class WolfmedBloodstreamSystem : SharedWolfmedBloodstreamSystem
  {
      [Dependency] private Content.Server.Body.Systems.BloodstreamSystem _bloodstream = default!;

      public override bool TryModifyBloodLevel(Entity<BloodstreamComponent?> ent, FixedPoint2 amount)
          => _bloodstream.TryModifyBloodLevel(ent.Owner, amount);
  }
  ```
  Client subclass returns `false`. With this in place the *call site* at `:67` is unchanged — only the
  `[Dependency]` type name at `:15` differs, which is one `// WOLFGATE` line. Because the parameter
  type is `Entity<BloodstreamComponent?>` with `BloodstreamComponent` being the **shared mirror**
  (§4.3(a)), `TryComp(body, out BloodstreamComponent? bloodstream)` at `:63` also resolves without
  further edits.
- (b) **`// WOLFGATE` edit, exactly two lines**, if you want to skip the mirror component entirely:
  ```csharp
  [Dependency] private SharedWolfmedBloodstreamSystem _bloodstream = default!; // WOLFGATE: bloodstream is server-only here
  ```
  and at `:63-67`:
  ```csharp
              // WOLFGATE: BloodstreamComponent is server-only; the facade resolves it server-side.
              if (TryGetBody(core.HoldingPart, out var body))
              {
                  var amount = FixedPoint2.New(internalBleeding.Rate * internalBleeding.Severity.Float() * frameTime);
                  if (amount > FixedPoint2.Zero)
                      _bloodstream.TryModifyBloodLevel(body, -amount);
              }
  ```
  with the facade taking a bare `EntityUid`. This is the smallest total change (no mirror component,
  no `ComponentProtoName` trickery) and is my recommendation if `WoundBleedingSystem` also goes the
  (b) route — but then §4's three `BloodstreamComponent` field reads still need the mirror, so (a) and
  (b) converge.
- (c) Upstream hook: §4.3(c), promoting bloodstream to shared. Then this file is **verbatim**.

### 5.4 Subscriptions registered

| Pair | Line | Wolfgate conflict |
|---|---|---|
| `<WoundInternalBleedingComponent, WoundChangedEvent>` | `:20` | none — new component, new event |
| `<WoundInternalBleedingComponent, WoundStateChangedEvent>` | `:21` | none |
| `<WoundInternalBleedingComponent, WoundRemovedEvent>` | `:22` | none |

Style note: `Initialize()` at `:18` does **not** call `base.Initialize()` (unlike the other five files).
Harmless — `EntitySystem.Initialize()` is empty — but do not "fix" it in a vendored file.

### 5.5 Networking / prediction

Server-gated at `:27, 36, 45, 54`. The `Update` loop (`:52`) returns immediately on the client.
**Server-only dependency:** `BloodstreamSystem` + `BloodstreamComponent`, both actually used. Blocked
on the bridge, same as §4.

**Port difficulty: light edits** (2 `// WOLFGATE` lines) — *once the bloodstream facade from §4 exists*.

---

## 6. `WoundScarSystem.cs` (86 lines)

### 6.1 Purpose and internal dependencies

Rolls for a permanent scar whenever a wound closes or heals from an open state, gated on the wound's
peak severity against its `WoundScarBehavior.Threshold`, its `Chance`, and the global
`surgery.scar_chance` CVar. It also makes scars untreatable by cancelling any
`WoundTreatmentAttemptEvent` raised on a `WoundScarComponent`.

Depends on Onyx files: `WoundSystem.cs` (`CreateOrMergeWound`, `SetWoundState`),
`WoundDamageComponents.cs` (`WoundComponent`, `WoundScarComponent`, `WoundableComponent`, `WoundState`),
`WoundEvents.cs` (`WoundStateChangedEvent`, `WoundTreatmentAttemptEvent`, `ScarCreatedEvent`),
`WoundBehaviors.cs` (`WoundScarBehavior`), `WoundPrototype.cs` (`BodyPartProfilePrototype.Scarrable`),
`_Onyx/CCVar/CCVars.Surgery.cs`.

### 6.2 External symbols

| Symbol | Namespace | Onyx signature/usage | Wolfgate status |
|---|---|---|---|
| `BodyPartComponent` / `.Body` | `Content.Shared.Body.Part` | `TryComp(source.Comp.HoldingPart, out BodyPartComponent? part)` (`:68`), `part.Body` (`:81`) | **SAME** — `WG/Content.Shared/Body/Part/BodyPartComponent.cs:16, 27` |
| `CCVars.SurgeryScarChance` | `Content.Shared.CCVar` | Onyx `_Onyx/CCVar/CCVars.Surgery.cs:7-8` `CVarDef.Create("surgery.scar_chance", 0.35f, CVar.SERVER \| CVar.ARCHIVE)`; call `:48` | **MISSING** — ships with the vendored `CCVars.Surgery.cs`; no `surgery.scar_chance` in Wolfgate (§0.6) |
| `IConfigurationManager.GetCVar` | `Robust.Shared.Configuration` | `:48` | **SAME** |
| `IPrototypeManager.TryIndex` | `Robust.Shared.Prototypes` | `:39, 70` | **SAME** |
| `INetManager.IsServer` | `Robust.Shared.Network` | `:35, 66` | **SAME** |
| `IRobustRandom.Prob(float)` | `Robust.Shared.Random` | `:53` | **SAME** — extension at `RandomExtensions.cs:171`, `using Robust.Shared.Random;` present at `:6` |
| `Math.Clamp` | `System` | `:44, 48` | **SAME** |
| `ProtoId<WoundPrototype>` implicit from `"MedicalScarWound"` | `Robust.Shared.Prototypes` | `:12` | **SAME (type)**; the `MedicalScarWound` prototype is an Onyx YAML asset that must be ported |

**Note on `CCVars.Surgery.cs`:** vendoring it also brings `SurgerySelfEnabled` and whatever else the
file holds. Per D7 (phase 1 keeps Shitmed surgery) those extra CVars will be inert but harmless.
Check for a `surgery.self_enabled` collision against Wolfgate's Shitmed CVars before dropping it in —
my grep of `WG/Content.Shared/CCVar` found none, but Shitmed may declare its own elsewhere.

### 6.3 Adaptations

**`CCVars.SurgeryScarChance` (MISSING).**
- (a) **Preferred — vendor `_Onyx/CCVar/CCVars.Surgery.cs` verbatim** into
  `Content.Shared/_Onyx/CCVar/`. `CCVars` is `sealed partial` on both sides (§0.6); the Onyx file
  omits the `: CVars` base list, which C# allows. No source edit anywhere. Verified: zero name and
  zero CVar-string collisions.
- (b) not needed.
- (c) not needed.

Everything else in this file is SAME. **This is the cleanest of the six.**

### 6.4 Subscriptions registered

| Pair | Line | Wolfgate conflict |
|---|---|---|
| `<WoundComponent, WoundStateChangedEvent>` | `:23` | none — new component, new event. Cross-check within the port: `WoundBleedingSystem.cs:42` uses `<WoundBleedingComponent, …>`, `WoundInternalBleedingSystem.cs:21` uses `<WoundInternalBleedingComponent, …>`, `WoundDamageRoutingSystem` uses `<WoundableComponent, …>` — all distinct component types, no intra-port clash. |
| `<WoundScarComponent, WoundTreatmentAttemptEvent>` | `:24` | none |

I enumerated every `SubscribeLocalEvent<` in `C:/tmp/onyx/Content.Shared/_Onyx/Wounds/*.cs` (40 lines,
all unique): there are **no duplicate component+event pairs inside the Wounds package itself**.

### 6.5 Networking / prediction

Server-gated at `:35` and `:66`. `OnTreatmentAttempt` (`:59-62`) runs on both sides but only sets a
flag on a by-ref event. **No server-only Wolfgate dependency.** This file compiles into the client
as-is.

**Port difficulty: verbatim.**

---

## 7. Port difficulty summary

| File | Difficulty | Why |
|---|---|---|
| `WoundPrototype.cs` | **verbatim** | needs only `CirculatoryStreamPrototype` vendored alongside; `BodyPartType` gap is data-level, not code-level here |
| `WoundScarSystem.cs` | **verbatim** | needs only `_Onyx/CCVar/CCVars.Surgery.cs` vendored alongside |
| `WoundSystem.cs` | **light edits** | +1 `using Content.Shared.Body.Components;` line; +1 line changing the `<BodyComponent, RejuvenateEvent>` subscription to avoid the `_Mono` duplicate-subscription crash |
| `WoundInternalBleedingSystem.cs` | **light edits** | 2 `// WOLFGATE` lines — *but gated on the bloodstream facade existing* |
| `WoundBleedingSystem.cs` | **heavy adaptation** | shared `BloodstreamComponent` mirror + bloodstream facade + `DamageSpecifier.GetPositive` partial + `CirculatoryStreamSystem` stand-in |
| `WoundHealingSystem.cs` | **heavy adaptation** | shared `HealingComponent` (server-only today, and missing 4 fields) + `GetPositiveDamage` extension; transitively blocked by `WoundBleedingSystem` |

## 8. Wolfgate files that need hooks or edits

**Upstream `// WOLFGATE` edits (unavoidable):**

| File | Change | Reason |
|---|---|---|
| `WG/Content.Shared/Body/Part/BodyPartType.cs` (`:19`) | append `Chest,` and `Groin,` after `Tail` | Onyx `WoundHostComponent` field initialisers reference them; existing ordinals stay stable (§0.4) |
| `WG/Content.Server/_Mono/Body/Systems/BodyRejuvenateSystem.cs:28` | *or* the vendored `WoundSystem.cs:34` | resolve the duplicate `<BodyComponent, RejuvenateEvent>` directed subscription (§1.4) |

**New `_WF/Wolfmed/Compat` files (no upstream edit):**

| New file | Contents |
|---|---|
| `Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Wolfmed.cs` | `partial class DamageSpecifier` adding `static GetPositive` / `GetNegative` |
| `Content.Shared/_WF/Wolfmed/Compat/DamageableSystemExtensions.cs` | `GetPositiveDamage(this DamageableSystem, Entity<DamageableComponent>)` (+ group overload) |
| `Content.Shared/_WF/Wolfmed/Compat/BloodstreamComponent.cs` | shared mirror of `DamageBleedModifiers` / `BloodHealedSound` / `BloodHealedSoundThreshold`, `[ComponentProtoName("WolfmedBloodstream")]` |
| `Content.Shared/_WF/Wolfmed/Compat/WolfmedBloodstreamSystem.cs` | abstract shared facade: `TryModifyBloodLevel`, `TryModifyWoundBleedProjection` |
| `Content.Server/_WF/Wolfmed/Compat/WolfmedBloodstreamSystem.cs` | forwards to `Content.Server.Body.Systems.BloodstreamSystem` (`:363` `TryModifyBloodLevel`, `:406` `TryModifyBleedAmount`) |
| `Content.Client/_WF/Wolfmed/Compat/WolfmedBloodstreamSystem.cs` | no-op subclass |
| `Content.Shared/_WF/Wolfmed/Compat/HealingComponent.cs` | shared mirror of Onyx's `HealingComponent` + its `_Onyx` partial fields, `[ComponentProtoName("WolfmedHealing")]` |
| `Content.Shared/_WF/Wolfmed/Compat/CirculatoryStreamSystem.cs` | `GetPartStream` / `SetBleedRates` stand-in for the un-portable Circulation package |
| `Content.Shared/_WF/Wolfmed/Compat/TargetBodyPartBridge.cs` | `_Onyx.Targeting.TargetBodyPart` ↔ `_Shitmed.Targeting.TargetBodyPart` bit remap (`Chest`↔`Torso`) |

**Vendored Onyx files these six require outside `Wounds/`:**
`_Onyx/CCVar/CCVars.Wounds.cs`, `_Onyx/CCVar/CCVars.Surgery.cs`,
`_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs`, `_Onyx/Targeting/TargetBodyPart.cs`,
`_Onyx/Medical/Healing/HealingComponent.Onyx.cs` (folded into the compat mirror instead),
plus `Resources/Prototypes/_Onyx/Wounds/wounds.yml` and
`Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl`.

## 9. Things I could not verify

- The `BrokenBones` alert prototype (`WoundPrototype.cs:244`) — I checked the C# type exists in
  Wolfgate but not whether a `BrokenBones` alert prototype does; it is an Onyx YAML asset either way.
- Whether `_Onyx/CCVar/CCVars.Surgery.cs` in full (I read only its first 11 lines) collides with any
  Shitmed CVar outside `WG/Content.Shared/CCVar/`.
- `WoundDamageRoutingSystem`'s API surface (`TryApplyPartDamage`, `TryApplyDamage`) — out of scope
  here; `WoundHealingSystem` depends on it and wounds-a/c should confirm the signatures.
- `WoundStatusEffectSystem.cs:30` also subscribes `<WoundHostComponent, RejuvenateEvent>`, which
  constrains the §1.4(b) fix. Cross-check with whoever analysed the status-effect files.
