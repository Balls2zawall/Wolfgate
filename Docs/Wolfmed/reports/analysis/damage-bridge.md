# Wolfmed D2 — Damage bridge design (Shitmed ⇄ Onyx `WoundDamageRoutingSystem`)

Analyst report. Everything below was read in the actual trees:

- **WG** = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
- **ONYX** = `C:/tmp/onyx` @ `2f5bab9` (sparse). Files quoted exist; anything not quoted is flagged as "absent from sparse checkout" where relevant.

---

## 0. Headline

**The bridge is not a matter of "skip Shitmed's spreading".** Onyx's routing is built on Wizden's *new* damage system, in which `DamageableSystem.ChangeDamage` **never writes damage itself** — it raises `DamageDealtEvent` and a separate subscriber (`InjurableComponent`) does the write. Routing hooks exactly between those two points. Wolfgate's `DamageableSystem.TryChangeDamage` writes inline, so **that seam does not exist in Wolfgate and must be created** (one `// WOLFGATE` insertion of ~4 lines). Everything else in D2 falls out of that.

**Blocker-class issues (details in §8):**

1. **`DamageDealtEvent` seam missing.** Without it, `WoundDamageRoutingSystem.OnDamageDealt` (ONYX `WoundDamageRoutingSystem.cs:64-72`) can never run, and a cancelled `BeforeDamageChangedEvent` means *no damage at all* lands on a WoundHost. Not optional.
2. **`DamageSpecifier.Clone()` does not exist in Wolfgate.** Routing calls it 6× . Needs a compat extension (§7.3).
3. **Adding an `Entity<DamageableComponent?>` overload of `TryChangeDamage` to Wolfgate's `DamageableSystem` silently hijacks ~106 existing call sites** (C# better-conversion-target rule). Must be avoided; see §7.1 for the precise rule and the safe subset.
4. **D2's premise "gated on Onyx's `CCVars.Wounds`" is not achievable as written** — `ONYX/Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` has **no master on/off CVar** (only bleeding-autostop, explosion tuning, and `wounds.body_part_functionality_enabled`, default **`false`**). Gating must be component-presence only.

---

## 1. Wolfgate today — verified facts

### 1.1 `WG/Content.Shared/Damage/Systems/DamageableSystem.cs` (583 lines, `public sealed partial class DamageableSystem : EntitySystem`, namespace `Content.Shared.Damage`)

Shitmed did **not** inline the part spreading into `TryChangeDamage`; it raises an event. The relevant body of `TryChangeDamage` (lines 190-272):

```csharp
// :190
public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool ignoreGlobalModifiers = false,
    float armorPenetration = 0f,
    // Shitmed Change
    bool? canSever = true, bool? canEvade = false, float? partMultiplier = 1.00f, TargetBodyPart? targetPart = null, EntityUid? tool = null,
    // Mono: arg to ID indirect damage sources
    DamageOriginFlag? originFlag = null)
```

| Line | What happens |
|---|---|
| 198-203 | resolve `DamageableComponent`, else return `null` |
| 205-208 | `if (damage.Empty) return damage;` |
| 210-212 | `var before = new BeforeDamageChangedEvent(damage, origin, targetPart, false, originFlag); RaiseLocalEvent(uid.Value, ref before);` |
| **214-215** | `if (before.Cancelled) return null;` ← **everything below is skipped on cancel** |
| 217-224 | `var partDamage = new TryChangePartDamageEvent(damage, origin, targetPart, ignoreResistances, canSever ?? true, canEvade ?? false, partMultiplier ?? 1.00f); RaiseLocalEvent(uid.Value, ref partDamage); if (partDamage.Evaded \|\| partDamage.Cancelled) return null;` ← **the Shitmed part-spread hook** |
| 226-245 | resistances: `DamageSpecifier.ApplyModifierSet(damage, DamageSpecifier.PenetrateArmor(modifierSet, armorPenetration))`, then `DamageModifyEvent(damage, origin, armorPenetration, targetPart, tool)` |
| 247-248 | `ApplyUniversalAllModifiers` |
| **250-266** | **the write loop** — mutates `damageable.Damage.DamageDict` directly, builds `delta` |
| 268-269 | `if (delta.DamageDict.Count > 0) DamageChanged(uid.Value, damageable, delta, interruptsDoAfters, origin, canSever);` |

`DamageChanged` (156-169) recomputes `DamagePerGroup`/`TotalDamage`, `Dirty()`s, updates appearance, and raises `new DamageChangedEvent(component, damageDelta, interruptsDoAfters, origin, canSever ?? true)` (line 168).

`SetDamage` (143-147) is the "projection-shaped" API Wolfgate already has:

```csharp
// :143
public void SetDamage(EntityUid uid, DamageableComponent damageable, DamageSpecifier damage)
{
    damageable.Damage = damage;
    DamageChanged(uid, damageable);   // damageDelta == null
}
```

Note the `damageDelta == null` — this matters a lot (§4.4).

Other Shitmed blocks in this file:
- `SetAllDamage` (311-340) — after setting the body, lines 328-339 loop `_body.GetBodyChildren(uid)` and recurse into every part **if `HasComp<TargetingComponent>(uid)`**.
- `ChangeAllDamage` (348-373) — same pattern, lines 361-372.
- `OnRejuvenate` (411-417) — `SetAllDamage(uid, component, 0)` between `SetAllowRevives(true/false)`.

Event records at the bottom of the file:

```csharp
// :469-475
[ByRefEvent]
public record struct BeforeDamageChangedEvent(
    DamageSpecifier Damage,
    EntityUid? Origin = null,
    TargetBodyPart? TargetPart = null, // Shitmed Change
    bool Cancelled = false,
    DamageOriginFlag? OriginFlag = null); // Mono: OriginFlag

// :480-490
[ByRefEvent]
public record struct TryChangePartDamageEvent(
    DamageSpecifier Damage, EntityUid? Origin = null, TargetBodyPart? TargetPart = null,
    bool IgnoreResistances = false, bool CanSever = true, bool CanEvade = false,
    float PartMultiplier = 1.00f, bool Evaded = false, bool Cancelled = false);
```

`DamageChangedEvent` (522-581) is a class with `Damageable`, `DamageDelta`, `DamageIncreased`, `InterruptsDoAfters`, `Origin`, and the Shitmed `CanSever` (560, 567).

**There is no `DamageDealtEvent` and no `InjurableComponent` anywhere in Wolfgate** (grep over `Content.Shared` + `Content.Server` returns nothing).

### 1.2 `WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` (503 lines) — the whole Shitmed part-damage engine

Subscriptions (63-71):

```csharp
_queryTargeting = GetEntityQuery<TargetingComponent>();
SubscribeLocalEvent<BodyComponent,     TryChangePartDamageEvent>(OnTryChangePartDamage);   // :67
SubscribeLocalEvent<BodyComponent,     DamageModifyEvent>(OnBodyDamageModify);             // :68
SubscribeLocalEvent<BodyPartComponent, DamageModifyEvent>(OnPartDamageModify);             // :69
SubscribeLocalEvent<BodyPartComponent, DamageChangedEvent>(OnDamageChanged);               // :70
```

**Spread** — `OnTryChangePartDamage` (109-161). Part-selection chain, in order:
1. `args.TargetPart` if non-null (118-121)
2. else origin's `TargetingComponent.Target`, with a 33 %-ish spread bonus when it is `Torso` (122-131)
3. else, if there *is* an origin, `GetRandomBodyPart(ent, targetEnt)` weighted by `TargetingComponent.TargetOdds` (136-139)
4. else (no origin: barotrauma, explosions) `damage /= 2; targetPart = TargetBodyPart.All` (140-146)

then `TryChangePartDamage(...)` (184-212), which for each flagged part does

```csharp
// :205
var damageResult = Damageable.TryChangeDamage(part.FirstOrDefault().Id, damage * partMultiplier, ignoreResistances, canSever: canSever);
```

**Note the whole handler is inside `if (_queryTargeting.TryComp(ent, out var targetEnt))` (113)** — entities without `TargetingComponent` never get part damage at all.

**Sever** — `OnDamageChanged` (214-239):

```csharp
// :223-230
if (args.CanSever
    && partEnt.Comp.CanSever
    && partIdSlot is not null
    && delta != null
    && !HasComp<BodyPartReattachedComponent>(partEnt)
    && !partEnt.Comp.Enabled
    && damageable.TotalDamage >= partEnt.Comp.SeverIntegrity
    && _severingDamageTypes.Any(damageType => delta.DamageDict.TryGetValue(damageType, out var value) && value > 0))
    severed = true;

CheckBodyPart(partEnt, GetTargetBodyPart(partEnt), severed, damageable);   // :233
if (severed) DropPart(partEnt);                                            // :235-236
```

`_severingDamageTypes = { "Slash", "Piercing", "Blunt" }` (36).

**Disable / re-enable + targeting doll** — `CheckBodyPart` (286-331):

```csharp
// :298-302  KILL
if (partEnt.Comp.Enabled && integrity >= partEnt.Comp.IntegrityThresholds[TargetIntegrity.CriticallyWounded])
{ var ev = new BodyPartEnableChangedEvent(false); RaiseLocalEvent(partEnt, ref ev); }

// :305-309  LIVE
if (!partEnt.Comp.Enabled && integrity <= partEnt.Comp.IntegrityThresholds[partEnt.Comp.EnableIntegrity] && !severed)
{ var ev = new BodyPartEnableChangedEvent(true); RaiseLocalEvent(partEnt, ref ev); }
```
then (311-330) writes `targeting.BodyStatus[targetPart] = newIntegrity`, mirrors Torso→Groin, `Dirty`s the body, and (server only) raises `TargetIntegrityChangeEvent` for the doll UI.

**Regen** — `Update` (88-107) + `ProcessIntegrityTick` (73-86):

```csharp
// :96-106
while (query.MoveNext(out var ent, out var part))
{
    part.HealingTimer += frameTime;
    if (part.HealingTimer >= part.HealingTime) { part.HealingTimer = 0; _integrityJobQueue.EnqueueJob(new IntegrityJob(this, (ent, part), IntegrityJobTime)); }
}

// :80-85
if (entity.Comp is { Body: { } body }
    && damage > entity.Comp.MinIntegrity
    && damage <= entity.Comp.IntegrityThresholds[TargetIntegrity.HeavilyWounded]
    && _queryTargeting.HasComp(body)
    && !_mobState.IsDead(body))
    Damageable.TryChangeDamage(entity, GetHealingSpecifier(entity), canSever: false, targetPart: GetTargetBodyPart(entity));
```

`GetHealingSpecifier` (409-426) heals `-SelfHealingAmount` of Blunt/Slash/Piercing/Heat/Cold/Shock and `-SelfHealingAmount * 0.1` Caustic.

**Armour is applied twice today.** `OnPartDamageModify` (172-182) relays the *body's* inventory to the *part's* `DamageModifyEvent` (174-176), applies the `PartDamage` modifier set (178-179) and `GetPartDamageModifier(partEnt.Comp.PartType)` (181). `OnBodyDamageModify` (163-170) separately applies `GetPartDamageModifier(targetType)` to the body's own damage. And because `TryChangePartDamageEvent` is raised at line **218**, *before* the body's resistance block at **227**, the part receives **pre-body-armour** damage. This is a balance-relevant fact for §8.7.

`canEvade` is dead code in practice: grep over the whole tree shows **no call site passes `canEvade: true`** — the only references are the parameter/field declarations and `TryChangePartDamage`'s internal use (`SharedBodySystem.Targeting.cs:188, 202`).

### 1.3 `WG/Content.Shared/Body/Part/BodyPartComponent.cs` — the fields D2 refers to

| Field | Line | Value |
|---|---|---|
| `MinIntegrity` | 57 | `0` |
| `CanSever` | 63 | `true` |
| `Enabled` | 69 | `true` |
| `CanEnable` | 75 | `true` |
| `HealingTime` | 87 | `30` |
| `HealingTimer` | 92 | (runtime) |
| `SelfHealingAmount` | 98 | `5` |
| `SeverIntegrity` | 124 | `130  // Mono 90->130` |
| `EnableIntegrity` | 136 | `TargetIntegrity.ModeratelyWounded` |
| `IntegrityThresholds` | 139-147 | Critically 90 / Heavily 75 / Moderately 60 / Somewhat 40 / Lightly 20 / Healthy 10 |

