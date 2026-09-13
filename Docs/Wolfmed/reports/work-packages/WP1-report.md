# WP1 — StatusEffectNew framework (D1) — report

**Agent:** WP1
**Date:** 2026-09-12
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`
**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG below)
**Status:** complete. Both builds green, YAML linter green, headless server starts with zero `[ERRO]`/`[FATL]`.

---

## 1. Files created / modified

### Vendored from Onyx — verbatim (byte-identical `cp`)

| # | Onyx path | WG path |
|---|---|---|
| 1 | `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs` | same |
| 2 | `Content.Shared/StatusEffectNew/Components/StatusEffectContainerComponent.cs` | same |
| 3 | `Content.Shared/StatusEffectNew/Components/StatusEffectAlertComponent.cs` | same |
| 4 | `Content.Shared/StatusEffectNew/Components/CloneableStatusEffectComponent.cs` | same (UTF-8 BOM preserved — verified) |
| 5 | `Content.Shared/StatusEffectNew/Components/ExaminableStatusEffectComponent.cs` | same |
| 6 | `Content.Shared/StatusEffectNew/Components/PermanentStatusEffectsComponent.cs` | same |
| 7 | `Content.Shared/StatusEffectNew/Components/RejuvenateRemovedStatusEffectComponent.cs` | same |
| 8 | `Content.Shared/StatusEffectNew/StatusEffectsSystem.cs` | same |
| 9 | `Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs` | same (Onyx's unused `System.ComponentModel.Design` / `YamlDotNet.Core.Tokens` usings kept; both resolve) |
| 10 | `Content.Shared/StatusEffectNew/StatusEffectAlertSystem.cs` | same |
| 11 | `Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs` | same |

### Vendored from Onyx — modified (`// WOLFGATE` edits, listed in §2)

| # | Onyx path | WG path |
|---|---|---|
| 12 | `Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs` | same |
| 13 | `Content.Shared/StatusEffectNew/ExaminableStatusEffectSystem.cs` | same |
| 14 | `Content.Shared/StatusEffectNew/PermanentStatusEffectsSystem.cs` | same |

### New `_WF` compat code

| # | WG path | Spec |
|---|---|---|
| 15 | `Content.Shared/_WF/Wolfmed/Compat/StatusEffectsSystem.Wolfgate.cs` | PLAN §2.5 |
| 16 | `Content.Shared/_WF/Wolfmed/Compat/EntityPrototypeCompatExtensions.cs` | PLAN §2.6 |
| 17 | `Content.Shared/_WF/Wolfmed/Compat/AlertsSystem.UpdateAlert.cs` | PLAN §2.4 (assigned to WP1 by the WP1 file table, row 16) |

### Resources

| # | WG path | Source |
|---|---|---|
| 18 | `Resources/Prototypes/_Onyx/Entities/categories.yml` | ONYX `Resources/Prototypes/Entities/categories.yml:31-34` — only the `StatusEffects` entityCategory |
| 19 | `Resources/Locale/en-US/_Onyx/entity-categories.ftl` | ONYX `Resources/Locale/en-US/entity-categories.ftl:7` — one key |
| 20 | `Resources/Prototypes/Entities/StatusEffects/misc.yml` | ONYX same path — only `StatusEffectBase`, `MobStatusEffectBase`, `MobStatusEffectDebuff` |

### Docs

| # | WG path |
|---|---|
| 21 | `Docs/Wolfmed/WOLFMED_MANIFEST.md` — created with the §7.1 header, the Onyx pin, one row per file WP1 touched or deliberately skipped, a **Deviations** section and a **Hazards** section |

**Upstream (non-`_Onyx`, non-`_WF`) files modified: zero.** `git status --porcelain` over
`Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests` shows only
untracked additions:

```
?? Content.Shared/StatusEffectNew/
?? Content.Shared/Traits/Assorted/PainNumbnessStatusEffectComponent.cs
?? Content.Shared/_WF/Wolfmed/
?? Docs/
?? Resources/Locale/en-US/_Onyx/
?? Resources/Prototypes/Entities/StatusEffects/
?? Resources/Prototypes/_Onyx/
```

