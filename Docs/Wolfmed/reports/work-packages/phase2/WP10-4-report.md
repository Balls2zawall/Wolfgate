# WP10-4 — Pain HUD overlay

Implements PLAN2 §4/WP10-4: HOOK 15 (client `DamageOverlay`) and HOOK 16 (client
`DamageOverlayUiController`), bodies in two new `_WF` partials. No YAML/FTL touched.

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.Client/UserInterface/Systems/DamageOverlays/Overlays/DamageOverlay.cs` | modified (hook) |
| `Content.Client/UserInterface/Systems/DamageOverlays/DamageOverlayUiController.cs` | modified (hook) |
| `Content.Client/_WF/Wolfmed/Overlays/DamageOverlay.Wolfmed.cs` | new |
| `Content.Client/_WF/Wolfmed/Overlays/DamageOverlayUiController.Wolfmed.cs` | new |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | modified (manifest rows + WP10-4 Deviations subsection appended directly, per updated serialisation rule since packages run sequentially) |

## 2. Every `// WOLFGATE` edit and reason

- `DamageOverlay.cs:66` — `TryApplyWolfmedPain(); // WOLFGATE: HOOK 15 — pain owns the brute vignette on wound hosts (Wolfmed phase 2).`
  Inserted after the eye/viewport guard, before the lerp block. Calls into the `_WF` partial.
- `DamageOverlay.cs:160` — `_bruteShader.SetParameter("darknessAlphaOuter", 0.8f);` → `0.8f * level); // WOLFGATE: HOOK 15 — ONYX Content.Client/DamageOverlay/DamageOverlay.cs:175`.
- `DamageOverlayUiController.cs:98` — extended the existing Mono `PainNumbnessComponent` guard with
  `&& !WolfmedPainOwnsVignette(entity) // WOLFGATE: HOOK 16 — pain owns the vignette on wound hosts`, so the
  brute/burn-damage vignette computation is also skipped when a `PainComponent` is present.

No `using` was added to either upstream file — both new symbols (`TryApplyWolfmedPain`,
`WolfmedPainOwnsVignette`) are members of the same partial class, declared in the `_WF` files under the
identical upstream namespace.

## 3. Deviations from PLAN2

None. All four files match PLAN2 §4/WP10-4 exactly. Line numbers had drifted slightly from PLAN2's citations
(re-verified against the tree before editing): the eye/viewport guard ends at `:64` and the call landed at
`:66` (PLAN2 said "after :64"); `darknessAlphaOuter` is at `:160` today, not PLAN2's `:158`;
`DamageOverlayUiController.cs`'s condition is still at `:98` as PLAN2 predicted. These are line-drift
observations, not scope deviations.

Confirmed at implementation time: RT 277 still has no `SubscribeLocalEventAttribute`
(`grep -rn "class SubscribeLocalEventAttribute" RobustToolbox` → zero hits), so the `Draw()`-level read
(P2-D6) stands as the only viable design; no client-side `PainChangedEvent` subscription was added (every
`RaisePainChanged` call site is `_net.IsServer`-gated, so such a subscription would compile and never fire).

Ordering dependency (CRITIQUE2 m6): WP10-3's `PainSystem.IsPainNumb` widening (P2-D8) was already present in
the tree (confirmed via `git status` before starting), so HOOK 16 is correct from the moment this WP ships —
no window where the vignette shows to a `PainNumbness` trait holder.

## 4. Build/test output tails

Server build:
```
Build succeeded.
    0 Error(s)
```

Client build (the real gate for this WP):
```
Build succeeded.
    0 Error(s)
```

No YAML or FTL was touched, so no headless server checkpoint was run for this WP.

`git status --short` after the change shows exactly the 4 intended files plus the manifest touched (verified
— no incidental changes to `Content.Client/_Onyx/`, `Content.Shared/_Onyx/`, or other in-flight WP files that
already existed uncommitted in this tree from other packages).

## 5. What later packages must know

- **Subscriptions registered: zero.** `DamageOverlay` is an `Overlay`, `DamageOverlayUiController` is a
  `UIController` — neither can hold a directed `SubscribeLocalEvent`. Nothing to check against §5's audit.
- **Numbers not independently re-measured beyond the build gate.** PLAN2's own text says "the vignette
  itself is not headlessly assertable" — this WP's checkpoint is the build only. WP10-6b owns
  T-PAIN-OVERLAY, the actual measurement. Until then, the predicted landmarks (vignette starts at pain
  6.75, sits at 0.963 when pain shock fires at 130, using `SoftPainCap = 135` and the 0.05 floor) are
  PLAN2's derivation, not a verified measurement.
- **`PainComponent.{SoftPainCap, RecoveryPerSecond, DamageMultipliers}` are not `[AutoNetworkedField]`.**
  Client and server currently agree only because no prototype declares `- type: Pain` (both sides use the
  same C# initialiser). If a future WP adds per-species tuning via a `- type: Pain` prototype entry without
  also marking the field `[AutoNetworkedField]`, the HUD's denominator will desync between client and
  server.
- **`TryApplyWolfmedPain` resolves `PainSystem` via `_entityManager.System<T>()` inside the method body**,
  not a `[Dependency]` field — required because the overlay is constructed from
  `DamageOverlayUiController.Initialize()`, which can run before entity systems exist. Keep this if the
  overlay's construction timing is ever refactored.
