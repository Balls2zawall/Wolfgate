# WP11-4 verify — Limb damage sprites (P3-4)

**Verdict: PASS.** No blockers, no majors. Checks are against the live worktree state, which also carries
WP11-1/2/3's uncommitted work per the sequential no-commit model (each already independently PASSed its own
verify — not re-audited here beyond a consistency spot-check).

---

## 1. Build (0 errors each)

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo -> Build succeeded. 0 Error(s)
dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo -> Build succeeded. 0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 9 modified tracked files plus untracked new files. WP11-4's own two modified upstream files:

| File | Marked? | Authorised by |
|---|---|---|
| `Content.Client/Damage/DamageVisualsSystem.cs` | Yes — every new line carries `// WOLFGATE: HOOK 20` (or `HOOK 20 (Option B)`) | PLAN3 §3 HOOK 20 (Option A insertions a+b) |
| `Content.Shared/Body/Part/BodyPartComponent.cs` | Yes — one word, `// WOLFGATE: HOOK 21 …` | PLAN3 §3 HOOK 21 |

Diffed line-for-line against PLAN3 §3's prescribed text: HOOK 20's `using Content.Shared._Onyx.Wounds;`,
the Option-A subscription, and the 5-line `HandleDamage` early return all match verbatim (including the
`return;` placement between `UpdateDisabledLayers` and `CheckOverlayOrdering`). HOOK 21's one-word flag
(`AutoGenerateComponentState(raiseAfterAutoHandleState: true)`) matches verbatim.

**Deviation confirmed and assessed — not a blocker/major.** HOOK 20 carries a *third* insertion beyond
§3's literal "two insertions, 1+4 lines" wording: `using Content.Shared.Body.Part;` plus
`SubscribeLocalEvent<BodyPartComponent, AfterAutoHandleStateEvent>(OnBodyPartState); // WOLFGATE: HOOK 20
(Option B) - needs HOOK 21's raiseAfterAutoHandleState flag`. This is disclosed by the report and the
manifest, with a stated revert path. Assessed as authorised in substance despite the §3 table's incomplete
line-count: PLAN3's own §5.1 subscription-audit table explicitly lists `<BodyPartComponent,
AfterAutoHandleStateEvent>` registered by `DamageVisualsSystem` (client) as a WP11-4 pair, pre-verified
free; §2.3 explicitly specifies `OnBodyPartState`'s body as part of this WP's `_WF` partial; and
DECISIONS.md §8.6-5 / PLAN3 P3-D18 pre-authorise Option B outright, which is provably dead without this
exact subscription (no other legal home exists — `Initialize()` is a single virtual override already owned
by the upstream file). The edit is 2 lines, `// WOLFGATE`-marked, and duplicate-free (§4 below). Judged as
a plan-table documentation gap, not undisclosed scope creep — same class of finding WP11-3's verify treated
as non-blocking.

**No unauthorised or oversized upstream edits found.**

