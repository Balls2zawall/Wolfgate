# Wolfmed phase 2 — `FractureEffectsSystem.cs` + `FractureAlertSystem.cs` port plan

**Analyst report `fractures`** — 2026-09-13.
**WG** = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
**ONYX** = `C:/tmp/onyx` @ `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
Read-only pass. Every claim below was verified by reading the real file; line numbers are from the files as
they exist today (phase 1 committed).

---

## 0. Executive summary

| | |
|---|---|
| Files to vendor | 2 (`FractureEffectsSystem.cs`, `FractureAlertSystem.cs`) |
| New `_WF` files | 1 (`WolfmedFractureDoAfterSystem.cs`) — optional but strongly recommended |
| Upstream (`// WOLFGATE`) edits required | **0** if the do-after bridge is used; 2 lines if Onyx's `Used` semantics must be preserved exactly (needs an explicit escalation — PLAN §3 forbids touching `SharedDoAfterSystem.cs`) |
| New shims needed | **none** — WP2's `OnyxBodyEvents.cs` and WP4's `WolfmedBodyPartComponent` already cover everything |
| Assets needed | **none** — WP7 already shipped the `BrokenBones` alert, `fracture.rsi` and `fractures.ftl`, all byte-identical to Onyx |
| Prototypes needed | **none** — `OrganicFractureProfile` and every `fractureProfile:` row already ship |
| Directed subscription pairs | 8 + 1 (bridge). **All free** — verified against every subscriber in WG |
| Blocker | **None.** One design decision to confirm (§4.4) and one Onyx balance inversion to flag (§8.1) |
| Difficulty | `FractureAlertSystem` **light** (1 edit). `FractureEffectsSystem` **medium** (1 method rewritten, everything else verbatim). Test port **medium** (every Onyx literal is stale, must be re-derived) |

---

## 1. `FractureAlertSystem.cs` — 46 lines, class `FractureAlertSystem`

**Source:** `ONYX Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` (present in the sparse checkout).
**Destination:** `WG/Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` (D6).

### 1.1 External symbol table

| Symbol | Onyx use | WG status | Evidence |
|---|---|---|---|
| `AlertsSystem` (type, `[Dependency]` `:11`) | `Content.Shared.Alert.AlertsSystem` | **SAME** | `WG/Content.Shared/Alert/AlertsSystem.cs:9` `public abstract partial class AlertsSystem : EntitySystem` |
| `AlertsSystem.ShowAlert` (`:38`, called as `_alerts.ShowAlert(uid, alert)`) | Onyx: `public void ShowAlert(Entity<AlertsComponent?> entity, ProtoId<AlertPrototype> alertType, short? severity = null, (TimeSpan, TimeSpan)? cooldown = null, bool autoRemove = false, bool showCooldown = true)` | **DIFFERENT signature, SAME at the call site** — WG `AlertsSystem.cs:81` `public void ShowAlert(EntityUid euid, ProtoId<AlertPrototype> alertType, short? severity = null, (TimeSpan, TimeSpan)? cooldown = null, bool autoRemove = false, bool showCooldown = true)`. The call passes a bare `EntityUid uid`, which binds to both. No edit. | read directly |
| `AlertsSystem.ClearAlert` (`:40`, `:44`) | `ClearAlert(Entity<AlertsComponent?>, ProtoId<AlertPrototype>)` | **DIFFERENT signature, SAME at the call site** — WG `AlertsSystem.cs:153` `public void ClearAlert(EntityUid euid, ProtoId<AlertPrototype> alertType)`. No edit. | read directly |
| `AlertsSystem.UpdateAlert` | **not used by this file** | n/a — WP2's shim (`WG/Content.Shared/_WF/Wolfmed/Compat/AlertsSystem.UpdateAlert.cs:9`) exists but is irrelevant here. It serves `StatusEffectAlertSystem` only. | read directly |
| `AlertPrototype`, `ProtoId<AlertPrototype>` | `Content.Shared.Alert` | **SAME** | — |
| `SharedBodySystem` (`[Dependency]` `:12`) | `Content.Shared.Body.Systems` | **SAME** | `WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Relay.cs:5` declares `namespace Content.Shared.Body.Systems` |
| `SharedBodySystem.GetBodyChildren(uid)` (`:22`) | `IEnumerable<(EntityUid, BodyPartComponent)>` | **SAME at the call site** — WG `Content.Shared/Body/Systems/SharedBodySystem.Body.cs:256` `public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid? id, BodyComponent? body = null, BodyPartComponent? rootPart = null)` | read directly |
| `WoundFractureSystem.GetFracture(part)` (`:30`) | returns `Entity<WoundComponent, WoundFractureComponent>?` | **SAME** — `WG/Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:88` `public Entity<WoundComponent, WoundFractureComponent>? GetFracture(Entity<WoundableComponent?> part)` | read directly |
| `FractureProfilePrototype.Alert / .AlertMinimumGrade / .AlertHiddenTreatments` | `:25-32` | **SAME** — `WG/Content.Shared/_Onyx/Wounds/WoundPrototype.cs:244` `public ProtoId<AlertPrototype>? Alert = "BrokenBones";`, `:247` `public FractureGrade AlertMinimumGrade = FractureGrade.Simple;`, `:250` `public HashSet<FractureTreatment> AlertHiddenTreatments = [FractureTreatment.Mended];` | read directly |
| `IPrototypeManager.TryIndex<T>` (`:25`), `EnumeratePrototypes<T>` (`:42`) | Robust | **SAME** — RT 277 `RobustToolbox/Robust.Shared/Prototypes/IPrototypeManager.cs:52` `IEnumerable<T> EnumeratePrototypes<T>() where T : class, IPrototype;` | read directly |
| **`BodyPartComponent.FractureProfile`** (`:24`) | `if (bodyPart.FractureProfile is not { } profileId ‖ …)` | **MISSING.** D8 keeps Wolfgate on Shitmed's `BodyPartComponent`, which has no such field; the data lives on `WolfmedBodyPartComponent.FractureProfile` (`WG/Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs`, `[DataField] public ProtoId<FractureProfilePrototype>? FractureProfile;`). **This is the only edit the file needs.** | precedent: `WoundFractureSystem.cs:151` already does `var profileId = _wfPart.Get(part).FractureProfile; // WOLFGATE: D8, FractureProfile lives on WolfmedBodyPartComponent.` |

### 1.2 The single `// WOLFGATE` edit

Two lines change plus one `[Dependency]` and one `using`. Follow `WoundFractureSystem.cs:21,151` exactly so
the two read sites stay identical in style.

Add to the dependency block (after `:11-14`):
```csharp
    [Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8, Onyx's extra part fields.
```
and to the `using` block:
```csharp
using Content.Shared._WF.Wolfmed.Body; // WOLFGATE: D8 keeps Onyx's part fields on WolfmedBodyPartComponent.
```

