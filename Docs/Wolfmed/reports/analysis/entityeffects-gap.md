# Entity-effect generation gap: Onyx ECS `EntityEffect` vs Wolfgate class-based `EntityEffect`

Scope: analyse the wound/pain/stamina reagent effects Onyx implements on top of upstream's new
ECS `EntityEffectSystem<T,TEffect>` pattern, and design old-style Wolfgate equivalents that keep
the same `!type:` YAML tags so `wounds.yml`/reagent files port verbatim.

Onyx source: pinned commit `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (sparse checkout at
`C:\tmp\onyx`). Wolfgate source: worktree
`C:\Users\jzo12\Documents\GitHub\Wolfgate\.claude\worktrees\rules-motd-updates-11c89c` (read-only,
nothing here was modified).

---

## 1. Onyx files in scope

### Wound-adjacent, new-style (`EntityEffectSystem<T,TEffect>` / `EntityEffectBase<T>`)

| File | Onyx path | Lines |
|---|---|---|
| `SuppressPainEntityEffectSystem` / `SuppressPain` | `Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs` | 38 |
| `HealthChangeEntityEffectSystem` (partial), `EvenHealthChangeEntityEffectSystem` (partial), `DistributedHealthChangeEntityEffectSystem` (partial), `MendFracturesEntityEffectSystem` / `MendFractures` | `Content.Shared/_Onyx/Wounds/ReagentTreatmentSystems.cs` | 92 |
| `HealthChange` (partial), `EvenHealthChange` (partial), `DistributedHealthChange` (partial), `MendFractures` (data, partial declared in the file above) | `Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs` | 54 |
| `TakeStaminaDamageEntityEffectSystem` / `TakeStaminaDamage` | `Content.Shared/_Onyx/Chemistry/TakeStaminaDamageEntityEffectSystem.cs` | 30 |
| `StaminaDamageConditionSystem` / `StaminaDamageCondition` (new-style `EntityCondition`, not `EntityEffect`, but gates the two files above in YAML) | `Content.Shared/_Onyx/Chemistry/StaminaDamageCondition.cs` | 28 |

I grepped every file under `Content.Shared/_Onyx/EntityEffects/**` and `Content.Server/_Onyx/EntityEffects/**` (the sparse checkout has both) for wound/pain/bleeding/fracture terms. None matched — that folder only holds unrelated effects (`DnaScrambleEntityEffectSystem`, `SexChangeEntityEffectSystem`, `SpeciesChangeEntityEffects`, `TeleportNearbyEntityEffect`, `PlaySoundEntityEffectSystem`, `MakeUnreactiveEntityEffectSystem`, `AdjustFireStacksEntityEffect`, `DelayedSpawnEntityEffect`, `SpawnRandomQuantityEntityEffectSystem`, `ApplyEffectsNearbyEntityEffectSystem`). The wound/pain/stamina effects all live directly under `_Onyx/Wounds` and `_Onyx/Chemistry`, not under `_Onyx/EntityEffects`. So the five files above are the complete in-scope set.

### The ECS base classes these build on (upstream-new, not `_Onyx`-prefixed — Onyx has adopted a newer upstream refactor Wolfgate hasn't received)

- `Content.Shared/EntityEffects/EntityEffect.cs` — abstract `EntityEffect` (`[ImplicitDataDefinitionForInheritors]`) with `RaiseEvent(EntityUid, IEntityEffectRaiser, float scale, EntityUid? user)`, plus `EntityEffectBase<T> : EntityEffect` which raises `IEntityEffectRaiser.RaiseEffectEvent<T>`.
- `Content.Shared/EntityEffects/EntityEffectEvent.cs` — `[ByRefEvent] readonly record struct EntityEffectEvent<T>(T Effect, float Scale, EntityUid? User)`.
- `Content.Shared/EntityEffects/SharedEntityEffectsSystem.cs` — houses `EntityEffectSystem<T,TEffect> : EntitySystem where T : Component where TEffect : EntityEffectBase<TEffect>`, which does `SubscribeLocalEvent<T, EntityEffectEvent<TEffect>>(Effect)` and requires subclasses to implement `protected abstract void Effect(Entity<T> entity, ref EntityEffectEvent<TEffect> args)`. Also defines `ApplyEffects`/`TryApplyEffect`/`ApplyEffect` — the entry points other systems call, e.g. `_entityEffects.ApplyEffect(target, effect, scale)`.
- `Content.Shared/EntityConditions/SharedEntityConditionsSystem.cs` (path **not** in the documented sparse-checkout set in `WOLFMED_HANDOFF.md`, but present on disk anyway — see note below) — the parallel new-style `EntityCondition`/`EntityConditionBase<T>`/`EntityConditionSystem<T,TCon>`/`EntityConditionEvent<T>` machinery that `SuppressPain`/`StaminaDamageCondition` etc. build on for their `Conditions` arrays.

**Note on sparse checkout coverage:** `WOLFMED_HANDOFF.md`'s documented `sparse-checkout set` command does not list `Content.Shared/EntityEffects`, `Content.Shared/EntityConditions`, or `Content.Shared/Metabolism`. The live checkout's actual `git sparse-checkout list` output (captured during this analysis) is broader than the handoff doc and does include `Content.Shared/EntityEffects/` and `Content.Server/EntityEffects/` — those are on disk and were read directly. `Content.Shared/EntityConditions/` and `Content.Shared/Metabolism/` are **absent from the sparse checkout** (confirmed: `find`/`ls` on those paths returns "No such file or directory"), so I read `SharedEntityConditionsSystem.cs`, `Content.Shared/_Onyx/EntityConditions/TypedDamageThreshold.cs`, and `Content.Shared/Metabolism/MetabolizerSystem.cs` via `git -C C:/tmp/onyx show HEAD:<path>` as the task instructions permit. Their content is quoted below and is not invented.

---

## 2. What each effect does, its fields, and its YAML usage

### `SuppressPain` / `SuppressPainEntityEffectSystem`
*(`Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs`)*

```csharp
public sealed partial class SuppressPainEntityEffectSystem : EntityEffectSystem<PainComponent, SuppressPain>
{
    [Dependency] private PainSystem _pain = default!;
    protected override void Effect(Entity<PainComponent> entity, ref EntityEffectEvent<SuppressPain> args)
    {
        _pain.SuppressPain((entity.Owner, entity.Comp), args.Effect.Identifier,
            args.Effect.Amount * args.Scale, args.Effect.DecayDuration, args.Effect.RecoveryMultiplier);
    }
}
public sealed partial class SuppressPain : EntityEffectBase<SuppressPain>
{
    [DataField(required: true)] public FixedPoint2 Amount;
    [DataField(required: true)] public TimeSpan DecayDuration;
    [DataField] public string Identifier = "PainSuppressant";
    [DataField] public float RecoveryMultiplier = 1f;
}
```

Requires a `PainComponent` on the target (the ECS subscription is component-gated); calls `PainSystem.SuppressPain` to add a keyed, decaying pain suppression (identifier lets multiple sources — Desoxyephedrine vs Ibuprofen vs Ketorolac — stack independently rather than overwrite each other) that also multiplies natural pain-recovery rate while active.

YAML usage — `!type:SuppressPain` — 12 hits across `Resources/Prototypes/Reagents/{alcohol,medicine,narcotics}.yml` and `Resources/Prototypes/_Onyx/Reagents/{Medicine/medicine.yml,Narcotics/opioids.yml}`, e.g.:
```yaml
- !type:SuppressPain
  amount: 0.75
  decayDuration: 9
  identifier: Desoxyephedrine
  recoveryMultiplier: 1.75
```

### `HealthChange` / `EvenHealthChange` / `DistributedHealthChange` — `TreatmentCapabilities` partial
*(`Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs` + `ReagentTreatmentSystems.cs`)*

These are **partial-class additions to upstream's own effects**, not new effect types. `ReagentTreatmentEffects.cs` adds one field to each:
```csharp
public sealed partial class HealthChange { [DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]; }
public sealed partial class EvenHealthChange { [DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]; }
public sealed partial class DistributedHealthChange { [DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]; }
```
`ReagentTreatmentSystems.cs` adds a corresponding partial to each of the three `EntityEffectSystem`s (`HealthChangeEntityEffectSystem`, `EvenHealthChangeEntityEffectSystem`, `DistributedHealthChangeEntityEffectSystem`) that wraps the actual damage-application call:
```csharp
private void ApplyScoped(Entity<DamageableComponent> entity, bool healing,
    IReadOnlySet<TreatmentCapability> capabilities, Action apply)
{
    if (healing && HasComp<WoundHostComponent>(entity))
        _woundRouting.WithTreatmentCapabilities(entity, capabilities, apply);
    else
        apply();
}
```
`TreatmentCapability` is `public enum TreatmentCapability : byte { Biological, Mechanical, Electrical }` (`Content.Shared/_Onyx/Wounds/WoundPrototype.cs:203`). `WoundDamageRoutingSystem.WithTreatmentCapabilities` stashes the set in a per-body dictionary for the duration of the callback; `CanTreatPart` (same file, ~line 862) then gates which limbs actually receive the heal by checking whether the limb's `WoundableComponent.Profile` (Organic/Ipc/Cybernetic/...) `TreatmentCapabilities.Overlaps(capabilities)`. **Net effect: a "Biological" healing reagent heals organic wound-hosting limbs but is inert against a cybernetic/IPC limb even on the same body**, and no non-`WoundHost` entity (or non-healing/positive damage) is affected — the gate only activates for `healing && HasComp<WoundHostComponent>`.

None of the reagent YAML grepped (`_Onyx/Reagents/Medicine/*.yml`, `Reagents/medicine.yml`, `narcotics.yml`) overrides `treatmentCapabilities:` — every `!type:HealthChange` in scope relies on the default `[Biological]`, so no YAML syntax example of the override exists to copy; it would read `treatmentCapabilities: [Mechanical]` etc. if used.

### `MendFractures` / `MendFracturesEntityEffectSystem`
*(`Content.Shared/_Onyx/Wounds/ReagentTreatmentSystems.cs` + `ReagentTreatmentEffects.cs`)*

```csharp
public sealed partial class MendFracturesEntityEffectSystem : EntityEffectSystem<WoundHostComponent, MendFractures>
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private WoundSystem _wounds = default!;
    protected override void Effect(Entity<WoundHostComponent> entity, ref EntityEffectEvent<MendFractures> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        foreach (var (part, _) in _body.GetBodyChildren(entity))
        {
            if (_fractures.GetFracture(part) is not { } fracture ||
                (args.Effect.Wounds.Count != 0 && !args.Effect.Wounds.Contains(fracture.Comp1.Prototype)) ||
                fracture.Comp2.Grade < args.Effect.MinimumGrade || fracture.Comp2.Grade > args.Effect.MaximumGrade)
                continue;
            _wounds.ChangeSeverity(fracture.Owner, -amount);
        }
    }
}
public sealed partial class MendFractures : EntityEffectBase<MendFractures>
{
    [DataField] public HashSet<ProtoId<WoundPrototype>> Wounds = ["BoneFractureWound"];
    [DataField] public FractureGrade MinimumGrade = FractureGrade.Hairline;
    [DataField] public FractureGrade MaximumGrade = FractureGrade.Comminuted;
    [DataField] public FixedPoint2 Amount = 1;
}
```
Requires `WoundHostComponent`; walks every body part via `_body.GetBodyChildren`, finds an active fracture wound matching the `Wounds` filter and grade range, and reduces its severity by `Amount * Scale` per tick (matching the wounds.yml treatment stages: None → Reduced (×0.25 effects) → Mended described in the handoff).

YAML usage — `!type:MendFractures` — 2 hits: `Resources/Prototypes/_Onyx/Reagents/Medicine/first_aid.yml:29` and `medicine.yml:140`:
```yaml
- !type:MendFractures
  amount: 1
  wounds: [BoneFractureWound]
  maximumGrade: Simple
```

### `TakeStaminaDamage` / `TakeStaminaDamageEntityEffectSystem`
*(`Content.Shared/_Onyx/Chemistry/TakeStaminaDamageEntityEffectSystem.cs`)*

```csharp
public sealed partial class TakeStaminaDamageEntityEffectSystem : EntityEffectSystem<StaminaComponent, TakeStaminaDamage>
{
    [Dependency] private SharedStaminaSystem _stamina = default!;
    protected override void Effect(Entity<StaminaComponent> entity, ref EntityEffectEvent<TakeStaminaDamage> args)
    {
        if (args.Scale != 1f) return;
        _stamina.TakeStaminaDamage(entity, args.Effect.Amount, entity.Comp, visual: false);
    }
}
public sealed partial class TakeStaminaDamage : EntityEffectBase<TakeStaminaDamage>
{
    [DataField] public float Amount = 10f;
    [DataField] public bool Immediate; // "Vanilla has no overtime stamina mode; port stunmeta before using this value."
}
```
Note the `Effect` body **ignores `Immediate` entirely** and hard-gates on `args.Scale != 1f` (so it silently no-ops on any partial/incomplete metabolism tick) — `Immediate` is a placeholder field the Onyx author flagged as blocked on porting "stunmeta" (see §5 — Wolfgate already has this).

YAML usage — `!type:TakeStaminaDamage` — 7 hits across `_Onyx/Reagents/Medicine/medicine.yml` (×4), `_Onyx/Reagents/Narcotics/opioids.yml`, `_Onyx/Reagents/Toxins/bitrunning.yml`:
```yaml
- !type:TakeStaminaDamage
  conditions:
  - !type:ReagentCondition
    reagent: Probital
    min: 20
  amount: 5
  immediate: false
```

### `StaminaDamageCondition` / `StaminaDamageConditionSystem`
*(`Content.Shared/_Onyx/Chemistry/StaminaDamageCondition.cs`)*

```csharp
public sealed partial class StaminaDamageConditionSystem : EntityConditionSystem<StaminaComponent, StaminaDamageCondition>
{
    [Dependency] private SharedStaminaSystem _stamina = default!;
    protected override void Condition(Entity<StaminaComponent> entity, ref EntityConditionEvent<StaminaDamageCondition> args)
    {
        var damage = _stamina.GetStaminaDamage(entity, entity.Comp);
        args.Result = damage > args.Condition.Min && damage < args.Condition.Max;
    }
}
public sealed partial class StaminaDamageCondition : EntityConditionBase<StaminaDamageCondition>
{
    [DataField] public float Min = -1f;
    [DataField] public float Max = float.PositiveInfinity;
}
```
This is a **condition**, not an effect (new-style `EntityCondition`, parallel ECS system to `EntityEffect`) — used inside other effects' `conditions:` arrays to gate on current stamina damage, e.g. gating `MovementSpeedModifier`/`GenericStatusEffect`/a second `TakeStaminaDamage` at `min: 80` / `min: 100` thresholds in `medicine.yml`'s Probital entry. 5 YAML hits, all in `_Onyx/Reagents/Medicine/medicine.yml`.

---

## 3. Old-style Wolfgate equivalents

### How `!type:` resolution actually works in old-style Wolfgate (verified in RobustToolbox 277 source)

`Content.Shared/EntityEffects/EntityEffect.cs` marks the abstract `EntityEffect` class `[ImplicitDataDefinitionForInheritors]`. When the YAML deserializer hits a node tagged `!type:X` for a field typed as (or under) `EntityEffect`, `SerializationManager.Reading.cs:56` calls `ResolveConcreteType(typeof(EntityEffect), "X")`, which calls `ReflectionManager.YamlTypeTagLookup(baseType, typeName)` (`RobustToolbox/Robust.Shared/Reflection/ReflectionManager.cs:319-359`):

```csharp
foreach (var derivedType in GetAllChildren(baseType))
{
    if (!derivedType.IsPublic) continue;
    if (derivedType.Name == typeName) { found = derivedType; break; }   // <-- exact simple-name match
    var serializedAttribute = derivedType.GetCustomAttribute<SerializedTypeAttribute>();
    if (serializedAttribute?.SerializeName == typeName) { found = derivedType; break; } // override
}
```

So `!type:SuppressPain` resolves to **any public class named exactly `SuppressPain` that derives (directly or transitively) from `Content.Shared.EntityEffects.EntityEffect`**, regardless of namespace — matched purely by `Type.Name`, with a `[SerializedType("...")]` attribute as an escape hatch if a name collision ever needs disambiguating. This is confirmed by reading `Content.Server/EntityEffects/Effects/HealthChange.cs` (`namespace Content.Server.EntityEffects.Effects; public sealed partial class HealthChange : EntityEffect`) referenced from reagent YAML purely as `!type:HealthChange` with no namespace qualifier.

Practical consequence: **Onyx's own new-style data classes are already named `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition`** — but they derive from Onyx's `EntityEffectBase<T>` (itself deriving from the *new* `Content.Shared.EntityEffects.EntityEffect`), which does not exist in Wolfgate. Wolfgate needs a **new class with the identical simple name**, deriving from **Wolfgate's own** `Content.Shared.EntityEffects.EntityEffect` (the old abstract base with `public abstract void Effect(EntityEffectBaseArgs args)`), living in whatever namespace we choose (e.g. `Content.Shared._WF.Wolfmed.EntityEffects`). I grepped the whole WG tree for existing classes named `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition` deriving from `EntityEffect`/`EntityEffectCondition` — **none exist**, so there is no name collision to resolve via `[SerializedType]`.

### Proposed classes

All in `Content.Shared/_WF/Wolfmed/EntityEffects/` (server-only logic can go in `Content.Server/_WF/Wolfmed/EntityEffects/` if a dependency is server-only — none of these five need to be; `PainSystem`, `WoundFractureSystem`, `WoundSystem`, `SharedBodySystem`, `StaminaSystem` are all `Content.Shared` systems in both Onyx and Wolfgate). Style per `DECISIONS.md`: no license header, `/// <summary>` one-liners, `[Dependency] private X _x = default!;` (not `readonly`).

**`SuppressPain.cs`** (`Content.Shared/_WF/Wolfmed/EntityEffects/SuppressPain.cs`):
```csharp
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._WF.Wolfmed.Wounds; // Wolfmed's ported PainSystem
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.EntityEffects;

/// <summary>Suppresses pain on a wound-hosting entity and boosts natural recovery while active.</summary>
public sealed partial class SuppressPain : EntityEffect
{
    [DataField(required: true)] public FixedPoint2 Amount;
    [DataField(required: true)] public TimeSpan DecayDuration;
    [DataField] public string Identifier = "PainSuppressant";
    [DataField] public float RecoveryMultiplier = 1f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-suppress-pain",
            ("chance", Probability), ("amount", Amount.Float()),
            ("duration", DecayDuration.TotalSeconds), ("recoveryMultiplier", RecoveryMultiplier));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out PainComponent? pain))
            return;
        var scale = args is EntityEffectReagentArgs reagentArgs ? reagentArgs.Scale : FixedPoint2.New(1);
        args.EntityManager.System<PainSystem>().SuppressPain(
            (args.TargetEntity, pain), Identifier, Amount * scale, DecayDuration, RecoveryMultiplier);
    }
}
```
Key change from Onyx: the ECS system's component-gating (`EntityEffectSystem<PainComponent,...>` silently no-ops if `PainComponent` is missing) becomes an explicit `TryGetComponent` guard in `Effect()`, since old-style `EntityEffect.Effect(EntityEffectBaseArgs)` is a plain virtual call with no automatic component filtering.

**`MendFractures.cs`**: same shape, guard on `WoundHostComponent`, loop `_body.GetBodyChildren`, call `WoundFractureSystem.GetFracture` + `WoundSystem.ChangeSeverity` — direct translation of the `Effect()` body already shown in §2, with `args is EntityEffectReagentArgs reagentArgs ? reagentArgs.Scale : 1` replacing `args.Scale`.

**`TakeStaminaDamage.cs`**: guard on `StaminaComponent`, call Wolfgate's `StaminaSystem.TakeStaminaDamage(uid, Amount, component, visual: false, immediate: Immediate)` (Wolfgate's method already accepts an `immediate` bool — see §5). Unlike Onyx's version, Wolfgate's port can actually **honor** `Immediate` since the stunmeta prerequisite Onyx flagged as blocking is already present (§5) — this is an improvement opportunity, not a strict-parity requirement.

**`StaminaDamageCondition.cs`** (`Content.Shared/_WF/Wolfmed/EntityEffects/StaminaDamageCondition.cs`, deriving from Wolfgate's `EntityEffectCondition`, not `EntityEffect`):
```csharp
using Content.Shared.EntityEffects;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.EntityEffects;

/// <summary>True while stamina damage on the target is strictly between Min and Max.</summary>
public sealed partial class StaminaDamageCondition : EntityEffectCondition
{
    [DataField] public float Min = -1f;
    [DataField] public float Max = float.PositiveInfinity;

    public override bool Condition(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out StaminaComponent? stamina))
            return false;
        var damage = args.EntityManager.System<StaminaSystem>().GetStaminaDamage(args.TargetEntity, stamina);
        return damage > Min && damage < Max;
    }

    public override string GuidebookExplanation(IPrototypeManager prototype) => string.Empty;
}
```
Mirrors the existing `TotalDamage : EntityEffectCondition` pattern (`Content.Server/EntityEffects/EffectConditions/TotalDamage.cs`) exactly — same `Min`/`Max` `FixedPoint2`-vs-`float` shape, same `TryGetComponent` guard, same empty guidebook string convention used by several WG conditions.

### `HealthChange`/`EvenHealthChange`/`DistributedHealthChange` — do **not** add a `TreatmentCapabilities` field to the existing `HealthChange` class directly (see §4 for the recommended approach: a `// WOLFGATE` hook plus a routing-capable subclass).

### Naming table (Onyx `!type:` → proposed Wolfgate class, same tag)

| YAML `!type:` | Onyx class (namespace) | Proposed WG class (namespace) | Base |
|---|---|---|---|
| `SuppressPain` | `Content.Shared._Onyx.Wounds.SuppressPain` | `Content.Shared._WF.Wolfmed.EntityEffects.SuppressPain` | `EntityEffect` |
| `MendFractures` | `Content.Shared._Onyx.Wounds.MendFractures` | `Content.Shared._WF.Wolfmed.EntityEffects.MendFractures` | `EntityEffect` |
| `TakeStaminaDamage` | `Content.Shared._Onyx.Chemistry.TakeStaminaDamage` | `Content.Shared._WF.Wolfmed.EntityEffects.TakeStaminaDamage` | `EntityEffect` |
| `StaminaDamageCondition` | `Content.Shared._Onyx.Chemistry.StaminaDamageCondition` | `Content.Shared._WF.Wolfmed.EntityEffects.StaminaDamageCondition` | `EntityEffectCondition` |
| `HealthChange` (with `treatmentCapabilities`) | partial on upstream `HealthChange` | **keep existing WG class name**, add field via `// WOLFGATE` (see §4) | `EntityEffect` (existing) |

Because resolution is by bare class name, every ported `wounds.yml`/reagent YAML file keeps its `!type:` tags **verbatim** — no YAML rewriting needed for these five effects, satisfying the D6/modularity goal of easy re-syncs.

---

## 4. `HealthChange` TreatmentCapabilities: mapping onto Wolfgate's `HealthChange`

Wolfgate's `Content.Server/EntityEffects/Effects/HealthChange.cs` is upstream-vendored (not `_Onyx`), already carries Shitmed/Mono modifications (`targetPart: TargetBodyPart.All, partMultiplier: 1.00f, canSever: false` baked into its one and only `TryChangeDamage` call), and per D5/D6 upstream files should only get **one- or two-line `// WOLFGATE` hooks**, not new required fields that would break every other reagent already using `!type:HealthChange` without a wounds-aware target.

Recommendation: **don't touch the field list of the upstream `HealthChange` class.** Onyx's field addition works because Onyx's `HealthChange` and `HealthChangeEntityEffectSystem` are two halves of a `partial class` pair the *engine ECS* dispatches per-component — Wolfgate's old-style dispatch has no such extension point (a C# `partial` on `HealthChange` in a different assembly-visible file works too, but Wolfgate's `Effect()` method already fully owns the `TryChangeDamage` call, and adding a field nobody defaults changes nothing unless the `Effect()` body also branches on `HasComp<WoundHostComponent>` — which *is* an upstream-file edit).

Two viable options, in preference order:

1. **`// WOLFGATE` hook inside `HealthChange.Effect()`** (2-3 lines): after computing `scale`/`damageSpec`, branch:
   ```csharp
   // WOLFGATE: route healing through wound treatment-capability gating for wound hosts.
   if (Damage.DamageDict.Values.Any(v => v < 0) && args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity))
   {
       args.EntityManager.System<WoundDamageRoutingSystem>().WithTreatmentCapabilities(args.TargetEntity, TreatmentCapabilities, () =>
           args.EntityManager.System<DamageableSystem>().TryChangeDamage(args.TargetEntity, Damage * scale, IgnoreResistances,
               interruptsDoAfters: false, targetPart: TargetBodyPart.All, partMultiplier: 1.00f, canSever: false));
       return;
   }
   ```
   plus a new `[DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];` field added directly to the class (this is the one field-list change to the vendored file — still small and mechanical, matching the "one- or two-line" spirit even though it's a field, not a call). This keeps every existing `!type:HealthChange` YAML entry working unchanged (default `[Biological]`, no-op for non-wound-host entities) and needs `WoundDamageRoutingSystem`/`TreatmentCapability` ported from `_Onyx/Wounds/WoundDamageRoutingSystem.cs` and `_Onyx/Wounds/WoundPrototype.cs:203` respectively (both already in-scope per the Wolfmed phase plan).

2. **Wolfmed subclass** (`Content.Shared/_WF/Wolfmed/EntityEffects/WoundHealthChange.cs : EntityEffect`, a parallel class with its own `!type:WoundHealthChange` tag) that duplicates `HealthChange`'s damage-application logic plus the capability gate, leaving upstream `HealthChange.cs` completely untouched. This avoids the vendored-file edit entirely but means **every reagent that should respect wound treatment capabilities needs its YAML changed from `!type:HealthChange` to `!type:WoundHealthChange`** — which, per the grep in §2, is every medicine reagent that heals brute/burn damage. That's a much larger, ongoing YAML-maintenance cost (every future Onyx medicine reagent re-sync needs the tag swapped again) and works against D6's "keep Onyx files close to verbatim" / easy-re-sync goal.

**Recommendation: option 1.** It costs one field + a ~6-line `// WOLFGATE` branch in one already-modified vendored file, and keeps every reagent YAML — Onyx's and Wolfgate's own — using the bare `!type:HealthChange` tag with no ported-file rewrites. `EvenHealthChange`/`DistributedHealthChange` need the equivalent treatment **if and when they're ported** — as of this analysis Wolfgate's `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` exists (found in the file listing in the environment's initial tool search) but was not read in this analysis; whether it already matches Onyx's `HealEvenly`/`HealDistributed`-based semantics is unverified and out of scope here (`DamageableSystem.HealEvenly`/`HealDistributed` are confirmed absent from Wolfgate per D5 and my own grep — see §5 — so Wolfgate's existing `EvenHealthChange` almost certainly uses different underlying math and needs its own gap analysis, not assumed to be a drop-in `partial`).

---

## 5. Reagent metabolism plumbing: `MetabolizerSystem` invocation and `ReagentEffectCondition`

### Wolfgate — `Content.Server/Body/Systems/MetabolizerSystem.cs` (server-only, unpredicted)

`TryMetabolize` (lines 106-234) resolves the metabolizing entity's solution, then for each reagent/metabolism-group entry:
```csharp
var actualEntity = ent.Comp2?.Body ?? solutionEntityUid.Value;
var args = new EntityEffectReagentArgs(actualEntity, EntityManager, ent, solution, mostToRemove, proto, null, scale);
foreach (var effect in entry.Effects)
{
    if (!effect.ShouldApply(args, _random)) continue;
    if (effect.ShouldLog) _adminLogger.Add(...);
    effect.Effect(args);   // <-- direct virtual call, no ECS event
}
```
- **Args type:** `EntityEffectReagentArgs : EntityEffectBaseArgs` — a plain record carrying `TargetEntity`, `EntityManager`, `OrganEntity`, `Source` (the `Solution`), `Quantity` (=`mostToRemove`), `Reagent`, `Method` (always `null` here), `Scale`.
- **Scale:** `float scale = (float) mostToRemove / (float) rate;` — the fraction of the intended per-tick rate actually available in the solution (1.0 unless the reagent is running out). `MetabolizerComponent.MaxReagentsProcessable` caps how many distinct reagents get processed per tick (`reagents >= ent.Comp1.MaxReagentsProcessable` skips further ones), with a Frontier-inherited carve-out for `Cryogenic`-tagged reagents.
- **Dispatch:** `effect.Effect(args)` is a synchronous, non-virtual-event method call — **not an ECS event**. Component-targeting (e.g. "only apply to entities with `PainComponent`") must be done *inside* each effect's own `Effect()` body via `TryGetComponent`, because `MetabolizerSystem` has no idea which component an arbitrary `EntityEffect` cares about.
- **Where it runs:** `Content.Server`, driven from `EntitySystem.Update(float)` on a per-`MetabolizerComponent` timer (`NextUpdate`/`UpdateInterval`) — server-authoritative, not predicted, not shared.

### Onyx / new upstream — `Content.Shared/Metabolism/MetabolizerSystem.cs` (shared, predicted; **absent from the sparse checkout**, read via `git show HEAD:...`)

`TryMetabolizeStage` computes `scale` almost identically (`(float) mostToRemove`, divided by `rate` unless `MetabolizeAll`), but the dispatch is completely different:
```csharp
foreach (var effect in entry.Effects)
{
    if (scale < effect.MinScale) continue;
    if (rand.NextFloat() >= effect.Probability) continue;
    if (effect.Conditions != null && !CanMetabolizeEffect(actualEntity, ent, solutionEntity.Value, effect.Conditions)) continue;
    ApplyEffect(effect);
}
void ApplyEffect(EntityEffect effect)
{
    switch (effect)
    {
        case ModifyLungGas: _entityEffects.ApplyEffect(ent, effect, scale); break;              // organ entity
        case AdjustReagent: _entityEffects.ApplyEffect(solutionEntity.Value, effect, scale); break; // the solution entity itself
        default: _entityEffects.ApplyEffect(actualEntity, effect, scale); break;                 // the body
    }
}
```
- `_entityEffects.ApplyEffect(target, effect, scale)` (`SharedEntityEffectsSystem.ApplyEffect`) calls `effect.RaiseEvent(target, this, scale, user)`, which — via `EntityEffectBase<T>.RaiseEvent` — raises `EntityEffectEvent<TEffect>` as a **`[ByRefEvent]` on the target entity**, picked up by whichever `EntityEffectSystem<TComp,TEffect>` subscribed `SubscribeLocalEvent<TComp, EntityEffectEvent<TEffect>>`. Component gating happens automatically at the ECS subscription level (the event simply never reaches a `TComp`-less entity), unlike Wolfgate where every effect must self-check.
- **Target entity varies by effect type** (`switch (effect) { case ModifyLungGas: ... case AdjustReagent: ... default: ... }`) — a hardcoded special case list the Onyx author's own comment calls out as a wart: `// TODO: We should have to do this with metabolism. ReagentEffect struct needs refactoring and so does metabolism!`
- **Runs in `Content.Shared`** — this metabolism pipeline is shared/predicted in Onyx's upstream base, a much bigger architectural difference than the entity-effect dispatch alone; Wolfgate's stays `Content.Server`-only.
- **Condition dispatch is also special-cased**, not uniform: `CanMetabolizeEffect` routes `MetabolizerTypeCondition` to a vampire-aware special case (`<Onyx-VampireMetabolism>`), `ReagentCondition` to the *solution* entity, and everything else to the *body* entity — three different `TryCondition` targets depending on condition type, again ad hoc rather than declared per-condition.

### Wolfgate `EntityEffectCondition` vs Onyx `EntityCondition` — do they differ?

Yes, structurally, not just in name:

- **Wolfgate** `Content.Shared/EntityEffects/EntityEffectCondition.cs`: `[ImplicitDataDefinitionForInheritors] public abstract partial class EntityEffectCondition { public abstract bool Condition(EntityEffectBaseArgs args); public abstract string GuidebookExplanation(IPrototypeManager prototype); }` — evaluated as a **plain synchronous method call** (`EntityEffectExt.ShouldApply` in `EntityEffect.cs` loops `Conditions` and calls `cond.Condition(args)` directly), receiving the *same* `EntityEffectBaseArgs`/`EntityEffectReagentArgs` the owning effect got. No ECS involved; no per-component subscription; the condition class itself is responsible for fetching whatever component it needs off `args.TargetEntity` via `args.EntityManager`.
- **Onyx/new upstream** `Content.Shared/EntityConditions/SharedEntityConditionsSystem.cs` (**absent from sparse checkout** — confirmed missing on disk; the documented `WOLFMED_HANDOFF.md` sparse-checkout command never lists `Content.Shared/EntityConditions`; read via `git show HEAD:Content.Shared/EntityConditions/SharedEntityConditionsSystem.cs`): `EntityCondition`/`EntityConditionBase<T>`/`EntityConditionSystem<T,TCon>`/`EntityConditionEvent<T>` is a **full parallel ECS system to `EntityEffect`** — `EntityConditionSystem<T,TCon> : EntitySystem` subscribes `SubscribeLocalEvent<T, EntityConditionEvent<TCon>>`, and `SharedEntityConditionsSystem.TryCondition<T>(target, condition, sourceEnt)` raises that event and reads back `args.Result`, with `Inverted` XOR'd in at the `TryCondition` level (`condition.Inverted != condition.RaiseEvent(...)`). Also carries an explicit `EntityUid? sourceEnt` parameter Wolfgate's condition interface has no equivalent for (used e.g. by `ReagentCondition`/`MetabolizerTypeCondition` to know *which* entity — body vs. organ vs. solution — is being tested, which is exactly why `MetabolizerSystem.CanMetabolizeEffect` needs its three-way `switch`).
- Consequence for the port: **`StaminaDamageCondition` cannot be ported as a literal translation of its Onyx `Condition()` body alone** — it must become a Wolfgate `EntityEffectCondition` (synchronous, `EntityEffectBaseArgs`-based, no target-entity indirection) as designed in §3, which is a strictly simpler shape than Onyx's, since Wolfgate has no parallel `EntityConditionSystem<T,TCon>` ECS machinery to hook into (and D5 doesn't call for porting one — it isn't in the missing-APIs list, and building it would be materially larger scope than this task's five files).

### Confirmed-absent APIs (cross-checked against D5)

Read `Content.Shared/Damage/Systems/DamageableSystem.cs` directly:
```csharp
public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool ignoreGlobalModifiers = false,
    float armorPenetration = 0f,
    bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null,
    DamageOriginFlag? originFlag = null)
```
— old `EntityUid?`-first signature (not the new `Entity<DamageableComponent?>` overload), returns `DamageSpecifier?` (not `bool`), and a grep for `HealEvenly`, `HealDistributed`, `GetTotalDamage`, bare `ChangeDamage(` in that file returned **zero matches** — D5's claim is confirmed accurate for this file. `ReagentTreatmentSystems.cs`'s calls to `_damageable.HealEvenly(...)`/`_damageable.HealDistributed(...)` (for `EvenHealthChange`/`DistributedHealthChange`) have **no Wolfgate equivalent to call into** — those two effects' treatment-capability wiring (option 1 in §4) cannot be built the same way `HealthChange`'s can until/unless those methods (or Wolfgate equivalents) exist.

### Wolfgate's stamina system already has what Onyx's own code says it's blocked on

Onyx's `TakeStaminaDamage.Immediate` field carries the comment *"Vanilla has no overtime stamina mode; port stunmeta before using this value."* Wolfgate's `Content.Shared/Damage/Systems/StaminaSystem.cs:274` — `public void TakeStaminaDamage(EntityUid uid, float value, StaminaComponent? component = null, EntityUid? source = null, EntityUid? with = null, bool visual = true, SoundSpecifier? sound = null, bool immediate = true)` — is tagged `// goob edit - stunmeta` and already threads an `immediate` bool through to `EnterStamCrit`. **Wolfgate does not have the gap Onyx's own comment warns about**; the ported `TakeStaminaDamage.Effect()` can pass `Immediate` straight through instead of leaving it inert like Onyx's own `Effect()` body does (Onyx's `Effect()` never reads `args.Effect.Immediate` at all — it's dead data on the Onyx side today). Also confirmed present: `StaminaSystem.GetStaminaDamage(EntityUid uid, StaminaComponent? component = null)` (line 98), matching exactly what `StaminaDamageConditionSystem.Condition` needs. One naming note: Onyx's files `[Dependency] private SharedStaminaSystem _stamina` — Wolfgate's class is named `StaminaSystem` (same `Content.Shared.Damage.Systems` namespace, file `StaminaSystem.cs`), not `SharedStaminaSystem`; the port must use the Wolfgate name.

### Guidebook locale text ports verbatim

`Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` (outside the sparse checkout; read via `git show`) has both keys fully written:
```ftl
entity-effect-guidebook-suppress-pain =
    { $chance -> [1] Suppresses *[other] suppress } { NATURALFIXED($amount, 2) } pain and multiplies natural pain recovery by up to { NATURALFIXED($recoveryMultiplier, 2) }. Repeated doses stack; the effect fades over { NATURALFIXED($duration, 2) } { MANY("second", $duration) }.

entity-effect-guidebook-mend-fractures =
    { $chance -> [1] Reduces *[other] reduce } matching fracture severity by { NATURALFIXED($amount, 2) } per metabolism tick. Types: { $wounds }. Grades: "{ $minimumGrade }" through "{ $maximumGrade }", inclusive.

entity-effect-guidebook-all-fractures = all fractures
fracture-grade-hairline = hairline
fracture-grade-simple = simple
fracture-grade-displaced = displaced
fracture-grade-comminuted = comminuted
```
Both `NATURALFIXED` and `MANY` fluent functions are already registered in Wolfgate (`Content.Shared/Localizations/ContentLocalizationManager.cs:38,52`), so this `.ftl` can be copied into `Resources/Locale/en-US/_WF/Wolfmed/entity-effects.ftl` (or wherever the manifest puts Wolfmed locale) with **zero rewriting**. Wolfgate's own `ReagentEffectGuidebookText` return type is `string?` returned from `protected abstract string? ReagentEffectGuidebookText(...)` (called by the public `GuidebookEffectDescription` wrapper in `EntityEffect.cs`, format key `guidebook-reagent-effect-description`) rather than Onyx's `public override string EntityEffectGuidebookText(...)` called directly — the method name, accessibility, and nullability differ, but the Fluent message keys and their `$chance`/`$amount`/etc. arguments are identical, and the existing `HealthChange` guidebook string in Wolfgate is keyed `reagent-effect-guidebook-health-change` (confirmed present in `Resources/Locale/en-US/guidebook/chemistry/effects.ftl:76`) — i.e. Wolfgate's convention prefixes these `reagent-effect-guidebook-*`, not `entity-effect-guidebook-*`; the new Wolfmed `.ftl` should follow Wolfgate's own naming (`reagent-effect-guidebook-suppress-pain`, `reagent-effect-guidebook-mend-fractures`) even though the *body* text can be copied verbatim from Onyx's file, to match the key `SuppressPain.ReagentEffectGuidebookText`/`MendFractures.ReagentEffectGuidebookText` will call.

---

## Summary of blockers / dependencies for this slice

1. `PainSystem`, `WoundFractureSystem`, `WoundSystem`, `WoundHostComponent`, `WoundDamageRoutingSystem` must already exist in Wolfmed (they're phase 1-3 core/fracture work per the handoff) before `SuppressPain`/`MendFractures`/the `HealthChange` hook can compile — this task only designs the effect classes, it doesn't unblock the systems they call.
2. `EvenHealthChange`/`DistributedHealthChange` treatment-capability wiring is blocked on `DamageableSystem.HealEvenly`/`HealDistributed` not existing in Wolfgate (confirmed absent) — needs its own decision (port the methods, or find/build a Wolfgate-native equivalent) before attempting option 1 of §4 for those two.
3. No blocker for `TakeStaminaDamage`/`StaminaDamageCondition` — `StaminaSystem.TakeStaminaDamage`/`GetStaminaDamage` already have everything needed, including the `immediate` support Onyx itself lacks.
4. `!type:` tag names port verbatim for all five effects in scope; no `wounds.yml`/reagent YAML rewriting is needed for this slice specifically (separate from whatever rewriting other Wolfmed gaps require elsewhere in those same files).
