# CRITIQUE2 — completeness critique of `p2/PLAN2.md` and the five phase-2 analyst reports

**Role:** completeness critic. Every claim below was re-derived by reading the real files in
`WG = C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` and
`ONYX = C:/tmp/onyx` (pin `2f5bab9`, via `git show HEAD:<path>` where the file is outside the sparse set).
Nothing here is taken on the word of PLAN2 or of `fractures.md` / `pain-hud.md` / `statuses.md` /
`examine.md` / `tests.md`.

**Verdict:** PLAN2 is accurate on the hard parts everyone worried about (the hands rewrite, the
`OrganGot*` shapes, the duplicate-subscription audit, the locale collision, the dead Onyx `emotes:` key)
and is wrong or incomplete on five things that will either break the build, break D2, or make three of the
new tests fail for reasons unrelated to Wolfmed. **3 blockers, 5 majors, 8 minors.**

---

## 1. Blockers

### B1 — `HealthExaminableSystem.PartStatus.cs` will not compile: `DamageableComponent` is not in `Content.Shared.Damage.Components` in Wolfgate

**Evidence.** ONYX `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs:6` is
`using Content.Shared.Damage.Components;` and the body uses the type at

```csharp
            if (TryComp(part, out DamageableComponent? damageable))
```

In Wolfgate the *file* is at `Content.Shared/Damage/Components/DamageableComponent.cs` but the
*namespace* is not:

```
WG/Content.Shared/Damage/Components/DamageableComponent.cs:9 : namespace Content.Shared.Damage
WG/Content.Shared/Damage/Components/DamageableComponent.cs:21:     public sealed partial class DamageableComponent : Component
```

`Content.Shared.Damage.Components` does exist in WG (`ActiveStaminaComponent.cs:1`,
`DamageContactsComponent.cs:4`, …) so the `using` directive itself compiles — but it imports the wrong
namespace and `DamageableComponent` is then unresolved: **CS0246**. Phase 1 already hit exactly this and
marked it: `WG/Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:4`
`using Content.Shared.Damage; // WOLFGATE: DamageableSystem/DamageableComponent live here, not in .Components/.Systems.`

**Who missed it.** `examine.md` §3 states flatly: *"No other using in either partial needs a change"*.
PLAN2 inherited that: §3's in-vendored-file table and WP10-2's file-1 row both say **2 edits**.

**Fix (concrete).** In `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs`:

```csharp
using Content.Shared.Damage; // WOLFGATE: DamageableComponent lives here in Wolfgate, not in .Damage.Components.
using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12 damage facade.
```

(the second is also missing — see m2 — and the same `_WF.Wolfmed.Compat` using is missing from
`Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`, whose D12 swap PLAN2 lists as "1-2 edits").
The file-1 edit count becomes **4**, not 2; update §3, WP10-2 and the §7 manifest row.

---

### B2 — GUARD F gives the Onyx part-status readout to every non-wound-host that has a body, breaking D2

**Evidence.** Onyx's `CreateMarkup` calls `AddPartStatusMarkup(uid, examiner, msg);` **outside** the
`if (!HasComp<WoundHostComponent>(uid))` wrap (ONYX `Content.Shared/HealthExaminable/HealthExaminableSystem.cs`,
read in full: the `<Onyx-PartHealthExamine-edited>` block closes at `}` and the call is a *separate* tagged
block immediately after). PLAN2 §3 GUARD F (e) copies that placement verbatim:
*"before `:97` insert `AddPartStatusMarkup(uid, examiner, msg);`"*.

That is safe in Onyx because everything with a body there is a wound host. It is **not** safe in Wolfgate:

* `HealthExaminableComponent` is declared on `BaseMob` — `WG/Resources/Prototypes/Entities/Mobs/base.yml:109` —
  i.e. on *every* mob, not on `BaseMobSpeciesOrganic`.
