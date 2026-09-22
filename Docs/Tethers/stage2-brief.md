# Stage 2 common brief

You run in an isolated git worktree that the harness forked from the MAIN checkout's HEAD, not from the feature
branch. First steps, in order, from your worktree root (PowerShell tool; keep commands plain, no subshell timing):

1. `git checkout --detach 54ddcfdeef` (stage 1 commit). Confirm `Docs/Tethers/design.md` exists.
2. Junction the engine: `cmd /c rmdir RobustToolbox` then
   `cmd /c mklink /J RobustToolbox C:\Users\jzo12\Documents\GitHub\Wolfgate\RobustToolbox`, and confirm
   `(Get-Item RobustToolbox).Attributes` shows ReparsePoint. From Bash use `cmd //c`. A build failing with NETSDK1013
   means the junction is missing. Never edit engine files. Never `Remove-Item -Recurse` the junction.
3. Read `Docs/Tethers/design.md` fully and `Content.Server/_WF/Tether/README.md`, then the stage 1 code under
   `Content.{Shared,Server,Client}/_WF/Tether/`.

Stage 1 public API (verbatim):

```csharp
// Content.Server._WF.Tether.RopeSystem
bool TryCreateRope(EntityUid a, EntityUid b, ProtoId<RopeTypePrototype> type, float length, out EntityUid? rope);
bool SetLength(EntityUid rope, float length);            // clamps to [0.5, type.MaxLength]
void BreakRope(EntityUid rope, bool refund = false, EntityUid? user = null);
float GetTension(EntityUid rope);
Vector2 GetAnchorPosition(EntityUid point);
bool TryGetBody(EntityUid point, out EntityUid body, out Vector2 localAnchor);
// Content.Shared._WF.Tether, all [ByRefEvent]
readonly record struct RopeAttachedEvent(EntityUid Rope, EntityUid Other, ProtoId<RopeTypePrototype> RopeType);
readonly record struct RopeDetachedEvent(EntityUid Rope, EntityUid Other, ProtoId<RopeTypePrototype> RopeType);
readonly record struct RopeBrokenEvent(EntityUid Rope, EntityUid EndA, EntityUid EndB, ProtoId<RopeTypePrototype> RopeType, bool Refunded);
record struct RopeCoilTargetAttemptEvent(EntityUid User, EntityUid Coil, EntityUid Target, ProtoId<RopeTypePrototype> RopeType) { EntityUid? AttachPoint; bool Handled; }
```
A body change under an end (unanchor, re-anchor, embed) re-creates the joint rather than severing. Carried ropes do
not occupy an attach point slot.

Rules:
- Three stage 2 agents run in parallel and their branches get merged afterwards. Touch ONLY the files your stage
  owns (listed in your task). Do not edit stage 1 files (`RopeSystem*.cs`, `rope.ftl`, `rope_types.yml`, `debug.yml`,
  shared Rope components) unless truly unavoidable; if you must, keep the edit tiny and list it in your report.
- New code only under `_WF` folders; upstream file edits wrapped in `// WOLFGATE` markers (YAML: `# WOLFGATE`).
- Comment style: short precise summaries, no licence headers. Sandbox whitelist applies to client/shared code.
  Only one system may make a directed subscription for a given component+event pair (server crashes at start).
- Bash heredocs over ~8 KB fail; write files with the file tools.
- Build per stage only: `dotnet build Content.Server -c DebugOpt`, `dotnet build Content.Client -c DebugOpt`, and the
  integration test project if you add tests. Filter build output with `error [A-Z]+\d+`. Run only your own tests
  (`dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c DebugOpt --filter "FullyQualifiedName~<YourFixture>"`);
  rerun any Skipped test on its own. No YAML linter, no full suite; a final gate does that. While the junction is in
  place `git status`/`git diff --stat` without a path may error; use path-limited git commands
  (`git add Content.Shared Content.Server Content.Client Content.IntegrationTests Resources Tools Docs`).
- Prototype parents and component names differ in this fork from upstream; always resolve a parent id or component
  by grepping before using it. YAML you write must load: check every component name exists as a C# component.
- When done: commit on a new branch named as given in your task (`git switch -c <branch>`), plain commit message, no
  attribution lines. Then remove the junction with `cmd /c rmdir RobustToolbox` and `mkdir RobustToolbox`.
  Do not push.
- Final report under 300 words: branch name and commit hash, files touched outside `_WF`, deviations, test results,
  what is unverified.
