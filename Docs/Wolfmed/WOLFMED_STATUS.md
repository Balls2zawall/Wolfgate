# Wolfmed status

Phases 1 and 2 of the Space Onyx wound port are implemented, build, and pass their tests. Nothing is committed.

- **Branch / worktree:** `clanker/wolfmed-port-orchestration-454c3d` in `.claude/worktrees/rules-motd-updates-11c89c`.
- **Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Reference sparse checkout at `C:\tmp\onyx` (recreate with the clone command in `WOLFMED_HANDOFF.md`, using `core.longpaths=true` and a short path).
- **Documents:** `DECISIONS.md` (D1–D35 plus the two phase-2 sections), `WOLFMED_PLAN.md` (phase 1's file-level plan), `WOLFMED_PLAN2.md` (phase 2's), `WOLFMED_MANIFEST.md` (every file: Onyx path, Wolfgate path, status, deviations, including the phase-2 §8.2 user-decision summary), `reports/analysis` (phase 1) and `reports/analysis/phase2` (phase 2's five analyst reports plus `CRITIQUE2.md`), `reports/work-packages` (phase 1) and `reports/work-packages/phase2` (one report and one verification per WP10-N package).

## What phase 1 delivers

- `StatusEffectNew` framework vendored at the upstream path (11 of 13 files byte-identical).
- Wound core in `Content.Shared/_Onyx/Wounds` (wound prototypes, `WoundSystem`, damage routing, projection, scars, wound status effects, pain, fractures, part functionality) and the server half in `Content.Server/_Onyx/Wounds` (bleeding, internal bleeding, organ damage, healing) plus a trimmed `CirculatoryStreamSystem`.
- Compat layer in `Content.Shared/_WF/Wolfmed/Compat`: `WolfmedDamageableSystem` (new-style damage API on a distinct name so it cannot bind to the legacy overload), `DamageDealtEvent` seam, `AlertsSystem.UpdateAlert`, body/stun/chat shims, `WoundTargetResolver`, `WolfmedBodyPartComponent`, part lifecycle bridge, part armour, wound-host exclusion.
- Damage bridge: for entities with `WoundHost`, Onyx routing owns limb damage; Shitmed spreading, sever-at-130 and part regen are skipped by component-gated `// WOLFGATE` guards. Armour applies exactly once. `TryChangeDamage` still returns the applied delta on the cancelled pass, so hitscan pierce, melee stamina and hit logs keep working.
- Prototypes: `wounds.yml`, wound status effects, alerts, textures (all CC-BY-SA-3.0), locale. `WoundHost` lands on `BaseMobSpeciesOrganic`; protogen is excluded at runtime; PassiveDamage is neutralised on wound hosts; the gib threshold is raised to 1500 on organics.
- Tests: `Content.IntegrationTests/Tests/_Onyx/Wounds` (6 ported) and `Tests/_WF/Wolfmed/WolfmedDamageBridgeTest.cs`. Last run: 30 passed, 0 failed; spawn-all-entities, prototype-save and dock smoke tests pass.

## What phase 2 delivers

Phase 2 (WP10-1 through WP10-6b, PLAN2's WP10 + WP9's handed-forward gates) makes fractures, pain and the
part-status examine live on real mobs for the first time. What a player now sees:

- **A fracture alert and its effects.** Breaking a limb hard enough (a Comminuted hit, `creationChance: 1`)
  shows the `BrokenBones` alert and — tracked by grade and treatment — slows movement (a Comminuted leg:
  walk ×0.5) and slows manipulation do-afters (a Comminuted arm: ×2.0, using the corrected balance below).
  Mending or detaching the limb clears both the alert and the penalty.
- **A pain overlay.** The existing brute/burn vignette is now driven by wound pain on wound hosts
  (`min(1, GetPain / SoftPainCap)`, floored below a small threshold) instead of raw projected damage, and a
  pain-numb character (the `PainNumbness` trait) is correctly exempted. At high pain (raw pain ≥ 130) a mob
  is paralysed for a few seconds, forced to scream, jitters, and gets a temporary reduced pain sensitivity
  window — pain shock's first real run on a mob in this fork.
- **Pain sounds.** Wound hosts now emote (e.g. `Scream`, `Crying`) as their total damage crosses configured
  thresholds — a Space Onyx feature that never actually worked at the pinned commit (see Deviations below)
  and now does in Wolfgate.
- **Examine shows part-by-part injury status and pain**, replacing the old body-level threshold text
  ("they don't look hurt" / "they look pale") for wound hosts specifically — non-wound-hosts (borgs,
  silicons, animals, the excluded Protogen) keep today's behaviour unchanged.
- **A `HighPainThreshold` trait** (25% less pain gain from wounds), mutually exclusive with `PainNumbness`.

Test counts: **39 of 39 passed** (`Content.IntegrationTests/Tests/_Onyx/Wounds` + `Tests/_WF/Wolfmed`,
`--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed"`), per `C:\tmp\wolfmed-plan\p2\wp\WP10-6b-tests.log`
— up from phase 1's 30. New in phase 2: `FractureAlertTracksGradeAndTreatmentTest`,
`FractureAlertRespectsMinimumGradeTest`, `FractureManipulationUsesHeldHandSymmetryTest`, the ported
`EffectsRefreshOnTreatmentHealingAndDetachTest`, `PainOverlayLevelTracksPainTest`,
`PainShockStunsAtThresholdTest`, `HighPainThresholdReducesWoundPainGainTest`,
`PainNumbnessSuppressesWoundPainTest`, plus WP10-6a's `ArmorPenetrationReachesWoundHostsTest` and the two
`PassiveDamage` canaries (T-PASSIVE-A/B, gating D29).

## Upstream footprint

**22 tracked upstream files** carry `// WOLFGATE` hooks (see the manifest) — 16 shipped in phase 1, 6 more
in phase 2: `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` (GUARD F), `Content.Client/Examine/ExamineSystem.cs`
(HOOK 14), `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs` (HOOK 15),
`Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs` (HOOK 16),
`Content.Server/Chat/EmoteOnDamageComponent.cs` (HOOK 17), `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs`
(HOOK 18) — plus one more phase-2 edit to an already-hooked phase-1 file (`Content.Server/Body/Systems/BloodstreamSystem.cs`,
GUARD E2) and one more block in an already-hooked phase-1 prototype (`Resources/Prototypes/Entities/Mobs/Species/base.yml`,
`PainShockTarget` + `EmoteOnDamage`). The phase-1 16 were the `DamageableSystem` seam, the Shitmed targeting
guards, the healing and armour hooks, and the species base prototype block.

## Bugs found and fixed during the port (re-check on every Onyx re-sync)

- `PainSystem` used `new ModifyPainGainEvent()`, which zeroes the struct's `Multiplier`; all pain was multiplied by zero. Onyx's source is identical, so this is an upstream Onyx bug.
- Wolfgate has no Nubody `BodyInventorySlotSystem`, so part add/remove had to be bridged (`WolfmedBodyPartLifecycleSystem`) with terminating-entity guards, and bleeding part-lifecycle entry points needed a caller.
- Four Onyx test literals disagree with Onyx's own pinned prototypes (fracture grade thresholds, bleeding minimum severity, reopen severity, healing multiplier); the ported tests assert the prototype values.
- **(Phase 2) `OrganicFractureProfile.manipulationModifier` was inverted relative to Onyx's own formula** —
  values below 1 made a shattered arm's do-afters *faster*. Corrected to the C# defaults (user decision,
  DECISIONS.md §8.2-1).
