# WP12-6 — Analyzer payload and server (P4-4a)

PLAN4 §4 WP12-6, items 1-7. All three builds green, headless server clean, manifest appended.

## 1. Files created / modified

| File | Status |
|---|---|
| `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerWoundDiagnostic.cs` | **new** (vendored from ONYX same path), 1 marked `using` edit |
| `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerOrganInfo.cs` | **new** (vendored), byte-verbatim, 0 edits |
| `WG/Content.Shared/_Onyx/Medical/HealthAnalyzerChemicalInfo.cs` | **new** (vendored), byte-verbatim, 0 edits |
| `WG/Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` | **modified — EXT 2**: 2 `using`s, 4 fields, 4 appended optional ctor params, 4 assignments (+14/-2) |
| `WG/Content.Server/_WF/Wolfmed/Medical/HealthAnalyzerSystem.Wolfmed.cs` | **new** (`_WF` partial of `Content.Server.Medical.HealthAnalyzerSystem`), 223 lines |
| `WG/Content.Server/Medical/HealthAnalyzerSystem.cs` | **modified — HOOK 23**: 1 marked line (+3/-1 incl. the comma) |
| `WG/Docs/Wolfmed/WOLFMED_MANIFEST.md` | appended `### WP12-6` |

No other file touched. `CryoPodSystem.cs` deliberately untouched (its nine positional arguments still compile).

## 2. Every WOLFGATE edit and reason

**Upstream (2 files):**

1. **HOOK 23** — `Content.Server/Medical/HealthAnalyzerSystem.cs`, one marked line appended to the
   `ServerSendUiMessage` argument list after `part != null ? GetNetEntity(part) : null,`:
   `BuildWoundDiagnostics(target), BuildOrganInfo(target), BuildChemicalInfo(target, bloodstream), BuildVitalDamage(target) // WOLFGATE: HOOK 23`.
   No `using`, no `[Dependency]` landed upstream — all five live in the `_WF` partial. `bloodstream` was
   already in scope from the `TryComp` at `:256`.
2. **EXT 2** — `Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs`: `using Content.Shared._Onyx.Medical;`
   and `using Content.Shared.FixedPoint;` (both marked); the four nullable fields `WoundDiagnostics` / `Organs` /
   `Chemicals` / `VitalDamage` after `Uncloneable`; four appended optional ctor parameters after
   `NetEntity? part = null`; a marked `// WOLFGATE: EXT 2 start/end` assignment block. Purely additive.

**Vendored (`_Onyx`, 1 edit total):**

3. `HealthAnalyzerWoundDiagnostic.cs` — `using Content.Shared._Onyx.Targeting;` →
   `using Content.Shared._Shitmed.Targeting;` (D10: `_Onyx/Targeting` is not ported because its
   `TargetingComponent` registers the bare name `"Targeting"` Shitmed already claims; `TargetBodyPart` is
   identical). The other two vendored files are verbatim.

