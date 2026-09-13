# WP4 — Wounds core, shared half — verification

**Verifier run:** 2026-09-12. **WG:** `.claude/worktrees/rules-motd-updates-11c89c`. **ONYX pin:**
`2f5bab9946539cbe083010c9ae6fbc59b47ae377` (confirmed live via `git -C ONYX rev-parse HEAD`).

**Verdict: PASS.** No blockers, no majors. One informational (non-major) nit recorded under check 3.

---

## 1. Build

Both commands run sequentially, full output filtered per the checkpoint spec.

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -40
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -40
Build succeeded.
    0 Error(s)
```

Both green, matching the report's claim. No `error [A-Z]+[0-9]+` lines in either.

**Additional check (not required, run to corroborate the report's headless claim):** started
`bin/Content.Server/Content.Server.exe --cvar net.port=1229 --cvar status.enabled=false` for 45s.
Reached `[INFO] root: Server Version 277.0.0.0 -> Ready` and bound its sockets.
`grep -icE "\[ERRO\]|Duplicate Subscription|unhandled|exception"` over the full log: **0**. 13 `[WARN]`
lines, all pre-existing chat-emote-word duplicates and an unrelated `PullingSystem` input-bind warning —
none from WP4 code.

## 2. Upstream discipline

```
$ git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
 Content.Server/Chat/Systems/ChatSystem.Emote.cs            | 2 +-
 Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs | 4 ++++
 2 files changed, 5 insertions(+), 1 deletion(-)
