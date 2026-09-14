# WP12-4 verification — Wound surgery C# and HOOK 24/25 (P4-3a)

Worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG).
Onyx reference: `C:/tmp/onyx`. Verified against `DECISIONS.md`, `PLAN4.md` (§WP12-4, §1, §3, §5, §8) and
`WP12-4-report.md`. No file touched by this verification pass except this file and the two snapshot files.

**Verdict: PASS.**

---

## 1. Build

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Both re-run fresh in this verification pass (not reused from the report). 0 errors both.

## 2. Upstream discipline

`git diff HEAD --stat` over `Content.Shared/Server/Client`, `Resources`, `Content.IntegrationTests` shows 11
modified tracked files. Diffed against WP12-3's own patch (`snapshots/WP12-3.patch`) to isolate what WP12-4
actually changed: exactly two files are new to the diff since WP12-3 —

- `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` — HOOK 24 (`OnWoundedValid`, +2 marked lines) and
  HOOK 25 (`OnPartRemovedConditionValid`, +2 marked lines), verbatim against PLAN4 §3.1's authorised text and
  placement. Both marked `// WOLFGATE: HOOK 24 - P4-D19 …` / `// WOLFGATE: HOOK 25 - P4-D18 …`. No `using`,
  no `[Dependency]` added upstream, as required — both bodies and both new dependencies live in the `_WF`
  partial (`SharedSurgerySystem.Wolfmed.cs`).
- `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` — EXT 1. The file's
  `… : Component;` becomes a body with three marked datafields (`WoundGroup`, `MinWoundSeverity`,
  `MaxWoundSeverity`, each carrying `// WOLFGATE: EXT 1 - P4-D19`) and three marked `using`s
  (`Content.Shared.Damage.Prototypes`, `Content.Shared.FixedPoint`, `Robust.Shared.Prototypes`). Both bounds
  default to null — pre-Wolfmed behaviour preserved exactly. Unmarked lines in the hunk are structural only
  (blank lines, the class-opening brace, `/// <summary>` doc-comment lines above each marked field) — no
  unmarked logic or data line exists. This matches the file-level marking precedent already in the tree
  (e.g. `EmoteOnDamageComponent.cs` from phase 2, which also leaves doc-comments and braces unmarked under a
  block-level WOLFGATE reason).

The other 9 modified tracked files (`HealthChange.cs`, `EvenHealthChange.cs`, `GibbingSystem.cs`,
`healing.yml`, `firstaidkits.yml`, `alcohol.yml`, `Reagents/medicine.yml`, `narcotics.yml`,
`_Goobstation/Reagents/medicine.yml`) are unchanged since WP12-3's own patch (confirmed by diffing WP12-4's
tracked-file list against WP12-3's) and were already verified in `WP12-0-verify.md` through `WP12-3-verify.md`
— out of this WP's scope, correctly not touched again here. `GibbingSystem.cs`'s two `.ToArray()` snapshots
predate WP12-4 (landed with the pre-phase-4 gibbing fix DECISIONS pre-authorised) and are unchanged.

No hook or edit outside PLAN4 §3's authorised list was found. **D2 spot check, traced through the new code**:

- `WolfmedWoundWindowFails` (HOOK 24 body) returns `false` immediately when both bounds are null **or**
  `!HasComp<WoundHostComponent>(body)` — confirmed by reading the body directly.
- `WolfmedStumpBlocksAttachment` (HOOK 25 body) requires `TryComp(body, out WoundHostComponent?)` to succeed
  before it can return `true`; a non-host body makes the whole expression `false` by short-circuit.
- `WoundSystem.GetWounds(Entity<WoundableComponent?>)` and `GetGroupSeverity`/`FindWound`
  (`Entity<WoundableComponent?>`) all open with `Resolve(part, ref part.Comp, false)` and return
  empty/`null`/`Zero` when the part carries no `WoundableComponent` — verified by reading
  `WoundSystem.cs:155-163` directly; non-host parts never carry `WoundableComponent` (only added in
  `WoundDamageProjectionSystem.SetupPart`, called only for wound hosts).
- V5 `OnSurgeryPain` calls `_pain.ChangePain(args.Part, amount)` unconditionally, but `PainSystem.ChangePain`
  opens with `Resolve(entity, ref entity.Comp, false)` and no-ops when the part has no `PainComponent`;
  `PainComponent` is only `EnsureComp`'d on parts where `_pain.CanFeelPain(part)` is true, itself downstream
  of the wound-host setup path (`WoundDamageProjectionSystem.SetupPart`/`SetupBody`). Confirmed by reading
  both files.
- V6 `OnOpenIncision` is explicitly `HasComp<WoundHostComponent>(args.Body)`-gated in the handler body.

No behaviour change for entities without `WoundHostComponent` is confirmed for every new handler.

## 3. Vendoring fidelity

No file under `_Onyx/` was added or changed in this WP — confirmed by diffing the untracked-file list against
WP12-3's own untracked list (`comm -13` shows zero new `_Onyx` paths, only the four new `_WF/Wolfmed/Surgery`
files). The report's claim that Onyx's `WoundSurgeryComponents.cs`/`WoundSurgerySystem.cs`/
`SurgerySystem.WoundEffects.cs` were re-authored under `_WF` names rather than vendored (P4-D17) is
consistent with what's on disk — nothing to diff against Onyx for this check.

## 4. Collisions

