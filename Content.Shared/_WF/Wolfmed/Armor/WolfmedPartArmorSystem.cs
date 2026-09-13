using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Wounds;

namespace Content.Shared._WF.Wolfmed.Armor;

/// <summary>Armours a wound host's localized damage on the equipped item, once routing resolved the struck part.</summary>
/// <remarks>
/// HOOK 10's other half. SharedArmorSystem armours only the systemic types for a wound host and returns early,
/// so the localized types (Blunt, Slash, Piercing, Heat, Cold, Shock, Caustic) would otherwise reach the part
/// unarmoured. WoundDamageRoutingSystem relays PartDamageModifyEvent to the wearer's inventory; this system is
/// the only subscriber of that relayed pair. Lives in _WF rather than inside SharedArmorSystem so the upstream
/// file keeps exactly the edit PLAN §3 HOOK 10 authorises.
/// Onyx picks a per-part modifier set here; Wolfgate's ArmorComponent has no PartModifiers, so the armour's
/// global modifiers protect every part — which is also Onyx's own fallback when no profile covers the part.
/// </remarks>
public sealed class WolfmedPartArmorSystem : EntitySystem
{
    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<PartDamageModifyEvent>>(OnPartDamageModify);
    }

    /// <summary>Applies the armour's penetration-adjusted modifiers to the routed part's damage.</summary>
    private void OnPartDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<PartDamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(component.Modifiers, args.Args.ArmorPenetration));
    }
}
