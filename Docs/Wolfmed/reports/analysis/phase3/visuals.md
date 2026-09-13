# P3-4 — Limb damage sprites: analyst report

**Scope:** the `PartDamageVisualsComponent` consumer — making wounds visible on the body sprite of organic
humanoids. Read-only analysis against WG (`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`,
HEAD `1171e02fb6`, phase 2 committed) and ONYX (`C:/tmp/onyx`, pinned `2f5bab9946539cbe083010c9ae6fbc59b47ae377`).

## 0. Headline correction to the task brief

The task brief says to "confirm WP7 copied [`brute_damage.rsi`/`burn_damage.rsi`] to
`WG/Resources/Textures/_Onyx/Wounds`". **It did not.** `WOLFMED_MANIFEST.md:395-397` states explicitly:

> `Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` are not ported, per PLAN's explicit "textures
> deliberately not ported" note — `wounds.yml` has zero texture references, and the sprites are cosmetic
> re-skins of ones Wolfgate already ships.

Verified directly: `find WG/Resources/Textures/_Onyx` shows only `Interface/Alerts/fracture.rsi` (WP7's
fracture alert icon) — no `Wounds/` directory exists under `_Onyx` textures at all. `WOLFMED_STATUS.md:85,91-92`
and `DECISIONS.md` P2-7 ("`PartDamageVisualsComponent` — defer to phase 3") both confirm the consumer, not
just the textures, was never built. **This report treats P3-4 as fully unstarted**, and corrects the premise
that a texture copy already happened.

The good news this correction uncovers: because nothing consumes `PartDamageVisualsComponent` yet, and
because the *server-side* data plumbing for it shipped complete and already-hardened in phase 1 (WP5) and
phase-1's WP9 bugfix pass, **P3-4 is almost entirely a client-only C# change** — no shared/server code, no
new components, no new subscriptions server-side, and (for the minimal deliverable) no new textures either.

## 1. `PartDamageVisualsComponent` — already shipped, already networked, already fed data

`Content.Shared/_Onyx/Wounds/WoundDamageComponents.cs:95-100` (ported verbatim in phase 1, WP4 file #4):

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class PartDamageVisualsComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, DamageSpecifier> Damage = new();
}
```

`raiseAfterAutoHandleState: true` is the load-bearing bit — the client gets an `AfterAutoHandleStateEvent`
every time this component's networked state changes, with no polling needed.

**Who writes it — `Content.Shared/_Onyx/Wounds/WoundDamageProjectionSystem.cs` (shared, ported in WP5, live
since phase 1):**

- `RefreshBodyDamage(EntityUid body)` (`:156-203`): server-only (`!_net.IsServer` early-out at `:158`).
  `EnsureComp<PartDamageVisualsComponent>(body)` (`:178`), `visual.Damage.Clear()` (`:179`), then for every
  **attached** body child it takes `_damage.GetPositiveDamage((part, damageable))` (the D12 facade,
  `:184`) and maps the part to a `HumanoidVisualLayers` key via `TryGetVisualLayer` (`:186`, defined at
  `:240-261`), accumulating per layer (`:188-191`) and `Dirty(body, visual)` (`:197`). Called from
  `OnMapInit` (`:41-45`), `OnPartDamageDealt` (`:99-105`, the post-write `DamageChangedEvent` handler —
  D11), `OnPartInserted`/`OnPartRemoved` (`:107-122`), and `OnRejuvenate` (`:89`).
- `RefreshDetachedDamage(EntityUid root)` (`:134-154`): same shape, but walks `_body.GetBodyPartChildren(root)`
  from a **detached** root and writes onto the detached part's own `PartDamageVisualsComponent` (`:139`,
  `Dirty(root, visual)` at `:153`). Called from `OnPartDamageDealt` when `component.Body is null` (`:103-104`,
  via `GetDetachedRoot`) and from `OnPartRemoved` (`:119`).
- `TryGetVisualLayer` (`:240-261`) is the **already-D9-folded** mapping: `(BodyPartType.Torso, _) →
  HumanoidVisualLayers.Chest` (`:248`, with a `// WOLFGATE: D9` comment and an explicit
  `// no BodyPartType.Groin and no HumanoidVisualLayers.Groin` note at `:249`), plus the eight
  symmetry-qualified Arm/Hand/Leg/Foot cases (`:251-258`) and Head (`:250`). **This exact mapping is what
  Onyx's own client code needs and it already exists — nothing to add here.**

