# WP5 — The damage bridge (D2, D11, D23, D27, D28) — report

**Branch:** `clanker/wolfmed-port-orchestration-454c3d` in
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG).
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`.
**Both builds green (0 errors). Headless server reaches `Ready` with 0 `[ERRO]`, 0 `Duplicate
Subscriptions`, 13 `[WARN]` (the pre-existing baseline).** Nothing committed.

Scope note: PLAN WP5 #1/#2/#3 (`WoundDamageRoutingSystem`, `WoundDamageProjectionSystem`,
`_Onyx/Mobs/Systems/MobThresholdSystem`) were largely delivered by WP4, which explicitly handed WP5 the
two remaining routing edits (D23's AP side table and D27's applied-delta accumulator) and the three
upstream guard sites. Those are what WP5 added, plus WP5 #4, #5 and #6 in full.

---

## 1. Files created / modified

### Upstream `// WOLFGATE` hooks (PLAN §3)

| # | WG path | Change | §3 entry |
|---|---|---|---|
| 1 | `Content.Shared/Damage/Systems/DamageableSystem.cs` | GUARD D (routing seam) + GUARD D2 (`BeforeDamageChangedEvent` members + cancel return) | GUARD D, GUARD D2 |
| 2 | `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | GUARDs A + B + C | GUARD A/B/C |

### Vendored `_Onyx` file, modified

| # | Onyx path | WG path | Edits added by WP5 |
|---|---|---|---|
| 3 | `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | same | 6 (D23 side table + read, D27 accumulator + 3 write points, `args.Applied` write) |

### New `_WF` code

| # | WG path | Spec |
|---|---|---|
| 4 | `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | PLAN §2.11, D28 |

### Docs

| # | WG path |
|---|---|
| 5 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` — 4 new rows, a `### WP5` Deviations subsection, 2 stale Hazards replaced by 3 current ones, header re-sync line bumped |

**Upstream files WP5 modified: exactly two.** Whole-port upstream footprint after WP5:

```
$ git -C WG diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests
 Content.Server/Chat/Systems/ChatSystem.Emote.cs    |  2 +-    <- WP4 HOOK 6
 Content.Shared/Damage/Systems/DamageableSystem.cs  | 26 +++++++++++++++++++---   <- WP5
 .../Body/Systems/SharedBodySystem.Targeting.cs     | 12 +++++++++-               <- WP5
 .../_Shitmed/Targeting/SharedTargetingSystem.cs    |  4 ++++                     <- WP2 HOOK 5
 4 files changed, 39 insertions(+), 5 deletions(-)
```

Files #3 and #4 are untracked (inside `Content.Shared/_Onyx/` and `Content.Server/_WF/Wolfmed/`).

---

## 2. Every `// WOLFGATE` edit, with its reason

### `Content.Shared/Damage/Systems/DamageableSystem.cs` (GUARD D + GUARD D2)

| Line | Edit | Reason |
|---|---|---|
| `:7` | `using Content.Shared.Damage.Systems; // WOLFGATE` | `DamageDealtEvent` lives in that namespace (PLAN §2.3) and this file did **not** already import it — see deviation D-WP5-1 |
| `:8` | `using Content.Shared._Onyx.Wounds; // WOLFGATE` | `WoundHostComponent` for the seam gate (D18: test the Onyx component directly, no `_WF` marker) |
| `:41` | `private EntityQuery<WoundHostComponent> _woundHostQuery; // WOLFGATE` | cached query beside `_appearanceQuery`/`_damageableQuery` |
| `:64` | `_woundHostQuery = GetEntityQuery<WoundHostComponent>(); // WOLFGATE` | its assignment, beside the other two |
| `:214-216` | `new BeforeDamageChangedEvent(..., originFlag, armorPenetration, tool)` | D23 — routing cancels at `:219`, i.e. **before** the resistance block at `:231`, so the routed pass is the only one that sees armour. Without this every Wolfgate AP weapon loses its AP against every humanoid |
| `:219-220` | `return null;` → `return before.Applied;` | D27 — routing applies the damage itself; returning `null` would tell every caller "nothing happened". `HitscanBasicDamageSystem.cs:33-34`'s `if (damageDealt == null) return;` sits *inside* the hit-entity loop, so a pierce would stop at the first humanoid |
| `:255-265` | GUARD D: the `DamageDealtEvent` seam, gated on `_woundHostQuery`, with a defensive `damage = new DamageSpecifier(damage);` | the one upstream change the port cannot avoid. The copy is an addition to PLAN's block — see deviation D-WP5-2 |
| `:485-493` | `BeforeDamageChangedEvent` gains `float ArmorPenetration = 0f`, `EntityUid? Tool = null`, `DamageSpecifier? Applied = null` | D23/D27. Appending is safe: positional `record struct`, **exactly one construction site** (verified by grep across `Content.Shared`, `Content.Server`, `Content.Client`), whose members are already mutated by handlers |

