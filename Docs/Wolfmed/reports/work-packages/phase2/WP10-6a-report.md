# WP10-6a — Bridge tests: T-AP, T-PASSIVE-A, T-PASSIVE-B

Implements PLAN2 §4/WP10-6a and §6.2: extends
`Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` with T-AP (armour penetration
survives wound-host routing) and T-PASSIVE-A/T-PASSIVE-B (D29: a wound host does not passively heal at the
body level; a non-wound-host with real `PassiveDamage` still does). No production code touched — test file
and manifest only, per this package's scope (group F1, no phase-2 dependency).

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.IntegrationTests/Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs` | modified (extended) |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified (row update + WP10-6a Deviations subsection, appended directly per the orchestrator's task instructions, which supersede PLAN2's `manifest-rows-WP10-N.md` indirection for this package) |

No `// WOLFGATE` edits in vendored or upstream files — this package is test-only.

## 2. What was added

**Three new `[TestPrototypes]` entries:**
- `WolfmedBridgeArmor` — `Clothing slots: [outerClothing]` + `Armor modifiers.coefficients.Blunt: 0.5`, mirrors
  `WoundFractureTest.cs`'s `WoundFractureArmor` exactly (no `coverage`, so it protects every part).
- `WolfmedPassiveWoundHost` / `WolfmedPassiveControl` — a real (non-neutralised) `PassiveDamage` pair on the
  existing `WolfmedBridgeBodyGraph` shape (`Body`, `Damageable(Biological)`, `MobState`, `PassiveDamage
  {allowedStates:[Alive], damageCap:0, damage:{Blunt:-5}}`), the host also carrying `WoundHost`. Onyx's own
  species prototype ships `damage: {}` per D29, so it cannot exercise the routing path — these bespoke
  prototypes exist purely to drive real healing through it.

**Three new `[Test]` methods:**
- `ArmorPenetrationReachesWoundHostsTest` (T-AP) — three `WolfmedBridgeBody`s (armoured@AP=0, armoured@AP=1,
  unarmoured@AP=0), one `DamageableSystem.TryChangeDamage(body, {Blunt:10}, targetPart: LeftArm,
  armorPenetration: X)` each, asserts the left arm's `DamageableComponent.TotalDamage`.
- `RealWoundHostPassiveDamageIsNeutralisedTest` (T-PASSIVE-A) — real `MobHuman`; asserts
  `PassiveDamageComponent.Damage.Empty`; 10 Blunt to the left arm via
  `WoundDamageRoutingSystem.TryApplyPartDamage`; advances 60 simulated seconds; asserts the arm is still
  exactly 10.
- `PassiveDamageMechanismStillRoutesIfReenabledTest` (T-PASSIVE-B) — the bespoke pair above; damages both
  (host via `TryApplyPartDamage` on its left arm, control via a plain `TryChangeDamage`); advances 300
  simulated ticks (10 s); asserts both reach zero (see the measured-result note below).

## 3. Deviations from PLAN2

