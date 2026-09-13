# Wolfmed port — dependency and API-gap analysis: `wounds-c`

Scope: the 12 Onyx files listed in the task, all under `C:/tmp/onyx/Content.Shared/_Onyx/Wounds/`.

- Onyx pin: `2f5bab9` (sparse checkout at `C:/tmp/onyx`; files outside the sparse set read with `git show HEAD:<path>`).
- Wolfgate worktree (`WG`): `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`.
- Engine: `WG/RobustToolbox` v277.0.0 (`RobustToolbox/MSBuild/Robust.Engine.Version.props:3`). Onyx is on 289.

Everything below was verified by reading the actual files. Where a path is not in the sparse checkout and `git show` was not run, it is marked **absent from sparse checkout**.

---

## 0. Cross-cutting findings (read this first)

These apply to several of the 12 files and are the real cost drivers.

### 0.1 Onyx's `SharedBodySystem` is itself an Onyx file — Wolfgate's is a different class

`C:/tmp/onyx/Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:12`

```csharp
namespace Content.Shared.Body.Systems;
public sealed partial class SharedBodySystem : EntitySystem
```

Nubody deleted upstream `SharedBodySystem`; Onyx re-declares a **sealed** one in the same namespace as a compatibility shim over its own part/organ graph. Wolfgate's is:

`WG/Content.Shared/Body/Systems/SharedBodySystem.cs:11`

```csharp
public abstract partial class SharedBodySystem : EntitySystem
```

Consequence: **do not vendor `_Onyx/Body/Systems/SharedBodySystem.cs`** — it would be a duplicate type in `Content.Shared.Body.Systems`. Every `_body.X(...)` call in the 12 files must bind to Wolfgate's `SharedBodySystem`. Per-method verdicts:

| Onyx call | Onyx signature (`_Onyx/Body/Systems/SharedBodySystem.cs`) | Wolfgate | Verdict |
|---|---|---|---|
| `_body.GetBodyChildren(body)` | `:133 public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body)` | `WG/Content.Shared/Body/Systems/SharedBodySystem.Body.cs:256 public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid? id, BodyComponent? body = null, BodyPartComponent? rootPart = null)` | **SAME at the call site** (extra params are optional; `EntityUid`/`Entity<T>` → `EntityUid?` implicit). Semantics differ subtly: Wolfgate walks the Shitmed part tree, Onyx walks Nubody's organ-unified tree, so Onyx's version also yields organs that carry `BodyPartComponent`. Behaviourally acceptable for Phase 1. |
| `_body.GetPartOrgans(part)` | `:424 public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid part)` | `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:825 public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)` | **SAME at the call site**, but the element type `OrganComponent` differs (see 0.3). |
| `_body.TryDetachPart(part)` | `:225 public bool TryDetachPart(EntityUid part, bool reparent = true)` | **MISSING.** Closest: `SharedBodySystem.Parts.cs:702 public bool DetachPart(EntityUid parentPartId, string slotId, EntityUid partId, BodyPartComponent? parentPart = null, BodyPartComponent? part = null)` and `:751 public bool DetachPart(EntityUid parentPartId, BodyPartSlot slot, EntityUid partId, ...)` | **MISSING** — needs a shim (see AmputationSystem §5). |

`[Dependency] private SharedBodySystem _body` on an abstract base resolves fine in Wolfgate — precedent: `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:30`, `WG/Content.Shared/Ensnaring/SharedEnsnareableSystem.cs:34`.

### 0.2 `BodyPartComponent`: six fields missing, `Parent` missing, `BodyPartType` missing two members

Onyx `C:/tmp/onyx/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs` (namespace `Content.Shared.Body.Part`) vs Wolfgate `WG/Content.Shared/Body/Part/BodyPartComponent.cs:20`.

| Member | Onyx | Wolfgate | Verdict |
|---|---|---|---|
| `EntityUid? Body` | `:48` | `:27` | SAME |
| `BodyPartType PartType` | `:56` | `:149` | SAME |
| `BodyPartSymmetry Symmetry` | `:57` | `:160` | SAME |
| `EntityUid? Parent` | `:50 [DataField, AutoNetworkedField] public EntityUid? Parent;` | **MISSING** — Wolfgate has `:32 public BodyPartSlot? ParentSlot;` and `WG/.../SharedBodySystem.Parts.cs:430 public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)` | **MISSING** |
| `ProtoId<FractureProfilePrototype>? FractureProfile` | `:71` | **MISSING** | MISSING |
| `FixedPoint2 MaxDamage` | `:80` | **MISSING** | MISSING |
| `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> AmputationThresholds` | `:87` | **MISSING** | MISSING |
| `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DismembermentFinishingDamage` | `:91` | **MISSING** | MISSING |
| `FixedPoint2 AmputationConsequenceSeverity = 35` | `:94` | **MISSING** | MISSING |
| `FixedPoint2? DismembermentSeverity` | `:97` | **MISSING** | MISSING |
| `Dictionary<string, BodyPartType> Children` | `:52` | `:180 Dictionary<string, BodyPartSlot> Children` | DIFFERENT (value type) |
| `HashSet<string> Organs` | `:54` | `:186 Dictionary<string, OrganSlot> Organs` | DIFFERENT |

And the enum:

`C:/tmp/onyx/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:15-27`
```csharp
public enum BodyPartType : ushort
{ Other = 0, Torso = 1, Head = 2, Arm = 3, Hand = 4, Leg = 5, Foot = 6, Tail = 7, Chest = 8, Groin = 9 }
```
`WG/Content.Shared/Body/Part/BodyPartType.cs:10-20`
```csharp
public enum BodyPartType
{ Other = 0, Torso, Head, Arm, Hand, Leg, Foot, Tail }
```

**`BodyPartType.Chest` and `BodyPartType.Groin` do not exist in Wolfgate.** Onyx's chest is Wolfgate's `Torso`. `AmputationSystem` references `BodyPartType.Chest` three times (`:35`, `:73`, `:115`). `WoundHostComponent` (not one of my files, but AmputationSystem/FractureEffectSystem read it) has `BodyPartType.Chest` and `.Groin` in its `[DataField]` defaults (`WoundDamageComponents.cs:20,21,52`).

D8 says the extra part fields go in a separate `_WF/Wolfmed` component. That means every `bodyPart.MaxDamage` / `.AmputationThresholds` / `.FractureProfile` / … read must be redirected. The cheapest shim that keeps the vendored files verbatim is **extension properties are not a C# feature**, so a verbatim-preserving shim is impossible for field access; these must be `// WOLFGATE` edits, or the Wolfmed part component must be merged into the upstream `BodyPartComponent`. See §0.6 for the recommended resolution.

### 0.3 `OrganComponent` is in a different namespace and has no health

| | Onyx | Wolfgate |
|---|---|---|
| File | `C:/tmp/onyx/Content.Shared/Body/OrganComponent.cs` | `WG/Content.Shared/Body/Organ/OrganComponent.cs` |
| Namespace | `Content.Shared.Body` (`:6`) | `Content.Shared.Body.Organ` (`:8`) |
| `Health` | `:20 public FixedPoint2 Health = FixedPoint2.New(15);` | **MISSING** |
| `MaxHealth` | `:23 public FixedPoint2 MaxHealth = FixedPoint2.New(15);` | **MISSING** |
| `DestructionWound` | `:31 public ProtoId<_Onyx.Wounds.WoundPrototype>? DestructionWound;` | **MISSING** |
| `DestructionWoundSeverity` | `:34` | **MISSING** |
| `EntityUid? Body` | `:41` | `:18` — SAME |

### 0.4 `OrganGotInsertedEvent` / `OrganGotRemovedEvent` do not exist in Wolfgate

Onyx: `C:/tmp/onyx/Content.Shared/Body/BodyComponent.cs:40,46`
```csharp
public readonly record struct OrganGotInsertedEvent(EntityUid Target);
public readonly record struct OrganGotRemovedEvent(EntityUid Target);
```
Raised on the **part/organ entity** whenever its `Body` changes (`_Onyx/Body/Systems/SharedBodySystem.cs:89-96,121-130`).

Wolfgate's nearest events (`WG/Content.Shared/Body/Events/MechanismBodyEvents.cs`):
```csharp
:16 public readonly record struct OrganAddedToBodyEvent(EntityUid Body, EntityUid Part);
:28 public readonly record struct OrganRemovedFromBodyEvent(EntityUid OldBody, EntityUid OldPart);
```
and for parts (`WG/Content.Shared/Body/Part/BodyPartEvents.cs:4,7`):
```csharp
[ByRefEvent] public readonly record struct BodyPartAddedEvent(string Slot, Entity<BodyPartComponent> Part);
[ByRefEvent] public readonly record struct BodyPartRemovedEvent(string Slot, Entity<BodyPartComponent> Part);
```
These are raised on the **body**, not the part (`WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:337-338, 357-358`). So neither is a drop-in. **MISSING**; shim in §3.

Note: `using Content.Shared.Body;` itself is legal in Wolfgate even though no type lives directly in that namespace, because `Content.Shared.Body.Part` etc. exist — so the `using` lines do not error, only the two event types do.

### 0.5 Namespace notes that affect `using` lines

| Type | Onyx namespace | Wolfgate namespace | Note |
|---|---|---|---|
| `DamageableSystem` | `Content.Shared.Damage.Systems` (`Content.Shared/Damage/Systems/DamageableSystem.cs:14`) | `Content.Shared.Damage` (`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:23,25`) | `Content.Shared.Damage.Systems` **does** exist in Wolfgate (e.g. `DamageContactsSystem.cs:8`) so the `using` compiles, but the *type* is unresolved unless `using Content.Shared.Damage;` is also present. |
| `DamageableComponent` | `Content.Shared.Damage.Components` (`Content.Shared/Damage/Components/DamageableComponent.cs:11`) | `Content.Shared.Damage` (`WG/Content.Shared/Damage/Components/DamageableComponent.cs:9,21`) | Same situation. |
| `CyberneticsComponent` | `Content.Shared._Onyx.Cybernetics` (`_Onyx/Cybernetics/CyberneticsComponent.cs:5,8`) | `Content.Shared._Shitmed.Cybernetics` (`WG/Content.Shared/_Shitmed/Cybernetics/CyberneticsComponent.cs:3,9`) | `.Disabled` exists in both (`:11` vs `:15`). |
| `OrganComponent` | `Content.Shared.Body` | `Content.Shared.Body.Organ` | |

**Only `WoundFractureSystem.cs` is missing `using Content.Shared.Damage;`** — it has `using Content.Shared.Damage.Components;` + `using Content.Shared.Damage.Systems;` (`:2,3`) but uses `DamageableComponent` and `DamageableSystem`. In Wolfgate that is two CS0246s.

### 0.6 `DamageSpecifier.DamageDict` key type differs — mostly transparent

| | Onyx | Wolfgate |
|---|---|---|
| | `Content.Shared/Damage/DamageSpecifier.cs:27 public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> DamageDict { get; set; }` | `WG/Content.Shared/Damage/DamageSpecifier.cs:44 public Dictionary<string, FixedPoint2> DamageDict { get; set; }` |

`ProtoId<T>` has implicit conversions in **both** directions (`WG/RobustToolbox/Robust.Shared/Prototypes/ProtoId.cs:24 implicit operator string(ProtoId<T>)`, `:34 implicit operator ProtoId<T>(string)`), so every call site in these 12 files still compiles: `DamageDict.TryGetValue(profile.DamageType, …)`, `thresholds.ContainsKey(type)` where `type` is a `string` from a `foreach`, `painMultipliers.TryGetValue(type, …)`, etc. all bind through the implicit conversion. **No shim needed**, but if `StatusEffectNew`/DamageSpecifier are ever re-synced this silently changes behaviour for keys that are not valid proto ids. `Clone()`, `Empty` (`:83`), `GetTotal()` (`:53`) all exist in Wolfgate.

### 0.7 New-style `DamageableSystem` API — the overload-resolution trap

Onyx (`Content.Shared/Damage/Systems/DamageableSystem.API.cs`):
```csharp
:68  public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)
:93  public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, out DamageSpecifier newDamage, …)
:120 public DamageSpecifier ChangeDamage(Entity<DamageableComponent?> ent, …)
:177 public DamageSpecifier HealEvenly(Entity<DamageableComponent?> ent, FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null)
:248 public DamageSpecifier HealDistributed(Entity<DamageableComponent?> ent, FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null)
:302 public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent, ProtoId<DamageGroupPrototype> group)
:327 public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent)
:422 public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent)   // [Obsolete]
:434 public FixedPoint2 GetTotalDamage(Entity<DamageableComponent?> ent)     // [Obsolete]
```

Wolfgate has **only**:
```csharp
WG/Content.Shared/Damage/Systems/DamageableSystem.cs:190
public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool ignoreGlobalModifiers = false,
    float armorPenetration = 0f,
    bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null,
    DamageOriginFlag? originFlag = null)
```
`ChangeDamage`, `HealEvenly`, `HealDistributed`, `GetPositiveDamage`, `GetAllDamage`, `GetTotalDamage` are all **MISSING** (verified: zero hits for each of those identifiers under `WG/Content.Shared/Damage/`).

**The trap.** `Entity<DamageableComponent?>` converts implicitly to `EntityUid` (`WG/RobustToolbox/Robust.Shared/GameObjects/Entity.cs:44`) and then to `EntityUid?`. So a vendored call written for Onyx's new API:

```csharp
_damageable.TryChangeDamage(entity.AsNullable(), change, args.Effect.IgnoreResistances, interruptsDoAfters: false)
```
(`ReagentTreatmentSystems.cs:19-20`) **compiles unchanged against Wolfgate's old overload**, but:
1. the 3rd positional argument now lands on Wolfgate's `ignoreResistances` — same meaning here, by luck;
2. the return type becomes `DamageSpecifier?` instead of `bool`. Any `if (_damageable.TryChangeDamage(...))` would become `if (DamageSpecifier?)` → CS0029, which is at least a compile error; but `var ok = _damageable.TryChangeDamage(...)` followed by a truthiness-free use silently changes type;
3. the `origin` parameter is at position 5 in Onyx and position 6 in Wolfgate, and position 5 in Wolfgate is `DamageableComponent? damageable`. **Any Onyx call that passes `origin` positionally will bind `origin` to `damageable`.** None of my 12 files do this, but `WoundDamageRoutingSystem`/`WoundHealingSystem` are likely to.

Recommended: **do not let the old overload be reachable from vendored code.** Add a compat layer that mirrors the new API and forwards, and shadow the old one by name so accidental binding is impossible (see §0.8).

### 0.8 Recommended compat layer for `DamageableSystem` (option (a) for several files)

`Content.Shared/_WF/Wolfmed/Compat/DamageableSystemOnyxCompat.cs`:

```csharp
namespace Content.Shared.Damage;

/// <summary>Onyx-shaped DamageableSystem API on top of Wolfgate's old one.</summary>
public static class DamageableSystemOnyxCompat
{
    public static bool TryChangeDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent,
        DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true,
        EntityUid? origin = null, bool ignoreGlobalModifiers = false);

    public static bool TryChangeDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent,
        DamageSpecifier damage, out DamageSpecifier newDamage, bool ignoreResistances = false,
        bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false);

    public static DamageSpecifier ChangeDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent,
        DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true,
        EntityUid? origin = null, bool ignoreGlobalModifiers = false);

    public static DamageSpecifier HealEvenly(this DamageableSystem sys, Entity<DamageableComponent?> ent,
        FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null);

    public static DamageSpecifier HealDistributed(this DamageableSystem sys, Entity<DamageableComponent?> ent,
        FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null);

    public static DamageSpecifier GetPositiveDamage(this DamageableSystem sys, Entity<DamageableComponent> ent);
    public static DamageSpecifier GetPositiveDamage(this DamageableSystem sys, Entity<DamageableComponent> ent,
        ProtoId<DamageGroupPrototype> group);

    public static DamageSpecifier GetAllDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent);
    public static FixedPoint2 GetTotalDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent);
}
```

