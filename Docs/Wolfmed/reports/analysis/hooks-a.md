# Wolfmed hook inventory — batch A

Onyx pinned at `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. WG = Wolfgate worktree
`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`.
All WG line numbers are current as of this read; they will drift as other Wolfmed batches land — re-grep before
patching.

Every claim below was read from the actual files (or `git -C C:/tmp/onyx show HEAD:<path>` for the one path
outside the sparse set); no signatures are assumed from memory.

## Top-level verdict

The handoff doc's characterization of these nine files as "small `// WOLFGATE` hooks" is **only true for
`BloodstreamSystem.cs` and `SharedArmorSystem.cs`**. The other files have diverged too far architecturally
for a 1–2 line insertion:

- **`HealthAnalyzerSystem.cs` / `HealthAnalyzerScannedUserMessage.cs`**: Wolfgate's Shitmed fork already
  rebuilt the health-analyzer payload around a `TargetIntegrity` doll (`Dictionary<TargetBodyPart,
  TargetIntegrity>`). Onyx independently rebuilt the same file around a `HealthAnalyzerUiState` struct
  carrying `DamageSpecifier` + wound diagnostics + organs + chemicals. These are two different, incompatible
  rewrites of the same vanilla file. There is no small hook here — this is a phase‑4 redesign question.
- **`HealingSystem.cs`**: Onyx's version is driven by three types that don't exist in Wolfgate at all
  (`WoundHealingSystem`, `ResolveHealingPartEvent`, `TargetResolverSystem`), and Wolfgate's own version is a
  much older, simpler fork with a different `TryHeal`/`HasDamage` signature shape. Real porting only becomes
  possible once the wound core (phase 1) exists.
- **`DamageableSystem*.cs`**: has **zero** Onyx-marked lines. Onyx doesn't hook this file at all — it routes
  around it via `BeforeDamageChangedEvent`, which **Wolfgate already has** (added by Shitmed, not Onyx). This
  is good news: D2's damage-bridge design is directly compatible with Wolfgate's existing event.
- **`DamageSpecifier*.cs` (ArmorPenetration)**: Wolfgate already has its own, differently-shaped
  armor-penetration system ("Goobstation partial AP", `DamageSpecifier.PenetrateArmor` +
  `DamageModifierSet.IgnoreArmorPierceFlags`) wired through five call sites. D5's claim that
  `DamageSpecifier.ArmorPenetration` is "missing" is misleading — an equivalent capability exists, just under
  a different architecture. Porting Onyx's field-based `ArmorPenetration` verbatim would create two competing
  armor-pen systems.

**Blocker found, not previously flagged:** Onyx's `Content.Shared/_Onyx/Targeting/TargetingComponent.cs`
registers under the RobustToolbox-derived component name `"Targeting"` — the exact same name Wolfgate's
existing `Content.Shared/_Shitmed/Targeting/TargetingComponent.cs` already registers under. RobustToolbox's
`ComponentFactory` throws `InvalidOperationException($"{name} is already registered...")` on a duplicate name
(`RobustToolbox/Robust.Shared/GameObjects/ComponentFactory.cs:122`) — this is a hard crash at server start,
not a soft conflict. Onyx's `TargetingComponent` (and everything in `_Onyx/Targeting` that depends on its
class name for prototype/network serialization) needs a rename before it can be vendored, even though D6/D1
call for vendoring at-relative-path "verbatim." This is unavoidable, so it should be documented as a permitted
exception to "verbatim," e.g. rename the class to `WoundTargetingComponent` and grep-replace its ~10 shared/
client/server usages within `_Onyx/Targeting`, `_Onyx/Wounds`, `_Onyx/Medical` before first compile.

---

## 1. `Content.Server/Medical/HealthAnalyzerSystem.cs`

Onyx marks 33 distinct tag occurrences (not 37 lines of substance — many are open/close comment pairs)
across five nested feature tags: `Onyx-HealthAnalyzer-StatusDoll` (outer), `Onyx-BodyScanner`,
`Onyx-VitalDamage`, `Onyx-HealthAnalyzerPain`, `Onyx-HealthAnalyzerOrgans-edited`,
`Onyx-HealthAnalyzerChemicals`.

### 1a. Imports / dependencies (Onyx lines 3–12, 14, 25, 51–59)

```csharp
// <Onyx-HealthAnalyzer-StatusDoll>
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Medical;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint; // <Onyx-HealthAnalyzerPain>
// </Onyx-HealthAnalyzer-StatusDoll>
...
using Content.Shared.Chemistry.Components; // <Onyx-HealthAnalyzerChemicals>
...
using Content.Shared.Mobs.Systems; // <Onyx-VitalDamage>
...
// <Onyx-HealthAnalyzer-StatusDoll>
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private PainSystem _pain = default!; // <Onyx-HealthAnalyzerPain>
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    // </Onyx-HealthAnalyzer-StatusDoll>
    [Dependency] private MobThresholdSystem _mobThreshold = default!; // <Onyx-VitalDamage>
```

**WG insertion point:** `Content.Server/Medical/HealthAnalyzerSystem.cs:1–37` (usings) and `:33–42`
(dependencies). WG already has its own competing set — `Content.Shared.Body.Part`, `Content.Shared.Body.
Systems`, `Content.Shared._Shitmed.Targeting` for the Shitmed doll (lines 24–27), and `_bodySystem` (line 37)
in place of `_body`. None of `WoundSystem`, `PainSystem`, `BodyPartFunctionalitySystem` exist yet.

**Classification: later phase (4).** Cannot add these dependencies until the wound core is ported; adding
them now would not compile.

### 1b. `GetHealthAnalyzerUiState` payload assembly (Onyx lines 259–313)

```csharp
        // <Onyx-VitalDamage>
        FixedPoint2? vitalDamage = null;
        if (HasComp<WoundHostComponent>(entity))
        {
            var damageable = Comp<DamageableComponent>(entity);
            vitalDamage = _mobThreshold.CheckVitalDamage(entity, damageable);
        }
        // </Onyx-VitalDamage>

        return new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable,
            // <Onyx-HealthAnalyzer-StatusDoll>
            BuildPartDamage(entity),
            BuildWoundDiagnostics(entity),
            // <Onyx-VitalDamage>
            vitalDamage,
            // </Onyx-VitalDamage>
            // <Onyx-HealthAnalyzerOrgans-edited>
            BuildOrganInfo(entity),
            // </Onyx-HealthAnalyzerOrgans-edited>
            BuildChemicalInfo(entity, bloodstream) // <Onyx-HealthAnalyzerChemicals>
            // </Onyx-HealthAnalyzer-StatusDoll>
        );
```

**WG equivalent (`UpdateScannedUser`, WG lines 264–287):**

