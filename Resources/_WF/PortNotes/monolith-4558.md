# Monolith #4558: Xenoborg Buffs

## Source

- PR: https://github.com/Monolith-Station/Monolith/pull/4558 (open when imported).
- Pinned head: `1ab354cf3014f15806e6537e1f4d1eaf3797852b`.
- PR base: `48b5e34cd7040af9f37abefcd60333f8f4c8af06`.
- Existing Wolfgate xenoborg systems, roles, factions, ghost spawners, and guidebook are reused.
  This is not a second implementation of the antagonist.
- Imported code/resources retain their upstream paths, namespaces, IDs, maps, and asset metadata.
  Wolfgate-only announcements and tests live in `_WF`.

## Scope and compatibility

- Adds the votable `MonoXeno` / Reclamation preset and all three xenoborg shuttle events.
- As explicitly requested, adds the slow xenoborg scheduler to `MonoMixed` and `MonoAllAtOnce`.
  Existing preset visibility, weights, faction rules, and the configured default preset are unchanged.
- Reclamation uses Wolfgate's `ManualPortstrikeTsfPdv` instead of upstream's timed portstrike rule,
  matching the local TSF/PDV presets. No engine/submodule upgrade is included.
- Keeps upstream event population thresholds (15/32/60), weights (2/3/2), occurrence caps (4),
  and scheduling delays. Reclamation remains votable below 15 players, but cannot select a xenoborg
  ship then. The slow scheduler first runs after three hours; Apocalypse's existing three-hour
  round-end rule may prevent it from firing before the round ends. These upstream balance choices
  are intentionally not redesigned by this port.
- Includes the engineering starter module/nanite applicator, regenerating plasma jetpacks,
  recipe costs, access overrider support, crates, walls, floors, sprites, and three maps.
  Both crate variants retain upstream's material fills; the basic nanite applicator is not made
  self-recharging.
- Supplies the three announcement localization keys missing from the upstream PR.
- Follow-up visual adaptation: `WallNanolaminateXorgDiagonal` uses the existing
  `Structures/Walls/xenoborg_diagonal.rsi` for both its sprite and icon. This replaces upstream's
  single-direction top-down blue stripe with AftrLite's attributed four-direction wallening corners.
  Collision, smoothing, durability, and map placement are unchanged. The original imported RSI is
  retained for upstream comparison, but this prototype no longer uses it.
- Normalizes touched recipe indentation and final newlines. Skips the upstream newline-only
  change to `_Mono/events/shuttles.ftl`.
- No prototype renames or save migrations are introduced.

## Attribution and unresolved provenance

Imported content remains upstream-authored, not newly authored Wolfgate content. See the pinned
repository's licensing and this repository's `README.md`/`LEGAL.md` for inherited code licensing.
The new upstream C# files have no per-file copyright/SPDX headers; none have been invented.

RSI metadata is preserved verbatim:

- `xeno.rsi`: CC-BY-SA-4.0, "Made by microwave".
- `xorgreinforced.rsi` and `xorgreinforced_Diag.rsi`: CC-BY-SA-3.0, "Taked From TG Station".
- Existing `Structures/Walls/xenoborg.rsi` attribution and `Tiles/exoborg.png` attribution are retained.

The user explicitly requested importing the following loose textures unchanged despite missing
upstream attribution entries: `Tiles/Malicous_blue_circuit.png`, `Tiles/Xeno_Mono.png`, and
`Tiles/Xeno_Squares.png`. Their exact source is the pinned PR; individual authorship/license provenance
has **not** been verified. User approval does not resolve that licensing gap. Attribution must be
confirmed with upstream before considering the asset licensing review complete; do not fabricate
copyright or license entries. The upstream replacement sprites also lack updated attribution.

## Verification and manual test plan

Automated checks performed on Windows with .NET SDK 10.0.203:

```text
dotnet restore
dotnet build --configuration DebugOpt --no-restore /p:WarningsAsErrors=nullable /m
dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj -c DebugOpt -- NUnit.ConsoleOut=0
dotnet test --no-build --configuration DebugOpt Content.Tests/Content.Tests.csproj -- NUnit.ConsoleOut=0
dotnet test --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~XenoborgPortTest" -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed
dotnet test --no-build --configuration DebugOpt Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName!~ShipyardTest" -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed
git diff --check
```

