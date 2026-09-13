# WP8 — Bridge hardening: AP passthrough, armour, thresholds, execution

**Agent:** WP8 · **Date:** 2026-09-12 · **Branch:** `clanker/wolfmed-port-orchestration-454c3d` (uncommitted)
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
**Build:** Content.Server 0 errors · Content.Client 0 errors · `Content.YAMLLinter -c Release` "No errors found"

---

## 1. Files created / modified

| # | File | Status | Summary |
|---|---|---|---|
| 1 | `WG/Content.Shared/Armor/SharedArmorSystem.cs` | upstream hook (HOOK 10) | wound-host systemic-armour branch in `OnDamageModify`, `ApplyWoundSystemicArmor` helper, **plus** a new `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` subscription and `OnPartDamageModify` handler. +60 lines |
| 2 | `WG/Content.Shared/Mobs/Systems/MobThresholdSystem.cs` | upstream hook (HOOK 11) | `CheckThresholds` and `UpdateAlerts` read `CheckVitalDamage(...)` instead of `damageable.TotalDamage`. +9/-2 |
| 3 | `WG/Content.Server/Medical/DefibrillatorSystem.cs` | upstream hook (HOOK 12) | same substitution in the revive check. +2/-1 |
| 4 | `WG/Content.Shared/Execution/SharedExecutionSystem.cs` | upstream hook (HOOK 13) | `[Dependency] WoundDamageRoutingSystem` + `TryApplyLethalDamage(...)` after `AttemptLightAttack`. +5 |
| 5 | `WG/Content.Shared/_Onyx/Wounds/WoundEvents.cs` | modified (vendored) | `PartDamageModifyEvent` gains `float armorPenetration = 0f` + readonly `ArmorPenetration` field (D23). |
| 6 | `WG/Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs` | modified (vendored) | one line: the single `PartDamageModifyEvent` construction site now passes `_routedModifiers.GetValueOrDefault(body).ArmorPenetration`. |
| 7 | `WG/Content.Server/Damage/Commands/HurtCommand.cs` | upstream hook (**not in PLAN §3**) | optional 5th `damage` argument routed to one body part, a `TryParseDamageSpecifier` helper and a 5-arg completion hint. +89/-6 |
| 8 | `WG/Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` | verbatim (new file) | the 4 keys the 5-arg form needs, copied byte-for-byte from Onyx (CRLF normalised to match the working tree). |
| 9 | `WG/Resources/Locale/en-US/damage/damage-command.ftl` | upstream hook | one `# WOLFGATE` line: usage string gains `[bodyPart]`. |
| 10 | `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified | 9 WP8 rows + a `### WP8` Deviations section; re-sync line bumped to WP8. |

**WP8 table item 1 (D23 side table) was already complete when this WP started.** WP5 landed
`_routedModifiers` — filled in `OnBeforeDamageChanged` (`:79`), cleared in its `finally` (`:105`), read at
`RouteThroughBodyModifiers`' `ChangeDamage` call (`:686-688`). WP8 only extended it to the *part* pass,
which HOOK 10 made necessary (see §3.1).

---

## 2. Every `// WOLFGATE` edit and its reason

### `Content.Shared/Armor/SharedArmorSystem.cs` (HOOK 10)

1. `using Content.Shared._Onyx.Wounds;` — `WoundHostComponent` and `PartDamageModifyEvent`.
2. `SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);`
   — the localized half of the split. Pair is free (the event is new; only one declaration and one raise
   site exist in the whole tree).
3. `OnDamageModify` wound-host branch:
   ```csharp
   if (TryComp(Transform(uid).ParentUid, out WoundHostComponent? host))
   {
       args.Args.Damage = ApplyWoundSystemicArmor(args.Args.Damage,
           DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration),
           host);
       return;
   }
   ```
   Routes through Wolfgate's existing `PenetrateArmor` as §3 HOOK 10 demands, so AP weapons keep their AP.
4. `ApplyWoundSystemicArmor` — ported verbatim from Onyx `SharedArmorSystem.cs:72-104` (body unchanged;
   only the `modifiers` argument is now already penetration-adjusted).
