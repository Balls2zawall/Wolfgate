# P2 analyst report — GUARD E2 + `_Onyx/HealthExaminable` + EmoteOnDamage pain sounds

**Scope (per task):** (1) plan the `_Onyx/HealthExaminable` port + GUARD E2, (2) verify EmoteOnDamage pain
sounds, (3) recommend the minimal phase-2 examine deliverable.

**Method:** read-only. Every symbol below was checked against the real WG worktree
(`C:/Users/jzo12/Documents/GitHub/Wolfgate/.claude/worktrees/rules-motd-updates-11c89c`) and the real Onyx
pin (`C:/tmp/onyx`, sparse — every "not in sparse set" file below was fetched with
`git -C C:/tmp/onyx show HEAD:<path>`, never guessed).

**Headline result:** the HealthExaminable port is small, low-risk, and requires exactly **two new upstream
hooks** that are not yet in PLAN.md §3's authorised list (naming them **GUARD F** and **HOOK 14** below,
continuing the existing letter/number sequence) plus **one new `_WF` compat shim**. **EmoteOnDamage pain
sounds are already fully shipped** — no phase-2 work exists there; the item should be struck from WP10's
scope and the manifest updated to say so.

---

## 1. GUARD E2 — exact definition and current location

Quoted verbatim from `PLAN.md:517`:

> | **GUARD E2** | same | `:274` | `!HasComp<WoundHostComponent>(ent) &&` in front of the
> `GetBloodLevelPercentage(...) < BloodlossThreshold` pale-message test. **Land this only together with the
> `_Onyx/HealthExaminable` port**, or the "looks pale" message silently disappears with nothing replacing
> it. | WP10 |

"same" = `Content.Server/Body/Systems/BloodstreamSystem.cs` (the file GUARD E is in).

**Current state, verified:** GUARD E is applied; GUARD E2 is not. The line has drifted from `:274` (hooks-a.md's
number, taken before WP6/WP9 edited the file) to **`BloodstreamSystem.cs:280`** today:

```csharp
278:        // If the mob's blood level is below the damage threshhold, the pale message is added.
279:        (unlabelled comment line above, see file)
280:        if (GetBloodLevelPercentage(ent, ent) < ent.Comp.BloodlossThreshold)
281:        {
```

Exact edit to make:
```csharp
280:        if (!HasComp<WoundHostComponent>(ent) && GetBloodLevelPercentage(ent, ent) < ent.Comp.BloodlossThreshold) // WOLFGATE: GUARD E2 — Onyx's HealthExaminable covers pallor for wound hosts
```
`WoundHostComponent` and the `using Content.Shared._Onyx.Wounds;` it needs are already resolvable — GUARD E's
own line 212 already added that using. **No new using needed for GUARD E2.**

The "bleeding"/"profusely bleeding" messages immediately above this line (`BleedAmount`-based, not
`GetBloodLevelPercentage`-based) are **not** part of GUARD E2 and must **not** be touched: `WoundBleedingSystem`
already projects onto the body's own `BloodstreamComponent.BleedAmount` for wound hosts (that's what GUARD E3's
`TryModifyWoundBleedProjection` does — WP6, already shipped), so those two messages already read correctly for
wound hosts today. Only the blood-*volume*-percentage pale check is Onyx's dead-battery-for-wound-hosts case.

Confirmed in the manifest already (`Docs/Wolfmed/WOLFMED_MANIFEST.md:89`): *"GUARD E2 is WP10"* — consistent
with everything above.

---

## 2. `_Onyx/HealthExaminable` — Onyx source read in full

### 2.1 `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs` (in sparse set)

Namespace `Content.Shared.HealthExaminable`, `public sealed partial class HealthExaminableSystem` — **no**
`: EntitySystem` base clause (correct partial-class syntax; the base class is declared once, in the base file).
Declares two new fields (`_wounds` : `WoundSystem`, `_prototypes` : `IPrototypeManager`) and one method,
`AddPartStatusMarkup(EntityUid examined, EntityUid examiner, FormattedMessage message)`, plus a private
`PartOrder(BodyPartType)` helper.

### 2.2 `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.Pain.cs` (in sparse set)

Same namespace/class shape. Declares `_body : SharedBodySystem`, `_pain : PainSystem`, and
`GetPainLevel(EntityUid part) : string?`.

### 2.3 Onyx's **upstream** (non-`_Onyx`) `Content.Shared/HealthExaminable/HealthExaminableSystem.cs`

