# Wolfmed status

Phase 1 of the Space Onyx wound port is implemented, builds, and passes its tests. Nothing is committed.

- **Branch / worktree:** `clanker/wolfmed-port-orchestration-454c3d` in `.claude/worktrees/rules-motd-updates-11c89c`.
- **Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Reference sparse checkout at `C:\tmp\onyx` (recreate with the clone command in `WOLFMED_HANDOFF.md`, using `core.longpaths=true` and a short path).
- **Documents:** `DECISIONS.md` (D1–D35), `WOLFMED_PLAN.md` (the file-level plan the agents executed), `WOLFMED_MANIFEST.md` (every file: Onyx path, Wolfgate path, status, deviations), `reports/analysis` (the dependency reports and critique), `reports/work-packages` (one report and one verification per work package).

## What phase 1 delivers

- `StatusEffectNew` framework vendored at the upstream path (11 of 13 files byte-identical).
- Wound core in `Content.Shared/_Onyx/Wounds` (wound prototypes, `WoundSystem`, damage routing, projection, scars, wound status effects, pain, fractures, part functionality) and the server half in `Content.Server/_Onyx/Wounds` (bleeding, internal bleeding, organ damage, healing) plus a trimmed `CirculatoryStreamSystem`.
- Compat layer in `Content.Shared/_WF/Wolfmed/Compat`: `WolfmedDamageableSystem` (new-style damage API on a distinct name so it cannot bind to the legacy overload), `DamageDealtEvent` seam, `AlertsSystem.UpdateAlert`, body/stun/chat shims, `WoundTargetResolver`, `WolfmedBodyPartComponent`, part lifecycle bridge, part armour, wound-host exclusion.
- Damage bridge: for entities with `WoundHost`, Onyx routing owns limb damage; Shitmed spreading, sever-at-130 and part regen are skipped by component-gated `// WOLFGATE` guards. Armour applies exactly once. `TryChangeDamage` still returns the applied delta on the cancelled pass, so hitscan pierce, melee stamina and hit logs keep working.
- Prototypes: `wounds.yml`, wound status effects, alerts, textures (all CC-BY-SA-3.0), locale. `WoundHost` lands on `BaseMobSpeciesOrganic`; protogen is excluded at runtime; PassiveDamage is neutralised on wound hosts; the gib threshold is raised to 1500 on organics.
- Tests: `Content.IntegrationTests/Tests/_Onyx/Wounds` (6 ported) and `Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs`. Last run: 30 passed, 0 failed; spawn-all-entities, prototype-save and dock smoke tests pass.

## Upstream footprint

16 tracked upstream files carry `// WOLFGATE` hooks (see the manifest). The larger ones are the `DamageableSystem` seam, the Shitmed targeting guards, the healing and armour hooks, and the species base prototype block.

## Bugs found and fixed during the port (re-check on every Onyx re-sync)

- `PainSystem` used `new ModifyPainGainEvent()`, which zeroes the struct's `Multiplier`; all pain was multiplied by zero. Onyx's source is identical, so this is an upstream Onyx bug.
- Wolfgate has no Nubody `BodyInventorySlotSystem`, so part add/remove had to be bridged (`WolfmedBodyPartLifecycleSystem`) with terminating-entity guards, and bleeding part-lifecycle entry points needed a caller.
- Four Onyx test literals disagree with Onyx's own pinned prototypes (fracture grade thresholds, bleeding minimum severity, reopen severity, healing multiplier); the ported tests assert the prototype values.

## Known deviations and balance flags

See `WOLFMED_PLAN.md` §8.2 and the manifest's Deviations section. Notable for playtest: limb damage versus armour changes (armour now applies once), environmental damage creates limb wounds, do-afters no longer interrupt on wound hosts, wound-host damage is unpredicted (transient client mispredict), `WoundPrototype.HealingMultiplier` is 1 for every wound, pain stun re-triggers stun VFX per call.

## Next phases (not started)

1. **Phase 2 (WP10):** `FractureEffectsSystem`, `FractureAlertSystem`, pain shock alert and HUD, high-pain-threshold trait, `MobStandStatusEffectBase` (needs a `KnockdownImmune` tag), remaining `StatusEffectNew` consumers.
2. **Phase 3:** `AmputationSystem` (retire Shitmed sever for wound hosts), organ damage consequences, per-part armour (`ArmorComponent.PartModifiers`), surgery-attach wound init assertion, the deferred tests listed in `reports/work-packages/WP9-report.md`.
3. **Phase 4 (WP11):** reagent treatment effects rewritten old-style (`SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `TreatmentCapabilities` on `HealthChange`/`EvenHealthChange`), tourniquet, medical patch, wound surgeries as Shitmed steps (extend Wolfgate's `SurgeryTendWoundsEffectComponent`/`SurgeryWoundedConditionComponent`, do not vendor Onyx's), health analyzer wound diagnostics (needs a parallel-UI-vs-graft decision), examine part status. Reagent id collisions to resolve first: `Stasizium`, `SalicylicAcid`.
4. **Phase 5:** IPC, cybernetic, slime and plant profiles. Blocked on a shared stage-based metabolizer for non-organic circulatory streams.
5. **Phase 6:** predicted routing; `HurtCommand` part argument (patch kept at `reports/work-packages` as `WP8-hurtcommand-deferred.patch` in `C:\tmp\wolfmed-plan\wp`).

## How to verify

```bash
dotnet build Content.Server/Content.Server.csproj -c DebugOpt
dotnet build Content.Client/Content.Client.csproj -c DebugOpt
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed"
dotnet run --project Content.YAMLLinter -c Release
```

A worktree needs `RobustToolbox` junctioned from the main checkout before building: `cmd /c rmdir RobustToolbox` then `cmd /c mklink /J RobustToolbox <main>\RobustToolbox`; remove the junction with `cmd /c rmdir RobustToolbox` afterwards.
