# Wolfmed phase 3 — `AmputationSystem` port plan (analyst report: "amputation")

**Scope:** port `ONYX Content.Shared/_Onyx/Wounds/AmputationSystem.cs` (212 lines) and re-enable the two D26
comment-outs in `WG Content.Server/_Onyx/Wounds/OrganDamageSystem.cs`.
**Sources re-verified against the current WG tree** (HEAD `1171e02fb6`, phases 1+2 committed) and ONYX pin
`2f5bab9`. `body-organ.md` §2.6 and `wounds-c.md` §5 were used as starting evidence; every row below was
re-checked in the real files and several of their claims are now stale (§9).

Abbreviations: **WG** = `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`,
**ONYX** = `C:/tmp/onyx` @ `2f5bab9`.

---

## 0. Executive summary (read this first)

1. **The compat layer is complete. Phase 3 needs no new `_WF` files for amputation.**
   `WolfmedBodySystem.TryDetachPart`, `WolfmedBodyPartSystem.Get`, `WolfmedDamageableSystem.GetAllDamage`,
   `SharedTargetingSystem.IsSelectable`, GUARD A/B/C, `WolfmedBodyPartLifecycleSystem` all exist and were read
   line by line. `WolfmedBodyPartComponent` **already carries all four amputation fields** (§1.2).
2. **The vendored file needs exactly 18 `// WOLFGATE` edit sites** (§1.4), not the "29 edits across 7 files"
   `body-organ.md` counts — that number was the whole 7-file field-migration, most of which landed in phases 1–2.
3. **Only one new directed subscription:** `<WoundableComponent, PartDamageOverflowedEvent>`. Verified free (§5).
4. **The overflow→amputation branch is dead code on the human profile, in Onyx too** (§2.1, §4.4). Every organic
   limb has `maxDamage: 0`; the only part with `maxDamage` is the Torso (250), and the Torso is explicitly
   excluded from amputation. Amputation is driven **entirely** by `HandlePartDamageApplied`'s threshold path.
   This contradicts the framing in the task brief and in `WOLFMED_MANIFEST.md:845-850`.
5. **Blocker-class finding (§2.5, B-1): decapitating a wound host can *reduce* its vital damage.**
   `CheckVitalDamage` sums only *attached* Head+Torso part damage plus systemic. Detaching a 200-damage head
   removes 200 from the sum and Shitmed's `PartRemoveDamage` adds only 100 Bloodloss — a net **−100**. A mob can
   be knocked out of crit by being decapitated. Needs a decision before phase 3 ships.
6. **Second blocker-class finding (§2.6, B-2): `AmputationConsequenceWound` is inert in Wolfgate.** In Onyx it
   blocks limb re-attachment through Nubody's `TryAttachPart`/`HasAmputationConsequence`; Shitmed has no such
   gate and D7 skips Onyx surgery, so the wound is a name and a bleed-free marker. Either hook Shitmed's
   `CanAttachPart` (and accept that phase 3 has no way to clear it) or record the loss.
7. **Explosion amputation requires a new upstream hook that PLAN.md §3 explicitly forbids** (`ExplosionSystem`
   is on the "Explicitly NOT hooked" list, D24). It also opens the Mono `DamageOriginFlag.Explosion`
   plate-protection hole (§3.3). Recommend deferring explosion amputation; the code paths are ported and inert.
8. **The numbers say no Wolfgate firearm will ever amputate anything** (§4.5). Baseline bullet is Piercing 14;
   Onyx's `DefaultDismembermentFinishingDamage["Piercing"] = 40`. Lasers deal Heat, which appears in no
   `amputationThresholds` dictionary at all. Amputation in Wolfgate = melee Slash and (if hooked) explosions.

---

## 1. Every external symbol, with WG status and the exact `// WOLFGATE` edit

### 1.1 The Onyx file's dependency block (`ONYX AmputationSystem.cs:1-23`)

| Onyx symbol | Onyx signature / site | WG status | Edit |
|---|---|---|---|
| `using System.Numerics;` | `:1`, `Vector2.UnitY` at `:125` | **SAME** | none |
| `using Content.Shared.Body.Part;` | `:2` — `BodyPartComponent`, `BodyPartType` | **SAME namespace** (`WG Content.Shared/Body/Part/BodyPartType.cs:4 namespace Content.Shared.Body.Part`) | none |
| `using Content.Shared.Body.Systems;` | `:3` — `SharedBodySystem` | **SAME** | none |
| `using Content.Shared.Damage;` | `:4` | **SAME** — and this is what resolves `DamageableComponent` in WG (`WG Content.Shared/Damage/Components/DamageableComponent.cs:9 namespace Content.Shared.Damage`, vs ONYX `…:11 namespace Content.Shared.Damage.Components`) | none |
| `using Content.Shared.Damage.Components;` | `:5` | namespace **exists** in WG (`PassiveDamageComponent`, `StaminaComponent`), resolves, unused for `DamageableComponent` | **keep verbatim** (PLAN §2.17 rule) |
| `using Content.Shared.Damage.Prototypes;` | `:6` — `DamageTypePrototype` | **SAME** | none |
| `using Content.Shared.Damage.Systems;` | `:7` — `DamageableSystem` | namespace exists in WG (`DamageContactsSystem`, `PassiveDamageSystem`); `DamageableSystem` itself is `namespace Content.Shared.Damage` (`WG Content.Shared/Damage/Systems/DamageableSystem.cs:25`) and comes from `:4` | keep verbatim |
| `using Content.Shared.FixedPoint;` | `:8` | **SAME** | none |
| `using Robust.Shared.Network;` / `Prototypes` / `Random` | `:9-11` | **SAME** | none |
| `using Content.Shared.Throwing;` | `:12` — `ThrowingSystem` | **SAME** (`WG Content.Shared/Throwing/ThrowingSystem.cs`) | none |
| `[Dependency] private SharedBodySystem _body` | `:18` | **SAME** — but its only use, `_body.TryDetachPart`, is MISSING; `_body` is still needed for `GetParentPartOrNull` | keep the field |
| `[Dependency] private DamageableSystem _damageable` | `:19` | **DIFFERENT.** ONYX `DamageableSystem.API.cs:422 public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent)`; WG's `DamageableSystem` has **no** `GetAllDamage` (`grep -c GetAllDamage` on `WG Content.Shared/Damage/Systems/DamageableSystem.cs` → **0**). | `[Dependency] private WolfmedDamageableSystem _damageable = default!; // WOLFGATE: D12` + `using Content.Shared._WF.Wolfmed.Compat;` |
| `[Dependency] private INetManager _net` | `:20` | **SAME** | none |
| `[Dependency] private IRobustRandom _random` | `:21` | **SAME** (`Prob(float)`) | none |
| `[Dependency] private WoundSystem _wounds` | `:22` | **SAME** — `WG Content.Shared/_Onyx/Wounds/WoundSystem.cs:166 public EntityUid? CreateOrMergeWound(Entity<WoundableComponent?> part, ProtoId<WoundPrototype> prototypeId, FixedPoint2 severity)` — byte-identical to Onyx | none |
| `[Dependency] private ThrowingSystem _throwing` | `:23` | **DIFFERENT return type, SAME call site.** ONYX `public bool TryThrow(EntityUid uid, Vector2 direction, float baseThrowSpeed = 10.0f, EntityUid? user = null, float pushbackRatio = PushbackDefault, float? friction = null, bool compensateFriction = false, bool recoil = true, bool animated = true, bool playSound = true, bool doSpin = true, ThrowingUnanchorStrength unanchor = ThrowingUnanchorStrength.None)`; WG `Content.Shared/Throwing/ThrowingSystem.cs:96-107` is the identical parameter list but **`public void`**. `:125` discards the result, so it compiles. | none |
| *(new)* `WolfmedBodyPartSystem _wfPart` | — | required by D8 | `[Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8` + `using Content.Shared._WF.Wolfmed.Body;` |
| *(new)* `WolfmedBodySystem _wfBody` | — | required for `TryDetachPart` | `[Dependency] private WolfmedBodySystem _wfBody = default!; // WOLFGATE: §2.7 compat` (same `Compat` using as `_damageable`) |

### 1.2 `WolfmedBodyPartComponent` — what it already carries

`WG Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartComponent.cs` (read in full, 29 lines):

| Field | Line | Type | Default | Needed by amputation |
|---|---|---|---|---|
| `FractureProfile` | `:13` | `ProtoId<FractureProfilePrototype>?` | `null` | no (fractures, phase 2) |
| `MaxDamage` | `:16` | `FixedPoint2` | `0` | yes — `:36`, `:44` |
| `AmputationThresholds` | `:19` | `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>` | empty | yes — `:74,84,91,103,158,188` |
| `DismembermentFinishingDamage` | `:22` | `Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>` | empty | yes — `:161` |
| `AmputationConsequenceSeverity` | `:25` | `FixedPoint2` | `35` | yes — `:64` |
| `DismembermentSeverity` | `:28` | `FixedPoint2?` | `null` | yes — `:123` |

