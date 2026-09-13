# WP10-1 — Fractures (PLAN2 §4, parallel group F1)

**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG), branch `clanker/wolfmed-port-orchestration-454c3d`, uncommitted.
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`, read via `git -C C:/tmp/onyx show HEAD:<path>`.
**Result:** Server, Client and IntegrationTests all build with 0 errors; headless server starts clean (0 `[ERRO]`/`[FATL]`/`Exception`); `WoundFractureTest` 2/2 pass; `SpawnAndDeleteAllEntities*` + `PrototypeSaveTest` 4/4 pass.

---

## 1. Files created / modified

| # | Path | Status | Notes |
|---|---|---|---|
| 1 | `WG/Content.Shared/_Onyx/Wounds/FractureAlertSystem.cs` | **new (vendored, modified)** | Onyx bytes copied verbatim, 3 `// WOLFGATE` edits (D8). No licence header in Onyx — none invented. 49 lines. |
| 2 | `WG/Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs` | **new (vendored, modified)** | Onyx bytes copied verbatim, 1 `// WOLFGATE` edit (P2-D2: `TryGetUsedHandSymmetry` body). 207 lines. |
| 3 | `WG/Content.Shared/_WF/Wolfmed/DoAfter/WolfmedFractureDoAfterSystem.cs` | **new (`_WF`)** | PLAN2 §2.2 verbatim. New directory `_WF/Wolfmed/DoAfter/`. |
| 4 | `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml` | **modified** | DECISIONS §8.2-1: four `manipulationModifier` values + a 4-line `# WOLFGATE` balance comment. |
| 5 | `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs` | **modified** | `[TestPrototypes]` block only (T-FIXTURE / P2-D21). No test method touched; the WP10-6b skip marker left in place. |
| 6 | `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | Rows + a `### WP10-1` deviations subsection appended directly (per the orchestrator's override of PLAN2's `manifest-rows-WP10-N.md` indirection). |

No upstream (non-`_Onyx`, non-`_WF`) C# file was touched. `SharedDoAfterSystem.cs`, `DoAfterDelayMultiplierSystem.cs`, `SharedHandsSystem.cs`, `Content.Shared/Movement/**` — all untouched, as PLAN2 §3 requires.

**Prototypes / locale / textures created: zero.** Confirmed already shipped by WP7 before copying anything:
- `BrokenBones` alert — `Resources/Prototypes/_Onyx/Alerts/alerts.yml:16` (with the WP7 `# WOLFGATE` note at `:14`).
- `Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{brokenbones.png,meta.json}`.
- `Resources/Locale/en-US/_Onyx/medical/fractures.ftl` — both keys (`alerts-broken-bones-name`, `alerts-broken-bones-desc`).
- `OrganicFractureProfile` with `alert:`/`alertMinimumGrade:`/`alertHiddenTreatments:` and all four grades.

---

## 2. Every `// WOLFGATE` edit, with reason

### `FractureAlertSystem.cs` — 3 edits, all D8

1. `using Content.Shared._WF.Wolfmed.Body; // WOLFGATE: D8 keeps Onyx's part fields on WolfmedBodyPartComponent.`
2. `[Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8, Onyx's extra part fields.`
3. In `Refresh`'s first loop, a comment line + the profile read:
   ```csharp
   // WOLFGATE: D8, FractureProfile lives on WolfmedBodyPartComponent, not Shitmed's BodyPartComponent.
   if (_wfPart.Get(part).FractureProfile is not { } profileId ||
   ```
   `bodyPart` is now an unused deconstruction variable — **left as-is** per PLAN2, matching `WoundFractureSystem.cs:148`'s precedent (not a warning).

`using Content.Shared.Body;` kept verbatim (P2-D3 — `OnyxBodyEvents.cs` declares `namespace Content.Shared.Body;`). No `Initialize`, no subscriptions; the second loop (clear any alert no live profile claims) is verbatim.

### `FractureEffectsSystem.cs` — 1 edit (P2-D2)

`TryGetUsedHandSymmetry` replaced with PLAN2 §4's body verbatim, preceded by the 3-line `// WOLFGATE` comment. Verified against the tree today:
- `SharedHandsSystem.cs:177` `public Hand? GetActiveHand(Entity<HandsComponent?> entity)`
- `SharedHandsSystem.cs:285` `public bool IsHolding(EntityUid uid, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out Hand? inHand, HandsComponent? handsComp = null)` — the 4-arg call binds here unambiguously; the 2-arg `:280` overload is not applicable.
- `HandsComponent.cs:107` `public sealed class Hand` (a class, so no `.Value`); `:156-161` `enum HandLocation : byte { Left, Middle, Right }` (no `Functional*`).