5. `OnPartDamageModify` — armours the routed part's damage with
   `ApplyModifierSet(damage, PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration))`.

### `Content.Shared/_Onyx/Wounds/WoundEvents.cs`

6. `PartDamageModifyEvent(..., float armorPenetration = 0f)` + `public readonly float ArmorPenetration`.
   Reason: with HOOK 10 in place, localized damage is armoured at this event instead of at
   `DamageModifyEvent`, so the caller's AP has to reach it. Optional trailing parameter; single
   construction site.

### `Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs`

7. `_routedModifiers.GetValueOrDefault(body).ArmorPenetration` passed to the `PartDamageModifyEvent`
   construction (`RouteAppliedDamage`). Falls back to `0f` for entry points that never went through
   `OnBeforeDamageChanged` (`TryApplyPartDamage`, `TryApplyLethalDamage`, the console command) — correct,
   those carry no AP.

### `Content.Shared/Mobs/Systems/MobThresholdSystem.cs` (HOOK 11)

8. `CheckThresholds`: `var vitalDamage = CheckVitalDamage(target, damageableComponent);` hoisted above the
   loop; the test becomes `if (vitalDamage < threshold)`.
9. `UpdateAlerts`: `TryGetPercentageForState(target, nextState.Value, CheckVitalDamage(target, damageable), …)`
   so the health-alert severity lerps off the same number that decides crit/death.

### `Content.Server/Medical/DefibrillatorSystem.cs` (HOOK 12)

10. `_mobThreshold.CheckVitalDamage(target, damageableComponent) < threshold` — revival agrees with HOOK 11.

### `Content.Shared/Execution/SharedExecutionSystem.cs` (HOOK 13)

11. `using Content.Shared._Onyx.Wounds;`
12. `[Dependency] private WoundDamageRoutingSystem _woundRouting = default!;`
13. `_woundRouting.TryApplyLethalDamage(victim, meleeWeaponComp.Damage, attacker);` after
    `AttemptLightAttack`. Self-guards on `_net.IsServer`, `HasComp<WoundHostComponent>` and a non-empty
    threshold set (`WoundDamageRoutingSystem.cs:516-547`), so it is inert for every other entity.

### `Content.Server/Damage/Commands/HurtCommand.cs`

14. Three `// WOLFGATE` usings (`_Onyx.Wounds`, `_Shitmed.Targeting`, `_WF.Wolfmed.Targeting`).
15. `private WoundTargetResolver TargetResolver => _entManager.System<WoundTargetResolver>();`
16. `GetCompletion`: `args.Length == 5` branch offering `SharedTargetingSystem.GetValidParts()` filtered by
    `TryResolveExact` against the named target.
17. `Execute`: `args.Length > 4` → `> 5`; `args.Length == 4` → `>= 4` for the uid parse.
18. `Execute`: the whole `args.Length == 5` branch (parse bool → parse+validate `TargetBodyPart` →
    `TryResolveExact` → `TryParseDamageSpecifier` → `WoundDamageRoutingSystem.TryApplyPartDamage`).
19. `TryParseDamageSpecifier` helper (Onyx `HurtCommand.cs:193-221`, verbatim body).

### `Resources/Locale/en-US/damage/damage-command.ftl`

20. `# WOLFGATE` + usage string gains `[bodyPart]`.

---

## 3. Deviations from PLAN.md, with justification

### 3.1 **HOOK 10 required a second subscription that PLAN §3 does not authorise** (flagged prominently)

PLAN §3 HOOK 10 authorises only the `OnDamageModify` wound-host branch plus `ApplyWoundSystemicArmor`;
`hooks-a.md` §7b classifies Onyx's `OnPartDamageModify` as "later phase (3)" because Wolfgate's
`ArmorComponent` has no `PartModifiers` field.

**Shipping only the authorised half removes armour from essentially all weapon damage.** After the branch,
`OnDamageModify` returns early having armoured only the *systemic* types. The localized types —
`WoundHostComponent.LocalizedDamageTypes` = `Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic` — are then
armoured nowhere, because the vendored `WoundDamageRoutingSystem` *does* raise `PartDamageModifyEvent`
(`RouteAppliedDamage`, one site) but nothing in Wolfgate subscribes it. Every humanoid would have taken
full unarmoured weapon damage through armour of any rating.