`BodyPartComponent` has **none** of Onyx's `MaxDamage`, `AmputationThresholds`, `DismembermentFinishingDamage`, `AmputationConsequenceSeverity`, `DismembermentSeverity`, `FractureProfile`, `Parent` — confirming D8.

### 1.4 `WG/Content.Shared/_Shitmed/Targeting/`

- `TargetBodyPart.cs` — `[Flags] enum TargetBodyPart : ushort { Head=1, Torso=1<<1, Groin=1<<2, LeftArm..RightFoot, Hands, Arms, Legs, Feet, All }`. **No `[Serializable, NetSerializable]` attributes**, and the chest member is **`Torso`**, not `Chest`.
- `TargetingComponent.cs` — `Target` (default `Torso`), `TargetOdds : Dictionary<TargetBodyPart,float>` (flat), `BodyStatus : Dictionary<TargetBodyPart,TargetIntegrity>`, `SwapSound`.
- `Content.Server/_Shitmed/Targeting/TargetingSystem.cs` — handles `TargetChangeEvent` and death/revival `BodyStatus` updates.

### 1.5 Body API (all present, signatures match Onyx's call shapes)

| Call used by Onyx | Wolfgate location |
|---|---|
| `GetBodyChildren(EntityUid?, BodyComponent?, BodyPartComponent?)` → `IEnumerable<(EntityUid Id, BodyPartComponent Component)>` | `SharedBodySystem.Body.cs:256` |
| `GetBodyChildrenOfType(EntityUid, BodyPartType, BodyComponent?, BodyPartSymmetry?)` | `SharedBodySystem.Parts.cs:991` |
| `GetBodyPartChildren(...)` | `SharedBodySystem.Parts.cs:880` |
| `BodyHasChild(EntityUid bodyId, EntityUid partId, ...)` | `SharedBodySystem.Parts.cs:978` |
| `GetPartOrgans(EntityUid, BodyPartComponent?)` | `SharedBodySystem.Parts.cs:825` |
| `DetachPart` / `CanDetachPart` | `SharedBodySystem.Parts.cs:702, 718, 733, 751` |
| `DropPart(Entity<BodyPartComponent>)` | `SharedBodySystem.Parts.cs:201` — **`protected virtual`**, not public |

`[Dependency] protected DamageableSystem Damageable` on `SharedBodySystem` (`SharedBodySystem.cs:34`).

### 1.6 Wolfgate APIs Onyx routing needs that are **missing**

| Onyx symbol | Wolfgate status |
|---|---|
| `DamageDealtEvent` | **absent** |
| `InjurableComponent` | **absent** |
| `DamageableSystem.ChangeDamage(Entity<DamageableComponent?>, …)` | **absent** (`ChangeDamage` name is free — the only hit is a private `TemperatureSystem.ChangeDamage`, `Content.Server/Temperature/Systems/TemperatureSystem.cs:255`) |
| `TryChangeDamage(Entity<…>, …, out DamageSpecifier, …) → bool` | **absent** |
| `GetPositiveDamage`, `GetAllDamage`, `GetTotalDamage`, `ClearAllDamage`, `CanBeDamagedBy`, `HealEvenly`, `HealDistributed`, `SetDamage(Entity<…>, spec)` | **absent** |
| `DamageSpecifier.Clone()` | **absent** (`DamageSpecifier.cs` has `GetTotal():53`, `AnyPositive():68`, `Empty:83`, copy-ctor `DamageSpecifier(DamageSpecifier):94`, `TrimZeros():194`, operators `+ - * /` 360-440 — but no `Clone`) |
| `MobThresholdSystem.CheckVitalDamage` | **absent** (used by `TryApplyLethalDamage`, ONYX `WoundDamageRoutingSystem.cs:466`) |
| `HandLocation.FunctionalLeft/FunctionalRight` | **absent** — Wolfgate's enum is `{ Left, Middle, Right }` (`Content.Shared/Hands/Components/HandsComponent.cs:156-161`) |
| `CCVars.TargetingEnabled`, `TargetingUseAnatomicalOdds`, `TargetingDownedTargetsAreExact` | **absent** (needed by `TargetResolverSystem`) |
| `PassiveDamageComponent`, `HealOnBuckleComponent` (used in `FilterPartDamage`) | present: `Content.Shared/Damage/Systems/PassiveDamageSystem.cs`; `HealOnBuckle` — verify separately, out of scope here |

Present and usable: `FixedPoint2.FromHundredths(int)` (`FixedPoint2.cs:51`), `FixedPoint2.Min/Max` (210-220), `Hand.Location` (`HandsComponent.cs:113`), `SharedHandsSystem.GetActiveHand(Entity<HandsComponent?>) → Hand?` (`SharedHandsSystem.cs:177`), `StandingStateSystem.IsDown` (`StandingStateSystem.cs:61`), `CCVars` is `public sealed partial class CCVars : CVars` (`CCVars.cs:15`) so `_Onyx/CCVar` partials drop in.

---

## 2. Onyx routing — verified facts

### 2.1 The seam Onyx relies on

`ONYX/Content.Shared/Damage/Systems/DamageableSystem.API.cs:120-166`:

```csharp
public DamageSpecifier ChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage,
    bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null, bool ignoreGlobalModifiers = false)
{
    …
    var before = new BeforeDamageChangedEvent(damage, origin);
    RaiseLocalEvent(ent, ref before);
    if (before.Cancelled) return damageDone;
    if (!ignoreResistances) { …modifier set…; var ev = new DamageModifyEvent(damage, origin); RaiseLocalEvent(ent, ev); damage = ev.Damage; if (damage.Empty) return damageDone; }
    if (!ignoreGlobalModifiers) damage = ApplyUniversalAllModifiers(damage);

    var evt = new DamageDealtEvent(damage, origin, interruptsDoAfters);   // :162
    RaiseLocalEvent(ent, ref evt);                                        // :163
    return damage;
}
```

**`ChangeDamage` never touches `DamageableComponent.Damage`.** The write is a *subscriber*: `ONYX/…/DamageableSystem.Events.cs:25` `SubscribeLocalEvent<InjurableComponent, DamageDealtEvent>(OnDamageDealt)`, body at 218-244, which is what actually mutates `damageable.Damage.DamageDict` and then calls `OnEntityDamageChanged`.

`DamageDealtEvent` (`DamageableSystem.Events.cs:293`):
```csharp
[ByRefEvent]
public readonly record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters);
```
It is `readonly`, but `Damage` is a reference type, so handlers mutate `args.Damage.DamageDict` in place.

Onyx's `BeforeDamageChangedEvent` (`DamageableSystem.Events.cs:251`) is `record struct BeforeDamageChangedEvent(DamageSpecifier Damage, EntityUid? Origin = null, bool Cancelled = false)` — a **strict prefix** of Wolfgate's, so the vendored routing code compiles unchanged against Wolfgate's 5-member version.

### 2.2 `ONYX/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` (1015 lines)

Subscriptions (50-52):
```csharp
SubscribeLocalEvent<WoundHostComponent,  BeforeDamageChangedEvent>(OnBeforeDamageChanged);
SubscribeLocalEvent<WoundHostComponent,  DamageDealtEvent>(OnDamageDealt, before: [typeof(DamageableSystem)]);
SubscribeLocalEvent<WoundableComponent,  BeforeDamageChangedEvent>(OnBeforePartDamageChanged);
```

```csharp
// :55-62
private void OnBeforeDamageChanged(Entity<WoundHostComponent> ent, ref BeforeDamageChangedEvent args)
{
    if (!_net.IsServer || _routing.Contains(ent)) return;
    args.Cancelled = true;
    RouteThroughBodyModifiers(ent, args.Damage, args.Origin);
}

// :64-72
private void OnDamageDealt(Entity<WoundHostComponent> ent, ref DamageDealtEvent args)
{
    if (!_net.IsServer || !_routing.Contains(ent)) return;
    var damage = args.Damage.Clone();
    args.Damage.DamageDict.Clear();          // ← suppresses the body's own write
    RouteAppliedDamage(ent, damage, args.Origin, args.InterruptsDoAfters);
}
```

`RouteThroughBodyModifiers` (574-630) — re-entrancy guard `_routing`, the part-resolution chain, then:
```csharp
_applied.Remove(body);
_damage.ChangeDamage(body.Owner, damage, ignoreResistances, interruptsDoAfters, origin);   // :621
return _applied.Remove(body);
```
Part-resolution chain when no part was explicitly requested (587-618), in order:
`origin`'s `TargetingSnapshotComponent` → `PoweredLightComponent` origin ⇒ active-hand part → `_targetResolver.TryResolve(body, origin)` (i.e. the origin's `TargetingComponent`) → `DefibrillatorComponent` origin ⇒ chest → `ResolveDamagePart(body, null)` (weighted random over `WoundHostComponent.TargetWeights`).

`RouteAppliedDamage` (632-723):
- splits into `systemic` vs `localized` using `WoundHostComponent.LocalizedDamageTypes` (`Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic` — `WoundDamageComponents.cs:35-44`)
- systemic → `ApplySystemicDamage` (978-1009) → `SystemicDamageComponent.Damage` + pain on the chest
- negative localized → `ApplyLocalizedHealing` (782-833), distributed across parts proportionally to existing damage
- positive localized → `FilterPartDamage` (936-968) → `PartDamageModifyEvent` relayed to inventory (674-683) → `AccumulateAmputationOverflow` (725-780) → then

```csharp
// :704-719
if (_damage.TryChangeDamage(target, localized, out var appliedDamage,
        ignoreResistances: true, interruptsDoAfters: interruptsDoAfters,
        origin: origin, ignoreGlobalModifiers: true))
{
    _applied.Add(body);
    var applied = new PartDamageAppliedEvent(body, target, appliedDamage, !_skipWoundHealing.Contains(body), origin,
        _explosionDamage.Contains(body), overflow.Empty && _explosionAmputationCandidates.GetValueOrDefault(body) == target,
        _woundSeverityMultipliers.GetValueOrDefault(body, 1f));
    RaiseLocalEvent(target, ref applied);
    return;
}
_projection.RefreshBodyDamage(body);
```

**So: Onyx calls `DamageableSystem` on the *part entity*, with `ignoreResistances: true` and `ignoreGlobalModifiers: true`.** The part's `DamageableComponent` is the storage. Answering D2's question in §5 directly: *the part's `DamageableComponent` stays authoritative for part damage.*

`PartDamageAppliedEvent` has exactly one subscriber, `OrganDamageSystem` (`OrganDamageSystem.cs:29`), which fans out (32-36):
```csharp
_wounds.HandlePartDamageApplied(part, ref args);      // WoundSystem.cs:64 — creates/merges wounds
_fractures.HandlePartDamageApplied(part, ref args);
_amputation.HandlePartDamageApplied(part, ref args);
_bleeding.HandlePartDamageApplied(part, ref args);
```

### 2.3 `ONYX/Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` — body damage is a *projection*

```csharp
// :31
SubscribeLocalEvent<WoundableComponent, DamageDealtEvent>(OnPartDamageDealt, after: [typeof(DamageableSystem)]);

// :82-93
private void OnPartDamageDealt(Entity<WoundableComponent> part, ref DamageDealtEvent args)
{
    if (!_net.IsServer || !TryComp(part, out BodyPartComponent? component)) return;
    _pain.ApplyDamage(part, args.Damage, component);
    if (component.Body is { } body) RefreshBodyDamage(body);
    else RefreshDetachedDamage(GetDetachedRoot(part));
}

// :143-190  RefreshBodyDamage
var total = new DamageSpecifier();
… systemic.Damage …
foreach (var (part, _) in _body.GetBodyChildren(body))
    if (TryComp(part, out DamageableComponent? damageable)) { total += _damage.GetPositiveDamage((part, damageable)); … }
_damage.SetDamage(body, total);     // :183
```