**Caveat that must be tested:** C# prefers an applicable *instance* method over an extension method. For `GetPositiveDamage`/`GetAllDamage`/`HealEvenly`/`HealDistributed`/`ChangeDamage` there is no instance member, so the extension wins — fine. For `TryChangeDamage` the instance method `TryChangeDamage(EntityUid?, …)` **is** applicable to `Entity<DamageableComponent?>` via the user-defined conversion, so **the instance method always wins and the extension is dead code**. Therefore `TryChangeDamage` cannot be shimmed by an extension method. Options, best first:

- (a′) Name the compat method differently (`ChangeDamage` — which Wolfgate lacks entirely — is already the bool-free primitive) and use a `// WOLFGATE` edit only where `TryChangeDamage` is called with the new shape.
- (b) `// WOLFGATE` edit at the call site: `ReagentTreatmentSystems.cs:19-20` becomes
  ```csharp
  // WOLFGATE: Wolfgate's TryChangeDamage returns DamageSpecifier? and takes EntityUid?
  () => _damageable.TryChangeDamage(entity.Owner, change, args.Effect.IgnoreResistances,
      interruptsDoAfters: false));
  ```
- (c) Upstream hook: add the `Entity<DamageableComponent?>` bool overload directly into `WG/Content.Shared/Damage/Systems/DamageableSystem.cs` marked `// WOLFGATE`. This is the most invasive but eliminates the whole class of traps and is a ~10-line addition; **recommended if the wounds core ends up calling `TryChangeDamage` in more than two places.**

### 0.9 `EntityEffect` model: Wolfgate is class-based, Onyx is ECS

Onyx (`C:/tmp/onyx/Content.Shared/EntityEffects/`):
```csharp
EntityEffect.cs:11   public abstract partial class EntityEffect
EntityEffect.cs:19   public abstract void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user);
EntityEffect.cs:53   public virtual string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;
EntityEffect.cs:72   public abstract partial class EntityEffectBase<T> : EntityEffect where T : EntityEffectBase<T>
EntityEffectEvent.cs:9  [ByRefEvent, Access(typeof(SharedEntityEffectsSystem))]
                        public readonly record struct EntityEffectEvent<T>(T Effect, float Scale, EntityUid? User) where T : EntityEffectBase<T>
SharedEntityEffectsSystem.cs:174 public abstract partial class EntityEffectSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase<TEffect>
SharedEntityEffectsSystem.cs:178     public override void Initialize() { SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect); }
SharedEntityEffectsSystem.cs:182     protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);
```

Wolfgate (`WG/Content.Shared/EntityEffects/EntityEffect.cs`):
```csharp
:22 public abstract partial class EntityEffect
:32 protected abstract string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);
:47 public abstract void Effect(EntityEffectBaseArgs args);
:96 public record class EntityEffectBaseArgs { public EntityUid TargetEntity; public IEntityManager EntityManager; }
```
There is **no** `EntityEffectBase<T>`, **no** `EntityEffectEvent<T>`, **no** `EntityEffectSystem<T, TEffect>`, **no** `IEntityEffectRaiser`, **no** `Probability`-free `MinScale`/`Scaling`/`Impact`/`LogType`. `EntityEffects/` in Wolfgate contains exactly two files: `EntityEffect.cs`, `EntityEffectCondition.cs`.

This is the single largest gap for `SuppressPainEntityEffect.cs`, `ReagentTreatmentEffects.cs` and `ReagentTreatmentSystems.cs`.

Also, the effects themselves live in different assemblies:

| Effect | Onyx | Wolfgate |
|---|---|---|
| `HealthChange` | `Content.Shared.EntityEffects.Effects.Damage` (shared, ECS) | `WG/Content.Server/EntityEffects/Effects/HealthChange.cs:18` — **server**, `Content.Server.EntityEffects.Effects`, `: EntityEffect` |
| `EvenHealthChange` | shared, ECS | `WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs:16` — **server**, old style |
| `DistributedHealthChange` | shared, ECS | **MISSING entirely** |
| `HealthChangeEntityEffectSystem`, `EvenHealthChangeEntityEffectSystem`, `DistributedHealthChangeEntityEffectSystem` | shared ECS systems | **MISSING entirely** |

### 0.10 `StatusEffectNew` — absent, per D1 it is ported verbatim

`WG/Content.Shared/StatusEffectNew` does not exist. Wolfgate only has `WG/Content.Shared/StatusEffect/StatusEffectsSystem.cs:11 public sealed partial class StatusEffectsSystem : EntitySystem` in namespace `Content.Shared.StatusEffect` with `:107/:126/:165 TryAddStatusEffect`, `:258 TryRemoveStatusEffect(EntityUid uid, string key, …)`, `:324 HasStatusEffect(EntityUid uid, string key, …)`.

Since the two `StatusEffectsSystem` classes live in different namespaces (`Content.Shared.StatusEffect` vs `Content.Shared.StatusEffectNew`) they can coexist, and the vendored files' `using Content.Shared.StatusEffectNew;` resolves the right one. **No name collision.** D1's decision is sound.

API the 12 files need from it (from `C:/tmp/onyx/Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs`, read via `git show`):
```csharp
:91  public bool TrySetStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null)
:134 public bool TryUpdateStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null)
:143 public bool TryRemoveStatusEffect(EntityUid target, EntProtoId effectProto)
:169 public bool HasStatusEffect(EntityUid target, EntProtoId effectProto)
:435 public IEnumerable<Entity<StatusEffectComponent, T>> EnumerateStatusEffects<T>(Entity<StatusEffectContainerComponent?> container) where T : Component
```
plus `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs:15` with `:40 public bool Applied;`.

`EntProtoId<T>` → `EntProtoId` implicit conversion exists in RT 277 (`WG/RobustToolbox/Robust.Shared/Prototypes/EntProtoId.cs:72`), so `WoundStatusEffectBehavior.StatusEffect` (`EntProtoId<StatusEffectComponent>`) passes into those methods cleanly.

### 0.11 Engine (RT 277) — everything these files need is present

| API | Where in RT 277 | Verdict |
|---|---|---|
| `Entity<T>.AsNullable()` | `Robust.Shared/GameObjects/Entity.cs:62` | SAME |
| `implicit operator EntityUid(Entity<T>)` | `Entity.cs:44` | SAME |
| `ProtoId<T>` ⇄ `string` implicit | `Robust.Shared/Prototypes/ProtoId.cs:24,34,39` | SAME |
| `EntProtoId<T>` and `EntProtoId<T>` → `EntProtoId` | `Robust.Shared/Prototypes/EntProtoId.cs:62,72` | SAME |
| `LocId` | `Robust.Shared/Localization/LocId.cs:15` | SAME |
| `IPrototypeManager.TryIndex<T>(ProtoId<T>, out T?)` / `(ProtoId<T>?, …)` | `Robust.Shared/Prototypes/IPrototypeManager.cs:273,411` | SAME |
| `IPrototypeManager.EnumeratePrototypes<T>()` | present | SAME |
| `IConfigurationManager.GetCVar<T>(CVarDef<T>)` | `Robust.Shared/Configuration/IConfigurationManager.cs:155` | SAME |
| `IRobustRandom.Prob(float)`, `Pick<T>(IReadOnlyList<T>)`, `NextFloat()` | `Robust.Shared/Random/RandomExtensions.cs:171,32`; `IRobustRandom.cs:23` | SAME |
| `EntityQueryEnumerator<T1,T2,T3>` | `Robust.Shared/GameObjects/EntityManager.Components.cs:1466` | SAME |
| `[ImplicitDataDefinitionForInheritors]` | `Robust.Shared/Serialization/Manager/Attributes/ImplicitDataDefinitionForInheritorsAttribute.cs:30` | SAME |
| `INetManager`, `IGameTiming`, `ComponentStartup`, `Dirty`, `CompOrNull`, `EnsureComp`, `AddComp` | present | SAME |
| Duplicate directed subscription throw | `Robust.Shared/GameObjects/EntityEventBus.Directed.cs:407,419` `"Duplicate Subscriptions for comp={compTypeObj}, event={eventType.Name}"` | — |
| by-ref/by-value enforcement | only for **broadcast** events (`EntityEventBus.Broadcast.cs:217-221`); **directed** subscriptions have no such check | so `ref` handlers on Wolfgate's class-typed `RejuvenateEvent` / `RefreshMovementSpeedModifiersEvent` are legal — precedent `WG/Content.Shared/Eye/Blinding/Systems/BlindableSystem.cs:20`, `WG/Content.Shared/Movement/Systems/SharedMobCollisionSystem.cs:118` |

### 0.12 Server-only dependencies in Wolfgate

| System | Onyx | Wolfgate | Impact on my 12 files |
|---|---|---|---|
| `BloodstreamSystem` | `Content.Shared/Body/Systems/BloodstreamSystem.cs:33,35` — **shared** | `WG/Content.Server/Body/Systems/BloodstreamSystem.cs` — **server** | `WoundBleedingSystem` (`_Onyx/Wounds/WoundBleedingSystem.cs:25 [Dependency] private BloodstreamSystem _bloodstream`) cannot stay shared. **`OrganDamageSystem` (mine) has `[Dependency] private WoundBleedingSystem _bleeding` (`OrganDamageSystem.cs:23`)** → transitive blocker. |
| `HealingSystem` | shared | `WG/Content.Server/Medical/HealingSystem.cs` | Not referenced by my 12 files. |
| `BedSystem` | shared | `WG/Content.Server/Bed/BedSystem.cs` | Not referenced by my 12 files. |
| `ChatSystem.TryEmoteWithChat` | `Content.Shared/Chat/SharedChatSystem.Emote.cs:51,82` — **shared, returns `bool`** | `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:60,85` — **server, returns `void`**; `WG/Content.Shared/Chat/SharedChatSystem.cs:13` has no emote API at all | **PainSystem blocker.** |

### 0.13 Duplicate directed subscriptions vs Wolfgate

Every component named in a `SubscribeLocalEvent<TComp, TEvent>` in these 12 files is a **new Onyx component** (`PainComponent`, `PainShockTargetComponent`, `WoundableComponent`, `WoundHostComponent`, `WoundFractureComponent`). RT's duplicate check is per `(component, event)` pair (`EntityEventBus.Directed.cs:407`), so **none of the 12 files can collide with Wolfgate**. Verified: no Wolfgate file mentions any of those component types.

One conflict does exist elsewhere in the wounds folder and is worth passing to the orchestrator: `WoundSystem.cs:34 SubscribeLocalEvent<BodyComponent, RejuvenateEvent>` collides with `WG/Content.Server/_Mono/Body/Systems/BodyRejuvenateSystem.cs:28 SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);` → server start crash.

---

## 1. `PainSystem.cs` (462 lines)

### 1.1 Purpose and intra-Onyx dependencies

Maintains per-part and per-body pain: converts incoming damage into pain by type, keeps a "pain floor" derived from active wounds, decays pain and stacking suppression modifiers, and drives pain shock (arm below 110, fire at 130 → 2 s paralyse + forced scream + jitter + 30 s adrenaline at ×0.7). It is the source of `PainChangedEvent` and reads `ModifyPainGainEvent` so traits can scale gain.

Onyx files it depends on: `WoundDamageComponents.cs` (`PainComponent`, `PainShockTargetComponent`, `PainSuppressionModifier`, `WoundableComponent`, `WoundComponent`), `WoundEvents.cs` (`PainChangedEvent`, `ModifyPainGainEvent`), `WoundPrototype.cs` (`WoundPrototype.TryGetBehavior`, `BodyPartProfilePrototype.CanFeelPain`), `WoundBehaviors.cs` (`WoundPainBehavior`), `WoundSystem.cs` (`GetWounds`), plus `_Onyx/Body/Part/BodyPartComponent.cs`.

### 1.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `SharedChatSystem` | `Content.Shared.Chat` | `[Dependency] private SharedChatSystem _chat` (`:22`) | SAME as a type (`WG/Content.Shared/Chat/SharedChatSystem.cs:13 public abstract partial class SharedChatSystem : EntitySystem`) |
| `SharedChatSystem.TryEmoteWithChat` | `Content.Shared.Chat` | `Content.Shared/Chat/SharedChatSystem.Emote.cs:51 public bool TryEmoteWithChat(EntityUid source, string emoteId, ChatTransmitRange range = ChatTransmitRange.Normal, bool hideLog = false, string? nameOverride = null, bool ignoreActionBlocker = false, bool forceEmote = false, EmoteVisibilityOptions? emoteVisibility = null)` | **MISSING** in shared. Wolfgate's is `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:60 public void TryEmoteWithChat(EntityUid source, string emoteId, ChatTransmitRange range = ChatTransmitRange.Normal, bool hideLog = false, string? nameOverride = null, bool ignoreActionBlocker = false, bool forceEmote = false)` — server-only, `void` instead of `bool`, no `emoteVisibility`. |
| `ChatTransmitRange` | `Content.Shared.Chat` | `ChatTransmitRange.HideChat` | SAME (`WG/Content.Shared/Chat/SharedChatSystem.cs:391 public enum ChatTransmitRange : byte`) |
| `SharedJitteringSystem` | `Content.Shared.Jittering` | `_jitter.DoJitter(entity, PainShockStunTime, true, 20f, 7f)`; Onyx `Content.Shared/Jittering/SharedJitteringSystem.cs:46 public void DoJitter(EntityUid uid, TimeSpan time, bool refresh, float amplitude = 10f, float frequency = 4f, bool forceValueChange = false, StatusEffectsComponent? status = null)` | **SAME** — `WG/Content.Shared/Jittering/SharedJitteringSystem.cs:46` is byte-identical |
| `StatusEffectsSystem` (new) | `Content.Shared.StatusEffectNew` | `[Dependency] private StatusEffectsSystem _statusEffects` (`:25`) | **MISSING** (D1: port verbatim) |
| `StatusEffectsSystem.EnumerateStatusEffects<T>` | `Content.Shared.StatusEffectNew` | `StatusEffectSystem.API.cs:435 public IEnumerable<Entity<StatusEffectComponent, T>> EnumerateStatusEffects<T>(Entity<StatusEffectContainerComponent?> container) where T : Component` | **MISSING** (arrives with D1) |
| `StatusEffectComponent.Applied` | `Content.Shared.StatusEffectNew.Components` | `StatusEffectComponent.cs:40 public bool Applied;` | **MISSING** (arrives with D1) |
| `PainNumbnessStatusEffectComponent` | `Content.Shared.Traits.Assorted` | `C:/tmp/onyx/Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs:12 public sealed partial class PainNumbnessStatusEffectComponent : Component` with `[DataField] public ProtoId<LocalizedDatasetPrototype>? ForceSayNumbDataset = "ForceSayNumbDataset";` | **MISSING.** Wolfgate has the *old* trait shape: `WG/Content.Shared/Traits/Assorted/PainNumbnessComponent.cs:8 public sealed partial class PainNumbnessComponent : Component`, driven by `WG/Content.Shared/Traits/Assorted/PainNumbnessSystem.cs:14-16` (`ComponentInit`/`ComponentRemove`/`BeforeForceSayEvent`), not a status effect. |
| `SharedStunSystem` | `Content.Shared.Stunnable` | `[Dependency] private SharedStunSystem _stun` (`:26`) | SAME type (`WG/Content.Shared/Stunnable/SharedStunSystem.cs:29 public abstract partial class SharedStunSystem : EntitySystem`; concrete `WG/Content.Server/Stunnable/Systems/StunSystem.cs:5`) |
| `SharedStunSystem.TryUpdateParalyzeDuration` | `Content.Shared.Stunnable` | `Content.Shared/Stunnable/SharedStunSystem.cs:314 public bool TryUpdateParalyzeDuration(EntityUid uid, TimeSpan? duration, bool visualized = false)` | **MISSING.** Nearest: `WG/Content.Shared/Stunnable/SharedStunSystem.cs:244 public bool TryParalyze(EntityUid uid, TimeSpan time, bool refresh, StatusEffectsComponent? status = null)` → `TryKnockdown(...) && TryStun(...)`. Semantic difference: Onyx *updates* the status-effect duration (keeps the longer of the two) and separately re-applies knockdown; Wolfgate's `refresh` flag chooses between **replace** (`true`) and **add** (`false`) on the old `StatusEffectsSystem`, and returns false if the entity has no `StatusEffectsComponent`. |
| `RejuvenateEvent` | `Content.Shared.Rejuvenate` | `public sealed class RejuvenateEvent : EntityEventArgs;` | **SAME** (`WG/Content.Shared/Rejuvenate/RejuvenateEvent.cs:3`) — subscribing `ref` is legal, precedent at `WG/Content.Shared/Eye/Blinding/Systems/BlindableSystem.cs:20` |
| `MobStateComponent`, `MobState` | `Content.Shared.Mobs(.Components)` | `mobState.CurrentState == MobState.Dead` | SAME (`WG/Content.Shared/Mobs/Components/MobStateComponent.cs:22`, `WG/Content.Shared/Mobs/MobState.cs:14`) |
| `BodyPartComponent.Body` | `Content.Shared.Body.Part` | `part.Body is { } body` | SAME (`WG/.../BodyPartComponent.cs:27`) |
| `DamageSpecifier.DamageDict` | `Content.Shared.Damage` | `foreach (var (type, amount) in damage.DamageDict)` with `ProtoId<DamageTypePrototype>` keys | DIFFERENT key type (`string`) but call-site compatible — §0.6 |
| `DamageTypePrototype` | `Content.Shared.Damage.Prototypes` | `IReadOnlyDictionary<ProtoId<DamageTypePrototype>, float>` | SAME (`WG/Content.Shared/Damage/Prototypes/DamageTypePrototype.cs:3`) |
| `FixedPoint2` (`Max`, `Min`, `Clamp`, `Zero`, `Float()`) | `Content.Shared.FixedPoint` | — | SAME |
| `INetManager`, `IGameTiming`, `IPrototypeManager` | Robust | — | SAME (RT 277) |
| `Entity<T>.AsNullable()` | Robust | `entity.AsNullable()` (`:298,438`) | SAME (`Entity.cs:62`) |
| `ComponentStartup` | Robust | — | SAME |

