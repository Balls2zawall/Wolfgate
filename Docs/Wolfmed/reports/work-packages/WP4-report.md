# WP4 — Wounds core, shared half — report

**Branch:** `clanker/wolfmed-port-orchestration-454c3d` in
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG).
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`.
**Both builds green (0 errors). Headless server reaches `Ready` with 0 `[ERRO]` and no
`Duplicate Subscriptions`.** Nothing committed.

Scope note: the task brief extends PLAN.md §4 WP4's 14-file table with `WoundDamageRoutingSystem` and
`WoundDamageProjectionSystem` (PLAN WP5 #1/#2). Both are delivered, with every in-file `// WOLFGATE` edit
from PLAN's WP5 tables **except** the two that depend on GUARD D2's new `BeforeDamageChangedEvent` members
(D27's applied-delta accumulator) and on WP8 (D23's AP side table). The upstream WP5 hooks — GUARD D,
GUARD D2, GUARDs A/B/C — and `WolfmedBodyPartLifecycleSystem` are **not** touched, per the brief.
`Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` (PLAN WP5 #3) was pulled in because
`WoundDamageRoutingSystem.cs:466` calls `CheckVitalDamage` and will not compile without it.

---

## 1. Files created / modified

### Vendored from Onyx — verbatim (byte-identical, `cmp`-verified)

| # | Onyx path | WG path |
|---|---|---|
| 1 | `Content.Shared/_Onyx/Wounds/WoundEvents.cs` | same |
| 2 | `Content.Shared/_Onyx/Wounds/WoundBehaviors.cs` | same |
| 3 | `Content.Shared/_Onyx/Wounds/WoundPrototype.cs` | same |
| 4 | `Content.Shared/_Onyx/Wounds/WoundScarSystem.cs` | same |
| 5 | `Content.Shared/_Onyx/Wounds/WoundStatusEffectSystem.cs` | same |
| 6 | `Content.Shared/_Onyx/Wounds/PainSystem.cs` | same |

`cmp -s` against `C:/tmp/onyx/...` returns equal for all six. `PainSystem.cs` needed **no** edits: WP1's
`StatusEffectNew` + `PainNumbnessStatusEffectComponent` and WP2's `StunSystemOnyxCompat` /
`SharedChatSystem.Wolfmed` shims cover it exactly as PLAN §2.8/§2.9 predicted.

### Vendored from Onyx — modified

| # | Onyx path | WG path | Edits |
|---|---|---|---|
| 7 | `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs` | same | 4 |
| 8 | `Content.Shared/_Onyx/Wounds/WoundSystem.cs` | same | 2 |
| 9 | `Content.Shared/_Onyx/Wounds/BodyPartFunctionalitySystem.cs` | same | 1 |
| 10 | `Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs` | same | 4 |
| 11 | `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | same | 13 |
| 12 | `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` | same | 9 |
| 13 | `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` | same | 4 (file absent from the sparse checkout; read via `git -C C:/tmp/onyx show HEAD:<path>`) |

### New `_WF` code

| # | WG path | Spec |
|---|---|---|
| 14 | `Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs` | PLAN §2.12, D8 |
| 15 | `Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartSystem.cs` | PLAN §2.12, D8 |
| 16 | `Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs` | PLAN §2.13, D10 |

### Upstream hook

| # | WG path | Change |
|---|---|---|
| 17 | `Content.Server/Chat/Systems/ChatSystem.Emote.cs` | HOOK 6 / D25 — one word at `:60` |

### Docs

| # | WG path |
|---|---|
| 18 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` — **rebuilt** (see deviation D-WP4-0) plus 19 new rows, a `### WP4` Deviations subsection and 4 new Hazards |

**Upstream (non-`_Onyx`, non-`_WF`) files modified by WP4: exactly one.**

```
$ git -C WG diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests
 Content.Server/Chat/Systems/ChatSystem.Emote.cs            | 2 +-
 Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs | 4 ++++
 2 files changed, 5 insertions(+), 1 deletion(-)
```

`SharedTargetingSystem.cs` is WP2's HOOK 5, untouched here. `ChatSystem.Emote.cs` is WP4's HOOK 6.

---

## 2. Every `// WOLFGATE` edit, with its reason

### `WoundDamageComponents.cs`

| Site | Edit | Reason |
|---|---|---|
| `:6` | `using Content.Shared._Onyx.Targeting;` → `using Content.Shared._Shitmed.Targeting;` | D10 skips Onyx's Targeting stack |
| `:20-21` | `[BodyPartType.Chest] = 2.5f` + `[BodyPartType.Groin] = 1.5f` → one `[BodyPartType.Torso] = 4f` row | D9; keeps Onyx's 4/13 torso share of random routing |
| `:47` | `SystemicPainTarget = TargetBodyPart.Chest` → `.Torso` | D9 |
| `:53` | `[BodyPartType.Groin] = 160` deleted | D9; Wolfgate's `BodyPartType` has no `Groin` |

`LocalizedDamageTypes` (`:34-43`) is **verbatim, `Caustic` included** (D20 reversed).

### `WoundSystem.cs`

| Site | Edit | Reason |
|---|---|---|
| `:34` | `SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);` deleted | D17 — `_Mono/Body/Systems/BodyRejuvenateSystem.cs:28` owns that pair; re-registering is a server-start crash |
| `:122` | `private void OnRejuvenate(Entity<BodyComponent>, ref RejuvenateEvent)` → `public void ClearBodyWounds(EntityUid body)` | D17 — handler body is now called from `WoundDamageProjectionSystem.OnRejuvenate` |

### `BodyPartFunctionalitySystem.cs`

| Site | Edit | Reason |
|---|---|---|
| `:5` | `using Content.Shared._Onyx.Cybernetics;` → `using Content.Shared._Shitmed.Cybernetics;` | Wolfgate keeps `CyberneticsComponent` under `_Shitmed` |

### `WoundFractureSystem.cs`

| Site | Edit | Reason |
|---|---|---|
| usings | added `Content.Shared.Damage` | Wolfgate declares **both** `DamageableComponent` and `DamageableSystem` in `Content.Shared.Damage`, not `.Components`/`.Systems` — two `CS0246` without it |
| usings | added `Content.Shared._WF.Wolfmed.Body`, `Content.Shared._WF.Wolfmed.Compat` | for `WolfmedBodyPartSystem` and the facade |
| `:14` | `[Dependency] DamageableSystem _damage` → `WolfmedDamageableSystem _damage` | D12 |
| `:17` | added `[Dependency] WolfmedBodyPartSystem _wfPart` | D8 |
| `:146` | `bodyPart.FractureProfile` → `_wfPart.Get(part).FractureProfile` | D8 |

### `WoundDamageRoutingSystem.cs`

| Site | Edit | Reason |
|---|---|---|
| `:3` | `using Content.Shared.Bed.Components;` → `_WF.Wolfmed.Body` + `.Compat` + `.Targeting` | `HealOnBuckleComponent` is server-only here; the three `_WF` namespaces supply its replacements |
| usings | added `Content.Shared._Shitmed.Targeting` | D10 — `TargetBodyPart`, `SharedTargetingSystem.IsSelectable` |
| usings | added `Content.Shared._Mono.ArmorPlate` | for the `before: [typeof(SharedArmorPlateSystem)]` ordering |
| `:26` | `DamageableSystem _damage` → `WolfmedDamageableSystem _damage` | D12 |
| `:33` | `TargetResolverSystem _targetResolver` → `WoundTargetResolver _targetResolver`; added `WolfmedBodyPartSystem _wfPart` | D10, D8 |
| `:50`, `:52` | `before: [typeof(SharedArmorPlateSystem)]` on **both** `BeforeDamageChangedEvent` subscriptions | §8.3 trap 4 — `SharedArmorPlateSystem.OnBeforeDamageChanged` mutates `args.Damage` in place and would absorb twice per hit. RT requires identical before/after sets when one system takes the same event twice (§5.4) |
| `:57` | `args.Cancelled` added to the early-return | Godmode/stasis already refused the hit; ordering against them is impossible from shared code (both concrete systems are abstract) |
| `:60` | Shitmed `targetPart` handoff added after `args.Cancelled = true` | Wolfgate's `BeforeDamageChangedEvent` carries `TargetPart?`; gated on `SharedTargetingSystem.IsSelectable` so composite masks fall through to Onyx's own resolution chain |
| `:481` | `TargetBodyPart.Chest` → `.Torso` | D9 |
| `:613` | `TargetBodyPart.Chest` → `.Torso` (defibrillator branch) | D9 |
| `:548-558` | hands block rewritten | Wolfgate's `GetActiveHand` returns `Hand` (a class), not `string?`, and `HandLocation` has no `Functional*` members. Exact replacement from `wounds-a.md` §1.3-C |
| `:730`, `:735` | `bodyPart.MaxDamage` → `_wfPart.Get(part).MaxDamage` | D8 |
| `:883-885` | `BodyPartType.Chest` → `.Torso`; `part.Parent == null` → `_body.GetParentPartOrNull(parts[i]) is null`; `part.AmputationThresholds` → `_wfPart.Get(parts[i]).AmputationThresholds` | D9, Shitmed has no `Parent` field, D8 |
| `:952` | `HasComp<HealOnBuckleComponent>` → `HasComp<WolfmedBedHealMarkerComponent>` | §2.10 |

### `WoundDamageProjectionSystem.cs`

| Site | Edit | Reason |
|---|---|---|
| usings | added `Content.Shared._WF.Wolfmed.Compat` | facade |
| `:19` | `DamageableSystem _damage` → `WolfmedDamageableSystem _damage` | D12 |
| `:22` | `[Dependency] CirculatoryStreamSystem _circulation` deleted; `[Dependency] WoundSystem _wounds` added | D15; D17 |
| `:29` | `after: [typeof(InitialBodySystem)]` → `after: [typeof(SharedBodySystem)]` | `InitialBodySystem` is Nubody glue that does not exist here |
| `:31` | `<WoundableComponent, DamageDealtEvent>(…, after: [typeof(DamageableSystem)])` → `<WoundableComponent, DamageChangedEvent>` | D11 — Wolfgate writes damage inline, so one raise point cannot serve both orderings |
| `:37` | `_circulation.SynchronizeStreams(body)` deleted | D15 — `Organic` **is** the primary stream, so this never attaches anything in phase 1 |
| `:42` | `_wounds.ClearBodyWounds(body);` added before the `try` | D17 |
| `:82-87` | handler signature `ref DamageDealtEvent args` → `ref DamageChangedEvent args`, `args.Damage` → `args.DamageDelta is not { } delta` | D11 |
| `:115` | `CompOrNull<BodyPartComponent>(root)?.Parent` → `_body.GetParentPartOrNull(root)` | Shitmed has no `Parent` field |
| `:215-220` | whole `EnsureComp<InjurableComponent>` block deleted | D19 — Wolfgate parts already carry `Damageable(damageContainer: OrganicPart)` statically |
| `:239-240` | `case (BodyPartType.Chest, _)` → `(BodyPartType.Torso, _)`; `Groin` case deleted | D9 |

### `_Onyx/Mobs/Systems/MobThresholdSystem.cs`

| Site | Edit | Reason |
|---|---|---|
| usings | added `Content.Shared.Body.Components` | Wolfgate keeps `BodyComponent` there, not in bare `Content.Shared.Body` |
| usings | added `Content.Shared._WF.Wolfmed.Compat` | facade |
| `:15` | **added** `[Dependency] WolfmedDamageableSystem _damageable` | M6 — there is no dependency to swap; the vendored partial uses `_damageable` expecting it on Onyx's main class, and Wolfgate's class declares only `_mobStateSystem`/`_alerts` |
| `:31-34` | `criticalParts` `{ Head, Chest, Groin }` → `{ Head, Torso }` (+ doc comment) | D9 |

### `Content.Server/Chat/Systems/ChatSystem.Emote.cs` (HOOK 6)

`:60` `public void TryEmoteWithChat(` → `public override void TryEmoteWithChat(`. Return type unchanged;
`:85` (the `EmotePrototype` overload) untouched. Without this the WP2 shared virtual is a silent no-op on
the server too and `PainSystem.cs:297` never screams.

---

## 3. Deviations from PLAN.md, with justification

### D-WP4-0 — the manifest was truncated and rebuilt (process incident, no code impact)

A manifest-editing script opened `Docs/Wolfmed/WOLFMED_MANIFEST.md` with the default `cp1252` codec and
threw `UnicodeEncodeError` on a `→` **after** truncating the file. The file is untracked (WP1 created it)
and is not in `C:/tmp/wolfmed-plan/snapshots/`, so it could not be restored. It was **rebuilt** from
PLAN.md §7.2's canonical row list plus the Deviations sections of `WP1-report.md`, `WP2-report.md` and
`WP3-report.md`, and a banner note records this at the top of the file. The row set and every recorded
deviation are believed complete; wording in the WP1–WP3 sections may differ from the original. The file is
now 21.6 KB (was 18.1 KB) and LF-only, matching its previous newline mode.
**Recommendation for later WPs: write the manifest with an explicit `encoding='utf-8'`, and prefer plain
ASCII arrows.**

### D-WP4-1 — `WoundSystem.cs` does not gain `using Content.Shared.Body.Components;`

PLAN WP4 #5 asks for it so `BodyComponent` resolves. D17 deletes the file's only two `BodyComponent`
references (`:34` and `:122`), so the using would be dead on arrival. §2.17's rule — do not manufacture
spurious diffs in the vendored tree — is applied in the other direction: Onyx's existing dead
`using Content.Shared.Body;` at `:2` is kept verbatim and nothing is added. Zero build impact.

### D-WP4-2 — the projection's D11 handler takes `ref DamageChangedEvent`, not a by-value parameter

PLAN WP5's edit table specifies `handler signature … → DamageChangedEvent args`. That is a `CS1503`:
Wolfgate's `DamageChangedEvent` is a `sealed class : EntityEventArgs`, but RT's overload resolution binds
`SubscribeLocalEvent<TComp, DamageChangedEvent>(handler)` to `EntityEventRefHandler<,>`, and every Wolfgate
subscriber of that event already uses `ref` (`_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:214`).
Minimal fix: `ref DamageChangedEvent args`.

### D-WP4-3 — `WoundDamageProjectionSystem` gains a `WoundSystem` dependency

D17 requires `OnRejuvenate` to call `WoundSystem.ClearBodyWounds` first. Onyx's projection file had no
`WoundSystem` dependency because the subscription lived on `WoundSystem` itself. The added `[Dependency]`
line replaces the deleted `CirculatoryStreamSystem` one, so the field count is unchanged.

### D-WP4-4 — routing's `AccumulateAmputationOverflow` keeps a now-unused `bodyPart` local

Only the two `bodyPart.MaxDamage` reads moved to `_wfPart.Get(part)`; the
`TryComp(part, out BodyPartComponent? bodyPart)` guard stays because it still means "this is a body part"
and deleting it would change behaviour for non-part entities. It compiles without warning.

### D-WP4-5 — `WoundTargetResolver` drops Onyx's anatomical-odds scatter

PLAN §2.13 resolves this explicitly ("phase 1 resolves to the exact requested part with no anatomical-odds
scatter"). Implemented as specified. **Consequence to record:** the `TryResolve(body, requested, shooter,
out part)` overload ignores `shooter` entirely, and `CCVars.TargetingUseAnatomicalOdds` /
`CCVars.TargetingDownedTargetsAreExact` (vendored by WP3) are read by nothing in phase 1.
`CCVars.TargetingEnabled` **is** read, and gates only the wound path.

### D-WP4-6 — `WoundTargetResolver.TryFind` exempts `Torso` and `Head` from symmetry, not `Groin`

Onyx's exempted `Chest`, `Groin` and `Head`. `BodyPartType.Groin` does not exist here (D9), and
`TargetBodyPart.Groin` already folds to `(BodyPartType.Torso, BodyPartSymmetry.None)` through Shitmed's
`ConvertTargetBodyPart` (`SharedBodySystem.Targeting.cs:395`), so the exemption survives the fold. Behaviour
is equivalent.

### D-WP4-7 — routing's `OnBeforeDamageChanged` gained two things Onyx does not have

(a) `args.Cancelled` in the early-return guard and (b) the Shitmed `targetPart` handoff. Both are listed in
PLAN's **WP5** routing edit table, not WP4's; they are in-file `// WOLFGATE` edits and the brief assigns the
file to WP4, so they landed here. (c) — the D27 applied-delta write — is **not** done, because
`BeforeDamageChangedEvent.Applied` does not exist until GUARD D2 (WP5).

### D-WP4-8 — `_Onyx/Mobs/Systems/MobThresholdSystem.cs` landed early (PLAN WP5 #3)

`WoundDamageRoutingSystem.cs:466` calls `_mobThreshold.CheckVitalDamage(body, damageable)`; the file does
not compile without the partial. Delivered with all four of PLAN WP5 #3's edits. WP5 should treat this row
as done.

### Non-deviations worth recording

- All six "verbatim" files are `cmp`-identical to the pin. No dead `using` was cleaned (§2.17 respected in
  `WoundPrototype.cs:3`, `WoundStatusEffectSystem.cs:1`, `BodyPartFunctionalitySystem.cs:1`,
  `WoundSystem.cs:2`, `WoundDamageProjectionSystem.cs:2`, and the now-dead
  `using Content.Shared._Onyx.Chemistry.Circulation;` left in the projection after D15).
- `WolfmedBodyPartComponent` is **not** `[NetworkedComponent]`, per §2.12.
- Line endings: every new and vendored file is CRLF (matching the working tree and the Onyx sources);
  `WOLFMED_MANIFEST.md` is LF, as it was before.
- No `[Dependency] DamageableSystem` remains in any vendored `_Onyx` file (verified by grep).

---

## 4. Build checkpoint — exact tails

### `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`

```
C:\...\RobustToolbox\Robust.Shared\Robust.Shared.csproj : warning NU1510: PackageReference System.Reflection.Metadata will not be pruned. ...
C:\...\Content.Server.Database\Content.Server.Database.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 9.0.0 has a known high severity vulnerability, ...
    22 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.86
```

Filtered form required by the checkpoint:

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

### `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`

```
C:\...\RobustToolbox\Robust.Shared\Robust.Shared.csproj : warning NU1510: PackageReference System.Reflection.Metadata will not be pruned. ...
C:\...\RobustToolbox\Robust.Client\Robust.Client.csproj : warning NU1510: PackageReference System.Text.Json will not be pruned. ...
    6 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.05
```

Filtered form:

```
$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

No new warning originates in any WP4 file.

### Headless server smoke test

`bin/Content.Server/Content.Server.exe --cvar net.port=1219 --cvar status.enabled=false`, 180 s:

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1219: True"
```

`grep -icE "\[ERRO\]|Duplicate Subscription|unhandled|exception"` over the full log: **0**. 13 `[WARN]`
lines, all pre-existing. The `wound` / `bodyPartProfile` / `fractureProfile` prototype kinds register with
no YAML and produce no `PrototypeManager` errors, which is PLAN's WP4 checkpoint.

---

## 5. What later WPs must know

### New public symbols

| Symbol | Namespace | File |
|---|---|---|
| `WoundSystem.ClearBodyWounds(EntityUid body)` | `Content.Shared._Onyx.Wounds` | `WoundSystem.cs` (D17; the only caller is the projection's `OnRejuvenate`) |
| `WolfmedBodyPartComponent` (registers as `WolfmedBodyPart`) — `FractureProfile`, `MaxDamage`, `AmputationThresholds`, `DismembermentFinishingDamage`, `AmputationConsequenceSeverity`, `DismembermentSeverity` | `Content.Shared._WF.Wolfmed.Body` | `WolfmedBodyPartComponent.cs` |
| `WolfmedBodyPartSystem.Get(EntityUid part) → WolfmedBodyPartComponent` (never null) | `Content.Shared._WF.Wolfmed.Body` | `WolfmedBodyPartSystem.cs` |
| `WoundTargetResolver.{TryResolve(EntityUid, EntityUid, out EntityUid), TryResolve(EntityUid, TargetBodyPart, EntityUid?, out EntityUid), TryResolveAvailable, TryResolveExact, IsAvailable, GetMatchingParts}` | `Content.Shared._WF.Wolfmed.Targeting` | `WoundTargetResolver.cs` |
| `MobThresholdSystem.CheckVitalDamage(EntityUid target, DamageableComponent)` | `Content.Shared.Mobs.Systems` | `_Onyx/Mobs/Systems/MobThresholdSystem.cs` — **HOOK 11 (WP8) and HOOK 12 (WP8) can be written against this today** |
| All 14 wound components + 4 enums + `WoundState`/`FractureGrade`/`FractureTreatment`/`BleedingTreatment` | `Content.Shared._Onyx.Wounds` | `WoundDamageComponents.cs` |
| `wound` / `bodyPartProfile` / `fractureProfile` prototype kinds | `Content.Shared._Onyx.Wounds` | `WoundPrototype.cs` |

### Directed subscriptions WP4 registered

| Component | Event | Registrant |
|---|---|---|
| `WoundableComponent` | `ComponentInit`, `RejuvenateEvent` | `WoundSystem` |
| `WoundableComponent` | `BeforeDamageChangedEvent` (ordered `before: SharedArmorPlateSystem`) | `WoundDamageRoutingSystem` |
| `WoundableComponent` | **`DamageChangedEvent`** | `WoundDamageProjectionSystem` (D11) — **claimed exclusively; nothing else may take this pair** |
| `WoundableComponent` | `WoundCreatedEvent`/`WoundChangedEvent`/`WoundStateChangedEvent`/`WoundRemovedEvent` | `WoundStatusEffectSystem` |
| `WoundHostComponent` | `BeforeDamageChangedEvent` (ordered), `DamageDealtEvent` | `WoundDamageRoutingSystem` |
| `WoundHostComponent` | `MapInitEvent` (`after: SharedBodySystem`), `RejuvenateEvent` | `WoundDamageProjectionSystem` |
| `WoundComponent` | `WoundStateChangedEvent` | `WoundScarSystem` |
| `WoundScarComponent` | `WoundTreatmentAttemptEvent` | `WoundScarSystem` |
| `WoundFractureComponent` | `WoundChangedEvent` | `WoundFractureSystem` |
| `PainComponent` | `RejuvenateEvent`; `PainShockTargetComponent` | `ComponentStartup` | `PainSystem` |

All verified free at server start (no `Duplicate Subscriptions` throw).

### Open TODOs handed forward

1. **WP5 owns GUARD D + GUARD D2** (`Content.Shared/Damage/Systems/DamageableSystem.cs`) and GUARDs A/B/C
   (`_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`). Neither file was opened.
   **Ordering hazard:** routing already cancels `BeforeDamageChangedEvent` for any `WoundHostComponent`
   entity. Nothing has that component until WP7, so the tree is inert — but if WP7 landed before WP5,
   every organic humanoid would become invulnerable (hit cancelled, nothing re-applies it to a part).
2. **WP5 owes the D27 applied-delta accumulator inside `WoundDamageRoutingSystem`.** The three write
   points named by PLAN (`ApplySystemicDamage`'s `applied`, `RouteAppliedDamage`'s `out var appliedDamage`,
   `ApplyLocalizedHealing`'s `appliedDamage`) are unmodified Onyx code today. `OnBeforeDamageChanged` has
   an obvious place to write `args.Applied` once GUARD D2 adds the member — right before each `return`.
3. **WP8 owes the D23 AP side table** in the same file, read at `RouteThroughBodyModifiers`' `ChangeDamage`
   call. The facade already accepts `armorPenetration`, `tool` and `originFlag`.
4. **WP5's `WolfmedBodyPartLifecycleSystem` is still missing** (D28, §8.3 trap 2).
   `WoundDamageProjectionSystem.OnPartInserted` (`:95`) and `OnPartRemoved` (`:105`) have **no caller** in
   the tree right now.
5. **WP5 #3 is done** — do not re-vendor `_Onyx/Mobs/Systems/MobThresholdSystem.cs`.
6. **WP6's `OrganDamageSystem` still owes D26** — disable **both** `:24` (`[Dependency] AmputationSystem`)
   and `:36` (the call). WP4 did not touch that file.
7. **WP7's `parts.yml` must cover all 13 limb abstracts** with a `WolfmedBodyPart` row.
   `WolfmedBodyPartComponent.MaxDamage` defaults to zero and zero disables amputation overflow entirely
   (§2.12, acceptance check m6).
8. **`Content.Shared._Onyx.Targeting` and `Content.Shared._Shitmed.Targeting` are both imported by
   `WoundDamageRoutingSystem.cs` and do not collide** — the `_Onyx` one only holds `DamageDistribution`,
   `TargetingSnapshotComponent` and `TargetingSnapshotSystem` after D10. If a later WP vendors anything
   else into `_Onyx/Targeting`, check for `CS0104` there first.
9. **The RA0002 `[Access]` analyzer** (WP2's D-WP2-2) did not bite WP4 — no vendored file writes another
   entity's restricted component field directly. It will bite WP6's organ-health work if it does.
