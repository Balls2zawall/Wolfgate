# Circulation dependency / API-gap analysis

Scope: `Content.Shared/_Onyx/Chemistry/Circulation/` (4 files, Onyx commit `2f5bab9`) against Wolfgate worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (`WG`). Read-only; nothing in `WG` was modified. All Onyx paths are relative to `C:/tmp/onyx` (sparse checkout unless noted "via `git show`" for paths outside the sparse set). All Wolfgate paths are relative to `WG`.

## 0. Headline conclusion

Onyx's circulation module is built on top of an **upstream SS14 solution/metabolism rewrite that Wolfgate has not received** (RT 277 vs 289; see `WOLFMED_HANDOFF.md`). Concretely:

- Onyx's `BloodstreamComponent` and `MetabolizerComponent` are **`Content.Shared`, networked, predicted**. Wolfgate's are **`Content.Server`-only**, with a different field layout (`MetabolizerComponent` has no per-stage `Solutions`/`Stages` model at all; `BloodstreamComponent` has no `MetabolitesSolutionName`/`AdjustedUpdateInterval`).
- Onyx's solution-container layer has a new `SolutionManagerComponent` (the old `SolutionContainerManagerComponent` is `[Obsolete]` and self-migrates on init). Wolfgate is still entirely on the **old** `SolutionContainerManagerComponent` — `SolutionManagerComponent` does not exist at all.
- Because `CirculatoryStreamSystem` is declared in `Content.Shared` in Onyx but its `Update()` queries `BloodstreamComponent`/`MetabolizerComponent` directly, **a verbatim Shared port cannot compile in Wolfgate** — those two component types are `Content.Server`-only here. This is a structural blocker, not a missing-method gap (see §5).
- Separately, and very usefully: **for Phase 1 (organic humans only), almost none of `CirculatoryStreamSystem`'s actual logic ever runs.** Every non-trivial code path in the file is gated on a *non-primary* circulatory stream existing, and the only stream defined anywhere in the pinned Onyx prototype set is `Organic`, which **is** the primary stream (`Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml`, `CirculatoryStreamPrototype.PrimaryStream = "Organic"`). See §6 for the full walk-through. This shrinks the real Phase-1 port to roughly 60 lines out of 474.

## 1. File inventory

| File | Lines | Purpose |
|---|---:|---|
| `CirculatoryStreamComponent.cs` | 25 | Per-body networked state: bleed rates per stream, which streams are initialized/configured. |
| `CirculatoryStreamPrototype.cs` | 43 | YAML-defined stream (solution names, metabolism stage IDs, reference/blood solution, volume/rate tuning). |
| `SharedSolutionContainerSystem.CirculatoryStreams.cs` | 31 | Partial-class extension of `SharedSolutionContainerSystem` adding `TryCreateCirculatorySolution`/`TryDeleteCirculatorySolution`. |
| `CirculatoryStreamSystem.cs` | 474 | The system: creates/destroys the extra per-stream solutions, wires them into the metabolizer, ticks non-primary bleed-out, and exposes lookup/`SetBleedRates` helpers used by Wounds. |

## 2. `CirculatoryStreamComponent.cs` — external symbols

```
Onyx: C:/tmp/onyx/Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamComponent.cs:1-25
```

| Symbol | Status | Citation | Notes |
|---|---|---|---|
| `Robust.Shared.GameStates.NetworkedComponent` | SAME | engine | — |
| `[AutoGenerateComponentState(raiseAfterAutoHandleState: true)]` | SAME | `WG/RobustToolbox/Robust.Shared/Analyzers/ComponentNetworkGeneratorAuxiliary.cs:55` — ctor takes `raiseAfterAutoHandleState` | RT277 supports this overload and `AfterAutoHandleStateEvent`. |
| `[AutoGenerateComponentPause]` / `[AutoPausedField]` | SAME | `WG/RobustToolbox/Robust.Shared/Analyzers/ComponentPauseGeneratorAttributes.cs:17,34` | Present in RT277. |
| `Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.TimeOffsetSerializer` | SAME | used pervasively in `WG` (e.g. `Content.Server/Body/Components/BloodstreamComponent.cs:25`) | — |
| `Robust.Shared.Prototypes.ProtoId<T>` | SAME | engine | — |

**Verdict:** portable verbatim. No gaps.

## 3. `CirculatoryStreamPrototype.cs` — external symbols

```
Onyx: C:/tmp/onyx/Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamPrototype.cs:1-43
```

