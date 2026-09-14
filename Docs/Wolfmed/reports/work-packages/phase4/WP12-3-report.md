# WP12-3 report — Tourniquet (P4-2b)

## 1. Files created / modified

| File | Status |
|---|---|
| `Content.Shared/_Onyx/Medical/Tourniquet/TourniquetComponent.cs` | new (vendored), verbatim |
| `Content.Server/_Onyx/Medical/Tourniquet/TourniquetSystem.cs` | new (vendored, relocated from Onyx's `Content.Shared` path per D13), 4 marked edit sites, namespace unchanged (`Content.Shared._Onyx.Medical.Tourniquet`) |
| `Resources/Prototypes/Entities/Objects/Specific/Medical/healing.yml` | modified — PROTO D (in-place `Healing` → `Tourniquet` block swap on the existing `id: Tourniquet` entity) |
| `Resources/Prototypes/Catalog/Fills/Items/firstaidkits.yml` | modified — PROTO E (one line, `MedkitAdvancedFilled` gains `Tourniquet`) |
| `Resources/Locale/en-US/_Onyx/medical/tourniquet.ftl` | new, verbatim (3 keys) |
| `Docs/Wolfmed/WOLFMED_MANIFEST.md` | appended `### WP12-3` section |

Zero new sprites/audio (both already present in WG: `medical.rsi` tourniquet states, `brutepack_{begin,end}.ogg`).
Zero touched fill/vending/spawner files beyond PROTO E — the `Tourniquet` id is unchanged so its 8 other
references keep working.

## 2. Every WOLFGATE edit and reason

In `TourniquetSystem.cs` (all `// WOLFGATE` marked):

1. `using Content.Shared._Onyx.Targeting;` → `using Content.Shared._Shitmed.Targeting;` + `using Content.Shared._WF.Wolfmed.Targeting;` — D10. Onyx's `TargetingComponent` registers as the bare name `"Targeting"`, already claimed by Shitmed's own copy; porting Onyx's is a `ComponentFactory` boot crash. Shitmed's `TargetingComponent.Target` field is identical, so `targeting.Target` reads unchanged.
2. `[Dependency] private TargetResolverSystem _targeting` → `[Dependency] private WoundTargetResolver _targeting` — `TargetResolverSystem` doesn't exist in WG (D5). `WoundTargetResolver.TryResolveExact(EntityUid, TargetBodyPart, out EntityUid)`, shipped in phase 1, is a signature-exact replacement.
3. Dropped `[Dependency] private INetManager _net` and its `using Robust.Shared.Network;`; simplified the `OnDoAfter` `if (_net.IsServer) QueueDel(tourniquet);` guard to a bare `QueueDel(tourniquet);`, and simplified a second, plan-unlisted `if (!_net.IsServer || !CanApply(body, part))` in `Apply()` to `if (!CanApply(body, part))` — the class is `Content.Server`-only after the D13 relocation, so both conditions collapse to always-true/always-false. Both sites carry the same WOLFGATE reason.

No edits in `TourniquetComponent.cs` (verbatim per plan).

YAML: PROTO D (healing.yml, in-place block swap, `# WOLFGATE` comment) and PROTO E (firstaidkits.yml, one `# WOLFGATE (P4-2)` line) as specified in PLAN4 §3.2.

## 3. Deviations from PLAN4

One textual-only correction, no behavioural deviation: PLAN4 §2.2 describes "3 edits" and quotes only the
`OnDoAfter` `_net.IsServer` guard. The vendored file actually carries a second, textually identical
`_net.IsServer` check inside the public `Apply()` method (`if (!_net.IsServer || !CanApply(body, part))`),
which the plan's prose and quoted diff didn't mention. Since `INetManager` is dropped entirely, this second
site had to be touched too or the file would not compile. It is the same edit (same field, same D13 rationale)
applied at a second site, not a new deviation — recorded in the manifest as "4 marked edits" rather than 3.
Everything else matches PLAN4 §2.2/§3.2/WP12-3 exactly: no `Tourniquet` tag added (P4-D9), no second
`id: Tourniquet` entity, `MedkitCombatFilled` left untouched, `TourniquetSystem.cs` relocated to
`Content.Server` (not left in `Content.Shared`).

## 4. Build / test output tails

- `dotnet build Content.Server -c DebugOpt`: **Build succeeded. 0 Error(s).**
- `dotnet build Content.Client -c DebugOpt`: **Build succeeded. 0 Error(s).**
- `dotnet build Content.IntegrationTests -c DebugOpt`: **Build succeeded. 0 Error(s).**
- Headless server (~120 s, port 1299): reached `[INFO] root: Server Version 277.0.0.0 -> Ready`, then
  bound the network socket. **Zero** `[ERRO]`/`[FATL]`/`Exception` lines in the whole log.
  Log: `C:/tmp/wolfmed-plan/p4/wp/WP12-3-report-server.log`.
- Release YAML lint (`dotnet run --project Content.YAMLLinter -c Release`): **1 error**, and it is not this
  package's — the pre-existing WP12-0 hazard on `Resources/Prototypes/_Onyx/Entities/Objects/Specific/Medical/medical_patch.yml`
  (`File not found. (/Textures)`, missing `icon:` on two construction prototypes, already on record in the
  manifest from WP12-2). Nothing was reported against `healing.yml`, `firstaidkits.yml`, or the new locale file.
- No integration tests were run for this WP: WP12-3's section names no test filter (the tourniquet test,
  `TourniquetStopsOnlySelectedPartTest`, belongs to WP12-9).

## 5. What later packages must know

- `TourniquetSystem` lives in `Content.Server/_Onyx/Medical/Tourniquet/`, namespace
  `Content.Shared._Onyx.Medical.Tourniquet` (namespace deliberately unchanged from Onyx despite the file
  relocation). Its public `Apply(EntityUid body, EntityUid part)` is available to any other server system.
- The `Tourniquet` prototype id is unchanged and untagged (no `Tourniquet` tag prototype exists in WG — do
  not add one without also authoring the tag prototype and re-checking the Release lint).
- D2/P4-D11 accepted loss confirmed by code inspection (not exercised by a runtime test here): a
  non-wound-host simply fails `CanApply` (`HasComp<WoundableComponent>(part)` returns false) and gets the
  existing "not bleeding" popup — no crash, no special-case needed elsewhere.
- The pre-existing `medical_patch.yml` Release-lint hazard (owned by WP12-0) is still unfixed and still
  blocks a fully clean Release lint for the phase; WP12-10 (or a WP12-0 re-run) needs to land the one-line
  `icon:` fix on both construction prototypes before phase 4 can claim a clean lint.
- Subscription pairs `<TourniquetComponent, UseInHandEvent/AfterInteractEvent/TourniquetDoAfterEvent>` and
  the `TourniquetComponent` registration are now live; any future package adding a second handler for any of
  these three pairs, or a second component named `TourniquetComponent`/`TourniquetSystem`, will crash at
  server start.