(`Docs/` was already untracked before WP1 — it holds the orchestrator's `DECISIONS.md` /
`WOLFMED_HANDOFF.md` / `WOLFMED_PLAN.md`.)

---

## 2. Every `// WOLFGATE` edit, with reason

### `Content.Shared/StatusEffectNew/StatusEffectSystem.Relay.cs`

1. **Line 2**, `using Content.Shared._Onyx.MartialArts; // <Onyx-MartialArts>` replaced by
   `// WOLFGATE: Onyx's _Onyx.MartialArts is not ported; the melee target modifier relay below is dropped.`
   — the namespace does not exist in Wolfgate (CS0246).
2. **After `using Content.Shared.Damage.Systems;`**, added
   `using Content.Shared.Damage; // WOLFGATE: Wolfgate keeps DamageModifyEvent and ModifySlowOnDamageSpeedEvent in Content.Shared.Damage.`
   — verified: `DamageableSystem.cs:23` and `SlowOnDamageSystem.cs:8` both declare
   `namespace Content.Shared.Damage`. `Content.Shared.Damage.Systems` still exists, so the original
   using stays and the two subscription lines are untouched.
3. **`StandUpAttemptEvent` / `StunEndAttemptEvent` / `RefreshStaminaCritThresholdEvent`** (Onyx :47-49)
   deleted, replaced by
   `// WOLFGATE: StandUpAttemptEvent, StunEndAttemptEvent and RefreshStaminaCritThresholdEvent do not exist in Wolfgate.`
4. **`GetMeleeTargetModifiersEvent`** (Onyx :52) deleted, replaced by
   `// WOLFGATE: the melee target modifier relay belongs to Onyx's MartialArts, which is not ported.`
5. **`EmoteActionEvent` / `AccentGetEvent` / `EmoteEvent`** (Onyx :61-63) deleted, replaced by
   `// WOLFGATE: EmoteActionEvent does not exist; EmoteEvent, AccentGetEvent, VocalSystem and MumbleAccentSystem are server-side in Wolfgate.`
   — verified: `AccentGetEvent` is `Content.Server/Speech/AccentSystem.cs:25`, `EmoteEvent` is
   `Content.Server/Chat/Systems/ChatSystem.Emote.cs:250`, `EmoteActionEvent` is absent everywhere.
6. **`BleedModifierEvent`** (Onyx :65) deleted →
   `// WOLFGATE: BleedModifierEvent does not exist; Wolfgate bleeding is a server-side BleedAmount.`
7. **`RefreshPressureImmunityEvent`** (Onyx :67) deleted →
   `// WOLFGATE: RefreshPressureImmunityEvent does not exist in Wolfgate.`
8. **`SelfBeforeInjectEvent`** (Onyx :69) deleted →
   `// WOLFGATE: SelfBeforeInjectEvent does not exist in Wolfgate.`
9. **`CatchAttemptEvent`** (Onyx :71) deleted →
   `// WOLFGATE: CatchAttemptEvent does not exist in Wolfgate.`

   Kept verbatim per the plan: `GetBlurEvent` (:51) — `Content.Shared/Eye/Blinding/Systems/BlurryVisionSystem.cs:56`
   — and every other subscription. All 19 remaining relayed event types were individually grepped and
   confirmed to exist in Wolfgate, with the Onyx `using` list already covering every namespace.

### `Content.Shared/StatusEffectNew/ExaminableStatusEffectSystem.cs`

10. `[SubscribeLocalEvent]` on `OnExaminedEvent` replaced by an explicit `Initialize()` prefixed with
    `// WOLFGATE: RT 277 has no [SubscribeLocalEvent] source generator; subscribe explicitly.`
    Same pair, `<ExaminableStatusEffectComponent, StatusEffectRelayedEvent<ExaminedEvent>>`.

### `Content.Shared/StatusEffectNew/PermanentStatusEffectsSystem.cs`