**Not in the sparse checkout** — fetched with `git -C C:/tmp/onyx show HEAD:Content.Shared/HealthExaminable/HealthExaminableSystem.cs`
per the task's sparse-checkout rule. This is the file the two partials above attach to, and Onyx edits it
in place with its own `<Onyx-Tag-edited>` convention (the same convention Onyx uses in every other upstream
file this port has already touched, e.g. `BloodstreamSystem.cs`'s `<Onyx-WoundSystem-edited>`).

Onyx's edited version differs from a bare pre-Onyx file by exactly two tags:
- `<Onyx-SelfPainExamine-edited>`: `CreateMarkup` gains an `EntityUid examiner` parameter (for the
  self-vs-other pain-visibility check in `GetPainLevel`), and its one call site passes `args.User`.
- `<Onyx-PartHealthExamine-edited>`: adds `using Content.Shared._Onyx.Wounds;` and
  `[Dependency] private DamageableSystem _damageable`; wraps the entire legacy threshold-message loop in
  `if (!HasComp<WoundHostComponent>(uid))`; swaps `damage.Damage.DamageDict` for
  `_damageable.GetAllDamage((uid, damage)).DamageDict` inside that loop; and appends
  `AddPartStatusMarkup(uid, examiner, msg);` after it.

### 2.4 WG's `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` — confirmed SAME as pre-Onyx upstream

Diffed byte-for-byte against Onyx's edited file; the only differences are exactly the two tag blocks above.
**WG's file is the untouched upstream ancestor Onyx forked from** — same namespace, same class name, same
`: EntitySystem` base, same method names. This means: **the two Onyx partials are directly attachable to
WG's class as-is (same namespace, same partial class, both `Content.Shared` — no `_WF` indirection needed
for the partial mechanism itself)**. What WG's base file lacks is exactly Onyx's two edits, which must land
as a new upstream hook (see §4).

**Exact WG line numbers** (`Content.Shared/HealthExaminable/HealthExaminableSystem.cs`):
```
32:                var markup = CreateMarkup(uid, component, damage);
45:    public FormattedMessage CreateMarkup(EntityUid uid, HealthExaminableComponent component, DamageableComponent damage)
49:        var first = true;
50:        foreach (var type in component.ExaminableTypes)
...
91:        if (msg.IsEmpty)
94:        }
96:        // Anything else want to add on to this?
97:        RaiseLocalEvent(uid, new HealthBeingExaminedEvent(msg), true);
```

### 2.5 `HealthExaminableComponent.cs` — DIFFERENT, but irrelevant to wound hosts

