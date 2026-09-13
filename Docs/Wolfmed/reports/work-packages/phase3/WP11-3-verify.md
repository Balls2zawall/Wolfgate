# WP11-3 verify — Per-part (locational) armour (P3-3, Option B)

**Verdict: PASS.** No blockers, no majors. Every check below is against the live worktree state (which
also carries WP11-0/1/2's uncommitted work, per the sequential-package model — noted where relevant but
not re-audited).

---

## 1. Build (0 errors each)

```
dotnet build Content.Server/Content.Server.csproj  -c DebugOpt -v q -nologo  -> Build succeeded. 0 Error(s)
dotnet build Content.Client/Content.Client.csproj  -c DebugOpt -v q -nologo  -> Build succeeded. 0 Error(s)
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo -> Build succeeded. 0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 9 files changed. WP11-3's own 5 code/content files (report §1, items 1-5; item 6 is the manifest,
checked in §5 below):

| File | Under `_Onyx`/`_WF`? | Marked? | Authorised by |
|---|---|---|---|
| `Content.Shared/_WF/Wolfmed/Armor/ArmorComponent.Wolfmed.cs` (new) | `_WF` — no marker convention | n/a | PLAN3 §2.1, P3-D5 |
| `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | `_WF` — no marker convention | n/a | PLAN3 §2.1/§3, P3-D5 (handler rewrite pre-authorised verbatim in PLAN3's own code block) |
| `Resources/Prototypes/_Mono/…/bulletproof_helmets.yml` | Not `_Onyx`/`_WF` (content) | **Yes** — every changed line carries `# WOLFGATE (WP11-3, P3-D6): locational armour coverage` | PLAN3 §3 table row **PROTO B**, DECISIONS §8.6-2 |
| `Resources/Prototypes/_Mono/…/bulletproof_vests.yml` | Not `_Onyx`/`_WF` (content) | **Yes**, all 5 lines | same |
| `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | `_Onyx` path, but a test file (§4 rule 3 pool, not vendored production code) | **Yes** — fixture block and every new/changed assertion carries a `// WOLFGATE` derivation comment | PLAN3 §6.2 test table, §4 serialisation rule 2 |

Confirmed byte-unchanged (report's claim): `git diff`/`git status --porcelain` on
`Content.Shared/Armor/ArmorComponent.cs` and `Content.Shared/Armor/SharedArmorSystem.cs` both empty — the
pre-authorised one-word `partial` edit genuinely wasn't needed, exactly as the report says.

Handler rewrite verified line-for-line against PLAN3's prescribed code block (§WP11-3 "File 2"): the
`PartModifiers` first-match-wins loop, both branches wrapping `DamageSpecifier.PenetrateArmor` (D23), the
`Covers()` gate placed **after** the loop, and no `MaskComponent.IsToggled` port — all present exactly as
specified, with the same reasoning in the `<remarks>` block.

**D2 (no behaviour change for non-wound-hosts):** structurally verified, not just claimed.
`grep -rn "PartDamageModifyEvent" Content.Shared Content.Server Content.Client` shows exactly one raise
site, `WoundDamageRoutingSystem.cs:741` (inside the routing path that only wound hosts reach), and one
subscriber, `WolfmedPartArmorSystem.cs:28`. A non-wound-host's armour never sees the new coverage/partModifiers
logic and keeps going through `SharedArmorSystem`'s original global-modifier path untouched.

**Also checked (in scope of the diff but not this WP's files, from earlier phase-3 packages still
uncommitted in the tree):** `Content.Server/_Onyx/Body/Systems/OrganHealthSystem.cs`,
`Content.Server/_Onyx/Wounds/OrganDamageSystem.cs` (both `_Onyx`, `// WOLFGATE`-marked, WP11-1/2 territory),
`Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` and `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml`
(`_WF`, no marker required), and `Resources/Prototypes/Body/Organs/human.yml` (upstream content, but every
changed line is `# WOLFGATE (WP11-2, D8)`-marked). None of these are WP11-3 deliverables; they are
pre-existing, already-marked state from WP11-1/WP11-2's own (separately verified) work and are not
re-litigated here beyond confirming they don't contaminate WP11-3's own checks above.

**No unauthorised or oversized upstream edits found. No blockers/majors in this section.**

## 3. Vendoring fidelity

**N/A for this WP.** WP11-3 adds no file under `_Onyx/`. The Onyx sources it draws from
(`ArmorComponent.Locational.cs`, `SharedArmorSystem.cs:108-129`) are re-authored into a new `_WF` file and
a rewritten `_WF` handler, per PLAN3 §2.1/§4 — not vendored verbatim — so there is nothing to diff against
`git -C C:/tmp/onyx show HEAD:<path>`.

## 4. Subscriptions

`git diff HEAD -- Content.Shared Content.Server Content.Client | grep "^+.*SubscribeLocalEvent"` — **zero
hits** across the whole tree. WP11-3 adds no new `SubscribeLocalEvent` call; it only rewrites the body of
`OnPartDamageModify`, whose subscription (`<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>`)
has existed since phase 1.

`grep -rn "SubscribeLocalEvent<ArmorComponent" Content.Shared Content.Server Content.Client --include=*.cs`
shows the pair registered exactly once (`WolfmedPartArmorSystem.cs:28`); Onyx's own registration site
(`SharedArmorSystem.cs:30`) was correctly **not** added, matching PLAN3 §5.1/§5.2's explicit warning that
doing so is a duplicate-directed-subscription server-start crash.

Matches PLAN3 §5: WP11-3 is listed as registering nothing, and it registers nothing.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for every file WP11-3 touched: `ArmorComponent.Wolfmed.cs`,
`WolfmedPartArmorSystem.cs`, both `_Mono` YAML files, and `WoundDamageFoundationTest.cs` (lines 191-195),
plus a `### WP11-3` deviations section (from line 980) covering the Option B divergence from Onyx's own
(non-functional) coverage gate, the nullable `Coverage`/`CoverageSymmetry` deviation, the AP-wrap rationale,
the dropped mask gate, D2 scoping, and the measured PROTO B balance consequences (aimed head 2.8 → 11.2,
unaimed heavy-vest mitigation 75.0% → 52.9%, the machete/vest finishing interaction). All match the report.

## 6. Plan conformance

All 6 files in the WP11-3 table exist and match PLAN3's prescription:

1. `ArmorComponent.Wolfmed.cs` — new, exact structure from PLAN3 §2.1 (nullable `Coverage`/`CoverageSymmetry`,
   `PartModifiers` list, `ArmorPartModifier` `[DataDefinition]`, no `[AutoNetworkedField]`). **Confirmed**: the
   pre-authorised `partial` keyword edit on upstream `ArmorComponent.cs` genuinely wasn't needed (already
   `sealed partial` with no `[Access]`) — a smaller change than planned, not a deviation.
2. `WolfmedPartArmorSystem.cs` — handler body matches PLAN3's code block verbatim (see §2 above).
3/4. PROTO B — exactly 6 lines across 2 files, `coverage: [Head]` on the sole helmet block and
   `coverage: [Torso, Arm, Leg]` on all 5 vest blocks, every line `# WOLFGATE (WP11-3, P3-D6)`-marked, no
   `Chest`/`Groin` emitted (D9). Confirmed by `grep -rn "^\s*coverage:" Resources/Prototypes` that these are
   the **only** 6 `- type: Armor` blocks in the entire repo carrying the new bracket-list `Coverage` field —
   every other `coverage:` hit in that grep belongs to an unrelated field (`MaskComponent`/glasses
   `EYES`/`MOUTH` enum coverage, a different component entirely). **Exactly 5 vests + 1 helmet annotated,
   confirmed structurally, not just by count.**
5. `WoundDamageFoundationTest.cs` — 4 new fixtures + 5 new tests, matching PLAN3 §6.2's T-P3ARM-1/2/3/AP/
   UNCOVERED-AP table line for line (setup, derived values, and the "RED against Onyx's own shipped code"
   callouts). All 4 new `[TestPrototypes]` ids (`WoundFoundationArmorHead`, `WoundFoundationArmorAllHead`,
   `WoundFoundationArmorLeftArm`, `WoundFoundationArmorLocational`) each declared exactly once and referenced
   only within this file — no collisions.
6. `WOLFMED_MANIFEST.md` — appended, checked in §5.

**DECISIONS.md §8.6 answers relevant to this WP's scope:**
- **§8.6-2 (armour annotation):** honoured exactly — PROTO B, 5 vests + 1 helmet, verified above.
- The other three items named in the verify brief (§8.6-1 guns/lasers sever via Piercing-12/Heat-15+200
  thresholds; §8.6-3 host-gated vital Bloodloss charge; §8.6-8 hand/foot visuals fold) are **not WP11-3's
  scope** — they belong to WP11-1 (`parts.yml`, `WolfmedBodyPartLifecycleSystem.cs`) and WP11-4
  respectively. Spot-checked in the ambient diff for sanity only (not re-audited as WP11-3 deliverables):
  `Resources/Prototypes/_WF/Wolfmed/Body/parts.yml` does carry the `# WOLFGATE (P3 balance, DECISIONS.md
  §8.6-1)` block with `Heat` thresholds mirroring each part's `Piercing` threshold and
  `dismembermentFinishingDamage: {Piercing: 12, Heat: 15}`, and `WolfmedBodyPartLifecycleSystem.cs` does carry
  `ChargeVitalPartLoss` gated on its own `<WoundHostComponent, BodyPartRemovedEvent>` subscription (P3-D1/
  §8.6-3) — both present and consistent with the decisions, but their correctness is those WPs' own verify
  responsibility, not re-derived here.

## 7. Snapshot

Written:
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-3.patch` (1021 lines — cumulative diff vs. the phase-2 commit,
  includes prior uncommitted phase-3 packages per the sequential no-commit model)
- `C:/tmp/wolfmed-plan/p3/snapshots/WP11-3.untracked.txt` (6 paths, including this WP's own new
  `ArmorComponent.Wolfmed.cs` alongside earlier WPs' untracked new files)

## 8. Test re-run

```
dotnet test ... --filter "FullyQualifiedName~DockTest"
  -> Passed: 3, Failed: 0, Total: 3

dotnet test ... --filter "FullyQualifiedName~WoundDamageFoundationTest" --logger "console;verbosity=detailed"
  -> Passed: 15, Failed: 0, Total: 15
     (includes all 3 pre-existing armour tests unchanged — AppliesArmorExactlyOnceTest,
     NonWoundHostUsesVanillaArmorTest is in WolfmedDamageBridgeTest, not re-run here but not touched by
     this WP's diff — plus the 5 new T-P3ARM-* tests, all green)

dotnet test ... --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"
  -> Passed: 48, Failed: 0, Total: 48   (matches the report's claimed wider PLAN3 §6.3 gate exactly)
```

Confirmed **exactly** 5 vests + 1 helmet annotated (§6 above). Armour tests pass (15/15 in the owning file,
48/48 in the full phase-3 filter).

---

## Summary of findings

No blockers. No majors. No minors worth recording — the report's own "Deviations from PLAN3" section (all
4 items) are each independently verified above as either PLAN3-anticipated or strictly smaller than
planned, not undisclosed drift.
