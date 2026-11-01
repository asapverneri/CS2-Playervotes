using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Playervotes.Hud;

namespace Playervotes;

public partial class Playervotes
{
    // How long the result stays on the panel after a vote ends
    private const int ResultSeconds = 4;

    private VoteSession? _vote;
    private readonly bool[] _live = new bool[MaxSlots];
    private readonly PlayerButtons[] _lastButtons = new PlayerButtons[MaxSlots];
    private readonly Dictionary<ulong, DateTime>[] _protectedUntil =
        Enum.GetValues<VoteType>().Select(_ => new Dictionary<ulong, DateTime>()).ToArray();

    private Listeners.OnTick? _voteTick;
    private bool _voteTickHooked;

    private void StartVote(CCSPlayerController starter, CCSPlayerController target, ulong targetSteamId, VoteType type)
    {
        if (_vote != null)
        {
            starter.PrintToChat($"{Localizer["voteinprogress"]}");
            return;
        }

        ulong starterSteamId = GetSteamId64(starter);
        if (targetSteamId == 0 || starterSteamId == 0)
            return;

        if (targetSteamId == starterSteamId && !Config.General.AllowSelfVote)
        {
            starter.PrintToChat($"{Localizer["voteurself"]}");
            return;
        }

        if (MatchesFlagOrGroup(target, Config.General.VoteImmunity))
        {
            starter.PrintToChat($"{Localizer["cantvote"]}");
            return;
        }

        int minutesLeft = ProtectedMinutesLeft(type, targetSteamId);
        if (minutesLeft > 0)
        {
            starter.PrintToChat($"{Localizer["protected", target.PlayerName, minutesLeft]}");
            return;
        }

        if (!EnoughPlayers())
        {
            starter.PrintToChat($"{Localizer["noenoughplayers", Config.General.RequiredPlayers]}");
            return;
        }

        if (!CanVote(starter))
        {
            starter.PrintToChat($"{Localizer["notactive"]}");
            return;
        }

        var settings = Settings(type);
        _vote = new VoteSession(type, targetSteamId, VoteView.Clean(target.PlayerName), VoteView.Clean(starter.PlayerName),
            settings.Percentage, DateTime.UtcNow.AddSeconds(Math.Max(5, Config.General.VoteDurationSeconds)));
        _vote.Cast(starterSteamId, true);

        // Without self votes the target can't choose, so they count as a fixed NO
        if (!Config.General.AllowSelfVote)
            _vote.Cast(targetSteamId, false);

        Array.Clear(_live);
        HookVoteTick(true);

        LogDebug("Vote {Type} on {Target} started by {Starter}, {Percentage}% needed", type, targetSteamId, starterSteamId, settings.Percentage);

        EvaluateVote();
    }

    private void CastVote(CCSPlayerController player, bool yes)
    {
        var vote = _vote;
        if (vote is not { IsOpen: true })
            return;

        ulong steamId = GetSteamId64(player);
        if (steamId == 0)
            return;

        if (steamId == vote.TargetSteamId && !Config.General.AllowSelfVote)
        {
            player.PrintToChat($"{Localizer["voteurself"]}");
            SetLive(player, false);
            return;
        }

        if (!CanVote(player))
        {
            player.PrintToChat($"{Localizer["notactive"]}");
            SetLive(player, false);
            return;
        }

        if (!vote.Cast(steamId, yes))
        {
            player.PrintToChat($"{Localizer["alreadyvoted"]}");
            SetLive(player, false);
            return;
        }

        if ((uint)player.Slot < MaxSlots)
            _live[player.Slot] = false;

        EvaluateVote();
    }

    /// <summary>
    /// Recounts against the players on the server right now and ends the vote when it is decided.
    /// Runs after every vote, every second and after a disconnect, so the threshold follows the player count.
    /// </summary>
    private void EvaluateVote()
    {
        var vote = _vote;
        if (vote is not { IsOpen: true })
            return;

        if (!vote.TargetLeft && FindHuman(vote.TargetSteamId) == null)
        {
            // Leaving must not dodge a voteban: the vote goes on and a pass bans by SteamID
            if (vote.Type != VoteType.Ban || string.IsNullOrWhiteSpace(Config.Ban.OfflineCommand))
            {
                FinishVote(VoteOutcome.Cancelled, "votecancelled.left");
                return;
            }

            vote.TargetLeft = true;
            Server.PrintToChatAll($"{Localizer["votecontinues.left", vote.TargetName]}");
            LogDebug("Voteban target {Target} left, vote continues", vote.TargetSteamId);
        }

        var tally = vote.Count(EligibleVoters(vote));
        var outcome = vote.Decide(tally, DateTime.UtcNow);
        if (outcome == VoteOutcome.Pending)
            ShowVoteToAll();
        else
            FinishVote(outcome);
    }