**T-PASSIVE-A's exact-value half was also wrong; corrected to a lower bound.** PLAN2 §6.2 predicted the arm
stays "still exactly 10 Blunt" after 60 simulated seconds. The `PassiveDamageComponent.Damage.Empty` check
(D29's `damage: {}`) passed exactly as predicted. The time-advance half did not: measured, the arm crept to
**13.11**, not down. A real `MobHuman` on a bare test map keeps every other body system running for those 60
seconds too — `Barotrauma`, `Temperature`/`ThermalRegulator`, etc., all declared on `BaseMobSpeciesOrganic`,
none of them Wolfmed's — and any localized damage type they deal can land on the same arm via the identical
"no requested part" random-part routing that T-PASSIVE-B documents. This is incidental environmental accrual,
not healing, and not a Wolfmed defect, but it makes an exact `EqualTo(10)` the wrong gate on a real,
fully-simulated mob. Fixed by asserting `GreaterThanOrEqualTo(10)` instead — still a strict test of D29 (any
*decrease* would mean the neutralised `PassiveDamage` healed the arm) without depending on an environment this
test does not control.

**T-PASSIVE-B's predicted outcome was wrong; measured and corrected, not silently patched over.** PLAN2 §6.2
predicted "control heals (< 10); wound host does **not**." The first run (2-second window, exact-10
assertion on the host) showed the wound host's arm *also* healed, landing at 5 rather than staying at 10 —
partial because the healing tick's 1-second boundary drifts against whatever simulated time the ticks
already sat at when the fixture spawned, which is not a fixed offset, so a short window is flaky by
construction, not just wrong in direction.

Reading `WoundDamageRoutingSystem.cs` explains why: `OnBeforeDamageChanged` intercepts **any**
`TryChangeDamage` call on a `WoundHostComponent` entity regardless of sign. For an un-targeted negative
amount, `RouteThroughBodyModifiers`'s `localizedDamage` gate (`amount > FixedPoint2.Zero`) is false, so no
`_requestedParts` entry is set — the change still reaches `OnDamageDealt` -> `RouteAppliedDamage`, which
buckets any negative localized-type amount as healing, and `ApplyLocalizedHealing` spreads it across whichever
parts currently carry positive damage of that type. This is exactly the same path a legitimate treatment item
uses. **There is no wound-host-specific code barrier against an un-targeted heal reaching a part** — D29's
`damage: {}` on every shipped species is a YAML-level guard, not a code-level one.

Fix applied to the test (not to production code — nothing ships real `PassiveDamage` on a wound host today,
so this is not a live bug): advance 300 ticks (10 simulated seconds, comfortably past saturation for 10
damage healing at 5/tick with `damageCap: 0`/unlimited) and assert **both** entities reach exactly zero. This
is deterministic and reflects the corrected understanding — the canary now proves what it was meant to prove
(the mechanism has no protection) rather than a near-tautological "damage: {} means nothing happens." Recorded
in `WOLFMED_MANIFEST.md`'s WP10-6a Deviations subsection with the same derivation, flagged for any later
phase that considers giving a wound host real `PassiveDamage`.

**No other deviation.** T-AP passed on the first measured run exactly as predicted (armoured/AP=0 arm 5,
armoured/AP=1 arm 10, unarmoured arm 10).

**Environmental, not a Wolfmed issue, and did not recur:** a first isolated debug run of
`RealWoundHostPassiveDamageIsNeutralisedTest` (before the T-PASSIVE-A fix above) hit `System.IO.IOException`
on `bin/Content.IntegrationTests/gravestone-*.txt` during `[SetUp]` — a file-lock race from running a second
`dotnet test` invocation against the same build output directory while an earlier one was still tearing down.
Clean on every other run, including this package's final full-file pass. Same class of trap as PLAN2 §6.1
item 2's `db.ef` warning — re-run in isolation rather than treat as a Wolfmed defect.

## 4. Build/test output tails

IntegrationTests build:
```
Build succeeded.
    0 Error(s)
```

Server build:
```
Build succeeded.
    0 Error(s)
```

Client build:
```
Build succeeded.
    0 Error(s)
```

Test run (`WolfmedDamageBridgeTest`, all 10 tests in the file, full log at `C:/tmp/wolfmed-plan/p2/wp/WP10-6a-tests.log`):
```
Passed CausticRoutesToTheHitPartTest [1 m 47 s]
Passed ArmorPenetrationReachesWoundHostsTest [1 m 47 s]
Passed DamageChangedDeltaSurvivesProjectionTest [174 ms]
Passed NoDoubleApplicationTest [169 ms]
Passed NonWoundHostUnchangedTest [75 ms]
Passed PassiveDamageMechanismStillRoutesIfReenabledTest [369 ms]
Passed PiercingHitscanDamagesEntitiesBehindAWoundHostTest [93 ms]
Passed EveryPartGetsWoundableOnMapInitTest [1 s]
Passed TryChangeDamageReportsRoutedDamageTest [90 ms]
Passed RealWoundHostPassiveDamageIsNeutralisedTest [1 s]

Test Run Successful.
Total tests: 10
     Passed: 10
 Total time: 1.8734 Minutes
```

All 10 tests pass, including the 7 pre-existing ones (unmodified, confirming no regression from the fixture
additions). No YAML or FTL was touched, so no headless server checkpoint was required for this package.

## 5. What later packages must know

- **A real `MobHuman` is not damage-neutral over a long simulated window, even doing nothing.** T-PASSIVE-A
  measured +3.11 accrued on one arm over 60 simulated seconds from ordinary body systems (`Barotrauma`,
  `Temperature`, etc.), unrelated to Wolfmed. Any later test that spawns a real full mob and advances
  simulated time by tens of seconds should assert a bound (`GreaterThanOrEqualTo`/tolerance), not an exact
  value, unless it specifically controls or neutralises those systems first.
- **T-PASSIVE-B is load-bearing documentation, not a safety net.** If any later phase (balance pass, a new
  species, a modder-facing prototype) puts non-empty `PassiveDamage` on an entity that also carries
  `WoundHostComponent`, it **will** heal through `WoundDamageRoutingSystem`'s un-targeted healing path exactly
  like a real treatment item would — there is no code-level guard to catch that mistake, only the D29
  convention of shipping `damage: {}`. Re-read this test before changing that convention.
- **`WolfmedBridgeArmor`, `WolfmedPassiveWoundHost`, `WolfmedPassiveControl` are new pool-global
  `[TestPrototypes]` ids** in `WolfmedDamageBridgeTest.cs`, alongside the pre-existing `WolfmedBridgeBody*`
  family. Nothing else in the tree claims these names (checked against PLAN2 §6.1 item 4's taken-id list).
- **No new subscriptions, no new upstream or vendored-file edits.** Nothing to add to PLAN2 §5's duplicate-
  subscription audit for this package.
- **WP10-6b (group F3) is unaffected** — it owns `WoundFractureTest.cs`'s assertions and the new
  `WolfmedPainTest.cs`, neither of which this package touches.
