# Wolfmed orchestrator decisions (2026-09-12)

These are constraints for every agent. Do not re-litigate them; flag concrete problems with evidence.

- **Onyx source pinned:** commit `2f5bab9946539cbe083010c9ae6fbc59b47ae377` (Space-Onyx/space-onyx-14 master, 2026-09-13). Reference sparse checkout at `C:\tmp\onyx`. Only paths listed in the sparse set exist; if a path is absent say so, never guess file contents.
- **Wolfgate worktree:** `C:\Users\jzo12\Documents\GitHub\Wolfgate\.claude\worktrees\rules-motd-updates-11c89c` (branch `clanker/wolfmed-port-orchestration-454c3d`). RobustToolbox 277.0.0 is junctioned in. Baseline builds with 0 errors.
- **D1 StatusEffectNew:** port it verbatim from Onyx's copy at the upstream path `Content.Shared/StatusEffectNew` (plus any client/server parts and the prototypes/locale it needs). Treat as vendored upstream code; edits marked `// WOLFGATE`. Rationale: keeps `_Onyx` wound files verbatim; it is upstream Wizden code that Monolith may inherit later. The old `Content.Shared.StatusEffect` system stays and keeps serving existing content.
- **D2 Damage bridge:** for entities with `WoundHostComponent`, Onyx `WoundDamageRoutingSystem` owns part damage. Shitmed's in-`DamageableSystem` spreading, sever-at-130 and part regen are bypassed for those entities via minimal `// WOLFGATE` guards. Entities without `WoundHostComponent` behave exactly as today. Everything is gated on Onyx's `CCVars.Wounds` plus component presence.
- **D3 Phase 1 species:** organic humanoids only (the Human body and any species sharing organic parts). IPC/cybernetic/slime/plant profiles are later phases.
- **D4 Balance:** Onyx defaults. Tuning later via CCVars and prototypes.
- **D5 Missing APIs** (verified absent in Wolfgate): `StatusEffectNew`, new-style `DamageableSystem` API (`Entity<DamageableComponent?>` overloads, `ChangeDamage`, `HealEvenly`, `HealDistributed`, `GetTotalDamage`...), `EntityEffectSystem<T>` ECS entity effects (Wolfgate has the old class-based `EntityEffect`), `AlertsSystem.UpdateAlert`, `DamageSpecifier.ArmorPenetration`. Strategy: a compat layer in `Content.Shared/_WF/Wolfmed/Compat` (extension methods, adapter classes) wherever a shim keeps a vendored file verbatim. Where a shim is impossible, a `// WOLFGATE` edit inside the vendored file. Upstream Wolfgate systems only get one- or two-line `// WOLFGATE` hooks.
- **D6 Layout:** vendored Onyx code in `Content.{Shared,Server,Client}/_Onyx/...`, `Resources/Prototypes/_Onyx/...`, `Resources/Locale/en-US/_Onyx/...`, `Resources/Textures/_Onyx/...` keeping Onyx's relative paths. Wolfgate glue in `_WF/Wolfmed`. Docs and manifest in `Docs/Wolfmed/`.
- **D7 Surgery:** phase 1 keeps Wolfgate's Shitmed surgery. Onyx's own surgery system (`_Onyx/Medical/Surgery/SharedSurgerySystem.*`, `_Onyx/Surgery`) is NOT ported. Wound surgeries are re-expressed on Shitmed's step system later (phase 4).
- **D8 Body:** stay on Wolfgate's Shitmed `BodyPartComponent`. Onyx's extra part fields go in a separate `_WF/Wolfmed` component. Onyx `_Onyx/Body` Nubody glue is not ported; only organ-damage and functional-organ pieces the wounds need.
- **Style:** `_WF` files: no license header, `/// <summary>` one-liners, `[Dependency] private X _x = default!;` without readonly. Vendored `_Onyx` files keep Onyx's headers verbatim.
- **Testing:** headless integration tests only. Port Onyx's wound tests into `Content.IntegrationTests/Tests/_Onyx/Wounds`.

## Orchestrator calls on PLAN.md §8.1 (2026-09-13) — reversible, flagged to the user

- **D32 Species (§8.1 item 1):** `- type: WoundHost` on `BaseMobSpeciesOrganic`. Diona and slime ship on the Organic profile (Onyx-consistent) until phase 5. **Protogen is excluded** (synthetic: remove `WoundHost` in its own prototype with a `# WOLFGATE` comment). Re-enumerate descendants at implementation time and list every exclusion in the manifest.
- **D33 PassiveDamage (§8.1 item 2):** accept D29 — body-level `PassiveDamage` neutralised on wound hosts; Onyx per-profile recovery is the only passive heal. Recorded as a balance deviation.
- **D34 Do-afters (§8.1 item 5):** accept the loss of damage-interrupts-do-after on wound hosts for phase 1.
- **D35 Prediction (§8.1 item 6):** accept unpredicted wound-host damage for phase 1 (transient mispredict). Predicting routing is a later phase.
- **No commits.** Work packages leave the tree uncommitted; the verify stage snapshots a patch per WP under `C:/tmp/wolfmed-plan/snapshots/`. The user commits.
