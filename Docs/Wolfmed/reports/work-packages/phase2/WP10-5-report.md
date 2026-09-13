# WP10-5 — Pain sounds + mob wiring (phase 2, group F2)

**Worktree:** `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c` (WG), branch
`clanker/wolfmed-port-orchestration-454c3d`, uncommitted.
**Onyx pin:** `2f5bab9946539cbe083010c9ae6fbc59b47ae377`, sparse reference at `C:/tmp/onyx`.
**Scope implemented:** PLAN2 §4/WP10-5 in full — the vendored `EmoteOnDamageSystem.PainSounds.cs`, HOOK 17,
HOOK 18, and the two `base.yml` mob-wiring blocks. No other WP's files were touched.

---

## 1. Files created / modified

| # | Path (absolute) | Status |
|---|---|---|
| 1 | `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c/Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` | **new (vendored, modified)** — Onyx bytes copied via `git show HEAD:Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs`, then 5 `// WOLFGATE` edits |
| 2 | `.../Content.Server/Chat/EmoteOnDamageComponent.cs` | **modified (upstream hook)** — HOOK 17, purely additive |
| 3 | `.../Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` | **modified (upstream hook)** — HOOK 18, one line |
| 4 | `.../Resources/Prototypes/Entities/Mobs/Species/base.yml` | **modified** — two blocks appended to the existing phase-1 `# WOLFGATE` block on `BaseMobSpeciesOrganic` |
| 5 | `.../Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 5 rows appended to the main table, a phase-2 note added to the species-exclusion paragraph, and a `### WP10-5` Deviations section added before `## Hazards` |

No audio and no locale files were added. `Scream` (`Resources/Prototypes/Voice/speech_emotes.yml:3`) and
`Crying` (`:110`) are existing upstream `Vocal`-category emote prototypes; their audio resolves through each
species' own `VocalComponent` `EmoteSounds` set, so there was nothing to copy and no
`meta.json` / `attributions.yml` licence entry to add. Verified by reading both prototypes and by
`grep -rn "type: EmoteOnDamage" Resources/Prototypes/` → the only hit is the new `base.yml` block.

Line endings: every touched file was read and rewritten in its own existing mode (the four code/YAML files are
CRLF, the manifest was already LF in the working tree from a prior WP). Verified byte-wise: 0 lone CR, 0 lone LF
in the CRLF files.

---

## 2. Every `// WOLFGATE` edit and its reason

### File 1 — `Content.Server/_Onyx/Chat/EmoteOnDamageSystem.PainSounds.cs` (vendored, 5 edits)

Onyx header: the file has none — none was invented. Namespace kept as Onyx wrote it
(`Content.Server.Chat.Systems`), file path under `_Onyx` — exactly D13's pattern, because the class is a
`partial` of upstream `EmoteOnDamageSystem`.

1. `+ using Content.Shared._Onyx.Wounds; // WOLFGATE: P2-D22 wound-host gate.`
   Needed by edit 4. **This is a correction to PLAN2**, which states twice that
   `using Content.Shared._Onyx.Wounds;` "is already line 1 of that file" and that the guard "costs no import".
   It is not: Onyx's file opens with `using Content.Shared.Chat;` and never imports the wounds namespace.
2. `+ using Content.Shared._WF.Wolfmed.Compat; // WOLFGATE: D12 damage facade.` — PLAN2 §3 edit (1).
3. `[Dependency] private DamageableSystem _damageable = default!;` →
   `[Dependency] private WolfmedDamageableSystem _damageable = default!; // WOLFGATE: D12, vendored files bind the facade, never DamageableSystem.`
   The call site `_damageable.GetTotalDamage(uid).Float()` compiles unchanged, as PLAN2 predicted: the facade's
   overload is `GetTotalDamage(Entity<DamageableComponent?>)` and RT's `Entity<T>` has an implicit
   `EntityUid` operator. Not "fixed" to `Comp<DamageableComponent>(uid).TotalDamage`.