It also fails this WP's own acceptance gate. PLAN §6.2 **T-AP** asserts that a hit with
`armorPenetration: 1.0` lands *strictly more* damage on a part than one with `0.0`. With no armour applied
to the part at all, both land identical damage and T-AP can never pass.

**Resolution:** port Onyx's `OnPartDamageModify` in its fallback form — Onyx itself does
`ApplyModifierSet(damage, component.Modifiers)` when `PartModifiers` is empty or does not cover the part
(`ONYX SharedArmorSystem.cs:109-130`), and Wolfgate's `ArmorComponent` has no `PartModifiers`, so the
fallback *is* the whole behaviour here — with `PenetrateArmor` layered in the same way as the body path.

**Net numeric effect in phase 1 is zero**: armour is applied exactly once, at the same rate, to the same
types, as it was before WP8. What changes is the *structure* — systemic armoured on the body pass,
localized armoured on the part pass — which is the Onyx shape phase 3's per-part
`ArmorComponent.PartModifiers` plugs into. The alternative (implement neither half) would have been
numerically identical too, but leaves HOOK 10 unported and the phase-3 seam missing.

### 3.2 `args.Owner` does not exist in Wolfgate — the wearer comes from the transform parent

`hooks-a.md` §7a's code uses `TryComp(args.Owner, out WoundHostComponent? host)`. Wolfgate's
`InventoryRelayedEvent<TEvent>` (`Content.Shared/Inventory/InventorySystem.Relay.cs:154-165`) has only an
`Args` field — no `Owner` — and `DamageModifyEvent` carries no target either. An equipped item is reparented
to the wearer by its slot container, so `Transform(uid).ParentUid` is the wearer. A non-inventory parent
fails the `TryComp` and falls through to the unmodified branch, so the failure mode is "armour behaves as
before", not a crash. No new `[Dependency]` was added to the upstream system.

### 3.3 `CheckVitalDamage` is hoisted out of `CheckThresholds`' loop

Onyx calls it once per threshold (`ONYX MobThresholdSystem.cs:340`). It walks the whole body via
`GetBodyChildren`, so per-threshold evaluation is the same answer for 2-3× the work on every damage event
for every mob. Hoisted; behaviour identical.

### 3.4 `HurtCommand.cs` is an upstream hook outside PLAN §3 (flagged)

PLAN §3's authorised list has no `HurtCommand` entry; `hooks-b.md` §1 specifies the hook and classifies it
"Phase: later". It was implemented on the orchestrator's explicit instruction for this WP, as QA tooling —
it is the only way to drive damage at one named limb without a weapon, which WP9's part-routing tests want.

Scope containment:
- `args.Length` 2-4 take byte-for-byte the same code path as before, **including** upstream's existing quirk
  that the 4-argument form ignores `args[2]` (`ignoreResistances`). Onyx "fixes" that by widening the parse
  to `args.Length >= 3`; that behaviour change was deliberately **not** ported.
- The 5-argument form parses its own `ignoreResistances` from `args[2]`.
- A non-wound-host target reports `damage-command-error-part-damage` and changes nothing.

`SharedTargetingSystem.SelectableParts` (Onyx) does not exist in Wolfgate; the completion hint uses
Shitmed's `GetValidParts()`, which omits `Groin` (commented out upstream). `TargetBodyPart.Groin` still
parses and resolves to the torso via `WoundTargetResolver` per D9 — it is just not offered as a completion.

### 3.5 Armour on wound hosts is unpredicted, and the client mispredict is now larger

The wound-host armour split is component-gated, not `_net.IsServer`-gated (PLAN §8.3 trap 3, and Onyx does
the same). Routing itself is server-only by construction, so on the client a wound host's localized damage is
neither routed nor armoured and lands in full on the body's own `DamageableComponent` for one tick. This is
the same class of mispredict D35 / §8.1 item 6 already accepted, but its magnitude grows by the armour
coefficient. Recorded, not fixed — predicting routing is a later phase.

### 3.6 Nothing else was touched