`SetupPart` (206-221) `EnsureComp`s `WoundableComponent`, `DamageableComponent`, `PainComponent`, `BodyPartFunctionalityComponent`, `InjurableComponent{DamageContainer="Biological"}` on every part.

### 2.4 Onyx `BodyPartComponent` and `TargetBodyPart` differ structurally

`ONYX/Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:14-27` — `enum BodyPartType : ushort { Other=0, Torso=1, Head=2, Arm=3, Hand=4, Leg=5, Foot=6, Tail=7, Chest=8, Groin=9 }`. Onyx's *humanoids use `Chest`, not `Torso`*, and have a real `Groin` part. `ONYX/_Onyx/Targeting/TargetBodyPart.cs:9` names the member `Chest`. Wolfgate's Shitmed uses `Torso` and maps `TargetBodyPart.Groin → (BodyPartType.Torso, None)` with the comment *"TODO: Groin is not a part type yet"* (`SharedBodySystem.Targeting.cs:395`).

Consequence for the bridge: **`TargetResolverSystem.TryResolveAvailable` (ONYX `TargetResolverSystem.cs:49-63`) falls back to `BodyPartType.Chest`, which will never match a Wolfgate body.** Both `TryResolveAvailable`'s fallback (line 62) and `SharedTargetingSystem.TryConvert` (55-72) need `Chest → Torso` and `Groin → Torso` mapping. This is a `// WOLFGATE` edit inside the ported `_Onyx/Targeting` files, or a Wolfgate-flavoured `TargetBodyPart` enum. Track it with the Targeting port, but the damage bridge depends on it: **routing with an unmappable part silently drops all localized damage.**

---

## 3. (1) Damage flow *today*, humanoid in Wolfgate

Attacker swings a knife at Bob's left arm (Bob has `TargetingComponent`, attacker has `TargetingComponent.Target = LeftArm`).

```
SharedMeleeWeaponSystem.cs:583
  Damageable.TryChangeDamage(Bob, {Slash:15}, origin: attacker, armorPenetration: X, partMultiplier: M)
│
├─ DamageableSystem.cs:198  resolve DamageableComponent on Bob                      → ok
├─ :205                     damage.Empty?                                           → no
├─ :210-212                 raise BeforeDamageChangedEvent(dmg, attacker, null, …) on Bob
│     ├─ SharedGodmodeSystem.cs:30        GodmodeComponent      → Cancelled = true
│     ├─ SharedStasisSystem.cs:94         InsideStasisComponent → Cancelled = true
│     └─ SharedArmorPlateSystem.cs:49     ArmorPlateProtectedComponent
│            └─ absorbs into the plate, damages plate, adds stamina, REWRITES args.Damage in place,
│               or sets Cancelled on full absorption
├─ :214                     if (before.Cancelled) return null       ← nothing further happens
│
├─ :217-219                 raise TryChangePartDamageEvent(dmg, attacker, null, ignoreRes, canSever, canEvade, M) on Bob
│     └─ SharedBodySystem.Targeting.cs:109  OnTryChangePartDamage    [BodyComponent]
│            ├─ :113  Bob has TargetingComponent?                    → yes
│            ├─ :122-131  attacker has TargetingComponent → targetPart = LeftArm
│            └─ :184-212  TryChangePartDamage:
│                  for LeftArm:
│                    Damageable.TryChangeDamage(LeftArmEntity, {Slash:15}*M, ignoreRes, canSever: true)
│                    └─ RECURSION into DamageableSystem for the PART entity
│                         ├─ BeforeDamageChangedEvent on the part  (no mob subscribers)
│                         ├─ TryChangePartDamageEvent on the part  (no BodyComponent → no handler)
│                         ├─ DamageModifyEvent on the part
│                         │     └─ SharedBodySystem.Targeting.cs:172 OnPartDamageModify
│                         │          • relays Bob's INVENTORY armour onto the part (:174-176)
│                         │          • applies "PartDamage" modifier set (:178-179)
│                         │          • × GetPartDamageModifier(Arm) = 0.7 (:181)
│                         ├─ write loop :250-266  → LeftArm.DamageableComponent.Damage += Δ
│                         └─ DamageChanged(part, …, Δ, canSever) :269
│                               └─ DamageChangedEvent on the part
│                                     └─ SharedBodySystem.Targeting.cs:214 OnDamageChanged [BodyPartComponent]
│                                          • sever test :223-230 (needs !Enabled && total ≥ 130 && Slash/Pierce/Blunt>0)
│                                          • CheckBodyPart :286  → BodyPartEnableChangedEvent(false) at ≥ 90
│                                                                → BodyPartEnableChangedEvent(true)  at ≤ 60
│                                                                → targeting.BodyStatus[LeftArm] = integrity, Dirty, doll net-event
│                                          • DropPart(part) if severed :236
│
├─ :226-245                 body resistances: DamageModifierSet + PenetrateArmor, then DamageModifyEvent on Bob
│                             └─ SharedBodySystem.Targeting.cs:163 OnBodyDamageModify (no-op, targetPart == null here)
│                             └─ SharedArmorSystem, inventory relay, etc.
├─ :247-248                 ApplyUniversalAllModifiers
├─ :250-266                 write loop → Bob.DamageableComponent.Damage += Δ
└─ :269                     DamageChanged(Bob, …, Δ) → DamageChangedEvent on Bob
                              ├─ MobThresholdSystem.cs:424    OnDamaged → CheckThresholds on TotalDamage → crit/death
                              ├─ BloodstreamSystem.cs:210     OnDamageChanged → bleed rate + crit bleed roll
                              ├─ HemophiliaSystem.cs:36       extra bleed
                              ├─ SlowOnDamageSystem.cs:58     RefreshMovementSpeedModifiers
                              ├─ SharedDoAfterSystem.cs:33    cancel do-afters
                              ├─ SleepingSystem.cs:214        wake up
                              ├─ DamageForceSaySystem.cs:99   forced speech  (after MobThresholdSystem)
                              ├─ KillTrackingSystem.cs:23     (before MobThresholdSystem)
                              ├─ EmoteOnDamageSystem.cs:25    scream
                              ├─ NPCRetaliationSystem.cs:27   NPC aggro
                              └─ FlammableSystem.cs:380       ignite on heat
```

Every 30 s per part, `SharedBodySystem.Update` (:96) enqueues `ProcessIntegrityTick` (:73), which heals the part by 5 of each brute/burn type (and 0.5 Caustic) while `MinIntegrity < damage ≤ 75`.

---

## 4. (2) Damage flow for a `WoundHost` entity *after* the bridge

Same attack, Bob now has `WoundHostComponent` (+ the `_WF` marker, §5.1) and every part has `WoundableComponent`.

```
SharedMeleeWeaponSystem.cs:583
  Damageable.TryChangeDamage(Bob, {Slash:15}, origin: attacker, partMultiplier: M, targetPart: null)
│
├─ :210-212  BeforeDamageChangedEvent #1 on Bob
│     ├─ SharedGodmodeSystem / SharedStasisSystem → may set Cancelled
│     ├─ WoundDamageRoutingSystem.OnBeforeDamageChanged   [WoundHostComponent]   ← NEW
│     │     • server only; _routing does NOT contain Bob
│     │     • // WOLFGATE: if (args.Cancelled) return;                (godmode/stasis short-circuit)
│     │     • // WOLFGATE: if (args.TargetPart is {} tp && IsSelectable(tp)
│     │     •                && _targetResolver.TryResolveAvailable(Bob, Map(tp), out var p))
│     │     •                    _requestedParts[Bob] = p;
│     │     • args.Cancelled = true
│     │     • RouteThroughBodyModifiers(Bob, args.Damage, attacker)     ← re-entrant, see below
│     │     • // WOLFGATE: _requestedParts.Remove(Bob)
│     └─ SharedArmorPlateSystem.cs:49 — SKIPPED, because it early-returns on args.Cancelled (:51)
│            (this is why routing must be ordered `before: [typeof(SharedArmorPlateSystem)]`)
│
└─ :214  before.Cancelled == true → return null
         ⇒ TryChangePartDamageEvent (:218) is NEVER RAISED ⇒ Shitmed spread is bypassed for free
         ⇒ the body write loop (:250) is never reached
```

The re-entrant pass created by `RouteThroughBodyModifiers` (ONYX :574):

```
_routing.Add(Bob)
_damage.ChangeDamage(Bob, dmg, ignoreResistances=false, interruptsDoAfters, attacker)   (ONYX :621)
  ⇒ compat shim in _WF/Wolfmed/Compat  →  DamageableSystem.TryChangeDamage(Bob, dmg, …)
│
├─ :210-212  BeforeDamageChangedEvent #2 on Bob
│     ├─ Godmode/Stasis → still cancel if applicable (routing then applies nothing — correct)
│     ├─ WoundDamageRoutingSystem: _routing.Contains(Bob) → returns immediately, no cancel
│     └─ SharedArmorPlateSystem: RUNS HERE — plate absorbs, rewrites args.Damage   ✔ exactly once
├─ :217-219  TryChangePartDamageEvent on Bob
│     └─ SharedBodySystem.OnTryChangePartDamage  → **GUARD A: returns immediately (WoundHost)**
├─ :226-245  body resistances + DamageModifyEvent on Bob (SharedArmorSystem, inventory relay)
├─ :247-248  ApplyUniversalAllModifiers
├─ NEW // WOLFGATE seam, inserted between :248 and :250:
│     var dealt = new DamageDealtEvent(damage, origin, interruptsDoAfters);
│     RaiseLocalEvent(uid.Value, ref dealt);
│     if (damage.Empty) return damage;                       ← routing cleared the dict ⇒ body write skipped
│     └─ WoundDamageRoutingSystem.OnDamageDealt  [WoundHostComponent]
│           • damage = args.Damage.Clone(); args.Damage.DamageDict.Clear();
│           • RouteAppliedDamage(Bob, damage, attacker, interruptsDoAfters):
│               ├─ split systemic / localized via WoundHostComponent.LocalizedDamageTypes
│               ├─ systemic  → SystemicDamageComponent.Damage (+ chest pain)
│               ├─ negative localized → ApplyLocalizedHealing across parts
│               └─ positive localized, part = _requestedParts[Bob] ?? weighted-random:
│                     ├─ FilterPartDamage (BodyPartProfilePrototype.AcceptedDamageTypes, recovery multipliers)
│                     ├─ PartDamageModifyEvent relayed to Bob's inventory (Onyx locational armour)
│                     ├─ AccumulateAmputationOverflow → PartDamageOverflowedEvent → AmputationSystem
│                     └─ _damage.TryChangeDamage(LeftArm, localized, out applied,
│                            ignoreResistances: true, ignoreGlobalModifiers: true)     ← compat overload
│                           └─ RECURSION into DamageableSystem for the PART
│                                ├─ BeforeDamageChangedEvent on the part
│                                │     └─ WoundDamageRoutingSystem.OnBeforePartDamageChanged [WoundableComponent]
│                                │          filters types not in the part profile
│                                ├─ TryChangePartDamageEvent on the part → no BodyComponent → no handler
│                                ├─ resistances SKIPPED (ignoreResistances: true)
│                                │     ⇒ OnPartDamageModify (:172) does NOT run — no double armour   ✔
│                                ├─ write loop → LeftArm.DamageableComponent.Damage += Δ
│                                └─ DamageChanged(part, …, Δ, canSever)
│                                      └─ DamageChangedEvent on the part
│                                            └─ SharedBodySystem.OnDamageChanged (:214)
│                                                 • sever branch  → **GUARD B: skipped (WoundHost)**
│                                                 • CheckBodyPart → KEPT (enable/disable + doll)
│                                            └─ _WF bridge / vendored projection hook:
│                                                 PainSystem.ApplyDamage(part, Δ)
│                                                 WoundDamageProjectionSystem.RefreshBodyDamage(Bob)
│                     └─ PartDamageAppliedEvent(Bob, LeftArm, applied, …)
│                           └─ OrganDamageSystem:32-36 fan-out
│                                 WoundSystem.HandlePartDamageApplied   → creates/merges SlashWound
│                                 WoundFractureSystem                   → fracture roll
│                                 AmputationSystem                      → dismemberment progress
│                                 WoundBleedingSystem                   → bleed rate on the wound
│                                 + organ-damage roll
└─ _applied.Remove(Bob) → true

RefreshBodyDamage(Bob)  (ONYX :143)
  total = SystemicDamageComponent.Damage + Σ GetPositiveDamage(each part)
  _damage.SetDamage(Bob, total)          ← compat overload, §5.6
    └─ DamageChanged(Bob, damageable, delta, interruptsDoAfters: false)
          └─ DamageChangedEvent on Bob
                ├─ MobThresholdSystem   → crit/death from the projected total   ✔
                ├─ BloodstreamSystem    → **GUARD E: skipped (WoundHost)** — Onyx owns bleeding
                ├─ HemophiliaSystem     → **GUARD E: skipped**
                └─ SlowOnDamage / DoAfter / Sleeping / ForceSay / KillTracking / Emote / NPC aggro / Flammable  ✔
```