4. P2-D22 wound-host gate, first statement of `HandlePainDamageEmote`:
   ```csharp
   // WOLFGATE (P2-D22): D32 strips only WoundHostComponent from Protogen, so without this gate the
   // D32-excluded species would still get Wolfmed's pain screams - a D2 breach. Onyx needs no such
   // check because every mob there is a wound host.
   if (!HasComp<WoundHostComponent>(uid))
       return;
   ```
5. `HasComp<PainNumbnessComponent>(uid)) // WOLFGATE: P2-D8 parity - Wolfgate's PainNumbness trait grants the legacy component.`
   appended to the existing bail-out chain, next to
   `_statusEffects.TryEffectsWithComp<PainNumbnessStatusEffectComponent>(uid, out _)`. PLAN2 §4/WP10-5 leaves
   this as an explicit choice ("consider adding the same clause here for consistency, or record the
   asymmetry"); the clause was added — see deviation D3 below.

PLAN2's fourth listed edit (Onyx's `ProtoMan` → the injected `IPrototypeManager`) is a **confirmed no-op**: the
file never references `ProtoMan`. Every other Onyx `using` resolves in WG unchanged — `Content.Shared.Damage`,
`.Damage.Components`, `.Damage.Systems`, `.Damage.Events`, `.Mobs.Components`, `.Traits.Assorted`,
`.StatusEffectNew` all verified present, and `StatusEffectsSystem.TryEffectsWithComp<T>` resolves at
`Content.Shared/StatusEffectNew/StatusEffectSystem.API.cs:357`.

**Registers no subscription.** The `_gameTiming` / `_random` / `_chatSystem` dependencies come from the
upstream partial; only `_statusEffects` and `_damageable` are declared here — no collision.

### File 2 — `Content.Server/Chat/EmoteOnDamageComponent.cs` (HOOK 17)

- `+ using Robust.Shared.Prototypes; // WOLFGATE: HOOK 17, ProtoId<> for the Wolfmed pain-sound thresholds.`
- One marked comment block introducing four **purely additive** datafields, with WG's explicit-name house
  style and `/// <summary>` one-liners:
  `[DataField("emotesThreshold")] Dictionary<float, HashSet<ProtoId<EmotePrototype>>> EmotesThreshold`,
  `[DataField("allowedDamageType")] HashSet<string> AllowedDamageType = ["Blunt","Caustic","Heat","Cold","Piercing","Shock","Slash"]`,
  `[DataField("painThreshold")] float PainThreshold = 6f`, `[ViewVariables] float LastTotalDamage`.
- **`Emotes` is untouched.** Onyx *replaces* it; replacing it here would silently change
  `ZombieSystem.cs:181` and `ZombieSystem.Transform.cs:145` (`AddEmote(uid, "Scream")`). The existing
  `[Access(typeof(EmoteOnDamageSystem))]` already covers the new fields.

### File 3 — `Content.Server/Chat/Systems/EmoteOnDamageSystem.cs` (HOOK 18)

One line, first statement of `OnDamage`, before the existing `if (!args.DamageIncreased) return;` (Onyx's
placement):

```csharp
HandlePainDamageEmote(uid, emoteOnDamage, args); // WOLFGATE: HOOK 18, Wolfmed pain sounds (<Onyx-PainSounds>); never a second subscription.
```

`<EmoteOnDamageComponent, DamageChangedEvent>` stays exclusively owned by this file's `Initialize` at `:22`
(PLAN2 §5.2 / §8.3 trap 5). No `AddEmote`/`RemoveEmote` threshold overloads were added — nothing calls them.

### File 4 — `Resources/Prototypes/Entities/Mobs/Species/base.yml`

Both blocks appended directly after `- type: WoundHost` inside the existing
`# WOLFGATE - Wolfmed phase 1 (D21/D32)` block on `BaseMobSpeciesOrganic`, each with its own `# WOLFGATE`
provenance comment (ONYX `species_base.yml:54` and `:124-133`):

