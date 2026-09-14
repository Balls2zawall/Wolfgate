# WP12-3 verification — Tourniquet (P4-2b)

Verifier pass against PLAN4.md §WP12-3/§1/§3/§5/§8, DECISIONS.md Phase 4 (incl. §8.4), and
`WP12-3-report.md`. Worktree: `.claude/worktrees/rules-motd-updates-11c89c`. Onyx pin:
`2f5bab9946539cbe083010c9ae6fbc59b47ae377` (confirmed via `git -C C:/tmp/onyx rev-parse HEAD`, matches
DECISIONS.md).

## 1. Build

Both builds run sequentially, `-c DebugOpt -v q`:

- `Content.Server/Content.Server.csproj` — **Build succeeded. 0 Error(s).** (First attempt hit a transient
  `MSB3027`/`MSB3021` file-lock on `Robust.Shared.CompNetworkGenerator.dll` from a stray concurrent
  `csc`/build-server process unrelated to this WP's code; retried immediately and it built clean. Not a
  code defect.)
- `Content.Client/Content.Client.csproj` — **Build succeeded. 0 Error(s).**

**PASS.**

## 2. Upstream discipline

`git diff HEAD --stat` over `Content.Shared Content.Server Content.Client Resources Content.IntegrationTests`
shows 9 files changed. Outside `_Onyx`/`_WF`, this WP touches exactly two:

| File | Hook | Marked? |
|---|---|---|
| `Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml` | PROTO D (PLAN4 §3.2) | Yes — single `# WOLFGATE (PROTO D, P4-D9): …` comment precedes the whole replaced block; every changed line (the `- type: Healing` → `- type: Tourniquet` swap, `bloodlossModifier` removal, `healingBeginSound`/`healingEndSound` → `beginSound`/`endSound` rename) is the one authorised in-place swap, not extra scope. `tags:` list untouched (P4-D9 trap avoided — no `Tourniquet` tag added). |
| `Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` | PROTO E (PLAN4 §3.2) | Yes — the single added line carries `# WOLFGATE (P4-2): …`. `MedkitCombatFilled` confirmed untouched (diff shows only the one line under `MedkitAdvancedFilled`). |