**Who actually calls `OnPartInserted`/`OnPartRemoved` — `Content.Server/_WF/Wolfmed/WolfmedBodyPartLifecycleSystem.cs`**
(new in WP5, hardened in WP9). Confirmed present and correct at `:22-23` (`SubscribeLocalEvent<WoundHostComponent,
BodyPartAddedEvent/RemovedEvent>`, D28), `:37/:40` (`_projection.OnPartInserted` + `_bleeding.OnPartInserted`),
`:55/:58` (`_projection.OnPartRemoved` + `_bleeding.OnPartChanged`), and the WP9 `TerminatingOrDeleted` guard
(`:31,52`) that stops `RefreshDetachedDamage`'s `EnsureComp` from throwing `DebugAssertException` during
`RecursiveDeleteEntity` (manifest WP9 row, verified against the live file).

**Conclusion:** the moment a client-side consumer exists, every organic wound host — attached or detached —
already carries a correctly-populated, correctly-networked `PartDamageVisualsComponent`. P3-4 has zero
outstanding shared/server work.

## 2. `Content.Client/Damage/DamageVisualsComponent.cs` — SAME (no Onyx-marked lines here)

Read in full both sides. **There are no `<Onyx-...>` tags in this file at all** — the task brief's framing
("Onyx-marked lines" in both files) only holds for `DamageVisualsSystem.cs`. The component diffs between
ONYX and WG are unrelated upstream drift, not wound work:

| Field | WG (`Content.Client/Damage/DamageVisualsComponent.cs`) | ONYX | Verdict |
|---|---|---|---|
| `DamageOverlayGroups` | `:58` `Dictionary<string, DamageVisualizerSprite>?` | `Dictionary<ProtoId<DamageGroupPrototype>, DamageVisualizerSprite>?` | DIFFERENT, but irrelevant — Onyx never adds a wound-specific field here, this is an unrelated upstream `ProtoId`-ification |
| `DamageGroup` | `:87` `string?` | `ProtoId<DamageGroupPrototype>?` | DIFFERENT, same as above |
| `Displacement` | absent | present (`:126-127`, `DisplacementData?`) | MISSING in WG, but this is the unrelated clothing-occlusion displacement-map feature, not wounds — WG has `Content.Client/DisplacementMap/DisplacementMapSystem.cs` and `Content.Shared/DisplacementMap/DisplacementData.cs` already, so it isn't even a missing-API problem, just a feature nobody asked this port to touch |

**Verdict for P3-4: no edit to `DamageVisualsComponent.cs` is needed at all.** Leave it byte-identical.

## 3. `Content.Client/Damage/DamageVisualsSystem.cs` — the real diff, and what it actually needs