WG: `Thresholds = { 10, 25, 50, 75 }` (4 entries). Onyx: `{ 8, 15, 30, 50, 75, 100, 200 }` (7 entries).
**Do not reconcile these.** Once GUARD-F's `if (!HasComp<WoundHostComponent>(uid))` wrap lands, every organic
wound-host mob skips this field entirely (it's the legacy threshold-message branch). WG's 4-value set keeps
serving the non-wound-host population (silicons, animals, IPC) exactly as today — this is D2's "entities
without `WoundHostComponent` behave exactly as today" guarantee, and matches WP10's own stated constraint
that GUARD-hooks must not change non-wound-host behaviour. **No edit to `HealthExaminableComponent.cs`.**

---

## 3. Symbol-by-symbol check: every external symbol the two `_Onyx` partials need

| Symbol | Used in | WG status | Evidence |
|---|---|---|---|
| `WoundSystem` (DI) | PartStatus.cs | **SAME** — `public sealed partial class WoundSystem : EntitySystem` | `Content.Shared/_Onyx/Wounds/WoundSystem.cs:17` (phase 1) |
| `IPrototypeManager` (DI) | PartStatus.cs | SAME — engine type | — |
| `SharedBodySystem` (DI) | Pain.cs | SAME | phase 1 usage elsewhere confirms |
| `PainSystem` (DI) | Pain.cs | **SAME** — `public sealed partial class PainSystem : EntitySystem` | `Content.Shared/_Onyx/Wounds/PainSystem.cs:20` (phase 1) |
| `PainSystem.GetPain(Entity<PainComponent?>)` | Pain.cs | SAME | `PainSystem.cs:165` |
| `_body.GetBodyChildren(EntityUid)` | PartStatus.cs | **SAME shape** — returns `IEnumerable<(EntityUid Id, BodyPartComponent Component)>`, named-tuple field is literally `Component`, matching Onyx's `part.Component.PartType` access | `Content.Shared/Body/Systems/SharedBodySystem.Body.cs:256` |
| `BodyPartComponent.PartType` / `.Symmetry` | PartStatus.cs | SAME | `Content.Shared/Body/Part/BodyPartComponent.cs:150,161` |
| `BodyPartType.{Head,Arm,Hand,Leg,Foot,Tail}` | PartStatus.cs (`PartOrder`) | SAME | `Content.Shared/Body/Part/BodyPartType.cs` |
| `BodyPartType.Chest` / `.Groin` | PartStatus.cs (`PartOrder`) | **MISSING** — D9 deleted these from Wolfgate's enum | see §5 |
| `WoundSystem.GetWounds(Entity<WoundableComponent?>)` | PartStatus.cs | SAME | `WoundSystem.cs:155` (phase 1) |
| `WoundScarComponent` | PartStatus.cs | SAME (`sealed partial class`, marker) | `WoundDamageComponents.cs:259` (phase 1) |
| `WoundBleedingComponent.CurrentRate` | PartStatus.cs | SAME | `WoundDamageComponents.cs:216` (phase 1) |
| `WoundState.{Healed,Scarred,Stabilized,Closed,Open}` | PartStatus.cs | SAME | `WoundDamageComponents.cs:262-269` (phase 1) |
| `WoundPrototype.Visibility` / `WoundVisibility.Visible` | PartStatus.cs | SAME | `WoundPrototype.cs:38,305` (phase 1) |
| `WoundPrototype.GetStageDefinition(FixedPoint2)` / `.ExamineDescription` | PartStatus.cs | SAME | `WoundPrototype.cs:76` (phase 1) |
| `SurgicalIncisionWound` prototype id | PartStatus.cs | SAME — prototype exists (`id: SurgicalIncisionWound`) even though the *surgery system* that opens/closes it is D7-deferred | `Resources/Prototypes/_Onyx/Wounds/wounds.yml:370` (phase 1) |
| `WoundHostComponent` | PartStatus.cs (via base-file edit), Pain.cs (indirectly) | SAME | `WoundDamageComponents.cs` (phase 1) |
| `_damageable.GetPositiveDamage(Entity<DamageableComponent>)` | PartStatus.cs | **must resolve to `WolfmedDamageableSystem`, not `DamageableSystem`** (D12) — the compat method exists exactly with this signature | PLAN.md:115, "Serves" list at PLAN.md:166 already names `HealthExaminableSystem.PartStatus.cs:44` |
| `PartStatusSystem.GetSeverity(float)` / `PartDamageSeverity` | PartStatus.cs | **MISSING** — lives in the D10-excluded `_Onyx/Targeting/PartStatusSystem.cs` | see §5 |

**One `using` needing a swap inside the vendored `PartStatus.cs`:** `using Content.Shared._Onyx.Targeting;`
must keep resolving — it does, once §5's shim exists in that same namespace. No other using in either
partial needs a change; `using Content.Shared._Onyx.Wounds;` and `using Content.Shared.Body.Part;` etc. all
already resolve against phase-1 vendored files.

---

## 4. New upstream hooks required (not yet in PLAN.md §3's authorised list)

PLAN.md's ground rule #3 caps upstream edits at "one or two lines... except where this plan explicitly
authorises more" (four named exceptions). Neither of the two hooks below is in that list yet, but both are
anticipated in spirit by the WP10 paragraph ("GUARD E2 + `_Onyx/HealthExaminable`"). **Recommend formally
adding them to PLAN.md §3 as GUARD F and HOOK 14** before implementation, both scoped to ≤2 lines per site
(5 sites total across the two files) — no site touches existing logic bodies, only wraps or extends them.

### 4.1 GUARD F — `Content.Shared/HealthExaminable/HealthExaminableSystem.cs`

Five single-purpose edits. **Recommended simplification vs. Onyx:** skip the `_damageable.GetAllDamage`
swap inside the legacy (non-wound-host) branch — it is semantically a no-op there (`damage.Damage` already
*is* the live `DamageSpecifier` for a non-wound-host, and D2 guarantees those entities are unaffected), and
skipping it means this file needs **no new `WolfmedDamageableSystem` dependency at all** — consistent with
PLAN.md §2.1's own "Serves" list, which names only `HealthExaminableSystem.PartStatus.cs:44` for the compat
facade, **not** the base file.

```csharp
// WOLFGATE: GUARD F — using for WoundHostComponent
using Content.Shared._Onyx.Wounds;
```
```csharp
32:                var markup = CreateMarkup(uid, args.User, component, damage); // WOLFGATE: GUARD F, examiner param for self-vs-other pain visibility
```
```csharp
45:    public FormattedMessage CreateMarkup(EntityUid uid, EntityUid examiner, HealthExaminableComponent component, DamageableComponent damage) // WOLFGATE: GUARD F
```
```csharp
49:        var first = true;
        if (!HasComp<WoundHostComponent>(uid)) // WOLFGATE: GUARD F — legacy threshold text is HealthExaminable's for non-wound-hosts only
        {
50:        foreach (var type in component.ExaminableTypes)
...
91:        if (msg.IsEmpty)
93:            msg.AddMarkupOrThrow(Loc.GetString($"health-examinable-{component.LocPrefix}-none"));
94:        }
        } // WOLFGATE: GUARD F, close
```
```csharp
96:        // Anything else want to add on to this?
        AddPartStatusMarkup(uid, examiner, msg); // WOLFGATE: GUARD F — Onyx's part-status list
97:        RaiseLocalEvent(uid, new HealthBeingExaminedEvent(msg), true);
```

No new `SubscribeLocalEvent` is added anywhere in this hook — `OnGetExamineVerbs`'s existing subscription
(`HealthExaminableComponent, GetVerbsEvent<ExamineVerb>`) is unchanged, so **§5's duplicate-subscription
audit has nothing new to check here.**

### 4.2 HOOK 14 — `Content.Client/Examine/ExamineSystem.cs`

Two edits, confirmed against WG's actual current file (diffed against Onyx's — the only relevant
difference; Onyx's file also carries an unrelated `<Onyx-ActiveActionExamine-edited>` feature, out of
scope, correctly not ported):

