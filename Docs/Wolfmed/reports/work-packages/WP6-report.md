# WP6 — Bleeding, circulation system, healing (server half, D13/D14/D15)

**Branch:** `clanker/wolfmed-port-orchestration-454c3d` in `WG = C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
**Build status:** `Content.Server` **0 errors**, `Content.Client` **0 errors**, `Content.IntegrationTests` **0 errors**.
**Nothing committed.**

---

## 1. Files created / modified

### Vendored Onyx code relocated to `Content.Server` (D13)

| # | Onyx path | Wolfgate path | Status |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Wounds/WoundBleedingSystem.cs` | `WG/Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs` | adapted (2 `using` lines) |
| 2 | `Content.Shared/_Onyx/Wounds/WoundInternalBleedingSystem.cs` | `WG/Content.Server/_Onyx/Wounds/WoundInternalBleedingSystem.cs` | adapted (2 `using` lines + M1 fix) |
| 3 | `Content.Shared/_Onyx/Wounds/OrganDamageSystem.cs` | `WG/Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` | adapted (D26 + D8) |
| 4 | `Content.Shared/_Onyx/Wounds/WoundHealingSystem.cs` | `WG/Content.Server/_Onyx/Wounds/WoundHealingSystem.cs` | adapted (D14 + D12 + D31) |
| 5 | `Content.Shared/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` | `WG/Content.Server/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` | adapted / trimmed (D15) |
| 6 | `Content.Shared/_Onyx/Body/Systems/OrganHealthSystem.cs` | `WG/Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs` | adapted (D13 + D8) |

All six keep their Onyx namespaces (`Content.Shared._Onyx.Wounds`, `Content.Shared._Onyx.Chemistry.Circulation`, `Content.Shared._Onyx.Body.Systems`). None of the Onyx originals carries a license header, so none was dropped.

### Vendored verbatim

| # | Onyx path | Wolfgate path | Status |
|---|---|---|---|
| 7 | `Content.Shared/_Onyx/Body/OrganDamageComponent.cs` | `WG/Content.Shared/_Onyx/Body/OrganDamageComponent.cs` | **verbatim** (23 lines, registers as `OrganDamage`, no collision) |

### New `_WF` code

| # | Wolfgate path | Status |
|---|---|---|
| 8 | `WG/Content.Shared/_WF/Wolfmed/Body/WolfmedOrganComponent.cs` | new — `Health`, `MaxHealth`, `DestructionWound`, `DestructionWoundSeverity`; `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]`, registers as `WolfmedOrgan` |

### Upstream Wolfgate hooks (§3)

| # | Wolfgate path | Hook |
|---|---|---|
| 9 | `WG/Content.Server/Body/Systems/BloodstreamSystem.cs` | **GUARD E** + **GUARD E3** |
| 10 | `WG/Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` | **GUARD E4** |
| 11 | `WG/Content.Server/Medical/Components/HealingComponent.cs` | **HOOK 7 / D14** |
| 12 | `WG/Content.Server/Medical/HealingSystem.cs` | **HOOK 8** |

### Vendored `_Onyx` files edited beyond WP6's own table (2 one-line visibility changes)

| # | Wolfgate path | Change |
|---|---|---|
| 13 | `WG/Content.Shared/_Onyx/Wounds/WoundSystem.cs` | `internal void HandlePartDamageApplied` → `public` |
| 14 | `WG/Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs` | `internal void HandlePartDamageApplied` → `public` |

### Docs

| # | Path | Change |
|---|---|---|
| 15 | `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | 16 rows added/replaced, a `### WP6` deviations section, 2 new hazards, re-sync line bumped to WP6 |

**Not touched (deliberate):** `Content.Server/Body/Systems/RespiratorSystem.cs` — PLAN §3 lists it under "explicitly NOT touched" and `hooks-a.md` §3a/§3b confirm there is no phase-1 hook (breathing immunity is already present; `InitiallyLungedComponent` / OrganConsequences are Nubody glue). `Content.Shared/_Onyx/Body/FunctionalOrganComponent.cs` — skipped per WP6 #8; only its `OrganFunctionChangedEvent` was needed. GUARD E2 stays for WP10.

