# Phase 3 — P3-3 Per-part (locational) armour

Analyst report. READ-ONLY pass. Every claim below was verified in the real files at the stated
line numbers. ONYX = `C:/tmp/onyx` @ `2f5bab9` (read via `git show HEAD:<path>`). WG =
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` @ `1171e02fb6`.

---

## 0. Executive summary — read this first

1. **The seam is already built.** `PartDamageModifyEvent` is vendored, carries `PartType`,
   `Symmetry`, `Body`, `Part`, `Damage` and (WOLFGATE) `ArmorPenetration`; the routing system already
   relays it through the wearer's inventory; `WolfmedPartArmorSystem` is already the sole subscriber.
   Phase 3 adds **datafields + one `foreach` loop** and needs **zero new subscriptions and zero
   upstream file edits**. This is the cheapest package in phase 3.

2. **BLOCKER-CLASS FINDING — Onyx's `coverage` / `coverageSymmetry` are DEAD CODE at the pin.**
   They are declared (`ONYX Content.Shared/Armor/ArmorComponent.Locational.cs:13,19`) and used by
   **no C# anywhere in the Onyx tree** (`git grep -n "Coverage" HEAD -- '*.cs'` returns only the two
   declarations plus an unrelated `IdentityBlockerCoverage` / solar-panel / dirt-shader set). Onyx
   deliberately disabled the gate: `ONYX SharedArmorSystem.cs:124-128` is wrapped in an
   `<Onyx-ArmorGlobalProtection-edited>` marker reading
   *"The armor's global modifiers protect the whole body; they are never skipped for an individual
   body part that is not listed in @Coverage/@CoverageSymmetry."*

3. **Consequence: two of the "three locational-armour tests" cannot pass against Onyx's own shipped
   code.** `AppliesLocationalArmorExactlyOnceTest` and `EmptyCoverageAndSymmetryTest` assert
   coverage/symmetry gating that the shipped `OnPartDamageModify` does not perform (worked through in
   §3.4). Only `LocationalModifierOverridesAndFallbackTest` is consistent with the shipped code.
   **This is a genuine user decision (§4.1): port Onyx's shipped behaviour, or port Onyx's *intent*
   (re-enable the gate) which is the only version that makes P3-3's stated goal — "helmets and vests
   matter per limb" — actually happen.**

4. **`partModifiers` is used by ZERO Onyx prototypes** (`git grep -n "partModifiers" HEAD -- '*.yml'`
   → no output). It only appears in the integration-test `[TestPrototypes]`. So Onyx ships the
   feature with no content behind it.

5. **Do NOT port `traumaDeductions`.** Onyx YAML sets it on ~20 `- type: Armor` blocks
   (`explorer_suit.yml:15`, `hats.yml:9,34`, `hardsuits.yml:159,206`, the modsuit files, …) but there
   is **no C# datafield for it anywhere** (`git grep -rn "traumaDeduction\|TraumaDeduction" HEAD` hits
   YAML only). It is an orphaned/aborted feature in Onyx. Porting the YAML key would hard-fail
   Wolfgate's YAML linter.

6. **P3-6 numbers the user must see:** §10. Headline — every gunshot carries `origin: Shooter`
   (`WG Content.Shared/Projectiles/SharedProjectileSystem.cs:167`), the shooter has a
   `TargetingComponent` whose `Target` defaults to **`TargetBodyPart.Torso`**
   (`WG Content.Shared/_Shitmed/Targeting/TargetingComponent.cs:14`), so **almost every PvP hit in
   Wolfgate lands on the torso**. Under coverage gating a torso-only vest loses nothing in aimed PvP;
   a helmet protects only the 5.9 % of *unaimed* hits plus whatever fraction of players re-aim the
   doll at the head. Numbers in §10.

---

## 1. ONYX source — exact reading

### 1.1 `Content.Shared/Armor/ArmorComponent.cs` (60 lines)

Base component. Relevant differences from WG at §7.

```csharp
11:[RegisterComponent, NetworkedComponent, Access(typeof(SharedArmorSystem))]
12:public sealed partial class ArmorComponent : Component
17:    [DataField(required: true)]
18:    public DamageModifierSet Modifiers = default!;
24:    [DataField]
25:    public float PriceMultiplier = 1;
30:    [DataField]
31:    public bool ShowArmorOnExamine = true;
```
No `Coverage` / `PartModifiers` here — they live in a **second partial file**.

### 1.2 `Content.Shared/Armor/ArmorComponent.Locational.cs` (46 lines) — the whole locational data model

```csharp
 1:using Content.Shared.Body.Part;
 2:using Content.Shared.Damage;
 4:namespace Content.Shared.Armor;
 6:public sealed partial class ArmorComponent
 7:{
 8:    /// <summary>
 9:    /// Body part types protected by this armor. Empty protects every part.
10:    /// Inventory slot does not affect coverage.
11:    /// </summary>
12:    [DataField]
13:    public HashSet<BodyPartType> Coverage = [];
14:
15:    /// <summary>
16:    /// Protected sides. Empty protects every symmetry.
17:    /// </summary>
18:    [DataField]
19:    public HashSet<BodyPartSymmetry> CoverageSymmetry = [];
20:
21:    /// <summary>
22:    /// Ordered location-specific modifier overrides. The first matching entry is used.
23:    /// Falls back to the component's legacy coverage and modifiers when none match.
24:    /// </summary>
25:    [DataField]
26:    public List<ArmorPartModifier> PartModifiers = [];
27:}
28:
29:[DataDefinition]
30:public sealed partial class ArmorPartModifier
31:{
32:    /// <summary>Matching body part types. Empty matches every type.</summary>
35:    [DataField]
36:    public HashSet<BodyPartType> Parts = [];
37:
38:    /// <summary>Matching sides. Empty matches every symmetry.</summary>
41:    [DataField]
42:    public HashSet<BodyPartSymmetry> Symmetry = [];
43:
44:    [DataField(required: true)]
45:    public DamageModifierSet Modifiers = default!;
46:}
```

Note the doc comment on line 23 — *"Falls back to the component's legacy **coverage** and modifiers"* —
is the fossil of the disabled gate. Nothing reads `Coverage`.

### 1.3 `Content.Shared/Armor/SharedArmorSystem.cs` — the Onyx-marked lines

* `:8-10` `// <Onyx-WoundSystem>` `using Content.Shared._Onyx.Wounds;`
* `:29-31` the extra subscription:
  ```csharp
  30:        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
  ```
* `:57-65` the systemic branch inside `OnDamageModify`:
  ```csharp
  60:        if (TryComp(args.Owner, out WoundHostComponent? host))
  61:        {
  62:            args.Args.Damage = ApplyWoundSystemicArmor(args.Args.Damage, component.Modifiers, host);
  63:            return;
  64:        }
  ```
* `:70-106` `ApplyWoundSystemicArmor(DamageSpecifier damage, DamageModifierSet modifiers, WoundHostComponent host)`
  — splits the spec on `host.LocalizedDamageTypes`, returns `damage` untouched if there is no positive
  systemic component, otherwise applies the modifier set to the systemic half only.
* `:108-130` **the relay handler this package ports**:
  ```csharp
  109:    private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
  110:    {
  111:        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
  112:            return;
  113:
  114:        foreach (var profile in component.PartModifiers)
  115:        {
  116:            if (profile.Parts.Count != 0 && !profile.Parts.Contains(args.Args.PartType) ||
  117:                profile.Symmetry.Count != 0 && !profile.Symmetry.Contains(args.Args.Symmetry))
  118:                continue;
  119:
  120:            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, profile.Modifiers);
  121:            return;
  122:        }
  123:
  124:        // <Onyx-ArmorGlobalProtection-edited>
  125:        // The armor's global modifiers protect the whole body; they are never skipped
  126:        // for an individual body part that is not listed in @Coverage/@CoverageSymmetry.
  127:        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
  128:        // </Onyx-ArmorGlobalProtection-edited>
  129:    }
  ```

  Semantics, precisely:
  * **first match wins** (`return` inside the loop, `:121`);
  * a `profile` with `Parts.Count == 0 && Symmetry.Count == 0` matches everything and short-circuits
    every later entry — order in YAML is load-bearing;
  * `Symmetry` is checked **independently** of `Parts` — a profile can be "any part, left side";
  * `args.Args.Symmetry` for a torso/head is `BodyPartSymmetry.None`, so `symmetry: [Left]` never
    matches a torso;
  * **the fallback always fires** — there is no "this armour does not cover this part" outcome.
  * `MaskComponent.IsToggled` gate (`:111-112`) is base-game Onyx drift; WG has no mask gate in any
    armour handler (see §7).

