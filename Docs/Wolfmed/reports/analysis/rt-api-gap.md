# RobustToolbox 289→277 / Content-API gap scan — Wolfmed wound-system port

Scope: every file listed in the task under `Content.Shared/_Onyx/Wounds`, `_Onyx/Chemistry/Circulation`,
`Content.Shared/StatusEffectNew/**`, `_Onyx/Medical/Tourniquet`, `_Onyx/HealthExaminable/**`,
`_Onyx/Targeting/**`, `_Onyx/Damage/GroupHealSpecifier.cs`, `_Onyx/Damage/Systems/DamageableSystem.API.cs`,
`_Onyx/Body/{OrganDamageComponent,FunctionalOrganComponent,OrganConsequenceComponents}.cs` +
`Systems/OrganHealthSystem.cs`, `Content.Server/_Onyx/Medical/{MedicalPatchComponent,MedicalPatchSystem}.cs`.
70 files, all confirmed present in the `C:\tmp\onyx` sparse checkout (pinned `2f5bab9`). File list is in
the appendix.

Method: grepped every file in scope for `using Robust.*`, every direct engine member call, and every
Content-level helper call (`_random.`, `_timing.`, `_damage.`, `_hands.`, `_stun.`, `_chat.`, `_mobState.`,
`_sticky.`, `_throwing.`, `_audio.`, `_doAfter.`, `_popup.`, `_alerts.`, `_containers.`, `_prototypes.`,
`_inventory.`, `EntityQueryEnumerator<...>`, `[AutoNetworkedField]`/`[AutoGenerateComponentState]`, `ProtoId</EntProtoId`,
`Dirty(`, `AsNullable()`). Every finding below was checked against the actual source at
`WG/RobustToolbox` (v277.0.0, confirmed via `RobustToolbox/MSBuild/Robust.Engine.Version.props:3`) and
`WG/Content.Shared` / `WG/Content.Server` — nothing here is taken on the Onyx side's word or from memory.

## TL;DR — 5 most important findings

1. **RobustToolbox 277 itself is not the problem.** Every RT-layer API Onyx's wound code touches —
   `PredictedQueueDel`/`PredictedDel`, `EntityQueryEnumerator<T1..T4>`, `AutoGenerateComponentState(fieldDeltas: true)`,
   `AutoGenerateComponentPause`, generic `EntProtoId<T>`, `Entity<T>.AsNullable()`, `Dirty(Entity<T>)`,
   `PredictedTrySpawnInContainer`, container helpers, `TryIndex<T>(ProtoId<T>)` — **already exists in
   RT 277 with matching signatures**. The 289→277 engine bump is a non-issue for this file set. All the
   real gaps are in **Wolfgate's Content layer**, not the engine.
2. **`DamageableSystem.TryChangeDamage` has an incompatible calling convention**, not just missing
   overloads. Onyx calls it as `bool TryChangeDamage(target, damage, out DamageSpecifier appliedDamage, ignoreResistances:, interruptsDoAfters:, origin:, ignoreGlobalModifiers:)`
   (`WoundDamageRoutingSystem.cs:704`, `:921`). Wolfgate's version returns `DamageSpecifier?` directly and
   has no `out` parameter at all (`Content.Shared/Damage/Systems/DamageableSystem.cs:190`) — this is a
   hard signature mismatch, not an overload gap, and it's the single most-called API in the whole port
   (used to actually apply every wound). Confirms and sharpens D5.
3. **`GetAllDamage`, `GetPositiveDamage`, `ClearAllDamage`, `CanBeDamagedBy`, `ChangeDamage`,
   `HealEvenly`, `HealDistributed`, `TryApplyPartDamage`** are all genuinely absent from Wolfgate's
   `DamageableSystem` (confirmed by listing every `public` member of the class — none of these eight
   names appear anywhere in `Content.Shared/Damage` or `Content.Shared/_Shitmed`). These are called from
   nearly every wound file (`AmputationSystem`, `WoundDamageProjectionSystem`, `WoundDamageRoutingSystem`,
   `WoundFractureSystem`, `WoundHealingSystem`, `HealthExaminableSystem.PartStatus`, `ReagentTreatmentSystems`,
   `TourniquetSystem`) — this is D5's "new-style DamageableSystem API" claim, now itemized per call site.
4. **Circulation is a hard compile blocker, not a tuning issue.** `CirculatoryStreamSystem.cs` (Content.Shared)
   subscribes `SubscribeLocalEvent<MetabolizerComponent, ComponentStartup>` and calls `TryComp`/`EnsureComp`/`RemComp<MetabolizerComponent>`
   at 8 call sites. In Wolfgate, `MetabolizerComponent` lives in **`Content.Server.Body.Components`**
   (`Content.Server/Body/Components/MetabolizerComponent.cs:12`) — a Content.Shared file cannot reference
   a Content.Server type at all (wrong assembly-reference direction), so this file cannot compile against
   Wolfgate as-is, independent of any method-signature question. This is the same "Bloodstream/Healing/Bed
   are server-side here" problem the handoff already flagged, but it also hits Circulation/Metabolizer,
   which the handoff didn't call out.