| Symbol | Status | Citation | Notes |
|---|---|---|---|
| `[Prototype]` (bare, no type string) | SAME | `WG/Content.Shared/Access/AccessGroupPrototype.cs:10` and dozens more; engine `PrototypeAttribute(string? type = null, ...)` at `WG/RobustToolbox/Robust.Shared/Prototypes/Attributes.cs:40` | Auto-derives YAML tag `circulatoryStream` from the class name — matches `Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml`'s `type: circulatoryStream`. |
| `Content.Shared.Chemistry.Components.Solution` / `ReagentQuantity` ctor `Solution(IEnumerable<ReagentQuantity>)` | SAME | `WG/Content.Shared/Chemistry/Components/Solution.cs:156`; `WG/Content.Shared/Chemistry/Reagent/ReagentQuantity.cs:11` | `new Solution(new[] { new ReagentQuantity("Blood", 600) })` compiles unchanged. |
| `Content.Shared.FixedPoint.FixedPoint2` | SAME | engine-adjacent, used everywhere | — |
| `Content.Shared.Metabolism.MetabolismStagePrototype` (`ProtoId<MetabolismStagePrototype> MetabolismStage`, `MetabolitesStage`) | **MISSING** | Onyx: `git show HEAD:Content.Shared/Metabolism/MetabolismStagePrototype.cs` (not in sparse checkout, confirmed via `git ls-tree`). `WG`: `find … -iname MetabolismStagePrototype.cs` → no hits; only `Content.Shared/Body/Prototypes/MetabolismGroupPrototype.cs` exists, a different, older concept (a metabolizer processes a *list of groups*, not *typed stages* with paired solutions). | See §7. This is the field that makes the prototype non-verbatim-portable. |

**Verdict:** 8 of 10 fields port verbatim (`SolutionName`, `MetabolitesSolutionName`, `TemporarySolutionName`, `ReferenceSolution`, `MaxVolumeModifier`, `MetabolismTransferRate`, `MaxReagentsProcessable`, `ID`). `MetabolismStage`/`MetabolitesStage` must be dropped or replaced (recommendation in §8) since `MetabolismStagePrototype` doesn't exist and nothing in Wolfgate's metabolizer consumes a "stage" concept.

## 4. `SharedSolutionContainerSystem.CirculatoryStreams.cs` — external symbols

```
Onyx: C:/tmp/onyx/Content.Shared/_Onyx/Chemistry/Circulation/SharedSolutionContainerSystem.CirculatoryStreams.cs:1-31
```

This file is a **partial-class extension** of `Content.Shared.Chemistry.EntitySystems.SharedSolutionContainerSystem`. Wolfgate's class is declared identically as `public abstract partial class SharedSolutionContainerSystem : EntitySystem` (`WG/Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs:61`), so a same-named partial file is mechanically legal — the problem is what it calls inside:

| Symbol | Status | Citation | Notes |
|---|---|---|---|
| `Content.Shared.Chemistry.Components.SolutionManager.SolutionManagerComponent` | **MISSING** | Onyx: `git show HEAD:Content.Shared/Chemistry/Components/SolutionManager/SolutionManagerComponent.cs` — new component, `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]`, holds `Dictionary<string, Entity<SolutionComponent>> Solutions`. `WG`: `grep -rn "class SolutionManagerComponent" Content.Shared` → 0 hits. | Onyx's own `SolutionContainerManagerComponent` (same file, `git show HEAD:Content.Shared/Chemistry/Components/SolutionManager/SolutionContainerManagerComponent.cs`) is `[Obsolete]` and only exists to migrate into `SolutionManagerComponent` on `ComponentInit`, then deletes itself. In Wolfgate, `SolutionContainerManagerComponent` (`WG/Content.Shared/Chemistry/Components/SolutionManager/SolutionContainerManagerComponent.cs:13`) is the **live, non-obsolete** manager type — Wolfgate never received this rewrite. |
| `private Entity<SolutionComponent> CreateDefaultSolution(...)` | **MISSING** | Onyx: defined in `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs:1171,1179` (`git show`). `WG`: `grep -n CreateDefaultSolution Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs` → 0 hits. | Wolfgate has no equivalent private helper by this name. |
| `TryGetSolution(EntityUid, string, out Entity<SolutionComponent>?, out _)` | SAME (already exists as a different overload; used to check pre-existence) | `WG/Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs:141-159` | Fine. |
| `Del(EntityUid)` (engine `EntityManager.DeleteEntity`) | SAME | engine | — |

**Wolfgate has an existing equivalent, just under a different name and with different idempotency semantics:** `EnsureSolutionEntity(Entity<SolutionContainerManagerComponent?> entity, string name, ..., out Entity<SolutionComponent>? solutionEntity, FixedPoint2 maxVol = default, Solution? prototype = null)` at `WG/Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs:1092-1153`. Differences from Onyx's `TryCreateCirculatorySolution`:
- Onyx's version **fails** if the solution already exists or if the entity has no `SolutionManagerComponent` at all.
- Wolfgate's `EnsureSolutionEntity` **creates** the `SolutionContainerManagerComponent` if missing, and **resolves** (rather than fails) if the solution already exists, returning `existed = true`.

