using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using CS2MenuManager.API;
using CS2MenuManager.API.Menu;
using CS2MenuManager.API.Class;

namespace Playervotes;

public class Playervotes : BasePlugin, IPluginConfig<PlayervotesConfig>
{
    public override string ModuleName => "Playervotes";
    public override string ModuleDescription => "Lightweight playervotes for cs2";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "2.2";

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

        RegisterListener<Listeners.OnMapStart>(OnMapStart);

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
    private void OnMapStart(string map)
    {
        if (Config.ClearVotesOnMapStart)
        {
            voteKickCounts.Clear();
            voteBanCounts.Clear();
            voteMuteCounts.Clear();
            voteGagCounts.Clear();
            voteSilenceCounts.Clear();
        }
    }
    public void OnVotekick(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.RequiredPlayers]}");
            return;
        }

        if (!string.IsNullOrEmpty(Config.FlagForCommands))
        {
            if (Config.FlagForCommands.StartsWith("#"))
            {
                if (!AdminManager.PlayerInGroup(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
            else if (Config.FlagForCommands.StartsWith("@"))
            {
                if (!AdminManager.PlayerHasPermissions(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
        }

        var menu = CreateMenu($"{Localizer["votekick.title"]}");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddItem(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "kick");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.Display(player, 10);
    }

    public void OnVoteban(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.RequiredPlayers]}");
            return;
        }

        if (!string.IsNullOrEmpty(Config.FlagForCommands))
        {
            if (Config.FlagForCommands.StartsWith("#"))
            {
                if (!AdminManager.PlayerInGroup(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
            else if (Config.FlagForCommands.StartsWith("@"))
            {
                if (!AdminManager.PlayerHasPermissions(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
        }

        var menu = CreateMenu($"{Localizer["voteban.title"]}");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddItem(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "ban");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.Display(player, 10);
    }

    public void OnVotemute(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.RequiredPlayers]}");
            return;
        }

        if (!string.IsNullOrEmpty(Config.FlagForCommands))
        {
            if (Config.FlagForCommands.StartsWith("#"))
            {
                if (!AdminManager.PlayerInGroup(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
            else if (Config.FlagForCommands.StartsWith("@"))
            {
                if (!AdminManager.PlayerHasPermissions(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
        }

        var menu = CreateMenu($"{Localizer["votemute.title"]}");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddItem(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "mute");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.Display(player, 10);
    }

    public void OnVotegag(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.RequiredPlayers]}");
            return;
        }

        if (!string.IsNullOrEmpty(Config.FlagForCommands))
        {
            if (Config.FlagForCommands.StartsWith("#"))
            {
                if (!AdminManager.PlayerInGroup(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
            else if (Config.FlagForCommands.StartsWith("@"))
            {
                if (!AdminManager.PlayerHasPermissions(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
        }

        var menu = CreateMenu($"{Localizer["votegag.title"]}");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddItem(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "gag");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.Display(player, 10);
    }
    public void OnVotesilence(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.RequiredPlayers]}");
            return;
        }

        if (!string.IsNullOrEmpty(Config.FlagForCommands))
        {
            if (Config.FlagForCommands.StartsWith("#"))
            {
                if (!AdminManager.PlayerInGroup(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
            else if (Config.FlagForCommands.StartsWith("@"))
            {
                if (!AdminManager.PlayerHasPermissions(player, Config.FlagForCommands))
                {
                    player.PrintToChat($"{Localizer["noaccess"]}");
                    return;
                }
            }
        }

        var menu = CreateMenu($"{Localizer["votesilence.title"]}");
        if (menu == null)
            return;

        foreach (var target in GetActivePlayers())
        {
            menu.AddItem(target.PlayerName, (client, option) =>
            {
                StartVote(client, target, "silence");
                MenuManager.CloseActiveMenu(player);
            });
        }

        menu.Display(player, 10);
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
            if (flag.StartsWith("#"))
            {
                if (AdminManager.PlayerInGroup(target, flag))
                {
                    voter.PrintToChat($"{Localizer["cantvote"]}");
                    return;
                }
            }
            else if (flag.StartsWith("@"))
            {
                if (AdminManager.PlayerHasPermissions(target, flag))
                {
                    voter.PrintToChat($"{Localizer["cantvote"]}");
                    return;
                }
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
    private bool EnoughPlayers()
    {
        int activePlayersCount = GetActivePlayers().Count;
        return activePlayersCount >= Config.RequiredPlayers;
    }

    private BaseMenu? CreateMenu(string menuName)
    {
        return Config.Menutype switch
        {
            "ChatMenu" => new ChatMenu(menuName, this),
            "ConsoleMenu" => new ConsoleMenu(menuName, this),
            "CenterHtml" => new CenterHtmlMenu(menuName, this),
            "WasdMenu" => new WasdMenu(menuName, this),
            "ScreenMenu" => new ScreenMenu(menuName, this),
            _ => null
        };
    }
}