    private void FinishVote(VoteOutcome outcome, string? cancelKey = null)
    {
        var vote = _vote;
        if (vote is not { IsOpen: true })
            return;

        vote.Finish(outcome, DateTime.UtcNow.AddSeconds(ResultSeconds));
        HookVoteTick(false);
        Array.Clear(_live);

        if (outcome != VoteOutcome.Cancelled)
            Protect(vote.Type, vote.TargetSteamId);

        var tally = vote.Tally;
        LogDebug("Vote {Type} on {Target} ended: {Outcome} ({Yes}/{Required}, {No} no)", vote.Type, vote.TargetSteamId, outcome, tally.Yes, tally.Required, tally.No);

        switch (outcome)
        {
            case VoteOutcome.Passed:
                if (FindHuman(vote.TargetSteamId) is { } target)
                    ExecuteVote(vote.Type, target);
                else if (vote.Type == VoteType.Ban)
                    ExecuteOfflineBan(vote);
                break;

            case VoteOutcome.Failed:
                Server.PrintToChatAll($"{Localizer["votefailed", Localizer[Settings(vote.Type).TitleKey], vote.TargetName, tally.Yes, tally.No, tally.Required]}");
                break;

            case VoteOutcome.Cancelled:
                Server.PrintToChatAll($"{Localizer[cancelKey ?? "votecancelled.admin", vote.TargetName]}");
                break;
        }

        // The result card shows for ResultSeconds, then OnVoteSecond closes it
        ShowVoteToAll();
    }

    private void ExecuteVote(VoteType type, CCSPlayerController target)
    {
        switch (type)
        {
            case VoteType.Ban:
                Server.PrintToChatAll($"{Localizer["votebanned", target.PlayerName, Config.Ban.Duration]}");
                Server.ExecuteCommand($"css_ban #{target.UserId} {Config.Ban.Duration} \"{Config.Ban.Reason}\"");
                break;
            case VoteType.Mute:
                Server.PrintToChatAll($"{Localizer["votemuted", target.PlayerName, Config.Mute.Duration]}");
                Server.ExecuteCommand($"css_mute #{target.UserId} {Config.Mute.Duration} \"{Config.Mute.Reason}\"");
                break;
            case VoteType.Kick:
                Server.PrintToChatAll($"{Localizer["votekicked", target.PlayerName]}");
                Server.ExecuteCommand($"css_kick #{target.UserId} \"{Config.Kick.Reason}\"");
                break;
            case VoteType.Gag:
                Server.PrintToChatAll($"{Localizer["votegagged", target.PlayerName, Config.Gag.Duration]}");
                Server.ExecuteCommand($"css_gag #{target.UserId} {Config.Gag.Duration} \"{Config.Gag.Reason}\"");
                break;
            case VoteType.Silence:
                Server.PrintToChatAll($"{Localizer["votesilenced", target.PlayerName, Config.Silence.Duration]}");
                Server.ExecuteCommand($"css_silence #{target.UserId} {Config.Silence.Duration} \"{Config.Silence.Reason}\"");
                break;
        }
    }

    private void ExecuteOfflineBan(VoteSession vote)
    {
        if (string.IsNullOrWhiteSpace(Config.Ban.OfflineCommand))
            return;

        var command = Config.Ban.OfflineCommand
            .Replace("{steamid64}", vote.TargetSteamId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{duration}", Config.Ban.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{reason}", Config.Ban.Reason.Replace("\"", "'"));

        Server.PrintToChatAll($"{Localizer["votebanned", vote.TargetName, Config.Ban.Duration]}");
        Server.ExecuteCommand(command);
        LogDebug("Offline voteban: {Command}", command);
    }

