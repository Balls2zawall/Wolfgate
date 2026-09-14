# WOLFMED PHASE 4 — P4-1 (reagent treatments) and P4-5 (pain numbness)

Analyst report. **READ-ONLY**: nothing in `WG` or `ONYX` was modified.

- `WG` = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`, HEAD `6329d204e3 Phase 3 completion`.
- `ONYX` = `C:/tmp/onyx`, pinned `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Sparse checkout; files absent on disk were read with `git show HEAD:<path>` and are quoted, not invented.
- Every claim below is cited `file:line`. Where a phase-1 report (`entityeffects-gap.md`, `medical-extras.md`) is corrected, it is called out explicitly.

---

## 0. Executive summary

1. **The four entity-effect classes are easy.** `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition` are direct old-style rewrites. Every system and symbol they need already exists in WG at the exact signature Onyx calls (§2). They are plain classes, not systems — **P4-1 adds zero `SubscribeLocalEvent` pairs and zero new components** (§10).
2. **HOOK 9 is smaller than PLAN.md assumed.** GUARD D already routes *all* `TryChangeDamage` on a wound host, including negative (healing) damage, into `WoundDamageRoutingSystem.ApplyLocalizedHealing`, which already reduces wound severity (`WoundSystem.HandlePartDamageApplied`, `WoundSystem.cs:~+30`). **Reagents already heal wounds today.** HOOK 9 adds only the capability *scope*: a `TreatmentCapabilities` `[DataField]` plus a `WithTreatmentCapabilities(...)` wrapper (§4).
3. **HOOK 9 is behaviourally inert until phase 5.** `OrganicBodyPartProfile` is the only `bodyPartProfile` in the tree and it is `treatmentCapabilities: [Biological]` (`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:3-4`). With one profile, `CanTreatPart` can never refuse. HOOK 9 is correctness/forward-compat plumbing, not a phase-4 gameplay feature. **This is a user decision** (§4.5).
4. **`DistributedHealthChange` is not needed.** Zero hits in the Onyx reagent set in scope (§3.4); zero in WG. Do not author it.
5. **The reagent port is the real work, and it is much bigger than DECISIONS P4-1 implies.** Of the 22 reagents in `ONYX/Resources/Prototypes/_Onyx/Reagents/Medicine/*.yml`, **20 do not exist in Wolfgate at all** (§6.2) — not the reagent, not its locale, not its reaction, not its dispenser entry. And the Onyx YAML **does not port verbatim**: 8 `!type:` tags Onyx uses have different names in Wolfgate and 5 do not exist at all (§6.1). I recommend a three-tier scope with only Tier A mandatory (§6.6).
6. **Collisions:** `Stasizium` — WG's Goobstation copy is functionally the same reagent; **extend it** with one marked `!type:MendFractures` block. `SalicylicAcid` — WG's is a Frontier *chemical precursor* for Traumoxadone with no metabolisms at all; Onyx's is a medicine. Not equivalent. **Recommendation: drop Onyx's SalicylicAcid entirely** rather than rename (§6.4).
7. **P4-5: recommend SKIP and record.** No in-scope reagent uses `ModifyStatusEffect`; WG's `Desoxyephedrine` never had pain numbness. The prerequisite `PainNumbnessStatusEffectComponent` exists in WG but is **dead code — nothing in the tree ever adds it** (§8). Porting the chain costs ~50 LOC + 2 prototypes if the user wants it.
8. **Blocker-grade trap:** `WithTreatmentCapabilities` is a non-reentrant per-body dictionary with a `finally` remove (`WoundDamageRoutingSystem.cs:136-147`). Nesting it (e.g. HOOK 9 inside HOOK 8) clears the outer scope early. Phase 4 must not nest it (§11-T3).

---

## 1. Onyx source — re-verified at the pin

### 1.1 `Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs` (38 lines, read in full)

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
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-suppress-pain", ("chance", Probability),
            ("amount", Amount.Float()), ("duration", DecayDuration.TotalSeconds),
            ("recoveryMultiplier", RecoveryMultiplier));
}
```
Matches `entityeffects-gap.md` §2 exactly. `SuppressPainEntityEffect.cs:7,11-15,18-37`.

### 1.2 `Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs` (55 lines, read in full)

`:9-25` add `[DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];` to `HealthChange`, `EvenHealthChange`, `DistributedHealthChange` as partials, in namespace `Content.Shared.EntityEffects.Effects.Damage`.

`:27-54` is `MendFractures : EntityEffectBase<MendFractures>` with `Wounds = ["BoneFractureWound"]`, `MinimumGrade = Hairline`, `MaximumGrade = Comminuted`, `Amount = 1`, plus `EntityEffectGuidebookText` that resolves each `ProtoId<WoundPrototype>` via `prototype.TryIndex(id, out WoundPrototype? wound) ? Loc.GetString(wound.Name) : id.Id` and lowercases grade names into `fracture-grade-{...}` keys.

### 1.3 `Content.Shared/_Onyx/Wounds/ReagentTreatmentSystems.cs` (94 lines, read in full)

`:10-31` `HealthChangeEntityEffectSystem` partial:
```csharp
private void ApplyTreatment(Entity<DamageableComponent> entity, EntityEffectEvent<HealthChange> args)
{
    var change = new DamageSpecifier(args.Effect.Damage) * args.Scale;
    ApplyScoped(entity, change.DamageDict.Values.Any(amount => amount < 0),
        args.Effect.TreatmentCapabilities,
        () => _damageable.TryChangeDamage(entity.AsNullable(), change, args.Effect.IgnoreResistances,
            interruptsDoAfters: false));
}
private void ApplyScoped(Entity<DamageableComponent> entity, bool healing,
    IReadOnlySet<TreatmentCapability> capabilities, Action apply)
{
    if (healing && HasComp<WoundHostComponent>(entity))
        _woundRouting.WithTreatmentCapabilities(entity, capabilities, apply);
    else
        apply();
}
```
`:33-50` `EvenHealthChangeEntityEffectSystem` — same shape, `Apply()` loops `args.Effect.Damage` calling `_damageable.HealEvenly(entity.AsNullable(), amount * args.Scale, group)`; healing test is `args.Effect.Damage.Values.Any(amount => amount < 0)`.

`:52-69` `DistributedHealthChangeEntityEffectSystem` — identical but `HealDistributed`.

`:71-93` `MendFracturesEntityEffectSystem : EntityEffectSystem<WoundHostComponent, MendFractures>` — quoted verbatim in `entityeffects-gap.md` §2 and re-confirmed line-for-line.

### 1.4 `Content.Shared/_Onyx/Chemistry/TakeStaminaDamageEntityEffectSystem.cs` (28 lines)

`:11-17` `if (args.Scale != 1f) return; _stamina.TakeStaminaDamage(entity, args.Effect.Amount, entity.Comp, visual: false);` — **`Immediate` is never read**. `:20-27` data class: `Amount = 10f`, `Immediate` with the comment `// ponytail: Vanilla has no overtime stamina mode; port stunmeta before using this value.`

### 1.5 `Content.Shared/_Onyx/Chemistry/StaminaDamageCondition.cs` (28 lines)

`:12-16` `args.Result = damage > args.Condition.Min && damage < args.Condition.Max;` over `_stamina.GetStaminaDamage(entity, entity.Comp)`. `:19-27` `Min = -1f`, `Max = float.PositiveInfinity`, guidebook `=> string.Empty`.

### 1.6 The exhaustive grep for other wound-related entity effects

```
git -C C:/tmp/onyx grep -n "EntityEffectSystem<" HEAD -- Content.Shared/_Onyx Content.Server/_Onyx
```
returns 29 hits. Filtering on wound/pain/fracture/bleed/organ/stamina gives **exactly three**:
- `Content.Shared/_Onyx/Chemistry/TakeStaminaDamageEntityEffectSystem.cs:7`
- `Content.Shared/_Onyx/Wounds/ReagentTreatmentSystems.cs:72` (`MendFractures`)
- `Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs:7`

The parallel `EntityConditionSystem<` grep returns **two**:
- `Content.Shared/_Onyx/Chemistry/StaminaDamageCondition.cs:8`
- `Content.Shared/_Onyx/EntityConditions/TypedDamageThreshold.cs:14` — **new, not in PLAN.md's list.** It is an `EntityCondition` on `DamageableComponent` used by Onyx's `SalicylicAcid` and `Oxandrolone` (§3.5). Not wound-specific, but a hard prerequisite for those two reagents.

Everything else in the grep is Onyx disease / xenobiology / cosmic-cult / DNA / clothing-dirt content, out of scope. **`entityeffects-gap.md` §1's "five files is the complete in-scope set" is confirmed, with `TypedDamageThreshold` added as a sixth if the reagent set that uses it is ported.**

There are **no** `MendFractures`-like effects beyond the above: `git grep -n MendFractures HEAD` in Onyx returns only `ReagentTreatmentEffects.cs`, `ReagentTreatmentSystems.cs` and two YAML call sites (`_Onyx/Reagents/Medicine/first_aid.yml:29`, `_Onyx/Reagents/Medicine/medicine.yml:140`).

---

## 2. External symbol table — SAME / DIFFERENT / MISSING in WG

Compat layer (`Content.Shared/_WF/Wolfmed/Compat/*`) checked first in every row.