Both files read in full (WG: 701 lines; ONYX: 904 lines). ONYX's extra ~200 lines are **not** all wound work —
Onyx's tree has independently rebased this file onto a newer upstream `SpriteSystem` entity-tuple API
(`SpriteSystem.LayerMapTryGet((uid, sprite), …)` instead of WG's `spriteComponent.LayerMapTryGet(…)`),
added the unrelated `DisplacementMapSystem` integration, and switched `_prototypeManager` → the base
`ProtoMan`, `DamagePerGroup` → `_damageable.GetDamagePerGroup(uid)`. **None of that is required to port** —
it is Onyx's independent upstream-sync noise sitting alongside the wound tags. The four `<Onyx-PartDamageVisuals...>`
tagged regions are the entire actual payload:

| Tag location (ONYX line) | What it does | WG has the API it needs? |
|---|---|---|
| `:2-4` `using Content.Shared._Onyx.Wounds;` | brings `PartDamageVisualsComponent` into scope | trivial |
| `:46-49` (`Initialize`) | two `SubscribeLocalEvent` calls: `<PartDamageVisualsComponent, AfterAutoHandleStateEvent>` and `<BodyPartComponent, AfterAutoHandleStateEvent>` | see §4 — first is free, second needs a 1-line upstream fix |
| `:52-157` | `OnPartDamageVisualsState`, `OnBodyPartState`, `UpdateDetachedPartDamage`, `UpdateDetachedDamageLayer`, `SetDetachedDamageLayerVisible`, `GetDetachedDamageThreshold`, `TryGetDetachedDamagePrefix` — the **detached/severed-limb** overlay | SAME `SpriteSystem` entity-tuple API already exists in WG's RT 277 — see §5 |
| `:498-505` (inside `HandleDamage`) + `:532-552` (`UpdatePartDamageVisuals`) | the **attached-body per-limb-accurate** overlay: branch inside `HandleDamage` that, when `PartDamageVisualsComponent` is present, reads *per-limb* damage instead of the mob's aggregate `DamagePerGroup` | everything it calls already exists in WG unchanged — see §5 |

### 3.1 Why the attached-body path is the one that matters most

WG's stock humanoid **already renders a whole-body brute/burn overlay today** — `Resources/Prototypes/Entities/Mobs/Species/base.yml:61-75`:

```yaml
- type: DamageVisuals
  thresholds: [ 10, 20, 30, 50, 70, 100 ]
  targetLayers:
  - "enum.HumanoidVisualLayers.Chest"
  - "enum.HumanoidVisualLayers.Head"
  - "enum.HumanoidVisualLayers.LArm"
  - "enum.HumanoidVisualLayers.LLeg"
  - "enum.HumanoidVisualLayers.RArm"
  - "enum.HumanoidVisualLayers.RLeg"
  damageOverlayGroups:
    Brute:
      sprite: Mobs/Effects/brute_damage.rsi
      color: "#FF0000"
    Burn:
      sprite: Mobs/Effects/burn_damage.rsi
```

Today this reads the **mob's aggregate** `DamageableComponent.DamagePerGroup` (WG `HandleDamage:383`,
`Comp<DamageableComponent>(uid).DamagePerGroup.Keys.ToList()`), so once WP5's routing/projection makes a
wound host's own `DamageableComponent` a *sum of all parts* (D30), **every targeted layer already lights up
together whenever the mob takes any damage anywhere** — it's just not per-limb-accurate: a chest hit makes
the arm sprite glow too. `UpdatePartDamageVisuals` is the fix — it replaces the aggregate read with a
per-layer read of `PartDamageVisualsComponent.Damage[layer]`, giving each of the six layers its own,
correct, independent threshold.

### 3.2 Onyx species wiring — read directly, and the one line D9 already forbids

`git -C C:/tmp/onyx show HEAD:Resources/Prototypes/Body/species_base.yml`, lines 14-35:

```yaml
- type: DamageVisuals
  thresholds: [ 10, 20, 30, 50, 70, 100 ]
  targetLayers:
  - "enum.HumanoidVisualLayers.Chest"
  # <Onyx-GroinDamageVisuals>
  - "enum.HumanoidVisualLayers.Groin"
  # </Onyx-GroinDamageVisuals>
  - "enum.HumanoidVisualLayers.Head"
  - "enum.HumanoidVisualLayers.LArm"
  - "enum.HumanoidVisualLayers.LLeg"
  - "enum.HumanoidVisualLayers.RArm"
  - "enum.HumanoidVisualLayers.RLeg"
  damageOverlayGroups:
    Brute:
      # <Onyx-PartDamageVisuals-edited>
      sprite: _Onyx/Wounds/brute_damage.rsi
      # </Onyx-PartDamageVisuals-edited>
      color: "#FF0000"
    Burn:
       # <Onyx-PartDamageVisuals-edited>
       sprite: _Onyx/Wounds/burn_damage.rsi
       # </Onyx-PartDamageVisuals-edited>
```

Two edits, both tiny: (a) add `HumanoidVisualLayers.Groin` to `targetLayers` — **forbidden by D9**, WG's
`HumanoidVisualLayers` enum (`Content.Shared/Humanoid/HumanoidVisualLayers.cs:6-34`, read in full) has no
`Groin` member, and D9 already folds `BodyPartType.Torso → HumanoidVisualLayers.Chest` everywhere including
`TryGetVisualLayer` above — **do not port this line**, `Chest` alone already carries the folded torso+groin
damage. (b) swap the sprite path from Onyx's own `Mobs/Effects/{brute,burn}_damage.rsi`-equivalent to
`_Onyx/Wounds/{brute,burn}_damage.rsi` — see §6 for whether WG needs to do the same.

