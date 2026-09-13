# WP11-4 — Limb damage sprites (P3-4)

Both Option A (mandatory) and Option B (severed-limb wound rendering, pre-authorised by DECISIONS.md §8.6-5 /
PLAN3 P3-D18) shipped. Option A built clean and the client/server showed zero errors before Option B was
attempted, satisfying the task's gate for proceeding to Option B.

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.Client/_WF/Wolfmed/Damage/DamageVisualsSystem.Wolfmed.cs` | **new** — HOOK 20 body (Option A + Option B) |
| `Content.Client/Damage/DamageVisualsSystem.cs` | **modified** — HOOK 20 (3 insertions: 2 usings, 2 subscriptions, 1 early-return block) |
| `Content.Shared/Body/Part/BodyPartComponent.cs` | **modified** — HOOK 21 (1 word) |
| `Resources/Prototypes/Entities/Mobs/Species/base.yml` | **modified** — PROTO C (2 lines) |
| `Resources/Textures/_Onyx/Wounds/brute_damage.rsi/` | **new** — 78 files (77 states + meta.json), byte-copied from ONYX |
| `Resources/Textures/_Onyx/Wounds/burn_damage.rsi/` | **new** — 78 files (77 states + meta.json), byte-copied from ONYX |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | **modified** — 7 master-table rows + `### WP11-4` deviations section appended |

No test file was touched (Option A/B needed no new fixture; WP11-0's `WolfmedVisualsTest.cs` already covers the
server-side data this package consumes and was re-run, not edited).

## 2. Every WOLFGATE edit and reason

**`Content.Client/Damage/DamageVisualsSystem.cs`:**
1. `using Content.Shared._Onyx.Wounds; // WOLFGATE: HOOK 20` — brings in `PartDamageVisualsComponent`.
2. `using Content.Shared.Body.Part; // WOLFGATE: HOOK 20 (Option B)` — brings in `BodyPartComponent`.
3. `Initialize()`: `SubscribeLocalEvent<PartDamageVisualsComponent, AfterAutoHandleStateEvent>(OnPartDamageVisualsState); // WOLFGATE: HOOK 20` — Option A, exactly as PLAN3's HOOK 20 table authorises.
4. `Initialize()`: `SubscribeLocalEvent<BodyPartComponent, AfterAutoHandleStateEvent>(OnBodyPartState); // WOLFGATE: HOOK 20 (Option B) - needs HOOK 21's raiseAfterAutoHandleState flag` — **beyond PLAN3's literal HOOK 20 table** (see Deviations §3).
5. `HandleDamage(...)`: 5-line early-return block after `UpdateDisabledLayers` and before `CheckOverlayOrdering`, marked `// WOLFGATE: HOOK 20 — per-limb accuracy for wound hosts; body in _WF/Wolfmed/Damage/DamageVisualsSystem.Wolfmed.cs`.

**`Content.Shared/Body/Part/BodyPartComponent.cs`:**
6. `[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]` → `AutoGenerateComponentState(raiseAfterAutoHandleState: true)`, marked `// WOLFGATE: HOOK 21 - P3-4 Option B needs AfterAutoHandleStateEvent on detached parts.` Verified zero pre-existing `<BodyPartComponent, AfterAutoHandleStateEvent>` subscribers repo-wide before this change (adds behaviour only).

**`Resources/Prototypes/Entities/Mobs/Species/base.yml`:**
7. `damageOverlayGroups.Brute.sprite`: `Mobs/Effects/brute_damage.rsi` → `_Onyx/Wounds/brute_damage.rsi # WOLFGATE (WP11-4, P3-4 Option B): severed parts render Onyx's wound art too; same 6 states + attribution as the stock rsi.`
8. `damageOverlayGroups.Burn.sprite`: `Mobs/Effects/burn_damage.rsi` → `_Onyx/Wounds/burn_damage.rsi # WOLFGATE (WP11-4, P3-4 Option B)`.
No `Groin` layer or Hand/Foot `targetLayers` added (D9; matches Onyx, which does not add them either).