### 1.3 Adaptations

**A. `TryEmoteWithChat` (MISSING, server-only in Wolfgate).**

(a) **Preferred — compat shim, file stays verbatim.** `SharedChatSystem` is `abstract partial` in Wolfgate, so add a partial declaration in `Content.Shared/_WF/Wolfmed/Compat/SharedChatSystem.Wolfmed.cs`:

```csharp
namespace Content.Shared.Chat;

public abstract partial class SharedChatSystem
{
    /// <summary>Shared entry point for emotes; only the server implementation does anything.</summary>
    public virtual bool TryEmoteWithChat(EntityUid source, string emoteId,
        ChatTransmitRange range = ChatTransmitRange.Normal, bool hideLog = false, string? nameOverride = null,
        bool ignoreActionBlocker = false, bool forceEmote = false) => false;
}
```
plus a one-line `// WOLFGATE` override in `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs` turning the existing `:60` method into `public override bool TryEmoteWithChat(...)` (returning `true` at the end). This keeps `PainSystem.cs` byte-for-byte and the call is already guarded by `_net.IsServer` at `PainSystem.cs:293` (inside `UpdatePainShock`, only reached from server-gated `Update`), so the shared no-op is never hit in practice. **Note this requires changing the return type of an upstream method from `void` to `bool` — 2 lines, both marked `// WOLFGATE`.**

(b) Minimal vendored edit. Replace `PainSystem.cs:22` and `:293-294`:
```csharp
    [Dependency] private WolfmedEmoteSystem _chat = default!; // WOLFGATE: Wolfgate's TryEmoteWithChat is server-only
```
with a `_WF/Wolfmed/Compat/WolfmedEmoteSystem.cs` shared system exposing the same method name and raising a networked-to-server event.

(c) Upstream hook: move Wolfgate's emote API into `SharedChatSystem` wholesale. Out of proportion for Phase 2.

**B. `TryUpdateParalyzeDuration` (MISSING).**

(a) **Preferred — extension shim** in `Content.Shared/_WF/Wolfmed/Compat/StunSystemOnyxCompat.cs`:
```csharp
namespace Content.Shared.Stunnable;

public static class StunSystemOnyxCompat
{
    /// <summary>Onyx-shaped paralyse call mapped onto Wolfgate's TryParalyze.</summary>
    public static bool TryUpdateParalyzeDuration(this SharedStunSystem stun, EntityUid uid, TimeSpan? duration,
        bool visualized = false);
}
```
implemented as `duration is { } d && d > TimeSpan.Zero && stun.TryParalyze(uid, d, refresh: false)`. No instance member of that name exists in Wolfgate, so the extension binds. `refresh: false` (add-to-existing) is the closer match to "update duration"; `refresh: true` would truncate an existing longer stun.

(b) `// WOLFGATE` edit at `PainSystem.cs:281`:
```csharp
        if (!_stun.TryParalyze(entity, PainShockStunTime, false)) // WOLFGATE: no TryUpdateParalyzeDuration in RT277-era Wolfgate
            return;
```

**C. `PainNumbnessStatusEffectComponent` (MISSING).**

(a) **Preferred — vendor the file.** It is a 12-line component with no behaviour of its own that `PainSystem` needs (only the type is used as a filter). Copy `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` verbatim into `Content.Shared/_Onyx/Traits/Assorted/` keeping namespace `Content.Shared.Traits.Assorted`, plus the `StatusEffectNew` entity prototype that carries it. Its `ForceSayNumbDataset` field references `LocalizedDatasetPrototype` — **verify separately** that Wolfgate has `Content.Shared.Dataset.LocalizedDatasetPrototype` and the `ForceSayNumbDataset` prototype; not checked here.

Do **not** also vendor `Content.Shared/Traits/Assorted/PainNumbnessSystem.cs` — its subscriptions (`PainNumbnessStatusEffectComponent` + `StatusEffectAppliedEvent`/`StatusEffectRemovedEvent`/`StatusEffectRelayedEvent<BeforeForceSayEvent>`/`StatusEffectRelayedEvent<BeforeAlertSeverityCheckEvent>`) need the full StatusEffectNew relay plus `BeforeAlertSeverityCheckEvent`, and Wolfgate already has a working `PainNumbnessSystem` on a different model. Keeping both is safe (different component types → no duplicate subscription).

(b) `// WOLFGATE` edit at `PainSystem.cs:321-325` to test Wolfgate's `PainNumbnessComponent` directly:
```csharp
    private bool IsPainNumb(EntityUid entity)
    {
        if (TryComp(entity, out BodyPartComponent? part) && part.Body is { } body)
            entity = body;

        return HasComp<PainNumbnessComponent>(entity); // WOLFGATE: Wolfgate's pain numbness is a plain trait component
    }
```

**D. `StatusEffectsSystem` (new) — resolved by D1.** No shim; vendor `Content.Shared/StatusEffectNew` (13 files) plus its client/server parts.

**E. `DamageableSystem` — not used by this file.** `PainSystem` touches only `DamageSpecifier`, so §0.7's trap does not apply here.

### 1.4 Directed subscriptions registered

| Component | Event | Wolfgate conflict |
|---|---|---|
| `PainShockTargetComponent` | `ComponentStartup` | none (new component) |
| `PainComponent` | `RejuvenateEvent` | none (new component) |

Also raises (not subscribes): `ModifyPainGainEvent` on the body (`:110,226`), `PainChangedEvent` on the part/body (`:412`).

### 1.5 Networking / prediction

Shared system, but **effectively server-authoritative**: almost every mutator opens with `if (!_net.IsServer) return` (`:47,53,78,124,190,253,290,338,352,363`). Client state arrives via `Dirty` on `PainComponent`/`PainShockTargetComponent`, both `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]` (`WoundDamageComponents.cs:102,145`). `GetPain`/`GetRawPain`/`CalculatePain`/`CanFeelPain` are pure reads and run on the client, which is what the Onyx client HUD and `PartStatusSystem` need.

Dependencies that are server-only in Wolfgate: **`SharedChatSystem.TryEmoteWithChat`** (adaptation A). `SharedStunSystem`, `SharedJitteringSystem`, `StatusEffectsSystem` are all shared in Wolfgate. No `BloodstreamSystem`/`HealingSystem`/`BedSystem` dependency.

**Port difficulty: light edits** (assuming D1 lands StatusEffectNew). Three shims, zero logic changes.

---

## 2. `WoundFractureSystem.cs` (189 lines)

### 2.1 Purpose and intra-Onyx dependencies

Creates, worsens and grades bone-fracture wounds from blunt damage on a part, using the part's `FractureProfilePrototype` (thresholds Hairline 8 / Simple 15 / Displaced 25 / Comminuted 40 in the prototype defaults, `WoundPrototype.cs:263-267`). Also owns fracture treatment state (`None → Reduced → Mended`) and raises `FractureGradeChangedEvent` / `FractureTreatmentChangedEvent`.

Onyx deps: `WoundDamageComponents.cs` (`WoundFractureComponent`, `WoundableComponent`, `WoundComponent`, `FractureGrade`, `FractureTreatment`), `WoundEvents.cs` (`WoundChangedEvent`, `PartDamageAppliedEvent`, `FractureGradeChangedEvent`, `FractureTreatmentChangedEvent`), `WoundPrototype.cs` (`FractureProfilePrototype`, `FractureGradeSettings`), `WoundSystem.cs` (`ChangeSeverity`, `CreateOrMergeWound`, `RemoveWound`, `GetWounds`).

### 2.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `DamageableSystem` | Onyx `Content.Shared.Damage.Systems` | `[Dependency] private DamageableSystem _damage` (`:14`) | **DIFFERENT namespace**: `Content.Shared.Damage` (`WG/Content.Shared/Damage/Systems/DamageableSystem.cs:23,25`). The file's `using` list (`:2,3`) lacks `using Content.Shared.Damage;` → **CS0246 in Wolfgate.** |
| `DamageableSystem.GetPositiveDamage` | — | `Content.Shared/Damage/Systems/DamageableSystem.API.cs:327 public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent)` used at `:65` | **MISSING** |
| `DamageableComponent` | Onyx `Content.Shared.Damage.Components` | `TryComp(part, out DamageableComponent? damageable)` (`:62`) | **DIFFERENT namespace**: `Content.Shared.Damage` (`WG/Content.Shared/Damage/Components/DamageableComponent.cs:9,21`). Fields used indirectly: `Damage` (`:48`), `TotalDamage` (`:63`). |
| `BodyPartComponent` | `Content.Shared.Body.Part` | `TryComp(part, out BodyPartComponent? bodyPart)` (`:143`), `CompOrNull<BodyPartComponent>(...)?.Body` (`:116,161,175`) | SAME |
| `BodyPartComponent.FractureProfile` | `Content.Shared.Body.Part` | `:146 var profileId = bodyPart.FractureProfile;` (`ProtoId<FractureProfilePrototype>?`) | **MISSING** (§0.2) |
| `DamageSpecifier.DamageDict` | `Content.Shared.Damage` | `args.Damage.DamageDict.TryGetValue(profile.DamageType, out var damage)` (`:26`) | key type DIFFERENT, call-site compatible (§0.6) |
| `FixedPoint2` | `Content.Shared.FixedPoint` | `FixedPoint2.Max/Zero` | SAME |
| `INetManager`, `IPrototypeManager`, `IRobustRandom` | Robust | `_random.Prob(Math.Clamp(...))` (`:49`) | SAME (RT 277) |
| `AddComp<T>`, `Dirty`, `CompOrNull<T>`, `TryComp` | Robust | — | SAME |

### 2.3 Adaptations

**A. Missing `using Content.Shared.Damage;` (compile break).**

(b) **Only option — 1-line `// WOLFGATE` edit** at the top of the file (no shim can fix a `using` list):
```csharp
using Content.Shared.Damage; // WOLFGATE: DamageableSystem/DamageableComponent live in Content.Shared.Damage here
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
```
(a) is technically possible via `global using DamageableSystem = Content.Shared.Damage.DamageableSystem;` in a `_WF/Wolfmed/Compat/GlobalUsings.cs`, but a `global using` alias for two of the most-used types in the codebase is a foot-gun; prefer the 1-line edit.

**B. `GetPositiveDamage` (MISSING).**

(a) **Preferred — extension shim** (part of the §0.8 compat class; no instance member of that name exists, so it binds):
```csharp
public static DamageSpecifier GetPositiveDamage(this DamageableSystem sys, Entity<DamageableComponent> ent);
// returns a new DamageSpecifier containing only ent.Comp.Damage.DamageDict entries > FixedPoint2.Zero
```
Direct port of `Content.Shared/Damage/Systems/DamageableSystem.API.cs:327-345`.

**C. `BodyPartComponent.FractureProfile` (MISSING).**

(a) **Preferred — put the field on a `_WF` component and shim the read.** Since C# has no extension properties, the shim has to be a method:
`Content.Shared/_WF/Wolfmed/Compat/WolfmedPartFields.cs`
```csharp
namespace Content.Shared._WF.Wolfmed.Compat;

/// <summary>Onyx BodyPartComponent fields Wolfgate keeps on WolfmedBodyPartComponent.</summary>
public static class WolfmedPartFields
{
    public static ProtoId<FractureProfilePrototype>? GetFractureProfile(this IEntityManager ent, EntityUid part);
    public static FixedPoint2 GetMaxDamage(this IEntityManager ent, EntityUid part);
    public static IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> GetAmputationThresholds(this IEntityManager ent, EntityUid part);
    public static IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2> GetDismembermentFinishingDamage(this IEntityManager ent, EntityUid part);
    public static FixedPoint2 GetAmputationConsequenceSeverity(this IEntityManager ent, EntityUid part);
    public static FixedPoint2? GetDismembermentSeverity(this IEntityManager ent, EntityUid part);
}
```
This still forces a `// WOLFGATE` edit at each read site, so it buys nothing over (b) except centralising the fallback defaults.

(b) **Recommended in practice — a `// WOLFGATE` edit per read.** `WoundFractureSystem.cs:143-149` becomes:
```csharp
        profile = default!;
        // WOLFGATE: fracture profile lives on WolfmedBodyPartComponent, not the upstream BodyPartComponent
        if (!Resolve(part, ref part.Comp, false) || !TryComp(part, out WolfmedBodyPartComponent? bodyPart))
            return false;

        var profileId = bodyPart.FractureProfile;
```
(one `using Content.Shared._WF.Wolfmed;` plus swapping the component type — 2 lines).

(c) **The clean alternative worth putting to the user:** add the six fields directly to `WG/Content.Shared/Body/Part/BodyPartComponent.cs` behind `// WOLFGATE` markers. That keeps all six vendored read sites verbatim (`WoundFractureSystem.cs:146`, `AmputationSystem.cs:36,44,74,84,91,103,117,123,158,161,188`, `FractureAlertSystem.cs:24`, and `WoundDamageComponents.cs`). It contradicts D8's letter but is ~30 lines of additive `[DataField]`s in one upstream file, against ~15 edits spread across five vendored files. **Recommend raising this with the orchestrator.**

### 2.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundFractureComponent` | `WoundChangedEvent` | none (both new) |

`HandlePartDamageApplied` (`:22`) is `internal` and is **not** subscribed — it is called manually from `OrganDamageSystem.OnPartDamageApplied` (`OrganDamageSystem.cs:35`), which is how Onyx avoids a duplicate `<WoundableComponent, PartDamageAppliedEvent>` subscription. Preserve that arrangement.

Raises: `FractureTreatmentChangedEvent` (`:118,119,177,178`) and `FractureGradeChangedEvent` (`:163,164`) on **both** the part and the wound entity.

### 2.5 Networking / prediction