**Note Onyx does *not* add Hand/Foot layers to `targetLayers` either** — the live on-body overlay stays at
six layers in Onyx too. Hand/Foot/Groin wound art only ever appears through the *detached*-part path (§3's
third row), which iterates `Enum.GetValues<HumanoidVisualLayers>()` directly and is not gated by
`targetLayers` at all (ONYX `DamageVisualsSystem.cs:86-90`, verified).

## 4. Subscription audit (both new pairs client-side; the `_WF` compat rule and §5-style check apply
here even though the whole audit lives in PLAN.md/PLAN2.md's server/shared context — client subs are per-`IEntityManager` too and RT's crash is the same class of bug)

| Pair | Registrant | Free? |
|---|---|---|
| `PartDamageVisualsComponent`, `AfterAutoHandleStateEvent` | new `DamageVisualsSystem` handler | **Yes — verified.** `grep -rn "PartDamageVisualsComponent" Content.Client` → zero hits anywhere in the client tree today. |
| `BodyPartComponent`, `AfterAutoHandleStateEvent` | new `DamageVisualsSystem` handler | **Yes — verified.** `grep -rn "BodyPartComponent, AfterAutoHandleStateEvent"` across `Content.Client`, `Content.Shared`, `Content.Server` → zero hits. **But see §4.1 — the event is never *raised* for this component in WG today, so subscribing to it is a silent no-op until fixed.** |

### 4.1 A genuine, verified gap: `BodyPartComponent` doesn't raise `AfterAutoHandleStateEvent` in WG

`Content.Shared/Body/Part/BodyPartComponent.cs:18-20` (Shitmed's component, the one D8 keeps WG on):

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(...)]
public sealed partial class BodyPartComponent : Component, ISurgeryToolComponent // Shitmed Change
```

No `raiseAfterAutoHandleState: true`. Compare Onyx's **own** `BodyPartComponent` (D8 already documents WG
never ports this file — it's the one that "redeclares four `Content.Shared.Body.Part` types", `PLAN.md`
D8 evidence) at `_Onyx/Body/Part/BodyPartComponent.cs:41-42`:

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class BodyPartComponent : Component
```

Onyx's client code can subscribe to `<BodyPartComponent, AfterAutoHandleStateEvent>` and have it actually
fire because *Onyx's* `BodyPartComponent` sets the flag. WG's Shitmed `BodyPartComponent` does not. **Porting
`OnBodyPartState`'s subscription verbatim compiles cleanly and never fires** — a silent dead handler, exactly
the class of bug this whole plan's process exists to catch before it ships.

**Fix, if the detached-limb path (§6, Option B) is taken:** a one-line upstream `// WOLFGATE` edit,
`Content.Shared/Body/Part/BodyPartComponent.cs:18`: add `raiseAfterAutoHandleState: true` to the existing
attribute. Confirmed safe: zero existing subscribers to `<BodyPartComponent, AfterAutoHandleStateEvent>`
anywhere in the tree (§4 table), so turning the flag on can only add behaviour, never change any existing
one. This is exactly the one- or two-line upstream-hook shape the plan requires, with the body (`OnBodyPartState`)
living in the `_WF` partial as usual. **If the minimal, attached-only deliverable (Option A) is taken instead,
skip this edit entirely** — `OnBodyPartState` and the detached-rendering methods are not needed and this
flag flip is not required.

## 5. Client sprite API — SAME, better than the task brief assumed

The task brief frames this as an open API-gap question. It is not — **WG's RT 277 already carries the exact
`SpriteSystem` entity-tuple API Onyx's newer file uses**, verified directly:

- `RobustToolbox/Robust.Client/GameObjects/EntitySystems/VisualizerSystem.cs:14`:
  `[Dependency] protected SpriteSystem SpriteSystem = default!;` — already available to every
  `VisualizerSystem<T>` descendant, `DamageVisualsSystem` included. No shim needed (unlike WP1's `ProtoMan`
  gap on `EntitySystem`, which this is *not* a repeat of).
- `SpriteSystem.LayerMapTryGet(Entity<SpriteComponent?>, Enum/string, out int, bool)` —
  `SpriteSystem.LayerMap.cs:118,138`. SAME signature Onyx calls.
