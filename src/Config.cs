using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Playervotes
{
    public class PlayervotesConfig : BasePluginConfig
    {
        // 2: settings grouped into sections (ConfigMigration moves a version 1 file over)
        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = 2;

        [JsonPropertyName("General")]
        public GeneralConfig General { get; set; } = new();

        [JsonPropertyName("Kick")]
        public VoteConfig Kick { get; set; } = new() { Reason = "Votekicked" };

        [JsonPropertyName("Ban")]
        public BanVoteConfig Ban { get; set; } = new() { Duration = 120, Reason = "Votebanned" };

        [JsonPropertyName("Mute")]
        public TimedVoteConfig Mute { get; set; } = new() { Reason = "Votemuted" };

        [JsonPropertyName("Gag")]
        public TimedVoteConfig Gag { get; set; } = new() { Reason = "Votegagged" };

        [JsonPropertyName("Silence")]
        public TimedVoteConfig Silence { get; set; } = new() { Reason = "Votesilenced" };

        [JsonPropertyName("EnableDebugLogs")]
        public bool EnableDebugLogs { get; set; } = false;

        [JsonPropertyName("EnablePerfMonitor")]
        public bool EnablePerfMonitor { get; set; } = false;

        [JsonPropertyName("PerfSlowSectionMs")]
        public double PerfSlowSectionMs { get; set; } = 2.0;
    }

    public class GeneralConfig
    {
        // Min players in the game to allow playervotes
        [JsonPropertyName("RequiredPlayers")]
        public int RequiredPlayers { get; set; } = 2;

        // Used by every vote type whose own Percentage is 0
        [JsonPropertyName("DefaultPercentage")]
        public float DefaultPercentage { get; set; } = 80.0f;

        [JsonPropertyName("VoteDurationSeconds")]
        public int VoteDurationSeconds { get; set; } = 30;

        // After a vote against a player ends, the same vote type can't target them again for this long
        [JsonPropertyName("TargetProtectionMinutes")]
        public int TargetProtectionMinutes { get; set; } = 5;

        // Players may start a vote against themselves and vote in a vote about themselves. false = the target counts as a fixed NO
        [JsonPropertyName("AllowSelfVote")]
        public bool AllowSelfVote { get; set; } = false;

        // "@css/flag" or "#group", comma separated
        [JsonPropertyName("VoteImmunity")]
        public string VoteImmunity { get; set; } = "@css/vip, @css/vvip";

        [JsonPropertyName("HideFromMenu")]
        public string HideFromMenu { get; set; } = "@css/root, @css/generic";
    }

    public class VoteConfig
    {
        // Commands are registered on load, so changing this needs a plugin reload
        [JsonPropertyName("Enabled"), JsonPropertyOrder(0)]
        public bool Enabled { get; set; } = true;

        // "@css/vip" or "#group"; empty = everyone
        [JsonPropertyName("Permission"), JsonPropertyOrder(1)]
        public string Permission { get; set; } = "";

        // 0 = General.DefaultPercentage
        [JsonPropertyName("Percentage"), JsonPropertyOrder(2)]
        public float Percentage { get; set; } = 0;

        [JsonPropertyName("Reason"), JsonPropertyOrder(4)]
        public string Reason { get; set; } = "";
    }

    public class TimedVoteConfig : VoteConfig
    {
        // Minutes
        [JsonPropertyName("Duration"), JsonPropertyOrder(3)]
        public int Duration { get; set; } = 60;
    }

    public class BanVoteConfig : TimedVoteConfig
    {
        // Used when the target left during a voteban that then passed. {steamid64}, {duration}, {reason}. Empty = cancel the vote when the target leaves
        [JsonPropertyName("OfflineCommand"), JsonPropertyOrder(5)]
        public string OfflineCommand { get; set; } = "css_addban {steamid64} {duration} \"{reason}\"";
    }
}