5. **Two cross-side (shared-vs-server) gaps beyond D5's list:** `SharedChatSystem.TryEmoteWithChat` (used
   by `PainSystem.cs:297`, a Content.Shared system) exists only on the server-only `Content.Server.Chat.Systems.ChatSystem`
   partial (`ChatSystem.Emote.cs:60,85`) — not on `SharedChatSystem` at all. And `SharedStunSystem.TryUpdateParalyzeDuration`
   (`PainSystem.cs:291`) doesn't exist under any name in Wolfgate's `SharedStunSystem` — Wolfgate only has
   `TryStun`/`TryKnockdown`/`TryParalyze(uid, time, refresh, status?)`, and Wolfgate's own `InstrumentSystem.cs:464`
   carries a code comment confirming this was a deliberate divergence: `// Mono - Wizden does TryUpdateParalyzeDuration here`.

No blocker invalidates the port; all five are addressable with `_WF/Wolfmed` compat shims or small
`// WOLFGATE` hooks per D5's strategy. Section 3 gives a concrete substitute for each.

---

## Part 1 — RobustToolbox (Robust.Shared / Robust.Client) engine API

Everything in this table was grepped directly out of `WG/RobustToolbox` (v277.0.0). All exist.

| API | Used in (Onyx file:line) | Exists in RT 277? | Signature / note |
|---|---|---|---|
| `PredictedQueueDel(EntityUid\|Entity<MetaDataComponent?>)` | `StatusEffectNew/StatusEffectsSystem.cs:56,116`, `StatusEffectSystem.API.cs:159` | Yes | `EntitySystem.Proxy.cs:852-894`; also `IEntityManager.PredictedQueueDeleteEntity` (`EntityManager.cs:738-775`) |
| `PredictedDel(...)` | not called directly in this file set (Robust itself uses it) | Yes | `EntitySystem.Proxy.cs:836-844`, `SharedContainerSystem.cs:634` |
| `EntityQueryEnumerator<T1..T4>()` | `PainSystem.cs:136,140,154`, `WoundBleedingSystem.cs:116`, `WoundInternalBleedingSystem.cs:57`, `CirculatoryStreamSystem.cs:51`, `StatusEffectsSystem.cs:42`, `Targeting/PartStatusSystem.cs:32` (server), `Body/Systems/OrganHealthSystem.cs:27`, `Medical/MedicalPatchSystem.cs:35` | Yes | Struct + accessor defined on `EntityManager.Components.cs:1450-1490`; proxied on `EntitySystem.Proxy.cs:1126-1158` — instance form (`EntityQueryEnumerator<T>()`, no `EntityManager.` prefix) works exactly as Onyx calls it |
| `[AutoGenerateComponentState(raiseAfterAutoHandleState: true)]` | `WoundDamageComponents.cs:95,102`, `Targeting/PartStatusComponent.cs:7`, `Targeting/TargetingComponent.cs:5` | Yes | `Analyzers/ComponentNetworkGeneratorAuxiliary.cs:55`, ctor `(bool raiseAfterAutoHandleState = false, bool fieldDeltas = false)` |
| `[AutoGenerateComponentState(fieldDeltas: true)]` | `StatusEffectNew/Components/StatusEffectComponent.cs:12` | Yes | Same ctor as above; Wolfgate already uses `fieldDeltas: true` itself on `AudioComponent`/`EyeComponent` |
| `[AutoGenerateComponentPause]` | `Chemistry/Circulation/CirculatoryStreamComponent.cs:7`, `StatusEffectComponent.cs:12` | Yes | `Analyzers/ComponentPauseGeneratorAttributes.cs` |
| `[AutoNetworkedField]` / `[AutoPausedField]` | throughout `WoundDamageComponents.cs`, `CirculatoryStreamComponent.cs`, `StatusEffectComponent.cs`, `TourniquetComponent.cs`, `TargetingComponent.cs`, `TargetingSnapshotComponent.cs`, `OrganConsequenceComponents.cs` (≈45 fields total) | Yes | Standard RT source-gen attributes, present unchanged |
| `NetEntity`, `GetNetEntity(...)`, `GetEntity(...)` | `Tourniquet/TourniquetComponent.cs:28,30`, `TourniquetSystem.cs:69,85` | Yes | Standard `EntitySystem` proxy members |
| `ProtoId<T>` | pervasive (≈60 occurrences across `Wounds`, `Chemistry/Circulation`, `Damage`, `Body`, `StatusEffectNew`) | Yes | `RobustToolbox/Robust.Shared/Prototypes/ProtoId.cs:18` |
| `EntProtoId` (non-generic) | `StatusEffectNew/Components/PermanentStatusEffectsComponent.cs:16`, `StatusEffectsSystem.cs` (≈10 call sites) | Yes | `Robust.Shared/Prototypes/EntProtoId.cs:20` |
| **`EntProtoId<T>` (generic)** | `WoundBehaviors.cs:102` (`EntProtoId<StatusEffectComponent> StatusEffect`), `WoundStatusEffectSystem.cs:199` | **Yes** | `Robust.Shared/Prototypes/EntProtoId.cs:62` — `readonly record struct EntProtoId<T>(string Id) where T : IComponent, new()`. Worth flagging explicitly since a generic `EntProtoId<T>` is easy to assume is a 289-only addition; it isn't. |
| `IPrototypeManager.TryIndex<T>(ProtoId<T>, out T?)` / `TryIndex(EntProtoId, out EntityPrototype?)` / `TryIndex(Type, string, out IPrototype?)` | pervasive (≈30 call sites, e.g. `WoundSystem.cs:102,111,178`, `CirculatoryStreamSystem.cs:62,258,297,314`) | Yes | `Robust.Shared/Prototypes/IPrototypeManager.cs:136,214,247-273` |
| `Entity<T>.AsNullable()` | `PainSystem.cs:307,308,445`, `ReagentTreatmentSystems.cs:19,42,61`, `WoundBleedingSystem.cs:98,99`, `WoundSystem.cs:87` | Yes | `Robust.Shared/GameObjects/Entity.cs:62` (and the 2-8 arity overloads) |
| `Dirty(Entity<T> ent)` / `Dirty(EntityUid, IComponent)` | ≈25 call sites across `Wounds/*` | Yes | `EntitySystem.Proxy.cs:204-247` (generic `Entity<T>`/`Entity<T1,T2>` forms) and `EntityManager.cs:412` (`EntityUid, IComponent` form) |
| `EntityWhitelistSystem`, `Content.Shared.Whitelist.EntityWhitelist` | `StatusEffectNew/StatusEffectsSystem.cs:18` | Yes (Content, not RT, but checked since Onyx treats it as given) | `Content.Shared/Whitelist/EntityWhitelistSystem.cs` |
| `SharedContainerSystem.EnsureContainer<Container>`, `TryGetContainer`, `Insert`, `ShutdownContainer` | `WoundSystem.cs:61,154,205-207,362,374`, `StatusEffectsSystem.cs:72` | Yes | `Robust.Shared/Containers/SharedContainerSystem.cs:129-175`, `SharedContainerSystem.Insert.cs:30` |
| `PredictedTrySpawnInContainer(...)` | `StatusEffectNew/StatusEffectsSystem.cs:187` | Yes | `EntityManager.Spawn.cs:267`, proxied `EntitySystem.Proxy.cs:1019` |
| `[Dependency] EntityQuery<T>` (struct injected as a dependency) | `StatusEffectNew/StatusEffectsSystem.cs:20-21` (`EntityQuery<StatusEffectContainerComponent>`, `EntityQuery<StatusEffectComponent>`) | Yes | Not RT-specific to verify in isolation, but confirmed working in Wolfgate today: `Content.Shared/Gravity/SharedGravitySystem.cs:20-22`, `Content.Shared/Lathe/SharedLatheSystem.cs:33` already use this exact pattern |
| `Loc.GetString(...)` / `ILocalizationManager` | pervasive | Yes | Standard |
| `IRobustRandom.Prob`/`NextFloat`/`Pick` | `AmputationSystem.cs:189`, `OrganDamageSystem.cs:45,61,95,97`, `WoundDamageRoutingSystem.cs:230,439,899`, `WoundFractureSystem.cs:49`, `WoundScarSystem.cs:53`, `WoundSystem.cs:228,245`, `TargetResolverSystem.cs:101` | Yes | `Robust.Shared/Random/IRobustRandom.cs:23` (`NextFloat`), extension `Prob` at line 195 |
| `IGameTiming.CurTime` / `.RealTime` | pervasive (`PainSystem`, `WoundBleedingSystem`, `CirculatoryStreamSystem`, `StatusEffectsSystem`, `Server/Targeting/TargetingSystem.cs:40,43,63`, `Client/Targeting/TargetingSystem.cs:65,77`, `MedicalPatchSystem.cs:38,40`) | Yes | `Robust.Shared/Timing/IGameTiming.cs:33,39` |
| `ComponentRegistry` | `Body/FunctionalOrganComponent.cs:12` | Yes | Standard RT type |
| `[ByRefEvent]` record-struct events | `OrganConsequenceComponents.cs`, `WoundEvents.cs`, `FunctionalOrganComponent.cs` | Yes | Standard |
| `Robust.Client.UserInterface.Controllers.{UIController, IOnSystemChanged<T>, IOnStateEntered<T>}` | `Client/_Onyx/Targeting/UI/TargetingUIController.cs:11` | Yes | `RobustToolbox/Robust.Client/UserInterface/Controllers/IOnSystemChanged.cs`, `IOnStateEntered.cs` |
| `IResourceCache`, `IoCManager.Resolve<T>()` | `TargetingControl.xaml.cs:12`, `HealthAnalyzerStatusDoll.xaml.cs:15` | Yes | Standard client IoC |
| `Robust.Shared.Utility.MarkupNode` | `Client/_Onyx/HealthExaminable/PartStatusTag.cs` | Yes | `Robust.Shared/Utility/MarkupNode.cs` |