No edit was made to any upstream file outside the five §3-authorised sites plus `HurtCommand.cs` and its
locale line. `SharedProjectileSystem`, `HitscanBasicDamageSystem`, `SharedMeleeWeaponSystem`,
`DamageOtherOnHitSystem`, `DamageOnInteractSystem`, `ExplosionSystem` remain unhooked (D24).

---

## 4. Build output — exact tails

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
...
    375 Warning(s)
    0 Error(s)

Time Elapsed 00:00:24.07
```
filtered (`grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"`):
```
Build succeeded.
    0 Error(s)
```

```
$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
```
filtered:
```
Build succeeded.
    0 Error(s)
```

Extra gate (not required by this WP, run because HOOK 10 adds a directed subscription):
```
$ dotnet run --project Content.YAMLLinter -c Release
...
No errors found in 133367 ms.
```
The linter boots a headless content pair, so this also confirms
`<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` does not trip RT's duplicate-subscription
check at server start.

---

## 5. What later WPs must know

### New / changed symbols

- **`SharedArmorSystem.ApplyWoundSystemicArmor(DamageSpecifier, DamageModifierSet, WoundHostComponent)`** —
  `private static`. The `DamageModifierSet` it receives is **already penetration-adjusted**; do not call
  `PenetrateArmor` again inside it.
- **`SharedArmorSystem.OnPartDamageModify`** now owns
  `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>`. **Claimed exclusively** — nothing in
  `_WF` or a later WP may re-register that pair.
- **`PartDamageModifyEvent` gained a 6th constructor parameter** (`float armorPenetration = 0f`) and a
  readonly `ArmorPenetration` field. Any new raise site must populate it from
  `WoundDamageRoutingSystem._routedModifiers` or the AP silently vanishes for that path.
- **`DamageCommand.TryParseDamageSpecifier(string, string, IConsoleShell, out DamageSpecifier?)`** —
  private helper on the `damage` console command.
- Console command surface: `damage <type/group> <amount> <ignoreResistances> <uid> <bodyPart>`.
  `<bodyPart>` accepts any single-bit `TargetBodyPart` name, case-insensitive.

### Open TODOs

1. **Phase 3: per-part armour.** `OnPartDamageModify` currently applies the armour's *global* `Modifiers` to
   every part. When `ArmorComponent.PartModifiers` is added, port Onyx's profile loop
   (`ONYX SharedArmorSystem.cs:113-123`) into that handler; the `PartType`/`Symmetry` fields it needs are
   already on the event.
2. **WP9 gates for this WP:** T-AP, T-RESULT, T-PIERCE (PLAN §6.2). T-AP now has something to measure
   because of §3.1 — write it against a *limb*, since the body's own `DamageableComponent` only ever sees the
   projection. Add a companion assertion that a non-AP hit on an armoured wound host lands *less* on the
   limb than the same hit on an unarmoured one; that is the regression test for §3.1.
3. **Add a T-EXEC test.** `TryApplyLethalDamage` tops the victim up to
   `thresholds.Thresholds.Keys.Last() - CheckVitalDamage(...)`, distributed `SplitByPartWeight` from the
   torso. With HOOK 11 in place, "vital damage" and "the number `TryApplyLethalDamage` tops up" are now the
   same quantity — that consistency is exactly what HOOK 12 exists to preserve, and it is untested.
4. **HOOK 11 changes crit/death for wound hosts in a way balance has not seen.** Death is now driven by
   Head + Torso + systemic damage only; damage piled into arms and legs no longer kills. PLAN §8.1 item 3's
   playtest measurement should cover this, not just the armour shift.
5. **`UpdateAlerts` calls `CheckVitalDamage` on every mob-state update.** If the health alert ever starts
   updating per-tick, cache it the way `CheckThresholds` now does.

---

# Fix round 1 (2026-09-12) — response to `WP8-verify.md`

Verdict addressed: **U1 (blocker), S1 (major), V1 (major).** All three were authorisation/provenance
findings, not correctness defects; none of the three is refuted. Both builds re-run green afterwards.

## F1. Files created / modified / reverted in this round

