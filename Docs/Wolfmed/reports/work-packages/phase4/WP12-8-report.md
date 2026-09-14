# WP12-8 — Explosion amputation (PLAN4 §4 WP12-8 / P4-6 / P4-D14 / HOOK 22)

Branch `clanker/wolfmed-port-orchestration-454c3d`, worktree
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`. Uncommitted, as required.

## 1. Files created / modified

| File | Status | Size of change |
|---|---|---|
| `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | **modified** (vendored `_Onyx`), 3 marked sites | +40 / −2 |
| `Content.Server/_WF/Wolfmed/Explosion/WolfmedExplosionSystem.cs` | **new** (`_WF`) | 45 lines |
| `Content.Server/Explosion/EntitySystems/ExplosionSystem.Processing.cs` | **modified** (upstream) — HOOK 22 | +4 (3 marked lines + 1 blank) |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — `### WP12-8` section appended directly | +596 |

Nothing else was touched. `ExplosionSystem.CVars.cs`, `ExplosionSystem.cs`, `CCVars.Wounds.cs`,
`SharedArmorPlateSystem.cs`, `AmputationSystem.cs`, all YAML/FTL/XAML/RSI and every file under
`Content.IntegrationTests` are unchanged. `WG/RobustToolbox` untouched. No git state-changing command was run.

## 2. Every WOLFGATE edit and its reason

### Upstream — `ExplosionSystem.Processing.cs` (HOOK 22, 3 marked lines, one file)

1. `:5` — `using Content.Server._WF.Wolfmed.Explosion; // WOLFGATE: HOOK 22`. Needed for the dependency's type name.
2. `:32` — `[Dependency] private WolfmedExplosionSystem _wolfmedExplosion = default!; // WOLFGATE: HOOK 22`, placed
   in this partial (not `ExplosionSystem.cs`) so the entire hook lives in one upstream file.
3. `:474` — `if (!_wolfmedExplosion.TryApplyExplosionDamage(entity, damage)) // WOLFGATE: HOOK 22 - wound hosts
   split the blast across their limbs; everyone else falls through unchanged.` directly above the existing
   `_damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true, ignoreGlobalModifiers: true, /* Mono:
   Explosion flag for plate protection */ originFlag: DamageableSystem.DamageOriginFlag.Explosion);` call. The call
   and its Mono comment are unchanged byte-for-byte. The `if` is intentionally brace-free: bracing it would have
   meant three marked lines plus a re-indent at a site PLAN4 §3.1 budgets at one, and the guarded statement is a
   single statement. **D2:** `TryApplyExplosionDamage` returns `false` immediately for anything without
   `WoundHostComponent`, so non-hosts reach today's call with today's arguments.

### Vendored `_Onyx` — `WoundDamageRoutingSystem.cs` (3 sites, 10 marked lines)

1. `TryApplyDistributedDamage` gains an **optional trailing** `DamageableSystem.DamageOriginFlag? originFlag = null`
   and a `_routedModifiers` scope around the call: save the previous entry, write `(0f, null, originFlag)`, restore
   (never bare-remove) in `finally`. Reason: the distributed entry points bypass `OnBeforeDamageChanged`, the only
   other writer of `_routedModifiers`, so `RouteThroughBodyModifiers`' read at `:686-688` handed
   `originFlag: null` to `ChangeDamage`, and `SharedArmorPlateSystem.OnBeforeDamageChanged`'s gate
   (`SharedArmorPlateSystem.cs:60`, `args.Origin == null && args.OriginFlag != Explosion → return`) refused plate
   protection against explosions outright. That is the P3-D3 hole PLAN4 describes. Save/restore is PLAN4 §8.5 trap
   T3's shape.
2. The original method body is now `private bool ApplyDistributedDamageCore(...)`, byte-for-byte (see deviation 1).
3. `TryRouteDistributedDamage` gains the same optional parameter and forwards it as the 11th argument.

### New `_WF` file

