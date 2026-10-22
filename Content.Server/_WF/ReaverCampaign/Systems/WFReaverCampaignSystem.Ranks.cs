using Content.Server._WF.Encounters;
using Content.Server._WF.Encounters.Components;
using Content.Server._WF.ReaverCampaign.Components;
using Content.Shared._WF.Encounters;

namespace Content.Server._WF.ReaverCampaign.Systems;

public sealed partial class WFReaverCampaignSystem
{
    /// <summary>Pirates and Reavers are one threat: an encounter that flies by tier takes the campaign's, while it runs.</summary>
    private void OnThreatTier(ref WFEncounterThreatTierEvent ev)
    {
        if (ev.Tier == null && Campaign != null && State() is { Finished: false } state)
            ev.Tier = state.Tier;
    }

    /// <summary>A stronghold's rank on the map: the tier it was founded at plus its level, and three stars for the citadel.</summary>
    private static void SetThreat(WFReaverStrongholdComponent stronghold, WFEncounterComponent encounter)
    {
        encounter.Threat = stronghold.Citadel
            ? WFEncounterThreat.Max
            : Math.Min(stronghold.Tier + stronghold.Level, WFEncounterThreat.MaxTier);
    }
}