- **(Phase 2) Onyx's `EmoteOnDamage` pain sounds were dead at the pin** — `species_base.yml` writes the YAML
  key `emotes:` for a field whose serialized name is `emotesThreshold`; RT silently drops the unknown key.
  Corrected (user decision, DECISIONS.md §8.2-3).
- **(Phase 2) `PainSystem.IsPainNumb` was permanently false** — it only recognised Onyx's status-effect form
  of pain numbness, which nothing in Wolfgate can apply; Wolfgate's actual `PainNumbness` trait grants a
  different, legacy component. Widened to recognise both.

## Known deviations and balance flags

See `WOLFMED_PLAN.md` §8.2, `WOLFMED_PLAN2.md` §8.2, and the manifest's Deviations section (including the
consolidated phase-2 §8.2 user-decisions summary). Notable for playtest: limb damage versus armour changes
(armour now applies once), environmental damage creates limb wounds, do-afters no longer interrupt on wound
hosts, wound-host damage is unpredicted (transient client mispredict), pain stun re-triggers stun VFX per
call, a fractured arm's do-after penalty is lost if the do-after itself opts out of `MultiplyDelay`, the
client never mirrors a server-side limb attach/detach movement-speed refresh (corrects within one network
state), and **`WoundPrototype.HealingMultiplier` is still 1 for every wound** — a balance-pass item kept on
record since phase 1 and explicitly not touched in phase 2 (DECISIONS.md §8.2-9). `PartDamageVisualsComponent`
is likewise still deferred to phase 3 (DECISIONS.md §8.2-7) — networked, `EnsureComp`'d, and unconsumed.

## Next phases (not started)

1. **Phase 3:** `AmputationSystem` (retire Shitmed sever for wound hosts), organ damage consequences,
   per-part armour (`ArmorComponent.PartModifiers`), `PartDamageVisualsComponent` consumers (per-limb damage
   sprites), surgery-attach wound init assertion, the deferred tests listed in `reports/work-packages/WP9-report.md`.
2. **Phase 4 (WP11):** reagent treatment effects rewritten old-style (`SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `TreatmentCapabilities` on `HealthChange`/`EvenHealthChange`), tourniquet, medical patch, wound surgeries as Shitmed steps (extend Wolfgate's `SurgeryTendWoundsEffectComponent`/`SurgeryWoundedConditionComponent`, do not vendor Onyx's), health analyzer wound diagnostics (needs a parallel-UI-vs-graft decision). Reagent id collisions to resolve first: `Stasizium`, `SalicylicAcid`.
3. **Phase 5:** IPC, cybernetic, slime and plant profiles. Blocked on a shared stage-based metabolizer for non-organic circulatory streams.
4. **Phase 6:** predicted routing; `HurtCommand` part argument (patch kept at `reports/work-packages` as `WP8-hurtcommand-deferred.patch` in `C:\tmp\wolfmed-plan\wp`).

## How to verify

```bash
dotnet build Content.Server/Content.Server.csproj -c DebugOpt
dotnet build Content.Client/Content.Client.csproj -c DebugOpt
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed"
dotnet run --project Content.YAMLLinter -c Release
```

A worktree needs `RobustToolbox` junctioned from the main checkout before building: `cmd /c rmdir RobustToolbox` then `cmd /c mklink /J RobustToolbox <main>\RobustToolbox`; remove the junction with `cmd /c rmdir RobustToolbox` afterwards.
