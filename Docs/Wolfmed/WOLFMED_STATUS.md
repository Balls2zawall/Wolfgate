# Wolfmed status

Phases 1, 2 and 3 of the Space Onyx wound port are implemented, build, and pass their tests. Nothing is committed.

- **Branch / worktree:** `clanker/wolfmed-port-orchestration-454c3d` in `.claude/worktrees/rules-motd-updates-11c89c`.
- **Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Reference sparse checkout at `C:\tmp\onyx` (recreate with the clone command in `WOLFMED_HANDOFF.md`, using `core.longpaths=true` and a short path).
- **Documents:** `DECISIONS.md` (D1–D35 plus the phase-2 and phase-3 sections), `WOLFMED_PLAN.md` (phase 1's file-level plan), `WOLFMED_PLAN2.md` (phase 2's), `WOLFMED_PLAN3.md` (phase 3's), `WOLFMED_MANIFEST.md` (every file: Onyx path, Wolfgate path, status, deviations, including the phase-2 §8.2 and phase-3 §8.6 user-decision summaries), `reports/analysis` (phase 1), `reports/analysis/phase2` (phase 2's five analyst reports plus `CRITIQUE2.md`) and `reports/analysis/phase3` (phase 3's five analyst reports plus `CRITIQUE3.md`), `reports/work-packages` (phase 1), `reports/work-packages/phase2` (one report and one verification per WP10-N package) and `reports/work-packages/phase3` (one report and one verification per WP11-N package).

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

## What phase 3 delivers

Phase 3 (WP11-0 through WP11-5, `PLAN3.md`) is the body-integrity phase: limbs can now be severed, organs
can fail, armour protects per limb instead of the whole body, and severed limbs show their wounds. What a
player now sees:

- **Amputation, by melee, guns and lasers.** A limb (or the head) that accumulates enough damage across the
  damage types its `amputationThresholds` names becomes `Severable`; the *next* hit that also meets that
  type's `dismembermentFinishingDamage` severs it, dropping it as a live, re-attachable entity and leaving a
  `DismembermentWound` + `AmputationConsequenceWound` on the parent stump (the latter is a marker only — it
  does not yet block re-attachment, phase 4). **User decision (DECISIONS.md §8.6-1): guns and lasers can
  sever**, deviating from Onyx's melee-only defaults. Piercing's finishing minimum was lowered from Onyx's
  40 to **12**, and a `Heat` amputation-threshold row was added per part (equal to that part's Piercing
  threshold — Head/Hand 200, Arm/Leg 250, Foot 220) with a Heat finishing minimum of **15**. Measured
  outcome: a hand is severed by the **16th** consecutive 14-Piercing round or the **14th** consecutive
  16-Heat laser shot; 5 bullets into a foot (70/220) leave it attached. Amputation thresholds themselves
  stay at Onyx values, so gunfire still needs many hits before a finishing shot; melee is unchanged (a
  machete needs 6 × Slash 32 on an arm). A lost vital part (head or torso) now charges its own total damage
  as systemic Bloodloss on wound hosts (§8.6-3, P3-D1) instead of the flat −100 HardLight/Shitmed decapitation
  used to read as; non-wound-hosts are provably untouched.
- **Organ damage, live for the first time.** Brain, eyes, heart, lungs, liver, stomach and kidneys (human
  and human-lineage species only — §8.6-7) now take damage from hits to their containing part and fail at
  0 HP: heart failure delays death 60s and blocks defib, brain destruction kills outright (without deleting
  the organ), eye destruction causes temporary blindness, lung loss causes suffocation. **Organ damage ships
  irreversible** (§8.6-4, Onyx defaults) — nothing in Wolfgate heals an organ yet. Balance: each hit caps at
  `MaxHealth × 0.3` organ HP, so exactly 4 solid hits destroy any organ regardless of size; per-hit odds are
  low enough (0.95%–4.0% depending on organ and part) that this is a shift-long ratchet, not a gunfight
  event — roughly 166 (lungs) to 421 (kidneys) torso hits, or ~100 head hits for the brain, expected to
  destroy one.
- **Locational armour.** Coverage-aware armour (`ArmorComponent.Coverage`/`CoverageSymmetry`/`PartModifiers`)
  now exists and is content-annotated on the five `_Mono` bulletproof vests (`coverage: [Torso, Arm, Leg]`)
  and the one `_Mono` light ballistic helmet (`coverage: [Head]`) — **user decision, §8.6-2**. Every other
  piece of armour in the game (272 of 273 `- type: Armor` blocks, `ClothingHeadHelmetSwat` included) is
  unaffected and keeps protecting the whole body. **The aimed-headshot change:** for the five annotated
  vests, aimed torso protection is unchanged, but aimed **head** damage against a vest-only wearer rises
  **2.8 → 11.2** (×4) for a 14-Piercing round, because the vest stops covering the head while the
  unannotated helmet keeps covering the torso. Aimed hand/foot shots now bypass an annotated vest entirely.
  A heavy vest's average *unaimed* Piercing mitigation falls from 75.0% to 52.9%. Combined with amputation:
  a vest at ≤0.46 Slash now lets a 32-Slash machete finish a covered limb it previously could not.
- **Limb wound sprites and severed-limb art.** Per-limb brute/burn damage sprites now render on the attached
  body instead of one aggregate overlay (Option A), and — because the package stayed green — **severed
  limbs render their own wounds too** (Option B, §8.6-5, 156 texture files ported from Onyx,
  CC-BY-SA-3.0/Ubaser, same licence already shipped in WG's own effects). Hand and foot wounds are folded
  onto the arm/leg reading rather than shipped invisible (§8.6-8, P3-D25) — Wolfgate has no hand/foot damage
  art, same as Onyx.

Test counts: **65 of 65 passed** (`Content.IntegrationTests/Tests/_Onyx/Wounds` + `Tests/_Onyx/Body` +
`Tests/_WF/Wolfmed`, `--filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~_Onyx.Body|FullyQualifiedName~Wolfmed"`),
per `C:\tmp\wolfmed-plan\p3\wp\WP11-5-report-tests.log` — up from phase 2's 39. New in phase 3: T-REATTACH,
T-VISUALS (WP11-0); the vendored `AmputationSystem` subscription plus T-AMP-VITAL/T-AMP-GUN/T-AMP-OVERFLOW/
T-AMP-EXPLOSION/T-AMP-CONSEQUENCE-SEPARATE and 3 of Onyx's `AmputationConsequenceTest`s (WP11-1/WP11-5);
7 organ tests T-ORG-DATA/CAP/DESTROY/HEART/BRAIN/EYES/FUNC/INERT (WP11-2/WP11-5); 5 locational-armour tests
including the Wolfgate-only `PartModifiersRouteThroughArmorPenetrationTest` (WP11-3); the restored
`TraumaticAmputationCreatesSevereStumpBleedingTest` (WP11-5).

**Known gaps, recorded for phase 4+:** organ damage has no healing path yet (`OrganHealthSystem.ChangeHealth`
exists and is public — a chem/surgery step is ~15 lines); there is no organ-damage readout on the health
analyzer or in examine (phase 4); `AmputationConsequenceWound` is inert — it marks a stump for examine but
does not yet block Shitmed's `CanAttachPart`, so a severed limb can still be surgically re-attached (P3-D2,
by design — hooking it in phase 3 would make re-attachment permanently impossible). Explosion amputation
also stays out (§8.6-6) pending an `ExplosionSystem` hook phase 4/5 will add alongside a plate regression
test. One pre-existing upstream bug was found and flagged, not fixed:
`Content.Shared/Gibbing/Systems/GibbingSystem.cs:141` throws `InvalidOperationException` when a body part
holding contents (e.g. an arm holding its hand) crosses a `Destructible` gib threshold — routing makes this
more reachable by concentrating damage on one part, but the bug and its one-line `.ToArray()` fix are both
pre-existing and out of PLAN3's authorised edit list.

## Upstream footprint

**27 tracked upstream files** carry `// WOLFGATE` hooks (see the manifest) — 16 shipped in phase 1, 6 more
in phase 2, 5 more in phase 3: `Content.Client/Damage/DamageVisualsSystem.cs` (HOOK 20),
`Content.Shared/Body/Part/BodyPartComponent.cs` (HOOK 21, a single word), `Resources/Prototypes/Body/Organs/human.yml`
(PROTO A, 7 one-line `parent:` edits), `Resources/Prototypes/_Mono/Entities/Clothing/Head/Helmets/bulletproof_helmets.yml`
and `.../OuterClothing/Armor/bulletproof_vests.yml` (PROTO B, 6 `coverage:` lines total) — plus one more phase-3
block in an already-hooked prototype (`Resources/Prototypes/Entities/Mobs/Species/base.yml`, PROTO C's 2
`sprite:` retargets). Phase 2's 6 were `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` (GUARD F),
`Content.Client/Examine/ExamineSystem.cs` (HOOK 14), `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs`
(HOOK 15), `Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs` (HOOK 16),
`Content.Server/Chat/EmoteOnDamageComponent.cs` (HOOK 17), `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs`
(HOOK 18). The phase-1 16 were the `DamageableSystem` seam, the Shitmed targeting guards, the healing and
armour hooks, and the species base prototype block. Every phase-3 hook is one line or one word except PROTO
A/B, which are one-line-per-entry data edits — no upstream file gained a new subscription pair without a §5
audit (phase 3 audited and registered zero new upstream-side pairs; its two new subscriptions,
`<WoundableComponent, PartDamageOverflowedEvent>` and `<BodyPartComponent, AfterAutoHandleStateEvent>`, are
both owned by vendored/hook-body files, not by editing an upstream `Initialize()` a second time).

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
- **(Phase 3) The manifest's WP6-era `OrganDamageSystem.cs` note named the wrong line numbers** for the D26
  amputation comment-outs (`:24`/`:36` instead of the real `:25-26`/`:38-39`) — corrected in the WP11-6
  reconcile, no behaviour change.
- **(Phase 3) `WolfmedBodyPartComponent.MaxDamage` was documented as the amputation gate; it is not.**
  `MaxDamage` only gates `AmputationSystem`'s overflow branch (dead for every organic limb, in Onyx too);
  `amputationThresholds` is what gates severing, and it was already populated for all ten limb abstracts
  since WP7 — no YAML gap ever existed (P3-D12). Corrected in the manifest, not a code change.
- **(Phase 3) Pre-existing upstream bug found, not fixed:** `GibbingSystem.cs:141`'s `Drop` branch enumerates
  a container while removing from it (`InvalidOperationException`), reachable whenever a body part holding
  contents crosses a `Destructible` gib threshold. Flagged for a later package; not in PLAN3's authorised
  edit list.

## Known deviations and balance flags

See `WOLFMED_PLAN.md` §8.2, `WOLFMED_PLAN2.md` §8.2, `WOLFMED_PLAN3.md` §8, and the manifest's Deviations
section (including the consolidated phase-2 §8.2 and phase-3 §8.6 user-decisions summaries). Notable for
playtest: limb damage versus armour changes (armour now applies once), environmental damage creates limb
wounds, do-afters no longer interrupt on wound hosts, wound-host damage is unpredicted (transient client
mispredict), pain stun re-triggers stun VFX per call, a fractured arm's do-after penalty is lost if the
do-after itself opts out of `MultiplyDelay`, the client never mirrors a server-side limb attach/detach
movement-speed refresh (corrects within one network state), and **`WoundPrototype.HealingMultiplier` is
still 1 for every wound** — a balance-pass item kept on record since phase 1. Phase-3-specific: **guns and
lasers can now sever limbs** (§8.6-1, a deliberate deviation from Onyx's melee-only defaults, expressed only
in `_WF/Wolfmed/Body/parts.yml`); **the five `_Mono` vests + one BP helmet quadruple aimed-headshot damage
for their wearer** (§8.6-2); **organ damage is permanent** with no healing path yet (§8.6-4); **explosion
amputation is out** (§8.6-6); **`AmputationConsequenceWound` is inert** — it does not block re-attachment
(P3-D2); Onyx's `MaskComponent.IsToggled` armour gate and its `traumaDeductions` field are not ported
(P3-D20); the two Onyx locational-armour tests that are red against Onyx's own shipped code were rewritten
against the corrected (first-match-wins-then-coverage-fallback) behaviour instead of skipped.

## Next phases (not started)

1. **Phase 4:** treatment and the health analyzer — reagent treatment effects rewritten old-style
   (`SuppressPain`, `MendFractures`, `TakeStaminaDamage`, `TreatmentCapabilities` on
   `HealthChange`/`EvenHealthChange`), tourniquet, medical patch, wound surgeries as Shitmed steps (extend
   Wolfgate's `SurgeryTendWoundsEffectComponent`/`SurgeryWoundedConditionComponent`, do not vendor Onyx's),
   health analyzer wound/organ diagnostics (needs a parallel-UI-vs-graft decision), an organ-healing path,
   the `AmputationConsequenceWound`/`CanAttachPart` gate, and a refund for P3-D1's vital-part Bloodloss
   charge on re-attachment. Reagent id collisions to resolve first: `Stasizium`, `SalicylicAcid`.
2. **Phase 5:** IPC, cybernetic, slime and plant profiles (including their organ-damage and dismemberment
   coverage, currently silent no-ops per §8.6-7). Blocked on a shared stage-based metabolizer for
   non-organic circulatory streams.
3. **Phase 6:** predicted routing; explosion amputation (`ExplosionSystem` hook + a plate regression test,
   §8.6-6); the full 272-entry locational-armour content pass (P3-D6); `HurtCommand` part argument (patch
   kept at `reports/work-packages` as `WP8-hurtcommand-deferred.patch` in `C:\tmp\wolfmed-plan\wp`);
   the `Gibbing/Systems/GibbingSystem.cs:141` container-mutation-during-enumeration fix.

## How to verify

```bash
dotnet build Content.Server/Content.Server.csproj -c DebugOpt
dotnet build Content.Client/Content.Client.csproj -c DebugOpt
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --filter "FullyQualifiedName~_Onyx.Wounds|FullyQualifiedName~Wolfmed"
dotnet run --project Content.YAMLLinter -c Release
```

A worktree needs `RobustToolbox` junctioned from the main checkout before building: `cmd /c rmdir RobustToolbox` then `cmd /c mklink /J RobustToolbox <main>\RobustToolbox`; remove the junction with `cmd /c rmdir RobustToolbox` afterwards.
