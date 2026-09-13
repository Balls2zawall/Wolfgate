# Phase 2 — StatusEffectNew consumers (movement-mod slowdown, MobStandStatusEffectBase, wounds.yml orphans, dropped relays, alerts)

**Scope:** DECISIONS.md P2-1's `MovementModStatusEffectComponent` / trimmed `MovementModStatusSystem` / `StatusEffectSlowdown` chain / `_Onyx/StatusEffects/wounds.yml` / `MobStandStatusEffectBase` items, plus the WP1 relay drop and the alert wiring for phase 2.
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. **WG:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`. Read-only analysis; no files touched.

## Verdict up front

**None of it is needed.** Tracing every `EntProtoId` reachable from `PainSystem.cs`, `WoundStatusEffectSystem.cs`, `WoundBehaviors.cs`, `FractureEffectsSystem.cs` and `WoundFractureSystem.cs` at the pin terminates in zero references to `MovementModStatusEffectComponent`, `MovementModStatusSystem`, the `StatusEffectSlowdown` chain, `MobStandStatusEffectBase`, or either entry in `_Onyx/StatusEffects/wounds.yml`. Fracture movement/manipulation penalties are applied directly (no status effect involved at all). WP1 already reached the same conclusion for `MobStandStatusEffectBase` independently (`Resources/Prototypes/Entities/StatusEffects/misc.yml:31-32`, a standing `// WOLFGATE` comment dropping it). **Recommendation: do not port any of P2-1's conditional "if wounds need it" items; strike them from the phase-2 file list.**

The one real blocker found in this pass is unrelated to StatusEffectNew: **`FractureEffectsSystem.cs`'s hands block will not compile against WG's `SharedHandsSystem`** — same class of API mismatch WP5 already fixed once in `WoundDamageRoutingSystem.TryGetActiveHandPart`. See §6.

---

## 1. MovementModStatusEffectComponent + MovementModStatusSystem

Not in the sparse checkout (`Content.Shared/Movement/{Components,Systems}/` is core-engine-adjacent content, not `_Onyx`), read via `git -C C:/tmp/onyx show HEAD:<path>` per the pin rules.

**`Content.Shared/Movement/Components/MovementModStatusEffectComponent.cs`** (full file, 22 lines):
```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(MovementModStatusSystem))]
public sealed partial class MovementModStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField] public float SprintSpeedModifier = 0.5f;
    [DataField, AutoNetworkedField] public float WalkSpeedModifier = 0.5f;
}
```

**`Content.Shared/Movement/Systems/MovementModStatusSystem.cs`** (full file, ~220 lines) subscribes:
- `<MovementModStatusEffectComponent, StatusEffectAppliedEvent>`
- `<MovementModStatusEffectComponent, StatusEffectRemovedEvent>`
- `<MovementModStatusEffectComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>`
- `<FrictionStatusEffectComponent, StatusEffectAppliedEvent>`
- `<FrictionStatusEffectComponent, StatusEffectRemovedEvent>`
- `<FrictionStatusEffectComponent, StatusEffectRelayedEvent<RefreshFrictionModifiersEvent>>`
- `<FrictionStatusEffectComponent, StatusEffectRelayedEvent<TileFrictionEvent>>`

and exposes `TryAddMovementSpeedModDuration`/`TryUpdateMovementSpeedModDuration`/`TryUpdateMovementStatus`/`TryAddFrictionModDuration`/`TryUpdateFrictionModDuration`, each keyed off one of five hard-coded `EntProtoId` constants: `ReagentSpeedStatusEffect`, `VomitingSlowdownStatusEffect`, `TaserSlowdownStatusEffect`, `FlashSlowdownStatusEffect`, `StatusEffectFriction`.

### Symbol table vs WG

