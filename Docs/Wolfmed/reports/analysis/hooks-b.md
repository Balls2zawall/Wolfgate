# Wolfmed Hooks — Batch B

Onyx pinned commit `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (verified via `git -C C:/tmp/onyx log -1`).
WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (read-only, nothing modified).
All Onyx quotes below are verbatim from the pinned commit, fetched with `grep -n` where the path is in the sparse checkout, or `git -C C:/tmp/onyx show HEAD:<path>` where noted "absent from sparse checkout". All WG quotes are from `cat -n` on the worktree.

## Headline correction to the handoff

Five of the twelve assigned files carry **zero wound-system code**. Every `Onyx-*` marker in them belongs to an unrelated Onyx feature that happens to live in the same upstream file. Porting Wolfmed does **not** require touching these five files at all:

| File | Onyx marker(s) found | Actual feature |
|---|---|---|
| `Content.Shared/Bed/BedSystem.cs` | `<Onyx-DoubleBed>`, `<Onyx-DoubleBed-edited>` | Two-person bed sprite/action sharing. No healing-rate or bleed changes. |
| `Content.Shared/Damage/Systems/SharedStaminaSystem.cs` (+ `.Modifier.cs`, `.Resistance.cs`) | `<Onyx-GoobShove-edited>`, `<Onyx-MartialArtBlocked>` | Martial-arts shove/block interplay with stamina crit. No pain/wound interaction (`PainSystem.cs` never references Stamina). |
| `Content.Server/Body/Systems/ThermalRegulatorSystem.cs` | `<Onyx-ColdBlooded>`, `<Onyx-ColdBlooded-edited>` | `ColdBloodedComponent`/`ModifyThermalRegulationEvent`, a reptilian species trait multiplier. No wound/pain coupling. |
| `Content.Server/Medical/CrewMonitoring/CrewMonitoringConsoleSystem.cs` + suit sensors (`Content.Shared/Medical/SuitSensors/SharedSuitSensor.cs`, `SharedSuitSensorSystem.cs`, `SuitSensorComponent.cs`) | `<Onyx-CommandTrackingImplant>`, `<Onyx-CommandTrackingImplant-edited>` | A command-tracking implant that filters crew-monitor sensors to command staff only. `Content.Server/Medical/SuitSensors/SuitSensorSystem.cs` itself (the file named in the task) has **no** Onyx markers at all — the real hook lives in the three `Shared/SuitSensors` files above. Nothing wound-related. |
| `Content.Shared/Medical/Cryogenics/SharedCryoPodSystem.cs` | `<Onyx-TieredMachineParts>` (1 line, line 126: `solutionToInject.ScaleSolution(entity.Comp.CoolingEfficiency);`) | Machine-part-tier scaling of cryo-pod cooling efficiency. Not wound-related; `SharedInsideCryoPodSystem.cs` and `CryoPodComponent.cs` have zero Onyx markers. |

**Recommendation: skip all five for Wolfmed.** If Wolfgate ever wants DoubleBed/ColdBlooded/CommandTrackingImplant/TieredMachineParts as standalone features, treat them as separate, unrelated ports — they are out of scope for the wound system and out of scope for the Wolfmed decisions doc.

The other seven behave as the handoff expected, and one of them (`MobThresholdSystem.cs`) hides a dependency on a whole **undocumented `_Onyx/Mobs` folder** the handoff's inventory table never listed — see below.

---

## Part 1 — the twelve assigned files

### 1. `Content.Server/Damage/Commands/HurtCommand.cs` — RELEVANT (admin `damage` console command)

Onyx marks (`grep -n -i onyx`): lines 4, 5, 21, 57, 68, 118, 126, 145, 158, 189, 192, 222.

Onyx adds a 5th console-command argument (`<body-part>`) that routes through `TargetResolverSystem`/`WoundDamageRoutingSystem` instead of the flat damage command:

```csharp
// Content.Server/Damage/Commands/HurtCommand.cs:57-68 (Onyx)
// <Onyx-PartDamageCommand>
if (args.Length == 5)
{
    var options = SharedTargetingSystem.SelectableParts.AsEnumerable();
    ...
}
// </Onyx-PartDamageCommand>
```//
```csharp
// Onyx Execute(), 5-arg branch:
if (!_entManager.System<WoundDamageRoutingSystem>()
        .TryApplyPartDamage(target.Value, part, damage, ignoreResistances: ignoreResistances))
    shell.WriteLine(...);
```

**WG baseline** (`Content.Server/Damage/Commands/HurtCommand.cs:1-147`) is the vanilla 4-arg version — `args.Length < 2 || args.Length > 4`, no `_Onyx` usings, no `TargetResolverSystem`. Confirmed by full read.

**Insertion point:** WG `HurtCommand.cs:101` (`if (args.Length < 2 || args.Length > 4)`) and `:53` (`GetCompletion`, after the `args.Length == 4` branch).

```csharp
// WOLFGATE: 5th arg = optional TargetBodyPart, routed through Onyx's wound targeting for testing/admin use
if (args.Length < 2 || args.Length > 5)
{
    shell.WriteLine(Loc.GetString("damage-command-error-args"));
    return;
}
...
if (args.Length == 5)
{
    if (!Enum.TryParse<TargetBodyPart>(args[4], ignoreCase: true, out var requestedPart) ||
        !SharedTargetingSystem.IsSelectable(requestedPart) ||
        !_entManager.System<Content.Shared._Onyx.Targeting.TargetResolverSystem>().TryResolveExact(target.Value, requestedPart, out var part))
    {
        shell.WriteLine(Loc.GetString("damage-command-error-body-part", ("arg", args[4])));
        return;
    }
    if (!TryParseDamageSpecifier(args[0], args[1], shell, out var damage))
        return;
    if (!_entManager.System<Content.Shared._Onyx.Wounds.WoundDamageRoutingSystem>()
            .TryApplyPartDamage(target.Value, part, damage, ignoreResistances: ignoreResistances))
        shell.WriteLine(Loc.GetString("damage-command-error-part-damage", ("target", target.Value)));
    return;
}
```

Depends on `TargetResolverSystem` (`Content.Shared/_Onyx/Targeting/TargetResolverSystem.cs`) and `WoundDamageRoutingSystem` (`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs`), both vendored verbatim per D6. **Phase: later** (quality-of-life admin/test command; not required for wounds to function — build it once routing exists, useful for phase-1 QA but not blocking).

### 2. `Content.Shared/Bed/BedSystem.cs` — checked location, **not wound-related**, SKIP (see headline correction). WG's bed logic is server-side at `Content.Server/Bed/BedSystem.cs` (confirmed: `Content.Shared/Bed/Sleep/SleepingSystem.cs` also exists but the buckle/heal logic itself is at `Content.Server/Bed/BedSystem.cs`), matching the handoff's note that Bloodstream/Healing/Bed are server-side in Wolfgate — but since the only Onyx changes to `BedSystem.cs` are the unrelated DoubleBed feature, there is nothing to port into either the Shared or Server file for Wolfmed.

### 3. `Content.Server/Damage/Systems/DamageOtherOnHitSystem.cs` — RELEVANT

Onyx marks: lines 13-16, 27, 42-54 (`<Onyx-Targeting>`). Onyx's version derives from a `SharedDamageOtherOnHitSystem` base (also exists in Onyx at `Content.Shared/Damage/Systems/SharedDamageOtherOnHitSystem.cs`, itself carrying **zero** Onyx markers — the shared base is untouched, only the server override is hooked):

```csharp
// Content.Server/Damage/Systems/DamageOtherOnHitSystem.cs:44-53 (Onyx)
// <Onyx-Targeting>
DamageSpecifier dmg;
if (HasComp<TargetingSnapshotComponent>(uid) && HasComp<WoundHostComponent>(args.Target))
    _woundRouting.TryApplyCarrierDamage(args.Target, uid, damage, args.Component.Thrower, out dmg, component.IgnoreResistances);