### 1.4 `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` — the raise (already vendored)

ONYX `:674-681`:
```csharp
674:            var modify = new PartDamageModifyEvent(
675:                body, target, partComponent.PartType, partComponent.Symmetry, localized);
680:            if (TryComp(body, out InventoryComponent? inventory))
681:                _inventory.RelayEvent((body, inventory), modify);
```
ONYX `WoundEvents.cs:138-151` defines the event; `TargetSlots => SlotFlags.WITHOUT_POCKET` (`:150`).

WG's vendored copy is at `WG Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:741-749` and is
identical except for the D23 sixth argument:
```csharp
741:            var modify = new PartDamageModifyEvent(
742:                body,
743:                target,
744:                partComponent.PartType,
745:                partComponent.Symmetry,
746:                localized,
747:                _routedModifiers.GetValueOrDefault(body).ArmorPenetration); // WOLFGATE: D23, HOOK 10 armours the part here.
748:            if (TryComp(body, out InventoryComponent? inventory))
749:                _inventory.RelayEvent((body, inventory), modify);
```
and `WG Content.Shared/_Onyx/Wounds/WoundEvents.cs:138-157` carries the extra
`float armorPenetration = 0f` primary-ctor parameter and readonly `ArmorPenetration` field.

**Conclusion: the event already carries everything locational armour needs. No event change in phase 3.**

---

## 2. WG current state — exact reading

### 2.1 `Content.Shared/Armor/ArmorComponent.cs` (55 lines)

```csharp
12:[RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // goob edit - remove access restrictions
13:public sealed partial class ArmorComponent : Component
18:    [DataField(required: true), AutoNetworkedField]
19:    public DamageModifierSet Modifiers = default!;
25:    [DataField, AutoNetworkedField]
26:    public float PriceMultiplier = 1;
```
* `partial` → **a `_WF` partial file can add the locational fields with zero upstream edits.**
* No `[Access]` → `WolfmedPartArmorSystem` may read anything. (Confirmed: it already reads
  `component.Modifiers`.)
* No `ShowArmorOnExamine` (Onyx-only, irrelevant here).

### 2.2 `Content.Shared/Armor/SharedArmorSystem.cs` (100 lines)

```csharp
23:        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
24:        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
25:        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
26:        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
...
42:    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
43:    {
44:        if (TryApplyWoundHostArmor(uid, component, args)) return; // WOLFGATE: HOOK 10
45:
46:        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
47:            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
48:    }
```
**There is no `PartDamageModifyEvent` subscription in this file** — phase 1/2 deliberately kept it out
(manifest `:122`). Phase 3 must not add one here.

### 2.3 `Content.Shared/_WF/Wolfmed/Armor/SharedArmorSystem.Wolfmed.cs` (HOOK 10, 64 lines)

`partial class SharedArmorSystem` holding `TryApplyWoundHostArmor` (`:18-27`) and
`ApplyWoundSystemicArmor` (`:33-63`). Key WG-specific line (`:20`):
```csharp
20:        if (!TryComp(Transform(uid).ParentUid, out WoundHostComponent? host))
```
because **WG's `InventoryRelayedEvent<T>` has no `Owner`** (§7). This file is systemic-only and
**phase 3 does not touch it** — coverage does not gate systemic damage in Onyx either.

### 2.4 `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` (34 lines) — the file phase 3 edits

```csharp
18:public sealed class WolfmedPartArmorSystem : EntitySystem
21:    public override void Initialize()
22:    {
23:        base.Initialize();
24:
25:        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
26:    }
27:
28:    /// <summary>Applies the armour's penetration-adjusted modifiers to the routed part's damage.</summary>
29:    private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
30:    {
31:        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
32:            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration));
33:    }
34:}
```
Its own `<remarks>` (`:15-17`) records the phase-3 TODO verbatim:
> *"Onyx picks a per-part modifier set here; Wolfgate's `ArmorComponent` has no `PartModifiers`, so the
> armour's global modifiers protect every part — which is also Onyx's own fallback when no profile
> covers the part."*

That is exactly right: **today's WG behaviour is byte-for-byte Onyx's fallback branch**
(`ONYX SharedArmorSystem.cs:127`) plus WG's `PenetrateArmor`. So adding `partModifiers` alone is a
**pure no-op for every existing WG prototype** (none declares one).

### 2.5 Existing tests

| Test | File:line | What it pins |
|---|---|---|
| `AppliesArmorExactlyOnceTest` | `WG Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs:421-451` | head **5** and torso **5** from two 10-Blunt part hits through `WoundFoundationArmor` (`outerClothing`, `Blunt: 0.5`, **no** `coverage`). Its comment `:424-427` says *"Onyx's coverage/symmetry/locational tests are phase-3 material."* |
| `NonWoundHostUsesVanillaArmorTest` | same file `:453-472` | non-wound-host body: 10 Blunt → 5 total. Must stay green; coverage must never touch the non-wound-host path. |
| `ArmorPenetrationReachesWoundHostsTest` (T-AP) | `WG Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs:361-405` | `WolfmedBridgeArmor` (`outerClothing`, `Blunt: 0.5`), `targetPart: TargetBodyPart.LeftArm`: AP 0 → **5**, AP 1 → **10**, unarmoured → **10**. **This test hits a LeftArm through an outerClothing item** — it is the one that breaks under a slot-derived default that excludes arms (§4.3). |

(The task brief calls the bridge test "AppliesArmorExactlyOnceTest"; the applies-once test actually
lives in `WoundDamageFoundationTest.cs:422`, the bridge test is `ArmorPenetrationReachesWoundHostsTest`.)

---

## 3. Onyx's locational-armour tests — what they assert and what they need

All three live in `ONYX Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs`.

### 3.1 Fixtures (`[TestPrototypes]`, `:85-142`)

```yaml
- type: entity                       # :85-94
  id: WoundFoundationArmorHead
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Head]
    modifiers: { coefficients: { Blunt: 0.5 } }

- type: entity                       # :96-104
  id: WoundFoundationArmorAll
  components:
  - type: Clothing
    slots: [head]
  - type: Armor
    modifiers: { coefficients: { Blunt: 0.5 } }

- type: entity                       # :106-116
  id: WoundFoundationArmorLeftArm
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Arm]
    coverageSymmetry: [Left]
    modifiers: { coefficients: { Blunt: 0.5 } }

- type: entity                       # :118-142
  id: WoundFoundationArmorLocational
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Chest]
    modifiers: { coefficients: { Blunt: 0.8 } }
    partModifiers:
    - parts: [Head]
      modifiers: { coefficients: { Blunt: 0.25 } }
    - parts: [Arm]
      symmetry: [Left]
      modifiers: { coefficients: { Blunt: 0.5 } }
    - parts: [Arm]
      symmetry: [Right]
      modifiers: { coefficients: { Blunt: 0.75 } }
```
Note the **deliberate slot/part mismatch**: the head-covering armour is worn in `outerClothing`, the
all-covering armour in `head`. That is Onyx proving its own doc comment
(`ArmorComponent.Locational.cs:10` — *"Inventory slot does not affect coverage"*) and
`WoundEvents.cs:136` — *"Item slot and protected body part are intentionally independent."*

Body fixture: `WoundFoundationBody` (`:40-57`) with a Nubody `InitialBody` of Torso(`Chest`)/Head/
LeftArm/RightArm only — **four parts, no legs/hands/feet**.

### 3.2 `AppliesLocationalArmorExactlyOnceTest` (`:480-506`)