`Content.Server/_WF/Wolfmed/Explosion/WolfmedExplosionSystem.cs` — `_WF` style (no licence header, one-line
`/// <summary>`, `[Dependency] private X _x = default!;` without `readonly`). Two `Subs.CVar` reads of
`CCVars.ExplosionLimbDamageVariation` (2f) and `CCVars.ExplosionWoundMultiplier` (4f) — their first consumer since
phase 1 — and `TryApplyExplosionDamage(EntityUid, DamageSpecifier)`, which gates on `HasComp<WoundHostComponent>`
and then calls `TryRouteDistributedDamage(TargetBodyPart.All, DamageDistribution.SplitWithVariation,
ignoreResistances: true, interruptsDoAfters: false, variation, isExplosion: true, woundSeverityMultiplier,
originFlag: Explosion)`. Because the CVars are read here, **`ExplosionSystem.CVars.cs` needed no edit at all**,
unlike Onyx.

### Audits run before writing

* **Subscriptions:** this WP registers **zero** directed `(component, event)` pairs — matching PLAN4 §5.1's row for
  WP12-8. `Subs.CVar` is not a directed subscription. `WoundDamageRoutingSystem`'s three registrations at `:64-66`,
  including both `before: [typeof(SharedArmorPlateSystem)]` orderings, are byte-identical to HEAD (D23, §5.4,
  §8.5 trap 22).
* **Component names:** none registered.
* **Type names:** `WolfmedExplosionSystem` grepped repo-wide → 0 hits before creation. `ApplyDistributedDamageCore`
  is a private method, also 0 hits.
* **Prototype / locale ids:** none added.

## 3. Deviations from PLAN4

1. **The `_routedModifiers` scope is a wrapper + renamed private core, not an in-place `try`/`finally`.**
   §2.11 sketches `try { … } finally { … }` inside `TryApplyDistributedDamage`. That body is ~95 lines with three
   `return` points, so an in-place `try` re-indents the whole method — a 95-line whitespace diff in a vendored file
   carrying ~25 existing `// WOLFGATE` marks, which would make a future Onyx re-sync materially harder to read.
   `ApplyDistributedDamageCore` keeps Onyx's body verbatim and the public wrapper is 18 lines. Semantics are
   identical: the core keeps its own `_routing.Contains(body)` re-entrancy guard, and the wrapper's `finally`
   restores any outer entry before another reader can see it. The plan's three authorised sites are all present.
2. **The wrapper writes `_routedModifiers[body]` before the core's guard runs**, so a call the guard refuses briefly
   holds a `(0f, null, originFlag)` entry. No damage is applied in that window and the `finally` restores the
   previous value, so it is observationally inert; duplicating the guard in the wrapper was judged worse.
3. **`ignoreGlobalModifiers` is not carried into the routed pass.** Vanilla explosion damage passes
   `ignoreGlobalModifiers: true`; `RouteThroughBodyModifiers` calls `WolfmedDamageableSystem.ChangeDamage` without
   it. **Pre-existing phase-1 behaviour, not introduced here** — explosion damage on a wound host has been routed
   through `OnBeforeDamageChanged` since GUARD D and that path never carried the flag either. Recorded because this
   is the package that makes explosions a designed wound-host mechanic. Closing it is a one-argument change in
   `RouteThroughBodyModifiers` that would affect every routed hit, so it belongs to the balance pass, not here.
4. **`TargetBodyPart.All` contains the `Groin` bit** (D9 / §8.5 trap 18). No Groin key is ever emitted:
   `WoundTargetResolver.GetMatchingParts` enumerates the body's real children and maps each through
   `GetTargetBodyPart`, so a bit with no matching part contributes nothing. Phase 3's existing
   `TryRouteDistributedDamage` call already used this mask.

No other deviation. Onyx's headers and namespaces are untouched; `GroupHealSpecifier` is not involved in this WP.

## 4. Build / test output

```
dotnet build Content.Server/Content.Server.csproj -c DebugOpt
Build succeeded.
    0 Error(s)

dotnet build Content.Client/Content.Client.csproj -c DebugOpt
Build succeeded.
    0 Error(s)

dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt
Build succeeded.
    0 Error(s)
```