**All four amputation fields are present. Nothing is missing from the component; no component change is needed
in phase 3.** Accessor: `WG Content.Shared/_WF/Wolfmed/Body/WolfmedBodyPartSystem.cs:9
public WolfmedBodyPartComponent Get(EntityUid part) => CompOrNull<WolfmedBodyPartComponent>(part) ?? None;`
(returns a shared zeroed singleton — never null, safe to chain).

What the YAML **does not** populate (§4.1): `dismembermentFinishingDamage`, `amputationConsequenceSeverity`,
`dismembermentSeverity` are unset on every WG part, exactly as in Onyx, so the C# defaults and the
`WoundHostComponent` per-part-type fallbacks are the live values.

### 1.3 Wound-set symbols (all phase-1 ports, all verified present)

| Symbol | WG site | Status |
|---|---|---|
| `WoundableComponent.Severable` | `WoundDamageComponents.cs:170` `[AutoNetworkedField] public bool Severable;` | **SAME** |
| `WoundableComponent.AmputationOverflow` | `:167` `[ViewVariables] public FixedPoint2 AmputationOverflow;` | **SAME** |
| `WoundHostComponent.DismembermentSeverities` | `:50-58` | **DIFFERENT (D9)** — Onyx's `Groin: 120` row deleted; Head 200 / Arm 120 / Leg 120 / Hand 80 / Foot 80 |
| `WoundHostComponent.DefaultDismembermentSeverity` | `:61` `= 100` | **SAME** |
| `WoundHostComponent.DismembermentWound` | `:64` `= "DismembermentWound"` | **SAME** |
| `WoundHostComponent.AmputationConsequenceWound` | `:67` `= "AmputationConsequenceWound"` | **SAME** |
| `WoundHostComponent.SeverableResetRatio` | `:70` `= 0.8f` | **SAME** |
| `WoundHostComponent.DefaultDismembermentFinishingDamage` | `:73-78` `Slash 15 / Piercing 40 / Blunt 50` | **SAME** |
| `PartDamageOverflowedEvent` | `WoundEvents.cs:62-67` `(EntityUid Body, EntityUid Part, DamageSpecifier Damage, bool IsExplosion = false, bool ExplosionAmputationCandidate = false)` | **SAME** |
| `PartDamageAppliedEvent` | `WoundEvents.cs:47-55` `(EntityUid Body, EntityUid Part, DamageSpecifier Damage, bool HealWounds = true, EntityUid? Origin = null, bool IsExplosion = false, bool ExplosionAmputationCandidate = false, float WoundSeverityMultiplier = 1f)` | **SAME** |
| `WoundSystem.CreateOrMergeWound` | `WoundSystem.cs:166` | **SAME** |
| Prototypes `DismembermentWound` / `AmputationConsequenceWound` | `Resources/Prototypes/_Onyx/Wounds/wounds.yml:385`, `:398`; both listed in `OrganicBodyPartProfile.supportedWounds` (`:22-23`) | **present** |
| Locale `wound-name-dismemberment` / `-amputation-consequence` | `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl:9,20` ("tissue rupture", "stump trauma") | **present** |

### 1.4 The exact edit table for `Content.Shared/_Onyx/Wounds/AmputationSystem.cs`

Onyx line numbers verified by `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/AmputationSystem.cs`.
`body-organ.md` §2.6's table is correct in substance; the version below is the re-verified, complete one.

| # | Onyx line | Before | After |
|---|---|---|---|
| 1 | `:19` | `[Dependency] private DamageableSystem _damageable = default!;` | `[Dependency] private WolfmedDamageableSystem _damageable = default!; // WOLFGATE: D12, GetAllDamage lives on the compat facade.` |
| 2 | after `:23` | — | `    [Dependency] private WolfmedBodyPartSystem _wfPart = default!; // WOLFGATE: D8`<br>`    [Dependency] private WolfmedBodySystem _wfBody = default!; // WOLFGATE: Onyx's SharedBodySystem.TryDetachPart` |
| 3 | after `:12` | — | `using Content.Shared._WF.Wolfmed.Body; // WOLFGATE: D8`<br>`using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12 + TryDetachPart` |
| 4 | `:35` | `bodyPart.PartType == BodyPartType.Chest \|\|` | `bodyPart.PartType == BodyPartType.Torso \|\| // WOLFGATE: D9, Wolfgate's chest is Torso` |
| 5 | `:36` | `bodyPart.MaxDamage <= FixedPoint2.Zero)` | `_wfPart.Get(part).MaxDamage <= FixedPoint2.Zero) // WOLFGATE: D8` |
| 6 | `:39` | `TryExplosionAmputate(args.Body, part, bodyPart, args.Damage)` | `TryExplosionAmputate(args.Body, part, args.Damage) // WOLFGATE: signature change, see #16` |
| 7 | `:44` | `if (part.Comp.AmputationOverflow >= bodyPart.MaxDamage)` | `if (part.Comp.AmputationOverflow >= _wfPart.Get(part).MaxDamage) // WOLFGATE: D8` |
| 8 | `:51` | `IsFinishingHit(args.Body, bodyPart, args.Damage)` | `IsFinishingHit(args.Body, part.Owner, args.Damage) // WOLFGATE: see #15` |
| 9 | `:64` | `parentPart.AmputationConsequenceSeverity` | `_wfPart.Get(parent).AmputationConsequenceSeverity // WOLFGATE: D8` — **keep** the `TryComp(parent, out BodyPartComponent? parentPart)` guard at `:58` (it still means "parent is a body part"); the local becomes unused, which is fine (same pattern the manifest records for `AccumulateAmputationOverflow`, `WOLFMED_MANIFEST.md:249-252`) |
| 10 | `:73` (a) | `bodyPart.PartType is BodyPartType.Chest \|\|` | `bodyPart.PartType is BodyPartType.Torso \|\| // WOLFGATE: D9` |
| 11 | `:73` (b) | `bodyPart.Parent == null \|\|` | `_body.GetParentPartOrNull(part) is null \|\| // WOLFGATE: Shitmed's BodyPartComponent has no Parent field` (`WG SharedBodySystem.Parts.cs:414 public EntityUid? GetParentPartOrNull(EntityUid uid)`) |
| 12 | `:74` | `bodyPart.AmputationThresholds.Count == 0 \|\|` | `_wfPart.Get(part).AmputationThresholds.Count == 0 \|\| // WOLFGATE: D8` |
| 13 | `:79` | `TryExplosionAmputate(args.Body, part, bodyPart, args.Damage, damage)` | `TryExplosionAmputate(args.Body, part, args.Damage, damage) // WOLFGATE` |
| 14 | `:84` | `ReachedThreshold(damage, bodyPart.AmputationThresholds)` | `ReachedThreshold(damage, _wfPart.Get(part).AmputationThresholds) // WOLFGATE: D8` |
| 15 | `:91` | `GetThresholdProgress(damage, bodyPart.AmputationThresholds)` | `GetThresholdProgress(damage, _wfPart.Get(part).AmputationThresholds) // WOLFGATE: D8` |
| 16 | `:103-104` | `ReachedThreshold(damageBeforeHit, bodyPart.AmputationThresholds) &&`<br>`IsFinishingHit(args.Body, bodyPart, args.Damage)` | `ReachedThreshold(damageBeforeHit, _wfPart.Get(part).AmputationThresholds) && // WOLFGATE: D8`<br>`IsFinishingHit(args.Body, part.Owner, args.Damage) // WOLFGATE` |
| 17 | `:114` | `bodyPart.PartType == BodyPartType.Chest)` | `bodyPart.PartType == BodyPartType.Torso) // WOLFGATE: D9` |
| 18 | `:117` | `var parent = bodyPart.Parent ?? part;` | `var parent = _body.GetParentPartOrNull(part) ?? part; // WOLFGATE` |
| 19 | `:118` | `if (!_body.TryDetachPart(part))` | `if (!_wfBody.TryDetachPart(part)) // WOLFGATE: §2.7 compat shim` |
| 20 | `:123` | `bodyPart.DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType)` | `_wfPart.Get(part).DismembermentSeverity ?? GetDismembermentSeverity(host, bodyPart.PartType) // WOLFGATE: D8` |
| 21 | `:151` | `private bool IsFinishingHit(EntityUid body, BodyPartComponent part, DamageSpecifier damage)` | `private bool IsFinishingHit(EntityUid body, EntityUid part, DamageSpecifier damage) // WOLFGATE: D8, Wolfmed data is resolved from the uid` + insert `var wf = _wfPart.Get(part); // WOLFGATE` as the first statement |
| 22 | `:158` | `!part.AmputationThresholds.ContainsKey(type)` | `!wf.AmputationThresholds.ContainsKey(type)` |
| 23 | `:161-162` | `part.DismembermentFinishingDamage.GetValueOrDefault(type, host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type))` | `wf.DismembermentFinishingDamage.GetValueOrDefault(type, host.DefaultDismembermentFinishingDamage.GetValueOrDefault(type))` |
| 24 | `:170-175` | `private bool TryExplosionAmputate(EntityUid body, Entity<WoundableComponent> part, BodyPartComponent bodyPart, DamageSpecifier hit, DamageSpecifier? totalDamage = null)` | drop the `BodyPartComponent bodyPart` parameter `// WOLFGATE` |
| 25 | `:177` | `if (!IsFinishingHit(body, bodyPart, hit))` | `if (!IsFinishingHit(body, part.Owner, hit))` |
| 26 | `:188` | `GetThresholdProgress(totalDamage, bodyPart.AmputationThresholds)` | `GetThresholdProgress(totalDamage, _wfPart.Get(part.Owner).AmputationThresholds) // WOLFGATE: D8` |

