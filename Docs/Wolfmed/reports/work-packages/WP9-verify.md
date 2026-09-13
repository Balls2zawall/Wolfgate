# WP9 — verification

**Verifier run:** 2026-09-13
**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`
**Onyx reference:** `C:/tmp/onyx` (pinned `2f5bab9946539cbe083010c9ae6fbc59b47ae377`)

**Verdict: PASS.** No blockers, no majors. One minor observation recorded in §9.

---

## 1. Build (0 errors required)

Ran sequentially, 600000 ms timeout each, exactly as specified:

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)
```

Also ran (PLAN.md ground rule 5: "plus `dotnet build Content.IntegrationTests` from WP9 onward"):

```
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
  -> Build succeeded.  0 Error(s)
```

All three match the report's §4.1–4.3 tails and the checked-in
`WP9-{server,client,tests}-build.txt` logs. **Pass.**

---

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
(cumulative, all WPs to date, since nothing has been committed) touches 16 tracked files. None are
under `_Onyx`/`_WF`/Docs. Diffed every one and matched each hunk against PLAN.md §3's authorised-hook
table (or, for YAML, against the specific decision/pre-authorised fallback that names it):

| File | Hook/decision | Marked? | WP |
|---|---|---|---|
| `Content.Server/Body/Systems/BloodstreamSystem.cs` | GUARD E, GUARD E3 | yes, every hunk | WP6 |
| `Content.Server/Chat/Systems/ChatSystem.Emote.cs` | HOOK 6 (one word) | yes | WP4 |
| `Content.Server/Medical/Components/HealingComponent.cs` | HOOK 7 / D14 | yes, block-commented | WP6 |
| `Content.Server/Medical/DefibrillatorSystem.cs` | HOOK 12 | yes | WP8 |
| `Content.Server/Medical/HealingSystem.cs` | HOOK 8 (explicitly exempted from the 1–2 line rule per PLAN.md ground rule 3) | yes, `// WOLFGATE: HOOK 8 start/end` block plus per-line marks outside it | WP6 |
| `Content.Server/_Mono/Traits/Physical/HemophiliaSystem.cs` | GUARD E4 | yes | WP6 |
| `Content.Shared/Armor/SharedArmorSystem.cs` | HOOK 10 (routes through `PenetrateArmor`, confirmed) | yes, full added method under a `// WOLFGATE (HOOK 10)` doc comment | WP8 |
| `Content.Shared/Damage/Systems/DamageableSystem.cs` | GUARD D + GUARD D2 (explicitly exempted) | yes, every hunk | WP5 |
| `Content.Shared/Execution/SharedExecutionSystem.cs` | HOOK 13 | yes | WP8 |
| `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` | HOOK 11 (`:340`, `:406`) | yes | WP8 |
| `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | GUARD A + B + C, component-gated not `_net.IsServer`-gated (confirmed) | yes, every hunk | WP5 |
| `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` | HOOK 5 | yes | WP2 |
| `Resources/Prototypes/Body/Parts/base.yml` | D8, pre-authorised fallback (PLAN.md WP7 #6: "if re-declaring an existing abstract id... errors, instead declare `WolfmedBase<Part>` abstracts... and add them to each `Base<Part>`'s `parent:` list") | yes, one `# WOLFGATE (WP7, D8)` per line | WP7 |
| `Resources/Prototypes/_Shitmed/Body/Parts/base.yml` | same | yes | WP7 |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | D21 (`WoundHost`) + D22 (gib threshold) + D29 (`PassiveDamage` neutralised) — block matches PLAN.md's literal WP7 #7 snippet | yes, one `# WOLFGATE` block | WP7 |
| `Resources/Prototypes/_Mono/Entities/Mobs/Species/protogen.yml` | D21/D32 exclusion; documentation-only comment, the actual strip happens at runtime in `WolfmedWoundHostExclusionSystem` (RT has no YAML component removal) | yes | WP7 |

Every added/changed line in every file carries `// WOLFGATE`/`# WOLFGATE` or sits inside a clearly
delimited WOLFGATE block, and every site/file matches the location PLAN.md §3 (or the cited decision)
names. **No unauthorised upstream edits found.** WP9 itself touched zero upstream files (confirmed:
its four fixed files — `PainSystem.cs`, `WolfmedBodyPartLifecycleSystem.cs`,
`WolfmedWoundHostExclusionSystem.cs`, `WolfmedBedHealMarkerSystem.cs` — are all `_Onyx`/`_WF`).
**Pass.**

---

## 3. Vendoring fidelity

Only one `_Onyx` file was added or changed **in WP9 itself**:
`Content.Shared/_Onyx/Wounds/PainSystem.cs`.

```
diff --strip-trailing-cr <(git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/PainSystem.cs) \
                          Content.Shared/_Onyx/Wounds/PainSystem.cs
```