Everything else in the file is Onyx-verbatim, including:
- `[Dependency] private WoundStatusEffectSystem _statusEffects = default!;` and both call sites (`HandlePartInserted`, `HandlePartRemoved`) — both exist in WG (`WoundStatusEffectSystem.cs:119` and `:109`), no shim needed.
- `RefreshTransferredPart` kept uncalled (P2-D18).
- The movement wiring: `Refresh(EntityUid?)` → `_movement.RefreshMovementSpeedModifiers(uid)`, and `OnRefreshSpeed` → `args.ModifySpeed(1f - (1f - modifier) * partScale * treatmentScale)` (`MovementSpeedModifierSystem.cs:80` and the single-arg `ModifySpeed` at `:177` both present).
- The class name stays `FractureEffectSystem` while the file stays `FractureEffectsSystem.cs` — Onyx's own mismatch, kept.

### `WolfmedFractureDoAfterSystem.cs` (new `_WF`)

PLAN2 §2.2 verbatim: subscribes `<WoundHostComponent, GetDoAfterDelayMultiplierEvent>` with a **`ref`** handler (matching the only other subscriber, `DoAfterDelayMultiplierSystem.cs:27`, even though the event is a class), and does `args.Multiplier *= _fractureEffects.GetDurationMultiplier(ent.Owner);`. No `before:`/`after:` edge. `_WF` style honoured: no licence header, `/// <summary>` one-liners, `[Dependency] private X _x = default!;` without `readonly`.

### `wounds.yml` — `# WOLFGATE` balance block (DECISIONS §8.2-1)

```yaml
  # WOLFGATE (DECISIONS §8.2-1): Onyx's manipulationModifier values are all below 1, but the formula is
  # `multiplier *= 1 + (modifier - 1) * partScale * treatmentScale`, so below 1 means FASTER — a shattered arm
  # made every do-after 25% quicker. Restored to the C# defaults in WoundPrototype.cs (1.1/1.25/1.5/2.0) so a
  # fractured arm slows hand work. movementModifier is untouched (correctly below 1, and read that way).
```
Values: Hairline `0.92 → 1.1`, Simple `0.84 → 1.25`, Displaced `0.75 → 1.5`, Comminuted `0.75 → 2.0`. `movementModifier`, `threshold` and `creationChance` untouched.

### `WoundFractureTest.cs` — `[TestPrototypes]` only (T-FIXTURE / P2-D21)

- `WoundFractureBodyGraph`: `left arm` gains `connections: [left hand]`; new `left hand: { part: LeftHandHuman }`.
- `WoundFractureBody`: `- type: Hands` added (kept `parent: InventoryBase`, `Damageable`, `MovementSpeedModifier`, `WoundHost`).
- New `WoundFractureHandsBodyGraph` + `WoundFractureHandsBody` (torso, both arms, both hands, `Hands`, `Damageable`, `MovementSpeedModifier`, `WoundHost`) for T-FRACT-HANDS's `used`-item branch.
- An 8-line `// WOLFGATE (P2-D21, WP10-1)` comment above the block explains why exactly one hand sits on `WoundFractureBody`.
- `LeftHandHuman`/`RightArmHuman`/`RightHandHuman` verified at `Resources/Prototypes/Body/Parts/human.yml:72,63,83`. Slot naming matches `Resources/Prototypes/Body/Prototypes/human.yml`.

---

## 3. Deviations from PLAN2, with justification

1. **P2-D13 is overridden by DECISIONS §8.2-1.** PLAN2 §1.2 P2-D13 says ship the `manipulationModifier` values unchanged and escalate; DECISIONS.md's §8.2 answer (the binding one) says FIX. Implemented as FIX. **This invalidates PLAN2 P2-D16's and §6.2 T-FRACT-EFFECTS's manipulation prediction of `0.75`** — see §5.
2. **Manifest indirection skipped, per the orchestrator's instruction.** PLAN2 §4 serialisation rule 2 sends each F1 package to `C:/tmp/wolfmed-plan/p2/manifest-rows-WP10-N.md`; the orchestrator overrode that because phase-2 packages run sequentially. Rows and deviations were appended directly to `Docs/Wolfmed/WOLFMED_MANIFEST.md`. No `manifest-rows-WP10-1.md` was written.
3. **`WoundFractureHandsBody` has no `parent:`.** PLAN2 §6.2 T-FIXTURE lists its components without a parent and T-FRACT-HANDS needs no inventory, so it is a bare entity. `SpawnAndDeleteAllEntities*` and `PrototypeSaveTest` pass with it in the pool.
4. **PLAN2's WP10-1 file table lists 3 files; 6 were touched.** The extra three are the T-FIXTURE prototype block (explicitly assigned to WP10-1 by serialisation rule 0), the §8.2-1 YAML, and the manifest. No scope beyond what the task assigned.
5. **No `manifest-rows` / no commit / no snapshot patch.** Tree left uncommitted as required.

Nothing outside PLAN2 §3's authorised list was edited; in fact WP10-1 needed **no** upstream hook at all.

### Duplicate-subscription audit actually performed (PLAN2 §5.1 rows 1-9)