Regen: `ProcessIntegrityTick` (:73) is **GUARD C**-skipped; `WoundHealingSystem` + `BodyPartProfilePrototype` passive/bed recovery take over.

Non-`WoundHost` mobs: `WoundDamageRoutingSystem.OnBeforeDamageChanged` never fires (directed on `WoundHostComponent`), the new `DamageDealtEvent` has no subscriber, guards A/B/C/E all test false, so the §3 flow is byte-for-byte unchanged.

---

## 5. (3) The exact minimal `// WOLFGATE` guards

### 5.0 The marker component (recommended over `WoundHostComponent` directly)

Upstream Wolfgate files should not gain `using Content.Shared._Onyx.Wounds;` — that couples the upstream diff to the vendored tree and makes every Onyx re-sync a merge risk. Use a `_WF` marker kept in lock-step by one glue system.

`WG/Content.Shared/_WF/Wolfmed/Components/WolfmedHostComponent.cs` (new):
```csharp
namespace Content.Shared._WF.Wolfmed;

/// <summary>Marks an entity whose part damage is owned by Wolfmed instead of Shitmed.</summary>
[RegisterComponent]
public sealed partial class WolfmedHostComponent : Component;
```

`WG/Content.Shared/_WF/Wolfmed/WolfmedBridgeSystem.cs` (new):
```csharp
public override void Initialize()
{
    SubscribeLocalEvent<WoundHostComponent, ComponentStartup>(OnHostStartup);
    SubscribeLocalEvent<WoundHostComponent, ComponentShutdown>(OnHostShutdown);
}
private void OnHostStartup(Entity<WoundHostComponent> ent, ref ComponentStartup args) => EnsureComp<WolfmedHostComponent>(ent);
private void OnHostShutdown(Entity<WoundHostComponent> ent, ref ComponentShutdown args) => RemComp<WolfmedHostComponent>(ent);
```
`WoundHostComponent` is `[NetworkedComponent]` (ONYX `WoundDamageComponents.cs:14`) so `ComponentStartup` fires on both sides and the non-networked marker stays consistent. Nothing else in Onyx subscribes `WoundHostComponent + ComponentStartup/Shutdown` (checked: the only `WoundHostComponent` subscriptions are `MapInitEvent`, `RejuvenateEvent`, `BeforeDamageChangedEvent`, `DamageDealtEvent`, `RefreshMovementSpeedModifiersEvent`, `GetManipulationDurationMultiplierEvent`, `SleepStateChangedEvent`, `ResolveHealingPartEvent`), so there is no duplicate-directed-subscription crash.

**Guards A–C must NOT be `_net.IsServer`-gated.** Onyx's routing is server-only (`!_net.IsServer` at `WoundDamageRoutingSystem.cs:57, 66`), but Shitmed's spread/sever/regen run on the client too. If the guards were server-only, the client would keep predicting Shitmed part spreading while the server routed through Onyx — guaranteed permanent mispredict. Gate on component presence only. See §8.2.

### 5.1 GUARD A — bypass Shitmed's spread

**File** `WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`
**Line** insert at 111, immediately after the opening brace of `OnTryChangePartDamage` (declared at 109):

```csharp
    private void OnTryChangePartDamage(Entity<BodyComponent> ent, ref TryChangePartDamageEvent args)
    {
        // WOLFGATE: Wolfmed routes part damage itself for wound hosts.
        if (_queryWolfmed.HasComp(ent))
            return;

        // If our target has a TargetingComponent, that means they will take limb damage
```

with a cached query added beside `_queryTargeting` (declared at 63, assigned at 66):

```csharp
    private EntityQuery<TargetingComponent> _queryTargeting;
    private EntityQuery<WolfmedHostComponent> _queryWolfmed; // WOLFGATE
    private void InitializeIntegrityQueue()
    {
        _queryTargeting = GetEntityQuery<TargetingComponent>();
        _queryWolfmed = GetEntityQuery<WolfmedHostComponent>(); // WOLFGATE
```
plus `using Content.Shared._WF.Wolfmed; // WOLFGATE` at the top.

*Strictly speaking this guard is redundant on the first pass* (Onyx cancels at `DamageableSystem.cs:214`, before `TryChangePartDamageEvent` at 218) — but it is **required** on the re-entrant pass, where `_routing.Contains(body)` makes the routing handler a no-op and the event does get raised. Without it, every wound-host hit lands twice: once through Shitmed's spread, once through Onyx's routing.

### 5.2 GUARD B — bypass Shitmed's sever (keep enable/disable + doll)

**File** same, **lines 223-230**, one added condition:

```csharp
        if (args.CanSever
            && partEnt.Comp.CanSever
            && !_queryWolfmed.HasComp(partEnt.Comp.Body) // WOLFGATE: Wolfmed owns dismemberment.
            && partIdSlot is not null
            && delta != null
            && !HasComp<BodyPartReattachedComponent>(partEnt)
            && !partEnt.Comp.Enabled
            && damageable.TotalDamage >= partEnt.Comp.SeverIntegrity
            && _severingDamageTypes.Any(damageType => delta.DamageDict.TryGetValue(damageType, out var value) && value > 0))
            severed = true;
```

(`EntityQuery<T>.HasComp` accepts `EntityUid?` — `partEnt.Comp.Body` is `EntityUid?`, `BodyPartComponent.cs:27`. If the RT 277 overload set does not, write `partEnt.Comp.Body is { } b && _queryWolfmed.HasComp(b)`.)

`CheckBodyPart` on line 233 is deliberately **left running** — see §6.

### 5.3 GUARD C — bypass Shitmed's part regen

**File** same. Two edits.

`ProcessIntegrityTick`, **lines 80-85**:
```csharp
        if (entity.Comp is { Body: { } body }
            && !_queryWolfmed.HasComp(body) // WOLFGATE: Wolfmed's WoundHealingSystem owns part recovery.
            && damage > entity.Comp.MinIntegrity
            && damage <= entity.Comp.IntegrityThresholds[TargetIntegrity.HeavilyWounded]
            && _queryTargeting.HasComp(body)
            && !_mobState.IsDead(body))
            Damageable.TryChangeDamage(entity, GetHealingSpecifier(entity), canSever: false, targetPart: GetTargetBodyPart(entity));
```