```csharp
264:        // Shitmed Change Start
265:        Dictionary<TargetBodyPart, TargetIntegrity>? body = null;
266:        if (HasComp<TargetingComponent>(target))
267:            body = _bodySystem.GetBodyPartStatus(target);
268:        // Shitmed Change End
269:
270:        if (TryComp<UnrevivableComponent>(target, out var unrevivableComp) && unrevivableComp.Analyzable)
271:            unrevivable = true;
272:
273:        if (TryComp<UncloneableComponent>(target, out var uncloneableComp) && uncloneableComp.Analyzable) // Frontier
274:            uncloneable = true; // Frontier
275:
276:        _uiSystem.ServerSendUiMessage(healthAnalyzer, HealthAnalyzerUiKey.Key, new HealthAnalyzerScannedUserMessage(
277:            GetNetEntity(target),
278:            bodyTemperature,
279:            bloodAmount,
280:            scanMode,
281:            bleeding,
282:            unrevivable,
283:            uncloneable, // Frontier
284:            // Shitmed Change
285:            body,
286:            part != null ? GetNetEntity(part) : null
287:        ));
```

WG builds a **flat 9-argument message constructor**; Onyx builds a **struct with an object-initializer-style
constructor with 10 optional-tail parameters**. There is no line-for-line hook: the two message shapes must
either be unified (replace WG's flat constructor with Onyx's `HealthAnalyzerUiState`, migrating all Shitmed
fields — `Body`, `Part` — into the state struct) or kept parallel (add a Wolfmed-only field to WG's existing
message and populate it only for `WoundHostComponent` targets). The parallel option is lower-risk and fits
`_WF/Wolfmed` glue better:

```csharp
// WOLFGATE: Wolfmed wound diagnostics, additive to the existing Shitmed payload.
public HealthAnalyzerWoundDiagnostics? WoundDiagnostics; // WOLFGATE
```
added to `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` as an 11th optional constructor
parameter, populated from a new `_WF/Wolfmed` builder method called from WG's `UpdateScannedUser` guarded by
`HasComp<WoundHostComponent>(target)`.

**Classification: later phase (4).** Blocked on `WoundSystem`, `PainSystem`, `BodyPartFunctionalitySystem`,
and a design decision on message-shape strategy (unify vs. append).

### 1c. `BuildPartDamage` / `BuildWoundDiagnostics` / `BuildOrganInfo` / `BuildChemicalInfo` (Onyx lines
315–517)

These are four wholesale new methods, not edits to existing ones — nothing to "hook", they'd be new
`_WF/Wolfmed` (or vendored `_Onyx`) methods once their dependencies exist. `BuildChemicalInfo` is the one
piece that's nearly hook-free of wound machinery (only needs `BloodstreamComponent`, `StomachComponent`,
`LungComponent`, all of which already exist in WG) — it could be ported standalone in an earlier phase than
the rest if the chemical-breakdown display is wanted before wounds land, but per the phase list it's grouped
with wound diagnostics in phase 4.

**Classification: later phase (4)**, `BuildChemicalInfo` optionally separable earlier.

---

## 2. `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`

Onyx replaced the flat message with a `HealthAnalyzerUiState` struct (`Onyx-HealthAnalyzer-StatusDoll`,
`Onyx-VitalDamage`, `Onyx-HealthAnalyzerOrgans-edited`, `Onyx-HealthAnalyzerChemicals`,
`Onyx-HealthAnalyzer-StatusDoll-edited`):

```csharp
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
// <Onyx-HealthAnalyzer-StatusDoll>
    public Dictionary<TargetBodyPart, DamageSpecifier>? PartDamage;
    public HealthAnalyzerWoundDiagnostics? WoundDiagnostics;
    // <Onyx-VitalDamage>
    public FixedPoint2? VitalDamage;
    // </Onyx-VitalDamage>
    // <Onyx-HealthAnalyzerOrgans-edited>
    public List<HealthAnalyzerOrganInfo>? Organs;
    // </Onyx-HealthAnalyzerOrgans-edited>
    public List<HealthAnalyzerChemicalInfo>? Chemicals; // <Onyx-HealthAnalyzerChemicals>
    // </Onyx-HealthAnalyzer-StatusDoll>
    ...
}
```

**WG counterpart in full** (`Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs:1–45`):

```csharp
1: using Content.Shared._Shitmed.Targeting; // Shitmed Change
...
10: public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
11: {
12:     public readonly NetEntity? TargetEntity;
13:     public float Temperature;
14:     public float BloodLevel;
15:     public bool? ScanMode;
16:     public bool? Bleeding;
17:     public Dictionary<TargetBodyPart, TargetIntegrity>? Body; // Shitmed Change
18:     public NetEntity? Part; // Shitmed Change
19:     public bool? Unrevivable;
20:     public bool? Uncloneable; // Frontier
21:
22:     public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, bool? uncloneable, Dictionary<TargetBodyPart, TargetIntegrity>? body, NetEntity? part = null) // Shitmed Change
23:     { ... }
24: }
```

WG kept the vanilla flat `sealed class` shape (no `HealthAnalyzerUiState`) and grew it with two Shitmed
fields plus one Frontier field. Onyx converted the whole message to wrap a struct. There is no shared anchor
line to hook — this is the same fork-point divergence as §1.

