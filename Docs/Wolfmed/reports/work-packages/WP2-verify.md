# WP2 — Compat core — verification

**Verifier run:** 2026-09-12. WG worktree: `C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`. Onyx pin: `2f5bab9946539cbe083010c9ae6fbc59b47ae377`. Nothing committed; no files modified by this verification pass except this report and the snapshot files under `C:/tmp/wolfmed-plan/snapshots/`.

**Verdict: PASS.** No blockers, no majors.

---

## 1. Build

```
$ dotnet build Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"
Build succeeded.
    0 Error(s)

$ dotnet build Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo 2>&1 | grep -E "error [A-Z]+[0-9]+|Build succeeded|[0-9]+ Error"
Build succeeded.
    0 Error(s)
```

Both green, matches the report's claim.

## 2. Upstream discipline

```
$ git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
 Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs | 4 ++++
 1 file changed, 4 insertions(+)
```

Exactly one tracked upstream file touched, as the report claims. Full diff:

```diff
@@ -23,4 +23,8 @@ public static TargetBodyPart[] GetValidParts()
 
         return parts;
     }
+
+    // WOLFGATE: Wolfmed snapshot/targeting needs a single-bit check; copied from Onyx's SharedTargetingSystem.
+    public static bool IsSelectable(TargetBodyPart part)
+        => part != 0 && (part & (part - 1)) == 0 && (part & TargetBodyPart.All) != 0;
 }
```

