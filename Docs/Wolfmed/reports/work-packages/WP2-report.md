# WP2 — Compat core — report

**Branch:** `clanker/wolfmed-port-orchestration-454c3d` in `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG).
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`.
**Both builds green.** Nothing committed.

---

## 1. Files created / modified

| # | Path (relative to WG) | Status | Spec |
|---|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs` | new | §2.1, D12, D19, D23, D27, D30, GUARD F |
| 2 | `Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Wolfmed.cs` | new | §2.2 |
| 3 | `Content.Shared/_WF/Wolfmed/Compat/DamageDealtEvent.cs` | new | §2.3, D11 |
| 4 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBodySystem.cs` | new | §2.7 |
| 5 | `Content.Shared/_WF/Wolfmed/Compat/StunSystemOnyxCompat.cs` | new | §2.8 |
| 6 | `Content.Shared/_WF/Wolfmed/Compat/SharedChatSystem.Wolfmed.cs` | new | §2.9, D25 |
| 7 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBedHealMarkerComponent.cs` | new | §2.10 |
| 8 | `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | new | §2.10 |
| 9 | `Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs` | new | §2.11 (WP2 half) |
| 10 | `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` | **hook (upstream)** | HOOK 5 / §2.14 |
| 11 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified | §7 — 10 rows + a WP2 Deviations section + 2 hazards |