**18 distinct edit *sites*** (26 rows above, several sharing one line). Two are additive (usings, dependencies);
the rest are mechanical redirects. No logic is changed.

### 1.5 `DamageSpecifier.DamageDict` key type — the WP10-2 trap, analysed

`WG Content.Shared/Damage/DamageSpecifier.cs:44 public Dictionary<string, FixedPoint2> DamageDict { get; set; }`
(ONYX keys it by `ProtoId<DamageTypePrototype>`). The four call shapes in `AmputationSystem`:

| Site | Expression | Verdict |
|---|---|---|
| `:98-101` | `foreach (var (type, amount) in args.Damage.DamageDict)` then `damageBeforeHit.DamageDict[type] = …` | `type` is `string` on both sides — **compiles unchanged** |
| `:146` (`GetThresholdProgress`) | `damage.DamageDict.GetValueOrDefault(type)` with `type : ProtoId<DamageTypePrototype>` | generic inference fixes `TKey` from the **exact** bound on the receiver (`Dictionary<string, FixedPoint2>`) → `TKey = string`; `ProtoId<T>`'s `implicit operator string` then converts the argument. **Compiles unchanged** |
| `:158` | `wf.AmputationThresholds.ContainsKey(type)` with `type : string` | `Dictionary<ProtoId<…>, …>.ContainsKey(ProtoId<…>)` + `implicit operator ProtoId<T>(string)`. **Compiles unchanged** |
| `:161` | `wf.DismembermentFinishingDamage.GetValueOrDefault(type, …)` with `type : string` | same as above, exact bound is `ProtoId<…>`. **Compiles unchanged** |

**No `.Key.Id` access anywhere in this file**, so WP10-2's exact failure mode does not recur. Flag for the
implementer anyway: if any of the four sites produces `CS0411`/`CS0121`, the fix is an explicit
`new ProtoId<DamageTypePrototype>(type)` / `type.Id`, not a redesign.

### 1.6 `OrganDamageSystem` — the two D26 sites

`WG Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` (namespace is `Content.Shared._Onyx.Wounds`, so a shared
`AmputationSystem` in the same namespace needs **no** extra `using`):

```
:25  // WOLFGATE: D26, AmputationSystem is phase 3 and is not ported; the field would be CS0246.
:26  // TODO: phase 3 - [Dependency] private AmputationSystem _amputation = default!;
```
→ `    [Dependency] private AmputationSystem _amputation = default!;` (delete both comment lines)

```
:38      // WOLFGATE: D26, amputation is phase 3.
:39      // TODO: phase 3 - _amputation.HandlePartDamageApplied(part, ref args);
```
→ `        _amputation.HandlePartDamageApplied(part, ref args);` (delete both comment lines)

**Call order must stay `_wounds` → `_fractures` → `_amputation` → `_bleeding`** (`:36-40`), matching
ONYX `OrganDamageSystem.cs:34-37`. It matters: `_wounds.HandlePartDamageApplied` creates the Slash/Blunt wound
*before* amputation can detach the part, and `_fractures` must see the part still attached.

### 1.7 Where the file lives

**`Content.Shared/_Onyx/Wounds/AmputationSystem.cs`** — keep Onyx's path and namespace. It has **no** server-only
dependency (`SharedBodySystem`, `ThrowingSystem`, `WolfmedDamageableSystem`, `WoundSystem`, `WolfmedBodySystem`,
`WolfmedBodyPartSystem` are all shared), so D13's "move to server" does not apply. `OrganDamageSystem` is a
**server** system in the shared namespace and may depend on a shared system — verified: it already depends on
`WoundFractureSystem` (shared) at `:22`.

---

## 2. Traumatic amputation, hop by hop

### 2.1 Onyx's two entry paths, and which one is live

| Path | Trigger | Live for organic humanoids? |
|---|---|---|
| **A — threshold** `OrganDamageSystem` → `AmputationSystem.HandlePartDamageApplied` (`ONYX :67`) | any part hit | **YES** |
| **B — overflow** `WoundDamageRoutingSystem.AccumulateAmputationOverflow` → `PartDamageOverflowedEvent` → `OnPartDamageOverflowed` (`ONYX :31`) | part damage exceeds `MaxDamage` | **NO — dead** |

Why B is dead (verified in both trees):
- `AccumulateAmputationOverflow` early-returns when `MaxDamage <= 0` (`WG WoundDamageRoutingSystem.cs:799`,
  `ONYX :730`). Only the Torso has `maxDamage` (`ONYX Resources/Prototypes/_Onyx/Body/chest_groin.yml:19
  maxDamage: 250`; `WG Resources/Prototypes/_WF/Wolfmed/Body/parts.yml:15 maxDamage: 250`). Every limb abstract
  in both trees leaves it unset → `0`.
- `OnPartDamageOverflowed` returns immediately for `PartType == Chest`/`Torso` (`ONYX :35`).
- A full grep of the Onyx tree confirms `AmputationSystem` is the **only** subscriber of
  `PartDamageOverflowedEvent` (`git -C C:/tmp/onyx grep -n PartDamageOverflowedEvent HEAD -- '*.cs'`).

So on a human: the Torso accumulates `AmputationOverflow` past 250 and raises the event; the handler drops it on
the floor. Limbs never raise it at all. **Port the branch verbatim anyway** — it is live for Onyx animals
(`ONYX Resources/Prototypes/_Onyx/Body/Parts/animal.yml:11 maxDamage: 250` on `ChestAnimal`) and for any future
WG profile that sets `maxDamage` on a limb, and removing it would be a re-sync diff.

**Correction to the record:** `WOLFMED_MANIFEST.md:845-850` and PLAN §2.12 both say a missing `maxDamage` row
means "some limbs can never be severed once phase 3 lands `AmputationSystem`". That is wrong — `maxDamage`
gates only the dead overflow branch. `amputationThresholds` is what gates severing, and WP7 populated it on all
ten limb abstracts. No YAML gap exists.

### 2.2 The live flow, hop by hop (machete swing to a left arm)