- Every added line carries `// WOLFGATE` or sits inside the marked block. Pass.
- Site (end of class) and content match **HOOK 5**, PLAN.md §3 line 520: `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs`, end of class, add the static `IsSelectable`, authorised for **WP2**. Pass — hook used matches the WP it was authorised for.
- No other upstream file (non-`_Onyx`, non-`_WF`, non-Docs) appears in the diff. `git status --porcelain` for the same path set shows only this one `M` plus untracked `_WF`/`_Onyx`/`Docs`/`StatusEffectNew` trees (WP1 leftovers, not WP2's). No unauthorised upstream edits found.

## 3. Vendoring fidelity (`_Onyx/` and `Content.Shared/StatusEffectNew/`)

Not applicable to WP2: WP2 created no files under `_Onyx/` or `Content.Shared/StatusEffectNew/` and modified none. Confirmed by (a) the git diff --stat above showing only the one `_Shitmed` hook against tracked files, and (b) `git status --porcelain` showing the `_Onyx`/`StatusEffectNew` trees as untracked directories that predate this WP (WP1's StatusEffectNew port) — WP2's own report and file table list no such files, and none of the 9 new WP2 files live under those prefixes. No findings.

## 4. Subscriptions

New files under `Content.Shared/_WF/Wolfmed/Compat`, `Content.Server/_WF/Wolfmed/Compat`, and the one upstream hook were grepped for `SubscribeLocalEvent`:

```
$ grep -rn "SubscribeLocalEvent" <the 9 new WP2 files> <the hook file>
Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs:13:        SubscribeLocalEvent<HealOnBuckleComponent, ComponentStartup>(OnStartup);
```

Exactly one pair added: `(HealOnBuckleComponent, ComponentStartup)`. Grepping the rest of WG (excluding this new file) for the same pair:

```
$ grep -rn "SubscribeLocalEvent<HealOnBuckleComponent" --include=*.cs . | grep -v WolfmedBedHealMarkerSystem.cs
Content.Server/Bed/BedSystem.cs:35:            SubscribeLocalEvent<HealOnBuckleComponent, StrappedEvent>(OnStrapped);
Content.Server/Bed/BedSystem.cs:36:            SubscribeLocalEvent<HealOnBuckleComponent, UnstrappedEvent>(OnUnstrapped);
```

`BedSystem.cs` only owns `StrappedEvent`/`UnstrappedEvent` on that component, not `ComponentStartup` — no duplicate. This matches PLAN.md §2.10 and §5.2's "free — verified" entry for this exact pair, and matches the WP2 report's own claim ("Exactly one ... §5.2's 'free' verdict re-confirmed"). Pass.

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` has a row for all 9 new WP2 files plus the HOOK 5 upstream change:

- `WolfmedDamageableSystem.cs`, `DamageSpecifier.Wolfmed.cs`, `DamageDealtEvent.cs`, `WolfmedBodySystem.cs`, `StunSystemOnyxCompat.cs`, `SharedChatSystem.Wolfmed.cs`, `WolfmedBedHealMarkerComponent.cs`, `WolfmedBedHealMarkerSystem.cs`, `OnyxBodyEvents.cs` — each has a manifest row tagged `WP2`.
- `Content.Shared/_Onyx/Targeting/SharedTargetingSystem.cs:53` → `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs`, status `hook`, WP `WP2` — present.
- A "### WP2" Deviations subsection documents the `[ByRefEvent]` addition, the `RA0002`/local-dictionary write, `HealEvenly`/`HealDistributed` being implemented rather than stubbed, the two extra facade methods, and the `SetAllDamage` empty-delta behaviour — all consistent with what's in the code (§ 2 above / code review below).

All 10 WP2-touched files have manifest rows. Pass.

## 6. Plan conformance

All 10 items in PLAN.md's WP2 file table (§4, lines 651-662) exist at the planned destination:

| # | Planned path | Present? |
|---|---|---|
| 1 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedDamageableSystem.cs` | yes |
| 2 | `Content.Shared/_WF/Wolfmed/Compat/DamageSpecifier.Wolfmed.cs` | yes |
| 3 | `Content.Shared/_WF/Wolfmed/Compat/DamageDealtEvent.cs` | yes |
| 4 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBodySystem.cs` | yes |
| 5 | `Content.Shared/_WF/Wolfmed/Compat/StunSystemOnyxCompat.cs` | yes |
| 6 | `Content.Shared/_WF/Wolfmed/Compat/SharedChatSystem.Wolfmed.cs` | yes |
| 7 | `Content.Shared/_WF/Wolfmed/Compat/WolfmedBedHealMarkerComponent.cs` | yes |
| 8 | `Content.Server/_WF/Wolfmed/Compat/WolfmedBedHealMarkerSystem.cs` | yes |
| 9 | `Content.Shared/_WF/Wolfmed/Compat/OnyxBodyEvents.cs` | yes |
| 10 | `Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs` (HOOK 5) | yes, `IsSelectable` present verbatim |

No extraneous file exists under `Content.Shared/_WF/Wolfmed/Compat` or `Content.Server/_WF/Wolfmed/Compat` beyond WP1's three (`AlertsSystem.UpdateAlert.cs`, `EntityPrototypeCompatExtensions.cs`, `StatusEffectsSystem.Wolfgate.cs`, already recorded as WP1 rows) and WP2's nine.

**Decisions cited, checked against code:**

- **D11** (`DamageDealtEvent` shape) — file matches §2.3 exactly: `record struct` (not `readonly`), namespace `Content.Shared.Damage.Systems`, three members `Damage`/`Origin`/`InterruptsDoAfters`, `[ByRefEvent]`. Pass.
- **D12** (facade, not a `DamageableSystem` partial) — `WolfmedDamageableSystem` is a distinctly-named `sealed class : EntitySystem` in `Content.Shared._WF.Wolfmed.Compat`. Pass.
- **D19** (no `InjurableComponent` shim) — `CanBeDamagedBy` reads `DamageableComponent.DamageContainerID` and unions `SupportedTypes` with every `SupportedGroups`' `DamageTypes`, mirroring `DamageableInit`'s seeding loop; no reference to `InjurableComponent` anywhere in the new files. Pass, and the `// WOLFGATE (D19)` comment is present.
- **D23** (armor penetration / tool threaded through) — `ChangeDamage`/`TryChangeDamage` both carry `float armorPenetration = 0f, EntityUid? tool = null, DamageableSystem.DamageOriginFlag? originFlag = null` and forward them positionally to `DamageableSystem.TryChangeDamage`, whose signature (`Content.Shared/Damage/Systems/DamageableSystem.cs:190-196`) accepts them in the same order. Pass — verified the call binds (build succeeded, and the argument order matches the real signature read directly from the file).
- **D27** (facade's `TryChangeDamage`/`ChangeDamage` never surface a lost `null` as swallowed) — `ChangeDamage` does `?? new DamageSpecifier()`, so a cancelled write reads as empty rather than throwing/nulling; consistent with GUARD D2's `before.Applied` contract that WP5 will wire up. Nothing in WP2 contradicts D27 (WP2 doesn't touch `DamageableSystem.cs` itself, correctly deferred to WP5 per the report).
- **D30** (`SetDamage` zeroes, doesn't remove) — code zeroes missing keys (`dict[type] = FixedPoint2.Zero`) rather than removing them, with a `// WOLFGATE (D30)` comment. Pass.
- **D25** (`TryEmoteWithChat` stays `virtual void`, exact signature copy) — `SharedChatSystem.Wolfmed.cs`'s signature (`EntityUid source, string emoteId, ChatTransmitRange range = ChatTransmitRange.Normal, bool hideLog = false, string? nameOverride = null, bool ignoreActionBlocker = false, bool forceEmote = false`) was diffed by eye against `Content.Server/Chat/Systems/ChatSystem.Emote.cs:60-68`'s overload of the same name — parameter names, order and defaults match. Return type `void`, `virtual`, empty body (server override is WP4's HOOK 6, correctly not done here — confirmed `ChatSystem.Emote.cs` does not appear in the tracked diff). Pass.
- **D28** (`WolfmedBodyPartLifecycleSystem` is WP5, not WP2) — WP2 shipped only `OnyxBodyEvents.cs` (event declarations, no subscriber); grepped the new files for `SubscribeLocalEvent<.*BodyPartAddedEvent>` / `BodyPartRemovedEvent` / `OrganAddedToBodyEvent` / `OrganRemovedFromBodyEvent` — none found. Matches report and plan. Pass.
- **D8/D10/D9** — not implicated by any WP2 file (targeting/body-part compat land in WP4 per §2.12/§2.13); nothing in WP2 references `BodyPartType.Chest`/`.Groin` or Onyx's `TargetingComponent`. No violation.

`WolfmedBedHealMarkerComponent`/`System` (§2.10) — `HealOnBuckleComponent` import is `Content.Server.Bed.Components`, used only from the new `Content.Server` file, so the shared marker component correctly has no reference to the server-only type. Pass.

Style check (DECISIONS.md: `_WF` no license header, `/// <summary>` one-liners, `[Dependency] private X _x = default!;` no `readonly`): all 9 new files comply — no license headers, one-line `<summary>` tags throughout, and `WolfmedDamageableSystem`/`WolfmedBodySystem` both declare their `[Dependency]` fields without `readonly`.

Smoke-test claim: the report says a compile-exercise helper (`ZZ_WolfmedCompatSmoke.cs`) was written, used to confirm both builds green, then deleted and preserved at `C:/tmp/wolfmed-plan/wp/WP2-compat-smoke.cs.txt`. Confirmed that file exists at that path and no `ZZ_WolfmedCompatSmoke.cs` (or similar) remains anywhere in the WG tree.

## 7. Snapshot

```
$ git -C WG diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP2.patch
$ git -C WG ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP2.untracked.txt
```

`WP2.patch` — 13 lines (the single HOOK 5 diff hunk above).
`WP2.untracked.txt` — 33 files: WP2's 9 new compat files plus WP1's untracked leftovers (`StatusEffectNew/**`, WP1's 3 compat files, `PainNumbnessStatusEffectComponent.cs`, `Docs/Wolfmed/**`, the `_Onyx` categories/locale files, `StatusEffects/misc.yml`) — expected, since no WP has been committed yet and the snapshot captures the whole uncommitted tree, not a WP2-only diff.

---

## Summary of findings

None. No blocker or major issues. WP2's report is accurate: builds are green, exactly one upstream file was touched and it is the authorised HOOK 5, no duplicate `(component, event)` subscriptions were introduced, the manifest is complete, every planned file exists, and the code matches the decisions it cites (D11, D12, D19, D23, D25, D27 contract, D28 deferral, D30).
