# CRITIQUE — completeness review of `PLAN.md` (Wolfmed port)

**Reviewer role:** completeness critic. **Mode:** read-only. Nothing under WG was modified.
**Sources read:** `DECISIONS.md`, `WOLFMED_HANDOFF.md`, `PLAN.md` (all 1035 lines), and the analyst
reports (`rt-api-gap.md`, `hooks-a.md`, `hooks-b.md`, `damage-bridge.md`, `wounds-a/b/c.md`,
`body-organ.md`, `mob-wiring.md`, `circulation.md`, `statuseffectnew.md`, `targeting.md`,
`content.md`, `medical-extras.md`, `entityeffects-gap.md`) — the last group consulted by targeted
search rather than end-to-end, because the brief is to find what is *missing*, which means
re-grepping the primary sources.

**Method:** every finding below was re-derived from the actual files. ONYX = `C:/tmp/onyx` at the
pinned commit (read with `git show HEAD:<path>` where a path is outside the sparse set).
WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`.
Line numbers are from those files as they stand today.

Severity key: **blocker** = the plan as written cannot produce a working build/boot, or ships a
combat regression that must not reach a playtest; **major** = a compile error, a wrong decision, or
a silent behaviour loss that will cost a day to diagnose; **minor** = imprecision, missing detail,
or a small balance/ergonomics issue.

---

## 0. Summary table

| # | Sev | Title | WP affected |
|---|---|---|---|
| B1 | blocker | `PainSystem` (WP4, "verbatim") needs a component WP1 defers to WP10 | WP1/WP4 |
| B2 | blocker | `<BodyComponent, BodyPartAddedEvent/BodyPartRemovedEvent>` is already taken — §5.1's "only collision" claim is wrong | WP2/WP5 |
| B3 | blocker | D24 leaves `TryChangeDamage` returning `null` for every wound host; hitscan, melee and projectile feedback all break | WP5/WP8 |
| M1 | major | `WoundInternalBleedingSystem.cs:67` does not compile (tuple → `EntityUid`, two user-defined conversions) | WP6 |
| M2 | major | `ResolveHealingPartEvent.DamageContainers` type mismatch is not fixed by the WP6 #5 edit | WP4/WP6 |
| M3 | major | `OrganDamageSystem`'s `[Dependency] AmputationSystem` must be deleted, not just its call | WP6 |
| M4 | major | `BaseMobSpeciesOrganic` has ≥19 descendants including a synthetic species — D21 contradicts D3 | WP7 |
| M5 | major | `PassiveDamage` on the wound-host base becomes a per-tick routed heal; never mentioned | WP7 |
| M6 | major | WP5 #3's "`[Dependency]` swap" is impossible — `MobThresholdSystem` has no damage dependency to swap | WP5 |
| M7 | major | Facade `CanBeDamagedBy` silently re-bases Onyx's `InjurableComponent` check onto `DamageableComponent` | WP2 |
| M8 | major | Projection `SetDamage` prunes the body's damage dict; Wolfgate's `TryChangeDamage` then silently skips those types | WP2/WP5 |
| m1 | minor | D25 is over-engineered — the call discards the return, so `virtual void` + `override` suffices | WP2/WP4 |
| m2 | minor | D9's Chest→Torso fold silently rebalances `TargetWeights` (4.0/13.0 → 2.5/11.5) | WP4 |
| m3 | minor | `TargetBodyPart.Groin` **does** exist in Wolfgate; D9's "delete every Groin case" is too broad | WP3/WP4 |
| m4 | minor | D16 misses `EvenHealthChange` (already exists in WG) and `DistributedHealthChange` | WP11 |
| m5 | minor | Dead `using Content.Shared.Body;` in five verbatim files — record it so nobody "fixes" it | WP4/WP6 |
| m6 | minor | `MaxDamage` default 0 makes any part missed by WP7's `parts.yml` silently non-amputatable | WP7 |
| m7 | minor | Barotrauma and Temperature damage now create limb wounds every tick; unstated | WP7 |

---

## 1. Blockers

### B1 — `PainSystem.cs` needs `PainNumbnessStatusEffectComponent`, which WP1 defers to WP10

**Evidence.**
ONYX `Content.Shared/_Onyx/Wounds/PainSystem.cs:323`:
```csharp
        return _statusEffects.EnumerateStatusEffects<PainNumbnessStatusEffectComponent>(entity)
```
with `using Content.Shared.Traits.Assorted;` at `:13`.

`PLAN.md:580` (WP1, "Deferred to WP10") lists, verbatim:
> `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs`

`PLAN.md:655` (WP4 file #9) schedules `PainSystem.cs` as **verbatim**.

WP4 therefore cannot compile: `CS0246 PainNumbnessStatusEffectComponent`.

**Why the deferral is unnecessary.** The component is 20 lines and drags nothing:
ONYX `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` needs only
`Content.Shared.Dataset.LocalizedDatasetPrototype` — present at
`WG/Content.Shared/Dataset/LocalizedDatasetPrototype.cs:12` — and its default
`ProtoId<LocalizedDatasetPrototype>? ForceSayNumbDataset = "ForceSayNumbDataset"`, whose target
prototype also already exists at `WG/Resources/Prototypes/Datasets/damage_force_say.yml:14`.

**Fix.** Move `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` from the WP1
"Deferred to WP10" list into WP1's file table (row 17), status `verbatim`. Remove it from the WP10
list at `PLAN.md:789`. No other change.

---

### B2 — `<BodyComponent, BodyPartAddedEvent>` / `<BodyComponent, BodyPartRemovedEvent>` are already owned

**Evidence.** `PLAN.md:804` states:
> That is the **only** confirmed collision with existing Wolfgate code.

and `PLAN.md:839` defers the check ("grep first") for the pair wanted by
`WolfmedBodyEventBridgeSystem` (§2.11, WP2 file #10) and `WolfmedPartLifecycleSystem` (WP5).

The grep result is **taken**:
```
WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.PartAppearance.cs:25:
    SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartAttachedToBody);
WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.PartAppearance.cs:26:
    SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnPartDroppedFromBody);
```
Registering either pair again throws `Duplicate Subscriptions for comp=…, event=…` at
`WG/RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Directed.cs:407,419` — a server-start
crash, exactly the class of failure §5 exists to prevent.

Also relevant: both events are raised **on the body only**, so "subscribe on the part instead" is not
available:
```
WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:337:  var ev = new BodyPartAddedEvent(slotId, partEnt);
WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:338:  RaiseLocalEvent(bodyEnt, ref ev);
WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:357:  var ev = new BodyPartRemovedEvent(slotId, partEnt);
WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:358:  RaiseLocalEvent(bodyEnt, ref ev);
```

This is not a hypothetical: `PLAN.md:709` makes wiring `OnPartInserted`/`OnPartRemoved` mandatory
("or wounds silently never initialise on surgically-attached limbs"), and `PLAN.md:1026` lists it as
trap #2.

**Fix.**
1. Merge `WolfmedBodyEventBridgeSystem` and `WolfmedPartLifecycleSystem` into **one** system (they
   want the same pair; §5.3 already warns about this).
2. Subscribe `<WoundHostComponent, BodyPartAddedEvent>` and
   `<WoundHostComponent, BodyPartRemovedEvent>`. The body carries both `BodyComponent` and
   `WoundHostComponent`, and RT keys registrations on `(component, event)`, so this pair is free and
   the handler fires only for wound hosts — which is exactly the desired scope.
3. Update `PLAN.md` §5.1 to say there are **two** confirmed collisions, and move both rows out of
   §5.3 ("must be checked") into §5.1 ("confirmed, with resolution").
4. If a *part*-scoped hook is ever wanted, note that Shitmed raises `BodyPartComponentsModifyEvent`
   on the part at `SharedBodySystem.Parts.cs:334` and `:352`.

Separately, §5.3's other row (`HealOnBuckleComponent`, `ComponentStartup`) **is** free — I grepped
`Content.Server` and found no subscriber. That one can be promoted to §5.2 as verified.

---

### B3 — D24 leaves `TryChangeDamage` returning `null` for every wound host

**Evidence.** Onyx's router cancels unconditionally:
```
ONYX Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:55-62
    private void OnBeforeDamageChanged(Entity<WoundHostComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_net.IsServer || _routing.Contains(ent))
            return;
        args.Cancelled = true;
        RouteThroughBodyModifiers(ent, args.Damage, args.Origin);
    }
```
and Wolfgate returns `null` on cancel:
```
WG/Content.Shared/Damage/Systems/DamageableSystem.cs:214-215
            if (before.Cancelled)
                return null;
```

`PLAN.md:53` (D24) removes exactly the hooks that Onyx uses to work around this, on the reasoning
that "the bridge catches them all automatically with AP/tool/originFlag intact". That is true for
*applying* damage and false for *reporting* it. Every call site that reads the return value now sees
"no damage dealt" for every humanoid:

| Call site | Consequence |
|---|---|
| `WG/Content.Shared/Weapons/Hitscan/Systems/HitscanBasicDamageSystem.cs:27-34` — `if (damageDealt == null) return;` **inside** `foreach (var hitEntity in args.HitEntities)` | A pierced hitscan that passes through a humanoid `return`s out of the loop, so **every entity behind them takes no damage at all**, and `HitscanDamageDealtEvent` never fires |
| `WG/Content.Shared/Weapons/Melee/SharedMeleeWeaponSystem.cs:583-609` (`:748` for heavy) | `damageResult is {Empty:false}` fails → no Blunt→stamina conversion (`_stamina.TakeStaminaDamage`, `:589`), no `LogType.MeleeHit` admin log, and `damageResult?.GetTotal() > 0` fails → no `DoDamageEffect` |
| `WG/Content.Shared/Projectiles/SharedProjectileSystem.cs:164-172` — `?? new DamageSpecifier()` | `modifiedDamage.AnyPositive()` false → no red damage effect, and the shooter/weapon hit log degrades |
| `WG/Content.Server/Damage/Systems/DamageUserOnTriggerSystem.cs:38` — `... is not null` | trigger reports "did not damage" |
| `WG/Content.Shared/Damage/Systems/DamageOnAttackedSystem.cs:76-79`, `DamageOnInteractSystem.cs:77-80` | admin log, `InteractSound` and `PopupText` all skipped |

This is not covered anywhere in `PLAN.md`. `hooks-b.md` §S1–S3 (lines 370-445) is the only analyst
material that addresses it, and D24 overrules S1/S2/S3 without inheriting the problem they solved.
`PLAN.md` §8.3 traps 1-10 do not list it. §6.2's T1-T16 do not test it.

**Fix (cheapest, one upstream file, keeps D24).** Fold it into the GUARD D2 edit that is already
authorised for `BeforeDamageChangedEvent`, and pull that edit forward from WP8 to WP5:

1. `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:470-476` — append a third `// WOLFGATE`
   member alongside D23's two: `DamageSpecifier? Applied = null`.