**Not used anywhere in this file set** (task asked to check for them explicitly): `PredictedRandom` (0
occurrences), `EntityCoordinates` (0 direct occurrences — Onyx's circulation/wound code stays entity-relative,
no coordinate math), `SpawnAtPosition`/`PredictedSpawnAtPosition` (0 occurrences; the only predicted-spawn
call is `PredictedTrySpawnInContainer`, confirmed above), `IEntityManager`/`EntityManager.` used as a direct
static-style prefix (0 occurrences — all access goes through system proxy members or `[Dependency]` fields).
`ComponentRegistry` is used only as a data-field type on `FunctionalOrganComponent`, never queried/spawned
with it directly in this file set.

## Part 2 — Content-level helper APIs Onyx treats as given

| API | Onyx call site | Exists in Wolfgate? | Detail |
|---|---|---|---|
| `SharedPopupSystem.PopupEntity(string, EntityUid, EntityUid, PopupType)` and `(string, EntityUid, EntityUid)` | `WoundBleedingSystem.cs:104-105`, `Tourniquet/TourniquetSystem.cs:55,61,92` | **Yes** | `Content.Shared/Popups/SharedPopupSystem.cs:91` (4-arg, `PopupType` optional) — exact match, both call shapes compile |
| `SharedAudioSystem.PlayPredicted(SoundSpecifier?, EntityUid, EntityUid?)` | `WoundBleedingSystem.cs:106`, `TourniquetSystem.cs:65,91` | **Yes** | `RobustToolbox/Robust.Shared/Audio/Systems/SharedAudioSystem.cs:652` |
| `SharedDoAfterSystem.TryStartDoAfter(DoAfterArgs)` + `DoAfterArgs` ctor, `.NeedHand`, `.BreakOnMove` | `TourniquetSystem.cs:66-77` | **Yes** | `Content.Shared/DoAfter/SharedDoAfterSystem.cs:176`, `DoAfterArgs.cs:99,120,210` |
| `ThrowingSystem.TryThrow(EntityUid, Vector2, baseThrowSpeed:, pushbackRatio:, doSpin:)` | `AmputationSystem.cs:125-126` | **Yes** | `Content.Shared/Throwing/ThrowingSystem.cs:96` — every named arg Onyx uses exists with the same defaults |
| `MobStateSystem.IsDead(EntityUid, MobStateComponent?)` | `CirculatoryStreamSystem.cs:72`, `OrganHealthSystem.cs:37` | **Yes** | `Content.Shared/Mobs/Systems/MobStateSystem.cs:65` |
| `MobStateSystem.IsIncapacitated(...)` | `Server/Targeting/TargetingSystem.cs:37` | **Yes** | `MobStateSystem.cs:78` |
| `MobStateSystem.HasState(EntityUid, MobState, MobStateComponent?)` | `Body/Systems/OrganHealthSystem.cs:38` | **Yes** | `Content.Shared/Mobs/Systems/MobStateSystem.StateMachine.cs:18` — note: lives in a *different partial file* than the rest of `MobStateSystem`'s public API; easy to miss on a first grep of `MobStateSystem.cs` alone |
| `MobStateSystem.ChangeMobState(EntityUid, MobState, MobStateComponent?, EntityUid? origin)` | `OrganHealthSystem.cs:39` | **Yes** | `MobStateSystem.StateMachine.cs:48` — 4-arg signature matches exactly |
| `StickySystem.UnstickFromEntity(Entity<StickyComponent>, EntityUid user)` | `Medical/MedicalPatchSystem.cs:42` | **Yes** | `Content.Shared/Sticky/Systems/StickySystem.cs:182` |
| `InventorySystem.RelayEvent<T>(Entity<InventoryComponent>, T)` | `WoundDamageRoutingSystem.cs:681` | **Yes** | `Content.Shared/Inventory/InventorySystem.Relay.cs:101,118` |
| `InventorySystem.TryGetSlots` | not actually called anywhere in this file set (task asked to check it) | Yes, but N/A here | `Content.Shared/Inventory/InventorySystem.Slots.cs:175` — present, just unused by these files |
| `SharedJitteringSystem.DoJitter(EntityUid, TimeSpan, bool, float, float)` | `PainSystem.cs:299` (`_jitter.DoJitter(entity, PainShockStunTime, true, 20f, 7f)`) | **Yes** | `Content.Shared/Jittering/SharedJitteringSystem.cs:46` — 5-positional-arg form matches |
| `ItemToggle*` | not called anywhere in this file set | N/A | Task listed it as a thing to check; it's simply not touched by any of the 70 files |
| **`SharedStunSystem.TryUpdateParalyzeDuration(EntityUid, TimeSpan)`** | `PainSystem.cs:291` | **No** | Wolfgate's `SharedStunSystem` (`Content.Shared/Stunnable/SharedStunSystem.cs:196,220,244`) only has `TryStun`/`TryKnockdown`/`TryParalyze(EntityUid uid, TimeSpan time, bool refresh, StatusEffectsComponent? status = null)` — no "update duration without re-triggering the stun VFX/jitter" variant exists under any name. Wolfgate's own `Content.Server/Instruments/InstrumentSystem.cs:464` has a comment acknowledging this: `_stuns.TryParalyze(mob, TimeSpan.FromSeconds(1), true); // Mono - Wizden does TryUpdateParalyzeDuration here`. **Substitute:** call `TryParalyze(uid, time, refresh: true)` — slightly different semantics (re-triggers stun/knockdown jitter each refresh instead of silently extending the timer), so the pain-shock rearm behavior (110/130 thresholds) needs a `// WOLFGATE` note when ported. |
| **`SharedChatSystem.TryEmoteWithChat(EntityUid, string, ChatTransmitRange, ...)`** | `PainSystem.cs:297-298` (dependency typed as `SharedChatSystem`, `Content.Shared`) | **No, on `SharedChatSystem`** | The method exists with an identical signature, but only on the server-only partial `Content.Server.Chat.Systems.ChatSystem` (`Content.Server/Chat/Systems/ChatSystem.Emote.cs:60,85`). `Content.Shared/Chat/SharedChatSystem.cs` has no `TryEmoteWithChat` member at all — a `[Dependency] private SharedChatSystem _chat` field in a Content.Shared system (as Onyx's `PainSystem` uses) cannot call it. This is the same "Bloodstream/Healing/Bed are server-side in Wolfgate, shared/predicted in Onyx" pattern the handoff already names for other systems — it also applies to the scream-on-pain-shock emote. **Substitute:** raise a shared event (e.g. `PainShockEmoteEvent`) from `PainSystem` and have a small server-only `_WF/Wolfmed` system call the real `ChatSystem.TryEmoteWithChat`, or move the scream trigger server-side outright. |
| **`SharedHandsSystem.GetActiveHand(...)` returning a hand-slot `string?`** | `FractureEffectsSystem.cs:139` (`handId = _hands.GetActiveHand((body, hands));` where `handId` is declared `string?` at line 131), `WoundDamageRoutingSystem.cs:549` | **No — return type differs** | Wolfgate's `GetActiveHand(Entity<HandsComponent?> entity)` returns `Hand?` (`Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:177`), an object, not the hand-slot id string Onyx's RT289-era Hands API returns. |
| **`SharedHandsSystem.IsHolding(Entity<HandsComponent?>, EntityUid, out string? handId)`** | `FractureEffectsSystem.cs:135` | **No — overload absent** | Wolfgate has `IsHolding(Entity<HandsComponent?>, EntityUid?)` → `bool` with no `out` at all (`SharedHandsSystem.cs:280`), and a second overload `IsHolding(EntityUid, EntityUid?, out Hand? inHand, HandsComponent?)` (`SharedHandsSystem.cs:285`) whose `out` parameter is a `Hand?`, not a `string?`. Onyx's 3-arg tuple-entity form with `out string?` doesn't exist under any name. **Substitute:** use Wolfgate's `IsHolding(uid, item, out Hand? inHand)` and read `inHand.Value.Name` for the hand-slot id, or `TryGetActiveHand`/`GetActiveHand` returning `Hand?` directly and skip the string round-trip — `TryGetHand(EntityUid, string handId, out Hand? hand)` (`SharedHandsSystem.cs:306`) is otherwise identical in both, so only the "get the id in the first place" step needs adapting. |

## Part 3 — Confirmed compile-blocking / behavior-changing gaps, ranked

Each of these is independently verified against the Wolfgate source (file:line cited on both sides), not
inferred from D5's summary — D5's claims are all reproduced here with corroborating call sites and,
where the shape differs from a simple "missing method," the exact incompatibility.

### 3.1 `DamageableSystem` new-style API — the D5 gap, itemized

Wolfgate's complete public surface of `Content.Shared/Damage/Systems/DamageableSystem.cs` is: `Initialize`,
`SetDamage(EntityUid, DamageableComponent, DamageSpecifier)`, `DamageChanged`, `TryChangeDamage(EntityUid?, DamageSpecifier, bool, bool, DamageableComponent?, EntityUid?, bool, float, bool?, bool?, float?, TargetBodyPart?, EntityUid?, DamageOriginFlag?)`,
`ApplyUniversalAllModifiers`, `SetAllDamage`, `ChangeAllDamage`, `SetDamageModifierSetId`, `GetDamages`
(confirmed by listing every `public` member — no other names exist, and a targeted grep for
`GetAllDamage|GetPositiveDamage|HealEvenly|HealDistributed|ClearAllDamage|CanBeDamagedBy|public.*ChangeDamage(`
across `Content.Shared/Damage` and `Content.Shared/_Shitmed` returns nothing beyond `TryChangeDamage`
itself). Public field: `UniversalTopicalsHealModifier` (`get; private set;`, line 49) — **this one does
exist** and Onyx's read-only use of it (`WoundHealingSystem.cs:117`) is fine as-is.

Missing, with every call site in scope:

- **`TryChangeDamage(EntityUid, DamageSpecifier, out DamageSpecifier, ...)`** (bool return + out-param
  convention) — `WoundDamageRoutingSystem.cs:704`, `:921`. Wolfgate's `TryChangeDamage` (line 190) returns
  `DamageSpecifier?` and has no `out` parameter — not an overload gap, a different calling convention
  entirely. The 2-arg calling forms elsewhere (`ReagentTreatmentSystems.cs:19`, `TourniquetSystem` is
  actually a different call, see below) that don't use the `out` form line up fine with Wolfgate's
  existing `TryChangeDamage`.
- **`GetAllDamage(Entity<DamageableComponent?>)`** → `DamageSpecifier` — `AmputationSystem.cs:78,182`,
  `WoundDamageRoutingSystem.cs:158,161,520,534`.
- **`GetPositiveDamage(Entity<DamageableComponent?>)`** → `DamageSpecifier` — `WoundDamageProjectionSystem.cs:133,171`,
  `WoundDamageRoutingSystem.cs:373,380,734,805,814,818`, `WoundFractureSystem.cs:65`, `WoundHealingSystem.cs:65,118,135`,
  `HealthExaminableSystem.PartStatus.cs:44`.
- **`ClearAllDamage(Entity<DamageableComponent?>)`** — `WoundDamageProjectionSystem.cs:56`.
- **`SetDamage(EntityUid, DamageSpecifier)`** (2-arg, no explicit component) — `WoundDamageProjectionSystem.cs:183`.
  Wolfgate's only `SetDamage` (line 143) requires the `DamageableComponent` explicitly as a 2nd positional
  arg — a distinct, additional signature gap from the missing methods above.
- **`CanBeDamagedBy(EntityUid, ProtoId<DamageTypePrototype>)`** → `bool` — `WoundDamageProjectionSystem.cs:155`,
  `WoundDamageRoutingSystem.cs:985`.
- **`ChangeDamage(EntityUid, DamageSpecifier, bool, bool, EntityUid?)`** (distinct from `TryChangeDamage`,
  presumably a non-nullable/always-succeeds variant) — `WoundDamageRoutingSystem.cs:621`.
- **`HealEvenly(Entity<DamageableComponent?>, DamageSpecifier, ProtoId<DamageGroupPrototype>? group)`** —
  `ReagentTreatmentSystems.cs:42`.
- **`HealDistributed(Entity<DamageableComponent?>, DamageSpecifier, ProtoId<DamageGroupPrototype>? group)`** —
  `ReagentTreatmentSystems.cs:61`.
- **`TryApplyPartDamage(EntityUid body, EntityUid part, DamageSpecifier, EntityUid user)`** —
  `Tourniquet/TourniquetSystem.cs:90`. Not found under any name in `Content.Shared/Damage` or
  `Content.Shared/_Shitmed` — this looks like a convenience wrapper Onyx expects around its own routing
  system rather than a stock upstream method; likely needs to become a `_WF/Wolfmed` helper that calls
  into `WoundDamageRoutingSystem`/Shitmed's targeted-damage path directly instead.

Also: **`DamageableSystem.API.cs`** (the file explicitly in scope) is itself broken against Wolfgate as
shipped — it's a `partial class DamageableSystem` (matches Wolfgate's class being `partial`, so the file
attaches fine) but calls `ProtoMan.TryIndex(...)` at lines 38 and 98. Wolfgate's field is named
`_prototypeManager` (`DamageableSystem.cs:27`, `[Dependency] private IPrototypeManager _prototypeManager`),
not `ProtoMan`. Trivial one-line `// WOLFGATE` fix (rename the identifier, or add a `private IPrototypeManager ProtoMan => _prototypeManager;` alias), but it does mean this file cannot be vendored byte-for-byte.