- `SpriteSystem.AddLayer(Entity<SpriteComponent?>, SpriteSpecifier, int?)` — `SpriteSystem.Layer.cs:223`. SAME.
- `SpriteSystem.LayerMapSet(Entity<SpriteComponent?>, Enum/string, int)` — `SpriteSystem.LayerMap.cs:14,29`. SAME.
- `SpriteSystem.LayerSetRsiState(Entity<SpriteComponent?>, int/string/Enum, StateId)` —
  `SpriteSystem.LayerSetters.cs:148,154,160`. SAME.
- `SpriteSystem.LayerSetVisible(Entity<SpriteComponent?>, int/string/Enum, bool)` —
  `SpriteSystem.LayerSetters.cs:358,364,370`. SAME.
- `SpriteSystem.LayerSetColor(Entity<SpriteComponent?>, int/string/Enum, Color)` —
  `SpriteSystem.LayerSetters.cs:395,401,407`. SAME.

For the **attached-body** path (`UpdatePartDamageVisuals`), the port doesn't even need `SpriteSystem` —
it reuses WG's existing private helpers `CheckThresholdBoundary` and `UpdateTargetLayer(SpriteComponent, …)`
verbatim (both already take a raw `SpriteComponent`, matching WG's older calling convention throughout the
rest of the file), and the one prototype lookup Onyx does via bare `ProtoMan` (`layerDamage.GetDamagePerGroup(ProtoMan)`,
ONYX `:78`) becomes `layerDamage.GetDamagePerGroup(_prototypeManager)` — WG's existing field, same name it
already uses everywhere else in this file (`DamageVisualsSystem.cs:29`). `DamageSpecifier.GetDamagePerGroup(IPrototypeManager)`
is confirmed SAME signature, `Content.Shared/Damage/DamageSpecifier.cs:298`.

**`EntitySystem.ProtoMan` itself is confirmed MISSING from RT 277** (`grep -n "ProtoMan" RobustToolbox/Robust.Shared/GameObjects/EntitySystem.cs`
→ zero hits — the same gap WP1 hit and shimmed for `StatusEffectsSystem`). It does not need shimming here:
because this port is *re-authoring Onyx's diff onto WG's actual file* rather than vendoring a byte-identical
Onyx copy (this file is upstream, not `_Onyx/`), the new code can simply say `_prototypeManager` instead of
`ProtoMan` — there is no vendored-verbatim constraint to preserve the name.

## 6. Textures — two options, neither is what the task brief assumed

Onyx's `_Onyx/Wounds/{brute,burn}_damage.rsi` (`git -C C:/tmp/onyx ls-tree -r --name-only HEAD --
Resources/Textures/_Onyx/Wounds/` → 156 files: 2×`meta.json` + 154 `.png`, 77 states per RSI = 11 layers ×
7 thresholds `{10,20,30,40,50,70,100}`) both declare `"license": "CC-BY-SA-3.0", "copyright": "Drawn by
Ubaser."` — **the identical artist and license already shipped and accepted** in WG's own
`Resources/Textures/Mobs/Effects/{brute,burn}_damage.rsi` (same copyright line, confirmed via
`meta.json`). No new licensing review needed if these are copied.

WG's existing `Mobs/Effects/{brute,burn}_damage.rsi` states (confirmed via `meta.json`, 6×6=36 states each):
`{Chest,Head,LArm,LLeg,RArm,RLeg}_{Brute|Burn}_{10,20,30,50,70,100}` — **exactly the six layers the species
prototype already targets, with no gaps.** Onyx's RSI is a strict superset adding `LHand`, `RHand`, `LFoot`,
`RFoot`, `Groin` states (plus an unused `_40` threshold not in any `Thresholds:` list on either side —
dead states in Onyx too).

**Option A — minimal (attached-body only, zero new texture files).** Leave `sprite:` on `base.yml:72,75`
pointing at WG's own `Mobs/Effects/brute_damage.rsi`/`burn_damage.rsi` unchanged. `UpdatePartDamageVisuals`
only ever touches the six already-targeted layers, and WG's RSI already has every state those six layers
need. **This satisfies "wounds visible on the body sprite for organic humanoids" completely, with a
zero-byte texture diff.**

