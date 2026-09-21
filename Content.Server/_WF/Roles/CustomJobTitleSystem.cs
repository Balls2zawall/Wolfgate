using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.StationRecords.Systems;
using Content.Shared._WF.Roles;
using Content.Shared.Clothing;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Roles;

/// <summary>Puts a player's custom job title on their ID card and station records when they spawn.</summary>
public sealed class CustomJobTitleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private StationRecordsSystem _records = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (GetTitle(ev.Profile, ev.JobId) is not { } title
            || !_idCard.TryFindIdCard(ev.Mob, out var card))
            return;

        _idCard.TryChangeJobTitle(card, title, card.Comp);
        _adminLogger.Add(LogType.Identity, LogImpact.Low,
            $"{ToPrettyString(ev.Mob):player} spawned as {ev.JobId} with custom job title {title}");
    }

    /// <summary>Covers both the station record and the sector-wide one.</summary>
    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        if (GetTitle(ev.Profile, ev.Record.JobPrototype) is not { } title)
            return;

        ev.Record.JobTitle = title;
        _records.Synchronize(ev.Key);
    }

    /// <summary>The profile's valid custom title for a job, or null to keep the job's own name.</summary>
    private string? GetTitle(HumanoidCharacterProfile profile, string? jobId)
    {
        if (string.IsNullOrEmpty(jobId)
            || !profile.Loadouts.TryGetValue(LoadoutSystem.GetJobPrototype(jobId), out var loadout))
            return null;

        return CustomJobTitleRules.Sanitize(loadout.CustomJobTitle, loadout.Role, _prototype);
    }
}
