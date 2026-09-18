using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._WF.PlanetCracker.Flight;

/// <summary>Unscaled engine ratings and the atmospheric power-recovery debounce.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class WFAtmosphereThrusterComponent : Component
{
    [DataField] public float RatedThrust;
    [DataField] public float RatedLoad;
    [DataField] public bool Atmospheric;
    [DataField] public bool WasPowered;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan RecoverAt;
}
