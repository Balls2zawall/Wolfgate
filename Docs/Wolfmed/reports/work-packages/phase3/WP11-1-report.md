# WP11-1 — Amputation (PLAN3 §4 / P3-1)

Executed in WG = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
(branch `clanker/wolfmed-port-orchestration-454c3d`, phase 2 committed `1171e02fb6`, WP11-0's two test files
already present). ONYX = `C:/tmp/onyx` @ `2f5bab9`. No commits made; nothing inside `WG/RobustToolbox` touched.

---

## 1. Files created / modified

| File | Status |
|---|---|
| `WG/Content.Shared/_Onyx/Wounds/AmputationSystem.cs` | **new (vendored from ONYX, adapted)** — 212 → 219 lines, 18 `// WOLFGATE` sites |
| `WG/Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` | **modified** — 2 sites, D26 lifted |
| `WG/Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | **modified** — P3-D1 `ChargeVitalPartLoss` (1 call, 1 method, 2 deps, 7 usings) |
| `WG/Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` | **modified** — DECISIONS §8.6-1 balance data |
| `WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | **modified** — 2 stale literals corrected for P3-D1 (see §3, deviation D-3) |
| `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 5 appended rows + a `### WP11-1` section |

Throwaway verification file `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedTempD2AmputationCheck.cs` was
created, run (both tests green), and **deleted**; the tree is clean of it (`git ls-files --others` confirms).

**No upstream (non-`_Onyx`, non-`_WF`) file was edited at all.** PLAN3 §3 authorises no upstream hook for
WP11-1 (HOOK 19 is withdrawn), and none was needed.

Snapshot: `C:/tmp/wolfmed-plan/p3/snapshots/WP11-1.patch` (351 lines) + `WP11-1.untracked.txt` +
`WP11-1.AmputationSystem.cs`.

---

## 2. Every WOLFGATE edit, with its reason

### 2.1 `AmputationSystem.cs` (vendored) — 18 sites

Byte-diffed against `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/AmputationSystem.cs`. Onyx's
namespace (`Content.Shared._Onyx.Wounds`) is kept; the file has no licence header in Onyx and none was
invented. Every line not listed is byte-identical. The result matches PLAN3 §4/WP11-1's 26-row edit table
exactly, one-for-one, with no extra edit.

| # | Site | Edit | Reason |
|---|---|---|---|
| 1 | after `using Content.Shared.Throwing;` | `+ using Content.Shared._WF.Wolfmed.Body;` / `+ using Content.Shared._WF.Wolfmed.Compat;` | D8 + D12/shim |
| 2 | `:19` | `DamageableSystem _damageable` → `WolfmedDamageableSystem _damageable` | D12 — `GetAllDamage` only exists on the facade |
| 3 | after `:23` | `+ WolfmedBodyPartSystem _wfPart`, `+ WolfmedBodySystem _wfBody` | D8, and Onyx's `SharedBodySystem.TryDetachPart` does not exist in WG |
| 4 | `:35` | `BodyPartType.Chest` → `BodyPartType.Torso` | D9 |
| 5 | `:36` | `bodyPart.MaxDamage` → `_wfPart.Get(part).MaxDamage` | D8 |
| 6 | `:39` | `TryExplosionAmputate(args.Body, part, bodyPart, args.Damage)` → drops `bodyPart` | P3-D15 |
| 7 | `:44` | `bodyPart.MaxDamage` → `_wfPart.Get(part).MaxDamage` | D8 |
| 8 | `:51` | `IsFinishingHit(args.Body, bodyPart, …)` → `(args.Body, part.Owner, …)` | P3-D15 |
| 9 | `:64` | `parentPart.AmputationConsequenceSeverity` → `_wfPart.Get(**parent**).AmputationConsequenceSeverity` | D8. **The severity comes off the parent, not the severed part** (PLAN3 §8.7 hazard 7). The `TryComp(parent, out BodyPartComponent? parentPart)` guard at `:58` is kept; `parentPart` is now an unused out-local, matching the `WoundFractureSystem.cs:148` precedent |
| 10 | `:73a` | `BodyPartType.Chest` → `BodyPartType.Torso` | D9 |
| 11 | `:73b` | `bodyPart.Parent == null` → `_body.GetParentPartOrNull(part) is null` | Shitmed's `BodyPartComponent` has no `Parent` field |
| 12 | `:74` | `bodyPart.AmputationThresholds` → `_wfPart.Get(part).AmputationThresholds` | D8 |
| 13 | `:79` | `TryExplosionAmputate(…, bodyPart, …)` → drops `bodyPart` | P3-D15 |
| 14 | `:84` | threshold read → `_wfPart.Get(part)` | D8 |
| 15 | `:91` | threshold read → `_wfPart.Get(part)` | D8 |
| 16 | `:103-104` | threshold read → `_wfPart.Get(part)`; `IsFinishingHit(… part.Owner …)` | D8 + P3-D15 |
| 17 | `:114` | `BodyPartType.Chest` → `BodyPartType.Torso` | D9 |
| 18 | `:117` | `bodyPart.Parent ?? part` → `_body.GetParentPartOrNull(part) ?? part` | as #11 |
| 19 | `:118` | `_body.TryDetachPart` → `_wfBody.TryDetachPart` | compat shim (`Compat/WolfmedBodySystem.cs:13`) |
| 20 | `:123` | `bodyPart.DismembermentSeverity` → `_wfPart.Get(part).DismembermentSeverity` | D8 |
| 21 | `:151` | `IsFinishingHit(EntityUid, BodyPartComponent, DamageSpecifier)` → `(EntityUid, EntityUid, DamageSpecifier)`, first statement `var wf = _wfPart.Get(part);` | P3-D15 |
| 22 | `:158` | `part.AmputationThresholds` → `wf.AmputationThresholds` | D8 |
| 23 | `:161-162` | `part.DismembermentFinishingDamage` → `wf.DismembermentFinishingDamage` | D8 |
| 24 | `:170-175` | `TryExplosionAmputate` drops its `BodyPartComponent bodyPart` parameter | P3-D15 |
| 25 | `:177` | `IsFinishingHit(body, bodyPart, hit)` → `(body, part.Owner, hit)` | P3-D15 |
| 26 | `:188` | `bodyPart.AmputationThresholds` → `_wfPart.Get(part.Owner).AmputationThresholds` | D8 |