For circulation's actual call site (`InitializeStream`, which already checks `streams.InitializedStreams.Contains(stream)` before calling this), the more permissive Wolfgate behavior is safe to substitute directly.

**Verdict:** cannot be ported verbatim (missing type + missing method). Needs a `// WOLFGATE` rewrite of the two-method body against `EnsureSolutionEntity`/`TryGetSolution`/`RemoveAllSolution`; the partial-class file itself (path, namespace, class declaration) stays exactly as Onyx wrote it.

## 5. `CirculatoryStreamSystem.cs` — external symbols

```
Onyx: C:/tmp/onyx/Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs:1-474
```

### 5.1 Dependencies (`[Dependency]` fields, lines 26-33)

| Symbol | Status | Citation |
|---|---|---|
| `BloodstreamSystem` | **DIFFERENT namespace/assembly** | Onyx: `Content.Shared.Body.Systems.BloodstreamSystem` (shared). WG: `Content.Server.Body.Systems.BloodstreamSystem` (`WG/Content.Server/Body/Systems/BloodstreamSystem.cs`) — server-only. A `Content.Shared` system cannot DI a `Content.Server` type. **This is the structural blocker** (§0, §5.4). |
| `SharedBodySystem` | SAME (verified) | WG: `Content.Shared/Body/Systems/SharedBodySystem.cs` + partials. See §5.3 for the two methods actually called. |
| `INetManager`, `IPrototypeManager`, `IGameTiming` | SAME | engine services, used identically in `WG`. |
| `SharedPuddleSystem` | SAME | `WG/Content.Shared/Fluids/SharedPuddleSystem.cs:164` — `TrySpillAt(EntityUid, Solution, out EntityUid, bool sound = true, TransformComponent? = null)`. Onyx's call `_puddles.TrySpillAt(body, contents, out _, sound: false)` matches this overload exactly. |
| `SharedSolutionContainerSystem` | SAME (see §5.5 for individual method calls) | `WG/Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs` |
| `MobStateSystem` | SAME | `WG/Content.Shared/Mobs/Systems/MobStateSystem.cs:65` — `IsDead(EntityUid, MobStateComponent? = null)`. |

### 5.2 Component/event types used