11. The three `[SubscribeLocalEvent]` attributes (Onyx :16 `ComponentInit`, :26 `MapInit`, :35
    `ComponentRemove` — actual lines :77, :87, :96 at the pin) removed and one `Initialize()` inserted
    after the `[Dependency]` line, same `// WOLFGATE` comment. The `// <Onyx-OrganEffects>` …
    `// </Onyx-OrganEffects>` block is intact.

### Resources

12. `Resources/Prototypes/_Onyx/Entities/categories.yml` — leading
    `# WOLFGATE: only the StatusEffects entityCategory from Onyx's Resources/Prototypes/Entities/categories.yml.`
13. `Resources/Locale/en-US/_Onyx/entity-categories.ftl` — leading `# WOLFGATE`.
14. `Resources/Prototypes/Entities/StatusEffects/misc.yml` — trailing
    `# WOLFGATE: MobStandStatusEffectBase and every concrete status effect entity in Onyx's file are dropped: …`

No `// WOLFGATE` edits were needed in any other vendored file.

---

## 3. Deviations from PLAN.md

Only one, and it is inside the latitude the plan gives.

1. **`MobStandStatusEffectBase` is NOT ported.** PLAN WP1's prototype table says
   "*only the abstract bases `StatusEffectBase`, `MobStatusEffectBase`, `MobStatusEffectDebuff`
   (and optionally `MobStandStatusEffectBase`)*". I took the "optionally" as no: its blacklist
   references the tag `KnockdownImmune`, which returns zero hits across `WG/Resources/Prototypes`.
   A missing tag id is a YAML-linter error, and the prototype has no consumer in WP1. Recorded in
   the manifest's Deviations section. Re-add it together with whatever WP lands Wolfgate's
   knockdown-immunity tag.

Everything else followed the plan exactly. Notable points where I re-verified rather than assumed:

- **§2.4's `AlertsSystem.UpdateAlert` was checked against the real Wolfgate types before writing.**
  `AlertsSystem` is `public abstract partial class` (`Content.Shared/Alert/AlertsSystem.cs:9`),
  `_timing` is a private `IGameTiming` at `:11`, `IGameTiming.ApplyingState` exists
  (`RobustToolbox/Robust.Shared/Timing/IGameTiming.cs:195`), `TryGetAlertState` is at `:60`,
  `ShowAlert(EntityUid, ProtoId<AlertPrototype>, short?, (TimeSpan, TimeSpan)?, bool, bool)` at `:81`,
  `TryGet` at `:306`, `AlertPrototype.AlertKey` at `AlertPrototype.cs:57`, and
  `AlertState.Cooldown` is an **unnamed** `(TimeSpan, TimeSpan)?` — so `.Item1`/`.Item2`, as the plan
  says. No pre-existing `UpdateAlert` exists in `Content.Shared/Alert`, so the partial adds, not hides.
- **RT 277 API surface used by the verbatim files is all present:** `PredictedQueueDel`
  (`EntitySystem.Proxy.cs:852-892`), `PredictedTrySpawnInContainer` (`:1019`), `DirtyField`
  (`:158-183`), `Factory` (`EntitySystem.cs:32`), `[Dependency] EntityQuery<T>` injection
  (`EntitySystemManager.cs:204`), `EntityQuery.Resolve(EntityUid, ref T?, bool)`
  (`EntityManager.Components.cs`), `IPrototypeManager.Resolve(EntProtoId, out EntityPrototype?)`
  (`IPrototypeManager.cs:176`). Only `ProtoMan` and `EntityPrototype.TryComp` were missing — both
  covered by §2.5 / §2.6 as planned.
- **Component-registration names are all free.** `StatusEffect`, `StatusEffectContainer`,
  `StatusEffectAlert`, `CloneableStatusEffect`, `ExaminableStatusEffect`, `PermanentStatusEffects`,
  `RejuvenateRemovedStatusEffect`, `PainNumbnessStatusEffect`: each class name returns exactly one
  hit across `Content.Shared`/`Content.Server`/`Content.Client`, and no `[ComponentProtoName]`
  override in Wolfgate claims any of those strings.
