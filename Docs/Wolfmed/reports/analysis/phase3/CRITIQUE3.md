# CRITIQUE3 — completeness critique of `p3/PLAN3.md`

**Reviewer role:** completeness critic. **Method:** read `DECISIONS.md`, `PLAN.md §1-3/§5`, `p2/PLAN2.md §1-3/§5`,
`p3/PLAN3.md` in full, and all five phase-3 analyst reports; then re-verified every load-bearing claim by
reading the real files in **WG** (`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`)
and **ONYX** (`git -C C:/tmp/onyx show HEAD:<path>`). Read-only; nothing was modified.

**Overall:** PLAN3 is unusually well-evidenced. Of ~60 spot-checks, **56 reproduced exactly** (line numbers,
signatures, prototype values, arithmetic). The findings below are the gaps: **1 blocker, 4 major, 10 minor.**

---

## 0. Verification results that held (so nobody re-checks them)

| Claim | Verdict |
|---|---|
| `PartDamageVisualsComponent` is `AutoGenerateComponentState(raiseAfterAutoHandleState: true)` → subscription #3 actually fires | **TRUE** — `WG/Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:95-100` |
| `BodyPartComponent` is **not** flagged → Option B needs HOOK 21 | **TRUE** — `WG/Content.Shared/Body/Part/BodyPartComponent.cs:18`; `grep <BodyPartComponent, AfterAutoHandleStateEvent>` → **0 subscribers** |
| `DamageVisualsSystem` is already `sealed partial` (no upstream `partial` keyword edit needed) | **TRUE** — `WG/Content.Client/Damage/DamageVisualsSystem.cs:27` |
| HOOK 20 line numbers (Initialize after `:35`; `HandleDamage` `UpdateDisabledLayers` `:362`, `CheckOverlayOrdering` `:365`) | **TRUE** — `:35`, `:355`, `:362`, `:365` |
| Onyx's `UpdatePartDamageVisuals` passes `lastThreshold: FixedPoint2.Zero` and discards the bool, so the per-**group** `LastThresholdPerGroup` cache is never consulted per-layer | **TRUE** — `ONYX DamageVisualsSystem.cs:533-551`; WG's `LastThresholdPerGroup` is `Dictionary<string, FixedPoint2>` (`DamageVisualsComponent.cs:124`). **No per-layer state bug** |
| `ArmorComponent` is `sealed partial`, no `[Access]` → a `_WF` partial is legal | **TRUE** — `WG/Content.Shared/Armor/ArmorComponent.cs:12-13` |
| Onyx's shipped `OnPartDamageModify` never reads `Coverage`/`CoverageSymmetry` (so P3-D5 Option B is a deliberate deviation, and 2 of Onyx's 3 tests are red upstream) | **TRUE** — `ONYX SharedArmorSystem.cs:130-140` + `<Onyx-ArmorGlobalProtection-edited>` at `:146-150`; `ArmorComponent.Locational.cs:13,19` |
| Not one WG prototype declares an `Armor` `coverage:` (Option B is a content-gated no-op) | **TRUE** — the only `coverage:` hits are `ClothingComponent` (`glasses.yml`, `bandanas.yml`, `masks.yml`), a different component |
| `_Mono` vest coefficients and block line numbers (`:12,42,74,107,137`) | **TRUE**, all five re-read: Light `0.85/0.85/0.45`, Medium `0.8/0.7/0.35`, Heavy `0.3/0.3/0.25`, Polyvalent `0.6/0.6/0.55`, Stabproof `0.65/0.45/0.7` (Blunt/Slash/Piercing) |
| `OrganicBodyPartProfile.supportedWounds` contains `DismembermentWound`, `AmputationConsequenceWound`, `InternalBleedingWound` — so `WoundSystem.CanCreateWound` will not silently swallow them | **TRUE** — `WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:14-26`. (This was the highest-risk unchecked assumption in the plan; it holds.) |
| Both consequence prototypes are `mergeMode: SeparateInstances`; `CreateOrMergeWoundInternal` only searches under `MergeByPrototype` | **TRUE** — `wounds.yml:384-403`, `WoundSystem.cs:166-221` (`:185`) |
| `human.yml` organ ids and `parent:` line numbers `:53,103,150,189,215,249,270` | **TRUE**, all seven re-read |
| `grep "type: OrganDamage\|type: WolfmedOrgan" Resources/Prototypes` → zero hits (organ half is inert today) | **TRUE** |
| `ThrowingSystem.TryThrow` is `public void` with `baseThrowSpeed`/`pushbackRatio`/`doSpin` named params | **TRUE** — `WG/Content.Shared/Throwing/ThrowingSystem.cs:96-107` |
| `WolfmedDamageableSystem.GetAllDamage(Entity<DamageableComponent?>)` at `:125`; `WolfmedBodySystem.TryDetachPart` at `:13`; `WolfmedBodyPartSystem.Get` at `:9`; all four amputation fields on `WolfmedBodyPartComponent:16,19,22,25,28` | **TRUE**, all read in full |
| `CheckVitalDamage` sums only attached Head+Torso plus systemic (the B-1 premise) | **TRUE** — `WG/Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs:25-52` |
| GUARD B suppresses Shitmed sever for wound hosts → no double detach | **TRUE** — `WG/Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs:234` |
| `PartDamageOverflowedEvent` / `OrganFunctionChangedEvent` / `<PartDamageVisualsComponent, AfterAutoHandleStateEvent>` / `<BodyPartComponent, AfterAutoHandleStateEvent>` all have **zero** existing subscribers | **TRUE**, all four re-grepped repo-wide |
| `AmputationSystem`, `WolfmedOrganConsequenceSystem`, `ArmorPartModifier` are free type names | **TRUE**, all three re-grepped |
| `TryRouteDistributedDamage` has **no non-test caller** in the tree → explosion amputation genuinely ships inert (P3-D3) | **TRUE** — only three test call sites |
| `GetMatchingParts` is an exact bitmask match, so a `TargetBodyPart.LeftArm` mask yields exactly one part → T-AMP-EXPLOSION really is deterministic | **TRUE** — `WoundTargetResolver.cs:74-88` (no hand/foot expansion) |
| Weapon numbers in §8.2 (`BaseBullet` Piercing 14, `BaseBulletAP` 11, `BulletLaser` Heat 16, EnergySword 50, Katana 40, Machete 32, CombatKnife 22) and all 30 hits-to-amputate cells | **TRUE** — prototypes re-read; `ceil(T/d)+1` recomputed cell by cell, all 30 correct |
| `_WF/Wolfmed/Body/parts.yml` thresholds (Head 200/200/350, Arm 130/250/250, Hand 70/200/150, Leg 150/250/300, Foot 80/220/170, Torso `maxDamage: 250` only) | **TRUE**, file read in full |
| Finishing minimums Slash 15 / Piercing 40 / Blunt 50; `AmputationConsequenceSeverity` 35; `SeverableResetRatio` 0.8; dismemberment severities Head 200 / Arm 120 / Leg 120 / Hand 80 / Foot 80 | **TRUE** — `WoundDamageComponents.cs:50-78`, `WolfmedBodyPartComponent.cs:25` |
| `MaxHealth × MaxDamageFraction = 15 × 0.3 = 4.5` → 4 applications per organ | **TRUE** — `OrganDamageComponent.cs:22`, `OrganDamageSystem.cs:75-76` |
| Manifest rows `:80, :85, :89, :90, :91, :122, :132, :395-397, :794-796, :845-850` | **TRUE**, all re-read (one stale sub-claim, minor M-6 below) |