Godmode (`SharedGodmodeSystem.cs:19`) and stasis (`SharedStasisSystem.cs:52`) never touch `Applied`, so they
still return `null` and behave bit-identically.

### `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` (GUARDs A/B/C)

| Line | Edit | Reason |
|---|---|---|
| `:9` | `using Content.Shared._Onyx.Wounds; // WOLFGATE` | D18 |
| `:65` | `private EntityQuery<WoundHostComponent> _queryWoundHost; // WOLFGATE` | beside `_queryTargeting` |
| `:69` | `_queryWoundHost = GetEntityQuery<WoundHostComponent>(); // WOLFGATE` | in `InitializeIntegrityQueue`, beside `_queryTargeting`'s assignment |
| `:116-118` | **GUARD A** — `if (_queryWoundHost.HasComp(ent.Owner)) return;` at the top of `OnTryChangePartDamage` | required on the *re-entrant* pass, where routing is a no-op (`_routing.Contains`) and `TryChangePartDamageEvent` does get raised. Without it every wound-host hit lands twice |
| `:234` | **GUARD B** — `&& !_queryWoundHost.HasComp(partEnt.Comp.Body)` in the sever condition | Wolfmed owns dismemberment (phase 3). `CheckBodyPart` at `:243` is deliberately left running — it drives limb enable/disable and the targeting doll |
| `:84` | **GUARD C** — `&& !_queryWoundHost.HasComp(body)` in `ProcessIntegrityTick`'s condition | Shitmed's 30 s part regen would silently erase wound damage; `WoundHealingSystem` + `BodyPartProfilePrototype` recovery own it |
| `:108` | **GUARD C** — `if (!_queryWoundHost.HasComp(part.Body))` around the `_integrityJobQueue.EnqueueJob` in `Update` | don't burn the job queue on wound hosts |

All four guards are **component-gated only, never `_net.IsServer`-gated** (PLAN §8.3 trap 3). A server-only
guard would give permanent client mispredict: the client would keep running Shitmed's spread while the
server routed through Onyx. `EntityQuery<T>.HasComp(EntityUid?)` exists
(`RobustToolbox/.../EntityManager.Components.cs:1918` → `:773`, `uid.HasValue && …`), so the two
`EntityUid?` call sites (`partEnt.Comp.Body`, `part.Body`) need no null dance.

### `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs`

| Line | Edit | Reason |
|---|---|---|
| `:52-54` | `_routedModifiers` side table `Dictionary<EntityUid, (float ArmorPenetration, EntityUid? Tool, DamageableSystem.DamageOriginFlag? OriginFlag)>` | D23 |
| `:56-57` | `_appliedDelta` side table `Dictionary<EntityUid, DamageSpecifier>` | D27 |
| `:78-79` | write the D23 tuple from `args.ArmorPenetration` / `args.Tool` / `args.OriginFlag` | D23. `OriginFlag` needed no new event member — Mono already carries it |
| `:81-83` | open the D27 accumulator for this body | D27 |
| `:87-99` | the Shitmed `targetPart` handoff restructured to a `requested` flag with **one** `try`/`finally` around `RouteThroughBodyModifiers`, instead of WP4's duplicated call | needed so both side tables are torn down on exactly one path |
| `:100-106` | `finally` clears `_requestedParts`, `_appliedDelta`, `_routedModifiers` | leak-free even if a handler throws |
| `:108-111` | `applied.TrimZeros(); args.Applied = applied;` | D27 — the routed delta reaches `TryChangeDamage`'s `return before.Applied` |
| `:115-125` | new `AccumulateApplied(EntityUid body, DamageSpecifier applied)` helper | folds one delta into the open pass; a no-op when no pass is open, so routing's public entry points (`TryApplyDamage`, `TryRouteTargetedDamage`, …) are unaffected |
| `:683-689` | read the D23 tuple and pass `armorPenetration:`/`tool:`/`originFlag:` into the facade's `ChangeDamage` | D23. `GetValueOrDefault` yields `(0f, null, null)` outside a `BeforeDamageChangedEvent` pass, i.e. the pre-WP5 behaviour |
| `:780` | `AccumulateApplied(body, appliedDamage);` in `RouteAppliedDamage`'s part write | D27 write point 1 (PLAN: "`RouteAppliedDamage`'s `out var appliedDamage`") |
| `:998` | `AccumulateApplied(body, appliedDamage);` in `ApplyPartChange` | D27 write point 2 (PLAN named `ApplyLocalizedHealing`'s `appliedDamage`; in the WG file that value lives in `ApplyPartChange`, which is `ApplyLocalizedHealing`'s only caller of `TryChangeDamage` and covers both of its branches) |
| `:1073` | `AccumulateApplied(body, applied);` in `ApplySystemicDamage` | D27 write point 3 |