```yaml
  - type: PainShockTarget
  - type: EmoteOnDamage
    emotesThreshold:
      50: [ Scream ]
      80: [ Scream, Crying ]
    emoteChance: 0.6
    withChat: true
    hiddenFromChatWindow: true
    emoteCooldown: 8
```

`emotes:` is deliberately left empty, so the upstream `OnDamage` body still returns at its
`Emotes.Count == 0` check — no double emote. Protogen exclusion is preserved *in C#* by edit 4 above, not by
this block (P2-D22).

---

## 3. Deviations from PLAN2, with justification

**D1 — PLAN2's claim that `using Content.Shared._Onyx.Wounds;` is already line 1 of Onyx's PainSounds file is
wrong.** It is absent. The P2-D22 guard therefore costs one added, marked `using` rather than zero. Factual
correction only; no behavioural consequence. Recorded in the manifest so a re-sync does not read the extra
import as unexplained.

**D2 — PLAN2 §3's "4 edits" for this file became 5.** Edits 1–3 are PLAN2's, edit 4 is P2-D22's guard, edit 5
is the pain-numbness parity clause below. PLAN2's own fourth edit (`ProtoMan`) was a verified no-op, so the
count is 4 authorised + 1 discretionary, not 5 unauthorised.

**D3 — `HasComp<PainNumbnessComponent>` added to `HandlePainDamageEmote`'s bail-out chain (discretionary).**
PLAN2 §4/WP10-5 explicitly offers this as a choice. Taken, because the asymmetry is player-visible: WP10-3
widened `PainSystem.IsPainNumb` to honour Wolfgate's legacy `PainNumbnessComponent` (the only thing the
`PainNumbness` trait actually grants — Onyx's `PainNumbnessStatusEffectComponent` has no applier in Wolfgate
at all), so without this clause a pain-numb character would get no pain vignette and no pain-shock scream yet
still scream from `EmoteOnDamage`.

