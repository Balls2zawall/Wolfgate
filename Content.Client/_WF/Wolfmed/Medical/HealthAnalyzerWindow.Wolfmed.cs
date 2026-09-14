// HOOK 26 body. Kept out of the upstream window so that file carries only the five marked lines PLAN4 authorises.
// The partial lives on the window class because the panel and the damage section are private generated fields a
// standalone control could not reach.

using Content.Shared.MedicalScanner;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerWindow
{
    private bool _wolfmedBound;

    /// <summary>Feeds the Wolfmed diagnostic panel, or hides it outright for anything that is not a wound host (D2).</summary>
    private void PopulateWolfmed(HealthAnalyzerScannedUserMessage msg)
    {
        if (!_wolfmedBound)
        {
            // The panel owns the damage section's visibility so its Damage tab can hand the whole section back.
            WolfmedPanel.DamageSection = WolfmedDamageGroupsPanel;
            _wolfmedBound = true;
        }

        if (msg.WoundDiagnostics == null && msg.Organs == null)
        {
            HideWolfmed();
            return;
        }

        WolfmedPanel.Visible = true;
        WolfmedPanel.Populate(msg);
    }

    /// <summary>Hides the panel and returns the damage section, so no early return leaves stale findings on screen.</summary>
    private void HideWolfmed()
    {
        WolfmedPanel.Clear();
        WolfmedPanel.Visible = false;
        WolfmedPanel.ReleaseDamageSection();
    }
}