Confirmed as predicted and therefore **not** edited:
* `:78` `_damageable.GetAllDamage((part.Owner, damageable))` and `:182` `_damageable.GetAllDamage(part.Owner)`
  both bind the facade unchanged (P3-D16).
* **No `DamageSpecifier.DamageDict` key-type edit is needed anywhere in this file.** WG's dict is
  `Dictionary<string, FixedPoint2>`; the four call shapes all resolve through `ProtoId<T>`'s implicit
  conversions. Zero `CS0411`/`CS0121`/`CS1503`. The WP10-2 trap does not recur.
* `_body` is still required after edit #19 (`GetParentPartOrNull`).
* `ThrowingSystem.TryThrow` is `void` in WG vs `bool` in Onyx; `:125` discards the result, so it compiles.

### 2.2 `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` — D26 lifted (2 sites)

```
-    // WOLFGATE: D26, AmputationSystem is phase 3 and is not ported; the field would be CS0246.
-    // TODO: phase 3 - [Dependency] private AmputationSystem _amputation = default!;
+    [Dependency] private AmputationSystem _amputation = default!; // WOLFGATE: D26 lifted in phase 3 (WP11-1); AmputationSystem is now vendored.
```
```
-        // WOLFGATE: D26, amputation is phase 3.
-        // TODO: phase 3 - _amputation.HandlePartDamageApplied(part, ref args);
+        _amputation.HandlePartDamageApplied(part, ref args); // WOLFGATE: D26 lifted in phase 3 (WP11-1); order wounds -> fractures -> amputation -> bleeding is load-bearing.
```
Fan-out order `_wounds → _fractures → _amputation → _bleeding` is preserved. No new `using` was needed (the
file's namespace is already `Content.Shared._Onyx.Wounds`, P3-D11).

### 2.3 `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` — P3-D1

`_WF` file, so no `// WOLFGATE` marker convention; `_WF` style followed (no licence header, one-line
`/// <summary>`, `[Dependency] private X _x = default!;` without `readonly`).

* 7 added `using`s (`System.Linq`, `_WF.Wolfmed.Compat`, `Damage`, `Damage.Prototypes`, `FixedPoint`,
  `Robust.Shared.Prototypes`).
* 2 new dependencies: `WolfmedDamageableSystem _damageable`, `IPrototypeManager _prototypes`.
* One call added to the **existing** `OnPartRemoved`, immediately after `_bleeding.OnPartChanged(body)`
  (i.e. after `_projection.OnPartRemoved`, per PLAN3 §8.7 hazard 16): `ChargeVitalPartLoss(body, args.Part);`
* One new private method, exactly PLAN3's body: `!IsVital || GetBodyChildrenOfType(...).Any()` → return;
  `lost = GetTotalDamage(part)`; `ChangeDamage(body, Bloodloss = lost)`.

**No new subscription** — it extends the body of the already-owned host-gated
`<WoundHostComponent, BodyPartRemovedEvent>` handler (`:31`). **No upstream file, no prototype edit** — HOOK 19
stays withdrawn.

Re-entrancy verified (this was the one non-obvious risk): the charge fires inside an open routed pass, so
`WoundDamageRoutingSystem.OnBeforeDamageChanged` early-returns on `_routing.Contains(ent)` **without
cancelling** — but `DamageableSystem`'s Wolfmed seam (`DamageableSystem.cs:255-264`) still raises
`DamageDealtEvent`, `OnDamageDealt` (`WoundDamageRoutingSystem.cs:126`) accepts it precisely *because*
`_routing` contains the body, and `RouteAppliedDamage`'s systemic/localized split (`:701-710`) sends
`Bloodloss` (absent from `LocalizedDamageTypes`) to `SystemicDamageComponent` via `ApplySystemicDamage`.
`MobThresholdSystem.CheckVitalDamage` sums attached Head+Torso part damage **plus** `SystemicDamageComponent`,
so the charge lands where P3-D1 needs it. Measured: 113, not 100 (see §5).

### 2.4 `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` — DECISIONS §8.6-1

A 14-line `# WOLFGATE (P3 balance, DECISIONS.md §8.6-1)` header block documents the mechanics in-file, and each
changed line carries `# WOLFGATE (P3 balance)`.

| Part | `amputationThresholds` (Slash / Piercing / Blunt unchanged) | **`Heat` added** | `dismembermentFinishingDamage` **added** |
|---|---|---|---|
| Head | 200 / 200 / 350 | **200** | `{Piercing: 12, Heat: 15}` |
| Arm L+R | 130 / 250 / 250 | **250** | same (YAML alias) |
| Hand L+R | 70 / 200 / 150 | **200** | same |
| Leg L+R | 150 / 250 / 300 | **250** | same |
| Foot L+R | 80 / 220 / 170 | **220** | same |
| Torso | (none; excluded from amputation by `PartType == Torso`) | — | — |

`Slash` and `Blunt` are deliberately **absent** from the per-part `dismembermentFinishingDamage` dict, so
`GetValueOrDefault(type, host.DefaultDismembermentFinishingDamage[type])` keeps falling back to Onyx's
**15 / 50**. Melee balance is therefore unchanged. Verified at runtime: the left hand reads
`thresholds Slash=70,Piercing=200,Blunt=150,Heat=200` and `finishing Piercing=12,Heat=15`.

### 2.5 `WoundDamageFoundationTest.cs` — two stale literals

`RoutesAndProjectsDamageTest` asserted `Bloodloss == 100` / body total `== 106` after de-heading. P3-D1 makes
those `113` / `119`. Both corrected with a `// WOLFGATE (WP11-1, PLAN3 P3-D1)` comment carrying the full
derivation. Ownership deviation, see §3 D-3.

---

## 3. Deviations from PLAN3

| # | Deviation | Justification |
|---|---|---|
| **D-1** | **`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` is edited**, which PLAN3 §3 lists under "Explicitly NOT touched" and P3-D13 declares complete, and the new numbers contradict **P3-D4** ("Onyx's amputation numbers ship unchanged; no gun can amputate"). | **DECISIONS.md §8.6-1 is the later, authoritative user decision and explicitly overrides PLAN3 here**: guns and lasers CAN sever, expressed *only* in Wolfgate's own part YAML, marked `# WOLFGATE (P3 balance)`. The task brief restates this as binding. PLAN3 §8.2's own closing paragraph names exactly these two knobs. |
| **D-2** | **T-AMP-NOGUN is dead.** PLAN3 §6.2 specifies `BulletsNeverAmputateTest` (20 × 14-Piercing into a hand never detaches). That test would now fail. | Superseded by DECISIONS §8.6-1, which replaces it with **T-AMP-GUN**. WP11-5 owns the test file; §5 below hands it the measured numbers. No test was written or deleted here. |
| **D-3** | **`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` was edited**, which PLAN3 §4 serialisation rule 2 assigns exclusively to WP11-3. | Unavoidable and non-negotiable: P3-D1 (a WP11-1 deliverable) is what changed the two numbers, so the red test is this package's regression. Leaving it red would have poisoned WP11-2's and WP11-3's own "suite still green" gates and mis-attributed the blame. The edit is 2 literals + a 6-line derivation comment inside one test method; WP11-3's armour work adds new `[TestPrototypes]` and new test methods and does not touch `RoutesAndProjectsDamageTest`. Flagged here and in the manifest. |
| **D-4** | **PLAN3 §4/WP11-1's build checkpoint says "this WP now changes **no** YAML"**; it changes one file. | Consequence of D-1. The Release YAML linter was *not* run separately; instead the full headless server start-up was run, which indexes and deserialises every prototype and produced **zero** `[ERRO]`/`[FATL]`/`Exception` lines, and the new fields were then read back at runtime through `WolfmedBodyPartSystem.Get` in a live integration check. That is a strictly stronger validation of these two datafields than the linter. |
| **D-5** | The D2 spot check PLAN3 asks for was done with a **throwaway integration test that was then deleted**, rather than left in the tree. | T-AMP-VITAL (both halves) is WP11-5's deliverable in WP11-5's own file (`WolfmedAmputationTest.cs`); shipping a duplicate here would have collided with serialisation rule 2 a second time. Result recorded in §5 and in the manifest. |

Everything else follows PLAN3 literally: file placement (P3-D11 — `Content.Shared`, not `Content.Server`),
the full 26-row edit table, P3-D12 (overflow branch ported verbatim even though it is dead on shipped limbs),
P3-D2 (`CanAttachPart` **not** hooked, consequence wound ships inert), P3-D3 (no `ExplosionSystem` hook, no
`_routedModifiers` origin-flag passthrough), P3-D24 (`OrganDamageSystem.cs` owned exclusively here),
P3-D1a (no refund on re-attachment).

**Subscriptions.** Exactly one pair registered: `<WoundableComponent, PartDamageOverflowedEvent>`
(`AmputationSystem.Initialize()`). Re-grepped across `Content.{Shared,Server,Client}` including all `_Onyx`
and `_WF` files before building: `PartDamageOverflowedEvent` appears only at its declaration
(`WoundEvents.cs:62`) and its raise (`WoundDamageRoutingSystem.cs:761`). Matches PLAN3 §5.1 row 1. The server
started with no `Duplicate Subscriptions` throw. `AmputationSystem.HandlePartDamageApplied` is a plain public
method called from `OrganDamageSystem.cs:38`, **not** a subscription (§5.2).

---

## 4. Mechanics — exactly how the threshold comparison works (for WP11-5's T-AMP-GUN)

Read out of the vendored `AmputationSystem.cs` and verified at runtime. **This is the section WP11-5 needs.**

**The damage that is compared** is the part's own `DamageableComponent.Damage`, fetched as
`WolfmedDamageableSystem.GetAllDamage(part)` (`AmputationSystem:78`). It is **not** wound severity, and it is
**not** a single scalar total.

**Progress is a sum of per-type ratios over the part's own threshold dict** (`GetThresholdProgress:137-149`):

```
progress = Σ  over every (type, T) in WolfmedBodyPartComponent.AmputationThresholds, T > 0
               accumulatedPartDamage[type] / T
```

Consequences that matter:
* A damage type **absent from that dict contributes nothing** to `progress`, and `IsFinishingHit:158` skips it
  outright — so before this WP, `Heat` could never sever anything at any volume. Adding the `Heat` row is
  precisely what makes lasers able to sever.
* Different types **stack**: 100 Slash on a 200-Slash / 200-Heat head plus 100 Heat is `0.5 + 0.5 = 1.0` and
  makes the head severable. This is Onyx's existing behaviour for Slash+Piercing+Blunt, now with a 4th term.
* The comparison is **per accumulated damage**, so healing the part lowers `progress` again.

**The state machine** (`HandlePartDamageApplied:72-106`), all evaluated *after* the hit has been applied to the
part:
1. Part not `Severable` and `progress ≥ 1` → `Severable = true`, **this hit does not detach**.
2. Part `Severable` and `progress < SeverableResetRatio` (host default **0.8**) → `Severable = false`, return.
3. Part `Severable`, `progress ≥ 0.8`, **pre-hit** progress (`damage − this hit`) `≥ 1`, **and**
   `IsFinishingHit` → `TryAmputate`.

**`IsFinishingHit(body, part, hit)`** (`:151-168`) iterates **this hit's** dict, not the accumulation. For each
`(type, amount)` with `amount > 0` that is a key of the part's `AmputationThresholds`:
`minimum = part.DismembermentFinishingDamage.GetValueOrDefault(type, host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type))`;
returns true if `minimum > 0 && amount ≥ minimum`.

**Does Heat on a part count toward a Heat threshold, given that Heat also makes `BurnWound`s?** Yes, directly,
and **no mapping is required**. Traced:
`Heat ∈ WoundHostComponent.LocalizedDamageTypes` (`WoundDamageComponents.cs:35-44`) → survives
`FilterPartDamage` because `OrganicBodyPartProfile.acceptedDamageTypes` lists `Heat`
(`_Onyx/Wounds/wounds.yml:6-13`) → `_damage.TryChangeDamage(target, localized, ignoreResistances: true, …)`
(`WoundDamageRoutingSystem.cs:772`) writes `Heat: n` straight onto the part's `DamageableComponent`, whose
container is `OrganicPart` = `{Brute, Burn}` and `Burn` = `{Heat, Shock, Cold, Caustic}`
(`Damage/containers.yml:78-82`, `Damage/groups.yml:10-16`). The `BurnWound` created by
`WoundSystem.HandlePartDamageApplied` is a **parallel record** that `GetThresholdProgress` never reads.

**Measured hit counts** (throwaway integration test on a real `MobHuman`, since deleted):

| Attack | Target | Result |
|---|---|---|
| 14 Piercing (`BaseBullet`) | left hand, Piercing T = 200, finishing min **12** | **severed on hit 16** (hits 1-14 accumulate to 196; hit 15 reaches 210 and only sets `Severable`; hit 16's pre-hit progress is 210/200 = 1.05 and 14 ≥ 12 → detach) |
| 16 Heat (`BulletLaser`) | right hand, Heat T = 200, finishing min **15** | **severed on hit 14** (hit 13 reaches 208 and sets `Severable`; hit 14 finishes, 16 ≥ 15) |
| 5 × 14 Piercing | left foot, Piercing T = 220 | **still attached, `Severable == false`** (70/220 = 0.32) |

Formula for a uniform single-type stream of `d` against threshold `T` with `d ≥ finishingMinimum`:
**`ceil(T/d) + 1` hits**.

---

## 5. Shitmed detach cascade — hop-by-hop, no double-apply

Every hop re-read in the tree today.

1. `WoundDamageRoutingSystem.RouteAppliedDamage:772` applies the localized damage to the **part** via
   `TryChangeDamage(target, localized, …)`.
2. That raises `DamageChangedEvent` on the part →
   `_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:223 OnDamageChanged`. **GUARD B at `:234`**
   (`&& !_queryWoundHost.HasComp(partEnt.Comp.Body)`) is false for a wound host, so `severed` stays false and
   the `DropPart(partEnt)` at `:246` **never runs**. Shitmed's sever-at-`SeverIntegrity` and Onyx's amputation
   therefore cannot both fire. ✔
3. Routing raises `PartDamageAppliedEvent` (`:782-786`) → `OrganDamageSystem.cs:34` fans out
   `_wounds → _fractures → _amputation → _bleeding`.
4. `AmputationSystem.HandlePartDamageApplied` → `TryAmputate(body, part)`:
   a. `parent = _body.GetParentPartOrNull(part) ?? part`, captured **before** the detach.
   b. `_wfBody.TryDetachPart(part)` (`Compat/WolfmedBodySystem.cs:13`) raises `AmputateAttemptEvent` →
      `SharedBodySystem.Parts.cs:219 OnAmputateAttempt` → `DropPart(:201)`.
   c. `DropPart` drops held items (`DropSlotContents`), raises `BodyPartEnableChangedEvent(false)` and
      `BodyPartDroppedEvent`, then `SharedTransform.AttachToGridOrMap(:213)`.
   d. Leaving the container raises `EntRemovedFromContainerMessage` →
      `SharedBodySystem.Parts.cs:245 OnBodyPartRemoved` → `RemovePart(:262)`.
   e. `RemovePart(:343)` raises `BodyPartRemovedEvent` on the body at **`:357`**, then `RemoveLeg`,
      `RemovePartEffect`, then **`PartRemoveDamage(:362)`**.
   f. `BodyPartRemovedEvent` → `WolfmedBodyPartLifecycleSystem.OnPartRemoved:56`:
      `_projection.OnPartRemoved` (which only refreshes detached visuals, pain and the body projection — it
      does **not** clear the detached part's own damage, `WoundDamageProjectionSystem.cs:117-122`), then
      `_bleeding.OnPartChanged(body)`, then **`ChargeVitalPartLoss`**.
   g. `PartRemoveDamage(:395-408)`: vital + no remaining part of that type → `Bloodloss = VitalDamage` (100).
   h. Back in `TryAmputate`: `CreateOrMergeWound(parent, DismembermentWound, …)`,
      `ApplyAmputationConsequences(body, parent)` → `CreateOrMergeWound(parent, AmputationConsequenceWound,
      _wfPart.Get(parent).AmputationConsequenceSeverity)`, then `_throwing.TryThrow(part, …)`.

**Double-apply audit:**

* **Sever:** exactly one detach path (GUARD B closes Shitmed's). `TryDetachPart` is also self-idempotent — it
  returns `_body.GetParentPartOrNull(part) is null`, which is already true once `AttachToGridOrMap` ran.
* **`AmputationConsequenceWound` / `DismembermentWound`:** created once each per `TryAmputate`, on the
  **parent** (still attached), *after* the cascade in (b)-(g) has fully unwound. **Nothing anywhere in the
  Shitmed cascade creates a wound**, so there is no second writer. Repeated amputations onto the same parent
  deliberately produce *separate* wound entities (`mergeMode: SeparateInstances`, P3-D10) — that is the
  prototype's contract, not a double-apply.
* **Bleeding lifecycle:** cannot double-count by construction. Both the cascade's entry
  (`WoundBleedingSystem.OnPartChanged:288`) and the wound-creation entry (`OnWoundCreated`, subscribed at
  `:41`) call `RefreshBody`, which **recomputes** `BloodstreamComponent.BleedAmount` from the live wound set
  rather than adding a delta. Step (f) runs before the dismemberment wound exists and step (h) runs after; the
  second pass simply supersedes the first.
* **Vital damage:** `PartRemoveDamage`'s flat 100 and P3-D1's `lost` are **two different charges, both
  intended** — the 100 is upstream's fixed decapitation penalty (unchanged for everyone), the `lost` replaces
  the damage the removed limb stops contributing to `CheckVitalDamage`. They are not duplicates of each other
  and neither is applied twice. Measured on the foundation fixture: head carrying 13 → systemic Bloodloss
  `13 + 100 = 113`, projected body total `6 + 113 = 119`.
* **D2:** `ChargeVitalPartLoss` is reachable only through
  `SubscribeLocalEvent<WoundHostComponent, BodyPartRemovedEvent>`, so it is *structurally* unreachable for a
  non-host. Confirmed empirically — de-heading a `MobMonkey` (no `WoundHostComponent`; its `HeadMonkey`
  inherits `BaseHead` → `WolfmedBaseHead` so it *does* carry `WolfmedBodyPart` data) charges **exactly 100**
  Bloodloss, unchanged. The existing `TerminatingOrDeleted` guards at `:39`/`:60` were not weakened, so gibbing
  and mob deletion still charge nothing.

---

## 6. Build / test output tails

**Content.Server** (`-c DebugOpt`):
```
Build succeeded.
    0 Error(s)
```
**Content.Client** (`-c DebugOpt`):
```
Build succeeded.
    0 Error(s)
```
**Content.IntegrationTests** (`-c DebugOpt`):
```
Build succeeded.
    0 Error(s)
```

**Headless server** (`C:/tmp/wolfmed-plan/p3/wp/WP11-1-report-server.log`, 120 s, `net.port=1299`) —
`grep -cE "\[ERRO\]|\[FATL\]|Exception"` → **0**:
```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
```

**DockTest** (`WP11-1-report-docktest.log`) — run first, per project memory:
```
Test Run Successful.
Total tests: 3
     Passed: 3
```

**Wound suite** (`WP11-1-report-tests.log`,
`--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"`):
```
Test Run Successful.
Total tests: 43
     Passed: 43
```
(First run of that filter, before the literal fix, was 42 passed + `RoutesAndProjectsDamageTest` dirty-disposed;
isolated in `WP11-1-report-routes.log` as `Expected: 100 / But was: 113`. That was the P3-D1 behaviour change,
not a defect — fixed by correcting the literal, as recorded in §3 D-3.)

**Throwaway checks** (`WP11-1-report-d2check.log`, file since deleted):
```
hand thresholds: Slash=70,Piercing=200,Blunt=150,Heat=200
hand finishing: Piercing=12,Heat=15
PIERCING 14 hits to sever a hand: 16
HEAT 16 hits to sever a hand: 14
Test Run Successful.
Total tests: 2
```

---

## 7. What later packages must know

* **WP11-2 must not touch `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs`** (P3-D24). Both D26 sites are
  now live code, not comments; re-commenting or reordering the fan-out silently breaks amputation.
* **WP11-5 — T-AMP-NOGUN is replaced by T-AMP-GUN.** Use §4's numbers verbatim: 14-Piercing severs a hand on
  hit **16**, 16-Heat on hit **14**, 5 bullets into a foot leaves it attached and not `Severable`. `ceil(T/d)+1`
  is the general form. Both were measured on a real `MobHuman` via
  `WoundDamageRoutingSystem.TryApplyPartDamage(body, part, Spec(type, d))` with no ticks in between (no wound
  healing interferes). Also note PLAN3 §6.2's **T-AMP-THRESHOLD** row is unaffected (Slash 200 then Slash 15 on
  a head still works — the Slash finishing minimum is untouched at 15), and **T-AMP-OVERFLOW's** 20 × Blunt 25
  setup is still valid (Blunt finishing minimum is still 50).
* **WP11-5 — T-AMP-VITAL is already half-proved.** `RoutesAndProjectsDamageTest` now pins the wound-host half
  (charge == the part's own total, so `after == before + 100` exactly) and the throwaway pinned the D2 half
  (`MobMonkey` = exactly 100). Ship both as the real test in `WolfmedAmputationTest.cs`; do not rely on
  `WoundDamageFoundationTest`.
* **WP11-5 — hazard 7 still bites.** `AmputationConsequenceSeverity` is read off the **parent**. With the stock
  35 everywhere, a wrong redirect to the severed part reads 35 too. Keep the fixture torso's
  `amputationConsequenceSeverity: 50` line.
* **WP11-3 — `WoundDamageFoundationTest.cs` has one WP11-1 edit in it** (two literals in
  `RoutesAndProjectsDamageTest` plus a comment). Build on the current file; do not restore 100/106.
* **Any package that changes part damage numbers now changes amputation.** `Heat` is a threshold type on every
  limb, so anything that adds Heat to a limb (fire, welders, lasers, explosions) accumulates toward severing.
  In particular **WP11-3's locational armour directly changes how many shots sever a limb**: an annotated vest
  at `Piercing 0.25` turns a 14-Piercing round into 3.5, which is *below the new 12 finishing minimum*, so a
  vest-covered arm becomes unseverable by that round — the same effect PLAN3 §8.2 already flagged for machetes
  vs a heavy vest. That is a balance interaction worth naming in `WOLFMED_STATUS.md`, not a bug.
* **`AmputationConsequenceWound` is still inert** (P3-D2): severed limbs can be re-attached by Shitmed surgery,
  and nothing clears the wound. Phase 4 owns `CanAttachPart` + the surgical clear.
* **Explosion amputation is ported but unreachable from grenades** (P3-D3). `TryExplosionAmputate` is only
  reachable through `WoundDamageRoutingSystem.TryRouteDistributedDamage(..., isExplosion: true)`, which is what
  PLAN3's T-AMP-EXPLOSION drives.
* **Manifest rows `:80` (AmputationSystem `skipped`) and `:85` (OrganDamageSystem's `:24`/`:36` line numbers)
  are now stale.** Per PLAN3 §7.1 WP11-6 reconciles them; WP11-1 appended superseding rows rather than editing
  them, and each new row says so.
