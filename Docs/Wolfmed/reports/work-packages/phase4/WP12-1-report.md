# WP12-1 — Reagent effect classes and HOOK 9 (P4-1a)

Worktree `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (**WG**),
Onyx pin `2f5bab9` at `C:/tmp/onyx`. Nothing committed; `WG/RobustToolbox` untouched.

## 1. Files created / modified

| # | Path (WG-relative) | Status |
|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/EntityEffects/SuppressPain.cs` | **new** (39 lines) — old-style rewrite of Onyx `Content.Shared/_Onyx/Wounds/SuppressPainEntityEffect.cs` |
| 2 | `Content.Shared/_WF/Wolfmed/EntityEffects/MendFractures.cs` | **new** (61 lines) — old-style rewrite of Onyx `ReagentTreatmentEffects.cs:27-54` + `ReagentTreatmentSystems.cs`'s `MendFracturesEntityEffectSystem` |
| 3 | `Content.Shared/_WF/Wolfmed/EntityEffects/TakeStaminaDamage.cs` | **new** (36 lines) — old-style rewrite of Onyx `Content.Shared/_Onyx/Chemistry/TakeStaminaDamageEntityEffectSystem.cs` |
| 4 | `Content.Shared/_WF/Wolfmed/EntityEffects/StaminaDamageCondition.cs` | **new** (28 lines) — old-style rewrite of Onyx `Content.Shared/_Onyx/Chemistry/StaminaDamageCondition.cs` |
| 5 | `Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` | **new** (8 keys) — Onyx's bodies, WG key convention |
| 6 | `Content.Server/EntityEffects/Effects/HealthChange.cs` | **modified — HOOK 9(a)**, +17 -2 |
| 7 | `Content.Server/EntityEffects/Effects/EvenHealthChange.cs` | **modified — HOOK 9(b)**, +18 -2 |
| 8 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — `### WP12-1` section appended (7 rows + notes) |

All four C# files are ASCII/CRLF; the FTL is UTF-8/CRLF (Onyx's curly quotes preserved). Both upstream C#
files were ASCII/CRLF before and after — verified with `file` after editing.

## 2. Every WOLFGATE edit and its reason

**`Content.Server/EntityEffects/Effects/HealthChange.cs` — HOOK 9(a), 3 marked sites**

1. `:6` — `using Content.Shared._Onyx.Wounds; // WOLFGATE: HOOK 9 - treatment-capability scope`. Needed for
   `TreatmentCapability`, `WoundHostComponent`, `WoundDamageRoutingSystem`.
2. `:41-43` — `// WOLFGATE: HOOK 9 - which body-part materials this healing can treat on a wound host.` over a
   `[DataField] public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Biological];`.
   Purely additive; `HashSet` (not `IReadOnlySet`) because `WithTreatmentCapabilities` takes `IReadOnlySet`.
3. `:172-191` — the single `TryChangeDamage` call becomes `void Apply()` over a hoisted `var change = Damage * scale;`,
   then a marked branch: healing (`change.DamageDict.Values.Any(a => a < 0)`) **and**
   `HasComponent<WoundHostComponent>` runs `Apply` inside `WoundDamageRoutingSystem.WithTreatmentCapabilities`,
   everything else calls `Apply()` directly. PLAN4 states a pure two-line hook is impossible here because the
   call must become a delegate. The Shitmed/Mono argument block (`targetPart: TargetBodyPart.All`,
   `partMultiplier: 1.00f, // Mono, 0.5f->1.00f`, `canSever: false`, plus the two `// Shitmed Change` markers)
   survives byte-for-byte; the expression stays `Damage * scale`, so the discarded-universal-modifier
   behaviour is **not** "fixed" (PLAN4 §3.4 / trap T4).

**`Content.Server/EntityEffects/Effects/EvenHealthChange.cs` — HOOK 9(b), 4 marked sites**