Replace `:22-27` with:
```csharp
        foreach (var (part, bodyPart) in _body.GetBodyChildren(uid))
        {
            // WOLFGATE: D8, FractureProfile lives on WolfmedBodyPartComponent, not Shitmed's BodyPartComponent.
            if (_wfPart.Get(part).FractureProfile is not { } profileId ||
                !_prototypes.TryIndex(profileId, out FractureProfilePrototype? profile) ||
                profile.Alert is not { } alert)
                continue;
```
`bodyPart` becomes unused in the loop header. **Do not** change it to `_` — `GetBodyChildren` yields a tuple
and the deconstruction pattern is what the rest of the vendored tree uses; an unused deconstruction variable
is not a warning. (`WoundFractureSystem.cs:148` keeps an equally unused `BodyPartComponent? bodyPart` for the
same reason.)

**Keep `using Content.Shared.Body;` at `:2` verbatim.** It resolves (C# declares every enclosing namespace,
and WP2's `OnyxBodyEvents.cs:1` now genuinely declares `namespace Content.Shared.Body`) and PLAN §2 explicitly
forbids "cleaning" it.

### 1.3 Directed subscriptions

**None.** `FractureAlertSystem` has no `Initialize`. Pure API (`Refresh(EntityUid?)`), called only from
`FractureEffectSystem` (`:41,48,55,62,69,85`). Zero collision risk.

### 1.4 Assets — already shipped by WP7, all byte-identical to Onyx

| Asset | WG path | Onyx path | Verified |
|---|---|---|---|
| Alert prototype | `WG/Resources/Prototypes/_Onyx/Alerts/alerts.yml` — `- type: alert / id: BrokenBones / icons: [sprite: /Textures/_Onyx/Interface/Alerts/fracture.rsi, state: brokenbones] / name: alerts-broken-bones-name / description: alerts-broken-bones-desc` | `Resources/Prototypes/_Onyx/Alerts/alerts.yml:34-40` | **identical.** No `category:` on this entry (Onyx's `Centered` at `:43-44` has one, `BrokenBones` does not) — so **no alert category is involved**, and `ClearAlertCategory` is never called |
| Texture | `WG/Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{brokenbones.png,meta.json}` | same | **identical.** `meta.json`: `"license": "CC-BY-SA-3.0"`, `"copyright": "Taken from tgstation, redrawn by darkrell"`, 32×32, one state `brokenbones`. Licence is compatible and already recorded by WP7 |
| Locale | `WG/Resources/Locale/en-US/_Onyx/medical/fractures.ftl` — `alerts-broken-bones-name = Broken bones` / `alerts-broken-bones-desc = You have one or more broken bones. Get medical attention as soon as possible.` | `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | **identical, 2 keys, complete** |

Nothing to copy. WP7 already lint-passed this YAML (`C:/tmp/wolfmed-plan/wp/WP7-yamllint2.log`).

**Consequence worth stating:** `FractureProfilePrototype.Alert` defaults to `"BrokenBones"` in C#
(`WoundPrototype.cs:244`), so any future `fractureProfile` that omits `alert:` still resolves — the prototype
must exist, and it does.

### 1.5 Networking / prediction

Shared, **not** `_net.IsServer`-gated — same as Onyx. WG's `AlertsSystem.ShowAlert`/`ClearAlert` both open with
`if (_timing.ApplyingState) return;` (`:84`, `:155`), matching Onyx, so a client-side refresh driven by a
networked component state change is safe. In practice every caller of `Refresh` in phase 2 is reached only
from a server-gated path (§2.5), so client-side alert churn is nil.

### 1.6 Difficulty

**Light.** One field redirect, one dependency, one using. ~6 changed lines.

---

## 2. `FractureEffectsSystem.cs` — 204 lines, class `FractureEffectSystem`

**Source:** `ONYX Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs`.
**Destination:** `WG/Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs`.
Note the file name (`…Effects…`) does not match the class name (`FractureEffectSystem`) in Onyx. **Keep both
as-is** — Onyx's test and `SpeciesChangeEntityEffectSystem` both name the class `FractureEffectSystem`, and a
rename is a re-sync diff for nothing.

### 2.1 External symbol table

| Symbol | Onyx site / signature | WG status |
|---|---|---|
| `SharedBodySystem` | `[Dependency]` `:18` | **SAME** (`Content.Shared.Body.Systems`) |
| `SharedBodySystem.GetBodyChildren(body)` | `:98`, `:114` with `body` of type `Entity<WoundHostComponent>` | **SAME.** WG `SharedBodySystem.Body.cs:256` takes `EntityUid?`. `Entity<T>` → `EntityUid` is a user-defined implicit conversion followed by the standard `EntityUid` → `EntityUid?` nullable conversion, which C# permits in one implicit step. If the compiler disagrees at implementation time the fix is one character: `body.Owner` |
| `BodyPartComponent.PartType` / `.Symmetry` / `.Body` | `:100,116,117,159` and `CompOrNull<BodyPartComponent>(part)?.Body` `:55,56,79,83` | **SAME** (`Content.Shared.Body.Part`) |
| `BodyPartSymmetry` | `:126-150` | **SAME** — `WG/Content.Shared/Body/Part/BodyPartSymmetry.cs:12-14` `None = 0, Left, Right` |
| `MovementSpeedModifierSystem` | `[Dependency]` `:20` | **SAME** — `WG/Content.Shared/Movement/Systems/MovementSpeedModifierSystem.cs:9-11` `namespace Content.Shared.Movement.Systems { public sealed partial class MovementSpeedModifierSystem : EntitySystem` |
| `MovementSpeedModifierSystem.RefreshMovementSpeedModifiers(uid)` | `:93` | **SAME at the call site** — WG `:80` `public void RefreshMovementSpeedModifiers(EntityUid uid, MovementSpeedModifierComponent? move = null, bool alsoFriction = false)` (the two extra params are optional and Mono-added) |
| `RefreshMovementSpeedModifiersEvent` | subscribed `:29`, handler `ref` `:96`, `args.ModifySpeed(float)` `:105` | **SAME at the call site.** Both trees declare it as a **class**, not a `[ByRefEvent]` struct: Onyx `MovementSpeedModifierSystem.cs:209`, WG `:164` `public sealed class RefreshMovementSpeedModifiersEvent : EntityEventArgs, IInventoryRelayEvent`. `ModifySpeed(float mod)` exists in both (WG `:177`). **A `ref` directed subscription on a non-`[ByRefEvent]` class is legal in RT 277** — the by-ref/by-value consistency check lives only in the *broadcast* path (`RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Broadcast.cs:217-222`); the directed overloads (`EntityEventBus.Directed.cs:243`, `:273`) do no such check. Verified by reading RT. **No edit.** |
| **Behavioural note on `ModifySpeed`** | Onyx `:223-234` splits speed-ups and slow-downs into separate accumulators (`_walkSlowdown`/`_walkSpeedup`) for its `GrantSlowdownImmunity` feature; WG `:171-175` simply multiplies. | **DIFFERENT internals, identical result here** — the fracture path only ever passes a single factor ≤ 1 per part, and both implementations multiply those together. Record as a nil-impact difference |
| `HandsComponent` | `TryComp(body, out HandsComponent? hands)` `:129` | **SAME** (`Content.Shared.Hands.Components`) |
| **`SharedHandsSystem.GetActiveHand`** | Onyx `SharedHandsSystem.cs:280` `public string? GetActiveHand(Entity<HandsComponent?> entity)` — used `:139` | **DIFFERENT** — WG `Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:177` `public Hand? GetActiveHand(Entity<HandsComponent?> entity)`. Returns the hand, not its id |
| **`SharedHandsSystem.IsHolding`** | Onyx `:438` `public bool IsHolding(Entity<HandsComponent?> ent, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out string? inHand)` — used `:135` | **DIFFERENT** — WG `:285` `public bool IsHolding(EntityUid uid, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out Hand? inHand, HandsComponent? handsComp = null)`. Out param is `Hand?`, first param `EntityUid`. (WG also has a 2-arg `:280` `IsHolding(Entity<HandsComponent?>, EntityUid?)` with no out.) |
| **`SharedHandsSystem.TryGetHand`** | Onyx `:462` `public bool TryGetHand(Entity<HandsComponent?> ent, [NotNullWhen(true)] string? handId, [NotNullWhen(true)] out Hand? hand)`, then `hand.Value.Location` `:141,144` | **DIFFERENT** — WG `:306` `public bool TryGetHand(EntityUid handsUid, string handId, [NotNullWhen(true)] out Hand? hand, HandsComponent? hands = null)`. And WG's `Hand` is a **class** (`WG/Content.Shared/Hands/Components/HandsComponent.cs:107` `public sealed class Hand`), so `hand.Value.Location` does not compile |
| **`HandLocation.FunctionalLeft` / `.FunctionalRight`** | `:146-147` | **MISSING.** WG `HandsComponent.cs:156-161` is `Left, Middle, Right` only. Onyx `HandsComponent.cs:232-242` is `Right, Middle, Left, Functional, FunctionalRight, FunctionalLeft` — note the **ordinals are also reversed**, so never compare these enums numerically across trees |
| `Hand.Location` | `hand.Value.Location` | **SAME member name** — WG `HandsComponent.cs:113` `public HandLocation Location { get; }`; different access shape (class vs Onyx's nullable struct) |
| **`OrganGotInsertedEvent` / `OrganGotRemovedEvent`** | subscribed `:34,35`, `args.Target` read `:61-71` | **PRESENT (shim, WP2).** `WG/Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs:1-9` declares `namespace Content.Shared.Body;` + `[ByRefEvent] public readonly record struct OrganGotInsertedEvent(EntityUid Target);` and the same for `…Removed`. **Byte-identical shape to Onyx** (`ONYX Content.Shared/Body/BodyComponent.cs:39-46`), including the `[ByRefEvent]` attribute. So `using Content.Shared.Body;` at `:1` resolves them with **no edit** — PLAN §2's prose ("FractureEffectsSystem.cs:34-35 gets a `// WOLFGATE` using swap") is **stale**; WP2 put the shim in the upstream namespace precisely so no swap is needed |
| `WoundFractureSystem.GetFracture` / `.TryGetProfile` | `:160,162` | **SAME** — `WG/…/WoundFractureSystem.cs:88`, `:145` `public bool TryGetProfile(Entity<WoundableComponent?> part, out FractureProfilePrototype profile)` (its body is already D8-adapted, so this file inherits the fix for free) |
| `BodyPartFunctionalitySystem.GetState / .Refresh / .RefreshPart` | `:174`, `:44,51,57,64,70`, `:88` | **SAME** — `WG/…/BodyPartFunctionalitySystem.cs:18` `public BodyPartFunctionalityState GetState(Entity<WoundableComponent?> part)`, `:57` `public void Refresh(EntityUid body)`, `:64` `public void RefreshPart(EntityUid body, EntityUid part)` |
| `WoundStatusEffectSystem.HandlePartInserted / .HandlePartRemoved` | `:64`, `:71` | **SAME** — `WG/…/WoundStatusEffectSystem.cs:119` `public void HandlePartInserted(EntityUid part)`, `:108` `public void HandlePartRemoved(EntityUid part, EntityUid body)`. Both open `if (!_net.IsServer) return;` |
| `WoundHostComponent.MobilityParts / .ManipulationParts / .PartEffectScales` | `:100,116,157` | **SAME** — `WG/…/WoundDamageComponents.cs:81` `MobilityParts = [BodyPartType.Leg, BodyPartType.Foot]`, `:84` `ManipulationParts = [BodyPartType.Arm, BodyPartType.Hand]`, `:87-92` `PartEffectScales = { [Leg]=0.5f, [Foot]=0.5f, [Hand]=0.75f }`. **`Arm` has no entry, so `GetValueOrDefault(…, 1f)` gives arms a scale of 1** — load-bearing for §8.1 |
| `FractureGradeChangedEvent` / `FractureTreatmentChangedEvent` | `:31,32,38,45` | **SAME** — `WG/…/WoundEvents.cs:70`, `:78`. Both are raised on the **part and the wound** (`WoundFractureSystem.cs:168-169`, `:182-183`), which is why the `<WoundFractureComponent, …>` pairs work |
| `WoundRemovedEvent` | `:33,52` | **SAME** — `WG/…/WoundEvents.cs:29` `public readonly record struct WoundRemovedEvent(EntityUid Part, EntityUid Wound, ProtoId<WoundPrototype> Prototype);`, raised on both part and wound at `WoundSystem.cs:368-370`. Note the handler reads `args.Part` (`:54`) — correct for a wound-scoped subscription |
| `BodyPartFunctionalityChangedEvent` | `:36,74` | **SAME** — `WG/…/WoundEvents.cs:108` |
| `GetManipulationDurationMultiplierEvent` | subscribed `:30`, constructed `:194` | **SAME, already vendored** — `WG/…/WoundEvents.cs:116` `public record struct GetManipulationDurationMultiplierEvent(EntityUid? Used, float Multiplier = 1f);`. It currently has **no raise site in WG** (grep: only this declaration), i.e. it is dead until §4 wires it |
| `FractureProfilePrototype.Grades` / `.TreatmentEffectScales`, `FractureGradeSettings.MovementModifier` / `.ManipulationModifier` | `:200-204` | **SAME** — `WG/…/WoundPrototype.cs:253,261,280,283` |
| `FractureTreatment.Mended`, `FractureGrade` | `:161` | **SAME** (`WoundDamageComponents.cs`) |
| `CompOrNull`, `TryComp`, `RaiseLocalEvent`, `EntitySystem` | Robust | **SAME** |

**Nothing in this file needs a new shim.** Everything D5 listed as missing is already supplied by phase 1.

### 2.2 Directed subscription audit (the crash check)

Every subscriber of every component in the file was enumerated across `WG/Content.Shared` +
`WG/Content.Server` (grep over `SubscribeLocalEvent<Wound…`, `<Pain…`, `<Organ…`, `<BodyPartFunctionality…`,
plus targeted greps for the two upstream events).

| Pair the port registers | Existing WG owner of that `(component, event)` | Free? |
|---|---|---|
| `<WoundHostComponent, RefreshMovementSpeedModifiersEvent>` | none. `WoundHostComponent`'s claimed events today are `MapInitEvent` (`WoundDamageProjectionSystem.cs:33`), `RejuvenateEvent` (`:34`), `BeforeDamageChangedEvent` (`WoundDamageRoutingSystem.cs:64`), `DamageDealtEvent` (`:65`), `SleepStateChangedEvent` (`WoundBleedingSystem.cs:45`), `ResolveHealingPartEvent` (`WoundHealingSystem.cs:29`), `BodyPartAddedEvent`/`BodyPartRemovedEvent` (`WolfmedBodyPartLifecycleSystem.cs:22-23`), `ComponentInit` (`WolfmedWoundHostExclusionSystem.cs:17`) | **YES** |
| `<WoundHostComponent, GetManipulationDurationMultiplierEvent>` | none (event has no subscriber at all) | **YES** |
| `<WoundFractureComponent, FractureGradeChangedEvent>` | none | **YES** |
| `<WoundFractureComponent, FractureTreatmentChangedEvent>` | none | **YES** |
| `<WoundFractureComponent, WoundRemovedEvent>` | none. `WoundFractureComponent` holds only `WoundChangedEvent` (`WoundFractureSystem.cs:24`). `WoundRemovedEvent` is held on *other* components: `WoundableComponent` (`WoundStatusEffectSystem.cs:34`), `WoundBleedingComponent` (`WoundBleedingSystem.cs:44`), `WoundInternalBleedingComponent` (`WoundInternalBleedingSystem.cs:22`) | **YES** |
| `<WoundableComponent, OrganGotInsertedEvent>` | none | **YES** |
| `<WoundableComponent, OrganGotRemovedEvent>` | none | **YES** |
| `<WoundableComponent, BodyPartFunctionalityChangedEvent>` | none. `WoundableComponent` today holds `ComponentInit` (`WoundSystem.cs:33`), `RejuvenateEvent` (`:36`), `DamageChangedEvent` (`WoundDamageProjectionSystem.cs:38`), `BeforeDamageChangedEvent` (`WoundDamageRoutingSystem.cs:66`), `PartDamageAppliedEvent` (`OrganDamageSystem.cs:31`), `Wound{Created,Changed,StateChanged,Removed}Event` (`WoundStatusEffectSystem.cs:31-34`) | **YES** |
| *(bridge, §4)* `<WoundHostComponent, GetDoAfterDelayMultiplierEvent>` | `GetDoAfterDelayMultiplierEvent` is held today by `DoAfterDelayMultiplierComponent` (`WG/Content.Shared/_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:13`) and `BodyComponent` (`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Relay.cs:11`). Different components | **YES** |

Onyx's two `OnChanged` overloads (`:38`, `:45`) and two `OnPartChanged` overloads (`:59`, `:66`) resolve by
event type — fine, nothing to change.

### 2.3 The hands block — the only rewrite (task item 1)

Onyx `:126-151`:
```csharp
        string? handId;
        if (used is { } item)
        {
            if (!_hands.IsHolding((body, hands), item, out handId))
                return false;
        }
        else
            handId = _hands.GetActiveHand((body, hands));

        if (!_hands.TryGetHand((body, hands), handId, out var hand))
            return false;

        symmetry = hand.Value.Location switch
        {
            HandLocation.Left or HandLocation.FunctionalLeft => BodyPartSymmetry.Left,
            HandLocation.Right or HandLocation.FunctionalRight => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
```
Three incompatibilities: `GetActiveHand` returns `Hand?` not `string?`; `IsHolding`'s out is `Hand?` not
`string?`; `Hand` is a class so `hand.Value` is illegal; and `HandLocation` has no `Functional*` members.

**An extension-method shim is not available.** WG's instance `Hand? GetActiveHand(Entity<HandsComponent?>)`
is applicable to the exact argument list Onyx uses, and C# prefers an applicable instance member over any
extension — so a `string? GetActiveHand(this SharedHandsSystem, …)` extension would be shadowed and never
bind. Same for `TryGetHand`. (An `IsHolding` extension *would* bind, because `out string?` cannot bind to
`out Hand?`, but shimming one of three buys nothing.) **Therefore: replace the method body.**

**Phase 1 set the precedent — follow it literally.** `WG/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:606-633`
already carries the identical rewrite for `TryGetActiveHandPart`:
```csharp
    public bool TryGetActiveHandPart(EntityUid body, out EntityUid handPart)
    {
        handPart = EntityUid.Invalid;
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

**Exact replacement for `FractureEffectsSystem.cs:126-151`:**
```csharp
    // WOLFGATE: Wolfgate's hands API hands back Hand objects, not hand-id strings (GetActiveHand returns
    // Hand?, IsHolding's out param is Hand?), Hand is a class so there is no .Value, and HandLocation has no
    // Functional* members. Same rewrite as WoundDamageRoutingSystem.TryGetActiveHandPart.
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
Overload binding for `_hands.IsHolding(body, item, out hand, hands)`: four arguments
`(EntityUid, EntityUid, out Hand?, HandsComponent)` match WG `:285` exactly; the 2-arg `:280` overload is not
applicable. No ambiguity.

**Semantic preservation.** Onyx's intent — *which* hand the do-after is using decides whether the left or the
right arm/hand chain is consulted — is fully preserved:
- **Which hand:** the `used` item's hand if the item is held, otherwise the active hand. Identical logic.
- **Which limb:** unchanged; `OnGetMultiplier` (`:114-123`) then filters `_body.GetBodyChildren(body)` by
  `ManipulationParts` (`Arm`, `Hand`) **and** `bodyPart.Symmetry == symmetry`. Wolfgate's
  `BodyPartComponent.Symmetry` is the same `Left/Right/None` enum, and Wolfgate's humanoid parts carry it, so a
  fractured left arm slows only left-handed work.
- **Loss:** Onyx's `Functional`, `FunctionalLeft`, `FunctionalRight` locations (its augment/modsuit hands,
  `ONYX Content.Server/_Onyx/Surgery/Augments/AugmentItemPanelSystem.cs:146-151`) do not exist in Wolfgate at
  all — there is no entity that could carry them. This is a **nil-behaviour loss**, not a compromise. If
  Wolfgate ever gains cybernetic extra hands they would come in as plain `Left`/`Right`/`Middle` and behave
  correctly except that `Middle` falls through to `None` — exactly as it already does in Onyx.
- `HandLocation`'s reversed ordinals between the trees are irrelevant: the switch is by name.

### 2.4 `OrganGotInsertedEvent` / `OrganGotRemovedEvent` (task item 2) — shapes match, pairs free

**Shape:** Onyx `Content.Shared/Body/BodyComponent.cs:37-46`
```csharp
[ByRefEvent]
public readonly record struct OrganGotInsertedEvent(EntityUid Target);
[ByRefEvent]
public readonly record struct OrganGotRemovedEvent(EntityUid Target);
```
WG `Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs:1-9` — `namespace Content.Shared.Body;`, both
`[ByRefEvent] public readonly record struct …(EntityUid Target);`. **Identical**, including by-ref-ness and
the member name `Target`, so `SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnPartChanged)`
with `ref OrganGotInsertedEvent args` and `args.Target` compiles unchanged.

**Raise site and semantics.** WP5/WP9's `WG/Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`:
- `:42-43` `var inserted = new OrganGotInsertedEvent(body); RaiseLocalEvent(part, ref inserted);` inside a
  `foreach (var (part, _) in _body.GetBodyPartChildren(args.Part.Owner, args.Part.Comp))` loop (`:35`).
- `:62-63` the same for `OrganGotRemovedEvent(body)` over the detached subtree (`:60`).

Both raise **on the part**, carrying **the body** as `Target` — exactly Onyx's contract
(`ONYX Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:85-95`: `new OrganGotInsertedEvent(insertedBody)`
raised on `partId`, `new OrganGotRemovedEvent(removedBody)` on removal). The subtree walk matches Onyx's
`SetSubtreeBody` (`:64-111`). **Nothing to change.**

Three real caveats for the implementer:

1. **`args.Target` is never `EntityUid.Invalid`, but it may be a terminating entity.** The lifecycle system
   already guards (`:31-32`, `:52-53` `if (TerminatingOrDeleted(body) || TerminatingOrDeleted(args.Part.Owner)) return;`),
   so the handlers are never reached mid-deletion. `FractureEffectSystem.OnPartChanged` calls
   `_functionality.Refresh(args.Target)` which does `EnsureComp<BodyPartFunctionalityComponent>` — the exact
   `DebugAssertException` WP9 fixed. **Do not weaken those guards.**
2. **The raise is server-only.** `WolfmedBodyPartLifecycleSystem` lives in `Content.Server`, so on the client
   `OnPartChanged` never fires and the client never refreshes movement speed on limb attach/detach. This is
   mitigated, not fatal: `MovementSpeedModifierComponent.WalkSpeedModifier` and `.SprintSpeedModifier` are
   `[AutoNetworkedField]` (`WG/Content.Shared/Movement/Components/MovementSpeedModifierComponent.cs:115-119`),
   so the server's value replicates and corrects the client within one state. **Recommended optional
   follow-up** (not required for correctness): a 20-line client-only `_WF` system subscribing the same
   `<WoundHostComponent, BodyPartAddedEvent/RemovedEvent>` pair and raising only the `OrganGot*` events.
   Directed subscriptions are per-`IEntityManager`, and client and server are separate instances, so this
   is **not** a duplicate registration. Flag it, do not do it blind.
3. **`_statusEffects.HandlePartInserted` / `HandlePartRemoved` currently have no caller in WG.** Verified by
   grep: the only references are their declarations (`WoundStatusEffectSystem.cs:108,119`) and the internal
   call at `:133` from `RefreshPartWounds`. Onyx drives them exclusively from `FractureEffectSystem:64,71`
   (plus `SpeciesChangeEntityEffectSystem`, not ported). **So this port closes a live gap, and there is no
   double-invocation risk.** Same for `BodyPartFunctionalitySystem.Refresh`, whose only WG caller today is
   nothing at all (`RefreshPart` is called from `WoundStatusEffectSystem.cs:147`).

### 2.5 Networking / prediction

The file has **no** `_net.IsServer` gate, by design: `OnRefreshSpeed` (`:96`) must run client-side or movement
mispredicts every tick. Its reads are all from networked components — `WoundFractureComponent`,
`WoundableComponent`, `BodyPartFunctionalityComponent` are all `[NetworkedComponent, AutoGenerateComponentState]`
(`WoundDamageComponents.cs`), and `WolfmedBodyPartComponent` is prototype-sourced so the client has it at spawn.
All three system dependencies it needs client-side (`WoundFractureSystem.GetFracture`/`.TryGetProfile`,
`BodyPartFunctionalitySystem.GetState`) are ungated. Compiles into `Content.Client` with no server-only
reference.

In practice the *other* six handlers only ever fire on the server, because every raise site is server-gated:
`FractureGradeChangedEvent`/`FractureTreatmentChangedEvent` come from `WoundFractureSystem.SetGrade`/
`.TrySetTreatment`/`.ResetTreatment`, reached only from `HandlePartDamageApplied` (`:29 if (!_net.IsServer …)`),
`OnWoundChanged` (`:77`) and `TrySetTreatment` (`:113`); `OrganGot*` from the server-only lifecycle system;
`BodyPartFunctionalityChangedEvent` from `RefreshPart`, driven by the server-gated `Refresh`.

### 2.6 `RefreshTransferredPart` (`:81-89`) is dead code in phase 2

Its only Onyx caller is `ONYX Content.Server/_Onyx/EntityEffects/Effects/Transform/SpeciesChangeEntityEffectSystem.cs:161`,
which D7/D16 defer to phase 4. **Keep it verbatim** (public API, zero cost, avoids a re-sync diff) and record
it in the manifest as intentionally uncalled.

### 2.7 `wounds.body_part_functionality_enabled` (P2-3) — what actually runs

`WG/Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs:7-8`:
`CVarDef.Create("wounds.body_part_functionality_enabled", false, CVar.SERVER | CVar.ARCHIVE)` — confirmed
**false**, as P2-3 requires. Consequences for `GetEffect` (`:152-189`):
- The **fracture branch** (`:160-170`) is unaffected by the cvar and is the live path. ✔ P2-3's "fracture
  movement/hand multipliers apply regardless" holds.
- The **fallback branch** (`:172-188`) calls `_functionality.GetState`, which returns `Functional` for
  everything **except** a part with `CyberneticsComponent.Disabled` — that check sits *above* the cvar gate
  (`BodyPartFunctionalitySystem.cs:19-23`). So a disabled cybernetic limb still yields `Disabled` → mobility
  `0f`, manipulation `2.5f`. That path is reachable today via `Content.Shared._Shitmed.Cybernetics`. Worth an
  explicit assertion in the phase-2 test suite, or at minimum a note.
- `BodyPartFunctionalityChangedEvent` will essentially never fire (state stays `Functional`, and `RefreshPart`
  early-returns on no-change), so `OnFunctionalityChanged` (`:73-76`) is effectively inert. Expected.

### 2.8 Difficulty

**Medium.** One method rewritten (26 lines → 26 lines), one comment block; everything else is verbatim. The
risk is not in the code, it is in the test numbers (§7).

---

## 3. `// WOLFGATE` edit list — complete and exhaustive

| # | File | Site | Edit |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` | using block | `+ using Content.Shared._WF.Wolfmed.Body; // WOLFGATE: D8 keeps Onyx's part fields on WolfmedBodyPartComponent.` |
| 2 | same | dependency block (after `:14`) | `+ [Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8, Onyx's extra part fields.` |
| 3 | same | `:24` | `bodyPart.FractureProfile` → `_wfPart.Get(part).FractureProfile`, with the 1-line `// WOLFGATE` reason above it |
| 4 | `Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs` | `:126-151` | whole `TryGetUsedHandSymmetry` body replaced per §2.3, 3-line `// WOLFGATE` header |
| — | same | `:1` `using Content.Shared.Body;` | **NO EDIT.** Resolves against WP2's `OnyxBodyEvents.cs`. PLAN §2's "using swap" note is stale |
| — | same | `:29` `RefreshMovementSpeedModifiersEvent` ref subscription | **NO EDIT.** Legal in RT 277 (§2.1) |
| — | same | `:30`, `:194` `GetManipulationDurationMultiplierEvent` | **NO EDIT.** Already vendored at `WoundEvents.cs:116`. It needs a *raiser* — §4 |
| 5 | *(new)* `Content.Shared/_WF/Wolfmed/DoAfter/WolfmedFractureDoAfterSystem.cs` | new file | §4.3 |
| — | `Content.Shared/DoAfter/SharedDoAfterSystem.cs` | — | **NOT TOUCHED** (PLAN §3 forbids it; §4 shows how to avoid it) |
| — | `Resources/**` | — | **NO CHANGES.** WP7 already shipped everything (§1.4, §6) |

**Upstream files touched: zero.** (`WolfmedFractureDoAfterSystem.cs` is new `_WF` code, not an upstream edit.)

---

## 4. The do-after delay multiplier (task item 3) — PLAN's instruction, verified and corrected

### 4.1 Which event Onyx subscribes, and where it is raised

`FractureEffectsSystem.cs:30` subscribes `GetManipulationDurationMultiplierEvent`, declared at
`ONYX Content.Shared/_Onyx/Wounds/WoundEvents.cs:116`:
```csharp
public record struct GetManipulationDurationMultiplierEvent(EntityUid? Used, float Multiplier = 1f);
```
Its only Onyx raise site outside the file itself is `ONYX Content.Shared/DoAfter/SharedDoAfterSystem.cs:262-264`,
inside `TryStartDoAfter`, *before* `ProcessDuplicates`:
```csharp
        var manipulation = new Content.Shared._Onyx.Wounds.GetManipulationDurationMultiplierEvent(args.Used); // <Onyx-WoundFunctionality>
        RaiseLocalEvent(args.User, ref manipulation); // <Onyx-WoundFunctionality>
        args.Delay *= manipulation.Multiplier; // <Onyx-WoundFunctionality>
```
The event type is **already vendored in WG** (`WG/Content.Shared/_Onyx/Wounds/WoundEvents.cs:116`, verbatim)
but has **no raiser**, so it is currently dead.

### 4.2 Wolfgate's existing `GetDoAfterDelayMultiplierEvent` — definition, raise site, shape

**Definition** — `WG/Content.Shared/_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:33-38`:
```csharp
public sealed class GetDoAfterDelayMultiplierEvent(float multiplier = 1f) : EntityEventArgs, IBodyPartRelayEvent
{
    public float Multiplier = multiplier;

    public BodyPartType TargetBodyPart => BodyPartType.Hand;
}
```
**Raise site** — `WG/Content.Shared/DoAfter/SharedDoAfterSystem.cs:208-215`, inside `TryStartDoAfter`, *after*
`ProcessDuplicates`:
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
**Existing subscribers:** `<DoAfterDelayMultiplierComponent, GetDoAfterDelayMultiplierEvent>`
(`DoAfterDelayMultiplierSystem.cs:13`) and `<BodyComponent, GetDoAfterDelayMultiplierEvent>`
(`WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Relay.cs:11`, which relays it to every
`BodyPartType.Hand` child as `BodyPartRelayedEvent<GetDoAfterDelayMultiplierEvent>` — `RelayEvent`,
`SharedBodySystem.Relay.cs:37-45`).

### 4.3 Verdict on PLAN's instruction: **workable, but not a drop-in — it loses `Used`**

PLAN §3's "Explicitly NOT touched" row says *"`SharedDoAfterSystem.cs` (Wolfgate's Goobstation
`GetDoAfterDelayMultiplierEvent` at `:207-214` already does the job — subscribe to it from `_WF` instead)"*.
The line reference is correct (now `:208-215`). The claim *"already does the job"* is **only partly true**, on
two counts, both verified:

1. **`GetDoAfterDelayMultiplierEvent` carries no `Used`.** Its only member is `Multiplier`. Onyx's whole
   symmetry decision starts from `args.Used`. Without it the best available answer is the **active hand**.
2. **`args.MultiplyDelay` gates the raise**, and Wolfgate has 20+ do-afters that opt out — including
   `Content.Server/Medical/DefibrillatorSystem.cs:148`, `Nutrition/EntitySystems/FoodSystem.cs:220`,
   `DrinkSystem.cs:232`, `_EE/Carrying/CarryingSystem.cs:276`, `_White/Standing/SharedLayingDownSystem.cs:144`,
   `Shared/RCD/Systems/RCDSystem.cs:230`, `Shared/Mech/EntitySystems/SharedMechSystem.cs:569`. Onyx applies the
   fracture multiplier to every do-after unconditionally. Those interactions will be unaffected by a broken arm.
   Record as a phase-2 balance deviation.

**Do not subscribe `BodyPartRelayedEvent<GetDoAfterDelayMultiplierEvent>` on `WoundableComponent` as an
alternative.** The relay targets `BodyPartType.Hand` only (so fractured *arms* are missed, and Onyx's
`ManipulationParts` is `[Arm, Hand]`) and it fires for **both** hands, which would double-apply.

**Recommended design — a `_WF` bridge, zero upstream edits, keeps the vendored file verbatim:**

`WG/Content.Shared/_WF/Wolfmed/DoAfter/WolfmedFractureDoAfterSystem.cs`
```csharp
using Content.Shared._Goobstation.DoAfter;
using Content.Shared._Onyx.Wounds;

namespace Content.Shared._WF.Wolfmed.DoAfter;

/// <summary>Feeds Onyx's fracture manipulation multiplier into Wolfgate's do-after delay event.</summary>
public sealed class WolfmedFractureDoAfterSystem : EntitySystem
{
    [Dependency] private FractureEffectSystem _fractureEffects = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        // Onyx raises its own GetManipulationDurationMultiplierEvent from SharedDoAfterSystem; PLAN §3 keeps
        // that file untouched, so we hang off Wolfgate's existing Goobstation multiplier event instead.
        // Pair verified free: the event is otherwise held by DoAfterDelayMultiplierComponent and BodyComponent.
        SubscribeLocalEvent<WoundHostComponent, GetDoAfterDelayMultiplierEvent>(OnGetDelayMultiplier);
    }

    private void OnGetDelayMultiplier(EntityUid uid, WoundHostComponent component,
        GetDoAfterDelayMultiplierEvent args)
    {
        // Wolfgate's event carries no Used item, so the active hand decides the symmetry.
        args.Multiplier *= _fractureEffects.GetDurationMultiplier(uid);
    }
}
```
`GetDurationMultiplier(EntityUid body, EntityUid? used = null)` is `FractureEffectsSystem.cs:192-197` and
raises `GetManipulationDurationMultiplierEvent` on the body, so the vendored handler runs unmodified. A
by-value `ComponentEventHandler<TComp, TEvent>` subscription is used to match every other subscriber of this
class event (`EntityEventBus.Directed.cs:243`).

**Alternative, if the user wants Onyx's exact `used`-item semantics** (escalation required — it edits an
upstream file PLAN forbids, though only in the fork-local Goobstation file plus one line of
`SharedDoAfterSystem.cs`):
```csharp
// DoAfterDelayMultiplierSystem.cs:33-38
public sealed class GetDoAfterDelayMultiplierEvent(float multiplier = 1f) : EntityEventArgs, IBodyPartRelayEvent
{
    public float Multiplier = multiplier;
    public EntityUid? Used;   // WOLFGATE: Wolfmed needs the used item to pick the fractured hand.
    ...
}
// SharedDoAfterSystem.cs:211
    var delayMultiplierEv = new GetDoAfterDelayMultiplierEvent { Used = args.Used }; // WOLFGATE
```
then the bridge passes `args.Used` through. Two lines, fully Onyx-faithful. **Put this to the user; default to
the zero-edit version.**