Grepped WG (including the phase-1 `_Onyx`/`_WF` files) for every pair before subscribing. All nine free, and the headless server started with no `Duplicate Subscriptions` throw — the definitive check. `<EmoteOnDamageComponent, DamageChangedEvent>` was not touched (it is WP10-5's, via HOOK 18).

---

## 4. Build / test output

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Headless server (YAML touched), 120 s on port 1299 — `C:/tmp/wolfmed-plan/p2/wp/WP10-1-server.log`:
```
$ grep -cE "\[ERRO\]|\[FATL\]|Exception"  →  0
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
```
(The only `[WARN]`s are the pre-existing `PullingSystem` command-bind and duplicate-emote-word lines.)

Fracture tests — `C:/tmp/wolfmed-plan/p2/wp/WP10-1-tests.log`:
```
  Passed GradeBoundariesAreDeterministicTest [39 s]
  Passed PostArmorHitAndTreatmentPreconditionsTest [40 s]
Test Run Successful.  Total tests: 2  Passed: 2
```

Post-landing regression sweep PLAN2 §4 asks for — `C:/tmp/wolfmed-plan/p2/wp/WP10-1-entity-tests.log`:
```
  Passed SpawnAndDeleteAllEntitiesInTheSameSpot [1 m 27 s]
  Passed SpawnAndDeleteAllEntitiesOnDifferentMaps [1 m 55 s]
  Passed UninitializedSaveTest [20 s]
  Passed AllItemsHaveSpritesTest [2 s]
Test Run Successful.  Total tests: 4  Passed: 4
```
This is the `DebugAssertException` class WP9 fixed: `FractureEffectSystem.OnPartChanged` now reaches `EnsureComp<BodyPartFunctionalityComponent>` through `_functionality.Refresh`, driven by `WolfmedBodyPartLifecycleSystem`'s `OrganGot*` re-raises. Its `TerminatingOrDeleted` guards were **not** weakened and the sweep is green.

---

## 5. What later packages must know

1. **WP10-6b: PLAN2's manipulation literals are stale, in the other direction.** With 1.1/1.25/1.5/2.0 shipped, T-FRACT-EFFECTS's arm assertion is **`2.0`, not `0.75`**. Derivation: Comminuted left arm, `manipulationModifier 2.0`, `Arm` absent from `PartEffectScales` → scale `1f`, `TreatmentEffectScales[None] = 1` → `1 + (2.0 - 1) * 1 * 1 = 2.0`; the undamaged left hand falls through to `BodyPartFunctionalitySystem.GetState` = `Functional` → `1 + (1 - 1) * 0.75 * 1 = 1`; product **2.0**. After `TryReduce` (`Reduced` scale 0.25) it would be `1 + 1 * 1 * 0.25 = 1.25`; after `TryMend` `removeWoundWhenMended: true` removes the wound → **1.0**. **Every number is a prediction until measured (P2-D16 / P2-4) — measure it.** The movement half is unaffected: `movementModifier` was not changed, so **walk 0.5** after a Comminuted leg still stands.
2. **The fixture is ready and its ids are taken.** `WoundFractureBody` (one left hand, active), `WoundFractureHandsBody` (left+right), `WoundFractureBodyGraph`, `WoundFractureHandsBodyGraph`, `WoundFractureArmor`. Do not add a right hand to `WoundFractureBody` — `AddHand` makes the first-attached hand active and T-FRACT-EFFECTS calls `GetDurationMultiplier(body)` with `used: null`.
3. **P2-D23 still binds.** Create fractures with a ≥ 60 effective hit (`creationChance: 1`), then re-grade with `WoundSystem.ChangeSeverity`. The `creationChance` values were not changed.
4. **`FractureAlertSystem.Refresh` has no caller but `FractureEffectSystem`'s six handlers** — T-FRACT-ALERT/-NEG will only work with both systems present, which they now are.
5. **The wound→status-effect limb-attach path is now live for the first time** (`WoundStatusEffectSystem.HandlePartInserted`/`HandlePartRemoved` gained their first caller). Inert only because no wound prototype names a `StatusEffect` (P2-D1). Any package that adds one must re-check.
6. **The do-after bridge is in.** Every `MultiplyDelay = true` do-after on a wound host now consults `FractureEffectSystem.GetDurationMultiplier` with `used: null`. If a later package wants the `Used` item back, that is the escalated §8.2-2 two-line variant in `SharedDoAfterSystem.cs` — still not taken.
7. **WP10-7 (docs):** the manifest already carries WP10-1's rows (`:77` split into three, the `WoundFractureTest.cs` row, the `wounds.yml` row, a new `WolfmedFractureDoAfterSystem.cs` row) and a `### WP10-1` deviations subsection before `## Hazards`. Reconcile rather than re-add. `WOLFMED_PLAN.md`'s stale claim that `FractureEffectsSystem.cs:34-35` needs a `using` swap (P2-D3) is still unfixed — WP10-7 owns that correction.
8. **`Docs/Wolfmed/DECISIONS.md` was already modified in the tree when WP10-1 started** (not by this package). Left untouched.