| Symbol | Status | Citation | Notes |
|---|---|---|---|
| `Content.Shared.Body.Components.BloodstreamComponent` | **MISSING (wrong assembly)** | WG: `Content.Server.Body.Components.BloodstreamComponent` (`WG/Content.Server/Body/Components/BloodstreamComponent.cs:16`) — no `[NetworkedComponent]`, not predicted. Field-level diffs also matter: Onyx has `MetabolitesSolutionName`/`MetabolitesSolution` and computed `AdjustedUpdateInterval` (`= UpdateInterval * UpdateIntervalMultiplier`, `Content.Shared/Body/Components/BloodstreamComponent.cs:37-50` via `git show`); WG has neither — only a flat `UpdateInterval` (`WG/Content.Server/Body/Components/BloodstreamComponent.cs:31-32`) and a `ChemicalSolutionName`/`ChemicalSolution` pair Onyx's shared version doesn't have at all. `BloodSolutionName`/`BloodSolution` **are** SAME by name and type (`WG:147,161`). | See §7 for the full field diff table. |
| `Content.Shared.Metabolism.MetabolizerComponent` | **MISSING (wrong assembly + wrong shape)** | Onyx: `Content.Shared.Metabolism.MetabolizerComponent` (`git show HEAD:Content.Shared/Metabolism/MetabolizerComponent.cs`) — shared, `Dictionary<ProtoId<MetabolismStagePrototype>, MetabolismSolutionEntry> Solutions`, `HashSet<ProtoId<MetabolismStagePrototype>> Stages` (referenced further down the file, not shown in the excerpt but used via `metabolizer.Comp.Stages.Add/Remove`), explicitly grants `[Access(typeof(MetabolizerSystem), typeof(_Onyx.Chemistry.Circulation.CirculatoryStreamSystem))]` — i.e. **Onyx itself patched this upstream file specifically to let CirculatoryStreamSystem touch it** (comment `// <Onyx-CirculatoryStreams-edited>`). WG: `Content.Server.Body.Components.MetabolizerComponent` (`WG/Content.Server/Body/Components/MetabolizerComponent.cs:13-69`) — server-only, uses `List<MetabolismGroupEntry> MetabolismGroups` (an ordered list of `ProtoId<MetabolismGroupPrototype>` + rate modifier), no `Solutions` dict, no `Stages` set, no `MaxReagentsProcessable`-driven per-stage wiring at all. | The entire "stage" model (a stage = named metabolism phase with its own paired solution + transfer target) does not exist in Wolfgate's metabolizer, which instead just walks an ordered list of *groups* against a single configured `SolutionName`/`SolutionOnBody`. `ConfigureMetabolizer`/`InitializeStream`/`HasStageConflict` (`CirculatoryStreamSystem.cs:200-308`) cannot be ported against Wolfgate's `MetabolizerComponent` without rewriting the metabolizer itself — out of scope for this file. |
| `Content.Shared._Onyx.Wounds.WoundHostComponent` | MISSING (expected — Wounds not ported yet) | Onyx: `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:15-30`, a bare marker/tag component (`Dictionary<BodyPartType, float> TargetWeights`). | Cross-module dependency on the Wounds port, not a circulation-specific gap. `Update()`'s query (`EntityQueryEnumerator<CirculatoryStreamComponent, BloodstreamComponent, WoundHostComponent>()`, line 51) will match zero entities until `WoundHostComponent` exists and is applied to organic mobs. |
| `Content.Shared._Onyx.Wounds.WoundableComponent` | MISSING (expected) | Onyx: `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:156-169` — `ProtoId<BodyPartProfilePrototype> Profile = "OrganicBodyPartProfile"`. | Same as above; `GetPartStream`/`GetAttachedStreams`/`TryGetPartSolution` all key off `WoundableComponent.Profile`. |
| `Content.Shared._Onyx.Wounds.BodyPartProfilePrototype` (`.CirculatoryStream` field) | MISSING (expected) | Onyx: `Content.Shared/_Onyx/Wounds/WoundPrototype.cs:135-157` — `ProtoId<CirculatoryStreamPrototype> CirculatoryStream = "Organic"`. | Same. The **only** value ever assigned to this field in the pinned commit is the default `"Organic"` — see §6. |
| `Content.Shared.Body.Events.BleedModifierEvent` | **MISSING** | Onyx: `Content.Shared/Body/Events/BleedModifierEvent.cs` (via `git show`, not in sparse set) — `[ByRefEvent] record struct BleedModifierEvent(float BleedAmount, float BleedReductionAmount)`, raised on the body before bleeding. WG: `grep -rln BleedModifierEvent Content.Shared Content.Server` → 0 hits. | Only touched in the (Phase-1-inert, see §6) `Update()` bleed-out loop for non-primary streams. |
| `Content.Shared.Body.Events.MetabolismExclusionEvent` | **MISSING** | Onyx: `Content.Shared/Body/Events/MetabolismExclusionEvent.cs` (via `git show`) — `[ByRefEvent] readonly record struct MetabolismExclusionEvent(string? SolutionName) { List<ReagentId> Reagents }`, doc-commented as "Event called by `Content.Server.Body.Systems.MetabolizerSystem`". WG: 0 hits anywhere. | Handler `OnMetabolismExclusion` (lines 341-356) is dead for Phase 1 regardless (see §6). |
| `Content.Shared.Chemistry.Reaction.ReactionAttemptEvent` | SAME | WG: `Content.Shared/Chemistry/Reaction/ChemicalReactionSystem.cs:299` — `record struct ReactionAttemptEvent(ReactionPrototype Reaction, Entity<SolutionComponent> Solution)`. Identical shape to Onyx's. |
| `Content.Shared.Chemistry.EntitySystems.SolutionRelayEvent<TEvent>` | **DIFFERENT shape** | Onyx: `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.Relays.cs:16` (via `git show`) — `record struct SolutionRelayEvent<TEvent>(TEvent Event, Entity<SolutionComponent> Solution)`. WG: `WG/Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.Relays.cs:52` — `record struct SolutionRelayEvent<TEvent>(TEvent Event, EntityUid ContainerEnt, string Name)`. | Onyx's version carries the solution *entity*; Wolfgate's carries the *container* entity + the solution's *name string*. `OnReactionAttempt`'s `args.Solution.Comp.Id` (line 364) has no direct Wolfgate equivalent — the adaptation is `args.Name` (already the solution name, no `.Comp` needed) since WG's `SolutionComponent` also has no `.Id` field (`WG/Content.Shared/Chemistry/Components/SolutionComponent.cs:15-21`, only `.Solution`; Onyx's does have `.Id`, `git show HEAD:Content.Shared/Chemistry/Components/SolutionComponent.cs:24-27`). The subscription mechanism itself (`SubscribeLocalEvent<ContainedSolutionComponent, ReactionAttemptEvent>` relaying up to the container) **does** exist in WG (`WG/…Relays.cs:82`), so the event still fires — only the field access needs adapting. |
| `Content.Shared.EntityEffects.Effects.EntitySpawning.SpawnEntity` | **MISSING** | Onyx: new ECS-style entity-effect class. WG: `find Content.Shared/EntityEffects -maxdepth 3 -type d` → only the empty root folder exists; no `Effects/EntitySpawning` subfolder at all. | Confirms D5: Wolfgate has no `EntityEffectSystem<T>` port yet. |
| `Content.Shared.EntityEffects.Effects.Solution.AreaReactionEffect` | **MISSING** | Same as above; WG's `AreaReactionEffect` is the **old-style** `Content.Server.EntityEffects.Effects.AreaReactionEffect : EntityEffect` (`WG/Content.Server/EntityEffects/Effects/AreaReactionEffect.cs:24`). | See next row — this doesn't matter for Phase 1. |
| `Content.Shared.Bed.Components.StasisBedBuckledComponent` | **MISSING** | Onyx: a marker applied to the buckled mob. WG: no `Content.Shared/Bed/Components/` folder exists; the stasis-bed side is entirely `Content.Server` (`WG/Content.Server/Bed/Components/StasisBedComponent.cs`, applied to the *bed*, not the mob) with no buckled-mob marker at all. | Only referenced in the Phase-1-inert secondary-bleed loop (`Update()`, line 69). |
| `Content.Shared.Body.Part` (bare `using`, no symbol visibly consumed) | N/A | Onyx: this **namespace string** resolves to Onyx's own relocated `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs`, which keeps the *original* namespace declaration `namespace Content.Shared.Body.Part;` (`git show HEAD:Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:12`) even though the file physically lives under `_Onyx/`. WG's own (Shitmed) `BodyPartComponent` happens to sit at the *same* namespace, `Content.Shared/Body/Part/BodyPartComponent.cs` → `namespace Content.Shared.Body.Part;`. | No symbol from this `using` is actually referenced in `CirculatoryStreamSystem.cs` — it appears to be a vestigial import. Flagging only because the namespace match is coincidental (both forks kept the vanilla name for a heavily-modified type) and must not be read as "the types are compatible." |

