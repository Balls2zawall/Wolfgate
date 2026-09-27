using Content.Server._WF.Wolfmed.Gore;
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.CCVar;
using Content.Shared._WF.Wolfmed.Damage;
using Content.Shared._WF.Wolfmed.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WF.Wolfmed.Damage;

/// <summary>
/// VISUALS: the open wound and the rot on each organic limb, for the humanoid sprite, and the blood colour the wound
/// is tinted with. Rides <see cref="PartDamageVisualsComponent"/> beside the degradation stage and the treatment.
/// </summary>
/// <remarks>
/// A new wound or a bleed change refreshes its body on the next tick. What raises nothing (an infection reaching
/// Septic, a wound closing, a drug stopping a bleed) is caught by a sweep every wolfmed.overlay_refresh_seconds.
/// Machines keep their chassis visuals: a mechanical part never shows either overlay.
/// </remarks>
public sealed class WolfmedWoundOverlaySystem : EntitySystem
{
    /// <summary>What a wound is tinted when its body has no blood reagent to read.</summary>
    private static readonly Color FallbackBlood = Color.FromHex("#800000");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WolfmedGoreSystem _gore = default!;
    [Dependency] private WolfmedWoundTraitSystem _traits = default!;
    [Dependency] private WoundDamageProjectionSystem _projection = default!;
    [Dependency] private WoundSystem _wounds = default!;