---

## 2. Every `// WOLFGATE` edit, with reason

### `Content.Server/_Onyx/Wounds/WoundBleedingSystem.cs`
- `:4` `using Content.Shared.Body.Components;` → `using Content.Server.Body.Components;` — D13, `BloodstreamComponent` is server-only in Wolfgate.
- `:7` **added** `using Content.Server.Body.Systems;` beside the existing `using Content.Shared.Body.Systems;` — D13, `BloodstreamSystem` is server-only, but `SharedBodySystem` still comes from the shared namespace (a literal swap, as PLAN WP6 #2 words it, is `CS0246`).
- Body otherwise byte-identical, including the dead `using Content.Shared.Body;` at `:3` (PLAN §2.17).

### `Content.Server/_Onyx/Wounds/WoundInternalBleedingSystem.cs`
- `:1`, `:3` — the same two `using` swaps (this file needs no `SharedBodySystem`, so `Content.Shared.Body.Systems` could be replaced outright).
- `:67` **M1 fix** — `_bloodstream.TryModifyBloodLevel((body, bloodstream), -amount);` → `_bloodstream.TryModifyBloodLevel(body, -amount, bloodstream);`. Wolfgate's signature is `(EntityUid, FixedPoint2, BloodstreamComponent? = null)`; reaching `EntityUid` from the tuple literal needs two chained user-defined conversions, which C# does not do (`CS1503`).

### `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs`
- `:2-3` `using Content.Shared.Body;` → `using Content.Shared.Body.Organ;` + `using Content.Shared._WF.Wolfmed.Body;` — Wolfgate's `OrganComponent` is in `Content.Shared.Body.Organ`; organ health is on `WolfmedOrganComponent` (D8).
- `:25-26` **D26** — `[Dependency] private AmputationSystem _amputation` commented out with a `TODO: phase 3`; leaving only the call commented would be `CS0246` on the field.
- `:38-39` **D26** — `_amputation.HandlePartDamageApplied(part, ref args);` commented out, same marker.
- `:51-57` **D8** — the organ list is built from `WolfmedOrganComponent` instead of Onyx's health-bearing `OrganComponent`: `.Select(...CompOrNull<WolfmedOrganComponent>...)` → `.Where(non-null && Health > 0 && HasComp<OrganDamageComponent>)` → `.Select(...!)`.
- `:94-96` **D8** — `PickOrgan`'s parameter and return type retargeted to `(EntityUid Id, WolfmedOrganComponent Component)`.
- `MaxHealth` / `ChangeHealth` reads follow from the list's element type; no further edits.

### `Content.Server/_Onyx/Wounds/WoundHealingSystem.cs`
- `:9` `using Content.Shared.Medical.Healing;` → `using Content.Server.Medical.Components;` — D14, Wolfgate keeps `HealingComponent` server-side.
- `:10` added `using Content.Shared._WF.Wolfmed.Compat;` — D12 facade.
- `:19` `[Dependency] DamageableSystem _damage` → `WolfmedDamageableSystem _damage` — D12.
- `:112-113` **D31** — `healing.Comp.DamageContainers?.Select(x => new ProtoId<DamageContainerPrototype>(x)).ToList()` at the single `ResolveHealingPartEvent` construction site. `ResolveHealingPart` (`:44`) and `IsCompatiblePart` (`:154`) keep Onyx's signatures, exactly as D31 requires. `using System.Linq;` was already present at `:1`.

### `Content.Server/_Onyx/Chemistry/Circulation/CirculatoryStreamSystem.cs` (D15)
- File-level `// WOLFGATE` block naming everything dropped: `Update`, `SynchronizeStreams` (both overloads), `GetAttachedStreams`, `ConfigureMetabolizer`, `InitializeStream`, `HasStageConflict`, `RemoveStream`, `DeleteSolution`, `OnMetabolizerInit`, `OnStreamState`, `OnStreamShutdown`, `OnMetabolismExclusion`, `OnReactionAttempt`, and all five `SubscribeLocalEvent`s. `Organic` **is** `PrimaryStream`, so none of it is reachable in phase 1; Wolfgate also has no `MetabolismStagePrototype`, `MetabolismExclusionEvent` or `TryCreateCirculatorySolution`.
- `:1-2` server `using`s for `BloodstreamComponent` / `BloodstreamSystem`.
- `TryGetPartSolution` / `TryGetStreamSolution` keep the primary-stream branch and return `false` for anything else, marked.
- `SetBleedRates` reduces to the single `_bloodstream.TryModifyWoundBleedProjection(body, rates.GetValueOrDefault(PrimaryStream) - bloodstream.BleedAmount, bloodstream);` call (Wolfgate's parameter shape, not Onyx's tuple).

### `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`
- `:1` `using Content.Server.Body.Components;` — Wolfgate's `BrainComponent` is server-only.
- `:2` `using Content.Shared.Body.Organ;` — `OrganComponent`'s namespace.
- `:6` `using Content.Shared._WF.Wolfmed.Body;` — D8 organ health.
- `OrganFunctionChangedEvent` relocated into this file, namespace `Content.Shared._Onyx.Body`, marked — `FunctionalOrganComponent.cs` is Nubody glue and is not ported (D8). The file uses two block namespaces for this.
- `Update`'s query is `EntityQueryEnumerator<WolfmedOrganComponent, OrganComponent>` instead of `OrganComponent` alone, marked.
- `SetHealth` reads the owning body off `CompOrNull<OrganComponent>(organ)?.Body`, marked.
- `DestroyOrgan` uses `SharedBodySystem.RemoveOrgan(organId, organ)` instead of Onyx's `part.Organs` walk with `TryGetOrganInSlot`/`TryRemoveOrgan`, neither of which exists in Wolfgate; marked.

### `Content.Shared/_Onyx/Wounds/WoundSystem.cs` and `WoundFractureSystem.cs`
- One line each: `internal void HandlePartDamageApplied` → `public`, marked *"D13 puts OrganDamageSystem (the sole caller) in Content.Server, so internal no longer reaches it."* There is no `InternalsVisibleTo` between `Content.Shared` and `Content.Server`. `WoundBleedingSystem`'s copy stays `internal` because it moved to `Content.Server` too.

### `Content.Server/Body/Systems/BloodstreamSystem.cs` — GUARD E + GUARD E3
- `:18` `using Content.Shared._Onyx.Wounds;`.
- `:213-215` **GUARD E** — `if (HasComp<WoundHostComponent>(ent)) return;` at the top of `OnDamageChanged`.
- `:411-428` **GUARD E3** — `TryModifyBleedAmount(EntityUid, float, BloodstreamComponent?)` becomes a forwarder; new `internal bool TryModifyWoundBleedProjection(EntityUid, float, BloodstreamComponent? = null)`; new `private bool TryModifyBleedAmount(EntityUid, float, BloodstreamComponent?, bool woundProjection)` carrying the original body plus `if (HasComp<WoundHostComponent>(uid) && !woundProjection) return false;`. The comment records that this deliberately no-ops the passive-decay call at `:137` and that the guard is **not** duplicated there.

### `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` — GUARD E4
- `:5` `using Content.Shared._Onyx.Wounds;`; `:39-42` `HasComp<WoundHostComponent>` early-return at the top of `OnDamageChanged`, with a note that hemophilia becomes a wound-bleeding multiplier later.

### `Content.Server/Medical/Components/HealingComponent.cs` — HOOK 7 / D14
- `:3` `using Content.Shared._Onyx.Wounds;` for `TreatmentCapability`.
- `:55-80` four additive `[DataField]`s inside a `// WOLFGATE: HOOK 7 / D14 start … end` block: `bool HealDamage = true`, `bool HealWounds = true`, `HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological]`, `HashSet<string>? AllowedWoundStages`. The comment records that the `HashSet<>` types are load-bearing (D31: `ResolveHealingPartEvent` takes `IReadOnlySet<>`). Nothing existing was changed.

### `Content.Server/Medical/HealingSystem.cs` — HOOK 8
- `:31-36` four `using`s (`_Onyx.Wounds`, `_WF.Wolfmed.Targeting`, `Damage.Prototypes`, `Robust.Shared.Prototypes`).
- `:53-54` two `[Dependency]`s: `WoundHealingSystem _woundHealing`, `WoundTargetResolver _woundTargets`.
- `:74-79` `OnDoAfter` wound-host branch → `OnWoundHostDoAfter(...)`, placed after the `Handled/Cancelled` checks and **before** the body-level `DamageContainers` test.
- `:149-273` the new block (§3 authorises more than two lines for HOOK 7/8):
  - `ResolveWoundTargetPart(user, target)` — the requested part, from the healer's Shitmed `TargetingComponent` via `WoundTargetResolver.TryResolveExact`, gated on `SharedTargetingSystem.IsSelectable`.
  - `GetHealingContainers(healing)` — the D31 `List<string>` → `List<ProtoId<DamageContainerPrototype>>` conversion, shared by the two construction sites in this file.
  - `OnWoundHostDoAfter` — Onyx's `OnDoAfter` wound branch plus Onyx's `FinishHealing`, re-expressed on Wolfgate's server-only API (`PopupEntity` not `PopupClient`, `_stacks.Use` not `ReduceCount`, `QueueDel` not `PredictedQueueDel`, `_audio.PlayPvs` not `PlayPredicted`).
  - `IsWoundDamaged(entity, healing, requestedPart)` — Onyx's wound-host half of `HasDamage`, as a *third parallel check* next to Wolfgate's `HasDamage`/`IsPartDamaged` (`hooks-a.md` §5b's lower-risk option). Raises `ResolveHealingPartEvent` and covers localized damage, treatable wounds, bleeding and blood-level restore.
- `:330-338` `TryHeal` — `var woundHost = HasComp<WoundHostComponent>(target);` and `!woundHost &&` in front of the body-level `DamageContainers` rejection (matching Onyx's `resolvedPart` branch).
- `:347-349` `TryHeal` — `anythingToDo` becomes a ternary on `woundHost`; the non-wound-host branch is Wolfgate's original expression, unchanged.

---

## 3. Deviations from PLAN.md, with justification

1. **`WoundSystem`/`WoundFractureSystem`'s `HandlePartDamageApplied` made `public`.** Not in PLAN's WP6 table. D13 moves `OrganDamageSystem` — the only `<WoundableComponent, PartDamageAppliedEvent>` subscriber and the only caller of all four `HandlePartDamageApplied` implementations — to `Content.Server`, a different assembly, with no `InternalsVisibleTo`. Without this the WP cannot compile. Minimal (one word per file), marked, and PLAN §5.2's "do not convert any of them to their own subscription" invariant is untouched.
2. **`WoundBleedingSystem` keeps `using Content.Shared.Body.Systems;` and *adds* the server one.** PLAN WP6 #2 words this as a swap; the file needs `SharedBodySystem` from the shared namespace and `BloodstreamSystem` from the server one, so a literal swap is `CS0246`.
3. **The requested healing part comes from the healer's Shitmed `TargetingComponent`, not `HealingDoAfterEvent.RequestedPart`.** `hooks-a.md` §5a/§6 want a `NetEntity? RequestedPart` field added to `Content.Shared/Medical/HealingDoAfterEvent.cs`, but that file is **not** in PLAN §3's authorised hook list and is not in WP6's table, so editing it would be an unauthorised upstream change. Wolfgate already picks the healed limb from the user's targeting today (`SharedBodySystem.Targeting.cs:129-131`), so this preserves current behaviour and Onyx's intent. **Behavioural consequence:** the part is re-read when the do-after completes rather than latched when it starts, so switching doll limbs mid-heal changes which limb is treated. Landing `RequestedPart` later is ~6 lines in one file plus swapping the two `ResolveWoundTargetPart` call sites.
4. **`IsWoundDamaged` is a separate method, not folded into `HasDamage`.** `hooks-a.md` §5b explicitly offers this as the lower-risk option, because Wolfgate's `HasDamage(DamageableComponent, HealingComponent)` has no entity parameter and already composes with a separate `IsPartDamaged`. `HasDamage` and `IsPartDamaged` are byte-unchanged and still serve every non-wound-host.
5. **`OrganHealthSystem.DestroyOrgan` does not walk the parent part's organ slots.** `TryGetOrganInSlot`/`TryRemoveOrgan` do not exist in Wolfgate; `SharedBodySystem.RemoveOrgan(organId, organ)` locates the containing container itself. The destruction wound is still created on the parent part and the organ is still deleted on both paths — behaviourally identical.
6. **`OrganHealthSystem`'s update query pairs `WolfmedOrganComponent` with `OrganComponent`.** Onyx queries `OrganComponent` alone because health lives on it. A stray `WolfmedOrganComponent` on a non-organ entity is therefore ignored rather than destroyed — strictly safer.
7. **`CirculatoryStreamSystem.TryGetPartSolution`/`TryGetStreamSolution` return `false` for any non-primary stream** instead of resolving one. D15 drops `InitializeStream`, so no secondary stream can exist in phase 1 and the fallback branches were unreachable. `SetBleedRates` likewise drops the `CirculatoryStreamComponent` bookkeeping and the `SynchronizeStreams` fallback.
8. **`OrganFunctionChangedEvent` is declared in a `Content.Server` file** (PLAN WP6 #8 says "relocate … here"), so it is **not reachable from `Content.Shared`**. Nothing consumes it in phase 1. Flagged for WP10/WP11 — see §5.
9. **`WolfmedOrganComponent` lives in `Content.Shared/_WF/Wolfmed/Body/`, namespace `Content.Shared._WF.Wolfmed.Body`,** matching WP4's `WolfmedBodyPartComponent`, rather than `wounds-c.md` §6.3(a)'s suggested `Content.Shared._WF.Wolfmed` / `WolfmedOrganHealthComponent` name. PLAN WP6 #7 gives the path and the field list; the name follows PLAN's text.

No other deviation. No additional upstream files were touched.

---

## 4. Build output tails

### `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`
```
...Content.Server.Database.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 9.0.0 has a known high severity vulnerability, https://github.com/advisories/GHSA-w3x6-4m5h-cxqf
    22 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.61
```
(filtered run: `Build succeeded.` / `0 Error(s)`. All 22 warnings are pre-existing NuGet `NU1903`/`NU1510` advisories on `Content.Server.Database` and `RobustToolbox`, unrelated to this WP.)

### `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`
```
...Robust.Client.csproj : warning NU1510: PackageReference System.Text.Json will not be pruned. ...
    6 Warning(s)
    0 Error(s)

Time Elapsed 00:00:04.41
```

### `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt` (WP6 checkpoint extra)
```
Build succeeded.
    0 Error(s)
```

Verified by name that the new server types are actually in the output assembly (`bin/Content.Server/Content.Server.dll` contains `OrganHealthSystem`, `WoundHealingSystem`, `CirculatoryStreamSystem`, `WolfmedOrganComponent`, `OrganDamageSystem`, `WoundBleedingSystem`, `WoundInternalBleedingSystem`). All 14 touched files are pure CRLF with no doubled CR and no lone LF.

---

## 5. What a later WP must know

**New public symbols**

- `Content.Shared._WF.Wolfmed.Body.WolfmedOrganComponent` — `FixedPoint2 Health = 15`, `FixedPoint2 MaxHealth = 15`, `ProtoId<WoundPrototype>? DestructionWound`, `FixedPoint2 DestructionWoundSeverity`. Networked, registers as **`WolfmedOrgan`**. **No prototype carries it yet** — until WP11's organ prototypes add `- type: WolfmedOrgan`, `OrganDamageSystem` finds zero eligible organs and `OrganHealthSystem` iterates nothing. That is the intended phase-1 state, not a bug.
- `Content.Shared._Onyx.Body.OrganDamageComponent` — registers as **`OrganDamage`**. Also needs WP11 prototypes.
- `Content.Shared._Onyx.Body.Systems.OrganHealthSystem` — `SetHealth(Entity<WolfmedOrganComponent>, FixedPoint2)`, `ChangeHealth(Entity<WolfmedOrganComponent>, FixedPoint2)`. **Server assembly.**
- `Content.Shared._Onyx.Body.OrganFunctionChangedEvent(EntityUid Body, bool Functional)` — `[ByRefEvent]`, declared inside `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`. **Not reachable from `Content.Shared`.** WP10's `FractureEffectsSystem` is shared but uses `OrganGotInsertedEvent`/`OrganGotRemovedEvent` (WP2's `OnyxBodyEvents.cs`), so nothing breaks today. Any shared consumer of `OrganFunctionChangedEvent` must first move this declaration into `Content.Shared/_WF/Wolfmed/Compat/`.
- `BloodstreamSystem.TryModifyWoundBleedProjection(EntityUid, float, BloodstreamComponent? = null)` — **`internal`**, so only `Content.Server` can write a wound host's `BleedAmount`. `CirculatoryStreamSystem.SetBleedRates` is the single caller. Do not widen the modifier; if bleeding code ever moves back to `Content.Shared`, re-open the gate deliberately.
- `HealingComponent.HealDamage` / `.HealWounds` / `.TreatmentCapabilities` / `.AllowedWoundStages` (HOOK 7).
- `HealingSystem.ResolveWoundTargetPart`, `.GetHealingContainers`, `.OnWoundHostDoAfter`, `.IsWoundDamaged` — all `private`; the two call sites of `ResolveWoundTargetPart` are what a later `RequestedPart` change would replace.

**Changed signatures**

- `WoundSystem.HandlePartDamageApplied` and `WoundFractureSystem.HandlePartDamageApplied` are now `public` (were `internal`). Their contract is unchanged: **only** `OrganDamageSystem` may call them, and none of the four `HandlePartDamageApplied` implementations may take its own `<WoundableComponent, PartDamageAppliedEvent>` subscription (PLAN §5.2).
- `BloodstreamSystem.TryModifyBleedAmount(EntityUid, float, BloodstreamComponent?)` keeps its public signature but now **returns false for wound hosts**. Every existing caller (healing items, stasis beds, the `Update()` decay) silently no-ops on a wound host from WP7 onward. That is GUARD E3's whole point, but it is invisible at the call site.

**Balance / behaviour notes for WP7's playtest**

- Every `HealingComponent` in the game now defaults to `HealDamage: true, HealWounds: true, TreatmentCapabilities: [Biological]`. No prototype overrides them, so **every current medical item will treat wounds** on wound hosts the moment WP7 lands `WoundHost`. Ointment/brutepack/gauze differentiation is a WP11 + D4 tuning item.
- Hemophilia (`_Mono` trait) does nothing on wound hosts (GUARD E4). Re-express it as a wound-bleeding multiplier in `_WF/Wolfmed` when phase 2 lands.
- GUARD E2 (the "looks pale" examine gate) is **still open** and must land in WP10 together with `_Onyx/HealthExaminable`, or the message disappears with nothing replacing it.

**Open TODOs left in the tree**

- `OrganDamageSystem.cs:25-26` and `:38-39` — `// TODO: phase 3` on the `AmputationSystem` dependency and its fan-out call. WP11 re-enables **both**.
- `HealingDoAfterEvent.RequestedPart` (deviation 3) — optional hardening for explicit-part healing.

**Duplicate-subscription ledger (all new pairs registered by WP6, all verified free against PLAN §5.2)**

`<WoundBleedingComponent, {ComponentInit, ComponentShutdown, WoundCreatedEvent, WoundChangedEvent, WoundStateChangedEvent, WoundRemovedEvent}>`, `<WoundHostComponent, SleepStateChangedEvent>`, `<WoundInternalBleedingComponent, {WoundChangedEvent, WoundStateChangedEvent, WoundRemovedEvent}>`, `<WoundableComponent, PartDamageAppliedEvent>`, `<WoundHostComponent, ResolveHealingPartEvent>`. `CirculatoryStreamSystem` and `OrganHealthSystem` register **no** subscriptions at all.