| Onyx symbol | Status | WG evidence |
|---|---|---|
| `PainSystem.SuppressPain(Entity<PainComponent?>, string, FixedPoint2, TimeSpan, float = 1f) -> bool` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:340-341` — `public bool SuppressPain(Entity<PainComponent?> entity, string identifier, FixedPoint2 amount, TimeSpan decayDuration, float recoveryMultiplier = 1f)`. Vendored verbatim. Guards `!_net.IsServer`, `amount <= 0`, `decayDuration <= 0`, `recoveryMultiplier < 1f` at `:342-345`. |
| `PainComponent` | **SAME**, namespace `Content.Shared._Onyx.Wounds` | `WoundDamageComponents.cs:12` (namespace), component declared in the same file. Ensured on the **body** at `WoundDamageProjectionSystem.cs SetupBody` (`EnsureComp<PainComponent>(body);`) and on pain-capable parts in `SetupPart`. So a body-targeted effect finds it. |
| body-level suppression reaches parts | **SAME** | `PainSystem.cs:182-195` `GetPainBeforeAdrenaline` adds `bodyPain.Suppression * share` pro-rata to each part. |
| suppression decays on a tick | **SAME** | `PainSystem.cs:141` calls `DecayPainSuppression((suppressionUid, suppressionPain), elapsed)` from the update loop. |
| `WoundFractureSystem.GetFracture(Entity<WoundableComponent?>) -> Entity<WoundComponent, WoundFractureComponent>?` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:88` |
| `WoundSystem.ChangeSeverity(Entity<WoundComponent?>, FixedPoint2) -> bool` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundSystem.cs:284`. Server-gated (`!_net.IsServer` at `:286`); refuses scars; clamps to `[0, prototype.MaximumSeverity]`; removes the wound at zero. |
| `WoundComponent.Prototype` (`ProtoId<WoundPrototype>`) | **SAME** | `WoundDamageComponents.cs:187` |
| `WoundFractureComponent.Grade` (`FractureGrade`) | **SAME** | `WoundDamageComponents.cs:252` |
| `enum FractureGrade { None, Hairline, Simple, Displaced, Comminuted }` | **SAME** | `WoundDamageComponents.cs:282-289` |
| `WoundHostComponent` | **SAME** | `WoundDamageComponents.cs`, applied at `WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:252` |
| `SharedBodySystem.GetBodyChildren(EntityUid)` | **SAME** | used by `WoundDamageProjectionSystem.cs` / `WoundDamageRoutingSystem.cs` throughout |
| `WoundDamageRoutingSystem.WithTreatmentCapabilities(EntityUid, IReadOnlySet<TreatmentCapability>, Action)` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:136-147` (exact body quoted in §4.1) |
| `enum TreatmentCapability : byte { Biological, Mechanical, Electrical }` | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundPrototype.cs:203-208` |
| `BodyPartProfilePrototype.TreatmentCapabilities` | **SAME** | `WoundPrototype.cs:153-154` `HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];` |
| `SharedStaminaSystem` | **DIFFERENT (name)** | WG: `public sealed partial class StaminaSystem : EntitySystem` — `WG/Content.Shared/Damage/Systems/StaminaSystem.cs:30`, namespace `Content.Shared.Damage.Systems` (`:28`). Onyx: `SharedStaminaSystem`. Use `StaminaSystem`. |
| `SharedStaminaSystem.TakeStaminaDamage(...)` | **DIFFERENT (richer)** | WG `StaminaSystem.cs:274-275`: `public void TakeStaminaDamage(EntityUid uid, float value, StaminaComponent? component = null, EntityUid? source = null, EntityUid? with = null, bool visual = true, SoundSpecifier? sound = null, bool immediate = true)` — tagged `// goob edit - stunmeta` at `:273`. The `immediate` parameter Onyx's own comment says is blocked **already exists**. |
| `SharedStaminaSystem.GetStaminaDamage(EntityUid, StaminaComponent?)` | **SAME** | `WG/Content.Shared/Damage/Systems/StaminaSystem.cs:98`, `[PublicAPI]` at `:97` |
| `StaminaComponent` | **SAME**, namespace `Content.Shared.Damage.Components` | `WG/Content.Shared/Damage/Components/StaminaComponent.cs:6` |
| `EntityEffect` base | **DIFFERENT (old-style)** | `WG/Content.Shared/EntityEffects/EntityEffect.cs:22,48` — `[ImplicitDataDefinitionForInheritors] public abstract partial class EntityEffect { … public abstract void Effect(EntityEffectBaseArgs args); }`. Guidebook hook is `protected abstract string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)` (`:33`) — **protected, nullable, different name** from Onyx's `public override string EntityEffectGuidebookText(...)`. `Probability` at `:39`. |
| `EntityEffectCondition` base | **DIFFERENT (old-style)** | `WG/Content.Shared/EntityEffects/EntityEffectCondition.cs:9,13,20` — `public abstract bool Condition(EntityEffectBaseArgs args);` + `public abstract string GuidebookExplanation(IPrototypeManager prototype);` |
| `EntityEffectReagentArgs.Scale` | **DIFFERENT (type)** | `WG/Content.Shared/EntityEffects/EntityEffect.cs:121` — `public FixedPoint2 Scale;` (Onyx passes a `float`). |
| `EntityEffectBase<T>`, `EntityEffectEvent<T>`, `EntityEffectSystem<T,TEffect>`, `SharedEntityEffectsSystem` | **MISSING** | zero hits in WG. D16/D5 confirmed. |
| `EntityCondition`, `EntityConditionBase<T>`, `EntityConditionSystem<T,TCon>`, `SharedEntityConditionsSystem` | **MISSING** | zero hits in WG. |
| `EntityEffect.MinScale` | **MISSING** | Onyx `Content.Shared/EntityEffects/EntityEffect.cs:32` `public virtual float MinScale { get; private set; }`; WG has no equivalent. No reagent in scope sets `minScale:` (§9.3). |
| `DamageableSystem.HealEvenly` / `HealDistributed` | **MISSING** | confirmed again: zero hits across `WG/Content.Shared/Damage/Systems/DamageableSystem.cs`. WG's `EvenHealthChange` implements the even-split inline (§4.3). |
| `DamageSpecifier.DamageDict` | **SAME (string-keyed)** | `WG/Content.Shared/Damage/DamageSpecifier.cs:44` `public Dictionary<string, FixedPoint2> DamageDict { get; set; } = new();` |
| `StatusEffectsSystem` (new) `TryAddStatusEffectDuration` / `TryUpdateStatusEffectDuration` / `TrySetStatusEffectDuration` / `TryRemoveTime` | **SAME** | `WG/Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs:48,134,91,307` (all four present with the `EntityUid, EntProtoId, TimeSpan?, TimeSpan?` shapes `ModifyStatusEffect` needs). **Caveat:** WG has *two* classes named `StatusEffectsSystem` — `Content.Shared/StatusEffect/StatusEffectsSystem.cs:11` (legacy) and `Content.Shared/StatusEffectNew/StatusEffectsSystem.cs:15` (ported). A ported `ModifyStatusEffect` must `using Content.Shared.StatusEffectNew;`. |
| `PainNumbnessStatusEffectComponent` | **SAME** | `WG/Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs:12`. Registers as `PainNumbnessStatusEffect`. |
| `ModifyStatusEffect` / `BaseStatusEntityEffect<T>` / `StatusEffectMetabolismType.Update` | **MISSING / PARTIAL** | no `ModifyStatusEffect` anywhere in WG (`.cs` and `.yml`). `StatusEffectMetabolismType` **does** exist but is `{ Add, Remove, Set }` — `WG/Content.Server/EntityEffects/Effects/StatusEffects/GenericStatusEffect.cs:76-81` — **no `Update` member** (§8.3). |
| `TypedDamageThreshold` | **MISSING** | zero hits in WG `.cs`/`.yml`. |
| `NATURALFIXED`, `MANY` fluent functions | **SAME** | `WG/Content.Shared/Localizations/ContentLocalizationManager.cs:38,52` |
| `WoundPrototype.Name` (`LocId`) | **SAME** | `WG/Content.Shared/_Onyx/Wounds/WoundPrototype.cs:19` |

**No name collision for the four new classes.** `grep -rn "class SuppressPain\b|class MendFractures\b|class TakeStaminaDamage\b|class StaminaDamageCondition\b"` over `WG/Content.*` returns nothing. `!type:` resolves by bare `Type.Name` against public subclasses of the field's base type (`RobustToolbox/Robust.Shared/Reflection/ReflectionManager.cs:319-359`), so any namespace works. Precedent for a shared old-style effect: `WG/Content.Shared/_Mono/Claws/ClawsGrowthModifierEffect.cs:8` — `public sealed partial class ClawsGrowth : EntityEffect`.

---

## 3. The four new old-style classes — full design

**Location:** `Content.Shared/_WF/Wolfmed/EntityEffects/`, namespace `Content.Shared._WF.Wolfmed.EntityEffects`.
Every dependency (`PainSystem`, `WoundSystem`, `WoundFractureSystem`, `SharedBodySystem`, `StaminaSystem`) lives in `Content.Shared`, so Shared placement compiles and matches PLAN.md §2.15. All four are only ever invoked from `Content.Server/Body/Systems/MetabolizerSystem.cs:218` in practice, and `SuppressPain`/`ChangeSeverity` self-guard on `_net.IsServer`, so client-side resolution is a harmless no-op.

Style per DECISIONS: no license header, `/// <summary>` one-liners, `[Dependency] private X _x = default!;` — but note these are **classes, not systems**, so they have no `[Dependency]` fields at all; they resolve systems through `args.EntityManager.System<T>()`, exactly as WG's own `HealthChange` does (`HealthChange.cs:149,167`).

### 3.1 `SuppressPain.cs` — `!type:SuppressPain`

```csharp
using Content.Shared._Onyx.Wounds;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.EntityEffects;

/// <summary>Adds keyed, decaying pain suppression to a wound host and speeds natural pain recovery while active.</summary>
public sealed partial class SuppressPain : EntityEffect
{
    [DataField(required: true)] public FixedPoint2 Amount;
    [DataField(required: true)] public TimeSpan DecayDuration;
    [DataField] public string Identifier = "PainSuppressant";
    [DataField] public float RecoveryMultiplier = 1f;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-suppress-pain",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("duration", DecayDuration.TotalSeconds),
            ("recoveryMultiplier", RecoveryMultiplier));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out PainComponent? pain))
            return;

        var scale = args is EntityEffectReagentArgs reagent ? reagent.Scale : FixedPoint2.New(1);
        args.EntityManager.System<PainSystem>()
            .SuppressPain((args.TargetEntity, pain), Identifier, Amount * scale, DecayDuration, RecoveryMultiplier);
    }
}
```

Notes:
- The explicit `TryGetComponent<PainComponent>` replaces Onyx's ECS component filter (`EntityEffectSystem<PainComponent, SuppressPain>`). Old-style dispatch does no filtering: `MetabolizerSystem.cs:218` is a bare virtual call.
- `Amount * scale` is `FixedPoint2 * FixedPoint2` — no cast needed (Onyx's is `FixedPoint2 * float`).
- `DecayDuration` deserialises from the bare seconds number Onyx's YAML uses (`decayDuration: 9`) — RT's `TimeSpan` serializer takes seconds. Verified against WG's own `TimeSpan` datafields (e.g. `BaseStatusEntityEffect.Time`'s Onyx counterpart uses the same form; WG has the identical serializer).
- Guidebook key **renamed** to Wolfgate's `reagent-effect-*` convention (§5).

### 3.2 `MendFractures.cs` — `!type:MendFractures`

```csharp
using System.Linq;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.EntityEffects;

/// <summary>Reduces the severity of matching fractures on every body part of a wound host.</summary>
public sealed partial class MendFractures : EntityEffect
{
    /// <summary>Fracture wound prototypes to treat. Empty means all fracture wounds.</summary>
    [DataField] public HashSet<ProtoId<WoundPrototype>> Wounds = ["BoneFractureWound"];
    [DataField] public FractureGrade MinimumGrade = FractureGrade.Hairline;
    [DataField] public FractureGrade MaximumGrade = FractureGrade.Comminuted;
    [DataField] public FixedPoint2 Amount = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var wounds = Wounds.Count == 0
            ? Loc.GetString("reagent-effect-guidebook-all-fractures")
            : string.Join(", ", Wounds.Select(id =>
                prototype.TryIndex(id, out WoundPrototype? wound) ? Loc.GetString(wound.Name) : id.Id));
        return Loc.GetString("reagent-effect-guidebook-mend-fractures",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("wounds", wounds),
            ("minimumGrade", Loc.GetString($"fracture-grade-{MinimumGrade.ToString().ToLowerInvariant()}")),
            ("maximumGrade", Loc.GetString($"fracture-grade-{MaximumGrade.ToString().ToLowerInvariant()}")));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity))
            return;

        var scale = args is EntityEffectReagentArgs reagent ? reagent.Scale : FixedPoint2.New(1);
        var amount = Amount * scale;
        var body = args.EntityManager.System<SharedBodySystem>();
        var fractures = args.EntityManager.System<WoundFractureSystem>();
        var wounds = args.EntityManager.System<WoundSystem>();

        foreach (var (part, _) in body.GetBodyChildren(args.TargetEntity))
        {
            if (fractures.GetFracture(part) is not { } fracture ||
                (Wounds.Count != 0 && !Wounds.Contains(fracture.Comp1.Prototype)) ||
                fracture.Comp2.Grade < MinimumGrade ||
                fracture.Comp2.Grade > MaximumGrade)
                continue;

            wounds.ChangeSeverity(fracture.Owner, -amount);
        }
    }
}
```