The other 7 changed/untracked files in the cumulative worktree diff belong to earlier phase-4 packages
sequenced ahead of WP12-3 in the same uncommitted worktree (per the orchestrator's "packages run
sequentially, manifest appended directly" rule) and are pre-authorised elsewhere, not part of this WP's
scope, but spot-checked for hygiene since they sit in the same diff:

- `Content.Server/EntityEffects/Effects/HealthChange.cs`, `EvenHealthChange.cs` — HOOK 9(a)/9(b) (WP12-1),
  every added line `// WOLFGATE`-marked, matches PLAN4 §3.1 exactly (delegate wrap, `TreatmentCapabilities`
  field, `WithTreatmentCapabilities` branch gated on `HasComponent<WoundHostComponent>` — D2-scoped).
- `Content.Shared/Gibbing/Systems/GibbingSystem.cs` — the two `.ToArray()` snapshots, pre-authorised by
  DECISIONS.md's "Phase 4 (2026-09-13)" preamble and "Gibbing fix" line, both sites marked `// WOLFGATE`.
- `Resources/Prototypes/Reagents/medicine.yml`, `narcotics.yml`, `Consumable/Drink/alcohol.yml`,
  `_Goobstation/Reagents/medicine.yml` — PROTO H/I/J/K (WP12-2), each added block preceded by a `# WOLFGATE
  (PROTO …)` comment.
- `Docs/Wolfmed/DECISIONS.md` — doc-only, mirrors the orchestrator's phase-4 decisions text already on
  record; not code, no marking rule applies.

No unauthorised upstream file, and no unmarked line, appears anywhere in the diff.

**D2 check for this WP (P4-D11):** `TourniquetSystem.CanApply` gates on `HasComp<WoundableComponent>(part)`.
Per D32 (BaseMobSpeciesOrganic ships `WoundHost`; Protogen alone is stripped), every organic humanoid is a
wound host, so the only entity losing function is Protogen — losing a weak `Healing.bloodlossModifier: -10`.
This is explicitly one of the four named D2 pressure points in PLAN4 §1.1 ("the tourniquet swap (P4-D11)")
and is pre-authorised there and at PLAN4 lines 86/1123/1768/1996, not a stealth D2 violation. Non-wound-hosts
take no other behaviour change (they simply fail `CanApply` and get the pre-existing "not bleeding" popup,
no crash). **PASS.**

## 3. Vendoring fidelity

- `Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs` — diffed byte-for-byte (`--strip-trailing-cr`)
  against `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs`:
  **zero differences.** Verbatim as the plan requires.
- `Content.Server/_Onyx/Medical/Tourniquet/TourniquetSystem.cs` (relocated from Onyx's `Content.Shared` path)
  diffed against `git -C C:/tmp/onyx show HEAD:Content.Shared/_Onyx/Medical/Tourniquet/TourniquetSystem.cs`
  (`--strip-trailing-cr`): every differing hunk carries a `// WOLFGATE` marker —
  1. the `using Content.Shared._Onyx.Targeting;` → two-using swap (D10),
  2. `INetManager`/`Robust.Shared.Network` drop,
  3. `TargetResolverSystem` → `WoundTargetResolver` dependency,
  4. `QueueDel(tourniquet)` guard simplification (`OnDoAfter`),
  5. `Apply()`'s `!_net.IsServer || !CanApply(...)` → `!CanApply(...)` guard simplification.
  No unmarked hunk. The report's note about the plan text describing "3 edits" when the vendored file
  actually needed a 4th marked site (the second `_net.IsServer` half-guard inside `Apply()`) is accurate —
  both `_net.IsServer` sites are the same D13 rationale at two call sites, correctly recorded in the
  manifest as "4 marked edits" rather than a new deviation.
- `Resources/Locale/en-US/_Onyx/medical/tourniquet.ftl` — diffed against
  `git -C C:/tmp/onyx show HEAD:Resources/Locale/en-US/_Onyx/medical/tourniquet.ftl`: **identical**, all 3 keys.

**PASS.**

## 4. Collisions

- `grep -rn "SubscribeLocalEvent<TourniquetComponent"` across the whole worktree: only the three
  registrations inside the new `TourniquetSystem.cs` (`UseInHandEvent`, `AfterInteractEvent`,
  `TourniquetDoAfterEvent`). No pre-existing or duplicate registrant.
- `grep -rn "class TourniquetComponent"` / `"class TourniquetSystem"`: exactly one each, both the new files.
- `grep -rn "^\s*id: Tourniquet$"` over `Resources/`: exactly one hit, the existing entity in `healing.yml`
  being edited in place (no second `id: Tourniquet` created).

**PASS — no collisions.**

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` carries a `### WP12-3 (phase 4 — tourniquet, P4-2b)` section with a row
for every file touched: `TourniquetComponent.cs`, `TourniquetSystem.cs` (relocated), `healing.yml`
(PROTO D), `firstaidkits.yml` (PROTO E), `tourniquet.ftl`, plus the manifest-section row itself. It lists
all 4 marked edits with reasons, the traps avoided, the subscription pairs, the D2 spot check, and the
build checkpoint. Matches the report and the on-disk diff exactly.

**PASS.**

## 6. Plan conformance

Every file in PLAN4 §WP12-3's table exists at the specified path:

1. `TourniquetComponent.cs` — same path, verbatim. ✓
2. `TourniquetSystem.cs` — relocated to `Content.Server`, namespace unchanged, edits marked. ✓
3. `healing.yml` — PROTO D in-place swap. ✓
4. `firstaidkits.yml` — PROTO E one line. ✓
5. `tourniquet.ftl` — new, verbatim, 3 keys. ✓
6. `WOLFMED_MANIFEST.md` — WP12-3 section appended. ✓

No new sprites/audio needed (both pre-existing in WG, confirmed unreferenced-as-new in the diff). No fill/
vending/spawner file beyond PROTO E touched. `MedkitCombatFilled` confirmed untouched. No `Tourniquet` tag
added (P4-D9). No second `id: Tourniquet` entity. `TourniquetSystem.cs` correctly relocated to
`Content.Server`, not left in `Content.Shared`.

DECISIONS.md §8.4 answers: none of the eight §8.4 items (explosion amputation, organ heal rate, surgery
scarring, reagent tier, analyzer UI, organ examine, treatmentCapabilities annotation, pain numbness) name or
bear on the tourniquet package — all are honoured by other WPs already in the tree, none contradicted here.

One deviation, textual only, correctly flagged in both the report and the manifest: PLAN4 §2.2 said "3
edits" and named only the `OnDoAfter` guard; the vendored file needed the same fix applied at a second,
textually-identical `_net.IsServer` site inside `Apply()` to compile at all once `INetManager` is dropped.
Same rationale, same WP, not a scope expansion — accepted.

**PASS.**

## 7. Snapshot

```
git -C .../rules-motd-updates-11c89c diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p4/snapshots/WP12-3.patch
git -C .../rules-motd-updates-11c89c ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/p4/snapshots/WP12-3.untracked.txt
```

Written: `WP12-3.patch` (494 lines), `WP12-3.untracked.txt` (38 lines, includes the tourniquet files plus
the untracked files from WP12-0/WP12-1 already sitting in the worktree ahead of this package).

**Done.**

## Verdict

No blocker, no major. **PASS.**
