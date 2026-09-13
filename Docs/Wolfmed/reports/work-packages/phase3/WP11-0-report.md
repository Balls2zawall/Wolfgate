# WP11-0 — Zero-dependency test debt (T-REATTACH + T-VISUALS)

Phase 3, PLAN3 §4 WP11-0. Executed against WG worktree
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`, phase 2 committed
(`1171e02fb6`). No uncommitted phase-3 work was present in the tree at start.

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedReattachTest.cs` | **new** — T-REATTACH |
| `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedVisualsTest.cs` | **new** — T-VISUALS |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 2 new rows (top table) + `### WP11-0` narrative section |

**No production code was touched.** Both tests passed on the first run against already-shipped phase-1/2 code;
no `// WOLFGATE` edit was needed anywhere.

## 2. WOLFGATE edits

None. This package is test-file-only, per its own goal ("close two test gaps in already-shipped code, before
any phase-3 code can muddy the blame").

## 3. Deviations from PLAN3

None. Both tests were written and pass exactly to PLAN3 §6.2's T-REATTACH/T-VISUALS specification:

- **T-REATTACH** (`WolfmedReattachTest.ReattachedPartRejoinsWoundTrackingTest`): bespoke one-arm
  `WolfmedReattachBody` fixture (Shitmed `body:` graph + `MobBloodstream`, `WoundHost`). Detaches the arm via
  `WolfmedBodySystem.TryDetachPart`, re-attaches via `SharedBodySystem.AttachPart(torso, "left arm", arm)`,
  then asserts: `WoundableComponent`/`DamageableComponent` still present; `WolfmedBodyPartSystem.Get(arm)
  .AmputationThresholds` still reads `{Slash 130, Piercing 250, Blunt 250}` (`WolfmedBaseLeftArm`, P3-D13,
  prototype data never mutated at runtime); a fresh `Slash 15` hit via
  `WoundDamageRoutingSystem.TryApplyPartDamage` creates exactly one new `SlashWound` on the arm and raises
  `BloodstreamComponent.BleedAmount` above zero.
- **T-VISUALS** (`WolfmedVisualsTest.PartDamageProjectsToVisualsComponentTest`): real `MobHuman`, targeted
  `Blunt` hits on the left arm and left hand via `TryApplyPartDamage`. Asserts
  `Comp<PartDamageVisualsComponent>(body).Damage[HumanoidVisualLayers.LArm]` and `[.LHand]` each equal the
  dealt amount, `.RArm`/`.RHand` are absent-or-zero, and the identical reads succeed against `Pair.Client`'s
  networked mirror entity (`clientEntities.GetEntity(entities.GetNetEntity(body))` after `Pair.RunTicksSync(10)`
  — `PartDamageVisualsComponent` is `[NetworkedComponent, AutoNetworkedField]`, so no extra hook is needed for
  state to arrive; this is unrelated to P3-4's HOOK 21, which only gates *detached*-part state). No
  sprite/screenshot assertion, per project convention (logic tests only, PLAN3 §6.2's own instruction for this
  row).

Both mechanisms under test were already correctly wired in phase 1/2 (`WolfmedBodyPartLifecycleSystem
.OnPartAdded` → `WoundDamageProjectionSystem.OnPartInserted` + `WoundBleedingSystem.OnPartInserted` for
T-REATTACH; `WoundDamageProjectionSystem.RefreshBodyDamage`/`TryGetVisualLayer` for T-VISUALS), so no fix was
required — the plan's own framing ("if it is broken, WP11-4 would look like the culprit") is now closed off
for WP11-4.

## 4. Build / test output tails

**Content.IntegrationTests build:**
```
Build succeeded.
    0 Error(s)
```

**Content.Server build:**
```
Build succeeded.
    0 Error(s)
```

**Content.Client build:**
```
Build succeeded.
    0 Error(s)
```

**DockTest** (`C:/tmp/wolfmed-plan/p3/wp/WP11-0-docktest.log`) — clean, no `db.ef`/`admin_notes` warning noise:
```
  Passed TestDockingConfig(<0.5, 0.5>,<0.5, 0.5>,0 rad,0 rad,True) [28 s]
  Passed TestDockingConfig(<0.5, 1.5>,<0.5, 1.5>,0 rad,0 rad,False) [18 ms]
  Passed TestPlanetDock [717 ms]

Test Run Successful.
Total tests: 3
     Passed: 3
```

**Combined wound suite** (`--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed"`,
`C:/tmp/wolfmed-plan/p3/wp/WP11-0-tests.log`):
```
  Passed ReattachedPartRejoinsWoundTrackingTest [132 ms]
  Passed PartDamageProjectsToVisualsComponentTest [218 ms]
  ...
Test Run Successful.
Total tests: 41
     Passed: 41
 Total time: 54.1729 Seconds
```

39 pre-existing wound/Wolfmed tests plus the 2 new ones = 41, all green, on the first run (no retry needed).

## 5. What later packages must know

- **WP11-1 through WP11-6 can build on a confirmed-working reattachment and visuals-projection substrate.**
  Neither `WolfmedBodyPartLifecycleSystem` nor `WoundDamageProjectionSystem` needs any change for these two
  mechanisms; if either test regresses after a later WP, treat it as that WP's regression, not a pre-existing
  gap.
- **WP11-4 (limb damage visuals, Option A/B):** T-VISUALS already pins the exact server-side data WP11-4's
  client reader will consume, including the P3-D25 fold anchor (`LHand`/`RHand`/`LFoot`/`RFoot` keys exist in
  `PartDamageVisualsComponent.Damage` today with zero client consumer). WP11-4 should re-run
  `WolfmedVisualsTest` as its own regression gate rather than re-deriving the server-side numbers.
- **`WolfmedReattachTest.cs` and `WolfmedVisualsTest.cs` are now claimed files** — per PLAN3 §4 serialisation
  rule, new test files belong to whichever WP created them (WP11-0), so later packages should add new tests
  elsewhere (their own new files, or the WP11-5-owned `WoundBleedingTest.cs` / new `_WF/Wolfmed/*.cs` files)
  rather than editing these two.
- **`[TestPrototypes]` id `WolfmedReattachBody`/`WolfmedReattachBodyGraph`** are now taken in the global pool
  (PLAN3 §4 rule 3) — re-grep before reusing.
- No CCVar was touched, no manifest row from an earlier phase needed correction, and no hazard from PLAN3 §8.7
  was triggered by this package.