---

## 5. Movement speed (task item 4) — how Onyx applies it, and what WG has

**Onyx does not use `MovementModStatusSystem` or any status effect for fractures.** It applies the modifier
directly through the vanilla refresh event:
- `FractureEffectsSystem.cs:29` subscribes `<WoundHostComponent, RefreshMovementSpeedModifiersEvent>`.
- `OnRefreshSpeed` (`:96-107`) walks `GetBodyChildren`, filters on `WoundHostComponent.MobilityParts`
  (`[Leg, Foot]`) and calls `args.ModifySpeed(1f - (1f - modifier) * partScale * treatmentScale)` once per
  mobility part.
- Refreshes are pushed by `Refresh(EntityUid?)` (`:91-94`) → `_movement.RefreshMovementSpeedModifiers(uid)`,
  called from all six change handlers.

**Wolfgate has exactly this API.**
- `WG/Content.Shared/Movement/Systems/MovementSpeedModifierSystem.cs:80-90` — `RefreshMovementSpeedModifiers(EntityUid uid, MovementSpeedModifierComponent? move = null, bool alsoFriction = false)`,
  which raises `new RefreshMovementSpeedModifiersEvent()` by value on `uid`.
- `:164-181` — the event class, `WalkSpeedModifier`/`SprintSpeedModifier` starting at `1.0f`, with
  `ModifySpeed(float walk, float sprint)` and `ModifySpeed(float mod)`.
