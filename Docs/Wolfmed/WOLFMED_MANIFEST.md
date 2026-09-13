# Wolfmed port manifest

**Onyx commit:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (Space-Onyx/space-onyx-14 master)
**Last re-sync:** 2026-09-13 by WP9

Status values: `verbatim` (byte-identical), `modified` (vendored `_Onyx` file with `// WOLFGATE` edits),
`adapted` (vendored but relocated and/or restructured), `new` (Wolfgate-authored `_WF` code or an upstream
`// WOLFGATE` hook), `skipped` (deliberately not ported).

> **Note (WP4):** this file was accidentally truncated during the WP4 run and rebuilt from PLAN.md §7.2
> plus the WP1/WP2/WP3 reports in `C:/tmp/wolfmed-plan/wp/`. The row set and every recorded deviation are
> believed complete; wording in the WP1–WP3 sections may differ from the original.

| Onyx path | Wolfgate path | Status | WP | Notes |
|---|---|---|---|---|
| `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs` | same | verbatim | WP1 | registers as `StatusEffect`; no Wolfgate collision |
| `Content.Shared/StatusEffectNew/Components/StatusEffectContainerComponent.cs` | same | verbatim | WP1 | registers as `StatusEffectContainer` |
| `Content.Shared/StatusEffectNew/Components/StatusEffectAlertComponent.cs` | same | verbatim | WP1 | needs the `StatusEffects` entityCategory and `AlertsSystem.UpdateAlert` compat |
| `Content.Shared/StatusEffectNew/Components/CloneableStatusEffectComponent.cs` | same | verbatim | WP1 | UTF-8 BOM preserved |
| `Content.Shared/StatusEffectNew/Components/ExaminableStatusEffectComponent.cs` | same | verbatim | WP1 | |
| `Content.Shared/StatusEffectNew/Components/PermanentStatusEffectsComponent.cs` | same | verbatim | WP1 | |
| `Content.Shared/StatusEffectNew/Components/RejuvenateRemovedStatusEffectComponent.cs` | same | verbatim | WP1 | |
| `Content.Shared/StatusEffectNew/StatusEffectsSystem.cs` | same | verbatim | WP1 | needs the `ProtoMan` + `EntityPrototype.TryComp` compat files |
| `Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs` | same | verbatim | WP1 | keeps Onyx's unused `System.ComponentModel.Design` / `YamlDotNet.Core.Tokens` usings on purpose |
| `Content.Shared/StatusEffectNew/StatusEffectAlertSystem.cs` | same | verbatim | WP1 | needs `AlertsSystem.UpdateAlert` compat |
| `Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs` | same | modified | WP1 | 1 using removed, 1 added, 11 relay lines dropped |
| `Content.Shared/StatusEffectNew/ExaminableStatusEffectSystem.cs` | same | modified | WP1 | `[SubscribeLocalEvent]` to explicit `Initialize()` |
| `Content.Shared/StatusEffectNew/PermanentStatusEffectsSystem.cs` | same | modified | WP1 | same, x3; the `// <Onyx-OrganEffects>` block kept verbatim |
| `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` | same | verbatim | WP1 | required by `PainSystem.cs:323`; registers as `PainNumbnessStatusEffect`, no clash with Wolfgate's `PainNumbness` |
| `Resources/Prototypes/Entities/categories.yml:31-34` | `Resources/Prototypes/_Onyx/Entities/categories.yml` | adapted | WP1 | only the `StatusEffects` entityCategory |
| `Resources/Locale/en-US/entity-categories.ftl:7` | `Resources/Locale/en-US/_Onyx/entity-categories.ftl` | adapted | WP1 | one key |
| `Resources/Prototypes/Entities/StatusEffects/misc.yml` | same | adapted | WP1 | only `StatusEffectBase`, `MobStatusEffectBase`, `MobStatusEffectDebuff` |
| `Resources/Prototypes/Entities/StatusEffects/{body,clumsy,damage,speech,traits,weather}.yml` | — | skipped | never | non-wound status effects; each needs components we do not port |
| `Resources/Prototypes/Entities/StatusEffects/movement.yml` | — | deferred | WP10 | `StatusEffectSlowdown` needs `MovementModStatusEffectComponent` |
| `Resources/Prototypes/_Onyx/StatusEffects/wounds.yml` | — | deferred | WP10 | phase 2 |
| `Resources/Prototypes/_Onyx/StatusEffects/surgery.yml` | — | skipped | never | needs `RaspyAccent`; D7 excludes Onyx surgery |
| `Resources/Prototypes/_Onyx/StatusEffects/{abductor_glands,breathing_immunity,cosmiccult,dementia,tile_movement,vampire}.yml` | — | skipped | never | unrelated features |
| `Content.{Shared,Server}/_Onyx/StatusEffects/Immunities/**` | — | skipped | never | unrelated features |
| — | `Content.Shared/_WF/Wolfmed/Compat/StatusEffectsSystem.Wolfgate.cs` | new | WP1 | supplies `ProtoMan`; delete on an RT upgrade that adds it |
| — | `Content.Shared/_WF/Wolfmed/Compat/EntityPrototypeCompatExtensions.cs` | new | WP1 | RT 277 names it `TryGetComponent` |
| — | `Content.Shared/_WF/Wolfmed/Compat/AlertsSystem.UpdateAlert.cs` | new | WP1 | partial, not extension; `AlertState.Cooldown` is an unnamed tuple here |
| — | `Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs` | new | WP2 | D12 facade; D30 zeroing `SetDamage`; `CanBeDamagedBy` re-based onto `DamageableComponent` |
| — | `Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Wolfmed.cs` | new | WP2 | `Clone`, `GetPositive`, `GetNegative` |
| — | `Content.Shared/_WF/Wolfmed/Compat/DamageDealtEvent.cs` | new | WP2 | non-`readonly` unlike Onyx's — deliberate |
| — | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBodySystem.cs` | new | WP2 | `TryDetachPart` via `AmputateAttemptEvent`; `reparent` ignored |
| — | `Content.Shared/_WF/Wolfmed/Compat/StunSystemOnyxCompat.cs` | new | WP2 | `TryParalyze(refresh: false)`; re-triggers stun VFX, unlike Onyx |
| — | `Content.Shared/_WF/Wolfmed/Compat/SharedChatSystem.Wolfmed.cs` | new | WP2 | `virtual void`; paired with HOOK 6 at `ChatSystem.Emote.cs:60` |
| — | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBedHealMarkerComponent.cs` | new | WP2 | `HealOnBuckleComponent` is server-only here |
| — | `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | new | WP2 | `<HealOnBuckleComponent, ComponentStartup>` verified free |
| — | `Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs` | new | WP2 | event declarations only; `[ByRefEvent]` restored (see Deviations) |
| — | `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` | new (hook) | WP2 | HOOK 5, 3-line static `IsSelectable` |
| `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamComponent.cs` | same | verbatim | WP3 | |
| `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs` | same | modified | WP3 | `MetabolismStage`/`MetabolitesStage` and the `Content.Shared.Metabolism` using commented out; phase-5 re-sync point |
| `Content.Shared/_Onyx/Chemistry/Circulation/SharedSolutionContainerSystem.CirculatoryStreams.cs` | — | skipped | never | only called from `InitializeStream`/`RemoveStream`, both dropped by D15 |
| `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` | same | verbatim | WP3 | zero name and zero cvar-string collisions |
| `Content.Shared/_Onyx/CCVar/CCVars.Surgery.cs` | same | verbatim | WP3 | |
| `Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs` | same | verbatim | WP3 | |
| `Content.Shared/_Onyx/Targeting/DamageDistribution.cs` | same | verbatim | WP3 | |
| `Content.Shared/_Onyx/Targeting/TargetingSnapshotComponent.cs` | same | modified | WP3 | added `using Content.Shared._Shitmed.Targeting;`; default `.Chest` to `.Torso` |
| `Content.Shared/_Onyx/Targeting/TargetingSnapshotSystem.cs` | same | modified | WP3 | added `using Content.Shared._Shitmed.Targeting;` for `TargetingComponent`/`TargetBodyPart`/`SharedTargetingSystem.IsSelectable` (HOOK 5, landed in WP2) |
| `Content.Shared/_Onyx/Targeting/{TargetBodyPart,TargetingComponent,SharedTargetingSystem,TargetResolverSystem}.cs` | — | skipped | never | D10 — Onyx's `TargetingComponent` registers as `"Targeting"`, colliding with Shitmed's; the resolver switches on `BodyPartType.Chest`/`Groin`, forbidden by D9 |
| `Content.{Server,Client}/_Onyx/Targeting/**`, `_Onyx/Targeting/PartStatus*.cs` | — | skipped | never | D10 — Shitmed's `TargetIntegrity` doll stays; revisit phase 3/4 |
| `Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml` | same | verbatim | WP3 | 2 lines: `- type: circulatoryStream / id: Organic` |
| `Content.Shared/_Onyx/Wounds/WoundEvents.cs` | same | verbatim | WP4 | `ResolveHealingPartEvent` keeps Onyx's `ProtoId`/`IReadOnlySet` types (D31) |
| `Content.Shared/_Onyx/Wounds/WoundBehaviors.cs` | same | verbatim | WP4 | needs WP1's `StatusEffectComponent` (D1) |
| `Content.Shared/_Onyx/Wounds/WoundPrototype.cs` | same | verbatim | WP4 | registers `wound` / `bodyPartProfile` / `fractureProfile`; dead `using Content.Shared.Body;` kept on purpose (PLAN 2.17) |
| `Content.Shared/_Onyx/Wounds/WoundScarSystem.cs` | same | verbatim | WP4 | |
| `Content.Shared/_Onyx/Wounds/WoundStatusEffectSystem.cs` | same | verbatim | WP4 | dead `using Content.Shared.Body;` kept (PLAN 2.17) |
| `Content.Shared/_Onyx/Wounds/PainSystem.cs` | same | modified | WP4 / **WP9** | Verbatim out of WP4 (served by PLAN 2.8 stun shim + 2.9 chat shim + HOOK 6 + WP1's `PainNumbnessStatusEffectComponent`). **WP9 fix:** both `new ModifyPainGainEvent()` sites (`:111`, `:233`) now pass `1f` explicitly - `new T()` on a record struct binds to the implicit parameterless struct constructor and zeroed `Multiplier`, so every pain gain was multiplied by zero and no wound host ever felt pain |
| `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs` | same | modified | WP4 | D9: `Chest`+`Groin` weights fold to one `Torso = 4f` row, `Groin` dismemberment row deleted, `SystemicPainTarget` to `.Torso`; `_Shitmed` targeting using. `LocalizedDamageTypes` verbatim incl. `Caustic` (D20 reversed) |
| `Content.Shared/_Onyx/Wounds/WoundSystem.cs` | same | modified | WP4 | D17: `<BodyComponent, RejuvenateEvent>` subscription deleted, handler exposed as `public void ClearBodyWounds(EntityUid)` |
| `Content.Shared/_Onyx/Wounds/BodyPartFunctionalitySystem.cs` | same | modified | WP4 | `_Shitmed.Cybernetics` using. `wounds.body_part_functionality_enabled` defaults **false**, so this is inert until phase 3 |
| `Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs` | same | modified | WP4 | added `using Content.Shared.Damage;`; D12 facade swap; D8 `FractureProfile` read moves to `WolfmedBodyPartComponent` |
| `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | same | modified | WP4 (+WP5, WP8) | D12 facade swap, D10 resolver swap, D8 part-field reads, D9 `Chest` to `Torso` x3, hands rewrite, bed-marker swap, `args.Cancelled` guard, Shitmed `targetPart` handoff, `before: [typeof(SharedArmorPlateSystem)]` on both `BeforeDamageChangedEvent` subs. D23 AP/Tool/OriginFlag side table and D27 applied-delta accumulator added in WP5 |
| `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` | same | modified | WP4 (WP5 scope) | D12 facade swap, D15 circulation dependency dropped, D11 re-point to `DamageChangedEvent`, D17 `ClearBodyWounds` call, D19 `InjurableComponent` block deleted, `after: [typeof(SharedBodySystem)]`, Shitmed parent walk, D9 visual layers |
| `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` | same | modified | WP4 (WP5 scope) | absent from the sparse checkout — read via `git show`. **Adds** `_damageable` (M6); `using Content.Shared.Body.Components;`; vital parts `{Head, Torso}` (D9) |
| `Content.Shared/_Onyx/Wounds/{AmputationSystem,FractureEffectsSystem,FractureAlertSystem}.cs` | — | skipped | WP10/WP11 | D26 — fractures ship in phase 1, amputation does not |
| `Content.Shared/_Onyx/Wounds/WoundBleedingSystem.cs` | `Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs` | adapted | WP6 | D13 — relocated to `Content.Server`, namespace unchanged. `using Content.Shared.Body.Components;` to `Content.Server.Body.Components` (`BloodstreamComponent`); `using Content.Server.Body.Systems;` **added** beside the shared one (`SharedBodySystem` still comes from `Content.Shared.Body.Systems`). Body otherwise byte-identical |
| `Content.Shared/_Onyx/Wounds/WoundInternalBleedingSystem.cs` | `Content.Server/_Onyx/Wounds/WoundInternalBleedingSystem.cs` | adapted | WP6 | D13 + M1 — same two `using` swaps, plus the mandatory `:67` fix `TryModifyBloodLevel((body, bloodstream), -amount)` to `TryModifyBloodLevel(body, -amount, bloodstream)` (two chained user-defined conversions, `CS1503`) |
| `Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs` | `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` | adapted | WP6 | D13 + D26 + D8 — `using Content.Shared.Body;` to `Content.Shared.Body.Organ` + `Content.Shared._WF.Wolfmed.Body`; **both** `:24` (`[Dependency] AmputationSystem`) and `:36` (`_amputation.HandlePartDamageApplied`) disabled with a `TODO: phase 3`; the organ list and `PickOrgan` retargeted from `OrganComponent` to `WolfmedOrganComponent` |
| `Content.Shared/_Onyx/Wounds/WoundHealingSystem.cs` | `Content.Server/_Onyx/Wounds/WoundHealingSystem.cs` | adapted | WP6 | D13 + D14 + D31 — `using Content.Shared.Medical.Healing;` to `Content.Server.Medical.Components`; D12 facade swap; D31 conversion at `:110` (`DamageContainers?.Select(x => new ProtoId<DamageContainerPrototype>(x)).ToList()`). `ResolveHealingPart`/`IsCompatiblePart` signatures untouched |
| `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` | `Content.Server/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` | adapted | WP6 | D15 — trimmed to `GetPartStream`, `TryGetPartSolution`, `TryGetStreamSolution`, `SetBleedRates`, primary-stream branches only. All five subscriptions, `Update`, `SynchronizeStreams`, `GetAttachedStreams`, `ConfigureMetabolizer`, `InitializeStream`, `HasStageConflict`, `RemoveStream`, `DeleteSolution` and both metabolism handlers dropped. Namespace unchanged |
| `Content.Shared/_Onyx/Body/OrganDamageComponent.cs` | same | verbatim | WP6 | registers as `OrganDamage`; no Wolfgate collision |
| `Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs` | `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` | adapted | WP6 | D13 + D8 — health reads move to `WolfmedOrganComponent`; `TryGetOrganInSlot`/`TryRemoveOrgan` replaced by Wolfgate's `SharedBodySystem.RemoveOrgan`; `BrainComponent` resolves from `Content.Server.Body.Components`. `OrganFunctionChangedEvent` relocated into this file (see Deviations) |
| `Content.Shared/_Onyx/Body/FunctionalOrganComponent.cs` | — | skipped | WP11 | D8 — Nubody glue. Only its `OrganFunctionChangedEvent` was needed and now lives in `OrganHealthSystem.cs` |
| — | `Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs` | new | WP6 | D8 — `Health`, `MaxHealth`, `DestructionWound`, `DestructionWoundSeverity`; Wolfgate's Shitmed `OrganComponent` has none. Networked; registers as `WolfmedOrgan`. No prototype carries it until WP11 |
| `Content.Shared/_Onyx/Wounds/WoundSystem.cs` | same | modified | WP6 | additional WP6 edit: `HandlePartDamageApplied` `internal` to `public` — D13 puts its only caller (`OrganDamageSystem`) in `Content.Server`, a different assembly |
| `Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs` | same | modified | WP6 | additional WP6 edit: same `internal` to `public` change on `HandlePartDamageApplied` |
| — | `Content.Server/Body/Systems/BloodstreamSystem.cs` | new (hook) | WP6 | **GUARD E** (`HasComp<WoundHostComponent>` early-return in `OnDamageChanged`) + **GUARD E3** (`TryModifyBleedAmount` split into the public wound-host-gated entry, `internal TryModifyWoundBleedProjection`, and a private `bool woundProjection` implementation). One gate also silently no-ops the passive-decay call in `Update()` — deliberate, not duplicated. **GUARD E2 is WP10** |
| — | `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` | new (hook) | WP6 | **GUARD E4** — one `HasComp<WoundHostComponent>` early-return at the top of `OnDamageChanged` |
| — | `Content.Server/Medical/Components/HealingComponent.cs` | new (hook) | WP6 | **HOOK 7 / D14** — four additive `[DataField]`s (`HealDamage`, `HealWounds`, `HashSet<TreatmentCapability> TreatmentCapabilities`, `HashSet<string>? AllowedWoundStages`) + `using Content.Shared._Onyx.Wounds;`. `HashSet<>` is mandatory (D31: `ResolveHealingPartEvent` takes `IReadOnlySet<>`) |
| — | `Content.Server/Medical/HealingSystem.cs` | new (hook) | WP6 | **HOOK 8**, call-site only after the tidy pass: `OnDoAfter`'s wound-host branch (calls `OnWoundHostDoAfter`) and `TryHeal`'s `woundHost` gate (calls `IsWoundDamaged`/`ResolveWoundTargetPart`). The hook body — `OnWoundHostDoAfter`, `ResolveWoundTargetPart`, `GetHealingContainers`, `IsWoundDamaged`, and the `WoundHealingSystem`/`WoundTargetResolver` dependencies — moved to `Content.Server/_WF/Wolfmed/Medical/HealingSystem.Wolfmed.cs`. Requested part comes from the healer's Shitmed `TargetingComponent` (see Deviations) |
| — | `Content.Server/_WF/Wolfmed/Medical/HealingSystem.Wolfmed.cs` | new | WP6 (split out in the HOOK 8/10 tidy pass) | HOOK 8's body, `partial class HealingSystem` sharing the upstream file's private fields (`_popupSystem`, `_stacks`, `_adminLogger`, `_bloodstreamSystem`, `_bodySystem`, `_solutionContainerSystem`, `_audio`, `EntityManager`). Logic is byte-identical to the original hook, just relocated |
| `Content.Server/Body/Systems/RespiratorSystem.cs` | — | skipped | never | PLAN 3 lists it under "explicitly NOT touched"; `hooks-a.md` 3a/3b confirm no phase-1 hook exists (breathing immunity already present, `InitiallyLungedComponent`/OrganConsequences are Nubody glue) |
| `Content.Shared/_Onyx/Wounds/{ReagentTreatmentEffects,ReagentTreatmentSystems,SuppressPainEntityEffect}.cs` | — | skipped | WP11 | D16 — rewritten old-style in `_WF/Wolfmed/EntityEffects`; `HealthChange`/`EvenHealthChange` get HOOK 9 instead |
| — | `Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs` | new | WP4 | D8/PLAN 2.12; 6 fields; not networked. `MaxDamage` default 0 disables amputation overflow — WP7's `parts.yml` must cover all 13 limb abstracts |
| — | `Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartSystem.cs` | new | WP4 | D8/PLAN 2.12; `Get(EntityUid)` with a shared zeroed default |
| — | `Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs` | new | WP4 | D10/PLAN 2.13; folds `TargetBodyPart.Groin` to the torso; no anatomical-odds scatter in phase 1 |
| — | `Content.Server/Chat/Systems/ChatSystem.Emote.cs` | new (hook) | WP4 | HOOK 6 / D25 — one word at `:60` (`public void` to `public override void`); `:85` untouched |
| `Content.Shared/Damage/Systems/DamageableSystem.cs` | same | new (hook) | WP5 | **GUARD D** (the `DamageDealtEvent` routing seam, gated on `_woundHostQuery`, with a defensive `new DamageSpecifier(damage)` copy) and **GUARD D2** (`BeforeDamageChangedEvent` gains `ArmorPenetration`/`Tool` (D23) and `Applied` (D27); `TryChangeDamage` returns `before.Applied` instead of `null` on cancel). 2 usings, 1 query field + its assignment |
| `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | same | new (hook) | WP5 | **GUARDs A/B/C** — component-gated on `WoundHostComponent`, never `_net.IsServer` (PLAN 8.3 trap 3). A skips Shitmed's part spread, B its sever, C its part regen (both the tick condition and the job enqueue). `CheckBodyPart` is left running |
| `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | same | modified | WP5 | WP5 half: D23 `_routedModifiers` side table written in `OnBeforeDamageChanged` and read at the routed `ChangeDamage` call; D27 `_appliedDelta` accumulator with three write points (`RouteAppliedDamage`, `ApplyPartChange`, `ApplySystemicDamage`) folded in by `AccumulateApplied`, written to `args.Applied` |
| — | `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | new | WP5 | D28 / PLAN 2.11 — subscribes `<WoundHostComponent, BodyPartAddedEvent/BodyPartRemovedEvent>` (never `<BodyComponent, …>`, which Shitmed owns), calls `WoundDamageProjectionSystem.OnPartInserted`/`OnPartRemoved` (PLAN 8.3 trap 2 — they had no caller) and fans `OrganGotInsertedEvent`/`OrganGotRemovedEvent` over the attached subtree |
| `Resources/Prototypes/_Onyx/Wounds/wounds.yml` | same | modified | WP7 | organic subset only (`OrganicBodyPartProfile`, `OrganicFractureProfile`, 12 wounds); Ipc/Slime/Plant/Cybernetic profiles and their 11 wounds dropped (D3). D9: `organDamage.chances` `Chest: 0.04`+`Groin: 0.04` fold to one `Torso: 0.04` (not summed). **D20 reversed: `Caustic` stays** in `acceptedDamageTypes` and `BurnWound.damageTypes` |
| `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | same | modified | WP7 | 43 keys copied verbatim, **plus 4 added**: `wound-examine-fracture-{hairline,simple,displaced,comminuted}`, sourced from Onyx's `_Onyx/medical/health-examinable.ftl` (deferred to WP10) because `BoneFractureWound`'s `examineDescription` fields reference them and the YAML linter fails without them — see Deviations |
| `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` | same | verbatim | WP7 | `alerts-broken-bones-{name,desc}` |
| `Resources/Prototypes/_Onyx/Alerts/alerts.yml` | same | modified | WP7 | `BrokenBones` only; header preserved verbatim; `ModsuitPower`/`Centered`/`HierophantBeat`/`DragonPower`/`SneakAttack`/`LossOfSurprise` dropped (unrelated features, would need un-ported textures/tags) |
| `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{meta.json,brokenbones.png}` | same | verbatim | WP7 | CC-BY-SA-3.0, "Taken from tgstation, redrawn by darkrell" — re-checked, compatible |
| — | `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` | new | WP7 | D8 — 10 `WolfmedBase<Part>` abstracts (`WolfmedBaseTorso/Head/Left|RightArm/Left|RightHand/Left|RightLeg/Left|RightFoot`) carrying `WolfmedBodyPart` data at Onyx's numbers (chest_groin.yml + body-organ.md §5.2). Separate ids, not re-declarations of `BaseTorso`/`BaseHead`/etc — see Deviations |
| — | `Resources/Prototypes/Body/Parts/base.yml` | same | modified | WP7 | upstream, 9 one-line `# WOLFGATE` `parent:` edits — `BaseHead`, `BaseLeftArm`, `BaseRightArm`, `BaseLeftHand`, `BaseRightHand`, `BaseLeftLeg`, `BaseRightLeg`, `BaseLeftFoot`, `BaseRightFoot` each add the matching `WolfmedBase<Part>` to their `parent:` list |
| — | `Resources/Prototypes/_Shitmed/Body/Parts/base.yml` | same | modified | WP7 | upstream, 1 one-line `# WOLFGATE` `parent:` edit — `BaseTorso` adds `WolfmedBaseTorso` |
| — | `Resources/Prototypes/Entities/Mobs/Species/base.yml` | same | modified | WP7 | upstream, `# WOLFGATE` block on `BaseMobSpeciesOrganic` (D21 `WoundHost`; D22 `Destructible` Blunt threshold 400→1500; D29 existing `PassiveDamage`'s `damage:` zeroed to `{}` in place, not duplicated — see Deviations) |
| — | `Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml` | same | modified | WP7 | documentation-only `# WOLFGATE` comment on `BaseMobProtogen` pointing at `WolfmedWoundHostExclusionSystem`; no functional YAML change (RT cannot remove an inherited component via YAML) |
| — | `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` | new | WP7 | D21/D32 — strips `WoundHostComponent` from entities descended from `BaseMobProtogen` (the only synthetic among the 18 `BaseMobSpeciesOrganic` descendants) at `ComponentInit`, shared so client and server agree. Deviation from the plan's literal "remove in its own prototype" — see Deviations. Subscribes `<WoundHostComponent, ComponentInit>`, free per grep |
| `Content.Shared/Armor/SharedArmorSystem.cs` | same | new (hook) | WP8 | **HOOK 10**, call-site only after the tidy pass: `OnDamageModify` now reads `if (TryApplyWoundHostArmor(uid, component, args)) return;`. The systemic-damage branch and the `ApplyWoundSystemicArmor` helper ported from Onyx moved to `Content.Shared/_WF/Wolfmed/Armor/SharedArmorSystem.Wolfmed.cs`. The localized half was already out of this upstream file, in `_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs`, since fix round 1 |
| — | `Content.Shared/_WF/Wolfmed/Armor/SharedArmorSystem.Wolfmed.cs` | new | WP8 (split out in the HOOK 8/10 tidy pass) | HOOK 10's systemic-damage half, `partial class SharedArmorSystem` holding `TryApplyWoundHostArmor` and the ported `ApplyWoundSystemicArmor` helper. Logic is byte-identical to the original hook, just relocated |
| — | `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | new | WP8 | HOOK 10's other half, kept out of upstream. Sole subscriber of `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>`; applies `ApplyModifierSet(damage, PenetrateArmor(Modifiers, ap))` to the routed part's damage. Without it armour would stop applying to every localized damage type (see Deviations) |
| `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` | same | new (hook) | WP8 | **HOOK 11**, two sites: `CheckThresholds` and `UpdateAlerts`' severity lerp now read `CheckVitalDamage(target, damageable)` instead of `damageable.TotalDamage`. `CheckVitalDamage` (the `_Onyx/Mobs/Systems` partial, WP5) falls back to total damage for non-wound-hosts, so no branch is needed |
| `Content.Server/Medical/DefibrillatorSystem.cs` | same | new (hook) | WP8 | **HOOK 12.** Same `CheckVitalDamage` substitution in the revive check, so revival agrees with HOOK 11's death decision |
| `Content.Shared/Execution/SharedExecutionSystem.cs` | same | new (hook) | WP8 | **HOOK 13.** One `[Dependency] WoundDamageRoutingSystem _woundRouting` + one `TryApplyLethalDamage(victim, meleeWeaponComp.Damage, attacker)` after `AttemptLightAttack`. Self-guards on `_net.IsServer` and `HasComp<WoundHostComponent>` |
| `Content.Shared/_Onyx/Wounds/WoundEvents.cs` | same | modified | WP8 | D23 — `PartDamageModifyEvent` gains an optional trailing `float armorPenetration = 0f` primary-constructor parameter and a readonly `ArmorPenetration` field, so HOOK 10's part pass can penetrate armour |
| `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | same | modified | WP8 | WP8 half: the single `PartDamageModifyEvent` construction site now passes `_routedModifiers.GetValueOrDefault(body).ArmorPenetration` (D23). The side table itself landed in WP5 |
| `Content.Server/Damage/Commands/HurtCommand.cs` | same | skipped | WP8 | Shipped in WP8 round 1 (optional 5th `<bodyPart>` argument), **reverted in fix round 1** — not in PLAN 3's authorised list and the instruction claimed for it is not recorded in PLAN.md/DECISIONS.md. File is byte-identical to HEAD again. The change is preserved at `C:/tmp/wolfmed-plan/wp/WP8-hurtcommand-deferred.patch` and can be re-applied if the user authorises it |
| — | `Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` | skipped | WP8 | Created in WP8 round 1, **deleted in fix round 1** with the command that used it. The path does not exist in the pinned Onyx sparse checkout, so its "verbatim" claim was never diffable — see Deviations |
| `Resources/Locale/en-US/damage/damage-command.ftl` | same | skipped | WP8 | Usage-string edit reverted in fix round 1 with `HurtCommand.cs`; byte-identical to HEAD again |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | same | adapted | WP9 | 9 of Onyx's 12 tests. Shitmed `body` prototype instead of Nubody `InitialBody`; Chest -> Torso (D9); `WolfmedDamageableSystem`/`WolfmedBodySystem`/`WoundTargetResolver` in place of Onyx's; Onyx's `TargetingComponent.DefaultOdds()`/`TryConvert` assertions dropped (D10); the two armour tests that need `coverage`/`partModifiers` dropped and folded into one applies-exactly-once test; `SuppressPain` entity effect replaced by the identical `PainSystem.SuppressPain` path (D16, phase 4) |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | same | adapted | WP9 | 4 of Onyx's 6. Server-side `WoundBleedingSystem`/`BloodstreamComponent` (D13); tourniquet test skipped (WP11); traumatic-amputation test skipped (needs `AmputationSystem` to set `Severable`, D26); two auto-clotting severities raised above `SlashWound.minimumSeverity: 9` |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundScarTest.cs` | same | adapted | WP9 | 1 test. Shitmed body graph; `WolfmedBodySystem.TryDetachPart` + `SharedBodySystem.AttachPart`; `CCVars.SurgeryScarChance` pinned to 1 for the duration (Onyx's copy is 35 % flaky) |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` | same | adapted | WP9 | 2 of Onyx's 3. `FractureEffectSystem` test deferred to phase 2 (WP10); grade boundaries corrected to `OrganicFractureProfile`'s own 20/35/50/60 |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundHealingTest.cs` | same | adapted | WP9 | 4 of Onyx's 5. Server-side `WoundHealingSystem` + `Content.Server` `HealingComponent` (D13/D14); `Repairable`/`TransplantCompatibility` dropped; `WoundTargetResolver` for the exact-target test; repair-event test skipped (`_Onyx.Repairable` not in scope) |
| `Content.IntegrationTests/Tests/_Onyx/Body/BodyConsequencesTest.cs` | same | adapted | WP9 | 2 of Onyx's 3, both rewritten. No Wolfgate system couples inventory slots to body parts and `BodyPartType.Groin`/`StandUpAttemptEvent` do not exist, so the surviving contract is cascade-on-detach and down-at-zero-legs on a real `MobHuman` wound host |
| - | `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | new | WP9 | PLAN 6.2: T-SETUP, T-RESULT, T-PIERCE, T-CAUSTIC, T12, non-wound-host control, no-double-application. `WolfmedBridgeBody` / `WolfmedControlBody` `[TestPrototypes]` pair |
| - | `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | new | WP9 | **Two WP9 fixes.** (1) `TerminatingOrDeleted` guards on both handlers - `RecursiveDeleteEntity` detaches every part while a mob terminates and `RefreshDetachedDamage`'s `EnsureComp<PartDamageVisualsComponent>` threw a `DebugAssertException` on every mob deletion. Onyx has the same guard in `BodyInventorySlotSystem.cs:32,45`. (2) `WoundBleedingSystem.OnPartInserted`/`OnPartChanged` are now driven from here (Onyx drives them from the unported `BodyInventorySlotSystem.cs:39,49`); without them a detached limb kept bleeding into the body |
| - | `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` | new | WP7 / **WP9** | WP9 fix: `RemCompDeferred` -> `RemComp`; the deferred form left the component in `_deleteSet` at life stage `Initialized` and `StartComponents` asserted, so every `MobProtogen` spawn threw |
| - | `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | new | WP2 / **WP9** | WP9 fix: `<HealOnBuckleComponent, ComponentStartup>` -> `<HealOnBuckleComponent, MapInitEvent>`; the marker was being gained on spawn, which fails `PrototypeSaveTest.UninitializedSaveTest` |

**Species included as wound hosts (17 of 18 `BaseMobSpeciesOrganic` descendants):** `arachnid`, `diona`,
`dwarf`, `gingerbread`, `human`, `moth`, `reptilian`, `slime`, `vox` (base) · `chitinid`, `feroxi`,
`rodentia`, `vulpkanin` (`_DV`) · `tajaran`, `yowie` (`_Goobstation`) · `asakim` (`_Mono`) · `hydrakin`
(`_Obelisk`). Diona and slime ship on `OrganicBodyPartProfile` until phase 5 (Onyx-consistent, not
Onyx-equivalent — §8.1 item 1(a)).

**Species excluded (1 of 18):** `protogen` (`_Mono`) — carries `prototype: SiliconDeathgasp`
(`_Mono/Entities/Mobs/Species/protogen.yml:74`), a synthetic. `WoundHostComponent` is stripped at
`ComponentInit` by `WolfmedWoundHostExclusionSystem` before any wound system observes it.

## Deviations

Deliberate departures from Onyx behaviour, with the reason. A re-sync should not re-litigate these.

### WP1

- **`MobStandStatusEffectBase` is not ported.** PLAN.md WP1's prototype table lists it as optional. Its
  blacklist references the tag `KnockdownImmune`, which returns zero hits across `WG/Resources/Prototypes`;
  a missing tag id is a YAML-linter error and the prototype has no consumer in phase 1. Re-add it with
  whatever WP lands Wolfgate's knockdown-immunity tag.
- **`StatusEffectSystem.Relay.cs` drops 11 relay subscriptions.** `StandUpAttemptEvent`,
  `StunEndAttemptEvent`, `RefreshStaminaCritThresholdEvent`, `GetMeleeTargetModifiersEvent`,
  `EmoteActionEvent`, `EmoteEvent`, `AccentGetEvent`, `BleedModifierEvent`,
  `RefreshPressureImmunityEvent`, `SelfBeforeInjectEvent`, `CatchAttemptEvent` either do not exist in
  Wolfgate or belong to systems that are server-side here. Each removal carries a `// WOLFGATE` reason.
- **`ExaminableStatusEffectSystem` and `PermanentStatusEffectsSystem` subscribe explicitly.** RT 277 has
  no `[SubscribeLocalEvent]` source generator.

### WP2

- **`OnyxBodyEvents.cs` carries `[ByRefEvent]`, which PLAN 2.11's snippet omits.** Onyx declares both
  events `[ByRefEvent]` and the only wound-set consumer (`FractureEffectsSystem`, phase 2) takes them
  `ref`; RT refuses a `ref` directed subscription for an event type without the attribute.
- **The facade writes damage through a local, not `ent.Comp.Damage.DamageDict[...]`.**
  `DamageableComponent` is `[Access(typeof(DamageableSystem), Other = AccessPermissions.ReadExecute)]`, so
  a direct write from another system is `RA0002`. `SetDamage`/`SetAllDamage` hoist the dictionary into a
  local first — the same pattern `DamageableSystem.TryChangeDamage` itself uses. **Any later WP writing an
  `[Access]`-restricted component field from `_WF`/`_Onyx` code will hit the same analyzer.**
- **`HealEvenly` / `HealDistributed` are implemented, not stubbed.** PLAN.md permits
  `NotImplementedException` until WP11; both are ported from `ONYX DamageableSystem.API.cs:177-272`
  instead, including the `+ Epsilon * (count - 1)` round-up that guarantees `HealEvenly` terminates.
- **Two facade methods beyond PLAN 2.1's listed surface:** `SetAllDamage` and `TryGetDamageGreaterThan`.
  Both are real Onyx members that `ClearAllDamage`, `HealEvenly` and `HealDistributed` call internally.
  `SetAllDamage` reports an **empty** delta rather than GUARD F's real delta, matching Onyx; setting all
  damage to a flat value is not "dealing" damage in either fork.
- **`ChangeDamage` passes `canSever: false, canEvade: false, partMultiplier: 1f, targetPart: null`.**
  PLAN 2.1's implementation note specifies exactly this; recorded because it makes every routed write
  `DamageChangedEvent.CanSever == false`, belt-and-braces on top of GUARD B (WP5).
- **The WP2 compile-exercise helper was not shipped.** It is preserved at
  `C:/tmp/wolfmed-plan/wp/WP2-compat-smoke.cs.txt`; WP9 should fold it into the integration tests as a
  compile gate rather than leaving a permanently-registered no-op `EntitySystem` in the tree.

### WP3

- **`CirculatoryStreamPrototype`'s `using Content.Shared.Metabolism;` is commented out, not just the two
  fields.** Wolfgate has no `Content.Shared.Metabolism` namespace at all, so an unresolvable `using` is a
  build error on its own, independent of whether `MetabolismStagePrototype` is referenced.
- **`TargetingSnapshotComponent`/`TargetingSnapshotSystem`'s "using swap" is an addition, not a literal
  replacement.** Onyx's files carry no explicit `using Content.Shared._Onyx.Targeting;` — they are declared
  inside that namespace, so the symbols resolved for free. Since D10 skips vendoring Onyx's own
  `TargetBodyPart`/`TargetingComponent`, the fix is to *add* one `using Content.Shared._Shitmed.Targeting;`
  line to each file. Flagged so a future re-sync does not look for a `using` line that was never there.
- **`TargetingSnapshotSystem.Capture`/`Refresh` bind to Shitmed's `TargetingComponent.Target`**, which
  already exists with that exact name and type, so the vendored reads resolve unchanged.

### WP4

- **`WoundSystem.cs` does not gain `using Content.Shared.Body.Components;`.** PLAN.md WP4 #5 asks for it so
  `BodyComponent` resolves, but D17 deletes the only two references to `BodyComponent` in the file, so the
  using would be dead on arrival. Onyx's existing dead `using Content.Shared.Body;` at `:2` is kept
  verbatim (PLAN 2.17) and nothing is added.
- **`WoundDamageProjectionSystem.OnPartDamageDealt` takes `ref DamageChangedEvent`, not a by-value
  parameter.** Wolfgate's `DamageChangedEvent` is a `sealed class : EntityEventArgs`, but every Wolfgate
  subscriber takes it `ref` (e.g. `_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:214`) and RT's
  overload resolution binds `SubscribeLocalEvent<TComp, DamageChangedEvent>` to the ref handler; the
  by-value form is a `CS1503`. PLAN.md §4 WP5's prose (`DamageChangedEvent args`) is corrected here.
- **`WoundDamageProjectionSystem` gains a `WoundSystem` dependency.** D17 requires `OnRejuvenate` to call
  `WoundSystem.ClearBodyWounds` first; the file had no `WoundSystem` dependency in Onyx because the
  subscription lived on `WoundSystem` itself.
- **Routing's `AccumulateAmputationOverflow` keeps its `TryComp(part, out BodyPartComponent? bodyPart)`
  guard even though `bodyPart` is now unused.** The guard still means "this is a body part"; deleting it
  would change behaviour for non-part entities. Only the two `bodyPart.MaxDamage` reads moved to
  `_wfPart.Get(part)`.
- **`PickExplosionAmputationCandidate`'s `part.Parent == null` becomes
  `_body.GetParentPartOrNull(parts[i]) is null`.** Shitmed's `BodyPartComponent` has no `Parent` field;
  the slot-container walk is the equivalent. Same substitution as `GetDetachedRoot`.
- **Routing's `OnBeforeDamageChanged` honours Shitmed's `BeforeDamageChangedEvent.TargetPart`.** Onyx has
  no such member, so this is an addition rather than a port. It is gated on
  `SharedTargetingSystem.IsSelectable` so the composite masks Wolfgate really passes
  (`TargetBodyPart.All` from `HealthChange.cs`, `Torso | <random>` from `SharedBodySystem.Targeting.cs`)
  fall through to Onyx's own resolution chain.
- **Routing's `OnBeforeDamageChanged` opens with `args.Cancelled` instead of ordering against godmode and
  stasis.** `SharedGodmodeSystem` and `SharedStasisSystem` are abstract, and RT keys ordering on
  `GetType()`, so `before:`/`after:` against them is silently inert (PLAN 5.4).
- **`WoundTargetResolver` drops Onyx's `Roll()` anatomical-odds scatter entirely.** PLAN 2.13 resolves
  this: Wolfgate's gun and melee systems already roll their own inaccuracy before calling in, and Onyx's
  `Roll`, Wolfgate's `GetRandomBodyPart` and Shitmed's `GetRandomPartSpread` are three incompatible
  designs. Consequence: `CCVars.TargetingUseAnatomicalOdds` and `CCVars.TargetingDownedTargetsAreExact`
  (vendored in WP3) are read by nothing in phase 1.
- **`WoundTargetResolver.TryFind` ignores symmetry for `Torso` and `Head` only.** Onyx's version also
  exempted `Groin`; `BodyPartType.Groin` does not exist here (D9) and `TargetBodyPart.Groin` already folds
  to `BodyPartType.Torso` through `ConvertTargetBodyPart`, so the exemption is preserved by the fold.

### WP5

- **GUARD D takes a defensive copy of the damage specifier before raising `DamageDealtEvent`.**
  Onyx does not (`ONYX Content.Shared/Damage/Systems/DamageableSystem.API.cs:161-164` raises the
  event on the caller's object). In Wolfgate that is unsafe: routing's `OnDamageDealt` clears the
  dict in place to suppress the body write, and with `ignoreResistances: true` the local in
  `TryChangeDamage` is still the caller's object — `ApplyModifierSet` and `DamageModifyEvent`, the
  two places that would otherwise re-bind it, are both inside `if (!ignoreResistances)`. Upstream
  call sites pass component datafields straight in (`ImmovableRodSystem.cs:122`,
  `RepairableSystem.cs:36`, `BibleSystem.cs:165`, `PassiveDamageSystem.cs:50`, ...), so without the
  copy the first wound host hit by one of those would permanently empty that component's `Damage`.
  One extra allocation per routed hit.
- **`TryChangeDamage` returns a non-null but possibly empty `DamageSpecifier` for a cancelled wound
  host** (D27). Godmode and stasis still return `null` — they never set `Applied`. The one visible
  edge: if routing's handler happens to run before godmode's on the same entity (handler order
  between them is undefined and cannot be constrained — both godmode systems are abstract), a
  godmoded wound host returns an empty specifier instead of `null`. No damage lands either way; the
  routed pass is cancelled by godmode in turn. Callers that test `!= null` see "a hit for zero".
- **`WolfmedBodyPartLifecycleSystem` does not subscribe `<OrganComponent, OrganAddedToBodyEvent>` /
  `<OrganComponent, OrganRemovedFromBodyEvent>`** although PLAN 5.2 assigns those pairs to it. The
  only consumer of `OrganGot*Event` is `FractureEffectsSystem` (phase 2, not ported), so phase 1
  would be registering two handlers with no readers. Both pairs remain free and unclaimed.
- **`WolfmedBodyPartLifecycleSystem.OnPartAdded` fans over the attached subtree; `OnPartRemoved`
  calls the projection once for the detached root.** Onyx's `SetSubtreeBody` raises `OrganGot*Event`
  per subtree member, which is reproduced; but `WoundDamageProjectionSystem.OnPartRemoved` already
  walks to the detached root itself (`RefreshDetachedDamage` -> `GetBodyPartChildren`), so calling it
  per subtree member would re-project the same tree N times.

### WP6

- **`WoundSystem.HandlePartDamageApplied` and `WoundFractureSystem.HandlePartDamageApplied` are `public`,
  not Onyx's `internal`.** D13 moves `OrganDamageSystem` — the single `<WoundableComponent,
  PartDamageAppliedEvent>` subscriber and the only caller of all four `HandlePartDamageApplied`
  implementations — into `Content.Server`, a different assembly from the two shared systems, and there is
  no `InternalsVisibleTo` between them. `WoundBleedingSystem`'s copy stays `internal` because it moved to
  `Content.Server` as well. PLAN 5.2's invariant still holds: none of the four may take its own
  subscription.
- **The requested healing part is read from the healer's Shitmed `TargetingComponent`, not from
  `HealingDoAfterEvent.RequestedPart`.** `hooks-a.md` 5a/6 add a `NetEntity? RequestedPart` field to
  `Content.Shared/Medical/HealingDoAfterEvent.cs`, but that file is **not** in PLAN 3's authorised hook
  list and WP6's table does not include it. Wolfgate already selects the healed limb from the user's
  targeting today (`SharedBodySystem.Targeting.cs:129-131`), so `HealingSystem.ResolveWoundTargetPart`
  reproduces that with `WoundTargetResolver.TryResolveExact`, gated on
  `SharedTargetingSystem.IsSelectable`. Consequence vs Onyx: the part is re-read at do-after completion
  instead of being latched when the do-after starts, so switching target doll limbs mid-heal changes which
  limb is treated. Landing `RequestedPart` later is a 6-line change in one file plus swapping the two
  `ResolveWoundTargetPart` call sites.
- **`HealingSystem.IsWoundDamaged` is a third parallel check, not a fold into `HasDamage`.** Onyx folds
  the wound branch into `HasDamage`; Wolfgate's `HasDamage(DamageableComponent, HealingComponent)` has no
  entity parameter and composes with a separate `IsPartDamaged`, so `hooks-a.md` 5b's lower-risk option
  was taken. `HasDamage` and `IsPartDamaged` are untouched and still serve every non-wound-host.
- **`TryHeal` skips the body-level `DamageContainers` rejection for wound hosts.** Matching Onyx
  (`HealingSystem.cs:335-340`, `resolvedPart` branch): the container test belongs to the resolved part's
  profile, not to the mob's own `DamageableComponent`.
- **`OrganFunctionChangedEvent` is declared in
  `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`,** in namespace `Content.Shared._Onyx.Body`, per
  PLAN WP6 #8. It is therefore **not reachable from `Content.Shared`**. Phase 2's `FractureEffectsSystem`
  is shared and uses `OrganGotInsertedEvent`/`OrganGotRemovedEvent` (which do live in shared, from WP2's
  `OnyxBodyEvents.cs`), so nothing breaks today — but any later shared consumer of
  `OrganFunctionChangedEvent` must move the declaration into `Content.Shared/_WF/Wolfmed/Compat/`.
- **`OrganHealthSystem.DestroyOrgan` does not walk the parent part's organ slots.** Onyx iterates
  `part.Organs` with `TryGetOrganInSlot`/`TryRemoveOrgan`, neither of which exists in Wolfgate;
  `SharedBodySystem.RemoveOrgan(organId, organ)` finds the containing container itself. The destruction
  wound is still created on the parent part, and the organ is still deleted on both paths.
- **`OrganHealthSystem` queries `WolfmedOrganComponent` paired with `OrganComponent`.** Onyx queries
  `OrganComponent` alone because health lives on it. A `WolfmedOrganComponent` on a non-organ entity is
  therefore ignored rather than destroyed.
- **`CirculatoryStreamSystem.TryGetPartSolution`/`TryGetStreamSolution` return `false` for any
  non-primary stream** instead of looking one up. D15 drops `InitializeStream`, so no secondary stream can
  ever exist in phase 1; the fallback branches would have been unreachable. `SetBleedRates` likewise drops
  the `CirculatoryStreamComponent` bookkeeping and the `SynchronizeStreams` fallback, leaving one call to
  `BloodstreamSystem.TryModifyWoundBleedProjection`.
- **`WoundBleedingSystem` keeps `using Content.Shared.Body.Systems;` alongside the added
  `using Content.Server.Body.Systems;`.** PLAN WP6 #2 describes this as a swap, but the file needs
  `SharedBodySystem` from the shared namespace and `BloodstreamSystem` from the server one; a literal swap
  is `CS0246`.

### WP7

- **RT has no YAML-level "remove this inherited component" mechanism.** Verified by reading
  `RobustToolbox/Robust.Shared/Serialization/TypeSerializers/Implementations/ComponentRegistrySerializer.cs`
  end to end: `Read`/`Validate` log `"Component of type '{compType}' defined twice in prototype!"` and
  **skip** the second entry if a components list declares the same type twice in one file (so PLAN's own
  example WOLFGATE block, which appends a second `- type: PassiveDamage`, would have silently no-opped the
  D29 neutralisation and logged an error every server start); `PushInheritance` only ever *adds* a parent's
  component to a child that lacks it, or merges fields if the child already declares it — there is no
  "delete" verb. Two consequences, both recorded as deviations from the plan's literal text:
  - **D29's `PassiveDamage` change is an in-place edit of the existing block on `BaseMobSpeciesOrganic`**
    (`damage: { types: { Heat: -0.07 }, groups: { Brute: -0.07 } }` → `damage: {}`), not a second
    `- type: PassiveDamage` entry appended after the new `WoundHost`/`Destructible` block. Functionally
    identical to the plan's intent; avoids the duplicate-component log error and the silent skip.
  - **Protogen's `WoundHost` exclusion (D21/D32) is a small shared C# system
    (`WolfmedWoundHostExclusionSystem`, `Content.Shared/_WF/Wolfmed/Body/`)**, not a prototype-level
    removal. It walks `IPrototypeManager.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true)`
    on `WoundHostComponent`'s `ComponentInit` and `RemCompDeferred`s the component if `BaseMobProtogen` is
    among the ancestors. Shared (not server-only) so client and server strip it identically before any
    GUARD A/B/C/D check ever runs (PLAN 8.3 trap 3: those guards must stay component-gated, and a
    server-only removal would desync a client that still carries the component from a stale prototype
    load). `protogen.yml` itself carries only a documentation `# WOLFGATE` comment — no functional change,
    since there is nothing to write there.
- **`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` declares 10 separate `WolfmedBase<Part>` abstracts**
  (`WolfmedBaseTorso`, `WolfmedBaseHead`, `WolfmedBaseLeftArm`, `WolfmedBaseRightArm`,
  `WolfmedBaseLeftHand`, `WolfmedBaseRightHand`, `WolfmedBaseLeftLeg`, `WolfmedBaseRightLeg`,
  `WolfmedBaseLeftFoot`, `WolfmedBaseRightFoot`), not re-declarations of `BaseTorso`/`BaseHead`/etc as
  `body-organ.md` §5.2's draft YAML literally wrote them. Re-declaring an existing `id:` in a second file
  hits the same engine wall as above (`PrototypeManager.YamlLoad.cs:228`,
  `PrototypeLoadException($"Duplicate ID: '{id}' for kind '{kind}")`) — this is exactly the "duplicate-id
  caveat" PLAN's WP7 #6 flagged and pre-authorised the fallback for: each `WolfmedBase<Part>` is added to
  the matching upstream abstract's `parent:` list with a one-line `# WOLFGATE` edit (9 in
  `Resources/Prototypes/Body/Parts/base.yml`, 1 in `Resources/Prototypes/_Shitmed/Body/Parts/base.yml`).
  `BasePartInorganic`, `BaseTorsoInorganic` and `BasePart` are untouched — they carry no per-limb data in
  Onyx either, and `WolfmedBodyPartComponent`'s zeroed defaults are harmless there since only organic
  species (via `BaseHead`/`BaseLeftArm`/…/`BaseTorso`) become wound hosts.
- **`wounds.ftl` is not byte-identical to Onyx's, despite PLAN calling it "verbatim (43 keys, complete —
  nothing missing)".** `BoneFractureWound`'s four `examineDescription` keys
  (`wound-examine-fracture-{hairline,simple,displaced,comminuted}`) actually live in Onyx's
  `_Onyx/medical/health-examinable.ftl`, which belongs to the unported `HealthExaminable` system (WP10).
  The Release YAML linter fails with "No localization message found" for all four without them. Fixed by
  appending the four keys (copied verbatim from `health-examinable.ftl`) to the end of `wounds.ftl` with a
  `# WOLFGATE` comment explaining the source and noting they should be deleted if `health-examinable.ftl`
  is ported in WP10 (at which point they would be a duplicate key error instead — check then).
- **`Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` are not ported**, per PLAN's explicit
  "textures deliberately not ported" note — `wounds.yml` has zero texture references, and the sprites are
  cosmetic re-skins of ones Wolfgate already ships.

### WP8

- **HOOK 10 is implemented as *two* subscriptions, not one — but only one of them lives in the upstream file.**
  (Revised in fix round 1: the part handler moved from `Content.Shared/Armor/SharedArmorSystem.cs` to the new
  `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs`, so the upstream file now carries exactly the
  edit PLAN 3 authorises and nothing more. Behaviour is unchanged: same pair, same single registration site,
  same modifier maths, and `ArmorComponent.Modifiers` is public so no upstream access change was needed.)
  PLAN 3 authorises only the wound-host branch
  in `OnDamageModify` plus the `ApplyWoundSystemicArmor` helper, and `hooks-a.md` 7b classifies Onyx's
  `OnPartDamageModify` as phase 3 because Wolfgate's `ArmorComponent` has no `PartModifiers` field. Shipping
  only the authorised half would have **removed armour from every localized damage type** (`Blunt`, `Slash`,
  `Piercing`, `Heat`, `Cold`, `Shock`, `Caustic` — i.e. essentially all weapon damage) for every wound host:
  `OnDamageModify` returns early after armouring systemic types, and nothing else armours the localized half
  because `PartDamageModifyEvent` had no subscriber. It would also have failed PLAN 6.2's own **T-AP** gate,
  which asserts a penetrating hit lands strictly *more* damage on a part than a non-penetrating one — with no
  armour applied at all, both land the same. The part handler is therefore ported in its Onyx-fallback form
  (`ApplyModifierSet(damage, PenetrateArmor(component.Modifiers, ap))`), which is what Onyx itself does when
  `PartModifiers` is empty. Net effect in phase 1 is numerically identical to the pre-WP8 behaviour (armour
  applied exactly once, at the same rate); what changes is the *structure*, which is now the Onyx shape that
  phase 3's per-part `ArmorComponent.PartModifiers` slots into.
- **The wearer is found via `Transform(uid).ParentUid`, not `args.Owner`.** Onyx's `InventoryRelayedEvent<T>`
  carries an `Owner`; Wolfgate's (`Content.Shared/Inventory/InventorySystem.Relay.cs:154-165`) does not, and
  neither does `DamageModifyEvent`. An equipped item is reparented to the wearer by its slot container, so the
  transform parent is the wearer. A non-inventory parent simply fails the `TryComp<WoundHostComponent>` test
  and takes the unmodified branch.
- **Armour is unpredicted on wound hosts, like the rest of the routed pass (D35).** The wound-host armour
  split is *not* `_net.IsServer`-gated — it is component-gated, matching Onyx and PLAN 8.3 trap 3 — but
  routing itself is server-only, so on the client a wound host's localized damage is neither routed nor
  armoured and lands in full on the body's own `DamageableComponent` for one tick. The mispredict is larger
  than it was before WP8 and self-corrects on the next server state. Predicting routing is a later phase.
- **`CheckThresholds` calls `CheckVitalDamage` once, hoisted above the threshold loop.** Onyx calls it inside
  the loop (`Content.Shared/Mobs/Systems/MobThresholdSystem.cs:340`). Same answer; the call walks the whole
  body with `GetBodyChildren`, and running it once per threshold per damage event is pure waste.
- **`HurtCommand.cs` and its two locale files were reverted in fix round 1 (WP8 round 1 shipped them).**
  `hooks-b.md` 1 specifies the hook and classifies it "later"; PLAN 3's complete authorised list has no
  `HurtCommand` entry, and the orchestrator instruction WP8 round 1 cited for it is not recorded in PLAN.md or
  DECISIONS.md, so it was withdrawn rather than left as an unauthorised upstream edit. Nothing else depended on
  it: `WoundDamageRoutingSystem.TryApplyPartDamage` (the API it drove) is vendored Onyx code with other callers,
  and `WoundTargetResolver.TryResolveExact` is still used by `WoundDamageRoutingSystem` and `HealingSystem`. WP9
  should drive part damage by calling `TryApplyPartDamage` from the integration fixture instead of through a
  console command. The withdrawn diff is kept at `C:/tmp/wolfmed-plan/wp/WP8-hurtcommand-deferred.patch`.
- **Sourcing gap disclosed (fix round 1):** the locale file WP8 round 1 added at
  `Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` was reported as "copied byte-for-byte from Onyx",
  but that path is **not in the pinned Onyx sparse checkout** — its wording was reconstructed from the four
  key names Onyx's `HurtCommand.cs` calls, not diffed against a source. It has been deleted. If the command is
  ever authorised, widen the sparse checkout and copy the real file rather than reusing that text.

### WP9

- **Two upstream-behaviour fixes landed in `_WF` glue, not in the tests.** `WolfmedBodyPartLifecycleSystem`
  gained `TerminatingOrDeleted` guards (mob deletion threw a `DebugAssertException` from
  `WoundDamageProjectionSystem.RefreshDetachedDamage`, failing every pooled-pair teardown in the repo once a
  `MobHuman` had been spawned) and now drives `WoundBleedingSystem.OnPartInserted`/`OnPartChanged`. Both
  mirror what Onyx's own `BodyInventorySlotSystem` does (`:32,45` and `:39,49`); that system is Nubody glue D8
  skips, so its two jobs had no caller in the port. This is the PLAN 8.3 trap-2 class, extended to bleeding.
- **Blocker found by the tests: all pain was being multiplied by zero.** `PainSystem` scales every pain gain
  and every wound-pain floor by `new ModifyPainGainEvent().Multiplier`. `ModifyPainGainEvent` is a
  `record struct` whose primary constructor declares `float Multiplier = 1f`, but `new T()` on a struct binds
  to the implicit parameterless constructor, which zeroes the field instead of applying that default. Result:
  `GetRawPain` stayed at 0 on every wound host, so pain, pain shock, the pain stun and every pain-driven
  emote were inert. Both construction sites now pass `1f` explicitly (`// WOLFGATE`, `PainSystem.cs:111,233`).
  Onyx's source is byte-identical, so Onyx is presumably affected too. `WoundDamageFoundationTest` carries a
  canary asserting `new ModifyPainGainEvent().Multiplier == 0f`; if that ever fails the workaround can go.
- **Pain shock needs `StatusEffectsComponent`.** Wolfgate's `SharedStunSystem.TryParalyze` (what PLAN 2.8's
  shim maps Onyx's `TryUpdateParalyzeDuration` onto) refuses any entity without the **old**
  `StatusEffectsComponent`; Onyx's stun runs on StatusEffectNew and needs none. Real mobs have it, but any
  test or prototype that wants pain shock must declare `- type: StatusEffects` with `Stun`/`KnockedDown`.
- **Three numeric deviations recorded in `WoundDamageFoundationTest`, all explained at the assertion:**
  (a) detaching a vital `HeadHuman` adds 100 `Bloodloss` through Shitmed's `PartRemoveDamage`, so the
  projected body total after a detach is Onyx's 6 plus 100; (b) damage dealt to a *detached* limb still goes
  through Shitmed's `<BodyPartComponent, DamageModifyEvent>` (`PartDamage` modifier set +
  `GetPartDamageModifier`), so 5 Blunt lands as 2 and pain moves 8.70 -> 10.44 where Onyx expects 13.05;
  (c) `PainComponent.RecoveryPerSecond` is `FixedPoint2.New(1f / 9f)` = 0.11 at two decimals here, so one
  second of recovery gives 8.59, not Onyx's 8.62. A fourth: Onyx's pain-shock figures assume no residual
  suppression, so the test clears suppression explicitly before that block rather than re-deriving them.
- **`WoundHealingTest`: healing a wound takes the full heal off its severity** (15 -> 5 for 10 points of
  Blunt healing), not Onyx's 13.5, because `WoundPrototype.HealingMultiplier` defaults to 1 and `BluntWound`
  overrides nothing - in both trees.
- **Two more blockers found by WP9's smoke run (`EntityTest` / `PrototypeSaveTest`), both fixed in `_WF`:**
  (a) `WolfmedWoundHostExclusionSystem` used `RemCompDeferred`, which leaves the component in
  `EntityManager`'s `_deleteSet` at life stage `Initialized`; `StartComponents` then trips
  `DebugTools.Assert(!_deleteSet.Contains(...))`, so **every `MobProtogen` spawn threw** and
  `SpawnAndDeleteAllEntities*` failed. Changed to `RemComp`, which moves it to `Deleted` and is skipped.
  (b) `WolfmedBedHealMarkerSystem` added its marker on `ComponentStartup`, so every healing bed gained a
  component on spawn and `PrototypeSaveTest.UninitializedSaveTest` failed on `NFBedrollStained*`. Moved to
  `MapInitEvent` (free pair; identical in game, since a live bed map-inits in the same spawn call).
- **Onyx test literals that are stale against Onyx's own pinned prototypes were corrected, not preserved.**
  (a) `WoundFractureTest` asserted grade boundaries 15/30/50/75; the vendored `OrganicFractureProfile` - which
  is byte-identical to Onyx's - declares 20/35/50/60. (b) `WoundBleedingTest`'s two auto-clotting tests used
  wound severities 1 and 3, both below `SlashWound`'s own `minimumSeverity: 9`, so nothing ever bled and the
  assertions were vacuous; raised to 10 and 30. (c) `WoundScarTest` depends on `CCVars.SurgeryScarChance`,
  which ships at 0.35, so Onyx's version passes about a third of the time; the test now pins it to 1 and
  restores it. (d) `WoundBleedingTest` expected a reopened, fully bandaged severity-30 wound to come back at
  `BleedingSeverity` 5; `WoundSystem.SetWoundState` -> `SyncRuntimeComponents` (also byte-identical to Onyx)
  re-seeds `BleedingSeverity` from the wound's whole severity, so it is 35. Recorded as 35 with the reasoning
  in the test. **Re-check all four on an Onyx re-sync** - if Onyx fixes its prototypes instead, these flip back.
- **Tests deliberately not ported in phase 1** (each has a `// WOLFGATE` note at the site):
  `TourniquetStopsOnlySelectedPartTest` (WP11), `TraumaticAmputationCreatesSevereStumpBleedingTest`
  (`Severable` is only ever set by `AmputationSystem`, D26 -> phase 3),
  `EffectsRefreshOnTreatmentHealingAndDetachTest` (`FractureEffectSystem`, phase 2),
  `RepairSelectionAndSnapshotValidationTest` (`_Onyx.Repairable`, out of scope), and Onyx's three
  coverage/symmetry/locational armour tests (Wolfgate's `ArmorComponent` has no `coverage`,
  `coverageSymmetry` or `partModifiers`; the applies-exactly-once contract is kept in one test).
- **`BodyConsequencesTest` is a rewrite, not a port.** Onyx's three tests assert Nubody inventory-slot
  coupling (`shoes`/`socks`/`underwearb` disappearing with the groin) that no Wolfgate system implements -
  nothing under `Content.{Shared,Server}/Inventory` subscribes `BodyPartRemovedEvent`/`BodyPartDroppedEvent`.
  What survives is cascade-on-detach and down-at-zero-legs (Wolfgate goes down at zero legs, not at one).
- **`DamageSpecifier.Empty` is not a usable 'nothing landed' assertion here.** `DamageableInit` seeds every
  supported type to zero (D30), so `BodyPartProfileContractsTest` asserts `GetTotal() == 0` instead.

## Hazards

- **Two types are now named `StatusEffectsSystem`** — `Content.Shared.StatusEffect.StatusEffectsSystem`
  (the old system, still serving all existing content) and
  `Content.Shared.StatusEffectNew.StatusEffectsSystem` (the port). A file that imports both namespaces
  gets `CS0104`. No ported file does; keep it that way.
- **`Content.Shared/_WF/Wolfmed/Compat/StatusEffectsSystem.Wolfgate.cs` is RT-version-scoped.** If
  Wolfgate ever moves to a RobustToolbox that adds `EntitySystem.ProtoMan`, this file starts emitting
  `CS0108` — delete it then. The same applies to `EntityPrototypeCompatExtensions.cs` if
  `EntityPrototype.TryComp` is added upstream.
- **`WolfmedDamageableSystem` is the only damage API vendored `_Onyx` files may use.** Binding
  `[Dependency] DamageableSystem` inside a wound file would silently resolve Wolfgate's legacy
  `TryChangeDamage(EntityUid?, ...)`; D12 exists to make that impossible. Every vendored file gets the
  one-line `// WOLFGATE` dependency swap instead.
- **GUARD D is now in place (WP5), so the bridge is closed.** Routing cancels
  `BeforeDamageChangedEvent` for every `WoundHostComponent` entity and the seam inside
  `TryChangeDamage` re-applies the damage to parts. Nothing carries `WoundHostComponent` until WP7,
  so the tree is still behaviourally inert.
- **`WoundDamageRoutingSystem`'s D23 and D27 side tables are keyed by body and only live for the
  duration of one `OnBeforeDamageChanged` call** (both are cleared in its `finally`). A later WP that
  adds another routing entry point must decide whether it wants them; `RouteThroughBodyModifiers`
  falls back to `(0f, null, null)` and `AccumulateApplied` is a no-op when no pass is open.
- **Every `// WOLFGATE` guard in `SharedBodySystem.Targeting.cs` and `DamageableSystem.cs` is gated on
  component presence only.** Making any of them `_net.IsServer`-gated gives a permanent client
  mispredict (PLAN 8.3 trap 3): the client would keep running Shitmed's spread while the server routes
  through Onyx.
- **`<WoundableComponent, DamageChangedEvent>` is now claimed** by `WoundDamageProjectionSystem` (D11).
  PLAN 5.2 marks it exclusive: nothing in `_WF` or a later WP may subscribe that pair.
- **`BloodstreamSystem.TryModifyWoundBleedProjection` is `internal`**, so only `Content.Server` code can
  write a wound host's `BleedAmount`. That is the whole point of GUARD E3 — `CirculatoryStreamSystem`
  (also `Content.Server` after D15) is the single caller. A later WP that moves any bleeding code back to
  `Content.Shared` must re-open this gate deliberately, not by widening the modifier.
- **Every `HealingComponent` in the game now defaults to `HealDamage: true, HealWounds: true,
  TreatmentCapabilities: [Biological]`** (HOOK 7). No existing prototype sets them, so every current
  medical item will treat wounds on wound hosts once WP7 lands `WoundHost`. Ointment/brutepack tuning is a
  WP11/D4 balance item, not a bug.
- **`WolfmedBodyPartComponent.MaxDamage` defaults to zero, and zero disables amputation overflow
  entirely.** Every limb abstract needs a row in WP7's `parts.yml` (13 abstracts across
  `Resources/Prototypes/Body/Parts/base.yml` and `Resources/Prototypes/_Shitmed/Body/Parts/base.yml`), or
  some limbs can never be severed once phase 3 lands `AmputationSystem`. Confirmed done for all 10
  limb-typed abstracts (`BaseTorso`, `BaseHead`, `Base{Left,Right}{Arm,Hand,Leg,Foot}`) as of WP7;
  `BasePartInorganic`/`BaseTorsoInorganic`/`BasePart` intentionally carry no row (see WP7 Deviations).
- **`WoundHost` is now live on 17 organic species (WP7).** Every prior WP's guards, hooks and compat
  systems become reachable for the first time. If any post-WP7 bug report reads like "limbs regenerate
  through wounds" or "double armour"/"no armour", re-check GUARDs A/B/C/E/E3 and HOOK 10 before assuming a
  new bug — those paths were previously untested because nothing carried `WoundHostComponent`.
- **New synthetic species must be added to `WolfmedWoundHostExclusionSystem.ExcludedAncestors`**
  (`Content.Shared/_WF/Wolfmed/Body/`), not opted out via YAML — RT has no component-removal mechanism
  (see WP7 Deviations). Check this list whenever a fork adds another `BaseMobSpeciesOrganic` descendant
  that is not truly organic.
