# WP11-3 — Per-part (locational) armour (PLAN3 §4 / P3-3, Option B)

**Status: complete and green.** Three builds 0 errors, Release YAML lint clean, headless server clean,
`DockTest` 3/3, `FullyQualifiedName~Wolfmed|FullyQualifiedName~_Onyx.Wounds` **46/46**, and the wider PLAN3 §6.3
gate `_Onyx.Wounds|_Onyx.Body|Wolfmed` **48/48**.

---

## 1. Files created / modified

| # | Path (all under WG) | Status |
|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/Armor/ArmorComponent.Wolfmed.cs` | **new** (PLAN3 §2.1) |
| 2 | `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | **modified** — handler rewritten, `Covers` added, `<remarks>` updated |
| 3 | `Resources/Prototypes/_Mono/Entities/Clothing/Head/Helmets/bulletproof_helmets.yml` | **modified** — PROTO B, 1 line |
| 4 | `Resources/Prototypes/_Mono/Entities/Clothing/OuterClothing/Armor/bulletproof_vests.yml` | **modified** — PROTO B, 5 lines |
| 5 | `Content.IntegrationTests/Tests/_Onyx/Wounds/WoundDamageFoundationTest.cs` | **modified** — 4 new `[TestPrototypes]` fixtures, 5 new tests, 1 stale comment corrected |
| 6 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 5 rows appended + a `### WP11-3` deviations section |

No upstream C# file was touched. `Content.Shared/Armor/ArmorComponent.cs` and
`Content.Shared/Armor/SharedArmorSystem.cs` are byte-unchanged (PLAN3 §3 "explicitly NOT touched"); the
upstream component was **already** `public sealed partial class`, so the pre-authorised one-word `partial`
edit was not needed.

**Subscriptions registered: none.** `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` has been
`WolfmedPartArmorSystem`'s since phase 1; Onyx's own registration at `SharedArmorSystem.cs:30` was deliberately
not added (duplicate directed pair = server-start crash).

---

## 2. Every `// WOLFGATE` / `# WOLFGATE` edit and its reason

`_WF` files carry no marker convention (PLAN3 §3), so files 1 and 2 are unmarked by design; their divergences
are documented in-file and in the manifest.

| Site | Edit | Reason |
|---|---|---|
| File 1, header | Block comment naming P3-D5, the no-`[AutoNetworkedField]` rule and the two non-ported Onyx keys | the file is a re-open of an upstream type from a `_WF` path; the comment is what stops a re-sync putting it back in `Content.Shared/Armor` |
| File 1 | `Coverage` / `CoverageSymmetry` declared `HashSet<T>?` (Onyx: non-nullable `= []`) | PLAN3 §2.1. `null` and empty both mean "protects everything"; nullable makes "unset" representable without allocating a set on 272 armours |
| File 1 | `PartModifiers` + `[DataDefinition] ArmorPartModifier` with **no** `[AutoNetworkedField]` | `ArmorPartModifier` is not `NetSerializable`; the fields are prototype-static so the client has them at spawn |
| File 2, `OnPartDamageModify` | Onyx's ordered first-match-wins `PartModifiers` loop, then `if (!Covers(...)) return;`, then the global fallback | P3-D5 Option B. Gate **after** the loop — Onyx's own `<Onyx-ArmorGlobalProtection>` comment scopes coverage to the fallback; putting it first makes a `partModifiers` entry unreachable outside `coverage` |
| File 2, both branches | `DamageSpecifier.PenetrateArmor(set, args.Args.ArmorPenetration)` | D23 / §8.7 risk 1. Onyx does not wrap the `partModifiers` branch because Onyx has no AP at all; without the wrap every AP weapon silently dies against any armour with a part profile |
| File 2 | Onyx's `MaskComponent.IsToggled` early-return **not** ported | base-game drift; WG has no mask check in any of its four armour handlers, and adding it only here would make a toggled-down mask armour the torso and not the head |
| File 3 | `coverage: [Head] # WOLFGATE (WP11-3, P3-D6): locational armour coverage` | PROTO B / DECISIONS §8.6-2 |
| File 4 (×5) | `coverage: [Torso, Arm, Leg] # WOLFGATE (WP11-3, P3-D6): locational armour coverage` | PROTO B. `Chest`/`Groin` never emitted (D9 — no such `BodyPartType`; the Release lint rejects them) |
| File 5, fixtures | `# WOLFGATE (WP11-3, P3-D5)` block over the four new prototypes | records the D9 `[Chest]`→`[Torso]` mapping and the `WoundFoundationArmorAll` → `WoundFoundationArmorAllHead` rename |
| File 5, 5 tests | a `// WOLFGATE` derivation comment at every expected value | P2-D16. The two assertions that are **red against Onyx's own pin** say so explicitly |
| File 5, `AppliesArmorExactlyOnceTest` | comment rewritten (assertions untouched) | the old comment claimed "Wolfgate's `ArmorComponent` has none of the three", which this package makes false |