else
    dmg = _damageable.ChangeDamage(args.Target, damage, component.IgnoreResistances, origin: args.Component.Thrower);
// </Onyx-Targeting>
```

`TryApplyCarrierDamage` signature (`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:303-309`):
```csharp
public bool TryApplyCarrierDamage(EntityUid body, EntityUid carrier, DamageSpecifier damage, EntityUid? origin, out DamageSpecifier damageDealt, bool ignoreResistances = false)
```

**WG baseline** (`Content.Server/Damage/Systems/DamageOtherOnHitSystem.cs:1-72`, full read): a standalone `EntitySystem` (does **not** derive from any shared base — structurally diverged from Onyx), old-style API: `_damageable.TryChangeDamage(args.Target, ..., origin: ...)` returning nullable `DamageSpecifier?`. Also has extra features Onyx's version lacks: `DamageExamineSystem` integration and `AttemptPacifiedThrowEvent` handling (confirms D5: no `ChangeDamage`/new-API here).

**Insertion point:** WG line 40:
```csharp
var dmg = _damageable.TryChangeDamage(args.Target, component.Damage * _damageable.UniversalThrownDamageModifier, component.IgnoreResistances, origin: args.Component.Thrower);
```

```csharp
// WOLFGATE: hand thrown-item damage to Onyx's wound routing for wound-host targets
var thrownDamage = component.Damage * _damageable.UniversalThrownDamageModifier;
DamageSpecifier? dmg;
if (HasComp<Content.Shared._Onyx.Targeting.TargetingSnapshotComponent>(uid) && HasComp<Content.Shared._Onyx.Wounds.WoundHostComponent>(args.Target))
{
    _woundRouting.TryApplyCarrierDamage(args.Target, uid, thrownDamage, args.Component.Thrower, out var routedDmg, component.IgnoreResistances);
    dmg = routedDmg;
}
else
{
    dmg = _damageable.TryChangeDamage(args.Target, thrownDamage, component.IgnoreResistances, origin: args.Component.Thrower);
}
```
Needs `[Dependency] private WoundDamageRoutingSystem _woundRouting = default!;` added. **Phase: 1** (core routing — thrown-weapon damage is a normal combat path).

### 4. `Content.Shared/Damage/Systems/StaminaSystem.cs` (Onyx name `SharedStaminaSystem.cs`) — checked, **not wound-related**, SKIP. Confirmed the Onyx name guess in the task is right (`SharedStaminaSystem.cs`, split further into `.Modifier.cs`/`.Resistance.cs` partials, neither of which carries any Onyx marker). All 6 markers are Goob-shove/martial-arts. `Content.Shared/_Onyx/Wounds/PainSystem.cs` never references stamina.

### 5. `Content.Server/Body/Systems/ThermalRegulatorSystem.cs` — checked, **not wound-related**, SKIP. Also note WG's copy has diverged structurally from Onyx's (Mono fork): `TemperatureComponent.CurrentTemperature` vs Onyx's `.Temperature`, `_tempSys.GetHeatCapacity(ent, ent)` (method) vs Onyx's `ent.Comp2.HeatCapacity` (field), and WG's `ChangeHeat` takes an extra trailing entity arg. If ColdBlooded is ever ported as its own feature, the signatures will need translating, not just copying.

### 6. `Content.Shared/Damage/Systems/DamageOnInteractSystem.cs` — RELEVANT

Onyx marks: lines 16, 31, 80-88.
```csharp
// Content.Shared/Damage/Systems/DamageOnInteractSystem.cs:80-88 (Onyx)
// <Onyx-Targeting>
// Sharp objects injure the interacting hand, not a random part.
if (HasComp<WoundHostComponent>(args.User) &&
    _woundRouting.TryGetActiveHandPart(args.User, out var handPart) &&
    _woundRouting.TryRoutePartDamage(args.User, handPart, totalDamage, args.Target, out var partDealt))
    totalDamage = partDealt;
else
    totalDamage = _damageableSystem.ChangeDamage(args.User, totalDamage, origin: args.Target);