- Restore and build succeeded (0 build errors; compiler/analyzer and dependency vulnerability warnings remain).
- YAML linter: no errors.
- Unit tests: 521 passed, 1 skipped.
- New `XenoborgPortTest`: 4 passed, covering all three shuttle loads, initial plasma, regeneration,
  pressure cap, disabled/empty recharge, and gas ratio normalization.
- Integration suite excluding shipyard tests: 1,845 passed, 23 skipped (includes the four new tests).
- Diff whitespace check passed. All 35 imported map/asset files match the pinned upstream Git blob hashes.
- RobustToolbox remains pinned to `08a3d120b7029d03e60b44b23fed2b2659ed3224`.

Python validator dependencies were installed only in a temporary virtual environment, outside the repository.
Using its Python interpreter:

```text
python RobustToolbox/Schemas/validate_rsis.py Resources/
python RobustToolbox/Schemas/validate_rsis.py Resources/Textures/_Mono/Structures/Storage/Crates Resources/Textures/_Mono/Structures/Walls Resources/Textures/Structures/Walls
python -X utf8 .github/mapchecker/mapchecker.py
```

- Full RSI scan found two unrelated local directories without `meta.json`:
  `Resources/Textures/Objects/Materials/xenoborg_crystal.rsi` and
  `Resources/Textures/Structures/Machines/xenoborg_extractor.rsi`. Neither path has tracked files in HEAD
  or is imported by this port. They were left untouched.
- Scoped RSI scan covering all changed RSI directories passed.
- Map prototype checker passed with existing missing-path/empty-map warnings. UTF-8 mode is needed
  on this Windows environment; without it, parsing stopped at the unchanged `quiver.yml`.
  This checker is not a substitute for the dedicated new shuttle load tests.
- The separate GitHub Actions YAML map-schema validator was not run locally.

Compilation/resource checks and headless integration tests are not an in-game playtest.

After the diagonal-wall sprite adaptation, reran the YAML linter (`--no-build`, no errors),
`XenoborgPortTest` (`--no-build`, all 4 passed), RSI validation over
`Resources/Textures/Structures/Walls` (passed), and `git diff --check` (passed).
The full integration suite above predates that visual-only follow-up. In-game inspection of all
four corner rotations and their seams with straight walls remains required.

Manual checks still required:

1. Select Reclamation via the vote or `setgamepreset MonoXeno`; check localized title/description.
2. Trigger each `UnknownShuttleXenoSmall`, `UnknownShuttleXenoMedium`, and `UnknownShuttleXenoLarge`
   event; check announcements, spawn placement, population gates, and occurrence limits.
3. Board each ship, claim its ghost roles, and verify stealth IFF, power, docking, floors/walls,
   crate appearance/contents, and existing mothership control/repair behavior.
4. Select engineering xenoborg; use the starter nanite applicator and lathe recipes. Deplete a
   defense/heavy-laser module jetpack and confirm plasma regeneration and the pressure cap.
5. Verify core destruction, xenoborg death/respawn, role cleanup, and round-end behavior.
6. Capture in-game media before submitting a gameplay PR. No manual playtest/media is claimed here.

This port includes AI-assisted integration, tests, documentation, and English announcement text;
retain the local PR template's AI-content disclosure requirements.

## Player-facing changelog

:cl:
- add: Added the Reclamation xenoborg gamemode and three xenoborg event ships.
- add: Mixed and Apocalypse rounds can now schedule xenoborg ships after three hours.
- tweak: Xenoborg engineers start with a nanite applicator, and xenoborg combat jetpacks regenerate fuel.
- tweak: Added xenoborg material crates, walls and floors, and adjusted xenoborg chassis/module costs.
- fix: Added missing xenoborg access, floor, and shuttle announcement text.
- fix: Xenoborg nanolaminate wall corners now use the existing directional wallening artwork.