Shared, server-gated (`:24,72,108`). `WoundFractureComponent` is `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]` (`WoundDamageComponents.cs:245`), so grade/treatment replicate. Pure readers (`GetFracture`, `GetGrade`, `TryGetProfile`) run client-side — required by `FractureEffectSystem`'s movement-speed hook, which **must** run on the client for movement prediction. No server-only Wolfgate dependency.

**Port difficulty: light edits.**

---

## 3. `FractureEffectsSystem.cs` (204 lines, class `FractureEffectSystem`)

### 3.1 Purpose and intra-Onyx dependencies

Turns fracture grade / treatment / part functionality into gameplay effects: movement-speed modifiers for mobility parts and a do-after duration multiplier for manipulation parts, and refreshes the fracture alert and part functionality whenever any of those change.

Onyx deps: `WoundFractureSystem`, `BodyPartFunctionalitySystem`, `WoundStatusEffectSystem`, `FractureAlertSystem`, `WoundDamageComponents.cs` (`WoundHostComponent.MobilityParts/ManipulationParts/PartEffectScales`, `WoundableComponent`, `WoundFractureComponent`), `WoundEvents.cs` (`FractureGradeChangedEvent`, `FractureTreatmentChangedEvent`, `WoundRemovedEvent`, `BodyPartFunctionalityChangedEvent`, `GetManipulationDurationMultiplierEvent`), `WoundPrototype.cs` (`FractureProfilePrototype`, `FractureGradeSettings`).

### 3.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(body)` (`:98,114`) — `GetBodyChildren(EntityUid body)` | **SAME at call site** (§0.1) |
| `OrganGotInsertedEvent` | `Content.Shared.Body` | `public readonly record struct OrganGotInsertedEvent(EntityUid Target);` — subscribed `<WoundableComponent, OrganGotInsertedEvent>` (`:34`), read `args.Target` (`:61-64`) | **MISSING** (§0.4) |
| `OrganGotRemovedEvent` | `Content.Shared.Body` | `public readonly record struct OrganGotRemovedEvent(EntityUid Target);` (`:35,66-71`) | **MISSING** (§0.4) |
| `BodyPartComponent.PartType` / `.Symmetry` / `.Body` | `Content.Shared.Body.Part` | `bodyPart.PartType` (`:100,116,159`), `bodyPart.Symmetry` (`:117`), `CompOrNull<BodyPartComponent>(part)?.Body` (`:55,56,79,83`) | SAME |
| `BodyPartType.Chest` / `.Groin` (indirect, via `WoundHostComponent` defaults) | `Content.Shared.Body.Part` | `WoundDamageComponents.cs:20,21` | **MISSING** in Wolfgate's enum (§0.2) — affects the component's defaults, not this file's code |
| `BodyPartSymmetry` | `Content.Shared.Body.Part` | `BodyPartSymmetry.None/Left/Right` | SAME (`WG/Content.Shared/Body/Part/BodyPartSymmetry.cs`, values `None, Left, Right`) |
| `HandsComponent` | `Content.Shared.Hands.Components` | `TryComp(body, out HandsComponent? hands)` (`:129`) | SAME |
| `SharedHandsSystem.IsHolding` | `Content.Shared.Hands.EntitySystems` | `Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:438 public bool IsHolding(Entity<HandsComponent?> ent, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out string? inHand)` — used at `:135` as `_hands.IsHolding((body, hands), item, out handId)` with `string? handId` | **DIFFERENT.** Wolfgate: `WG/Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:285 public bool IsHolding(EntityUid uid, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out Hand? inHand, HandsComponent? handsComp = null)` — out param is `Hand?`, not `string?`. |
| `SharedHandsSystem.GetActiveHand` | idem | `SharedHandsSystem.cs:280 public string? GetActiveHand(Entity<HandsComponent?> entity)` — used at `:139` | **DIFFERENT.** Wolfgate: `:177 public Hand? GetActiveHand(Entity<HandsComponent?> entity)` — returns `Hand?`. |
| `SharedHandsSystem.TryGetHand` | idem | `SharedHandsSystem.cs:462 public bool TryGetHand(Entity<HandsComponent?> ent, [NotNullWhen(true)] string? handId, [NotNullWhen(true)] out Hand? hand)` then `hand.Value.Location` (`:141,144`) | **DIFFERENT.** Wolfgate: `:306 public bool TryGetHand(EntityUid handsUid, string handId, [NotNullWhen(true)] out Hand? hand, HandsComponent? hands = null)`. Crucially **Wolfgate's `Hand` is a class** (`WG/Content.Shared/Hands/Components/HandsComponent.cs:107 public sealed class Hand`), so `hand.Value.Location` does not compile — it must be `hand.Location`. |
| `HandLocation` | `Content.Shared.Hands.Components` | Onyx `HandsComponent.cs:232-242`: `Right, Middle, Left, Functional, FunctionalRight, FunctionalLeft` — used as `HandLocation.Left or HandLocation.FunctionalLeft` (`:146-147`) | **DIFFERENT.** Wolfgate `:156-161`: `Left, Middle, Right` only. `Functional`, `FunctionalLeft`, `FunctionalRight` are **MISSING**; the ordinal values are also reversed. |
| `Hand.Location` | `Content.Shared.Hands.Components` | `hand.Value.Location` | SAME name (`WG/.../HandsComponent.cs:113 public HandLocation Location { get; }`), different access shape (class vs struct). |
| `MovementSpeedModifierSystem.RefreshMovementSpeedModifiers` | `Content.Shared.Movement.Systems` | `_movement.RefreshMovementSpeedModifiers(uid)` (`:93`) | **SAME** (`WG/Content.Shared/Movement/Systems/MovementSpeedModifierSystem.cs:80 public void RefreshMovementSpeedModifiers(EntityUid uid, MovementSpeedModifierComponent? move = null, bool alsoFriction = false)`) |
| `RefreshMovementSpeedModifiersEvent.ModifySpeed(float)` | idem | `args.ModifySpeed(1f - (1f - modifier) * partScale * treatmentScale)` (`:105`) | **SAME** (`WG/.../MovementSpeedModifierSystem.cs:177 public void ModifySpeed(float mod)`). Event is a class (`:164 public sealed class RefreshMovementSpeedModifiersEvent : EntityEventArgs, IInventoryRelayEvent`); `ref` subscription is fine (§0.11). |
| `Dirty`, `CompOrNull`, `TryComp`, `RaiseLocalEvent` | Robust | — | SAME |

### 3.3 Adaptations

**A. `OrganGotInsertedEvent` / `OrganGotRemovedEvent` (MISSING).**

(a) **Preferred — compat shim, file verbatim.** Two files in `Content.Shared/_WF/Wolfmed/Compat/`:

`OnyxBodyEvents.cs`
```csharp
namespace Content.Shared.Body;

/// <summary>Raised on a part when it gains a body. Onyx shape.</summary>
public readonly record struct OrganGotInsertedEvent(EntityUid Target);

/// <summary>Raised on a part when it loses a body. Onyx shape.</summary>
public readonly record struct OrganGotRemovedEvent(EntityUid Target);
```
`WolfmedBodyEventBridgeSystem.cs`
```csharp
namespace Content.Shared._WF.Wolfmed.Compat;

/// <summary>Translates Wolfgate's body-scoped part events into Onyx's part-scoped ones.</summary>
public sealed class WolfmedBodyEventBridgeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<BodyComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
    }
    // each re-raises OrganGot{Inserted,Removed}Event(bodyUid) on the part/organ entity
}
```
Check first that `<BodyComponent, BodyPartAddedEvent>` and `<BodyComponent, BodyPartRemovedEvent>` are not already taken in Wolfgate (they are raised at `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:337-338,357-358` on the body). Wolfgate raises `OrganAddedToBodyEvent`/`OrganRemovedFromBodyEvent` **on the organ entity** (`SharedBodySystem.Organs.cs:46,68`; `SharedBodySystem.Parts.cs:299,305-306`), not on the body, so those two subscriptions need to be `<OrganComponent, …>` instead — verify at implementation time.

Note the Onyx semantics this bridge must preserve: Onyx raises these for **the whole subtree** (`_Onyx/Body/Systems/SharedBodySystem.cs:64-111` walks `GetBodyPartChildren`), so detaching an arm fires for the arm, the hand, and the hand's organs. Wolfgate's `BodyPartRemovedEvent` fires once for the detached root. The bridge must walk `GetBodyPartChildren`-equivalent (`SharedBodySystem.Parts.cs` `GetBodyPartChildren`) to match, otherwise wounds on a severed hand keep their body-scoped status effects.

**B. Hands API (DIFFERENT — three methods + a missing enum member).**

This is the only place in the 12 files where a shim alone cannot fix it, because `HandLocation.FunctionalLeft`/`FunctionalRight` **do not exist** in Wolfgate.

(a) **Preferred — shim the three methods, `// WOLFGATE`-edit only the enum arm.** `Content.Shared/_WF/Wolfmed/Compat/HandsSystemOnyxCompat.cs`:
```csharp
namespace Content.Shared.Hands.EntitySystems;

public static class HandsSystemOnyxCompat
{
    /// <summary>Onyx-shaped IsHolding returning the hand name.</summary>
    public static bool IsHolding(this SharedHandsSystem hands, Entity<HandsComponent?> ent,
        [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out string? inHand);
}
```
**This does not work for `IsHolding`** — Wolfgate's instance `IsHolding(EntityUid, EntityUid?, out Hand?, HandsComponent?)` is applicable to `(Entity<HandsComponent?>, EntityUid?, out string?)`? No: `out string?` cannot bind to `out Hand?`, so the instance member is *not* applicable and the extension **does** bind. Confirmed viable. Same reasoning for `GetActiveHand`: the instance `Hand? GetActiveHand(Entity<HandsComponent?>)` **is** applicable (same parameters), so an extension `string? GetActiveHand(...)` would be shadowed → **`GetActiveHand` cannot be shimmed by extension**; and `TryGetHand(Entity<HandsComponent?>, string?, out Hand?)` vs instance `TryGetHand(EntityUid, string, out Hand?, HandsComponent? = null)` — the instance is applicable, so it shadows too.

(b) **Therefore: replace the whole method with a `// WOLFGATE` block.** Exact replacement for `FractureEffectsSystem.cs:126-151`:
```csharp
    // WOLFGATE: Wolfgate's hands API returns Hand objects, not hand-id strings, and has no Functional* locations.
    private bool TryGetUsedHandSymmetry(EntityUid body, EntityUid? used, out BodyPartSymmetry symmetry)
    {
        symmetry = BodyPartSymmetry.None;
        if (!TryComp(body, out HandsComponent? hands))
            return false;

        Hand? hand;
        if (used is { } item)
        {
            if (!_hands.IsHolding(body, item, out hand, hands))
                return false;
        }
        else
            hand = _hands.GetActiveHand((body, hands));

        if (hand is null)
            return false;

        symmetry = hand.Location switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
        return symmetry != BodyPartSymmetry.None;
    }
```
Semantic loss: Wolfgate has no `HandLocation.Middle`-vs-`Functional*` distinction, so cybernetic/functional hands are classified by their plain L/R location — acceptable, and Wolfgate's `HandLocation.Middle` already falls through to `None` in both versions.

**C. Consumers of `GetManipulationDurationMultiplierEvent`.** Onyx's only raise site outside this file is `Content.Shared/DoAfter/SharedDoAfterSystem.cs:262-264`:
```csharp
var manipulation = new Content.Shared._Onyx.Wounds.GetManipulationDurationMultiplierEvent(args.Used); // <Onyx-WoundFunctionality>
RaiseLocalEvent(args.User, ref manipulation); // <Onyx-WoundFunctionality>
args.Delay *= manipulation.Multiplier; // <Onyx-WoundFunctionality>
```
(c) **Upstream hook (preferred here).** Wolfgate already has a Goobstation multiplier hook at `WG/Content.Shared/DoAfter/SharedDoAfterSystem.cs:207-214`:
```csharp
        if (args.MultiplyDelay)
        {
            var delayMultiplierEv = new GetDoAfterDelayMultiplierEvent();
            RaiseLocalEvent(args.User, delayMultiplierEv);
            args.Delay *= delayMultiplierEv.Multiplier;
        }
```
Add three `// WOLFGATE` lines immediately after that block mirroring Onyx's. Do **not** try to reuse `GetDoAfterDelayMultiplierEvent` — it carries no `Used` and is `IBodyPartRelayEvent` targeting `BodyPartType.Hand` only (`WG/Content.Shared/_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:33-38`), which would lose Onyx's per-symmetry logic.

### 3.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundHostComponent` | `RefreshMovementSpeedModifiersEvent` | none (new component) |
| `WoundHostComponent` | `GetManipulationDurationMultiplierEvent` | none (both new) |
| `WoundFractureComponent` | `FractureGradeChangedEvent` | none |
| `WoundFractureComponent` | `FractureTreatmentChangedEvent` | none |
| `WoundFractureComponent` | `WoundRemovedEvent` | none |
| `WoundableComponent` | `OrganGotInsertedEvent` | none (event is shimmed in) |
| `WoundableComponent` | `OrganGotRemovedEvent` | none |
| `WoundableComponent` | `BodyPartFunctionalityChangedEvent` | none |

Note the two `OnChanged` overloads (`:38,45`) and two `OnPartChanged` overloads (`:59,66`) rely on overload resolution by event type — fine.

### 3.5 Networking / prediction

**This is the one system in the set that genuinely must be predicted.** It has **no** `_net.IsServer` gate anywhere, and `OnRefreshSpeed` (`:96`) runs inside `RefreshMovementSpeedModifiers`, which the client evaluates every movement tick. Its reads — `_fractures.GetFracture`, `_fractures.TryGetProfile`, `_functionality.GetState` — must all work client-side, which is why `WoundFractureComponent`, `WoundableComponent` and `BodyPartFunctionalityComponent` are all networked with auto-state.

Server-only Wolfgate dependencies: **none.** `SharedBodySystem`, `SharedHandsSystem`, `MovementSpeedModifierSystem` are all shared. `BodyPartFunctionalitySystem` gates `Refresh` on server (§7) but `GetState` does not.

**Port difficulty: heavy adaptation** — the hands block must be rewritten, and the `OrganGot*` bridge has non-trivial subtree semantics.

---

## 4. `FractureAlertSystem.cs` (46 lines)

### 4.1 Purpose and intra-Onyx dependencies

Recomputes the `BrokenBones` alert (and any other alert declared by a `FractureProfilePrototype`) for a body by scanning every part's fracture grade against the profile's `AlertMinimumGrade` and `AlertHiddenTreatments`. It is pure refresh logic with no subscriptions.

Onyx deps: `WoundFractureSystem.GetFracture`, `WoundPrototype.cs` (`FractureProfilePrototype.Alert/AlertMinimumGrade/AlertHiddenTreatments`), `WoundDamageComponents.cs` (`FractureGrade`, `FractureTreatment`).