- `:115-119` — `MovementSpeedModifierComponent.WalkSpeedModifier` / `.SprintSpeedModifier` are
  `[AutoNetworkedField]`, so the computed result replicates.

**Conclusion: no `MovementModStatusEffectComponent`, no `MovementModStatusSystem`, and no
`StatusEffectSlowdown` prototype chain is required by either of these two files.** P2-1 lists those as
*"if wounds need it"* — for fractures they are not needed. They belong to whichever other phase-2 item wants
them (pain slowdown / `MobStandStatusEffectBase`), and should be scoped there, not here.

Only difference worth recording: Onyx's `ModifySpeed` keeps separate speed-up/slow-down accumulators for its
`GrantSlowdownImmunity` feature (`ONYX MovementSpeedModifierSystem.cs:213-245`); Wolfgate's multiplies
directly. Identical outcome for the fracture path, which only ever contributes factors ≤ 1.

---

## 6. Alert, textures, locale, prototypes (task items 5 and 6) — all already present

| Item | Status |
|---|---|
| `BrokenBones` alert prototype | **Already shipped, WP7.** `WG/Resources/Prototypes/_Onyx/Alerts/alerts.yml`, identical to `ONYX …/alerts.yml:34-40`. **No `category:`** on this entry, so no `AlertCategoryPrototype` is involved and `ClearAlertCategory` is never called |
| `fracture.rsi` | **Already shipped, WP7.** `WG/Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{brokenbones.png,meta.json}`. `meta.json` byte-identical to Onyx: `CC-BY-SA-3.0`, `"Taken from tgstation, redrawn by darkrell"`, 32×32, one state `brokenbones`. Licence clean, already in the manifest |
| `fractures.ftl` | **Already shipped, WP7.** `WG/Resources/Locale/en-US/_Onyx/medical/fractures.ftl`, 2 keys, identical to Onyx |
| `AlertsSystem` calls made | **`ShowAlert(uid, alert)` and `ClearAlert(uid, alert)` only.** Both SAME at the call site (§1.1). **`UpdateAlert` is not used by these files** — WP2's shim is untouched by phase 2's fracture work |
| `OrganicFractureProfile` | **Already shipped, WP7.** `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:38-76` — byte-identical to `ONYX …/wounds.yml:153-191` including `alert: BrokenBones`, `alertMinimumGrade: Simple`, `alertHiddenTreatments: [Mended]`, `treatmentEffectScales {None:1, Reduced:0.25, Mended:0}` and all four grades |
| Which parts reference it | **Already shipped, WP7.** `WG/Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` gives `fractureProfile: OrganicFractureProfile` to `WolfmedBaseTorso`, `WolfmedBaseHead`, `WolfmedBase{Left,Right}Arm`, `WolfmedBase{Left,Right}Hand`, `WolfmedBase{Left,Right}Leg` (and the feet — confirm the tail of the file at implementation time). These abstracts are added to each upstream `Base*` part's `parent:` list |
| `BoneFractureWound` | **Already shipped.** Referenced by `OrganicFractureProfile.wound` and listed in `OrganicBodyPartProfile.supportedWounds` (`wounds.yml:20`) |