// </Onyx-Targeting>
```

**WG baseline** (`Content.Shared/Damage/Systems/DamageOnInteractSystem.cs:1-99`, full read) already carries its **own** Shitmed hand-targeting hook doing the equivalent job via the old API:
```csharp
// WG lines 64-78, "Shitmed Change Start/End"
TargetBodyPart? targetPart = null;
var hands = CompOrNull<HandsComponent>(args.User);
if (hands is { ActiveHand: not null })
{
    targetPart = hands.ActiveHand.Location switch { HandLocation.Left => TargetBodyPart.LeftHand, HandLocation.Right => TargetBodyPart.RightHand, _ => null };
}
totalDamage = _damageableSystem.TryChangeDamage(args.User, totalDamage, origin: args.Target, targetPart: targetPart);
```
This is a direct D2 collision: for `WoundHostComponent` entities the Shitmed `targetPart` spread must be bypassed in favor of Onyx's routing.

**Insertion point:** WG line 77 (inside the "Shitmed Change" block).
```csharp
// WOLFGATE: wound hosts route hand-interaction damage through Onyx instead of Shitmed's targetPart spread
if (HasComp<Content.Shared._Onyx.Wounds.WoundHostComponent>(args.User) &&
    _woundRouting.TryGetActiveHandPart(args.User, out var handPart) &&
    _woundRouting.TryRoutePartDamage(args.User, handPart, totalDamage, args.Target, out var partDealt))
{
    totalDamage = partDealt;
}
else
{
    totalDamage = _damageableSystem.TryChangeDamage(args.User, totalDamage, origin: args.Target, targetPart: targetPart);
}
```
Needs `[Dependency] private WoundDamageRoutingSystem _woundRouting = default!;`. `TryGetActiveHandPart`/`TryRoutePartDamage` signatures at `WoundDamageRoutingSystem.cs:545`, `:506`. **Phase: 1** (this is the D2 bridge itself — a template for the pattern used everywhere else).

### 7. `Content.Server/Medical/CrewMonitoring/CrewMonitoringConsoleSystem.cs` — checked, **not wound-related**, SKIP (CommandTrackingImplant, see headline).

### 8. `Content.Shared/Medical/SharedDefibrillatorSystem.cs` (WG: `Content.Server/Medical/DefibrillatorSystem.cs`) — RELEVANT, and the path correction the task asked for is confirmed: Onyx's is Shared/predicted, **Wolfgate's is server-only** at `Content.Server/Medical/DefibrillatorSystem.cs` (no `Content.Shared` defibrillator system file exists in WG at all — only `DefibrillatorComponent.cs`/`DefibrillatorEvents.cs` are shared).

Onyx (`Content.Shared/Medical/SharedDefibrillatorSystem.cs:207-216`):
```csharp
// <Onyx-VitalDamage-edited>
if (TryComp<DamageableComponent>(target, out var targetDamageable) &&
    TryComp<MobThresholdsComponent>(target, out var targetThresholds) &&
    _mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold, targetThresholds) &&
    _mobThreshold.CheckVitalDamage(target, targetDamageable) < threshold)
{
    _mobState.ChangeMobState(target, MobState.Critical, targetMobState, user);
    failedRevive = false;
}
// </Onyx-VitalDamage-edited>
```

**WG baseline** (`Content.Server/Medical/DefibrillatorSystem.cs:203-214`, full read):
```csharp
if (_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold) &&
    TryComp<DamageableComponent>(target, out var damageableComponent) &&
    damageableComponent.TotalDamage < threshold)
{
    _mobState.ChangeMobState(target, MobState.Critical, mob, uid);
    dead = false;
}
```
Structurally identical, just `damageableComponent.TotalDamage` (WG's eager field, old API) vs. Onyx's `CheckVitalDamage(...)` call.

**Insertion point:** WG `Content.Server/Medical/DefibrillatorSystem.cs:205-209`.
```csharp
// WOLFGATE: for wound hosts, revive threshold uses vital-part damage (Head/Chest/Groin + systemic), not raw total damage
if (_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold) &&
    TryComp<DamageableComponent>(target, out var damageableComponent) &&
    _mobThreshold.CheckVitalDamage(target, damageableComponent) < threshold)
{
    _mobState.ChangeMobState(target, MobState.Critical, mob, uid);
    dead = false;
}
```
This is a straight drop-in once `CheckVitalDamage` exists (see file 11 below) — `CheckVitalDamage` itself falls back to `GetTotalDamage` for non-wound-hosts, so no branching is needed here. **Phase: 1** (revival must agree with whatever decides death, from day one).

### 9. Suit sensors (`Content.Server/Medical/SuitSensors/`) — checked, **not wound-related**, SKIP. The task-named file (`SuitSensorSystem.cs`) has no markers; the real markers are in `Content.Shared/Medical/SuitSensors/{SharedSuitSensor.cs, SharedSuitSensorSystem.cs, SuitSensorComponent.cs}`, all `<Onyx-CommandTrackingImplant>` — the same unrelated feature as file 7.

### 10. `Content.Shared/Medical/Cryogenics/SharedCryoPodSystem.cs` — checked, **not wound-related**, SKIP (TieredMachineParts, see headline). WG has the matching file tree (`Content.Shared/Medical/Cryogenics/{SharedCryoPodSystem.cs, SharedInsideCryoPodSystem.cs, CryoPodComponent.cs, ...}` plus server split), but nothing to port.

### 11. `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` — RELEVANT, and hides an undocumented dependency

Onyx marks directly in this file: lines 340, 398 only — both just *call* `CheckVitalDamage(...)`:
```csharp
// Content.Shared/Mobs/Systems/MobThresholdSystem.cs:340 (Onyx)
if (CheckVitalDamage(target, damageableComponent) < threshold) // <Onyx-VitalDamage-edited>
```
`CheckVitalDamage` is **not defined in this file**. It lives in a partial-class file the handoff's inventory table never lists: `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` (absent from the sparse checkout; read via `git show HEAD:...`):

```csharp
// Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs (full file, absent from sparse checkout)
public sealed partial class MobThresholdSystem
{
    [Dependency] private SharedBodySystem _body = default!;

    /// <summary>
    /// Calculates the total damage from vital body parts (Head, Chest, Groin), for complex bodies,
    /// including systemic damage. For non-complex bodies or if no vital parts are found, returns the total
    /// damage from the target entity.
    /// </summary>
    public FixedPoint2 CheckVitalDamage(EntityUid target, DamageableComponent damageableComponent)
    {
        if (!HasComp<WoundHostComponent>(target) ||
            !TryComp(target, out BodyComponent? body) ||
            body.RootContainer?.ContainedEntity is not { } rootPart)
            return _damageable.GetTotalDamage((damageableComponent.Owner, damageableComponent));

        var criticalParts = new[] { BodyPartType.Head, BodyPartType.Chest, BodyPartType.Groin };
        var result = FixedPoint2.Zero;
        foreach (var (part, partComponent) in _body.GetBodyChildren(target))
        {
            if (!TryComp(part, out DamageableComponent? partDamageable) || !criticalParts.Contains(partComponent.PartType))
                continue;
            result += _damageable.GetTotalDamage((part, partDamageable));
        }
        if (TryComp(target, out SystemicDamageComponent? systemic))
            result += systemic.Damage.GetTotal();
        return result;
    }
}
```
This is the folder `Content.Shared/_Onyx/Mobs/` — sibling to `Mobs/Growth` (species growth, unrelated) and `Mobs/DeadExamineSystem.cs` (unrelated). **The handoff's inventory table has no row for `_Onyx/Mobs` at all; add one.**

Also note: `CheckVitalDamage` uses `_damageable.GetTotalDamage((entity, component))` — the new-style API D5 already flags as missing — so this partial file cannot be vendored verbatim without the compat layer, or without rewriting it against WG's old `damageableComponent.TotalDamage` field.

**WG baseline** (`Content.Shared/Mobs/Systems/MobThresholdSystem.cs:334-345`, full read):
```csharp
private void CheckThresholds(EntityUid target, MobStateComponent mobStateComponent,
    MobThresholdsComponent thresholdsComponent, DamageableComponent damageableComponent, EntityUid? origin = null)
{
    foreach (var (threshold, mobState) in thresholdsComponent.Thresholds.Reverse())
    {
        if (damageableComponent.TotalDamage < threshold)
            continue;
        TriggerThreshold(target, mobState, mobStateComponent, thresholdsComponent, origin);
        break;
    }
}
```
No `TryGetPercentageForState`/`CheckVitalDamage` call site exists yet in WG's alert-severity path either (WG's `UpdateAlerts` at line ~362 uses `damageableComponent.TotalDamage` directly there too — need the second hook site).

**Insertion point 1:** WG line 338 (`CheckThresholds`):
```csharp
// WOLFGATE: wound hosts trigger mob-state thresholds off vital-part damage, everyone else off raw total
if (CheckVitalDamage(target, damageableComponent) < threshold)
    continue;
