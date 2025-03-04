using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Playervotes
{
    public class PlayervotesConfig : BasePluginConfig
    {
        [JsonPropertyName("RequiredVotePercentage")]
        public float RequiredVotePercentage { get; set; } = 80.0f;

        public string Line { get; set; } = "----------------------------------";

        [JsonPropertyName("MuteDuration")]
        public int MuteDuration { get; set; } = 60;

        [JsonPropertyName("MuteReason")]
        public string MuteReason { get; set; } = "Votemuted";

        public string Line2 { get; set; } = "----------------------------------";

        [JsonPropertyName("BanDuration")]
        public int BanDuration { get; set; } = 120;

        [JsonPropertyName("BanReason")]
        public string BanReason { get; set; } = "Votebanned";

        public string Line3 { get; set; } = "----------------------------------";

        [JsonPropertyName("KickReason")]
        public string KickReason { get; set; } = "Votekicked";

        public string Line4 { get; set; } = "----------------------------------";

        [JsonPropertyName("GagDuration")]
        public int GagDuration { get; set; } = 60;

        [JsonPropertyName("GagReason")]
        public string GagReason { get; set; } = "Votegagged";

        public string Line5 { get; set; } = "----------------------------------";

        [JsonPropertyName("VoteImmunity")]
        public string VoteImmunity { get; set; } = "@css/vip, @css/vvip";
    }
}