**D4 — one existing manifest paragraph was edited, not just appended to.** The "Species excluded (1 of 18)"
note gained a phase-2 sentence recording that `WolfmedWoundHostExclusionSystem` strips only
`WoundHostComponent`, so protogen *does* carry the two components WP10-5 added beside it
(`PainShockTarget` inert there; `EmoteOnDamage` gated in C#). Leaving the paragraph alone would have left a
reader with a false claim about what the exclusion covers.

**D5 — a known zombie interaction is documented but not fixed.** `ZombieSystem.OnMobState`'s non-`Alive`
branch calls `RemoveEmote(uid, "Scream")`, whose `removeEmpty: true` default `RemCompDeferred`s the whole
`EmoteOnDamageComponent` once `Emotes` empties. Now that the component is YAML-declared on every organic
species, a zombie leaving `Alive` drops the prototype's `emotesThreshold` with it, and the `EnsureComp` on any
later return to `Alive` yields a component with empty thresholds. Confined to ex-zombies, invisible while
crit/dead (`HandlePainDamageEmote` already bails on `MobState.Critical or Dead`). A fix means either
`removeEmpty: false` at those two call sites or moving the zombie groan onto its own component — both outside
PLAN2 §3's authorised hook list, so it is escalated rather than taken.

**Deviations already recorded by PLAN2 and re-affirmed here (not new):** P2-D9's corrected `emotesThreshold`
key (manifest deviation 4), P2-D9's additive component extension (deviation 5), P2-D22's wound-host gate
(deviation 17), and the carried-forward `StunSystemOnyxCompat`/`TryParalyze` stun-VFX warning (deviation 13),
which matters now because P2-D7's `PainShockTarget` makes pain shock run on a real mob for the first time.

---

## 4. Build / test output

All sequential, one at a time, `-c DebugOpt`.

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

**Headless server** (YAML/FTL touched), 150 s, log at `C:/tmp/wolfmed-plan/p2/WP10-5-report-server.log`:

```
$ grep -nE "\[ERRO\]|\[FATL\]|Exception" WP10-5-report-server.log
(no output — zero matches)

... tail ...
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "0.0.0.0": "Socket bound to 0.0.0.0:1299: True"
[INFO] net: "0.0.0.0": "Network thread started"
```

The only `[WARN]`s are 12 pre-existing upstream `system.chat: Duplicate of emote word …` lines (beep/hiss/honk)
plus `MainLoop: Cannot keep up!` — none Wolfmed-related, all present before this package.

**Release YAML linter — run deliberately, because the headless start cannot prove the key binds.** RT drops
unknown mapping keys at *read* time and raises `FieldNotFoundErrorNode` only on the validate path
(`RobustToolbox/Robust.Shared/Serialization/Manager/Definition/DataDefinition.cs:277`) — which is precisely why
Onyx's `emotes:` bug survived at the pin and why a clean server start is **not** evidence that
`emotesThreshold:` bound. Log at `C:/tmp/wolfmed-plan/p2/WP10-5-report-lint.log`:

```
$ dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj -c Release -v q -nologo
Build succeeded.
    0 Error(s)

$ ./Content.YAMLLinter.exe
No errors found in 135940 ms.
```

That is the positive proof the corrected key binds and that `- type: PainShockTarget` resolves.

No integration tests were run: WP10-5 ships no test file, and the pain-sound/pain-shock assertions belong to
WP10-6b.

---

## 5. What later packages must know

1. **`Resources/Prototypes/Entities/Mobs/Species/base.yml` is released.** WP10-5's ownership ends here. Both
   phase-2 blocks live inside the phase-1 `# WOLFGATE` block on `BaseMobSpeciesOrganic`; append there, do not
   start a new block.
2. **Pain shock now runs on real mobs for the first time in this build** (P2-D7 satisfied
   `PainSystem.Update`'s three-component query). Expect the 2 s paralyse, the forced `Scream`, jitter and the
   30 s ×0.7 adrenaline window to be observable. `StunSystemOnyxCompat` → `TryParalyze` re-triggers stun VFX on
   every call; `UpdatePainShock` disarms after each shock and rearms only below pain 110, so repetition is
   bounded but visible. **WP10-6b should measure, not assume, every number here (P2-4).**
3. **`<EmoteOnDamageComponent, DamageChangedEvent>` remains single-owner.** If a later package wants anything
   off damage for this component, call into the existing `OnDamage` handler; a second subscription is a
   server-start `Duplicate Subscriptions` crash.
4. **Two independent emote paths now coexist on `EmoteOnDamageComponent`** and are mutually exclusive only
   because `emotes:` is empty on `BaseMobSpeciesOrganic`. Any prototype that populates `emotes:` on a wound
   host will emit both the legacy random emote and the Wolfmed threshold emote from the same damage event.
5. **Protogen (and any future D32 exclusion) carries `PainShockTarget` and `EmoteOnDamage`.** The exclusion
   system removes `WoundHostComponent` and nothing else. Anything added beside `WoundHost` in that YAML block
   must be inert on non-hosts or gated in C#, as these two are.
6. **The ex-zombie `emotesThreshold` loss (deviation D5) is live.** If WP10-6b or a later phase writes a pain-
   sound test, do not use a zombie, and do not assume the component's YAML data survives a zombie mob-state
   round trip.
7. **WP10-7 reconciliation:** per the orchestrator's instruction, WP10-5's rows and deviations were appended
   **directly** to `Docs/Wolfmed/WOLFMED_MANIFEST.md` (no `manifest-rows-WP10-5.md` was written). The manifest
   working copy is **LF**, not CRLF; a patch script that assumes CRLF will double the line count. It is UTF-8 —
   read and write it with an explicit `encoding='utf-8'`, not the Windows locale default.