| Symbol | WG status | Evidence |
|---|---|---|
| `MovementModStatusEffectComponent` | **MISSING** | zero hits, `Content.Shared/`+`Content.Server/` |
| `MovementModStatusSystem` | **MISSING** | zero hits |
| `FrictionStatusEffectComponent` | **MISSING** | zero hits — porting `MovementModStatusSystem` verbatim would drag this file in too, undocumented in P2-1 |
| `RefreshMovementSpeedModifiersEvent` | SAME | native Wolfgate event, 31 files reference it already |
| `RefreshFrictionModifiersEvent` | SAME | native Wolfgate event, 6 files |
| `TileFrictionEvent` | SAME | native Wolfgate event, 8 files |
| `StatusEffectAppliedEvent` / `StatusEffectRemovedEvent` | SAME | WP1's `StatusEffectsSystem` core |
| `StatusEffectRelayedEvent<T>` | SAME | WP1's `StatusEffectSystem.Relay.cs` |
| `ReagentSpeedStatusEffect`, `VomitingSlowdownStatusEffect`, `TaserSlowdownStatusEffect`, `FlashSlowdownStatusEffect`, `StatusEffectFriction` (prototypes) | **MISSING** | none ported, none referenced by any phase-1/2 file |

### EntProtoId trace from the five named files (the actual test)

```
grep -n "EntProtoId" on all five files, Onyx pin:
  WoundBehaviors.cs:102          public EntProtoId<StatusEffectComponent> StatusEffect;   (a DATA FIELD TYPE, not a literal id)
  WoundStatusEffectSystem.cs:199 EntProtoId<StatusEffectComponent> statusEffect)           (parameter of the same type)
  PainSystem.cs                  — no EntProtoId at all
  FractureEffectsSystem.cs       — no EntProtoId at all
  WoundFractureSystem.cs         — no EntProtoId at all
```

The only `EntProtoId` surface in the whole five-file set is `WoundStatusEffectBehavior.StatusEffect` — a **field**, populated per-wound from `wounds.yml`'s `WoundBehavior` list. It is not a literal reference to any concrete prototype in these files; the concrete id comes from data. Searching that data (`Resources/Prototypes/_Onyx/Wounds/wounds.yml`, 1064 lines) for the tag that would populate it:

```
grep -ni "statuseffect" Resources/Prototypes/_Onyx/Wounds/wounds.yml   → 0 hits
grep -n "WoundStatusEffectBehavior" Resources/Prototypes/**/*.yml       → 0 hits anywhere in the repo
```

**No wound in the pinned `wounds.yml` uses `WoundStatusEffectBehavior`.** The field type exists and compiles (ported verbatim in WP4, `WoundBehaviors.cs`), `WoundStatusEffectSystem.cs` (also ported verbatim, WP4) is fully wired to consume it — but there is no data instance anywhere that ever populates `.StatusEffect`, so the code path is dead at the pin. This closes the loop: even the generic "wound → status effect" bridge has no live wound behind it, so the specific slowdown-status-effect chain has nothing to reach it through.

`FractureEffectsSystem.cs` (traced fully, 205 lines) confirms the independent path: mobility/manipulation penalties are applied by `OnRefreshSpeed`/`OnGetMultiplier` handlers subscribed directly on `WoundHostComponent` to `RefreshMovementSpeedModifiersEvent`/`GetManipulationDurationMultiplierEvent`, calling `args.ModifySpeed(...)`/`args.Multiplier *= ...` straight from `FractureGrade` lookups (`WolfmedBodyPartComponent`/`FractureProfilePrototype` data, both already ported). **No status effect entity is created, applied, or queried anywhere in this file.**

### Verdict

**Wounds/fractures/pain do not need `MovementModStatusEffectComponent` or `MovementModStatusSystem` at the pin.** Porting them would also silently require `FrictionStatusEffectComponent` (undocumented dependency) for zero behavioural gain in phase 2. **Recommendation: skip both files entirely; strike "trimmed MovementModStatusSystem" from P2-1.**

---

## 2. `StatusEffectSlowdown` / `MobStandStatusEffectBase` chains

`Resources/Prototypes/Entities/StatusEffects/movement.yml` (Onyx, full file) and `misc.yml` (Onyx, full file) read directly.

### `StatusEffectSlowdown` descendants at the pin

| id | parent | components |
|---|---|---|
| `StatusEffectSlowdown` (abstract) | `MobStatusEffectDebuff` | `StatusEffect` (whitelist `MobState`+`MovementSpeedModifier`, blacklist tag `SlowImmune`), `MovementModStatusEffect` |
| `VomitingSlowdownStatusEffect` | `StatusEffectSlowdown` | (name only) |
| `TaserSlowdownStatusEffect` | `StatusEffectSlowdown` | (name only) |
| `FlashSlowdownStatusEffect` | `StatusEffectSlowdown` | (name only) |
| `StatusEffectStaminaLow` | `StatusEffectSlowdown` | (name only) |
| `StatusEffectSpeed` (abstract) | `MobStatusEffectBase` | `StatusEffect`, `MovementModStatusEffect` |
| `ReagentSpeedStatusEffect` | `StatusEffectSpeed` | (name only) |