2. `WoundDamageRoutingSystem.OnBeforeDamageChanged` — after `RouteThroughBodyModifiers(...)` returns,
   write the routed delta into `args.Applied` (the router already tracks it: `_applied` /
   `RouteThroughBodyModifiers`'s bool return at `WoundDamageRoutingSystem.cs:620-621`; the sum of
   `appliedDamage` from `RouteAppliedDamage`'s `_damage.TryChangeDamage(target, …, out var appliedDamage)`
   at `:704` plus `ApplySystemicDamage`'s `applied` at `:985-996` is the right value).
3. `DamageableSystem.cs:215` — `return before.Applied;` instead of `return null;`. For godmode and
   stasis `Applied` stays null, so their behaviour is unchanged.
4. Add **T-RESULT** to §6.2: one melee light attack on `WolfmedBridgeBody` must return a non-null,
   non-empty `DamageSpecifier`, and a `Blunt` melee hit must still produce stamina damage.
5. Add **T-PIERCE**: a hitscan with two `HitEntities` where the first is a wound host must damage the
   second.

If for some reason the event cannot carry the value, the fallback is to reinstate `hooks-b.md`
S1/S2/S3 for the three combat files — but that is six divergent fork shapes D24 correctly wanted to
avoid, so the event member is the better trade.

---

## 2. Majors

### M1 — `WoundInternalBleedingSystem.cs:67` will not compile

```
ONYX Content.Shared/_Onyx/Wounds/WoundInternalBleedingSystem.cs:67
                    _bloodstream.TryModifyBloodLevel((body, bloodstream), -amount);
```
Wolfgate's signature takes a bare `EntityUid`:
```
WG/Content.Server/Body/Systems/BloodstreamSystem.cs:363
    public bool TryModifyBloodLevel(EntityUid uid, FixedPoint2 amount, BloodstreamComponent? component = null)
```
`PLAN.md:725` (WP6 #3) says:
> `TryModifyBloodLevel((body, bloodstream), -amount)` binds to Wolfgate's `TryModifyBloodLevel(EntityUid, …)` via `Entity<T>`→`EntityUid` — **verify it does**…

It does not. Reaching `EntityUid` from the tuple literal requires **two** user-defined conversions —
`(EntityUid, T)` → `Entity<T>` (`WG/RobustToolbox/Robust.Shared/GameObjects/Entity.cs:34`) and then
`Entity<T>` → `EntityUid` (`Entity.cs:44`) — and C# does not chain user-defined conversions. Result:
`CS1503`.

(The other 30-odd tuple-literal call sites in the wound set are fine, because their targets are
`Entity<T>`/`Entity<T?>` parameters, where exactly one conversion applies. I checked all of them;
`:67` is the only cross-boundary one that fails, apart from the hands block at
`WoundDamageRoutingSystem.cs:549-550` and `FractureEffectsSystem.cs:135-141`, which the plan already
schedules for a rewrite.)

**Fix.** WP6 #3 gains a third `// WOLFGATE` edit:
`_bloodstream.TryModifyBloodLevel(body, -amount, bloodstream);`

---

### M2 — `ResolveHealingPartEvent.DamageContainers` mismatch is not fixed by the WP6 #5 edit

**Evidence.** The event is declared in a file WP4 ships **verbatim** (`PLAN.md:647`, WP4 #1):
```
ONYX Content.Shared/_Onyx/Wounds/WoundEvents.cs:122-133
public record struct ResolveHealingPartEvent(
    EntityUid Body,
    DamageSpecifier Healing,
    IReadOnlyList<ProtoId<DamageContainerPrototype>>? DamageContainers,
    IReadOnlySet<TreatmentCapability> TreatmentCapabilities,
    IReadOnlySet<string>? AllowedWoundStages, …);
```
and it is *constructed* from the Wolfgate component:
```
ONYX Content.Shared/_Onyx/Wounds/WoundHealingSystem.cs:110-112
        var resolve = new ResolveHealingPartEvent(body, healing.Comp.Damage, healing.Comp.DamageContainers,
            healing.Comp.TreatmentCapabilities, healing.Comp.AllowedWoundStages,
            healing.Comp.BloodlossModifier, requestedPart, healing.Comp.HealWounds);
```
```
WG/Content.Server/Medical/Components/HealingComponent.cs:39
        public List<string>? DamageContainers;
```
`PLAN.md:727` (WP6 #5) only changes the *parameter* types of `ResolveHealingPart` and
`IsCompatiblePart` (`:44`/`:154`) to `IReadOnlyList<string>?`. That does not help `:110` (still
`List<string>` → `IReadOnlyList<ProtoId<…>>`, CS1503) and it *breaks* `:33`, which forwards
`args.DamageContainers` — still `IReadOnlyList<ProtoId<…>>` — into the newly-`string` parameter.

**Fix.** Leave all three signatures at Onyx's types and convert once, at the construction site:
```csharp
// WOLFGATE: Wolfgate's HealingComponent.DamageContainers is List<string>.
healing.Comp.DamageContainers?.Select(x => new ProtoId<DamageContainerPrototype>(x)).ToList(),
```
Also tighten HOOK 7 (`PLAN.md:471`), which currently names four fields without types. The event
demands `IReadOnlySet<TreatmentCapability>` and `IReadOnlySet<string>?`, so the two new
`HealingComponent` datafields must be declared as `HashSet<TreatmentCapability>` and
`HashSet<string>?` (not `List<>`), or `:110-111` fails for the same reason.
`TreatmentCapability` lives in `ONYX Content.Shared/_Onyx/Wounds/WoundPrototype.cs:203`, so HOOK 7
also needs a `// WOLFGATE using Content.Shared._Onyx.Wounds;` on an upstream server file — worth
stating explicitly, since ground rule 3 caps upstream edits at "one or two lines".

---

### M3 — `OrganDamageSystem`'s `AmputationSystem` dependency must be removed, not just its call

`PLAN.md:55` (D26) and `PLAN.md:726` (WP6 #4) say only:
> `// WOLFGATE`-disable the `_amputation.HandlePartDamageApplied` call

But the dependency field itself references a type that phase 1 does not port:
```
ONYX Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs:24
    [Dependency] private AmputationSystem _amputation = default!;
ONYX Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs:36
        _amputation.HandlePartDamageApplied(part, ref args);
```
`AmputationSystem` is referenced nowhere else in the whole wound set (I grepped `_Onyx/Wounds/**`:
only its own declaration at `AmputationSystem.cs:16` plus these two lines), so with
`AmputationSystem.cs` unported, line 24 is `CS0246`.

**Fix.** WP6 #4's edit list becomes: comment out **both** `:24` and `:36` with the same
`TODO: phase 3` marker.

---

### M4 — D21 makes 19+ species wound hosts, including a synthetic; this contradicts D3

`DECISIONS.md` D3 and `PLAN.md:27` scope phase 1 to "organic humanoids only… the Human body and any
species sharing organic parts". `PLAN.md:50` (D21) then puts `- type: WoundHost` on
`BaseMobSpeciesOrganic`, with the rationale "One line covers all nine organic species".

There are not nine. Direct children of `BaseMobSpeciesOrganic`
(`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:247`), by grep:

`arachnid`, `diona`, `dwarf`, `gingerbread`, `human`, `moth`, `reptilian`, `slime`, `vox`
(base) + `chitinid`, `feroxi`, `rodentia`, `vulpkanin` (`_DV`) + `tajaran`, `yowie`
(`_Goobstation`) + `asakim`, `protogen` (`_Mono`) + `hydrakin` (`_Obelisk`) — **18 direct**, plus
whatever inherits from those.

At least one is not organic:
```
WG/Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml:2   parent: BaseMobSpeciesOrganic
WG/Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml:74     prototype: SiliconDeathgasp
```
A Protogen would get organic bleeding, fractures, scars and pain on `OrganicBodyPartProfile` — the
exact outcome D3 defers to phase 5.

`PLAN.md:1008` (§8.1 item 1) asks the user about **Diona and Slime only**, which understates the
decision by a factor of nine.

**Fix.** Rewrite §8.1 item 1 to list all 18 descendants and split the question in two:
(a) do plant/slime species ship on Organic defaults? (b) do synthetics (`Protogen`, and any other
descendant with a silicon deathgasp / no bloodstream) ship at all in phase 1? If the answer to (b)
is no, the one-line D21 becomes one line plus one `- type: WoundHost` removal per synthetic — still
far cheaper than six per-species additions, so D21's shape survives, but the manifest must record
the exclusions. Also add a WP7 checkpoint: enumerate `BaseMobSpeciesOrganic` descendants at
implementation time rather than trusting this list, since forks add species.

---

### M5 — `PassiveDamage` on the wound-host base becomes a per-tick routed heal

Never mentioned anywhere in `PLAN.md`. `BaseMobSpeciesOrganic` — the exact prototype D21 targets —
carries:
```
WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:255-263
  - type: PassiveDamage # Slight passive regen…
    allowedStates: [Alive]
    damageCap: 20
    damage:
      types:   { Heat:  -0.07 }
      groups:  { Brute: -0.07 }
```
and the system applies it through the very entry point GUARD D intercepts:
```
WG/Content.Shared/Damage/Systems/PassiveDamageSystem.cs:40
            if (comp.DamageCap != 0 && damage.TotalDamage >= comp.DamageCap)
WG/Content.Shared/Damage/Systems/PassiveDamageSystem.cs:50
                    _damageable.TryChangeDamage(uid, comp.Damage, true, false, damage);
```
After the bridge, for every alive humanoid whose *projected* total is under 20, each tick runs
GUARD D → `RouteAppliedDamage` → `ApplyLocalizedHealing` → `_projection.RefreshBodyDamage` →
facade `SetDamage` → `DamageChanged` → `DamageChangedEvent` on the body. Three consequences:

1. **Double passive healing.** Onyx already models passive recovery per body-part profile
   (`passiveRecoveryMultiplier` / `bedRecoveryMultiplier` in `wounds.yml`). Wolfgate's regen is now
   layered on top, silently, which is a D4 balance decision taken by accident.
2. **`damageCap: 20` now means something different** — it reads `DamageableComponent.TotalDamage`,
   which after the bridge is the projection (Σ parts + systemic), not the old body-local value.
3. **Event storm.** A `DamageChangedEvent` on the body every tick for every lightly-wounded player
   drives `MobThresholdSystem.OnDamaged`, alerts, `SharedArmorPlateSystem`-adjacent listeners, NPC
   aggro and the rest. Worth a profiling pass before a playtest.

**Fix.** Make the decision explicit in the same `// WOLFGATE` block as D21/D22 (WP7 #7): either
remove `PassiveDamage` from the wound-host base, or set its `damage` to `{}` for wound hosts, or
accept it and say so in the manifest with a tuning note. Add a test: a wound host with 10 Blunt on
one arm and no treatment must still have 10 Blunt on that arm after 60 s.

---

### M6 — WP5 #3's "`[Dependency]` swap" is impossible as written

`PLAN.md:678` says of the vendored `_Onyx/Mobs/Systems/MobThresholdSystem.cs`:
> Edits: `[Dependency]` swap to `WolfmedDamageableSystem`; `criticalParts` … → `{ Head, Torso }`

There is no dependency to swap. The vendored partial declares only `_body`:
```
ONYX Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:15
    [Dependency] private SharedBodySystem _body = default!;
```
and *uses* `_damageable` at `:27` and `:42` (`_damageable.GetTotalDamage(...)`), expecting it on
Onyx's main `MobThresholdSystem` class. Wolfgate's class has no such field:
```
WG/Content.Shared/Mobs/Systems/MobThresholdSystem.cs:12-15
public sealed partial class MobThresholdSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private AlertsSystem _alerts = default!;
```

**Fix.** Reword WP5 #3 to: "**add** `[Dependency] private WolfmedDamageableSystem _damageable = default!;`
to the vendored partial (Wolfgate's `MobThresholdSystem` declares neither `_damageable` nor `_body`,
so neither addition collides — verified)". Good news confirmed while checking: the namespace is
`Content.Shared.Mobs.Systems` and Wolfgate's class is `sealed partial`, so the partial does attach,
and `body.RootContainer?.ContainedEntity` compiles against
`WG/Content.Shared/Body/Components/BodyComponent.cs:26` (`ContainerSlot RootContainer`).

---

### M7 — Facade `CanBeDamagedBy` silently re-bases Onyx's check

`PLAN.md:121` declares `public bool CanBeDamagedBy(Entity<DamageableComponent?> ent, ProtoId<DamageTypePrototype> type)`
and `PLAN.md:145` says it reads `DamageableComponent.DamageContainerID`. Onyx's does not:
```
ONYX Content.Shared/Damage/Systems/DamageableSystem.API.cs:455-464
    [Obsolete("Do not rely on the ability to determine if an entity will be able to be damaged by something")]
    public bool CanBeDamagedBy(Entity<InjurableComponent?> ent, ProtoId<DamageTypePrototype> type)
    {
        if (!_injurableQuery.Resolve(ent, ref ent.Comp, false))
            return false;
        return SupportsType(ent.Comp.DamageContainer, type);
    }
```
The substitution compiles — both call sites pass a bare `EntityUid` body
(`WoundDamageProjectionSystem.cs:155`, `WoundDamageRoutingSystem.cs:985`) — but the plan never says
what changed, and the consequence is sharp: the projection **deletes** systemic damage types the
check rejects:
```
ONYX Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs:155-161
                    if (_damage.CanBeDamagedBy(body, type)) { total.DamageDict[type] = amount; continue; }
                    systemic.Damage.DamageDict.Remove(type);
                    Dirty(body, systemic);
```
So the test is now "is this type in the mob's `Biological` damage container" — and any type outside
it is silently deleted from `SystemicDamageComponent` on the next projection.

**Fix.** Add an implementation note under §2.1 saying the check is now against the mob's
`DamageContainerID` (`Biological` for `MobDamageable`, `WG/Resources/Prototypes/Entities/Mobs/base.yml:69-70`),
and that a null container means "supports everything" (already stated). Extend **T6** and
**T-CAUSTIC** to assert that the systemic value survives a second `RefreshBodyDamage` call, not just
the first — that is the assertion that catches a container regression.

---

### M8 — The projection's `SetDamage` prunes the body's damage dict

Onyx's semantics, faithfully restated in `PLAN.md:137-143`, are prune-then-merge:
```
ONYX Content.Shared/Damage/Systems/DamageableSystem.API.cs:42-51
        foreach (var type in ent.Comp.Damage.DamageDict.Keys)
            if (!damage.DamageDict.ContainsKey(type))
                ent.Comp.Damage.DamageDict.Remove(type);
        foreach (var (type, amount) in damage.DamageDict)
            ent.Comp.Damage.DamageDict[type] = amount;
```
The plan's body (`PLAN.md:139-142`) does `ent.Comp.Damage = new DamageSpecifier(damage);`, which is
equivalent. But Wolfgate's damage pipeline depends on the dict being *fully seeded*:
```
WG/Content.Shared/Damage/Systems/DamageableSystem.cs:107-113   // DamageableInit seeds every supported type to zero
WG/Content.Shared/Damage/Systems/DamageableSystem.cs:256-258
                if (!dict.TryGetValue(type, out var oldValue))
                    continue;                                   // type absent → silently skipped
```
`RefreshBodyDamage` calls `SetDamage(body, total)` where `total` holds only the currently non-zero
types (`WoundDamageProjectionSystem.cs:150-182`). For an undamaged wound host `total` is **empty**,
so after the first projection the body's `DamageableComponent.Damage.DamageDict` is empty. This is
the same mechanism as §8.3 trap #1, which the plan documents only for *parts* and
`localizedDamageTypes`.

Concrete consequences: `DamagePerGroup` loses its zero rows (health analyzer / crew monitor
readouts), and any future non-routed write to the body's own `DamageableComponent` — a direct
`SetAllDamage`/`ChangeAllDamage`, a `DamageableComponent` on a wound host that later loses
`WoundHostComponent`, or a code path added later that bypasses GUARD D — becomes a silent no-op.

**Fix.** Make the facade's `SetDamage` *zero* instead of *remove*:
```csharp
foreach (var type in ent.Comp.Damage.DamageDict.Keys.ToArray())
    if (!damage.DamageDict.ContainsKey(type))
        ent.Comp.Damage.DamageDict[type] = FixedPoint2.Zero;   // WOLFGATE: keep the container seeding
foreach (var (type, amount) in damage.DamageDict)
    ent.Comp.Damage.DamageDict[type] = amount;
```
computing the delta the same way as before. Behaviourally identical for every reader of
`TotalDamage`/`DamagePerGroup`, and it preserves the invariant `DamageableInit` establishes. Record
the deviation from Onyx in §8.2 and add an assertion to **T-SETUP**: after map-init and one
`RefreshBodyDamage`, `Comp<DamageableComponent>(body).Damage.DamageDict.Count` still equals the
`Biological` container's supported-type count.

---

## 3. Minors

**m1 — D25 is over-engineered.** `PLAN.md:54` changes `ChatSystem.Emote.cs`'s return type from
`void` to `bool`. The only caller discards it:
```
ONYX Content.Shared/_Onyx/Wounds/PainSystem.cs:297-298
        _chat.TryEmoteWithChat(entity, "Scream", ChatTransmitRange.HideChat,
            ignoreActionBlocker: true, forceEmote: true);
```
So §2.9's shim can be `public virtual void TryEmoteWithChat(...)` and HOOK 6 becomes the single word
`override` — no return-type change, and `PainSystem.cs` still stays byte-identical. Also settle the
open "verify `:85` vs `:60`" note now: PainSystem passes a **string** emote id, so HOOK 6 targets
`WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:60`
(`public void TryEmoteWithChat(EntityUid source, string emoteId, ChatTransmitRange range = …, bool hideLog = false, string? nameOverride = null, bool ignoreActionBlocker = false, bool forceEmote = false)`);
`:85` takes an `EmotePrototype` and must be left alone. Verified `SharedChatSystem` is
`public abstract partial class` (`WG/Content.Shared/Chat/SharedChatSystem.cs:13`) and
`ChatSystem : SharedChatSystem` (`WG/Content.Server/Chat/Systems/ChatSystem.cs:52`).

**m2 — D9's fold rebalances hit distribution.** `PLAN.md:650` maps `[BodyPartType.Chest] = 2.5f` →
`Torso` and deletes `[BodyPartType.Groin] = 1.5f`:
```
ONYX Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:18-28
        [BodyPartType.Chest] = 2.5f, [BodyPartType.Groin] = 1.5f, [Head] = 1f,
        [Arm] = 2f, [Hand] = 1f, [Leg] = 2f, [Foot] = 1f, [Tail] = 1f, [Other] = 1f,
```
Torso's share of random routing falls from 4.0/13.0 (31 %) to 2.5/11.5 (22 %) — a silent balance
change on a gun-PvP server, and inconsistent with Shitmed's `ConvertTargetBodyPart`, which folds
`Groin` **into** `Torso` (`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:395`).
**Fix:** set `[BodyPartType.Torso] = 4f` in the D9 edit and record it in the manifest as a
deliberate deviation.

**m3 — `TargetBodyPart.Groin` exists in Wolfgate.** D9 (`PLAN.md:38`) says "delete every `Groin`
case… Same for `TargetBodyPart.Chest` → `Torso`", which reads as though `TargetBodyPart.Groin` is
also gone. It is not:
```
WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:16    Groin = 1 << 2,
WG/Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:30    All = Head | Torso | Groin | LeftArm | …
```
Real Wolfgate code passes `Groin` (the targeting doll offers it, and it is inside `All`). `WoundTargetResolver`
(§2.13) must therefore accept it and fold it to Torso — which `ConvertTargetBodyPart` already does —
rather than reject it. **Fix:** scope D9's "delete Groin" to `BodyPartType.Groin` and
`HumanoidVisualLayers.Groin` (both genuinely absent:
`WG/Content.Shared/Body/Part/BodyPartType.cs:11-19`, `WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs`
has `Chest` at `:15` and no `Groin`), and add one line to §2.13 saying `TargetBodyPart.Groin`
resolves to the torso part.

**m4 — D16 misses two effect classes.** `PLAN.md:45`/§2.15 lists four classes to author. Onyx's file
declares six:
```
ONYX Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs:9  HealthChange
ONYX Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs:15 EvenHealthChange
ONYX Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs:21 DistributedHealthChange
ONYX Content.Shared/_Onyx/Wounds/ReagentTreatmentEffects.cs:27 MendFractures
```
`EvenHealthChange` **already exists in Wolfgate** at
`WG/Content.Server/EntityEffects/Effects/EvenHealthChange.cs`, so it needs the same HOOK 9
wound-routing branch as `HealthChange` rather than a new class; `DistributedHealthChange` exists
nowhere in Wolfgate and must be authored if any ported reagent uses that tag. Phase 4, but record it
in §8.3 next to traps 6 and 7. (Verified no name collision for `SuppressPain`, `MendFractures`,
`TakeStaminaDamage`, `StaminaDamageCondition`, `DistributedHealthChange`.)

**m5 — dead `using Content.Shared.Body;` is harmless; say so.** Onyx keeps `BodyComponent`/
`OrganComponent` in the bare `Content.Shared.Body` namespace; Wolfgate keeps them in
`Content.Shared.Body.Components` / `.Organ`. I checked every wound file for which symbols actually
come from that namespace: only `WoundSystem.cs:34` (`BodyComponent`),
`OrganDamageSystem.cs:87` (`OrganComponent`), `WoundDamageProjectionSystem.cs:29`
(`InitialBodySystem`) and `FractureEffectsSystem.cs:34-35` (`OrganGot*Event`). The `using` in
`WoundPrototype.cs`, `WoundStatusEffectSystem.cs`, `BodyPartFunctionalitySystem.cs`,
`WoundBleedingSystem.cs` and `FractureAlertSystem.cs` resolves (the namespace exists as a parent of
`Content.Shared.Body.Part`) and is unused. **Fix:** add one line to §4/WP4 saying these usings stay
verbatim on purpose, so nobody "cleans" them and creates a spurious diff at re-sync time. The
plan's per-file edits for the four real cases are all correct.

**m6 — `MaxDamage` default 0 silently disables overflow.** `PLAN.md:367` gives
`WolfmedBodyPartComponent.MaxDamage` no initialiser (so `FixedPoint2.Zero`), and the router
early-returns on it:
```
ONYX Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:726-731
        if (!TryComp(part, out WoundableComponent? woundable) || … ||
            bodyPart.MaxDamage <= FixedPoint2.Zero || …)
            return overflow;
```
Any limb WP7's `parts.yml` forgets gets no amputation overflow **and** never resets
`WoundableComponent.AmputationOverflow`. Since phase 1 ships no `AmputationSystem` (D26) the effect
is invisible until phase 3, then presents as "some limbs can never be severed". **Fix:** add a WP7
acceptance check that every `Base<Part>` abstract in
`WG/Resources/Prototypes/_Shitmed/Body/Parts/` has a matching `WolfmedBodyPart` row.

**m7 — environmental damage now makes limb wounds.** All of these are in `localizedDamageTypes`
(D20's list: `Blunt, Slash, Piercing, Heat, Cold, Shock`), and all sit on the exact prototype D21
targets:
```
WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:250-254   Barotrauma  Blunt 0.50/s, Heat 0.1/s
WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:268-276   Temperature Cold 0.1/s, Heat 1.5/s
```
So a hull breach or a hot room now routes damage to a randomly-picked limb every tick and creates
blunt/burn wounds there. That is Onyx-consistent, but it is a visible gameplay change nobody has
written down. **Fix:** one bullet in §8.1 so the first playtest report of "space makes my arm
bleed" is recognised as intended.

---

## 4. What I verified as correct

These were checked against the primary sources and hold. Listing them so the next agent does not
re-do the work.

**Component registration names — no collisions.** All 14 wound components
(`WoundHostComponent`, `PartDamageVisualsComponent`, `PainComponent`, `PainShockTargetComponent`,
`WoundableComponent`, `BodyPartFunctionalityComponent`, `WoundComponent`, `WoundFunctionalityComponent`,
`WoundBleedingComponent`, `WoundInternalBleedingComponent`, `WoundFractureComponent`,
`WoundScarComponent`, `SystemicDamageComponent`, `OrganDamageComponent`) and all 7 StatusEffectNew
components (`StatusEffect`, `StatusEffectContainer`, `StatusEffectAlert`, `CloneableStatusEffect`,
`ExaminableStatusEffect`, `PermanentStatusEffects`, `RejuvenateRemovedStatusEffect`) are absent from
Wolfgate C# and YAML. D8/D10's claimed collisions are the only ones.

**All wound components live in one shared file.** `WoundDamageComponents.cs:15-300` declares every
one of them, so D13's move of four *systems* to `Content.Server` does not strand a component on the
server. Verified by grepping `public sealed partial class .*Component` across `_Onyx/Wounds/**`.

**CCVars — no collisions.** `targeting.enabled`, `targeting.use_anatomical_odds`,
`targeting.downed_targets_are_exact`, `WoundsBodyPartFunctionalityEnabled`, `WoundsBleeding*`,
`ExplosionLimbDamageVariation`, `ExplosionWoundMultiplier` all return zero hits across
`WG/Content.Shared` + `WG/Content.Server`.

**Sandbox.yml needs nothing.** `Content` is a whitelisted namespace *prefix*
(`WG/RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml:24-27`), and `System.Linq` (`:634`) /
`System.Numerics` (`:646`) are already allowed. `YamlDotNet.Core.Tokens` — the odd `using` WP1 keeps
verbatim — is already used by content (`WG/Content.Shared/_Goobstation/Clothing/Systems/ClothingGrantingSystem.cs:6`),
so the reference resolves.

**GUARD D's insertion point is right.** `WG/Content.Shared/Damage/Systems/DamageableSystem.cs`:
`:210-212` raises `BeforeDamageChangedEvent`, `:227-245` applies resistances and `DamageModifyEvent`,
`:247-248` applies the universal modifiers, `:250-266` writes. Inserting between `:248` and `:250`
puts the seam after all modifiers and before the write, exactly as the plan says, and the
re-entrant routed pass does pick up armour at `:233-234`.

**GUARDs A/B/C sites are accurate.** `_queryTargeting` + `InitializeIntegrityQueue` at
`SharedBodySystem.Targeting.cs:63-71`; `ProcessIntegrityTick` at `:73-85`; the
`_integrityJobQueue.EnqueueJob` at `:101`; `OnTryChangePartDamage` at `:108`; the sever condition and
`CheckBodyPart` at `:222-234`.

**`<WoundableComponent, DamageChangedEvent>` (D11) is free.** Shitmed owns
`<BodyPartComponent, DamageChangedEvent>` (`SharedBodySystem.Targeting.cs:70`) — a different
component. Both will fire on the same part in undefined order; GUARD B is what keeps that safe.

**Armour is not double-applied on the part.** The routed part write uses `ignoreResistances: true`
(`WoundDamageRoutingSystem.cs:704-710`), and Wolfgate raises `DamageModifyEvent` only inside
`if (!ignoreResistances)` (`DamageableSystem.cs:227-237`), so Shitmed's
`<BodyPartComponent, DamageModifyEvent>` (`SharedBodySystem.Targeting.cs:69`) does not fire on the
routed pass.

**`PartDamageModifyEvent` relays correctly.** It is a `sealed class … : EntityEventArgs, IInventoryRelayEvent`
with `SlotFlags TargetSlots` (`WoundEvents.cs:138-151`), matching Wolfgate's interface
(`WG/Content.Shared/Inventory/InventorySystem.Relay.cs:172-180`), and `RelayEvent<T>(Entity<InventoryComponent>, T)`
exists at `InventorySystem.Relay.cs:118` — so `localized = modify.Damage` after the relay does see
handler mutations, because it is a reference type.

**Body/enum/API facts the plan relies on.** `BodyPartType = {Other, Torso, Head, Arm, Hand, Leg, Foot, Tail}`
(`WG/Content.Shared/Body/Part/BodyPartType.cs:11-19`) — no `Chest`, no `Groin`, so D9 is right.
`HumanoidVisualLayers` has `Chest` (`:15`), `RArm/LArm/RHand/LHand/RLeg/LLeg/RFoot/LFoot` (`:21-29`),
no `Groin`. `GetBodyChildren` (`SharedBodySystem.Body.cs:256`), `GetBodyPartChildren` (`Parts.cs:880`),
`BodyHasChild` (`:978`), `GetBodyChildrenOfType` (`:991`), `GetPartOrgans` (`:825`),
`RemoveOrgan` (`Organs.cs:172`) all exist. `DamageSpecifier` is `sealed partial` (`DamageSpecifier.cs:20`)
with no existing `Clone`/`GetPositive`/`GetNegative`, so §2.2 is safe.
`DamageSpecifier.PenetrateArmor` exists (`:306`), backing HOOK 10.
`AlertsSystem` is `abstract partial` with private `_timing` (`:11`), `TryGetAlertState` (`:60`),
`ShowAlert(…, (TimeSpan, TimeSpan)? cooldown, …)` (`:81`) and `TryGet` (`:306`) — §2.4 compiles as
written, and the unnamed-tuple note is correct. `SharedStunSystem` has only
`TryStun`/`TryKnockdown`/`TryParalyze` (`:196/:223/:244`), so §2.8's extension cannot shadow anything.
`SharedJitteringSystem.DoJitter(EntityUid, TimeSpan, bool, float, float, …)` (`:46`) matches
`PainSystem.cs:299` positionally. `DamageableSystem.UniversalTopicalsHealModifier` exists (`:49`).
`EntitySystem.ProtoMan` does **not** exist in RT 277, so §2.5 is needed.
`ConvertTargetBodyPart` already folds `Groin`→`Torso` (`SharedBodySystem.Targeting.cs:395`).
`MobThresholdSystem` is `sealed partial` (`:12`), so the vendored partial attaches.
`BodyRejuvenateSystem` really does own `<BodyComponent, RejuvenateEvent>`
(`WG/Content.Server/_Mono/Body/Systems/BodyRejuvenateSystem.cs:28`), so D17 is justified.
`SharedArmorPlateSystem.OnBeforeDamageChanged` is real and self-guards on `args.Cancelled`
(`WG/Content.Shared/_Mono/ArmorPlate/SharedArmorPlateSystem.cs:46,49-51`), so §5.4's ordering rule
and its rationale are correct. `SharedExecutionSystem`'s `AttemptLightAttack` call is at
`WG/Content.Shared/Execution/SharedExecutionSystem.cs:221`, matching HOOK 13.
`MobDamageable`'s `Destructible` Blunt 400 → `GibBehavior` is at
`WG/Resources/Prototypes/Entities/Mobs/base.yml:65-77` and `BaseMobSpeciesOrganic` is at
`Species/base.yml:247`, so D22's override target is right.
`DefibrillatorComponent`, `PoweredLightComponent` and `PassiveDamageComponent` — the three upstream
components `WoundDamageRoutingSystem` touches besides `HealOnBuckleComponent` — are all **shared** in
Wolfgate (`Content.Shared/Medical/DefibrillatorComponent.cs`, `Content.Shared/Light/Components/PoweredLightComponent.cs`,
`Content.Shared/Damage/Components/PassiveDamageComponent.cs`), so §2.10's single server-only shim is
the complete set.
`wounds.yml` references exactly one external prototype — `alert: BrokenBones` at `:164` — plus the
five `!type:Wound*Behavior` tags, all of which WP7 or WP4 supply; no unscheduled prototype is
referenced.
`HealEvenly`/`HealDistributed` in §2.1 match Onyx's real signatures
(`ONYX DamageableSystem.API.cs:177,248`: `(Entity<DamageableComponent?>, FixedPoint2, ProtoId<DamageGroupPrototype>?, EntityUid?)`),
correcting `rt-api-gap.md` §3.1, which described the second parameter as a `DamageSpecifier`.

---

## 5. Gaps in the analyst reports themselves

- **No report covers the `TryChangeDamage` return value** except `hooks-b.md` §S1-S3, and only
  incidentally (it reproduces the `if (damageDealt == null) return;` line at `hooks-b.md:388,407`
  without naming it as a regression). When the plan overruled S1-S3 the consequence went with it.
- **No report covers `PassiveDamage`, `Barotrauma` or `Temperature` on `BaseMobSpeciesOrganic`.**
  `mob-wiring.md` analysed the prototype for `WoundHost` placement and the gib threshold but not for
  existing damage sources that the bridge will start routing.
- **No report enumerated `BaseMobSpeciesOrganic`'s descendants.** "Nine organic species" appears in
  the plan without a citation; the real count is 18 direct children across five fork directories.
- **`rt-api-gap.md` stopped at method existence, not argument binding.** The two conversion failures
  (M1's tuple → `EntityUid`, M2's `List<string>` → `IReadOnlyList<ProtoId<…>>`) are both "the method
  exists, the call does not bind", which its Part-1/Part-2 tables are not shaped to catch.
- **§5.3's two deferred subscription checks were never run.** One of the two is a crash (B2).
  Deferring a duplicate-subscription check to implementation time is precisely the pattern the
  project's own memory note warns about; §5 should carry the answers, not the questions.

---

## 6. Nothing found contradicting `DECISIONS.md`, except

- **D3 vs D21** — see M4. `DECISIONS.md` D3 scopes phase 1 to "organic humanoids only (the Human
  body and any species sharing organic parts)"; `PLAN.md` D21 ships wounds to 18 species including
  a silicon-deathgasp synthetic. This is the one place the plan's own decision exceeds its binding
  constraint. Everything else (D1 verbatim StatusEffectNew, D2 single damage owner, D5 compat-layer
  strategy, D6 layout, D7 no Onyx surgery, D8 stay on Shitmed `BodyPartComponent`, the `_WF` style
  rules) is respected consistently throughout §2-§7.