    private readonly HashSet<EntityUid> _pending = new();
    private readonly Dictionary<HumanoidVisualLayers, WolfmedWoundOverlay> _woundScratch = new();
    private readonly HashSet<HumanoidVisualLayers> _rotScratch = new();
    private readonly Dictionary<WolfmedArterySite, WolfmedArteryOverlay> _arteryScratch = new();
    private TimeSpan _nextSweep;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WolfmedWoundLifecycleEvent>(OnWoundLifecycle);
        // Free pair: the treatment overlay holds this event on WoundComponent, Onyx raises it and subscribes nothing.
        SubscribeLocalEvent<WoundBleedingComponent, WoundBleedingChangedEvent>(OnBleedingChanged);
    }

    private void OnWoundLifecycle(ref WolfmedWoundLifecycleEvent args) => Queue(args.Part);

    private void OnBleedingChanged(Entity<WoundBleedingComponent> wound, ref WoundBleedingChangedEvent args) =>
        Queue(args.Part);

    private void Queue(EntityUid part)
    {
        if (!TerminatingOrDeleted(part) && CompOrNull<BodyPartComponent>(part)?.Body is { } body)
            _pending.Add(body);
    }

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now >= _nextSweep)
        {
            _nextSweep = now + TimeSpan.FromSeconds(MathF.Max(0.1f, _cfg.GetCVar(WolfmedCVars.OverlayRefreshSeconds)));
            var hosts = EntityQueryEnumerator<WoundHostComponent, PartDamageVisualsComponent>();
            while (hosts.MoveNext(out var uid, out _, out _))
                _pending.Add(uid);
        }

        if (_pending.Count == 0)
            return;

        foreach (var body in _pending)
            Refresh(body);

        _pending.Clear();
    }

    /// <summary>Recomputes a body's wound and rot overlays and networks them when they changed. Public for tests.</summary>
    public void Refresh(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || !TryComp(body, out WoundHostComponent? host) ||
            !TryComp(body, out PartDamageVisualsComponent? visual))
            return;

        var streamRate = _cfg.GetCVar(WolfmedCVars.WoundOverlayStreamRate);
        var wounds = _woundScratch;
        var rot = _rotScratch;
        var arteries = _arteryScratch;
        wounds.Clear();
        rot.Clear();
        arteries.Clear();
        var hasHead = false;
        var neck = WolfmedArteryOverlay.None;

        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (bodyPart.PartType == BodyPartType.Head)
                hasHead = true;

            if (!_projection.TryGetVisualLayer(part, out var layer) || !TryComp(part, out WoundableComponent? woundable) ||
                !_traits.IsOrganic((part, woundable)))
                continue;

            var look = GetWoundOverlay((part, woundable), host.DismembermentWound, streamRate);
            if (look != WolfmedWoundOverlay.None &&
                (!wounds.TryGetValue(layer, out var current) || WolfmedWoundOverlays.Rank(look) > WolfmedWoundOverlays.Rank(current)))
                wounds[layer] = look;

            if (IsRotting((part, woundable)))
                rot.Add(layer);

            // Playtest 4: the arteries. The head's own when an arterial bleed is on it; the neck's is the torso's stump,
            // shown once the head is gone.
            if (bodyPart.PartType == BodyPartType.Head)
            {
                var artery = GetArteryOverlay((part, woundable));
                if (artery != WolfmedArteryOverlay.None)
                    arteries[WolfmedArterySite.Head] = artery;
            }
            else if (bodyPart.PartType == BodyPartType.Torso)
            {
                neck = GetStumpOverlay((part, woundable), host.DismembermentWound);
            }
        }

        if (!hasHead && neck != WolfmedArteryOverlay.None)
            arteries[WolfmedArterySite.Neck] = neck;

        var colour = _gore.GetBloodColor(body) ?? FallbackBlood;
        if (Same(visual.Wounds, wounds) && visual.Rot.SetEquals(rot) && visual.WoundColor == colour &&
            Same(visual.Arteries, arteries))
            return;

        visual.Wounds = new Dictionary<HumanoidVisualLayers, WolfmedWoundOverlay>(wounds);
        visual.Rot = new HashSet<HumanoidVisualLayers>(rot);
        visual.WoundColor = colour;
        visual.Arteries = new Dictionary<WolfmedArterySite, WolfmedArteryOverlay>(arteries);
        Dirty(body, visual);
    }

    /// <summary>
    /// The head's artery: the spray while any arterial bleed on the part pumps, the still artery once every one of them
    /// has been clamped, dressed to a stop or has clotted, nothing when the part has no cut artery.
    /// </summary>
    public WolfmedArteryOverlay GetArteryOverlay(Entity<WoundableComponent> part)
    {
        var look = WolfmedArteryOverlay.None;
        foreach (var wound in _wounds.GetWounds(part.AsNullable()))
        {
            if (wound.Comp.State != WoundState.Open || !_traits.TryGetBehavior(wound.Owner, out WolfmedArterialBleedBehavior _))
                continue;

            if (TryComp(wound, out WoundBleedingComponent? bleeding) && bleeding.CurrentRate > 0f)
                return WolfmedArteryOverlay.Bleeding;

            look = WolfmedArteryOverlay.Still;
        }

        return look;
    }

    /// <summary>The neck's artery: the torso's open stump, spraying while it bleeds and still once it has stopped.</summary>
    public WolfmedArteryOverlay GetStumpOverlay(Entity<WoundableComponent> part, ProtoId<WoundPrototype> stump)
    {
        var look = WolfmedArteryOverlay.None;
        foreach (var wound in _wounds.GetWounds(part.AsNullable()))
        {
            if (wound.Comp.State != WoundState.Open || wound.Comp.Prototype != stump)
                continue;

            if (TryComp(wound, out WoundBleedingComponent? bleeding) && bleeding.CurrentRate > 0f)
                return WolfmedArteryOverlay.Bleeding;

            look = WolfmedArteryOverlay.Still;
        }

        return look;
    }

    /// <summary>
    /// A drip while any open wound on the part bleeds, a trickle once the part bleeds at the stream rate, and the still
    /// wound once every bleed on it has clotted or been dressed; an open stump shows even after its bleed is gone.
    /// </summary>
    public WolfmedWoundOverlay GetWoundOverlay(Entity<WoundableComponent> part, ProtoId<WoundPrototype> stump,
        float streamRate)
    {
        var rate = 0f;
        var open = false;
        foreach (var wound in _wounds.GetWounds(part.AsNullable()))
        {
            if (wound.Comp.State != WoundState.Open)
                continue;

            // The bleeding component comes with a bleed that rolled and goes when the wound drops under its bleeding
            // stage; a dressing or clotting leaves it at zero, which is the still wound.
            if (TryComp(wound, out WoundBleedingComponent? bleeding))
            {
                open = true;
                rate += bleeding.CurrentRate;
            }
            else if (wound.Comp.Prototype == stump)
            {
                open = true;
            }
        }

        if (rate > 0f)
            return rate >= streamRate ? WolfmedWoundOverlay.Stream : WolfmedWoundOverlay.Drip;

        return open ? WolfmedWoundOverlay.Old : WolfmedWoundOverlay.None;
    }

    /// <summary>Dead tissue, or an infection on the part at its top stage.</summary>
    public bool IsRotting(Entity<WoundableComponent> part)
    {
        if (CompOrNull<WolfmedNecrosisComponent>(part)?.Necrotic == true)
            return true;

        foreach (var wound in _wounds.GetWounds(part.AsNullable()))
        {
            if (CompOrNull<WolfmedInfectionComponent>(wound)?.Stage == WolfmedInfectionStage.Septic)
                return true;
        }

        return false;
    }

    private static bool Same<TKey, TLook>(Dictionary<TKey, TLook> current, Dictionary<TKey, TLook> next)
        where TKey : notnull
        where TLook : struct, Enum
    {
        if (current.Count != next.Count)
            return false;

        foreach (var (layer, look) in next)
        {
            if (!current.TryGetValue(layer, out var existing) || !EqualityComparer<TLook>.Default.Equals(existing, look))
                return false;
        }

        return true;
    }
}