Headless server, 120 s, `net.port=1299` (`WP12-8-report-server.log`):

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
```

Run even though no YAML/FTL/XAML/RSI was touched, because HOOK 22 adds a new `[Dependency]` edge
(`ExplosionSystem -> WolfmedExplosionSystem`) that would throw at entity-system init if it failed to resolve.

Tests (`WP12-8-report-tests.log`), `DockTest` first per project memory, then the whole amputation class:

```
dotnet test --filter "FullyQualifiedName~DockTest|FullyQualifiedName~WolfmedAmputationTest"
  Passed TestDockingConfig(<0.5, 1.5>,<0.5, 1.5>,0 rad,0 rad,False) [20 ms]
  Passed DecapitationNeverReducesVitalDamageTest [41 s]
  Passed TestPlanetDock [769 ms]
  Passed ExplosionAmputatesDeterministicallyTest [212 ms]
  Passed OverflowAmputationMechanismIsInertOnShippedLimbsTest [88 ms]
  Passed RepeatedAmputationCreatesSeparateConsequenceWoundsTest [50 ms]
  Passed GunsAndLasersAmputateOverThresholdLimbsTest [601 ms]
Test Run Successful. Total tests: 8  Passed: 8  Total time: 45.9405 Seconds
```

`ExplosionAmputatesDeterministicallyTest` is phase 3's `T-AMP-EXPLOSION` and is the regression gate demanded by
WP12-8's checkpoint: it still passes with the new optional parameter defaulting to `null`.

**Not run, because they do not exist:** `T-EXPLOSION-PLATE` and `T-EXPLOSION-WRAPPER` (WP12-9). There is **no
armour-plate test anywhere in `Content.IntegrationTests`** — `grep -rln "ArmorPlate\|Plate" Content.IntegrationTests/Tests`
returns nothing — so the plate half of P4-D14 is presently backed by code reading only, not by an assertion.

## 5. What later packages must know

* **WP12-9 owns the gate for this package.** `T-EXPLOSION-PLATE` and `T-EXPLOSION-WRAPPER` are unwritten, and
  §8.4-1 ships this feature gated on them. Concretely:
  * `T-EXPLOSION-PLATE`: a wound host wearing an `ArmorPlateHolder` with an active plate, hit via
    `WolfmedExplosionSystem.TryApplyExplosionDamage` (or a real explosion), must lose plate durability / absorb
    damage. Before this WP it would not have. `SharedArmorPlateSystem.OnBeforeDamageChanged` is `public`, so the
    handler can also be asserted directly.
  * `T-EXPLOSION-WRAPPER`: `TryApplyExplosionDamage` returns `false` and applies nothing for an entity without
    `WoundHostComponent` (D2), and `true` for a host. The system is resolvable as
    `entities.System<WolfmedExplosionSystem>()` from `Content.Server._WF.Wolfmed.Explosion`.
  * A useful third assertion: `_routedModifiers` restore. Call `TryApplyDistributedDamage` with an `originFlag`
    while a routed pass is open (it will be refused by the `_routing` guard) and confirm a subsequent normal hit
    still carries its armour penetration.
* **`WolfmedAmputationTest.ExplosionAmputatesDeterministicallyTest`'s `<remarks>` at `:404-407` is now stale** — it
  says "D24/P3-D3 keep ExplosionSystem unhooked, so nothing in the game reaches this today". As of this WP the
  path is live. WP12-9 owns every file under `Content.IntegrationTests`, so I did not edit it; please update that
  block when you touch the file.
* **New public API surface** for anyone else who needs the plate flag: `TryApplyDistributedDamage` and
  `TryRouteDistributedDamage` both take a trailing `DamageableSystem.DamageOriginFlag? originFlag = null`. Pass it
  whenever you call a distributed entry point on behalf of a damage source that sets an origin flag, otherwise
  plate protection will silently not engage for that source.
* **Balance knobs for the playtest writeup (WP12-10):** `explosion.damage_variation` (2f → per-limb weight roll up
  to 3x) and `explosion.wounding_multiplier` (4f → wound severity) are live for the first time and are
  `CVar.SERVERONLY`. Setting them to `0` and `1` respectively gives an even, unamplified split. The mechanic itself
  has no off switch other than removing `WoundHostComponent`.
* **Guidebook (WP12-10):** explosions can now sever a limb on a wound host, and armour plates protect against
  explosions. Both are worth a sentence in the `Wounds` entry alongside §8.6-1's gun/laser severing.
* **Upstream-file count:** `ExplosionSystem.Processing.cs` is a first-time Wolfmed touch; it had no `// WOLFGATE`
  marker before this package. WP12-10's reconcile should count it once.
