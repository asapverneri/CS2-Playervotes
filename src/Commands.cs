using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;

namespace Playervotes;

public partial class Playervotes
{
    private readonly record struct VoteSettings(bool Enabled, string Permission, float Percentage, string TitleKey, string Command);

    private VoteConfig VoteConfigFor(VoteType type) => type switch
    {
        VoteType.Kick => Config.Kick,
        VoteType.Ban => Config.Ban,
        VoteType.Mute => Config.Mute,
        VoteType.Gag => Config.Gag,
        VoteType.Silence => Config.Silence,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private VoteSettings Settings(VoteType type)
    {
        var vote = VoteConfigFor(type);
        var name = "vote" + type.ToString().ToLowerInvariant();
        float percentage = vote.Percentage > 0 ? vote.Percentage : Config.General.DefaultPercentage;
        return new VoteSettings(vote.Enabled, vote.Permission, percentage, name + ".title", "css_" + name);
    }

    // Vote commands are only registered when enabled, so toggling one needs a plugin reload
    private void RegisterCommands()
    {
        foreach (var type in Enum.GetValues<VoteType>())
        {
            var settings = Settings(type);
            if (settings.Enabled)
                AddCommand(settings.Command, $"Start {settings.Command[4..]}", (player, _) => OnVoteCommand(player, type));
        }

        AddCommand("css_yes", "Vote yes in the current vote", (player, _) => OnChoiceCommand(player, true));
        AddCommand("css_no", "Vote no in the current vote", (player, _) => OnChoiceCommand(player, false));
        AddCommand("css_cancelvote", "Cancel the current vote", OnCancelVoteCommand);
    }

    private void OnVoteCommand(CCSPlayerController? player, VoteType type)
    {
        using var perf = PerfScope.Measure("vote command");
        if (player == null || !player.IsValid)
            return;

        if (_vote != null)
        {
            player.PrintToChat($"{Localizer["voteinprogress"]}");
            return;
        }

        if (!EnoughPlayers())
        {
            player.PrintToChat($"{Localizer["noenoughplayers", Config.General.RequiredPlayers]}");
            return;
        }

        var settings = Settings(type);
        if (!HasAccess(player, settings.Permission))
        {
            player.PrintToChat($"{Localizer["noaccess"]}");
            return;
        }

        OpenPlayerMenu(player, $"{Localizer[settings.TitleKey]}", type);
    }

    private void OnChoiceCommand(CCSPlayerController? player, bool yes)
    {
        using var perf = PerfScope.Measure("vote choice command");
        if (player == null || !player.IsValid)
            return;

        if (_vote is not { IsOpen: true })
        {
            player.PrintToChat($"{Localizer["novote"]}");
            return;
        }

        CastVote(player, yes);
    }

    private void OnCancelVoteCommand(CCSPlayerController? player, CommandInfo command)
    {
        using var perf = PerfScope.Measure("cancel vote command");
        // null = server console
        if (player != null && !AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            command.ReplyToCommand($"{Localizer["noaccess"]}");
            return;
        }

        if (_vote is not { IsOpen: true })
        {
            command.ReplyToCommand($"{Localizer["novote"]}");
            return;
        }

        FinishVote(VoteOutcome.Cancelled, "votecancelled.admin");
    }

    private static bool HasAccess(CCSPlayerController player, string permission)
    {
        if (string.IsNullOrEmpty(permission))
            return true;

        if (permission.StartsWith('#'))
            return AdminManager.PlayerInGroup(player, permission);

        if (permission.StartsWith('@'))
            return AdminManager.PlayerHasPermissions(player, permission);

        return true;
    }

    private static bool MatchesFlagOrGroup(CCSPlayerController player, string list)
    {
        if (string.IsNullOrWhiteSpace(list))
            return false;

        foreach (var entry in list.Split(','))
        {
            var flag = entry.Trim();
            if (flag.StartsWith('#'))
            {
                if (AdminManager.PlayerInGroup(player, flag))
                    return true;
            }
            else if (flag.StartsWith('@'))
            {
                if (AdminManager.PlayerHasPermissions(player, flag))
                    return true;
            }
        }

        return false;
    }

    private void OpenPlayerMenu(CCSPlayerController player, string title, VoteType type)
    {
        if (_menu == null)
        {
            player.PrintToChat($"{Localizer["nomenu"]}");
            return;
        }

        IMenu menu = _menu.GetMenu(title);
        menu.PostSelectAction = PostSelectAction.Close;

        foreach (var target in GetActivePlayers())
        {
            if (MatchesFlagOrGroup(target, Config.General.HideFromMenu))
                continue;

            ulong targetSteamId = GetSteamId64(target);
            if (targetSteamId == 0)
                continue;

            menu.AddMenuOption(target.PlayerName, (client, _) =>
            {
                using var perf = PerfScope.Measure("vote menu select");
                var current = FindHuman(targetSteamId);
                if (current == null)
                {
                    client.PrintToChat($"{Localizer["targetleft"]}");
                    return;
                }

                StartVote(client, current, targetSteamId, type);
            });
        }

        menu.Open(player);
    }

    private static ulong GetSteamId64(CCSPlayerController player)
    {
        if (!player.IsValid)
            return 0;

        return player.AuthorizedSteamID?.SteamId64 ?? 0;
    }

    /// <summary>Connected humans, whether or not they are playing. Everyone of them sees the vote.</summary>
    private static List<CCSPlayerController> GetHumans()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && !p.IsBot && p.Connected == PlayerConnectedState.Connected)
            .ToList();
    }

    /// <summary>Humans in the game. They are the ones who can vote and count towards the threshold.</summary>
    private static List<CCSPlayerController> GetActivePlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => !p.IsHLTV && !p.IsBot && p.PlayerPawn.IsValid && p.Connected == PlayerConnectedState.Connected)
            .ToList();
    }

    private static CCSPlayerController? FindHuman(ulong steamId)
    {
        if (steamId == 0)
            return null;

        return GetHumans().FirstOrDefault(player => GetSteamId64(player) == steamId);
    }

    private bool EnoughPlayers()
    {
        return GetActivePlayers().Count >= Config.General.RequiredPlayers;
    }
}