| # | File:line | What happens |
|---|---|---|
| 1 | `SharedMeleeWeaponSystem` → `DamageableSystem.TryChangeDamage(body, Slash 32, …, targetPart: <doll target>)` | `WG Content.Shared/Damage/Systems/DamageableSystem.cs:194-200` |
| 2 | `DamageableSystem.cs:214-217` | `BeforeDamageChangedEvent` raised on the body, now carrying `ArmorPenetration`, `Tool`, `Applied` (GUARD D2, `:487-495`) |
| 3 | `WoundDamageRoutingSystem.cs:64` → `:69-112` | `OnBeforeDamageChanged` sets `args.Cancelled = true`, stashes `(ArmorPenetration, Tool, OriginFlag)` in `_routedModifiers` (`:79`), honours `args.TargetPart` when `SharedTargetingSystem.IsSelectable` (`:88-94`), then `RouteThroughBodyModifiers` |
| 4 | `:644` | `_routing.Add(body)` — from here the seam is re-entrant-safe |
| 5 | `:686-688` | re-entrant `_damage.ChangeDamage(body, …, armorPenetration, tool, originFlag)` → real `TryChangeDamage` |
| 6 | `DamageableSystem.cs:217` | `BeforeDamageChangedEvent` again — routing returns at `:73` (`_routing.Contains`), so `SharedArmorPlateSystem.OnBeforeDamageChanged` (`_Mono/ArmorPlate/SharedArmorPlateSystem.cs:49`) runs **exactly once** on the routed pass |
| 7 | `SharedBodySystem.Targeting.cs:114-118` | GUARD A — `TryChangePartDamageEvent` handler returns for wound hosts, so Shitmed does **not** spread damage to parts |
| 8 | `SharedArmorSystem` HOOK 10 | armours only the systemic types for a wound host |
| 9 | `DamageableSystem.cs:255-265` | **GUARD D** — `DamageDealtEvent` on the body |
| 10 | `WoundDamageRoutingSystem.cs:65` → `:126-134` | `OnDamageDealt` clones and clears the dict, calls `RouteAppliedDamage` |
| 11 | `:699-727` | systemic vs localized split; part resolution via `_requestedParts` or `ResolveDamagePart` |
| 12 | `:741-751` | `PartDamageModifyEvent` relayed to the wearer's inventory → `WolfmedPartArmorSystem.OnPartDamageModify` (`_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:30-34`) applies the worn armour's modifiers to the **part's** share |
| 13 | `:758-764` | `AccumulateAmputationOverflow` — no-op on a limb (`MaxDamage == 0`), so no `PartDamageOverflowedEvent` |
| 14 | `:772-778` | `_damage.TryChangeDamage(part, …, ignoreResistances: true, ignoreGlobalModifiers: true)` → `WolfmedDamageableSystem.ChangeDamage` forces **`canSever: false, canEvade: false`** (`Compat/WolfmedDamageableSystem.cs:42-53`) |
| 15 | `SharedBodySystem.Targeting.cs:223-249` | Shitmed's `OnDamageChanged` on the part: **GUARD B** (`:234 && !_queryWoundHost.HasComp(partEnt.Comp.Body)`) plus `args.CanSever == false` — sever suppressed twice over. `CheckBodyPart` (`:243`) still runs → `BodyPartEnableChangedEvent(false)` at 90 damage, targeting-doll colour update |
| 16 | `:782-786` | `PartDamageAppliedEvent` raised on the part |
| 17 | `OrganDamageSystem.cs:31` → `:34-40` | fan-out: `_wounds` (creates/merges the `SlashWound`) → `_fractures` → **`_amputation`** → `_bleeding` |
| 18 | `AmputationSystem.cs:67-106` | `HandlePartDamageApplied`: reads the part's **total** damage via `_damageable.GetAllDamage`, compares `GetThresholdProgress` to 1.0 and to `SeverableResetRatio` (0.8), reconstructs `damageBeforeHit`, requires `IsFinishingHit` |
| 19 | `AmputationSystem.cs:111-128` | `TryAmputate`: `parent = GetParentPartOrNull(part) ?? part` → `_wfBody.TryDetachPart(part)` |
| 20 | `Compat/WolfmedBodySystem.cs:13-25` | `GetParentPartAndSlotOrNull` (`SharedBodySystem.Parts.cs:430`) + `CanDetachPart` (`:733`) guard, then `AmputateAttemptEvent` raised on the part |
| 21 | `SharedBodySystem.Parts.cs:219-220` | `OnAmputateAttempt` → `DropPart` |
| 22 | `:201-217` | `DropPart`: `DropSlotContents` (drops held items / the limb's inventory slots), `BodyPartEnableChangedEvent(false)`, `BodyPartDroppedEvent` on the **body**, `AttachToGridOrMap`, `RandomOffset(0.5f)`. Gated on `_timing.IsFirstTimePredicted` — always true on the server |
| 23 | `:245-264` | the transform move triggers `EntRemovedFromContainerMessage` → `CheckBodyPart(..., severed: true)` → `RemovePart` → `RecursiveBodyUpdate(part, null)` (children keep riding the detached root; `RemovePartChildren` is **not** called here, so an arm takes its hand with it — matches Onyx) |
| 24 | `:357-358` | `BodyPartRemovedEvent` on the body → **two** subscribers: `SharedBodySystem.PartAppearance.cs:26 OnPartDroppedFromBody` (EnsureComp `BodyPartAppearanceComponent`, strip the humanoid layer) and `WolfmedBodyPartLifecycleSystem.cs:23 OnPartRemoved` |
| 25 | `WolfmedBodyPartLifecycleSystem.cs:48-65` | `_projection.OnPartRemoved(part, body)` → `RefreshDetachedDamage(part)` + `RefreshBodyDamage(body)`; `_bleeding.OnPartChanged(body)` → `RefreshBody` → `CirculatoryStreamSystem.SetBleedRates` → `BloodstreamSystem.TryModifyWoundBleedProjection` (GUARD E3, `BloodstreamSystem.cs:417`); then `OrganGotRemovedEvent` on the whole detached subtree |
| 26 | `:360-362` | `RemoveLeg`, `RemovePartEffect`, then **`PartRemoveDamage`** |
| 27 | `:395-408` | if the part `IsVital` **and** no other part of that type remains: `Damageable.TryChangeDamage(body, Bloodloss <VitalDamage>, partMultiplier: 0f)`. `VitalDamage` default is **100** (`Body/Part/BodyPartComponent.cs:39`); in WG only `BaseHead` sets `vital: true` (`Resources/Prototypes/Body/Parts/base.yml:93`) |
| 28 | back in `AmputationSystem.cs:120-122` | `_wounds.CreateOrMergeWound(parent, host.DismembermentWound, severity)` — severity = per-part override ?? `host.DismembermentSeverities[PartType]` ?? 100 |
| 29 | `:124` | `ApplyAmputationConsequences(body, parent)` → `CreateOrMergeWound(parent, "AmputationConsequenceWound", 35)` |
| 30 | `:125-126` | `_throwing.TryThrow(part, Vector2.UnitY, baseThrowSpeed: 3f, pushbackRatio: 0f, doSpin: true)` — parts inherit `BaseItem` (`Resources/Prototypes/Body/Parts/base.yml:8`) so they have Physics/Fixtures and the throw lands |
| 31 | back in `OrganDamageSystem.cs:40` | `_bleeding.HandlePartDamageApplied(part, ref args)` runs on the **now-detached** part — harmless (it only tries a cauterisation reduction) |
| 32 | back in `WoundDamageRoutingSystem.cs:791` | `_projection.RefreshBodyDamage(body)` → `_damage.SetDamage(body, total)` → real `DamageChangedEvent` → `MobThresholdSystem.CheckThresholds` (`Mobs/Systems/MobThresholdSystem.cs:341` uses `CheckVitalDamage`) |

### 2.3 Nested re-entrancy at hop 27 — verified safe

`PartRemoveDamage`'s `TryChangeDamage` happens **inside** the outer routed pass. Trace:
- `BeforeDamageChangedEvent` → `WoundDamageRoutingSystem.cs:73` `_routing.Contains(ent)` is **true** → returns
  without cancelling. Good: no double-cancel, no lost damage.
- GUARD A returns (wound host).
- GUARD D (`DamageableSystem.cs:258`) raises `DamageDealtEvent` → `OnDamageDealt` (`:128` also requires
  `_routing.Contains`, true) → `RouteAppliedDamage(body, Bloodloss 100, …)`.
- `Bloodloss` is **not** in `WoundHostComponent.LocalizedDamageTypes` (`WoundDamageComponents.cs:35-44`:
  Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic), so it goes systemic → `ApplySystemicDamage`
  (`:1048-1080`) → `SystemicDamageComponent.Damage["Bloodloss"] += 100` → pain on the systemic target →
  `RefreshBodyDamage`. `_projecting` guards re-entrancy (`WoundDamageProjectionSystem.cs:158`).
- `_requestedParts[body]` may still point at the just-detached part, but `localized` is empty so the part branch
  (`:729`) is never entered. Safe.
- **Side effect worth recording:** `AccumulateApplied` (`:1074`) folds the 100 Bloodloss into the **outer**
  `_appliedDelta[body]`, so `TryChangeDamage`'s `Applied` return (D27) for the machete swing will report
  `Slash 32 + Bloodloss 100`. Melee damage popups, hit logs and pierce-through logic read that value.
  Cosmetic, but it will look like a 132-damage swing in the admin log.

### 2.4 Double-apply audit — Shitmed cascade vs Onyx consequence wound

| Shitmed effect on detach | Onyx equivalent | Double-applies? |
|---|---|---|
| `DropSlotContents` — drops the limb's held item / inventory slots | none (Onyx uses `BodyInventorySlotSystem`, not ported) | **no** — Wolfgate-only, desirable |
| `BodyPartEnableChangedEvent(false)` (`Parts.cs:209`) | none | **no** — Onyx's equivalent is `BodyPartFunctionalitySystem`, and `wounds.body_part_functionality_enabled` is `false` (P2-3) |
| `BodyPartDroppedEvent` → appearance strip | Onyx `SharedVisualBodySystem` | **no** |
| `RemoveLeg` → `Standing.Down` when the last leg goes | Onyx `FractureEffectsSystem` mobility scaling | **no overlap** — different mechanism, both desirable |
| `CheckBodyPart(..., severed: true)` → targeting doll shows `Severed` | Onyx part-status readout (P2, `HealthExaminableSystem.PartStatus`) | **no** — two independent readouts, both correct |
| **`PartRemoveDamage` → Bloodloss 100 on the body** (vital head only) | Onyx `DismembermentWound` severity 200 on the parent + `WoundBleedingBehavior rate 0.2` | **partially.** Both represent "you are bleeding out from a missing head". Onyx's is a bleed *rate*; Shitmed's is flat systemic Bloodloss *damage*. They stack. This is the only genuine overlap and it is **load-bearing** — see B-1 |
| Shitmed sever-at-`SeverIntegrity` 130 (`Targeting.cs:239`, Mono raised 90→130) | Onyx amputation thresholds | **suppressed** by GUARD B (`:234`) **and** by `canSever: false` from the compat facade |
| Shitmed integrity regen (`ProcessIntegrityTick`) | Onyx `WoundHealingSystem` | **suppressed** by GUARD C (`:84`, `:108`) |

**Conclusion: nothing double-applies except the Bloodloss/DismembermentWound pair, and that one should be kept**
(see B-1 — it is the only thing preventing decapitation from being a net heal).

### 2.5 B-1 — decapitation can reduce vital damage (blocker-class)

`WG Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:25-52`:
```csharp
public FixedPoint2 CheckVitalDamage(EntityUid target, DamageableComponent damageableComponent)
{
    …
    foreach (var (part, partComponent) in _body.GetBodyChildren(target))
    {
        if (!TryComp(part, out DamageableComponent? partDamageable) ||
            !criticalParts.Contains(partComponent.PartType))   // Head, Torso
            continue;
        result += _damageable.GetTotalDamage((part, partDamageable));
    }
    if (TryComp(target, out SystemicDamageComponent? systemic))
        result += systemic.Damage.GetTotal();
    return result;
}
```
`GetBodyChildren` only walks **attached** parts. Decapitation therefore removes the head's accumulated damage
from the sum. The head's Slash threshold is 200, so at the moment of detachment the head carries ≥ 200 damage
(§4.2). `PartRemoveDamage` adds 100 Bloodloss. **Net change to `CheckVitalDamage`: −100 or worse.**

Consumers: `MobThresholdSystem.CheckThresholds` (`Mobs/Systems/MobThresholdSystem.cs:341`, HOOK 11),
`UpdateAlerts` (`:411`), `DefibrillatorSystem.cs:212` (HOOK 12), `WoundDamageRoutingSystem.TryApplyLethalDamage`
(`:528`, HOOK 13). So a mob that was in crit from head damage can be knocked **back to alive** by having its
head cut off, and the health alert will improve.

Onyx does not have this problem because Onyx's Nubody has no `PartRemoveDamage` equivalent and its dismemberment
severities (Head 200, `WoundDamageComponents.cs:52`) drive a bleed that kills over seconds — Onyx accepts
"decapitation is not instantly lethal, you bleed out". Wolfgate's `MaxBleedAmount` clamp makes that much slower
(§2.7).

**Options for the user (pick one before implementation):**
- **(a) Do nothing.** Ship Onyx behaviour; decapitation is a bleed-out, not an instant kill, and the vital-damage
  dip is a visible bug the first time a player sees it.
- **(b) Raise `BaseHead`'s `vitalDamage`** in `Resources/Prototypes/Body/Parts/base.yml` with a `# WOLFGATE`
  balance comment — 300 instead of 100 makes decapitation strictly lethal for a 200-threshold head. One-line
  YAML, no code. **Recommended.**
- **(c) Count detached vital parts in `CheckVitalDamage`** — add a `// WOLFGATE` branch that charges
  `DefaultDismembermentSeverity` per missing critical part. Touches a `_Onyx` vendored file, more re-sync cost.

### 2.6 B-2 — `AmputationConsequenceWound` has no consumer in Wolfgate (blocker-class)

In Onyx the consequence wound is what makes amputation *matter surgically*:
`ONYX Content.Shared/_Onyx/Body/Systems/SharedBodySystem.cs:209-223 HasAmputationConsequence(EntityUid part)`,
called from `TryAttachPart` at `:253` and `:389` — **you cannot reattach a limb while the stump carries the
wound**, and `SharedSurgerySystem.BodyParts.cs:148` is the surgical route that clears it.

Wolfgate: D7 keeps Shitmed surgery and does not port `_Onyx/Medical/Surgery`. Shitmed's attach choke point is
`WG Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:606-619 CanAttachPart(EntityUid parentId, string
slotId, EntityUid partId, …)` — both `AttachPart` overloads (`:652`, `:667`) funnel through it. There is **no**
consequence check. So after the port the wound will exist on the parent, bleed nothing (`wounds.yml:397-402`:
`damageTypes: {}`, no `behaviors`), show up in the P2 part-status examine, heal passively through
`WoundHealingSystem`, and otherwise do nothing.

**Options:**
- **(a) Record the loss, port the wound anyway.** Phase 4's wound surgeries reintroduce the gate. Zero upstream
  edits. **Recommended for phase 3** — hooking `CanAttachPart` in phase 3 would make reattachment permanently
  impossible until the wound decays, because no phase-3 mechanic clears it deliberately.
- **(b) Hook `CanAttachPart:606`** with one `// WOLFGATE` line calling a `_WF` helper. This is a **new** upstream
  hook not on PLAN.md §3's authorised list and needs escalation. Only do it together with a treatment path.

### 2.7 Bleeding after amputation — the WG clamp

`DismembermentWound` (`Resources/Prototypes/_Onyx/Wounds/wounds.yml:384-395`): `mergeMode: SeparateInstances`,
`maximumSeverity: 200`, `WoundBleedingBehavior rate 0.2, chance 1, awakeMultiplier 1.5, clottingMultiplier 4`.
Head dismemberment severity 200 → a raw rate of ~40, which is what Onyx's own test asserts
(`ONYX WoundBleedingTest.cs:197 BleedAmount, Is.GreaterThanOrEqualTo(40f)`).

**WG clamps it to 10.** `Content.Server/Body/Components/BloodstreamComponent.cs:57 MaxBleedAmount = 10.0f`, and
GUARD E3's shared implementation clamps at `BloodstreamSystem.cs:432
component.BleedAmount = Math.Clamp(component.BleedAmount, 0, component.MaxBleedAmount);`. Onyx's own default is
also 10 (`ONYX Content.Shared/Body/Components/BloodstreamComponent.cs:75`) — Onyx's test prototype must raise it.
**The ported `TraumaticAmputationCreatesSevereStumpBleedingTest` must assert `Is.EqualTo(10f)`
(or `Is.EqualTo(bloodstream.MaxBleedAmount)`), not `≥ 40f`.** Record as a corrected stale-literal deviation,
the same class as the ones WP10-1/WP10-6 already recorded.

### 2.8 Reattachment and the bleeding lifecycle — already correct

`WolfmedBodyPartLifecycleSystem.cs:27-45 OnPartAdded` calls `_projection.OnPartInserted` **and**
`_bleeding.OnPartInserted` for the whole reattached subtree, and re-raises `OrganGotInsertedEvent`. The phase-1
test `WoundBleedingTest.ProjectsTreatsAndTracksAttachmentTest` (`:87-90`) already proves detach→`BleedAmount 1.5`
→ `AttachPart(torso, "head", head)`→`1.875`. **Phase 3 adds nothing here.** PLAN §8.3 trap 2 is closed.

One residual: `WolfmedBodySystem.TryDetachPart` guards on `CanDetachPart` (`Parts.cs:733-746`), which requires
`part.PartType == parentSlotData.Type` and `Containers.CanRemove`. If anything has already mangled the slot,
`TryAmputate` silently returns false and the part stays on at ≥ threshold damage, permanently `Severable`.
Worth one assertion in the test.

---

## 3. Explosion amputation

### 3.1 How Onyx rolls it

- `ONYX Content.Server/Explosion/EntitySystems/ExplosionSystem.CVars.cs:16-17,30-31` adds two properties and two
  `Subs.CVar` lines for `CCVars.ExplosionLimbDamageVariation` and `CCVars.ExplosionWoundMultiplier`.
- `ONYX ExplosionSystem.Processing.cs:461-471` replaces the plain damage call:
  ```csharp
  if (!_woundDamageRouting.TryRouteDistributedDamage(entity, damage, TargetBodyPart.All,
          DamageDistribution.SplitWithVariation, ignoreResistances: true, interruptsDoAfters: false,
          variation: LimbDamageVariation, isExplosion: true,
          woundSeverityMultiplier: WoundMultiplier))
  {
      _damageableSystem.ChangeDamage((entity, damageable), damage);
  }
  ```
- `WoundDamageRoutingSystem.TryApplyDistributedDamage` (`WG :251-345`) splits the blast across every attached
  woundable part by `TargetWeights`, multiplied by `_random.NextFloat() * variation + 1f` (`:292`), then
  `PickExplosionAmputationCandidate(parts, shares)` (`:943-977`) weighted-rolls **one** part to be the
  amputation candidate (skipping Torso, parentless parts, and parts with no `amputationThresholds`).
- The candidate's `PartDamageAppliedEvent`/`PartDamageOverflowedEvent` carries
  `ExplosionAmputationCandidate = true` (`:784`, `:762`), and `TryExplosionAmputate` (`ONYX :170-191`) rolls
  `chance = clamp(GetThresholdProgress(totalDamage, thresholds) * 0.5f, 0, 1)` — but **only after**
  `IsFinishingHit` passes on the blast share.

**`DamageOriginFlag` is not involved on Onyx's side at all** — Onyx has no such enum (it is Mono's).

### 3.2 What WG's `ExplosionSystem` passes today

`WG Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs:471-473`:
```csharp
_damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true,
// Mono: Explosion flag for plate protection
originFlag: DamageableSystem.DamageOriginFlag.Explosion);
```
`origin` is **null**; the flag is the only signal. `WG ExplosionSystem.CVars.cs` is byte-identical to Onyx's
minus the two Onyx lines, so the CVar half of the hook is a clean 4-line addition.
`CCVars.ExplosionLimbDamageVariation` (2f) and `CCVars.ExplosionWoundMultiplier` (4f) already exist in WG
(`Content.Shared/_Onyx/CCVar/CCVars.Wounds.cs:22-26`) with **zero consumers** — verified by grep.

### 3.3 The Mono plate-protection gap (PLAN.md line 882)

`WG Content.Shared/_Mono/ArmorPlate/SharedArmorPlateSystem.cs:60`:
```csharp
if (args.Origin == null && args.OriginFlag != DamageableSystem.DamageOriginFlag.Explosion)
    return;
