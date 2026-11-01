using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Playervotes;

/// <summary>
/// Moves a version 1 config (every setting at the top level, separated by "Line" keys) into the
/// sectioned layout of version 2. The owner's values are carried over; the old file is kept next to
/// it as <c>.v1.bak</c>, because a restructure this size can't be done as an in-place text edit.
/// </summary>
internal static class ConfigMigration
{
    /// <summary>Returns the migrated config when the file was version 1, otherwise null.</summary>
    public static PlayervotesConfig? TryMigrate(string path, ILogger logger)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var text = File.ReadAllText(path);
            var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) as JsonObject;

            if (root == null || root.ContainsKey("General") || !(root.ContainsKey("RequiredVotePercentage") || root.ContainsKey("Votekick")))
                return null;

            var config = new PlayervotesConfig();
            var general = config.General;
            general.RequiredPlayers = Get(root, "RequiredPlayers", general.RequiredPlayers);
            general.DefaultPercentage = Get(root, "RequiredVotePercentage", general.DefaultPercentage);
            general.VoteDurationSeconds = Get(root, "VoteDurationSeconds", general.VoteDurationSeconds);
            general.TargetProtectionMinutes = Get(root, "TargetProtectionMinutes", general.TargetProtectionMinutes);
            general.VoteImmunity = Get(root, "VoteImmunity", general.VoteImmunity);
            general.HideFromMenu = Get(root, "HideFromMenu", general.HideFromMenu);

            Fill(root, config.Kick, "Votekick", "PermissionForVoteKick", "KickPercentage", "KickReason");
            Fill(root, config.Ban, "Voteban", "PermissionForVoteBan", "BanPercentage", "BanReason");
            Fill(root, config.Mute, "Votemute", "PermissionForVoteMute", "MutePercentage", "MuteReason");
            Fill(root, config.Gag, "Votegag", "PermissionForVoteGag", "GagPercentage", "GagReason");
            Fill(root, config.Silence, "Votesilence", "PermissionForVoteSilence", "SilencePercentage", "SilenceReason");
            config.Ban.Duration = Get(root, "BanDuration", config.Ban.Duration);
            config.Mute.Duration = Get(root, "MuteDuration", config.Mute.Duration);
            config.Gag.Duration = Get(root, "GagDuration", config.Gag.Duration);
            config.Silence.Duration = Get(root, "SilenceDuration", config.Silence.Duration);
            config.Ban.OfflineCommand = Get(root, "OfflineBanCommand", config.Ban.OfflineCommand);

            config.EnableDebugLogs = Get(root, "EnableDebugLogs", config.EnableDebugLogs);
            config.EnablePerfMonitor = Get(root, "EnablePerfMonitor", config.EnablePerfMonitor);
            config.PerfSlowSectionMs = Get(root, "PerfSlowSectionMs", config.PerfSlowSectionMs);

            var backup = path + ".v1.bak";
            File.Copy(path, backup, overwrite: true);

            var temp = path + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions(ConfigUpdater.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            File.Move(temp, path, overwrite: true);

            logger.LogInformation("Config moved to the sectioned layout (version 2). The old file is saved as {Backup}", Path.GetFileName(backup));
            return config;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Config migration to version 2 failed; using the config as read.");
            return null;
        }
    }

    private static void Fill(JsonObject root, VoteConfig vote, string enabled, string permission, string percentage, string reason)
    {
        vote.Enabled = Get(root, enabled, vote.Enabled);
        vote.Permission = Get(root, permission, vote.Permission);
        vote.Percentage = Get(root, percentage, vote.Percentage);
        vote.Reason = Get(root, reason, vote.Reason);
    }

    private static T Get<T>(JsonObject root, string key, T fallback)
    {
        if (root[key] is not JsonValue value)
            return fallback;

        try { return value.Deserialize<T>() ?? fallback; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new JsonException($"Config key \"{key}\" has a value of the wrong type.", ex);
        }
    }
}