**Not done in WP2 — already delivered by WP1** (checked before starting, per the task's instruction):
`Content.Shared/_WF/Wolfmed/Compat/AlertsSystem.UpdateAlert.cs` (§2.4),
`StatusEffectsSystem.Wolfgate.cs` (§2.5), `EntityPrototypeCompatExtensions.cs` (§2.6). All three are
present, correct against their §2 spec (I read `AlertsSystem.UpdateAlert.cs` and confirmed the
unnamed-tuple `.Item1`/`.Item2` handling) and already recorded in the manifest as WP1 rows.

**Not done in WP2 — correctly assigned elsewhere:** GUARD D and GUARD D2 (the `DamageableSystem`
routing seam and the three `BeforeDamageChangedEvent` members) are **WP5** per §3 and are not touched.
`WolfmedBodyPartLifecycleSystem` is **WP5** per D28 and §2.11. `Content.Shared/Damage/Systems/DamageableSystem.cs`
was not opened for edit.

Diff scope confirmation:

```
$ git -C WG diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests
 Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs | 4 ++++
 1 file changed, 4 insertions(+)
```

Exactly one tracked upstream file touched, as WP2's checkpoint demands. Everything else is new
untracked files under `_WF/Wolfmed/Compat` plus the untracked `Docs/` tree WP1 created.

---

## 2. Every `// WOLFGATE` edit and its reason

There is exactly **one** upstream `// WOLFGATE` edit in WP2.

**`Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs`** — appended to the class body (HOOK 5):

```csharp
    // WOLFGATE: Wolfmed snapshot/targeting needs a single-bit check; copied from Onyx's SharedTargetingSystem.
    public static bool IsSelectable(TargetBodyPart part)
        => part != 0 && (part & (part - 1)) == 0 && (part & TargetBodyPart.All) != 0;
```

Byte-for-byte the expression from `ONYX Content.Shared/_Onyx/Targeting/SharedTargetingSystem.cs:53`,
reformatted onto two lines. D10 skips Onyx's whole Targeting stack, so `TargetingSnapshotSystem.cs:23,40`
(WP3) and WP5's `targetPart` handoff need this one static on Shitmed's type instead.
`TargetBodyPart.All` includes `Groin`, so `Groin` stays selectable — required by D9's folding rule.

Two further `// WOLFGATE`-marked comments live inside **new `_WF` files**, not upstream, and are there
to make a deliberate divergence from Onyx legible at re-sync time:

- `WolfmedDamageableSystem.SetDamage` — `// WOLFGATE (D30): Onyx removes types missing from 'damage';
  Wolfgate zeroes them so DamageableInit's container seeding survives and TryChangeDamage:256-257 keeps working.`
- `WolfmedDamageableSystem.CanBeDamagedBy` — `// WOLFGATE (D19): Onyx reads InjurableComponent.DamageContainer;
  that component is not ported, so this mirrors DamageableInit's seeding loop over DamageableComponent.DamageContainerID.`

---

## 3. Deviations from PLAN.md, with justification

### D-WP2-1 — `OnyxBodyEvents.cs` carries `[ByRefEvent]`; §2.11's snippet omits it

Onyx declares both events `[ByRefEvent]` (`ONYX Content.Shared/Body/BodyComponent.cs:36-46`), and the
only wound-set consumer takes them `ref`:

```
ONYX Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs:34  SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnPartChanged);
ONYX Content.Shared/_Onyx/Wounds/FractureEffectsSystem.cs:59  private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotInsertedEvent args)
```

RT refuses a `ref` directed subscription for an event type without the attribute. Adding it is the
minimal fix and restores Onyx's exact shape; the plan's snippet was simply abbreviated. Namespace
(`Content.Shared.Body`), record-struct shape and member name (`Target`) are exactly as §2.11 specifies.

### D-WP2-2 — the damage dictionary is written through a local, not `ent.Comp.Damage.DamageDict[...]`

§2.1's `SetDamage` body writes `ent.Comp.Damage.DamageDict[type] = …` directly. That is a hard build
error from any system other than `DamageableSystem`:

```
error RA0002: Tried to perform a 'Write' other-type access to member 'Damage' in type
'DamageableComponent', despite only having 'ReadExecute' access. Type Permissions: rwxrwxr-x.
```

`DamageableComponent` is `[Access(typeof(DamageableSystem), Other = AccessPermissions.ReadExecute)]`
(`WG/Content.Shared/Damage/Components/DamageableComponent.cs:20`). Fix: hoist the dictionary into a
local first (`var dict = ent.Comp.Damage.DamageDict;`), which is a *read* of `Damage` plus mutation of
the dictionary object it already references — behaviourally identical, and the exact pattern
`DamageableSystem.TryChangeDamage` itself uses at `WG/Content.Shared/Damage/Systems/DamageableSystem.cs:253`.
Applied in `SetDamage` and `SetAllDamage`. **Later WPs writing a component's damage from `_WF` or
`_Onyx` code will hit the same analyzer and need the same hoist.**

### D-WP2-3 — `HealEvenly` / `HealDistributed` are implemented, not stubbed

PLAN.md WP2 #1 permits `NotImplementedException` until WP11; the task brief forbids placeholder bodies.
Both are ported straight from `ONYX Content.Shared/Damage/Systems/DamageableSystem.API.cs:177-272`
(including the `+ Epsilon * (count - 1)` round-up that guarantees the `HealEvenly` loop terminates)
and route through this facade's own `ChangeDamage`. Nothing about phase 4 is pre-empted; the only
effect is that a WP11 stub can no longer ship by accident.

### D-WP2-4 — two facade methods beyond §2.1's listed surface

`SetAllDamage(Entity<DamageableComponent?>, FixedPoint2)` and
`TryGetDamageGreaterThan(Entity<DamageableComponent>, FixedPoint2, out DamageSpecifier, ProtoId<DamageGroupPrototype>?)`
are added. Both are real Onyx members (`DamageableSystem.API.cs:379`, `:486`) that `ClearAllDamage`,
`HealEvenly` and `HealDistributed` call internally — §2.1 lists the externally-consumed surface only,
and `ClearAllDamage` cannot be written without one of them. `SetAllDamage` reports an **empty** delta
rather than GUARD F's real delta, matching Onyx: setting all damage to a flat value is not "dealing"
damage in either fork. GUARD F's real-delta path is `SetDamage` alone, which is what `RefreshBodyDamage`
calls and what §8.3 trap 5 / T12 gate.

### D-WP2-5 — the compile-exercise helper is not shipped

To satisfy "every facade method must be exercised by at least a compile-time reference", I wrote
`Content.Shared/_WF/Wolfmed/Compat/ZZ_WolfmedCompatSmoke.cs` — an `EntitySystem` that calls every public
member of `WolfmedDamageableSystem` (including the `out var` + named-argument form Onyx's routing uses),
both `DamageSpecifier` statics plus `Clone()`, `WolfmedBodySystem.TryDetachPart`,
`TryUpdateParalyzeDuration`, `AlertsSystem.UpdateAlert`, `SharedChatSystem.TryEmoteWithChat`,
`SharedTargetingSystem.IsSelectable`, `HasComp<WolfmedBedHealMarkerComponent>`, and a `ref` raise of
`DamageDealtEvent` / `OrganGotInsertedEvent` / `OrganGotRemovedEvent`. **Both builds were green with it
in the tree**, so every signature is proven to bind. It was then deleted rather than shipped, because
RT auto-registers every `EntitySystem` subclass in a content assembly and a permanently-instantiated
no-op system that raises events is worse than no dead code at all.

The file is preserved verbatim at `C:/tmp/wolfmed-plan/wp/WP2-compat-smoke.cs.txt`. **WP9 should fold
it into `Content.IntegrationTests/Tests/_Onyx/Wounds` as a compile gate**, where it costs nothing at
runtime.

### Non-deviations worth recording

- `DamageDealtEvent` is `record struct`, **not** `readonly record struct` — §2.3 and §8.2 require this
  (GUARD D's handler clears `Damage`'s dict, and WP5's router mutates it).
- `ChangeDamage` forwards exactly §2.1's argument list, including `canSever: false, canEvade: false,
  partMultiplier: 1f, targetPart: null`. Consequence to know: every routed write raises
  `DamageChangedEvent` with `CanSever == false`, belt-and-braces on top of GUARD B (WP5).
- `CanBeDamagedBy` is the D19 re-basing onto `DamageableComponent.DamageContainerID`, checking
  `SupportedTypes` **and** every `DamageTypes` entry of every `SupportedGroups` group — i.e. the same
  union `DamageableInit` seeds (`DamageableSystem.cs:103-121`). A null/unresolvable container returns
  `true` ("supports everything"), a missing `DamageableComponent` returns `false`, matching Onyx's
  `Resolve(..., false)`. §8.2's consequence stands: `Structural`, `Holy` and anything outside
  `Biological`'s groups will be dropped from systemic damage by `RefreshBodyDamage`.
- `StunSystemOnyxCompat.TryUpdateParalyzeDuration` keeps the unused `visualized` parameter so Onyx call
  sites stay verbatim, and uses `refresh: false`. §8.2 already records the VFX-retrigger deviation.
- `SharedChatSystem.TryEmoteWithChat`'s parameter names, order and defaults were copied character-for-character
  from `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:59-68`, so HOOK 6's one-word `override` will bind.
- Line endings: all new files are CRLF, matching the working tree (`.gitattributes text=auto`). No file
  has mixed or doubled terminators. The manifest was LF before WP2 and is still LF.

---

## 4. Build output tails

### `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`

```
C:\...\Content.Server\GameTicking\GameTicker.Spawning.cs(279,17): warning RA0045: Use the proxy method AddComp instead of calling EntityManager.AddComponent directly [C:\...\Content.Server\Content.Server.csproj]
    1337 Warning(s)
    0 Error(s)

Time Elapsed 00:00:13.26
```

Filtered form required by the checkpoint:

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

### `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`

```
C:\...\Content.Client\_NF\Emp\Overlays\EmpBlastOverlay.cs(31,68): warning RA0033: The id parameter of Index forbids literal values [C:\...\Content.Client\Content.Client.csproj]
    1085 Warning(s)
    0 Error(s)

Time Elapsed 00:00:08.08
```

Filtered form:

```
$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error" | head -60
Build succeeded.
    0 Error(s)
```

The warning counts are the pre-existing baseline; no new warning originates in any WP2 file (the two
tail lines above are upstream `_NF` / `GameTicking` files).

---

## 5. What later WPs must know

### New public symbols

**`Content.Shared._WF.Wolfmed.Compat.WolfmedDamageableSystem`** (sealed `EntitySystem`):

```csharp
float          UniversalTopicalsHealModifier { get; }
DamageSpecifier ChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage,
                   bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null,
                   bool ignoreGlobalModifiers = false, float armorPenetration = 0f, EntityUid? tool = null,
                   DamageableSystem.DamageOriginFlag? originFlag = null);
bool           TryChangeDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage, out DamageSpecifier newDamage,
                   bool ignoreResistances = false, bool interruptsDoAfters = true, EntityUid? origin = null,
                   bool ignoreGlobalModifiers = false, float armorPenetration = 0f, EntityUid? tool = null,
                   DamageableSystem.DamageOriginFlag? originFlag = null);
DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent);
DamageSpecifier GetPositiveDamage(Entity<DamageableComponent> ent, ProtoId<DamageGroupPrototype> group);
DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent);
FixedPoint2    GetTotalDamage(Entity<DamageableComponent?> ent);
void           SetDamage(Entity<DamageableComponent?> ent, DamageSpecifier damage);   // GUARD F real delta
void           ClearAllDamage(Entity<DamageableComponent?> ent);
void           SetAllDamage(Entity<DamageableComponent?> ent, FixedPoint2 newValue);  // empty delta
bool           CanBeDamagedBy(Entity<DamageableComponent?> ent, ProtoId<DamageTypePrototype> type);
bool           TryGetDamageGreaterThan(Entity<DamageableComponent> ent, FixedPoint2 amount,
                   out DamageSpecifier damage, ProtoId<DamageGroupPrototype>? group = null);