- **No directed subscription conflicts.** The 9 pairs WP1 registers are all on new components
  (PLAN §5.2 last row). Confirmed empirically: the headless server reached
  `Server Version 277.0.0.0 -> Ready` with no `Duplicate Subscriptions` throw.

---

## 4. Build and validation output

### `dotnet build Content.Server/Content.Server.csproj -c DebugOpt`

```
===== Content.Server =====
Build succeeded.
    0 Error(s)
```

### `dotnet build Content.Client/Content.Client.csproj -c DebugOpt`

```
===== Content.Client =====
Build succeeded.
    0 Error(s)
```

(Filter: `grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"`. Both were green on the first
attempt; no iteration was needed. A `-v n` build of `Content.Shared` produces **zero** warnings
mentioning `StatusEffectNew`, `Wolfmed` or `PainNumbnessStatus` — in particular no CS0108 from the
`ProtoMan` partial.)

### YAML linter, Release (the documented final gate)

```
No errors found in 75638 ms.
```

### Headless server start — prototype sanity

`bin/Content.Server/Content.Server.exe --cvar net.port=1291 --cvar game.lobbyenabled=false`, run to a
240 s timeout:

```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1291: True"
```

Zero `[ERRO]`, zero `[FATL]`. The only `[WARN]` lines are the pre-existing `db.ef` sqlite-migration
warnings, the `PullingSystem` command-bind warning, the duplicate-emote-word warnings and
`MainLoop: Cannot keep up!` — none new.

### Prototype component check (as required by the WP brief)

Every `- type: X` in the two copied prototype files resolves to a registered component class in WG:

| YAML `type:` | Resolves to |
|---|---|
| `StatusEffect` | `Content.Shared/StatusEffectNew/Components/StatusEffectComponent.cs` |
| `RejuvenateRemovedStatusEffect` | `Content.Shared/StatusEffectNew/Components/RejuvenateRemovedStatusEffectComponent.cs` |
| `Sprite` | `RobustToolbox/Robust.Client/GameObjects/Components/Renderable/SpriteComponent.cs` |
| `Tag` | `Content.Shared/Tag/TagComponent.cs` |

Non-component references also verified: tag `HideContextMenu` (`Resources/Prototypes/tags.yml:681`),
`drawdepth: Effects` (`Content.Shared/DrawDepth/DrawDepth.cs:114`), `MobState` whitelist component
(`Content.Shared/Mobs/Components/MobStateComponent.cs`), `entityCategory` prototype kind. Every
parent (`StatusEffectBase`, `MobStatusEffectBase`) is defined in the same file. Duplicate-(kind,id)
scan over `Resources/Prototypes` returns exactly one definition for each of `StatusEffectBase`,
`MobStatusEffectBase`, `MobStatusEffectDebuff` and the `StatusEffects` entityCategory.

**Nothing had to be removed or `# WOLFGATE`-commented after the fact** — the only prototypes that
would have failed (`MobStandStatusEffectBase` and all 14 concrete entries) were never copied in the
first place, and each exclusion is listed in the manifest with its missing component.

### Line-ending / BOM audit

All 20 touched text files are pure CRLF (`lf == crlf`, zero `\r\r`), matching the working tree's
`text=auto` convention. `CloneableStatusEffectComponent.cs` keeps its UTF-8 BOM.

---

## 5. What a later WP must know

### New public symbols now available

Namespace **`Content.Shared.StatusEffectNew`**:

- `StatusEffectsSystem` (sealed partial) — `TryAddStatusEffectDuration`, `TrySetStatusEffectDuration`,
  `TryUpdateStatusEffectDuration`, `TryRemoveStatusEffect`, `HasStatusEffect`, `TryGetStatusEffect`,
  `TryGetTime`, `TryGetMaxTime<T>`, `TryAddTime`, `TryRemoveTime`, `TrySetTime`, `HasEffectComp<T>`,
  `TryEffectsWithComp<T>`, `TryGetEffectsEndTimeWithComp<T>`, `EnumerateStatusEffects` (3 overloads),
  `CanAddStatusEffect`, `RelayEvent<T>` (ref and class overloads).