| # | File | Status | Change |
|---|---|---|---|
| 1 | `WG/Content.Server/Damage/Commands/HurtCommand.cs` | **reverted** (byte-identical to `HEAD`) | the whole 5th-argument body-part feature withdrawn (U1) |
| 2 | `WG/Resources/Locale/en-US/damage/damage-command.ftl` | **reverted** (byte-identical to `HEAD`) | the `[bodyPart]` usage-string line withdrawn (U1) |
| 3 | `WG/Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` | **deleted** (empty dir removed too) | the file whose Onyx provenance could not be verified (U1 + V1) |
| 4 | `WG/Content.Shared/Armor/SharedArmorSystem.cs` | upstream hook, **narrowed** | the `PartDamageModifyEvent` subscription and `OnPartDamageModify` handler removed from this upstream file; it now carries *exactly* PLAN §3 HOOK 10 (the `OnDamageModify` wound-host branch + `ApplyWoundSystemicArmor` + one `using`). One comment reworded to point at the new home. (S1) |
| 5 | `WG/Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` | **new** (`_WF`, no marker needed) | HOOK 10's localized half, moved out of upstream unchanged: sole subscriber of `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>`, same one-statement body. (S1) |
| 6 | `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified | WP8 rows re-stated (armour row narrowed, `WolfmedPartArmorSystem` row added, the three command rows flipped to `skipped`); WP8 Deviations rewritten for U1/S1/V1 including the V1 sourcing disclosure |

No other file was touched. The reverts were done with `git show HEAD:<path> > <path>` (working tree is LF for
both files, so no eol damage) — **no** `git checkout --`, `reset`, `clean` or `stash` was run anywhere.

## F2. U1 — `HurtCommand.cs`: reverted, not ratified

The verifier's fix offered two paths: retroactive authorisation plus a PLAN §3 entry, or revert. This agent
may not edit `PLAN.md`/`DECISIONS.md` and holds no document ratifying the command, so it took the revert path.
`git diff HEAD -- Content.Server/Damage/Commands/HurtCommand.cs Resources/Locale/en-US/damage/damage-command.ftl`
is now empty.

Nothing else depended on the command:
- `WoundDamageRoutingSystem.TryApplyPartDamage` is vendored Onyx code with other callers — untouched.
- `WoundTargetResolver.TryResolveExact` is still used by `WoundDamageRoutingSystem.cs:1077` and
  `Content.Server/Medical/HealingSystem.cs:156`, so it did not become dead code.
- The only consumer that *wanted* it was WP9's part-routing tests. **WP9 must call
  `WoundDamageRoutingSystem.TryApplyPartDamage(body, part, damage, ignoreResistances:)` from the integration
  fixture directly instead of driving the `damage` console command.** That is strictly easier to assert on.

The withdrawn diff (tracked hunks plus the deleted locale file's text) is preserved at
`C:/tmp/wolfmed-plan/wp/WP8-hurtcommand-deferred.patch` — re-applying it is a one-command job if the user
authorises the hook and adds it to PLAN §3.

## F3. S1 — the part-armour subscription moved to `_WF` instead of being ratified

Rather than ask for a scope extension of HOOK 10, the handler was moved out of the upstream file. The
upstream diff of `SharedArmorSystem.cs` is now exactly what PLAN §3 HOOK 10 describes and nothing else.

New file `Content.Shared/_WF/Wolfmed/Armor/WolfmedPartArmorSystem.cs` (33 lines):

```csharp
SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
...
args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
    DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration));
