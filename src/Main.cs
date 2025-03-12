using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using MenuManager;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;

namespace Playervotes;

public class Playervotes : BasePlugin, IPluginConfig<PlayervotesConfig>
{
    public override string ModuleName => "Playervotes";
    public override string ModuleDescription => "Lightweight playervotes for cs2";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "1.5";

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");

    public PlayervotesConfig Config { get; set; } = new();

    private readonly Dictionary<ulong, HashSet<ulong>> voteKickCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteBanCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteMuteCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteGagCounts = new();
    private readonly Dictionary<ulong, HashSet<ulong>> voteSilenceCounts = new();

    public void OnConfigParsed(PlayervotesConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation($"Loaded (version {ModuleVersion})");

        if (Config.EnableVotekick && !string.IsNullOrWhiteSpace(Config.Menutype))
        AddCommand($"css_votekick", "Start votekick", OnVotekick);

        if (Config.EnableVoteban && !string.IsNullOrWhiteSpace(Config.Menutype))
        AddCommand($"css_voteban", "Start voteban", OnVoteban);

        if (Config.EnableVotemute && !string.IsNullOrWhiteSpace(Config.Menutype))
        AddCommand($"css_votemute", "Start votemute", OnVotemute);

        if (Config.EnableVotegag && !string.IsNullOrWhiteSpace(Config.Menutype))
        AddCommand($"css_votegag", "Start votegag", OnVotegag);

        if (Config.EnableVotesilence && !string.IsNullOrWhiteSpace(Config.Menutype))
        AddCommand($"css_votesilence", "Start votesilence", OnVotesilence);

        if (string.IsNullOrWhiteSpace(Config.Menutype))
        {
            Logger.LogError("Commands disabled, menutype configuration is incorrect. Check the configuration file.");
        }

    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager not found.");
    }

    public void OnVotekick(CCSPlayerController? player, CommandInfo command)
    {
        if (_api == null || player == null || !player.IsValid)
            return;

        var menu = GetVoteMenu("Votekick");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "kick");
                _api.CloseMenu(player);
            });
        }

        menu.Open(player);
    }

    public void OnVoteban(CCSPlayerController? player, CommandInfo command)
    {
        if (_api == null || player == null || !player.IsValid)
            return;

        var menu = GetVoteMenu("Voteban");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "ban");
                _api.CloseMenu(player);
            });
        }

        menu.Open(player);
    }

    public void OnVotemute(CCSPlayerController? player, CommandInfo command)
    {
        if (_api == null || player == null || !player.IsValid)
            return;

        var menu = GetVoteMenu("Votemute");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "mute");
                _api.CloseMenu(player);
            });
        }

        menu.Open(player);
    }

    public void OnVotegag(CCSPlayerController? player, CommandInfo command)
    {
        if (_api == null || player == null || !player.IsValid)
            return;

        var menu = GetVoteMenu("Votegag");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "gag");
                _api.CloseMenu(player);
            });
        }

        menu.Open(player);
    }
    public void OnVotesilence(CCSPlayerController? player, CommandInfo command)
    {
        if (_api == null || player == null || !player.IsValid)
            return;

        var menu = GetVoteMenu("Votesilence");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddMenuOption(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "silence");
                _api.CloseMenu(player);
            });
        }

        menu.Open(player);
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
            "silence" => voteSilenceCounts,
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
        Server.PrintToChatAll($"{Localizer["voteprogress", voter.PlayerName, action, target.PlayerName, currentVotes, requiredVotes]}");

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
            else if (action == "silence")
            {
                Server.PrintToChatAll($"{Localizer["votesilenced", target.PlayerName]}");
                Server.ExecuteCommand($"css_silence #{target.UserId} {Config.SilenceDuration} \"{Config.SilenceReason}\"");
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
    private IMenu? GetVoteMenu(string menuName)
    {
        if (_api == null)
            return null;

        return Config.Menutype switch
        {
            "chat" => _api.GetMenuForcetype(menuName, MenuType.ChatMenu),
            "center" => _api.GetMenuForcetype(menuName, MenuType.CenterMenu),
            "wasd" => _api.GetMenuForcetype(menuName, MenuType.ButtonMenu),
            "all" => _api.GetMenu(menuName),
            _ => null
        };
    }
}