**`Content.Client/_WF/Wolfmed/Damage/DamageVisualsSystem.Wolfmed.cs` (new, `_WF` style, no `// WOLFGATE` markers needed — it's Wolfgate-authored, not vendored):**
- `OnPartDamageVisualsState` — re-runs `HandleDamage` and (Option B) `UpdateDetachedPartDamage` when the server's per-part damage projection changes.
- `UpdatePartDamageVisuals` — Option A: per-layer read via `GetLayerDamage`, re-typed from Onyx's `Entity<SpriteComponent, DamageVisualsComponent>` tuple call shape to WG's existing `UpdateTargetLayer(SpriteComponent, DamageVisualsComponent, object, string, FixedPoint2)` (`:621`). Per-group threshold cache deliberately not consulted (kept from Onyx — it's keyed by group only, not (layer, group)).
- `GetLayerDamage` — **P3-D25 fold**: `LArm+=LHand`, `RArm+=RHand`, `LLeg+=LFoot`, `RLeg+=RFoot` via `DamageSpecifier.operator+`, so hand/foot wounds remain visible on the arm/leg exactly as they are pre-phase-3 (D30). Without this fold hand/foot wounds go invisible — WG's stock overlay has neither the `targetLayers` entries nor the RSI states for them.
- `OnBodyPartState`, `UpdateDetachedPartDamage`, `UpdateDetachedDamageLayer`, `SetDetachedDamageLayerVisible`, `GetDetachedDamageThreshold`, `TryGetDetachedDamagePrefix` — Option B, re-authored from Onyx's tagged region onto WG's `SpriteSystem` entity-tuple API (`(uid, sprite)` pairs), confirmed present and unchanged in RT 277. `TryGetDetachedDamagePrefix` **drops Onyx's `Groin` arm** (D9 — no such `HumanoidVisualLayers` member in WG; porting it verbatim is `CS0117`).

## 3. Deviations from PLAN3

1. **HOOK 20 needed a third insertion beyond the literal table.** PLAN3's HOOK 20 row authorises only the
   Option-A subscription and the `HandleDamage` early return. Option B's `<BodyPartComponent,
   AfterAutoHandleStateEvent>` subscription has no other legal home — `EntitySystem.Initialize()` is a single
   virtual override, already owned by the upstream file, and a `_WF` partial cannot add a second override — so
   it was added as a marked one-line extension of the same hook. Justification: Option B is itself
   pre-authorised (DECISIONS.md §8.6-5) and is provably dead without this exact subscription (P3-D19's stated
   purpose). If this is judged too broad, reverting is a 3-line removal (the subscription, its `using`, and
   Option B's 5 methods) leaving Option A fully intact — recorded in the manifest for anyone who wants to take
   that path.
2. **Detached-part layer-map key renamed.** Onyx's `OnyxDetached{layer}{group}` → `WolfmedDetached{layer}{group}`.
   Purely cosmetic; the key is internal to `SpriteComponent` and read by no YAML/other system.
3. All other deviations (P3-D25 fold, D9 Groin drop, the two upstream hooks, the RSI copy) are exactly what
   PLAN3 §2.3/§3/§8 already specify — no further departures.

## 4. Build / test output tails

**Content.Server build:** `Build succeeded. 0 Error(s)`
**Content.Client build:** `Build succeeded. 0 Error(s)`
**Content.IntegrationTests build:** `Build succeeded. 0 Error(s)` (not rebuilt for the checkpoint since no test file changed, but confirmed green earlier in the session)

**Release YAML lint** (`Content.YAMLLinter`, `-c Release`): `No errors found in 244675 ms.` (all warnings printed are pre-existing and unrelated to this WP — Index-literal/RA0045-proxy-method style warnings elsewhere in the tree).

**Headless server (120 s, `Content.Server.exe --cvar net.port=1299`):**
```
[INFO] root: Server Version 277.0.0.0 -> Ready
```
`grep -E "\[ERRO\]|\[FATL\]|Exception"` on the full log: **zero matches**. No `Duplicate Subscriptions` throw.

**RSI/meta.json validation** (custom Node script, not Python — Python unavailable in this environment):
- `brute_damage.rsi`: 77 states declared, 77 matching `.png` files present, zero missing, zero orphaned.
- `burn_damage.rsi`: same, 77/77.
- Every state the code/YAML can request (`{prefix}_{group}_{threshold}` for the 11 prefixes and the live
  overlay's thresholds `[10,20,30,50,70,100]`, plus Onyx's unused `40`) exists in both `meta.json` files.
- License check: both new `meta.json` declare `"license": "CC-BY-SA-3.0", "copyright": "Drawn by Ubaser."` —
  byte-identical to WG's own already-shipped `Resources/Textures/Mobs/Effects/{brute,burn}_damage.rsi/meta.json`.

**T-VISUALS re-run** (`WolfmedVisualsTest.PartDamageProjectsToVisualsComponentTest`):
```
Passed PartDamageProjectsToVisualsComponentTest [1 m 32 s]
Total tests: 1
     Passed: 1
```
Only `db.ef` sqlite migration warnings and one unrelated `PullingSystem` command-bind warning in the log
(environmental, per project memory) — no real failures.

No sprite-pixel/screenshot test was attempted, per project convention (headless logic tests preferred;
`DamageVisualsSystem` rendering is not headlessly assertable). Visual confidence for this WP rests on: clean
client build, zero client/server console errors, the server-side data test (T-VISUALS) staying green, and the
RSI-state cross-check above.

## 5. What later packages must know

- **Both Option A and Option B are live.** A severed limb now renders its own accumulated wound art
  (`_Onyx/Wounds/{brute,burn}_damage.rsi`) once it is off the body, and it stops the moment it is reattached.
  Attached-body damage still renders through the stock six-layer overlay, now reading each limb's own damage
  (Option A) with the P3-D25 hand→arm/foot→leg fold.
- **`BodyPartComponent` now raises `AfterAutoHandleStateEvent` for every body part in the game** (HOOK 21 has no
  component gate). This is safe today — the only subscriber is the new Option B handler, and it no-ops
  immediately for non-wound-hosts (`TryComp<PartDamageVisualsComponent>` fails) — but any future subscriber to
  this pair must remember it fires universally, not just for wound hosts.
- **If a future package needs to touch `Content.Client/Damage/DamageVisualsSystem.cs` again**, the file now
  carries 3 marked hook sites (2 usings + 2 subscriptions in `Initialize()`, 1 early-return block in
  `HandleDamage`) instead of PLAN3's originally-scoped 2; see Deviation 1 above before adding a 4th.
- **If Option B is ever judged out of scope in a later audit**, reverting it means: dropping the
  `<BodyPartComponent, AfterAutoHandleStateEvent>` subscription and its `using` from
  `DamageVisualsSystem.cs`, reverting HOOK 21's one-word flag, dropping the 5 Option-B methods from the `_WF`
  partial, and reverting PROTO C's 2-line sprite retarget (the RSI files themselves are harmless to leave —
  they'd simply become unreferenced).
- **`Docs/Wolfmed/WOLFMED_MANIFEST.md`** has this WP's 7 master-table rows and its `### WP11-4` deviations
  section already appended, ready for WP11-6's reconciliation pass.
- **WP11-5** (amputation + organ tests) needs no visuals assertion beyond what WP11-0 already wrote; nothing in
  this package changed server-side data shape.