---

## 1. BLOCKER

### B3-1 — HOOK 19 (`BaseHead.vitalDamage: 300`) is a **D2 breach**: it changes behaviour for every non-wound-host in the game with a `BaseHead`-derived head

**Severity: blocker** (contradicts a binding DECISIONS entry, and the escalation in §8.6 does not disclose it).

**Evidence.**

* `BaseHead` is the **only** `vital: true` part in the repo:
  `grep -rn "vital: true\|vitalDamage" WG/Resources/Prototypes` → exactly one hit,
  `Resources/Prototypes/Body/Parts/base.yml:93`. PLAN3 states this correctly.
* But `BaseHead` is a **shared** abstract. Twenty-six head prototypes inherit it, and a large share belong to
  entities that are **not** wound hosts under D32 (`WoundHost` is on `BaseMobSpeciesOrganic` only, and
  Protogen is stripped at runtime by `WolfmedWoundHostExclusionSystem`):

  ```
  _Mono/Body/Parts/protogen.yml:41            parent: [PartProtogen, BaseHead]      # WoundHost explicitly removed (D32)
  _Shitmed/Body/Parts/animal.yml:3            parent: [PartAnimalBase, BaseHead]    # animals are not wound hosts
  _Shitmed/Body/Parts/Animal/kobold.yml:34
  _Shitmed/Body/Parts/Animal/monkey.yml:34
  _NF/Body/Parts/goblin_parts.yml:38
  Body/Parts/skeleton.yml:33 / silicon.yml:88
  _EinsteinEngines/Body/Parts/ipc.yml:53
  _Mono/Body/Parts/chimera.yml:30
  ```