```
Today a wound host survives this correctly: routing cancels the first pass, and on the **re-entrant** pass
(`WoundDamageRoutingSystem.cs:686-688`) it replays `originFlag: modifiers.OriginFlag` from the `_routedModifiers`
side table (`:54`, `:79`) — so the plate sees `Explosion` and absorbs. Verified end to end.

`TryApplyDistributedDamage` / `TryRouteDistributedDamage` **never populate `_routedModifiers`** (grep: the
dictionary is written only at `:79`). The moment the explosion hook lands, `RouteThroughBodyModifiers` replays
`originFlag: null` with `origin: null`, `SharedArmorPlateSystem` returns at `:60`, and **armour plates stop
protecting wound hosts from explosions entirely** — a Wolfgate-specific combat regression Onyx cannot see.

Fix, if the hook is authorised: add a `// WOLFGATE` optional
`DamageableSystem.DamageOriginFlag? originFlag = null` parameter to both distributed entry points
(`:251-261`, `:904-914`) and seed `_routedModifiers[body] = (0f, null, originFlag)` before the loop at `:322`,
clearing it in the existing `finally` at `:339-344`. ~6 lines in one vendored file, no upstream change beyond
the ExplosionSystem hook itself.

### 3.4 Recommendation

**Defer explosion amputation out of phase 3.** Reasons:
1. `ExplosionSystem` is on PLAN.md §3's *Explicitly NOT hooked* list (D24). Hooking it needs a new authorisation.
2. It is the only phase-3 item that can regress non-wound combat (plates).
3. The code is inert without it: `_explosionDamage`, `_explosionAmputationCandidates`,
   `PickExplosionAmputationCandidate`, `TryExplosionAmputate` and both CVars already ship and simply never fire.