- Events: `StatusEffectAppliedEvent`, `StatusEffectRemovedEvent`, `BeforeStatusEffectAddedEvent`,
  `StatusEffectEndTimeUpdatedEvent`, `StatusEffectStartTimeUpdatedEvent`,
  `StatusEffectRelayedEvent<TEvent>` — all `[ByRefEvent]`.
- Systems: `StatusEffectAlertSystem`, `ExaminableStatusEffectSystem`, `PermanentStatusEffectsSystem`.
- `EntityPrototypeCompatExtensions.TryComp<T>(this EntityPrototype, out T?, IComponentFactory)`.

Namespace **`Content.Shared.StatusEffectNew.Components`**: the seven components listed in §1.

Namespace **`Content.Shared.Alert`**: `AlertsSystem.UpdateAlert(EntityUid, ProtoId<AlertPrototype>,
short? = null, TimeSpan? = null, bool = false, bool = true)` — **new, available to every WP.**
WP2 must not re-declare it.

Namespace **`Content.Shared.Traits.Assorted`**: `PainNumbnessStatusEffectComponent` (registers as
`PainNumbnessStatusEffect`; distinct from Wolfgate's existing `PainNumbnessComponent`/`PainNumbness`).

Prototypes: abstract entities `StatusEffectBase`, `MobStatusEffectBase`, `MobStatusEffectDebuff`;
entityCategory `StatusEffects`; locale key `entity-category-name-status-effects`.

### Changed signatures

None. Nothing existing was modified.

### Hard constraints for later WPs

- **`CS0104` hazard:** two types are now named `StatusEffectsSystem` —
  `Content.Shared.StatusEffect.StatusEffectsSystem` (the old one, still driving all shipping content)
  and `Content.Shared.StatusEffectNew.StatusEffectsSystem`. Never import both namespaces in one file;
  alias if you must. This is also documented in the manifest's Hazards section.
- **Do not add a second partial declaring `ProtoMan` on `StatusEffectsSystem`** — WP1 owns
  `Content.Shared/_WF/Wolfmed/Compat/StatusEffectsSystem.Wolfgate.cs`. If Wolfgate ever takes an RT
  that adds `EntitySystem.ProtoMan`, that file starts emitting CS0108 and should be deleted; same for
  `EntityPrototypeCompatExtensions.cs` if `EntityPrototype.TryComp` appears upstream.
- **WP2 must not re-create `AlertsSystem.UpdateAlert.cs`** (PLAN §2.4). It shipped here, per the WP1
  file table.

### Open TODOs this WP leaves behind

1. **11 relay events are dropped** (§2 items 3-9). A status effect entity cannot react to stand-up,
   stun-end, stamina-crit-threshold, emotes/accents, bleed modifiers, pressure immunity, self-inject
   or catch attempts on its wearer. If a later WP ports a wound status effect that needs one of
   these, the relay line has to be re-added *and* the event type authored in Wolfgate first.
   `EmoteEvent`/`AccentGetEvent` in particular would need to move to `Content.Shared` before the
   relay can subscribe them.
2. **No concrete status-effect prototype exists yet.** `Resources/Prototypes/Entities/StatusEffects/`
   holds only the three abstract bases. Every Onyx file in that directory
   (`body.yml`, `clumsy.yml`, `damage.yml`, `movement.yml`, `speech.yml`, `traits.yml`,
   `weather.yml`) is skipped, listed in the manifest. Relevant to WP4: Onyx's
   `PainNumbnessStatusEffectBase` lives in `damage.yml` and `TraitStatusEffectPainNumbness` in
   `traits.yml`, so `PainSystem`'s numbness check will compile and run but will never match until a
   WP lands those prototypes. That is silent, not an error — plan for it.
3. **`MobStandStatusEffectBase`** needs the `KnockdownImmune` tag before it can be ported (§3).
4. **`Docs/Wolfmed/WOLFMED_MANIFEST.md` is now the shared file every WP appends to.** Add rows, do
   not rewrite the header or the Onyx pin line.