**Option B — full parity (adds severed/detached-limb wound rendering).** Copy Onyx's two 78-file RSIs to
`Resources/Textures/_Onyx/Wounds/{brute,burn}_damage.rsi` (same license, no new attribution burden), swap
`base.yml`'s two `sprite:` lines to point at them (harmless for the six shared layers — same artist, and the
`_40` states are simply unreferenced), port `OnBodyPartState` + the five detached-rendering helper methods,
and take the §4.1 `BodyPartComponent` flag fix. This is what makes a **severed arm lying on the floor** (P3-1
amputation, same phase) show its own brute/burn wound texture instead of its plain unwounded sprite.

**Recommendation:** ship Option A as the phase-3 floor (matches the DECISIONS.md P3-4 wording exactly: "so
wounds show on the body sprite"). Do Option B in the same work package if time allows — its server-side data
source (`RefreshDetachedDamage`) has been live and WP9-hardened since phase 1 with zero consumers, P3-1 is
landing amputation this same phase so severed limbs will actually exist to look at, and the incremental cost
is one texture copy + ~70 lines + one already-safe upstream flag flip. If deferred, record it in the manifest
as a P3-4 sub-item handed to a later phase, same pattern as this port's other explicit hand-offs.

## 7. Humanoid layering conflict check — none found

Checked whether Shitmed's severed-limb visual (`Content.Shared/_Shitmed/Body/Systems/SharedBodySystem.PartAppearance.cs`,
`BodyPartAppearanceComponent`) claims the same layers `DamageVisualsSystem` would animate. Read the whole
207-line file. It operates through a **different mechanism entirely**: `SharedHumanoidAppearanceSystem.SetLayerVisibility`/`SetBaseLayerColor`/`AddMarking`
on `HumanoidAppearanceComponent`'s own base-sprite-layer and marking system (`:121-122,142,172,176,178,182,185,199`),
toggling the *humanoid's own body-part sprite layer* (e.g. the `Chest` skin/clothing layer itself) on
attach/detach (`OnPartAttachedToBody`/`OnPartDroppedFromBody`, `:135-161`).

`DamageVisualsSystem`, by contrast, never touches that layer's *content* — `AddDamageLayerToSprite`
(`DamageVisualsSystem.cs:325-335`) creates a **new, separate** sprite layer keyed `"{layer}{group}"` (e.g.
`"ChestBrute"`, `"ChestBurn"`) and inserts it immediately *after* the humanoid's own `Chest` layer by index
(`spriteComponent.LayerMapGet(layer)` at `:256`, `index += 1` at `:261`). Different layer-map keys, so no
collision, no double-write, no ordering hazard between the two systems — confirmed by reading both files in
full, not inferred.

One pre-existing (not phase-3-introduced) rough edge, noted for completeness: `UpdateDisabledLayers`
(`DamageVisualsSystem.cs:395-421`) reads an appearance-data key per target layer to hide a damage overlay
when the underlying limb layer is disabled, but nothing in WG sets that key today (`:399-400`'s own comment:
`"I assume this gets set by something like body system if limbs are missing??? TODO is this actually used
by anything anywhere?"`). This does **not** cause a stale-overlay bug for the wound-host case specifically:
`RefreshBodyDamage`/`RefreshDetachedDamage` only populate `PartDamageVisualsComponent.Damage` for **currently
attached** children (`_body.GetBodyChildren(body)`, WoundDamageProjectionSystem.cs:180) and `.Clear()` the
dict first, so a just-detached limb's layer key naturally drops out of the dict on the very next refresh,
`GetValueOrDefault` returns null, `UpdatePartDamageVisuals` computes threshold 0, and `UpdateTargetLayer` →
`UpdateDamageLayerState` (`:686-700`) hides the layer. **Self-healing by construction — no extra code needed
for this in P3-4.**

## 8. Client sandbox notes

`Content.Client/Damage/DamageVisualsSystem.cs` and its new `_WF` partial are pure `Content.Client` code —
never loaded into the client's sandboxed prediction AppDomain (that restriction, and `Sandbox.yml`, governs
what `Content.Shared` may reference, since only `Content.Shared` runs inside the client's sandbox for
prediction; `Content.Client` is fully trusted, server-side code never runs it). No `Sandbox.yml` entry is
needed. The new namespaces the `_WF` partial pulls in (`Content.Shared._Onyx.Wounds`, `Content.Shared.Humanoid`)
are already referenced elsewhere from `Content.Client` today, so this doesn't even add a new
`Content.Shared`-to-`Content.Client` dependency edge.