```csharp
202:            _examineTooltipOpen = new Popup { MaxWidth = 560 }; // WOLFGATE: HOOK 14 — was 400; Onyx's part-status boxes are PartStatusMaxWidth=520
```
```csharp
279:                if (!TryAddPartStatusMessage(vBox, message)) // WOLFGATE: HOOK 14
280:                {
281:                    var richLabel = new RichTextLabel() { Margin = new Thickness(4, 4, 0, 4)};
282:                    richLabel.SetMessage(message);
283:                    vBox.AddChild(richLabel);
284:                } // WOLFGATE: HOOK 14, close
```
(line numbers are WG's current `ExamineSystem.cs:202,279-282` — confirmed by direct read, not the Onyx
numbering, which differs because of the unrelated feature.)

`ExamineSystem` is `public sealed partial class ExamineSystem : ExamineSystemShared` in namespace
`Content.Client.Examine` in **both** trees, byte-identical declaration — Onyx's
`Content.Client/_Onyx/HealthExaminable/ExamineSystem.PartStatus.cs` partial attaches directly, no `_WF`
indirection needed here either.

---

## 5. New `_WF` compat shim required — the one real gap in this port

`HealthExaminableSystem.PartStatus.cs` calls `PartStatusSystem.GetSeverity(totalDamage)`. That type lives at
`Content.Shared/_Onyx/Targeting/PartStatusSystem.cs` (namespace `Content.Shared._Onyx.Targeting`), which
**D10 explicitly excludes**: *"Skip `_Onyx/Targeting/{TargetBodyPart,TargetingComponent,SharedTargetingSystem,TargetResolverSystem}.cs`, all `Content.Server/_Onyx/Targeting`, all `Content.Client/_Onyx/Targeting`, and `PartStatus*`."*
(`PLAN.md`, D10 row). Confirmed via `find` — `Content.Shared/_Onyx/Targeting/PartStatusSystem.cs` and
`PartStatusComponent.cs` are the only two matches for `PartStatus*` under Targeting, and neither is vendored
in WG today (only `DamageDistribution.cs`, `TargetingSnapshotComponent.cs`, `TargetingSnapshotSystem.cs`
exist there — D10's allowed four, minus `CCVars.Targeting.cs` which lives elsewhere).

**This was not caught by any phase-1 report** because phase 1 never touched HealthExaminable. It is a real,
previously-undocumented gap: without a fix, `HealthExaminableSystem.PartStatus.cs` fails to compile with
`CS0246 'PartStatusSystem' does not exist in the current context` the moment it's vendored.

Read in full (`git -C C:/tmp/onyx show`), the excluded file is tiny and its only non-Targeting-doll piece is
the severity classification:

```csharp
// ONYX Content.Shared/_Onyx/Targeting/PartStatusSystem.cs (full file)
public sealed partial class PartStatusSystem : EntitySystem
{
    public static PartDamageSeverity GetSeverity(float damage) => damage switch
    {
        <= 0f => PartDamageSeverity.None,
        < 15f => PartDamageSeverity.Minor,
        < 40f => PartDamageSeverity.Moderate,
        < 70f => PartDamageSeverity.Severe,
        _ => PartDamageSeverity.Critical,
    };
    public static PartStatus Missing => new(false, PartDamageSeverity.None, false, FractureGrade.None, false);
}
```
`PartStatus`/`FractureGrade`/`PartStatusComponent` (the rest of that file's sibling `PartStatusComponent.cs`)
belong exclusively to the Targeting-doll UI D10 excludes and are **not** needed by HealthExaminable — only
`PartDamageSeverity` and `GetSeverity` are.

**Recommended fix, following the exact pattern already established at PLAN.md §2.4-§2.10** (a `_WF`-authored
file that deliberately declares an *Onyx* namespace so a vendored file's `using` and unqualified call resolve
with **zero edits to the vendored file**):

```csharp
// Content.Shared/_WF/Wolfmed/Compat/PartStatusSeverity.cs
namespace Content.Shared._Onyx.Targeting; // deliberate: HealthExaminableSystem.PartStatus.cs's `using` and
                                            // its `PartStatusSystem.GetSeverity(...)` call resolve unmodified.

/// <summary>Trimmed stand-in for Onyx's Targeting PartStatusSystem: only the severity classification
/// HealthExaminable needs, without the Targeting-doll PartStatus/PartStatusComponent/FractureGrade that D10 excludes.</summary>
public static class PartStatusSystem
{
    public static PartDamageSeverity GetSeverity(float damage) => damage switch
    {
        <= 0f => PartDamageSeverity.None,
        < 15f => PartDamageSeverity.Minor,
        < 40f => PartDamageSeverity.Moderate,
        < 70f => PartDamageSeverity.Severe,
        _ => PartDamageSeverity.Critical,
    };
}

public enum PartDamageSeverity : byte
{
    None, Minor, Moderate, Severe, Critical,
}
```

Confirmed zero collision: `grep -rl "PartDamageSeverity\|class PartStatusSystem"` across
`Content.{Server,Shared,Client}` returns nothing today, and `Content.Shared._Onyx.Targeting` currently holds
only `DamageDistribution`, `TargetingSnapshotComponent`, `TargetingSnapshotSystem` (D10's other three
allowed files) — no name overlap.

This choice also means the client's `SeverityColor` switch (`"minor"→Yellow`, `"moderate"→Orange`,
`"severe"→Red`, `"critical"→Crimson`, default green) and the five `health-examinable-part-severity-*` locale
keys line up automatically, because `severity.ToString().ToLowerInvariant()` on this enum produces exactly
`none/minor/moderate/severe/critical` — unchanged from Onyx.

---

## 6. Required `// WOLFGATE` edit inside the vendored `PartStatus.cs` itself (D9)

`PartOrder(BodyPartType)` switches on `BodyPartType.Chest` and `BodyPartType.Groin`, both **deleted from
Wolfgate's `BodyPartType` enum by D9** (`{Other, Torso, Head, Arm, Hand, Leg, Foot, Tail}` — confirmed,
`Content.Shared/Body/Part/BodyPartType.cs`). Left as-is this is `CS0117` ×2. Fix, per D9's standing rule
("map Chest→Torso, delete every Groin case"):