### `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` (new, D28)

Subscribes **only** `<WoundHostComponent, BodyPartAddedEvent>` and `<WoundHostComponent,
BodyPartRemovedEvent>`. `<BodyComponent, BodyPart*Event>` is owned by
`_Shitmed/Body/Systems/SharedBodySystem.PartAppearance.cs:25-26`; both events are raised on the **body
only** (`Body/Systems/SharedBodySystem.Parts.cs:337-338`, `:357-358`), and the body carries
`WoundHostComponent`, so the wound-host-scoped pair is both free and correctly scoped. Verified free by
grep: today only `BodyComponent`, `HandsComponent`, `MantisBladeArmComponent` and
`CorticalBorerInfestedComponent` hold those events.

It closes PLAN §8.3 trap 2: `WoundDamageProjectionSystem.OnPartInserted` (`:107`) and `OnPartRemoved`
(`:117`) had **no caller** anywhere in the tree, so wounds would silently never initialise on surgically
attached limbs.

---

## 3. Deviations from PLAN.md, with justification

### D-WP5-1 — `DamageableSystem.cs` gained **two** usings, not one

PLAN §2.3 says the seam event's namespace is chosen "so the vendored files' existing
`using Content.Shared.Damage.Systems;` resolves it and so `DamageableSystem.cs` needs only one added
`using`". `DamageableSystem.cs` does **not** import `Content.Shared.Damage.Systems` (its using block is
20 lines and that is not among them, verified), so the seam needs it too. Both are one line each and
marked. The alternative — spelling the type `Systems.DamageDealtEvent` — is uglier for no benefit. No
ambiguity resulted: that namespace holds only `DamageContactsSystem`, `DamageExamineSystem`,
`DamageOnAttackedSystem`, `DamageOnHighSpeedImpactSystem`, `DamageOnHoldingSystem`,
`DamageOnInteractSystem`, `DamageProtectionBuffSystem`, `RequireProjectileTargetSystem`,
`SharedGodmodeSystem`, `StaminaSystem` and `DamageDealtEvent`, none of which collides with a simple name
used in this file. Both builds green.

### D-WP5-2 — GUARD D takes a defensive copy of the damage specifier (one added line)

**This is a bug fix, not a style preference, and it is the most important thing in this report.**

PLAN §3 GUARD D and `damage-bridge.md` §5.4 both raise `DamageDealtEvent(damage, …)` on the caller's
object, matching Onyx (`ONYX Content.Shared/Damage/Systems/DamageableSystem.API.cs:161-164` does the same).
In Wolfgate that is unsafe, because routing's `OnDamageDealt` suppresses the body write by **clearing the
dict in place** (`WoundDamageRoutingSystem.cs:132`), and inside `TryChangeDamage` the local `damage` is
still the *caller's* object whenever the two places that would re-bind it are skipped:

- `damage = DamageSpecifier.ApplyModifierSet(...)` (`:237-239`) and `damage = ev.Damage` (`:242-244`) are
  both inside `if (!ignoreResistances)` (`:231`);
- `ApplyUniversalAllModifiers` (`:296-318`) returns the **same object** when both cvars are `1f`, and
  mutates it in place otherwise.