`Update`, **line 101** (skip the enqueue so the job queue isn't burned on wound hosts):
```csharp
            if (part.HealingTimer >= part.HealingTime)
            {
                part.HealingTimer = 0;
                if (!_queryWolfmed.HasComp(part.Body)) // WOLFGATE
                    _integrityJobQueue.EnqueueJob(new IntegrityJob(this, (ent, part), IntegrityJobTime));
            }
```

### 5.4 GUARD D — create the `DamageDealtEvent` seam (the one non-optional edit)

**File** `WG/Content.Shared/Damage/Systems/DamageableSystem.cs`
**Line** insert between 248 and 250:

```csharp
            if (!ignoreGlobalModifiers)
                damage = ApplyUniversalAllModifiers(damage);

            // WOLFGATE: Wolfmed routing seam. A handler that consumes the damage (clearing the dict)
            // keeps it off this entity's own DamageableComponent and applies it to body parts instead.
            if (_wolfmedSeamQuery.HasComp(uid.Value))
            {
                var dealt = new DamageDealtEvent(damage, origin, interruptsDoAfters);
                RaiseLocalEvent(uid.Value, ref dealt);
                if (damage.Empty)
                    return damage;
            }

            var delta = new DamageSpecifier();
```

with, next to the other queries (37-38 / 59-60):
```csharp
        private EntityQuery<WolfmedHostComponent> _wolfmedSeamQuery; // WOLFGATE
        …
            _wolfmedSeamQuery = GetEntityQuery<WolfmedHostComponent>(); // WOLFGATE
```
and `using Content.Shared._WF.Wolfmed; // WOLFGATE` at the top.

`DamageDealtEvent` itself is **new `_WF` code**, not an upstream edit:
`WG/Content.Shared/_WF/Wolfmed/Compat/DamageDealtEvent.cs`
```csharp
namespace Content.Shared.Damage;

/// <summary>Raised after modifiers, before damage is stored. Clearing Damage suppresses the store.</summary>
[ByRefEvent]
public readonly record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters);
```
It sits in namespace `Content.Shared.Damage` so `DamageableSystem.cs` needs no extra `using` beyond the marker one. The vendored Onyx files' `using Content.Shared.Damage.Systems;` is satisfied by the compat file in §7.2.

**Why the `HasComp` gate:** without it this event is raised on every damage application in the game (projectiles, structures, fires) for no benefit. With it, it only fires on wound hosts — parts are handled by `DamageChangedEvent` (§5.5), which already exists.

> Alternative considered and rejected: have routing not re-enter `DamageableSystem` at all and replicate the armour pipeline in `_WF`. That duplicates `PenetrateArmor`, `DamageModifyEvent`, `SharedArmorSystem`, `SharedArmorPlateSystem` and the Universal modifiers — far more code, and it silently diverges on every upstream armour change.

### 5.5 GUARD D′ — re-point Onyx's part projection hook

`WoundDamageProjectionSystem` subscribes `WoundableComponent, DamageDealtEvent, after: [typeof(DamageableSystem)]`. In Wolfgate, (a) the seam only fires for wound *hosts*, not parts, and (b) "after DamageableSystem" is meaningless because the write is inline, not a subscriber. A subscription cannot be shimmed away, so this needs a vendored edit.

**File** `WG/Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` (vendored), **lines 31 and 82-87**:

```csharp
        // WOLFGATE: Wolfgate's DamageableSystem writes inline and raises DamageChangedEvent after the write.
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnPartDamageDealt);
…
    // WOLFGATE: DamageChangedEvent instead of DamageDealtEvent; the delta is the applied damage.
    private void OnPartDamageDealt(Entity<WoundableComponent> part, DamageChangedEvent args)
    {
        if (!_net.IsServer || args.DamageDelta is not { } delta || !TryComp(part, out BodyPartComponent? component))
            return;

        _pain.ApplyDamage(part, delta, component);
```
(the remainder of the method, lines 89-92, is unchanged)

`SharedBodySystem` already subscribes `BodyPartComponent, DamageChangedEvent` (`SharedBodySystem.Targeting.cs:70`) — a different component, so no duplicate-subscription crash. **`WoundableComponent, DamageChangedEvent` must be claimed by exactly one system; do not also subscribe it from `_WF`.**

### 5.6 GUARD F — keep `DamageChangedEvent` deltas alive (compat, no upstream edit)

Onyx's projection ends with `_damage.SetDamage(body, total)` (ONYX `WoundDamageProjectionSystem.cs:183`). Wolfgate's existing `SetDamage` (`DamageableSystem.cs:143`) raises `DamageChangedEvent` with `DamageDelta == null`, which would **silently kill** `BloodstreamSystem`, `HemophiliaSystem`, `SleepingSystem`, `EmoteOnDamageSystem`, `DamageForceSaySystem`, `KillTrackingSystem`, `NPCRetaliationSystem` and `FlammableSystem` for every wound host (all of them early-return on `DamageDelta == null` / `!DamageIncreased` — verified individually, §9).

Fix it in the compat overload so the vendored line compiles *and* behaves:

`WG/Content.Shared/_WF/Wolfmed/Compat/DamageableSystem.Wolfmed.cs`
```csharp
namespace Content.Shared.Damage;

public sealed partial class DamageableSystem
{
    /// <summary>New-API SetDamage; unlike the legacy one this reports a real delta.</summary>
    public void SetDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage)
    {
        if (!_damageableQuery.Resolve(ent, ref ent.Comp, false))
            return;

        var delta = damage - ent.Comp.Damage;
        delta.TrimZeros();
        ent.Comp.Damage = new DamageSpecifier(damage);
        DamageChanged(ent.Owner, ent.Comp, delta.Empty ? null : delta, interruptsDoAfters: false);
    }
}
```
`DamageableSystem` is `public sealed partial` (`DamageableSystem.cs:25`), so this is a partial-class extension with full access to `_damageableQuery` — no upstream edit, no reflection. Arity (2 args) makes it unambiguous against the legacy 3-arg `SetDamage(EntityUid, DamageableComponent, DamageSpecifier)`.

### 5.7 GUARD E — stop double bleeding

With G-F restoring real deltas, Wolfgate's blood systems would now fire on projected body damage *and* Onyx's `WoundBleedingSystem` would bleed per wound. Two one-line guards:

**File** `WG/Content.Server/Body/Systems/BloodstreamSystem.cs`, **line 212** (inside `OnDamageChanged`, declared at 210):
```csharp
    private void OnDamageChanged(Entity<BloodstreamComponent> ent, ref DamageChangedEvent args)
    {
        if (HasComp<WolfmedHostComponent>(ent)) // WOLFGATE: Wolfmed wounds own bleeding.
            return;

        if (args.DamageDelta is null || !args.DamageIncreased)
```

**File** `WG/Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs`, **line 38** (inside `OnDamageChanged`, declared at 36): same one-line guard. (Re-express hemophilia as a wound-bleeding multiplier in `_WF/Wolfmed` later.)

### 5.8 Handing Shitmed's resolved `targetPart` to Onyx

The only point where Wolfgate carries a part request that Onyx cannot see is `BeforeDamageChangedEvent.TargetPart` (`DamageableSystem.cs:473`, Shitmed's extra field). Two `// WOLFGATE` lines in the vendored routing entry point:

**File** `WG/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` (vendored), **lines 55-62**:

```csharp
    private void OnBeforeDamageChanged(Entity<WoundHostComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_net.IsServer || _routing.Contains(ent))
            return;

        // WOLFGATE: godmode/stasis already refused this hit; do not resurrect it through routing.
        if (args.Cancelled)
            return;

        args.Cancelled = true;

        // WOLFGATE: Shitmed resolves the struck limb into BeforeDamageChangedEvent.TargetPart. Hand it to routing.
        var requested = false;
        if (args.TargetPart is { } target
            && SharedTargetingSystem.IsSelectable(target)
            && _targetResolver.TryResolveAvailable(ent, target, out var part)
            && IsAttachedWoundablePart(ent, part))
        {
            _requestedParts[ent] = part;
            requested = true;
        }

        try
        {
            RouteThroughBodyModifiers(ent, args.Damage, args.Origin);
        }
        finally
        {
            if (requested)
                _requestedParts.Remove(ent);
        }
    }
```

and the subscription, **line 50**:
```csharp
        // WOLFGATE: must cancel before Mono's armour plates get a chance to absorb; they re-run on the routed pass.
        SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged,
            before: [typeof(SharedArmorPlateSystem)]);
```

Key correctness points:

- **`IsSelectable` filter is mandatory.** `SharedTargetingSystem.IsSelectable` (ONYX `SharedTargetingSystem.cs:52`) requires a single set bit. Wolfgate passes composite masks in real code — `TargetBodyPart.All` from `Content.Server/EntityEffects/Effects/HealthChange.cs:173` and `Content.Server/_Mono/EntityEffects/Effects/HealthScaleEffect.cs:117`, and `Torso | <random>` from `SharedBodySystem.Targeting.cs:129`. For a composite mask, leaving `_requestedParts` unset is the *correct* behaviour: `RouteAppliedDamage` then spreads healing across all parts via `ApplyLocalizedHealing` (ONYX :782) and picks a weighted-random part for damage. Setting it would throw or mis-target.
- **`Torso` → `Chest` mapping is required first.** Until §2.4's enum mapping is done, `TryResolveAvailable` returns false for everything and this block is dead. Sequence the Targeting port before the bridge.
- **Ordering vs godmode/stasis is impossible from shared code.** `SharedGodmodeSystem` (`SharedGodmodeSystem.cs:9`) and `SharedStasisSystem` (`SharedStasisSystem.cs:25`) are `abstract`, with concrete `Content.Server/Damage/Systems/GodmodeSystem.cs:7` and `Content.Server/_Goobstation/ChronoLegionnaire/Systems/StasisSystem.cs:5`. RT keys ordering on `GetType()` (`RobustToolbox/Robust.Shared/GameObjects/EntitySystem.Subscriptions.cs:118`), i.e. the *concrete* type, which shared code cannot reference. `before:`/`after:` on the abstract type would compile and silently do nothing. The `if (args.Cancelled) return;` guard is the order-independent fix. `SharedArmorPlateSystem` (`SharedArmorPlateSystem.cs:22`) is `sealed` and shared, so `before: [typeof(SharedArmorPlateSystem)]` does work.

### 5.9 Guard summary

| # | File | Line | Purpose | Upstream edit? |
|---|---|---|---|---|
| A | `_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | 111 (+63/66) | skip part spread | yes, 3 lines |
| B | same | 223-230 | skip sever | yes, 1 line |
| C | same | 80-85, 101 | skip part regen | yes, 2 lines |
| D | `Damage/Systems/DamageableSystem.cs` | 248/250 (+37/59) | `DamageDealtEvent` seam | yes, ~10 lines |
| D′ | `_Onyx/Wounds/WoundDamageProjectionSystem.cs` | 31, 82-87 | part hook onto `DamageChangedEvent` | vendored, 5 lines |
| E | `Content.Server/Body/Systems/BloodstreamSystem.cs` | 212 | no double bleed | yes, 2 lines |
| E | `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` | 38 | no double bleed | yes, 2 lines |
| F | `_WF/Wolfmed/Compat/DamageableSystem.Wolfmed.cs` | new | delta-preserving `SetDamage` | no |
| — | `_Onyx/Wounds/WoundDamageRoutingSystem.cs` | 50, 55-62 | targetPart handoff + ordering + cancel guard | vendored, ~15 lines |

Total upstream footprint: **~20 lines across 4 files**, all one- or two-line hooks except the `DamageDealtEvent` seam. That is within D2's "minimal guards" intent.

---

## 6. (4) How Onyx part damage maps to Wolfgate's part `DamageableComponent` — and Impaired/Disabled

### 6.1 It maps 1:1; the part's `DamageableComponent` stays authoritative

Onyx does **not** invent a parallel store. `RouteAppliedDamage` calls `_damage.TryChangeDamage(target, …)` on the **part entity** (ONYX `WoundDamageRoutingSystem.cs:704`), and `ApplyPartChange` does the same for healing (`:921`). `WoundDamageProjectionSystem.SetupPart` (`:206-221`) even `EnsureComp<DamageableComponent>(part)`. So on Wolfgate:

- `LeftArm.DamageableComponent.Damage` / `.TotalDamage` accumulate exactly as today, just fed from Onyx instead of from `TryChangePartDamage`.
- `SharedBodySystem.OnDamageChanged` (`SharedBodySystem.Targeting.cs:214`) still fires for the part, with a real `DamageDelta`.
- Therefore **`CheckBodyPart` (:286) keeps working unchanged**: disable at `IntegrityThresholds[CriticallyWounded] = 90`, re-enable at `IntegrityThresholds[ModeratelyWounded] = 60`, `targeting.BodyStatus[...]` doll update, `TargetIntegrityChangeEvent`.

The only Shitmed reaction that must be suppressed is the sever branch (GUARD B), because Onyx's `AmputationSystem` owns dismemberment via `PartDamageOverflowedEvent`.

### 6.2 Recommendation: **keep Shitmed's `Enabled` thresholds in phase 1; do not adopt Onyx's Impaired/Disabled yet**

Reasons, all verified:

1. **Onyx ships part functionality OFF.** `ONYX/Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` line 7-8: `WoundsBodyPartFunctionalityEnabled = CVarDef.Create("wounds.body_part_functionality_enabled", false, …)`. `BodyPartFunctionalitySystem.GetState` (`:23-24`) returns `Functional` unconditionally when it is false. Adopting it as the *replacement* for Shitmed's `Enabled` would ship Wolfgate with **no limb disabling at all** on Onyx defaults (D4 says use Onyx defaults).
2. **Onyx's states drive different things.** The only consumer of `BodyPartFunctionalityChangedEvent` is `FractureEffectsSystem` (`FractureEffectsSystem.cs:36, 74`), which converts `Disabled/Impaired` into movement-speed and manipulation-duration *multipliers* (`:178-185`). It never raises anything equivalent to `BodyPartEnableChangedEvent`.
3. **Shitmed's `Enabled` has real mechanical consumers Onyx cannot replace.** `SharedBodySystem.OnPartEnableChanged` (`SharedBodySystem.Parts.cs:76-95`) calls `EnablePart`/`DisablePart` → `AddLeg` for legs, `BodyPartEnabledEvent` for arms/hands, `BodyPartComponentsModifyEvent`, `DropSlotContents`. Other producers exist (`SharedSurgerySystem.Steps.cs:473`, `CyberneticsSystem.cs:32, 50`, `SharedBodySystem.Parts.cs:191, 209`). Dropping the damage-driven producer would leave legs/hands permanently functional no matter how ruined.
4. **The targeting doll reads `targeting.BodyStatus`**, which is only written by `CheckBodyPart` (`:311-325`). Removing it blanks the Shitmed limb-targeting UI.

**Phase-1 verdict:** the two systems are *orthogonal* — Shitmed's `Enabled` is a damage threshold on the part's `DamageableComponent`; Onyx's `BodyPartFunctionalityState` is a wound-derived modifier state. Run both. Because `SeverIntegrity = 130` is above `CriticallyWounded = 90`, the part will still visibly "die" at 90 and then keep accumulating damage toward Onyx's `BodyPartComponent.MaxDamage` overflow instead of Shitmed's 130 sever.

**Phase 3** (per the handoff roadmap) is the right time to revisit: set `wounds.body_part_functionality_enabled = true`, and have a `_WF` adapter translate `BodyPartFunctionalityChangedEvent` into `BodyPartEnableChangedEvent` (`Disabled → false`, `Functional/Impaired → true`), then raise `IntegrityThresholds[CriticallyWounded]` out of reach so the two do not fight. Do **not** do this in phase 1.

### 6.3 Visuals

Shitmed's part visuals go through `BodyPartAppearanceComponent` / `SharedBodySystem.PartAppearance.cs`, keyed on part `Enabled`/severed state, not on `Damage` — unaffected. Onyx ships its own `PartDamageVisualsComponent` (`WoundDamageComponents.cs:95-100`) filled by `RefreshBodyDamage` (`:165-181`) and 156 textures under `Resources/Textures/_Onyx/Wounds`; that is a separate (phase 4) client feature and does not block the bridge. Wolfgate's body-level `DamageVisualizerKeys.DamageUpdateGroups` (`DamageableSystem.cs:163-167`) keeps working because the projection goes through `DamageChanged`.

---

## 7. (5) The C# overload trap and the compat layer

### 7.1 The exact rule, and what Wolfgate's old overload does with Onyx's calls

Onyx's calls, both in `WoundDamageRoutingSystem` (`:704-710`, `:921-927`):
```csharp
_damage.TryChangeDamage(target, localized, out var appliedDamage,
    ignoreResistances: true, interruptsDoAfters: interruptsDoAfters, origin: origin, ignoreGlobalModifiers: true)
```
Wolfgate's only overload today (`DamageableSystem.cs:190`):
```csharp
public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
    bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, …)
```

**What happens if you paste Onyx's call unchanged:** the third positional argument is `out var appliedDamage`, which cannot bind to `bool ignoreResistances` (CS1503/CS1615), and `ignoreResistances:` is then also supplied by name (CS1744, "named argument specifies a parameter for which a positional argument has already been given"). It is a **hard compile error, not a silent misbind.** Good: nothing silently mis-routes.

The same is true of the other shapes:
- `_damage.ChangeDamage(body.Owner, damage, ignoreResistances, interruptsDoAfters, origin)` (`:621`) — `ChangeDamage` does not exist ⇒ CS1061.
- `if (_damage.TryChangeDamage(…))` — `DamageSpecifier?` is not convertible to `bool` ⇒ CS0029.
- `_damage.GetPositiveDamage((part, damageable))` — CS1061.

**The real trap is what you might add to fix it.** `Robust.Shared/GameObjects/Entity.cs:39` declares
```csharp
public static implicit operator Entity<T?>(EntityUid owner)
```
and `Entity.cs:44` `public static implicit operator EntityUid(Entity<T> ent)`. So for a bare `EntityUid` argument and two candidate overloads — legacy `(EntityUid? uid, …)` vs new `(Entity<DamageableComponent?> ent, …)` — the C# better-conversion-target rule (§12.6.4.7: *T₁ is better than T₂ if an implicit conversion T₁→T₂ exists and T₂→T₁ does not*) applies:

- `Entity<DamageableComponent?>` → `EntityUid?` : **exists** (user-defined `→ EntityUid`, then lifted).
- `EntityUid?` → `Entity<DamageableComponent?>` : **does not** (the operator takes a non-nullable `EntityUid`).

⇒ `Entity<DamageableComponent?>` is the better target ⇒ **the new overload wins for every existing `TryChangeDamage(someEntityUid, …)` call.** There are **106** `TryChangeDamage(` call sites in Wolfgate (`Content.Shared` + `Content.Server` + `Content.Client`, excluding `bin`/`obj`). Since the new overload returns `bool` instead of `DamageSpecifier?`, most would break loudly (`.GetTotal()` on a bool), but the dangerous class is `if (result != null)` — `bool != null` is **warning CS0472, not an error**, and would become permanently true.

### 7.2 Safe adapter surface

Put it in `WG/Content.Shared/_WF/Wolfmed/Compat/DamageableSystem.Wolfmed.cs` as `namespace Content.Shared.Damage; public sealed partial class DamageableSystem { … }` (the class is already `public sealed partial`, `DamageableSystem.cs:25`, so this gets private-member access without reflection and without an upstream edit).

**Safe to add — names not already used on `DamageableSystem`:**

```csharp
public DamageSpecifier ChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage,
    bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null,
    bool ignoreGlobalModifiers = false)
    => TryChangeDamage(ent.Owner, damage, ignoreResistances, interruptsDoAfters, ent.Comp, origin,
                       ignoreGlobalModifiers, canSever: false) ?? new DamageSpecifier();

public DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent);
public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent);          // returns new DamageSpecifier(comp.Damage)
public FixedPoint2    GetTotalDamage(Entity<DamageableComponent?> ent);
public void           ClearAllDamage(Entity<DamageableComponent?> ent);
public void           SetAllDamage(Entity<DamageableComponent?> ent, FixedPoint2 v);   // 2-arg, unambiguous vs the 3-arg legacy
public void           SetDamage(Entity<DamageableComponent?> ent, DamageSpecifier d);  // §5.6
public bool           CanBeDamagedBy(EntityUid ent, ProtoId<DamageTypePrototype> type);// reads DamageableComponent.DamageContainerID
public DamageSpecifier HealEvenly(Entity<DamageableComponent?> ent, FixedPoint2 amount, ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null);
public DamageSpecifier HealDistributed(…);
```

**Conditionally safe — add only the `out` form of `TryChangeDamage`:**

```csharp
public bool TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, out DamageSpecifier newDamage,
    bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null,
    bool ignoreGlobalModifiers = false)
{
    newDamage = ChangeDamage(ent, damage, ignoreResistances, interruptsDoAfters, origin, ignoreGlobalModifiers);
    return !newDamage.Empty;
}
```
This is safe because the legacy overload's third parameter is `bool ignoreResistances`, and no existing Wolfgate call passes an `out` argument there — verified: `grep -rn "TryChangeDamage([^)]*out "` over `Content.Shared`/`Content.Server`/`Content.Client` returns **zero** matches.

**Do NOT add** the no-`out` `bool TryChangeDamage(Entity<DamageableComponent?>, DamageSpecifier, bool, bool, EntityUid?, bool)`. Only one Onyx call needs it (`ReagentTreatmentSystems.cs:19`, a phase-4 file); give that one a `// WOLFGATE` edit to `ChangeDamage(...)` instead.