### 3.2 Circulation depends on a server-only component from Content.Shared

`Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` is declared in `Content.Shared._Onyx.Chemistry.Circulation`
and:
- `SubscribeLocalEvent<MetabolizerComponent, ComponentStartup>(OnMetabolizerInit)` — line 38
- `TryComp(entity, out MetabolizerComponent? metabolizer)` — lines 111, 117, 129
- `EnsureComp<MetabolizerComponent>(body)` — line 164
- `RemComp<MetabolizerComponent>(body)` — line 191
- `Entity<MetabolizerComponent>` used as a parameter type — line 200

In Wolfgate, `MetabolizerComponent` is declared in `Content.Server.Body.Components`
(`Content.Server/Body/Components/MetabolizerComponent.cs:7-12`, `[RegisterComponent, Access(typeof(MetabolizerSystem))]`,
no `NetworkedComponent`). `Content.Shared` cannot reference `Content.Server` types — this is a hard
assembly-boundary violation, so this file fails to compile against Wolfgate regardless of any individual
method's signature. This matches (and extends) the handoff's existing note that "Bloodstream/Healing/Bed
are server-side here (Onyx's versions shared/predicted)" — Metabolizer is in the same boat and wasn't
called out there. **This blocks the whole Circulation subsystem**, which Bleeding, `WoundPrototype`, and
damage projection all sit on top of per the phase-1 plan — worth flagging to the user before Phase 1 starts,
since it's more invasive than a compat shim (it needs either a shared `MetabolizerComponent` split out of
server code, or `CirculatoryStreamSystem`'s metabolizer hooks moved server-side).

Additionally, `SharedSolutionContainerSystem.CirculatoryStreams.cs` (in scope) calls
`CreateDefaultSolution((entity, manager), name)` (line 15) as a `partial class SharedSolutionContainerSystem`
extension. Wolfgate's `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs` (also
`abstract partial`, so the file attaches) has **no `CreateDefaultSolution` method at all** — the closest
equivalents are `EnsureSolutionEntity(Entity<SolutionContainerManagerComponent?>, string, out bool existed, out Entity<SolutionComponent>? solutionEntity, FixedPoint2 maxVol, Solution? prototype)` and
`EnsureSolution(...)` (lines 1023-1157), which return via `out` + a `bool existed` flag rather than an
unconditional `Entity<SolutionComponent>` — a straightforward but real signature adaptation (unwrap the
nullable `out`, decide what to do with `existed`).