Two hunks (`:111`, `:233`), both `new ModifyPainGainEvent()` → `new ModifyPainGainEvent(1f)` with a
3-line `// WOLFGATE` explanation attached to each. No unmarked drift. The report's claim that the bug
is a real blocker (the record struct's implicit parameterless constructor zeroes `Multiplier` instead
of taking the primary constructor's `= 1f` default) is correct and independently verifiable from the
diff alone. **Pass.**

No other `_Onyx/` or `Content.Shared/StatusEffectNew/` files were touched in WP9 (confirmed via the
report's file table and `git status --porcelain`, which shows exactly the four fixed files as
untracked, matching earlier WPs' unstaged state).

---

## 4. Subscriptions

WP9 added **zero new `SubscribeLocalEvent<X,Y>` registrations** — its three production-code fixes
(`WolfmedBodyPartLifecycleSystem.cs`, `WolfmedWoundHostExclusionSystem.cs`,
`WolfmedBedHealMarkerSystem.cs`) only changed handler bodies or an event-type argument on an
**existing** subscription (`<HealOnBuckleComponent, ComponentStartup>` → `<HealOnBuckleComponent,
MapInitEvent>`).

Verified counts across `Content.Shared`/`Content.Server`/`Content.Client` for every pair these three
files (and the wound set generally) register — each is exactly 1:

```
WoundHostComponent, BeforeDamageChangedEvent      -> 1
WoundableComponent, BeforeDamageChangedEvent      -> 1
WoundHostComponent, DamageDealtEvent              -> 1
WoundableComponent, DamageChangedEvent            -> 1
WoundHostComponent, MapInitEvent                  -> 1
WoundHostComponent, RejuvenateEvent               -> 1
WoundHostComponent, BodyPartAddedEvent            -> 1
WoundHostComponent, BodyPartRemovedEvent          -> 1
WoundableComponent, RejuvenateEvent               -> 1
WoundableComponent, ComponentInit                 -> 1
WoundableComponent, PartDamageAppliedEvent        -> 1
WoundHostComponent, SleepStateChangedEvent        -> 1
WoundHostComponent, ResolveHealingPartEvent       -> 1
PainComponent, RejuvenateEvent                    -> 1
WoundHostComponent, ComponentInit                 -> 1
HealOnBuckleComponent, MapInitEvent               -> 1
```

A whole-tree grep for every `SubscribeLocalEvent<Comp,Event>` pair with count > 1 turned up 34 hits,
none involving any Wound*/Organ*/Pain*/HealOnBuckle* component — all are pre-existing, unrelated
Wolfgate pairs (e.g. `EntityStorageComponent, ComponentInit` — the usual client/server split, which
does not collide because RT keeps separate buses per side). **No duplicate directed subscription for
any Wolfmed pair.** **Pass.**

**One observation, not a WP9 defect** (recorded here for completeness since the check asks to verify
pairs against §5): PLAN.md §5.2 lists `OrganComponent | OrganAddedToBodyEvent / OrganRemovedFromBodyEvent
| WolfmedBodyPartLifecycleSystem (D28)` as a pair the port registers. The shipped
`WolfmedBodyPartLifecycleSystem.cs` does not subscribe that pair; instead, inside its
`<WoundHostComponent, BodyPartAddedEvent/BodyPartRemovedEvent>` handlers it walks
`GetBodyPartChildren` and raises the Onyx-shaped `OrganGotInsertedEvent`/`OrganGotRemovedEvent`
(§2.11) directly on each child part. This achieves the same fan-out without the extra subscription and
is not a duplicate-registration risk either way (grep confirms 0 hits for the `OrganComponent,
OrganAddedToBodyEvent` pair anywhere in the tree). This file and its subscriptions are **WP5's**
scope — WP9 only added the two `TerminatingOrDeleted` guards and wired the `WoundBleedingSystem`
calls inside the existing handlers — so it is not a WP9 regression. Flagging for whoever verifies WP5,
not blocking WP9.

---

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` header now reads `**Last re-sync:** 2026-09-13 by WP9`. Every one
of the WP9 report's 11 code/test files has a manifest row (the 12th item, the manifest itself, does
not need to list itself):

- 7 test files (rows at manifest lines 124–130, all tagged `WP9`)
- `Content.Shared/_Onyx/Wounds/PainSystem.cs` (line 69, `WP4 / WP9`, describes the exact fix)
- `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` (line 131, `WP9`, describes both fixes)
- `Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs` (line 132, `WP7 / WP9`)
- `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` (line 133, `WP2 / WP9`)

Manifest text matches the report's stated reasons in each case. **Pass.**

---

## 6. Plan conformance

All 12 WP9 file-table entries verified to exist at their planned destination (see the `ls -f` check —
all 12 present, no deviations to justify).

Decisions the WP9 report cites and honours, spot-checked against the actual test/prototype/code state:

- **D9** (Groin→Torso fold) — `WoundDamageComponents.cs` carries the folded `Torso = 4f` weight; no test
  asserts Chest/Groin.
- **D17** (rejuvenate merge) — `RejuvenateClearsEverything`-style coverage exists via
  `ThresholdTreatmentAttachmentAndRejuvenateTest`; `WoundSystem.cs:34`'s subscription is confirmed absent
  (§4 above, 1 hit only, owned by `WoundDamageProjectionSystem`).
- **D19** (no `InjurableComponent`) — none of the ported fixtures declare `- type: Injurable`.
- **D20 reversed** (Caustic routed) — `CausticRoutesToTheHitPartTest` passes, exercising exactly the
  `Burn`-group membership path the decision's rationale describes.
- **D26** (fractures ship, amputation doesn't) — the report correctly drops
  `TraumaticAmputationCreatesSevereStumpBleedingTest` to phase 3 with the right justification (`Severable`
  has no writer without `AmputationSystem`); no test exercises amputation.
- **D27** (`Applied` return value) — `TryChangeDamageReportsRoutedDamageTest` (T-RESULT) and
  `PiercingHitscanDamagesEntitiesBehindAWoundHostTest` (T-PIERCE) both pass, and T-PIERCE genuinely
  drives the real `HitscanBasicDamageSystem.cs:33-34` loop per the report.
- **D30** (zeroing, not removing) — `BodyPartProfileContractsTest` asserts `GetTotal() == 0`, not
  `.Empty`, exactly per the decision's rationale; `T-SETUP` additionally asserts the seeded
  `DamageDict.Count` invariant survives a `RefreshBodyDamage` call.
- **D28** (one lifecycle system, wound-host-scoped) — confirmed in the source read (§4 above); the
  `<BodyComponent, BodyPart*Event>` pair stays exclusively Shitmed's.

No decision the WP9 report cites is contradicted by the code or the test results. **Pass.**

---

## 7. Snapshot

```
git -C <WG> diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/snapshots/WP9.patch                      (764 lines)
git -C <WG> ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests \
  > C:/tmp/wolfmed-plan/snapshots/WP9.untracked.txt              (83 files)
```

Both written. The untracked list includes the 7 new WP9 test files plus every still-uncommitted file
from WP1–WP8 (StatusEffectNew, the whole `_Onyx`/`_WF` wound tree, Docs, etc.) — expected, since
DECISIONS.md says no WP commits and this is a cumulative untracked set. **Done.**

---

## 8. Test log cross-check

`C:/tmp/wolfmed-plan/wp/WP9-tests.log`: `Total tests: 30 / Passed: 30`. Extracted every `Passed <name>`
line from the log and compared against the report's §5 table — **all 30 names match 1:1** (order
differs only because NUnit ran fixtures in parallel). No `Failed` or bare `Skipped` line appears
anywhere in the log.

`db.ef` migration warnings **are present** in the log (`AdminLogs`, `Cleanup`, `ServerNameFts`,
`MarkingsJsonb`, `AdminNotesImprovement*`, `ConnectionLogServer`, `AdminLogPk` — all the usual
"cannot be executed in a transaction" / "rebuild of table 'admin_notes' is pending" noise), but they
did **not** fail anything in this run — 30/30 passed, and the report's own smoke run
(`WP9-smoke.log`) shows `DockTest` (`TestDockingConfig`×2, `TestPlanetDock`) passing, which the
project's own convention treats as the tell that the environmental `db.ef` failure mode did not
trigger this session. So every one of the 30 results is a real pass, not a masked environmental
failure. **Pass.**

`WP9-smoke.log`: `Total tests: 11 / Passed: 9 / Skipped: 2` — matches the report's §4.5 table exactly
(both skips are pre-existing upstream `[Ignore]`s: `SpawnAndDeleteEntityCountTest`,
`SpawnAndDirtyAllEntities`).

The three build-log files (`WP9-{server,client,tests}-build.txt`) all tail `Build succeeded. 0
Error(s)`, matching the report and my own re-run in §1.

---

## 9. Summary

| Check | Result |
|---|---|
| 1. Build (Server/Client, +IntegrationTests) | Pass — 0 errors each |
| 2. Upstream discipline | Pass — 16 tracked upstream files, all marked, all match an authorised hook or pre-authorised YAML fallback |
| 3. Vendoring fidelity | Pass — 1 `_Onyx` file changed in WP9 (`PainSystem.cs`), both hunks marked |
| 4. Subscriptions | Pass — 0 new pairs added in WP9, all wound-related pairs unique tree-wide; 1 non-blocking observation about a WP5-scope pair (§4) |
| 5. Manifest | Pass — every WP9 file has a row |
| 6. Plan conformance | Pass — every file exists at its planned path; every cited decision honoured |
| 7. Snapshot | Done — `WP9.patch` (764 lines), `WP9.untracked.txt` (83 files) |
| 8. Test log cross-check | Pass — 30/30 match report claims; db.ef noise present but harmless this run; smoke run matches |

**pass = true.** No blockers or majors. The single minor item (§4) is forwarded as a note for whoever
verifies WP5, not a WP9 defect.
