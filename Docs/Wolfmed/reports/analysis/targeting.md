# Targeting: Onyx → Wolfgate mapping

Onyx pinned commit `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Wolfgate worktree
`C:\Users\jzo12\Documents\GitHub\Wolfgate\.claude\worktrees\rules-motd-updates-11c89c`
(branch `clanker/wolfmed-port-orchestration-454c3d`). All paths below were read directly;
none were guessed. Everything the sparse checkout doesn't have is called out explicitly.

## 0. File inventory (what actually exists)

| Onyx | Wolfgate (Shitmed) |
|---|---|
| `Content.Shared/_Onyx/Targeting/TargetBodyPart.cs` | `Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs` |
| `Content.Shared/_Onyx/Targeting/TargetingComponent.cs` | `Content.Shared/_Shitmed/Targeting/TargetingComponent.cs` |
| `Content.Shared/_Onyx/Targeting/SharedTargetingSystem.cs` | `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` |
| `Content.Shared/_Onyx/Targeting/TargetingEvents.cs` | `Content.Shared/_Shitmed/Targeting/Events.cs` |
| `Content.Shared/_Onyx/Targeting/TargetResolverSystem.cs` | *(no equivalent type; logic lives on `SharedBodySystem`, see §4)* |
| `Content.Shared/_Onyx/Targeting/DamageDistribution.cs` | *(none)* |
| `Content.Shared/_Onyx/Targeting/TargetingSnapshotComponent.cs` + `...SnapshotSystem.cs` | *(none)* |
| `Content.Shared/_Onyx/Targeting/PartStatusComponent.cs` + `PartStatusSystem.cs` | `Content.Shared/_Shitmed/Targeting/TargetIntegrity.cs` (different shape, see §5) |
| `Content.Server/_Onyx/Targeting/TargetingSystem.cs` | `Content.Server/_Shitmed/Targeting/TargetingSystem.cs` |
| `Content.Server/_Onyx/Targeting/PartStatusSystem.cs` | *(none — folded into `SharedBodySystem.Targeting.cs`, see §5)* |
| `Content.Client/_Onyx/Targeting/TargetingSystem.cs` + `UI/*` | `Content.Client/_Shitmed/Targeting/TargetingSystem.cs` + `Content.Client/_Shitmed/UserInterface/Systems/{Targeting,PartStatus}/*` |

Wolfgate has no `Content.Shared/Targeting` (vanilla, un-prefixed) at all — only `_Shitmed`.
Onyx's own repo tree has no `Content.Shared/Targeting` either (`git ls-tree` confirms), and
no `Content.Shared/Body/Part/BodyPartType.cs` — Onyx replaced that vanilla file wholesale
with `Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs`, which is filed under `_Onyx` but
declares `namespace Content.Shared.Body.Part` (the vanilla namespace). This matters for §3.

## 1. Enum diff: `TargetBodyPart`

**Onyx** — `Content.Shared/_Onyx/Targeting/TargetBodyPart.cs:6-30`
```csharp
[Flags, Serializable, NetSerializable]
public enum TargetBodyPart : ushort
{
    Head = 1, Chest = 1 << 1, Groin = 1 << 2,
    LeftArm = 1 << 3, LeftHand = 1 << 4, RightArm = 1 << 5, RightHand = 1 << 6,
    LeftLeg = 1 << 7, LeftFoot = 1 << 8, RightLeg = 1 << 9, RightFoot = 1 << 10,

    Hands = LeftHand | RightHand, Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg, Feet = LeftFoot | RightFoot,
    FullArms = Arms | Hands, FullLegs = Legs | Feet,
    BodyMiddle = Chest | Groin | FullArms, FullLegsGroin = FullLegs | Groin,
    Vital = Head | Chest | Groin,
    All = Head | Chest | Groin | FullArms | FullLegs,
}
```

**Wolfgate/Shitmed** — `Content.Shared/_Shitmed/Targeting/TargetBodyPart.cs:12-31`
```csharp
[Flags]
public enum TargetBodyPart : ushort
{
    Head = 1, Torso = 1 << 1, Groin = 1 << 2,
    LeftArm = 1 << 3, LeftHand = 1 << 4, RightArm = 1 << 5, RightHand = 1 << 6,
    LeftLeg = 1 << 7, LeftFoot = 1 << 8, RightLeg = 1 << 9, RightFoot = 1 << 10,

    Hands = LeftHand | RightHand, Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg, Feet = LeftFoot | RightFoot,
    All = Head | Torso | Groin | LeftArm | LeftHand | RightArm | RightHand | LeftLeg | LeftFoot | RightLeg | RightFoot,
}
```

**Diff:**
- Every base flag bit (`Head`..`RightFoot`) is numerically identical between the two enums.
  The only naming divergence is bit `1<<1`: Onyx calls it `Chest`, Shitmed calls it `Torso`.
  `Hands`/`Arms`/`Legs`/`Feet` are byte-for-byte identical in both. `All` is byte-for-byte
  identical in both (`0x7FF`, includes `Groin`).
- Onyx-only composite names: `FullArms`, `FullLegs`, `BodyMiddle`, `FullLegsGroin`, `Vital`.
  Grepped for use across all of `Content.{Shared,Server,Client}/_Onyx` **and** every checked-out
  `Resources/Prototypes/_Onyx` — zero hits. They are unused dead constants within the sparse
  checkout's surface (may be used in weapon/gun prototypes outside the pinned sparse set —
  absent from sparse checkout, cannot confirm).
- Onyx's enum carries `[Serializable, NetSerializable]`; Shitmed's carries neither attribute
  (it's still sent over the wire fine — `TargetingComponent.Target` is `[AutoNetworkedField]` on
  a plain `[Flags]` enum, RT serializes flag enums without `NetSerializable`).

## 2. Helper functions (`GetRandomBodyPart`, `ToBodyPartType`-equivalents, etc.)

Onyx's helpers live on `SharedTargetingSystem` (`Content.Shared/_Onyx/Targeting/SharedTargetingSystem.cs:1-94`)
and `TargetResolverSystem` (`Content.Shared/_Onyx/Targeting/TargetResolverSystem.cs:1-125`).
Wolfgate has **no `SharedTargetingSystem` methods at all** beyond `GetValidParts()` — the
equivalent logic already exists, but on `SharedBodySystem` (partial file
`Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`, namespace
`Content.Shared.Body.Systems`). Side-by-side:

| Onyx | Signature | Wolfgate equivalent | Signature |
|---|---|---|---|
| `SharedTargetingSystem.SelectableParts` | `static readonly TargetBodyPart[]` (11 entries incl. `Groin`) | `SharedTargetingSystem.GetValidParts()` | `static TargetBodyPart[]` — **10 entries, `Groin` is commented out** (`Events.cs`… `SharedTargetingSystem.cs:13`: `//TargetBodyPart.Groin,`) |
| `SharedTargetingSystem.IsSelectable(TargetBodyPart)` | `static bool` | *(none)* | — |
| `SharedTargetingSystem.TryConvert(TargetBodyPart, out BodyPartType, out BodyPartSymmetry)` | `static bool` | `SharedBodySystem.ConvertTargetBodyPart(TargetBodyPart)` | `(BodyPartType Type, BodyPartSymmetry Symmetry)` — non-static, instance method, `SharedBodySystem.Targeting.cs:389-407` |
| `SharedTargetingSystem.TryConvert(BodyPartType, BodyPartSymmetry, out TargetBodyPart)` | `static bool` | `SharedBodySystem.GetTargetBodyPart(BodyPartType, BodyPartSymmetry)` | `TargetBodyPart?` — `SharedBodySystem.Targeting.cs:368-384` (also two overloads taking `Entity<BodyPartComponent>` / `BodyPartComponent`) |
| `TargetResolverSystem.TryResolve(body, origin, out part)` (reads origin's `TargetingComponent`) | `bool` | *(none — no direct match)* | — |
| `TargetResolverSystem.TryResolve(body, requested, shooter, out part)` (rolls anatomical odds via `Roll()`) | `bool` | *(none — no anatomical-odds roll step exists)* | — |
| `TargetResolverSystem.TryResolveAvailable(body, target, out part)` (exact-with-hand/foot→arm/leg fallback, else chest) | `bool` | *(none, but same fallback shape is inline in* `TryChangePartDamage` *(`SharedBodySystem.Targeting.cs:184-212`) — it iterates* `GetValidParts()` *masked by the flags instead of resolving one exact part)* | — |
| `TargetResolverSystem.TryResolveExact(body, target, out part)` | `bool` | *(none)* | — |
| `TargetResolverSystem.GetMatchingParts(body, mask)` | `List<EntityUid>` | *(none as a returned list, but the same mask-iteration is inlined in* `TryChangePartDamage`*)* | — |
| `TargetResolverSystem.Roll(component, requested)` — nested-dict weighted scatter keyed by *requested* part | `TargetBodyPart` | `SharedBodySystem.GetRandomBodyPart(uid, target?)` | `TargetBodyPart?` — **flat-dict weighted roll, ignores what was requested** (`SharedBodySystem.Targeting.cs:265-281`); separately, `OnTryChangePartDamage` has its own hardcoded 33%-scatter-off-Torso via `GetRandomPartSpread(random, torsoWeight)` (`:126-130`, `:246-263`) — a *third*, differently-shaped scatter mechanism |
| — | | `SharedBodySystem.GetBodyPartStatus(EntityUid)` | `Dictionary<TargetBodyPart, TargetIntegrity>` — no Onyx equivalent name, but same job as Onyx's `PartStatusSystem.Refresh` (different payload type, see §5) |

**Body-graph lookups Onyx's `TargetResolverSystem` calls that do already exist verbatim in
Wolfgate** (confirms the handoff's dependency-map claim):
- `_body.GetBodyChildren(body)` → `SharedBodySystem.Body.cs:256`: `IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid bodyId, BodyComponent? body = null)`.
- `_body.GetBodyChildrenOfType(body, type)` → `SharedBodySystem.Parts.cs:991`: `IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(EntityUid bodyId, BodyPartType type, BodyComponent? body = null, BodyPartSymmetry? symmetry = null)`.

Both match by name; Onyx's positional-arg call `_body.GetBodyChildrenOfType(body, type)` is
still valid against Wolfgate's signature (symmetry is optional).

**CCVars Onyx's resolver reads, none of which exist in Wolfgate** (grepped
`Content.Shared` for `TargetingEnabled`/`TargetingUseAnatomicalOdds`/`TargetingDownedTargetsAreExact`
— zero hits):
```csharp
// Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs:7-14
public static readonly CVarDef<bool> TargetingEnabled =
    CVarDef.Create("targeting.enabled", true, CVar.REPLICATED | CVar.SERVER);
public static readonly CVarDef<bool> TargetingUseAnatomicalOdds =
    CVarDef.Create("targeting.use_anatomical_odds", true, CVar.SERVER | CVar.ARCHIVE);
public static readonly CVarDef<bool> TargetingDownedTargetsAreExact =
    CVarDef.Create("targeting.downed_targets_are_exact", true, CVar.SERVER | CVar.ARCHIVE);
```
Wolfgate's Shitmed targeting/body files (`_Shitmed/Targeting/*`, `_Shitmed/Body/Systems/SharedBodySystem.Targeting.cs`)
have **no CCVar gate at all** — limb targeting is unconditionally on, and there is no
"downed targets are exact" or "anatomical odds" toggle anywhere in the grep.

## 3. `BodyPartType.Chest` / `BodyPartType.Groin` do not exist in Wolfgate — this is the real blocker

`TargetResolverSystem.TryConvert`/`TryFind` and `SharedTargetingSystem.TryConvert` both switch
on `BodyPartType.Chest` and `BodyPartType.Groin`
(`Content.Shared/_Onyx/Targeting/TargetResolverSystem.cs:62,117`; `SharedTargetingSystem.cs:60-61,80-81`).
Those values come from Onyx's own replacement of the vanilla body-part file:

```csharp
// Content.Shared/_Onyx/Body/Part/BodyPartComponent.cs:12-26 (namespace Content.Shared.Body.Part — replaces vanilla)
public enum BodyPartType : ushort
{
    Other = 0, Torso = 1, Head = 2, Arm = 3, Hand = 4, Leg = 5, Foot = 6, Tail = 7,
    Chest = 8, Groin = 9
}
```

Wolfgate's actual `BodyPartType` (`Content.Shared/Body/Part/BodyPartType.cs:12-20`, vanilla file,
not touched by Shitmed):
```csharp
public enum BodyPartType
{
    Other = 0, Torso, Head, Arm, Hand, Leg, Foot, Tail
}
```
**No `Chest`, no `Groin`.** Wolfgate has no physical Groin body part in the graph at all — this
is confirmed by Shitmed's own code treating Groin as a pure alias of Torso everywhere:
- `SharedBodySystem.Targeting.cs:395`: `TargetBodyPart.Groin => (BodyPartType.Torso, BodyPartSymmetry.None), // TODO: Groin is not a part type yet`
- `SharedBodySystem.Targeting.cs:356-357`: `// Hardcoded shitcode for Groin :)` / `result[TargetBodyPart.Groin] = result[TargetBodyPart.Torso];`
- `SharedBodySystem.Targeting.cs:321-322`: whenever `Torso`'s integrity changes, `Groin`'s `BodyStatus` entry is copied from it.
- `Events.cs`… `SharedTargetingSystem.cs:13`: `Groin` is commented out of `GetValidParts()`, i.e. it is not player-selectable and never independently resolved to a part.

D8 in DECISIONS.md forbids editing Wolfgate's core `BodyPartComponent`/`BodyPartType` (Onyx's
extra part fields go in a separate `_WF/Wolfmed` component instead), so adding `Chest`/`Groin`
to `BodyPartType` to make Onyx's `TryConvert`/`TryFind` compile verbatim is off the table. Any
port of `TargetResolverSystem`/`SharedTargetingSystem.TryConvert` **must** be rewritten against
Wolfgate's real `BodyPartType` (8 values, Groin folded into Torso) — it cannot be vendored
as-is even after a `TargetBodyPart` alias, because the failure is in the `BodyPartType` side of
the conversion, not the `TargetBodyPart` side.

## 4. Which Targeting symbols the Wounds files use (grep of `_Onyx/Wounds` for `Targeting`)

```
Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:17:using Content.Shared._Onyx.Targeting;
Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:329:  TryComp(carrier, out TargetingSnapshotComponent? snapshot) &&
Content.Shared/_Onyx/Wounds/WoundDamageRoutingSystem.cs:604:  TryComp(source, out TargetingSnapshotComponent? snapshot) &&
Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:6:using Content.Shared._Onyx.Targeting;
```
Two files only. Exact symbols consumed, with signatures:

- **`TargetResolverSystem`** — field `[Dependency] private TargetResolverSystem _targetResolver` (`WoundDamageRoutingSystem.cs:33`). Calls, with exact signatures from `TargetResolverSystem.cs`:
  - `_targetResolver.TryResolve(EntityUid body, TargetBodyPart requested, EntityUid? shooter, out EntityUid part)` — `WoundDamageRoutingSystem.cs:367,605`.
  - `_targetResolver.TryResolveAvailable(EntityUid body, TargetBodyPart target, out EntityUid part)` — `WoundDamageRoutingSystem.cs:613`, called with the literal `TargetBodyPart.Chest`.
  - `_targetResolver.TryResolve(EntityUid body, EntityUid origin, out EntityUid part)` — `WoundDamageRoutingSystem.cs:610`.
  - `_targetResolver.GetMatchingParts(EntityUid body, TargetBodyPart mask)` — `WoundDamageRoutingSystem.cs:210`.
- **`TargetBodyPart`** (the enum type) — parameter type on `TryApplyDistributedDamage(EntityUid, DamageSpecifier, TargetBodyPart mask, DamageDistribution mode, …)` (`:189-199`) and `TryApplyTargetedDamage`/`TryRouteTargetedDamage` (`:285-363`); literal `TargetBodyPart.Chest` used as the fallback mask for defibrillator-origin damage (`:613`) and as `WoundHostComponent.SystemicPainTarget`'s default (`WoundDamageComponents.cs:47`: `public TargetBodyPart SystemicPainTarget = TargetBodyPart.Chest;`).
- **`DamageDistribution`** (the enum) — parameter on the same two methods; only 3 of its 3 members are ever compared: `SplitEvenly`, `SplitByPartWeight`, `SplitWithVariation` (`:202,225,229,848`).
- **`TargetingSnapshotComponent`** — read-only, two fields: `.RequestedTarget` (`TargetBodyPart`) and `.Shooter` (`EntityUid?`), at `WoundDamageRoutingSystem.cs:329-336` (`TryRouteCarrierDamage`, used when a *carrier* entity — e.g. a thrown/reflected object — was captured from a shooter) and `:604-606` (systemic/localized-damage routing fallback chain).

No other Wounds file (`PainSystem`, `AmputationSystem`, `WoundSystem`, `OrganDamageSystem`, etc.)
references `Content.Shared._Onyx.Targeting` directly — routing is centralized in
`WoundDamageRoutingSystem`. Two adjacent, non-Wounds files also consume Targeting and matter for
later phases: `Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` (keys a
`Dictionary<TargetBodyPart, HealthAnalyzerWoundDiagnostic>` for the health-analyzer scan result)
and `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs` (examine text).
Both are phase-4 (health analyzer / examine) concerns, not phase-1 routing.

## 5. Does Onyx's part-status doll replace Shitmed's `TargetIntegrity` doll?

**No — recommend keeping Shitmed's doll for phase 1.** Evidence:

Onyx's `PartStatus` struct (`Content.Shared/_Onyx/Targeting/PartStatusComponent.cs:15-20`) is:
```csharp
public readonly record struct PartStatus(bool Exists, PartDamageSeverity Severity, bool Bleeding, FractureGrade Fracture, bool Scar);
```
`FractureGrade` comes from `using Content.Shared._Onyx.Wounds;` (`PartStatusComponent.cs:1`) — the
struct is wound-shaped by construction. Its only populator,
`Content.Server/_Onyx/Targeting/PartStatusSystem.cs:50-91` (`Refresh`), depends on:
```csharp
[Dependency] private WoundSystem _wounds = default!;
[Dependency] private PainSystem _pain = default!;
```
and subscribes `WoundableComponent, PartBleedingChangedEvent` / `FractureGradeChangedEvent` /
`ScarCreatedEvent`, then walks `_wounds.GetWounds(part)` summing `WoundComponent.Severity`,
checking `WoundBleedingComponent.CurrentRate`, `WoundFractureComponent.Grade`,
`WoundScarComponent`. **None of these types exist in Wolfgate yet** — they are the Wounds core
this same phase plan says ships in phase 1/2/3 (bleeding/fracture/scar are phase 2-3 per the
handoff's phase list). Porting `PartStatusComponent`/`PartStatusSystem` now would compile against
nothing.

Wolfgate's existing doll is driven by a completely different, already-working pipeline that
needs none of this:
```csharp
// Content.Shared/_Shitmed/Targeting/TargetingComponent.cs:38-52
[ViewVariables, AutoNetworkedField]
public Dictionary<TargetBodyPart, TargetIntegrity> BodyStatus = new() { ... 11 entries, all Healthy ... };
```
populated by `SharedBodySystem.CheckBodyPart`/`GetIntegrityThreshold`
(`SharedBodySystem.Targeting.cs:286-331,449-464`) purely from each part's `DamageableComponent.TotalDamage`
against `BodyPartComponent.IntegrityThresholds`, pushed to clients via `TargetIntegrityChangeEvent`
and rendered by `PartStatusControl.SetTextures(Dictionary<TargetBodyPart, TargetIntegrity>)`
(`Content.Client/_Shitmed/UserInterface/Systems/PartStatus/Widgets/PartStatusControl.xaml.cs:37-46`).
It is a coarser 9-value enum (`Healthy`..`Disabled`, `TargetIntegrity.cs:3-12`) with no bleeding/
fracture/scar detail, but it is fully wired end-to-end today (HUD alert doll, death handling in
`Content.Server/_Shitmed/Targeting/TargetingSystem.cs:28-54`, and it's what feeds the health
analyzer scan: `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs:17` already ships
`Dictionary<TargetBodyPart, TargetIntegrity>? Body`, populated from the identical
`_bodySystem.GetBodyPartStatus(uid)` call).

Onyx's `HealthAnalyzerStatusDoll` (`Content.Client/_Onyx/Targeting/UI/HealthAnalyzerStatusDoll.xaml.cs:47-63`)
is actually the *shallow* one — it computes severity straight from a `DamageSpecifier` per part
(`PartStatusSystem.GetSeverity(partDamage.GetTotal().Float())`), not from wounds — so that
specific widget has no hard Wounds dependency, but it is a different message shape
(`HealthAnalyzerWoundDiagnostics`, keyed dict of a diagnostics record) than Wolfgate's existing
`HealthAnalyzerScannedUserMessage.Body : Dictionary<TargetBodyPart, TargetIntegrity>`, so wiring
it in now would mean running two parallel health-analyzer payloads for no phase-1 benefit.

**Recommendation:** Phase 1 keeps Shitmed's `TargetIntegrity` doll (HUD alert doll +
health-analyzer doll) completely as-is, driven by `DamageableComponent`/`BodyPartComponent`
exactly like today. Do not vendor `PartStatusComponent`/`PartStatusSystem`
(shared/server/client) or `HealthAnalyzerStatusDoll` in phase 1. When Wounds ships bleeding/
fracture/scar (phases 2-3), feed that detail into the *existing* doll by widening
`TargetIntegrity` or by adding a second, additive per-part wound-summary message/widget in
`_WF/Wolfmed` — do not replace `BodyStatus`/`TargetIntegrity`, since `TargetingComponent`,
`CheckBodyPart`, death/revival, and the health-analyzer scan all read/write it already.

## 6. Recommendation: adapt to Shitmed directly, do not vendor a parallel Onyx targeting stack

Given §3 (Onyx's own `BodyPartType.Chest`/`Groin` don't exist and can't be added under D8) and
§1-2 (Onyx's `TargetBodyPart`, `TargetingComponent`, `SharedTargetingSystem`,
`TargetResolverSystem` all duplicate live, already-networked Shitmed functionality — keybinds,
HUD doll, death/revival, health-analyzer scan), a `// WOLFGATE` using-alias that makes Onyx's
`TargetBodyPart` *literally be* Shitmed's type is the right shape for the enum, but a full
parallel `TargetingComponent`/`SharedTargetingSystem`/`TargetResolverSystem` port is not: it
would attach a second, competing "current target" component to every mob, and its resolver
can't compile against Wolfgate's `BodyPartType` without rewriting the exact same switch
Wolfgate already wrote in `SharedBodySystem.Targeting.cs`. Recommended split:

**Do NOT vendor these Onyx files at all** (no `_Onyx/Targeting/` folder needed for the enum/
component/system layer):
- `TargetBodyPart.cs`, `TargetingComponent.cs`, `SharedTargetingSystem.cs`
- `TargetResolverSystem.cs`
- `Content.Server/_Onyx/Targeting/TargetingSystem.cs`, `Content.Client/_Onyx/Targeting/TargetingSystem.cs` + all of `Content.Client/_Onyx/Targeting/UI/*`
- `PartStatusComponent.cs`/`PartStatusSystem.cs` (shared+server), deferred per §5

**DO vendor as new files**, each with `using Content.Shared._Onyx.Targeting;` replaced by
`using Content.Shared._Shitmed.Targeting;` (mark the import line `// WOLFGATE`):
- `Content.Shared/_Onyx/Targeting/DamageDistribution.cs` — verbatim, zero dependency on `TargetBodyPart`/`BodyPartType`, no edit needed at all beyond the folder location.
- `Content.Shared/_Onyx/Targeting/TargetingSnapshotComponent.cs` — verbatim except its `Content.Shared._Onyx.Targeting` → `Content.Shared._Shitmed.Targeting` `using`, and its default `RequestedTarget = TargetBodyPart.Chest` → `TargetBodyPart.Torso` (one line, `// WOLFGATE`).
- `Content.Shared/_Onyx/Targeting/TargetingSnapshotSystem.cs` — verbatim except the same `using` swap, plus its two calls to `SharedTargetingSystem.IsSelectable(targeting.Target)` (`:23,40`) need `IsSelectable`, which Shitmed's `SharedTargetingSystem` doesn't have (§2). Cheapest fix: add a 3-line `// WOLFGATE` static helper to Shitmed's `SharedTargetingSystem.cs` — `public static bool IsSelectable(TargetBodyPart part) => part != 0 && (part & (part - 1)) == 0 && (part & TargetBodyPart.All) != 0;` (copied verbatim from Onyx's version, it has no Onyx-specific dependency) rather than reimplementing selectability checks in the snapshot system itself.

**New `_WF/Wolfmed` glue** (per D5/modularity rule 2 — adapters live here, not inside vendored files):
- `Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs` — a small new system providing the four methods `WoundDamageRoutingSystem` actually calls (§4), implemented on top of Wolfgate's existing `SharedBodySystem` methods instead of reimplementing body-graph search:
  - `TryResolveAvailable(EntityUid body, TargetBodyPart target, out EntityUid part)` → `_body.ConvertTargetBodyPart(target)` then `_body.GetBodyChildrenOfType(body, type, symmetry: symmetry).FirstOrDefault()`, with the same hand→arm/foot→leg/else→torso fallback Onyx's `TryFind` does (`TargetResolverSystem.cs:113-124`), rewritten against Wolfgate's `BodyPartType` (`Torso` instead of `Chest`, no `Groin` case needed since `ConvertTargetBodyPart` already folds `Groin`→`Torso`).
  - `TryResolve(EntityUid body, TargetBodyPart requested, EntityUid? shooter, out EntityUid part)` → phase-1 should skip Onyx's anatomical-odds `Roll()` step entirely and resolve to the exact requested part (see the open question below), i.e. just call `TryResolveAvailable`.
  - `TryResolve(EntityUid body, EntityUid origin, out EntityUid part)` → read `origin`'s Shitmed `TargetingComponent.Target` and forward to the method above.
  - `GetMatchingParts(EntityUid body, TargetBodyPart mask)` → `_body.GetBodyChildren(body)`, convert each part's `(PartType, Symmetry)` via `_body.GetTargetBodyPart(...)`, keep the ones `mask.HasFlag(...)` — mirrors Onyx's `TargetResolverSystem.cs:74-89` almost line for line.
  - This keeps `WoundDamageRoutingSystem` itself changed only at the field declaration (`TargetResolverSystem _targetResolver` → `WoundTargetResolver _targetResolver`, one `// WOLFGATE` line) and the `using` swap — the routing logic (distribution math, systemic/localized split, amputation hooks) stays untouched, matching modularity rule 1 ("keep vendored files close to verbatim").
- `Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs` — vendor verbatim (it's Onyx's own unmodified file with no Chest/Groin dependency); `WoundTargetResolver` reads `CCVars.TargetingEnabled` the same way Onyx's resolver did, gating only the new wound-routing path — it must NOT gate Shitmed's existing always-on targeting, since D2 says non-`WoundHostComponent` entities behave exactly as today.

**Mechanical rename, not alias, for `TargetBodyPart.Chest`:** there are exactly three literal
`.Chest` references in the whole Wounds+Targeting surface (`WoundDamageRoutingSystem.cs:481-482`
inside `TryApplyTargetedDamage`'s default-part logic — re-check this call site when vendoring,
`WoundDamageRoutingSystem.cs:613`, `WoundDamageComponents.cs:47`). Renaming these three to
`TargetBodyPart.Torso` (with `// WOLFGATE` comments) is simpler and safer than adding a
`Chest = Torso` second name to Shitmed's real enum — it touches only the files already being
hand-edited for the `_Onyx.Targeting` → `_Shitmed.Targeting` `using` swap, versus permanently
growing Shitmed's shared enum for the sake of three call sites.

**Open question to resolve before implementing** (not answered by this report — flagging per
task instructions): Onyx's anatomical-odds scatter (`TargetResolverSystem.Roll`, nested dict
keyed by requested part) and Wolfgate's two *existing*, differently-shaped scatter mechanisms
(`SharedBodySystem.GetRandomBodyPart` — flat-dict, ignores request; `OnTryChangePartDamage`'s
hardcoded 33%-off-Torso `GetRandomPartSpread`) are three incompatible designs. Recommend
`WoundTargetResolver` resolve to the *exact* requested/snapshot part with no scatter in phase 1
(simplest, avoids double-applying inaccuracy on top of whatever the gun/melee system already
rolls before calling into wound routing), and leave "should wound-hosted precision aim also
scatter" as a phase-4/6 balance decision rather than a routing-correctness one.

## 7. Key-function and body-string diffs (secondary, but a real compile/behavior gap)

`ContentKeyFunctions.cs` in Wolfgate (`Content.Shared/Input/ContentKeyFunctions.cs:92-101`)
defines 10 target-select bindings, matching Shitmed's naming (`TargetTorso`, no `TargetChest`,
no `TargetGroin`). Onyx's client `TargetingSystem.cs:34-43` binds 11, including
`ContentKeyFunctions.TargetChest` and `ContentKeyFunctions.TargetGroin`, both absent from
Wolfgate. Not a blocker since phase 1 doesn't port Onyx's client `TargetingSystem` at all (§6),
but note it for any later phase that revisits keybinds — Wolfgate's Shitmed client system
(`Content.Client/_Shitmed/Targeting/TargetingSystem.cs:28-49`) already binds all 10 real
functions to `TargetBodyPart.Torso`/etc. and has no groin bind (consistent with Groin not being
independently selectable).

## Summary of concrete file actions for phase 1

| Action | Path |
|---|---|
| Skip entirely | `_Onyx/Targeting/{TargetBodyPart,TargetingComponent,SharedTargetingSystem}.cs`, `TargetResolverSystem.cs`, all `Content.Server/_Onyx/Targeting/*`, all `Content.Client/_Onyx/Targeting/*` |
| Defer to phase 3/4 | `PartStatusComponent.cs`, `PartStatusSystem.cs` (shared+server), `HealthAnalyzerStatusDoll.xaml(.cs)` |
| Vendor verbatim (folder move + `using` swap only) | `DamageDistribution.cs` |
| Vendor + 1-line edit | `TargetingSnapshotComponent.cs` (default `.Chest`→`.Torso`), `TargetingSnapshotSystem.cs` (needs `IsSelectable`) |
| Vendor verbatim | `Content.Shared/_Onyx/CCVar/CCVars.Targeting.cs` |
| Add (Shitmed file, `// WOLFGATE`) | `IsSelectable(TargetBodyPart)` static helper on `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` |
| New file | `Content.Shared/_WF/Wolfmed/Targeting/WoundTargetResolver.cs` |
| Rewrite against Shitmed types | `WoundDamageRoutingSystem.cs` (field type + 3× `.Chest`→`.Torso` + `using`), `WoundDamageComponents.cs` (1× `.Chest`→`.Torso` + `using`) |

No blocker prevents phase-1 routing from working against Wolfgate's existing body/targeting
stack; the work is real but mechanical, and every signature quoted above was read from the
actual files, not assumed.