**Nothing to port in Resources. Zero YAML changes.**

---

## 7. Test port — `WoundFractureTest.EffectsRefreshOnTreatmentHealingAndDetachTest` (P2-2)

WP9 skipped this test with a marker at `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs:117-118`.
The fixture it needs is already there (`WoundFractureBodyGraph` / `WoundFractureBody`, `:20-56`) — torso +
left arm + left leg, `- type: MovementSpeedModifier`, `- type: WoundHost`.

**Every numeric literal in Onyx's version of this test is stale and must be re-derived.** This is the same
class of defect WP9 documented in its §3.2: `GradeBoundariesAreDeterministicTest` in the *same Onyx file*
asserts grade thresholds 15/30/50/75 while `OrganicFractureProfile` at the pin declares 20/35/50/60. The
effects test was written against the same superseded profile.

Derivation against the **current** data (`wounds.yml:56-76`, `WoundDamageComponents.cs:87-92`):

| Step | Onyx literal | Derived value for WG | Working |
|---|---|---|---|
| Leg takes 75 Blunt → fracture | (implicit) | severity 75, grade `Comminuted` | threshold 60 ≤ 75; matches the already-passing `PostArmorHitAndTreatmentPreconditionsTest` |
| `WalkSpeedModifier` after leg fracture | `0.4f` | **`0.5f`** | `Comminuted.movementModifier = 0`; `PartEffectScales[Leg] = 0.5`; `TreatmentEffectScales[None] = 1` → `ModifySpeed(1 - (1-0)*0.5*1) = ModifySpeed(0.5)` |
| `GetDurationMultiplier(body)` after arm fracture | `2f` | **`0.75f`** | `Comminuted.manipulationModifier = 0.75`; `Arm` is absent from `PartEffectScales` so its scale is `1f`; → `1 + (0.75-1)*1*1 = 0.75` |
| after `TryMend(arm fracture)` | `1f` | **`1f`** | `removeWoundWhenMended: true` → wound removed → no fracture → fallback `Functional` → `1f` |
| after `TryDetachPart(leg)` | `1f` | **`1f`** | leg leaves `GetBodyChildren`, and `BodyPartRemovedEvent` → `WolfmedBodyPartLifecycleSystem.OnPartRemoved` → `OrganGotRemovedEvent` → `Refresh` → `RefreshMovementSpeedModifiers` |