### 3.3 `EntityEffectSystem<T1,T2>` ECS entity-effects pattern is entirely absent (confirms D5)

Wolfgate's only entity-effects code is the old class-based `Content.Shared/EntityEffects/EntityEffect.cs`
(`abstract partial class EntityEffect` with `Effect(EntityEffectBaseArgs args)`). None of
`EntityEffectSystem<TComponent,TEffect>`, `EntityEffectBase<TSelf>`, or `EntityEffectEvent<TEffect>` exist
anywhere in `Content.Shared/EntityEffects` (confirmed by listing the directory and grepping for the three
class names — zero matches). This breaks, in scope:
- `SuppressPainEntityEffect.cs:7` (`SuppressPainEntityEffectSystem : EntityEffectSystem<PainComponent, SuppressPain>`) and `:18` (`SuppressPain : EntityEffectBase<SuppressPain>`)
- `ReagentTreatmentEffects.cs:27` (`MendFractures : EntityEffectBase<MendFractures>`)
- `ReagentTreatmentSystems.cs:14,37,56,72,79` — three `ApplyTreatment` methods hung off partial extensions
  of generated systems (`HealthChangeEntityEffectSystem`, an `EntityEffectSystem<WoundHostComponent, MendFractures>`)
  that Onyx expects the ECS entity-effects source generator to have already produced upstream.