**Exact `// WOLFGATE` hook** (additive, parallel-field strategy, matching §1b's recommendation):

```csharp
19:     public bool? Unrevivable;
20:     public bool? Uncloneable; // Frontier
21:     public HealthAnalyzerWoundDiagnostics? WoundDiagnostics; // WOLFGATE: Wolfmed wound diagnostics, null for non-wound-hosts
22:
23:     public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, bool? uncloneable, Dictionary<TargetBodyPart, TargetIntegrity>? body, NetEntity? part = null, HealthAnalyzerWoundDiagnostics? woundDiagnostics = null) // WOLFGATE
24:     {
25:         ...
26:         WoundDiagnostics = woundDiagnostics; // WOLFGATE
27:     }
```

`HealthAnalyzerWoundDiagnostics` and its element type would be vendored from
`Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` unmodified (that file itself carries no Onyx
tags to check — confirm separately in a body/medical-data-types pass).

**Classification: later phase (4)**, same blockers as §1.

---

## 3. `Content.Server/Body/Systems/RespiratorSystem.cs`

Onyx marks 27 tag occurrences, but only two of the seven distinct tags are wound/organ related. The rest are
**unrelated Onyx feature ports** that happen to live in the same file and are out of Wolfmed's scope
entirely — this contradicts the handoff's blanket "relevant" classification for this file.

| Tag | What it does | Relevance |
|---|---|---|
| `Onyx-BreathingImmunity-edited` (line 92) | `HasComp<BreathingImmunityComponent>(uid)` added to the dead-check | **Already present in Wolfgate**, see below |
| `Onyx-GoobGrab` (95–98, 116–122) | Choking while grabbed (Goobstation `GrabbableComponent`/`GrabStage`) | Skip — unrelated feature, not wounds |
| `Onyx-MartialArts` (100–104, 108, 121) | KravMaga breath-blocking status | Skip — unrelated feature |
| `Onyx-OrganConsequences`/`-edited` (106–114, 247–250, 276–279) | `InitiallyLungedComponent` gates whether a body needs to breathe at all | Nubody anatomy glue — see below |
| `Onyx-MissingHead-edited` (139–142) | Suppress gasp emote when headless | Later phase (dismemberment consequences), low value |
| `Onyx-CosmicCult` (151–154) | CosmicCult faction members ignore suffocation unless incapacitated | Skip — unrelated faction feature |
| `Onyx-ShadowkinBreathing-edited` (117) | Renamed a call site, no logic of its own | Skip — cosmetic to an unrelated species feature |

### 3a. BreathingImmunity — already ported, no work needed

Onyx (line 92):
```csharp
if (_mobState.IsDead(uid) || HasComp<BreathingImmunityComponent>(uid)) // <Onyx-BreathingImmunity-edited>
    continue;
```

WG (`Content.Server/Body/Systems/RespiratorSystem.cs:79`, independently arrived at by Shitmed):
```csharp
79:            if (_mobState.IsDead(uid) || HasComp<BreathingImmunityComponent>(uid)) // Shitmed: BreathingImmunity
80:                continue;
```
`BreathingImmunityComponent` already exists at `Content.Shared/_Shitmed/Body/Components/
BreathingImmunityComponent.cs` and is wired exactly like Onyx's. **No hook needed — confirmed identical
behavior already shipped.**

### 3b. `InitiallyLungedComponent` / OrganConsequences — Nubody anatomy glue, do not port as-is

Onyx's checks (`!HasComp<InitiallyLungedComponent>(uid)`) gate on a component set once, at body init, by
`SharedBodySystem.cs:179–182`:
```csharp
if (anatomy.RequiredOrgans.ContainsKey(new ProtoId<OrganCategoryPrototype>("Lungs")))
    EnsureComp<InitiallyLungedComponent>(body);
```
`anatomy` here is a `BodyAnatomyComponent.RequiredOrgans` dictionary populated by Onyx's species-anatomy
initializer (`Content.Shared/_Onyx/Body/OrganConsequenceComponents.cs` + init system). This is exactly the
"`InitialBodySystem`... Nubody glue" the handoff explicitly excludes (D8, handoff §"Upstream files Onyx
hooks" skip list). Neither `InitiallyLungedComponent` nor `BodyAnatomyComponent` exist in WG, and porting
them means porting the anatomy-requirement system, not just these two lines.

**Classification: skip for phase 1; revisit in phase 3** (organ damage / functional organs) with a
Wolfgate-native check instead — e.g. "does this body still have a `LungComponent`-bearing organ with
`Health > 0`" via `_WF/Wolfmed`'s own functional-organ tracking, rather than porting the anatomy-requirement
component.

### 3c. Everything else in this file

`GoobGrab`, `MartialArts`, `CosmicCult`, `ShadowkinBreathing` are separate Onyx-fork feature ports (grabbing/
choking, martial arts, a cult faction, a species) that happen to touch `RespiratorSystem.cs` because Onyx's
repo carries all of its content mods in one tree. None of them are wound-system code and none should be
ported as part of Wolfmed. **Classification: skip, out of scope.**

---

## 4. Bloodstream — ONYX `Content.Shared/Body/Systems/BloodstreamSystem.cs` → WG `Content.Server/Body/
Systems/BloodstreamSystem.cs`

This file has the cleanest, most direct hooks of the batch, and is the concrete implementation of D2's
damage bridge for bleeding. Onyx marks 15 occurrences across four tags: `Onyx-WoundSystem-edited` (the real
wound-host bridge, appears 5 times), `Onyx-PartHealthExamine-edited`, `Onyx-ClothingDirt` (unrelated
feature), `Onyx-CirculatoryStreams` (unrelated feature, and the whole subscribing method doesn't exist in
WG).

**Shared+predicted vs. server-only:** confirmed — Onyx's file lives in `Content.Shared/Body/Systems/` and
is `sealed partial class BloodstreamSystem : EntitySystem` with no server/client split; WG's is
`Content.Server/Body/Systems/BloodstreamSystem.cs` only. Every hook below is written for WG's server-only
context — no prediction concerns, but also means Wolfmed's wound bleeding (`WoundBleedingSystem`, presumably
also shared+predicted in Onyx) will need its own decision about whether it stays shared/predicted while its
`BloodstreamSystem` host is server-only. If `WoundBleedingSystem` predicts bleed-rate changes on the client
and then calls into the (server-only) `BloodstreamSystem.TryModifyWoundBleedProjection`, that call must be
guarded to no-op or be skipped entirely on the client, or `WoundBleedingSystem` needs to be server-only too
(diverging further from Onyx). Recommend keeping `WoundBleedingSystem` server-only in Wolfgate to match its
`BloodstreamSystem`/`HealingSystem` — flagged as an open question for the phase-1 implementer, not resolved
here.

### 4a. `OnDamageChanged` — the core damage-bridge hook (D2)

Onyx (`Content.Shared/Body/Systems/BloodstreamSystem.cs:200–205`):
```csharp
    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<BloodstreamComponent> ent, ref DamageChangedEvent args)
    {
        // Onyx-WoundSystem-edited: wound sources project bleeding for migrated bodies.
        if (HasComp<WoundHostComponent>(ent))
            return;
```

WG insertion point (`Content.Server/Body/Systems/BloodstreamSystem.cs:210–216`):
```csharp
210:    private void OnDamageChanged(Entity<BloodstreamComponent> ent, ref DamageChangedEvent args)
211:    {
212:        if (args.DamageDelta is null || !args.DamageIncreased)
213:        {
214:            return;
215:        }
```

Exact hook:
```csharp
210:    private void OnDamageChanged(Entity<BloodstreamComponent> ent, ref DamageChangedEvent args)
211:    {
212:        // WOLFGATE: wound hosts get their bleeding from WoundBleedingSystem instead.
213:        if (HasComp<WoundHostComponent>(ent))
214:            return;
215:
216:        if (args.DamageDelta is null || !args.DamageIncreased)
217:        {
218:            return;
219:        }
```

**Classification: phase 1 core.** This is the exact bridge D2 describes for bleeding: once
`WoundHostComponent` exists, this file's classic bleed-on-damage math is switched off for wound hosts.

### 4b. `OnHealthBeingExamined` pale-message gate

Onyx (line 293):
```csharp
        if (!HasComp<WoundHostComponent>(ent) && GetBloodLevel(ent.AsNullable()) < ent.Comp.BloodlossThreshold) // <Onyx-PartHealthExamine-edited>
```

WG (`Content.Server/Body/Systems/BloodstreamSystem.cs:274–279`):
```csharp
274:        // If the mob's blood level is below the damage threshhold, the pale message is added.
275:        if (GetBloodLevelPercentage(ent, ent) < ent.Comp.BloodlossThreshold)
276:        {
277:            args.Message.PushNewline();
278:            args.Message.AddMarkupOrThrow(Loc.GetString("bloodstream-component-looks-pale", ("target", ent.Owner)));
279:        }
```

Exact hook:
```csharp
274:        // If the mob's blood level is below the damage threshhold, the pale message is added.
275:        if (!HasComp<WoundHostComponent>(ent) && GetBloodLevelPercentage(ent, ent) < ent.Comp.BloodlossThreshold) // WOLFGATE: Onyx's HealthExaminable system covers pallor for wound hosts
276:        {
```
Note the name swap: WG's `GetBloodLevelPercentage(EntityUid, BloodstreamComponent?)` replaces Onyx's
`GetBloodLevel(Entity<BloodstreamComponent?>)` — same semantics (fraction of max blood volume), different
parameter style; keep WG's own method, do not introduce Onyx's.

**Classification: phase 1 core** (small, low-risk, ships alongside 4a). It should land together with Onyx's
`_Onyx/HealthExaminable` port (per handoff inventory) so the "looks pale" message doesn't just silently
disappear for wound hosts before its replacement exists — sequence this after `HealthExaminable` is vendored,
or the message vanishes for organic wound-host mobs with no substitute.

### 4c. `TryModifyBleedAmount` — the write-gate that makes 4a's bridge airtight

Onyx (lines 556–590, `Onyx-WoundSystem-edited`):
```csharp
    public bool TryModifyBleedAmount(Entity<BloodstreamComponent?> ent, float amount)
    {
        return TryModifyBleedAmount(ent, amount, false);
    }

    internal bool TryModifyWoundBleedProjection(Entity<BloodstreamComponent?> ent, float amount)
    {
        return TryModifyBleedAmount(ent, amount, true);
    }

    private bool TryModifyBleedAmount(Entity<BloodstreamComponent?> ent, float amount, bool woundProjection)
    {
        if (!Resolve(ent, ref ent.Comp, logMissing: false))
            return false;

        if (HasComp<WoundHostComponent>(ent) && !woundProjection)
            return false;
        ...
```

WG (`Content.Server/Body/Systems/BloodstreamSystem.cs:403–423`):
```csharp
403:    /// <summary>
404:    ///     Tries to make an entity bleed more or less
405:    /// </summary>
406:    public bool TryModifyBleedAmount(EntityUid uid, float amount, BloodstreamComponent? component = null)
407:    {
408:        if (!Resolve(uid, ref component, logMissing: false))
409:            return false;
410:
411:        component.BleedAmount += amount;
```

Exact hook (adapted to WG's `EntityUid`+optional-component signature rather than Onyx's `Entity<T?>` tuple,
and split into a public no-op-for-wound-hosts entry point plus an internal projection entry point so
`WoundBleedingSystem` is the only writer once ported):

```csharp
403:    /// <summary>
404:    ///     Tries to make an entity bleed more or less
405:    /// </summary>
406:    public bool TryModifyBleedAmount(EntityUid uid, float amount, BloodstreamComponent? component = null)
407:    {
408:        return TryModifyBleedAmount(uid, amount, component, woundProjection: false); // WOLFGATE
409:    }
410:
411:    // WOLFGATE: sole write path for WoundBleedingSystem once wounds own bleeding for wound hosts.
412:    internal bool TryModifyWoundBleedProjection(EntityUid uid, float amount, BloodstreamComponent? component = null)
413:    {
414:        return TryModifyBleedAmount(uid, amount, component, woundProjection: true); // WOLFGATE
415:    }
416:
417:    private bool TryModifyBleedAmount(EntityUid uid, float amount, BloodstreamComponent? component, bool woundProjection) // WOLFGATE
418:    {
419:        if (!Resolve(uid, ref component, logMissing: false))
420:            return false;
421:
422:        if (HasComp<WoundHostComponent>(uid) && !woundProjection) // WOLFGATE
423:            return false;
424:
425:        component.BleedAmount += amount;
```
(remaining body unchanged, renumbered)

**Important side effect, not a separate hook:** WG's `Update()` loop already calls
`TryModifyBleedAmount(uid, -bloodstream.BleedReductionAmount, bloodstream);` at line 137 inside `if
(bloodstream.BleedAmount > 0)`. Once 4c's gate is in place, that call becomes a silent no-op for wound hosts
automatically — this is exactly Onyx's own design (their equivalent line, `Onyx-WoundSystem-edited` at line
517–519 of their file, needs no separate edit because it calls the now-gated public overload). **Do not
duplicate the guard at the call site** — one gate in `TryModifyBleedAmount` covers both.

**Classification: phase 1 core.** This is the linchpin hook; without it, 4a's bridge is incomplete because
non-wound code paths (healing items, stasis beds, anything else calling the public
`TryModifyBleedAmount`) could still mutate `BleedAmount` out from under `WoundBleedingSystem`.

### 4d. Out of scope in this file

- **`Onyx-ClothingDirt`** (lines 24–27, 48–50, 503–512): a separate Onyx feature (blood dirties worn
  clothing) requiring `ClothingDirtSystem`, which doesn't exist in WG (`Content.Shared._Onyx.Clothing`
  never checked out, confirmed absent from Wolfgate). **Skip — unrelated feature**, not part of Wolfmed.
- **`Onyx-CirculatoryStreams`** (lines 24, 329–332): guards a method, `OnMetabolismExclusion`, that **does
  not exist in WG at all** — `MetabolismExclusionEvent` is entirely absent from Wolfgate. This is Onyx's own
  new multi-circulatory-stream feature (relevant to IPC coolant, phase 5 species profiles per the handoff),
  not an edit to an existing WG hook. **Skip for phase 1.**

---

## 5. Healing — ONYX `Content.Shared/Medical/Healing/HealingSystem.cs` → WG `Content.Server/Medical/
HealingSystem.cs`

**Shared+predicted vs. server-only:** confirmed — Onyx's file is `Content.Shared/Medical/Healing/
HealingSystem.cs`, `sealed partial class HealingSystem : EntitySystem` (shared, predicted: it calls
`_audio.PlayPredicted`, `PredictedQueueDel`, and reads `RequestedPart` off a networked `NetEntity?`
explicitly because "explicit part survives prediction and is revalidated after the delay", per the
`HealingDoAfterEvent.cs` comment). WG's is `Content.Server/Medical/HealingSystem.cs`, server-only
(`_audio.PlayPvs`, `QueueDel`). This means every `_bodySystem`/`_woundHealing` call Onyx makes assuming
client-side prediction (e.g. re-resolving `RequestedPart` after `BreakOnMove`) has no client echo in
Wolfgate at all — the do-after resolves once, server-side, with no risk of a mispredicted part, but also no
client-side instant feedback if the part becomes invalid mid-delay (WG already lacks this feedback for its
existing Shitmed part-targeting, so this is a pre-existing gap, not a new one Wolfmed introduces).

Onyx marks 14 occurrences across three tags: `Onyx-WoundTreatment` (the real bridge, 3 places),
`Onyx-TargetedHealingFeedback` (1), `Onyx-WoundSystem-edited` (naming/dependency only).

### 5a. `OnDoAfter` wound-host branch (Onyx lines 59–94)

```csharp
        // <Onyx-WoundTreatment>
        // Onyx-WoundSystem-edited: localized healing belongs to the selected part, never the body projection.
        if (HasComp<WoundHostComponent>(target))
        {
            EntityUid? requestedPart = args.RequestedPart is { } netPart ? GetEntity(netPart) : null;
            if (requestedPart is { } concretePart && _woundHealing.ResolveHealingPart(target, concretePart,
                    healing.Damage, healing.DamageContainers, healing.TreatmentCapabilities,
                    healing.AllowedWoundStages, healing.BloodlossModifier, healing.HealWounds) != concretePart)
            {
                var message = _bodySystem.BodyHasChild(target, concretePart)
                    ? "targeting-selected-part-incompatible"
                    : "targeting-selected-part-missing";
                _popupSystem.PopupClient(Loc.GetString(message), target, args.User);
                return;
            }

            if (!_woundHealing.TryApplyHealing(target, requestedPart, (args.Used.Value, healing), args.User,
                    out var woundHealed, out var stoppedBleeding))
                return;
            ...
            FinishHealing(target, ref args, healing, woundHealed);
            return;
        }
        // </Onyx-WoundTreatment>
```

WG insertion point (`Content.Server/Medical/HealingSystem.cs:56–71`, its `OnDoAfter`):
```csharp
56:    private void OnDoAfter(Entity<DamageableComponent> entity, ref HealingDoAfterEvent args)
57:    {
58:        var dontRepeat = false;
59:
60:        if (!TryComp(args.Used, out HealingComponent? healing))
61:            return;
62:
63:        if (args.Handled || args.Cancelled)
64:            return;
65:
66:        if (healing.DamageContainers is not null &&
67:            entity.Comp.DamageContainerID is not null &&
68:            !healing.DamageContainers.Contains(entity.Comp.DamageContainerID))
69:        {
70:            return;
71:        }
```

Exact hook (calling into a `_WF/Wolfmed` adapter rather than Onyx's `WoundHealingSystem` directly, since
Onyx's method names/signatures (`ResolveHealingPart`, `TryApplyHealing`) don't exist and their exact shape
should be decided when the wound core itself is ported, not guessed here):

```csharp
56:    private void OnDoAfter(Entity<DamageableComponent> entity, ref HealingDoAfterEvent args)
57:    {
58:        var dontRepeat = false;
59:
60:        if (!TryComp(args.Used, out HealingComponent? healing))
61:            return;
62:
63:        if (args.Handled || args.Cancelled)
64:            return;
65:
66:        // WOLFGATE: wound hosts heal through WoundHealingSystem, never the flat DamageableComponent path.
67:        if (HasComp<WoundHostComponent>(entity))
68:        {
69:            _woundHealing.HandleHealingDoAfter(entity, ref args, healing); // WOLFGATE: see _WF/Wolfmed adapter
70:            return;
71:        }
72:
73:        if (healing.DamageContainers is not null &&
```

**Classification: phase 1 core** (this is the "plus the Shitmed damage bridge" item in the handoff's phase-1
list), but genuinely **blocked** on `WoundHealingSystem` existing first — there is nothing to call yet, and
guessing its method signatures now would just have to be redone.

### 5b. `HasDamage` wound-host branch (Onyx lines 186–224)

Uses a new event, `ResolveHealingPartEvent` (raised, not just checked), and reads
`host.LocalizedDamageTypes` off `WoundHostComponent`. WG's `HasDamage` (`Content.Server/Medical/
HealingSystem.cs:134–147`) takes a bare `DamageableComponent` + `HealingComponent`, no targeting awareness
at all — Shitmed's own part-check lives in a **separate** method, `IsPartDamaged` (WG lines 150–162), called
alongside `HasDamage` rather than inside it. Porting this hook means either folding wound-awareness into
`HasDamage` (matching Onyx) or adding a third parallel check next to `IsPartDamaged` (matching WG's existing
pattern of composing independent boolean checks). The latter is lower-risk:

```csharp
128:        args.Repeat = HasDamage(entity.Comp, healing) && !dontRepeat || IsPartDamaged(args.User, entity) || IsWoundDamaged(entity, healing); // WOLFGATE
```
with a new `_WF/Wolfmed` `IsWoundDamaged` following WG's existing `IsPartDamaged` shape rather than Onyx's
`ResolveHealingPartEvent`.

**Classification: phase 1 core, blocked** on `WoundHealingSystem`/wound components.

### 5c. `TryHealTargeted` — `Onyx-TargetedHealingFeedback` (lines 286–308)

```csharp
    private bool TryHealTargeted(Entity<HealingComponent> healing, EntityUid target, EntityUid user)
    {
        if (!HasComp<WoundHostComponent>(target) || !TryComp(user, out TargetingComponent? targeting))
            return false;
        ...
        // <Onyx-TargetedHealingFeedback>
        if (!_woundHealing.IsCompatiblePart(target, part, healing.Comp.DamageContainers,
                healing.Comp.TreatmentCapabilities))
        {
            _popupSystem.PopupClient(Loc.GetString("targeting-selected-part-incompatible"), target, user);
            return true;
        }
        // </Onyx-TargetedHealingFeedback>
```

This whole method doesn't exist in WG — WG's `TryHeal` has no separate "targeted" entry point; targeting
feedback is folded into `IsPartDamaged`. **No direct hook exists to insert into; this is new code**, not an
edit. Note also the `TargetingComponent` name collision from the top-level verdict: this line
(`TryComp(user, out TargetingComponent? targeting)`) resolves to **Wolfgate's own Shitmed `TargetingComponent`**
once both are compiled into the same assembly (same namespace-qualified type WG already uses at
`Content.Server/Medical/HealingSystem.cs:38`, `_targetingSystem`), NOT Onyx's — confirming the rename in the
top-level verdict is mandatory, not optional, before this kind of code can even be written correctly.

**Classification: later phase** (bundled with wound treatment UX, not core bleeding/healing bridge).

### 5d. `TryHeal` public entry point — `Onyx-WoundTreatment` (lines 319–333)

Same wound-host branch pattern as 5a, in the public non-do-after entry point. WG's `TryHeal`
(`Content.Server/Medical/HealingSystem.cs:184–242`) has a materially different signature
(`EntityUid uid, EntityUid user, EntityUid target, HealingComponent component`) vs. Onyx's
(`Entity<HealingComponent> healing, Entity<DamageableComponent?> target, EntityUid user, EntityUid?
requestedPart = null`). Same blocker as 5a/5b.

**Classification: phase 1 core, blocked** on `WoundHealingSystem`.

---

## 6. `Content.Shared/Medical/HealingDoAfterEvent.cs`

Found at the exact path given (present in both trees, not renamed). Only one Onyx-marked line:

Onyx:
```csharp
[Serializable, NetSerializable]
public sealed partial class HealingDoAfterEvent : SimpleDoAfterEvent
{
    // Onyx-WoundSystem-edited: explicit part survives prediction and is revalidated after the delay.
    public readonly NetEntity? RequestedPart;

    public HealingDoAfterEvent(NetEntity? requestedPart = null)
    {
        RequestedPart = requestedPart;
    }
}
```

WG in full (`Content.Shared/Medical/HealingDoAfterEvent.cs:1–9`):
```csharp
1: using Content.Shared.DoAfter;
2: using Robust.Shared.Serialization;
3:
4: namespace Content.Shared.Medical;
5:
6: [Serializable, NetSerializable]
7: public sealed partial class HealingDoAfterEvent : SimpleDoAfterEvent
8: {
9: }
```

Exact hook:
```csharp
6: [Serializable, NetSerializable]
7: public sealed partial class HealingDoAfterEvent : SimpleDoAfterEvent
8: {
9:     public readonly NetEntity? RequestedPart; // WOLFGATE: explicit part survives prediction, revalidated after the delay
10:
11:    public HealingDoAfterEvent(NetEntity? requestedPart = null) // WOLFGATE
12:    {
13:        RequestedPart = requestedPart; // WOLFGATE
14:    }
15: }
```

**Classification: phase 1 core.** Trivial, no blockers — this can be added the moment §5's `OnDoAfter`/
`TryHeal` changes are made, since it's just a data-carrier field. Safe to land early even before
`WoundHealingSystem` exists (it'll just be unread until then).

---

## 7. `Content.Shared/Armor/SharedArmorSystem.cs`

Onyx marks 13 occurrences across `Onyx-WoundSystem` (armor-vs-wound-host routing, 4 blocks) and
`Onyx-ArmorGlobalProtection-edited` (1, a bug-fix note, not wound-specific). Full Onyx file already quoted
in-context above; WG's full file already quoted above. No shared/predicted split applies — both are
`Content.Shared`, both fully shared+predicted already.

### 7a. `OnDamageModify` wound-host branch (Onyx lines 52–68)

```csharp
    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        // <Onyx-WoundSystem-edited>
        // Wound hosts apply equipped armor to systemic (whole body) damage here;
        // localized part damage is armored after the struck body part is resolved via PartDamageModifyEvent.
        if (TryComp(args.Owner, out WoundHostComponent? host))
        {
            args.Args.Damage = ApplyWoundSystemicArmor(args.Args.Damage, component.Modifiers, host);
            return;
        }
        // </Onyx-WoundSystem-edited>

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }
```

WG in full (`Content.Shared/Armor/SharedArmorSystem.cs:42–46`, no `MaskComponent` check exists in WG at all
— that's Onyx-base-game drift, unrelated to wounds):
```csharp
42:    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
43:    {
44:        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
45:            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
46:    }
```

Exact hook — **must route through WG's existing `PenetrateArmor`, not bypass it**, or Wolfgate's own
armor-penetration weapons (anything setting `ProjectileComponent.ArmorPenetration`,
`HitscanBasicDamageComponent`, melee weapons) silently stop working against wound-host mobs:

```csharp
42:    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
43:    {
44:        // WOLFGATE: wound hosts armor systemic damage here; localized part damage is armored
45:        // after the struck part is resolved (see PartDamageModifyEvent, phase 3+).
46:        if (TryComp(args.Owner, out WoundHostComponent? host))
47:        {
48:            args.Args.Damage = ApplyWoundSystemicArmor(args.Args.Damage,
49:                DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration), host); // WOLFGATE
50:            return;
51:        }
52:
53:        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
54:            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration)); // Goob edit
55:    }
56:
57:    // WOLFGATE: ported from Onyx SharedArmorSystem.ApplyWoundSystemicArmor, adapted to WG's PenetrateArmor-based AP.
58:    private static DamageSpecifier ApplyWoundSystemicArmor(
59:        DamageSpecifier damage,
60:        DamageModifierSet modifiers,
61:        WoundHostComponent host)
62:    {
63:        var systemic = new DamageSpecifier(damage);
64:        var hasSystemic = false;
65:        foreach (var (type, value) in damage.DamageDict)
66:        {
67:            if (host.LocalizedDamageTypes.Contains(type))
68:                systemic.DamageDict.Remove(type);
69:            else if (value != 0)
70:                hasSystemic = true;
71:        }
72:
73:        if (!hasSystemic)
74:            return damage;
75:
76:        var reduced = DamageSpecifier.ApplyModifierSet(systemic, modifiers);
77:
78:        var result = damage.Clone();
79:        foreach (var (type, _) in systemic.DamageDict)
80:        {
81:            if (reduced.DamageDict.TryGetValue(type, out var value))
82:                result.DamageDict[type] = value;
83:            else
84:                result.DamageDict.Remove(type);
85:        }
86:
87:        return result;
88:    }
```
Note: `modifiers` is now already-penetration-adjusted (`DamageModifierSet`, not raw `component.Modifiers`)
because the penetration was applied one level up, matching how the non-wound-host branch already calls
`PenetrateArmor` before `ApplyModifierSet`.

**Classification: phase 1 core** (armor must not double- or zero-apply to wound hosts from day one of the
damage bridge), **blocked only on `WoundHostComponent` existing** — this hook is otherwise ready to write
verbatim-adapted today.

### 7b. `OnPartDamageModify` / `PartDamageModifyEvent` (Onyx lines 108–130)

```csharp
    // <Onyx-WoundSystem>
    private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
    {
        ...
        // <Onyx-ArmorGlobalProtection-edited>
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
        // </Onyx-ArmorGlobalProtection-edited>
    }
    // </Onyx-WoundSystem>
```

`PartDamageModifyEvent` does not exist anywhere in Wolfgate (confirmed by repo-wide grep). This is a
wholesale new event + subscription (`SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<
PartDamageModifyEvent>>(OnPartDamageModify);`, Onyx line 30), needed once localized per-part armor
(`ArmorComponent.PartModifiers`, a field that also doesn't exist in WG's `ArmorComponent`) is wanted for
wound hosts.

**Classification: later phase (3)**, alongside amputation/organ-damage work — this is what §7a's comment
defers to ("localized part damage is armored after the struck body part is resolved").

---

## 8. `Content.Shared/Damage/Systems/DamageableSystem*.cs`

Grepped all four files present in Wolfgate's sparse set — `DamageableSystem.cs`, `.API.cs`, `.Events.cs`,
`.BenchmarkHelpers.cs` — plus the `_Onyx`-namespaced `DamageableSystem.API.cs` partial. **Zero `onyx`
matches in any of them.** Onyx does not hook `DamageableSystem` at all for wounds; it routes entirely through
events raised inside it.

The relevant integration point is `BeforeDamageChangedEvent`, which **already exists in Wolfgate**, added by
Shitmed (not Onyx):

```csharp
// Content.Shared/Damage/Systems/DamageableSystem.cs:210-212
            var before = new BeforeDamageChangedEvent(damage, origin, targetPart, //Shitmed Change
                false, originFlag); // Mono: originFlag
            RaiseLocalEvent(uid.Value, ref before);

            if (before.Cancelled)
                return null;
```
```csharp
// Content.Shared/Damage/Systems/DamageableSystem.cs:469-475
    [ByRefEvent]
    public record struct BeforeDamageChangedEvent(
        DamageSpecifier Damage,
        EntityUid? Origin = null,
        TargetBodyPart? TargetPart = null, // Shitmed Change
        bool Cancelled = false,
        DamageOriginFlag? OriginFlag = null); // Mono: OriginFlag
```

`RaiseLocalEvent(uid.Value, ref before)` is a **broadcast** raise (not directed at `DamageableComponent`
specifically), so a new `SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(...)` subscription
does **not** collide with the existing `DuplicateDirectedSubscription` trap — that trap only fires when two
systems subscribe the *same component type* to the *same event type*. Confirmed no other system currently
subscribes `WoundHostComponent` to anything (it doesn't exist yet), so this is safe.

This directly validates **D2 as written**: Onyx's `WoundDamageRoutingSystem` pattern —
`SubscribeLocalEvent<WoundHostComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged)`, then
`args.Cancelled = true` inside the handler — will work against Wolfgate's `TryChangeDamage` exactly as it
does against Onyx's, with **zero edits needed inside `DamageableSystem.cs` itself**. No `// WOLFGATE` hook
is required in this file for the damage-bridge to function.

One related, unmarked fact worth flagging: `WoundDamageRoutingSystem` (not one of this batch's nine files,
but load-bearing for D2) also subscribes `SubscribeLocalEvent<WoundHostComponent, DamageDealtEvent>`. That
event type (`Content.Shared/Damage/Systems/DamageableSystem.Events.cs:293` in Onyx,
`public readonly record struct DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool
InterruptsDoAfters)`) also **does not exist in Wolfgate** and is not raised anywhere in WG's
`DamageableSystem.cs`. Porting `WoundDamageRoutingSystem` verbatim will require adding this event (and its
raise site inside `TryChangeDamage`/`DamageChanged`) as a small upstream addition, separate from
`BeforeDamageChangedEvent` which already exists. Flagging this now so it isn't discovered mid-port of a
different file in a later batch.

**Classification: phase 1 core** (this is the mechanism, not optional), **no hook needed in this file** —
the extension point already exists.

---

## 9. `Content.Shared/Damage/DamageSpecifier*.cs` (ArmorPenetration partial)

### 9a. What Onyx adds

`Content.Shared/_Onyx/Damage/DamageSpecifier.ArmorPenetration.cs` (full file, carries no Onyx tags itself —
it's a wholesale new partial, not an edit):
```csharp
namespace Content.Shared.Damage;

public sealed partial class DamageSpecifier
{
    [DataField]
    public float ArmorPenetration { get; set; }
}
```

...and `Content.Shared/Damage/DamageSpecifier.cs` gets 5 marked edits so this field survives copies and
feeds `ApplyModifierSet`:
```csharp
90:            ArmorPenetration = damageSpec.ArmorPenetration; // <Onyx-ArmorPenetration>
...
132:            var penetration = float.IsFinite(damageSpec.ArmorPenetration)
133:                ? Math.Clamp(damageSpec.ArmorPenetration, 0f, 1f)
134:                : 0f;
135:            DamageSpecifier newDamage = new(damageSpec); // <Onyx-ArmorPenetration-edited>
136:            newDamage.DamageDict.Clear(); // <Onyx-ArmorPenetration>
...
153:                    newValue = Math.Max(0f, newValue - reduction * (1f - penetration)); // <Onyx-ArmorPenetration-edited>
...
158:                        : coefficient; // <Onyx-ArmorPenetration-edited>
```
(plus `Equals` comparing `ArmorPenetration != other.ArmorPenetration`, unmarked but adjacent).

This makes armor penetration **travel with the `DamageSpecifier` itself** — set once by a weapon, carried
through the whole damage pipeline, consumed automatically by `ApplyModifierSet` wherever it's called.

### 9b. What Wolfgate already has instead

Confirmed via repo-wide grep — **Wolfgate already has a complete, independently-built armor-penetration
system**, "Goobstation partial AP":

- `Content.Shared/Damage/DamageSpecifier.cs:306-345` — `DamageSpecifier.PenetrateArmor(DamageModifierSet
  modifierSet, float penetration)`: takes a raw penetration float and an armor's `DamageModifierSet`,
  returns a **new, weakened** `DamageModifierSet` (coefficients pushed toward 1, flat reductions scaled
  down), respecting `DamageModifierSet.IgnoreArmorPierceFlags` (`Content.Shared/Damage/
  DamageModifierSet.cs:26-31`, a `[Flags]` enum letting a specific armor set opt out of being penetrated).
- The penetration value itself lives as a **per-event field**, not on `DamageSpecifier`:
  `DamageModifyEvent.ArmorPenetration` (read at `SharedArmorSystem.cs:45,51` and
  `DamageableSystem.cs:234`), or as a plain `float` parameter threaded through `TryChangeDamage(...,
  float armorPenetration = 0f, ...)` (`DamageableSystem.cs:192,234`).
- Five call sites already wired: `SharedArmorSystem.cs:45,51`, `BlockingSystem.User.cs:71`,
  `SharedCursedMaskSystem.cs:59`, `Damage/Systems/DamageableSystem.cs:234`, `Damage/Systems/
  DamageProtectionBuffSystem.cs:18`. Weapon-side sources: `ProjectileComponent.ArmorPenetration`,
  `HitscanBasicDamageComponent`, `MeleeWeaponComponent`.

**These are two different architectures solving the same problem**, not a missing API that can just be
added. If Onyx's `DamageSpecifier.ArmorPenetration` field is vendored verbatim as D5 suggests, Wolfgate ends
up with **two independent, non-interacting armor-penetration values** on the same damage pipeline: a weapon
setting Wolfgate's `ProjectileComponent.ArmorPenetration` would be read by `PenetrateArmor` at the armor
layer, while `DamageSpecifier.ArmorPenetration` (Onyx's field) would sit unset and unread by any of
Wolfgate's own call sites, doing nothing — except inside whatever Onyx wound code reads `damage.
ArmorPenetration` directly (`WoundDamageRoutingSystem`/wound severity calculations reference it — outside
this batch's file list, flagging for whoever ports that file next).

**Recommendation for the phase-1 implementer** (decision, not yet made — flagging for the user per
"Confirm existing features first"): either (a) keep both systems and have Wolfmed's wound severity/armor
math read Wolfgate's existing per-call `armorPenetration` float instead of `DamageSpecifier.ArmorPenetration`
wherever Onyx's wound files reference the latter (more `// WOLFGATE` edits scattered through vendored wound
files, but zero risk to existing weapons), or (b) still add the `ArmorPenetration` field/partial verbatim
(harmless dead weight if genuinely unread by Wolfgate code) purely so vendored Onyx wound files compile
unmodified, and separately teach `TryChangeDamage` to *populate* `damage.ArmorPenetration` from its existing
`armorPenetration` parameter right before wound routing consumes it — bridging the two systems instead of
replacing either. (b) preserves "verbatim" for the wound files at the cost of one small `// WOLFGATE` bridge
line in `TryChangeDamage`; (a) preserves Wolfgate's existing system untouched at the cost of touching more
wound files during vendoring.

**Classification: phase 1 core decision required** (this blocks any wound file that reads
`DamageSpecifier.ArmorPenetration`), **not a simple hook** — needs the user's call on (a) vs (b) before
vendoring `_Onyx/Damage/DamageSpecifier.ArmorPenetration.cs` or `_Onyx/Wounds` files that depend on it.

---

## HealthAnalyzer UI "same generation" check

Confirmed via `git -C C:/tmp/onyx show HEAD:<path>` (the non-`_Onyx` `Content.Client/HealthAnalyzer` path is
**absent from the sparse checkout** — the sparse set only pulled `Content.Client/_Onyx/...`; read via `git
show` per the task's fallback instruction, not invented).

- **Base BUI class is the same generation**: `HealthAnalyzerBoundUserInterface` in both trees is the same
  vanilla shape (`CreateWindow<HealthAnalyzerWindow>()`, `ReceiveMessage` casts to
  `HealthAnalyzerScannedUserMessage`, calls `_window.Populate(cast)`). Onyx's copy has **no changes at all**
  to this class (confirmed no Onyx markers in it). Wolfgate's copy adds Shitmed's part-selection wiring
  (`OnBodyPartSelected` event, `SendBodyPartMessage`) on top of the same unmodified base — so the BUI
  entry-point pattern itself is compatible.
- **The window/control layer has diverged completely**, matching the message-shape divergence in §1/§2:
  - Onyx's `HealthAnalyzerWindow.xaml.cs` (`git show HEAD:Content.Client/HealthAnalyzer/UI/
    HealthAnalyzerWindow.xaml.cs`) is a thin 24-line shell: three tab buttons (Body/Organs/Chemicals)
    wired in the constructor, and `Populate` just forwards `msg.State` to a separate
    `HealthAnalyzerControl` (`Content.Client/_Onyx/...` — the actual rendering logic lives there, tabbed).
  - Wolfgate's `HealthAnalyzerWindow.xaml.cs` is a 367-line monolith: builds an 11-entry
    `Dictionary<TargetBodyPart, TextureButton>` limb doll wired to Shitmed's `OnBodyPartSelected`, renders
    damage-group/type lists directly (`DrawDiagnosticGroups`), and paints the doll via
    `SetupIcon(Dictionary<TargetBodyPart, TargetIntegrity>?)` — layered sprites from
    `/Textures/_Shitmed/Interface/Targeting/Status/*.rsi`, keyed by `TargetIntegrity` enum value.

**Verdict:** the BUI *pattern* (open/populate/dispose) is the same generation and porting-compatible; the
*content* (window internals, control tree, message payload) is not — Wolfgate's client already committed to
a single flat window with a texture-based limb doll, Onyx committed to a tabbed control with three
independent panels. Landing Onyx's wound diagnostics client-side (phase 4) means either: (i) adding a fourth
tab/panel to Wolfgate's existing flat window that reads the `_WF/Wolfmed` additive message field from §2 and
renders wound text next to the existing limb doll (lower risk, keeps Shitmed's doll as the primary visual),
or (ii) porting Onyx's tabbed `HealthAnalyzerControl` wholesale and rebuilding Shitmed's part-selection UX
inside one of its tabs (higher risk, touches code that currently works). Recommend (i) for phase 4; not
implemented or further speced here since it's downstream of every blocker above.

---

## Summary table

| File | Phase-1 hooks ready today | Blocked hooks | Skip (out of scope) |
|---|---|---|---|
| HealthAnalyzerSystem.cs | none | all (needs WoundSystem/PainSystem/BodyPartFunctionalitySystem + message-shape decision) | — |
| HealthAnalyzerScannedUserMessage.cs | none | additive `WoundDiagnostics` field (needs wound data types) | — |
| RespiratorSystem.cs | none (BreathingImmunity already shipped) | InitiallyLunged/OrganConsequences → phase 3, own design | GoobGrab, MartialArts, CosmicCult, ShadowkinBreathing |
| BloodstreamSystem.cs | `OnDamageChanged` gate, `OnHealthBeingExamined` pale gate, `TryModifyBleedAmount`/`TryModifyWoundBleedProjection` split | — | ClothingDirt, CirculatoryStreams |
| HealingSystem.cs | none (needs WoundHealingSystem) | `OnDoAfter`, `HasDamage`, `TryHeal` wound branches | `TryHealTargeted` (later phase, UX) |
| HealingDoAfterEvent.cs | `RequestedPart` field — **land now** | — | — |
| SharedArmorSystem.cs | `OnDamageModify` wound branch + `ApplyWoundSystemicArmor` (blocked only on `WoundHostComponent`) | — | `OnPartDamageModify`/`PartDamageModifyEvent` → phase 3 |
| DamageableSystem*.cs | nothing to add — `BeforeDamageChangedEvent` already exists | `DamageDealtEvent` needs adding for `WoundDamageRoutingSystem` (separate file, flagged) | — |
| DamageSpecifier*.cs (ArmorPenetration) | — | user decision needed: bridge vs. parallel armor-pen systems | — |

## Confirmed pre-existing collisions/duplicates (new findings this batch)

1. **`TargetingComponent` name collision** (hard crash, `ComponentFactory.cs:122`): Onyx's
   `_Onyx/Targeting/TargetingComponent.cs` vs. Wolfgate's existing `_Shitmed/Targeting/TargetingComponent.cs`
   both register as `"Targeting"`. Must rename Onyx's before first compile.
2. **`BreathingImmunityComponent`**: already ported by Shitmed, byte-for-byte equivalent to Onyx's hook.
   Skip — do not re-add.
3. **`BeforeDamageChangedEvent`**: already exists (Shitmed-added), directly usable by
   `WoundDamageRoutingSystem` with zero edits to `DamageableSystem.cs`.
4. **Armor penetration**: Wolfgate already has a full, independent system (`PenetrateArmor` +
   `IgnoreArmorPierceFlags`) that does not talk to Onyx's `DamageSpecifier.ArmorPenetration` field design.
