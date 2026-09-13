# WP10-4 verify — Pain HUD overlay

**Verdict: PASS**

## 1. Build

Both builds run sequentially, 0 errors:

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

## 2. Upstream discipline

`git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 9 modified tracked files (cumulative tree state; packages run sequentially with no commits between
WPs, so earlier WPs' edits are still present uncommitted). Files not under `_Onyx`/`_WF`:

| File | WP | Verdict |
|---|---|---|
| `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs` | **WP10-4** | 2 one-line edits, both WOLFGATE-marked, both verbatim-match PLAN2 §3 HOOK 15 (a) the `TryApplyWolfmedPain();` call after the eye/viewport guard and before the lerp block, and (b) `darknessAlphaOuter` -> `0.8f * level` citing `ONYX Content.Client/DamageOverlay/DamageOverlay.cs:175`. No new `using`. |
| `Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs` | **WP10-4** | 1 condition edit, WOLFGATE-marked, matches PLAN2 §3 HOOK 16 exactly: `&& !WolfmedPainOwnsVignette(entity)` appended to the existing Mono guard. No new `using`. |
| `Content.Client/Examine/ExamineSystem.cs` | WP10-2 (earlier) | HOOK 14, both sites WOLFGATE-marked, text matches PLAN2 §3 exactly (Popup width 400->560; `TryAddPartStatusMessage` wrap). Not this WP's scope; confirmed authorised and unchanged from spec. |
| `Content.Server/Body/Systems/BloodstreamSystem.cs` | WP10-2 (earlier) | GUARD E2, 1 line, WOLFGATE-marked, matches PLAN2 §3 verbatim. Not this WP's scope. |
| `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` | WP10-2 (earlier) | GUARD F, 5 sites, all WOLFGATE-marked (using, call-site param, signature, wound-host wrap open, `else` branch), matches PLAN2 §3 verbatim including the P2-D20 `else`-placement deviation. Not this WP's scope. |

No unauthorised or oversized upstream edit found anywhere in the diff. WP10-4's own two hook files are each
exactly the "one or two lines, body in `_WF` partial" shape PLAN2 §3 and DECISIONS.md's phase-2 rule require.

`Content.Shared/_Onyx/Wounds/PainSystem.cs`, `Resources/Prototypes/_Onyx/Wounds/wounds.yml`,
`Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl`, `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs`
are all under `_Onyx`, from earlier WPs (WP10-3's `IsPainNumb` widening, WP10-1's fracture-multiplier fix and
FTL cleanup, WP9's ported test) — out of scope for WP10-4's own upstream-discipline check; see §6 below for
spot-checks of the decisions they carry.

## 3. Vendoring fidelity

**N/A for this WP.** WP10-4 adds/changes zero files under `_Onyx/` — its only new files are the two `_WF`
partials, and its only touched files are the two upstream hook sites. No Onyx diff to check.

## 4. Subscriptions

Zero `SubscribeLocalEvent<Component, Event>` pairs added by WP10-4. `DamageOverlay` is an `Overlay` and
`DamageOverlayUiController` is a `UIController`; neither can hold a directed subscription — confirmed by
reading both new `_WF` files (a private method and a private predicate, no `Initialize`/subscription code)
and by grepping `SubscribeLocalEvent` across the touched/new files: the only hits are the four pre-existing
broadcast subscriptions in `DamageOverlayUiController.cs:28-31` (`LocalPlayerAttachedEvent`,
`LocalPlayerDetachedEvent`, `MobStateChangedEvent`, `MobThresholdChecked`), none of them component-directed
and none added by this WP. Matches PLAN2 §5 (WP10-4 is absent from §5.1's registered-pairs table) and the
report's own claim ("Subscriptions registered: zero").

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a row for all 4 WP10-4 files (lines 155-158: the two hook files
and the two `_WF` partials, each tagged **WP10-4**) plus a "WP10-4 (phase 2 — pain HUD overlay)" Deviations
subsection matching the report point-for-point (line-drift note, P2-D6 confirmation, CRITIQUE2 m6 ordering
confirmation, the un-remeasured-numbers caveat, and the `System<T>()`-vs-`[Dependency]` note).

## 6. Plan conformance

All 4 files in PLAN2's WP10-4 table exist at their planned destinations with the planned status (2
"modified (hook)", 2 "new"). No deviation from PLAN2 §4/WP10-4 — report and manifest both say "None"/"no
deviation" and the diff confirms it: every character of both hook edits and both new partial bodies matches
§3's and §4's quoted code blocks (including the exact WOLFGATE comment text, the `State == MobState.Dead`
bail, the 0.05 floor, and `FixedPoint2.Min(1f, ...)`for the pain-level clamp).

Decisions cited by WP10-4 itself (DECISIONS.md §8.2-4/5/6: HOOK 15 + HOOK 16 authorised, bodies in `_WF`
partials) are honoured exactly as specified.

Spot-checked the other §8.2 answers this task lists, even though they belong to different WPs, to confirm
overall tree health does not block WP10-4:
- **§8.2-1 fracture multipliers:** `Resources/Prototypes/_Onyx/Wounds/wounds.yml` shows
  `manipulationModifier: 1.1/1.25/1.5/2.0` for Hairline/Simple/Displaced/Comminuted, with a `# WOLFGATE
  (DECISIONS §8.2-1)` comment explaining the correction. Landed (WP10-1), honoured.
- **§8.2-3 pain sounds:** HOOK 17/HOOK 18 (`Content.Server/Chat/EmoteOnDamageComponent.cs` and
  `EmoteOnDamageSystem.cs`) are WP10-5's scope (group F2) and have not landed in this tree yet (no diff, no
  manifest rows) — expected at this point in the sequence, not a WP10-4 defect.
- **§8.2-8 part status wound-hosts-only:** confirmed via `HealthExaminableSystem.cs`'s GUARD F wrap —
  `AddPartStatusMarkup` is called only from the `else` of `if (!HasComp<WoundHostComponent>(uid))`, matching
  P2-D20. Landed (WP10-2), honoured.

## 7. Build/functional notes carried forward

- `PainComponent.SoftPainCap` (WoundDamageComponents.cs:137) confirmed to have **no** `[AutoNetworkedField]`
  attribute (spot-checked against neighbouring fields, which do carry it) — the report's forward-looking
  desync warning for later WPs is accurate.
- `PainSystem.GetPain(Entity<PainComponent?>)` (PainSystem.cs:165) signature matches the call site in
  `DamageOverlay.Wolfmed.cs`.

## Snapshot

Written:
- `C:/tmp/wolfmed-plan/p2/snapshots/WP10-4.patch` (full cumulative tracked diff under
  Content.Shared/Content.Server/Content.Client/Resources/Docs/Content.IntegrationTests — 11 files, includes
  earlier in-flight WPs' uncommitted changes since packages run sequentially with no commits between them)
- `C:/tmp/wolfmed-plan/p2/snapshots/WP10-4.untracked.txt` (15 untracked files, all from earlier/other WPs —
  none belong to WP10-4 except the two already-tracked-as-new `_WF` partials, which `git ls-files --others`
  correctly lists since they are new files: `DamageOverlay.Wolfmed.cs`, `DamageOverlayUiController.Wolfmed.cs`)

## Result

No blockers, no majors. WP10-4 is a clean, minimal, spec-exact implementation: two one-line/one-condition
upstream hooks, both WOLFGATE-marked and PLAN2 §3-authorised, with all logic correctly isolated in two new
`_WF` partials; zero new subscriptions; manifest fully updated; no vendoring surface to check; both builds
green.
