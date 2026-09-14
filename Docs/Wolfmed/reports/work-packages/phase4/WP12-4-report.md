# WP12-4 — Wound surgery C# and HOOK 24/25 (PLAN4 §4, P4-3a)

Worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**).
No commits. RobustToolbox untouched.

---

## 1. Files created / modified

| File | Status | Size |
|---|---|---|
| `WG/Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryComponents.cs` | **new** | 10 components + `WolfmedIncisionTreatment` enum, ~100 lines |
| `WG/Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryConditionSystem.cs` | **new** | S1–S8 + `FindWound` / `GetGroupSeverity` / `TryFindOrgan` / `CanEverReach`, ~215 lines |
| `WG/Content.Server/_WF/Wolfmed/Surgery/WolfmedWoundSurgerySystem.cs` | **new** | V1–V7, ~110 lines |
| `WG/Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs` | **new** | HOOK 24 + HOOK 25 bodies, 2 `[Dependency]`s, ~38 lines |
| `WG/Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` | **modified — EXT 1** | +13 lines (3 datafields, 3 usings) |
| `WG/Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` | **modified — HOOK 24 + HOOK 25** | +4 lines |
| `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** | `### WP12-4` section appended |

Build order honoured: components before both systems; the `_WF` partial before the upstream hook that calls it.

The mandatory assembly split (§2.5/§2.6) is in place: every `SurgeryValidEvent` and
`SurgeryStepCompleteCheckEvent` handler is in `Content.Shared`, every `SurgeryStepEvent` handler in
`Content.Server`.

---

## 2. Every `// WOLFGATE` edit and its reason

**Upstream files (2 files, 3 marked sites, 8 marked lines total):**

1. **HOOK 24** — `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs`, end of `OnWoundedValid`
   (after the shipped `args.Cancelled = true;` at what was `:127`). Two lines:
   `if (WolfmedWoundWindowFails(ent, args.Body, args.Part)) // WOLFGATE: HOOK 24 - P4-D19 wound-severity window`
   / `args.Cancelled = true;`. Reason: P4-D19's severity window, so WP12-5's deep tend surgeries do not
   overlap the two shallow ones. No `using`, no `[Dependency]` lands upstream — the body and both new
   dependencies live in the `_WF` partial.
2. **HOOK 25** — same file, inside `OnPartRemovedConditionValid`, immediately after the `CanAttachToSlot`
   guard block. Two lines:
   `if (WolfmedStumpBlocksAttachment(args.Part)) // WOLFGATE: HOOK 25 - P4-D18 untreated amputation consequence`
   / `{ args.Cancelled = true; return; }`. Reason: P4-D18 closes the phase-3 P3-D2 gap at the surgery layer.
   `SharedBodySystem.CanAttachPart` was deliberately NOT hooked (§3.4, §8.5 trap 13) — it is reached by five
   non-surgery callers that would silently `QueueDel` a prosthetic on exactly the stump they exist for.
3. **EXT 1** — `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs`. The file's
   `public sealed partial class … : Component;` becomes a body carrying three marked datafields
   (`WoundGroup = "Brute"`, `MinWoundSeverity`, `MaxWoundSeverity`, each `// WOLFGATE: EXT 1 - P4-D19`), plus
   three marked `using`s the types need: `Content.Shared.Damage.Prototypes` (`DamageGroupPrototype`),
   `Content.Shared.FixedPoint` (`FixedPoint2`) and `Robust.Shared.Prototypes` (`ProtoId`). Purely additive —
   both bounds default to null, which is the pre-Wolfmed behaviour exactly.

**`_WF` files carry `// WOLFGATE` only where the file itself needs an explanation** (the header of
`SharedSurgerySystem.Wolfmed.cs` and its two dependency lines); the rest are new Wolfgate files under the
authorised `_WF/Wolfmed` layout, per D6/§style, and are commented in `_WF` house style
(`/// <summary>` one-liners, `[Dependency] private X _x = default!;` without `readonly`, no licence header).

**No vendored Onyx file was touched.** Onyx's `WoundSurgeryComponents.cs` / `WoundSurgerySystem.cs` /
`SurgerySystem.WoundEffects.cs` are re-authored under `_WF` names per P4-D17 rather than vendored, which is
what §4's "new" status column asks for.

---

## 3. Deviations from PLAN4

All five are mechanical; none changes shipped behaviour relative to the plan's intent.