This is the same finding the handoff already lists under "D5 Missing APIs," here confirmed file-by-file:
none of it is portable without either (a) backporting the upstream ECS entity-effects rework wholesale, or
(b) rewriting these four effects against Wolfgate's old class-based `EntityEffect`/`EntityEffectBaseArgs`
pattern as `_WF/Wolfmed` originals rather than vendoring Onyx's files verbatim.

### 3.4 `AlertsSystem.UpdateAlert` — confirms D5, with a parameter-shape note

Wolfgate's `Content.Shared/Alert/AlertsSystem.cs` has `ShowAlert(EntityUid, ProtoId<AlertPrototype>, short? severity, (TimeSpan,TimeSpan)? cooldown, bool autoRemove, bool showCooldown)` (line 81) and `ClearAlert`/`ClearAlertCategory`
(lines 134, 153) — no `UpdateAlert` under any name. `_Onyx/Wounds/FractureAlertSystem.cs` (in scope) only
uses `ShowAlert`/`ClearAlert` and is unaffected. `StatusEffectNew/StatusEffectAlertSystem.cs:29,39` (in
scope) calls `_alerts.UpdateAlert(target, alert, cooldown: TimeSpan?)` — even once a substitute for
`UpdateAlert` exists, note Wolfgate's `ShowAlert.cooldown` parameter is a `(TimeSpan, TimeSpan)?` pair
(start/end), not the single `TimeSpan?` end-time Onyx passes — the shim needs to synthesize the pair
(likely `(_timing.CurTime, endTime)`), not just rename the call.