### 5.3 `SharedBodySystem` calls — signature verification (per task instructions)

Two calls are made: `_body.GetBodyChildren(body)` (line 332) and `_body.BodyHasChild(body, part)` (line 394).

| Onyx signature | Wolfgate signature | Verdict |
|---|---|---|
| `GetBodyChildren(EntityUid body)` — `Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:133` (via `git show`; note Onyx's *only* `SharedBodySystem.cs` lives under `_Onyx/Body/Systems/` — it fully replaces the vanilla file while keeping the vanilla namespace `Content.Shared.Body.Systems`) | `GetBodyChildren(EntityUid? id, BodyComponent? body = null, BodyPartComponent? rootPart = null)` — `WG/Content.Shared/Body/Systems/SharedBodySystem.Body.cs:256` | **SAME call site.** Wolfgate's extra two params are optional and default harmlessly; `_body.GetBodyChildren(body)` compiles unchanged. Semantics differ under the hood (Onyx walks Nubody's tree, Wolfgate walks Shitmed's), but that's exactly the intended substitution — circulation should walk *Wolfgate's* body tree. |
| `BodyHasChild(EntityUid body, EntityUid part)` — same file, `:191` | `BodyHasChild(EntityUid bodyId, EntityUid partId, BodyComponent? body = null, BodyPartComponent? part = null)` — `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:978` | **SAME call site**, same reasoning. |

This confirms the handoff's dependency-map row ("Already exist with the same names… verify the signatures match") — verified compatible for the two methods circulation actually calls.

### 5.4 The structural blocker: Shared vs. Server

`CirculatoryStreamSystem.Update()` (line 51) does:

```csharp
var query = EntityQueryEnumerator<CirculatoryStreamComponent, BloodstreamComponent, WoundHostComponent>();
```

and `OnMetabolizerInit`/`ConfigureMetabolizer`/`InitializeStream` all take `MetabolizerComponent` by value. Both `BloodstreamComponent` and `MetabolizerComponent` are `Content.Server`-only types in Wolfgate. **`Content.Shared` cannot reference `Content.Server`** (standard SS14 assembly graph: Server → Shared, Client → Shared, never the reverse) — so this file cannot compile as a `Content.Shared` system in Wolfgate no matter how the individual method bodies are adapted. This is separate from, and more fundamental than, any single missing symbol above.

Consequence for the consumer: `Content.Shared/_Onyx/Wounds/WoundBleedingSystem.cs:28` DI's `CirculatoryStreamSystem` directly (`[Dependency] private CirculatoryStreamSystem _circulation`) and calls `_circulation.GetPartStream(...)` / `_circulation.SetBleedRates(body, streamRates)` at lines 305, 318, 323 (confirmed via `git show`, file not in sparse set). If `CirculatoryStreamSystem` moves to `Content.Server`, `WoundBleedingSystem` (intended to stay `Content.Shared`, matching Onyx and the Wounds phase-1 plan) can no longer DI it directly. See §8 for the recommended split.

### 5.5 `SharedSolutionContainerSystem` calls made directly by `CirculatoryStreamSystem.cs`

Unlike the partial-class shim (§4), these calls go through the *public, EntityUid-based* overloads, which resolve their manager-component type internally — so they are unaffected by the `SolutionManagerComponent`/`SolutionContainerManagerComponent` split:

| Call (Onyx line) | Wolfgate signature | Status |
|---|---|---|
| `TryGetSolution(body, name, out Entity<SolutionComponent>?, out _)` (63, 84, 407, 431) | `WG:141-159` | SAME |
| `SplitSolution(solution.Value, FixedPoint2)` (87) | `WG:347` | SAME |
| `UpdateChemicals(temporary.Value)` (95) | `WG:302` (extra optional args default) | SAME |
| `SetCapacity(solution, FixedPoint2)` (282-284) | `WG:434` | SAME |
| `TryAddSolution(solution, fill)` (288) | `WG:603` | SAME |
| `ResolveSolution(body, name, ref Entity<SolutionComponent>?, out _)` (400, 422) | `WG:103,117` | SAME |
| `TryCreateCirculatorySolution` / `TryDeleteCirculatorySolution` (266, 269, 275, 317-319) | — | Only exist via the shim in §4; adapt there. |

## 6. Why most of `CirculatoryStreamSystem.cs` is inert in Phase 1

`Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml` (full contents, pinned commit):

```yaml
- type: circulatoryStream
  id: Organic
```

That is the **only** `circulatoryStream` prototype that exists anywhere in the pinned Onyx tree (`grep -rln "circulatoryStream\|CirculatoryStream" Resources/` → this one file). `CirculatoryStreamPrototype.PrimaryStream = "Organic"` (`CirculatoryStreamPrototype.cs:12`). `BodyPartProfilePrototype.CirculatoryStream` defaults to `"Organic"` (`WoundPrototype.cs:157`) and — per the handoff's Phase-1/Phase-5 split — no other species profile exists yet to override it. So for every organic body part in Phase 1, `GetPartStream` (line 384-389) always returns `PrimaryStream`.

Tracing that through:

- `GetAttachedStreams` (329-339) collects one stream per part, then `SynchronizeStreams` (121, 157) **removes `PrimaryStream` from the attached set** before doing anything else. Result: `attached` is always empty for organic bodies.
- `SynchronizeStreams` early-returns `if (attached.Count == 0 && !existed) return;` (140) — a `CirculatoryStreamComponent` is **never added** to an organic body.
- Every one of `ConfigureMetabolizer`, `InitializeStream`, `HasStageConflict`, `RemoveStream`, `OnMetabolizerInit`, `OnStreamState`, `OnStreamShutdown` only runs against an existing `CirculatoryStreamComponent` or a non-empty `attached` set → **all dead code for Phase 1.**
- `Update()`'s per-tick bleed loop (51-98) both requires a `CirculatoryStreamComponent` (query, line 51) *and* explicitly skips the primary stream inside the loop (`if (stream == CirculatoryStreamPrototype.PrimaryStream || …) continue;`, line 61) — dead twice over for organic bodies.
- `OnReactionAttempt` (358-382) and `OnMetabolismExclusion` (341-356) are subscribed **on `CirculatoryStreamComponent`** — never fires for organic bodies, since the component is never attached. (Also separately redundant: Wolfgate's own `BloodstreamSystem` already subscribes `SolutionRelayEvent<ReactionAttemptEvent>` on `BloodstreamComponent` and cancels `CreateEntityReactionEffect`/`AreaReactionEffect` reactions in blood — `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:57-58, 71-90, 97-106`. Onyx's own equivalent logic lives in *its* forked `BloodstreamSystem`, not in circulation, for the same reason.)

**What actually matters for Phase 1** is the primary-stream fast path inside three methods:

- `GetPartStream` (384-389) — trivial prototype lookup, no Bloodstream/Metabolizer dependency at all.
- `TryGetPartSolution` / `TryGetStreamSolution` (391-437) — for `stream == PrimaryStream`, both just resolve `BloodstreamComponent.BloodSolutionName` → `BloodstreamComponent.BloodSolution` (409-412, 424-433 for GetStreamSolution's Primary branch). This is the one piece Wounds actually needs: "give me the blood solution for this body part."
- `SetBleedRates` (439-473) — for a rates dictionary containing only the `PrimaryStream` key (the only case possible in Phase 1), the method reduces to exactly one line: `_bloodstream.TryModifyWoundBleedProjection((body, bloodstream), rates.GetValueOrDefault(PrimaryStream) - bloodstream.BleedAmount);` (444-445). Everything after that (447-473) only executes if a *secondary* stream key is present, which cannot happen in Phase 1.

`TryModifyWoundBleedProjection` itself is **not part of these 4 files** — it's `internal` on Onyx's own forked `Content.Shared/Body/Systems/BloodstreamSystem.cs:562` (via `git show`) — but it's the one piece of the Bloodstream API surface this module hard-requires and Wolfgate doesn't have (§7).

## 7. `BloodstreamComponent`/`BloodstreamSystem` field-level diff (Onyx shared vs. Wolfgate server)

| Aspect | Onyx (`Content.Shared/Body/Components/BloodstreamComponent.cs`, shared, networked) | Wolfgate (`Content.Server/Body/Components/BloodstreamComponent.cs`, server-only) |
|---|---|---|
| Assembly/prediction | `Content.Shared`, `[NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]` (`:18-19`) | `Content.Server`, no networking attributes at all |
| `ChemicalSolutionName`/`ChemicalSolution` | **Absent** | Present (`WG:18,32,151,167`) — Wolfgate keeps the classic separate "chemicals" solution the metabolizer drains from |
| `MetabolitesSolutionName`/`MetabolitesSolution` | Present (`:25,179,199`) | **Absent** |
| `AdjustedUpdateInterval` (computed, `UpdateInterval * UpdateIntervalMultiplier`) | Present (`:37-50`) | **Absent** — only a flat `UpdateInterval` (`WG:32`) |
| `BloodSolutionName`/`BloodSolution` | Present (`:23,167,185`) | **SAME** (`WG:19,147,161`) |
| `BleedAmount`, `BleedReductionAmount`, `MaxBleedAmount`, `BloodlossThreshold`, `BloodlossDamage`, `BloodlossHealDamage`, `BloodRefreshAmount`, `BleedPuddleThreshold`, `DamageBleedModifiers`, `BleedingAlert` | Present | **SAME** (matching names/types in `WG:45-100,143,184`) — this half of the component is essentially unchanged from vanilla in both forks. |
| `TryModifyBleedAmount` guard for wound-routed entities | `BloodstreamSystem.cs:562-583` (via `git show`): public `TryModifyBleedAmount(ent, amount)` refuses to run `if (HasComp<WoundHostComponent>(ent))`; a separate `internal TryModifyWoundBleedProjection(ent, amount)` bypasses that guard — this is the only sanctioned way to push a value into `BleedAmount` for a wound-host entity. Marked `// <Onyx-WoundSystem-edited>`. | `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:406-421` — `TryModifyBleedAmount` has **no** such guard and there is no `TryModifyWoundBleedProjection` equivalent; anything can freely add to `BleedAmount`. |
| `TickBleed`/`Update()` decay skip for wound hosts | `if (!HasComp<WoundHostComponent>(entity)) TryModifyBleedAmount(...)` (`:515-516`, via `git show`) | `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:135-137` — unconditional decay every tick, no `WoundHostComponent` concept |

## 8. Recommended port plan

1. **`CirculatoryStreamComponent.cs`** → `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamComponent.cs`, verbatim, no `// WOLFGATE` edits needed (§2).
2. **`CirculatoryStreamPrototype.cs`** → same path, verbatim except drop or repoint `MetabolismStage`/`MetabolitesStage` (§3). Recommend a `// WOLFGATE` comment-out of both fields rather than inventing a fake `MetabolismStagePrototype` — nothing in the Phase-1 code path reads them (§6).
3. **`SharedSolutionContainerSystem.CirculatoryStreams.cs`** → same path (Shared is correct here; this partial has zero Bloodstream dependency). Rewrite the two method bodies against `EnsureSolutionEntity`/`RemoveAllSolution` + `TryGetSolution`, marked `// WOLFGATE` (§4).
4. **`CirculatoryStreamSystem.cs`** — split along the Shared/Server line Wolfgate already draws for the rest of the Bloodstream stack (§5.4):
   - Keep a small `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` containing **only** `GetPartStream` (needs `IPrototypeManager` + `WoundableComponent`, nothing Bloodstream-related). This is what `WoundBleedingSystem` (Shared) and any other Shared Wounds code can call directly, unchanged from Onyx.
   - Add a new, `// WOLFGATE`-authored `Content.Server/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.Bloodstream.cs` (or a second partial) holding `TryGetPartSolution`, `TryGetStreamSolution`, and `SetBleedRates`, trimmed to the Phase-1 primary-only paths identified in §6 — i.e. no `Update()` tick, no `SynchronizeStreams`/`ConfigureMetabolizer`/`InitializeStream`/`HasStageConflict`/`RemoveStream`, no `OnMetabolizerInit`/`OnStreamState`/`OnStreamShutdown`/`OnMetabolismExclusion`/`OnReactionAttempt` subscriptions. Document in the port manifest that these are deliberately dropped, not overlooked, and are the re-sync point once Phase 5 (species streams) needs the full machinery — at which point Wolfgate's own Bloodstream/Metabolizer will likely also need the shared/predicted rewrite as a prerequisite, which is a much larger, separate decision.
   - **Decide how the Server half is reached from Shared `WoundBleedingSystem`.** Two options, both reasonable — flag to the orchestrator:
     (a) Port `WoundBleedingSystem` itself to `Content.Server` too (extra deviation from Onyx, but consistent with the handoff's existing "Bloodstream/Healing/Bed are server-side here" pattern), letting it DI the server-side circulation piece directly; or
     (b) keep `WoundBleedingSystem` Shared and have it raise a small `[ByRefEvent]` (e.g. `WoundBleedRatesChangedEvent(EntityUid Body, Dictionary<ProtoId<CirculatoryStreamPrototype>, float> Rates)`) that only the new Server-side circulation system subscribes to — the same Shared-event/Server-handler bridge Wolfgate already uses for `ApplyMetabolicMultiplierEvent` (`WG/Content.Server/Bed/BedSystem.cs:104,113,162`, a Wolfgate-local type distinct from Onyx's same-named event, so no collision) and Onyx uses for `MetabolismExclusionEvent`/`BleedModifierEvent`.
5. In `BloodstreamSystem.cs`/`BloodstreamComponent.cs` (`Content.Server`), add the two-to-three-line `// WOLFGATE` hooks needed once `WoundHostComponent` exists (Phase 1 Wounds deliverable, not this file, but noted here since §7 shows exactly where): a `WoundHostComponent` guard in `TryModifyBleedAmount` plus an internal `TryModifyWoundBleedProjection`-equivalent for the circulation system to call, and the `!HasComp<WoundHostComponent>` guard around the passive-decay line in `Update()`.
6. Register `Resources/Prototypes/_Onyx/Chemistry/circulatory_streams.yml` verbatim under `Resources/Prototypes/_Onyx/Chemistry/` (D6 layout) — it is a single 2-line file with no Wolfgate-incompatible fields.

## 9. Full `SubscribeLocalEvent` inventory (as required by the task)

Only `CirculatoryStreamSystem.cs` subscribes to anything (the other 3 files are components/prototypes/partial-helpers).

| # | Subscription (`Initialize()` line) | Component type status | Event type status | Phase-1 relevance | Recommendation |
|---|---|---|---|---|---|
| 1 | `SubscribeLocalEvent<MetabolizerComponent, ComponentStartup>(OnMetabolizerInit)` (38) | MISSING (wrong assembly + shape, §5.2) | SAME (engine `ComponentStartup`) | Dead — `CirculatoryStreamComponent` never coexists with it in Phase 1 (§6) | Drop for Phase 1 |
| 2 | `SubscribeLocalEvent<CirculatoryStreamComponent, AfterAutoHandleStateEvent>(OnStreamState)` (39) | Ported (§2) | SAME | Dead (component never attached, §6) | Drop for Phase 1 |
| 3 | `SubscribeLocalEvent<CirculatoryStreamComponent, ComponentShutdown>(OnStreamShutdown)` (40) | Ported | SAME | Dead | Drop for Phase 1 |
| 4 | `SubscribeLocalEvent<CirculatoryStreamComponent, MetabolismExclusionEvent>(OnMetabolismExclusion)` (41) | Ported | MISSING (§5.2) | Dead | Drop for Phase 1 |
| 5 | `SubscribeLocalEvent<CirculatoryStreamComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnReactionAttempt)` (42) | Ported | DIFFERENT shape (§5.2) but subscribable | Dead + redundant with `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:57-58` | Drop for Phase 1 |

All five subscriptions are safe to omit from the Phase-1 port; none of them can fire for an organic-only body given the pinned prototype set (§6), and re-adding them is exactly the marker for when Phase 5 (non-organic circulatory streams) becomes real work.

## 10. Blockers / open questions for the orchestrator

1. **Structural (not a missing-API gap):** `CirculatoryStreamSystem` cannot stay a single `Content.Shared` file in Wolfgate because it needs `BloodstreamComponent`/`MetabolizerComponent`, which are `Content.Server`-only here. Needs the Shared/Server split in §8, and a decision on how `WoundBleedingSystem` reaches the Server half (§8 item 4).
2. **`SolutionManagerComponent`/`CreateDefaultSolution` gap (§4)** is small and mechanically shimmable — not a blocker, just needs the two-method rewrite.
3. **`MetabolismStagePrototype`/stage-based `MetabolizerComponent` (§3, §5.2)** don't exist in Wolfgate at all. Not a blocker for Phase 1 (the code paths that need them are dead per §6), but flags that **Phase 5 species streams (IPC/Slime/Plant blood) cannot be ported until Wolfgate's metabolizer gets the shared stage-based rewrite** — a much bigger, separate piece of work than anything in this file set. Recommend calling this out explicitly in the phase-5 planning rather than discovering it then.
4. No existing `Circulation`/`_Onyx`/`_WF/Wolfmed` code exists anywhere in the Wolfgate worktree today (`grep -rln "Circulat" …` only matches the unrelated TEG power generator; `find … -iname "*_Onyx*" -o -iname "*Wolfmed*"` → no hits) — clean slate, no naming collisions to worry about for this module.
