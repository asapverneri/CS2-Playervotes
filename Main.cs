using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;

namespace Playervotes;

public class Playervotes : BasePlugin, IPluginConfig<PlayervotesConfig>
{
    public override string ModuleName => "Playervotes";
    public override string ModuleDescription => "Lightweight playervotes for cs2";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.2";

    public PlayervotesConfig Config { get; set; } = new();

    private readonly Dictionary<ulong, HashSet<ulong>> voteKickCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteBanCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteMuteCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteGagCounts = new();

    public void OnConfigParsed(PlayervotesConfig config)
	{
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"Loaded (version {ModuleVersion})");

        AddCommand($"css_votekick", "Start votekick", OnVotekick);
        AddCommand($"css_voteban", "Start voteban", OnVoteban);
        AddCommand($"css_votemute", "Start votemute", OnVotemute);
        AddCommand($"css_votegag", "Start votegag", OnVotegag);

    }

    public void OnVotekick(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        CenterHtmlMenu menu = new CenterHtmlMenu("Votekick Menu", this);

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) => {
                StartVote(client, target, "kick");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.ExitButton = true;
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    public void OnVoteban(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        CenterHtmlMenu menu = new CenterHtmlMenu("Voteban Menu", this);

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) => {
                StartVote(client, target, "ban");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.ExitButton = true;
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    public void OnVotemute(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        CenterHtmlMenu menu = new CenterHtmlMenu("Votemute Menu", this);

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) => {
                StartVote(client, target, "mute");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.ExitButton = true;
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    public void OnVotegag(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        CenterHtmlMenu menu = new CenterHtmlMenu("Votegag Menu", this);

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) => {
                StartVote(client, target, "gag");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.ExitButton = true;
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void StartVote(CCSPlayerController voter, CCSPlayerController target, string action)
    {
        if (target.SteamID == voter.SteamID)
        {
            voter.PrintToChat($"{Localizer["voteurself"]}");
            return;
        }

        string[] flags = Config.VoteImmunity.Split(',').Select(flag => flag.Trim()).ToArray();
        foreach (var flag in flags)
        {
            if (AdminManager.PlayerHasPermissions(target, flag))
            {
                voter.PrintToChat($"{Localizer["cantvote"]}");
                return;
            }
        }
        var voteDictionary = action switch
        {
            "ban" => voteBanCounts,
            "mute" => voteMuteCounts,
            "kick" => voteKickCounts,
            "gag" => voteGagCounts,
            _ => throw new ArgumentException("Invalid action")
        };

        if (!voteDictionary.ContainsKey(target.SteamID))
        {
            voteDictionary[target.SteamID] = new HashSet<ulong>();
        }

        if (voteDictionary[target.SteamID].Contains(voter.SteamID))
        {
            voter.PrintToChat($"{Localizer["alreadyvoted", action]}");
            return;
        }

        voteDictionary[target.SteamID].Add(voter.SteamID);

        int currentVotes = voteDictionary[target.SteamID].Count;
        int requiredVotes = (int)System.Math.Ceiling((Config.RequiredVotePercentage / 100) * GetActivePlayers().Count);
        Server.PrintToChatAll($"{Localizer["voteprogress", voter.PlayerName, action, target.PlayerName, voteDictionary[target.SteamID], requiredVotes]}");

        if (currentVotes >= requiredVotes)
        {
            if (action == "ban")
            {
                Server.PrintToChatAll($"{Localizer["votebanned", target.PlayerName, Config.BanDuration]}");
                Server.ExecuteCommand($"css_ban #{target.UserId} {Config.BanDuration} \"{Config.BanReason}\"");
            }
            else if (action == "mute")
            {
                Server.PrintToChatAll($"{Localizer["votemuted", target.PlayerName, Config.MuteDuration]}");
                Server.ExecuteCommand($"css_mute #{target.UserId} {Config.MuteDuration} \"{Config.MuteReason}\"");
            }
            else if (action == "kick")
            {
                Server.PrintToChatAll($"{Localizer["votekicked", target.PlayerName]}");
                Server.ExecuteCommand($"css_kick #{target.UserId} \"{Config.KickReason}\"");
            }
            else if (action == "gag")
            {
                Server.PrintToChatAll($"{Localizer["votegagged", target.PlayerName]}");
                Server.ExecuteCommand($"css_gag #{target.UserId} {Config.GagDuration} \"{Config.GagReason}\"");
            }
            voteDictionary.Remove(target.SteamID);
        }
    }
    private static List<CCSPlayerController> GetActivePlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && !p.IsBot && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();
    }
}