Every concrete descendant is consumed exclusively by vomiting, tasers, flashes, stamina-low and reagents — **none of which are in P2-1's scope**, and none of which are referenced from `PainSystem.cs`/`WoundStatusEffectSystem.cs`/`WoundBehaviors.cs`/`FractureEffectsSystem.cs`/`WoundFractureSystem.cs` (§1). Every one of these carries `MovementModStatusEffect` (§1: MISSING) as its functional component, so even in isolation each concrete prototype would fail component registration.

`Resources/Prototypes/_Onyx/StatusEffects/wounds.yml`'s two entries (`StatusEffectBurnSlowdown`, `StatusEffectWoundImpairment`) also parent `StatusEffectSlowdown` — covered in §3, also orphaned.

**Component check:**

| Component tag | Registered in WG? | Evidence |
|---|---|---|
| `StatusEffect` | yes | WP1, `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs` |
| `MovementModStatusEffect` | **no** | §1 |

Conclusion: **zero concrete prototypes from this chain are needed by phase 2.** If a later phase needs taser/flash/vomit slowdowns, that is a separate, self-contained port of `MovementModStatusEffectComponent`+`MovementModStatusSystem`+`FrictionStatusEffectComponent` plus these five entities — not a wound/fracture/pain dependency.

### `MobStandStatusEffectBase`

```yaml
- type: entity
  parent: MobStatusEffectDebuff
  id: MobStandStatusEffectBase
  abstract: true
  components:
  - type: StatusEffect
    whitelist: { components: [MobState, StandingState], requireAll: true }
    blacklist: { tags: [KnockdownImmune] }
```

`git grep -n "parent:.*MobStandStatusEffectBase"` across every `Resources/Prototypes/Entities/StatusEffects/*.yml` returns **zero concrete descendants at the pin** — the base is declared and never used by anything (abstract, no children, no direct component consumers). `git grep -n "Knockdown\|Standing\|StandUp"` across all five traced wound/fracture/pain files also returns zero hits — nothing in scope ever downs, stands up, or checks `StandingState`/`KnockdownImmune` for a wound host.

WP1 independently reached the identical conclusion and already recorded it: `Resources/Prototypes/Entities/StatusEffects/misc.yml:31-32` in WG today reads
```yaml
# WOLFGATE: MobStandStatusEffectBase and every concrete status effect entity in Onyx's file are
# dropped: they need the KnockdownImmune tag and status-effect components Wolfgate does not have.
```
There is no `Resources/Prototypes/_WF/Wolfmed/tags.yml` in WG yet (`Resources/Prototypes/_WF/Wolfmed/` currently holds only `Body/`), and there is no pre-existing `KnockdownImmune` tag in WG (`grep -rn "KnockdownImmune" Resources/Prototypes/` hits only the *comment* quoted above and an unrelated `FTLKnockdownImmune` tag used by NF/diona FTL code — a different concept, not a collision but also not reusable).

**Verdict: nothing in scope needs `MobStandStatusEffectBase`. Do not propose the `KnockdownImmune` tag** — there is no consumer to justify it, and adding an unused tag+base would be exactly the kind of speculative scaffolding the port has otherwise avoided.

---

## 3. `_Onyx/StatusEffects/wounds.yml`

Full file (8 lines):
```yaml
- type: entity
  parent: StatusEffectSlowdown
  id: StatusEffectBurnSlowdown
  name: burn slowdown

- type: entity
  parent: StatusEffectSlowdown
  id: StatusEffectWoundImpairment
  name: wound impairment
```

`git grep -n "StatusEffectBurnSlowdown\|StatusEffectWoundImpairment"` across the **entire** Onyx repo returns exactly these two definition lines and nothing else — no `WoundStatusEffectBehavior` in any wound prototype references either id (confirmed in §1: zero uses of that behavior type anywhere), no C# file references either id, no reagent or system applies them. **Both are orphaned at the pin**, exactly as DECISIONS.md's Phase 2 section already suspected ("Onyx's two entries are orphaned at the pin; skip if so").

