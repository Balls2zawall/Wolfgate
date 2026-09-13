# Phase 2 analysis — **pain-hud**: making pain visible and audible

**Scope:** everything in Onyx that surfaces `PainSystem`'s state to a player — overlays, alerts, emotes/sounds,
status effects, the `HighPainThreshold` trait — and what it takes to land each in Wolfgate.

**Sources:** ONYX = `C:/tmp/onyx` pinned `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (sparse; every path below
read with `git -C C:/tmp/onyx show HEAD:<path>`). WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(phase 1 committed). READ-ONLY analysis; nothing was modified.

---

## 0. Executive summary

1. **Onyx has no pain alert and no shock alert.** The only wound-related alert prototype in the whole Onyx tree
   is `BrokenBones` (fracture), and **WP7 already shipped it** — prototype, RSI and locale. `Resources/Prototypes/_Onyx/Alerts/categories.yml`
   declares only `TileMovement` / `Counter` / `Ninjutsu`, none of them wound-related. **PLAN P2-2's "add a
   fracture-alert *and pain-alert* assertion" cannot be satisfied as written** — there is nothing to assert.
   See §2. This is the one item the orchestrator must re-scope.
2. **Pain's only HUD surface in Onyx is the red damage-overlay vignette**, and Onyx delivers it through a
   *whole-system refactor of upstream's damage overlay* (`Content.Shared/DamageOverlay/**` + `Content.Client/DamageOverlay/**`,
   networked `DamageOverlayComponent`) that Wolfgate does not have and **cannot compile verbatim** — Onyx's two
   files use the `[SubscribeLocalEvent]` attribute, which does not exist in RT 277. §3 gives a 2-file,
   ~20-line Wolfgate equivalent that reproduces Onyx's exact visual behaviour with no new components.
3. **Nothing currently gives any Wolfgate mob `PainShockTargetComponent`**, so pain shock — the stun, the
   forced `Scream` emote, the jitter and the 30 s adrenaline window — is dead code today. One YAML block fixes it (§4.1).
4. **Nothing currently gives any Wolfgate mob `EmoteOnDamageComponent`** (zero YAML hits; only `ZombieSystem`
   `EnsureComp`s it), and WG's `EmoteOnDamageComponent` has a *different shape* from Onyx's. §4.2 gives an
   **additive** port that leaves the zombie path untouched.
5. **Onyx's own `species_base.yml` pain-sounds block is broken at the pin**: it writes `emotes:` under
   `- type: EmoteOnDamage`, but Onyx renamed the field to `EmotesThreshold` (YAML key `emotesThreshold`).
   RT silently drops unknown mapping keys at read time and raises `FieldNotFoundErrorNode` at validation
   (`RobustToolbox/Robust.Shared/Serialization/Manager/Definition/DataDefinition.cs:277`), so Onyx's pain
   sounds never fire and their YAML linter should be failing. **Do not copy that block verbatim.**
6. **`HighPainThreshold` is a 2-file, ~25-line trait** that ports cleanly, but Onyx's trait prototype fields
   `conflicts:` and `specials:` do not exist in Wolfgate — `conflicts:` maps to `mutuallyExclusiveTraits:`,
   and `specials:` has no equivalent at all (§5).
7. **`PainSystem.IsPainNumb` is unreachable in Wolfgate.** It tests only `PainNumbnessStatusEffectComponent`
   (a StatusEffectNew effect). WG's `PainNumbness` trait grants the *other*, Mono/legacy `PainNumbnessComponent`,
   and **no prototype in WG carries `PainNumbnessStatusEffect`** — WP1 ported the component but not
   `StatusEffectPainNumbness`/`PainNumbnessStatusEffectBase`, and Wolfgate's trait system cannot apply a
   status effect at all. So the `PainNumbness` trait does nothing to wound pain (§6).
8. **`PainSystem` is 100 % server-authoritative — zero prediction.** Every mutator is `_net.IsServer`-gated;
   every reader is unconditional and touches only `[AutoNetworkedField]` state. A client HUD is safe as long
   as it *reads* `PainComponent` and never subscribes `PainChangedEvent` (server-only raise). §7.
9. **`_Onyx/StatusEffects/wounds.yml` is confirmed orphaned** — its two entries `StatusEffectBurnSlowdown` and
   `StatusEffectWoundImpairment` are referenced nowhere in Onyx except their own prototype and the ru-RU
   locale. **No wound prototype in Onyx declares a `WoundStatusEffectBehavior` at all**, so
   `WoundStatusEffectSystem`'s whole apply/remove half is dead at the pin. PLAN P2-1's conditional ("skip if
   so") fires: **skip the file and skip the `StatusEffectSlowdown`/`MovementModStatusEffect` chain** — nothing
   in the phase-2 scope needs it (§8).

---

## 1. What `PainSystem` exposes, and who consumes it in Onyx

`Content.Shared/_Onyx/Wounds/PainSystem.cs` (vendored verbatim in WG apart from the two WP9 `// WOLFGATE`
`ModifyPainGainEvent(1f)` fixes) produces exactly four observable outputs:

| Output | Where produced | Consumer in Onyx | Status in WG |
|---|---|---|---|
| `PainComponent.{Value,Suppression,WoundPain}` (networked) | `SetPain`, `RefreshSuppression`, `RefreshWoundPain` | `SharedDamageOverlaySystem` → red vignette; `HealthExaminableSystem.Pain`; health analyzer | **component ships, no consumer** |
| `PainChangedEvent` (ByRef, server-only raise) | `PainSystem.RaisePainChanged` (`PainSystem.cs:432-444`) | `SharedDamageOverlaySystem.OnPainChanged` | **zero subscribers in WG** (verified by grep over `Content.{Shared,Server,Client}`) |
| `ModifyPainGainEvent(float Multiplier)` (ByRef, raised on the **body**) | `PainSystem.cs:114-116` and `:239-241` | `HighPainThresholdSystem` only | **zero subscribers in WG** |
| pain shock: paralyse + `Scream` emote + jitter + adrenaline | `PainSystem.UpdatePainShock` (`:268-307`) | the player | **never runs** — no mob has `PainShockTargetComponent` |

Exhaustive grep of the Onyx client tree for wound/pain symbols returns **two** files:
`Content.Client/Damage/DamageVisualsSystem.cs` (part damage sprites — a different work package) and
`Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs:306-307` (`diagnostic.Pain` →
`health-analyzer-wound-pain-short`; PLAN defers the health analyzer to WP11).
**There is no `Content.Client/_Onyx/**` pain system.** The client half of the pain HUD is entirely the
damage overlay.

### 1.1 The numbers the HUD will show (P2-4 requires these be verified on a real mob, not assumed)

From `WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs` and `PainSystem.cs`, all present in WG today:

- `PainComponent.SoftPainCap = 135` (`WoundDamageComponents.cs:137`) — `SetPain` clamps to it (`PainSystem.cs:208`).
- `PainComponent.RecoveryPerSecond = FixedPoint2.New(1f / 9f)` ≈ **0.11/s** (`WoundDamageComponents.cs:134`).
- `PainComponent.DamageMultipliers` (`:119-131`): `Blunt 0.87, Slash 0.67, Piercing 0.67, Heat 0.8, Cold 0.75,
  Shock 0.7, Cellular 0.32, Caustic 0.12, Radiation 0.12, Poison 0.7`.
- `PainShockThreshold = 130`, `PainShockRearmThreshold = 110`, `PainShockStunTime = 2 s`,
  `PainShockAdrenalineTime = 30 s`, `PainShockAdrenalineMultiplier = 0.7` (`PainSystem.cs:31-35`).
- Overlay level = `min(1, GetPain / SoftPainCap)`; Onyx floors anything `< 0.05` to 0
  (`ONYX SharedDamageOverlaySystem.cs:96-97`), i.e. the vignette starts at **pain 6.75** and is at **0.963**
  when pain shock fires.
- The body's `PainComponent.Value` is the **sum of the parts' raw pain**, maintained by
  `WoundDamageProjectionSystem.RefreshBodyPain` (`WG/.../WoundDamageProjectionSystem.cs:232-238`) and by
  `SetPain`'s part→body propagation (`PainSystem.cs:217-219`). So the HUD reads the **body's** `PainComponent`.

---

## 2. Alerts — **there are none for pain**

### 2.1 What exists in Onyx

`git -C C:/tmp/onyx grep -n -i "pain|shock" HEAD -- Resources/Prototypes/_Onyx/Alerts Resources/Prototypes/Alerts`
returns exactly one line: `Resources/Prototypes/_Onyx/Alerts/alerts.yml:37: - sprite: /Textures/_Onyx/Interface/Alerts/fracture.rsi`.

The only wound alert is:

```yaml
- type: alert
  id: BrokenBones
  icons:
  - sprite: /Textures/_Onyx/Interface/Alerts/fracture.rsi
    state: brokenbones
  name: alerts-broken-bones-name
  description: alerts-broken-bones-desc
```
(ONYX `Resources/Prototypes/_Onyx/Alerts/alerts.yml:33-40`; locale `Resources/Locale/en-US/_Onyx/medical/fractures.ftl:1-2`.)

`Resources/Prototypes/_Onyx/Alerts/categories.yml` contains only `TileMovement`, `Counter`, `Ninjutsu` — no
wound/pain category. No `alertCategory` or `alert` prototype anywhere in Onyx mentions pain or shock.
The strings `alerts-*pain*` do not exist in `Resources/Locale/en-US/alerts/*.ftl`; the only two hits for "pain"
there are incidental prose (`alerts-starving-desc`, `alerts-adrenaline-desc`).

### 2.2 WG status

| Artefact | WG status |
|---|---|
| `Resources/Prototypes/_Onyx/Alerts/alerts.yml` (`BrokenBones`) | **SAME** — shipped by WP7, `id: BrokenBones` at `:16` |
| `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{meta.json,brokenbones.png}` | **SAME** — shipped by WP7 |
| `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | **SAME** — shipped by WP7 |
| `FractureProfilePrototype.Alert` default `"BrokenBones"` | **SAME** — `WG/Content.Shared/_Onyx/Wounds/WoundPrototype.cs:244`, with `AlertMinimumGrade = FractureGrade.Simple` (`:247`) and `AlertHiddenTreatments = [Mended]` (`:250`) |
| `Resources/Prototypes/_Onyx/Wounds/wounds.yml` `alert: BrokenBones` | **SAME** — `:49` |
| `Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` (the only consumer) | **MISSING** — manifest row 77 lists it skipped to WP10 |
| `AlertsSystem.ShowAlert` / `ClearAlert` (what `FractureAlertSystem` calls) | **SAME** — Wolfgate's `AlertsSystem`; note `FractureAlertSystem` uses `ShowAlert`/`ClearAlert` only, **not** the WP1 `UpdateAlert` compat partial |

**Everything for the fracture alert is in place except its system.** That system is the fracture work package's
business, not this one; the only thing this analysis owes it is the confirmation above.

### 2.3 Action for the orchestrator

**P2-2's "pain-alert assertion" has no target.** Choose one:

- **(a) Drop it** (recommended, and consistent with D4 "Onyx defaults"). Replace it with an assertion on the
  pain *overlay level* — `min(1, PainSystem.GetPain(body) / PainComponent.SoftPainCap)` — which is what the
  player actually sees and is computable headlessly from networked state.
- (b) Invent a Wolfgate-only `Pain` alert. That is new design, not a port; it needs an icon RSI with severity
  states, an `alert` prototype, locale, a category decision and a `_WF` driver system. Out of D4's spirit.

---

## 3. The client pain overlay

### 3.1 What Onyx actually did

Onyx did **not** add an `_Onyx` overlay. It refactored upstream's damage overlay from a client-only
`UIController` into a **networked shared system**, and then hung pain off it:

| ONYX file | What |
|---|---|
| `Content.Shared/DamageOverlay/DamageOverlayComponent.cs` (new) | `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)]`, `[Access(typeof(SharedDamageOverlaySystem))]`; fields `CurrentState`, **`PainLevel`**, `CritLevel`, `DeadLevel`, `OxygenLevel`, all `[AutoNetworkedField]` |
| `Content.Shared/DamageOverlay/SharedDamageOverlaySystem.cs` (new) | computes the four levels **server-side** and `DirtyField`s them; subscribes `ComponentStartup`, `MobStateChangedEvent`, `MobThresholdChecked`, **`PainChangedEvent`** |
| `Content.Client/DamageOverlay/DamageOverlaySystem.cs` (new) | adds/removes the `Overlay` for the local player |
| `Content.Client/DamageOverlay/DamageOverlay.cs` (moved from `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/`) | reads `DamageOverlayComponent` each `Draw()` and lerps |

The pain half is three marked edits:

- `SharedDamageOverlaySystem.cs:93-97`:
  ```csharp
  entity.Comp.PainLevel = TryComp(entity, out PainComponent? pain) && pain.SoftPainCap > FixedPoint2.Zero
      ? FixedPoint2.Min(1f, _pain.GetPain((entity.Owner, pain)) / pain.SoftPainCap).Float()
      : 0f;
  if (entity.Comp.PainLevel < 0.05f)
      entity.Comp.PainLevel = 0f;
  ```
  — i.e. **pain *replaces* the brute-damage vignette entirely**; upstream's `DamagePerGroup["Brute"]`/`["Burn"]`
  computation is gone from Onyx's version.
- `SharedDamageOverlaySystem.cs:45-51`: a `PainChangedEvent` subscription that re-runs `UpdateOverlays`.
- `Content.Client/DamageOverlay/DamageOverlay.cs:175`: `_bruteShader.SetParameter("darknessAlphaOuter", 0.8f * level);`
  (`// <Onyx-PartPain-edited>`) — upstream passes a flat `0.8f`, so in Onyx the vignette's opacity now scales
  with the level as well as its radius.

Onyx also adds `- type: DamageOverlay` to `MobDamageable` (ONYX `Resources/Prototypes/Entities/Mobs/base.yml:78`).

### 3.2 WG status of every symbol Onyx's version needs

| Symbol | WG status |
|---|---|
| `Content.Shared/DamageOverlay/**` | **MISSING** — WG has no shared damage overlay at all |
| `Content.Client/DamageOverlay/**` | **MISSING** — WG's equivalent lives at `Content.Client/UserInterface/Systems/DamageOverlays/{DamageOverlayUiController.cs,Overlays/DamageOverlay.cs}` (the pre-refactor upstream shape) |
| `[SubscribeLocalEvent]` attribute | **MISSING from RT 277.** `grep -rn "SubscribeLocalEventAttribute|\[SubscribeLocalEvent\]" RobustToolbox` → zero hits. Onyx's two files use it **eight** times; a verbatim port does not compile |
| `DirtyField(uid, comp, nameof(...))` | **SAME** — `RobustToolbox/Robust.Shared/GameObjects/EntityManager.ComponentDeltas.cs:42` `public virtual void DirtyField<T>(EntityUid uid, T comp, [ValidateMember] string fieldName, MetaDataComponent? metadata = null)` |
| `AutoGenerateComponentState(raiseAfterAutoHandleState:, fieldDeltas:)` | **SAME** — `RobustToolbox/Robust.Shared/Analyzers/ComponentNetworkGeneratorAuxiliary.cs:55` |
| `DamageableSystem.GetDamagePerGroup(Entity<DamageableComponent>)` / `GetTotalDamage(Entity<…>)` | **DIFFERENT** — WG has `DamageableComponent.DamagePerGroup`/`.TotalDamage` properties only; the new-style API lives on the D12 facade `WolfmedDamageableSystem` (`GetTotalDamage` yes; **`GetDamagePerGroup` is not in the §2.1 facade spec and is not implemented**) |
| `MobThresholdChecked` | **SAME** — `WG/Content.Shared/Mobs/Systems/MobThresholdSystem.cs:493-494`, `readonly record struct MobThresholdChecked(EntityUid Target, MobStateComponent MobState, MobThresholdsComponent Threshold, DamageableComponent Damageable)` |
| `MobThresholdSystem.TryGetIncapThreshold` / `TryGetDeadPercentage` | **SAME** — used by WG's controller at `:82` and `:124` |
| `PainSystem.GetPain(Entity<PainComponent?>)` | **SAME** — `WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:165` |
| `PainComponent.SoftPainCap` | **SAME** — `WoundDamageComponents.cs:137` |
| `IOverlayManager`, `IPlayerManager`, `ShaderPrototype "GradientCircleMask"` | **SAME** — already used by `Overlays/DamageOverlay.cs:49-56` |

### 3.3 Why Wolfgate's `UIController` cannot host the fix

`DamageOverlayUiController` refreshes only on `LocalPlayerAttachedEvent`, `LocalPlayerDetachedEvent`,
`MobStateChangedEvent` and `MobThresholdChecked` (`DamageOverlayUiController.cs:28-31`). **Pain changes on
their own raise none of those** — pain decays at 0.11/s and suppression decays every tick with no damage
event, so a controller-only fix would freeze the vignette between hits.

And `UIController` **cannot** subscribe a directed event to fix that:
`RobustToolbox/Robust.Client/UserInterface/Controllers/UiController.Subscriptions.cs` exposes only broadcast
`SubscribeLocalEvent<T>`, `SubscribeNetworkEvent<T>`, `SubscribeAllEvent<T>` — there is **no**
`SubscribeLocalEvent<TComp, TEvent>` overload. So `<PainComponent, AfterAutoHandleStateEvent>` is not
available to it.

### 3.4 Recommended Wolfgate implementation — read pain in `Draw()`

This is structurally **identical to what Onyx's own `DamageOverlay.Draw` does** (`ONYX Content.Client/DamageOverlay/DamageOverlay.cs:66-81`
reads the component every frame), it needs no new component, no networking, no system, and it is immune to the
refresh-trigger problem because `Draw` runs every frame.

**Edit 1 — `WG/Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs`.**
Two `// WOLFGATE` sites:

- After the existing eye check at `:58-64`, before the lerp block, add ~8 lines:
  ```csharp
  // WOLFGATE (Wolfmed phase 2): Onyx replaces the brute-damage vignette with a pain vignette
  // (ONYX Content.Shared/DamageOverlay/SharedDamageOverlaySystem.cs:93-97). Onyx reads a networked
  // PainLevel off DamageOverlayComponent; Wolfgate has no shared damage overlay, so we read the
  // networked PainComponent directly here - PainSystem.GetPain is a pure read of AutoNetworkedFields.
  if (_playerManager.LocalEntity is { } local &&
      _entityManager.TryGetComponent(local, out PainComponent? pain) &&
      pain.SoftPainCap > FixedPoint2.Zero)
  {
      var painSystem = _entityManager.System<PainSystem>();
      var painLevel = FixedPoint2.Min(1f, painSystem.GetPain((local, pain)) / pain.SoftPainCap).Float();
      BruteLevel = painLevel < 0.05f ? 0f : painLevel;
  }
  ```
  Notes: `_playerManager` and `_entityManager` are already injected (`:15-16`). `_entityManager.System<T>()`
  is the established pattern in WG client overlays (`Content.Client/Overlays/EntityHealthBarOverlay.cs:42-46`).
  **Resolve the system inside `Draw`, not in the constructor** — the overlay is constructed from
  `DamageOverlayUiController.Initialize()` (`:27`), which can run before entity systems exist.
- `:158` `_bruteShader.SetParameter("darknessAlphaOuter", 0.8f);` → `0.8f * level;` with a `// WOLFGATE`
  note citing `ONYX Content.Client/DamageOverlay/DamageOverlay.cs:175`. This is the `<Onyx-PartPain-edited>`
  line and is the *only* difference between WG's `Draw` body and Onyx's, apart from the component read —
  the two files are otherwise line-for-line identical (verified by diffing `:66-...` against ONYX `:83-...`).

**Edit 2 — `WG/Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs:96-121`.**
One `// WOLFGATE` guard so the two writers do not fight: inside `case MobState.Alive:` skip the
`DamagePerGroup["Brute"]`/`["Burn"]` computation when the entity has `PainComponent`. Without it the
controller writes a brute level computed from the *projection* (on a wound host, `damageable.DamagePerGroup["Brute"]`
is the sum of all part damage, i.e. large) on every `MobThresholdChecked`; `Draw` would overwrite it a frame
later, but the intermediate frame flashes. The existing `PainNumbnessComponent` branch at `:98-106` is the
natural place — extend its condition.

**Behaviour after the edits, versus Onyx:** identical for `MobState.Alive`. For `Critical` the existing
`level > 0f && _oldCritLevel <= 0f` guard at `:145` already suppresses the red vignette, matching Onyx; for
`Dead`, Onyx explicitly zeroes `PainLevel` — Wolfgate's controller already zeroes `BruteLevel` in the `Dead`
case (`:135`) and `Draw` would re-derive it from pain, so **add `&& State != MobState.Dead` to the Edit-1
condition** to match Onyx exactly (pain is not cleared on death by `PainSystem`).

**Governance flag:** both files are upstream (non-`_Onyx`, non-`_WF`) and **neither is on PLAN §3's authorised
hook list**. PLAN ground rule 1 in the header ("Nothing outside this list may be edited in an upstream file
without escalating") therefore requires the orchestrator to add them — propose **HOOK 14**
(`Overlays/DamageOverlay.cs`, two sites) and **HOOK 15** (`DamageOverlayUiController.cs`, one condition).

### 3.5 The rejected alternative — port Onyx's shared refactor

Cost, for the record, so it is not re-litigated: rewrite 8 `[SubscribeLocalEvent]` attributes into explicit
`Initialize()` subscriptions (RT 277 has no attribute); author `DamageOverlayComponent` + `SharedDamageOverlaySystem`
under an **upstream** namespace (`Content.Shared.DamageOverlay`), i.e. a new upstream subsystem, not an `_Onyx`
or `_WF` one; **delete** `Content.Client/UserInterface/Systems/DamageOverlays/**` (two overlays would otherwise
both be added to `IOverlayManager`); add `- type: DamageOverlay` to `Resources/Prototypes/Entities/Mobs/base.yml`
(`MobDamageable`), an upstream YAML edit affecting every damageable mob; implement `GetDamagePerGroup` on the
D12 facade; and lose Mono's `PainNumbnessComponent` branch. It also moves the whole overlay computation
server-side, adding four networked floats per mob. **Not worth it in phase 2.** Revisit only if Wolfgate ever
takes the upstream refactor for its own reasons.

### 3.6 Sandbox

No new BCL or engine types: `FixedPoint2`, `IEntityManager.System<T>()`, `IPlayerManager.LocalEntity`,
`MathF`, `Vector3`, `Color` are all already used by the two files being edited and by
`Content.Client/Overlays/EntityHealthBarOverlay.cs`. `Content.Client` already references `Content.Shared`
(phase 1's client build is green per WP9 §4.2), so `Content.Shared._Onyx.Wounds.{PainSystem,PainComponent}`
resolve. **`Content.IntegrationTests/Tests/Utility/SandboxTest.cs` needs no change.**

---

## 4. Pain-driven emotes and sounds

### 4.1 Pain shock — scream, jitter, stun, adrenaline (**one YAML block, no code**)

`PainSystem.UpdatePainShock` (`WG/Content.Shared/_Onyx/Wounds/PainSystem.cs:268-307`) already does all of it:

```csharp
if (!_stun.TryUpdateParalyzeDuration(entity, PainShockStunTime))
    return;
shockTarget.Armed = false;
Dirty(entity.Owner, shockTarget);
_chat.TryEmoteWithChat(entity, "Scream", ChatTransmitRange.HideChat,
    ignoreActionBlocker: true, forceEmote: true);
_jitter.DoJitter(entity, PainShockStunTime, true, 20f, 7f);
ApplyPainShockAdrenaline(entity);
```

Dependency check — **every symbol exists in WG**:

| Symbol | WG status |
|---|---|
| `SharedStunSystem.TryUpdateParalyzeDuration` | **shimmed, SAME behaviourally** — `Content.Shared/_WF/Wolfmed/Compat/StunSystemOnyxCompat.cs` maps it to `SharedStunSystem.TryParalyze(uid, d, refresh: false)` (`Content.Shared/Stunnable/SharedStunSystem.cs:244`) |
| `SharedChatSystem.TryEmoteWithChat(EntityUid, string, ChatTransmitRange, bool, string?, bool, bool)` | **SAME** — shared shim `Content.Shared/_WF/Wolfmed/Compat/SharedChatSystem.Wolfmed.cs`, HOOK 6 `override` landed at `Content.Server/Chat/Systems/ChatSystem.Emote.cs:60` |
| `Scream` emote prototype | **SAME** — `Resources/Prototypes/Voice/speech_emotes.yml:3` |
| `SharedJitteringSystem.DoJitter(EntityUid, TimeSpan, bool, float, float, …)` | **SAME** — `Content.Shared/Jittering/SharedJitteringSystem.cs:46`. It needs the **old** `StatusEffectsComponent` (`:49`) and the `"Jitter"` key; `Resources/status_effects.yml:16-17` declares `Jitter` with `alwaysAllowed: true`, so the `allowed:` list does not have to list it |
| old `StatusEffectsComponent` with `Stun`/`KnockedDown` | **SAME** — `Resources/Prototypes/Entities/Mobs/Species/base.yml:125-140` on `BaseMobSpecies` (id at `:10`), which `BaseMobSpeciesOrganic` (`:247`) inherits. This closes WP9's "pain shock needs `StatusEffectsComponent`" open item for real mobs |
| `PainShockTargetComponent` | **component SAME** (`WoundDamageComponents.cs:145-153`, networked), **wiring MISSING** |

**The gap:** `grep -rn "PainShockTarget" WG/Resources/` returns **nothing**. `PainSystem.Update` only visits
`EntityQueryEnumerator<PainComponent, MobStateComponent, PainShockTargetComponent>` (`PainSystem.cs:143`), and
`RaisePainChanged` only calls `UpdatePainShock` when `PainShockTargetComponent` is present (`:441-443`). So
pain shock has never run.

**Fix.** Onyx puts `- type: PainShockTarget # <Onyx-PainShock>` on `BaseSpeciesMob`
(ONYX `Resources/Prototypes/Body/species_base.yml:54`), whose Wolfgate counterpart is `BaseMobSpecies`
(`WG/Resources/Prototypes/Entities/Mobs/Species/base.yml:10`). **Put it on `BaseMobSpeciesOrganic` instead**,
inside the existing `# WOLFGATE - Wolfmed phase 1 (D21/D32)` block at `:250-252`, so it tracks `WoundHost`
and the D32 protogen exclusion. (Either placement is functionally safe — the component is inert without
`PainComponent`, which only `WoundDamageProjectionSystem.SetupBody` adds, and that is `WoundHost`-scoped —
but co-locating them keeps one source of truth.)

```yaml
  # WOLFGATE - Wolfmed phase 2. ONYX Resources/Prototypes/Body/species_base.yml:54 (<Onyx-PainShock>);
  # moved from BaseMobSpecies to here so it tracks WoundHost and the D32 protogen exclusion.
  - type: PainShockTarget
```

**Balance warning (carried from WP9 open item 2):** `StunSystemOnyxCompat` maps onto `TryParalyze`, which
re-triggers stun VFX on every call — a recorded §8.2 deviation. `UpdatePainShock` disarms after each shock and
rearms only below pain 110, so repeat firing is bounded, but this is the first time it will actually run.
P2-4 says verify on a real mob.

### 4.2 `EmoteOnDamage` pain sounds

#### 4.2.1 What Onyx has

- `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` (new partial, 50 lines) —
  `HandlePainDamageEmote(EntityUid, EmoteOnDamageComponent, DamageChangedEvent)`.
- `Content.Server/Chat/EmoteOnDamageComponent.cs` — **replaces** `HashSet<string> Emotes` with
  `Dictionary<float, HashSet<ProtoId<EmotePrototype>>> EmotesThreshold` (`:24-25`), and adds
  `HashSet<string> AllowedDamageType = ["Blunt","Caustic","Heat","Cold","Piercing","Shock","Slash"]`,
  `float PainThreshold = 6f`, `[ViewVariables] float LastTotalDamage` (`:53-60`).
- `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` — one added line in `OnDamage`
  (`HandlePainDamageEmote(uid, emoteOnDamage, args); // <Onyx-PainSounds>`, **before** the
  `if (!args.DamageIncreased) return;`), plus 4-arg `AddEmote`/`RemoveEmote` overloads keeping the 3-arg ones.
- `Resources/Prototypes/Body/species_base.yml:124-133` — the component on `BaseSpeciesMob`.

Algorithm (ONYX `EmoteOnDamageSystem.PainSounds.cs:17-50`): track `LastTotalDamage`; bail if the threshold map
is empty, the total went down, the 8 s cooldown is live, the 0.6 roll fails, the mob is Critical/Dead, or
`TryEffectsWithComp<PainNumbnessStatusEffectComponent>` hits. Sum the `DamageDelta` entries whose type is in
`AllowedDamageType`; bail under `PainThreshold` (6). Pick the **highest threshold key ≤ total damage** and emote
a random member. Note: the thresholds are on **total damage, not pain**.

#### 4.2.2 The Onyx bug — do not copy the YAML

ONYX `species_base.yml:126` writes `emotes:` under `- type: EmoteOnDamage`, but the datafield is
`EmotesThreshold` → YAML key `emotesThreshold` (`[DataField]` with no explicit name, ONYX
`Content.Server/Chat/EmoteOnDamageComponent.cs:24-25`). RT's data-definition reader iterates *field
definitions*, so an unknown mapping key is silently dropped at runtime, and
`DataDefinition.Validate` emits a `FieldNotFoundErrorNode` for it
(`RobustToolbox/Robust.Shared/Serialization/Manager/Definition/DataDefinition.cs:267-281`). So at the pin
**Onyx's `EmotesThreshold` is empty on every mob and `HandlePainDamageEmote` returns at its first condition.**
Use `emotesThreshold:` in the Wolfgate port and record it as a corrected-upstream-bug row in the manifest
(same class as WP9's `ModifyPainGainEvent` find).

#### 4.2.3 WG status of every symbol

| Symbol | WG status |
|---|---|
| `EmoteOnDamageComponent` | **DIFFERENT**. WG `Content.Server/Chat/EmoteOnDamageComponent.cs:24-25`: `[DataField("emotes", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<EmotePrototype>)), ViewVariables(VVAccess.ReadWrite)] public HashSet<string> Emotes = new();`  vs ONYX `:24-25`: `[DataField] public Dictionary<float, HashSet<ProtoId<EmotePrototype>>> EmotesThreshold = new();`. WG also has **no** `AllowedDamageType`, `PainThreshold`, `LastTotalDamage`. WG uses explicit `[DataField("…")]` names throughout; Onyx uses bare `[DataField]` |
| `EmoteOnDamageSystem` | **DIFFERENT** — WG `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs:25-50` is upstream's unconditional version; `AddEmote(EntityUid, string, EmoteOnDamageComponent?)` at `:55` and `RemoveEmote(EntityUid, string, EmoteOnDamageComponent?, bool)` at `:69` (3/4-arg, no `float threshold` parameter). It injects `IPrototypeManager _prototypeManager` (`:14`); Onyx's version uses `ProtoMan`, which **does not exist on RT 277's `EntitySystem`** — same gap PLAN §2.5 papers over for `StatusEffectsSystem` |
| `SubscribeLocalEvent<EmoteOnDamageComponent, DamageChangedEvent>` | **ALREADY CLAIMED** by `EmoteOnDamageSystem.cs:22`. **A second subscription on that pair is a server-start crash** — the pain path must go *through* the existing handler |
| `StatusEffectsSystem.TryEffectsWithComp<T>(EntityUid?, out HashSet<Entity<T, StatusEffectComponent>>?)` | **SAME** — `Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs:357` |
| `PainNumbnessStatusEffectComponent` | **SAME type, but unreachable** — see §6 |
| `DamageableSystem.GetTotalDamage(EntityUid)` (new style) | **MISSING** on WG's `DamageableSystem`; use the D12 facade `WolfmedDamageableSystem.GetTotalDamage(Entity<DamageableComponent?>)`, or simply `Comp<DamageableComponent>(uid).TotalDamage` |
| `ChatSystem.TryEmoteWithoutChat` | **SAME** — used at WG `EmoteOnDamageSystem.cs:46` |
| `Crying` emote prototype | **SAME** — `Resources/Prototypes/Voice/speech_emotes.yml:110` |
| `- type: EmoteOnDamage` on any mob | **MISSING** — zero YAML hits in WG; the component is only `EnsureComp`'d by `ZombieSystem.cs:181` / `ZombieSystem.Transform.cs:145`, which then call the 2-arg `AddEmote(uid, "Scream")` |

#### 4.2.4 Recommended port — **additive**, not Onyx's field replacement

Onyx *replaces* `Emotes` with `EmotesThreshold`. Replacing it in Wolfgate would silently change the meaning of
the two zombie call sites and break any downstream fork YAML. Instead:

1. **`Content.Server/Chat/EmoteOnDamageComponent.cs`** — four purely **additive** `// WOLFGATE` datafields,
   leaving `Emotes` untouched:
   ```csharp
   // WOLFGATE (Wolfmed phase 2): Onyx's pain-sound fields, added alongside Emotes instead of
   // replacing it (ONYX Content.Server/Chat/EmoteOnDamageComponent.cs:24,53-60) so Wolfgate's
   // zombie AddEmote path is unaffected. NOTE: Onyx's own species_base.yml:126 writes `emotes:`
   // for this field, which does not bind - the key is `emotesThreshold`.
   [DataField("emotesThreshold")]
   public Dictionary<float, HashSet<ProtoId<EmotePrototype>>> EmotesThreshold = new();

   [DataField("allowedDamageType")]
   public HashSet<string> AllowedDamageType = ["Blunt", "Caustic", "Heat", "Cold", "Piercing", "Shock", "Slash"];

   [DataField("painThreshold")]
   public float PainThreshold = 6f;

   [ViewVariables] public float LastTotalDamage;
   ```
   Needs `using Robust.Shared.Prototypes;` for `ProtoId<>`. The `[Access(typeof(EmoteOnDamageSystem))]`
   attribute at `:11` already covers the new partial (same class).
2. **`Content.Server/Chat/Systems/EmoteOnDamageSystem.cs`** — **one line**, first statement of `OnDamage`
   (`:25-28`), matching Onyx's placement *before* the `DamageIncreased` early-return:
   ```csharp
   HandlePainDamageEmote(uid, emoteOnDamage, args); // WOLFGATE: Wolfmed pain sounds (<Onyx-PainSounds>)
   ```
   No `AddEmote`/`RemoveEmote` overloads needed — nothing in Wolfgate calls the threshold form.
3. **`Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`** — vendored from ONYX with three
   `// WOLFGATE` edits: drop the `[Dependency] DamageableSystem _damageable` in favour of the D12 facade
   (`[Dependency] private WolfmedDamageableSystem _damage = default!;`) and `_damage.GetTotalDamage((uid, null)).Float()`;
   keep `StatusEffectsSystem` (present); and keep the class `public sealed partial class EmoteOnDamageSystem`
   in namespace `Content.Server.Chat.Systems` (partial of the upstream class — the file path is `_Onyx`, the
   namespace is not, exactly as D13 does for the server-side wound systems).
4. **`Resources/Prototypes/Entities/Mobs/Species/base.yml`** — in the same `# WOLFGATE` block as §4.1:
   ```yaml
   # WOLFGATE - Wolfmed phase 2. ONYX Resources/Prototypes/Body/species_base.yml:124-133
   # (<Onyx-PainSounds>). Key corrected from Onyx's `emotes:` to `emotesThreshold:` - see manifest.
   - type: EmoteOnDamage
     emotesThreshold:
       50: [ Scream ]
       80: [ Scream, Crying ]
     emoteChance: 0.6
     withChat: true
     hiddenFromChatWindow: true
     emoteCooldown: 8
   ```
   With `Emotes` left empty, WG's existing `OnDamage` body returns at `:33` (`Emotes.Count == 0`), so there is
   **no double emote**.

**Alternative if the orchestrator refuses any upstream `EmoteOnDamage` edit:** a `_WF` twin —
`Content.Server/_WF/Wolfmed/Chat/WolfmedPainEmoteComponent.cs` + `WolfmedPainEmoteSystem.cs` subscribing
`<WolfmedPainEmoteComponent, DamageChangedEvent>` (a free pair, new component). Zero upstream edits, but it
duplicates ~60 lines and drifts from Onyx at the next re-sync. The additive version above is 1 upstream line
plus 4 upstream datafields and is preferred.

**Does `DamageChangedEvent` still fire on a wound host?** Yes. `WoundDamageProjectionSystem.RefreshBodyDamage`
writes through the facade's `SetDamage`, which calls `DamageableSystem.DamageChanged(..., delta, interruptsDoAfters: false)`
(PLAN §2.1 / D30). WP9's `DamageChangedDeltaSurvivesProjectionTest` (T12) is the standing gate for that.

---

## 5. The `HighPainThreshold` trait

### 5.1 Onyx source (3 artefacts, all tiny)

- `Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` — `[RegisterComponent, NetworkedComponent]`,
  one `[DataField] public float PainMultiplier = 0.75f;`.
- `Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs` — 23 lines:
  ```csharp
  SubscribeLocalEvent<HighPainThresholdComponent, ModifyPainGainEvent>(OnModifyPainGain);
  …
  private void OnModifyPainGain(Entity<HighPainThresholdComponent> ent, ref ModifyPainGainEvent args)
      => args.Multiplier *= ent.Comp.PainMultiplier;
  ```
- `Resources/Prototypes/_Onyx/Traits/quirks.yml:28-37`:
  ```yaml
  - type: trait
    id: HighPainThreshold
    name: trait-high-pain-threshold-name
    description: trait-high-pain-threshold-desc
    category: Quirks
    cost: 3
    conflicts:
    - PainNumbness
    components:
    - type: HighPainThreshold
  ```
- `Resources/Locale/en-US/_Onyx/traits/quirks.ftl:13-14` — `trait-high-pain-threshold-name = High pain threshold`,
  `trait-high-pain-threshold-desc = You accumulate pain 25% slower, but still suffer its full consequences when it builds up.`
- ONYX also adds the reciprocal `conflicts: [HighPainThreshold]` to `Resources/Prototypes/Traits/disabilities.yml:96`.
- ONYX `Resources/Prototypes/Entities/Mobs/Player/clone.yml:18` also lists `HighPainThreshold` in a trait set —
  out of scope (no clone feature in WG).

### 5.2 Trait prototype shape: Onyx vs Wolfgate

| Field | Onyx | Wolfgate (`Content.Shared/Traits/TraitPrototype.cs`) |
|---|---|---|
| `id` / `name` / `description` | yes | **SAME** (`:15`, `:21`, `:27`) |
| `category` | yes | **SAME** — `ProtoId<TraitCategoryPrototype>? Category` (`:63`) |
| `cost` | yes | **SAME** — `int Cost = 0` (`:57`) |
| **`components`** | yes | **SAME** — `ComponentRegistry Components` (`:45`), applied by `Content.Server/Traits/TraitSystem.cs:49` `EntityManager.AddComponents(args.Mob, traitPrototype.Components, false);` |
| **`conflicts`** | Onyx-only (`Content.Shared/_Onyx/Traits/TraitPrototype.Conflicts.cs` partial) | **MISSING.** Wolfgate's equivalent is **`mutuallyExclusiveTraits`** — `HashSet<ProtoId<TraitPrototype>> MutuallyExclusiveTraits` (`:69`) |
| **`specials`** (`!type:ApplyStatusEffectSpecial`) | Onyx-only | **MISSING entirely.** No `specials`, no `TraitFunction`, no `functions:` — Wolfgate is on the pre-function upstream trait system |
| `functions:` | neither | **not present in either tree** — the task's "confirm the `functions`/`components` fields match" resolves to: **both trees use `components:`; neither uses `functions:`** |
| `traitGear`, `speciesBlacklist`, `languagesSpoken/Understood` | — | Wolfgate extras (`:51`, `:75`, `:82-100`), unused here |

`mutuallyExclusiveTraits` is enforced **client-side only**, in the lobby editor:
`Content.Client/Lobby/UI/HumanoidProfileEditor.xaml.cs:998`
`if (selProto.MutuallyExclusiveTraits.Contains(traitId) || thisProto.MutuallyExclusiveTraits.Contains(sel))` —
it checks **both directions**, so declaring the exclusion only on the new `_Onyx` entry is enough and
**`Resources/Prototypes/Traits/disabilities.yml` needs no edit**. `HumanoidCharacterProfile.WithTraitPreference`
(`Content.Shared/Preferences/HumanoidCharacterProfile.cs:423-468`) does **not** re-check exclusivity server-side
— an existing Wolfgate limitation, not something this port introduces.

`cost: 3` is inert: `Resources/Prototypes/Traits/categories.yml` gives `Quirks` **no `maxTraitPoints`**, and
`WithTraitPreference` short-circuits when `MaxTraitPoints` is null (`:440-445`, `:457`). Wolfgate's own
`quirks.yml` entries declare no cost at all. **Recommend dropping `cost:`** and recording it as a deliberate
deviation (Onyx's `<Onyx-TraitEconomy>` numbers assume a points budget Wolfgate's Quirks category does not have).

### 5.3 Port plan

| # | Path | Action |
|---|---|---|
| 1 | `WG/Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` | copy **verbatim** (keep the Onyx AGPL header per D6) |
| 2 | `WG/Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs` | copy **verbatim** — `Content.Shared._Onyx.Wounds.ModifyPainGainEvent` resolves (`WG/Content.Shared/_Onyx/Wounds/WoundEvents.cs:44`) |
| 3 | `WG/Resources/Prototypes/_Onyx/Traits/quirks.yml` | **new file, `HighPainThreshold` entry only** (same trim WP7 applied to `_Onyx/Alerts/alerts.yml`); `conflicts:` → `mutuallyExclusiveTraits:`; drop `cost:`; keep the Onyx SPDX header |
| 4 | `WG/Resources/Locale/en-US/_Onyx/traits/quirks.ftl` | **new file, the two `trait-high-pain-threshold-*` keys only** |

**Registration-name check:** `grep -rn "HighPainThreshold" WG` → zero hits in C#, YAML and FTL. The component
registers as `HighPainThreshold`; **free**.

**Subscription pair:** `<HighPainThresholdComponent, ModifyPainGainEvent>` — new component, and
`ModifyPainGainEvent` currently has **zero** subscribers anywhere in WG (grep). **Free.**

**Correctness check:** `ModifyPainGainEvent` is raised on the **body**, not the part
(`PainSystem.cs:232-240`: `target = part.Body ?? entity`; and `:109-115` raises on `bodyPart.Body`), and
`TraitSystem` adds the component to the player mob (`args.Mob`). The two agree. ✅

**Behaviour after the WP9 fix:** with `ModifyPainGainEvent(1f)` now seeded correctly, `PainMultiplier = 0.75`
really does cut pain gain and the wound-pain floor by 25 %. Before WP9 the multiplier was 0 and the trait was
indistinguishable from vanilla. This is the first build in which it is testable.

---

## 6. Status effects: what `PainSystem` and `WoundStatusEffectSystem` reference

The task asks for **the exact `EntProtoId<>` strings** these two files reference. The honest answer is that
**neither file contains a single `EntProtoId` literal**:

### 6.1 `PainSystem.cs` — applies **no** status effect

Its only StatusEffectNew contact is a **read** at `:329-330`:
```csharp
return _statusEffects.EnumerateStatusEffects<PainNumbnessStatusEffectComponent>(entity)
    .Any(effect => effect.Comp1.Applied);
```
`EnumerateStatusEffects<T>` returns `Entity<StatusEffectComponent, T>`
(`WG/Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs:435`), so `Comp1.Applied` is
`StatusEffectComponent.Applied` (`WG/Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs:40`) —
**SAME**, compiles, no shim needed.

**But nothing in Wolfgate can make that predicate true.**

| Artefact | ONYX | WG |
|---|---|---|
| `PainNumbnessStatusEffectComponent` (registers as `PainNumbnessStatusEffect`) | `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` | **SAME** — WP1 file #17, manifest row 29 |
| `PainNumbnessStatusEffectBase` (abstract entity, adds `- type: PainNumbnessStatusEffect`) | `Resources/Prototypes/Entities/StatusEffects/body.yml:26-36` | **MISSING** |
| `StatusEffectPainNumbness` (concrete) | `Resources/Prototypes/Entities/StatusEffects/body.yml:56-59` | **MISSING** |
| `TraitStatusEffectPainNumbness` (concrete, cloneable) | `Resources/Prototypes/Entities/StatusEffects/traits.yml:20-23` | **MISSING** |
| `MobStatusEffectDebuff`, `MobStatusEffectBase`, `StatusEffectBase` (parents) | `body.yml` / `misc.yml` | **SAME** — `WG/Resources/Prototypes/Entities/StatusEffects/misc.yml` |
| `TraitStatusEffectBase` (`- type: CloneableStatusEffect`) | `traits.yml:1-6` | **MISSING** (and `CloneableStatusEffect` is not among WP1's ported components) |
| Appliers | trait `specials: !type:ApplyStatusEffectSpecial` (`Resources/Prototypes/Traits/disabilities.yml:96-101`); reagents `!type:ModifyStatusEffect effectProto: StatusEffectPainNumbness` (`Resources/Prototypes/Reagents/narcotics.yml:48,556`) | **both MISSING** — Wolfgate's `TraitPrototype` has no `specials`, and the narcotics are phase 4 (D16/WP11) |
| `PainNumbnessSystem` | ONYX version is **status-effect-driven**: subscribes `<PainNumbnessStatusEffectComponent, StatusEffectAppliedEvent / StatusEffectRemovedEvent / StatusEffectRelayedEvent<BeforeForceSayEvent> / StatusEffectRelayedEvent<BeforeAlertSeverityCheckEvent>>` | **DIFFERENT** — WG's `Content.Shared/Traits/Assorted/PainNumbnessSystem.cs:14-17` is Mono's **component**-driven version on `PainNumbnessComponent` (`ComponentInit`, `ComponentRemove`, `BeforeForceSayEvent`, `BeforeAlertSeverityCheckEvent`) |
| WG's `PainNumbness` trait | — | `Resources/Prototypes/Traits/disabilities.yml:68-74`, grants `- type: PainNumbness` (the legacy component) |

**Consequence:** in Wolfgate today the `PainNumbness` trait suppresses the *old* brute vignette
(`DamageOverlayUiController.cs:98`), the health alert and the force-say prefix, but **does nothing to wound
pain** — `IsPainNumb` is permanently false, so a pain-numb character still gets the pain vignette (after §3),
still screams from pain shock, still gets the pain sounds, and still suffers the full pain stun.

**Recommended fix (1 line, vendored file):** a `// WOLFGATE` clause in `PainSystem.IsPainNumb` (`:324-331`):
```csharp
// WOLFGATE: Wolfgate's PainNumbness trait grants the legacy PainNumbnessComponent
// (Content.Shared/Traits/Assorted/PainNumbnessComponent.cs); Onyx's status-effect form
// (StatusEffectPainNumbness) has no applier here - no `specials:` on TraitPrototype, and the
// narcotics that apply it are phase 4. Honour both.
if (HasComp<PainNumbnessComponent>(entity))
    return true;
```
`Content.Shared.Traits.Assorted` is already `using`-ed at `PainSystem.cs:13`. **SAME** type, no shim.

**Deferred alternative (phase 4):** ship `PainNumbnessStatusEffectBase` + `StatusEffectPainNumbness` into
`WG/Resources/Prototypes/Entities/StatusEffects/misc.yml` when the narcotics land (they need
`!type:ModifyStatusEffect`, a StatusEffectNew entity effect that Wolfgate does not have either). The
`TraitStatusEffect*` chain needs `CloneableStatusEffect`, also unported. **Do not attempt in phase 2.**

Also note the relay path is live: `WG/Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs:55`
already relays `BeforeForceSayEvent` into status effects, so if the prototype ever ships, a ported
status-effect `PainNumbnessSystem` half would work — but **beware**: adding
`<PainNumbnessStatusEffectComponent, StatusEffectRelayedEvent<BeforeForceSayEvent>>` is a *different* pair from
WG's existing `<PainNumbnessComponent, BeforeForceSayEvent>` (`PainNumbnessSystem.cs:16`), so both can coexist.
`Content.Shared/Bed/Sleep/SleepingSystem.cs:70` orders `after: [typeof(PainNumbnessSystem)]` — keep any new
handler inside that same class or the ordering silently stops applying.

### 6.2 `WoundStatusEffectSystem.cs` — applies whatever a wound prototype names, and **no prototype names anything**

The system reads `WoundStatusEffectBehavior.StatusEffect`, typed
`EntProtoId<StatusEffectComponent>` (ONYX `Content.Shared/_Onyx/Wounds/WoundBehaviors.cs:100-102`,
**SAME** in WG), and calls `TryUpdateStatusEffectDuration` / `TrySetStatusEffectDuration` /
`TryRemoveStatusEffect` / `HasStatusEffect` — **all four SAME** in WG
(`StatusEffectSystem.API.cs:134`, `:91`, `:143`, `:169`).

`git -C C:/tmp/onyx grep -rn "WoundStatusEffectBehavior|!type:WoundStatusEffect" HEAD -- Resources/` →
**zero hits**. So at the Onyx pin **no wound declares a status-effect behavior**, and
`ApplyEffect`/`RemoveEffect`/`HasOtherOwner` are unreachable. WG's ported
`Resources/Prototypes/_Onyx/Wounds/wounds.yml` is byte-derived from Onyx's, so the same holds here.

**There are therefore zero status-effect prototype ids to check for the pain HUD.** The only StatusEffectNew
prototype id anywhere in the pain path is the *absent* `StatusEffectPainNumbness` of §6.1.

### 6.3 `_Onyx/StatusEffects/wounds.yml` — confirmed orphaned, skip

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
`git grep -n "StatusEffectBurnSlowdown|StatusEffectWoundImpairment" HEAD` → 4 hits, all of them the
prototype itself plus `Resources/Locale/ru-RU/_Onyx/prototypes/status-effects/wounds.ftl:1,3`. **No consumer.**

Consequently **`MovementModStatusEffectComponent`, the trimmed `MovementModStatusSystem` and the
`StatusEffectSlowdown` prototype chain are NOT needed by anything in phase 2's scope.** `FractureEffectSystem`
applies its movement penalty through `RefreshMovementSpeedModifiersEvent` / `ModifySpeed`, not through a status
effect (ONYX `FractureEffectsSystem.cs:97-110`). PLAN P2-1's conditional resolves to **skip**; WP1's
"deferred to WP10" row for `movement.yml` / `MovementModStatusEffectComponent` / `_Onyx/StatusEffects/wounds.yml`
should be reclassified from *deferred* to **not needed** in the manifest.

---

## 7. Prediction: what `PainSystem` does on client vs server

**Answer: nothing at all on the client. Pain is 100 % server-authoritative and fully replicated.** A HUD must
read networked component state and must **not** listen for pain events.

### 7.1 Every mutator is `_net.IsServer`-gated

| Method | Gate | Line |
|---|---|---|
| `Update` | `if (!_net.IsServer) return;` | `:129-130` |
| `SetPain` | `if (!_net.IsServer \|\| !Resolve(...))` | `:205` |
| `ApplyOneTimePain` | `if (!_net.IsServer \|\| !CanFeelPain(...))` | `:55` |
| `RefreshWoundPain` | `if (!_net.IsServer \|\| !Resolve(...))` | `:80` |
| `SuppressPain` | `if (!_net.IsServer \|\| …)` | `:336` |
| `ClearPainSuppression` | `if (!_net.IsServer \|\| …)` | `:358` |
| `DecayPainSuppression` | `if (!_net.IsServer \|\| …)` | `:372` |
| `OnRejuvenate` | `if (_net.IsServer)` | `:311` |
| `OnPainShockTargetStartup` | `if (_net.IsServer && …)` | `:47` |
| `ChangePain` / `ApplyDamage` | **not directly gated**, but every path ends in `SetPain`, so the client no-ops. (Side effect: `ChangePain` still raises `ModifyPainGainEvent` on the client before bailing — harmless, and `HighPainThresholdSystem` is idempotent) | `:225-245`, `:389-397` |

`WoundDamageProjectionSystem.SetupBody`/`RefreshBodyPain`, which `EnsureComp<PainComponent>` on the body and
each part, is likewise `_net.IsServer`-gated (`WG/Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs:207-208`).
**So the client never creates a `PainComponent` locally; it only ever receives one in an entity state.**

### 7.2 Every reader is unconditional and touches only replicated state

`GetPain` (`:165`), `GetPainBeforeAdrenaline` (`:182`), `GetRawPain` (`:198`), `CanFeelPain` (`:399`),
`CalculatePain` (`:406`), `IsPainNumb` (`:324`) have no net gate. Their inputs:

- `PainComponent` — `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]`
  (`WoundDamageComponents.cs:102`), with `[AutoNetworkedField]` on `Value` (`:105-106`), `Suppression` (`:108-109`)
  and `WoundPain` (`:115-116`). ✅
- `PainShockTargetComponent` — `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]`
  (`:145`), `[AutoNetworkedField]` `Armed` (`:148`) and `AdrenalineEnds` (`:151`). ✅
  `GetPain`'s adrenaline test compares `AdrenalineEnds > _timing.CurTime`; `IGameTiming.CurTime` is
  synchronised, so client and server agree to within one tick.
- `BodyPartComponent.Body` — Shitmed, networked. ✅
- StatusEffectNew containers, for `IsPainNumb`. ✅
- `PainComponent.{SoftPainCap, RecoveryPerSecond, DamageMultipliers}` are `[DataField]` **without**
  `[AutoNetworkedField]`. Because no prototype declares `- type: Pain`, both sides instantiate the component
  from the same C# field initialisers (`SoftPainCap = 135`), so they agree. **Caveat to record:** the day
  anyone wants a per-species `softPainCap`, they must declare `- type: Pain` in YAML *and* make the field
  `[AutoNetworkedField]`, or the HUD's denominator desyncs.
- `PainComponent.SuppressionModifiers` is `[ViewVariables]` only — **not networked**. It is never read by
  `GetPain`; only `GetRecoveryMultiplier` (server-side recovery) uses it. No HUD impact.

### 7.3 Consequences for the HUD

- **`PainChangedEvent` never fires on the client.** Every `RaisePainChanged` call sits inside a server-gated
  method. A client system must not subscribe it — it would compile and never run.
- **The correct client-side trigger** is `<PainComponent, AfterAutoHandleStateEvent>` — the component declares
  `raiseAfterAutoHandleState: true`. That pair is **free**: the only two `PainComponent` subscriptions in WG
  are `PainSystem.cs:42` (`RejuvenateEvent`) and — for `PainShockTargetComponent` — `:41` (`ComponentStartup`).
- **Update cadence:** `PainSystem.Update` runs its recovery/suppression/shock sweep once per second
  (`:132-137`), and `SetPain` early-returns when the value is unchanged (`:210-211`), so states arrive at
  roughly 1 Hz while pain is non-zero and stop entirely when it settles. The overlay's own lerp
  (`GetDiff`, `value * 5f * lastFrameTime`) smooths that fine — this is exactly the cadence Onyx runs at.
- **The §3.4 `Draw()` design needs no trigger at all** and is therefore immune to all of the above. It is the
  recommendation.
- **Mispredict note (D35):** wound-host damage routing is unpredicted in phase 1, so a client sees *no* damage
  and *no* pain until the server state lands. That is one round-trip of "nothing happened" on every hit —
  already an accepted phase-1 deviation; the pain HUD inherits it and does not make it worse.

---

## 8. Adjacent pain-visibility surfaces (not this work package — cross-references)

| Surface | Owner | Status |
|---|---|---|
| `_Onyx/HealthExaminable` — `HealthExaminableSystem.Pain.cs` maps pain to `light/strong/terrible/agony` at thresholds `>0 / ≥15 / ≥30 / ≥50`, rendered by `HealthExaminableSystem.PartStatus.cs:112` and only for `examined == examiner` | PLAN P2-1 "GUARD E2 + `_Onyx/HealthExaminable`" | locale keys `health-examinable-pain-*` live in ONYX `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl:1-4`, **not yet in WG** (WP7 copied only the four `wound-examine-fracture-*` keys out of that file into `wounds.ftl` — manifest Deviations, rows 356-359; **delete those four when the real file lands**) |
| Health analyzer pain readout (`health-analyzer-wound-pain`, `…-short`) | PLAN §6.1 → WP11 | `Content.Client/HealthAnalyzer/UI/HealthAnalyzerControl.xaml.cs:306-307` in Onyx; WG's analyzer is untouched |
| `PartDamageVisualsComponent` → per-limb damage sprites | not in P2-1's list | **shipped but inert**: `WoundDamageProjectionSystem` `EnsureComp`s and networks it, and Onyx's only consumer is `Content.Client/Damage/DamageVisualsSystem.cs:47,53,63,67,500,535` (plus `_Onyx/Wounds/{brute,burn}_damage.rsi`), none of which is ported. Worth its own phase-2/3 package — flagging so nobody assumes the port already draws wounds |
| Guidebook (`Resources/Locale/en-US/_Onyx/guidebook/wounds.ftl`, 90+ lines explaining pain/wounds to players) | unscoped | not ported; cheap and high-value once the systems are live |

---

## 9. Consolidated change set for the **pain-hud** package

### 9.1 New files

| # | Path | Source | Notes |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` | ONYX same path | verbatim, Onyx header |
| 2 | `Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs` | ONYX same path | verbatim |
| 3 | `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` | ONYX same path | 3 `// WOLFGATE` edits (§4.2.4 step 3); namespace stays `Content.Server.Chat.Systems` |
| 4 | `Resources/Prototypes/_Onyx/Traits/quirks.yml` | ONYX same path, trimmed | `HighPainThreshold` only; `conflicts:`→`mutuallyExclusiveTraits:`; drop `cost:` |
| 5 | `Resources/Locale/en-US/_Onyx/traits/quirks.ftl` | ONYX same path, trimmed | two keys only |

### 9.2 Modified files

| # | Path | Kind | Change |
|---|---|---|---|
| 6 | `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs` | **upstream — needs HOOK 14** | ~8-line pain read at the top of `Draw` (after `:64`); `:158` `0.8f` → `0.8f * level` |
| 7 | `Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs` | **upstream — needs HOOK 15** | one condition at `:98` so the brute/burn vignette is skipped on pain-capable entities |
| 8 | `Content.Server/Chat/EmoteOnDamageComponent.cs` | **upstream — extends HOOK-7-style precedent** | 4 additive `// WOLFGATE` datafields (§4.2.4 step 1) |
| 9 | `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` | **upstream — one line** | `HandlePainDamageEmote(uid, emoteOnDamage, args);` as the first statement of `OnDamage` (`:25`) |
| 10 | `Content.Shared/_Onyx/Wounds/PainSystem.cs` | vendored `_Onyx` | one `// WOLFGATE` clause in `IsPainNumb` (`:324`) honouring the legacy `PainNumbnessComponent` (§6.1) |
| 11 | `Resources/Prototypes/Entities/Mobs/Species/base.yml` | upstream YAML, existing WOLFGATE block at `:250` | add `- type: PainShockTarget` and the `- type: EmoteOnDamage` block (§4.1, §4.2.4 step 4) |
| 12 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | doc | rows for 1-11 + deviations: Onyx's broken `emotes:` key, dropped `cost:`, `conflicts:`→`mutuallyExclusiveTraits:`, `IsPainNumb` widening, "no pain alert exists" |

### 9.3 Every `SubscribeLocalEvent<Component, Event>` pair this package registers

| Pair | Registrant | Conflict check |
|---|---|---|
| `<HighPainThresholdComponent, ModifyPainGainEvent>` | `HighPainThresholdSystem` (file 2) | **FREE** — `HighPainThreshold` absent from WG entirely; `ModifyPainGainEvent` has zero subscribers in WG |
| *(none)* | file 3 `EmoteOnDamageSystem.PainSounds.cs` | **deliberately registers nothing** — `<EmoteOnDamageComponent, DamageChangedEvent>` is already owned by `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs:22`; the pain path is called from that handler. A second subscription would throw `Duplicate Subscriptions` at server start (`RobustToolbox/Robust.Shared/GameObjects/EntityEventBus.Directed.cs:407,419`) |
| *(none)* | files 6/7 | `UIController` has only broadcast subscriptions, and the `Draw()` design adds none |
| `<PainComponent, AfterAutoHandleStateEvent>` | **only if** the orchestrator picks the rejected client-system variant instead of §3.4 | **FREE** — the only `PainComponent` subscription in WG is `PainSystem.cs:42` (`RejuvenateEvent`) |
| `<WolfmedPainEmoteComponent, DamageChangedEvent>` | **only if** the `_WF`-twin fallback of §4.2.4 is chosen | **FREE** — new component |

No component-registration collisions: `HighPainThreshold` is the only new registered name and it is absent
from WG C#, YAML and FTL.

### 9.4 Tests this package should add (feeding P2-2)

1. **T-PAIN-OVERLAY** — spawn a `MobHuman`, drive pain to a known value, assert
   `min(1, PainSystem.GetPain(body) / PainComponent.SoftPainCap)` equals the expected level and that it floors
   to 0 below 6.75. (Replaces P2-2's impossible "pain-alert assertion"; §2.3.)
2. **T-PAIN-SHOCK** — with `- type: PainShockTarget` on the mob, push pain past 130 and assert
   `StunnedComponent`/`KnockedDownComponent`, `JitteringComponent`, `PainShockTargetComponent.Armed == false`
   and `AdrenalineEnds != null`; then assert `GetPain` drops by exactly the ×0.7 adrenaline factor. This is the
   first end-to-end exercise of WP9 open item 1.
3. **T-HIGH-PAIN-THRESHOLD** — same damage on a mob with and without `HighPainThresholdComponent`; assert the
   pain ratio is 0.75 ± rounding. Also a canary that `ModifyPainGainEvent`'s default is still 1 after the WP9 fix.
4. **T-PAIN-NUMB** — with the §6.1 widening, assert `HasComp<PainNumbnessComponent>` ⇒ `GetPain == 0` and
   no pain-shock emote.
5. **T-PAIN-EMOTE** — total damage ≥ 50 with a ≥ 6 allowed-type delta emits an emote; a Caustic-only hit below
   `PainThreshold` does not; a Critical mob does not. Pin `EmoteChance` to 1 for the test's duration
   (same pattern WP9 used for `CCVars.SurgeryScarChance` in `WoundScarTest`).

**Harness note (project memory + PLAN §6):** run `DockTest` first before blaming Wolfmed for a pair failure;
lint YAML in **Release**; `[TestPrototypes]` ids are pool-global, so do not reuse WP9's `Wound*Body*` ids.

---

## 10. Open questions for the orchestrator

1. **P2-2's "pain-alert assertion" has no target** (§2.3). Drop it, or commission a Wolfgate-only `Pain` alert
   as new design? *Recommendation: drop; assert the overlay level instead.*
2. **Authorise HOOK 14 / HOOK 15** (§3.4) — the two upstream client files are not on PLAN §3's list. Without
   them there is no pain HUD at all; the only alternative is the §3.5 full refactor.
3. **Authorise the additive `EmoteOnDamageComponent` datafields + the one-line `OnDamage` call** (§4.2.4), or
   take the `_WF`-twin fallback. *Recommendation: additive upstream — 1 line + 4 datafields, re-syncable.*
4. **Confirm the `IsPainNumb` widening** (§6.1). Without it Wolfgate's `PainNumbness` trait is decorative
   against wound pain, and `HighPainThreshold`'s `mutuallyExclusiveTraits: [PainNumbness]` guards nothing.
5. **Reclassify `MovementModStatusEffectComponent` / `MovementModStatusSystem` / `StatusEffectSlowdown` /
   `_Onyx/StatusEffects/wounds.yml` from "deferred to WP10" to "not needed"** in the manifest (§6.3), unless a
   *different* phase-2 package (fractures?) turns out to need `MovementModStatusEffect` for a reason outside
   the wound set.
6. **`PartDamageVisualsComponent` is networked but has no consumer in WG** (§8). Is per-limb damage sprite art
   in phase 2, phase 3, or never? It is currently paying networking cost for nothing.