4. The arithmetic barely works anyway (§4.6): the blast share per part must individually clear
   `Blunt 50` / `Piercing 40` to count as a finishing hit, which at `Default`'s `5 Blunt / 5 Piercing` per
   intensity needs an intensity around 25–30 landing on one limb.

If the user wants it in phase 3, the package is: 4 lines in `ExplosionSystem.CVars.cs`, ~7 lines in
`ExplosionSystem.Processing.cs`, ~6 lines of `// WOLFGATE` in `WoundDamageRoutingSystem.cs` for the origin-flag
passthrough, plus a plate-absorbs-under-explosion regression test.

---

## 4. The exact numbers (P3-6)

### 4.1 Per-part data, Onyx source vs what WG ships

`amputationThresholds` live on `ONYX Resources/Prototypes/Body/base_organs.yml` (human limbs) and
`ONYX Resources/Prototypes/_Onyx/Body/chest_groin.yml` (chest/groin). WG's phase-1 copy is
`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`, attached via one-line `parent:` edits in
`Resources/Prototypes/Body/Parts/base.yml:81,137,156,176,191,206,226,246,261` and
`Resources/Prototypes/_Shitmed/Body/Parts/base.yml:50`.

| Part | Onyx `amputationThresholds` | Onyx src | WG `parts.yml` | Match |
|---|---|---|---|---|
| Torso (Onyx Chest) | *none*; `maxDamage: 250` | `chest_groin.yml:19` | `:15 maxDamage: 250` | **yes** |
| *(Onyx Groin)* | Slash 220 / Piercing 250 / Blunt 400 | `chest_groin.yml:46-49` | **dropped (D9)** | n/a |
| Head | Slash **200** / Piercing **200** / Blunt **350** | `base_organs.yml:96-99` | `:23-26` | **yes** |
| Arm L/R | Slash **130** / Piercing **250** / Blunt **250** | `base_organs.yml:146-149`, `:189-192` | `:34-37`, `:45` | **yes** |
| Hand L/R | Slash **70** / Piercing **200** / Blunt **150** | `base_organs.yml:231-234`, `:275-278` | `:53-56`, `:64` | **yes** |
| Leg L/R | Slash **150** / Piercing **250** / Blunt **300** | `base_organs.yml:320-323`, `:363-366` | `:72-75`, `:83` | **yes** |
| Foot L/R | Slash **80** / Piercing **220** / Blunt **170** | `base_organs.yml:405-408`, `:445-448` | `:91-94`, `:102` | **yes** |

**No YAML change is needed for phase 3.** Phase 1 already landed every threshold, verbatim.

`dismembermentFinishingDamage`, `amputationConsequenceSeverity` and `dismembermentSeverity` are **unset on every
part in both trees** (`git -C C:/tmp/onyx grep -n 'dismembermentFinishingDamage\|amputationConsequenceSeverity\|dismembermentSeverity' HEAD -- Resources/Prototypes` → **zero hits**). The live values are therefore the
C# defaults:

| Value | Source | Number |
|---|---|---|
| `DismembermentFinishingDamage` (fallback) | `WoundDamageComponents.cs:73-78` | **Slash 15, Piercing 40, Blunt 50** |
| `AmputationConsequenceSeverity` | `WolfmedBodyPartComponent.cs:25` | **35** |
| `DismembermentSeverity` (per part) | `WolfmedBodyPartComponent.cs:28` | `null` → host table |
| `DismembermentSeverities` (host) | `WoundDamageComponents.cs:50-58` | **Head 200, Arm 120, Leg 120, Hand 80, Foot 80** |
| `DefaultDismembermentSeverity` | `WoundDamageComponents.cs:61` | **100** (Torso, Tail, Other) |
| `SeverableResetRatio` | `WoundDamageComponents.cs:70` | **0.8** |
| Explosion sever chance | `ONYX AmputationSystem.cs:188` | `clamp(progress × 0.5, 0, 1)` |
| `explosion.damage_variation` | `CCVars.Wounds.cs:23` | **2.0** (weight × 1.0–3.0) |
| `explosion.wounding_multiplier` | `CCVars.Wounds.cs:26` | **4.0** |

### 4.2 The severing rule, stated exactly

For a part with thresholds `T[type]`:
`progress = Σ_type damage[type] / T[type]` (`ONYX :136-148`).
1. Not `Severable` and `progress ≥ 1` → set `Severable`, **return** (this hit does not detach).
2. `Severable` and `progress < 0.8` → clear `Severable`, return.
3. `Severable`, `progress ≥ 0.8`, **pre-hit** progress ≥ 1, and the incoming hit contains a type present in
   `T` with `amount ≥ finishingMinimum[type]` → **detach**.

So with uniform hits of `d` of one type against threshold `T`: **`ceil(T/d) + 1` hits**.

### 4.3 Proposed YAML block for WG human parts

**None required.** `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` is already complete and correct
(§4.1). If the user wants to tune after playtest, the file to edit is that one; the shape is:

```yaml
- type: entity
  id: WolfmedBaseLeftArm
  abstract: true
  components:
  - type: WolfmedBodyPart
    fractureProfile: OrganicFractureProfile
    amputationThresholds: &wolfmedArmThresholds
      Slash: 130
      Piercing: 250
      Blunt: 250
    # optional phase-3 tuning knobs, all currently unset (C# defaults apply):
    # dismembermentFinishingDamage: { Slash: 15, Piercing: 40, Blunt: 50 }
    # amputationConsequenceSeverity: 35
    # dismembermentSeverity: 120
```
The only thing worth *considering* adding is `maxDamage` on limbs — that is the switch that turns the dead
overflow branch on (§2.1). **Do not add it without a deliberate balance decision:** it also caps how much damage
a limb can hold, which changes `CheckVitalDamage` and the part-status readout.

### 4.4 Overflow numbers (for completeness)

Torso `maxDamage: 250`. `AccumulateAmputationOverflow` (`WoundDamageRoutingSystem.cs:794-849`) diverts any damage
past 250 into `WoundableComponent.AmputationOverflow` and raises `PartDamageOverflowedEvent`.
`AmputationSystem.OnPartDamageOverflowed` discards it because `PartType == Torso`. Net effect on a human: **the
torso's `DamageableComponent` is hard-capped at 250 and the excess is accounted but unused.** That cap is
already live in phase 1/2 — phase 3 changes nothing about it.

### 4.5 Lethality against real Wolfgate weapons

Weapon damage read from WG prototypes:

| Weapon | WG file:line | Damage |
|---|---|---|
| `BaseBullet` (every ballistic round) | `Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml:106` | **Piercing 14** |
| `BaseBulletAP` | `:191` | Piercing 11, `ignoreResistances: true` |
| `BulletLaser` | `:1186` | **Heat 16** (Mono) |
| `EnergySword` (active) | `Melee/e_sword.yml:124` | **Slash 50** |
| `Machete` / large melee baseline | `Melee/sword.yml:121,132` | **Slash 32** ("base for all large sized melee weapons" — Mono) |
| `Katana`/`Claymore` tier | `Melee/sword.yml:101,156` | Slash 40 |
| `CombatKnife` | `Melee/knife.yml:109` | **Slash 22** |
| `FireAxe` | `Melee/fireaxe.yml:32` | Slash 25 |
| `Default` explosion | `Resources/Prototypes/explosion.yml:5-10` | Heat 5 / Blunt 5 / Piercing 5 / Structural 20 **per intensity** |

Hits to amputate, unarmoured, all landing on the same targeted part:

| Part (Slash T) | Energy sword (50) | Katana (40) | Machete (32) | Combat knife (22) | Any bullet (Piercing 14) | Laser (Heat 16) |
|---|---|---|---|---|---|---|
| Hand — 70 | **3** | 3 | **4** | 5 | **never** | **never** |
| Foot — 80 | **3** | 3 | **4** | 5 | never | never |
| Arm — 130 | **4** | 5 | **6** | 7 | never | never |
| Leg — 150 | **4** | 5 | **6** | 8 | never | never |
| Head — 200 | **5** | 6 | **8** | 11 | never | never |

**Why bullets can never amputate — two independent reasons.**
1. `DefaultDismembermentFinishingDamage["Piercing"] = 40` (`WoundDamageComponents.cs:76`). A 14-damage round is
   never a finishing hit, at any accumulated damage, forever.
2. Even ignoring that, the Piercing thresholds are 200–250, i.e. 15–18 rounds into one limb.

**Why lasers can never amputate.** `Heat` appears in **no** `amputationThresholds` dictionary
(`GetThresholdProgress` iterates the threshold dict, not the damage dict), so `progress` stays 0 and
`IsFinishingHit` skips every type not in `AmputationThresholds` (`ONYX :158`).

**Armour scales all of this.** `WolfmedPartArmorSystem` (`_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs:30-34`)
applies the worn armour's modifier set to the part's share. A vest with a 0.7 Slash coefficient turns a machete
from 32 to 22.4 — still above the Slash-15 finishing minimum, but hits-to-arm go from 6 to 8 on an arm. A vest
at ≤ 0.46 Slash pushes a machete below the finishing minimum and makes that limb **unamputatable by machete**.

**For the user, the one-line summary:** *with Onyx defaults, amputation in Wolfgate is a melee-only mechanic.
Three to eight deliberate slashes to the same limb take it off; no gun in the game can do it at any range or
volume.* If that is not the intended feel, the knob to turn is
`WoundHostComponent.DefaultDismembermentFinishingDamage["Piercing"]` (40 → ~12–14) plus the Piercing thresholds
in `parts.yml` (200–250 → ~60–120). That is a D4 balance change and should be a separate, explicit decision.

### 4.6 Explosion arithmetic (if §3 is authorised)

The blast's `damage` is split across all attached woundable parts by `TargetWeights`
(`WoundDamageComponents.cs:18-29`: Torso 4, Head 1, Arm 2, Hand 1, Leg 2, Foot 1, Tail 1, Other 1 — 13 units for
a stock human with Torso/Head/2 Arms/2 Hands/2 Legs/2 Feet = 4+1+4+2+4+2 = 17 weight units), then multiplied by
`rand(1.0, 3.0)`. An arm's expected share is `2/17 ≈ 12 %`, up to ~35 % with maximum variation.
For that share to be a finishing hit it needs **Blunt ≥ 50 or Piercing ≥ 40**, i.e. a blast total of roughly
**Blunt 145–420** → intensity **29–84** at `Default`'s 5 Blunt/intensity. Only large ordnance reaches that.
Then the roll is `clamp(progress × 0.5, 0, 1)`, where `progress` is computed on the post-hit total — for an arm
at Blunt 250 threshold taking a Blunt 50 share cold, `progress = 0.2` → 10 % chance. **Explosion amputation with
stock numbers is rare, which matches Onyx's intent.**

---

## 5. Directed subscriptions and duplicate audit

### 5.1 Pairs phase-3 amputation registers

| # | Component | Event | Registrant | Existing WG owner | Free? |
|---|---|---|---|---|---|
| 1 | `WoundableComponent` | `PartDamageOverflowedEvent` | `AmputationSystem.Initialize():28` | **none** | **yes** |

Evidence: a full grep of `SubscribeLocalEvent<WoundableComponent`, `<WoundHostComponent`, `<BodyPartComponent`
across `WG Content.{Shared,Server,Client}` returns 34 lines; `PartDamageOverflowedEvent` appears in none of them.
The event's only other appearances in WG are its declaration (`WoundEvents.cs:62`) and its raise site
(`WoundDamageRoutingSystem.cs:761-763`).

`WoundableComponent`'s currently-claimed events, for the next package's convenience:
`ComponentInit` + `RejuvenateEvent` (`WoundSystem.cs:33,36`), `DamageChangedEvent`
(`WoundDamageProjectionSystem.cs:38`), `BeforeDamageChangedEvent` (`WoundDamageRoutingSystem.cs:66`),
`PartDamageAppliedEvent` (`OrganDamageSystem.cs:31`), `Wound{Created,Changed,StateChanged,Removed}Event`
(`WoundStatusEffectSystem.cs:31-34`), `OrganGot{Inserted,Removed}Event` + `BodyPartFunctionalityChangedEvent`
(`FractureEffectsSystem.cs:34-36`). **Plus `PartDamageOverflowedEvent` after phase 3.**

### 5.2 Pairs phase 3 must NOT register

| Pair | Why |
|---|---|
| `<WoundableComponent, PartDamageAppliedEvent>` | **Owned by `OrganDamageSystem.cs:31`**, which is the single fan-out point. `AmputationSystem.HandlePartDamageApplied` is a plain public method called from `:39`, never a subscription. Converting it is a server-start crash (`EntityEventBus.Directed.cs:407`). |
| `<BodyPartComponent, AmputateAttemptEvent>` | Owned by `SharedBodySystem.Parts.cs:41`. The compat shim *raises* it, never subscribes. |
| `<WoundHostComponent, BodyPartRemovedEvent>` | Already owned by `WolfmedBodyPartLifecycleSystem.cs:23`. Do not add an amputation-specific handler; extend that one if needed. |

### 5.3 Component registration names

Phase 3 amputation registers **no new component**. `AmputationSystem` as a type name is free:
`grep -rn "AmputationSystem" WG/Content.{Shared,Server,Client} --include=*.cs` returns only the two D26 comment
lines in `OrganDamageSystem.cs:25-26`.

### 5.4 Ordering

`AmputationSystem` needs **no** `before:`/`after:` on its one subscription. `PartDamageOverflowedEvent` has a
single subscriber, and the deterministic order that matters (wounds → fractures → amputation → bleeding) is
enforced by `OrganDamageSystem`'s explicit call sequence, not by the bus.

---

## 6. Prediction and server gating

- `AmputationSystem` is `Content.Shared` but **fully server-gated**: `_net.IsServer` at `ONYX :33` (overflow),
  `:57` (`ApplyAmputationConsequences`), `:68` (`HandlePartDamageApplied`), `:113` (`TryAmputate`). The client
  instantiates it and it does nothing. This matches D35 (unpredicted wound-host damage, phase 1).