Equip `WoundFoundationArmorHead` in `outerClothing`; `TryApplyPartDamage(body, head, Blunt 10)` and
`(body, torso, Blunt 10)`.
Asserts **head = 5**, **torso = 10**.

### 3.3 `EmptyCoverageAndSymmetryTest` (`:508-543`)

Part A: `WoundFoundationArmorAll` in the `head` slot; hit the **torso** for 10; assert **torso = 5**
(empty coverage → protects everything, slot irrelevant). Unequip.
Part B: `WoundFoundationArmorLeftArm` in `outerClothing`; hit left arm 10 and right arm 10; assert
**leftArm = 5**, **rightArm = 10**.

### 3.4 `LocationalModifierOverridesAndFallbackTest` (`:545-580`)

`WoundFoundationArmorLocational` in `outerClothing`; 20 Blunt to head, torso, leftArm, rightArm.
Asserts **head 5** (0.25), **torso 16** (no profile matches → fallback 0.8), **leftArm 10** (0.5),
**rightArm 15** (0.75).

### 3.5 Reconciliation against Onyx's shipped `OnPartDamageModify` — the contradiction

| Test | Assertion | What `ONYX SharedArmorSystem.cs:109-129` actually produces | Verdict |
|---|---|---|---|
| 3.2 head | 5 | `PartModifiers` empty → fallback `0.5` → 5 | ✔ |
| 3.2 **torso** | **10** | `PartModifiers` empty → fallback `0.5` → **5** | ✘ **fails** |
| 3.3A torso | 5 | fallback `0.5` → 5 | ✔ |
| 3.3B leftArm | 5 | fallback `0.5` → 5 | ✔ |
| 3.3B **rightArm** | **10** | fallback `0.5` → **5** | ✘ **fails** |
| 3.4 head/torso/leftArm/rightArm | 5 / 16 / 10 / 15 | first-match-wins loop + fallback → 5 / 16 / 10 / 15 | ✔ |

So **`AppliesLocationalArmorExactlyOnceTest` and `EmptyCoverageAndSymmetryTest` are red against Onyx's
own pinned code.** They were written for the pre-`<Onyx-ArmorGlobalProtection-edited>` gate and never
updated. Only `LocationalModifierOverridesAndFallbackTest` matches the shipped behaviour.

This is not a Wolfgate porting problem — it is an upstream inconsistency that P3-3 must resolve
explicitly, because the two red tests encode the *only* semantics under which "helmets and vests
matter per limb" is true.

---

## 4. Design

### 4.1 USER DECISION — coverage semantics