* `VitalDamage` has exactly one consumer, and it is **not** wound-host-gated:
  `WG/Content.Shared/Body/Systems/SharedBodySystem.Parts.cs:396-408`
  ```csharp
  private void PartRemoveDamage(Entity<BodyComponent?> bodyEnt, Entity<BodyPartComponent> partEnt)
  {
      …
      if (!_timing.ApplyingState && partEnt.Comp.IsVital
          && !GetBodyChildrenOfType(bodyEnt, partEnt.Comp.PartType, bodyEnt.Comp).Any())
      {
          var damage = new DamageSpecifier(Prototypes.Index<DamageTypePrototype>("Bloodloss"), partEnt.Comp.VitalDamage);
          Damageable.TryChangeDamage(bodyEnt, damage, partMultiplier: 0f);
      }
  }
  ```
  It runs from `DropPart`, which every head-removal path reaches: Shitmed's sever
  (`SharedBodySystem.Targeting.cs:223-247`, `severIntegrity: 400` on `BaseHead`), Shitmed surgery, gibbing, and
  `AmputateAttemptEvent` (`SharedBodySystem.Parts.cs:219-220`). **GUARD B only gates the sever *decision*
  (`Targeting.cs:234`), never `PartRemoveDamage` itself.**
* Net effect for a non-wound-host: de-heading a Protogen / monkey / kobold / goblin deals **300** systemic
  Bloodloss instead of **100**. For a 100-crit / 200-dead humanoid that converts "crit, then bleed out" into
  "dead on the spot".

**Why this is not already covered.** DECISIONS D2: *"Entities without `WoundHostComponent` behave exactly as
today."* PLAN3 §1.1 restates it as *"Every phase-3 change must be `HasComp<WoundHostComponent>`-scoped or
structurally unreachable for non-hosts."* HOOK 19 is neither. §8.6 decision 3 asks the user only about the
*balance* value ("take it, or accept that decapitation briefly improves the health readout?") and never says the
change also lands on ten non-host species and every `BaseHead` animal.

**Concrete fix (pick one).**

1. **Preferred — take the alternative PLAN3 rejected (P3-D1 note, option (c)):** charge missing critical parts
   inside `MobThresholdSystem.CheckVitalDamage`. That file **already early-returns for non-hosts**
   (`MobThresholdSystem.cs:27-30`: `if (!HasComp<WoundHostComponent>(target) || …) return _damageable.GetTotalDamage(...)`),
   so the fix is structurally unreachable for non-hosts and is D2-clean by construction. PLAN3 rejected it for
   "edits a vendored `_Onyx` file for a Wolfgate-only problem" — but the file is already a Wolfgate-authored
   partial full of `// WOLFGATE` edits (`:4, :10, :18, :35`), so the re-sync cost argument is weak against a
   binding-decision breach.
2. **Or** keep the YAML value and add a one-line `// WOLFGATE` guard at `PartRemoveDamage`
   (`Parts.cs:400`) so only `HasComp<WoundHostComponent>(bodyEnt)` bodies see the raised figure.
3. **Or** escalate explicitly: restate §8.6 decision 3 as *"raise it, accepting that de-heading every non-host
   organic — Protogen, monkeys, kobolds, goblins, chimera — also goes from 100 to 300 Bloodloss"*, and get a
   written D2 waiver before WP11-1 starts.

