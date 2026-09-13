# WP10-3 — `HighPainThreshold` trait + `IsPainNumb` widening

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.Shared/_Onyx/Traits/HighPainThresholdComponent.cs` | **new**, verbatim from ONYX |
| `Content.Shared/_Onyx/Traits/HighPainThresholdSystem.cs` | **new**, verbatim from ONYX |
| `Resources/Prototypes/_Onyx/Traits/quirks.yml` | **new**, adapted (trimmed to `HighPainThreshold` only) |
| `Resources/Locale/en-US/_Onyx/traits/quirks.ftl` | **new**, adapted (2 keys only) |
| `Content.Shared/_Onyx/Wounds/PainSystem.cs` | **modified**, 1 `// WOLFGATE` edit |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | appended (5 file rows + WP10-3 Deviations subsection) |

All new/touched files verified CRLF in the working tree (matching the repo convention); the `PainSystem.cs`
edit was a pure insertion with no line-ending churn (`git diff --stat`: 7 insertions, 0 deletions).

## 2. Every `// WOLFGATE` edit and reason

- `Content.Shared/_Onyx/Wounds/PainSystem.cs`, inside `IsPainNumb` (after the existing part→body redirect,
  before the status-effect check):
  ```csharp
  // WOLFGATE (P2-D8): Wolfgate's PainNumbness trait grants the legacy PainNumbnessComponent
  // (Content.Shared/Traits/Assorted/PainNumbnessComponent.cs); Onyx's status-effect form
  // (StatusEffectPainNumbness) has no applier here — TraitPrototype has no `specials:`, and the
  // narcotics that apply it are phase 4. Honour both.
  if (HasComp<PainNumbnessComponent>(entity))
      return true;
  ```
  No new `using` — `Content.Shared.Traits.Assorted` was already imported at line 13. This is the sole
  in-vendored-file edit; no upstream (non-`_Onyx`, non-`_WF`) file was touched by this WP.
- `Resources/Prototypes/_Onyx/Traits/quirks.yml`: `# WOLFGATE (WP10-3)` trim-scope comment, and
  `# WOLFGATE (P2-D15)` comment documenting `conflicts:` → `mutuallyExclusiveTraits:` and the dropped `cost: 3`.
- `Resources/Locale/en-US/_Onyx/traits/quirks.ftl`: `# WOLFGATE (WP10-3)` trim-scope comment.

`HighPainThresholdComponent.cs` and `HighPainThresholdSystem.cs` are byte-verbatim from
`git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Traits/{Component,System}.cs` — no `// WOLFGATE` marks needed
or added; Onyx's headers, namespace (`Content.Shared._Onyx.Traits`) and class names kept as-is.

## 3. Deviations from PLAN2

None. All five files landed exactly as PLAN2 §4/WP10-3 specifies: two verbatim `_Onyx` C# files, one adapted
prototype (trimmed + the two P2-D15 adaptations), one adapted locale file, and the single `IsPainNumb` widening
in the vendored `PainSystem.cs`. `cost: 3` was dropped per P2-D15 (Wolfgate's `Quirks` category declares no
`maxTraitPoints`, so the field would be inert); `specials:` was not ported (no Wolfgate equivalent).

## 4. Build / test output tails

`dotnet build Content.Server` (`-c DebugOpt -v q -nologo`):
```
Build succeeded.
    0 Error(s)
```

`dotnet build Content.Client` (`-c DebugOpt -v q -nologo`):
```
Build succeeded.
    0 Error(s)
```

Headless server, 120 s, `--cvar net.port=1299`, log at `C:/tmp/wolfmed-plan/p2/WP10-3-server.log`:
```
[INFO] cvarcontrol: Registered 33 CVars.
[INFO] root: Server Version 277.0.0.0 -> Ready
[INFO] net: "::": "Socket bound to [::]:1299: True"
```
`grep -E "\[ERRO\]|\[FATL\]|Exception"` on the log: **0 matches** — no Fluent duplicate-id error, no
`FieldNotFoundErrorNode`/`ErrorNode` for `Resources/Prototypes/_Onyx/Traits/quirks.yml` or the new locale file,
server reached "Ready" and started listening.

## 5. What later packages must know

- `HighPainThreshold` is now a real, selectable trait (category `Quirks`, `mutuallyExclusiveTraits: [PainNumbness]`,
  no cost) that multiplies `ModifyPainGainEvent.Multiplier` by 0.75 on the body. This is the first live
  subscriber of `ModifyPainGainEvent` in Wolfgate.
- `IsPainNumb` now returns `true` for any entity (or its body, if a body part is passed) carrying the legacy
  `PainNumbnessComponent` — i.e. anyone with Wolfgate's shipped `PainNumbness` trait. This affects `PainSystem`'s
  pain-vignette suppression, pain-shock scream, and pain stun for every pain-numb character, retroactively
  (T-PAIN-NUMB in WP10-6b should now pass; it was failing-by-design before this widening — that is the point
  of the fix, per PLAN2 P2-D8).
- WP10-4 (Pain HUD, HOOK 16) depends on this widening landing first: PLAN2 explicitly orders "ship WP10-3 and
  WP10-4 together, and if only one can land, land WP10-3 first" — WP10-3 is now landed and green.
- No new upstream hooks, no new `base.yml` edits, no `Docs/Wolfmed/` file other than `WOLFMED_MANIFEST.md`
  touched (per the task's direct-append instruction, superseding PLAN2's `manifest-rows-WP10-N.md` indirection).
- Registration/subscription pairs added: component `HighPainThreshold` (was a zero-hit grep before this WP);
  directed pair `<HighPainThresholdComponent, ModifyPainGainEvent>` (free — `ModifyPainGainEvent` had no
  subscribers anywhere in WG before this WP, per PLAN2 §5.1 row 10). No duplicate-subscription risk.
