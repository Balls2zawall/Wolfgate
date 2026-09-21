using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Content.Shared.Clothing;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Roles;

/// <summary>Cleans and checks player-written job titles. Shared so the editor and the server agree.</summary>
public static class CustomJobTitleRules
{
    private const string AllowedPunctuation = " -'.,&/()";

    /// <summary>Trims and collapses runs of whitespace.</summary>
    public static string Clean(string title)
    {
        return string.Join(' ', title.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Lower-case letters and digits only, so spacing and punctuation can't dodge a match.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>Checks a cleaned title against the role's rules. Empty is valid and means the job's own name.</summary>
    public static bool IsValid(string title, CustomJobTitlePrototype rules, IPrototypeManager protoManager, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (title.Length == 0)
            return true;

        if (title.Length > rules.MaxLength)
        {
            reason = Loc.GetString("custom-job-title-too-long", ("max", rules.MaxLength));
            return false;
        }

        // ASCII only, so look-alike letters can't spell out a real job.
        foreach (var c in title)
        {
            if (char.IsAsciiLetterOrDigit(c) || AllowedPunctuation.Contains(c))
                continue;

            reason = Loc.GetString("custom-job-title-bad-character", ("character", c.ToString()));
            return false;
        }

        var normalized = Normalize(title);
        if (!normalized.Any(char.IsAsciiLetter))
        {
            reason = Loc.GetString("custom-job-title-no-letters");
            return false;
        }

        foreach (var job in protoManager.EnumeratePrototypes<JobPrototype>())
        {
            if (Normalize(job.LocalizedName) != normalized)
                continue;

            reason = Loc.GetString("custom-job-title-matches-job", ("job", job.LocalizedName));
            return false;
        }

        foreach (var blocked in rules.BlockedTitles)
        {
            if (Normalize(blocked) != normalized)
                continue;

            reason = Loc.GetString("custom-job-title-blocked");
            return false;
        }

        var words = title.Split(AllowedPunctuation.ToCharArray()).Select(Normalize).ToHashSet();
        foreach (var word in rules.BlockedWords)
        {
            if (!words.Contains(Normalize(word)))
                continue;

            reason = Loc.GetString("custom-job-title-blocked-word", ("word", word));
            return false;
        }

        return true;
    }

    /// <summary>The profile's valid custom title for a job, or null to keep the job's own name.</summary>
    public static string? GetTitle(HumanoidCharacterProfile profile, string? jobId, IPrototypeManager protoManager)
    {
        if (string.IsNullOrEmpty(jobId)
            || !profile.Loadouts.TryGetValue(LoadoutSystem.GetJobPrototype(jobId), out var loadout))
            return null;

        return Sanitize(loadout.CustomJobTitle, loadout.Role, protoManager);
    }

    /// <summary>Job name for the join menu, e.g. "Vagrant (Bounty Hunter)".</summary>
    public static string JoinMenuName(JobPrototype job, HumanoidCharacterProfile profile, IPrototypeManager protoManager)
    {
        return GetTitle(profile, job.ID, protoManager) is { } title
            ? Loc.GetString("custom-job-title-join-name", ("job", job.LocalizedName), ("title", title))
            : job.LocalizedName;
    }

    /// <summary>Returns the cleaned title if the role allows one and it passes the rules, otherwise null.</summary>
    public static string? Sanitize(string? title, ProtoId<RoleLoadoutPrototype> role, IPrototypeManager protoManager)
    {
        if (title == null || !protoManager.TryIndex<CustomJobTitlePrototype>(role.Id, out var rules))
            return null;

        var cleaned = Clean(title);
        if (cleaned.Length == 0 || !IsValid(cleaned, rules, protoManager, out _))
            return null;

        return cleaned;
    }
}