**Treat these as predictions, not facts.** P2-4's rule applies: run the test and read the real numbers before
committing the literals, and put the derivation in a `// WOLFGATE` comment at each assertion, exactly as WP9 did.

Required test adaptations beyond the numbers:
- `graph.TryDetachPart(leg)` → `WolfmedBodySystem.TryDetachPart` (PLAN §2.7), as every other ported fixture does.
- `entityManager.System<FractureEffectSystem>()` — the class name, not the file name.
- add `using Content.Shared.Movement.Components;` for `MovementSpeedModifierComponent` (Onyx's version has it
  at `:10`; WG's current file does not).
- `[TestOf(typeof(WoundFractureSystem))]` can stay.

**Additional assertions worth adding while the file is open:**
- **Fracture alert:** after the leg fracture, `alerts.IsShowingAlert(body, "BrokenBones")` is true
  (`AlertsSystem.cs:39`); after `TryMend`, false. This is the only coverage `FractureAlertSystem` will get and
  it directly exercises `AlertHiddenTreatments`/`AlertMinimumGrade`.
- **Symmetry:** fracture the *left* arm, put an item in a right hand, assert `GetDurationMultiplier(body, item)`
  is `1f`. That is the assertion that proves the §2.3 rewrite preserved Onyx's semantics — Onyx has no such
  test. (Needs a hand part and a `HandsComponent` on the fixture; the current `WoundFractureBodyGraph` has
  neither, so either extend the graph with `LeftHandHuman`/`RightArmHuman`/`RightHandHuman` or declare a
  second body id. Note WP9's warning: `[TestPrototypes]` ids are pool-global — do not reuse existing ids.)
- **Do-after bridge:** start a do-after on a wound host with a fractured arm and assert the delay scaled.
  Optional; the unit-level `GetDurationMultiplier` assertion covers the maths.

---

## 8. Findings the implementer must not skip

### 8.1 Onyx's `manipulationModifier` values are inverted relative to the formula (balance, flag to the user)

`OnGetMultiplier` computes `args.Multiplier *= 1f + (modifier - 1f) * partScale * treatmentScale` — a
`modifier` **above** 1 means *slower*. The C# defaults agree:
`WoundPrototype.cs:261-267` `Hairline = new(8, 0.9f, 1.1f)`, `Simple = new(15, 0.8f, 1.25f)`,
`Displaced = new(25, 0.6f, 1.5f)`, `Comminuted = new(40, 0.4f, 2f)` — manipulation 1.1 → 2.0, i.e. up to 2×
slower. The fallback branch agrees too (`FractureEffectsSystem.cs:182-186`: `Disabled => 2.5f`,
`Impaired => 1.25f`).

But `OrganicFractureProfile` in YAML (`wounds.yml:56-76`, identical in both trees) declares
`manipulationModifier: 0.92 / 0.84 / 0.75 / 0.75` — **below** 1. With the shipped data **a shattered arm makes
every do-after 25 % faster.** This also explains Onyx's stale `2f` test literal: the test was written against
the C# defaults, i.e. against the intended direction.

D4 says ship Onyx's defaults, so **do not change the YAML unilaterally.** Flag it to the user with this
evidence and let them choose: ship as-is and record the deviation, or set the four values to the C# defaults
(1.1 / 1.25 / 1.5 / 2.0) behind a `# WOLFGATE` balance comment. The movement side is unaffected —
`movementModifier` is correctly below 1 and the mobility formula `1 - (1 - modifier) * …` reads it that way.

### 8.2 The client never sees limb attach/detach refreshes (§2.4 item 2)

Server-only `OrganGot*` raise → one-state mispredict on limb loss/gain. Mitigated by `[AutoNetworkedField]`
on the speed modifiers. Optional client-side `_WF` system described in §2.4. Not a blocker.

### 8.3 `MultiplyDelay = false` do-afters escape the fracture penalty (§4.3 item 2)

20+ call sites, including defib, eating, drinking, carrying, lying down, RCD, mech. Record as a deviation.

### 8.4 Do not weaken the `TerminatingOrDeleted` guards

`FractureEffectSystem.OnPartChanged` reaches `EnsureComp<BodyPartFunctionalityComponent>` via
`_functionality.Refresh` → `RefreshPart`. That is the same shape as the `DebugAssertException` WP9 fixed in
`WolfmedBodyPartLifecycleSystem.cs:31,52`. Re-run `EntityTest.SpawnAndDeleteAllEntities*` and
`PrototypeSaveTest` after this work lands.

### 8.5 PLAN §2's "using swap" note for `FractureEffectsSystem.cs:34-35` is stale

WP2 declared the shim in `namespace Content.Shared.Body`, so Onyx's `using Content.Shared.Body;` at `:1`
resolves as-is. No swap. Worth correcting in `Docs/Wolfmed/WOLFMED_PLAN.md:501` when the phase-2 doc is written.

---

## 9. Ordered file list

| # | Action | Path | Kind | Depends on |
|---|---|---|---|---|
| 1 | vendor + 3 edits | `WG/Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` | `_Onyx`, **modified** | `WolfmedBodyPartSystem` (WP4) — present |
| 2 | vendor + 1 rewrite | `WG/Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs` | `_Onyx`, **modified** | #1 (it is a `[Dependency]`), `OnyxBodyEvents.cs` (WP2) — present |
| 3 | new | `WG/Content.Shared/_WF/Wolfmed/DoAfter/WolfmedFractureDoAfterSystem.cs` | `_WF`, **new** | #2 |
| 4 | build gate | `dotnet build Content.Server` **and** `Content.Client` in `DebugOpt` | — | #1–#3. The client build is the one that matters: it proves the shared file has no server-only reference |
| 5 | restore test | `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` — replace the `:117-118` skip marker with the ported test, numbers re-derived per §7 | test, **adapted** | #1–#3 |
| 6 | new assertions | same file — fracture-alert assertion, hand-symmetry assertion (§7) | test | #5 |
| 7 | smoke | `dotnet test --filter "FullyQualifiedName~DockTest"` **first** (project memory: `db.ef` sqlite warnings fail every pair test), then `_Onyx.Wounds|_Onyx.Body|Wolfmed`, then `PrototypeSaveTest\|EntityTest` | — | #5–#6 |
| 8 | docs | `Docs/Wolfmed/WOLFMED_MANIFEST.md` — split the combined row at `:77` (`{AmputationSystem,FractureEffectsSystem,FractureAlertSystem}.cs … skipped`) into an `AmputationSystem` row that stays `skipped / WP11` and two new `modified / WP10` rows; add the `_WF` bridge row; update the `WoundFractureTest.cs` row at `:129` and the deferred-test list at `:464`. Add `Docs/Wolfmed/WOLFMED_PLAN2.md` (P2-5) | docs | all |

**No YAML, no locale, no texture, no upstream C# in this work package.**

---

## 10. Difficulty rating

| Unit | Rating | Why |
|---|---|---|
| `FractureAlertSystem.cs` | **Light** | 3 mechanical edits, precedent already in the tree at `WoundFractureSystem.cs:151`. Assets all present |
| `FractureEffectsSystem.cs` | **Medium** | One 26-line method rewritten with an exact precedent at `WoundDamageRoutingSystem.cs:606-633`. All 8 subscription pairs verified free. No new shims |
| `WolfmedFractureDoAfterSystem.cs` | **Light** | ~25 lines, one free pair |
| Test port | **Medium** | Trivial mechanically, but every Onyx literal is stale and must be measured, not copied (§7). A hand-symmetry fixture has to be built to cover the §2.3 rewrite |
| **Overall** | **Medium** | Zero upstream edits, zero new shims, zero assets. The only judgement calls are §4.3 (`Used`) and §8.1 (inverted balance) — both for the user, neither blocking |

**Blocker: none.**