```csharp
    private static int PartOrder(BodyPartType type) => type switch
    {
        BodyPartType.Head => 0,
        BodyPartType.Torso => 1, // WOLFGATE (D9): Chest+Groin folded to Torso
        BodyPartType.Arm => 3,
        BodyPartType.Hand => 4,
        BodyPartType.Leg => 5,
        BodyPartType.Foot => 6,
        BodyPartType.Tail => 7,
        _ => 8,
    };
```
(Gaps in the numeric literals are cosmetic only — switch expressions don't require contiguous values.)

This is the only in-file edit the vendored `PartStatus.cs` needs. `Pain.cs` needs none.

---

## 7. Locale keys

### 7.1 `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl` — port verbatim (43 lines, full content read)

Contains: `health-examinable-pain-{light,strong,terrible,agony}`,
`health-examinable-part-{title-self,title-other,summary,summary-pain,chat-line,chat-line-details,injuries,
damage-{blunt,slash,piercing,heat,cold,shock,caustic},severity-{none,minor,moderate,severe,critical},
wound-{stabilized,closed},incision-open,bleeding,scars}`, and four `wound-examine-fracture-*` /
four `wound-examine-frame-*` keys.

**Blocker if ported naively: duplicate-ID crash.** `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl`
**already contains** the four `wound-examine-fracture-*` keys, added in WP7 as a documented stopgap:

```
# WOLFGATE (WP7): BoneFractureWound's examineDescription keys live in Onyx's _Onyx/medical/health-examinable.ftl,
# which belongs to the unported HealthExaminable system (deferred to WP10). Copied here verbatim so the
# YAML linter resolves them; delete this block if health-examinable.ftl is ported later.
wound-examine-fracture-hairline = slight swelling
wound-examine-fracture-simple = severe swelling
wound-examine-fracture-displaced = unnatural deformation
wound-examine-fracture-comminuted = shattered bone
```
(`WOLFMED_MANIFEST.md:356-360` names this exact scenario and asks for the check now happening.) **Action for
whoever implements WP10: delete this 8-line block from `wounds.ftl` in the same commit that adds
`health-examinable.ftl`**, or Fluent's loader throws `PrototypeLoadException`/duplicate-ID at startup
(the "ErrorNode crash trap" from the YAML-validation-workflow memory applies to Fluent duplicate ids the
same way it applies to YAML).

The four `wound-examine-frame-*` keys have **no current consumer** — `wounds.yml` wires
`examineDescription:` only for the four organic `wound-examine-fracture-*` stages (grep-confirmed, no
`CyberneticFrameFractureWound` examine wiring exists at the pin) — so porting them is harmless dead weight,
not a collision.

### 7.2 `Resources/Locale/en-US/_Onyx/targeting/part-status.ftl` — **orphaned, do not port**

Single key, `part-status-self-examine-title`. `git -C C:/tmp/onyx grep` for this key across the **entire**
sparse checkout returns only its own definition — zero consumers anywhere in Onyx at the pin. Same class of
finding as P2-1's already-noted "`_Onyx/StatusEffects/wounds.yml`'s two entries are orphaned at the pin, skip
if so" — apply the identical rule here.

---

## 8. Client-side control — sandbox and UI-type check

`Content.Client/_Onyx/HealthExaminable/ExamineSystem.PartStatus.cs` and `PartStatusTag.cs` use:
`BoxContainer`, `ScrollContainer`, `PanelContainer`, `ContainerButton`, `RichTextLabel`, `Label`,
`StyleBoxFlat`, `Thickness`, `Control`, plus engine helpers `Vector2Helpers.Infinity`,
`FormattedMessage.{RemoveMarkupPermissive,FromMarkupPermissive,FromMarkupOrThrow,EscapeStringParameter}`,
`MarkupNode.Attributes`/`MarkupParameter.TryGetString`, and the `RichText` tag types `BoldItalicTag`,
`BoldTag`, `BulletTag`, `ColorTag`, `FontTag`, `HeadingTag`, `ItalicTag`.

**All confirmed present at their expected RT 277 paths** (`Robust.Client/UserInterface/Controls/*`,
`Robust.Client/UserInterface/RichText/{BoldItalicTag,BulletTag,HeadingTag,...}.cs`,
`Robust.Shared/Utility/{FormattedMessage,MarkupNode}.cs`, `Robust.Shared.Maths/Vector2Helpers.cs`). None of
these are Content-sandbox concerns — the client-sandbox-whitelist memory issue (BinaryWriter,
EndOfStreamException, Process) is about *Content* types leaking non-whitelisted *engine/BCL* types into
sandboxed assemblies; `Robust.Client.UserInterface.*` controls are used freely elsewhere in WG's client code
today (`ContainerButton` in the admin ban-list UI, `ScrollContainer` in half a dozen existing windows) with
no `Sandbox.yml` entry, confirming they need none here either. **No sandbox work needed.**

The `[partstatus ...]`/`[partstatusend]` markup tags Onyx invents are **not** registered engine markup tags —
`FormattedMessage.AddMarkupOrThrow`'s parser is purely syntactic (name + `key="value"` attributes), so an
unregistered tag name parses into a plain `MarkupNode` with no special engine-side handling required; the
client walks `message.Nodes` by hand looking for `Name == "partstatus"` before any node ever reaches
`RichTextLabel.SetMessage`'s `tagsAllowed` rendering path. Verified against `FormattedMessage.cs`'s actual
parse entry points — there is no tag-registry gate to satisfy.

---

## 9. EmoteOnDamage pain sounds — already fully shipped, no phase-2 work

Searched every angle the task asked for:

- `git -C C:/tmp/onyx grep -n "EmoteOnDamage\|TryEmoteWithChat\|Scream"` restricted to `_Onyx/*` wound files:
  the **only** hit is `Content.Shared/_Onyx/Wounds/PainSystem.cs:297-298`:
  ```csharp
  _chat.TryEmoteWithChat(entity, "Scream", ChatTransmitRange.HideChat,
      ignoreActionBlocker: true, forceEmote: true);
  ```
  inside `UpdatePainShock`, fired once per pain-shock arm/discharge cycle (alongside `_jitter.DoJitter` and
  `ApplyPainShockAdrenaline`). Broadened to the whole sparse tree: the only other `_chat`/emote references are
  unrelated (`RespiratorSystem`'s gasp, `EmoteEntityEffectSystem`, surgery's configurable
  `SurgeryEffects.Emote` field, and ~20 reagent prototypes' `emote: Scream` fun/toxin effects — none of
  these are wound-pain-specific and none are in scope).
- **This exact line is already vendored, byte-identical, in
  `Content.Shared/_Onyx/Wounds/PainSystem.cs:297-298` in WG today** (diffed — identical). It is phase-1
  content (WP4), and WP9's report documents fixing the actual blocker that made it inert:
  *"`new ModifyPainGainEvent()` zeroed the multiplier... pain, pain shock, the pain stun and all pain-driven
  emotes were inert"* (`WP9-report.md` §2.1) — fixed in the same session, confirmed by the 30/30 passing test
  suite.
- The mechanism this line depends on is `SharedChatSystem.TryEmoteWithChat`, which is **exactly** phase-1's
  D25/HOOK 6: `SharedChatSystem.Wolfmed.cs` (a `_WF` shared no-op) + a one-word `override` at
  `Content.Server/Chat/Systems/ChatSystem.Emote.cs:60`. **Both confirmed present and correctly wired in WG
  today** (read in full — the override reads `public override void TryEmoteWithChat(` with the `// WOLFGATE:
  HOOK 6` comment, exactly per plan).
- `"Scream"` resolves: `Resources/Prototypes/Voice/speech_emotes.yml:3` declares `id: Scream`.

**Conclusion: pain-shock screams are complete, tested, and working as of WP9.** There is nothing left to
"port" under the "EmoteOnDamage pain sounds" heading.

**Do not confuse this with WG's *separate*, unrelated `EmoteOnDamageComponent`/`EmoteOnDamageSystem`**
(`Content.Server/Chat/{EmoteOnDamageComponent,Systems/EmoteOnDamageSystem}.cs`) — a pre-existing Wolfgate/fork
feature (chance-per-damage-increase emote with a cooldown, subscribed to plain `DamageChangedEvent`) that
Onyx's `species_base.yml` also happens to use (`{50: [Scream], 80: [Scream, Crying]}`) but which **is not
wired to any WG mob prototype today** (`grep -rln "EmoteOnDamage" Resources/Prototypes` → empty). It is
dead/orphaned code in WG, structurally unrelated to the wound-pain system, and out of Wolfmed's scope
entirely — no action needed, and it should **not** be repurposed for wound pain (it would double up with the
pain-shock scream above and re-open exactly the "no `WolfmedHostComponent` marker, one source of truth"
concern D18 was written to avoid).

**Recommendation:** strike "EmoteOnDamage pain sounds" from WP10's remaining scope; update
`Docs/Wolfmed/WOLFMED_MANIFEST.md`'s WP10 paragraph to note it shipped in WP4/WP9, not WP10.

---

## 10. Duplicate directed subscription audit (task requirement)

Zero new `SubscribeLocalEvent` calls are introduced anywhere in this work package:
- `HealthExaminableSystem`'s only subscription (`HealthExaminableComponent, GetVerbsEvent<ExamineVerb>`,
  base file line 18) is untouched by GUARD F.