Notes:
- `GetBodyChildren` is enumerated while `ChangeSeverity` may **remove a wound entity** (`WoundSystem.cs:~300` → `RemoveWound`). `GetBodyChildren` yields body *parts*, not wounds, so the enumeration is not invalidated. `GetFracture` itself iterates `_wounds.GetWounds(part)` and returns on the first hit before any mutation. Safe — but if the implementer changes it to heal *all* fractures per part, materialise with `.ToArray()` first (cf. the `GibbingSystem` container-mutation crash noted in DECISIONS Phase 4).
- `fracture.Comp1` is `WoundComponent`, `fracture.Comp2` is `WoundFractureComponent` — matching WG's `GetFracture` return `Entity<WoundComponent, WoundFractureComponent>?` (`WoundFractureSystem.cs:88`).

### 3.3 `TakeStaminaDamage.cs` — `!type:TakeStaminaDamage`

```csharp
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Wolfmed.EntityEffects;

/// <summary>Applies stamina damage from a metabolised reagent.</summary>
public sealed partial class TakeStaminaDamage : EntityEffect
{
    [DataField] public float Amount = 10f;

    /// <summary>Whether a target already at the stamina cap is dropped immediately.</summary>
    [DataField] public bool Immediate;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-take-stamina-damage",
            ("chance", Probability), ("amount", Amount));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out StaminaComponent? stamina))
            return;

        // WOLFGATE (P4): Onyx hard-gates on Scale == 1 because its framework can hand out partial ticks;
        // Wolfgate's MetabolizerSystem does the same (scale = mostToRemove / rate), so the gate is kept for parity.
        if (args is EntityEffectReagentArgs reagent && reagent.Scale != FixedPoint2.New(1))
            return;

        args.EntityManager.System<StaminaSystem>()
            .TakeStaminaDamage(args.TargetEntity, Amount, stamina, visual: false, immediate: Immediate);
    }
}
```

**Decision inside this class (flag to the user):** Onyx's `Effect()` ignores `Immediate` entirely; Wolfgate's `StaminaSystem.TakeStaminaDamage` has the `immediate` parameter Onyx says it lacks (`StaminaSystem.cs:273-275`, `// goob edit - stunmeta`). Honouring it is a **deliberate divergence from Onyx**. It matters for exactly one in-scope reagent: `Probital`'s second `TakeStaminaDamage` at `amount: -100, immediate: true` (`ONYX _Onyx/Reagents/Medicine/medicine.yml:116-124`). Since `Amount` is negative there and `TakeStaminaDamage`'s `immediate` branch only fires on `component.Critical` (`StaminaSystem.cs:288-292`), honouring it changes `Probital`'s crit-recovery from "stays down until decay" to "drops now, then recovers" — arguably the author's intent. **Recommend: honour it**, record as a corrected-upstream-bug deviation like §8.2-1/§8.2-3 in phase 2. If Probital is not ported (Tier C, §6.6), this is moot and `Immediate` ships inert either way.

### 3.4 `StaminaDamageCondition.cs` — `!type:StaminaDamageCondition`

```csharp
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
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

Mirrors `WG/Content.Server/EntityEffects/EffectConditions/TotalDamage.cs:16-26` exactly (same `TryGetComponent` + `> Min && < Max` shape). Onyx's `EntityCondition` also carries an `Inverted` flag XOR'd at `TryCondition` level and an `EntityUid? sourceEnt`; Wolfgate's `EntityEffectCondition` has neither and no in-scope YAML uses them, so the simpler shape is exact for this port.

### 3.5 `TypedDamageThreshold` — a fifth class, **only if Tier C reagents are ported**

`ONYX/Content.Shared/_Onyx/EntityConditions/TypedDamageThreshold.cs` (67 lines, read in full). It is gated behind `SalicylicAcid` and `Oxandrolone` only. Its `Condition()` body calls `_damageable.GetAllDamage(entity.AsNullable())` (`:22`, behind `#pragma warning disable CS0618`) — **`GetAllDamage` does not exist in WG's `DamageableSystem`** (same missing new-style API family as `HealEvenly`). A WG port would substitute `damageable.Damage`, which on a wound host is the projected aggregate (`WoundDamageProjectionSystem.cs:196` `_damage.SetDamage(body, total)`), so the semantics survive. `DamageSpecifier.ExclusiveAdd` and `AnyPositive` must also be verified present before committing to it.
**Recommendation: do not port.** Drop `SalicylicAcid` (§6.4) and `Oxandrolone`; both are Tier C.

---

## 4. HOOK 9 — how healing reaches wounds today, and the exact edit

### 4.1 What already happens (re-verified in the current tree)

1. `HealthChange.Effect()` calls `DamageableSystem.TryChangeDamage(args.TargetEntity, Damage * scale, IgnoreResistances, interruptsDoAfters: false, targetPart: TargetBodyPart.All, partMultiplier: 1.00f, canSever: false)` — `WG/Content.Server/EntityEffects/Effects/HealthChange.cs:167-176`.
2. `TryChangeDamage` raises `BeforeDamageChangedEvent`; `WoundDamageRoutingSystem.OnBeforeDamageChanged` (`WoundDamageRoutingSystem.cs:69-112`) cancels it and re-routes. `TargetBodyPart.All` is a composite mask, so `SharedTargetingSystem.IsSelectable(requestedTarget)` at `:88` is **false** and `_requestedParts` stays unset — the correct behaviour (`WoundDamageRoutingSystem.cs:85-94`).
3. GUARD D at `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:255-265` raises `DamageDealtEvent` on the wound host; `OnDamageDealt` (`WoundDamageRoutingSystem.cs:127-134`) clears the dict and calls `RouteAppliedDamage`.
4. `RouteAppliedDamage` (`:699-722`) splits `damage` into systemic vs localized by `body.Comp.LocalizedDamageTypes`, then pulls **every negative localized amount** into a `healing` specifier and calls `ApplyLocalizedHealing(body, healing, origin, interruptsDoAfters)` at `:722-723`.
5. `ApplyLocalizedHealing` (`:851-…`) already calls `CanTreatPart(body, part)` for every candidate (`:858`, `:863`) and distributes the heal proportionally to each part's positive damage per type.
6. `ApplyPartChange` (`:979-1003`) writes the part damage and raises `PartDamageAppliedEvent`, which `OrganDamageSystem` fans out to `WoundSystem.HandlePartDamageApplied`; for negative amounts that calls `HealWounds(part, prototype, -severity)`.

**Conclusion: reagents already heal wounds on wound hosts.** The only thing missing is that `_treatmentCapabilities` is never populated for the reagent path, so `CanTreatPart` returns `true` unconditionally (`WoundDamageRoutingSystem.cs:933-941`):
```csharp
private bool CanTreatPart(EntityUid body, EntityUid part)
{
    if (!_treatmentCapabilities.TryGetValue(body, out var capabilities))
        return true;

    return TryComp(part, out WoundableComponent? woundable) &&
           _prototypes.TryIndex(woundable.Profile, out var profile) &&
           profile.TreatmentCapabilities.Overlaps(capabilities);
}
```
and the scoping entry point, verbatim (`WoundDamageRoutingSystem.cs:136-147`):
```csharp
public void WithTreatmentCapabilities(EntityUid body, IReadOnlySet<TreatmentCapability> capabilities, Action action)
{
    _treatmentCapabilities[body] = capabilities;
    try
    {
        action();
    }
    finally
    {
        _treatmentCapabilities.Remove(body);
    }
}
```

### 4.2 HOOK 9a — `Content.Server/EntityEffects/Effects/HealthChange.cs`

Three marked additions. Field list (after `:38`):
```csharp
        // WOLFGATE: HOOK 9 - Wolfmed treatment-capability scope for wound hosts (ONYX ReagentTreatmentEffects.cs:9-13).
        [DataField]
        public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
```
Usings (after `:6`):
```csharp
using Content.Shared._Onyx.Wounds; // WOLFGATE: HOOK 9
```
`Effect()` — replace the single `TryChangeDamage(...)` call at `:167-176` with:
```csharp
            // WOLFGATE: HOOK 9 - route healing through wound treatment-capability scoping (ONYX ReagentTreatmentSystems.cs:14-30).
            var change = Damage * scale;
            void Apply() => args.EntityManager.System<DamageableSystem>().TryChangeDamage(
                args.TargetEntity,
                change,
                IgnoreResistances,
                interruptsDoAfters: false,
                // Shitmed Change Start
                targetPart: TargetBodyPart.All,
                partMultiplier: 1.00f, // Mono, 0.5f->1.00f
                canSever: false);
            // Shitmed Change End

            if (change.DamageDict.Values.Any(amount => amount < 0) &&
                args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity))
                args.EntityManager.System<WoundDamageRoutingSystem>()
                    .WithTreatmentCapabilities(args.TargetEntity, TreatmentCapabilities, Apply);
            else
                Apply();
```
`System.Linq` is already imported at `:9`. This is the minimum that preserves the Shitmed/Mono arguments exactly; a pure "two-line" hook is not possible because the call must become a delegate.

**Do not "fix" the pre-existing upstream bug while here.** `HealthChange.Effect()` builds `damageSpec` and applies the universal reagent modifiers to it at `:142,149-165`, then passes **`Damage * scale`**, not `damageSpec * scale`, at `:169` — the universal modifiers are computed and discarded. That is Wolfgate's current live behaviour; changing it is a balance change unrelated to Wolfmed. Keep `change = Damage * scale`.

### 4.3 HOOK 9b — `Content.Server/EntityEffects/Effects/EvenHealthChange.cs`

`EvenHealthChange` exists (`WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs:16`) but does **not** use `HealEvenly` (absent). It reads `damageable.Damage.DamageDict` (`:107`) to split each group amount proportionally, then makes one `TryChangeDamage` call at `:135-139`.

On a wound host, `damageable.Damage` is the projection of part damage written by `WoundDamageProjectionSystem.RefreshBodyDamage` → `_damage.SetDamage(body, total)` (`WoundDamageProjectionSystem.cs:196`), so the weighting is a faithful aggregate. The routed pass then re-distributes per part. Net effect matches Onyx's `HealEvenly`-then-route closely enough for D4.

Edit shape — identical to 9a:
```csharp
        // WOLFGATE: HOOK 9
        [DataField]
        public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];
```
and in `Effect()`, wrap `:135-139`:
```csharp
        // WOLFGATE: HOOK 9 (ONYX ReagentTreatmentSystems.cs:33-49).
        var final = dspec * scale;
        void Apply() => damagableSystem.TryChangeDamage(args.TargetEntity, final, IgnoreResistances, interruptsDoAfters: false);

        if (Damage.Values.Any(amount => amount < 0) &&
            args.EntityManager.HasComponent<WoundHostComponent>(args.TargetEntity))
            args.EntityManager.System<WoundDamageRoutingSystem>()
                .WithTreatmentCapabilities(args.TargetEntity, TreatmentCapabilities, Apply);
        else
            Apply();
```
`Damage.Values` is `Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>.Values` — matches Onyx's own test (`ReagentTreatmentSystems.cs:45`). Needs `using System.Linq;` (**not currently present** in `EvenHealthChange.cs`; its usings end at `:8`) and `using Content.Shared._Onyx.Wounds;`.

### 4.4 HOOK 9c — `DistributedHealthChange`: **do not author it**

- Zero `!type:DistributedHealthChange` in `ONYX/Resources/Prototypes/_Onyx/Reagents/**` and in `ONYX/Resources/Prototypes/Reagents/**`.
- Zero `DistributedHealthChange` anywhere in WG.
- The class does not exist in Wolfgate and nothing would reference it.

PLAN.md D16 / §2.15's "author it if a ported reagent uses that tag" resolves to **no**. Record as *not needed*, not as *deferred*.

### 4.5 USER DECISION D-P4-1-A — is HOOK 9 worth it in phase 4?