So for any `ignoreResistances: true` call the dict routing clears is the caller's. Upstream call sites
that pass a component datafield straight in, verified by grep:
`ImmovableRodSystem.cs:122` (`ignoreResistances: true`), `RepairableSystem.cs:36` (`true`),
`BibleSystem.cs:165` (`true`), `PassiveDamageSystem.cs:50` (`true`), plus
`ClimbSystem.cs:521-522`, `ClumsySystem.cs:85`, `DamageOnHoldingSystem.cs:42`,
`DamagedByFlashingSystem.cs:21`, `RespiratorSystem.cs:302`, `StunProviderSystem.cs:65`,
`DamageOnHitSystem.cs:22`, `DamageOnLandSystem.cs:22`, `DamageNearbyArtifactSystem.cs:35`,
`FixtureDamageDealerSystem.cs:59`, `CEFallingDamageSystem.cs:22`. Without the copy, the **first** wound
host hit by an immovable rod (or repaired, or bible-healed) would permanently empty that component's
`Damage` datafield for the rest of the round, for every entity that shares the component.

Fix: `damage = new DamageSpecifier(damage);` as the first line inside the seam's `if` block
(`DamageableSystem.cs:260`), taking GUARD D from 9 lines to 10 — still PLAN's "~10 lines". `damage` is a
local that this method already re-binds three times, the write loop at `:267-285` iterates identical
contents, and `return delta` is unaffected. Cost: one allocation per wound-host damage application.
D29's neutering of `PassiveDamage` covers only one of the fifteen call sites above, so it is not a
substitute.

### D-WP5-3 — the D27 result is a non-null but possibly **empty** `DamageSpecifier`, not `null`

D27 does not say what routing should write when the routed pass lands nothing. `args.Applied` is set
unconditionally after routing runs, so a routed hit that is fully filtered out returns an empty specifier.
This is deliberate and strictly closer to pre-bridge behaviour than `null`: Wolfgate's own
`TryChangeDamage` already returns a non-null empty specifier at `:209-212`, and every reader of the
return value either null-checks and then tests the total
(`SharedMeleeWeaponSystem.cs:750`, `SharedBodySystem.Targeting.cs:215`,
`DamageOnAttackedSystem.cs:78`, `DamageOnInteractSystem.cs:80`, `DeepFryerSystem.cs:234`) or handles
`Empty` explicitly (`BibleSystem.cs:167`).

One visible edge: if routing's handler happens to run **before** godmode's on the same entity, a godmoded
wound host returns an empty specifier where it used to return `null` (`DamageUserOnTriggerSystem.cs:38`'s
`is not null` then reads `true`). Handler order between them is undefined and cannot be constrained —
`SharedGodmodeSystem` and `SharedStasisSystem` are `abstract`, and RT keys ordering on `GetType()`
(PLAN §5.4). No damage lands either way: the routed pass re-raises `BeforeDamageChangedEvent`, godmode
cancels it, `ChangeDamage` returns empty, `_applied` never receives the body, and `AccumulateApplied`
never fires.

### D-WP5-4 — `WolfmedBodyPartLifecycleSystem` does not take the two `OrganComponent` pairs

PLAN §5.2 assigns `<OrganComponent, OrganAddedToBodyEvent>` / `<OrganComponent,
OrganRemovedFromBodyEvent>` to this system, and the WP5 brief says it subscribes the two `BodyPart*`
pairs **only**. The brief wins: the sole consumer of `OrganGot*Event` is `FractureEffectsSystem.cs:34-35`,
which is phase 2 and not ported, so phase 1 would be registering two handlers with no readers. Both pairs
remain free and unclaimed; whoever lands `FractureEffectsSystem` should add them here.

### D-WP5-5 — the lifecycle handlers are asymmetric on purpose

`OnPartAdded` iterates `GetBodyPartChildren(args.Part)` and calls `OnPartInserted` + raises
`OrganGotInsertedEvent` per subtree member, matching Onyx's `SetSubtreeBody`
(`ONYX _Onyx/Body/Systems/SharedBodySystem.cs:64-111`). `OnPartRemoved` calls
`WoundDamageProjectionSystem.OnPartRemoved` **once** for the detached root and only fans the
`OrganGotRemovedEvent` over the subtree, because that projection method already walks to the detached
root itself (`RefreshDetachedDamage` → `GetBodyPartChildren`), so calling it per member would re-project
the same tree N times.

