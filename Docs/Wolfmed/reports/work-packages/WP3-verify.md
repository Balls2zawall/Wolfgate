# WP3 verification — Circulation data layer + CCVars + targeting snapshot

Verifier pass over `wp/WP3-report.md` against `DECISIONS.md` and `PLAN.md` (§1, §3, §4 WP3, §5, §8.3).

## 1. Build

Both builds run sequentially from the worktree, 0 errors each.

```
$ dotnet build .../Content.Server/Content.Server.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)

$ dotnet build .../Content.Client/Content.Client.csproj -c DebugOpt -v q -nologo
Build succeeded.
    0 Error(s)
```

**PASS.**

## 2. Upstream discipline

```
$ git diff HEAD --stat -- Content.Shared Content.Server Content.Client Resources Content.IntegrationTests
 Content.Shared/_Shitmed/Targeting/SharedTargetingSystem.cs | 4 ++++
 1 file changed, 4 insertions(+)
```

Exactly one tracked (upstream, non-`_Onyx`/`_WF`) file is modified anywhere in the worktree:

```diff
+    // WOLFGATE: Wolfmed snapshot/targeting needs a single-bit check; copied from Onyx's SharedTargetingSystem.
+    public static bool IsSelectable(TargetBodyPart part)
+        => part != 0 && (part & (part - 1)) == 0 && (part & TargetBodyPart.All) != 0;
```

This is HOOK 5 (PLAN.md §3, row "HOOK 5"), authorised for WP2 — it already landed before WP3 started (confirmed by the WP3 report and by its content matching §2.14 verbatim). It carries a `// WOLFGATE` marker and both added lines are inside the marked comment+method block. WP3 itself touched **zero** upstream files.

**PASS — no unauthorised upstream edits.**

## 3. Vendoring fidelity

All 9 files WP3's report table lists, diffed against `git -C C:/tmp/onyx show HEAD:<path>` with `diff --strip-trailing-cr`:

| # | File | Diff vs Onyx | Marker present |
|---|---|---|---|
| 1 | `CirculatoryStreamComponent.cs` | none (byte-identical mod CRLF) | n/a |
| 2 | `CirculatoryStreamPrototype.cs` | `using Content.Shared.Metabolism;` commented out; `MetabolismStage`/`MetabolitesStage` fields commented out | yes — `// WOLFGATE (D15/WP3#2): ...` on every changed line |
| 3 | `CCVars.Wounds.cs` | none | n/a |
| 4 | `CCVars.Surgery.cs` | none | n/a |
| 5 | `CCVars.Targeting.cs` | none | n/a |
| 6 | `DamageDistribution.cs` | none | n/a |
| 7 | `TargetingSnapshotComponent.cs` | added `using Content.Shared._Shitmed.Targeting;`; `RequestedTarget` default `Chest`→`Torso` | yes — `// WOLFGATE (D10/WP3#7)` on the using, `// WOLFGATE (D9)` on the default |
| 8 | `TargetingSnapshotSystem.cs` | added `using Content.Shared._Shitmed.Targeting;` | yes — `// WOLFGATE (D10/WP3#8)` |
| 9 | `circulatory_streams.yml` | none | n/a |

Every differing hunk carries a `// WOLFGATE` marker. No unmarked drift.

**PASS.**

## 4. Subscriptions

Grepped all 8 WP3 C# files for `SubscribeLocalEvent`:

```
Content.Shared/_Onyx/Targeting/TargetingSnapshotSystem.cs:11:        SubscribeLocalEvent<ThrownEvent>(OnThrown);
```

This is a single-type (broadcast) subscription, not a directed `SubscribeLocalEvent<TComponent, TEvent>` pair — WP3 adds **zero** directed `(component, event)` pairs. Matches the report's note ("system not yet `Initialize()`d by anything but its own `ThrownEvent` subscription — that's Onyx's own behaviour, verbatim") and PLAN.md §5, which lists no WP3-originated pairs in either §5.1 (conflicts) or §5.2 (new pairs) — all listed pairs belong to WP1/WP2/WP4/WP5/WP6 systems.

**PASS — nothing to check for duplication; matches PLAN.md §5.**

## 5. Manifest

`Docs/Wolfmed/WOLFMED_MANIFEST.md` has one row per WP3 file, all tagged `WP3`, plus a "### WP3" Deviations subsection covering the two documented deviations (Metabolism `using` removal, Targeting "using addition not swap"). All 9 files accounted for; no WP3 file missing a row.

**PASS.**

## 6. Plan conformance

- All 9 files in PLAN.md §4 WP3's table exist at the planned destination (`same` path in every row) — confirmed present on disk.
- File count matches exactly (9).
- D-numbers cited by WP3 (D9, D10, D15) all honoured:
  - **D9** — `TargetingSnapshotComponent.RequestedTarget` default is `TargetBodyPart.Torso`, not `.Chest` (which doesn't exist in Shitmed's enum). Correct fold.
  - **D10** — Onyx's own Targeting stack (`TargetBodyPart`, `TargetingComponent`, `SharedTargetingSystem`, `TargetResolverSystem`) is not vendored; both snapshot files instead pull `Content.Shared._Shitmed.Targeting`, confirmed by reading the files — `TargetingComponent.Target` (Shitmed) is a `TargetBodyPart` field, resolves cleanly, and `SharedTargetingSystem.IsSelectable` (HOOK 5) is called correctly.
  - **D15** — `CirculatoryStreamPrototype`'s `MetabolismStage`/`MetabolitesStage` fields and their `using` are commented out with `// WOLFGATE (D15/...)` tags; `CirculatoryStreamSystem.cs` correctly deferred to WP6 (not touched here); `SharedSolutionContainerSystem.CirculatoryStreams.cs` correctly not ported.
- Deviations from the plan's literal wording (the `using Content.Shared.Metabolism;` removal, and the Targeting "using addition" vs. literal "using swap") are cosmetic/mechanical, justified in both the report and the manifest, and do not change behaviour from what §4 WP3 specifies.
- Zero CCVar name / cvar-string collisions independently re-verified: extracted all 13 new `CVarDef` member names and cvar strings from the three CCVar files and grepped the rest of `Content.Shared`/`Content.Server`/`Content.Client` — no hits outside the new files.

**PASS.**

## 7. Snapshot

```
$ git diff HEAD -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP3.patch
$ git ls-files --others --exclude-standard -- Content.Shared Content.Server Content.Client Resources Docs Content.IntegrationTests > C:/tmp/wolfmed-plan/snapshots/WP3.untracked.txt
```

`WP3.patch` — 13 lines (the single HOOK 5 upstream hunk plus headers).
`WP3.untracked.txt` — 42 files (WP1 + WP2 + WP3's own new files + Docs), all previously accounted for; no stray files outside the WP1/WP2/WP3/Docs scope.

## Verdict

No blocker or major issues found. All 7 checks pass.

**pass = true**