**`_WF` file (marked in place, deviations from Onyx's body):**

4. `BuildWoundDiagnostics` gates on `HasComp<WoundHostComponent>` rather than Onyx's `SurgeryTargetComponent`
   — D2 / §8.5 trap 20 (a borg or a Protogen would otherwise get an always-empty dict rendering as "no
   findings" instead of "unavailable").
5. `SharedTargetingSystem.TryConvert` → `_bodySystem.GetTargetBodyPart(PartType, Symmetry)` (WG has no
   `TryConvert`). It returns `null` for anything unmappable, so **no `Groin` key can be emitted** (D9, trap 18).
6. `BuildOrganInfo` reads `WolfmedOrganComponent.Health/.MaxHealth` and orders by `OrganComponent.SlotId`
   (D8; Onyx's `OrganComponent.Health` and `OrganCategoryPrototype` are Nubody). Organs lacking
   `WolfmedOrganComponent` are skipped, not reported at 0/0.
7. `BuildChemicalInfo` reads `bloodstream.ChemicalSolutionName` instead of Onyx's `MetabolitesSolutionName`,
   **keeping `HealthAnalyzerSolutionType.Metabolites` as the wire value** so the payload type does not churn
   (WP12-7 localises that row as "Chemicals"). Its gate drops Onyx's `SurgeryTarget` requirement.

Everything else in the three builders is Onyx's logic line-for-line: worst untreated fracture, summed bleeding
rate with the treatment taken from the highest-rate bleeder, scar count, open-wound internal bleeding
(`Rate * Severity.Float()`), the four-way clotting phase collapse (0 → `NotApplicable`, 1 → itself, >1 →
`Mixed`), visible wounds keyed `(prototype.Name, stage?.Name)` skipping non-`Visible` prototypes and
`Healed`/`Scarred` states, `_pain.GetPain`, `_functionality.GetState`, and the `HasFindings` gate.

## 3. Deviations from PLAN4

**None behavioural.** Two textual notes, both forced by WG type signatures:

- `BuildChemicalInfo` returns `List<HealthAnalyzerChemicalInfo>?` — PLAN4 §2.9's own signature; Onyx's is
  non-nullable. It never returns `null` in practice; the nullable form matches the message field's type.
- `OrganOrder(string? slotId)` rather than the plan's `string` — `OrganComponent.SlotId` is nullable in WG. A
  null slot falls to the `_ => 10` arm exactly as an unknown id does.

Everything else in §2.8 / §2.9 / §3.1 HOOK 23 / §3.2 EXT 2 was implemented as written. `BuildPartDamage` was
**not** ported (P4-D27). All four builders are `public` (P4-D26). No subscription and no component was
registered (§5.1 lists WP12-6 as registering nothing; §5.2's analyzer pairs were left to their existing owners
at `HealthAnalyzerSystem.cs:46-55`). All eight new type names grepped 0 hits repo-wide before creation and
resolve to exactly one definition each now.

## 4. Build / run output

```
dotnet build Content.Server   -c DebugOpt  ->  Build succeeded.  0 Error(s)
dotnet build Content.Client   -c DebugOpt  ->  Build succeeded.  0 Error(s)
dotnet build Content.IntegrationTests -c DebugOpt -> Build succeeded.  0 Error(s)
```

`Content.Client` is the build that catches a non-NetSerializable field (WP12-6's own checkpoint note) and it is
green.

Headless server, port 1299, ~130 s (`C:/tmp/wolfmed-plan/p4/wp/WP12-6-report-server.log`, 111 lines):

```
[INFO] root: Server Version 277.0.0.0 -> Ready
grep -cE "\[ERRO\]|\[FATL\]|Exception"  ->  0
```

This also exercises the serializer's startup scan over the four new `[NetSerializable]` types and the
resolution of the five new `[Dependency]` fields. No YAML/FTL/XAML/RSI was touched, so no Release lint run.
No integration tests run — none are specified for WP12-6 (`T-AN-*` belongs to WP12-9).

## 5. What later packages must know

**WP12-7 (analyzer client UI) — the contract this package ships:**

- `msg.WoundDiagnostics == null` means **"diagnostics unavailable"** (non-wound-host: borg, Protogen, animal).
  `msg.Organs == null` likewise. Hide the whole panel when both are null — that is WP12-7's D2 gate.
- `msg.WoundDiagnostics != null` with an **empty** `Parts` dictionary means "wound host, no findings". Clean
  parts are dropped by `HasFindings`, so the dict is sparse by design; do not assume 10 keys.
- `Parts` keys are `Content.Shared._Shitmed.Targeting.TargetBodyPart` and can never contain `Groin` (D9).
  Render in `SharedTargetingSystem.GetValidParts()` order.
- `msg.Chemicals` is non-null for essentially any scanned target (its gate is not wound-gated), and a
  `HealthAnalyzerSolutionType.Metabolites` row is **Wolfgate's `chemicals` solution** — localise it as
  "Chemicals", not "Metabolites".
- `msg.VitalDamage` is non-null only for wound hosts; it is `MobThresholdSystem.CheckVitalDamage`, i.e. the
  figure that actually decides crit, which diverges from the window's existing projection-sum "Total Damage".
- `HealthAnalyzerVisibleWound.Name` and `.StageName` are `LocId`s from the wound prototype / stage definition —
  `Loc.GetString` them, do not print raw. `StageName` is nullable.
- `HealthAnalyzerOrganInfo.Entity` is a `NetEntity`; `Order` is already sorted ascending by the server.
  **Only 7 organ rows exist for a human and none for any other species** until phase 5 annotates more organs.

**WP12-9 (tests):** the four builders are `public` on `Content.Server.Medical.HealthAnalyzerSystem`, so
`T-AN-*` can call `BuildWoundDiagnostics/BuildOrganInfo/BuildChemicalInfo/BuildVitalDamage` directly on the
server system without a client harness (P4-D26). There is still no capture point for the wire message itself.

**WP12-10 (reconcile):** upstream files newly touched by this package are
`Content.Server/Medical/HealthAnalyzerSystem.cs` (HOOK 23, first Wolfmed touch) and
`Content.Shared/MedicalScanner/HealthAnalyzerScannedUserMessage.cs` (EXT 2, first Wolfmed touch) — both counted
in §3.4's revised 46/19 arithmetic. `Content.Shared/_Onyx/Medical/` now holds the three payload files beside
WP12-3's `Tourniquet/` subfolder. No second licence to record: all three vendored files carry no header in
Onyx and none was invented.

**Anyone editing the message again:** the four new parameters are trailing and optional specifically so
`CryoPodSystem.cs:206-221`'s nine positional arguments keep compiling. Do not reorder the fields — NetSerializer
emits members in declaration order.
