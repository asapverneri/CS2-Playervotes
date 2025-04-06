using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Playervotes
{
    public class PlayervotesConfig : BasePluginConfig
    {
        [JsonPropertyName("Menutype")]
        public string Menutype { get; set; } = "ScreenMenu";

        [JsonPropertyName("RequiredVotePercentage")]
        public float RequiredVotePercentage { get; set; } = 80.0f;

        [JsonPropertyName("ClearVotesOnMapStart")]
        public bool ClearVotesOnMapStart { get; set; } = true;

        [JsonPropertyName("FlagForCommands")]
        public string FlagForCommands { get; set; } = "";

        public string Line { get; set; } = "----------------------------------";

        [JsonPropertyName("Votemute")]
        public bool EnableVotemute { get; set; } = true;

        [JsonPropertyName("MuteDuration")]
        public int MuteDuration { get; set; } = 60;

        [JsonPropertyName("MuteReason")]
        public string MuteReason { get; set; } = "Votemuted";

        public string Line2 { get; set; } = "----------------------------------";

        [JsonPropertyName("Voteban")]
        public bool EnableVoteban { get; set; } = true;

        [JsonPropertyName("BanDuration")]
        public int BanDuration { get; set; } = 120;

        [JsonPropertyName("BanReason")]
        public string BanReason { get; set; } = "Votebanned";

        public string Line3 { get; set; } = "----------------------------------";

        [JsonPropertyName("Votekick")]
        public bool EnableVotekick { get; set; } = true;

        [JsonPropertyName("KickReason")]
        public string KickReason { get; set; } = "Votekicked";

        public string Line4 { get; set; } = "----------------------------------";

        [JsonPropertyName("Votegag")]
        public bool EnableVotegag { get; set; } = true;

        [JsonPropertyName("GagDuration")]
        public int GagDuration { get; set; } = 60;

        [JsonPropertyName("GagReason")]
        public string GagReason { get; set; } = "Votegagged";

        public string Line5 { get; set; } = "----------------------------------";

        [JsonPropertyName("Votesilence")]
        public bool EnableVotesilence { get; set; } = true;

        [JsonPropertyName("SilenceDuration")]
        public int SilenceDuration { get; set; } = 60;

        [JsonPropertyName("SilenceReason")]
        public string SilenceReason { get; set; } = "Votesilenced";

        public string Line6 { get; set; } = "----------------------------------";

        [JsonPropertyName("VoteImmunity")]
        public string VoteImmunity { get; set; } = "@css/vip, @css/vvip";
    }
}