---

## 3. Deviations from PLAN3

**None substantive.** Everything below is a PLAN3-anticipated choice or a strictly smaller change than planned.

1. **The pre-authorised `partial` keyword edit on `ArmorComponent.cs` was not needed** — the upstream class is
   already `public sealed partial class ArmorComponent : Component` with no `[Access]`. Zero upstream edits, as
   §2.1 predicted.
2. **Phase 1's `AppliesArmorExactlyOnceTest` was extended, not replaced.** PLAN3 §6.2 requires it to stay green
   *unchanged* at head 5 / torso 5, and it does; only its now-false comment changed. The task brief's
   "replacing/extending" is resolved in favour of PLAN3's explicit instruction.
3. **The file `WoundDamageFoundationTest.cs` was already modified by WP11-1** (two P3-D1 literals), despite
   §4 serialisation rule 2 naming WP11-3 as its owner. WP11-1 recorded that deviation; my edits are additive and
   in different methods, so nothing was clobbered.
4. **Two extra test-run artefacts** beyond the brief: `DockTest` (PLAN3 §6.1 trap 1) and the wider
   `_Onyx.Body` filter, both green.

Anticipated-but-not-done, exactly as PLAN3 directs: Option C (slot-derived coverage) and its CCVar; the
272-entry content pass; `traumaDeductions` (P3-D20); `ShowArmorOnExamine`; the armour-examine covered-parts
line; part-aware `_Mono` armour plates.

---

## 4. Build / test output tails

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

Headless server (`WP11-3-report-server.log`, 120 s, `net.port=1299`):

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
$ grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
```

Release YAML lint (`WP11-3-report-lint.log`):

```
No errors found in 271900 ms.
```

`DockTest` (`WP11-3-report-docktest.log`): `Total tests: 3  Passed: 3`.

Tests (`WP11-3-report-tests.log`, the brief's filter):

```
Test Run Successful.
Total tests: 46
     Passed: 46