1. `:6` — the same `using Content.Shared._Onyx.Wounds;`, marked.
2. `:10` — `using System.Linq; // WOLFGATE: HOOK 9 - Any() on the healing test` (trap T5/§8.5-21: absent before).
3. `:40-42` — the same `TreatmentCapabilities` datafield, marked.
4. `:141-155` — `var final = dspec * scale;` + `void Apply()` + the same marked wound-host branch, with the
   healing test written as `Damage.Values.Any(a => a < 0)` over the `ProtoId<DamageGroupPrototype>` dictionary
   per PLAN4.

**`Resources/Locale/en-US/_Onyx/guidebook/entity-effects.ftl` — 2 `# WOLFGATE` comments**

- Header comment recording the `entity-effect-guidebook-*` → `reagent-effect-guidebook-*` rename (D16: these
  are old-style effects, not Onyx's ECS entity effects, so they follow WG's key convention).
- A comment on `reagent-effect-guidebook-take-stamina-damage` recording that it has **no Onyx source** —
  Onyx's `TakeStaminaDamage` overrides nothing, but WG's `ReagentEffectGuidebookText` is `abstract` and a
  `null` return hides the effect from the chemistry guidebook entirely.

**`Content.Shared/_WF/Wolfmed/EntityEffects/TakeStaminaDamage.cs` — 2 `// WOLFGATE (P4-D5)` comments**
(in a `_WF` file, marking the two deliberate divergences from Onyx's system body):

- the retained `Scale == 1` gate, with the reason WG's `MetabolizerSystem` scale is structurally `[0,1]`;
- `immediate: Immediate` being passed at all, with Onyx's `false` datafield default kept — WG's
  `StaminaSystem.TakeStaminaDamage` defaults `immediate: true` and `:288-292` returns *without applying the
  value* when the target is already critical, so `false` is both faithful and strictly safer.

No other upstream file was touched. No YAML, no prototype, no XAML, no RSI.

## 3. Deviations from PLAN4

**None behavioural.** Two cosmetic departures from the code PLAN4 §2.1 quotes, plus one process note:

1. `StaminaDamageCondition.Condition` assigns the stamina value to a plain local instead of the plan's
   `... is var damage && damage > Min && damage < Max` pattern. Identical semantics, easier to read.
2. Every `[DataField]` in the four new classes carries a `/// <summary>` one-liner, per the `_WF` style rule;
   the plan's quoted snippets have summaries only on the class and on two fields.
3. **Release YAML lint not run.** This package touches no YAML or prototype file. The only content file is the
   new FTL, which the clean headless server start covers (a duplicate Fluent id logs `[ERRO]`; there were none).

Verifications that could have become deviations but did not:

- **Class-name audit (trap T1):** `grep -rn "class SuppressPain\b|class MendFractures\b|class TakeStaminaDamage\b|class StaminaDamageCondition\b"`
  over `Content.Shared Content.Server Content.Client` → **0 hits** before creation. The bare `Type.Name` is how
  `!type:` resolves, so a collision would be a load-time ambiguity, not a compile error.
- **Locale-id audit:** all 8 new ids grepped over `Resources/Locale` → 0 hits. `NATURALFIXED` and `MANY` are
  registered (`ContentLocalizationManager.cs:38,52`).
- **`System.Linq` ambiguity check:** `EvenHealthChange`'s pre-existing `groupDamage.Values.Sum()` binds to
  `Content.Shared.FixedPoint`'s own `Sum(this IEnumerable<FixedPoint2>)` (`FixedPoint2.cs:313`).
  `System.Linq.Enumerable` has no `IEnumerable<FixedPoint2>` overload and no parameterless generic `Sum<T>`,
  so the new `using` cannot create ambiguity — confirmed by the clean build, not assumed.
- **Subscriptions / components:** this WP registers **none** of either (PLAN4 §5.1). Nothing was added to any
  `Initialize`.

**Incident, disclosed:** while normalising em dashes I ran a `perl -pi` substitution over the *whole*
`WOLFMED_MANIFEST.md` instead of only my appended block, rewriting `—`/`→` across phases 1-3 and WP12-0.
I detected it immediately from the diff stat and rebuilt the file from `git show HEAD:` plus WP12-0's two
additions (6 table rows + the end block, em dashes restored by inspection) plus my block. The manifest diff is
now **95 insertions, 0 deletions** — purely additive, with no pre-existing line altered. No `git checkout`,
`stash`, `clean` or `reset` was used at any point.

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

Headless server, 120 s, port 1299 (`WP12-1-report-server.log`) — `grep -cE "\[ERRO\]|\[FATL\]|Exception"` → **0**:

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
[WARN] eng: MainLoop: Cannot keep up!
```

Phase-4 gate filter (`WP12-1-report-tests.log`):

```
$ dotnet test ... --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~_Onyx.Medical|FullyQualifiedName~Wolfmed"
  Passed RealWoundHostPassiveDamageIsNeutralisedTest [1 s]
  Passed PainNumbnessSuppressesWoundPainTest [80 ms]
  Passed PainOverlayLevelTracksPainTest [105 ms]
  Passed PainShockStunsAtThresholdTest [83 ms]
  Passed ReattachedPartRejoinsWoundTrackingTest [80 ms]
  Passed PartDamageProjectsToVisualsComponentTest [98 ms]
Test Run Successful.
Total tests: 65
     Passed: 65
```

HOOK 9 changed no existing assertion, which is exactly what P4-D1 predicts (inert with one profile).

## 5. What later packages must know

- **The four `!type:` names now exist and are load-bearing.** `SuppressPain`, `MendFractures`,
  `TakeStaminaDamage` (effects) and `StaminaDamageCondition` (condition), namespace
  `Content.Shared._WF.Wolfmed.EntityEffects`. **WP12-2** can write `- !type:SuppressPain` / `!type:MendFractures`
  blocks directly (PROTO H-K). Never introduce a second class with any of these bare names anywhere in WG.
- **Field spellings WP12-2's YAML must use** (camelCase datafields): `SuppressPain` → `amount` (required),
  `decayDuration` (required, bare seconds), `identifier`, `recoveryMultiplier`.
  `MendFractures` → `wounds` (default `[BoneFractureWound]`, `[]` means all), `minimumGrade`, `maximumGrade`,
  `amount` (default 1). `TakeStaminaDamage` → `amount`, `immediate`.
  `StaminaDamageCondition` → `min`, `max`.
- **`treatmentCapabilities:` is now a valid datafield on `HealthChange` *and* `EvenHealthChange`** — but
  P4-D7 says write it nowhere in phase 4. WP12-2 must not add it to the five new reagents either; `[Biological]`
  is the correct default for all of them.
- **HOOK 9 is inert until a non-`Biological` `bodyPartProfile` exists.** WP12-9's T-REAGENT-CAP-NO must assert
  the **capability scope**, not "reagent X does not heal wounds" — GUARD D already routes reagent healing into
  wounds (§8.5 trap 23). The scope-open path is only reachable on a wound host with a negative, *localized*
  damage type; Toxin/Airloss/Bloodloss/Genetic/Cellular/Radiation healing bypasses `CanTreatPart` entirely.
- **Do not nest `WithTreatmentCapabilities`** (§8.5 trap 3). Both hooks call it at top level only. WP12-8's
  `_routedModifiers` change has the same shape and must save/restore rather than remove.
- **`DistributedHealthChange` was not authored** and is recorded as *not needed* (P4-D6), so the third partial
  class in Onyx's `ReagentTreatmentEffects.cs` has no WG counterpart. WP12-10's §7.1 row change should say
  `skipped (re-authored)` / WP12-1 for the three Onyx source files.
- **Upstream files this WP claims (do not re-edit):** `Content.Server/EntityEffects/Effects/HealthChange.cs`
  and `EvenHealthChange.cs`. Both are now first-time Wolfmed touches and count toward PLAN4 §3.4's
  upstream-file arithmetic (2 of the 19 files / 46 edits).
- **`Resources/Locale/en-US/_Onyx/guidebook/` is a new directory** owned by this WP; WP12-10's guidebook FTL
  goes to `Resources/Locale/en-US/_WF/Wolfmed/guidebook/wounds.ftl`, a different tree.
- **The `fracture-grade-{hairline,simple,displaced,comminuted}` keys are now defined** and are reachable by
  string interpolation from `MendFractures`. WP12-6/WP12-7's analyzer fracture-grade rows can reuse them
  rather than declaring a second set.