1. **`GetGroupSeverity`/`FindWound` take `Entity<WoundableComponent?>`, not `EntityUid`.** §2.5 writes the
   signatures with a bare `EntityUid`. `Entity<T?>` has an implicit conversion from `EntityUid`
   (`RobustToolbox/Robust.Shared/GameObjects/Entity.cs:39`), so every call site in the plan compiles unchanged;
   the widened parameter is what lets the helper open with `Resolve(..., false)` and return empty for a
   non-woundable part — the same shape Onyx's own `FindWound` has.
2. **`prototype.DamageTypes.Keys.Any(type => types.Contains(type.Id))`, not Onyx's `Any(types.Contains)`.**
   Forced: WG's `DamageGroupPrototype.DamageTypes` is `List<string>` (`DamageGroupPrototype.cs:27`); Onyx's is
   `List<ProtoId<DamageTypePrototype>>`. The literal Onyx line is `CS0123`, and it was, on the first build.
3. **`OnOrganValid` (S3) requires `0 < Health < MaxHealth`.** Onyx's `SurgeryOrganConditionComponent.Damaged`
   has no lower bound. P4-D24 and §2.4's own summary line ("damaged but alive") ask for one: `OrganHealthSystem.Update`
   destroys any organ at `Health <= 0` on the next tick, so a heal surgery listed on a dead organ would be
   unreachable by the time the do-after finished.
4. **EXT 1 is +13 lines, not the plan's "+6".** Three `using`s and a one-line `/// <summary>` per datafield,
   both required by house style and by the compiler respectively. Still purely additive.
5. **`OnIncisionCheck` (S8) semantics are spelled out**, since §2.5 lists the handler but not its body.
   `Clamp` is pending while a matching wound carries a `WoundBleedingComponent` whose
   `Treatment < BleedingTreatment.Clamped` — a `!=` test would report an already-**cauterised** incision as
   pending and re-run `TreatPart(Clamped)`, downgrading it. `Close` is pending while a matching wound is
   `Open` or `Stabilized`, exactly the set V7's `Close` branch acts on.

**Nothing outside §3's authorised list was edited.** Specifically NOT touched:
`SharedBodySystem.Parts.cs`, `SurgeryTendWoundsEffectComponent.cs`, `SharedSurgerySystem.Steps.cs`, and every
prototype file (PROTO F and PROTO G belong to WP12-5).

---

## 4. Build / test output

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

**Headless server** (`--cvar net.port=1299`, ~130 s) — `C:/tmp/wolfmed-plan/p4/wp/WP12-4-report-server.log`:

```
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
106:[INFO] root: Server Version 277.0.0.0 -> Ready
```

No `Duplicate Subscriptions for comp=…, event=…` throw. This was the named gate for this package: it adds 15
directed subscriptions, more than the rest of phase 4 combined.

**Integration tests**, phase-4 gate filter
(`FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~_Onyx.Medical|FullyQualifiedName~Wolfmed`)
— `C:/tmp/wolfmed-plan/p4/wp/WP12-4-report-tests.log`:

```
Test Run Successful.
Total tests: 65
     Passed: 65
 Total time: 1.3207 Minutes
```

No YAML, FTL, RSI or XAML was touched, so the Release YAML lint was not re-run. WP12-3's standing
`medical_patch.yml` icon hazard is unchanged and still belongs to WP12-10 (or a WP12-0 re-run).

---

## 5. What later packages must know

**A. HOOK 25 is live now, and WP12-5 is a hard dependency, not a follow-up.**
WP12-4's brief called HOOK 24 and HOOK 25 "provably inert until WP12-5". HOOK 24 is (measured: 0 hits for
`minWoundSeverity`/`maxWoundSeverity`/`woundGroup` in `Resources/Prototypes`; the only two
`- type: SurgeryWoundedCondition` users are `surgeries.yml:294` and `:307`, PROTO F's own sites). **HOOK 25 is
not.** `AmputationConsequenceWound` is created on the parent part by phase 3's shipped
`AmputationSystem.ApplyAmputationConsequences`, and its prototype is `damageTypes: {}`
(`_Onyx/Wounds/wounds.yml:398-401`) — wound healing selects by damage type, so no reagent, topical or
`TryHealWounds` path can ever remove it. From this package onward, a traumatically amputated wound host
**cannot have the limb re-attached by any means** until WP12-5 ships `SurgeryHealAmputationConsequence`. If
WP12-5's attach block were dropped, HOOK 25 must be dropped with it (and P4-D15's guidebook paragraph too).
Surgical limb removal is unaffected: `ApplyAmputationConsequences` has exactly one caller
(`AmputationSystem.TryAmputate:130`) and Shitmed's `SurgeryDetachPart` does not go through it.