Note the `canSever: false` in `ChangeDamage`: it makes the `DamageChangedEvent.CanSever` flag false for everything routed by Wolfmed, which is belt-and-braces on top of GUARD B.

### 7.3 The namespace and `Clone()` shims

`WG/Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageCompat.cs`:
```csharp
namespace Content.Shared.Damage;

/// <summary>New-API helpers Onyx expects on DamageSpecifier.</summary>
public static class WolfmedDamageSpecifierExtensions
{
    /// <summary>Deep copy; Wolfgate's DamageSpecifier only has a copy constructor.</summary>
    public static DamageSpecifier Clone(this DamageSpecifier spec) => new(spec);
}
```
Six call sites need it in the routing file alone (`WoundDamageRoutingSystem.cs:69, 158, 374, 520, 942`, plus `AmputationSystem.cs:182`). Extension methods are only considered when no applicable instance method exists — `DamageSpecifier` has none named `Clone` — so this is safe.

`WG/Content.Shared/_WF/Wolfmed/Compat/DamageSystemsNamespace.cs` — satisfies the vendored files' `using Content.Shared.Damage.Systems;`:
```csharp
namespace Content.Shared.Damage.Systems;

/// <summary>Exists so vendored Onyx files can keep their upstream `using` for the damage namespace.</summary>
public static class WolfmedDamageSystemsMarker;
```
(Alternatively strip the `using` in each vendored file with `// WOLFGATE`; the marker keeps them verbatim.)

### 7.4 Other new-API calls in the routing file that will not compile as-is

| ONYX line | Symbol | Wolfgate fix |
|---|---|---|
| `:373, 380, 734, 805, 814, 818` | `GetPositiveDamage` | compat (§7.2) |
| `:158, 520, 534` | `GetAllDamage` | compat |
| `:466` | `_mobThreshold.CheckVitalDamage(body, damageable)` | **absent**; `// WOLFGATE` → `damageable.TotalDamage` |
| `:549-558` | `HandLocation.FunctionalLeft/FunctionalRight` | **absent**; `// WOLFGATE` → `HandLocation.Left`/`Right` only, and `_hands.GetActiveHand((body, hands))` returns `Hand?` directly in Wolfgate (`SharedHandsSystem.cs:177`) so the `TryGetHand` round-trip collapses to `hand.Location` |
| `:730, 885, 883` | `BodyPartComponent.MaxDamage`, `.AmputationThresholds`, `.Parent`, `BodyPartType.Chest` | D8: new `_WF/Wolfmed` part component + `Chest→Torso` mapping |
| `:985` | `_damage.CanBeDamagedBy(body, type)` | compat over `DamageableComponent.DamageContainerID` |
| `:1006` | `_targetResolver.TryResolveExact(body, host.SystemicPainTarget /* = TargetBodyPart.Chest */, …)` | `Chest→Torso` mapping (§2.4) |

---

## 8. (6) Risks

### 8.1 Infinite recursion
**Contained by design, but only if the re-entrancy guard stays intact.** `RouteThroughBodyModifiers` opens with `if (!_routing.Add(body)) return false;` (ONYX `:581`) and closes with `_routing.Remove(body)` in a `finally` (`:628`). `OnBeforeDamageChanged` bails on `_routing.Contains(ent)` (`:57`) and `OnDamageDealt` only acts when `_routing.Contains(ent)` (`:66`).

Residual hazards specific to Wolfgate:
- **`SetDamage` inside `RefreshBodyDamage`** (ONYX `:183`) writes the body's `Damage` directly, bypassing `TryChangeDamage`, so it cannot re-enter the router. `_projecting` (`:145`) guards nested refreshes. With GUARD F's delta-aware `SetDamage`, `DamageChangedEvent` fires — confirm no `DamageChangedEvent` subscriber calls `TryChangeDamage` back on the same entity. Checked all 30 subscribers (§9): none does. `FlammableSystem.OnDamageChanged` only calls `Ignite`; `RevenantSystem`, `GuardianSystem`, `MechSystem` are non-humanoid components.
- **Part damage re-entering the body.** `_damage.TryChangeDamage(part, …)` targets the part, which has no `WoundHostComponent`, so the seam in GUARD D (`HasComp<WolfmedHostComponent>`) does not fire. Correct.
- **`ProcessIntegrityTick` if GUARD C is forgotten**: it calls `TryChangeDamage(part, healing, targetPart: …)` every 30 s per part; harmless recursion-wise but it silently erases wound damage.
- **Watch `WoundDamageProjectionSystem.OnRejuvenate`** (`:40-80`): it clears every part then calls `RefreshBodyDamage`, while Wolfgate's `DamageableSystem.OnRejuvenate` (`DamageableSystem.cs:411`) calls `SetAllDamage(body, 0)` which itself loops parts (`:328-339`). Both run on the same `RejuvenateEvent` with **undefined relative order** (different components: `DamageableComponent` vs `WoundHostComponent`). End state is 0 either way, but add an integration assertion.

### 8.2 Prediction / mispredict — the sharpest risk
Onyx routing is **server-only** (`!_net.IsServer` at `:57`, `:66`, `:106`, `:155`…). Wolfgate's `DamageableSystem` and `SharedBodySystem.Targeting` are **shared and predicted** (`SharedBodySystem.Targeting.cs:93` `if (!_timing.IsFirstTimePredicted) return;`).

Consequence: on the client, `BeforeDamageChangedEvent` is **not** cancelled for a wound host, so without the guards the client would run Shitmed's spread + body write, then get corrected by the server state every tick — a visible damage-number/health-bar jitter on every hit, and a limb-status doll that flickers.

Mitigations, in order of importance:
1. **Guards A/B/C must be component-gated only, never `_net.IsServer`-gated.** Then the client predicts "nothing happens locally" and accepts the server's `DamageableComponent` deltas, which is the standard, quiet behaviour.
2. `DamageableComponent` is `[NetworkedComponent]` with `DamageableGetState`/`HandleState` (`DamageableSystem.cs:384-395, 419-440`); part damage is networked because parts have their own `DamageableComponent`. So the doll and health bar converge from server state.
3. `WoundComponent`, `WoundableComponent`, `PainComponent`, `SystemicDamageComponent`, `PartDamageVisualsComponent` are all `[NetworkedComponent, AutoGenerateComponentState]` (ONYX `WoundDamageComponents.cs:95, 102, 145, 155, 180, …`), so wound state replicates. Wounds are *entities in a container* (`WoundableComponent.WoundsContainer`, `:163`) — they will be created server-side and spawned to the client normally, with one-tick latency.
4. Accept that phase 1 is **unpredicted damage for wound hosts**. Given Wolfgate is gun-PvP (memory: `wolfgate-gun-prediction`), expect a complaint about "damage numbers appear a tick late". Predicting Onyx routing is a phase-6 project, not a phase-1 one.

### 8.3 Duplicate directed subscriptions
Only one system may subscribe a given (component, event) pair; violating it crashes at server start (project memory: `duplicate-directed-subscriptions`). Verified collision map for the pairs this design touches:

| Pair | Claimed by |
|---|---|
| `BodyComponent`, `TryChangePartDamageEvent` | `SharedBodySystem` (`SharedBodySystem.Targeting.cs:67`) — untouched |
| `BodyPartComponent`, `DamageChangedEvent` | `SharedBodySystem` (`:70`) — untouched |
| `BodyComponent`, `DamageModifyEvent` / `BodyPartComponent`, `DamageModifyEvent` | `SharedBodySystem` (`:68, :69`) — untouched |
| `WoundableComponent`, `DamageChangedEvent` | **claimed by `WoundDamageProjectionSystem` after GUARD D′.** Nothing else may take it. |
| `WoundHostComponent`, `ComponentStartup`/`ComponentShutdown` | `_WF` `WolfmedBridgeSystem` — free today |
| `DamageableComponent`, `RejuvenateEvent` | `DamageableSystem` (`:57`); Onyx adds `WoundHostComponent, RejuvenateEvent` + `BodyComponent, RejuvenateEvent` (`WoundSystem.cs:34`) + `WoundableComponent, RejuvenateEvent` (`:35`) + `PainComponent, RejuvenateEvent` (`PainSystem.cs:42`) — all distinct components, OK |
| `BodyComponent`, `RejuvenateEvent` | `WoundSystem.cs:34` — **check** nothing in Wolfgate already claims it |
| `WoundHostComponent`, `RefreshMovementSpeedModifiersEvent` | `FractureEffectsSystem.cs:29` — distinct from Wolfgate's `SlowOnDamageComponent` handler, OK |

Also note `WoundDamageRoutingSystem` subscribes `WoundHostComponent, BeforeDamageChangedEvent` **and** `WoundableComponent, BeforeDamageChangedEvent` (`:50, :52`) — fine, different components, and RT's ordering comment (`EntityEventBus.Ordering.cs:70-78`) warns that a system subscribing the same event twice must use *identical* before/after sets. So the `before: [typeof(SharedArmorPlateSystem)]` from §5.8 must be applied to **both** lines 50 and 52, or to neither.

### 8.4 Godmode / stasis ordering
`SharedGodmodeSystem.OnBeforeDamageChanged` (`SharedGodmodeSystem.cs:30`) and `SharedStasisSystem.OnDamage` (`SharedStasisSystem.cs:94`) unconditionally set `Cancelled = true`. Onyx's handler does **not** check `args.Cancelled` (ONYX `:55-62`). Without the `// WOLFGATE` cancel guard (§5.8):

- If godmode runs first: Onyx still cancels (no-op) and still routes. The routed pass re-raises `BeforeDamageChangedEvent`, godmode cancels again, `ChangeDamage` returns empty, `_applied` stays empty. Net effect **correct but wasteful** — an extra event round-trip per hit, per godmoded entity.
- If godmode runs second (order is undefined): identical outcome.

So this is a *robustness/perf* issue, not a correctness hole — but add the guard anyway: it makes the behaviour order-independent and removes a whole class of "routing sees a hit that was already refused" bugs when more `BeforeDamageChangedEvent` cancellers land upstream.

**`SharedGodmodeSystem.EnableGodmode`** also recurses into body children (`SharedGodmodeSystem.cs:57-58`), adding `GodmodeComponent` to every part. That is fine and actually desirable: routing's `TryChangeDamage(part, …)` then gets cancelled per part too.

### 8.5 Armour-plate double absorption
`SharedArmorPlateSystem.OnBeforeDamageChanged` (`SharedArmorPlateSystem.cs:49`) damages the plate, inflicts stamina, and **mutates `args.Damage.DamageDict` in place** (`:95-97`). Routing then hands that same `DamageSpecifier` reference to `RouteThroughBodyModifiers`, whose re-entrant pass raises `BeforeDamageChangedEvent` again ⇒ **the plate would absorb twice per hit** (double durability loss, double stamina).

It is saved only by its own first line, `if (args.Cancelled || !args.Damage.AnyPositive()) return;` (`:51`) — which means **Onyx must cancel before ArmorPlate runs**. Hence `before: [typeof(SharedArmorPlateSystem)]` in §5.8 is mandatory, not cosmetic. `SharedArmorPlateSystem` is `sealed` and lives in `Content.Shared`, so the ordering reference resolves correctly (unlike godmode/stasis, §5.8).

This is the single highest-value ordering constraint in the whole design, and it is Wolfgate-specific — Onyx has no `ArmorPlate`, so upstream gives no guidance.

### 8.6 Silent loss of damage-reaction systems
Already covered by GUARD F (§5.6). Without it, every wound host silently loses: bleeding-from-damage, hemophilia, wake-on-damage, pain screams, forced say, kill attribution, NPC retaliation, and ignite-on-heat, because all of those require `DamageChangedEvent.DamageDelta != null`. Verified line-by-line in §9. This is the easiest bug to ship by accident and the hardest to notice in a test.

### 8.7 Balance shift (expected, should be measured)
1. **Armour now applies once, not twice.** Today a part receives body-inventory armour (`OnPartDamageModify:174-176`) *plus* the `PartDamage` modifier set *plus* `GetPartDamageModifier(type)`, and the body separately applies its own modifier set + `DamageModifyEvent`. After the bridge, body armour is applied once in the routed pass and the part receives `ignoreResistances: true`. **Expect wound-host limb damage to roughly double at equal armour** unless Onyx's `BodyPartProfilePrototype` multipliers compensate.
2. **Part damage was pre-armour, now it is post-armour.** `TryChangePartDamageEvent` is raised at `DamageableSystem.cs:218`, before the body resistance block at `:227`.
3. **`GetPartDamageModifier`** (head ×0.5, limbs ×0.7, `SharedBodySystem.Targeting.cs:432-444`) no longer applies to wound-host parts; Onyx uses `WoundHostComponent.TargetWeights` and profile multipliers instead.
4. **Sever at 130 → Onyx `MaxDamage` overflow + finishing hit.** Different feel, deliberately (D2/phase 3).
5. **Evade is lost** — but it was already dead (no call site sets `canEvade: true`).

### 8.8 Other
- **`SharedBodySystem.PartRemoveDamage`** (`SharedBodySystem.Parts.cs:395-408`) applies `VitalDamage = 100` of `"Bloodloss"` with `partMultiplier: 0f`. `Bloodloss` is **not** in `WoundHostComponent.LocalizedDamageTypes`, so routing sends it to `SystemicDamageComponent`, which projects into the body total and still kills. ✔ No guard needed — but assert it in tests.
- **`SetAllDamage`/`ChangeAllDamage` part loops** (`DamageableSystem.cs:328-339, 361-372`) are gated on `HasComp<TargetingComponent>`, not on the marker. They stay active for wound hosts. That is acceptable (they are used by rejuvenate/admin verbs and set parts and body consistently), but the projection should be refreshed afterwards; simplest is a `_WF` handler on `RejuvenateEvent` ordered last that calls `WoundDamageProjectionSystem.RefreshBodyDamage`.
- **`DropPart` is `protected virtual`** (`SharedBodySystem.Parts.cs:201`) — Onyx's `AmputationSystem` uses `_body.TryDetachPart`; map to public `DetachPart` (`:702/751`), not `DropPart`.
- **`TargetBodyPart` is not `[NetSerializable]` in Wolfgate** (`_Shitmed/Targeting/TargetBodyPart.cs`) while Onyx's is (`_Onyx/Targeting/TargetBodyPart.cs:5`). Any Onyx component that networks a `TargetBodyPart` (e.g. `TargetingSnapshotComponent`) needs the Onyx enum, not Shitmed's — plan on shipping the `_Onyx` enum and a converter, not on reusing Shitmed's.
- **Sandbox**: any new shared/client type must be allow-listed in `Sandbox.yml` (project memory: `client-sandbox-whitelist`).

---

## 9. Full subscriber inventory

### 9.1 `BeforeDamageChangedEvent` — every subscriber in Wolfgate (grep, excluding `bin`/`obj`)

| System | File:line | Component | Behaviour | Conflict with routing? |
|---|---|---|---|---|
| `SharedGodmodeSystem` | `Content.Shared/Damage/Systems/SharedGodmodeSystem.cs:19,30` | `GodmodeComponent` | unconditional `Cancelled = true` | **Ordering** — abstract class, cannot be ordered from shared. Fix with `if (args.Cancelled) return;` in routing (§5.8). Not a double-apply. |
| `SharedStasisSystem` | `Content.Shared/_Goobstation/ChronoLegionnaire/EntitySystems/SharedStasisSystem.cs:52,94` | `InsideStasisComponent` | unconditional `Cancelled = true` | same as godmode |
| `SharedArmorPlateSystem` | `Content.Shared/_Mono/ArmorPlate/SharedArmorPlateSystem.cs:46,49` | `ArmorPlateProtectedComponent` | damages plate, adds stamina, **mutates `args.Damage`**, may cancel | **DOUBLE-APPLY** unless routing is ordered `before:` it (§8.5) |

That is the complete list. `WoundDamageRoutingSystem` adds two more (`WoundHostComponent`, `WoundableComponent`) — no component collision.

### 9.2 `DamageChangedEvent` — every `SubscribeLocalEvent` in Wolfgate

**Mob / body relevant — these matter for the bridge:**