```

with, individually:

```
Passed AppliesArmorExactlyOnceTest [160 ms]            (pre-existing, unchanged)
Passed NonWoundHostUsesVanillaArmorTest [102 ms]       (pre-existing, unchanged)
Passed ArmorPenetrationReachesWoundHostsTest [141 ms]  (pre-existing, unchanged)
Passed AppliesLocationalArmorExactlyOnceTest [121 ms]           T-P3ARM-1
Passed EmptyCoverageAndSymmetryTest [106 ms]                    T-P3ARM-2
Passed LocationalModifierOverridesAndFallbackTest [88 ms]       T-P3ARM-3
Passed PartModifiersRouteThroughArmorPenetrationTest [128 ms]   T-P3ARM-AP
Passed UncoveredPartIgnoresArmorPenetrationTest [90 ms]         T-P3ARM-UNCOVERED-AP
```

Wider PLAN3 §6.3 gate (`WP11-3-report-tests-full.log`): `Total tests: 48  Passed: 48` (43 before this package,
+5 new).

**Derived expected values, all measured not assumed:** T-P3ARM-1 head 5 / torso 10; T-P3ARM-2 torso 5 (head-slot
armour, unset coverage), leftArm 5 / rightArm 10; T-P3ARM-3 head 5 / torso 16 / leftArm 10 / rightArm 15;
T-P3ARM-AP head 20 at AP 1 vs 5 at AP 0; T-P3ARM-UNCOVERED-AP torso 10 at both AP 0 and AP 1.

**One environmental failure, resolved:** the first test run failed 46/46 with
`ReflectionTypeLoadException: Method 'ReceiveLocalRayAtMainThread' in type 'Robust.Server.Debugging.DebugRayDrawingSystem'
… does not have an implementation` — a stale `Robust.Server.dll` (10:23) beside a newer `Robust.Shared.dll`
(10:57) in `bin/Content.IntegrationTests`, i.e. the RobustToolbox junction was rebuilt mid-package from the main
checkout. Rebuilding `Content.IntegrationTests` once refreshed it; `DockTest` then passed and so did everything
else. Nothing in Wolfmed was at fault and no RT file was touched. **If a later package sees a wall of identical
SetUp failures, rebuild the test project before investigating.**

---

## 5. What later packages must know

1. **`ArmorComponent` now has `Coverage`, `CoverageSymmetry` and `PartModifiers`**, declared from
   `Content.Shared/_WF/Wolfmed/Armor/ArmorComponent.Wolfmed.cs` in `namespace Content.Shared.Armor`. `null` and
   empty both mean "protects everything" — any new consumer must preserve that or it inverts all 272 unannotated
   armours in the game.
2. **`ArmorPartModifier` is a new `[DataDefinition]` type name.** It is now taken.
3. **Do not add `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` anywhere.**
   `WolfmedPartArmorSystem.cs:28` owns it; Onyx registers the same pair in `SharedArmorSystem.cs:30` and copying
   that is a server-start crash.
4. **Any new armour modifier path must wrap `DamageSpecifier.PenetrateArmor`** (D23), and any new coverage-style
   gate must sit **after** the `PartModifiers` loop. Two tests pin each of those.
5. **Content annotation is now a live lever.** Only the five `_Mono` vests and the one `_Mono` light ballistic
   helmet declare `coverage:`. Adding `coverage:` to any other `- type: Armor` is a real balance change from that
   moment on — it is no longer inert data. WP11-6 / the balance pass owns the other 266 entries; `Hand`/`Foot`
   on the vests is a one-line content edit if playtest asks for it.
6. **For the status doc (WP11-6), the aimed-headshot change is the thing to surface** (DECISIONS §8.6-2): aimed
   **torso** protection is unchanged, aimed **head** damage for a vest wearer goes **2.8 → 11.2** from a single
   14-Piercing round (×4), because vests stop covering the head while unannotated helmets — `ClothingHeadHelmetSwat`
   included — keep covering the torso. A heavy vest's average mitigation against a random *unaimed* bullet falls
   75.0 % → 52.9 %. Aimed hand/foot shots bypass an annotated vest entirely (non-lethal: limb damage does not
   feed `CheckVitalDamage`).
7. **Interaction with WP11-1's amputation, now that both are live:** a heavy vest (`Slash 0.30`) turns a 32-Slash
   machete into 9.6, below the Slash-15 finishing minimum, so **a machete can no longer finish a heavy-vest
   wearer's torso, arm or leg** — while that wearer's head, hands and feet now take the full 32. Worth one line
   in the balance notes.
8. **WP11-4 must not touch** `WolfmedPartArmorSystem.cs`, `ArmorComponent.Wolfmed.cs`, the two `_Mono` armour
   YAML files, or `WoundDamageFoundationTest.cs`.