**Verdict: skip this file entirely.** It also inherits from `StatusEffectSlowdown` (§2: needs `MovementModStatusEffect`, MISSING), so it could not even register as a component-complete prototype without also porting §1's system — which would still have nothing to apply it.

---

## 4. Dropped `StatusEffectSystem.Relay.cs` lines vs phase-2 consumers

Read `Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs` as it stands in WG today (WP1's edit). Lines dropped, each with a `// WOLFGATE` reason already in the file:

| Dropped relay pair | WP1's stated reason |
|---|---|
| `<StatusEffectContainerComponent, StandUpAttemptEvent>` | type does not exist in WG |
| `<StatusEffectContainerComponent, StunEndAttemptEvent>` | type does not exist in WG |
| `<StatusEffectContainerComponent, RefreshStaminaCritThresholdEvent>` | type does not exist in WG |
| `<StatusEffectContainerComponent, GetMeleeTargetModifiersEvent>` | belongs to Onyx's un-ported MartialArts |
| `<StatusEffectContainerComponent, EmoteActionEvent>` (+ related `EmoteEvent`/`AccentGetEvent`/`VocalSystem`/`MumbleAccentSystem` wiring) | type doesn't exist / server-side-only in WG |
| `<StatusEffectContainerComponent, BleedModifierEvent>` | doesn't exist; WG bleeding is server-side `BleedAmount` |
| `<StatusEffectContainerComponent, RefreshPressureImmunityEvent>` | doesn't exist |
| `<StatusEffectContainerComponent, SelfBeforeInjectEvent>` | doesn't exist |
| `<StatusEffectContainerComponent, CatchAttemptEvent>` | doesn't exist |

**Kept** (relevant to double-check against phase 2): `RefreshMovementSpeedModifiersEvent` (line 38), `DamageModifyEvent` (line 63), `ExaminedEvent` (line 58), `GetBlurEvent` (line 50).

**Cross-check against every phase-2 consumer:**
- `FractureEffectsSystem` subscribes `RefreshMovementSpeedModifiersEvent`/`GetManipulationDurationMultiplierEvent` **directly on `WoundHostComponent`**, not through `StatusEffectContainerComponent` — it never enters the relay at all, so it is unaffected by anything dropped *or* kept here.
- `PainSystem`'s stun path (`StunSystemOnyxCompat.TryUpdateParalyzeDuration` → WG's old-style `SharedStunSystem.TryParalyze`) needs the **old** `Content.Shared.StatusEffect` framework's `StatusEffectsComponent` (confirmed by WP9: `- type: StatusEffects` had to be added to the foundation test body), not StatusEffectNew's `StatusEffectStunned`/`StunEndAttemptEvent` at all. The dropped `StunEndAttemptEvent` relay is irrelevant to pain shock.
- `HighPainThresholdSystem` (§7) subscribes `ModifyPainGainEvent` directly on `HighPainThresholdComponent` — a plain C# event, no relation to `StatusEffectContainerComponent` relaying.
- `WoundStatusEffectSystem`/`WoundBehaviors` never reach the relay either (dead code path, §1/§3).

**Verdict: none of the 9 dropped relay lines are depended on by any phase-2 consumer.** No relay-restoration work is needed for phase 2.

---

## 5. Alerts — `StatusEffectAlertComponent` usage and `_Onyx/Alerts/alerts.yml`