| System | File:line | Component | Requires delta? | Verdict under the bridge |
|---|---|---|---|---|
| `SharedBodySystem` (Shitmed) | `_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:70,214` | `BodyPartComponent` | yes (`delta != null`, `:226`) | **GUARD B**: skip the sever branch; keep `CheckBodyPart` |
| `MobThresholdSystem` | `Content.Shared/Mobs/Systems/MobThresholdSystem.cs:24,424` | `MobThresholdsComponent` | no — reads `args.Damageable.TotalDamage` (`:335-343`) | ✔ works off the projection |
| `BloodstreamSystem` | `Content.Server/Body/Systems/BloodstreamSystem.cs:52,210` | `BloodstreamComponent` | yes (`:212`) | **DOUBLE-APPLY** with `WoundBleedingSystem` → **GUARD E** |
| `HemophiliaSystem` | `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs:23,36` | `HemophiliaComponent` | yes (`:38`) | **DOUBLE-APPLY** → **GUARD E** |
| `SlowOnDamageSystem` | `Content.Shared/Damage/Systems/SlowOnDamageSystem.cs:18,58` | `SlowOnDamageComponent` | no — just refreshes speed | ✔ (may now co-exist with `FractureEffectsSystem`'s own speed modifier; both are multiplicative `RefreshMovementSpeedModifiers` contributions, not a conflict) |
| `SharedDoAfterSystem` | `Content.Shared/DoAfter/SharedDoAfterSystem.cs:33` | `DoAfterComponent` | uses `InterruptsDoAfters` | ✔ but note GUARD F passes `interruptsDoAfters: false` for the projection; the *part* `DamageChanged` (`DamageableSystem.cs:269`) carries the routed value, and the part is not the mob — **do-afters on wound hosts will no longer be interrupted by damage** unless the projection forwards it. Recommend threading `interruptsDoAfters` through `RefreshBodyDamage` in a later pass, or accept it and re-check in playtest. |
| `SleepingSystem` | `Content.Shared/Bed/Sleep/SleepingSystem.cs:54,214` | `SleepingComponent` | yes (`:216`) | ✔ with GUARD F |
| `EmoteOnDamageSystem` | `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs:22,25` | `EmoteOnDamageComponent` | `DamageIncreased` (`:27`) | ✔ with GUARD F (would go silent without it) |
| `DamageForceSaySystem` | `Content.Server/Damage/ForceSay/DamageForceSaySystem.cs:33,99` | `DamageForceSayComponent` | yes (`:101`), ordered `after: MobThresholdSystem` | ✔ with GUARD F |
| `KillTrackingSystem` | `Content.Server/KillTracking/KillTrackingSystem.cs:19,23` | `KillTrackerComponent` | yes (`:25`), ordered `before: MobThresholdSystem` | ✔ with GUARD F; attribution now uses the projected delta — origin is preserved through `ChangeDamage` |
| `NPCRetaliationSystem` | `Content.Server/NPC/Systems/NPCRetaliationSystem.cs:23,27` | `NPCRetaliationComponent` | `DamageIncreased` + `Origin` (`:29-33`) | ✔ with GUARD F |
| `FlammableSystem` | `Content.Server/Atmos/EntitySystems/FlammableSystem.cs:89,380` | `IgniteOnHeatDamageComponent` | yes (`:387`) | ✔ with GUARD F |
| `CESharedZFlightSystem` | `Content.Shared/_CE/ZLevels/Flight/CESharedZFlightSystem.cs:57` | `CEZFlyerComponent` | — | ✔ mob-adjacent but wound-irrelevant |
| `SharedChameleonProjectorSystem` | `Content.Shared/Polymorph/Systems/SharedChameleonProjectorSystem.cs:43` | `ChameleonDisguiseComponent` | — | ✔ irrelevant |
| `GuardianSystem` | `Content.Server/Guardian/GuardianSystem.cs:49` | `GuardianComponent` | — | ✔ guardians are not wound hosts in phase 1 (D3) |
| `RevenantSystem` | `Content.Server/Revenant/EntitySystems/RevenantSystem.cs:61` | `RevenantComponent` | — | ✔ irrelevant |
| `MechSystem` | `Content.Server/Mech/Systems/MechSystem.cs:69` | `MechComponent` | — | ✔ irrelevant |
| `RechargeableBlockingSystem` | `Content.Server/_White/Blocking/RechargeableBlockingSystem.cs:25` | `RechargeableBlockingComponent` | — | ✔ shield item, not the mob |

**Wound-irrelevant (structures, items, props) — no action:**
`SharedPoweredLightSystem.cs:57`, `SharedTapeRecorderSystem.cs:49`, `DamagePopupSystem.cs:16`, `DamageRandomPopupSystem.cs:20`, `DestructibleSystem.cs:53`, `ExplosionSystem.cs:132` (`AirtightComponent`), `CardboardBoxSystem.cs:42`, `KudzuSystem.cs:25`, `VendingMachineSystem.cs:72`, `ArtifactDamageTriggerSystem.cs:13`, `HarpySingerSystem.cs:45` (`InstrumentComponent`), `DelayedItemSystem.cs:20`, `MailSystem.cs:98`.
(`BlindableSystem.cs:17` subscribes `EyeDamageChangedEvent`, a different event — listed only because it matches the grep.)

---

## 10. (7) Test plan — headless integration tests

Location per D6/Testing: `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/`. Wolfgate **already has the same `GameTest` fixture Onyx's tests use** (`WG/Content.IntegrationTests/Fixtures/GameTest.cs:39` `public abstract partial class GameTest`, with `Server`, `SEntMan`, `SComp<T>`, `SSpawn`, `PoolSettings`) plus `Fixtures/Attributes/*` — so ONYX `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` ports with edits only for the prototype block and the `Chest`/`Torso` naming.

Project memory warnings that apply: `db.ef` sqlite warnings fail every pair test — run `DockTest` first to confirm the harness is green before blaming Wolfmed; lint YAML in Release; `ErrorNode` crashes the linter.

### 10.1 `WolfmedDamageBridgeTest` — new, Wolfgate-specific

Prototypes (adapted from ONYX `WoundDamageFoundationTest.cs:32-140`, with `partType: Torso` instead of `Chest`, no `InjurableComponent`, and Shitmed's `BodyPart`/`Body` shape):

```yaml
- type: entity
  id: WolfmedBridgeBody           # WoundHost + Targeting + Damageable + MobState + MobThresholds
- type: entity
  id: WolfmedControlBody          # identical MINUS WoundHost   ← the regression control
```

| # | Test | Assertion |
|---|---|---|
| **T1** | `WoundHostRoutesToTargetedPart` | attacker with `TargetingComponent.Target = LeftArm`; `TryChangeDamage(body, {Blunt:10}, origin: attacker)`; then `Comp<DamageableComponent>(leftArm).TotalDamage == 10`, every other part `== 0`, `Comp<DamageableComponent>(body).TotalDamage == 10` (projection). |
| **T2** | `WoundHostCreatesWoundOnPart` | after T1's hit, `Comp<WoundableComponent>(leftArm).WoundsContainer.ContainedEntities` contains exactly one entity with `WoundComponent.Prototype == "BluntWound"` and `Severity > 0`. |
| **T3** | `ExplicitTargetPartIsHonoured` | `TryChangeDamage(body, {Slash:10}, targetPart: TargetBodyPart.Head)` → head takes 10, arms 0. This is the §5.8 handoff. |
| **T4** | `NoDoubleApplication` | single `TryChangeDamage(body, {Blunt:10})`; assert `Σ over parts of TotalDamage + SystemicDamageComponent.Damage.GetTotal() == 10` **and** `body.TotalDamage == 10`. A Shitmed+Onyx double-apply shows up as 20, or as 10 on a part *and* 10 on the body. |
| **T5** | `NonWoundHostUnchanged` | same attack on `WolfmedControlBody` → `leftArm.TotalDamage == 10 * GetPartDamageModifier(Arm)` (0.7, per `SharedBodySystem.Targeting.cs:432`) **and** `body.TotalDamage == 10`, i.e. today's behaviour exactly, and `!HasComp<WoundableComponent>(leftArm)`. |
| **T6** | `SystemicDamageStaysOnBody` | `TryChangeDamage(body, {Asphyxiation:5})` → every part `0`, `Comp<SystemicDamageComponent>(body).Damage.GetTotal() == 5`, `body.TotalDamage == 5`. Mirrors ONYX `WoundDamageFoundationTest.cs:314-316`. |
| **T7** | `NoSeverAtOneThirty` | drive one arm to `TotalDamage >= 130` with repeated Slash while it is `!Enabled`; assert the arm is **still attached** (`_body.BodyHasChild(body, leftArm)`), i.e. GUARD B works and Onyx's `MaxDamage` overflow is the only dismemberment path. |
| **T8** | `NoShitmedRegen` | set a part to 40 damage, advance `HealingTime + 1` s of server ticks (`part.HealingTime == 30`, `BodyPartComponent.cs:87`), assert the part's damage is unchanged by Shitmed (`SelfHealingAmount == 5` would have removed 5 of each type). Control body: assert it **did** regen. |
| **T9** | `PartStillDisablesAtNinety` | drive an arm to ≥ 90 → `Comp<BodyPartComponent>(arm).Enabled == false` and `Comp<TargetingComponent>(body).BodyStatus[LeftArm] != TargetIntegrity.Healthy`. Proves §6.2's "keep `CheckBodyPart`". |
| **T10** | `GodmodeStillBlocks` | `EnableGodmode(body)`, hit it, assert **all** parts 0, body 0, and `Comp<WoundableComponent>(arm).WoundsContainer` empty. |
| **T11** | `ArmorPlateAbsorbsOnce` | equip a `ArmorPlateHolder` with a plate of known durability; one hit; assert plate durability dropped by exactly one hit's worth. Catches the §8.5 ordering bug. |
| **T12** | `DamageChangedDeltaSurvivesProjection` | subscribe a probe to `DamageChangedEvent` on the body; one hit; assert the probe saw `DamageDelta != null && DamageIncreased`. Catches the §8.6 silent-death class. |
| **T13** | `MobThresholdsStillTrigger` | pile damage past the crit threshold across several parts; assert `MobState == Critical`, then past death; assert `Dead`. Proves the projection feeds `MobThresholdSystem`. |
| **T14** | `VitalPartRemovalStillKills` | remove the vital part; `PartRemoveDamage` (`SharedBodySystem.Parts.cs:406`) applies 100 Bloodloss; assert it landed in `SystemicDamageComponent` and the mob died. (§8.8) |
| **T15** | `RejuvenateClearsEverything` | damage several parts, create wounds, `RaiseLocalEvent(body, new RejuvenateEvent())`; assert all part damage 0, `SystemicDamageComponent.Damage.Empty`, no `WoundComponent` entities, `body.TotalDamage == 0`, `MobState == Alive`. Catches the §8.1 ordering hazard. |
| **T16** | `NoRecursionUnderLoad` | 200 sequential hits alternating damage/heal on the same body; assert no stack overflow, no `Debug.Assert` trip, and final totals consistent. Cheap canary for the `_routing`/`_projecting` guards. |

### 10.2 Ported Onyx tests

`ONYX/Content.IntegrationTests/Tests/_Onyx/Wounds/` has 8 files: `WoundDamageFoundationTest`, `WoundBleedingTest`, `WoundFractureTest`, `WoundHealingTest`, `WoundScarTest`, `WoundSurgeryTest`, `WoundSurgeryScarTest`, `AmputationConsequenceTest`. Phase 1 should port `WoundDamageFoundationTest` (the routing contract), `WoundBleedingTest` and `WoundHealingTest`. The surgery/scar/fracture ones belong to phases 2-4 (D7).

Known port edits: `Chest → Torso` everywhere; drop `- type: Injurable`; `graph.TryDetachPart` (`WoundDamageFoundationTest.cs:318`) → `_body.DetachPart(...)`; `TargetingComponent.DefaultOdds()` (`:169-175`) is Onyx's nested-dictionary shape and only exists if the `_Onyx/Targeting` port lands; `damage.GetAllDamage(x)` needs the §7.2 compat.

### 10.3 Ordering of the work

1. Port `_Onyx/Targeting` with `Chest→Torso` mapping; assert `TargetResolverSystem.TryResolveAvailable` finds a Wolfgate torso. **Nothing below works until this passes.**
2. Compat layer (§7.2, §7.3) + `DamageDealtEvent` — build clean, no behaviour change yet (nothing subscribes).
3. GUARD D + D′ + F, with T12 as the gate.
4. Port `WoundDamageRoutingSystem` + `WoundDamageProjectionSystem` + `WoundSystem` + `WoundPrototype`; T1, T4, T5, T6 as the gate.
5. GUARDs A, B, C, E; T7, T8, T9, T11 as the gate.
6. `targetPart` handoff (§5.8); T3 as the gate.

---

## 11. Corrections / open items for the orchestrator

1. **D2 wording.** "Shitmed's *in-`DamageableSystem`* spreading" is inaccurate: the spreading lives in `SharedBodySystem.Targeting.cs`, reached via `TryChangePartDamageEvent` raised at `DamageableSystem.cs:218`. The `DamageableSystem.cs` change needed is not a guard on spreading — it is the **addition of a `DamageDealtEvent` seam** without which routing cannot work at all.
2. **D2 gating.** There is no `CCVars.Wounds` master switch. `ONYX/Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` defines only `WoundsBodyPartFunctionalityEnabled` (default `false`), three bleeding-autostop CVars, and two explosion CVars. Gating is component presence (`WoundHostComponent` → `_WF` marker). If a kill-switch is wanted, add a Wolfgate CVar in `_WF/Wolfmed` and check it in `WolfmedBridgeSystem` before `EnsureComp<WolfmedHostComponent>`.
3. **D5 list is incomplete.** Also missing and needed by the routing path: `DamageDealtEvent`, `InjurableComponent`, `DamageSpecifier.Clone()`, `MobThresholdSystem.CheckVitalDamage`, `HandLocation.FunctionalLeft/Right`, `CCVars.TargetingEnabled` / `TargetingUseAnatomicalOdds` / `TargetingDownedTargetsAreExact`, and `BodyPartType.Chest`/`Groin`.
4. **New dependency for the bridge:** the damage bridge **cannot be built before the `_Onyx/Targeting` port**, because `TargetResolverSystem.TryResolveAvailable` is what turns a `TargetBodyPart` into a part `EntityUid`, and its `Chest` fallback (`TargetResolverSystem.cs:62`) never matches a Wolfgate body. Sequence accordingly.
5. **Decision needed:** do-after interruption on wound hosts (§9.2, `SharedDoAfterSystem` row). Cheapest phase-1 answer is to accept the loss and revisit; the alternative is threading `interruptsDoAfters` through `RefreshBodyDamage`, which means an extra `// WOLFGATE` parameter on a vendored method.
6. **Balance flag for playtest (D4 says Onyx defaults):** §8.7 predicts wound-host limb damage roughly doubles at equal armour, because today's Shitmed flow applies armour twice. Worth measuring before blaming the wound severity multipliers in `wounds.yml`.