**Note also:** moving HOOK 19 onto `WolfmedBaseHead` (`Resources/Prototypes/_WF/Wolfmed/Body/parts.yml:17-19`,
already `BaseHead`'s parent per `base.yml:81`) would remove the upstream edit, but **does not fix this** — the
inheritance set is identical. Do not mistake it for a fix.

---

## 2. MAJOR

### M3-1 — P3-4 Option A makes **hand and foot wounds invisible**, a regression from today's behaviour, recorded nowhere

**Evidence.** Today the stock overlay drives all six `targetLayers` (`Species/base.yml:61-75`: Chest, Head,
LArm, LLeg, RArm, RLeg) off the mob's **aggregate** `DamagePerGroup` (`DamageVisualsSystem.cs:383`), and by D30
that aggregate is the sum of **all** parts including hands and feet
(`WoundDamageProjectionSystem.RefreshBodyDamage:156-197` walks every `GetBodyChildren`). So a hand injury
currently lights every limb layer. After HOOK 20, `UpdatePartDamageVisuals` reads
`PartDamageVisualsComponent.Damage[layer]`, and `TryGetVisualLayer` (`WoundDamageProjectionSystem.cs:240-262`)
maps hands to `HumanoidVisualLayers.LHand/RHand` and feet to `LFoot/RFoot` — **neither of which is in
`TargetLayerMapKeys`**, so those entries are read by nothing. Verified the art side too:
`ls WG/Resources/Textures/Mobs/Effects/brute_damage.rsi` = exactly 36 states, `{Chest,Head,LArm,LLeg,RArm,RLeg}_Brute_{10,20,30,50,70,100}` —
**no hand or foot states exist in WG at all.**

Onyx has the identical six-layer list, so this is Onyx-faithful; but PLAN3 sells P3-4 as *"a fix rather than a
new feature"* (P3-D30) and the §7.3 deviations block records only the *severed-limb* loss (item 17), not the
attached hand/foot loss. A player will slash someone's hands to ribbons and see a pristine sprite.

**Fix (cheapest first).** In `UpdatePartDamageVisuals`, fold `LHand→LArm`, `RHand→RArm`, `LFoot→LLeg`,
`RFoot→RLeg` before the lookup (≈4 lines, preserves today's "hand damage shows on the arm" behaviour exactly and
needs no art) — **or** record it as §7.3 deviation 18 with the user-visible wording. Adding the four layers to
PROTO C is *not* an option under Option A: WG has no art for them; only Option B's Onyx RSIs carry
`LHand/RHand/LFoot/RFoot` prefixes (`ONYX DamageVisualsSystem.cs:99-117`).

### M3-2 — Nothing in the phase-3 test plan can catch §8.7 risk 7 (parent-vs-part `AmputationConsequenceSeverity`)

**Evidence.** §8.7 item 7 names it as a silent-ship risk: *"Redirecting `AmputationConsequenceSeverity` to the
severed part instead of the parent → the consequence wound lands at the wrong severity, and the bug is invisible
while both default to 35."* `WolfmedBodyPartComponent.cs:25` is `AmputationConsequenceSeverity = 35` and
`WolfmedBodyPartSystem.cs:6` returns a zeroed singleton whose value is *also* 35. **No phase-3 fixture overrides
it**: `parts.yml` (read in full) sets it on no part, and T-AMP-CONSEQUENCE-1's own row explicitly reasons that
"a test torso without the component behaves identically". So every phase-3 assertion is `35`, whether edit #9
reads the parent or the part. The plan flags the risk and then leaves it ungated.

**Fix.** Give the WP11-5 fixture's **torso** (the parent) a distinct value —
`- type: WolfmedBodyPart` / `amputationConsequenceSeverity: 50` — and assert **50** in
T-AMP-CONSEQUENCE-1 and T-AMP-CONSEQUENCE-SEPARATE. One fixture line, one literal; turns a named silent-ship
risk into a red test.

### M3-3 — Option B's rote port does not compile: `HumanoidVisualLayers.Groin` does not exist in WG

**Evidence.** `ONYX Content.Client/Damage/DamageVisualsSystem.cs:99-117 TryGetDetachedDamagePrefix` contains
`HumanoidVisualLayers.Groin => "Groin",`. WG's enum has no such member
(`grep -n "Groin" WG/Content.Shared/Humanoid/HumanoidVisualLayers.cs` → nothing; only `Chest` at `:15`).
PLAN3 forbids the Groin **`targetLayers`** line in PROTO C (§3, §8.8) but never says the switch arm inside the
ported method must go — and §2.3 lists `TryGetDetachedDamagePrefix` as a method to port. A rote port is a hard
`CS0117` in `Content.Client`. Same class of D9 omission as the `coverage: [Chest]` trap the plan *does* call out.

**Fix.** Add a row to §2.3: *"delete the `Groin` arm from `TryGetDetachedDamagePrefix` (D9). The Onyx RSIs ship
`Groin_*` states that WG will simply never request."*

### M3-4 — T-AMP-OVERFLOW (a) is unreachable as specified

**Evidence.** §6.2 T-AMP-OVERFLOW (a): *"Real `MobHuman`: drive an arm to 500 Blunt … and the arm still
amputates via the threshold path once a Blunt ≥ 50 finishing hit lands."* The arm's Blunt threshold is **250**
(`_WF/Wolfmed/Body/parts.yml:72-75`) and `DefaultDismembermentFinishingDamage["Blunt"]` is **50**
(`WoundDamageComponents.cs:76`). Any hit sequence of ≥50-Blunt chunks satisfies both `ReachedThreshold(damageBeforeHit)`
and `IsFinishingHit` on the **first hit after cumulative 250** (`ONYX AmputationSystem.cs:103-105`) — so the arm
detaches at ~300 and can never reach 500 while attached; after detaching, `bodyPart.Body == null` short-circuits
`HandlePartDamageApplied` at `:72` and the `AmputationOverflow == 0` assertion stops meaning anything.

**Fix.** Specify the accumulation in **sub-50** chunks: `20 × Spec("Blunt", 25)` → 500 total, `Severable` from
hit 10, never a finishing hit, `AmputationOverflow == 0` throughout; **then** one `Spec("Blunt", 50)` to prove
the threshold path still detaches. Put the "<50 is deliberate" reasoning in the test comment.

---

## 3. MINOR

| # | Finding | Evidence | Fix |
|---|---|---|---|
| **M-1** | §2.2's `WolfmedOrganConsequenceSystem` snippet does not compile — it calls `HasComp<OrganComponent>(ent)` but has no `using Content.Shared.Body.Organ;`. Its three usings are `_Onyx.Body`, `_Shitmed.Body.Organ`, `_WF.Wolfmed.Body`. | `OrganComponent` namespace confirmed by `WG/Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs:2` ("Wolfgate keeps OrganComponent in Content.Shared.Body.Organ") | add the using |
| **M-2** | WP11-3 declares **no** `[TestPrototypes]` ids, though §6.2 introduces four (`WoundFoundationArmorHead`, `WoundFoundationArmorAllHead`, `WoundFoundationArmorLeftArm`, `WoundFoundationArmorLocational`). §4 serialisation rule 3 requires every WP to list them. | I re-grepped the pool: all four are free. Note Onyx's own id is `WoundFoundationArmorAll` (`ONYX WoundDamageFoundationTest.cs:97`), not `…AllHead` — deliberate rename, worth stating so the port isn't "corrected" back | add the four ids to WP11-3 |
| **M-3** | §2.2 raises `OrganEnableChangedEvent(false)`; one tick later `OrganHealthSystem.DestroyOrgan` → `SharedBodySystem.RemoveOrgan` raises **the same event again** (`SharedBodySystem.Organs.cs:65-66`), so `OrganEffectSystem.OnOrganComponentsModify` runs `RemoveComponents` twice for the same `onAdd:` grants. Idempotent in practice, but §2.2's "covers only the one-tick window" framing omits it — and T-ORG-FUNC deliberately does **not** tick, so nothing exercises the double path. | `Organs.cs:56-79`, `:273-290`; `_Shitmed/BodyEffects/OrganEffectSystem.cs:47-68` | one sentence in §2.2; optionally let T-ORG-FUNC tick once and re-assert |
| **M-4** | Amputating a **head** inflates the value `TryChangeDamage` returns for the killing swing. `PartRemoveDamage`'s Bloodloss runs re-entrantly inside the routed pass and `ApplySystemicDamage` folds it into the outer `Applied` delta (`WoundDamageRoutingSystem.cs:1074 AccumulateApplied(body, applied)`), so `SharedMeleeWeaponSystem.cs:583,597` logs `damageResult.GetTotal()` as **Slash 32 + Bloodloss 100**. P3-D1 makes that **+300**. `amputation.md §2.3` records it; PLAN3 drops it. | verified end to end | §7.3 deviation; log-only impact (the Blunt-keyed stamina branch at `:588` is unaffected) |
| **M-5** | PROTO A makes `OrganHealthSystem.Update`'s per-tick `EntityQueryEnumerator<WolfmedOrganComponent, OrganComponent>()` non-empty **for the first time**, and it will enumerate organs on **non-hosts** too — `Body/Organs/rat.yml:3`, `_NF/Body/Organs/goblin_organs.yml`, and every `_Shitmed`/`_Mono` cybernetic organ parented to `OrganHuman*`. No behaviour change (health never drops off-host, so D2 holds), but an unmeasured per-tick cost the plan never mentions. | `OrganHealthSystem.cs:33-58`; PLAN3 §4 WP11-2 "inheritance side effects" lists the prototypes but only for the balance angle | one line in the WP11-2 checkpoint: sample the tick cost on a populated round, or add a cheap `Health > 0` component-less fast path |
| **M-6** | §7.1's `:85` row **appends** to a manifest note whose line numbers are already wrong: it says the disabled sites are `:24` and `:36`; the real sites are `:25-26` and `:38-39` (PLAN3 §4 uses the correct ones). WP11-6 will leave a self-contradicting row. | `WOLFMED_MANIFEST.md:85` vs `Content.Server/_Onyx/Wounds/OrganDamageSystem.cs:25-26,38-39` | make §7.1's `:85` row a *correction*, not an append |
| **M-7** | §8.3 prices vest coverage only for **aimed torso** and **unaimed** hits. It never states that an **aimed hand or foot shot now bypasses an annotated vest entirely** — hands and feet are selectable doll targets, and `WoundTargetResolver.TryResolveAvailable:52-54` falls Hand→Arm only when the hand is *missing*. "Aim at the hands" becomes a vest-bypass tactic that did not exist before. | `_Shitmed/Targeting/TargetBodyPart.cs`; `WoundTargetResolver.cs:42-57` | add the row to §8.3 and to user decision 2 |
| **M-8** | HOOK 20's early `return` skips `DamageVisualizerKeys.ForceUpdate` → `ForceUpdateLayers` and the `TrackAllDamage` branch (`DamageVisualsSystem.cs:367-380`) for every wound host. Onyx does the same (`ONYX :498-505`), and the stock human sets neither key, so it is inert today — but it is an unstated behavioural narrowing that will bite any future species prototype that sets `trackAllDamage`. | verified both files | one comment line at the hook |
| **M-9** | DECISIONS P3-1 says *"`WolfmedBodyPartComponent` gets the amputation fields (`AmputationThresholds`, `DismembermentFinishingDamage`, `AmputationConsequenceSeverity`, `DismembermentSeverity`) **populated for human parts**"*. P3-D13 ships **three of the four unset**, relying on C# defaults (`parts.yml` read in full: only `amputationThresholds` and `maxDamage` appear). Equivalent in effect and correct per D4, but it is a literal deviation from binding text with no manifest row. | `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` | one deviation row: "three of P3-1's four fields are left at their C# defaults, which are Onyx's values; no prototype in either tree overrides them" |
| **M-10** | §3's "In-vendored-file edits" table omits `ONYX AmputationSystem.cs:78` `_damageable.GetAllDamage((part.Owner, damageable))`. It needs no edit (the tuple converts to `Entity<DamageableComponent?>` because `damageable` is already declared nullable at `:75`), but §4's edit table calls out `:182` for exactly this reason and silently skips `:78`, which reads as an oversight to the implementer. | `ONYX AmputationSystem.cs:75-78` | add a "no edit, and here is why" row beside the `:182` note |

---

## 4. Checks the brief asked for, with the verdict

| Category | Verdict |
|---|---|
| **Symbols no report covered** | `WoundSystem.CanCreateWound`'s `SupportedWounds` gate was covered by **no** report and is the one that would have made the whole amputation feature silently inert. **It passes** (`wounds.yml:14-26`). `CanDetachPart` (`SharedBodySystem.Parts.cs:718`) is likewise uncited but sound. `ONYX AmputationSystem.cs:78` — see M-10. Everything else `AmputationSystem` touches (`host.AmputationConsequenceWound`/`DismembermentWound`/`DismembermentSeverities`/`SeverableResetRatio`/`DefaultDismembermentFinishingDamage`, `WoundableComponent.Severable`/`AmputationOverflow`, `PartDamage{Applied,Overflowed}Event.Body`/`.Damage`/`.ExplosionAmputationCandidate`) is present and SAME (`WoundDamageComponents.cs:50-78,156-171`, `WoundEvents.cs:46-67`). The `using Content.Shared.Damage.{Systems,Components};` lines both resolve in WG. |
| **Shims colliding with existing compat members / WG overloads** | None found. `ArmorComponent.Coverage/CoverageSymmetry/PartModifiers` and `ArmorPartModifier` are new names; `WolfmedPartArmorSystem` keeps its single subscription and only its body changes; `WolfmedOrganConsequenceSystem` is a new type name. The `ArmorComponent` partial is legal (no `[Access]`, `sealed partial`, `AutoGenerateComponentState` unaffected by non-networked additions). |
| **Hooks duplicating existing directed subscriptions** | All four §5.1 pairs re-grepped repo-wide across `_Onyx`, `_WF`, `_Shitmed`, `_Mono` and upstream: **all free.** §5.2's eight "do not register" pairs all resolve to the owners named. P3-D21's correction (`<OrganComponent, OrganAddedToBodyEvent/OrganRemovedFromBodyEvent>` free — only `BrainSystem:24,26`, `HeartSystem:16,17`, `NymphSystem:24` claim the *specific* components) reproduces. |
| **Prototypes referenced but not scheduled** | None missing. `InternalBleedingWound` (`wounds.yml:405-413`), `DismembermentWound`, `AmputationConsequenceWound`, `OrganicBodyPartProfile.organDamage` all ship. The seven `WolfmedOrgan*` abstracts are scheduled in WP11-2. Only gap is M-2 (WP11-3's four test ids undeclared). |
| **Components referenced by scheduled YAML that don't exist in WG** | None. `WolfmedBodyPart`, `WolfmedOrgan`, `OrganDamage` all `[RegisterComponent]`ed. PROTO B emits no new component. PROTO C changes only `sprite:` paths. |
| **String-keyed `DamageDict` traps (WP10-2)** | Re-derived all six call shapes in `AmputationSystem.cs` (`:98-101, :146, :158, :161-162, :183-185`). P3-D16 is right: every one resolves through `ProtoId<T>`'s implicit `string` conversions with the receiver fixing `TKey`. **No key-type edit needed.** No `.Key.Id` access exists in the file. |
| **Client sandbox violations** | None. No `Sandbox.yml` exists in the repo (RT-side), and every construct the new client partial needs — `Enum.GetValues<T>()`, `SpriteSpecifier.Rsi`, `ResPath`, `Color.FromHex` — is already used from `Content.Client` today (e.g. `Content.Client/Atmos/EntitySystems/AtmosPipeAppearanceSystem.cs:32`). `Content.Client` already references `Content.Shared._Onyx.Wounds` (phase-2 HUD). |
| **Build-order errors** | Sound. WP11-1 before WP11-2 for `OrganDamageSystem` ownership (P3-D24) holds; file #1 before file #2 inside WP11-1 (CS0246) holds; one owner per shared file across WP11-0/3/5 holds; no two WPs touch the same YAML. Only ordering nit: **WP11-0's T-VISUALS asserts behaviour that WP11-4 later changes nothing about, but it must be re-run after WP11-4** — the plan's WP11-4 checkpoint doesn't name it. |
| **D2 breaches** | **One, and it is the blocker** (B3-1). Everything else is genuinely host-scoped: `AmputationSystem`'s only two entry points are raised solely by `WoundDamageRoutingSystem` (`:761`, `:824`) which is `WoundHostComponent`-gated; the armour rewrite fires only on `PartDamageModifyEvent` (same gate); `Covers()` never touches `DamageModifyEvent`; HOOK 20's branch requires `PartDamageVisualsComponent`, which only `WoundDamageProjectionSystem` ever adds and only to hosts and detached roots; PROTO A's components sit inert on non-hosts because organ health only changes through `OrganDamageSystem`, which is behind `PartDamageAppliedEvent`. |
| **Shitmed double-application on detach** | Correctly handled. GUARD B (`Targeting.cs:234`) suppresses the sever decision; GUARD C suppresses integrity regen; the facade forces `canSever: false`. The single genuine overlap — `PartRemoveDamage`'s Bloodloss vs. Onyx's `DismembermentWound` bleed — is deliberate and load-bearing for B-1. But see **B3-1** and **M-4**: the plan reasons about the overlap's *magnitude* only for wound hosts. |
| **Contradictions with `DECISIONS.md`** | **B3-1** (D2) is the substantive one. **M-9** is a wording-level one against P3-1. The rest reconcile: P3-D8 answers P3-2's `MissingHeartComponent`/`BodyStasis` question **no** with evidence; P3-5's `RepairSelectionAndSnapshotValidationTest` "skip and record" is honoured (§6.2); P3-3's "three locational-armour tests" are all scheduled; P3-5's surgery-attach assertion is T-REATTACH; P3-7's three docs are WP11-6. |

---

## 5. P3-6 — the numbers, independently re-derived

Every figure below was recomputed from the tree, not copied from PLAN3. **All of PLAN3's §8 numbers are correct.**

**Amputation (per-part, `parts.yml` + `WoundDamageComponents.cs:50-78`):**

| Part | Slash T | Piercing T | Blunt T | Dismemberment severity on parent | `MaxDamage` |
|---|---|---|---|---|---|
| Head | 200 | 200 | 350 | 200 | 0 |
| Arm | 130 | 250 | 250 | 120 | 0 |
| Hand | 70 | 200 | 150 | 80 | 0 |
| Leg | 150 | 250 | 300 | 120 | 0 |
| Foot | 80 | 220 | 170 | 80 | 0 |
| Torso | — (empty; excluded anyway) | — | — | 100 (unreachable) | **250** (only nonzero in the game) |

Finishing minimums **Slash 15 / Piercing 40 / Blunt 50**; `AmputationConsequenceSeverity` **35** (on the
**parent**); `SeverableResetRatio` **0.8**. Hits to amputate = `ceil(T/d) + 1`:

| Part | E-sword 50 | Katana 40 | Machete 32 | Combat knife 22 | Any bullet (Piercing 14) | Laser (Heat 16) |
|---|---|---|---|---|---|---|
| Hand 70 | 3 | 3 | 4 | 5 | **never** | **never** |
| Foot 80 | 3 | 3 | 4 | 5 | never | never |
| Arm 130 | 4 | 5 | 6 | 7 | never | never |
| Leg 150 | 4 | 5 | 6 | 8 | never | never |
| Head 200 | 5 | 6 | 8 | 11 | never | never |

**Confirmed: no Wolfgate firearm or laser can amputate, at any range or volume.** Two independent reasons:
Piercing's finishing minimum is 40 vs. a 14-damage round (`projectiles.yml:106`), and `Heat` appears in no
`amputationThresholds` dict so laser `progress` stays 0. Amputation is melee-Slash-only.

**Per-limb armour.** Heavy vest `Piercing 0.25` with `coverage: [Torso, Arm, Leg]`: aimed torso **75 %**
(unchanged); random unaimed hit **52.9 %** average (from 75 %); aimed hand/foot **0 %** (see M-7).
Target weights Torso 4 / Head 1 / Arm 2 / Hand 1 / Leg 2 / Foot 1, total 17 → torso 23.5 %, head 5.9 %,
each arm/leg 11.8 %, each hand/foot 5.9 % — all re-derived, all correct. Stacking is the number to playtest:
heavy vest + SWAT helmet goes from `0.25 × 0.80 = 0.20` on **every** part to 0.25 torso / 0.85 head.

**Organs.** `15 × 0.3 = 4.5` HP per damage application → **exactly 4 applications** to destroy any organ,
regardless of hit size; the cap binds above **9.9–11.2 Piercing**, i.e. below Wolfgate's base bullet (14), so
weapon damage is irrelevant to organs. ~**166** torso hits for lungs, ~382 for heart, ~**100** head hits for
brain. Destruction is a cliff (brain → instant death; heart → `DelayedDeathComponent`, 60 s, defib refused) and
**irreversible** — nothing in Wolfgate raises organ health.

---

## 6. Recommended plan amendments, in order

1. **Resolve B3-1 before WP11-1 starts.** Either re-take P3-D1 as the `CheckVitalDamage` variant, or add the
   host guard at `PartRemoveDamage`, or get a written D2 waiver with the non-host blast radius spelled out.
2. Add the hand/foot fold (or the deviation row) to §2.3 / §7.3 — **M3-1**.
3. Give the WP11-5 torso fixture a non-default `amputationConsequenceSeverity` — **M3-2**.
4. Add the `Groin` switch-arm deletion to §2.3's Option B list — **M3-3**.
5. Rewrite T-AMP-OVERFLOW (a)'s setup in sub-50 chunks — **M3-4**.
6. Sweep the ten minors; M-1, M-2 and M-6 are single-line edits.