**Facts.** `treatmentCapabilities` can only ever *refuse* a heal when a part's `bodyPartProfile` lacks the reagent's capability. The tree has exactly one profile — `OrganicBodyPartProfile`, `treatmentCapabilities: [Biological]` (`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:2-4`) — and `WoundHost` only lands on organic humanoids (D32, Protogen excluded). Ipc/Slime/Plant/Cybernetic profiles were explicitly dropped for phase 1 (the `# WOLFGATE (WP7)` comment at `wounds.yml:1`). **Therefore HOOK 9 changes no observable behaviour in phase 4.**

**Option 1 (recommended): land HOOK 9 anyway.** ~14 lines across two files, no behaviour change, zero regression surface, and it makes every existing and future `!type:HealthChange`/`!type:EvenHealthChange` reagent — Wolfgate's own included — automatically correct the moment phase 5 adds a Mechanical/Electrical profile. Doing it later means re-opening two upstream files after the manifest has been signed off.

**Option 2: defer HOOK 9 to phase 5** and ship P4-1 as the four effect classes plus the Tier A reagents. Saves two upstream file edits now, costs them later, and leaves a `treatmentCapabilities:` YAML key silently ignored if anyone writes one.

Recommend **Option 1**, recorded in the manifest as *plumbing, inert until IPC/cybernetic profiles land*.

---

## 5. Guidebook text and locale

Onyx's source, read via `git show HEAD:Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` (18 lines, exact):

```ftl
entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } { NATURALFIXED($amount, 2) } pain and multiplies natural pain recovery by up to { NATURALFIXED($recoveryMultiplier, 2) }. Repeated doses stack; the effect fades over { NATURALFIXED($duration, 2) } { MANY("second", $duration) }.

entity-effect-guidebook-mend-fractures =
    { $chance ->
        [1] Reduces
        *[other] reduce
    } matching fracture severity by { NATURALFIXED($amount, 2) } per metabolism tick. Types: { $wounds }. Grades: "{ $minimumGrade }" through "{ $maximumGrade }", inclusive.

entity-effect-guidebook-all-fractures = all fractures

fracture-grade-hairline = hairline
fracture-grade-simple = simple
fracture-grade-displaced = displaced
fracture-grade-comminuted = comminuted
```

**Port target:** `WG/Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` (the `_Onyx` locale tree already exists — `WG/Resources/Locale/en-US/_Onyx/{entity-categories.ftl,medical/fractures.ftl,medical/health-examinable.ftl,prototypes/wounds/wounds.ftl,traits/quirks.ftl}`).

**Key prefix must change.** Wolfgate's convention is `reagent-effect-guidebook-*` — e.g. `reagent-effect-guidebook-health-change`, `reagent-effect-guidebook-even-health-change` (`WG/Resources/Locale/en-US/guidebook/chemistry/effects.ftl:90`). The bodies port verbatim; only the three `entity-effect-guidebook-*` keys are renamed to `reagent-effect-guidebook-suppress-pain`, `reagent-effect-guidebook-mend-fractures`, `reagent-effect-guidebook-all-fractures`. The four `fracture-grade-*` keys keep their names (`MendFractures` builds them by string interpolation).

**One new key with no Onyx source:** `reagent-effect-guidebook-take-stamina-damage`. Onyx's `TakeStaminaDamage` overrides nothing, so its guidebook entry falls through to the base. Wolfgate's `ReagentEffectGuidebookText` is `abstract`, not virtual (`EntityEffect.cs:33`) — **every** subclass must implement it. Write a one-line key, or `return null` (the `GuidebookEffectDescription` wrapper at `:54-58` handles null by omitting the effect). Recommend a real key; `null` hides it from the chemistry guidebook entirely.

`NATURALFIXED` and `MANY` are registered in Wolfgate (`WG/Content.Shared/Localizations/ContentLocalizationManager.cs:38,52`), so no fluent-function work.

### `!type:` tag table (final)

| YAML tag | Onyx type | Wolfgate type (new) | Base |
|---|---|---|---|
| `SuppressPain` | `Content.Shared._Onyx.Wounds.SuppressPain` | `Content.Shared._WF.Wolfmed.EntityEffects.SuppressPain` | `EntityEffect` |
| `MendFractures` | `Content.Shared.EntityEffects.Effects.Damage.MendFractures` | `Content.Shared._WF.Wolfmed.EntityEffects.MendFractures` | `EntityEffect` |
| `TakeStaminaDamage` | `Content.Shared._Onyx.Chemistry.TakeStaminaDamage` | `Content.Shared._WF.Wolfmed.EntityEffects.TakeStaminaDamage` | `EntityEffect` |
| `StaminaDamageCondition` | `Content.Shared._Onyx.Chemistry.StaminaDamageCondition` | `Content.Shared._WF.Wolfmed.EntityEffects.StaminaDamageCondition` | `EntityEffectCondition` |
| `HealthChange` + `treatmentCapabilities:` | partial on upstream | existing `Content.Server.EntityEffects.Effects.HealthChange` + HOOK 9a | — |
| `EvenHealthChange` + `treatmentCapabilities:` | partial on upstream | existing `Content.Server.EntityEffects.Effects.EvenHealthChange` + HOOK 9b | — |
| `DistributedHealthChange` | partial on upstream | **not needed** | — |

---

## 6. Reagents

### 6.1 The tag-translation table — Onyx reagent YAML **does not port verbatim**

This corrects `entityeffects-gap.md`'s "no YAML rewriting needed for this slice". That claim is true **for the five effect tags only**; every other tag in the same files needs translating. Verified by enumerating every `!type:` in `WG/Resources/Prototypes/Reagents/**` and grepping the class names.

| Onyx `!type:` | Wolfgate status | Wolfgate replacement (evidence) |
|---|---|---|
| `HealthChange` | SAME | `WG/Content.Server/EntityEffects/Effects/HealthChange.cs:18` |
| `EvenHealthChange` | SAME | `WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs:16` |
| `ModifyBloodLevel` | SAME | `.../Effects/ModifyBloodLevel.cs:9` |
| `ReduceRotting` | SAME | `.../Effects/ReduceRotting.cs:13` |
| `AdjustTemperature` | SAME | `.../Effects/AdjustTemperature.cs:8` |
| `AdjustReagent` | SAME | `.../Effects/AdjustReagent.cs:12` |
| `MobStateCondition` | SAME | `.../EffectConditions/MobStateCondition.cs:8` |
| `GenericStatusEffect` | SAME | `.../Effects/StatusEffects/GenericStatusEffect.cs:19` — fields `key/component/time/refresh/type` match Onyx's usage |
| `Jitter`, `Drunk`, `PopupMessage`, `Emote`, `PlantAdjustWeeds`, `PlantAdjustHealth`, `PlantDestroySeeds` | SAME | all present under `WG/Content.Server/EntityEffects/Effects/**` |
| `ModifyBleed` | **DIFFERENT (name)** | → `ModifyBleedAmount` (`.../Effects/ModifyBleedAmount.cs:8`) |
| `ReagentCondition` | **DIFFERENT (name)** | → `ReagentThreshold` (`.../EffectConditions/ReagentThreshold.cs:15`); same `min`/`max`/`reagent` fields |
| `MovementSpeedModifier` | **DIFFERENT (name+fields)** | → `MovespeedModifier` (`.../Effects/MovespeedModifier.cs:13`); Onyx's `time:` → `statusLifetime:` (`:31`) |
| `Satiate` + `satiationType:` | **DIFFERENT (split)** | → `SatiateThirst` / `SatiateHunger` (`.../Effects/SatiateThirst.cs:13`, `SatiateHunger.cs:14`) |
| `Vomit` | **DIFFERENT (name)** | → `ChemVomit` (`.../Effects/ChemVomit.cs:12`) |
| `TemperatureCondition` | **DIFFERENT (name)** | → `Temperature` (`.../EffectConditions/BodyTemperature.cs:11`) |
| `ModifyParalysis` | **DIFFERENT (name)** | → `Paralyze` (`.../Effects/Paralyze.cs:7`) — field names must be re-checked at implementation |
| `Flammable` | **DIFFERENT (name)** | → `FlammableReaction` (`.../Effects/FlammableReaction.cs:11`) |
| `PressureThreshold` | **MISSING** | no equivalent condition in `WG/Content.Server/EntityEffects/EffectConditions/` (full listing: `BodyTemperature, HasTagCondition, JobCondition, MobStateCondition, OrganType, ReagentThreshold, SolutionTemperature, TotalDamage, TotalHunger`). Blocks `MinersSalve` and `Luxurium`. |
| `TypedDamageThreshold` | **MISSING** | §3.5. Blocks `SalicylicAcid`, `Oxandrolone`. |
| `ModifyStatusEffect` | **MISSING** | §8. Blocks all of `Narcotics/opioids.yml` and `capsaicin.yml`. |
| `ImmunityModifier`, `DiseaseProgressChange` | **MISSING** | Onyx's whole disease subsystem, not ported. Blocks all of `Medicine/virology.yml`. |
| `ChemCureDnaDisease` | **MISSING** | Onyx Wega genetics. Blocks `_Onyx/Reagents/medicine.yml`'s `Mutadon`. |

### 6.2 Full inventory: `ONYX/Resources/Prototypes/_Onyx/Reagents/Medicine/*.yml`

Five files, 22 reagents, all read in full.

| # | File:line | Reagent | Wound-relevant effects | WG id status | Blocking missing tag | Tier |
|---|---|---|---|---|---|---|
| 1 | `first_aid.yml:1` | **Stasizium** | `MendFractures amount:10 wounds:[] Hairline..Comminuted`; `EvenHealthChange Brute/Burn/Airloss/Toxin/Genetic -20` | **COLLISION** — `WG/Resources/Prototypes/_Goobstation/Reagents/medicine.yml:2` | none | **A** |
| 2 | `first_aid.yml:48` | SilverSulfadiazine | `HealthChange` Heat/Cold heal; Acidic touch reaction | **MISSING** everywhere in `WG/Resources` | none | C |
| 3 | `first_aid.yml:90` | StypticPowder | `HealthChange Brute -2`; Acidic touch + `ModifyBleed -2` | **MISSING** | none (`ModifyBleed`→`ModifyBleedAmount`) | C |
| 4 | `lavaland.yml:1` | VitriumFroth | `HealthChange Brute -2 / Heat,Shock,Cold -1` | **MISSING** | none | C |
| 5 | `lavaland.yml:21` | SerakaExtract | `Bloodloss -1`, `ModifyBleed -1`, `ModifyBloodLevel 1` | **MISSING** | none | C |
| 6 | `synthflesh.yml:22` | Synthflesh | Acidic touch heal + `ModifyBleed -2` | **MISSING** | none | C — **carries an AGPL-3.0-or-later header with 18 SPDX lines (`synthflesh.yml:1-20`); the manifest must record the second license if ported** |
| 7-10 | `virology.yml:1,17,36,55` | Immurin, Spaceacilin, Devirate, TripleCitrus | none (disease) | **MISSING** ×4 | `ImmunityModifier`, `DiseaseProgressChange` | **SKIP — out of scope** |
| 11 | `medicine.yml:1` | MinersSalve | `Brute/Burn -1.75`, `Bloodloss -1.25` | **MISSING** | `PressureThreshold` | C |
| 12 | `medicine.yml:64` | Probital | `Brute -1.5`; `TakeStaminaDamage` ×2; `StaminaDamageCondition` ×3 | **MISSING** | needs `Mitogen` | B |
| 13 | `medicine.yml:129` | **Osteogen** | `MendFractures amount:1 wounds:[BoneFractureWound] maximumGrade:Simple` — **the only effect on the reagent** | **MISSING** | none | **A** |
| 14 | `medicine.yml:145` | Mitogen | `Vomit` at ≥10 | **MISSING** | none (`Vomit`→`ChemVomit`) | B (Probital dep) |
| 15 | `medicine.yml:164` | Mitotrophin | `Brute -4/Burn -2`, `ModifyBleed -0.5`, `ModifyBloodLevel 3` | **MISSING** | none | C |
| 16 | `medicine.yml:190` | **Ibuprofen** | `SuppressPain 0.5 / 27s / ×2.5`; `Brute -1`; `TakeStaminaDamage 2.4` | **MISSING** | none | **A** |
| 17 | `medicine.yml:242` | **Ketorolac** | `SuppressPain 0.9 / 50s / ×3`; `Brute -0.5`; `ModifyBleed 0.15` | **MISSING** | none | **A** |
| 18 | `medicine.yml:278` | SalicylicAcid | `Brute -12 / -3 / +3` by damage threshold | **COLLISION** — `WG/Resources/Prototypes/_NF/Reagents/chemicals.yml:2` | `TypedDamageThreshold` | **DROP** (§6.4) |
| 19 | `medicine.yml:327` | Atropine | crit `Airloss/Brute/Burn/Toxin -2..-4`; `ModifyBleed -1` | **MISSING** | `ModifyParalysis`→`Paralyze` | C |
| 20 | `medicine.yml:386` | Luxurium | big heal under low pressure | **MISSING** | `PressureThreshold` | C |
| 21 | `medicine.yml:412` | **Tramadol** | `SuppressPain 1.25 / 45s / ×3.5`; `GenericStatusEffect Adrenaline/IgnoreSlowOnDamage` | **MISSING** | none — `IgnoreSlowOnDamageComponent` exists (`WG/Content.Shared/Damage/Components/IgnoreSlowOnDamageComponent.cs:9`) | **A** |
| 22 | `medicine.yml:442` | **Oxycodone** | `SuppressPain 2 / 60s / ×4.5`; `GenericStatusEffect Adrenaline` | **MISSING** | none, but its Onyx reaction needs `Heroin` (out of scope) — needs a Wolfgate recipe | **A** (if recipe re-authored) |
| 23 | `medicine.yml:472` | Oxandrolone | `Burn` heal by threshold | **MISSING** | `TypedDamageThreshold` | C |