```
**Insertion point 2:** WG's `UpdateAlerts`, wherever it reads `damageableComponent.TotalDamage` for severity lerp (mirror the same substitution).

**New addition:** a `_WF/Wolfmed`-adapted `CheckVitalDamage(EntityUid, DamageableComponent)` extension, rewritten against WG's old-style `DamageableSystem` (`part.TotalDamage` field reads instead of `GetTotalDamage((entity, comp))` calls) since D5 says the new-API overloads don't exist. `SystemicDamageComponent` needs to be confirmed as part of the Onyx Wounds vendor set (it is — `Content.Shared/_Onyx/Wounds`, not checked individually here but referenced consistently across routing code). **Phase: 1** (this gates both file 8's revive check and file 3/6's routing decisions — CheckVitalDamage is load-bearing infrastructure, not a nice-to-have).

### 12. `Content.Shared/Mobs/Systems/MobStateSystem*.cs` — confirmed clean, no hook needed

`git grep -in onyx HEAD -- "Content.Shared/Mobs/Systems/MobStateSystem*.cs"` returns **nothing** across `MobStateSystem.cs`, `.StateMachine.cs`, `.Subscribers.cs`, and `MobStateActionsSystem.cs`. Onyx does not touch `MobStateSystem` at all — everything death/crit-related runs through `MobThresholdSystem` (file 11) instead, which calls `_mobStateSystem.ChangeMobState(...)` unmodified. **No Wolfgate hook needed here.**

---

## Part 2 — the sweep

Ran (against the full Onyx tree via `git grep ... HEAD`, not just the sparse checkout, so nothing sparse-restricted was missed):
```
git grep -l -i -E "wound|pain|fracture|circulat|amputat" HEAD -- Content.Shared Content.Server Content.Client ':!*_Onyx*'
```
84 files matched. Most are **false positives** from substring collisions and filtered out after checking each match line:
- `SprayPainter*` (18 files) — "Pain" substring of "Painter".
- `Teg*`/`SensorMonitoringConsoleSystem.cs`/`SharedTeg.cs` — "circulat" substring of TEG "circulator" (a thermo-electric generator part, nothing to do with blood circulation).
- Idiom/comment noise: "pain in the ass", "painted ourselves into a corner", "wound up", "repaint the sprites", "aus to spain" — `ClientAdminManager.cs`, `AtmosphereSystem.*`, `ReagentProducerAnomaly*`, `EventHorizonSystem.cs`, `ColorNetworkCommand.cs`, `LagCompensationSystem.cs`, `Stylesheets/*`, `SingularityOverlay.cs`, `ClumsyStatusEffectSystem.cs`, `MeleeOperator.cs`/`GunOperator.cs`, `MeleeWeaponComponent.cs`, `BibleComponent.cs`.
- Pre-existing vanilla features with no Onyx marker, confirmed by direct grep: `Content.Shared/Damage/Components/InjurableComponent.cs` (`PainDamageGroups` is vanilla stock's own critical-overlay field, unrelated to Onyx pain), `Content.Shared/DamageOverlay/DamageOverlayComponent.cs` (`PainLevel` field itself is vanilla; only the *system* that fills it is Onyx-hooked, see below), `Content.Shared/Traits/Assorted/PainNumbness*.cs` (pre-existing SS14 trait, not Onyx), `Content.Shared/Mobs/Components/MobThresholdsComponent.cs` (comment only), `Content.Shared/Traits/Assorted/LegsParalyzedSystem.cs` (a generic `// TODO: ... surgery related wound` comment, no Onyx marker, not actionable).

**Genuine wound-system files the handoff's file-by-file list never named.** All confirmed by grep/git-show; hooks below for the ones with real gameplay weight, "later"/"skip" noted for the rest.

### S1. `Content.Shared/Projectiles/SharedProjectileSystem.cs` — the actual gunfire wound-routing seam in Wolfgate (structural mismatch with Onyx)

Onyx hooks damage application inside `Content.Server/Projectiles/ProjectileSystem.cs:OnStartCollide` (lines 12-14, 29, 62-91, all `<Onyx-Targeting>`):
```csharp
// Content.Server/Projectiles/ProjectileSystem.cs:62-91 (Onyx, absent from sparse checkout, read via git show)
if (HasComp<TargetingSnapshotComponent>(uid))
{
    var routed = _woundRouting.TryRouteCarrierDamage(target, uid, ev.Damage, component.Shooter, out damage, component.IgnoreResistances);
    damaged = routed && !damage.Empty;
    if (!routed)
        damaged = _damageableSystem.TryChangeDamage((target, damageableComponent), ev.Damage, out damage, component.IgnoreResistances, origin: component.Shooter);
}
else
    damaged = _damageableSystem.TryChangeDamage((target, damageableComponent), ev.Damage, out damage, component.IgnoreResistances, origin: component.Shooter);
```

**Wolfgate's `Content.Server/Projectiles/ProjectileSystem.cs` is a completely different shape** (confirmed by full read, 1-100+): it overrides `ProjectileCollide` and calls `base.ProjectileCollide(...)`, doing almost nothing else server-specific (destructible-threshold bookkeeping, penetration). **The actual damage application lives in the shared base class**, `Content.Shared/Projectiles/SharedProjectileSystem.cs:159-169`:
```csharp
DamageSpecifier modifiedDamage;
if (_net.IsServer)
{
    modifiedDamage = _damageableSystem.TryChangeDamage(target, ev.Damage, component.IgnoreResistances,
        origin: component.Shooter, tool: uid, armorPenetration: component.ArmorPenetration) ?? new DamageSpecifier();
}
else
{
    modifiedDamage = new DamageSpecifier(ev.Damage);
}
```
This is a direct consequence of Wolfgate's gun-prediction rework (client predicts its own bullet copy; only the server actually applies damage — see project memory on gun prediction). **This means the wound-routing hook for every bullet fired in Wolfgate belongs in `Content.Shared/Projectiles/SharedProjectileSystem.cs`, a file neither the handoff nor the task's file list ever named**, not in the server `ProjectileSystem.cs`.

```csharp
// WOLFGATE: route bullet damage through Onyx's wound system for wound-host targets, server-only (matches gun prediction: client never actually deals damage)
if (_net.IsServer)
{
    if (HasComp<Content.Shared._Onyx.Targeting.TargetingSnapshotComponent>(uid) &&
        _woundRouting.TryRouteCarrierDamage(target, uid, ev.Damage, component.Shooter, out var routedDamage, component.IgnoreResistances))
    {
        modifiedDamage = routedDamage;
    }
    else
    {
        modifiedDamage = _damageableSystem.TryChangeDamage(target, ev.Damage, component.IgnoreResistances,
            origin: component.Shooter, tool: uid, armorPenetration: component.ArmorPenetration) ?? new DamageSpecifier();
    }
}
else
{
    modifiedDamage = new DamageSpecifier(ev.Damage);
}
```
Note: `TryRouteCarrierDamage` (unlike WG's fallback) has no `armorPenetration`/`tool` parameters — check whether `WoundDamageRoutingSystem`'s internal damage application honors armor penetration at all before relying on it for gun balance; this needs verification against `TryRouteCarrierDamage`'s body, not just its signature, before phase-1 sign-off. **Phase: 1** (this is the single most-used damage path in a ship/gun PvP game — must be right from the first cut).

### S2. `Content.Shared/Weapons/Hitscan/Systems/HitscanBasicDamageSystem.cs` — laser/hitscan weapons

Onyx (full file, confirmed present in WG at the same path):
```csharp
// <Onyx-Targeting>
if (TryComp(ent, out TargetingSnapshotComponent? snapshot))
{
    var routed = _woundRouting.TryRouteTargetedDamage(args.Data.HitEntity.Value, dmg, snapshot.RequestedTarget, snapshot.Shooter, out damageDealt);
    damaged = routed && !damageDealt.Empty;
    if (!routed)
        damaged = _damage.TryChangeDamage(args.Data.HitEntity.Value, dmg, out damageDealt, origin: args.Data.Shooter);
}
else
    damaged = _damage.TryChangeDamage(args.Data.HitEntity.Value, dmg, out damageDealt, origin: args.Data.Shooter);
// </Onyx-Targeting>
```
**WG baseline** (`Content.Shared/Weapons/Hitscan/Systems/HitscanBasicDamageSystem.cs:1-45`, full read) diverges structurally too — it's a "Mono" fork variant that loops over **multiple** hit entities per shot (piercing hitscan):
```csharp
foreach (var hitEntity in args.HitEntities) // Mono edit
{
    var damageDealt = _damage.TryChangeDamage(hitEntity, dmg, origin: args.Gun, armorPenetration: ent.Comp.ArmorPenetration, ignoreResistances: ent.Comp.IgnoreResistances);
    if (damageDealt == null) return;
    ...
}
```
**Insertion point:** WG line 27, inside the loop:
```csharp
// WOLFGATE: route each pierced hit through wound routing for wound-host targets
foreach (var hitEntity in args.HitEntities)
{
    DamageSpecifier? damageDealt;
    if (TryComp(ent, out Content.Shared._Onyx.Targeting.TargetingSnapshotComponent? snapshot) &&
        _woundRouting.TryRouteTargetedDamage(hitEntity, dmg, snapshot.RequestedTarget, snapshot.Shooter, out var routedDamage))
    {
        damageDealt = routedDamage;
    }
    else
    {
        damageDealt = _damage.TryChangeDamage(hitEntity, dmg, origin: args.Gun, armorPenetration: ent.Comp.ArmorPenetration, ignoreResistances: ent.Comp.IgnoreResistances);
    }
    if (damageDealt == null) return;
    ...
}
```
Same armor-penetration caveat as S1: `TryRouteTargetedDamage` doesn't take an `armorPenetration` parameter — verify internally before trusting hitscan/gun balance. **Phase: 1.**

### S3. `Content.Shared/Weapons/Melee/SharedMeleeWeaponSystem.cs` — melee light + heavy attacks

This file is heavily modified by several unrelated Onyx features too (GoobShove, MartialArts, `_Onyx-CrusherUpgrades`, `<Onyx-PKAAttachments>`) — only the `<Onyx-Targeting>` blocks matter here.

Light attack (Onyx, ~line 603-616):
```csharp
if (_woundRouting.TryRouteOriginDamage(target.Value, modifiedDamage, user, out damageResult, resistanceBypass))
    damageChanged = !damageResult.Empty;
else
    damageChanged = Damageable.TryChangeDamage(target.Value, modifiedDamage, out damageResult, origin: user, ignoreResistances: resistanceBypass);
```
Heavy attack (Onyx, ~line 794-799): identical pattern via `Damageable.ChangeDamage` (new API) as the non-routed fallback.

**WG baseline**, exact call sites confirmed:
```csharp
// Content.Shared/Weapons/Melee/SharedMeleeWeaponSystem.cs:583 (light/"click" attack, "Shitmed Change")
var damageResult = Damageable.TryChangeDamage(target, modifiedDamage, origin: user, armorPenetration: component.ArmorPenetration, partMultiplier: component.ClickPartDamageMultiplier);
// :748 (heavy attack, "Shitmed Change")
var damageResult = Damageable.TryChangeDamage(entity, modifiedDamage, origin: user, armorPenetration: component.ArmorPenetration, partMultiplier: component.HeavyPartDamageMultiplier);
```
Both are Shitmed's own part-multiplier spread — the D2 collision again.

```csharp
// WOLFGATE (line 583, click attack): wound hosts route through Onyx instead of Shitmed's partMultiplier spread
DamageSpecifier? damageResult;
if (HasComp<Content.Shared._Onyx.Wounds.WoundHostComponent>(target) &&
    _woundRouting.TryRouteOriginDamage(target, modifiedDamage, user, out var routedResult, false))
    damageResult = routedResult;
else
    damageResult = Damageable.TryChangeDamage(target, modifiedDamage, origin: user, armorPenetration: component.ArmorPenetration, partMultiplier: component.ClickPartDamageMultiplier);
```
Mirror at line 748 for the heavy-attack path. **Phase: 1** (melee is the other primary combat path).

### S4. `Content.Shared/Execution/SharedExecutionSystem.cs` — trivial 1-line hook

Onyx:
```csharp
_melee.AttemptLightAttack(attacker, weapon, meleeWeaponComp, victim);
_woundRouting.TryApplyLethalDamage(victim, meleeWeaponComp.Damage, attacker); // <Onyx-ExecutionLethal>
```
**WG baseline**, exact match found at `Content.Shared/Execution/SharedExecutionSystem.cs:221`:
```csharp
_melee.AttemptLightAttack(attacker, weapon, meleeWeaponComp, victim);
```
`TryApplyLethalDamage` (`WoundDamageRoutingSystem.cs:454-468`) self-guards on `HasComp<WoundHostComponent>` and computes `lethalAmount = thresholds.Thresholds.Keys.Last() - _mobThreshold.CheckVitalDamage(body, damageable)`, so it's safe to call unconditionally:
```csharp
// WOLFGATE: guarantee executions finish wound-host victims outright
_melee.AttemptLightAttack(attacker, weapon, meleeWeaponComp, victim);
_woundRouting.TryApplyLethalDamage(victim, meleeWeaponComp.Damage, attacker);
```
**Phase: 1** (cheap, and executions being survivable on wound hosts would be an obvious/exploitable bug otherwise).

### S5. `Content.Server/Explosion/EntitySystems/ExplosionSystem.CVars.cs` + `.Processing.cs` — explosion wound distribution

CVars added (Onyx):
```csharp
public float LimbDamageVariation { get; private set; } // <Onyx-Wounds>
public float WoundMultiplier { get; private set; } // <Onyx-Wounds>
Subs.CVar(_cfg, CCVars.ExplosionLimbDamageVariation, value => LimbDamageVariation = value, true);
Subs.CVar(_cfg, CCVars.ExplosionWoundMultiplier, value => WoundMultiplier = value, true);
```
Damage application (Onyx, `.Processing.cs`, ~line 461-471):
```csharp
// <Onyx-Wounds-edited>
if (!_woundDamageRouting.TryRouteDistributedDamage(entity, damage, TargetBodyPart.All,
        DamageDistribution.SplitWithVariation, ignoreResistances: true, interruptsDoAfters: false,
        variation: LimbDamageVariation, isExplosion: true, woundSeverityMultiplier: WoundMultiplier))
{
    _damageableSystem.ChangeDamage((entity, damageable), damage);
}
// </Onyx-Wounds-edited>
```
This is the mechanism behind the handoff's "explosions also have a chance [to amputate]" line.

**WG baseline**, confirmed exact call site at `Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs:470-473`:
```csharp
_damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
    // Mono: Explosion flag for plate protection
    originFlag: DamageableSystem.DamageOriginFlag.Explosion);
```
**Integration risk, not just a hook:** WG has a Mono-specific `originFlag: DamageOriginFlag.Explosion` (armor "plate protection vs. explosions" modifier) that `TryRouteDistributedDamage`'s signature has **no parameter for** (`WoundDamageRoutingSystem.cs:835-845`: `body, damage, mask, mode, origin, ignoreResistances, interruptsDoAfters, variation, isExplosion, woundSeverityMultiplier` — no origin-flag equivalent). If wound-host entities take the routed path, they silently skip whatever the plate-protection-vs-explosion modifier does. This needs a design decision (extend the routing call, or accept the balance gap for phase-1 explosions) before porting, not just a copy-paste.

```csharp
// WOLFGATE CVars.cs: new tunables for wound-host explosion damage spread
public float LimbDamageVariation { get; private set; }
public float WoundMultiplier { get; private set; }
Subs.CVar(_cfg, CCVars.ExplosionLimbDamageVariation, value => LimbDamageVariation = value, true);
Subs.CVar(_cfg, CCVars.ExplosionWoundMultiplier, value => WoundMultiplier = value, true);

// WOLFGATE Processing.cs:470, replacing the single TryChangeDamage call:
if (!_woundDamageRouting.TryRouteDistributedDamage(entity, damage, TargetBodyPart.All,
        DamageDistribution.SplitWithVariation, ignoreResistances: true, interruptsDoAfters: false,
        variation: LimbDamageVariation, isExplosion: true, woundSeverityMultiplier: WoundMultiplier))
{
    _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
        originFlag: DamageableSystem.DamageOriginFlag.Explosion); // plate protection only applies on the non-wound-host fallback path today
}
```
**Phase: 3** (amputation/explosion phase per the decisions doc's phase list), but flag the plate-protection gap for whoever designs phase 3.

### S6. `Content.Server/Chat/EmoteOnDamageComponent.cs` + `EmoteOnDamageSystem.cs` + `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` — pain-triggered screaming, needs component restructuring (not a minimal hook)

This is the mechanism behind the handoff's "Shock ... triggers ... forced scream" line. Onyx restructures `EmoteOnDamageComponent.Emotes` (flat `HashSet<string>`) into a threshold-keyed `Dictionary<float, HashSet<ProtoId<EmotePrototype>>> EmotesThreshold`, adds `AllowedDamageType`/`PainThreshold`/`LastTotalDamage`, and adds `AddEmote`/`RemoveEmote` API methods (`<Onyx-PainSounds-edited>`). The actual pain gate lives in a **new partial file the task's list never named**, `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` (absent from sparse checkout, full file read via `git show`):
```csharp
private void HandlePainDamageEmote(EntityUid uid, EmoteOnDamageComponent component, DamageChangedEvent args)
{
    var totalDamage = _damageable.GetTotalDamage(uid).Float(); // new-style API (D5)
    ...
    if (component.EmotesThreshold.Count == 0 || totalDelta <= 0 || ... ||
        _statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(uid, out _)) // depends on StatusEffectNew (D1)
        return;
    ...
}
```
**WG baseline** (`Content.Server/Chat/EmoteOnDamageComponent.cs`, full read): still the flat `HashSet<string> Emotes` with old `[DataField("emotes", ...)]` string-keyed attributes, no threshold dictionary, no pain fields, no `AddEmote`/`RemoveEmote`. `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs:25-30` is the plain vanilla `OnDamage` handler.

Porting this cleanly requires: (a) `StatusEffectNew` decided/ported per D1 (this file uses `_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>`, itself a `StatusEffectNew`-based check), (b) the new-API `GetTotalDamage` compat shim per D5, and (c) restructuring `EmoteOnDamageComponent`'s data fields, which is a wider change than a 1-2 line `// WOLFGATE` hook — expect a rewritten component plus a new `_WF/Wolfmed`-adapted pain-emote partial. **Phase: 2** (fractures-and-pain phase, matches the handoff's own phase-2 placement of the pain shock alert).

### S7. `Content.Shared/DoAfter/SharedDoAfterSystem.cs` — good news, Wolfgate likely needs **no edit at all**

Onyx:
```csharp
var manipulation = new Content.Shared._Onyx.Wounds.GetManipulationDurationMultiplierEvent(args.Used); // <Onyx-WoundFunctionality>
RaiseLocalEvent(args.User, ref manipulation);
args.Delay *= manipulation.Multiplier;
```
This is the mechanism behind the handoff's fracture "hand-use multiplier" line (DoAfters run slower with a fractured hand).

**WG baseline** (`Content.Shared/DoAfter/SharedDoAfterSystem.cs:207-213`, confirmed) **already has the identical architectural seam**, ported from Goobstation:
```csharp
// Goobstation start
if (args.MultiplyDelay)
{
    var delayMultiplierEv = new GetDoAfterDelayMultiplierEvent();
    RaiseLocalEvent(args.User, delayMultiplierEv);
    args.Delay *= delayMultiplierEv.Multiplier;
}
// Goobstation end
```
`DoAfterArgs.MultiplyDelay` defaults to `true` (`Content.Shared/DoAfter/DoAfterArgs.cs:61`), so this fires for effectively every DoAfter already. **Recommendation: don't port Onyx's `GetManipulationDurationMultiplierEvent` or touch this file at all** — instead have the Wolfmed fracture system subscribe to WG's existing `GetDoAfterDelayMultiplierEvent` and multiply in the hand-use factor there. This is the "confirm existing features first" case explicitly called out in project memory: Wolfgate already has the exact hook point. **Phase: 2, zero upstream edits needed.**

### S8. `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` — wound/pain examine text

Onyx wraps the existing `CreateMarkup` call:
```csharp
// <Onyx-SelfPainExamine-edited>
var markup = CreateMarkup(uid, args.User, component, damage);
// </Onyx-SelfPainExamine-edited>
```
(Onyx's `CreateMarkup` gained an extra `args.User` parameter to special-case first-person pain text.) **WG baseline** (`Content.Shared/HealthExaminable/HealthExaminableSystem.cs`, confirmed): `CreateMarkup(uid, component, damage)` — 3-arg, no `args.User`, and its body builds markup purely from `component.ExaminableTypes`/`component.Thresholds`/loc strings, no wound awareness. Porting this means adding wound/pain text branches inside `CreateMarkup`, which will collide with Shitmed's own examine strings if any exist for part health — worth a quick check by whoever picks this up, not verified here. **Phase: 4** (per the decisions doc's own phase-4 "examine part status" line).

### S9. `Content.Shared/EntityEffects/Effects/Body/ModifyBleedEntityEffectSystem.cs` — blocked on D5's missing ECS entity-effects

Full Onyx file:
```csharp
public sealed partial class ModifyBleedEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyBleed>
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private WoundBleedingSystem _woundBleeding = default!; // <Onyx-WoundTreatment>
    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyBleed> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        if (HasComp<WoundHostComponent>(entity))
            _woundBleeding.ModifyBodyBleeding(entity, amount);
        else
            _bloodstream.TryModifyBleedAmount(entity.AsNullable(), amount);
    }
}
```
This derives from `EntityEffectSystem<T, TEffect>` — the new ECS-style entity-effect base class D5 already flags as absent from Wolfgate (Wolfgate has the old class-based `EntityEffect`). This file **cannot be vendored as-is**; it needs either the D5 compat layer or a rewrite against the old `EntityEffect` class hierarchy before it can exist in Wolfgate at all. **Phase: 4** (treatment/reagents phase), blocked on the D5 compat-layer decision, not a simple hook.

### S10. Circulatory Streams cluster — `Content.Shared/Chemistry/EntitySystems/InjectorSystem.cs` + `InjectorEvents.cs`, `Content.Shared/Metabolism/MetabolizerComponent.cs` + `MetabolizerSystem.cs`, `Content.Shared/Body/Events/MetabolismExclusionEvent.cs` — bigger than "later", likely **Phase 1**

The handoff already lists `_Onyx.Chemistry.Circulation` as a 4-file port under Phase 1 ("Core: Circulation, WoundPrototype/WoundSystem, routing, bleeding..."), but its "Upstream files Onyx hooks" table never mentions the upstream chemistry/metabolism files that circulation plugs into. Found here:

- `InjectorSystem.cs` adds `[Dependency] private CirculatoryStreamSystem _circulation`, `TargetResolverSystem _targetResolver`, and threads a `RequestedPart` through injection so IV/injector doses can target a specific limb's local bloodstream (`Content.Shared/Chemistry/Events/InjectorEvents.cs`: `InjectorDoAfterEvent` gained `public readonly NetEntity? RequestedPart;`).
- `MetabolizerComponent.cs`'s `[Access(...)]` attribute grows a third allowed type: `typeof(_Onyx.Chemistry.Circulation.CirculatoryStreamSystem)`.
- `MetabolizerSystem.cs` raises a new `MetabolismExclusionEvent` before shuffling the reagent list, to let circulation exclude blood-carried reagents from ordinary metabolism.
- `MetabolismExclusionEvent.cs` itself gained a `SolutionName` field (Onyx: `public readonly record struct MetabolismExclusionEvent(string? SolutionName = null)`).

**WG baseline is structurally different in a way that matters:** Onyx's metabolizer files are `Content.Shared/Metabolism/{MetabolizerComponent,MetabolizerSystem}.cs` (shared/predicted). **Wolfgate's are server-only**, at `Content.Server/Body/{Components/MetabolizerComponent.cs, Systems/MetabolizerSystem.cs}` — confirmed by search, no `Content.Shared/Metabolism/*` path exists in WG at all. `MetabolismExclusionEvent.cs` does not exist anywhere in WG (`find` returns nothing) — there is currently **no exclusion-event mechanism in WG's metabolizer at all**, not even a vanilla one to extend.

This means "Circulation" isn't a clean 4-file `_Onyx` drop plus tiny hooks — it needs a brand-new event type introduced into WG's server-side `MetabolizerSystem.cs`, plus an `[Access]` and a `RaiseLocalEvent` call added there, plus the `InjectorSystem`/`InjectorEvents` hooks for IV/limb targeting. None of this is optional if circulation is genuinely phase-1 (bleeding/healing routes reagents through it) — **recommend the orchestrator re-scope this into phase 1's task list explicitly, with its own file-by-file hook pass**, rather than leaving it implicit inside "port 4 files."

**Phase: 1** (per the existing decision to include Circulation in the core phase), but under-scoped in the current handoff — flag for follow-up, no ready-to-drop hook written here given the server/shared path mismatch needs a design call first.

### S11. `Content.Shared/Body/OrganComponent.cs` — no hook needed; use a separate Wolfmed component instead

Onyx adds an `[Access(..., typeof(_Onyx.Wounds.OrganDamageSystem), typeof(_Onyx.Medical.Surgery.SharedSurgerySystem), typeof(_Onyx.Body.Systems.OrganHealthSystem))]` list plus `Health`/`MaxHealth`/`DestructionWound`/`DestructionWoundSeverity` fields directly onto the base `OrganComponent`.

**WG's `OrganComponent` lives at a different path**, `Content.Shared/Body/Organ/OrganComponent.cs` (Shitmed restructured it under an `Organ/` folder), and — confirmed by full read — carries **no `[Access(...)]` attribute at all** (it's commented out: `// [Access(typeof(SharedBodySystem))] // Shitmed Change - no explicit access`) and no `Health`/`MaxHealth` fields; Shitmed's organ model is built around `SlotId`/`ToolName`/`OnAdd`/`OnRemove` (transplant/surgery bookkeeping), not a damage/health value. This actually makes the port **easier than Onyx's own approach**: since there's no access restriction to fight and no colliding field names, organ health/destruction-wound data can live entirely in a new `_WF/Wolfmed` component (`OrganDamageComponent` or similar, per D8's precedent for `BodyPartComponent`) with its own system, subscribed the normal way — **zero edits to `OrganComponent.cs` itself.** **Phase: 3** (organ damage, per the decisions phase list).

### S12. Pain HUD overlay — `Content.Shared/DamageOverlay/SharedDamageOverlaySystem.cs` (+ `DamageOverlayComponent.cs`, `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs`)

`DamageOverlayComponent.PainLevel` is a **pre-existing vanilla field** (no Onyx marker on the component). Onyx only changes how the *system* computes it:
```csharp
// Content.Shared/DamageOverlay/SharedDamageOverlaySystem.cs (Onyx, absent from sparse checkout)
// <Onyx-PainDamageOverlay-edited>
[Dependency] private MobThresholdSystem _mobThresholdSystem = default!;
[Dependency] private PainSystem _pain = default!;
...
entity.Comp.PainLevel = TryComp(entity, out PainComponent? pain) && pain.SoftPainCap > FixedPoint2.Zero
    ? FixedPoint2.Min(1f, _pain.GetPain((entity.Owner, pain)) / pain.SoftPainCap).Float()
    : 0f;
```
i.e. the red vignette switches from "damage ratio" to "actual wound pain value." **WG's equivalent client overlay lives at a different path**, `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs` (confirmed present); the shared `SharedDamageOverlaySystem`/`DamageOverlayComponent` split needs checking against WG's actual class structure before writing a hook — not done in this pass given time budget. Uses new-API `_damageable.GetDamagePerGroup`/`GetTotalDamage` too (D5). **Phase: 2** (pain HUD, cosmetic-but-important feedback for the pain-shock mechanic in S6).

### S13. Wound sprite overlays — `Content.Client/Damage/DamageVisualsSystem.cs`, `Content.Client/HealthAnalyzer/UI/{HealthAnalyzerControl.xaml(.cs), HealthAnalyzerWindow.xaml}`, `Content.Client/Medical/Cryogenics/CryoPodWindow.xaml`

All client-only, all `<Onyx-PartDamageVisuals>`/`<Onyx-HealthAnalyzer-WoundPanel>` markers. `DamageVisualsSystem.cs` adds per-part detached-limb damage-texture layers (`_Onyx/Wounds/{group}_damage.rsi`, matching the handoff's 156-texture inventory) via `PartDamageVisualsComponent`; the HealthAnalyzer XAML files add a wound-diagnostics panel to the health scanner window and reuse the same panel inside the cryopod window. WG has matching files at `Content.Client/Damage/DamageVisualsSystem.cs` and `Content.Client/HealthAnalyzer/UI/HealthAnalyzerWindow.xaml(.cs)`, but **no `HealthAnalyzerControl.xaml`** — Onyx apparently split the wound panel into its own control file WG never had a reason to split out; WG's version likely has the equivalent markup inlined in `HealthAnalyzerWindow.xaml` directly (not verified line-by-line here). **Phase: 4** (health analyzer wound diagnostics, per the decisions phase list) — pure UI, no gameplay risk, safe to defer entirely.

### S14. `Content.Shared/Changeling/Systems/RegenerativeStasisSystem.cs` — SKIP, species absent

`<Onyx-WoundTreatment>`-marked (Changeling regen interacts with `WoundBleedingSystem`). **Wolfgate has no `Content.Shared/Changeling` folder at all** (confirmed: `find` returns nothing) — Changeling isn't a species/antagonist in this fork. Nothing to port; if Changeling is ever added to Wolfgate this file becomes relevant, not before.

---

## Cross-cutting risks worth flagging to the orchestrator

1. **Armor-penetration / origin-flag gap in wound routing.** Three separate routing call families (`TryRouteCarrierDamage` for projectiles/thrown, `TryRouteTargetedDamage` for hitscan, `TryRouteDistributedDamage` for explosions) have no parameter for Wolfgate-specific damage-pipeline extras: `armorPenetration`/`tool` (S1, S2) and the Mono `DamageOriginFlag.Explosion` plate-protection flag (S5). Every combat-damage hook above therefore has a fallback path that preserves these Wolfgate extras only when routing *doesn't* apply — meaning a wound-host entity's armor may behave differently from a non-wound-host entity's armor under the same gunfire/explosion. This should be a deliberate design decision (extend Onyx's routing signatures with a WOLFGATE-added parameter, or accept the discrepancy for phase 1), not something that falls out accidentally per-file.
2. **`CheckVitalDamage` is load-bearing and was completely missing from the handoff's inventory.** It gates death (`MobThresholdSystem`), revival (`DefibrillatorSystem`), execution (`SharedExecutionSystem`), and the pain overlay (S12). It needs the D5 new-API compat shim (`GetTotalDamage`) resolved before any of those four features can work correctly for wound hosts — this should be one of the very first things built in phase 1, ahead of the individual combat-path hooks.
3. **Circulation (S10) is under-scoped.** The handoff's "port 4 files" line hides that WG's metabolizer is server-only (not shared, unlike Onyx's) and has zero existing reagent-exclusion mechanism to extend — this needs its own design pass, not a blind vendor-and-hook.
4. **Two "free" hooks found:** `GetDoAfterDelayMultiplierEvent` (S7) already does what Onyx's manipulation-duration hook does — no upstream edit needed there at all. Confirm before phase 2 whether any other Onyx hook has a similar pre-existing Wolfgate/Goobstation equivalent; this sweep only checked the files explicitly named or turned up by keyword grep.

## Phase summary

| Phase 1 (core, blocking) | Phase 2 (fractures/pain) | Phase 3 (amputation/organs) | Phase 4 (treatment/UI) | Skip |
|---|---|---|---|---|
| DamageOtherOnHitSystem (#3), DamageOnInteractSystem (#6), MobThresholdSystem+`_Onyx/Mobs` partial (#11), DefibrillatorSystem (#8), SharedProjectileSystem (S1), HitscanBasicDamageSystem (S2), SharedMeleeWeaponSystem (S3), SharedExecutionSystem (S4), Circulation cluster (S10, needs re-scoping) | EmoteOnDamage pain-scream (S6), DoAfter manipulation multiplier (S7, no edit needed), Pain overlay (S12) | Explosion wound distribution (S5), OrganComponent → new Wolfmed component (S11) | HealthExaminable wound/pain text (S8), ModifyBleedEntityEffectSystem (S9, blocked on D5), Wound sprite overlays (S13) | HurtCommand part-targeting (#1, QoL not blocking), BedSystem (#2), StaminaSystem (#4), ThermalRegulatorSystem (#5), CrewMonitoringConsoleSystem+SuitSensors (#7/#9), SharedCryoPodSystem (#10), RegenerativeStasisSystem (S14) |

MobStateSystem*.cs (#12): no hook, confirmed untouched by Onyx.