DamageSpecifier HealEvenly(Entity<DamageableComponent?> ent, FixedPoint2 amount,
                   ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null);
DamageSpecifier HealDistributed(Entity<DamageableComponent?> ent, FixedPoint2 amount,
                   ProtoId<DamageGroupPrototype>? group = null, EntityUid? origin = null);
```

There is **no** no-`out` `bool TryChangeDamage`. `ONYX ReagentTreatmentSystems.cs:19` (phase 4) needs the
`// WOLFGATE` rewrite to `ChangeDamage(...)` that damage-bridge.md §7.2 prescribes.

Other new symbols:

| Symbol | Namespace | File |
|---|---|---|
| `DamageSpecifier.Clone()`, `DamageSpecifier.GetPositive`, `DamageSpecifier.GetNegative` | `Content.Shared.Damage` | `DamageSpecifier.Wolfmed.cs` |
| `DamageDealtEvent(DamageSpecifier Damage, EntityUid? Origin, bool InterruptsDoAfters)` `[ByRefEvent] record struct` | `Content.Shared.Damage.Systems` | `DamageDealtEvent.cs` |
| `WolfmedBodySystem.TryDetachPart(EntityUid part, bool reparent = true)` | `Content.Shared._WF.Wolfmed.Compat` | `WolfmedBodySystem.cs` |
| `SharedStunSystem.TryUpdateParalyzeDuration(EntityUid, TimeSpan?, bool visualized = false)` (extension) | `Content.Shared.Stunnable` | `StunSystemOnyxCompat.cs` |
| `SharedChatSystem.TryEmoteWithChat(EntityUid, string, ChatTransmitRange, bool, string?, bool, bool)` `virtual void` | `Content.Shared.Chat` | `SharedChatSystem.Wolfmed.cs` |
| `WolfmedBedHealMarkerComponent` (registers as `WolfmedBedHealMarker`) | `Content.Shared._WF.Wolfmed.Compat` | `WolfmedBedHealMarkerComponent.cs` |
| `OrganGotInsertedEvent(EntityUid Target)`, `OrganGotRemovedEvent(EntityUid Target)` `[ByRefEvent]` | `Content.Shared.Body` | `OnyxBodyEvents.cs` |
| `SharedTargetingSystem.IsSelectable(TargetBodyPart)` static | `Content.Shared._Shitmed.Targeting` | upstream, HOOK 5 |