### D-WP5-6 — WP5 #1/#2/#3's other edits were already delivered by WP4

`WoundDamageRoutingSystem`, `WoundDamageProjectionSystem` and `_Onyx/Mobs/Systems/MobThresholdSystem` were
brought in by WP4 with every PLAN WP5 in-file edit except the two that needed GUARD D2's new members.
WP5 added only those two. No file was re-vendored.

### Non-deviations worth recording

- The `before: [typeof(SharedArmorPlateSystem)]` ordering on **both** of routing's
  `BeforeDamageChangedEvent` subscriptions (`:57`, `:59`, PLAN §8.3 trap 4 / §5.4) was landed by WP4 and is
  unchanged. Verified still present and identical on both lines.
- `BeforeDamageChangedEvent` has exactly one construction site
  (`DamageableSystem.cs:214`), so appending three optional positional members is safe.
- No `_net.IsServer` appears in any of the four guards.
- Line endings: both upstream files and the routing file stayed CRLF; the new
  `WolfmedBodyPartLifecycleSystem.cs` was normalised to CRLF to match; `WOLFMED_MANIFEST.md` stayed LF
  and was written with an explicit `encoding='utf-8'` (per WP4's D-WP4-0 recommendation).
- Nothing was committed; `git status` was only ever run path-limited.

---

## 4. The hand trace

### (a) A non-`WoundHost` mob takes damage exactly as before

`DamageableSystem.TryChangeDamage` (`Content.Shared/Damage/Systems/DamageableSystem.cs:194`):

1. `:214-217` — `BeforeDamageChangedEvent` is constructed with the three new members at their defaults
   (`0f`, `null`, `null`) and raised. Its only subscribers are `GodmodeComponent`
   (`SharedGodmodeSystem.cs:19`), `InsideStasisComponent` (`SharedStasisSystem.cs:52`) and
   `ArmorPlateProtectedComponent` (`SharedArmorPlateSystem.cs:46`). None reads or writes `Applied`.
2. `:219-220` — `return before.Applied` is reached only if one of those cancelled; `Applied` is still
   `null`, so this is bit-identical to the old `return null`.
3. `:223-227` — `TryChangePartDamageEvent` → `SharedBodySystem.OnTryChangePartDamage`
   (`_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:114`). **GUARD A** at `:116-118` tests
   `_queryWoundHost.HasComp(ent.Owner)` → false → Shitmed's spread runs unchanged.
4. `:231-250` — resistances, `DamageModifyEvent`, universal modifiers: untouched.
5. `:257` — **GUARD D**'s gate `_woundHostQuery.HasComp(uid.Value)` → false. `DamageDealtEvent` is
   **not raised at all** (not even inertly) and no copy is made. One `EntityQuery` lookup is the entire
   cost.
6. `:267-286` — write loop and `DamageChanged`: untouched.
7. The part's `DamageChangedEvent` → `SharedBodySystem.OnDamageChanged`
   (`SharedBodySystem.Targeting.cs:223`). **GUARD B** at `:234` evaluates
   `!_queryWoundHost.HasComp(partEnt.Comp.Body)` → the body is not a wound host → `true` → the sever
   condition is unchanged. `CheckBodyPart` at `:243` runs as before.
8. Regen: **GUARD C** at `:84` → `!HasComp(body)` → `true` → `ProcessIntegrityTick` unchanged; `:108` →
   `true` → the job is enqueued as before.

Net change for a non-wound-host: three new defaulted struct members and four `EntityQuery.HasComp`
lookups. No new event is raised on its path.

### (b) A `WoundHost` mob: cancel → routed pass → parts → projection → `before.Applied`

| # | Hop | file:line |
|---|---|---|
| 1 | entry — any caller | `Content.Shared/Damage/Systems/DamageableSystem.cs:194` |
| 2 | `new BeforeDamageChangedEvent(damage, origin, targetPart, false, originFlag, armorPenetration, tool)`, raised on the mob | `DamageableSystem.cs:214-217` |
| 3 | `WoundDamageRoutingSystem.OnBeforeDamageChanged` (subscribed `before: [typeof(SharedArmorPlateSystem)]`) | `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:57` → handler `:69` |
| 3a | guard: server, `!args.Cancelled` (godmode/stasis refused it → leave it refused), `!_routing.Contains` | `WoundDamageRoutingSystem.cs:73` |
| 3b | `args.Cancelled = true` — this is what makes `SharedArmorPlateSystem.OnBeforeDamageChanged` early-return at `_Mono/ArmorPlate/SharedArmorPlateSystem.cs:51`, so the plate absorbs only on the routed pass | `WoundDamageRoutingSystem.cs:75` |
| 3c | **D23** — `_routedModifiers[body] = (args.ArmorPenetration, args.Tool, args.OriginFlag)` | `:79` |
| 3d | **D27** — `_appliedDelta[body] = applied` (a fresh `DamageSpecifier`) | `:82-83` |
| 3e | Shitmed `targetPart` handoff into `_requestedParts`, gated on `SharedTargetingSystem.IsSelectable` so composite masks fall through | `:87-94` |
| 4 | `RouteThroughBodyModifiers(ent, args.Damage, args.Origin)` | `:98` → method `:637`; re-entrancy latch `_routing.Add` at `:643` |
| 4a | pre-resolve the target part (targeting snapshot / powered light / defibrillator → `Torso` / weighted random) | `:648-680` |
| 4b | **D23 read** + the routed call: `_damage.ChangeDamage(body, damage, ignoreResistances, interruptsDoAfters, origin, armorPenetration: …, tool: …, originFlag: …)` | `:686-688` |
| 5 | facade `WolfmedDamageableSystem.ChangeDamage` → `DamageableSystem.TryChangeDamage(..., canSever: false, canEvade: false, tool, originFlag)` | `Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs:28` (call at `:45-61`) |
| 6 | **re-entrant pass.** `BeforeDamageChangedEvent` #2: routing bails on `_routing.Contains` (`WoundDamageRoutingSystem.cs:73`); `SharedArmorPlateSystem` runs **here**, exactly once | `DamageableSystem.cs:214-217` |
| 7 | `TryChangePartDamageEvent` → **GUARD A** returns immediately | `SharedBodySystem.Targeting.cs:116-118` |
| 8 | body resistance set + `DamageModifyEvent` (`SharedArmorSystem`, inventory relay) carrying the D23 `armorPenetration` and `tool`; then the universal modifiers | `DamageableSystem.cs:231-253` |
| 9 | **GUARD D**: gate true → `damage = new DamageSpecifier(damage)` (D-WP5-2) → `new DamageDealtEvent(damage, origin, interruptsDoAfters)` raised | `DamageableSystem.cs:257-262` |
| 10 | `WoundDamageRoutingSystem.OnDamageDealt` — clones the damage, **clears** `args.Damage.DamageDict`, calls `RouteAppliedDamage` | `WoundDamageRoutingSystem.cs:126-134` |
| 10a | back in GUARD D: `damage.Empty` is now true → `return damage` → the body's own write loop at `:267` is never reached | `DamageableSystem.cs:263-264` |
| 11 | `RouteAppliedDamage` splits systemic / localized by `WoundHostComponent.LocalizedDamageTypes` | `WoundDamageRoutingSystem.cs:699-704` |
| 11a | systemic → `ApplySystemicDamage` → `SystemicDamageComponent.Damage` + chest pain; **D27** `AccumulateApplied` | `:1047`, accumulate at `:1073` |
| 11b | negative localized → `ApplyLocalizedHealing` → `ApplyPartChange`; **D27** `AccumulateApplied` | `:850`, `:978`, accumulate at `:998` |
| 11c | positive localized → `FilterPartDamage`, `PartDamageModifyEvent` (inventory relay), `AccumulateAmputationOverflow`, then `_damage.TryChangeDamage(part, localized, out appliedDamage, ignoreResistances: true, ignoreGlobalModifiers: true)`; `_applied.Add(body)`; **D27** `AccumulateApplied`; then `PartDamageAppliedEvent` on the part | `:771-785` |
| 12 | **parts written.** Recursion into `DamageableSystem.TryChangeDamage` for the **part**: the part has no `WoundHostComponent`, so GUARD D's gate is false and routing cannot recurse; `ignoreResistances: true` means `DamageModifyEvent` never fires, so Shitmed's `OnPartDamageModify` cannot double-apply armour | `DamageableSystem.cs:194`, gate `:257`, write `:267-286` |
| 13 | `DamageChangedEvent` on the part → `SharedBodySystem.OnDamageChanged`: **GUARD B** suppresses sever, `CheckBodyPart` still runs | `SharedBodySystem.Targeting.cs:223`, `:234`, `:243` |
| 14 | **projection.** `WoundDamageProjectionSystem.OnPartDamageDealt` (D11: `<WoundableComponent, DamageChangedEvent>`) → `_pain.ApplyDamage(part, delta, component)` → `RefreshBodyDamage(body)` | `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs:38`, handler `:93`, pain `:98`, refresh `:102` |
| 15 | `RefreshBodyDamage` sums `SystemicDamageComponent.Damage` (filtered by `CanBeDamagedBy`) + `GetPositiveDamage` of every part, then `_damage.SetDamage(body, total)` | `WoundDamageProjectionSystem.cs:156`, `:196` |
| 16 | **body `SetDamage` with `DamageDelta` preserved** (GUARD F): `delta = damage - comp.Damage`, `TrimZeros`, D30 zero-instead-of-remove, then `DamageChanged(body, comp, delta.Empty ? null : delta, interruptsDoAfters: false)` — the delta is real, so bleeding, hemophilia, wake-on-damage, pain screams, forced say, kill attribution, NPC retaliation and ignite-on-heat all still fire | `Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs:148-180` |
| 17 | unwind: `RouteThroughBodyModifiers` returns `_applied.Remove(body)`; its `finally` clears `_requestedParts` / `_routing` | `WoundDamageRoutingSystem.cs:689`, `:691-695` |
| 18 | `OnBeforeDamageChanged`'s `finally` clears `_requestedParts`, `_appliedDelta`, `_routedModifiers`; then `applied.TrimZeros(); args.Applied = applied;` | `:100-106`, `:110-111` |
| 19 | **back in the outer `TryChangeDamage`**: `before.Cancelled` is true → `return before.Applied` — the routed delta, not `null` | `DamageableSystem.cs:219-220` |

Two properties the trace turns on:

- `args.Applied` survives because `BeforeDamageChangedEvent` is a `[ByRefEvent] record struct` handed to
  directed handlers by `ref`; the handler at hop 18 is writing the very struct the outer frame reads at
  hop 19 (the same mechanism `args.Cancelled = true` already relies on).
- `applied` at hop 3d is a reference the accumulator mutates in place, so removing the dictionary entry at
  hop 18 does not lose the data.

---

## 5. Build checkpoint — exact tails

### `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`

```
...\Content.Shared\_Mono\CorticalBorer\SharedCorticalBorerSystem.cs(137,17): warning RA0045: Use the proxy method AddComp instead of calling EntityManager.AddComponent directly [...\Content.Shared\Content.Shared.csproj]
    374 Warning(s)
    0 Error(s)

Time Elapsed 00:00:07.21
```

Filtered form required by the checkpoint:

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

(The 374 warnings are the pre-existing RA0045 analyzer noise from a cold `Content.Shared` compile; an
incremental rebuild reports 22, as in WP4. None originates in a WP5 file.)

### `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`

```
...\RobustToolbox\Robust.Shared\Robust.Shared.csproj : warning NU1510: PackageReference System.Reflection.Metadata will not be pruned. This package is automatically available and does not need to be referenced explicitly. Remove the PackageReference item.
    6 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.00
```

Filtered form:

```
$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

### Headless server smoke test

`bin/Content.Server/Content.Server.exe --cvar net.port=1222 --cvar status.enabled=false`, 180 s:

```
[INFO] root: Server Version 277.0.0.0 -> Ready
```

`grep -icE "\[ERRO\]|Duplicate Subscription|Unhandled|exception"` over the full log: **0**. 13 `[WARN]`
lines, identical to WP4's baseline. The two new `<WoundHostComponent, BodyPart*Event>` subscriptions
register cleanly.

---

## 6. What later WPs must know

### New / changed symbols

| Symbol | Where | Note |
|---|---|---|
| `BeforeDamageChangedEvent(DamageSpecifier, EntityUid?, TargetBodyPart?, bool, DamageOriginFlag?, float ArmorPenetration, EntityUid? Tool, DamageSpecifier? Applied)` | `Content.Shared.Damage.DamageableSystem` | **signature changed** — three appended optional positional members. Any new construction site must not rely on the old arity for positional args past `OriginFlag` |
| `DamageableSystem.TryChangeDamage` returns `before.Applied` on cancel | `DamageableSystem.cs:220` | `null` for godmode/stasis; the routed delta for wound hosts |
| `WolfmedBodyPartLifecycleSystem` | `Content.Server._WF.Wolfmed` | server-only; owns `<WoundHostComponent, BodyPartAddedEvent>` and `<WoundHostComponent, BodyPartRemovedEvent>` **exclusively** |
| `WoundDamageRoutingSystem.AccumulateApplied(EntityUid, DamageSpecifier)` | `Content.Shared._Onyx.Wounds` | `private`. A new routed write point must call it or its damage will be missing from `TryChangeDamage`'s return |
| `_routedModifiers`, `_appliedDelta` | same file | `private`, keyed by body, alive only for the duration of one `OnBeforeDamageChanged` |
| `_woundHostQuery` / `_queryWoundHost` | `DamageableSystem.cs:41` / `SharedBodySystem.Targeting.cs:65` | the guard queries; reuse them rather than adding more |

### Directed subscriptions WP5 registered

| Component | Event | Registrant |
|---|---|---|
| `WoundHostComponent` | `BodyPartAddedEvent` | `WolfmedBodyPartLifecycleSystem` |
| `WoundHostComponent` | `BodyPartRemovedEvent` | `WolfmedBodyPartLifecycleSystem` |

Still free and unclaimed: `<OrganComponent, OrganAddedToBodyEvent>` and
`<OrganComponent, OrganRemovedFromBodyEvent>` (D-WP5-4).

### Open TODOs handed forward

1. **The bridge is now closed, so WP7's `- type: WoundHost` is live ammunition.** Until WP7 lands it the
   tree is behaviourally inert; the first prototype that carries `WoundHostComponent` switches on the
   whole path traced in §4(b). WP7 should land D29 (`PassiveDamage` neutered) in the **same** YAML block
   as D21/D22, or every lightly-wounded humanoid gets a per-tick routed heal.
2. **WP6 owes GUARD E, GUARD E3, GUARD E4 and HOOK 7/HOOK 8.** GUARD F is already live inside the facade
   (`WolfmedDamageableSystem.SetDamage`), so from the moment WP7 lands, wound hosts **will** get real
   `DamageChangedEvent` deltas on the body — i.e. `BloodstreamSystem.OnDamageChanged` and
   `HemophiliaSystem.OnDamageChanged` will start firing on projected damage. Without GUARD E/E4 that is
   double bleeding, not a silent no-op.
3. **WP8's HOOK 10 (`SharedArmorSystem`) must route through `DamageSpecifier.PenetrateArmor(modifiers,
   args.Args.ArmorPenetration)`.** The AP value now reaches the routed pass correctly (D23), so a HOOK 10
   that bypasses `PenetrateArmor` would throw it away again one step later.
4. **HOOK 11 / HOOK 12 can be written today.** `MobThresholdSystem.CheckVitalDamage(EntityUid,
   DamageableComponent)` exists (WP4, declared at `_Onyx/Mobs/Systems/MobThresholdSystem.cs:25`) and
   routing already calls it at `WoundDamageRoutingSystem.cs:528`.
5. **Do not make any of GUARD A/B/C/D `_net.IsServer`-gated** under any circumstances (PLAN §8.3 trap 3).
   If a later WP sees client-side wound-host oddities, the answer is D35 (accept unpredicted wound-host
   damage in phase 1), not a server gate.
6. **The GUARD D defensive copy (D-WP5-2) must not be "cleaned up" on an Onyx re-sync.** Onyx does not
   have it and does not need it; Wolfgate does. It is recorded in the manifest's WP5 Deviations.
7. **`WoundDamageProjectionSystem.OnPartInserted`/`OnPartRemoved` now have exactly one caller.** If phase 3
   adds a second lifecycle path (surgical attach through a different event), check that it does not
   double-call them — `RefreshBodyDamage` is `_projecting`-latched, but `SetupPart` is not.
8. **`TryChangeDamage`'s cancel return is now meaningful.** Any future `BeforeDamageChangedEvent`
   subscriber that cancels *and* applies damage itself should set `Applied`; one that cancels to **refuse**
   damage must leave it `null`.