**Subscription pairs** — all 15 pairs this WP registers (S1–S8 in `WolfmedSurgeryConditionSystem.cs`, V1–V7
in `WolfmedWoundSurgerySystem.cs`) were grepped for `SubscribeLocalEvent<Component, Event>` across the whole
tree, excluding the two new files themselves: **0 extra hits for every pair.** No duplicate-subscription
crash risk.

**Component names** — all ten new `WolfmedSurgery*Component` classes and the `WolfmedIncisionTreatment` enum
grepped for a second `class`/`enum` definition anywhere in the tree: **exactly one definition each.** No new
`EntityEffect` class was added in this WP (that was WP12-1's scope) and no new prototype id was added in this
WP (YAML lands in WP12-5) — nothing further to check for those two categories in this package's diff.

Headless server run (`C:/tmp/wolfmed-plan/p4/wp/WP12-4-report-server.log`, re-inspected in this pass) shows 0
`[ERRO]`/`[FATL]`/`Exception`/`Duplicate Subscriptions` lines and reaches `Server Version 277.0.0.0 -> Ready`,
independently confirming no directed-subscription crash at boot — this WP is the named gate package (15 new
subscriptions, more than the rest of phase 4 combined).

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md`'s `### WP12-4` section carries a row for every file this WP touched: the
four new `_WF` files, the two upstream-modified files (with every `// WOLFGATE` edit listed and reasoned),
and the manifest's own row for itself. Matches the report's file table exactly (7 rows, including the
manifest entry). The five mechanical deviations from PLAN4 §2.4–§2.7 are recorded identically in both the
report and the manifest.

## 6. Plan conformance

Every file in PLAN4's WP12-4 table (§ "Wound surgery C# and HOOK 24/25") exists on disk at the path given:

| Plan destination | Found |
|---|---|
| `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryComponents.cs` | yes, 10 components + enum |
| `Content.Shared/_WF/Wolfmed/Surgery/WolfmedSurgeryConditionSystem.cs` | yes, S1–S8 + helpers |
| `Content.Server/_WF/Wolfmed/Surgery/WolfmedWoundSurgerySystem.cs` | yes, V1–V7 |
| `Content.Shared/_WF/Wolfmed/Surgery/SharedSurgerySystem.Wolfmed.cs` | yes, HOOK 24/25 bodies + 2 deps |
| `Content.Shared/_Shitmed/Surgery/Conditions/SurgeryWoundedConditionComponent.cs` (EXT 1) | yes |
| `Content.Shared/_Shitmed/Surgery/SharedSurgerySystem.cs` (HOOK 24 + 25) | yes |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` (append) | yes |

Component/system code was read in full and matches PLAN4 §2.4–§2.7's specification, modulo the five
mechanical deviations the report itself documents (all independently re-checked against the live tree in
this pass: `GetGroupSeverity`/`FindWound` take `Entity<WoundableComponent?>` and compile via the implicit
`EntityUid` conversion; `DamageGroupPrototype.DamageTypes` is confirmed `List<string>` in this tree, so
`Keys.Any(type => types.Contains(type.Id))` is the correct/only-compiling form; `OnOrganValid`'s
`0 < Health < MaxHealth` window matches P4-D24's "damaged but alive" requirement and `OrganHealthSystem`'s
next-tick destroy-at-zero behaviour; EXT 1's line count includes required `using`s and doc-comments beyond
the plan's literal count; `OnIncisionCheck`'s `Clamp`/`Close` pending-tests are internally consistent with
V7's `Close` branch in `WolfmedWoundSurgerySystem.cs`).

`OnFractureCheck`/`CanEverReach` correctly re-implements `WoundFractureSystem.CanTreat`'s
`ReductionMinimumGrade` gate (P4-D20) without touching the upstream `private static` method — confirmed by
reading `WoundFractureSystem.cs`'s `GetFracture`/`TryGetProfile` signatures, which match what the new code
calls.

DECISIONS.md phase-4 items relevant to this WP (D2, D13, P3-D2, P4-D16 through P4-D20, P4-D24) are all
honoured: server/shared split follows D13 exactly (`SurgeryValidEvent`/`SurgeryStepCompleteCheckEvent` in
`Content.Shared`, `SurgeryStepEvent` in `Content.Server`); the re-attachment block is the surgery-layer hook
P4-D18 specifies, not a `CanAttachPart` edit — `SharedBodySystem.Parts.cs` is confirmed untouched in this
WP's diff; `SurgeryTendWoundsEffectComponent.cs` and `SharedSurgerySystem.Steps.cs` are confirmed untouched
(P4-D16). No DECISIONS §8.4 answer is contradicted by this package (none of §8.4's items are this WP's direct
subject; the ones that are — organ-heal amount, scarring chain — land in WP12-5's YAML, correctly deferred).

Nothing outside PLAN4 §3's authorised list was edited in this WP's slice of the diff.

## 7. Snapshot

Written:

- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-4.patch` (652 lines) — `git diff HEAD` over
  `Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests`.
- `C:/tmp/wolfmed-plan/p4/snapshots/WP12-4.untracked.txt` (42 lines) — `git ls-files --others
  --exclude-standard` over the same paths.

---

## Summary

Build green (both re-run fresh), upstream discipline holds (exactly 2 upstream files touched by this WP, both
fully marked, both matching PLAN4 §3's authorised text), no vendored `_Onyx` file touched, all 15 new
subscription pairs and all 10 new component names collision-free, manifest row present for every touched
file, every planned file exists and matches spec modulo documented mechanical deviations, D2 traced and
confirmed for every new handler. No blocker or major issue found.