| Option | Code | Content cost | Balance delta today | Tests |
|---|---|---|---|---|
| **A. Onyx-verbatim (shipped)** | port `PartModifiers` loop + fallback only; add `Coverage`/`CoverageSymmetry` as inert fields for re-sync fidelity | 0 | **none** | only 3.4 portable; 3.2/3.3 must be rewritten or dropped again |
| **B. Onyx-intended (gate active)** | as A, plus: a part not in `Coverage`/`CoverageSymmetry` gets **no** modifier at all; empty set = all (Onyx's doc comment) | 0 (nothing in WG declares `coverage`) | **none until content is annotated** | all three port verbatim modulo D9 |
| **C. B + slot-derived default** | when `Coverage` is *unset*, derive it from the slot the item is actually equipped in | 0 (no per-item YAML) | **large, immediate** | 3.2/3.3/3.4 port; **breaks `AppliesArmorExactlyOnceTest` and T-AP** (§4.3) |

**Recommendation: B as the shipped code, C behind a CCVar defaulted OFF in phase 3**, plus a small
explicit-`coverage` content pass on the abstract armour bases (§5). Rationale:
* B is a strict superset of A, is what Onyx's doc comments and tests describe, and is a **zero-risk
  no-op** for the current tree because not one WG prototype declares `coverage:`.
* C is the piece that actually delivers P3-3's goal, but it silently re-balances 272 armour entries at
  once and invalidates two green tests. It deserves its own CCVar and its own playtest, not to be
  smuggled in with a datafield port. Suggested name `wounds.armor_slot_coverage`, default `false`,
  alongside the existing `wounds.*` CCVars.

If the user prefers a single decision: **B now, C in the balance pass.**

### 4.2 Datafields to add (all `// WOLFGATE`, additive, one new file)

New file `Content.Shared/_WF/Wolfmed/Armor/ArmorComponent.Wolfmed.cs`, `namespace Content.Shared.Armor`,
mirroring how `SharedArmorSystem.Wolfmed.cs` already extends an upstream type from `_WF` (manifest
`:121`). **Zero upstream edits.**

```csharp
public sealed partial class ArmorComponent
{
    // WOLFGATE: nullable, unlike Onyx's non-nullable `= []`, so "unset" and "explicitly covers
    // everything" are distinguishable — needed by the slot-derived default (Option C).
    [DataField] public HashSet<BodyPartType>? Coverage;
    [DataField] public HashSet<BodyPartSymmetry>? CoverageSymmetry;
    [DataField] public List<ArmorPartModifier> PartModifiers = [];
}

[DataDefinition]
public sealed partial class ArmorPartModifier
{
    [DataField] public HashSet<BodyPartType> Parts = [];
    [DataField] public HashSet<BodyPartSymmetry> Symmetry = [];
    [DataField(required: true)] public DamageModifierSet Modifiers = default!;
}
```

**Traps:**
* **Do NOT add `[AutoNetworkedField]`.** WG's `ArmorComponent` carries
  `[AutoGenerateComponentState]` with `[AutoNetworkedField]` on `Modifiers` and `PriceMultiplier`
  (`ArmorComponent.cs:12,18,25`). `ArmorPartModifier` is a `[DataDefinition]`, **not**
  `[Serializable, NetSerializable]`, so an auto-networked `List<ArmorPartModifier>` would not compile
  / would need a NetSerializable mirror. These fields are prototype-static and never mutated at
  runtime, so the client already gets them from the prototype at spawn. Leave them un-networked.
* Nullable `HashSet<BodyPartType>?` is a deliberate deviation from Onyx and must be recorded in the
  manifest Deviations section.
* `ArmorPartModifier` is a **new type name** — checked: no `ArmorPartModifier` anywhere in WG
  (`grep -rn "ArmorPartModifier" Content.*` → nothing). No collision.
* **`traumaDeductions` is NOT ported** (§0.5).

### 4.3 The handler — `WolfmedPartArmorSystem.OnPartDamageModify` rewrite

Target shape (Option B; the Option-C block is the marked `if` in the middle):

```csharp
private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
{
    foreach (var profile in component.PartModifiers)          // Onyx :114-122, first match wins
    {
        if (profile.Parts.Count != 0 && !profile.Parts.Contains(args.Args.PartType) ||
            profile.Symmetry.Count != 0 && !profile.Symmetry.Contains(args.Args.Symmetry))
            continue;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(profile.Modifiers, args.Args.ArmorPenetration)); // WOLFGATE: D23
        return;
    }

    if (!Covers(uid, component, args.Args.PartType, args.Args.Symmetry))   // WOLFGATE: Option B gate
        return;

    args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
        DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration));
}
```
with
```csharp
private bool Covers(EntityUid uid, ArmorComponent component, BodyPartType type, BodyPartSymmetry symmetry)
{
    var coverage = component.Coverage;
    if (coverage is null && _configuration.GetCVar(CCVars.WoundsArmorSlotCoverage))   // Option C
        coverage = SlotCoverage(uid);

    if (coverage is { Count: > 0 } && !coverage.Contains(type))
        return false;

    return component.CoverageSymmetry is not { Count: > 0 } sides || sides.Contains(symmetry);
}
```

Design points, each verified:

* **Penetration must wrap `profile.Modifiers` too.** The existing line 32 already does this for the
  global set; a `partModifiers` entry that skipped `PenetrateArmor` would silently make every AP
  weapon useless against any armour that declares a part profile. `DamageSpecifier.PenetrateArmor`
  (`WG Content.Shared/Damage/DamageSpecifier.cs:306-330`) returns the set unchanged at `penetration == 0`
  (`:308,311`) and a **new empty** set at `>= 1f` (`:313-315`); `ApplyModifierSet` (`:133-163`) leaves a
  type untouched when the set has no entry — so an empty set is a true no-op, as T-AP already proved
  (`WP10-6a` measured 5 / 10 / 10).
* **The gate must sit AFTER the `PartModifiers` loop**, matching Onyx's `<Onyx-ArmorGlobalProtection>`
  comment which scopes coverage only to "an individual body part ... not listed in
  `@Coverage`/`@CoverageSymmetry`" — i.e. only the fallback. An explicit `partModifiers` entry is an
  intentional override and is not coverage-gated. (This also makes 3.4's `coverage: [Chest]` +
  head/arm profiles behave: the profiles fire regardless of the `Chest`-only coverage, and the torso
  falls through to the coverage-satisfied `0.8` fallback — 5/16/10/15 either way.)
* **`Symmetry` vs `Parts` are independent** in Onyx; keep that. `symmetry: [Left]` alone means
  "any left part". A torso (`BodyPartSymmetry.None`) is excluded by such a set.
* **No mask gate.** WG has no `MaskComponent.IsToggled` check in any armour handler
  (`SharedArmorSystem.cs:42-54` — none; `hooks-a.md` §7a already classified it as base-game drift,
  unrelated to wounds). `MaskComponent.IsToggled` does exist in WG
  (`Content.Shared/Clothing/Components/MaskComponent.cs:28`), but adding the check only on the part
  path would make a toggled-down mask armour the torso and not the head. Record as a deliberate
  deviation; if the user ever wants mask gating it belongs in all four handlers at once.
* **Systemic path untouched.** `SharedArmorSystem.Wolfmed.cs:23-25` keeps applying the global
  modifiers to systemic types for any worn armour. Onyx does the same; coverage is a *localized*
  concept.
* **Non-wound-host path untouched.** `OnDamageModify`'s fallback (`SharedArmorSystem.cs:46-47`),
  `OnBorgDamageModify` (`:50-54`), `OnCoefficientQuery` (`:34-40`) and `GetArmorExamine` (`:71-99`) all
  stay as-is. Mice, vehicles (`Entities/Objects/Vehicles/buckleable.yml`), blastdoors
  (`_Mono/Entities/Structures/Doors/blastdoor.yml`) and mothroaches
  (`_Mono/Entities/Mobs/NPCs/mothroaches.yml`) carry `- type: Armor` and are not wound hosts —
  coverage must never reach them. It does not: the gate lives only in `WolfmedPartArmorSystem`, which
  only ever runs off `PartDamageModifyEvent`, which only `WoundDamageRoutingSystem` raises.

### 4.4 D9 mapping — `BodyPartType`, `TargetBodyPart`, symmetry

| Onyx concept | Onyx value | WG value | Where |
|---|---|---|---|
| `BodyPartType` | `Other=0, Torso=1, Head=2, Arm=3, Hand=4, Leg=5, Foot=6, Tail=7, Chest=8, Groin=9` (`ONYX Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:15-27`) | `Other=0, Torso, Head, Arm, Hand, Leg, Foot, Tail` (`WG Content.Shared/Body/Part/BodyPartType.cs:11-20`) | D9 |
| `coverage: [Chest]` | Chest | **`[Torso]`** | D9 |
| `coverage: [Groin]` | Groin | **deleted** (folds into Torso — never emit `Torso` twice) | D9 |
| `coverage: [Chest, Groin, Arm, Leg, Tail]` (`ONYX explorer_suit.yml:14`) | — | `[Torso, Arm, Leg, Tail]` | D9 |
| `BodyPartSymmetry` | `None, Left, Right` (`ONYX …BodyPartComponent.cs:30`) | `None, Left, Right` (`WG Content.Shared/Body/Part/BodyPartSymmetry.cs:10-15`) | **SAME** |

`TargetBodyPart` never enters the coverage path — `PartDamageModifyEvent` carries the resolved
`BodyPartComponent.PartType`/`.Symmetry` directly (`WoundDamageRoutingSystem.cs:744-745`), and
`TargetBodyPart.Groin` has already been folded to the torso part upstream by
`WoundTargetResolver.TryResolveAvailable` → `SharedBodySystem.ConvertTargetBodyPart`
(`WG Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs:48`). So **a `Groin` hit arrives at the
relay as `PartType == Torso, Symmetry == None`** and is covered by a torso-covering vest. Correct and
free.

Human wound host has exactly these part types: Torso ×1, Head ×1, Arm ×2, Hand ×2, Leg ×2, Foot ×2
(`WG Resources/Prototypes/Body/Prototypes/human.yml:2-50`) — 11 parts, no Tail, no Other.

### 4.5 How the inventory relay picks "which worn item covers which part"

**Onyx's answer: it does not.** `ArmorComponent.Locational.cs:10` — *"Inventory slot does not affect
coverage"*; `WoundEvents.cs:136-137` — *"Item slot and protected body part are intentionally
independent."* The relay fans the event out to **every** equipped item in
`SlotFlags.WITHOUT_POCKET` and each item decides for itself from its own `coverage`.

WG's relay machinery (`Content.Shared/Inventory/InventorySystem.Relay.cs:79-90`):
```csharp
79:    public void RelayEvent<T>(Entity<InventoryComponent> inventory, T args) where T : IInventoryRelayEvent
81:        if (args.TargetSlots == SlotFlags.NONE) return;
84:        var ev = new InventoryRelayedEvent<T>(args);
85:        var enumerator = new InventorySlotEnumerator(inventory, args.TargetSlots);
86:        while (enumerator.NextItem(out var item))
88:            RaiseLocalEvent(item, ev);
```
`SlotFlags.WITHOUT_POCKET = All & ~POCKET` (`WG Content.Shared/Inventory/SlotFlags.cs:37`), so on a
human the event reaches items in: `shoes(FEET) jumpsuit(INNERCLOTHING) outerClothing(OUTERCLOTHING)
gloves(GLOVES) neck/neck2/neck3(NECK) mask(MASK) eyes(EYES) ears(EARS) head(HEAD) suitstorage
back belt id wallet balaclava leftarmband rightarmband helmetcover helmetattachment`
(`WG Resources/Prototypes/InventoryTemplates/human_inventory_template.yml`, slot names/flags at
`:4-178`). Multiple armours therefore **stack multiplicatively** in slot-enumeration order — today and
after phase 3.

**For Option C (slot-derived default) the handler needs the slot.** WG's relayed wrapper does **not**
carry it:
```csharp
115:public sealed class InventoryRelayedEvent<TEvent> : EntityEventArgs
117:    public TEvent Args;
119:    public InventoryRelayedEvent(TEvent args)
```
(vs `ONYX InventorySystem.Relay.cs:226-237` which has `public EntityUid Owner;`). The supported,
zero-upstream-edit lookup is:
```csharp
[Dependency] private InventorySystem _inventory = default!;
...
if (_inventory.TryGetContainingSlot(uid, out var slot))  // WG InventorySystem.Helpers.cs:41
    → slot.SlotFlags                                     // WG InventoryTemplatePrototype.cs:25
```
`TryGetContainingSlot(Entity<TransformComponent?, MetaDataComponent?> entity, [NotNullWhen(true)] out SlotDefinition? slot)`
resolves via `TryGetContainingContainer` + `TryGetSlot` (`Helpers.cs:43-49`) — exact, cheap, and
authoritative (the actual slot, not the item's allowed flags).

Fallback if that returns false (item not in an inventory slot — shouldn't happen inside the relay):
treat as full coverage.

Why not read `ClothingComponent.Slots` (`WG Content.Shared/Clothing/Components/ClothingComponent.cs:43`)
instead? It is the item's *allowed* slots and is frequently a multi-slot set; `TryGetContainingSlot`
is unambiguous. Keep `ClothingComponent.Slots` only as a second-chance fallback if desired.

**Proposed default table** (`SlotFlags → HashSet<BodyPartType>`), derived from Onyx's own content
conventions (§5.1) and WG's human template:

| SlotFlags | Parts | Onyx precedent |
|---|---|---|
| `HEAD`, `MASK`, `EYES`, `EARS`, `BALACLAVA`, `HELMETCOVER`, `HELMETATTACHMENT` | `Head` | `hats.yml:7`, `hardsuit-helmets.yml:83`, `abductor/helmets.yml:19` all `coverage: [Head]` |
| `OUTERCLOTHING`, `INNERCLOTHING` | `Torso, Arm, Leg, Tail, Other` | `explorer_suit.yml:14` `[Chest, Groin, Arm, Leg, Tail]`; `coats.yml:27,108` same |
| `GLOVES`, `ARMBANDLEFT`, `ARMBANDRIGHT` | `Hand` | `gloves.yml:295` `coverage: [Hand]` |
| `FEET` | `Foot` | no Onyx precedent; symmetric with gloves |
| `NECK` | `Head, Torso` | no Onyx precedent (scarves/capes); judgement call |
| `BACK`, `BELT`, `SUITSTORAGE`, `IDCARD`, `WALLET`, anything else | **all parts** (no gating) | conservative: these are containers, not armour |

`ClothingModsuitChestplate*` in Onyx (`modsuit.yml:95-103`) covers
`[Chest, Groin, Arm, Hand, Leg, Foot, Tail, Other]` — i.e. **everything but the head**. If the user
wants the gentler variant of Option C, use that for `OUTERCLOTHING` instead; see the numbers in §10.

**Option C breaks two currently-green tests** — both equip an `outerClothing` armour and then hit a
part that a torso/arm/leg default does or does not include:
* `AppliesArmorExactlyOnceTest` (`WoundDamageFoundationTest.cs:445-449`) hits the **head** through an
  `outerClothing` armour and expects 5 → would become 10.
* `ArmorPenetrationReachesWoundHostsTest` (`WolfmedDamageBridgeTest.cs:379-403`) hits the **LeftArm**
  through an `outerClothing` armour and expects 5/10/10 → survives the `{Torso, Arm, Leg}` table, dies
  under a `{Torso}`-only table.
Whichever option is picked, both tests must be re-derived explicitly, not left to luck.

### 4.6 Interaction with `_Mono` `ArmorPlate`

`SharedArmorPlateSystem` (`WG Content.Shared/_Mono/ArmorPlate/SharedArmorPlateSystem.cs:22-47`) is
**not part-aware and is not touched by this package.** It subscribes
`<ArmorPlateProtectedComponent, BeforeDamageChangedEvent>` (`:46`) on the *wearer*, absorbs from
`args.Damage` in place and converts a share to stamina (`:49-80+`).

Ordering, as shipped: `WoundDamageRoutingSystem` subscribes the same event
`before: [typeof(SharedArmorPlateSystem)]` on both of its handlers
(`WG Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:64,66`, PLAN §8.3 trap 4). On the first
pass routing cancels (`:76`) and the plate self-guards on `args.Cancelled` (`ArmorPlate:51`) and does
nothing. On the re-entrant routed pass routing returns early because `_routing.Contains(ent)`
(`:73`), so the plate absorbs exactly once, **before** the damage is split systemic/localized and long
before `PartDamageModifyEvent` exists. Net order per hit:

> plate absorption (whole body) → systemic/localized split → part resolution →
> `PartDamageModifyEvent` relay → per-part armour → part `TryChangeDamage`

So plates remain effectively omnidirectional torso-grade protection applied to every limb. That is a
**balance inconsistency worth recording** (a plate carrier protects your feet) but fixing it means
part-awareness in a `_Mono` system on an event that predates part resolution — out of scope for P3-3,
flag for the balance pass.

---

## 5. Wolfgate content

### 5.1 Scale of the problem

* `- type: Armor` appears **272 times across 75 prototype files**
  (`grep -rn "^\s*-\s*type:\s*Armor\s*$" Resources/Prototypes --include=*.yml | wc -l` → 272; `-l | wc -l` → 75).
* Breakdown by tree: `_Mono/Entities` 37 files, `Entities/Clothing` 20, `_NF/Entities` 7,
  `_Goobstation/Entities` 7, `Nyanotrasen/Entities` 2, `Entities/Objects` 2.
* **271 of 272 inherit their `Clothing.slots` from a parent prototype**; only one declares
  `slots: [HEAD]` inline. (Scripted split of every `- type: entity` block containing an `Armor`
  component; the bases are e.g. `ClothingHeadBase` → `slots: [HEAD]`,
  `WG Resources/Prototypes/Entities/Clothing/Head/base_clothinghead.yml:4-8`.)

**Therefore a per-item `coverage:` pass is a 272-edit change and a static "derive coverage from the
YAML slot list" is not even possible without walking the inheritance chain.** The runtime
`TryGetContainingSlot` default (§4.5) is the only data-driven option that costs zero prototype edits —
which is exactly why Option C exists.

### 5.2 Items that would get coverage, if annotating explicitly

| Class | Representative WG prototypes | Proposed `coverage` |
|---|---|---|
| Helmets / hats | `ClothingHeadHelmetBasic`, `ClothingHeadHelmetSwat`, `ClothingHeadHelmetRiot` (`Entities/Clothing/Head/helmets.yml`), `ClothingHeadBPHelmetLight/...` (`_Mono/.../Head/Helmets/bulletproof_helmets.yml`), all `_Mono/.../Head/Hardsuits/*.yml` (11 files), `_NF`/`_Goobstation` helmet files | `[Head]` |
| Vests / armour / coats / suits | `_Mono/.../OuterClothing/Armor/bulletproof_vests.yml` (5 vests), `Armor/exosuit.yml`, `Vests/vests.yml`, `Entities/Clothing/OuterClothing/{armor,vests,coats,suits,wintercoats,bio,softsuits}.yml`, all `_Mono/.../OuterClothing/Hardsuits/*.yml` (10 files) | `[Torso, Arm, Leg]` (+ `Tail` for tailed species) |
| Jumpsuits | `Entities/Clothing/Uniforms/jumpsuits.yml`, `_Mono/.../Jumpsuit/jumpsuits.yml`, `_Goobstation/.../Uniforms/jumpsuits.yml` | `[Torso, Arm, Leg]` |
| Gloves | `Entities/Clothing/Hands/gloves.yml`, `_Mono/.../Hands/gloves.yml`, `_NF brassknuckles.yml` | `[Hand]` |
| Masks / glasses | `Entities/Clothing/Masks/masks.yml`, `Eyes/glasses.yml`, `_Mono/.../Eyes/glasses.yml` | `[Head]` |
| Shoes | (none currently carry `Armor`) | `[Foot]` |
| **Non-worn `Armor` carriers — leave alone** | `_Mono/Entities/Mobs/NPCs/mothroaches.yml`, `_Mono/Entities/Structures/Doors/blastdoor.yml`, `Entities/Objects/Vehicles/buckleable.yml`, `Entities/Objects/Specific/Janitorial/janitor.yml`, `_Goobstation/.../ghetto_stuff.yml`, `_Mono/Entities/Clothing/base_shielding.yml` | none — not wound hosts and/or never in an inventory slot |

### 5.3 Recommended content scope for phase 3

Under Option B, annotate **only the abstract armour bases that many children inherit** — that is a
handful of edits covering most items:
`ClothingHeadBase`-derived armoured bases, `Entities/Clothing/OuterClothing/base_clothingouter.yml`
and `Entities/Clothing/base_clothing.yml` armour blocks, plus the five `_Mono` bulletproof vests and
the `_Mono` bulletproof/hardsuit helmets by hand (they are the Wolfgate gun-PvP items that matter).
Everything else keeps unset coverage = full protection = today's behaviour, and can be annotated
incrementally.

---

## 6. Tests

### 6.1 Fixtures needed

All three Onyx tests drop straight into the **existing** WG
`Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs`, which already has
`GameTest`, the `Spec(string, int)` helper (`:598-604`), a Shitmed `WoundFoundationBodyGraph`
(`:39-55`) and the `WoundFoundationBody` wound host (`:57-73`).

Three new `[TestPrototypes]` entries, D9-mapped:
```yaml
- type: entity
  id: WoundFoundationArmorHead
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Head]                       # WOLFGATE: Onyx :91
    modifiers: { coefficients: { Blunt: 0.5 } }

- type: entity
  id: WoundFoundationArmorLeftArm
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Arm]
    coverageSymmetry: [Left]
    modifiers: { coefficients: { Blunt: 0.5 } }

- type: entity
  id: WoundFoundationArmorLocational
  components:
  - type: Clothing
    slots: [outerClothing]
  - type: Armor
    coverage: [Torso]                      # WOLFGATE: D9, Onyx `coverage: [Chest]`
    modifiers: { coefficients: { Blunt: 0.8 } }
    partModifiers:
    - parts: [Head]
      modifiers: { coefficients: { Blunt: 0.25 } }
    - parts: [Arm]
      symmetry: [Left]
      modifiers: { coefficients: { Blunt: 0.5 } }
    - parts: [Arm]
      symmetry: [Right]
      modifiers: { coefficients: { Blunt: 0.75 } }
```
The existing `WoundFoundationArmor` (`:75-83`, `outerClothing`, `Blunt: 0.5`, no coverage) serves as
Onyx's `WoundFoundationArmorAll`; Onyx's own `WoundFoundationArmorAll` uses the `head` slot on purpose
(to prove slot-independence). **Add a second no-coverage armour in the `head` slot** —
`WoundFoundationArmorAllHead` — so the slot-independence half of `EmptyCoverageAndSymmetryTest` still
means something, and so it will catch an accidental Option-C regression.

The `WoundFoundationBodyGraph` already contains torso + head + left arm + right arm — **exactly the
four parts all three tests need.** No body-prototype work.

### 6.2 Test table

| Onyx test | WG name | Asserts (Option B, D9-mapped) | Notes |
|---|---|---|---|
| `AppliesLocationalArmorExactlyOnceTest` (`:480-506`) | same | equip `WoundFoundationArmorHead` in `outerClothing`; `TryApplyPartDamage(body, head, Blunt 10)` → head **5**; `(body, torso, Blunt 10)` → torso **10** | `damage` must be `WolfmedDamageableSystem` (WG's facade) not `DamageableSystem`, as the existing test already does (`:440`). **Red against Onyx's shipped code** (§3.5) — only green under Option B. |
| `EmptyCoverageAndSymmetryTest` (`:508-543`) | same | A: `WoundFoundationArmorAllHead` in `head`, hit **torso** 10 → **5** (slot-independent, empty coverage protects all); unequip. B: `WoundFoundationArmorLeftArm` in `outerClothing`, leftArm 10 → **5**, rightArm 10 → **10** | Part B **red** against Onyx's shipped code — only green under Option B. |
| `LocationalModifierOverridesAndFallbackTest` (`:545-580`) | same | `WoundFoundationArmorLocational` in `outerClothing`; 20 Blunt each → head **5**, torso **16**, leftArm **10**, rightArm **15** | **Green under both A and B.** This is the only one that can be ported unconditionally. |

Plus two regression assertions to add in the same package:

| New | Where | Asserts |
|---|---|---|
| **T-P3ARM-AP** | extend `LocationalModifierOverridesAndFallbackTest`, or a 4th test | same armour, `armorPenetration: 1f` via `WolfmedDamageableSystem.ChangeDamage` / `DamageableSystem.TryChangeDamage(..., targetPart: TargetBodyPart.Head, armorPenetration: 1f)` → head takes **20**, proving the `partModifiers` branch routes through `PenetrateArmor`. Nothing in Onyx covers this; without it the new branch can silently kill AP. |
| **T-P3ARM-UNCOVERED-AP** | same | armour with `coverage: [Head]`, hit the torso with `armorPenetration: 0f` and `1f` → both **10** (the gate returns before any modifier maths, so AP is irrelevant on an uncovered part). Pins that the gate is not accidentally AP-sensitive. |

Existing tests to re-derive and keep green: `AppliesArmorExactlyOnceTest`
(`WoundDamageFoundationTest.cs:421-451`, head 5 / torso 5 — unchanged under A and B, **changes under C**),
`NonWoundHostUsesVanillaArmorTest` (`:453-472`, unchanged in all options),
`ArmorPenetrationReachesWoundHostsTest` (`WolfmedDamageBridgeTest.cs:361-405`, 5/10/10 — unchanged
under A and B, **at risk under C**).

### 6.3 Standing traps (from PLAN2 §6.1, still apply)

* Integration tests fail on `db.ef` sqlite warnings — check `DockTest` first (memory:
  integration-test-db-warning-failures).
* Wound creation is a dice roll (`P2-D23`) — **irrelevant here**: all three armour tests assert
  `GetAllDamage(part)`, never wound severity, so they are deterministic.
* `TryApplyPartDamage` overload used by the tests is
  `WG WoundDamageRoutingSystem.cs:240-249` — `(EntityUid body, EntityUid part, DamageSpecifier damage,
  EntityUid? origin = null, bool ignoreResistances = false, bool healWounds = true)`. It routes with an
  explicit part and therefore never touches the random weighted fallback.

---

## 7. Symbols the phase-3 files need — SAME / DIFFERENT / MISSING vs WG

| Symbol (as Onyx uses it) | Status in WG | Evidence |
|---|---|---|
| `PartDamageModifyEvent` (ctor, `Body`, `Part`, `PartType`, `Symmetry`, `Damage`, `TargetSlots`) | **SAME + extra** — WG adds trailing `float armorPenetration = 0f` and readonly `ArmorPenetration` | ONYX `WoundEvents.cs:138-151` vs WG `Content.Shared/_Onyx/Wounds/WoundEvents.cs:138-157` |
| `SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` | **SAME pair, already registered** in `_WF`, not in upstream | WG `_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:25` (ONYX registers it in `SharedArmorSystem.cs:30`) |
| `ArmorComponent` (partial, no `[Access]`) | **DIFFERENT but friendlier** — WG is `partial`, has `[AutoGenerateComponentState]`, **no** `[Access]`; Onyx has `[Access(typeof(SharedArmorSystem))]` and no auto-state | WG `Content.Shared/Armor/ArmorComponent.cs:12-13` vs ONYX `ArmorComponent.cs:11-12` |
| `ArmorComponent.Modifiers` | SAME (`DamageModifierSet`, `[DataField(required: true)]`) | WG `:18-19` / ONYX `:17-18` |
| `ArmorComponent.Coverage` / `.CoverageSymmetry` / `.PartModifiers` | **MISSING in WG** — this package adds them | ONYX `ArmorComponent.Locational.cs:13,19,26`; `grep -rn "PartModifiers\|CoverageSymmetry" WG/Content.*` → nothing |
| `ArmorPartModifier` (`[DataDefinition]`, `Parts`, `Symmetry`, `Modifiers`) | **MISSING in WG** — new type, no name collision | ONYX `ArmorComponent.Locational.cs:29-46` |
| `ArmorComponent.ShowArmorOnExamine` | **MISSING in WG** — Onyx-only, not needed | ONYX `:30-31` |
| `BodyPartType` | **DIFFERENT** — WG has no `Chest`/`Groin` (D9) | WG `Content.Shared/Body/Part/BodyPartType.cs:11-20` vs ONYX `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:15-27` |
| `BodyPartSymmetry` | **SAME** (`None, Left, Right`) | WG `Content.Shared/Body/Part/BodyPartSymmetry.cs:10-15` vs ONYX `…BodyPartComponent.cs:30` |
| `DamageSpecifier.ApplyModifierSet(DamageSpecifier, DamageModifierSet)` | SAME signature | WG `Content.Shared/Damage/DamageSpecifier.cs:133` |
| `DamageSpecifier.PenetrateArmor(DamageModifierSet, float)` | **WG-ONLY** (Goob) — Onyx has no AP at all (D5). Must wrap every modifier set the port applies | WG `DamageSpecifier.cs:306-330` |
| `DamageSpecifier.DamageDict` | **DIFFERENT** — `Dictionary<string, FixedPoint2>` in WG, `ProtoId<DamageTypePrototype>`-keyed in Onyx (bit WP10-2) | WG `DamageSpecifier.cs:44`. *Not touched by this package* — coverage never enumerates damage types. |
| `DamageModifierSet.Coefficients` / `.FlatReduction` | **DIFFERENT names/keys** — WG `Dictionary<string,float> Coefficients` + **`FlatReduction`** (singular); Onyx `FlatReductions` with `ProtoId` keys | WG `Content.Shared/Damage/DamageModifierSet.cs:23,26` vs ONYX `SharedArmorSystem.cs:173,177`. *Only affects `GetArmorExamine`, which this package does not port.* |
| `InventoryRelayedEvent<T>` | **DIFFERENT** — WG has **no `Owner`** field/ctor param | WG `Content.Shared/Inventory/InventorySystem.Relay.cs:115-123` vs ONYX `InventorySystem.Relay.cs:226-237`. Already worked around by `Transform(uid).ParentUid` in `SharedArmorSystem.Wolfmed.cs:20`. |
| `IInventoryRelayEvent.TargetSlots` | SAME | WG `InventorySystem.Relay.cs:133-143` |
| `SlotFlags.WITHOUT_POCKET` | SAME concept, **DIFFERENT bit set** — WG adds `LEGS, WALLET, BALACLAVA, ARMBANDRIGHT, ARMBANDLEFT, HELMETCOVER, HELMETATTACHMENT` | WG `Content.Shared/Inventory/SlotFlags.cs:12-37` |
| `InventorySystem.TryGetContainingSlot(...)` | **PRESENT** (needed only for Option C) | WG `Content.Shared/Inventory/InventorySystem.Helpers.cs:41-50` |
| `SlotDefinition.SlotFlags` | PRESENT | WG `Content.Shared/Inventory/InventoryTemplatePrototype.cs:17,25` |
| `MaskComponent.IsToggled` | **PRESENT** but deliberately **not used** by WG armour | WG `Content.Shared/Clothing/Components/MaskComponent.cs:28` |
| `WoundHostComponent.LocalizedDamageTypes` | SAME (`Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic`) | WG `Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:35-44` |
| `traumaDeductions` | **MISSING in BOTH** — orphaned Onyx YAML key with no C# anywhere. **Do not port.** | `git grep -rn "TraumaDeduction" HEAD -- '*.cs'` → no output |

---

## 8. Duplicate directed-subscription audit

Phase 3's armour package registers **no new `SubscribeLocalEvent<TComp, TEvent>` pair at all.** It
edits the body of a handler that already exists.

| Pair | Existing subscriber(s) in WG | Verdict |
|---|---|---|
| `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` | `WolfmedPartArmorSystem.Initialize` (`_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:25`) — **sole** subscriber (repo-wide grep for `PartDamageModifyEvent` returns only `WoundEvents.cs:138`, `WoundDamageRoutingSystem.cs:741`, and this file) | ✔ do not re-register anywhere |
| `<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>` | `SharedArmorSystem.Initialize:24` | ✔ untouched |
| `<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>` | `SharedArmorSystem.Initialize:23` | ✔ untouched |
| `<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>` | `SharedArmorSystem.Initialize:25` | ✔ untouched |
| `<ArmorComponent, GetVerbsEvent<ExamineVerb>>` | `SharedArmorSystem.Initialize:26` | ✔ untouched |
| `<ArmorPlateProtectedComponent, BeforeDamageChangedEvent>` | `SharedArmorPlateSystem.Initialize:46` | ✔ untouched; routing's `before:` ordering (`WoundDamageRoutingSystem.cs:64,66`) stays as shipped |

If Option C is taken, `WolfmedPartArmorSystem` gains `[Dependency] private InventorySystem _inventory`
and `[Dependency] private IConfigurationManager _configuration` — dependencies, not subscriptions. No
`GotEquipped`/`GotUnequipped` caching system: resolve the slot on demand inside the handler. (A cache
would need two new directed pairs and would have to invalidate on container moves — not worth it for
a handler that runs once per limb hit.)

---

## 9. Ordered files, and difficulty

| # | File | Action | Lines | Difficulty |
|---|---|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/Armor/ArmorComponent.Wolfmed.cs` | **new** — partial `ArmorComponent` with `Coverage`, `CoverageSymmetry`, `PartModifiers`; `ArmorPartModifier` `[DataDefinition]` | ~50 | **Easy** |
| 2 | `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | **modify** — Onyx's `PartModifiers` loop (AP-wrapped) + `Covers()` gate; update `<remarks>` | ~35 changed | **Medium** (semantics, not code) |
| 3 | *(Option C only)* `Content.Shared/_WF/Wolfmed/Armor/WolfmedArmorCoverage.cs` | **new** — static `SlotFlags → HashSet<BodyPartType>` table + `SlotCoverage(EntityUid)` | ~45 | Easy |
| 4 | *(Option C only)* `Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs` *(or the existing wounds CCVar file)* | **modify** — `wounds.armor_slot_coverage`, default `false` | 3 | Easy |
| 5 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | **modify** — 4 new `[TestPrototypes]` entries, 3 ported tests, 2 new AP assertions, re-derive `AppliesArmorExactlyOnceTest`'s comment | ~140 added | **Medium** |
| 6 | *(Option B content pass)* a handful of `Resources/Prototypes/**/*.yml` armour bases + the `_Mono` bulletproof vests/helmets | **modify** — add `coverage:` | ~20 blocks | Easy but needs the Release YAML lint (memory: yaml-validation-workflow) |
| 7 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **append** rows for files 1–4 + a Deviations entry for nullable `Coverage`, the dropped mask gate, and the two upstream-red Onyx tests | — | Easy |
| 8 | `Docs/Wolfmed/WOLFMED_STATUS.md`, `Docs/Wolfmed/WOLFMED_PLAN3.md` | **modify / new** | — | Easy |

**Overall difficulty: LOW for the code, MEDIUM for the decision.** No upstream file is edited, no new
subscription is registered, no vendored `_Onyx` file changes, `RobustToolbox` untouched. The entire
risk is in §4.1.

Sequencing inside phase 3: this package is **independent of P3-1 (amputation) and P3-2 (organs)** —
it touches no file they touch. It shares `WoundDamageFoundationTest.cs` with P3-5's other assertions,
so run it in the same sequential slot as, or before, the test package (memory: packages run
sequentially in the one worktree).

---

## 10. Balance — the numbers the user must see (P3-6)

### 10.1 Where hits land

Two regimes, both verified:

**(a) Aimed — the normal case in Wolfgate gun PvP.** Projectile damage is dealt with
`origin: component.Shooter` (`WG Content.Shared/Projectiles/SharedProjectileSystem.cs:164-169`).
Routing resolves the part from the origin's `TargetingComponent`
(`WG WoundDamageRoutingSystem.cs:673`: `_targetResolver.TryResolve(body, targetingSource, out var targetedPart)`
→ `WoundTargetResolver.TryResolve:22-29` reads `targeting.Target`). **`TargetingComponent.Target`
defaults to `TargetBodyPart.Torso`** (`WG Content.Shared/_Shitmed/Targeting/TargetingComponent.cs:14`).
`targeting.enabled` defaults **true** (`WG Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs:7-8`).
→ **Every shot from a player who never touches the targeting doll hits the torso.**

**(b) Unaimed — no origin, or an origin with no `TargetingComponent`** (environmental damage, most
explosions, traps). Falls to `ResolveDamagePart(body, null)`
(`WG WoundDamageRoutingSystem.cs:475-510`), a weighted roll over **attached woundable parts**, weight
per part = `WoundHostComponent.TargetWeights[PartType]` (`:490`).

WG weights (`WG Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:18-29`):
`Torso 4, Head 1, Arm 2, Hand 1, Leg 2, Foot 1, Tail 1, Other 1`.
Human = 11 parts (`Resources/Prototypes/Body/Prototypes/human.yml:2-50`), total weight
`4 + 1 + 2·2 + 1·2 + 2·2 + 1·2 = 17`:

| Part | Weight | Share of an unaimed hit |
|---|---|---|
| Torso | 4 | **23.53 %** |
| Head | 1 | **5.88 %** |
| Each arm | 2 | 11.76 % (both: 23.53 %) |
| Each hand | 1 | 5.88 % (both: 11.76 %) |
| Each leg | 2 | 11.76 % (both: 23.53 %) |
| Each foot | 1 | 5.88 % (both: 11.76 %) |

### 10.2 What a helmet / vest is worth, per option

Real WG numbers:

| Item | Piercing coeff | Blunt | Slash | File |
|---|---|---|---|---|
| `ClothingHeadBPHelmetLight` | 0.85 (−15 %) | 0.85 | 0.85 | `_Mono/.../Head/Helmets/bulletproof_helmets.yml:13-18` |
| `ClothingHeadHelmetSwat` | 0.80 (−20 %) | 0.80 | 0.80 | `Entities/Clothing/Head/helmets.yml:79-82` |
| `ClothingHeadHelmetRiot` | 0.95 (−5 %) | 0.8 | 0.8 | `Entities/Clothing/Head/helmets.yml:122-124` |
| `ClothingOuterArmorBPVestLight` | 0.45 (−55 %) | 0.85 | 0.85 | `_Mono/.../OuterClothing/Armor/bulletproof_vests.yml:12-19` |
| `ClothingOuterArmorBPVestMedium` | 0.35 (−65 %) | 0.80 | 0.70 | same `:42-49` |
| `ClothingOuterArmorBPVestHeavy` | **0.25 (−75 %)** | 0.30 | 0.30 | same `:74-81` |
| `ClothingOuterArmorBPVestPolyvalent` | 0.55 (−45 %) | 0.60 | 0.60 | same `:107-114` |

**Heavy bulletproof vest (`Piercing 0.25`), against a random unaimed bullet:**

| Coverage rule | Fraction of hits protected | Average Piercing mitigation |
|---|---|---|
| Today / Option A / Option B-unannotated (protects everything) | 100 % | **75.0 %** |
| Option B/C, `[Torso]` only | 4/17 = 23.5 % | **17.6 %** |
| Option C table `[Torso, Arm, Leg, Tail, Other]` | 12/17 = 70.6 % | **52.9 %** |
| Onyx modsuit-chestplate style `[Torso, Groin→Torso, Arm, Hand, Leg, Foot, Tail, Other]` (everything but Head) | 16/17 = 94.1 % | **70.6 %** |

**Against an aimed torso shot (the default and overwhelmingly common case): all four rows are
identical — 75 %.** Locational armour does not nerf vests in normal PvP; it nerfs them against
splash, environmental damage and deliberate limb-shooting.

**Light ballistic helmet (`Piercing 0.85`) with `coverage: [Head]`:**
* aimed head shot: **15 % mitigation** (unchanged from today);
* random unaimed hit: `5.88 % × 15 % =` **0.88 % average mitigation** (down from 15 %);
* aimed torso shot: **0 %** (today: 15 %).

**Reading of that:** locational armour makes headgear near-useless *unless the attacker aims at the
head*, and makes aiming at the head the counter to body armour. That is the intended gun-PvP dynamic
in P3-3, and it only exists because the targeting doll is already wired
(`TargetingComponent`, `targeting.enabled = true`). If the user wants unaimed fire to also scatter,
that is `WoundTargetResolver`'s deliberately-skipped anatomical scatter
(`WoundTargetResolver.cs:12-14`: *"Phase 1 resolves the requested part exactly: Wolfgate's combat code
rolls its own inaccuracy before calling in"*) — a separate decision, not part of P3-3.

### 10.3 Stacking

Armour stacks **multiplicatively** across slots (the relay raises the same event on every equipped
item in `WITHOUT_POCKET`, each calling `ApplyModifierSet` on the running value). A heavy vest
(`Torso 0.25`) plus a light helmet (`Head 0.85`) under coverage gating gives 0.25 on the torso and
0.85 on the head, instead of today's 0.2125 on **both**. **Today a helmet silently multiplies into
your torso protection; after coverage gating it does not.** That is a real, if modest, across-the-board
nerf to stacked kit even in aimed torso combat: heavy vest + SWAT helmet + armoured jumpsuit currently
yields `0.25 × 0.80 × (jumpsuit)` on every part; under coverage it yields `0.25 × (jumpsuit)` on the
torso. **This is the one number that changes even under aimed-torso PvP, and it is the single most
important thing to playtest.**

### 10.4 Unchanged by this package

* Systemic damage (Poison, Asphyxiation, Radiation, Bloodloss, Cellular, …) — still armoured once,
  globally, by `ApplyWoundSystemicArmor` (`SharedArmorSystem.Wolfmed.cs:33-63`). Coverage does not
  apply. Matches Onyx.
* Armour penetration — still applied through `PenetrateArmor` on both branches (§4.3); T-AP stays the
  gate.
* `_Mono` armour plates — still whole-body, still absorb exactly once on the routed pass (§4.6).
* Non-wound-host entities — completely untouched.
* The armour examine verb still reports `component.Modifiers` with no mention of coverage or part
  profiles (`SharedArmorSystem.cs:61,71-99`). **After this package the examine text will overstate
  protection for any coverage-gated item.** Onyx has the identical gap. Cheap fix (append the covered
  parts to the `ArmorExamineEvent` markup) but it needs a locale string — recommend deferring to the
  content pass and recording it.

---

## 11. Traps that will silently ship something broken

1. **`[AutoNetworkedField]` on the new fields** → either a compile failure on
   `List<ArmorPartModifier>` or a needless net-state widening. Leave them un-networked (§4.2).
2. **Forgetting `PenetrateArmor` around `profile.Modifiers`** → every AP weapon stops working against
   any armour with a `partModifiers` entry. T-P3ARM-AP (§6.2) is the gate.
3. **Putting the coverage gate before the `PartModifiers` loop** → an explicit `partModifiers` entry
   for an uncovered part would be ignored, and `LocationalModifierOverridesAndFallbackTest` would go
   red on `head = 5` (its armour declares `coverage: [Chest]` only).
4. **`coverage: [Chest]` or `[Groin]` surviving into WG YAML** → YAML lint error (no such enum member).
   Every ported `coverage` list must be D9-mapped (§4.4), and `Chest` + `Groin` must collapse to a
   single `Torso` entry.
5. **Porting `traumaDeductions`** → hard YAML failure; the datafield does not exist in either tree.
6. **Adding the `PartDamageModifyEvent` subscription to `Content.Shared/Armor/SharedArmorSystem.cs`**
   (as Onyx does at `:30`) → duplicate directed subscription with `WolfmedPartArmorSystem.cs:25`,
   server crashes at start (memory: duplicate-directed-subscriptions). Keep it in `_WF`.
7. **Option C silently regressing two green tests** — `AppliesArmorExactlyOnceTest` (head through an
   outerClothing item) and `ArmorPenetrationReachesWoundHostsTest` (LeftArm through an outerClothing
   item). Whichever option ships, re-derive both by hand (§4.5).
8. **Assuming the Onyx tests pass upstream.** Two of the three do not (§3.5). If a WG implementation
   "fails to reproduce Onyx", check this table before changing the implementation.
9. **Empty vs unset `coverage`.** Under Onyx's non-nullable `= []`, "empty" means "all". Under the
   recommended nullable field, `null` means "unset" (→ slot-derived under Option C, all otherwise) and
   `coverage: []` means "explicitly all". Getting this backwards inverts every armour in the game.

---

## 12. Open questions for the user

1. **§4.1 — Option A, B, or C?** Recommendation: ship **B**, add **C** behind
   `wounds.armor_slot_coverage` defaulted `false`.
2. **§10.3 — is the stacking nerf acceptable?** Coverage gating removes helmet/glove multipliers from
   torso protection even in aimed PvP. This is the only change that touches the common case.
3. **§4.5 — `NECK` slot mapping.** No Onyx precedent. `Head + Torso`, or ungated?
4. **§5.3 — content scope.** Annotate only the `_Mono` bulletproof vests/helmets and the abstract
   armour bases (recommended), or all 272 entries?
5. **§10.4 — armour examine.** Accept that examine overstates protection for gated items in phase 3
   (Onyx-identical), or add the covered-parts line + locale string now?
6. **§4.6 — `_Mono` armour plates stay whole-body.** Confirm that is acceptable for phase 3.