*(Rows 7-10 counted as four; 23 rows listed for 22 reagents because `first_aid.yml` holds three.)*

Every "MISSING" was checked three ways: `grep -rn "id: <X>" Resources/Prototypes`, a whole-`Resources/` grep, and a locale-key grep. **None of `reagent-name-osteogen`, `-probital`, `-ibuprofen`, `-ketorolac`, `-atropine`, `-tramadol`, `-oxycodone`, `-minerssalve`, `-styptic-powder`, `-silver-sulfadiazine`, `-synthflesh` exists anywhere under `WG/Resources/Locale/en-US`.** Wolfgate's `Resources/Prototypes/Reagents/medicine.yml` has 48 reagents and contains no `SilverSulfadiazine`, `StypticPowder` or `Synthflesh` at all — this fork stripped them (consistent with the known Wolfgate-vs-HardLight divergences). So a reagent port is *net-new content*, not a merge: prototype + locale name/desc + reaction recipe + ChemMaster/dispenser inventory + medkit/cargo placement.

### 6.3 Onyx-marked entries in `ONYX/Resources/Prototypes/Reagents/*.yml`

`git -C C:/tmp/onyx grep -n -i onyx HEAD -- Resources/Prototypes/Reagents` returns 61 hits. Filtering to wound-relevant markers gives exactly **four `<Onyx-PartPain>` blocks**, all pure `!type:SuppressPain` additions to reagents Wolfgate already has:

| ONYX file:line | Reagent | Block | WG id present? |
|---|---|---|---|
| `Consumable/Drink/alcohol.yml:115-123` | **Cognac** | `SuppressPain amount:0.25 decayDuration:9 identifier:Painkiller recoveryMultiplier:1.1` | YES — `WG/Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml:106` |
| `medicine.yml:153-159` | **Bicaridine** | `SuppressPain amount:0.75 decayDuration:18 identifier:Bicaridine recoveryMultiplier:1.75` | YES — `WG/Resources/Prototypes/Reagents/medicine.yml:145` |
| `narcotics.yml:50-56` | **Desoxyephedrine** | `SuppressPain amount:0.75 decayDuration:9 identifier:Desoxyephedrine recoveryMultiplier:1.75` (the `1.75` itself marked `<Onyx-PartPain-edited>`) | YES — `WG/Resources/Prototypes/Reagents/narcotics.yml:2` |
| `narcotics.yml:632-638` | **Happiness** | `SuppressPain amount:0.4 decayDuration:9 identifier:Happiness recoveryMultiplier:1.5` | YES — `WG/Resources/Prototypes/Reagents/narcotics.yml:566` |

All four other Onyx marker families (`<Onyx-BurningPuddles>`, `<Onyx-VirologyReagents>`, `<Onyx-ClothingDirt>`, `<Onyx-Feroxi>`, `<Onyx-CosmicCult>`, `<Onyx-WegaGenetics>`, `<Onyx-XenobiologyMetabolism>`, `<Onyx-CyberneticMetabolism>`, `<Onyx-EasierOpporozidoneRecipe>`) are unrelated subsystems and out of scope.

**These four blocks are Tier A and are the single highest-value item in P4-1**: four reagents Wolfgate already stocks become painkillers for the brand-new pain system, for four small marked YAML additions and zero new content. Note the placement difference — Onyx puts Bicaridine's in the `Bloodstream` group; Wolfgate's Bicaridine also uses `Bloodstream` (check at implementation), and Desoxyephedrine's must go in Wolfgate's `Narcotic` group (`WG/Resources/Prototypes/Reagents/narcotics.yml:45`), not `Poison`.

### 6.4 Collision handling (DECISIONS P4-1)

**`Stasizium` — extend Wolfgate's, do not rename.** Side-by-side:

| Field | ONYX `_Onyx/Reagents/Medicine/first_aid.yml:1-46` | WG `_Goobstation/Reagents/medicine.yml:1-41` |
|---|---|---|
| `name`/`desc`/`physicalDesc`/`flavor`/`color`/`worksOnTheDead` | `reagent-name-stasizium` / … / `#8364BE` / true | **identical, all six** |
| metabolism group | `Bloodstream` | `Medicine` |
| `ModifyBloodLevel 10` | yes | yes |
| bleed | `ModifyBleed -2` | `ModifyBleedAmount -2` (same effect, WG name) |
| `ReduceRotting 30` gated on Dead | yes | yes |
| bulk heal | `EvenHealthChange` groups Brute/Burn/Airloss/Toxin/Genetic `-20` | `HealthChange` **groups** Brute/Burn/Airloss/Toxin/Genetic `-20` |
| overdose | `HealthChange Blunt 100` at `ReagentCondition reagent:Stasizium min:21` | `HealthChange Blunt 100` at `ReagentThreshold min:21` |
| freeze | `AdjustTemperature -1000000` below 263.15 K | `AdjustTemperature -50000` below 263.15 K |
| **`MendFractures amount:10 wounds:[] Hairline..Comminuted`** | **yes** | **no** |