### Directed subscriptions WP2 registered

Exactly one: `<HealOnBuckleComponent, ComponentStartup>` in `WolfmedBedHealMarkerSystem`. §5.2's
"free" verdict re-confirmed by grep at implementation time — `WG/Content.Server/Bed/BedSystem.cs:35-36`
holds only `StrappedEvent`/`UnstrappedEvent` on that component. **No other pair is taken by WP2**;
`<WoundableComponent, DamageChangedEvent>` and everything in §5.2 remain unclaimed.

### Open TODOs handed forward

1. **WP4 must land HOOK 6** — `WG/Content.Server/Chat/Systems/ChatSystem.Emote.cs:60`, `public void` →
   `public override void`. Without it the shared virtual is a silent no-op on the server too and
   `PainSystem.cs:297` never screams. `:85` (the `EmotePrototype` overload) must stay untouched.
2. **WP5 owns GUARD D / GUARD D2** — `DamageDealtEvent` exists and is raised nowhere yet; nothing routes.
   `BeforeDamageChangedEvent` is still Wolfgate's 5-member shape.
3. **WP5 owns `WolfmedBodyPartLifecycleSystem`** (D28, §2.11 second half). `OrganGotInsertedEvent` /
   `OrganGotRemovedEvent` currently have **no raiser** anywhere in WG — they compile and nothing sends
   them. §8.3 trap 2 is still live.
4. **Every vendored `_Onyx` file gets the D12 dependency swap**, one `// WOLFGATE` line:
   `[Dependency] private WolfmedDamageableSystem _damage = default!; // WOLFGATE: Onyx-shaped damage API; see _WF/Wolfmed/Compat`
   in `WoundDamageRoutingSystem.cs`, `WoundDamageProjectionSystem.cs`, `WoundFractureSystem.cs`,
   `WoundHealingSystem.cs`, `AmputationSystem.cs`, later `HealthExaminableSystem.PartStatus.cs`.
   `_Onyx/Mobs/Systems/MobThresholdSystem.cs` gets an *addition*, not a swap (WP5 #3).
5. **`WoundDamageRoutingSystem.cs:3,952` needs the §2.10 bed-marker swap** —
   `using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE` and
   `else if (origin is { } bed && HasComp<WolfmedBedHealMarkerComponent>(bed)) // WOLFGATE`.
6. **The RA0002 analyzer will bite any later WP** that writes another entity's `[Access]`-restricted
   component field from `_WF`/`_Onyx` code. `DamageableComponent`, and likely others, are
   `Other = ReadExecute`. Hoist to a local, or the build fails loudly (it does not silently pass).
7. **WP9: adopt `C:/tmp/wolfmed-plan/wp/WP2-compat-smoke.cs.txt`** as the facade's compile gate so a
   later signature change cannot silently orphan a vendored call site.
8. `WolfmedBedHealMarkerComponent` has no YAML and no client-side registration beyond auto-registration;
   it is `[NetworkedComponent]` with no state, which is correct — presence is the whole payload.