## 9. Prediction notes

`DamageVisualsSystem` is a `VisualizerSystem<T>` reacting only to `AppearanceChangeEvent`/`AfterAutoHandleStateEvent` —
both driven purely by confirmed, server-authoritative networked component state, never by client-predicted
local simulation. It was never part of client-side prediction before this port and remains so after it.
Per D35 (accepted for phase 1, unchanged since), wound-host damage routing itself is unpredicted, so a
player's own hit already lags one round trip before the pain HUD/alerts update; the limb sprite will lag by
exactly the same one round trip, for exactly the same accepted reason. P3-4 introduces no new prediction
surface and no new mispredict class beyond what D35 already priced in.

## 10. Minimal phase-3 deliverable — decision

**Ship:** wounds visible on the attached body sprite for organic humanoids, per-limb accurate (Option A,
§6), optionally extended to severed limbs in the same work package if time allows (Option B).

**Symbols vs WG:**

| Symbol | Verdict |
|---|---|
| `PartDamageVisualsComponent` | SAME — already shipped verbatim in WP4, unconsumed since |
| `WoundDamageProjectionSystem.RefreshBodyDamage`/`RefreshDetachedDamage`/`TryGetVisualLayer` | SAME — already shipped and D9-folded in WP5, unconsumed since |
| `WolfmedDamageableSystem` (D12 facade) | not needed client-side — client only reads networked state |
| `DamageVisualsComponent.TargetLayers`/`TargetLayerMapKeys`/`DamageOverlayGroups` | SAME as WG today, no field changes |
| `SpriteSystem` entity-tuple API (`LayerMapTryGet`/`AddLayer`/`LayerMapSet`/`LayerSetRsiState`/`LayerSetVisible`/`LayerSetColor`) | SAME — already on `VisualizerSystem<T>` in RT 277 (§5) |
| `EntitySystem.ProtoMan` | MISSING in RT 277 — irrelevant here, use WG's existing `_prototypeManager` field instead (no shim needed, this file isn't vendored verbatim) |
| `BodyPartComponent` raising `AfterAutoHandleStateEvent` | DIFFERENT — WG's Shitmed component lacks `raiseAfterAutoHandleState: true` (§4.1); needed only for Option B |
| `HumanoidVisualLayers.Groin` | MISSING (by design, D9) — do not port the one Onyx line that adds it to `targetLayers` |
| `DamageSpecifier.GetDamagePerGroup(IPrototypeManager)` | SAME, `DamageSpecifier.cs:298` |

**Hooks (bodies in `_WF` partials; `DamageVisualsSystem` is upstream, not vendored):**

1. `Content.Client/Damage/DamageVisualsSystem.cs`, inside `Initialize()` (after the existing single
   `SubscribeLocalEvent` line): **1 line** — `SubscribeLocalEvent<PartDamageVisualsComponent, AfterAutoHandleStateEvent>(OnPartDamageVisualsState);`
   (+ a `using Content.Shared._Onyx.Wounds;` at the top). Option B adds a second line for
   `<BodyPartComponent, AfterAutoHandleStateEvent>`.
2. `Content.Client/Damage/DamageVisualsSystem.cs`, inside `HandleDamage(...)`, immediately after the existing
   `UpdateDisabledLayers` call and before `CheckOverlayOrdering`: **4 lines** —
   ```csharp
   // WOLFGATE: P3-4 wound-host per-part visuals; body in _WF/Wolfmed/Compat/DamageVisualsSystem.Wolfmed.cs
   if (damageVisComp.TargetLayers != null && damageVisComp.DamageOverlayGroups != null &&
       TryComp(uid, out PartDamageVisualsComponent? partDamage))
   { UpdatePartDamageVisuals(uid, spriteComponent, damageVisComp, partDamage); return; }
   ```
3. (Option B only) `Content.Shared/Body/Part/BodyPartComponent.cs:18` — **1 word**, add
   `raiseAfterAutoHandleState: true` to the existing `AutoGenerateComponentState` attribute.

**New file:** `Content.Client/_WF/Wolfmed/Compat/DamageVisualsSystem.Wolfmed.cs` — a partial on
`Content.Client.Damage.DamageVisualsSystem`, holding `OnPartDamageVisualsState` and `UpdatePartDamageVisuals`
(≈25 lines, Option A) or all seven Onyx-tagged methods (≈95 lines, Option B). Calls WG's existing private
`HandleDamage`, `CheckThresholdBoundary`, and `UpdateTargetLayer` — legal because partial-class parts share
private member access.