- Neither `_Onyx` partial (`PartStatus.cs`, `Pain.cs`) declares `Initialize()` or any subscription.
- The client `ExamineSystem.PartStatus.cs` partial declares no subscription either — `TryAddPartStatusMessage`
  is called synchronously from `UpdateTooltipInfo`, itself already invoked from existing (unchanged) call
  sites.
- `PartStatusSeverity`/`PartStatusSystem` (the new `_WF` shim, §5) is a plain `static class`, not an
  `EntitySystem` — it registers nothing and subscribes nothing.

**No new entries for PLAN.md §5. No server-start crash risk from this work package.**

---

## 11. Recommended minimal phase-2 examine deliverable

Scoped strictly to what GUARD E2 + the two HealthExaminable partials + the existing (phase-1) wound/pain
systems already produce, with no dependency on `FractureEffectsSystem`/`FractureAlertSystem` (P2-1's other,
separate scope items):

**Examining someone else** (`examined != examiner`) who has taken damage to, say, a slashed and fractured
left arm:
```
You check <name> for injuries.
▸ Left Arm: badly damaged
    Injuries: cuts · 2 closed wounds · active bleeding · unnatural deformation
```
(clicking the arm's row expands the detail panel; `severity` drives both the inline color — orange/red/
crimson — and the collapsed/expanded UI from `ExamineSystem.PartStatus.cs`.) Pain is **not** shown, because
`GetPainLevel` only returns a value when `examined == examiner` — this is Onyx's own deliberate
self-only-pain design (anti-metagame), unchanged by the port.