### 3.5 StatusEffectNew is confirmed wholly absent, old system confirmed present (D1 baseline check)

`WG/Content.Shared/StatusEffectNew` does not exist; `WG/Content.Shared/StatusEffect/{StatusEffectPrototype.cs, StatusEffectsComponent.cs, StatusEffectsSystem.cs}`
does. This matches D1's premise exactly — no surprise here, included for completeness since the task's
scope centers on `StatusEffectNew/**`. Every file under `Content.Shared/StatusEffectNew/` in scope (13
files) is new code to be vendored per D1, not a partial-class extension of anything pre-existing, so none
of the "does the base class have this member" questions from Parts 1-2 apply to it directly — its risk is
entirely in what it *itself* depends on, which Part 1 and 2 cover (`EntityQuery<T>` DI, `PredictedQueueDel`,
`PredictedTrySpawnInContainer`, `EntityWhitelistSystem`, `RejuvenateEvent`, `AlertsSystem.UpdateAlert`) —
all confirmed to exist except the last.

---

## Part 4 — Files scanned with no engine/content-API gap found

For completeness: `WoundEvents.cs`, `WoundPrototype.cs`, `WoundDamageComponents.cs`, `WoundScarSystem.cs`,
`WoundFractureSystem.cs` (besides the shared `DamageableSystem` gaps in 3.1), `BodyPartFunctionalitySystem.cs`,
`OrganDamageComponent.cs`, `FunctionalOrganComponent.cs`, `OrganConsequenceComponents.cs`,
`Targeting/{DamageDistribution,PartStatusComponent,TargetingComponent,TargetingEvents,TargetingSnapshotComponent,TargetingSnapshotSystem,TargetResolverSystem,SharedTargetingSystem,TargetBodyPart}.cs`,
`Server/Targeting/{PartStatusSystem,TargetingSystem}.cs`, `Client/Targeting/*` (incl. the four XAML
code-behind files), `Client/HealthExaminable/*`, `MedicalPatchComponent.cs`, all 7
`StatusEffectNew/Components/*.cs` files, `StatusEffectAlertSystem.cs` (besides the `UpdateAlert` call in
3.4), `PermanentStatusEffectsSystem.cs`, `ExaminableStatusEffectSystem.cs`, `StatusEffectSystem.Relay.cs`,
`GroupHealSpecifier.cs`. These use only APIs confirmed present in Parts 1-2, or are pure data/event/component
declarations with no engine calls to verify. Their content correctness (do the *Content* types they
reference, like `WoundHostComponent`'s Wolfgate-side counterpart, actually exist) is a separate,
non-engine-API question outside this scan's scope.

---

## Part 5 — Unverified / explicitly out of scope for this scan

- Whether any of the missing `DamageableSystem`/`EntityEffectSystem`/Hands APIs exist **elsewhere** in
  Wolfgate under a completely different name not searched here (a targeted grep for the exact identifiers
  Onyx uses was run; a rename to something unrelated, e.g. buried in `_Shitmed` or `_Goobstation` code,
  could theoretically still exist and wasn't exhaustively ruled out beyond the greps shown).
- Prototype-level (YAML) compatibility — this scan is C# API surface only.
- Whether Wolfgate's Shitmed `TargetBodyPart`/targeting doll can coexist with Onyx's own
  `Content.Shared._Onyx.Targeting.TargetBodyPart` type (same class name, different namespace) without a
  naming collision in usings — that's a content-architecture question the handoff's dependency map already
  flags for a separate pass, not an engine-API gap.
- Runtime/behavioral correctness of any of the "exists with matching signature" rows — confirmed by
  reading declarations, not by compiling the ported files (WG is read-only per task constraints).

---

## Appendix — 70 files scanned

```
Content.Shared/_Onyx/Wounds/{AmputationSystem,BodyPartFunctionalitySystem,FractureAlertSystem,FractureEffectsSystem,
  OrganDamageSystem,PainSystem,ReagentTreatmentEffects,ReagentTreatmentSystems,SuppressPainEntityEffect,WoundBehaviors,
  WoundBleedingSystem,WoundDamageComponents,WoundDamageProjectionSystem,WoundDamageRoutingSystem,WoundEvents,
  WoundFractureSystem,WoundHealingSystem,WoundInternalBleedingSystem,WoundPrototype,WoundScarSystem,
  WoundStatusEffectSystem,WoundSystem}.cs
Content.Shared/_Onyx/Chemistry/Circulation/{CirculatoryStreamComponent,CirculatoryStreamPrototype,
  CirculatoryStreamSystem,SharedSolutionContainerSystem.CirculatoryStreams}.cs
Content.Shared/StatusEffectNew/Components/{CloneableStatusEffectComponent,ExaminableStatusEffectComponent,
  PermanentStatusEffectsComponent,RejuvenateRemovedStatusEffectComponent,StatusEffectAlertComponent,
  StatusEffectComponent,StatusEffectContainerComponent}.cs
Content.Shared/StatusEffectNew/{ExaminableStatusEffectSystem,PermanentStatusEffectsSystem,StatusEffectAlertSystem,
  StatusEffectsSystem,StatusEffectSystem.API,StatusEffectSystem.Relay}.cs
Content.Shared/_Onyx/Medical/Tourniquet/{TourniquetComponent,TourniquetSystem}.cs
Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.{Pain,PartStatus}.cs
Content.Client/_Onyx/HealthExaminable/{ExamineSystem.PartStatus,PartStatusTag}.cs
Content.Shared/_Onyx/Targeting/{DamageDistribution,PartStatusComponent,PartStatusSystem,SharedTargetingSystem,
  TargetBodyPart,TargetingComponent,TargetingEvents,TargetingSnapshotComponent,TargetingSnapshotSystem,
  TargetResolverSystem}.cs
Content.Server/_Onyx/Targeting/{PartStatusSystem,TargetingSystem}.cs
Content.Client/_Onyx/Targeting/{TargetingSystem,UI/HealthAnalyzerStatusDoll.xaml,UI/PartStatusControl.xaml,
  UI/TargetingControl.xaml,UI/TargetingUIController}.cs
Content.Shared/_Onyx/Damage/{GroupHealSpecifier,Systems/DamageableSystem.API}.cs
Content.Shared/_Onyx/Body/{OrganDamageComponent,FunctionalOrganComponent,OrganConsequenceComponents,
  Systems/OrganHealthSystem}.cs
Content.Server/_Onyx/Medical/{MedicalPatchComponent,MedicalPatchSystem}.cs
```