**Subscription pairs (client-side, both confirmed free — §4):** `PartDamageVisualsComponent`/`AfterAutoHandleStateEvent`;
Option B adds `BodyPartComponent`/`AfterAutoHandleStateEvent`.

**Resources:** Option A — none. Option B — copy 156 files (2 RSIs) to `Resources/Textures/_Onyx/Wounds/`
(same license already accepted in WG) and a 2-line `sprite:` swap in `Resources/Prototypes/Entities/Mobs/Species/base.yml`
(do **not** add the `Groin` targetLayers line — D9 forbids it).

**Ordered file list:**
1. `Content.Client/_WF/Wolfmed/Compat/DamageVisualsSystem.Wolfmed.cs` (new)
2. `Content.Client/Damage/DamageVisualsSystem.cs` (upstream hook, 2 insertions)
3. *(Option B)* `Content.Shared/Body/Part/BodyPartComponent.cs` (upstream, 1-word flag)
4. *(Option B)* `Resources/Textures/_Onyx/Wounds/brute_damage.rsi/*`, `burn_damage.rsi/*` (new, 156 files)
5. *(Option B)* `Resources/Prototypes/Entities/Mobs/Species/base.yml` (2-line sprite path swap, in the same
   existing `# WOLFGATE` block per this port's convention of appending to one tracked block)

**Difficulty:** low. Zero shared/server changes, zero new components, zero facade work, two upstream
insertions of 1 and 4 lines respectively (plus a 1-word flag for Option B), one new `_WF` file of 25-95
lines depending on option. The hardest part of P3-4 — the data plumbing and its detach/terminate edge
cases — was already built and bug-fixed in phases 1 and WP9 for a consumer that didn't exist yet.

**Testable headlessly:** yes, for the part that matters. `PartDamageVisualsComponent.Damage` is
`Content.Shared` data written by `Content.Server`-gated logic; an integration test can deal targeted damage
to one limb of a wound host and assert `Comp<PartDamageVisualsComponent>(body).Damage[HumanoidVisualLayers.LArm]`
carries the expected positive `DamageSpecifier` while a sibling layer (e.g. `.RArm`) stays absent/zero — this
exercises exactly the contract the client consumes, per this project's stated preference for logic tests
over UI-driving (project memory: "Integration tests for logic", "Prefer logic tests when user is present").
The client `DamageVisualsSystem` rendering itself (actual sprite pixels) is not headlessly assertable and
does not need to be — it is a thin, low-risk shim over already-battle-tested core SS14 visualizer code with
no new branching logic beyond "read this dict instead of that one."

## 11. Numbers for the user

No new balance numbers — P3-4 is a rendering feature, not a damage-tuning one. P3-6 (amputation thresholds
and finishing damage) is not applicable to this report; it belongs to P3-1. The numbers worth surfacing here:

- **6** body-sprite layers get correct per-limb accuracy immediately (Chest, Head, LArm, RArm, LLeg, RLeg) —
  all already targeted by the existing species `DamageVisuals` block, zero new prototypes needed.
- **0** new texture files for the minimal (Option A) deliverable; **156** files (2 RSIs × 77 states + 2
  `meta.json`) for the full-parity (Option B) extension, same license already in the tree.
- **2** upstream files touched, **1** new `_WF` file, **1-2** new client-side subscription pairs (both
  confirmed collision-free).
- **4** hand/foot layers (`LHand`, `RHand`, `LFoot`, `RFoot`) get wound art only under Option B, and then
  only when severed and lying separately — the live/attached overlay never targets them, in Onyx either.

## 12. Blocker

None. This is the cleanest phase-3 sub-area found: the hard half (server data plumbing) already shipped and
was already hardened against the exact edge cases (mob termination, detach mid-delete) that usually bite
this kind of feature. The one thing that would have shipped a silently-dead handler if ported by rote —
`BodyPartComponent` not raising `AfterAutoHandleStateEvent` in WG (§4.1) — is caught here and is a one-word,
zero-risk fix if Option B is taken, or simply not needed if Option A is taken.