**Examining yourself** (`examined == examiner`) with the same wounds and moderate pain:
```
You check yourself for injuries.
▸ Left Arm: badly damaged, hurts badly
    Injuries: cuts · 2 closed wounds · active bleeding · unnatural deformation
```
(`"hurts badly"` = the `strong` pain-level bracket, ≥30 pain on that part, orange text per
`health-examinable-pain-strong`.) A part fractured badly enough to reach `PainShockThreshold` on the whole
body additionally produces, in the chat window (not the examine tooltip): a forced "Scream" emote line and a
jitter/stun — already live today per §9, not new phase-2 behaviour, just newly *visible* once players start
examining wound hosts instead of only feeling the mechanical effects.

**What is genuinely new in phase 2, minimally:** the part-by-part breakdown itself (injuries / wound states /
bleeding / scars / fracture-stage description / pain word), replacing what a wound-host examine shows *today*
— which, prior to this port, is **nothing at all for the legacy per-type threshold messages** (they're
correctly skipped for non-wound-hosts only after GUARD-F lands; today, *before* GUARD-F, a wound host still
gets the stale legacy `health-examinable-carbon-*` messages computed off `damage.Damage.DamageDict`, which is
the body's *projected* total, not per-part — a confusing, not-yet-flagged behavioural gap this port directly
fixes) plus the pale-blood-loss message GUARD E2 removes (with the part-status list now covering that
information per-part instead of as one generic "looks pale" line).

