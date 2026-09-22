using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Traders;

/// <summary>
/// Lets a trader refuel a docked ship's generators, priced off a vending machine prototype.
/// </summary>
[RegisterComponent]
public sealed partial class TraderRefuelComponent : Component
{
    /// <summary>
    /// Vending machine entity prototype the fuel items are priced against.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Vendor;

    /// <summary>
    /// Fuels this trader can supply.
    /// </summary>
    [DataField]
    public List<TraderFuelEntry> Fuels = new();

    /// <summary>
    /// Fraction added on top of the fuel cost.
    /// </summary>
    [DataField]
    public float ServiceFee = 0f;

    /// <summary>
    /// Set while the customer is being asked to accept a refuelling quote.
    /// </summary>
    [ViewVariables]
    public bool AwaitingConfirmation;
}

/// <summary>
/// Maps one material or reagent to the vendor item that supplies it.
/// </summary>
[DataDefinition]
public sealed partial class TraderFuelEntry
{
    /// <summary>
    /// Material id filled into solid fuel adapters.
    /// </summary>
    [DataField]
    public string? Material;

    /// <summary>
    /// Reagent id filled into chemical fuel adapters.
    /// </summary>
    [DataField]
    public string? Reagent;

    /// <summary>
    /// Vendor item this fuel is bought as, used to derive the unit price.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;
}