- `WoundableComponent.Severable` is `[AutoNetworkedField]` (`WoundDamageComponents.cs:169-170`) and `SetSeverable`
  dirties it (`ONYX :205`), so the client sees the "this limb is about to come off" state. Nothing reads it
  client-side today — a phase-4 HUD cue is the obvious consumer.
- `WoundableComponent.AmputationOverflow` is `[ViewVariables]` only — server-side, correct.
- `DropPart` is gated on `_timing.IsFirstTimePredicted` (`Parts.cs:207`); always true on the server, so the shim
  never silently no-ops. The detach itself is a container/transform change, replicated normally, so the client
  sees the limb fly off one tick late. Acceptable and identical to Shitmed's existing sever behaviour.
- `ThrowingSystem.TryThrow` is shared and unpredicted here (server-only caller) — the thrown limb's arc will pop
  in on the client. Same as every other server-side throw in Wolfgate.
- **Do not add `[Dependency] INetManager` checks anywhere new.** Every guard Onyx needs is already in the file.

---

## 7. Ordered file list and difficulty

| # | File | Action | Lines touched | Difficulty | Notes |
|---|---|---|---|---|---|
| 1 | `Content.Shared/_Onyx/Wounds/AmputationSystem.cs` | **new (vendored)** | 212 + 18 marked edit sites | **medium** | §1.4. Keep Onyx's header and namespace verbatim. |
| 2 | `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` | modify | 4 lines deleted, 2 restored | **trivial** | §1.6. Two D26 comment-outs. |
| 3 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundBleedingTest.cs` | modify | +~30 | **low** | Un-skip `TraumaticAmputationCreatesSevereStumpBleedingTest` (the skip note is at `:150-153`). **Change the `≥ 40f` assertion to `MaxBleedAmount` (10)** — §2.7. `BodyPartType.Chest`→`Torso`. |
| 4 | `Content.IntegrationTests/Tests/_Onyx/Wounds/AmputationConsequenceTest.cs` | **new** | ~200 | **medium** | Port 4 of Onyx's 5 tests. `TraumaticAmputationCreatesBlockingConsequenceTest` must drop the `HasAmputationConsequence` / `TryAttachPart(…) Is.False` assertions (B-2) and assert the wound's presence + severity 35 instead. `SurgicalHealRemovesConsequenceAndUnblocksTest` **cannot be ported** (needs `SurgeryStepEvent` + `SurgeryTreatWoundEffect`, D7/phase 4) — record the skip. Port `HealingPartAboveThresholdDoesNotAmputateTest` and `HealingBelowResetRatioClearsSeverableTest` verbatim (pure routing + thresholds). Rebuild the test prototypes on Shitmed's body graph, the way `WoundBleedingTest.cs:27-49` does — Onyx's `InitialBody`/`TransplantCompatibility` shape does not exist in WG. |
| 5 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modify | +6 rows, 1 amended, new `### WP11-x` deviation block | **trivial** | Amend `:80` (AmputationSystem "skipped"→"adapted") and `:85` (OrganDamageSystem D26 note), `:132` (bleeding test), `:845-850` (the `maxDamage` hazard is **wrong**, §2.1). |
| 6 | `Docs/Wolfmed/WOLFMED_STATUS.md` | modify | ~4 lines | **trivial** | `:90`. |
| 7 | *(optional, B-1 option b)* `Resources/Prototypes/Body/Parts/base.yml` | modify | 1 line | **trivial** | `vitalDamage: 300` on `BaseHead` with a `# WOLFGATE` balance comment. **Needs the user's answer.** |
| 8 | *(deferred, §3)* `Content.Server/Explosion/EntitySystems/ExplosionSystem.CVars.cs` + `.Processing.cs` + `WoundDamageRoutingSystem.cs` origin-flag passthrough | modify | ~17 | **medium** | **Not authorised by PLAN §3.** Needs escalation + a plate regression test. |

**Overall difficulty: medium.** The file itself is mechanical (the compat layer absorbed every hard part in
phases 1–2). The risk is entirely in the two behavioural findings (B-1, B-2) and in the test prototypes.

---

## 8. Acceptance checks for the implementing package

1. Server starts with no `Duplicate Subscriptions` throw (the one new pair, §5.1).
2. `WoundBleedingTest` + `AmputationConsequenceTest` green; `WoundDamageFoundationTest`, `WoundHealingTest`,
   `WoundFractureTest` still green (the `OrganDamageSystem` fan-out order changed).
3. Machete × 4 to a targeted hand detaches it; the hand is on the floor with its wounds
   (`RefreshDetachedDamage`), the arm carries a `DismembermentWound` severity 80 and an
   `AmputationConsequenceWound` severity 35, and `BloodstreamComponent.BleedAmount > 0`.
4. A bullet-only volley (Piercing 14 × 20) into one hand detaches **nothing** — this is the expected result,
   not a bug (§4.5). Assert it, so the balance decision is visible in CI.
5. Healing a `Severable` part below 0.8 × threshold clears `Severable`
   (`HealingBelowResetRatioClearsSeverableTest`).
6. A reattached limb rejoins the body's bleed total (already covered by
   `WoundBleedingTest.ProjectsTreatsAndTracksAttachmentTest`).
7. **B-1 regression check:** record `CheckVitalDamage` before and after decapitation in a test and assert the
   direction the user chose in §2.5.

---

## 9. Where the starting evidence is now stale

| Claim | Source | Status |
|---|---|---|
| "29 edits across 7 files" for the `BodyPartComponent` field migration | `body-organ.md:1074-1075` | **stale** — 5 of the 7 files were done in phases 1–2. Phase 3 adds 18 sites in one file. |
| "`TryDetachPart` MISSING — needs a shim" | `wounds-c.md:40`, §5.3-A | **resolved in WP2** — `Compat/WolfmedBodySystem.cs:13`. Note the shipped shim uses `AmputateAttemptEvent`→`DropPart`, **not** `DetachPart` as `wounds-c.md` §5.3-A(a) proposed; the `CanDetachPart` guard was kept. |
| "`GetAllDamage` MISSING — extension shim" | `wounds-c.md` §5.3-B | **resolved in WP2** as an instance method on `WolfmedDamageableSystem` (`:125`), not an extension. |
| "six missing part fields" | `wounds-c.md:750-753` | **resolved in WP4** — all six are on `WolfmedBodyPartComponent`. |
| `IsFinishingHit(body, bodyPart, …)` keeps its `BodyPartComponent` parameter | `wounds-c.md` (implicit) | **must change** to `EntityUid` — the Wolfmed data is not on `BodyPartComponent` (`body-organ.md` §2.6 got this right). |
| "Every limb abstract needs a `maxDamage` row or some limbs can never be severed" | `WOLFMED_MANIFEST.md:845-850`, PLAN §2.12 | **wrong** — §2.1. `maxDamage` gates the dead overflow branch; `amputationThresholds` gates severing. Onyx itself ships limbs with no `maxDamage`. Amend the manifest. |
| Onyx's stump-bleed test asserts `≥ 40f` | `ONYX WoundBleedingTest.cs:197` | **cannot hold in WG** — `MaxBleedAmount = 10` clamp (§2.7). |
| `AmputationConsequenceWound` blocks reattachment | Onyx behaviour | **lost in WG** (B-2) — Shitmed has no `HasAmputationConsequence` gate. |

---

## 10. Decisions needed from the user before the package starts

| # | Question | Recommendation |
|---|---|---|
| **P3-Q1** | B-1: decapitation currently *reduces* `CheckVitalDamage` by ~100. Accept (a), raise `BaseHead.vitalDamage` to 300 (b), or charge missing vital parts in `CheckVitalDamage` (c)? | **(b)** — one YAML line, no vendored-file churn. |
| **P3-Q2** | B-2: `AmputationConsequenceWound` is inert without Onyx surgery. Record the loss (a) or add a new upstream hook on `SharedBodySystem.Parts.cs:606 CanAttachPart` (b)? | **(a)** for phase 3; (b) belongs with phase 4's wound surgeries, which provide the way to clear it. |
| **P3-Q3** | Explosion amputation needs a hook on `ExplosionSystem`, which PLAN §3/D24 forbids, plus a `_routedModifiers` origin-flag passthrough to keep Mono armour plates working. In or out of phase 3? | **Out.** Ship it with the phase-4/5 balance pass, together with the plate regression test. |
| **P3-Q4** | §4.5: with Onyx defaults, **no Wolfgate firearm can ever amputate**, and lasers are excluded entirely. Is that the intended feel for a gun-PvP server, or should `DefaultDismembermentFinishingDamage["Piercing"]` and the Piercing thresholds be tuned now? | Ship Onyx defaults (D4), add the "bullets do not amputate" assertion to CI, and revisit after playtest. |
