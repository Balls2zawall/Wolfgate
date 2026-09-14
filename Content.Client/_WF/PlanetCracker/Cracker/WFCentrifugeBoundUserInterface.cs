using Content.Shared.Power;
using Robust.Client.UserInterface;

namespace Content.Client._WF.PlanetCracker.Cracker;

/// <summary>
/// Stands in for PowerChargeBoundUserInterface on the gravitic centrifuge: the same UI key and the same
/// <see cref="SwitchChargingMachineMessage"/>, so no server change and no new key, but it hosts the rotor dial window
/// instead of the stock charge window.
/// </summary>
public sealed class WFCentrifugeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private WFCentrifugeWindow? _window;

    public WFCentrifugeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    /// <summary>Flips the machine's power switch, exactly as the stock window does.</summary>
    public void SetPowerSwitch(bool on)
    {
        SendMessage(new SwitchChargingMachineMessage(on));
    }

    /// <inheritdoc/>
    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out Content.Client.Power.PowerCharge.PowerChargeComponent? charge))
            return;

        _window = this.CreateWindow<WFCentrifugeWindow>();
        _window.SetOwner(this, Loc.GetString(charge.WindowTitle));
    }

    /// <inheritdoc/>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PowerChargeState chargeState)
            _window?.UpdateState(chargeState);
    }
}