**D2 (no behaviour change for entities without `WoundHostComponent`):** structurally verified. HOOK 20's
early return in `HandleDamage` is gated on `TryComp(uid, out PartDamageVisualsComponent? partDamage)`
succeeding; `grep -rn "EnsureComp<PartDamageVisualsComponent>"` shows it is only ever added by
`WoundDamageProjectionSystem` (pre-existing WP5 code, host-gated). A non-wound-host never has the
component, so `HandleDamage` falls through unchanged to the stock aggregate-overlay path. Likewise,
`OnBodyPartState` (fired for *every* body part now, per HOOK 21's ungated flag) immediately no-ops via
`TryComp(ent, out PartDamageVisualsComponent? damage)` for any part belonging to a non-wound-host. Confirmed
no other file references `PartDamageVisualsComponent` outside `_Onyx`/`_WF`/the two hooked files.

## 3. Vendoring fidelity

`Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` (78 files each, 77 states + `meta.json`) byte-
compared against `C:/tmp/onyx` at the pinned commit `2f5bab99...` for every file:
- All 154 `.png` files: byte-identical (`cmp -s`).
- Both `meta.json`: identical content, differ only in a trailing newline (`diff --strip-trailing-cr` shows a
  single `\ No newline at end of file` hunk, no textual change). JSON carries no comment syntax, so no
  `// WOLFGATE` marker applies; this is not a content deviation.

No other `_Onyx` files are touched by this WP (`Content.Shared/_Onyx/Wounds/AmputationSystem.cs` is
WP11-1's, already verified there).

## 4. Subscriptions

Both pairs added in this WP, grepped repo-wide (`Content.Client Content.Server Content.Shared`, excluding
nothing since these are compiled `.cs` files not new to the repo):

- `SubscribeLocalEvent<PartDamageVisualsComponent, AfterAutoHandleStateEvent>` — **one** hit
  (`Content.Client/Damage/DamageVisualsSystem.cs:38`). No duplicate.
- `SubscribeLocalEvent<BodyPartComponent, AfterAutoHandleStateEvent>` — **one** hit
  (`Content.Client/Damage/DamageVisualsSystem.cs:39`). Six other `SubscribeLocalEvent<BodyPartComponent, …>`
  registrations exist in the tree, all with different event types (`EntInsertedIntoContainerMessage`,
  `EntRemovedFromContainerMessage`, `MapInitEvent`, `ComponentRemove`, `AmputateAttemptEvent`,
  `BodyPartEnableChangedEvent`, `DamageModifyEvent`, `DamageChangedEvent`,
  `BodyPartComponentsModifyEvent`, `SurgeryToolExaminedEvent`) — no `(component, event)` pair collision.

Matches PLAN3 §5.1 rows 3 and 4 exactly (component, event, registrant, "YES" free).

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for all 6 WP11-4 table files: the new `_WF` partial, the
`DamageVisualsSystem.cs` hook (with the third-insertion deviation called out inline), the
`BodyPartComponent.cs` hook, both new RSI directories (with licence/attribution note), and `base.yml`'s
PROTO C edit. A `### WP11-4` deviations section (from the manifest's phase-3 block) covers Option A, the
P3-D25 fold, Option B, and the HOOK 20 third-insertion deviation with justification — matching the report.

## 6. Plan conformance

All 6 files in the WP11-4 table (PLAN3 §"WP11-4 — Limb damage sprites") exist and match:

1. `Content.Client/_WF/Wolfmed/Damage/DamageVisualsSystem.Wolfmed.cs` — new, structure matches §2.3's
   prescribed method list exactly (`OnPartDamageVisualsState`, `UpdatePartDamageVisuals`, `GetLayerDamage`,
   plus Option B's six methods). Re-authored onto WG's calling convention as specified: `ProtoMan` →
   `_prototypeManager` (confirmed field name at `DamageVisualsSystem.cs:31`), Onyx's tuple-shaped
   `UpdateTargetLayer(Entity<...>, layer, group, threshold)` re-typed to WG's existing
   `UpdateTargetLayer(SpriteComponent, DamageVisualsComponent, object, string, FixedPoint2)` (confirmed at
   `DamageVisualsSystem.cs:633`), `CheckThresholdBoundary` reused verbatim (confirmed same shape at `:554`).
   Per-group threshold cache correctly not consulted, matching §2.3's explicit instruction. P3-D25 fold
   present and correctly scoped to the attached-body path only (`GetLayerDamage`), not applied to
   `UpdateDetachedPartDamage`'s full `Enum.GetValues<HumanoidVisualLayers>()` walk — exactly the distinction
   §2.3 calls out as a trap. Both Option-B compile traps avoided: `Groin` dropped from
   `TryGetDetachedDamagePrefix` (D9), and no `ProtoMan`-bare reference anywhere.
2. `Content.Client/Damage/DamageVisualsSystem.cs` — HOOK 20, verified in §2 above.
3. `Content.Shared/Body/Part/BodyPartComponent.cs` — HOOK 21, verified in §2 above.
4. `Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` — 156 files, vendoring fidelity verified in §3.
5. `Resources/Prototypes/Entities/Mobs/Species/base.yml` — PROTO C, exactly 2 lines, both `# WOLFGATE
   (WP11-4, P3-4 Option B)`-marked, no `Groin` layer, no Hand/Foot `targetLayers` added — confirmed by diff
   in §2.3-adjacent review above.
6. `Docs/Wolfmed/WOLFMED_MANIFEST.md` — appended, checked in §5.

**DECISIONS.md §8.6 answers, full sweep (per the verify brief):**
- **§8.6-1 (guns/lasers sever via Piercing-12/Heat thresholds):** confirmed still present and unchanged in
  `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` (WP11-1's file, not touched by WP11-4) —
  `dismembermentFinishingDamage: {Piercing: 12, Heat: 15}` and per-part `Heat` thresholds mirroring
  `Piercing` (Head 200, Arm 250, Hand 200, Leg 250, Foot 220), all `# WOLFGATE (P3 balance)`-marked.
- **§8.6-2 (vests + BP helmet annotated):** confirmed still present, 5 vests `coverage: [Torso, Arm, Leg]`
  and 1 helmet `coverage: [Head]`, all `# WOLFGATE (WP11-3, P3-D6)`-marked (WP11-3's files, not touched by
  WP11-4).
- **§8.6-3 (host-gated vital Bloodloss charge):** confirmed `ChargeVitalPartLoss` still present in
  `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` (WP11-1's file, not touched by WP11-4).
- **§8.6-5 (Option B in WP11-4 if green):** **this WP's own scope**, honoured — Option A built clean with
  zero client/server console errors before Option B was attempted (report §4), satisfying the stated gate;
  Option B is fully implemented as verified throughout this document.
- **§8.6-8 (hand/foot visuals fold, P3-D25):** **this WP's own scope**, honoured — verified in item 1 above.

## 7. Snapshot

Written:
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-4.patch` (1161 lines — cumulative diff vs. the phase-2 commit,
  includes prior uncommitted phase-3 packages per the sequential no-commit model)
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-4.untracked.txt` (163 paths, including this WP's own new
  `DamageVisualsSystem.Wolfmed.cs` and both RSI directories, alongside earlier WPs' untracked files)

## 8. Test re-run (from the report's saved logs, cross-checked)

```
WP11-4-report-tests.log -> WolfmedVisualsTest.PartDamageProjectsToVisualsComponentTest
  Passed [1 m 32 s]; Total tests: 1, Passed: 1

WP11-4-report-server.log -> "[INFO] root: Server Version 277.0.0.0 -> Ready"
  grep -iE "\[ERRO\]|\[FATL\]|Exception|Duplicate" -> only benign "[WARN] system.chat: Duplicate of emote
  word …" lines (unrelated chat-emote dedup warnings, not subscription duplicates); zero real errors, zero
  `Duplicate Subscriptions` throw.
```

---

## Summary of findings

No blockers. No majors. One disclosed deviation (HOOK 20's third insertion for Option B's
`<BodyPartComponent, AfterAutoHandleStateEvent>` subscription, beyond §3's literal "two insertions" count)
assessed as authorised in substance by PLAN3 §2.3/§5.1 and DECISIONS.md §8.6-5, not undisclosed scope
creep — recorded as a minor plan-documentation gap, not a code defect. Both builds green, both hook sites
match PLAN3 §3 verbatim, RSI vendoring is byte-identical to Onyx apart from a trailing newline in
`meta.json`, no duplicate subscriptions, D2 held structurally, manifest complete, and every relevant
DECISIONS.md §8.6 answer (this WP's own §8.6-5/§8.6-8 plus the earlier-WP §8.6-1/2/3 items) is honoured in
the live tree.
