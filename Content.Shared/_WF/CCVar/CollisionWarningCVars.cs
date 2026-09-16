using Robust.Shared.Configuration;

namespace Content.Shared._WF.CCVar;

/// <summary>
/// Tuning for the ship collision warning (TCAS).
/// </summary>
[CVarDefs]
public sealed class CollisionWarningCVars
{
    /// <summary>
    /// Whether ships predict collisions and warn their crew at all.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("wf.tcas.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// How far ahead contact is predicted, in seconds. Nothing further out than this warns.
    /// </summary>
    public static readonly CVarDef<float> Lookahead =
        CVarDef.Create("wf.tcas.lookahead", 15f, CVar.SERVERONLY);

    /// <summary>
    /// Seconds to contact at which the warning escalates from advisory to imminent.
    /// </summary>
    public static readonly CVarDef<float> ImminentTime =
        CVarDef.Create("wf.tcas.imminent_time", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Closing speed below which contact is a nudge rather than a crash, and nothing warns. Roughly
    /// the speed the impact damage system starts caring at.
    /// </summary>
    public static readonly CVarDef<float> MinimumClosingSpeed =
        CVarDef.Create("wf.tcas.minimum_closing_speed", 6f, CVar.SERVERONLY);

    /// <summary>
    /// Extra metres added to both hulls when predicting contact, so the warning arrives before the paint
    /// touches.
    /// </summary>
    public static readonly CVarDef<float> Margin =
        CVarDef.Create("wf.tcas.margin", 4f, CVar.SERVERONLY);

    /// <summary>
    /// How long a warning is held after the threat clears, so the banner cannot strobe while a ship yaws.
    /// </summary>
    public static readonly CVarDef<float> Hysteresis =
        CVarDef.Create("wf.tcas.hysteresis", 2f, CVar.SERVERONLY);

    /// <summary>
    /// How often ships are checked, in seconds.
    /// </summary>
    public static readonly CVarDef<float> UpdateInterval =
        CVarDef.Create("wf.tcas.update_interval", 0.25f, CVar.SERVERONLY);
}