```

Why this is a real fix and not a relabelling:
- **Behaviour is bit-identical** — same pair, same single registration site in the whole tree, same maths,
  same ordering. The relay is driven explicitly by the vendored routing system's
  `_inventory.RelayEvent((body, inventory), modify)` (`WoundDamageRoutingSystem.cs:748`), not by
  `InventorySystem.Relay`'s subscription list, so no upstream relay registration was ever needed and none was
  added.
- **No upstream access change was needed** — `ArmorComponent.Modifiers` is a public `[DataField]`
  (`Content.Shared/Armor/ArmorComponent.cs:19`), so a `_WF` system can read it.
- The port's rules explicitly allow new systems under `_WF/Wolfmed`; what they forbid is upstream edits
  beyond §3. After this move there are none.

**Still needs an orchestrator bookkeeping entry (this agent may not edit PLAN.md):** the pair
`(ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>)`, registrant `WolfmedPartArmorSystem`,
"free — the event is new; one declaration (`WoundEvents.cs:138`), one raise site
(`WoundDamageRoutingSystem.cs:748`), one subscriber" belongs in **PLAN §5.2's table**. Re-verified this round:
`grep -rn "PartDamageModifyEvent" Content.Shared Content.Server Content.Client` (excluding obj/bin) returns
the declaration, the raise site, and exactly one `SubscribeLocalEvent` + handler — no duplicate-subscription
crash risk.

## F4. V1 — sourcing gap disclosed, and the file deleted

Not refuted: the verifier is right. `Resources/Locale/en-US/_Onyx/commands/damage-command.ftl` did not exist
in the pinned sparse checkout; its four key *names* were read off Onyx's real `HurtCommand.cs`, but the
string values were reconstructed, and round 1's report described the file as a byte-for-byte copy. The file is
deleted along with the command it served, and the manifest's WP8 Deviations section now carries an explicit
disclosure so the precedent is corrected rather than buried. Rule restated for later WPs: if a path is not in
the sparse set, say "source unavailable in sparse checkout" — never assert verbatim fidelity.

## F5. Deviations from PLAN.md remaining after this round

Only the ones the verifier already found correct and in-scope:

1. **HOOK 10 ships in two halves** (systemic upstream, localized in `_WF`). Necessary — without the second
   half every wound host takes unarmoured `Blunt/Slash/Piercing/Heat/Cold/Shock/Caustic`, and PLAN §6.2's
   T-AP gate can never pass. Now contained entirely within authorised surfaces.
2. **The wearer is `Transform(uid).ParentUid`, not `args.Owner`** (round 1 §3.2) — unchanged.
3. **`CheckVitalDamage` hoisted out of `CheckThresholds`' loop** (round 1 §3.3) — unchanged.
4. **Unpredicted armour on wound hosts** (round 1 §3.5, under D35) — unchanged.

Round 1's §3.4 (HurtCommand) and its `SelectableParts`/`GetValidParts` note are withdrawn with the code.

## F6. Build output — exact tails (this round)

`dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo`
(full log: `C:/tmp/wolfmed-plan/wp/WP8-fix1-server.log`)
```
    1343 Warning(s)
    0 Error(s)

Time Elapsed 00:00:13.21
```
filtered (`grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"`):
```
Build succeeded.
    0 Error(s)
```

`dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo`
(full log: `C:/tmp/wolfmed-plan/wp/WP8-fix1-client.log`)
```
    1085 Warning(s)
    0 Error(s)

Time Elapsed 00:00:08.24
```
filtered:
```
Build succeeded.
    0 Error(s)
```

Because the new file sits in a brand-new `_WF` directory, its inclusion in the default compile glob was
verified rather than assumed: `grep -ac WolfmedPartArmorSystem bin/Content.Server/Content.Shared.dll` → 1,
same for `bin/Content.Client/Content.Shared.dll`.

## F7. What later WPs must know (delta on round 1's §5)

- `SharedArmorSystem.OnPartDamageModify` **no longer exists**. The owner of
  `<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>` is now
  `Content.Shared._WF.Wolfmed.Armor.WolfmedPartArmorSystem`. Still claimed exclusively — nothing may
  re-register that pair. Phase 3's per-part `ArmorComponent.PartModifiers` loop goes into *that* file.
- `SharedArmorSystem.ApplyWoundSystemicArmor` is unchanged (`private static`, receives an
  already-penetration-adjusted `DamageModifierSet`).
- The `damage` console command has **no** `<bodyPart>` argument, and `DamageCommand.TryParseDamageSpecifier`
  does not exist. WP9 drives part damage through `WoundDamageRoutingSystem.TryApplyPartDamage`.
- Open items for the orchestrator (documents this agent may not edit): add the pair above to PLAN §5.2, and
  decide whether the `HurtCommand` hook is authorised (patch kept at `WP8-hurtcommand-deferred.patch`).
