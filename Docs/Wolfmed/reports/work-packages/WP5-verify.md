# WP5 verification — The damage bridge (D2, D11, D23, D27, D28)

**Verifier scope:** WG worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`,
branch `clanker/wolfmed-port-orchestration-454c3d`. Onyx pin confirmed `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
(`git -C C:/tmp/onyx log -1` matches DECISIONS.md). No files modified except this report and the WP5 snapshot.

**Verdict: PASS.** No blockers, no majors. One out-of-scope minor noted (belongs to WP4, not WP5).

---

## 1. Build

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"
Build succeeded.
    0 Error(s)
```

Both green, matches the report. **PASS.**

## 2. Upstream discipline

```
$ git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
 Content.Server/Chat/Systems/ChatSystem.Emote.cs    |  2 +-
 Content.Shared/Damage/Systems/DamageableSystem.cs  | 26 +++++++++++++++++++---
 .../Body/Systems/SharedBodySystem.Targeting.cs     | 12 +++++++++-
 .../_Shitmed/Targeting/SharedTargetingSystem.cs    |  4 ++++
 4 files changed, 39 insertions(+), 5 deletions(-)
```

Matches the WP5 report's "whole-port upstream footprint" exactly. All four files are outside `_Onyx`/`_WF`.

- **`Content.Server/Chat/Systems/ChatSystem.Emote.cs`** — one line, `public void` → `public override void
  TryEmoteWithChat(` with an inline `// WOLFGATE: HOOK 6...` marker. Matches **HOOK 6** (PLAN §3, WP4) / D25
  exactly: only the string-`emoteId` overload at `:60` touched, the `EmotePrototype` overload untouched.
  Authorised for an earlier WP, still valid.
- **`Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs`** — adds static `IsSelectable`, fully inside
  a `// WOLFGATE:` comment block. Matches **HOOK 5** (PLAN §3, WP2). Authorised for an earlier WP.
- **`Content.Shared/Damage/Systems/DamageableSystem.cs`** — every changed/added line carries an inline
  `// WOLFGATE` marker or sits inside one: 2 new usings, 1 query field + assignment, the `BeforeDamageChangedEvent`
  construction site (armorPenetration/tool args), the cancel-return change (`before.Applied`), the ~14-line
  GUARD D seam block, and the 3 appended record members. Matches **GUARD D** + **GUARD D2** (PLAN §3, WP5)
  line-for-line, including the ~10-line budget and the defensive copy (D-WP5-2, justified in the report as a
  bug fix — verified below in §3).
- **`Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`** — every changed/added line marked:
  1 using, 1 query field + assignment, GUARD A's early-return in `OnTryChangePartDamage`, GUARD B's sever-condition
  addition, GUARD C's two additions (tick condition + job-enqueue skip). Matches **GUARDs A/B/C** (PLAN §3, WP5)
  exactly, including PLAN's cited line ranges (`:63/:66+:108`, `:222-234`, `:80-85`/`:101`).

No unauthorised upstream edits. No file outside the §3 list was touched. **PASS.**

Files explicitly forbidden from touching by D24/§3's "not touched" list
(`SharedProjectileSystem`, `HitscanBasicDamageSystem`, `SharedMeleeWeaponSystem`, `DamageOtherOnHitSystem`,
`DamageOnInteractSystem`, `ExplosionSystem`, `BedSystem.cs`, `SharedStaminaSystem.cs`,
`ThermalRegulatorSystem.cs`, `CrewMonitoringConsoleSystem.cs`, `SharedCryoPodSystem.cs`,
`SharedDoAfterSystem.cs`, `MobStateSystem*.cs`, `RespiratorSystem.cs`, `OrganComponent.cs`,
`BodyPartType.cs`, `HumanoidVisualLayers.cs`, `TargetBodyPart.cs`) — confirmed absent from the diff by grep.
**PASS.**

## 3. Vendoring fidelity

Files under `_Onyx/` that WP5's own report attributes to WP5 (D-WP5-6: "WP5 added only [the D23/D27 edits to
WoundDamageRoutingSystem.cs]; WoundDamageProjectionSystem and MobThresholdSystem were already fully delivered
by WP4"):

### `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` (WP5's actual edit surface)

`diff --strip-trailing-cr` against `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs`
(1015 → 1085 lines). Every one of the ~30 differing hunks carries a `// WOLFGATE` marker, either inline or in
an immediately preceding comment covering the whole block:

- 3 using swaps (D8/D10/D12/D23-ordering) — all marked.
- `_damage`/`_targetResolver` dependency swaps + new `_wfPart` — all marked (D12/D10/D8).
- New `_routedModifiers`/`_appliedDelta` side tables — marked (D23/D27).
- Both `BeforeDamageChangedEvent` subscriptions gain identical `before: [typeof(SharedArmorPlateSystem)]` —
  the reasoning comment sits above the first subscription only; the second (`:52`→`:66`) has no comment of
  its own but is the mandatory paired half of the same edit (PLAN §3 GUARD D trap: "RT requires... identical
  before/after sets... apply to both or neither") and is explicitly itemised in the WP5 report's edit table
  as one entry ("`:50` and `:52`"). Not counted as unmarked drift.
- `args.Cancelled` early-return, the try/finally restructure with the Shitmed `targetPart` handoff, the
  `AccumulateApplied` helper, `TargetBodyPart.Chest`→`.Torso` (×2), the hand-symmetry rewrite (comment leads
  the whole contiguous block), the D23 armour/tool/originFlag read at the routed `ChangeDamage` call, the
  three `AccumulateApplied` write points, the `_wfPart.Get(...)` reads (×3, D8), the `_body.GetParentPartOrNull`
  substitution (D8), the `WolfmedBedHealMarkerComponent` swap (D10/§2.10) — all individually marked.

**No unmarked drift found in the WP5-attributed file. PASS.**

### `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` and `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs`

Not WP5's edit surface per the report (D-WP5-6), but diffed anyway for completeness since PLAN §4's WP5 file
table lists them as items #2/#3:

- **`WoundDamageProjectionSystem.cs`** (43 differing lines against `git -C C:/tmp/onyx show HEAD:...`): every
  hunk carries a marker inline or in an immediately-adjacent comment (D12 facade swap, D15 dependency drop +
  dead-call comment, `after: [typeof(SharedBodySystem)]` swap, D11 event re-point on both the subscription and
  the handler signature/body, D17 `ClearBodyWounds` call, D8 parent-walk substitution, D19's `InjurableComponent`
  block deletion, D9's `Chest`→`Torso` visual-layer case). No unmarked drift.
- **`MobThresholdSystem.cs`** (verified via `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs`,
  which resolves even though the path is outside the sparse checkout, exactly as the report/PLAN note): the
  functional changes (2 new usings, added `_damageable` dependency, `BodyPartType.Chest`/`Groin` → `.Torso` in
  the `criticalParts` array) are all marked. **One unmarked hunk**: the XML-doc comment
  `/// Calculates the total damage from vital body parts (Head, Chest, Groin), ...` was updated to
  `(Head, Torso)` with no `// WOLFGATE` marker on that line — a documentation-only line, 14 lines away from
  the nearest marked functional change. **Not attributed to WP5 by the report** (D-WP5-6 says this file's
  edits were all WP4's), so it is out of this verification's scope and is not counted against WP5's
  pass/fail. Flagged as a minor for whoever verifies WP4, or for a later cleanup pass.

## 4. Subscriptions

New `SubscribeLocalEvent<X, Y>` pairs added in WP5 (both in the new
`Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`):

| Pair | Registrant |
|---|---|
| `WoundHostComponent`, `BodyPartAddedEvent` | `WolfmedBodyPartLifecycleSystem.Initialize` |
| `WoundHostComponent`, `BodyPartRemovedEvent` | `WolfmedBodyPartLifecycleSystem.Initialize` |

No other new `SubscribeLocalEvent` pair was introduced in WP5 (the routing-system edits only added optional
parameters/`before:` ordering to two *existing* subscriptions, not new pairs).

Grep across the whole tree (excluding the new file) for the same two events:

```
Content.Client/Hands/Systems/HandsSystem.cs:58        <HandsComponent, BodyPartRemovedEvent>
Content.Server/Hands/Systems/HandsSystem.cs:56-57      <HandsComponent, BodyPartAddedEvent/BodyPartRemovedEvent>
Content.Server/_Goobstation/.../MantisBladesSystem.cs  <MantisBladeArmComponent, BodyPartAddedEvent/BodyPartRemovedEvent>
Content.Server/_Mono/CorticalBorer/CorticalBorerInfestedSystem.cs  <CorticalBorerInfestedComponent, BodyPartRemovedEvent>
Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.PartAppearance.cs:25-26  <BodyComponent, BodyPartAddedEvent/BodyPartRemovedEvent>
```

No existing subscriber registers `<WoundHostComponent, BodyPartAddedEvent>` or
`<WoundHostComponent, BodyPartRemovedEvent>`. **No duplicate. PASS.**

Matches PLAN.md §5.2's row exactly ("verified, only `BodyComponent`, `HandsComponent`,
`MantisBladeArmComponent` and `CorticalBorerInfestedComponent` hold those events today") and D28 (never
`<BodyComponent, …>`, which Shitmed's `PartAppearance.cs:25-26` already owns).

Also verified: `new BeforeDamageChangedEvent(...)` has exactly one construction site in the whole tree
(`DamageableSystem.cs:214`), matching GUARD D2's "positional record struct, one construction site" claim
that appending three optional members is safe.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` rows 84–87 cover all four files WP5 touched:

- `Content.Shared/Damage/Systems/DamageableSystem.cs` — WP5, GUARD D + D2.
- `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` — WP5, GUARDs A/B/C.
- `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` — WP5 (with a WP4 row above it correctly
  distinguishing "WP4 (WP5/WP8 parts pending)" from the "WP5" row for the D23/D27 half).
- `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` — WP5, D28.

A `### WP5` Deviations subsection (4 entries: the defensive copy, the possibly-empty `Applied`, the
`OrganComponent` pairs left unclaimed, the asymmetric lifecycle handlers) and 3 new/updated Hazards entries
are present, matching the report's claim. Header re-sync line reads "Last re-sync: 2026-09-12 by WP5".
**PASS.**

## 6. Plan conformance

WP5's file table (PLAN §4, 6 entries) checked against disk:

| # | File | Exists at destination? |
|---|---|---|
| 1 | `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | yes |
| 2 | `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` | yes (edits pre-existing from WP4, per D-WP5-6 — justified in report) |
| 3 | `Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs` | yes (same justification) |
| 4 | `Content.Shared/Damage/Systems/DamageableSystem.cs` | yes |
| 5 | `Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs` | yes |
| 6 | `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs` | yes |

Decisions D2, D11, D23, D27, D28 cited by the WP5 heading, all honoured:

- **D2** (Onyx routing owns part damage for wound hosts) — GUARDs A/B/C gate Shitmed's spread/sever/regen on
  `WoundHostComponent` presence; confirmed in the diffs above.
- **D11** (split the two `DamageDealtEvent` subscriptions) — routing keeps the pre-write `DamageDealtEvent`
  (GUARD D); projection re-points to the post-write `DamageChangedEvent` on `WoundableComponent`, confirmed
  in the `WoundDamageProjectionSystem.cs` diff (subscription + handler signature + body all updated together).
- **D23** (armour penetration/tool threaded through) — `BeforeDamageChangedEvent` gains the two members
  (GUARD D2), routing's `_routedModifiers` side table stores them and the routed `ChangeDamage` call reads
  them back; confirmed.
- **D27** (`Applied` survives the cancel) — `BeforeDamageChangedEvent.Applied`, `TryChangeDamage` returning
  `before.Applied`, routing's `_appliedDelta` accumulator with all three claimed write points
  (`RouteAppliedDamage`, `ApplyPartChange`, `ApplySystemicDamage`) — all present and marked.
- **D28** (one `WolfmedBodyPartLifecycleSystem`, `<WoundHostComponent, BodyPart*Event>` only) — confirmed;
  file subscribes exactly those two pairs, never `<BodyComponent, …>`.

D-WP5-4 (report's own deviation: `<OrganComponent, OrganAddedToBodyEvent/OrganRemovedFromBodyEvent>` from
PLAN §5.2 not subscribed) is justified in both the report and the manifest's WP5 Deviations section
(`FractureEffectsSystem`, the only consumer, is phase 2 and unported) and correctly recorded as "free and
unclaimed" rather than silently dropped. **PASS.**

## 7. Snapshot

```
$ git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP5.patch
$ git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP5.untracked.txt
```

`WP5.patch`: 161 lines (the 4 upstream-hook files' diffs). `WP5.untracked.txt`: 59 files, including both
WP5 additions (`Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`,
`Docs/Wolfmed/WOLFMED_MANIFEST.md`) plus every earlier WP's untracked file. **Written.**

---

## Summary

| # | Check | Result |
|---|---|---|
| 1 | Build (Server + Client, DebugOpt) | PASS — 0 errors both |
| 2 | Upstream discipline | PASS — 4 files, all authorised (GUARD A/B/C/D/D2, HOOK 5/6), every line marked |
| 3 | Vendoring fidelity | PASS — WP5's file (`WoundDamageRoutingSystem.cs`) fully marked; 1 minor doc-comment gap found in a WP4-attributed file, out of WP5 scope |
| 4 | Subscriptions | PASS — 2 new pairs, both free, match PLAN §5.2 |
| 5 | Manifest | PASS — all 4 WP5 files have rows; Deviations + Hazards updated |
| 6 | Plan conformance | PASS — all 6 file-table entries present; D2/D11/D23/D27/D28 honoured; D-WP5-4 deviation justified |
| 7 | Snapshot | Written — `WP5.patch` (161 lines), `WP5.untracked.txt` (59 files) |

**No blockers. No majors.** One minor (unmarked XML-doc comment in
`Content.Shared/_Onyx/Mobs/Systems/MobThresholdSystem.cs`, attributable to WP4 not WP5) noted for hygiene,
not gating this WP.

**pass = true**