```

Exactly two upstream (non-`_Onyx`, non-`_WF`, non-`Docs`) files touched in the whole uncommitted tree.

- **`Content.Server/Chat/Systems/ChatSystem.Emote.cs`** — the only changed line:
  `public void TryEmoteWithChat(` → `public override void TryEmoteWithChat( // WOLFGATE: HOOK 6, overrides
  SharedChatSystem's shared entry point (D25).` Return type unchanged; the `EmotePrototype` overload at
  `:85` is untouched (confirmed by reading the file). Marked. Authorised: PLAN §3 HOOK 6, assigned to WP4,
  matches D25 exactly (one word, same line, same file).
- **`Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs`** — adds a marked
  `// WOLFGATE: Wolfmed snapshot/targeting needs a single-bit check; copied from Onyx's
  SharedTargetingSystem.` `public static bool IsSelectable(TargetBodyPart part)`. This is PLAN §3 HOOK 5,
  assigned to **WP2** — the WP4 report correctly says it is "untouched here" (it's a pre-existing
  uncommitted change from an earlier WP in this no-commits pipeline, re-verified present and correctly
  marked, but not attributable to WP4's work).

Both changed lines/blocks carry a `// WOLFGATE` marker and map to an authorised §3 hook for this or an
earlier WP. **No unauthorised upstream edits.**

## 3. Vendoring fidelity

Diffed every `_Onyx`-tree file WP4 added or changed against `git -C ONYX show HEAD:<path>`
(`--strip-trailing-cr`). `Content.Shared/StatusEffectNew/**` has no WP4 changes (WP1's files, unmodified
here) so it was not re-diffed.

**Verbatim files (report claims byte-identical) — confirmed identical, 0 diff:**
`WoundEvents.cs`, `WoundBehaviors.cs`, `WoundPrototype.cs`, `WoundScarSystem.cs`,
`WoundStatusEffectSystem.cs`, `PainSystem.cs`.

**Modified files — every differing hunk checked for a `// WOLFGATE` marker:**

| File | Hunks | Marked? |
|---|---|---|
| `WoundDamageComponents.cs` | 4 (using swap, Torso weight fold, SystemicPainTarget, Groin row delete) | all 4 marked |
| `WoundSystem.cs` | 2 (subscription delete, handler rename) | both marked |
| `BodyPartFunctionalitySystem.cs` | 1 (using swap) | marked |
| `WoundFractureSystem.cs` | 4 (usings, facade dep, `_wfPart` dep, `FractureProfile` read) | all marked |
| `WoundDamageRoutingSystem.cs` | 13 (usings x3, dep swaps x2, ordering x2, `args.Cancelled`+targetPart handoff, `.Torso` x3, hands rewrite, `MaxDamage`/`Parent`/`AmputationThresholds` block, bed-marker swap) | all marked |
| `WoundDamageProjectionSystem.cs` | 9 (usings, dep swap+delete, `after:` type, D11 re-point x2, `SynchronizeStreams` delete, `ClearBodyWounds` call, parent walk, `InjurableComponent` delete, visual-layer cases) | all marked |
| `_Onyx/Mobs/Systems/MobThresholdSystem.cs` (fetched via `git -C ONYX show HEAD:<path>`, absent from the sparse checkout, consistent with the report) | 5 hunks | 4 of 5 marked; see nit below |

Edit counts match the report's per-file tallies exactly (4/2/1/4/13/9/4).

**Nit (informational, not a major):** `MobThresholdSystem.cs`'s doc-comment line — `Calculates the total
damage from vital body parts (Head, Torso)` (was `Head, Chest, Groin`) — changed without its own
`// WOLFGATE` tag. It sits in the same three-line XML-doc block as, and is a direct consequence of, the
marked `BodyPartType.Torso, // WOLFGATE: D9 …` edit two lines below in the same method, so it is not
undisclosed drift — but it is a changed line strictly outside the marked line itself. No behavioural
effect (comment only). Not a blocker or major; flagging for a future doc-comment marker pass if the team
wants one.

No other unmarked drift found in any vendored file.

## 4. Subscriptions

`SubscribeLocalEvent<X, Y>` pairs added in WP4 (grepped from the actual files, not just the report):

| Component | Event | Registrant | Ordering |
|---|---|---|---|
| `PainShockTargetComponent` | `ComponentStartup` | `PainSystem` | — |
| `PainComponent` | `RejuvenateEvent` | `PainSystem` | — |
| `WoundHostComponent` | `MapInitEvent` | `WoundDamageProjectionSystem` | `after: SharedBodySystem` |
| `WoundHostComponent` | `RejuvenateEvent` | `WoundDamageProjectionSystem` | — |
| `WoundableComponent` | `DamageChangedEvent` | `WoundDamageProjectionSystem` | — |
| `WoundHostComponent` | `BeforeDamageChangedEvent` | `WoundDamageRoutingSystem` | `before: SharedArmorPlateSystem` |
| `WoundHostComponent` | `DamageDealtEvent` (new `_WF` compat event) | `WoundDamageRoutingSystem` | `before: DamageableSystem` |
| `WoundableComponent` | `BeforeDamageChangedEvent` | `WoundDamageRoutingSystem` | `before: SharedArmorPlateSystem` |
| `WoundFractureComponent` | `WoundChangedEvent` | `WoundFractureSystem` | — |
| `WoundComponent` | `WoundStateChangedEvent` | `WoundScarSystem` | — |
| `WoundScarComponent` | `WoundTreatmentAttemptEvent` | `WoundScarSystem` | — |
| `WoundableComponent` | `WoundCreatedEvent` / `WoundChangedEvent` / `WoundStateChangedEvent` / `WoundRemovedEvent` | `WoundStatusEffectSystem` | — |
| `WoundableComponent` | `ComponentInit` | `WoundSystem` | — |
| `WoundableComponent` | `RejuvenateEvent` | `WoundSystem` | — |

Grepped the whole tree (`Content.Shared`, `Content.Server`, `Content.Client`, `Content.IntegrationTests`)
for `SubscribeLocalEvent<` on each of the seven new component types
(`WoundHostComponent`, `WoundableComponent`, `WoundComponent`, `WoundScarComponent`,
`WoundFractureComponent`, `PainComponent`, `PainShockTargetComponent`), excluding the WP4 files themselves:
**zero matches**. All seven components are new to Wolfgate; no duplicate registration exists anywhere
else in the tree. `DamageDealtEvent` (the new `_WF` compat record struct in
`Content.Shared.Damage.Systems`) does not collide with the unrelated `HitscanDamageDealtEvent` pairs
already registered elsewhere.

Also confirmed **removed**: `WoundSystem.cs`'s Onyx-original
`SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate)` is gone from the file (replaced by a
`// WOLFGATE: D17` comment), consistent with `BodyRejuvenateSystem.cs:28` already owning that pair.

Cross-checked against PLAN.md §5.2's expected pair list: every pair above appears there with the same
registrant and the same ordering constraint. No mismatches.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries one row per WP4-touched file — checked against the 17 files the
report actually delivered (14-file WP4 table + `WoundDamageRoutingSystem`/`WoundDamageProjectionSystem`
pulled from WP5 scope + `MobThresholdSystem.cs` pulled from WP5 #3):

All 17 present (lines 64–76 for the 13 `_Onyx` files, lines 80–83 for the 3 new `_WF` files + the
`ChatSystem.Emote.cs` hook row). A `### WP4` Deviations subsection exists (5 entries, matching D-WP4-1
through D-WP4-6 in substance) and 4 new WP4-relevant Hazards entries are present (invulnerability-if-
WP7-before-WP5, the two pending routing edits, the `DamageChangedEvent` exclusive claim, and the
`MaxDamage` default-zero trap). The manifest's own truncation-and-rebuild incident (D-WP4-0) is disclosed
in a banner note; this is a process incident, not a code-correctness issue, and does not affect any of the
checks above (the rebuilt row set was independently verified against the actual working tree, not just
trusted from the manifest text).

## 6. Plan conformance

All 14 files in PLAN.md §4 WP4's table exist at their planned destination:

1–9, 14 confirmed present under `Content.Shared/_Onyx/Wounds/` and `Content.Server/Chat/Systems/`.
10 (`WoundFractureSystem.cs`) present with all 4 specified edits. 11–13 (`WolfmedBodyPartComponent.cs`,
`WolfmedBodyPartSystem.cs`, `WoundTargetResolver.cs`) present under `Content.Shared/_WF/Wolfmed/{Body,
Targeting}/`, read in full — content matches PLAN §2.12/§2.13 (component not `[NetworkedComponent]`;
resolver folds `TargetBodyPart.Groin` via `SharedBodySystem.ConvertTargetBodyPart`, reads
`CCVars.TargetingEnabled`, drops anatomical-odds scatter exactly as D-WP4-5 records).

The report's scope extension (pulling in `WoundDamageRoutingSystem.cs`, `WoundDamageProjectionSystem.cs`
from WP5 #1/#2, and `MobThresholdSystem.cs` from WP5 #3) is explicitly justified in the report's own
opening scope note and in Deviation D-WP4-8, and is consistent with WP5's file table in PLAN.md §4 (same
three files, same edit lists) — cross-checked line-by-line against PLAN's WP5 edit tables, matching in
substance (D12 facade swap, D10 resolver swap, D9 `.Torso` folds, `args.Cancelled` guard, targetPart
handoff, `before:` ordering, hands rewrite, D11 re-point, D15 dependency drop, D17 `ClearBodyWounds`,
D19 `InjurableComponent` deletion). The report's two explicitly-declined WP5 items — the D27 applied-delta
accumulator (blocked on GUARD D2, not yet landed) and the D23 AP side table (blocked on WP8) — are
confirmed **not** present in the diff (verified: `_appliedDelta`/`args.Applied` and an AP side table do
not appear anywhere in `WoundDamageRoutingSystem.cs`), matching the report's own disclosure.

**Decisions cited by the WP4 section and this report, checked against the actual code:**

- **D1** (StatusEffectNew verbatim) — inherited from WP1, unaffected by WP4; `PainSystem.cs` compiling
  verbatim against it confirms the dependency is satisfied.
- **D8** (Shitmed `BodyPartComponent` stays; extra fields on a separate `_WF` component) — confirmed:
  `WolfmedBodyPartComponent` holds exactly the 6 fields the report claims, not `[NetworkedComponent]`.
- **D9** (no `BodyPartType.Chest`/`Groin`; fold to `Torso`; compensating `4f` weight) — confirmed in every
  cited call site (`WoundDamageComponents.cs`, routing, projection, `MobThresholdSystem.cs`).
- **D10** (no Onyx Targeting stack; `WoundTargetResolver` instead) — confirmed; only
  `DamageDistribution.cs`, `TargetingSnapshotComponent.cs`, `TargetingSnapshotSystem.cs` exist under
  `_Onyx/Targeting`, no `TargetingComponent`/`SharedTargetingSystem`/`TargetResolverSystem` vendored.
- **D11** (split `DamageDealtEvent`/`DamageChangedEvent`) — confirmed at
  `WoundDamageProjectionSystem.cs:38`, `ref DamageChangedEvent args`, `args.DamageDelta`.
- **D12** (`WolfmedDamageableSystem` facade, not a `DamageableSystem` partial) — confirmed; grepped for
  `[Dependency] DamageableSystem` in every vendored `_Onyx` file touched by WP4: zero hits.
- **D17** (`WoundSystem`'s `RejuvenateEvent` sub deleted, `ClearBodyWounds` exposed) — confirmed both
  halves (see checks 2/4 above).
- **D19** (no `InjurableComponent` shim) — confirmed the block is deleted from `SetupPart`, not merely
  edited.
- **D20 reversed** (`Caustic` stays in `LocalizedDamageTypes`) — confirmed `WoundDamageComponents.cs:34-43`
  is untouched relative to Onyx (part of the verbatim-hunk region; not in the diff at all).
- **D25** (`SharedChatSystem.TryEmoteWithChat` virtual + HOOK 6 override) — confirmed both halves; the
  `EmotePrototype` overload is untouched.
- **§8.3 trap 4** (armour-plate double-absorb; `before:` ordering on both `BeforeDamageChangedEvent` subs)
  — confirmed both subscriptions carry `before: [typeof(SharedArmorPlateSystem)]`.

No cited decision was found unhonoured.

## 7. Snapshot

```
$ git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP4.patch
$ git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP4.untracked.txt
```

`WP4.patch`: 26 lines (the two tracked-file diffs from check 2). `WP4.untracked.txt`: 58 paths (every
`_Onyx`, `_WF`, `StatusEffectNew`, prototype/locale and Docs file created across WP1–WP4 so far, consistent
with the no-commits accumulation model in DECISIONS.md).

---

## Summary

Both builds green, headless server reaches Ready with 0 errors/duplicate-subscription crashes. Exactly two
upstream files touched, both marked and both authorised (one is WP4's own HOOK 6, one is WP2's pre-existing
HOOK 5, correctly left alone). Every vendored `_Onyx` file's drift from the Onyx pin is `// WOLFGATE`-marked
except one cosmetic doc-comment line with no behavioural effect. No duplicate `(component, event)`
subscriptions anywhere in the tree; the 14 pairs registered all appear in PLAN §5.2 with matching ordering.
Manifest has a row for every touched file plus deviations and hazards. Every file in the WP4 plan table
exists at its planned destination with the specified edits; the two WP5 files and one WP5-scoped file
pulled in early are justified, match WP5's own edit tables, and correctly omit the two edits that depend on
work not yet landed (GUARD D2, WP8's AP table).

**pass = true.**