---

## 12. Summary of the phase-2 file list for this sub-scope

| # | Path | Action | Notes |
|---|---|---|---|
| 1 | `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.PartStatus.cs` | vendor, **one in-file `// WOLFGATE` edit** | §6 (D9 Chest/Groin fold in `PartOrder`) |
| 2 | `Content.Shared/_Onyx/HealthExaminable/HealthExaminableSystem.Pain.cs` | vendor, verbatim | zero edits |
| 3 | `Content.Client/_Onyx/HealthExaminable/ExamineSystem.PartStatus.cs` | vendor, verbatim | zero edits |
| 4 | `Content.Client/_Onyx/HealthExaminable/PartStatusTag.cs` | vendor, verbatim | zero edits |
| 5 | `Content.Shared/_WF/Wolfmed/Compat/PartStatusSeverity.cs` | **new** | §5 — fills the D10-excluded `PartStatusSystem.GetSeverity`/`PartDamageSeverity` gap |
| 6 | `Content.Shared/HealthExaminable/HealthExaminableSystem.cs` | **upstream hook, propose "GUARD F"** | §4.1 — 5 sites, ≤2 lines each |
| 7 | `Content.Client/Examine/ExamineSystem.cs` | **upstream hook, propose "HOOK 14"** | §4.2 — 2 sites |
| 8 | `Content.Server/Body/Systems/BloodstreamSystem.cs` | **GUARD E2** | §1 — current line 280 |
| 9 | `Resources/Locale/en-US/_Onyx/medical/health-examinable.ftl` | vendor, verbatim (43 keys) | §7.1 |
| 10 | `Resources/Locale/en-US/_Onyx/prototypes/wounds/wounds.ftl` | **edit — delete 8 lines** | §7.1, the WP7 stopgap block, or duplicate-ID crash |
| 11 | `Resources/Locale/en-US/_Onyx/targeting/part-status.ftl` | **skip — orphaned** | §7.2 |
| — | `Content.Shared/_Onyx/Wounds/PainSystem.cs` (Scream call), `SharedChatSystem.Wolfmed.cs`, `ChatSystem.Emote.cs` HOOK 6 | **no action — already shipped** | §9 |

No file in this list needs `HealthExaminableComponent.cs`, `PainSystem.cs`, `SharedChatSystem.Wolfmed.cs`, or
`ChatSystem.Emote.cs` touched again.

---

## 13. Open items / risks for the orchestrator

1. **GUARD F and HOOK 14 need sign-off before implementation** — neither is in PLAN.md §3's authorised-hooks
   table today, and ground rule #3 caps unauthorised upstream edits at "one or two lines." Both proposals
   above stay within that budget *per site* but touch 5 and 2 sites respectively in files that are otherwise
   pristine upstream code; recommend adding two rows to §3's table using the exact diffs in §4 before a WP10
   implementer starts, so the diff that lands matches what was reviewed.
2. **The `wounds.ftl` duplicate-key deletion (§7.1, file #10) is easy to forget** — it is a *removal*, not an
   addition, in a file nobody will be looking at while adding `health-examinable.ftl`. Recommend the WP10
   task description name this file explicitly, not just rely on the manifest note.
3. **No blocker.** Every symbol this sub-scope needs is either already vendored (phase 1), already shipped
   and working (EmoteOnDamage, §9), or has a concrete, small, already-precedented fix (§5's compat shim,
   §6's one-line D9 fold). Build-checkpoint risk is low: the two new upstream files compile independently of
   fracture/pain-HUD/movement-status work also scheduled for WP10, so this sub-scope can land and be tested
   in isolation.