    private void CloseVote()
    {
        _vote = null;
        HookVoteTick(false);
        Array.Clear(_live);
        HideVoteFromAll();
    }

    private void OnVoteSecond()
    {
        if (_vote == null || _unloading)
            return;

        // Panel open/close can touch timers in other plugins, so it never runs inside a timer callback
        Server.NextFrame(() =>
        {
            using var perf = PerfScope.Measure("vote second");
            var vote = _vote;
            if (vote == null || _unloading)
                return;

            if (vote.IsOpen)
                EvaluateVote();
            else if (DateTime.UtcNow >= vote.ClosesAt)
                CloseVote();
        });
    }

    private void OnClientDisconnect(int slot)
    {
        using var perf = PerfScope.Measure("client_disconnect");
        if ((uint)slot >= MaxSlots)
            return;

        _live[slot] = false;
        _lastButtons[slot] = 0;
        _hud?.Forget(slot);

        // The controller is still counted during this callback
        if (_vote is { IsOpen: true })
            Server.NextFrame(EvaluateVote);
    }

    private void OnMapEnd()
    {
        using var perf = PerfScope.Measure("map_end");
        if (_vote == null)
            return;

        LogDebug("Map ended during a vote, vote dropped");
        CloseVote();
    }

    // E toggles the panel between watching and clicking. Hooked only while a vote is open
    private void HookVoteTick(bool on)
    {
        if (on == _voteTickHooked)
            return;

        _voteTickHooked = on;
        _voteTick ??= OnVoteTick;
        Array.Clear(_lastButtons);
        if (on)
            RegisterListener(_voteTick);
        else
            RemoveListener(_voteTick);
    }

    private void OnVoteTick()
    {
        var vote = _vote;
        if (vote is not { IsOpen: true })
            return;

        using var perf = PerfScope.Measure("vote use key");
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || player.IsHLTV || (uint)player.Slot >= MaxSlots)
                continue;
            if (player.Pawn.Value is not { IsValid: true, MovementServices: not null })
                continue;

            var buttons = player.Buttons;
            var pressed = buttons & ~_lastButtons[player.Slot];
            _lastButtons[player.Slot] = buttons;

            if ((pressed & PlayerButtons.Use) != 0)
                OnUsePressed(player, vote);
        }
    }

    private void OnUsePressed(CCSPlayerController player, VoteSession vote)
    {
        if (_live[player.Slot])
        {
            SetLive(player, false);
            return;
        }

        ulong steamId = GetSteamId64(player);
        if (steamId == 0 || IsExcludedTarget(vote, steamId) || vote.ChoiceOf(steamId) != VoteChoice.None || !CanVote(player))
            return;

        SetLive(player, true);
    }

    private static bool CanVote(CCSPlayerController player) =>
        player is { IsValid: true, IsBot: false, IsHLTV: false, Connected: PlayerConnectedState.Connected } && player.PlayerPawn.IsValid;

    // The target is always counted: their own vote, or the fixed NO given at the start.
    // A voteban target who left stays in the count, so their vote is not lost
    private static List<ulong> EligibleVoters(VoteSession vote)
    {
        var voters = new List<ulong>();
        foreach (var player in GetActivePlayers())
        {
            ulong steamId = GetSteamId64(player);
            if (steamId != 0)
                voters.Add(steamId);
        }

        if (vote.TargetLeft && !voters.Contains(vote.TargetSteamId))
            voters.Add(vote.TargetSteamId);

        return voters;
    }

    // The target can't cast or change a vote on themselves unless AllowSelfVote is on
    private bool IsExcludedTarget(VoteSession vote, ulong steamId) =>
        steamId == vote.TargetSteamId && !Config.General.AllowSelfVote;

    private void Protect(VoteType type, ulong steamId)
    {
        if (Config.General.TargetProtectionMinutes > 0)
            _protectedUntil[(int)type][steamId] = DateTime.UtcNow.AddMinutes(Config.General.TargetProtectionMinutes);
    }

    private int ProtectedMinutesLeft(VoteType type, ulong steamId)
    {
        var list = _protectedUntil[(int)type];
        if (!list.TryGetValue(steamId, out var until))
            return 0;

        var left = until - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            list.Remove(steamId);
            return 0;
        }

        return (int)Math.Ceiling(left.TotalMinutes);
    }
}