### 4.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `AlertsSystem.ShowAlert` | `Content.Shared.Alert` | `Content.Shared/Alert/AlertsSystem.cs:135 public void ShowAlert(Entity<AlertsComponent?> entity, ProtoId<AlertPrototype> alertType, short? severity = null, (TimeSpan, TimeSpan)? cooldown = null, bool autoRemove = false, bool showCooldown = true)` — used at `:38` as `_alerts.ShowAlert(uid, alert)` | **SAME at the call site.** Wolfgate: `WG/Content.Shared/Alert/AlertsSystem.cs:81 public void ShowAlert(EntityUid euid, ProtoId<AlertPrototype> alertType, short? severity = null, (TimeSpan, TimeSpan)? cooldown = null, bool autoRemove = false, bool showCooldown = true)`. First param `EntityUid` vs `Entity<AlertsComponent?>` — the call passes a bare `EntityUid`, which binds to both. |
| `AlertsSystem.ClearAlert` | idem | `AlertsSystem.cs:250 public void ClearAlert(Entity<AlertsComponent?> entity, ProtoId<AlertPrototype> alertType)` — `:40,44` | **SAME at the call site** (`WG/.../AlertsSystem.cs:153 public void ClearAlert(EntityUid euid, ProtoId<AlertPrototype> alertType)`) |
| `AlertsSystem` (type) | `Content.Shared.Alert` | `[Dependency] private AlertsSystem _alerts` (`:11`) | SAME (`WG/.../AlertsSystem.cs:9 public abstract partial class AlertsSystem : EntitySystem`) |
| `AlertsSystem.UpdateAlert` | `Content.Shared.Alert` | **not used by this file** | (D5 lists it missing; irrelevant here) |
| `AlertPrototype` | `Content.Shared.Alert` | `ProtoId<AlertPrototype>` | SAME |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(uid)` (`:22`) | SAME at call site (§0.1) |
| `BodyPartComponent.FractureProfile` | `Content.Shared.Body.Part` | `:24 if (bodyPart.FractureProfile is not { } profileId …)` | **MISSING** (§0.2) |
| `IPrototypeManager.TryIndex<T>` / `EnumeratePrototypes<T>` | Robust | `:25,42` | SAME |

Alert prototype required: `BrokenBones`, defined in Onyx at `Resources/Prototypes/_Onyx/Alerts/*.yml` with `name: alerts-broken-bones-name`, `description: alerts-broken-bones-desc`, icon `/Textures/_Onyx/Interface/Alerts/fracture.rsi` state `brokenbones`. Locale keys live in `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` (2 keys, verified present). Neither the prototype nor the texture exists in Wolfgate — both must be vendored under `Resources/Prototypes/_Onyx/Alerts/` and `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi` (check the rsi `meta.json` licence per the handoff).

### 4.3 Adaptations

**A. `BodyPartComponent.FractureProfile`** — same three options as §2.3-C. Exact `// WOLFGATE` replacement for `:22-27`:
```csharp
        foreach (var (part, bodyPart) in _body.GetBodyChildren(uid))
        {
            // WOLFGATE: fracture profile lives on WolfmedBodyPartComponent
            if (!TryComp(part, out WolfmedBodyPartComponent? wolfmed) ||
                wolfmed.FractureProfile is not { } profileId ||
                !_prototypes.TryIndex(profileId, out FractureProfilePrototype? profile) ||
                profile.Alert is not { } alert)
                continue;
```

**B. Nothing else.** This is the cleanest file in the set.

### 4.4 Directed subscriptions

**None.** Pure API surface (`Refresh(EntityUid?)`), called from `FractureEffectSystem` (`:41,48,55,62,69,85`).

### 4.5 Networking / prediction

Shared, **not** server-gated. `AlertsSystem` in Wolfgate refuses to act while applying state (`WG/.../AlertsSystem.cs:84,155 if (_timing.ApplyingState) return;`), same as Onyx (`AlertsSystem.cs:148,252`), so client-side refreshes driven from a networked component state change behave identically. No server-only dependency.

**Port difficulty: light edits** (one field redirect; plus the alert prototype/texture/locale assets).

---

## 5. `AmputationSystem.cs` (212 lines)

### 5.1 Purpose and intra-Onyx dependencies

Decides when a wounded limb becomes severable and when a follow-up hit actually detaches it: tracks per-damage-type amputation thresholds, arms/disarms `WoundableComponent.Severable`, gives explosions a probabilistic sever, then detaches the part, throws it, and applies a dismemberment wound plus an amputation-consequence wound to the parent.

Onyx deps: `WoundDamageComponents.cs` (`WoundableComponent.Severable/AmputationOverflow`, `WoundHostComponent.*`), `WoundEvents.cs` (`PartDamageOverflowedEvent`, `PartDamageAppliedEvent`), `WoundSystem.CreateOrMergeWound`, `_Onyx/Body/Part/BodyPartComponent.cs`.

### 5.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `SharedBodySystem.TryDetachPart` | `Content.Shared.Body.Systems` | `_Onyx/Body/Systems/SharedBodySystem.cs:225 public bool TryDetachPart(EntityUid part, bool reparent = true)` — `:118 if (!_body.TryDetachPart(part))` | **MISSING.** Wolfgate: `WG/.../SharedBodySystem.Parts.cs:702 public bool DetachPart(EntityUid parentPartId, string slotId, EntityUid partId, BodyPartComponent? parentPart = null, BodyPartComponent? part = null)` and `:751 public bool DetachPart(EntityUid parentPartId, BodyPartSlot slot, EntityUid partId, …)`; slot lookup via `:430 public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)`. |
| `DamageableSystem.GetAllDamage` | `Content.Shared.Damage.Systems` | `DamageableSystem.API.cs:422 [Obsolete] public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent)` — `:78,182` | **MISSING** |
| `DamageableComponent` | Onyx `Content.Shared.Damage.Components` | `TryComp(part, out DamageableComponent? damageable)` (`:75`) | namespace differs, but the file has `using Content.Shared.Damage;` (`:4`) → resolves |
| `DamageableSystem` | Onyx `Content.Shared.Damage.Systems` | `[Dependency] private DamageableSystem _damageable` (`:19`) | namespace differs, `using Content.Shared.Damage;` (`:4`) present → resolves |
| `ThrowingSystem.TryThrow` | `Content.Shared.Throwing` | `Content.Shared/Throwing/ThrowingSystem.cs:87 public bool TryThrow(EntityUid uid, Vector2 direction, float baseThrowSpeed = 10.0f, EntityUid? user = null, float pushbackRatio = PushbackDefault, float? friction = null, bool compensateFriction = false, bool recoil = true, bool animated = true, bool playSound = true, bool doSpin = true, ThrowingUnanchorStrength unanchor = ThrowingUnanchorStrength.None)` — used at `:125-126` as `_throwing.TryThrow(part, Vector2.UnitY, baseThrowSpeed: 3f, pushbackRatio: 0f, doSpin: true)` | **SAME at the call site.** Wolfgate `WG/Content.Shared/Throwing/ThrowingSystem.cs:96` has the identical parameter list except it returns `void` instead of `bool`; the return is discarded here, so it compiles. |
| `BodyPartComponent.PartType == BodyPartType.Chest` | `Content.Shared.Body.Part` | `:35,73,115` | **MISSING enum member** — Wolfgate's chest is `BodyPartType.Torso` (§0.2) |
| `BodyPartComponent.Parent` | `Content.Shared.Body.Part` | `:73 bodyPart.Parent == null`, `:117 var parent = bodyPart.Parent ?? part;` | **MISSING** (§0.2) |
| `BodyPartComponent.MaxDamage` | idem | `:36,44` | **MISSING** |
| `BodyPartComponent.AmputationThresholds` | idem | `:74,84,91,103,158,188` | **MISSING** |
| `BodyPartComponent.DismembermentFinishingDamage` | idem | `:161` | **MISSING** |
| `BodyPartComponent.DismembermentSeverity` | idem | `:123` | **MISSING** |
| `BodyPartComponent.AmputationConsequenceSeverity` | idem | `:64` | **MISSING** |
| `DamageSpecifier.DamageDict`, `.Clone()` | `Content.Shared.Damage` | `:98,100,146,158,184` | key type DIFFERENT, call-site compatible (§0.6); `Clone()` SAME |
| `DamageTypePrototype` | `Content.Shared.Damage.Prototypes` | `IReadOnlyDictionary<ProtoId<DamageTypePrototype>, FixedPoint2>` (`:132,139`) | SAME |
| `Vector2` / `Vector2.UnitY` | `System.Numerics` | `:125` | SAME |
| `IRobustRandom.Prob` | Robust | `:189` | SAME |
| `INetManager`, `IPrototypeManager` | Robust | — | SAME |

### 5.3 Adaptations

**A. `TryDetachPart` (MISSING).**

(a) **Preferred — extension shim** (no instance member of that name in Wolfgate, so it binds cleanly). `Content.Shared/_WF/Wolfmed/Compat/BodySystemOnyxCompat.cs`:
```csharp
namespace Content.Shared.Body.Systems;

public static class BodySystemOnyxCompat
{
    /// <summary>Onyx-shaped detach: finds the parent slot itself.</summary>
    public static bool TryDetachPart(this SharedBodySystem body, EntityUid part, bool reparent = true);
}
```
Implementation: `body.GetParentPartAndSlotOrNull(part) is not { } p ? false : body.DetachPart(p.Parent, p.Slot, part)`. Wolfgate's `DetachPart` already handles the container remove and reparent (`SharedBodySystem.Parts.cs:751-790`), so `reparent` is ignored — document that in the shim summary. **`GetParentPartAndSlotOrNull` is `public`** (`:430`), confirmed.

Semantic difference to flag: Onyx's version also prunes `parentPart.Children` for dynamic slots (`_Onyx/.../SharedBodySystem.cs:240-241`) and is gated by nothing; Wolfgate's `DetachPart` first calls `CanDetachPart` (`:718,733`), which checks `part.PartType == parentSlotData.Type` and `Containers.CanRemove` — an extra refusal path Onyx does not have. If Shitmed's own severing has already mangled the slot, `CanDetachPart` can return false and amputation silently no-ops. Worth an integration test.

**B. `GetAllDamage` (MISSING).** Extension shim from §0.8 — no instance member, binds cleanly:
```csharp
public static DamageSpecifier GetAllDamage(this DamageableSystem sys, Entity<DamageableComponent?> ent);
// => ent.Comp.Damage.Clone() after resolving
```

**C. `BodyPartType.Chest` (MISSING enum member).**

(b) **Only workable option — three `// WOLFGATE` edits.** Exactly:
- `:35` `bodyPart.PartType == BodyPartType.Chest` → `bodyPart.PartType == BodyPartType.Torso // WOLFGATE: Wolfgate's chest is Torso`
- `:73` `bodyPart.PartType is BodyPartType.Chest` → `bodyPart.PartType is BodyPartType.Torso // WOLFGATE`
- `:115` `bodyPart.PartType == BodyPartType.Chest` → `bodyPart.PartType == BodyPartType.Torso // WOLFGATE`

(a) A shim is impossible: you cannot add a member to an existing enum from another file. The alternative (a′) is to add `Chest = 8, Groin = 9` to `WG/Content.Shared/Body/Part/BodyPartType.cs` as `// WOLFGATE` members — but Wolfgate has ~300 references to `BodyPartType.Torso` and the YAML/prototype set uses `Torso`, so two synonymous members would silently split every `PartType` comparison. **Do not do this.** Take option (b), and additionally retarget the `WoundHostComponent` YAML defaults (`WoundDamageComponents.cs:20,21,52`) and `wounds.yml` from `Chest`/`Groin` to `Torso`/`Groin` — note **`Groin` exists** in Wolfgate's `TargetBodyPart` (`WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:16`) but **not** in `BodyPartType`.

**D. `BodyPartComponent.Parent` (MISSING).**

(b) `// WOLFGATE` edits at `:73` and `:117`:
```csharp
        // WOLFGATE: Wolfgate stores the slot, not the parent uid
        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null ||
            bodyPart.PartType is BodyPartType.Torso ||
            _body.GetParentPartAndSlotOrNull(part.Owner) is null ||
```
```csharp
        var parent = _body.GetParentPartAndSlotOrNull(part)?.Parent ?? part; // WOLFGATE
```
(This adds `[Dependency] private SharedBodySystem _body` — already present at `:18`.)

**E. The five remaining `BodyPartComponent` fields** — §2.3-C options. Six edit sites in this file.

### 5.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundableComponent` | `PartDamageOverflowedEvent` | none (both new) |

`HandlePartDamageApplied` (`:67`) is public but **not** subscribed — called from `OrganDamageSystem.cs:36`. `ApplyAmputationConsequences` (`:55`) and `TryAmputate` (`:111`) are public entry points with **no callers anywhere in the Onyx checkout outside this file** (verified by grep over `Content.Shared`, `Content.Server`, `Content.Client`) — they are intended for surgery/admin use.

### 5.5 Networking / prediction

Shared but fully server-gated (`:33,57,68,113`). `WoundableComponent.Severable` is dirtied at `:205` for client display. No server-only Wolfgate dependency (`SharedBodySystem`, `ThrowingSystem`, `DamageableSystem` are all shared here), **but** `_body.TryDetachPart` ultimately runs Wolfgate's container removal, which in Shitmed also triggers the `BodyPartRemovedEvent`/`BodyPartEnableChangedEvent` cascade (`WG/.../SharedBodySystem.Parts.cs:191-212`) — that cascade includes Shitmed's own severing/gibbing hooks and **must be reconciled with D2's bypass**, otherwise an Onyx amputation will also fire Shitmed's part-disable logic.

**Port difficulty: heavy adaptation** — 3 `Chest`→`Torso` edits, 2 `Parent` edits, 6 missing-field redirects, 2 shims.

---

## 6. `OrganDamageSystem.cs` (107 lines)

### 6.1 Purpose and intra-Onyx dependencies

The **single** `<WoundableComponent, PartDamageAppliedEvent>` subscriber: it fans the event out to `WoundSystem`, `WoundFractureSystem`, `AmputationSystem` and `WoundBleedingSystem` by direct call, then rolls a per-part-type chance to damage a weighted random subset of the part's organs, capped by each organ's `MaxDamageFraction`.

Onyx deps: `WoundSystem`, `WoundFractureSystem`, `AmputationSystem`, `WoundBleedingSystem`, `WoundDamageComponents.cs` (`WoundableComponent.Profile`), `WoundPrototype.cs` (`BodyPartProfilePrototype.OrganDamage`, `OrganDamageRouting`), `_Onyx/Body/OrganDamageComponent.cs`, `_Onyx/Body/Systems/OrganHealthSystem.cs`.

### 6.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `SharedBodySystem.GetPartOrgans` | `Content.Shared.Body.Systems` | `_Onyx/Body/Systems/SharedBodySystem.cs:424 public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid part)` — `:48` | **SAME at call site** (`WG/.../SharedBodySystem.Parts.cs:825`) |
| `OrganComponent` | Onyx `Content.Shared.Body` | `(EntityUid Id, OrganComponent Component)` (`:87-88`) | **DIFFERENT namespace**: `Content.Shared.Body.Organ` (`WG/Content.Shared/Body/Organ/OrganComponent.cs:8,12`). The file has `using Content.Shared.Body;` (`:2`) which will not resolve it. |
| `OrganComponent.Health` | idem | `:49 organ.Component.Health > FixedPoint2.Zero` | **MISSING** in Wolfgate |
| `OrganComponent.MaxHealth` | idem | `:69 organ.Component.MaxHealth * policy.MaxDamageFraction` | **MISSING** in Wolfgate |
| `OrganDamageComponent` | `Content.Shared._Onyx.Body` | `_Onyx/Body/OrganDamageComponent.cs:10` with `HitChance` (`:13`), `SelectionWeight` (`:16`), `DamageMultipliers` (`:19`, `Dictionary<ProtoId<DamageTypePrototype>, float>`), `MaxDamageFraction` (`:22`) | **MISSING** — Onyx file, vendor it (23 lines, no external deps beyond `DamageTypePrototype`) |
| `OrganHealthSystem.ChangeHealth` | `Content.Shared._Onyx.Body.Systems` | `_Onyx/Body/Systems/OrganHealthSystem.cs:62 public void ChangeHealth(Entity<OrganComponent> organ, FixedPoint2 amount) => SetHealth(organ, organ.Comp.Health + amount);` — `:71` | **MISSING** — Onyx file; but it depends on `OrganComponent.Health/MaxHealth/DestructionWound/DestructionWoundSeverity` and on `_body.TryGetOrganInSlot` / `_body.TryRemoveOrgan` (`OrganHealthSystem.cs:72,75`), neither of which exists in Wolfgate's `SharedBodySystem`. |
| `BodyPartComponent.PartType` / `.Body` | `Content.Shared.Body.Part` | `:39,44` | SAME |
| `DamageSpecifier.DamageDict` | `Content.Shared.Damage` | `:80` | key type DIFFERENT, compatible (§0.6) |
| `IRobustRandom.Prob` / `.Pick` / `.NextFloat` | Robust | `:45,61,95,97` | SAME |
| `INetManager`, `IPrototypeManager` | Robust | — | SAME |

### 6.3 Adaptations

**A. `OrganComponent` namespace + `Health`/`MaxHealth` (DIFFERENT/MISSING).** This is the file's real cost.

(a) **Preferred — a `_WF` organ-health component + a compat facade.** Wolfgate's `OrganComponent` is Shitmed's and has no health model (`WG/Content.Shared/Body/Organ/OrganComponent.cs`: `Body`, `OriginalBody`, `SlotId`, `ToolName`, `Speed`, `Used`, `OnAdd`, `OnRemove`, `Enabled`, `CanEnable`, `Removable`). Put health on a new component:
```csharp
namespace Content.Shared._WF.Wolfmed;

/// <summary>Onyx organ health on top of Wolfgate's Shitmed OrganComponent.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WolfmedOrganHealthComponent : Component
{
    [DataField, AutoNetworkedField] public FixedPoint2 Health = FixedPoint2.New(15);
    [DataField, AutoNetworkedField] public FixedPoint2 MaxHealth = FixedPoint2.New(15);
    [DataField] public ProtoId<WoundPrototype>? DestructionWound;
    [DataField] public FixedPoint2 DestructionWoundSeverity;
}
```
and a `WolfmedOrganHealthSystem` exposing `ChangeHealth(Entity<WolfmedOrganHealthComponent>, FixedPoint2)` ported from `OrganHealthSystem.cs`, with `TryGetOrganInSlot`/`TryRemoveOrgan` re-expressed on Wolfgate's `SharedBodySystem.Organs.cs` API.

This cannot be reached without editing the vendored file, because the element type of `_body.GetPartOrgans(...)` is baked into `:87-88`, `:49`, `:69`. So option (a) still needs a `// WOLFGATE` rewrite of `:47-72` and `:87-105`.

(b) **`// WOLFGATE` edits.** Exact replacement for `:47-52`:
```csharp
        // WOLFGATE: organ health lives on WolfmedOrganHealthComponent; Wolfgate's OrganComponent has none
        var organs = _body.GetPartOrgans(part)
            .Where(organ => TryComp(organ.Id, out WolfmedOrganHealthComponent? health)
                            && health.Health > FixedPoint2.Zero
                            && HasComp<OrganDamageComponent>(organ.Id))
            .Select(organ => (organ.Id, Component: Comp<WolfmedOrganHealthComponent>(organ.Id)))
            .ToList();
```
with `PickOrgan`'s signature changed to `List<(EntityUid Id, WolfmedOrganHealthComponent Component)>` and `:69/:71` retargeted. Roughly 12 changed lines.

(c) Upstream hook: add `Health`/`MaxHealth` to `WG/Content.Shared/Body/Organ/OrganComponent.cs` marked `// WOLFGATE`. Same trade-off as §2.3-C option (c); 4 additive `[DataField]`s keep this whole file verbatim except the `using`.

**B. `using Content.Shared.Body;` does not resolve `OrganComponent`.** Requires `using Content.Shared.Body.Organ;` — a 1-line `// WOLFGATE` add regardless of which option above is chosen (unless (c) is taken, in which case it is still needed).

**C. `OrganDamageComponent`, `OrganHealthSystem` (MISSING but Onyx-owned).** Vendor `_Onyx/Body/OrganDamageComponent.cs` verbatim (23 lines, clean). `OrganHealthSystem.cs` needs the body-API rework above → treat as a `_WF` reimplementation, not a vendor.

**D. `WoundBleedingSystem` dependency (`:23`) is the networking blocker** — see §6.5.

### 6.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundableComponent` | `PartDamageAppliedEvent` | none (both new) |

**Critical invariant:** this is the *only* subscriber to that pair in the whole wounds folder (verified across all 22 files). `WoundSystem`, `WoundFractureSystem`, `AmputationSystem` and `WoundBleedingSystem` each expose a `HandlePartDamageApplied` that this system calls in a fixed order (`:34-37`). **Do not convert any of those into their own `SubscribeLocalEvent<WoundableComponent, PartDamageAppliedEvent>`** — RT would throw `"Duplicate Subscriptions for comp=WoundableComponent, event=PartDamageAppliedEvent"` (`WG/RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Directed.cs:407`). Note `Initialize()` here does **not** call `base.Initialize()` (`:27-30`) — harmless, but keep it if re-syncing.

### 6.5 Networking / prediction

Shared, server-gated for the organ-damage roll (`:39`) but the **fan-out at `:34-37` runs on both client and server** (it is above the `_net.IsServer` check), so all four `HandlePartDamageApplied` implementations must be callable client-side.

**Blocker:** `[Dependency] private WoundBleedingSystem _bleeding` (`:23`). `WoundBleedingSystem` depends on `[Dependency] private BloodstreamSystem _bloodstream` (`WoundBleedingSystem.cs:25`), and `BloodstreamSystem` is **server-only** in Wolfgate (`WG/Content.Server/Body/Systems/BloodstreamSystem.cs`; Onyx's is shared at `Content.Shared/Body/Systems/BloodstreamSystem.cs:33,35`). A shared `OrganDamageSystem` cannot take a `[Dependency]` on a system whose own dependency does not exist on the client — the client would fail `EntitySystemManager` resolution at startup.

Three ways out, in order of preference:
1. **Split `WoundBleedingSystem`** into a shared part (wound state, rate bookkeeping, `HandlePartDamageApplied`) and a `_WF` server part that talks to `BloodstreamSystem` via an event. Keeps `OrganDamageSystem` shared and keeps the fan-out predicted.
2. **Make the bloodstream dependency lazy** — a `_WF/Wolfmed/Compat/WolfmedBloodstreamBridge` shared system with a server implementation, injected instead of `BloodstreamSystem`. 1-line `// WOLFGATE` edit in `WoundBleedingSystem.cs:25`.
3. **Move `OrganDamageSystem` server-side** and lose client prediction of the whole `PartDamageAppliedEvent` fan-out. Cheapest, but it breaks `FractureEffectSystem`'s predicted movement modifier reacting to a fresh fracture on the same tick.

**Port difficulty: heavy adaptation.**

---

## 7. `BodyPartFunctionalitySystem.cs` (78 lines)

### 7.1 Purpose and intra-Onyx dependencies

Computes a part's functionality state (`Functional` / `Impaired` / `Disabled` / `Unavailable`) from its active wounds' `WoundFunctionalityBehavior`, softened one step when a fracture is `Reduced` and skipped entirely when `Mended`, and caches it on `BodyPartFunctionalityComponent`, raising `BodyPartFunctionalityChangedEvent`.

Onyx deps: `WoundSystem.GetWounds`, `WoundDamageComponents.cs` (`WoundableComponent`, `WoundHostComponent`, `WoundFunctionalityComponent`, `WoundFractureComponent`, `BodyPartFunctionalityComponent`, `BodyPartFunctionalityState`, `WoundState`, `FractureTreatment`), `WoundEvents.cs` (`BodyPartFunctionalityChangedEvent`).

### 7.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `CyberneticsComponent` | Onyx `Content.Shared._Onyx.Cybernetics` | `_Onyx/Cybernetics/CyberneticsComponent.cs:8` with `:11 [DataField, AutoNetworkedField] public bool Disabled;` — `:20 TryComp(part, out CyberneticsComponent? cybernetics) && cybernetics.Disabled` | **DIFFERENT namespace**: `WG/Content.Shared/_Shitmed/Cybernetics/CyberneticsComponent.cs:3,9` with `:15 [DataField, AutoNetworkedField] public bool Disabled = false;`. **The field is identical**; only the namespace differs. Onyx's version has three extra fields (`Effects`, `NightVisionEnabled`, `ThermalVisionEnabled`) this file does not use. |
| `CCVars.WoundsBodyPartFunctionalityEnabled` | `Content.Shared.CCVar` | `_Onyx/CCVar/CCVars.Wounds.cs:7 public static readonly CVarDef<bool> WoundsBodyPartFunctionalityEnabled = CVarDef.Create("wounds.body_part_functionality_enabled", false, CVar.SERVER | CVar.ARCHIVE);` — `:23` | **MISSING** (Onyx file). Arrives by vendoring `_Onyx/CCVar/CCVars.Wounds.cs`; Wolfgate's `CCVars` is `WG/Content.Shared/CCVar/CCVars.cs:15 public sealed partial class CCVars : CVars` in namespace `Content.Shared.CCVar` so a partial adds cleanly. No cvar-name collisions: `wounds.*`, `explosion.damage_variation`, `explosion.wounding_multiplier` have zero hits in `WG/Content.Shared/CCVar/`. |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(body)` (`:61`) | SAME at call site (§0.1) |
| `BodyPartComponent.Body` | `Content.Shared.Body.Part` | `:29,30` | SAME |
| `IConfigurationManager.GetCVar` | Robust | `:23` | SAME (`IConfigurationManager.cs:155`) |
| `INetManager`, `EnsureComp`, `Dirty`, `TryComp` | Robust | — | SAME |
| `using Content.Shared.Body;` | — | `:1` | resolves (namespace exists via child namespaces) but brings in nothing this file needs |

### 7.3 Adaptations

**A. `CyberneticsComponent` namespace (DIFFERENT).**

(a) **Compat shim, file verbatim.** Two pieces in `Content.Shared/_WF/Wolfmed/Compat/`:
```csharp
// OnyxCyberneticsNamespace.cs — makes the `using` resolve
namespace Content.Shared._Onyx.Cybernetics;
/// <summary>Placeholder so vendored _Onyx files can `using` this namespace.</summary>
internal static class OnyxCyberneticsNamespaceMarker;
```
```csharp
// GlobalUsings.cs
global using CyberneticsComponent = Content.Shared._Shitmed.Cybernetics.CyberneticsComponent;
```
The `global using` alias is assembly-wide in `Content.Shared` and there is exactly one `CyberneticsComponent` type in Wolfgate, so it is unambiguous. **Caveat:** a `global using` alias shadows nothing but it does apply to every file in `Content.Shared`, including future Onyx vendors — acceptable but note it in the port manifest.

(b) **Preferred in practice — 1-line `// WOLFGATE` edit** at `:5`:
```csharp
using Content.Shared._Shitmed.Cybernetics; // WOLFGATE: Wolfgate's cybernetics live under _Shitmed
```
Simplest, local, self-documenting. Recommend (b) over (a) here; a global alias for a one-line `using` swap is disproportionate.

**B. `CCVars.Wounds` (MISSING).** No shim needed — vendor `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` verbatim to `WG/Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs`.

**C. Nothing else.** Every other symbol is SAME.

### 7.4 Directed subscriptions

**None.** `Initialize()` is not even overridden. Public API only: `GetState(Entity<WoundableComponent?>)` (`:18`), `Refresh(EntityUid body)` (`:56`), `RefreshPart(EntityUid body, EntityUid part)` (`:65`). Raises `BodyPartFunctionalityChangedEvent` on the part (`:76`).

### 7.5 Networking / prediction

Mixed on purpose: `GetState` (`:18`) is **not** server-gated (the client needs it — `FractureEffectSystem.GetEffect` calls it at `FractureEffectsSystem.cs:173` inside the movement-speed handler), while `Refresh` is (`:58 if (!_net.IsServer || !HasComp<WoundHostComponent>(body)) return;`). `BodyPartFunctionalityComponent` is `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]` (`WoundDamageComponents.cs:173`) so the cached state replicates.

One consequence worth flagging: because `RefreshPart` is server-only but `GetState` is not, the client's `GetEffect` fallback path reads the **replicated** `BodyPartFunctionalityComponent.State` only indirectly — it recomputes `GetState` live. With `wounds.body_part_functionality_enabled` defaulting to **`false`** (`CCVars.Wounds.cs:8`) this whole path short-circuits to `Functional` on both sides, so prediction is trivially consistent out of the box. Turning the cvar on is a `CVar.SERVER` change → the client receives it, so `GetCVar` agrees. Good.

No server-only Wolfgate dependency.

**Port difficulty: verbatim** (with a 1-line `using` swap; call it "light edits" if the vendored file must not be touched at all, in which case use option (a)).

---

## 8. `WoundStatusEffectSystem.cs` (213 lines)

### 8.1 Purpose and intra-Onyx dependencies

The wound-lifecycle hub: on every wound created/changed/state-changed/removed it refreshes the part's pain floor, applies one-time pain spikes, refreshes functionality, and applies or removes the `StatusEffectNew` effect declared by the wound's `WoundStatusEffectBehavior` — removing it only when no other active wound on the same target still grants it.

Onyx deps: `WoundSystem.GetWounds`, `PainSystem` (`RefreshWoundPain`, `ApplyOneTimePain`), `BodyPartFunctionalitySystem.RefreshPart`, `WoundBehaviors.cs` (`WoundStatusEffectBehavior`), `WoundPrototype.cs` (`WoundPrototype.TryGetBehavior`), `WoundDamageComponents.cs` (`WoundableComponent`, `WoundComponent`, `WoundState`), `WoundEvents.cs` (four wound events).

### 8.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `StatusEffectsSystem` (new) | `Content.Shared.StatusEffectNew` | `[Dependency] private StatusEffectsSystem _statusEffects` (`:22`) | **MISSING** (D1 ports it) |
| `.TryUpdateStatusEffectDuration` | idem | `StatusEffectSystem.API.cs:134 public bool TryUpdateStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null)` — `:171` | **MISSING** (arrives with D1) |
| `.HasStatusEffect` | idem | `:169 public bool HasStatusEffect(EntityUid target, EntProtoId effectProto)` — `:172` | **MISSING** (arrives with D1) |
| `.TrySetStatusEffectDuration` | idem | `:91 public bool TrySetStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null)` — `:173` | **MISSING** (arrives with D1) |
| `.TryRemoveStatusEffect` | idem | `:143 public bool TryRemoveStatusEffect(EntityUid target, EntProtoId effectProto)` — `:195` | **MISSING** (arrives with D1) |
| `StatusEffectComponent` | `Content.Shared.StatusEffectNew.Components` | `EntProtoId<StatusEffectComponent> statusEffect` (`:199`) | **MISSING** (arrives with D1) |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(uid)` (`:190`) | SAME at call site (§0.1) |
| `BodyPartComponent.Body` | `Content.Shared.Body.Part` | `CompOrNull<BodyPartComponent>(part)?.Body` (`:146,162`) | SAME |
| `EntProtoId<T>` → `EntProtoId` implicit | Robust | `behavior.StatusEffect` passed to methods taking `EntProtoId` | SAME (`EntProtoId.cs:72`) |
| `ProtoId<WoundPrototype>` | Robust + Onyx | `:136,150` | SAME |
| `FixedPoint2` | `Content.Shared.FixedPoint` | — | SAME |
| `INetManager`, `IPrototypeManager`, `CompOrNull`, `TryComp` | Robust | — | SAME |
| `using Content.Shared.Body;` | — | `:1` | resolves, unused |

**This file has no Wolfgate-specific gap at all beyond StatusEffectNew.** Every non-`StatusEffectNew` symbol is SAME.

### 8.3 Adaptations

**A. `StatusEffectNew` (MISSING).** Resolved by D1 — vendor `Content.Shared/StatusEffectNew/` (13 files), the client/server halves, and the `StatusEffects` prototypes under `Resources/Prototypes/_Onyx/StatusEffects/` (directory confirmed present in the Onyx checkout).

If D1 were ever reversed, the fallback shim would be:
```csharp
namespace Content.Shared._WF.Wolfmed.Compat;

/// <summary>Maps Onyx StatusEffectNew calls onto Wolfgate's key-based StatusEffectsSystem.</summary>
public sealed class WolfmedStatusEffectCompatSystem : EntitySystem
{
    public bool TryUpdateStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null);
    public bool TrySetStatusEffectDuration(EntityUid target, EntProtoId effectProto, TimeSpan? duration = null, TimeSpan? delay = null);
    public bool TryRemoveStatusEffect(EntityUid target, EntProtoId effectProto);
    public bool HasStatusEffect(EntityUid target, EntProtoId effectProto);
}
```
plus an `EntProtoId → status-effect-key` prototype mapping, and a `// WOLFGATE` edit to the `[Dependency]` line (`:22`). Wolfgate's old system is `WG/Content.Shared/StatusEffect/StatusEffectsSystem.cs:126 public bool TryAddStatusEffect(EntityUid uid, string key, TimeSpan time, bool refresh, string component, StatusEffectsComponent? status = null)` / `:258 TryRemoveStatusEffect(EntityUid uid, string key, …)` / `:324 HasStatusEffect(EntityUid uid, string key, …)`. Note: the old system cannot express "permanent until removed" (`duration == null`) cleanly, and cannot apply effects **to a body part** (`behavior.ApplyToPart`, `:159,179,183`) because it requires `StatusEffectsComponent` and an `AllowedEffects` whitelist on the entity. **Do not take this path** — it is a large behavioural downgrade. D1 is correct.

**B. Nothing else.**

### 8.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundableComponent` | `WoundCreatedEvent` | none |
| `WoundableComponent` | `WoundChangedEvent` | none |
| `WoundableComponent` | `WoundStateChangedEvent` | none |
| `WoundableComponent` | `WoundRemovedEvent` | none |

Intra-Onyx note: `WoundBleedingSystem` subscribes the same four events but on `WoundBleedingComponent` (`WoundBleedingSystem.cs:40-43`) and `WoundInternalBleedingSystem` on `WoundInternalBleedingComponent` (`:20-22`) — different components, so no duplicate. `WoundScarSystem.cs:23` subscribes `<WoundComponent, WoundStateChangedEvent>` — also distinct.

### 8.5 Networking / prediction

Shared. The pain/functionality refresh calls at the top of each handler (`:39,53,68,97`) run on **both** sides; the status-effect apply/remove work is server-gated (`:41,56,72,99,111,121`). `HandlePartRemoved` (`:109`) / `HandlePartInserted` (`:119`) / `RefreshPartWounds` (`:129`) are called from `FractureEffectSystem` (`:64,71`) and from `Content.Server/_Onyx/EntityEffects/Effects/Transform/SpeciesChangeEntityEffectSystem.cs:160`.

No server-only Wolfgate dependency (once `StatusEffectNew` is vendored as shared).

**Port difficulty: verbatim** (conditional on D1).

---

## 9. `WoundBehaviors.cs` (152 lines)

### 9.1 Purpose and intra-Onyx dependencies

Pure data definitions: the `WoundBehavior` base and the six behaviour "bricks" (bleeding, internal bleeding, scar, pain, status effect, functionality) plus `WoundStageDefinition`. No system, no logic.

Onyx deps: `WoundDamageComponents.cs` (`BodyPartFunctionalityState`). Consumed by `WoundPrototype.TryGetBehavior<T>`.

### 9.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `StatusEffectComponent` | `Content.Shared.StatusEffectNew.Components` | `:102 [DataField(required: true)] public EntProtoId<StatusEffectComponent> StatusEffect;` | **MISSING** (arrives with D1) |
| `FixedPoint2` | `Content.Shared.FixedPoint` | `:27,63,83,106,143` | SAME |
| `LocId` | Robust | `:135 public LocId Name;`, `:138 public LocId? ExamineDescription;` | SAME (`Robust.Shared/Localization/LocId.cs:15`) |
| `EntProtoId<T>` | Robust | `:102` — constraint `where T : IComponent, new()` | SAME (`EntProtoId.cs:62`); `StatusEffectComponent` satisfies it |
| `[ImplicitDataDefinitionForInheritors]` | Robust | `:12` | SAME |
| `[DataDefinition]`, `[DataField]` | Robust | — | SAME |
| `TimeSpan?` serialization | Robust | `:110 public TimeSpan? Duration;` | SAME — RT 277 serialises `TimeSpan` from a seconds-valued number |

### 9.3 Adaptations

**A. `StatusEffectComponent` (MISSING).** Arrives with D1. If it did not, the only change would be `:102` → `public EntProtoId StatusEffect;` (`// WOLFGATE`), losing compile-time component checking; but see §8.3-A — don't.

**B. Nothing else.** This file needs **zero** Wolfgate-specific adaptation.

One thing to verify at YAML-load time, not compile time: `WoundFunctionalityBehavior.State` (`:124`) is `BodyPartFunctionalityState`, and `WoundStageDefinition.Name`/`ExamineDescription` are `LocId`s resolved from `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` (present in the Onyx checkout per the handoff). Missing FTL keys will not fail the linter but will render as raw key names.

### 9.4 Directed subscriptions

**None** (data definitions only).

### 9.5 Networking / prediction

Not a system. These are `[DataDefinition]`s reached through `WoundPrototype`, so they exist identically on client and server via prototype replication. `WoundStatusEffectBehavior.StatusEffect` is only ever *consumed* by server-gated code (`WoundStatusEffectSystem.cs:171-173,195`), and `WoundPainBehavior` by server-gated `PainSystem` code; `WoundFunctionalityBehavior` **is** read client-side (`BodyPartFunctionalitySystem.cs:37-50`).

No server-only Wolfgate dependency.

**Port difficulty: verbatim** (conditional on D1).

---

## 10. `ReagentTreatmentEffects.cs` (55 lines)

### 10.1 Purpose and intra-Onyx dependencies

Adds a `TreatmentCapabilities` data field to the three upstream healing entity effects (`HealthChange`, `EvenHealthChange`, `DistributedHealthChange`) via `partial class`, and defines the new `MendFractures` entity effect (which fracture wounds a reagent can reduce, and between which grades).

Onyx deps: `WoundPrototype.cs` (`WoundPrototype.Name`), `WoundDamageComponents.cs` (`TreatmentCapability` enum at `:203`, `FractureGrade` at `:282`).

### 10.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `HealthChange` (partial) | `Content.Shared.EntityEffects.Effects.Damage` | `:9 public sealed partial class HealthChange` | **DIFFERENT.** Wolfgate: `WG/Content.Server/EntityEffects/Effects/HealthChange.cs:18 public sealed partial class HealthChange : EntityEffect` in **`Content.Server.EntityEffects.Effects`**. Different assembly (Content.Server, not Content.Shared) and different namespace → the `partial` will not merge; it would declare a **second, unrelated class**. |
| `EvenHealthChange` (partial) | idem | `:15` | **DIFFERENT** — `WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs:16 public sealed partial class EvenHealthChange : EntityEffect` |
| `DistributedHealthChange` (partial) | idem | `:21` | **MISSING** — no such type anywhere in Wolfgate |
| `EntityEffectBase<T>` | `Content.Shared.EntityEffects` | `Content.Shared/EntityEffects/EntityEffect.cs:72 public abstract partial class EntityEffectBase<T> : EntityEffect where T : EntityEffectBase<T>` — `:27 public sealed partial class MendFractures : EntityEffectBase<MendFractures>` | **MISSING** (§0.9) |
| `EntityEffect.Probability` | `Content.Shared.EntityEffects` | `:49 ("chance", Probability)` | SAME name (`WG/Content.Shared/EntityEffects/EntityEffect.cs:38 [DataField("probability")] public float Probability = 1.0f;`) but the containing base class differs |
| `EntityEffect.EntityEffectGuidebookText` | idem | `Content.Shared/EntityEffects/EntityEffect.cs:53 public virtual string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;` — overridden at `:42` as `public override string EntityEffectGuidebookText(...)` | **DIFFERENT.** Wolfgate: `WG/Content.Shared/EntityEffects/EntityEffect.cs:32 protected abstract string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);` — different name, `protected`, `abstract` not `virtual`. |
| `EntityEffect.Effect` | idem | Onyx has no such member on the base (ECS model) | Wolfgate: `WG/.../EntityEffect.cs:47 public abstract void Effect(EntityEffectBaseArgs args);` — **an abstract member Onyx effects do not implement** |
| `TreatmentCapability` | `Content.Shared._Onyx.Wounds` | `WoundDamageComponents.cs:203 public enum TreatmentCapability : byte` | Onyx-internal — arrives with the wounds port |
| `FractureGrade` | idem | `WoundDamageComponents.cs:282` | Onyx-internal |
| `ProtoId<WoundPrototype>` | Robust + Onyx | `:31 HashSet<ProtoId<WoundPrototype>> Wounds = ["BoneFractureWound"];` | SAME mechanism |
| `Loc.GetString` | Robust | `:45,48,52,53` | SAME |
| Loc keys | — | `entity-effect-guidebook-all-fractures`, `entity-effect-guidebook-mend-fractures`, `fracture-grade-{hairline,simple,displaced,comminuted}` — all in `Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl:7,13,15-18` | **MISSING** in Wolfgate — vendor the ftl. It uses `NATURALFIXED` (`:11`) and `MANY` (`:5`), both registered in Wolfgate (`WG/Content.Shared/Localizations/ContentLocalizationManager.cs:38,52`). ✔ |

### 10.3 Adaptations

There is no shim that makes this file compile. It is the clearest **heavy adaptation** in the set.

(b) **Recommended — rewrite as three separate pieces, all `// WOLFGATE`-marked or `_WF`:**

1. `TreatmentCapabilities` on `HealthChange`/`EvenHealthChange`: since those are server classes in Wolfgate, add the field as a `// WOLFGATE` `[DataField]` directly in `WG/Content.Server/EntityEffects/Effects/HealthChange.cs` and `EvenHealthChange.cs`:
   ```csharp
       // WOLFGATE: Wolfmed treatment scoping
       [DataField]
       public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
   ```
   (2 files, 3 lines each).
2. `DistributedHealthChange`: does not exist. Either port it from Onyx as a **new** Wolfgate-style `EntityEffect` in `Content.Server/_WF/Wolfmed/EntityEffects/DistributedHealthChange.cs`, or drop it from Phase 4 and remap the reagents that use it onto `EvenHealthChange`. **Recommend dropping it for Phase 1-4** and recording it in the manifest.
3. `MendFractures`: rewrite as a Wolfgate-style effect in `Content.Server/_WF/Wolfmed/EntityEffects/MendFractures.cs`:
   ```csharp
   namespace Content.Server._WF.Wolfmed.EntityEffects;

   /// <summary>Reduces fracture severity on every matching broken part.</summary>
   public sealed partial class MendFractures : EntityEffect
   {
       [DataField] public HashSet<ProtoId<WoundPrototype>> Wounds = ["BoneFractureWound"];
       [DataField] public FractureGrade MinimumGrade = FractureGrade.Hairline;
       [DataField] public FractureGrade MaximumGrade = FractureGrade.Comminuted;
       [DataField] public FixedPoint2 Amount = 1;

       protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);
       public override void Effect(EntityEffectBaseArgs args);
   }
   ```
   The body of `Effect` is `MendFracturesEntityEffectSystem.Effect` from `ReagentTreatmentSystems.cs:78-93`, with `args.Scale` replaced by `(args as EntityEffectReagentArgs)?.Scale.Float() ?? 1f` (Wolfgate's scale lives on `EntityEffectReagentArgs.Scale`, `WG/Content.Shared/EntityEffects/EntityEffect.cs:117`).

(a) A shim is not possible: you cannot retrofit `EntityEffectBase<T>`/`EntityEffectSystem<T,TEffect>` onto Wolfgate's `EntityEffect` without also porting `SharedEntityEffectsSystem` and rewriting every one of Wolfgate's ~150 existing effects.

(c) Upstream hook — port Wizden's ECS `EntityEffects` refactor wholesale into Wolfgate. Out of scope; would be its own project the size of StatusEffectNew.

### 10.4 Directed subscriptions

**None** (data definitions only).

### 10.5 Networking / prediction

Onyx's model is shared/predicted (effects raised through `SharedEntityEffectsSystem`). Wolfgate's `EntityEffect.Effect(EntityEffectBaseArgs)` is invoked from server-side reagent metabolism only — `HealthChange` and `EvenHealthChange` both live in `Content.Server`. So the Wolfgate rewrite is **server-only, unpredicted**, which is consistent with the rest of Wolfgate's chemistry. Fine for Phase 4.

**Port difficulty: heavy adaptation** (effectively a rewrite; the vendored file should be dropped, not vendored).

---

## 11. `ReagentTreatmentSystems.cs` (94 lines)

### 11.1 Purpose and intra-Onyx dependencies

The ECS halves of the four treatment effects: `partial`s on the three upstream `*EntityEffectSystem`s that route healing through `WoundDamageRoutingSystem.WithTreatmentCapabilities` when the target is a `WoundHost`, plus the standalone `MendFracturesEntityEffectSystem`.

Onyx deps: `WoundDamageRoutingSystem.WithTreatmentCapabilities`, `WoundFractureSystem.GetFracture`, `WoundSystem.ChangeSeverity`, `WoundDamageComponents.cs` (`WoundHostComponent`, `TreatmentCapability`), `ReagentTreatmentEffects.cs`.

### 11.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `HealthChangeEntityEffectSystem` (partial) | `Content.Shared.EntityEffects.Effects.Damage` | `:10 public sealed partial class HealthChangeEntityEffectSystem` with `[Dependency] private DamageableSystem _damageable` assumed from the other half | **MISSING** — no such type in Wolfgate |
| `EvenHealthChangeEntityEffectSystem` (partial) | idem | `:33` | **MISSING** |
| `DistributedHealthChangeEntityEffectSystem` (partial) | idem | `:52` | **MISSING** |
| `EntityEffectSystem<T, TEffect>` | `Content.Shared.EntityEffects` | `SharedEntityEffectsSystem.cs:174 public abstract partial class EntityEffectSystem<T, TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase<TEffect>`, `:182 protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args);` — `:71-72 public sealed partial class MendFracturesEntityEffectSystem : EntityEffectSystem<WoundHostComponent, MendFractures>` | **MISSING** (§0.9) |
| `EntityEffectEvent<T>` | idem | `EntityEffectEvent.cs:9 [ByRefEvent, Access(typeof(SharedEntityEffectsSystem))] public readonly record struct EntityEffectEvent<T>(T Effect, float Scale, EntityUid? User)` | **MISSING** |
| `DamageableSystem.TryChangeDamage` | Onyx `Content.Shared.Damage.Systems` | `DamageableSystem.API.cs:68 public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)` — `:19-20 _damageable.TryChangeDamage(entity.AsNullable(), change, args.Effect.IgnoreResistances, interruptsDoAfters: false)` | **DIFFERENT — the overload trap (§0.7).** Wolfgate's `WG/.../DamageableSystem.cs:190 public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false, bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, …)` **silently binds** to this call (via `Entity<T>` → `EntityUid` → `EntityUid?`), returning `DamageSpecifier?`. Here the return is discarded inside a lambda, so it compiles and behaves correctly by luck. The instance method also **shadows any extension shim**. |
| `DamageableSystem.HealEvenly` | idem | `DamageableSystem.API.cs:177 public DamageSpecifier HealEvenly(Entity<DamageableComponent?> ent, FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null)` — `:42` | **MISSING** (§0.8 shim binds cleanly — no instance member of that name) |
| `DamageableSystem.HealDistributed` | idem | `:248` — `:61` | **MISSING** (shimmable) |
| `DamageableComponent` | Onyx `Content.Shared.Damage.Components` | `Entity<DamageableComponent>` (`:14,23,37,56`) | namespace differs; file has `using Content.Shared.Damage;` (`:2`) → resolves |
| `SharedBodySystem.GetBodyChildren` | `Content.Shared.Body.Systems` | `_body.GetBodyChildren(entity)` (`:83`) | SAME at call site (§0.1) |
| `DamageSpecifier` ctor + `* float` | `Content.Shared.Damage` | `new DamageSpecifier(args.Effect.Damage) * args.Scale` (`:16`) | SAME (`WG/Content.Shared/Damage/DamageSpecifier.cs` has the copy ctor and `operator *`) |
| `DamageGroupPrototype` | `Content.Shared.Damage.Prototypes` | via `args.Effect.Damage` group dictionary | SAME |
| `Entity<T>.AsNullable()` | Robust | `:19,42,61` | SAME |
| `System.Linq` `.Any()` | BCL | `:17,45,64` | SAME |

### 11.3 Adaptations

Same verdict as §10: **not shimmable**, because three of the four classes it extends do not exist.

(b) **Recommended — fold into the §10.3 rewrite.** The `WithTreatmentCapabilities` scoping is the only genuinely new behaviour; it can be expressed as a `// WOLFGATE` wrapper inside Wolfgate's server `HealthChange.Effect`:
```csharp
        // WOLFGATE: route healing through the wound system for wound hosts
        if (args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity)
            && damageSpec.DamageDict.Values.Any(amount => amount < 0))
        {
            args.EntityManager.System<WoundDamageRoutingSystem>()
                .WithTreatmentCapabilities(args.TargetEntity, TreatmentCapabilities, Apply);
            return;
        }
        Apply();
```
That is ~8 `// WOLFGATE` lines in each of `WG/Content.Server/EntityEffects/Effects/HealthChange.cs` and `EvenHealthChange.cs`, and it needs `WoundDamageRoutingSystem` to be reachable from `Content.Server` (it is — `Content.Shared` is referenced).

**Watch the trap** while doing this: inside Wolfgate's `HealthChange.Effect`, the existing call is already the old-style one, so no change there. But if anyone later re-vendors `ReagentTreatmentSystems.cs:19-20` verbatim, the `TryChangeDamage` call binds to the old overload and **the `origin` parameter would land on `damageable`** if it were ever passed positionally. Add a `// WOLFGATE` comment at every `TryChangeDamage` site in vendored code.

(a)/(c) — same as §10.3.

### 11.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `WoundHostComponent` | `EntityEffectEvent<MendFractures>` (via `EntityEffectSystem<T,TEffect>.Initialize`, `SharedEntityEffectsSystem.cs:178`) | n/a — the whole mechanism is MISSING in Wolfgate |

The three `partial` systems declare **no** subscriptions of their own (their `Initialize` lives in the other half of the partial, which is not being ported).

### 11.5 Networking / prediction

Shared/predicted in Onyx; **server-only after the Wolfgate rewrite** (see §10.5). `WoundDamageRoutingSystem` is shared in Onyx, so calling it from `Content.Server` is fine.

**Port difficulty: heavy adaptation** (drop the file; re-express as `// WOLFGATE` hooks in two server files).

---

## 12. `SuppressPainEntityEffect.cs` (38 lines)

### 12.1 Purpose and intra-Onyx dependencies

Defines the `SuppressPain` reagent effect (amount, decay duration, identifier, recovery multiplier) and its ECS system, which forwards to `PainSystem.SuppressPain`. This is how painkillers work.

Onyx deps: `PainSystem.SuppressPain`, `WoundDamageComponents.cs` (`PainComponent`).

### 12.2 External symbols

| Symbol | Namespace | Onyx signature used | Wolfgate status |
|---|---|---|---|
| `EntityEffectSystem<T, TEffect>` | `Content.Shared.EntityEffects` | `:7 public sealed partial class SuppressPainEntityEffectSystem : EntityEffectSystem<PainComponent, SuppressPain>` | **MISSING** (§0.9) |
| `EntityEffectEvent<T>` | idem | `:11 protected override void Effect(Entity<PainComponent> entity, ref EntityEffectEvent<SuppressPain> args)` | **MISSING** |
| `EntityEffectBase<T>` | idem | `:18 public sealed partial class SuppressPain : EntityEffectBase<SuppressPain>` | **MISSING** |
| `EntityEffect.EntityEffectGuidebookText` | idem | `:32 public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)` | **DIFFERENT** — Wolfgate's is `WG/Content.Shared/EntityEffects/EntityEffect.cs:32 protected abstract string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys);` |
| `EntityEffect.Probability` | idem | `:34 ("chance", Probability)` | SAME name (`WG/.../EntityEffect.cs:38`) |
| `EntityEffect.Effect(EntityEffectBaseArgs)` | idem | not implemented by Onyx effects | Wolfgate requires it (`:47 public abstract void Effect(EntityEffectBaseArgs args);`) |
| `EntityEffectEvent<T>.Scale` | idem | `args.Scale` (`:14`) — `float` | Wolfgate's equivalent is `EntityEffectReagentArgs.Scale` (`WG/.../EntityEffect.cs:117 public FixedPoint2 Scale;`) — **DIFFERENT type (`FixedPoint2` vs `float`) and only present on the reagent subclass** |
| `PainSystem.SuppressPain` | `Content.Shared._Onyx.Wounds` | `PainSystem.cs:331 public bool SuppressPain(Entity<PainComponent?> entity, string identifier, FixedPoint2 amount, TimeSpan decayDuration, float recoveryMultiplier = 1f)` | Onyx-internal — arrives with the wounds port |
| `FixedPoint2` | `Content.Shared.FixedPoint` | `:21,36` | SAME |
| `TimeSpan` `[DataField]` | Robust | `:24 public TimeSpan DecayDuration;` | SAME |
| Loc key `entity-effect-guidebook-suppress-pain` | — | `Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl:1-5` (verified, uses `NATURALFIXED` and `MANY`) | **MISSING** — vendor the ftl; both FTL functions exist in Wolfgate (`ContentLocalizationManager.cs:38,52`) ✔ |

### 12.3 Adaptations

(b) **Recommended — rewrite as a Wolfgate-style effect.** `Content.Server/_WF/Wolfmed/EntityEffects/SuppressPain.cs`:
```csharp
namespace Content.Server._WF.Wolfmed.EntityEffects;

/// <summary>Suppresses pain on the target, decaying over time.</summary>
public sealed partial class SuppressPain : EntityEffect
{
    [DataField(required: true)] public FixedPoint2 Amount;
    [DataField(required: true)] public TimeSpan DecayDuration;
    [DataField] public string Identifier = "PainSuppressant";
    [DataField] public float RecoveryMultiplier = 1f;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-suppress-pain",
            ("chance", Probability), ("amount", Amount.Float()),
            ("duration", DecayDuration.TotalSeconds), ("recoveryMultiplier", RecoveryMultiplier));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = args is EntityEffectReagentArgs reagent ? reagent.Scale.Float() : 1f;
        args.EntityManager.System<PainSystem>()
            .SuppressPain(args.TargetEntity, Identifier, Amount * scale, DecayDuration, RecoveryMultiplier);
    }
}
```
Note `SuppressPain(Entity<PainComponent?>, …)` accepts a bare `EntityUid` via `implicit operator Entity<T?>(EntityUid)` (`WG/RobustToolbox/Robust.Shared/GameObjects/Entity.cs:39`), and `PainSystem.SuppressPain` already resolves the component and no-ops on failure (`PainSystem.cs:331-337`), so the `PainComponent` guard Onyx got from `EntityEffectSystem<PainComponent, …>` is preserved.

Also note the `Scale` semantics change: Onyx's `EntityEffectEvent<T>.Scale` is a `float` the effect framework clamps to ≤1 unless `Scaling` is set (`SharedEntityEffectsSystem.cs:141-142`); Wolfgate's `EntityEffectReagentArgs.Scale` is a `FixedPoint2` set by the metabolism and **is not clamped**. Wolfmed painkillers will scale differently at high reagent volumes — flag for D4 tuning.

(a) Not shimmable (§0.9).
(c) Not proportionate.

### 12.4 Directed subscriptions

| Component | Event | Wolfgate conflict |
|---|---|---|
| `PainComponent` | `EntityEffectEvent<SuppressPain>` (registered by the base class, `SharedEntityEffectsSystem.cs:178`) | n/a — mechanism MISSING |

After the rewrite: **no subscriptions at all** (Wolfgate effects are invoked directly by the metabolism, not via events).

### 12.5 Networking / prediction

Onyx: shared/predicted. After the rewrite: **server-only**, which matches `PainSystem.SuppressPain`'s own `if (!_net.IsServer) return false;` guard at `PainSystem.cs:333`. No loss.

**Port difficulty: heavy adaptation** (small file, but a full rewrite; drop the vendored copy).

---

## 13. Port difficulty summary

| File | Rating | Dominant cost |
|---|---|---|
| `WoundBehaviors.cs` | **verbatim** (needs D1) | `EntProtoId<StatusEffectComponent>` only |
| `WoundStatusEffectSystem.cs` | **verbatim** (needs D1) | nothing beyond StatusEffectNew |
| `BodyPartFunctionalitySystem.cs` | **verbatim / light edits** | 1-line `using` swap for `CyberneticsComponent`; vendor `CCVars.Wounds.cs` |
| `FractureAlertSystem.cs` | **light edits** | `BodyPartComponent.FractureProfile`; vendor the `BrokenBones` alert proto + rsi + ftl |
| `PainSystem.cs` | **light edits** | 3 shims: `TryEmoteWithChat`, `TryUpdateParalyzeDuration`, `PainNumbnessStatusEffectComponent` |
| `WoundFractureSystem.cs` | **light edits** | missing `using Content.Shared.Damage;`; `GetPositiveDamage` shim; `FractureProfile` field |
| `AmputationSystem.cs` | **heavy adaptation** | `BodyPartType.Chest`→`Torso` ×3, `Parent` ×2, 6 missing part fields, `TryDetachPart` + `GetAllDamage` shims, Shitmed sever-cascade reconciliation |
| `FractureEffectsSystem.cs` | **heavy adaptation** | hands API rewrite (`Hand` is a class, no `HandLocation.Functional*`), `OrganGot*Event` bridge with subtree semantics, DoAfter hook |
| `OrganDamageSystem.cs` | **heavy adaptation** | `OrganComponent` has no `Health`/`MaxHealth` and lives in a different namespace; `OrganHealthSystem` needs re-expressing; `WoundBleedingSystem`→`BloodstreamSystem` shared/server split |
| `ReagentTreatmentEffects.cs` | **heavy adaptation** | drop and rewrite — `EntityEffectBase<T>` missing, `HealthChange`/`EvenHealthChange` are server classes, `DistributedHealthChange` absent |
| `ReagentTreatmentSystems.cs` | **heavy adaptation** | drop and rewrite as `// WOLFGATE` hooks in two server files |
| `SuppressPainEntityEffect.cs` | **heavy adaptation** | drop and rewrite as an old-style `EntityEffect` |

---

## 14. Wolfgate files that would need hooks

Upstream (`// WOLFGATE`, one- or two-line where possible):

| File | Hook | For |
|---|---|---|
| `WG/Content.Shared/DoAfter/SharedDoAfterSystem.cs` (after the Goobstation block at `:207-214`) | raise `GetManipulationDurationMultiplierEvent(args.Used)` on `args.User` and apply `Multiplier` to `args.Delay` | `FractureEffectsSystem` |
| `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:60,85` | change `public void TryEmoteWithChat` → `public override bool TryEmoteWithChat` (2 lines) | `PainSystem` (option (a) of §1.3-A) |
| `WG/Content.Shared/Chat/SharedChatSystem.cs` | new partial file adding `virtual bool TryEmoteWithChat(...)` (in `_WF/Wolfmed/Compat`, not an edit to the upstream file) | `PainSystem` |
| `WG/Content.Shared/Body/Part/BodyPartComponent.cs` | **optional but strongly recommended**: six additive `[DataField]`s (`FractureProfile`, `MaxDamage`, `AmputationThresholds`, `DismembermentFinishingDamage`, `AmputationConsequenceSeverity`, `DismembermentSeverity`) — removes ~15 edits across 5 vendored files. Contradicts D8's letter; put to the orchestrator. | `WoundFractureSystem`, `FractureAlertSystem`, `AmputationSystem`, `WoundDamageComponents` |
| `WG/Content.Shared/Body/Organ/OrganComponent.cs` | **optional**: four additive `[DataField]`s (`Health`, `MaxHealth`, `DestructionWound`, `DestructionWoundSeverity`) — keeps `OrganDamageSystem.cs` nearly verbatim | `OrganDamageSystem` |
| `WG/Content.Shared/Damage/Systems/DamageableSystem.cs` | **optional**: add the `Entity<DamageableComponent?>`-shaped `TryChangeDamage`/`ChangeDamage`/`HealEvenly`/`HealDistributed`/`GetPositiveDamage`/`GetAllDamage`/`GetTotalDamage` overloads | eliminates the §0.7 overload trap for the whole wounds port |
| `WG/Content.Server/EntityEffects/Effects/HealthChange.cs:18` | add `TreatmentCapabilities` `[DataField]` + wound-routing branch in `Effect` | `ReagentTreatment*` |
| `WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs:16` | same | `ReagentTreatment*` |
| `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs` | none required if `TryDetachPart` is a `_WF` extension over `GetParentPartAndSlotOrNull` (`:430`) + `DetachPart` (`:702`) — but the Shitmed sever cascade at `:191-212` must be reconciled with D2 | `AmputationSystem` |

New `_WF/Wolfmed/Compat` files implied by this report:

- `DamageableSystemOnyxCompat.cs` — `ChangeDamage`, `HealEvenly`, `HealDistributed`, `GetPositiveDamage` ×2, `GetAllDamage`, `GetTotalDamage` (**not** `TryChangeDamage`, which is shadowed).
- `BodySystemOnyxCompat.cs` — `TryDetachPart(this SharedBodySystem, EntityUid, bool)`.
- `StunSystemOnyxCompat.cs` — `TryUpdateParalyzeDuration(this SharedStunSystem, EntityUid, TimeSpan?, bool)`.
- `OnyxBodyEvents.cs` + `WolfmedBodyEventBridgeSystem.cs` — `OrganGotInsertedEvent` / `OrganGotRemovedEvent` in `Content.Shared.Body`, bridged from `BodyPartAddedEvent`/`BodyPartRemovedEvent`/`OrganAddedToBodyEvent`/`OrganRemovedFromBodyEvent`, **fanning out over the part subtree**.
- `SharedChatSystem.Wolfmed.cs` — virtual `TryEmoteWithChat`.
- `WolfmedBodyPartComponent.cs` (or the upstream-field hook) and `WolfmedOrganHealthComponent.cs` + `WolfmedOrganHealthSystem.cs`.
- `WolfmedBloodstreamBridge` (shared interface + server impl) if `WoundBleedingSystem` stays shared.

Assets to vendor for these 12 files specifically:

- `Resources/Prototypes/_Onyx/Alerts/*.yml` (the `BrokenBones` alert) and `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi` (check `meta.json` licence).
- `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` (2 keys) and `Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` (7 keys).
- `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs`.
- `Content.Shared/_Onyx/Body/OrganDamageComponent.cs`.
- `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` (+ its `StatusEffectNew` entity prototype and the `ForceSayNumbDataset` dataset — **not verified against Wolfgate**).
- `Content.Shared/StatusEffectNew/` (13 files) per D1.

## 15. Things to hand back to the orchestrator

1. **`<BodyComponent, RejuvenateEvent>` duplicate** (outside my file set): `WoundSystem.cs:34` vs `WG/Content.Server/_Mono/Body/Systems/BodyRejuvenateSystem.cs:28`. Server start crash.
2. **`BodyPartType.Chest` / `.Groin` do not exist in Wolfgate.** `WoundHostComponent` defaults, `wounds.yml` body-part profiles, and `AmputationSystem` all use them. A repo-wide `Chest`→`Torso` mapping decision is needed before any YAML is vendored. `TargetBodyPart` **does** have `Groin` but uses `Torso` (`WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:15,16`).
3. **D8 vs. cost**: keeping Onyx's six part fields (and four organ fields) out of the upstream components costs ~19 `// WOLFGATE` edits spread across 5 vendored files, versus ~10 additive `[DataField]` lines in 2 upstream files. Recommend revisiting D8 for these specific fields.
4. **`OrganDamageSystem` → `WoundBleedingSystem` → `BloodstreamSystem`** is the one hard shared/server split in this file set. Decide before Phase 1 whether `WoundBleedingSystem` gets split or the `PartDamageAppliedEvent` fan-out goes server-only.
5. **`TryChangeDamage` cannot be shimmed by an extension method** — Wolfgate's old `EntityUid?` overload is applicable to every new-style call and always wins. Either add the new overloads upstream or mark every vendored call site.
