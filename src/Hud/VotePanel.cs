using System.Globalization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;
using PanoramaManager;
using Playervotes.Hud;

namespace Playervotes;

public partial class Playervotes
{
    private VoteHud? _hud;
    private PanelHandle? _passivePanel;
    private PanelHandle? _livePanel;
    private bool _panoramaStarted;

    private void StartPanel()
    {
        if (_unloading || _panoramaStarted)
            return;

        try
        {
            Panorama.Init(this);
            _panoramaStarted = true;
            _passivePanel = Panorama.Spawn(VoteHud.LayoutPath, VoteHud.Contract(captureInput: false));
            _livePanel = Panorama.Spawn(VoteHud.LayoutPath, VoteHud.Contract(captureInput: true));
            _hud = new VoteHud { Clicked = OnPanelClicked, Restored = OnPanelRestored };
            _hud.Attach(_passivePanel, _livePanel);

            if (!Panorama.CanReceiveClicks)
                Logger.LogWarning("Panorama has no click channel: the vote panel shows, but players have to vote with !yes / !no.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Vote panel failed to start; votes work through chat (!yes / !no) only.");
            StopPanel();
        }
    }

    private void StopPanel()
    {
        try { _hud?.Detach(); } catch (Exception ex) { Logger.LogError(ex, "Vote panel detach failed."); }
        _hud = null;
        try { _livePanel?.Dispose(); } catch (Exception ex) { Logger.LogError(ex, "Vote panel dispose failed."); }
        try { _passivePanel?.Dispose(); } catch (Exception ex) { Logger.LogError(ex, "Vote panel dispose failed."); }
        _livePanel = null;
        _passivePanel = null;

        if (!_panoramaStarted)
            return;

        _panoramaStarted = false;
        try { Panorama.Shutdown(); } catch (Exception ex) { Logger.LogError(ex, "Panorama shutdown failed."); }
    }

    private void ShowVoteToAll()
    {
        if (_hud == null || _vote == null)
            return;

        foreach (var player in GetHumans())
            ShowVote(player);
    }

    private void ShowVote(CCSPlayerController player)
    {
        if (_hud == null || _vote == null || (uint)player.Slot >= MaxSlots)
            return;

        try
        {
            bool live = _live[player.Slot] && _vote.IsOpen;
            if (!_hud.Show(player, BuildView(player, _vote), live))
                _live[player.Slot] = false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Vote panel draw failed for {SteamId}.", player.SteamID);
            _live[player.Slot] = false;
        }
    }

    private void HideVoteFromAll()
    {
        if (_hud == null)
            return;

        foreach (var player in GetHumans())
        {
            try { _hud.Hide(player); }
            catch (Exception ex) { Logger.LogError(ex, "Vote panel close failed for {SteamId}.", player.SteamID); }
        }
    }

    private void SetLive(CCSPlayerController player, bool live)
    {
        if ((uint)player.Slot >= MaxSlots)
            return;

        _live[player.Slot] = live && _vote is { IsOpen: true };
        ShowVote(player);
    }

    private void OnPanelClicked(CCSPlayerController player, string button)
    {
        using var perf = PerfScope.Measure("vote panel click");
        switch (button)
        {
            case VoteHud.YesButton: CastVote(player, true); break;
            case VoteHud.NoButton: CastVote(player, false); break;
            case VoteHud.CancelButton: SetLive(player, false); break;
        }
    }

    private void OnPanelRestored(CCSPlayerController player)
    {
        using var perf = PerfScope.Measure("vote panel restored");
        if (_vote == null)
            _hud?.Hide(player);
        else
            ShowVote(player);
    }

    private VoteView BuildView(CCSPlayerController player, VoteSession vote)
    {
        string L(string key, params object[] args) => Localizer.ForPlayer(player, key, args);

        var tally = vote.Tally;
        ulong steamId = GetSteamId64(player);
        bool isTarget = IsExcludedTarget(vote, steamId);
        var choice = vote.ChoiceOf(steamId);
        bool live = (uint)player.Slot < MaxSlots && _live[player.Slot];

        string hint;
        if (!vote.IsOpen) hint = "";
        else if (isTarget) hint = L("panel.hint.target");
        else if (choice == VoteChoice.Yes) hint = L("panel.hint.voted.yes");
        else if (choice == VoteChoice.No) hint = L("panel.hint.voted.no");
        else if (!CanVote(player)) hint = L("panel.hint.spectator");
        else if (live) hint = L("panel.hint.live");
        else hint = L("panel.hint.press");

        string result = vote.Outcome switch
        {
            VoteOutcome.Passed => L("panel.result.passed"),
            VoteOutcome.Failed => L("panel.result.failed"),
            VoteOutcome.Cancelled => L("panel.result.cancelled"),
            _ => ""
        };

        return new VoteView
        {
            Title = L("panel.title"),
            Time = vote.IsOpen ? L("panel.time", vote.SecondsLeft(DateTime.UtcNow)) : "",
            Type = L("panel.type." + vote.Type.ToString().ToLowerInvariant()),
            Target = vote.TargetName,
            Starter = L("panel.starter", vote.StarterName),
            YesLabel = L("panel.yes"),
            NoLabel = L("panel.no"),
            YesCount = tally.Yes.ToString(CultureInfo.InvariantCulture),
            NoCount = tally.No.ToString(CultureInfo.InvariantCulture),
            Need = L("panel.need", tally.Required, tally.Eligible),
            Hint = hint,
            Result = result,
            FillStep = tally.Required > 0 ? Math.Min(10, tally.Yes * 10 / tally.Required) : 0,
            Choice = choice,
            IsTarget = isTarget,
            Outcome = vote.Outcome
        };
    }
}