**B. Scope of the block, re-confirmed:** the hook keys on `args.Part`, the part being attached *to*. A torso
stump hides the 6 `SurgeryAttach*` surgeries whose `SurgeryPartCondition` is `part: Torso`; an arm stump hides
that side's `AttachHand`, a leg stump that side's `AttachFoot`. Ten `SurgeryAttach*` surgeries exist, not
eleven (P4-D18 confirmed).

**C. WP12-5's component names and datafield names** (the YAML must match these exactly):
`WolfmedSurgeryWoundCondition` {`woundPrototype`, `visibility`, `state`, `bleeding`, `internalBleeding`,
`inverse`}; `WolfmedSurgeryClampBleedingEffect` {`amount` **required**, `woundPrototype`};
`WolfmedSurgeryTreatWoundEffect` {`woundPrototype`, `internalBleeding`, `amount`};
`WolfmedSurgeryFractureCondition` {`minGrade`, `grade`, `treatment`};
`WolfmedSurgeryMendFractureEffect` {`treatment`, default `Mended`};
`WolfmedSurgeryOrganDamagedCondition` {`slot` **required**, `inverse`};
`WolfmedSurgeryOrganHealEffect` {`slot` **required**, `amount`, default 1 — **P4-D23 wants `amount: 3` written
in the prototypes**, the C# default is deliberately left at Onyx's 1 so the balance number lives in YAML where
reverting is one line); `WolfmedSurgeryPainEffect` {`amount`, `sleepModifier` (inert)};
`WolfmedSurgeryIncisionWoundEffect` {`severity`, `wound`}; `WolfmedSurgeryIncisionTreatmentEffect`
{`wound`, `treatment`: `Clamp` | `Close`}.
`slot` is Shitmed's `OrganComponent.SlotId` string (`brain, eyes, lungs, heart, stomach, liver, kidneys` on
the human), **not** an Onyx `OrganCategoryPrototype`.

**D. Placement rules WP12-5 must honour** (WP12-4 traps 1 and 2): `WolfmedSurgeryWoundCondition`,
`WolfmedSurgeryFractureCondition` and `WolfmedSurgeryOrganDamagedCondition` are **visibility** conditions and
belong on the **surgery** singleton — `SurgerySystem.RefreshUI` raises `SurgeryValidEvent` on the surgery only.
Onyx attaches its equivalent to a *step*; copying that placement is the bug. And a tight condition on the
surgery (e.g. `bleeding: true`) aborts a repeat loop the instant the last bleeder is clamped and logs
`"tried to start invalid surgery"` — put the tight condition on the step, the loose one on the surgery.

**E. Fracture ladder.** `WolfmedSurgeryMendFractureEffect` with `treatment: Reduced` is the `BoneSetter` step,
`treatment: Mended` the `BoneGel` step. The completion check treats a target as reached when it can never be
reached — `CanTreat` gates `Reduced` on `Grade >= profile.ReductionMinimumGrade`, which is `Simple` on
`OrganicFractureProfile`, so a Hairline fracture skips the reduce step instead of stalling it.

**F. For WP12-9's tests.** `FindWound`, `GetGroupSeverity` and `TryFindOrgan` on
`WolfmedSurgeryConditionSystem` are `public` and callable headlessly; so is
`SharedSurgerySystem.GetSingleton(EntProtoId)`. `WolfmedWoundWindowFails` and `WolfmedStumpBlocksAttachment`
are `private` — T-REATTACH-BLOCKED must assert through `SurgeryValidEvent` (raise it on the stump and check
`Cancelled`, or call `IsSurgeryValid`), and must also assert that `SharedBodySystem.CanAttachPart` stays
`true`, which is the guard that P4-D18's decision was not quietly reverted.

**G. Upstream-file count.** `SharedSurgerySystem.cs` and `SurgeryWoundedConditionComponent.cs` are both
first-time Wolfmed touches, so WP12-10's §3.4 arithmetic (27 after phase 3 → 46 after phase 4) gains 2 from
this package.