* `AddPartStatusMarkup` only early-returns when `_body.GetBodyChildren(examined)` is empty. Borg chassis
  (`base_borg_chassis.yml:60`), NPC silicons (`NPCs/silicon.yml:41`), EE silicons
  (`_EinsteinEngines/.../silicon_base.yml:284`), animals, and — most pointedly — **Protogen**, which D32
  deliberately strips `WoundHost` from, all have bodies and all keep the legacy threshold text (because
  GUARD F's wrap lets it through for non-hosts).
* Result: those entities get **both** readouts. That is a plain D2 breach
  ("Entities without `WoundHostComponent` behave exactly as today").
* It does not crash — `WoundSystem.GetWounds` is `Resolve(..., false)`-guarded
  (`WG/Content.Shared/_Onyx/Wounds/WoundSystem.cs:155-159`) — so the build and the YAML lint stay green and
  nobody notices until someone examines a borg.

**Fix.** Make GUARD F (e) conditional rather than unconditional — fold it into the `else` of the wrap:

```csharp
        if (!HasComp<WoundHostComponent>(uid)) // WOLFGATE: GUARD F
        {
            … legacy threshold loop + msg.IsEmpty fallback …
        }
        else
            AddPartStatusMarkup(uid, examiner, msg); // WOLFGATE: GUARD F — wound hosts only (D2).
```

and record the divergence from Onyx (Onyx calls it unconditionally) as a deviation. If the team *wants*
part status on silicons, that is a scope decision for the user, not a side effect of a hook.

---

### B3 — `T-FRACT-EFFECTS` cannot measure `0.75`: the fixture has no hands, so `GetDurationMultiplier` returns `1f`

**Evidence.** `FractureEffectSystem.OnGetMultiplier` returns immediately unless
`TryGetUsedHandSymmetry` succeeds, and that method's first guard is
`if (!TryComp(body, out HandsComponent? hands)) return false;` (ONYX `FractureEffectsSystem.cs`, and
unchanged by P2-D2's rewrite — PLAN2 §4/WP10-1 keeps that line).

The fixture PLAN2 names has no `HandsComponent`:

```
WG/Content.IntegrationTests/Tests/_Onyx/Wounds/WoundFractureTest.cs:38-47
  id: WoundFractureBody
  parent: InventoryBase
  components:
  - type: Body            (WoundFractureBodyGraph: torso → left arm, left leg — no hand slot)
  - type: Damageable
  - type: MovementSpeedModifier
  - type: WoundHost
```

and `InventoryBase` supplies only `Inventory` + `InventorySlots`
(`WG/Resources/Prototypes/InventoryTemplates/inventorybase.yml:3-6`) — **not** `Hands`.

Even adding `- type: Hands` is not enough: a hand only exists once an *enabled* `BodyPartType.Hand` part is
attached — `WG/Content.Server/Hands/Systems/HandsSystem.cs:116-135`
(`TryAddHand` → `part.Comp.PartType != BodyPartType.Hand → return`, then `AddHand(uid, slot, location)` with
`BodyPartSymmetry.Left => HandLocation.Left`), driven from `HandleBodyPartAdded` at `:137-140`.

So on the fixture as it stands, `GetDurationMultiplier(body)` is **`1f`**, not the `0.75f` P2-D16 derives
(and not Onyx's `2f` — which is why Onyx's own `EffectsRefreshOnTreatmentHealingAndDetachTest` could never
have passed either; its fixture has no hands at all, ONYX `WoundFractureTest.cs:22-64`). PLAN2 knows the
failure mode — T-FRACT-HANDS says *"otherwise the multiplier test silently measures 'no hand found' (1f)"* —
but still schedules T-FRACT-EFFECTS against the **unmodified** fixture and predicts 0.75, and puts the
fixture extension only under T-FRACT-HANDS as an "or a second body id" option. Worse, T-FRACT-HANDS's guard
assertion `Comp<HandsComponent>(body)` **throws** on the current fixture.

**Fix.** Make the fixture change a hard prerequisite of T-FRACT-EFFECTS, not of T-FRACT-HANDS:

```yaml
- type: body
  id: WoundFractureBodyGraph
  root: torso
  slots:
    torso: { part: TorsoHuman, connections: [left arm, left leg] }
    left arm: { part: LeftArmHuman, connections: [left hand] }
    left hand: { part: LeftHandHuman }
    left leg: { part: LeftLegHuman }
…
  - type: Hands
```

(`LeftHandHuman` / `RightArmHuman` / `RightHandHuman` all exist —
`WG/Resources/Prototypes/Body/Parts/human.yml:72,63,83`.) With the hand present the P2-D16 arithmetic does
hold: fractured left arm Comminuted → `manipulationModifier 0.75`, `Arm` absent from `PartEffectScales`
(`WoundDamageComponents.cs:87-92` declares only Leg 0.5 / Foot 0.5 / Hand 0.75) → `1 + (0.75-1)·1·1 = 0.75`;
the undamaged left hand contributes `1 + (1-1)·0.75·1 = 1`. Product **0.75**. Also assert
`_hands.GetActiveHand(...)` is non-null *before* the multiplier assertion, as PLAN2 already asks.

The leg half of P2-D16 is correct as written: 75 Blunt → Comminuted (threshold 60,
`wounds.yml:72-76`), `movementModifier: 0`, `PartEffectScales[Leg] = 0.5` → `ModifySpeed(1 - (1-0)·0.5·1)` =
walk **0.5**.

---

## 2. Majors

### M1 — P2-D7's premise is false: the phase-1 `# WOLFGATE` block does **not** track the Protogen exclusion

PLAN2 P2-D7 and WP10-5 file 4 both justify putting `- type: PainShockTarget` and `- type: EmoteOnDamage`
on `BaseMobSpeciesOrganic` *"inside the existing `# WOLFGATE` block, so it tracks `WoundHost` and the
D32 protogen exclusion."*

D32 is **not** implemented as a prototype-level removal. It is a C# opt-out that removes exactly one
component:

```
WG/Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs:12  ExcludedAncestors = { "BaseMobProtogen" }
WG/Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs:17  SubscribeLocalEvent<WoundHostComponent, ComponentInit>(OnWoundHostInit);
WG/Content.Shared/_WF/Wolfmed/Body/WolfmedWoundHostExclusionSystem.cs:31  RemComp<WoundHostComponent>(ent);
```

Consequences:

* `PainShockTarget` on Protogen is **inert** (no `PainComponent` is ever ensured on a non-host —
  `WoundDamageProjectionSystem.cs:212,224,237` are the only `EnsureComp<PainComponent>` sites and they sit
  behind the wound-host path; `PainSystem.Update`'s query at `:142` needs all three components). Harmless,
  but the stated reason is still wrong and the manifest would record a false claim.
* `EmoteOnDamage` on Protogen is **live**. `HandlePainDamageEmote` (ONYX
  `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`) checks `EmotesThreshold.Count`, cooldown,
  `_random.Prob`, mob state and pain numbness — **never** `HasComp<WoundHostComponent>`, and reads
  `_damageable.GetTotalDamage(uid)`, which works for any damageable. So the D32-excluded species gets the
  new pain screams. D2 breach.

**Fix.** Either (a) add `PainShockTargetComponent` and `EmoteOnDamageComponent` to
`WolfmedWoundHostExclusionSystem`'s removal (it already runs on `<WoundHostComponent, ComponentInit>`, so
the hook point exists), or (b) add one `// WOLFGATE` line at the top of `HandlePainDamageEmote`
(`if (!HasComp<WoundHostComponent>(uid)) return;`) — which is also the honest gate given the feature is
being shipped as "Wolfmed pain sounds". Either way, correct P2-D7's rationale.

### M2 — `T-FRACT-ALERT` / `T-FRACT-ALERT-NEG` are non-deterministic: fracture creation is a dice roll

PLAN2 §6.2 specifies *"35 Blunt to the leg (clears `alertMinimumGrade: Simple` at 35, stays below
Comminuted at 60)"*. Creation is gated on a random roll:

```
WG/Content.Shared/_Onyx/Wounds/WoundFractureSystem.cs:51-55
        var hitGrade = GetGrade(profile, effectiveTrauma);
        if (hitGrade == FractureGrade.None ||
            !profile.Grades.TryGetValue(hitGrade, out var gradeSettings) ||
            !_random.Prob(Math.Clamp(gradeSettings.CreationChance, 0f, 1f)))
            return;
```

and the shipped profile declares `Hairline creationChance: 0.05`, `Simple: 0.25`, `Displaced: 0.65`,
`Comminuted: 1` (`WG/Resources/Prototypes/_Onyx/Wounds/wounds.yml:56-76`). A 35-damage hit therefore creates
a fracture **25 % of the time** — the test fails three runs in four. This is precisely why phase 1's
`PostArmorHitAndTreatmentPreconditionsTest` hits for 150 through a ×0.5 armour to land on 75 (Comminuted,
chance 1): it is the only deterministic path. `T-FRACT-ALERT-NEG`'s "optionally a second at 25" has the
mirror problem (5 % chance a Hairline *does* appear — the alert assertion still passes, so it is only a
misleading test rather than a flaky one).

**Fix.** Reach Simple deterministically: create at ≥60 (chance 1), then bring severity down into the Simple
band via `_wounds.ChangeSeverity` / a heal, since `OnWoundChanged` re-grades without any random roll
(`WoundFractureSystem.cs:73-86`). Alternatively pin the RNG for the fixture. Say so in the WP text — a
"prediction until measured" note (P2-D16) does not help against non-determinism.

### M3 — `T-PAIN-SHOCK`'s fixture spec is insufficient: `Stun`/`KnockedDown` are not `alwaysAllowed`

PLAN2 §6.2 and §8.3 item 7 both say the interim fixture needs **`- type: StatusEffects`**. That component
with a default (empty) `allowed` list blocks the stun:

```
WG/Resources/Prototypes/status_effects.yml:4-10
- type: statusEffect
  id: Stun
  alert: Stun
- type: statusEffect
  id: KnockedDown
  alert: Stun
```

Neither carries `alwaysAllowed: true` (only `Jitter:17`, `PressureImmunity:39` and three downstream files
do). `BaseMobSpecies` gets them by listing them explicitly (`base.yml:125-127`). So
`StunSystemOnyxCompat.TryUpdateParalyzeDuration` → `SharedStunSystem.TryParalyze` silently no-ops on the
bespoke fixture and the test fails for an unrelated reason — the exact trap PLAN2 documents but does not
spell out.

**Fix.** `- type: StatusEffects` **`allowed: [Stun, KnockedDown, Jitter]`** in `WolfmedPainShockBody`.
(`Jitter` is `alwaysAllowed` so it is belt-and-braces, but `_jitter.DoJitter` at `PainSystem.cs:298` also
needs the component to exist at all — `SharedJitteringSystem.cs:49` `Resolve(uid, ref status, false)`.)

### M4 — `Docs/Wolfmed/WOLFMED_MANIFEST.md` has no owner, while four work packages run in parallel and are told to edit it

Ground rule 6: *"Record every file you touch in `Docs/Wolfmed/WOLFMED_MANIFEST.md` (§7) in the same work
package."* §4's graph puts WP10-1/-2/-3/-4/-6a in group F1 **in parallel**, and also assigns the manifest to
WP10-7. PLAN2 itself identifies this hazard for `base.yml` ("§8.3 item 2: **`base.yml` has one owner**.
If two work packages edit the phase-1 `# WOLFGATE` block concurrently, one silently loses") and then leaves
the manifest — which *all five* F1 packages must append to — unserialised.

**Fix.** Either declare WP10-7 the sole editor of `WOLFMED_MANIFEST.md` and have each WP emit its rows into
`C:/tmp/wolfmed-plan/p2/manifest-rows-WP10-N.md` for WP10-7 to merge, or serialise the manifest edit at the
end of each WP. Same for `Docs/Wolfmed/WOLFMED_PLAN.md` (§4/WP10-7 corrects two stale notes there).

### M5 — `FractureEffectSystem`'s `WoundStatusEffectSystem` dependency is missing from every list in PLAN2

`FractureEffectSystem` holds `[Dependency] private WoundStatusEffectSystem _statusEffects = default!;` and
calls it in two handlers:

```csharp
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotInsertedEvent args)
    { … _statusEffects.HandlePartInserted(part.Owner); }
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotRemovedEvent args)
    { … _statusEffects.HandlePartRemoved(part.Owner, args.Target); }
```

PLAN2 §2's "everything it needs already exists — verified present and usable" list, §4/WP10-1's exact-edit
section and §7's manifest row all omit `WoundStatusEffectSystem`. The symbols do exist
(`WG/Content.Shared/_Onyx/Wounds/WoundStatusEffectSystem.cs:109` `public void HandlePartRemoved(EntityUid part, EntityUid body)`,
`:119` `public void HandlePartInserted(EntityUid part)`), so this is not a build break — but a repo-wide grep
shows **zero callers today** (only the internal `RefreshPartWounds:133`). WP10-1 therefore activates
Onyx's wound→status-effect apply/remove-on-limb-attach path for the first time. It is inert only because
P2-D1 established that no wound prototype populates `WoundStatusEffectBehavior.StatusEffect`; that
dependency between P2-D1's evidence and WP10-1's behaviour is not stated anywhere.

**Fix.** Add the symbol to §2's "already exists" list, say in WP10-1 that the limb-attach status-effect path
goes live, and add a one-line deviation ("inert at the pin because no wound names a `StatusEffect`; revisit
if a wound ever does").

---

## 3. Minors

| # | Finding | Evidence | Fix |
|---|---|---|---|
| **m1** | HOOK 15's code block references `PainComponent`, `PainSystem` and `FixedPoint2`, but `DamageOverlay.cs`'s using block (`:1-7`: `System.Numerics`, `Content.Shared.Mobs`, `Robust.Client.Graphics`, `Robust.Client.Player`, `Robust.Shared.Enums`, `Robust.Shared.Prototypes`, `Robust.Shared.Timing`) has none of them. PLAN2 says only that `_entityManager`/`_playerManager` are already injected. | read directly | Add `using Content.Shared._Onyx.Wounds;` + `using Content.Shared.FixedPoint;`. (`System<T>()` is fine — `Robust.Shared.GameObjects` is a global using, `WG/Content.Client/GlobalUsings.cs:9`.) |
| **m2** | §3's in-vendored-file table describes the PartStatus.cs D12 edit as `[Dependency] DamageableSystem` → `WolfmedDamageableSystem`. In Onyx that dependency lives in `HealthExaminableSystem.cs:15`, **not** in the partial, so in Wolfgate it is an *addition* (GUARD F deliberately leaves the base file without a facade). An implementer searching for the line to change will not find one. | ONYX `HealthExaminableSystem.cs:14-15`; WG's copy has only `_examineSystem` at `:12` | Reword as "add", and count it plus its `using` (→ 4 edits with B1). |
| **m3** | HOOK 14 (a) widens **every** examine popup in the game from 400 to 560 px, not just wound hosts' — a global UI change riding on a wound port. | `WG/Content.Client/Examine/ExamineSystem.cs:202` | Ship it (Onyx does the same) but record it as a deviation; it is the one HOOK 14 site that is not behaviour-neutral for non-hosts. |
| **m4** | §7's manifest row says `health-examinable.ftl` is "43 keys". The file declares **45** message ids, four of which (`wound-examine-frame-*`) are the dead-weight set PLAN2 separately (correctly) calls out. | ONYX `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl`, read in full | Correct the count. |
| **m5** | WP9 open item 3 — *"`WoundPrototype.HealingMultiplier` is 1 for every ported wound … Onyx's tests imply 0.15 was intended. A D4 tuning item."* — appears nowhere in PLAN2 (§7 deviations, §8.2 decisions, §8.4 deferred). WP9's other four open items are all carried. | `C:/tmp/wolfmed-plan/wp/WP9-report.md` "Open items handed forward" item 3 | Add to §8.4 (deferred) or §8.2 (user decision) so it is not silently dropped between phases. |
| **m6** | HOOK 16 (WP10-4) stops the controller writing the brute vignette for `PainComponent` holders; the pain-numb suppression the HUD relies on is `IsPainNumb`'s widening in **WP10-3**. Both sit in group F1 with no stated order, so a tree with WP10-4 but not WP10-3 shows the vignette to pain-numb characters. | §4 graph; P2-D8 | One sentence: WP10-4's behaviour is only correct once WP10-3 lands; both are F1 so ship them together. |
| **m7** | §5.1 row 2 says `GetManipulationDurationMultiplierEvent` "has no subscriber at all today". True — but it also has **no raiser**: `grep SubscribeLocalEvent<…, GetManipulationDurationMultiplierEvent>` over `Content.{Shared,Server,Client}` returns nothing, and the only raiser is `FractureEffectSystem.GetDurationMultiplier`, which does not exist yet. The row understates how dead the event is. | re-grepped | Cosmetic; say "declared at `WoundEvents.cs:116`, neither raised nor subscribed in WG today". |
| **m8** | P2-D15 is right that selection is checked both ways (`WG/Content.Client/Lobby/UI/HumanoidProfileEditor.xaml.cs:998` `selProto.MutuallyExclusiveTraits.Contains(traitId) \|\| thisProto.MutuallyExclusiveTraits.Contains(sel)`), but the **tooltip** enumeration at `:725-728` is one-directional, so `PainNumbness`'s tooltip will not mention `HighPainThreshold`. | read directly | Accept and note, or add the reciprocal entry to `Traits/disabilities.yml` — which §3 currently forbids. |

---

## 4. Claims I re-verified and found **correct** (do not re-litigate)

These were spot-checked against the real trees because a completeness critic has to be able to say which
half of the plan is solid.

| Claim | Verification |
|---|---|
| **P2-D2** hands rewrite compiles and preserves semantics | `WG/Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs:177` `public Hand? GetActiveHand(Entity<HandsComponent?> entity)`, `:285` `public bool IsHolding(EntityUid uid, [NotNullWhen(true)] EntityUid? entity, [NotNullWhen(true)] out Hand? inHand, HandsComponent? handsComp = null)`; `HandsComponent.cs:107` `public sealed class Hand`, `:113` `public HandLocation Location { get; }`, `:156-161` `enum HandLocation : byte { Left, Middle, Right }`. `statuses.md` §6 and `tests.md` §4.3 are both wrong; `fractures.md` §2.3 is right. |
| **P2-D3** no using swap for `OrganGot*` | `WG/Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs` declares `namespace Content.Shared.Body;` + `[ByRefEvent] public readonly record struct OrganGot{Inserted,Removed}Event(EntityUid Target);` — identical shape. Raised on the part with `Target` = body by `WG/Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs:41-43,60-64`, which is what `<WoundableComponent, OrganGot*Event>` needs. |
| **§5.1 subscription audit rows 1-10 all free** | Independently re-grepped `SubscribeLocalEvent<[^>]*, *<Event>>` across `Content.{Shared,Server,Client}` for all ten events. `GetManipulationDurationMultiplierEvent`, `FractureGradeChangedEvent`, `FractureTreatmentChangedEvent`, `OrganGotInsertedEvent`, `OrganGotRemovedEvent`, `BodyPartFunctionalityChangedEvent`, `ModifyPainGainEvent`: **zero** subscribers anywhere. `WoundRemovedEvent`: only `WoundStatusEffectSystem.cs:34` (`WoundableComponent`), `WoundBleedingSystem.cs:44`, `WoundInternalBleedingSystem.cs:22` — none on `WoundFractureComponent`. `RefreshMovementSpeedModifiersEvent`: 25 subscribers, none on `WoundHostComponent`. `GetDoAfterDelayMultiplierEvent`: `DoAfterDelayMultiplierComponent` (`_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:13`) and `BodyComponent` (`_Shitmed/Body/Systems/SharedBodySystem.Relay.cs:11`) — different components. |
| **`ref` handler on the class event is legal** | `WG/Content.Shared/_Goobstation/DoAfter/DoAfterDelayMultiplierSystem.cs:27` already does `private void OnGetMultiplier(Entity<DoAfterDelayMultiplierComponent> ent, ref GetDoAfterDelayMultiplierEvent args)` against a by-value raise at `SharedDoAfterSystem.cs:208-214`. `fractures.md` §4.3's by-value proposal is the wrong one. |
| **P2-D12** locale collision is real | `WG/Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl:46-52` holds the `# WOLFGATE (WP7)` comment + the four `wound-examine-fracture-*` keys; ONYX `health-examinable.ftl` defines the same four. Deleting `:46-52` is exactly right. `part-status.ftl` is correctly skipped. |
| **P2-D9** Onyx's key really is dead | ONYX `Content.Server/Chat/EmoteOnDamageComponent.cs:24-25` — `[DataField]` (no explicit name) on `EmotesThreshold` ⇒ YAML key `emotesThreshold`; ONYX `Resources/Prototypes/Body/species_base.yml:125` writes `emotes:`. Dead at the pin. `- type: PainShockTarget` is at `species_base.yml:54`. `examine.md` §9's "already fully shipped, strike it" is wrong; `pain-hud.md` §4.2 is right. |
| **GUARD E2** site and shape | `WG/Content.Server/Body/Systems/BloodstreamSystem.cs:280` is the `GetBloodLevelPercentage(...) < ent.Comp.BloodlossThreshold` line; `using Content.Shared._Onyx.Wounds;` already present at `:18`. Matches Onyx's own guard verbatim (`ONYX Content.Shared/Body/Systems/BloodstreamSystem.cs:292` `if (!HasComp<WoundHostComponent>(ent) && …) // <Onyx-PartHealthExamine-edited>`). The two bleeding branches above are correctly left alone. |
| **GUARD F line numbers** | `WG/.../HealthExaminableSystem.cs:32` call site, `:45` signature, `:49` `var first = true;`, `:91-94` the `msg.IsEmpty` block, `:97` `RaiseLocalEvent`. `CreateMarkup` has exactly one caller repo-wide, so the signature change is safe. |
| **HOOK 14 (b)** matches Onyx byte-for-byte | ONYX `Content.Client/Examine/ExamineSystem.cs` `<Onyx-PartHealthExamine-edited>` wraps the same three lines inside the same `foreach`, keeping the `break;` outside. WG's sites are `:202` and `:279-281`. |
| **WP10-1 needs zero prototypes/locale/textures** | `WG/Resources/Prototypes/_Onyx/Alerts/alerts.yml` (`BrokenBones`, no `category:`), `WG/Resources/Textures/_Onyx/Interface/Alerts/fracture.rsi/{brokenbones.png,meta.json}`, `WG/Resources/Locale/en-US/_Onyx/medical/fractures.ftl` (2 keys), `OrganicFractureProfile` with all four grades + `alert:`/`alertMinimumGrade:`/`alertHiddenTreatments:` — all present. `AlertsSystem.ShowAlert` `:81` / `ClearAlert` `:153` / `IsShowingAlert` `:39` all match the call shapes. |
| **Client sandbox: nothing to do** | `System.Text.StringBuilder` including `Append(object)` and `Clear()` is whitelisted (`WG/RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml:897-955`). RT 277 has `FormattedMessage.FromMarkupPermissive` (`:118,126`), `RemoveMarkupPermissive` (`:159`), `EscapeStringParameter` (`:139`), `MarkupNode.ToString()` (`:37,139`), `RichTextLabel.SetMessage(FormattedMessage, Type[]?, Color?)` (`:110`), `ScrollContainer.ReturnMeasure` (`:73`), `Vector2Helpers.Infinity` (`Robust.Shared.Maths/Vector2Helpers.cs:10`, covered by the global using) and all five `RichText` tag types. |
| **`emoteCooldown: 8` parses** | `TimespanSerializer.Read` → `TimeSpanExt.TryTimeSpan(str)` `:56-60` treats a bare number as seconds. `Scream` (`speech_emotes.yml:3`) and `Crying` (`:110`) exist. |
| **`HighPainThreshold` is collision-free and lands correctly** | `grep -rn HighPainThreshold` across WG C#/YAML/FTL → zero. `Quirks` category exists with no `maxTraitPoints` (`Traits/categories.yml:11-12`), so dropping `cost: 3` is inert. `PainNumbness` trait at `Traits/disabilities.yml:69`. `TraitPrototype.MutuallyExclusiveTraits` exists (`Content.Shared/Traits/TraitPrototype.cs:69`). `ModifyPainGainEvent` is raised on the **body** at `PainSystem.cs:114-116` and `:239-241` — agrees with `TraitSystem` adding to `args.Mob`. |
| **P2-D8's placement** | `PainSystem.cs:324-331` `IsPainNumb` does the part→body redirect first; `using Content.Shared.Traits.Assorted;` is already at `:13`; `PainNumbnessComponent` is `[RegisterComponent, NetworkedComponent]` so the client's `GetPain` agrees with the server's. |
| **P2-D14** | `BodyPartFunctionalitySystem.cs:19-21` returns `Disabled` for `CyberneticsComponent.Disabled` **above** the `CCVars.WoundsBodyPartFunctionalityEnabled` gate at `:23-24`. Correct. |
| **HUD numbers** | `PainComponent.SoftPainCap = 135` (`WoundDamageComponents.cs:137`), `PainShockThreshold = 130` (`PainSystem.cs:31`), so a 0.05 floor ⇒ vignette from pain 6.75 and 0.963 at shock. `FixedPoint2` has `implicit operator FixedPoint2(float)` (`:202`), `Min(FixedPoint2,FixedPoint2)` (`:215`) and `Float()` (`:186`), so the proposed expression compiles. |
| **`ExamineDescription` exists** | `WG/Content.Shared/_Onyx/Wounds/WoundBehaviors.cs` `WoundStageDefinition.ExamineDescription` is byte-identical to Onyx's — it is in `WoundBehaviors.cs`, not `WoundPrototype.cs`, which is why a naive grep of the latter comes up empty. |

---

## 5. Recommended plan edits, in order

1. **B1** — add the two `// WOLFGATE` usings to `HealthExaminableSystem.PartStatus.cs` and the one to
   `EmoteOnDamageSystem.PainSounds.cs`; update §3's table, WP10-2's file-1 row and §7's manifest rows to the
   real edit counts.
2. **B2** — move `AddPartStatusMarkup` into GUARD F's `else`; record the divergence from Onyx.
3. **B3** — extend `WoundFractureBodyGraph` with `- type: Hands` + a `left hand: LeftHandHuman` slot, and
   make that a stated prerequisite of **T-FRACT-EFFECTS**, not only T-FRACT-HANDS.
4. **M1** — decide the Protogen gate (exclusion-system removal vs. a `HasComp<WoundHostComponent>` line in
   `HandlePainDamageEmote`) and rewrite P2-D7's rationale.
5. **M2 / M3** — respecify the two fracture-alert tests deterministically and give
   `WolfmedPainShockBody` an explicit `allowed: [Stun, KnockedDown, Jitter]`.
6. **M4** — give the manifest a single owner.
7. **M5** and the minors — documentation only.

None of these changes the phase-2 scope, the work-package split, or any of the seven §8.2 user decisions.