`StatusEffectAlertComponent` (WP1, verbatim) is the StatusEffectNew mechanism that shows an alert while a *status-effect entity* is active (e.g. `StatusEffectStunned`'s `- type: StatusEffectAlert / alert: Stun` in `movement.yml`). **Nothing phase-2 needs uses it:**

- `FractureAlertSystem.cs` (Onyx, read in full) calls WG's classic `AlertsSystem.ShowAlert(uid, alert)`/`ClearAlert(uid, alert)` **directly on the body**, driven by `FractureProfilePrototype.Alert`/`AlertMinimumGrade`/`AlertHiddenTreatments` — no status-effect entity is involved anywhere in this file.
- Pain has **no alert at all** at the pin: `grep -n "id: Pain\|PainAlert"` across every prototype file in Onyx returns nothing pain-related (only an unrelated `Painkiller` drink id). Pain shock manifests as a stun + jitter + scream emote, not an alert-bar icon. `PainComponent`/`PainShockTargetComponent` (`WoundDamageComponents.cs`) carry no alert field. **DECISIONS.md P2-1's phrase "fracture/pain/shock alerts" overstates this — only `BrokenBones` is a real alert prototype at the pin; there is no separate pain or shock alert to port.**

**`BrokenBones` is the only alert phase 2 needs**, and it is **already fully wired by WP7**:

| Piece | WG path | Present? |
|---|---|---|
| Alert prototype | `Resources/Prototypes/_Onyx/Alerts/alerts.yml` | yes — `id: BrokenBones`, WP7 comment "only BrokenBones is ported" |
| Texture | `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi` | yes |
| Locale | `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | yes — `alerts-broken-bones-{name,desc}` |
| Prototype field wiring | `Resources/Prototypes/_Onyx/Wounds/wounds.yml:49-51` (`OrganicFractureProfile`) | yes — `alert: BrokenBones`, `alertMinimumGrade: Simple`, `alertHiddenTreatments: [Mended]` |
| Field definitions | `Content.Shared/_Onyx/Wounds/WoundPrototype.cs:244,247,250` | yes — `Alert`, `AlertMinimumGrade`, `AlertHiddenTreatments` all present, WP4 |
| Part→profile wiring | `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` (all 10 body parts) | yes — `fractureProfile: OrganicFractureProfile` |

**Verdict: zero new alert/texture/locale work for phase 2.** `FractureAlertSystem.cs` can be ported as a near-verbatim file (`Content.Shared.Alert`/`SharedBodySystem`/`IPrototypeManager` are all already-resolvable WG symbols); it needs no `// WOLFGATE` edit beyond the standard file move.

---

## 6. Blocker found in passing: `FractureEffectsSystem.cs`'s hands block will not compile

Not asked for directly, but discovered while tracing item 1 and material to the phase-2 file list (the same class of bug WP5 already hit and fixed once). `FractureEffectsSystem.TryGetUsedHandSymmetry` (Onyx, lines 126-151):

```csharp
string? handId;
if (used is { } item) { if (!_hands.IsHolding((body, hands), item, out handId)) return false; }
else handId = _hands.GetActiveHand((body, hands));
if (!_hands.TryGetHand((body, hands), handId, out var hand)) return false;
symmetry = hand.Value.Location switch
{
    HandLocation.Left or HandLocation.FunctionalLeft => BodyPartSymmetry.Left,
    HandLocation.Right or HandLocation.FunctionalRight => BodyPartSymmetry.Right,
    _ => BodyPartSymmetry.None,
};
```

| Onyx symbol | Onyx shape | WG shape | Compiles? |
|---|---|---|---|
| `Hand` | `partial record struct` (`HandsComponent.cs:129`) | `sealed class` (`HandsComponent.cs:107`, `//TODO: This should definitely be a struct`) | **no** — `hand.Value.Location` is invalid on a nullable reference type |
| `GetActiveHand(Entity<HandsComponent?>)` | returns `string?` (hand id) | returns `Hand?` (WG `SharedHandsSystem.cs:177`) | **no** — `handId = _hands.GetActiveHand(...)` assigns `Hand?` to `string?` |
| `IsHolding(Entity<HandsComponent?>, EntityUid?, out string? inHand)` | 3-arg, `out string?` | WG has `IsHolding(Entity<HandsComponent?>, EntityUid?)` (bool only, no `out`) **or** `IsHolding(EntityUid, EntityUid?, out Hand?, HandsComponent?)` (`out Hand?`, not `out string?`) | **no** — neither WG overload matches the call shape |
| `TryGetHand(Entity<HandsComponent?>, string? handId, out Hand? hand)` | | WG: `TryGetHand(EntityUid handsUid, string handId, out Hand? hand, ...)` (`SharedHandsSystem.cs:306`) — non-nullable `handId`, `EntityUid` not `Entity<HandsComponent?>` | partial mismatch |
| `HandLocation` | `{Right, Middle, Left, Functional, FunctionalRight, FunctionalLeft}` (Onyx's own FunctionalHands feature, `<Onyx-FunctionalHands>`) | `{Left, Middle, Right}` only (`HandsComponent.cs:156-161`) | **no** — `HandLocation.FunctionalLeft`/`FunctionalRight` are undeclared members, CS0117 |

This is exactly the shape WP5 already solved once, verbatim, in `WoundDamageRoutingSystem.TryGetActiveHandPart` (`Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:607-621`, quoted below) — reuse the identical pattern:

```csharp
// WOLFGATE: Wolfgate's GetActiveHand returns the Hand itself (a class), and HandLocation has no Functional* members.
if (!TryComp(body, out HandsComponent? hands) || _hands.GetActiveHand((body, hands)) is not { } hand)
    return false;
var symmetry = hand.Location switch
{
    HandLocation.Left => BodyPartSymmetry.Left,
    HandLocation.Right => BodyPartSymmetry.Right,
    _ => BodyPartSymmetry.None,
};
```

For the `used is { } item` (explicit tool) branch, `IsHolding(Entity<HandsComponent?>, EntityUid?)` (bool-only) cannot recover *which* hand is holding it, so the rewrite needs `SharedHandsSystem`'s enumerable of held hands (e.g. iterate `hands.Hands` / use `TryGetHand`-by-item lookup if one exists) rather than a 1:1 signature swap — this needs the same author attention WP5's `wounds-a.md §1.3-C` note got; flagging it here so WP10 does not discover it mid-build.

---

## 7. `HighPainThreshold` trait (adjacent, cheap to confirm while here)

Not one of the 5 numbered items, but named in DECISIONS.md P2-1 ("HighPainThreshold trait + PainNumbness status effect") and touches the same pain/status surface, so confirmed while the files were open:

- `Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` (14 lines) + `HighPainThresholdSystem.cs` (19 lines) — **not StatusEffectNew at all**: a plain component + a system subscribing `<HighPainThresholdComponent, ModifyPainGainEvent>` (already-ported event, `WoundEvents.cs`/`PainSystem.cs`, WP4/WP9). Both names are **MISSING** in WG (zero grep hits) — straightforward two-file port, no compat needed.
- Trait prototype: `Resources/Prototypes/_Onyx/Traits/quirks.yml:29-37` — `id: HighPainThreshold`, `components: [{type: HighPainThreshold}]`, `conflicts: [PainNumbness]`. **`PainNumbness` here is WG's own pre-existing trait** (`Resources/Prototypes/Traits/disabilities.yml:69`, `PainNumbnessComponent`/`PainNumbnessSystem` — unrelated to Onyx's StatusEffectNew-based pain numbness), so the `conflicts:` entry resolves against real, already-existing WG content with no collision.
- "PainNumbness status effect" (`StatusEffectPainNumbness`, parent `[PainNumbnessStatusEffectBase, MobStatusEffectDebuff]`, `body.yml:57-58`) is consumed **only** by `Resources/Prototypes/Reagents/narcotics.yml:48,556` (`effectProto: StatusEffectPainNumbness`) — reagent/chem work is explicitly phase 4 (WP11). `TraitStatusEffectPainNumbness` (`traits.yml:22`) has **zero consumers at the pin**, same orphan pattern as §3. WP1's `PainNumbnessStatusEffectComponent` (already ported) is the component both prototypes carry, so `PainSystem.IsPainNumb()` compiles and runs correctly today — it simply has nothing that can ever set it true until phase 4 ports a narcotic. This is a correct, harmless, already-complete no-op; no action needed in phase 2.

---

## 8. Ordered file list for phase 2 (StatusEffectNew-related scope only)

| # | File | Action | Notes |
|---|---|---|---|
| — | `Content.Shared/Movement/Components/MovementModStatusEffectComponent.cs` | **do not port** | §1 — no consumer |
| — | `Content.Shared/Movement/Systems/MovementModStatusSystem.cs` | **do not port** | §1 — no consumer; would also drag in unported `FrictionStatusEffectComponent` |
| — | `Resources/Prototypes/Entities/StatusEffects/movement.yml` | **do not port** | §2 — all concrete entries need `MovementModStatusEffect` (missing) and have no in-scope consumer |
| — | `Resources/Prototypes/_Onyx/StatusEffects/wounds.yml` | **do not port** | §3 — both entries orphaned at the pin |
| — | `MobStandStatusEffectBase` / `KnockdownImmune` tag | **do not add** | §2 — zero concrete descendants, zero consumers in scope; WP1 already reached this conclusion |
| 1 | `Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs` | port, **edited** | rename `FractureEffectSystem`→ as-is (Onyx's own class name is `FractureEffectSystem`, singular — verify against `body-organ.md`/manifest naming before landing); hands block rewrite per §6; `OrganGotInsertedEvent`/`OrganGotRemovedEvent` bridge already exists and is free (WP5's `WolfmedBodyPartLifecycleSystem`, confirmed by grep — zero existing subscribers on `<WoundableComponent, OrganGotInsertedEvent/OrganGotRemovedEvent>`); subscribe WG's existing `GetDoAfterDelayMultiplierEvent` (`Content.Shared/_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:33`, raised at `SharedDoAfterSystem.cs:211`) on `WoundHostComponent` instead of relying on Onyx's manual `GetManipulationDurationMultiplierEvent`-only path (Onyx itself never wires that event to a real do-after either — confirmed by grep, its only caller anywhere in Onyx is the unit test) |
| 2 | `Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` | port, **verbatim** | no `// WOLFGATE` edit needed; all prototype/texture/locale/field dependencies already exist (§5) |
| — | `Resources/Prototypes/_Onyx/Alerts/alerts.yml`, `fracture.rsi`, `fractures.ftl`, `wounds.yml`'s `alert:` fields, `parts.yml`'s `fractureProfile:` | **already done (WP4/WP7)** | no phase-2 work |
| 3 | `Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` | port, verbatim | §7 |
| 4 | `Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs` | port, verbatim | §7 |
| 5 | `Resources/Prototypes/_Onyx/Traits/quirks.yml` | port, adapted | only the `HighPainThreshold` trait block (§7); do not port `Voracious`/`ColdBlooded`/etc. unless separately scoped |
| 6 | locale for `trait-high-pain-threshold-{name,desc}` | port | wherever Onyx keeps trait locale (not checked here — outside the 5-item task scope; flag for whoever lands file 5) |

### Subscription pairs this adds (all confirmed free by grep against the current WG tree)

| Component | Event | Registrant | Free? |
|---|---|---|---|
| `WoundHostComponent` | `RefreshMovementSpeedModifiersEvent` | `FractureEffectsSystem` | yes — zero existing subscribers of this pair in `_WF`/`_Onyx` |
| `WoundHostComponent` | `GetManipulationDurationMultiplierEvent` | `FractureEffectsSystem` | yes — new event, only declared in `WoundEvents.cs` |
| `WoundHostComponent` | `GetDoAfterDelayMultiplierEvent` | `FractureEffectsSystem` (new, per WP10 note) | yes — zero existing subscribers found in `_WF`/`_Onyx` |
| `WoundFractureComponent` | `FractureGradeChangedEvent` / `FractureTreatmentChangedEvent` / `WoundRemovedEvent` | `FractureEffectsSystem` | yes — new component |
| `WoundableComponent` | `OrganGotInsertedEvent` / `OrganGotRemovedEvent` | `FractureEffectsSystem` | yes — events exist (`OnyxBodyEvents.cs`, `[ByRefEvent]`, raised by WP5's `WolfmedBodyPartLifecycleSystem`), currently zero subscribers |
| `WoundableComponent` | `BodyPartFunctionalityChangedEvent` | `FractureEffectsSystem` | yes — event exists (`WoundEvents.cs`/`BodyPartFunctionalitySystem.cs`, WP4), currently zero subscribers |
| `HighPainThresholdComponent` | `ModifyPainGainEvent` | `HighPainThresholdSystem` | yes — new component, event already exists and is already raised twice by `PainSystem` (WP9-fixed) |

No pair above collides with anything WP1–WP9 registered (checked by grep, not just by reading PLAN.md §5).

---

## Summary of blockers

**No StatusEffectNew-related blocker exists for phase 2** — the entire movement-mod/Slowdown/MobStandStatusEffectBase/wounds.yml-orphan surface is confirmed out of scope by direct tracing, matching what WP1 already independently concluded for `MobStandStatusEffectBase`. The one real risk is non-StatusEffectNew: **`FractureEffectsSystem.TryGetUsedHandSymmetry` needs a hand-API rewrite before it will compile** (§6), of the same kind and difficulty as WP5's already-solved `TryGetActiveHandPart`. Recommend WP10 budget time for that rewrite explicitly rather than discovering it as a build error.