Same reagent, two tag dialects and two balance numbers. It is shipped content in Wolfgate (`MedkitCombatStasiziumFilled`, `StasiziumAutoInjector` — `WG/Resources/Prototypes/_Goobstation/Catalog/Fills/Items/firstaidkit.yml:2,10`, `.../hypospray.yml:4,24`). **Recommendation: add one marked block to WG's existing entry:**
```yaml
        # WOLFGATE (Wolfmed P4-1): ONYX _Onyx/Reagents/Medicine/first_aid.yml:29-33.
        - !type:MendFractures
          amount: 10
          wounds: []
          minimumGrade: Hairline
          maximumGrade: Comminuted
```
Leave `EvenHealthChange`-vs-`HealthChange` and the `-50000`/`-1000000` temperature alone (balance, D4 does not apply to Wolfgate's own content). Record in the manifest.

**`SalicylicAcid` — not equivalent; recommend DROP, not rename.** Wolfgate's, in full (`WG/Resources/Prototypes/_NF/Reagents/chemicals.yml:1-6`):
```yaml
- type: reagent
  id: SalicylicAcid
  name: reagent-name-salicylicacid
  desc: reagent-desc-salicylicacid
  physicalDesc: reagent-physical-desc-powdery
  color: "#EEEEEE"
```
No `group:`, no `metabolisms:`, no effects — an inert Frontier **precursor**. Its only consumers are `WG/Resources/Prototypes/_NF/Recipes/Reactions/chemicals.yml:2` (Phenol + Sodium + SulfuricAcid + Carbon + Oxygen → SalicylicAcid ×3) and `WG/Resources/Prototypes/_NF/Recipes/Reactions/medicine.yml:6` (Cryoxadone + SalicylicAcid + Lipozine → Traumoxadone). Onyx's is a `group: Medicine` brute healer with three threshold-gated `HealthChange` blocks. Adding metabolisms to a precursor would make a chem intermediate heal on ingestion — an unintended Frontier-content change.

DECISIONS allows `OnyxSalicylicAcid`. I recommend against it for three reasons: (a) it needs `TypedDamageThreshold`, a whole missing condition type, for its only interesting behaviour; (b) Onyx's own *reaction* is also `id: SalicylicAcid` (`ONYX/Resources/Prototypes/_Onyx/Recipes/Reactions/medicine.yml:102`), colliding a second time with `WG/.../_NF/Recipes/Reactions/chemicals.yml:2`, so a rename cascades; (c) its role (threshold-scaled brute healing) is already covered by Bicaridine + Brutepack. **Recommend: skip, record as a deliberate omission with the collision reason.** If the user insists, `OnyxSalicylicAcid` + `OnyxSalicylicAcidReaction` + `TypedDamageThreshold` is the full cost.

### 6.5 Reaction and content chain

`ONYX/Resources/Prototypes/_Onyx/Recipes/Reactions/medicine.yml` carries recipes for `Probital, Osteogen, MitotrophinNutriment/Protein/Vitamin, Ibuprofen, Ketorolac, SalicylicAcid, Atropine, Tramadol, Oxycodone, Oxandrolone`. **No recipe for `MinersSalve`, `Mitogen`, `Mitotrophin` (direct).** Notable reactant dependencies:
- `Osteogen` = Bicaridine + Milk + Phosphorus → Osteogen ×3. All three exist in Wolfgate. **Fully portable as-is.**
- `Ibuprofen` = Charcoal + Benzene + Fluorine → ×3. Benzene presence in WG must be checked.
- `Ketorolac` = Ibuprofen + **Tramadol** + Carbon + Plasma → ×1 (chain dependency).
- `Tramadol` = Acetone + Inaprovaline + Ethanol → ×3. All present in Wolfgate.
- `Oxycodone` = Tramadol + **Heroin** + Epinephrine + Plasma → ×1. **`Heroin` is in `_Onyx/Reagents/Narcotics/opioids.yml`, outside P4-1's `Medicine/*.yml` scope.** Either pull `Heroin` in, or re-author the Oxycodone recipe for Wolfgate (a `# WOLFGATE` recipe deviation).

Each ported reagent also needs: locale `reagent-name-X`/`reagent-desc-X` (none exist), a ChemMaster/dispenser listing if it is to be craftable in-round, and optional medkit/cargo placement. Budget ~4 touchpoints per reagent beyond the prototype.

### 6.6 Recommended scope tiers — **USER DECISION D-P4-1-B**

**Tier A — mandatory (recommend).** Delivers the entire gameplay point of P4-1 with no missing tag types and almost no new content:
1. The four `<Onyx-PartPain>` `SuppressPain` blocks on Cognac, Bicaridine, Desoxyephedrine, Happiness (§6.3) — 4 marked YAML additions to existing Wolfgate reagents.
2. `MendFractures` on Wolfgate's existing `Stasizium` (§6.4) — 1 marked YAML addition.
3. **`Osteogen`** — new reagent + recipe (Bicaridine+Milk+Phosphorus, all present) + locale. The dedicated fracture medicine; without it `MendFractures` only exists on a combat-medkit chem.
4. **`Ibuprofen`, `Ketorolac`, `Tramadol`, `Oxycodone`** — the painkiller ladder (0.5 → 0.9 → 1.25 → 2.0 suppression). These make the pain system a thing medics interact with. Recipes need Benzene/Plasma/Epinephrine checks and an Oxycodone-without-Heroin re-author.
5. HOOK 9a + 9b (§4).

Tier A needs **zero** missing effect types beyond the four new classes. `TakeStaminaDamage` is only used by Ibuprofen (at `min: 10`), and `StaminaDamageCondition` by nothing in Tier A — both still ship, because they are cheap and P4-1 names them.

**Tier B — optional.** `Probital` + `Mitogen` (the stamina-cost brute healer; exercises `StaminaDamageCondition` and both `TakeStaminaDamage` modes). Adds a `ForcedSleeping` / `StutteringAccent` `GenericStatusEffect` chain — both components exist in WG.

**Tier C — defer to a later content pass.** `SilverSulfadiazine`, `StypticPowder`, `Synthflesh`, `VitriumFroth`, `SerakaExtract`, `Mitotrophin`, `Atropine`, `MinersSalve`, `Luxurium`, `Oxandrolone`. Each is a whole new medicine with no wound-system interaction beyond ordinary `HealthChange` healing (which already routes to wounds via GUARD D). `MinersSalve`/`Luxurium` additionally need `PressureThreshold`; `Oxandrolone` needs `TypedDamageThreshold`; `Synthflesh` drags in an AGPL header.

**Skip permanently (record):** `Medicine/virology.yml` (4 reagents, needs Onyx's disease subsystem), `_Onyx/Reagents/medicine.yml`'s `Mutadon` (needs Wega genetics), `SalicylicAcid` (§6.4). `Narcotics/opioids.yml`, `Narcotics/capsaicin.yml`, `Toxins/bitrunning.yml` are outside DECISIONS P4-1's stated `Medicine/*.yml` scope and additionally need `ModifyStatusEffect`.

---

## 7. `treatmentCapabilities` for Wolfgate's existing medicines and topicals

### 7.1 Reagents — **no YAML edits needed**

Every `HashSet<TreatmentCapability>` HOOK 9 adds defaults to `[TreatmentCapability.Biological]`, and `OrganicBodyPartProfile` is `treatmentCapabilities: [Biological]`. So all 156 `!type:HealthChange` and 17 `!type:EvenHealthChange` uses across `WG/Resources/Prototypes/Reagents/**` (counted by tag histogram) are correct with **zero** annotation. **Do not write `treatmentCapabilities:` into any Wolfgate reagent in phase 4.** Writing it now would add 170+ diff lines that say exactly what the default already says, and would have to be revisited anyway once phase 5 decides what an IPC limb accepts.

The only reagent that would ever need a non-default is one intended to repair machines. I found none in `WG/Resources/Prototypes/Reagents/**`, `_NF`, `_Mono` or `_Goobstation` reagent files.

### 7.2 Items with `HealingComponent` — one candidate, and it is phase 5

`HealingComponent` already carries the four HOOK-7 fields (`WG/Content.Server/Medical/Components/HealingComponent.cs:55-80`, marked `// WOLFGATE: HOOK 7 / D14`), defaulting `TreatmentCapabilities = [TreatmentCapability.Biological]`. Items reach wounds through a **different** path from reagents: `HealingSystem.Wolfmed.OnWoundHostDoAfter` passes `healing.TreatmentCapabilities` explicitly into `WoundHealingSystem.ResolveHealingPart(...)` (`WG/Content.Server/_WF/Wolfmed/Medical/HealingSystem.Wolfmed.cs:49-51`), which tests `profile.TreatmentCapabilities.Overlaps(treatmentCapabilities)` at `WG/Content.Server/_Onyx/Wounds/WoundHealingSystem.cs:162`. Items do **not** use `WithTreatmentCapabilities`. Two independent mechanisms — do not try to unify them in phase 4.

Complete `HealingComponent` inventory in WG (`grep -rln "type: Healing$"` → 4 files):

| Entity | File:line | `damageContainers` | Recommendation |
|---|---|---|---|
| `Ointment` (+ `Ointment1/10Lingering`, `OintmentAdvanced1`) | `Entities/Objects/Specific/Medical/healing.yml:23,33-38` | `[Biological]` | default `[Biological]` — **no edit** |
| `RegenerativeMesh` | `…healing.yml:80,90-93` | `[Biological]` | no edit |
| `Brutepack` (+ variants) | `…healing.yml:128,136-141` | `[Biological]` | no edit |
| `MedicatedSuture` | `…healing.yml:180,191-196` | `[Biological]` | no edit |
| `Bloodpack` | `…healing.yml:227,235-240` | `[Biological]` | no edit |
| `Tourniquet` | `…healing.yml:268,279-284` | `[Biological]` | no edit — **note for P4-2: Wolfgate already ships an entity named `Tourniquet` with a `Healing` component that deals `Brute 5` and `bloodlossModifier: -10`. Onyx's `_Onyx/Medical/Tourniquet` port collides with it by name and by function.** Flagging for the P4-2 analyst. |
| `Gauze` (+ variants) | `…healing.yml:304,315-320` | `[Biological]` | no edit |
| `HealingToolbox` | `…healing.yml:371,379-385` | `[Biological]` | no edit |
| `MaterialCloth` | `Entities/Objects/Materials/materials.yml:90,95-102` | `[Biological]` | no edit |
| `ResinJelly` | `_Mono/Entities/Objects/Specific/xeno_drops.yml:3,14-24` | `[Biological]` | no edit |
| **cable coil** | `Entities/Objects/Tools/cable_coils.yml:36-45` | **`[Silicon]`** — heals Heat/Shock/Radiation `-3.0` | **the only `treatmentCapabilities: [Mechanical, Electrical]` candidate in the tree** |

**Recommendation:** annotate **nothing** in phase 4. Cable coil is the sole Mechanical/Electrical item, and it is inert until a non-Biological `bodyPartProfile` exists (phase 5, D3). Record the cable-coil mapping in `WOLFMED_STATUS.md` as the phase-5 first action, so the intent is not lost. If the user prefers the annotation now, it is one marked `treatmentCapabilities: [Mechanical, Electrical]` line at `cable_coils.yml:37` and is provably behaviour-neutral today.

### 7.3 What the capability gate can and cannot reach

`WoundHostComponent.LocalizedDamageTypes` defaults to `[Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic]` (`WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:35-44`) and Wolfgate's `base.yml:252` leaves it at the default (`# D20: localizedDamageTypes left at Onyx's default, Caustic included`). `RouteAppliedDamage` only sends **localized** negatives through `ApplyLocalizedHealing`/`CanTreatPart` (`WoundDamageRoutingSystem.cs:701-723`). Therefore healing of `Toxin`, `Airloss`/`Asphyxiation`, `Bloodloss`, `Genetic`, `Cellular`, `Radiation` goes to `SystemicDamageComponent` and **bypasses `treatmentCapabilities` entirely**. This is Onyx's behaviour too — document it so nobody debugs "why is Dylovene not capability-gated".

---

## 8. P4-5 — pain numbness

### 8.1 The Onyx chain, end to end

1. `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` — the component. **Already in WG** (`WG/Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs:12`), ported in phase 2, registers as `PainNumbnessStatusEffect`.
2. `ONYX/Resources/Prototypes/Entities/StatusEffects/body.yml:26-37` —
```yaml
- type: entity
  abstract: true
  parent: MobStatusEffectBase
  id: PainNumbnessStatusEffectBase
  components:
  - type: StatusEffect
    whitelist:
      components:
      - MobState
      - MobThresholds
  - type: PainNumbnessStatusEffect
```
3. `ONYX/Resources/Prototypes/Entities/StatusEffects/body.yml:56-59` — `parent: [ PainNumbnessStatusEffectBase, MobStatusEffectDebuff ]`, `id: StatusEffectPainNumbness`.
4. `ONYX/Resources/Prototypes/Entities/StatusEffects/traits.yml:20-23` — `parent: [ PainNumbnessStatusEffectBase, TraitStatusEffectBase ]`, `id: TraitStatusEffectPainNumbness`; used by `ONYX/Resources/Prototypes/Traits/disabilities.yml:100` via `specials:`.
5. The applier — `!type:ModifyStatusEffect effectProto: StatusEffectPainNumbness`, at `ONYX/Resources/Prototypes/Reagents/narcotics.yml:47-48` (Desoxyephedrine, `time: 2`) and `:556-558` (StrawberryIce, `time: 10`). **Both are unmarked upstream code, not Onyx additions.**

### 8.2 WG state

- `PainNumbnessStatusEffectBase`, `StatusEffectPainNumbness`, `TraitStatusEffectPainNumbness`, `TraitStatusEffectBase`: **all MISSING.** `WG/Resources/Prototypes/Entities/StatusEffects/` contains exactly one file, `misc.yml`, with `StatusEffectBase`, `MobStatusEffectBase`, `MobStatusEffectDebuff` and a trailing comment (`misc.yml:31-32`): *"WOLFGATE: MobStandStatusEffectBase and every concrete status effect entity in Onyx's file are dropped: they need the KnockdownImmune tag and status-effect components Wolfgate does not have."*
- `PainNumbnessStatusEffectComponent` is present but **dead**: the only references are two *reads* — `PainSystem.cs:336` (`_statusEffects.EnumerateStatusEffects<PainNumbnessStatusEffectComponent>(entity).Any(effect => effect.Comp1.Applied)`) and `EmoteOnDamageSystem.PainSounds.cs:37`. **Nothing in the tree ever adds it.** Grep over all `.cs`/`.yml`/`.ftl` confirms.
- Wolfgate's live pain numbness is the **legacy** path: trait `PainNumbness` (`WG/Resources/Prototypes/Traits/disabilities.yml:69,74`) grants `PainNumbnessComponent` (`WG/Content.Shared/Traits/Assorted/PainNumbnessComponent.cs:8`), which `PainSystem.IsPainNumb` honours via the P2-D8 widening (`PainSystem.cs:329-334`).
- Wolfgate's **Desoxyephedrine has no pain-numbness effect at all** (`WG/Resources/Prototypes/Reagents/narcotics.yml:45-75`, `Narcotic` group: `MovespeedModifier`, `GenericStatusEffect Stutter`, `Jitter`, `GenericStatusEffect Stun/KnockedDown Remove`). Wolfgate never had it; this is not a regression to repair.
- `StrawberryIce`: not checked as in-scope; it is a `Toxins`/drink reagent outside P4-1.

### 8.3 Cost of porting `ModifyStatusEffect` old-style

All four `StatusEffectsSystem` (new) APIs it needs exist in WG (`StatusEffectSystem.API.cs:48,91,134,307`). The port is:
- ~45 LOC: `Content.Shared/_WF/Wolfmed/EntityEffects/ModifyStatusEffect.cs` merging Onyx's `BaseStatusEntityEffect<T>` fields (`Time`, `Type`, `Delay`) into one old-style class with `EffectProto`.
- **One real friction point:** the enum. `StatusEffectMetabolismType` already exists in WG as `{ Add, Remove, Set }` (`WG/Content.Server/EntityEffects/Effects/StatusEffects/GenericStatusEffect.cs:76-81`) — Onyx's has a fourth member `Update`, which is Onyx's **default** and the value every one of Onyx's reagent entries relies on implicitly. Adding `Update` to the shipped enum is an upstream edit that changes nothing for `GenericStatusEffect` (its `if/else` chain has no `Update` branch, so `Update` would silently fall through to no-op — a landmine). **Declare a separate `_WF` enum** (e.g. `WolfmedStatusEffectAction { Update, Add, Remove, Set }`); enum type names play no part in `!type:` resolution.
- 2 prototypes (`PainNumbnessStatusEffectBase`, `StatusEffectPainNumbness`) in a new `WG/Resources/Prototypes/_Onyx/StatusEffects/body.yml`.
- 1-2 locale keys (`entity-effect-guidebook-status-effect` / `-indef`, renamed to Wolfgate's `reagent-effect-*` prefix).
- Zero new components, zero new subscriptions.

Roughly a half-day including a test.

### 8.4 USER DECISION D-P4-5 — **recommend SKIP and record**

**Recommendation: skip in phase 4; record in `WOLFMED_STATUS.md` as a known gap with the exact cost above.** Rationale:
1. **No in-scope consumer.** Not one reagent in `_Onyx/Reagents/Medicine/*.yml` uses `ModifyStatusEffect`, and none of the four `<Onyx-PartPain>` blocks does either. P4-5's own wording — *"if the ported reagents need them; otherwise record as skipped"* — resolves to skipped on the evidence.
2. **Wolfgate parity is already met.** The trait path works today via `PainNumbnessComponent`; the phase-2 widening at `PainSystem.cs:329-334` was written precisely so that the trait, not the status effect, drives numbness.
3. **It is a balance change dressed as a port.** `PainNumbnessStatusEffect` hides the damage overlay and shows the health alert as full (`PainNumbnessStatusEffectComponent.cs:7-8` summary). Giving that to meth in a gun-PvP server is a real combat buff that Wolfgate has never had, and D4 ("Onyx defaults") does not cover a reagent Wolfgate's own YAML defines differently.
4. The work does not get harder later: `ModifyStatusEffect` has no dependency on anything phase 4 builds.

**If the user says yes anyway,** the minimum honest scope is: `ModifyStatusEffect` + `WolfmedStatusEffectAction` enum + the two prototypes + `StatusEffectPainNumbness` on Desoxyephedrine (`time: 2`) as a **marked Wolfgate balance addition**, plus a test asserting `PainSystem.IsPainNumb` flips while the effect is live. Do **not** also add `TraitStatusEffectPainNumbness` — that would duplicate the working legacy trait and give the same character two numbness sources.

**Cleanup either way:** if P4-5 is skipped, `PainNumbnessStatusEffectComponent` and the two reads of it stay dead. Leave them (they are vendored-adjacent and harmless) but say so in the manifest, so a future reader does not assume a live feature.

---

## 9. Metabolism plumbing and `Scale` semantics

### 9.1 Wolfgate — `Content.Server/Body/Systems/MetabolizerSystem.cs`

```csharp
var rate = entry.MetabolismRate * group.MetabolismRateModifier;            // :181
mostToRemove = FixedPoint2.Clamp(rate, 0, quantity);                       // :184
float scale = (float) mostToRemove / (float) rate;                         // :186
…
var actualEntity = ent.Comp2?.Body ?? solutionEntityUid.Value;             // :197
var args = new EntityEffectReagentArgs(actualEntity, EntityManager, ent, solution,
    mostToRemove, proto, null, scale);                                     // :198
foreach (var effect in entry.Effects)
{
    if (!effect.ShouldApply(args, _random)) continue;                      // :203
    if (effect.ShouldLog) { _adminLogger.Add(…); }                         // :205-216
    effect.Effect(args);                                                   // :218
}
```
Server-only, unpredicted, plain virtual dispatch. **Every effect must self-check its own component** — there is no ECS component filter.

### 9.2 Onyx — `Content.Shared/Metabolism/MetabolizerSystem.cs` (read via `git show`)

```csharp
var rate = solutionData.MetabolizeAll ? quantity : entry.MetabolismRate;   // :184
var mostToRemove = FixedPoint2.Clamp(rate, 0, quantity);                   // :187
var scale = (float) mostToRemove;                                          // :193
if (!solutionData.MetabolizeAll) scale /= (float) rate;                    // :194-195
…
if (scale < effect.MinScale) continue;                                     // :208
ApplyEffect(effect);                                                       // :218
void ApplyEffect(EntityEffect effect)
{
    switch (effect)
    {
        case ModifyLungGas:   _entityEffects.ApplyEffect(ent, effect, scale); break;
        case AdjustReagent:   _entityEffects.ApplyEffect(solutionEntity.Value, effect, scale); break;
        default:              _entityEffects.ApplyEffect(actualEntity, effect, scale); break;
    }
}
```
Shared/predicted, ECS-event dispatch, per-effect-type target switch, `MinScale` gate.

### 9.3 The differences that actually bite

| Aspect | Wolfgate | Onyx | Consequence for P4-1 |
|---|---|---|---|
| `Scale` type | `FixedPoint2` (`EntityEffect.cs:121`) | `float` | `Amount * scale` is FP2×FP2 — fine. `TakeStaminaDamage`'s `!= 1f` becomes `!= FixedPoint2.New(1)`. |
| `Scale` range | **`mostToRemove / rate` with `mostToRemove = Clamp(rate, 0, quantity)` ⇒ strictly `[0, 1]`**, and `== 1` unless the reagent is running out | `[0, 1]` normally; **`> 1` possible** when a metabolism entry sets `metabolizeAll: true` (then `scale = mostToRemove`, a raw unit count) | **Corrects PLAN.md §8.2's note** ("Onyx's framework clamps Scale to ≤1 unless Scaling is set; Wolfgate's is unclamped"). The opposite is true: Wolfgate's is structurally bounded to `[0,1]`; Onyx's is the one that can exceed 1. Wolfgate has no `MetabolizeAll`. **Net: `SuppressPain`'s `Amount * scale` and `MendFractures`'s `Amount * scale` behave identically or slightly more conservatively in Wolfgate. No tuning item.** |
| other `Scale` producers | all four other `EntityEffectReagentArgs` construction sites pass `1f` — `ChemicalReactionSystem.cs:201`, `ReactiveSystem.cs:42`, `ReagentPrototype.cs:200`, `DeepFryerSystem.cs:447`/`.Update.cs:55` | n/a | reactive/touch applications get `Scale == 1`, so `TakeStaminaDamage`'s gate never blocks a touch reaction |
| `MinScale` | absent | `EntityEffect.cs:32`, gated at `MetabolizerSystem.cs:208` and `SharedEntityEffectsSystem.cs:115` | No reagent in `_Onyx/Reagents/**` sets `minScale:`; the only `minScale:` uses at the pin are in `Reagents/chemicals.yml` and `Reagents/gases.yml`, both out of scope. **Nothing to port.** |
| component gating | none — each effect self-checks | ECS subscription filters by `TComp` | Every one of the four classes must open with an explicit `TryGetComponent`/`HasComponent` (done in §3). |
| condition dispatch | `EntityEffectExt.ShouldApply` loops `Conditions` and calls `cond.Condition(args)` synchronously with the same args (`EntityEffect.cs:70-89`) | `EntityConditionSystem<T,TCon>` raises `EntityConditionEvent<T>`; `TryCondition` XORs `Inverted`; carries `EntityUid? sourceEnt`; `MetabolizerSystem.CanMetabolizeEffect` routes `ReagentCondition` to the solution and `MetabolizerTypeCondition` to a vampire special-case | `StaminaDamageCondition` targets the body in both designs, so the simpler Wolfgate shape is exact. No `Inverted`, no `sourceEnt` needed. |
| target entity | always `actualEntity` (the body) | `switch` by effect type | irrelevant for all four classes (all body-targeted) |
| prediction | server-only | shared/predicted | consistent with D13/D35; `PainSystem.SuppressPain` and `WoundSystem.ChangeSeverity` are `_net.IsServer`-gated anyway |

**Conclusion: no Scale-semantics work item, and PLAN.md §8.2's tuning note should be retracted.**

---

## 10. Subscription-pair and registration-name audit

### 10.1 New `SubscribeLocalEvent<Component, Event>` pairs: **none**

P4-1 and P4-5 add **no `EntitySystem`s**. `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition` and (if taken) `ModifyStatusEffect` are `[ImplicitDataDefinitionForInheritors]` data classes dispatched by a direct virtual call from `MetabolizerSystem.cs:218`. HOOK 9 mutates two existing effect classes. Nothing registers a directed subscription.

This is the whole audit for this slice — there is no pair to collide with, in either direction. (For completeness: the one directed subscription the reagent path *travels through*, `<WoundHostComponent, DamageDealtEvent>` at `WoundDamageRoutingSystem.cs:65`, was claimed in phase 1 and is unchanged.)

### 10.2 New component registration names: **none**

- No `[RegisterComponent]` type is added by P4-1.
- P4-5, if taken, adds only **prototypes**, reusing components already registered: `StatusEffect` (`WG/Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs`) and `PainNumbnessStatusEffect` (`WG/Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs:12`). `grep -rn "PainNumbnessStatusEffect" WG` shows no second registrant.
- New **prototype ids** to check instead: `StatusEffectPainNumbness`, `PainNumbnessStatusEffectBase` — both absent from WG (`grep -rn "StatusEffectPainNumbness" WG` returns only a comment in `PainSystem.cs:331`). `OnyxStasizium`/`OnyxSalicylicAcid` would be new ids if the user rejects §6.4.
- New **C# type names** to check for `!type:` ambiguity: `SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `StaminaDamageCondition` — all four verified absent from WG (§2). `ModifyStatusEffect` likewise absent.
- **One near-miss to watch:** `StatusEffectMetabolismType` already exists in WG (§8.3). It is an enum, not a `!type:`-resolved class, so it cannot collide at YAML level — but it *will* collide at C# level if a `_WF` file declares another one and both namespaces are imported. Use a distinct name.

---

## 11. Ordered file list, difficulty, and traps

### 11.1 Work order (each step compiles on its own)

| # | File | Action | Diff | Difficulty |
|---|---|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/EntityEffects/SuppressPain.cs` | NEW | ~35 | **Low** |
| 2 | `Content.Shared/_WF/Wolfmed/EntityEffects/MendFractures.cs` | NEW | ~55 | **Low** |
| 3 | `Content.Shared/_WF/Wolfmed/EntityEffects/TakeStaminaDamage.cs` | NEW | ~35 | **Low** |
| 4 | `Content.Shared/_WF/Wolfmed/EntityEffects/StaminaDamageCondition.cs` | NEW | ~28 | **Low** |
| 5 | `Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` | NEW | 20 | **Low** |
| 6 | `Content.Server/EntityEffects/Effects/HealthChange.cs` | HOOK 9a — 1 using, 3-line field, `Effect()` body → local fn + branch | ~16 | **Medium** (upstream file; the Shitmed/Mono args at `:172-176` must survive byte-for-byte) |
| 7 | `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` | HOOK 9b — 2 usings (`System.Linq` is missing), 3-line field, branch | ~15 | **Medium** |
| 8 | `Resources/Prototypes/_Goobstation/Reagents/medicine.yml` | `MendFractures` block on `Stasizium` | 6 | **Low** |
| 9 | `Resources/Prototypes/Reagents/medicine.yml` | `<Onyx-PartPain>` → Bicaridine | 6 | **Low** |
| 10 | `Resources/Prototypes/Reagents/narcotics.yml` | `<Onyx-PartPain>` → Desoxyephedrine (`Narcotic` group) + Happiness | 12 | **Low** |
| 11 | `Resources/Prototypes/Reagents/Consumable/Drink/alcohol.yml` | `<Onyx-PartPain>` → Cognac | 6 | **Low** |
| 12 | `Resources/Prototypes/_Onyx/Reagents/Medicine/medicine.yml` | NEW — Tier A reagents only (Osteogen, Ibuprofen, Ketorolac, Tramadol, Oxycodone) with translated tags | ~140 | **Medium** (tag translation per §6.1; every `ReagentCondition`→`ReagentThreshold`) |
| 13 | `Resources/Prototypes/_Onyx/Recipes/Reactions/medicine.yml` | NEW — 5 recipes; verify every reactant exists; re-author Oxycodone without `Heroin` | ~60 | **Medium** |
| 14 | `Resources/Locale/en-US/_Onyx/reagents/medicine.ftl` | NEW — name/desc for the 5 | ~20 | **Low** |
| 15 | ChemMaster/dispenser + medkit placement for the 5 | edits | ~15 | **Low-Medium** (owner conflicts with P4-2's medkit fills — assign one owner) |
| 16 | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReagentTreatmentTest.cs` | NEW — §11.3 | ~200 | **Medium** |
| 17 | `Docs/Wolfmed/WOLFMED_MANIFEST.md`, `WOLFMED_STATUS.md` | append | — | Low |
| — | *(P4-5 only, if taken)* `Content.Shared/_WF/Wolfmed/EntityEffects/ModifyStatusEffect.cs`, `Resources/Prototypes/_Onyx/StatusEffects/body.yml`, locale | NEW | ~80 | **Medium** |

**Overall difficulty: Low-Medium.** The C# is the easy half; the YAML translation and content chain is where the time goes. No compat-layer additions are needed — the layer built in phases 1-3 already covers everything (`WolfmedDamageableSystem`, `DamageDealtEvent`, `SharedTargetingSystem.IsSelectable`, `WoundTargetResolver` are all used *behind* the hooks, not by them).

### 11.2 Traps

- **T1 — `!type:` resolution is by bare `Type.Name`.** Naming the new classes anything else (e.g. `WolfmedSuppressPain`) silently breaks every ported YAML with no error message: the deserializer throws on the *unknown tag*, which surfaces as a prototype-load failure at server start, not a compile error. Keep the four names exact.
- **T2 — `ReagentEffectGuidebookText` is `abstract`, not virtual** (`EntityEffect.cs:33`). All four classes must implement it or fail to compile. `TakeStaminaDamage` has no Onyx text — write one.
- **T3 — `WithTreatmentCapabilities` is not reentrant.** `_treatmentCapabilities[body] = capabilities; try { action(); } finally { _treatmentCapabilities.Remove(body); }` (`WoundDamageRoutingSystem.cs:136-147`). A nested call removes the outer scope on the inner `finally`, so the rest of the outer heal runs **unscoped**. Phase 4 must not nest it — and note that `HealingSystem.Wolfmed` deliberately does *not* use it (§7.2), so today there is no nesting. If a future surgery or patch path wants scoping, convert the dictionary to a stack first.
- **T4 — do not "fix" `HealthChange`'s discarded universal modifiers** (`HealthChange.cs:142,149-169`). Live Wolfgate behaviour; out of scope (§4.2).
- **T5 — `EvenHealthChange.cs` has no `using System.Linq;`** (usings end at `:8`). HOOK 9b's `.Any(...)` needs it.
- **T6 — Metabolism group names differ.** Onyx's ported reagents all use `Bloodstream:`; Wolfgate reagents variously use `Bloodstream`, `Medicine`, `Narcotic`, `Poison` (`Stasizium` uses `Medicine`; `Desoxyephedrine` splits `Poison`/`Narcotic`). A blind copy of Onyx's `Bloodstream:` block can land a `SuppressPain` in a group the target's metabolizer does not process. Check `MetabolismGroups` on the organ prototype for each edit.
- **T7 — `Immediate` divergence** (§3.3) — a deliberate difference from Onyx; must be recorded, not silently shipped.
- **T8 — `MendFractures` with `wounds: []` treats *every* fracture prototype.** `Stasizium`'s block uses `wounds: []` + `amount: 10`; with `BoneFractureWound` the only fracture prototype in `wounds.yml`, that is equivalent today, but it will also hit any future fracture type.
- **T9 — reagent healing is already live.** Any test asserting "reagent X does not affect wounds before HOOK 9" will **fail**: GUARD D already routes it. Write the tests against the *capability scope*, not against wound healing per se.

### 11.3 Test plan for this slice (feeds P4-8)

All headless, in `Content.IntegrationTests/Tests/_WF/Wolfmed/` (matching the existing `WolfmedPainTest.cs` / `WoundDamageFoundationTest.cs` layout).

1. **T-P4-SUPPRESS** — damage a human's arm to raise pain; apply a `SuppressPain` effect directly (`new SuppressPain { Amount = …, DecayDuration = … }.Effect(args)` or via a test reagent); assert `PainSystem.GetPain(body)` drops, that `PainComponent.SuppressionModifiers` is keyed by `Identifier`, that a second dose with the same identifier **accumulates** (`PainSystem.cs:347-350`), and that a different identifier stacks independently. Then tick and assert decay.
2. **T-P4-MEND** — create a `BoneFractureWound` on a part, apply `MendFractures { Amount = 5 }`, assert severity dropped by 5; assert the `MaximumGrade: Simple` filter refuses a `Comminuted` fracture; assert a `wounds: [SomeOtherWound]` filter is a no-op.
3. **T-P4-STAM** — `TakeStaminaDamage` raises `StaminaComponent.StaminaDamage`; `StaminaDamageCondition { Min = 80 }` flips at the threshold via `StaminaSystem.GetStaminaDamage`.
4. **T-P4-CAP-SCOPE** — the HOOK 9 test, **and the only way to test it with one profile**: inside `WithTreatmentCapabilities(body, new HashSet<TreatmentCapability> { TreatmentCapability.Mechanical }, …)`, a `HealthChange Brute: -10` must heal **nothing** (`CanTreatPart` refuses `[Biological]` parts); with `{ Biological }` it must heal normally; with no scope at all it must heal normally. Assert `_treatmentCapabilities` is cleared afterwards (call it twice and check the second unscoped heal lands).
5. **T-P4-REAGENT-WOUND** — metabolise Bicaridine on a wounded wound host and assert both the part `DamageableComponent` **and** the `BluntWound` severity fall — pins §4.1's "already works" so a later refactor cannot silently break it.
6. **T-P4-SYSTEMIC-BYPASS** — a `Toxin: -5` heal inside a `{ Mechanical }` scope still lands (systemic path is not capability-gated, §7.3). Guards against someone "fixing" the gate into the systemic branch.
7. *(P4-5 only)* **T-P4-NUMB** — `ModifyStatusEffect{ EffectProto = "StatusEffectPainNumbness" }` makes `PainSystem.IsPainNumb(body)` true and `GetPain` return zero, and it expires.

Reminder from prior phases: run `DockTest` / a `db.ef` sqlite-warning check first — those warnings fail every pair test and will mask real failures here.

---

## 12. Decisions the user must make

| ID | Question | Options | Recommendation |
|---|---|---|---|
| **D-P4-1-A** | Land HOOK 9 now, knowing it is behaviourally inert until phase 5 adds a non-Biological `bodyPartProfile`? | (1) land it (~14 lines, 2 upstream files) (2) defer to phase 5 | **(1) land it.** Zero regression surface; avoids re-opening two upstream files after sign-off; makes every future reagent automatically correct. Record as *inert plumbing*. |
| **D-P4-1-B** | How much of the Onyx reagent set ships? | Tier A / A+B / A+B+C (§6.6) | **Tier A** (4 `<Onyx-PartPain>` blocks + Stasizium's `MendFractures` + Osteogen + the Ibuprofen→Oxycodone painkiller ladder). It delivers the full gameplay point with no missing effect types. Tier B (`Probital`+`Mitogen`) if the package is otherwise green. Tier C is a separate content pass. |
| **D-P4-1-C** | `Stasizium` collision | extend WG's / rename Onyx's to `OnyxStasizium` | **Extend.** Same name, desc, colour, flavour, `worksOnTheDead` and five of six effects; it is the same reagent in two tag dialects. One marked `MendFractures` block. |
| **D-P4-1-D** | `SalicylicAcid` collision | extend / rename to `OnyxSalicylicAcid` / **drop** | **Drop.** WG's is an inert Frontier precursor for Traumoxadone with no metabolisms; Onyx's is a medicine. Not equivalent, so extending is wrong; and a rename also has to rename the colliding *reaction* id and drag in `TypedDamageThreshold`. Its role is covered by Bicaridine + Brutepack. |
| **D-P4-1-E** | `TakeStaminaDamage.Immediate` — honour it (Wolfgate can) or leave it inert like Onyx? | honour / inert | **Honour**, recorded as a corrected-upstream-bug deviation (precedent: phase-2 §8.2-1 and §8.2-3). Only affects Probital, which is Tier B. |
| **D-P4-1-F** | Annotate `treatmentCapabilities` on Wolfgate items/reagents now? | annotate all / annotate cable coil only / **none** | **None** in phase 4. The `[Biological]` default is already right for every one of the 11 `HealingComponent` entities and all 173 `HealthChange`/`EvenHealthChange` reagent uses. Record cable coil (`cable_coils.yml:37`, `damageContainers: [Silicon]`) as the single phase-5 `[Mechanical, Electrical]` candidate. |
| **D-P4-5** | Port the pain-numbness chain? | port (~½ day) / **skip and record** | **Skip and record.** No in-scope reagent needs it; the legacy trait path already works via P2-D8; and giving meth full HUD-blindness immunity is a combat balance change Wolfgate never had. If taken, see §8.4 for the minimum honest scope. |

### Blockers

**None that stop phase 4.** Three things that would become blockers if scope creeps:

- **B1 — `PressureThreshold` does not exist in Wolfgate** (no equivalent condition in `Content.Server/EntityEffects/EffectConditions/`). Hard-blocks `MinersSalve` and `Luxurium`. Both are Tier C.
- **B2 — `TypedDamageThreshold` does not exist**, and its Onyx body calls `DamageableSystem.GetAllDamage`, which Wolfgate also lacks. Hard-blocks `SalicylicAcid` and `Oxandrolone`. Both are droppable.
- **B3 — Onyx's `Oxycodone` reaction requires `Heroin`**, which lives outside P4-1's stated `Medicine/*.yml` scope. Either widen the scope by one reagent or re-author the recipe as a `# WOLFGATE` deviation. Needed for Tier A item 4.

### Cross-package notes for the other phase-4 analysts

- **P4-2 (tourniquet):** Wolfgate **already ships an entity `Tourniquet`** with a `HealingComponent` (`WG/Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml:268-300`, `Brute: 5` + `bloodlossModifier: -10` + `delay: 0.5`). Onyx's `_Onyx/Medical/Tourniquet` port collides by id and overlaps by function.
- **P4-3/P4-4:** items reach wounds through `WoundHealingSystem.ResolveHealingPart`, **not** `WithTreatmentCapabilities` (§7.2). Do not unify the two paths, and do not nest the scope (T3).
- **P4-7 (guidebook):** the wounds guidebook entry is `ONYX/Resources/Locale/en-US/_Onyx/guidebook/wounds.ftl` + `ONYX/Resources/Prototypes/_Onyx/Guidebook/medical.yml`; the *entity-effect* guidebook strings are the separate file handled here (§5).
