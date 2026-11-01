using CounterStrikeSharp.API.Core;
using PanoramaManager;

namespace Playervotes.Hud;

/// <summary>
/// The vote panel. One layout driven through two handles: a passive one that only shows the vote
/// (no cursor, the player keeps playing) and a live one that captures the mouse so TAK / NIE can be
/// clicked. Switching mode closes one handle and opens the other in the same tick, so the reveal class
/// never changes on the client and the card does not blink.
/// Holds no plugin state: the plugin builds a <see cref="VoteView"/> per viewer and calls <see cref="Show"/>.
/// </summary>
public sealed class VoteHud
{
    public const string LayoutPath = "panorama/layout/custom_game/pv_vote.vxml_c";
    public const string RootId = "PvVoteRoot";

    public const string YesButton = "pv_yes";
    public const string NoButton = "pv_no";
    public const string CancelButton = "pv_cancel";

    private const string HiddenClass = "pv-hidden";
    private const string LiveClass = "pv-live";
    private const string TargetClass = "pv-me";
    private const string EndedClass = "pv-ended";
    private const string PassedClass = "pv-passed";
    private const string FailedClass = "pv-failed";
    private const string PickedClass = "pv-picked";
    private const string FillId = "pv_fill";
    private const int MaxSlots = 64;

    private static readonly string[] FillClasses = Enumerable.Range(0, 11).Select(i => "pv-f-" + i).ToArray();

    public static LayoutContract Contract(bool captureInput) => new()
    {
        RootPanelId = RootId,
        RowCount = 0,
        RevealClass = "pv-show",
        HiddenClass = HiddenClass,
        TitleVar = "pv_title",
        CaptureInput = captureInput,
        SpectatingMessage = null
    };

    private readonly Viewer?[] _viewers = new Viewer?[MaxSlots];
    private PanelHandle? _passive;
    private PanelHandle? _live;
    private bool _switching;

    /// <summary>A button on the live panel was clicked (<see cref="YesButton"/>, <see cref="NoButton"/>, <see cref="CancelButton"/>).</summary>
    public Action<CCSPlayerController, string>? Clicked { get; set; }

    /// <summary>The library rebuilt the panel for this player (round restart, respawn) - paint it again.</summary>
    public Action<CCSPlayerController>? Restored { get; set; }

    public void Attach(PanelHandle passive, PanelHandle live)
    {
        Detach();
        _passive = passive;
        _live = live;
        _passive.OnEvent += OnEvent;
        _live.OnEvent += OnEvent;
    }

    public void Detach()
    {
        if (_passive != null) _passive.OnEvent -= OnEvent;
        if (_live != null) _live.OnEvent -= OnEvent;
        _passive = null;
        _live = null;
        Array.Clear(_viewers);
    }

    public bool IsLive(CCSPlayerController player) => ViewerOf(player) is { Live: true } viewer && viewer.Open;

    /// <summary>Opens the panel in the given mode (switching handles if needed) and paints what changed.</summary>
    public bool Show(CCSPlayerController player, VoteView view, bool live)
    {
        if (_passive is null || _live is null || player is not { IsValid: true, IsBot: false, IsHLTV: false })
            return false;
        if ((uint)player.Slot >= MaxSlots)
            return false;

        var viewer = ViewerOf(player);
        if (viewer == null)
            _viewers[player.Slot] = viewer = new Viewer(player.SteamID);

        var handle = live ? _live : _passive;
        var other = live ? _passive : _live;

        if (other.IsOpenFor(player))
        {
            _switching = true;
            try { other.Close(player); }
            finally { _switching = false; }
            viewer.Reset();
        }

        if (!handle.IsOpenFor(player))
        {
            handle.Open(player);
            viewer.Reset();
            if (!handle.IsOpenFor(player))
            {
                viewer.Open = false;
                return false;
            }
        }

        viewer.Open = true;
        viewer.Live = live;
        viewer.Handle = handle;
        Paint(player, viewer, view);
        return true;
    }

    public void Hide(CCSPlayerController player)
    {
        if (_passive is null || _live is null || !player.IsValid || (uint)player.Slot >= MaxSlots)
            return;

        _switching = true;
        try
        {
            if (_live.IsOpenFor(player)) _live.Close(player);
            if (_passive.IsOpenFor(player)) _passive.Close(player);
        }
        finally { _switching = false; }

        _viewers[player.Slot] = null;
    }

    /// <summary>Drops what we remember about a slot (disconnect). The library closes the entity itself.</summary>
    public void Forget(int slot)
    {
        if ((uint)slot < MaxSlots)
            _viewers[slot] = null;
    }

    private static void Paint(CCSPlayerController player, Viewer viewer, VoteView view)
    {
        Var(player, viewer, "pv_title", view.Title);
        Var(player, viewer, "pv_time", view.Time);
        Var(player, viewer, "pv_type", view.Type);
        Var(player, viewer, "pv_target", view.Target);
        Var(player, viewer, "pv_starter", view.Starter);
        Var(player, viewer, "pv_yes_lbl", view.YesLabel);
        Var(player, viewer, "pv_no_lbl", view.NoLabel);
        Var(player, viewer, "pv_yes_count", view.YesCount);
        Var(player, viewer, "pv_no_count", view.NoCount);
        Var(player, viewer, "pv_need", view.Need);
        Var(player, viewer, "pv_hint", view.Hint);
        Var(player, viewer, "pv_result", view.Result);

        bool ended = view.Outcome != VoteOutcome.Pending;
        Class(player, viewer, RootId, LiveClass, viewer.Live && !ended);
        Class(player, viewer, RootId, TargetClass, view.IsTarget);
        Class(player, viewer, RootId, EndedClass, ended);
        Class(player, viewer, RootId, PassedClass, view.Outcome == VoteOutcome.Passed);
        Class(player, viewer, RootId, FailedClass, view.Outcome is VoteOutcome.Failed or VoteOutcome.Cancelled);
        Class(player, viewer, YesButton, PickedClass, view.Choice == VoteChoice.Yes);
        Class(player, viewer, NoButton, PickedClass, view.Choice == VoteChoice.No);
        Swap(player, viewer, FillId, FillClasses[Math.Clamp(view.FillStep, 0, FillClasses.Length - 1)], FillClasses);
    }

    private static void Var(CCSPlayerController player, Viewer viewer, string name, string value)
    {
        if (viewer.Vars.TryGetValue(name, out var current) && current == value)
            return;

        viewer.Handle!.SetVariableFor(player, name, value);
        viewer.Vars[name] = value;
    }

    // Sends only changes, but after a Reset every class is written once: per-player classes stay on the
    // entity after a close, so a reopened panel still carries pv-ended and the rest from the last vote
    private static void Class(CCSPlayerController player, Viewer viewer, string panelId, string cls, bool on)
    {
        var key = panelId + "|" + cls;
        if (viewer.Classes.TryGetValue(key, out var current) && current == on)
            return;

        viewer.Handle!.SetClassFor(player, panelId, cls, on);
        viewer.Classes[key] = on;
    }

    private static void Swap(CCSPlayerController player, Viewer viewer, string panelId, string next, string[] group)
    {
        if (viewer.Swapped.TryGetValue(panelId, out var current))
        {
            if (current == next)
                return;
            viewer.Handle!.SetClassFor(player, panelId, current, false);
        }
        else
        {
            // Unknown after a Reset: an old step may still be on from the previous vote
            foreach (var cls in group)
                if (cls != next)
                    viewer.Handle!.SetClassFor(player, panelId, cls, false);
        }

        viewer.Handle!.SetClassFor(player, panelId, next, true);
        viewer.Swapped[panelId] = next;
    }

    private void OnEvent(PanelEvent e)
    {
        if (_switching || e.Player is not { IsValid: true } player)
            return;

        var viewer = ViewerOf(player);
        if (viewer == null)
            return;

        switch (e.Action)
        {
            case PanelAction.Close:
                // Round restart, Dispose or disconnect. The plugin's next refresh opens it again if the vote is still on
                viewer.Open = false;
                viewer.Live = false;
                viewer.Reset();
                break;

            case PanelAction.Restored:
                viewer.Reset();
                Restored?.Invoke(player);
                break;

            case PanelAction.Button:
                switch (e.ElementId)
                {
                    case "pv_yes":
                    case "pv_no":
                    case "pv_cancel":
                        Clicked?.Invoke(player, e.ElementId);
                        break;
                }
                break;
        }
    }

    private Viewer? ViewerOf(CCSPlayerController player)
    {
        if ((uint)player.Slot >= MaxSlots)
            return null;

        var viewer = _viewers[player.Slot];
        return viewer != null && viewer.SteamId == player.SteamID ? viewer : null;
    }

    private sealed class Viewer
    {
        public Viewer(ulong steamId) => SteamId = steamId;

        public ulong SteamId { get; }
        public bool Open;
        public bool Live;
        public PanelHandle? Handle;
        public Dictionary<string, string> Vars { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> Classes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Swapped { get; } = new(StringComparer.Ordinal);

        // State on the client is unknown after a handle switch, close or restore, so everything is written again
        public void Reset()
        {
            Vars.Clear();
            Classes.Clear();
            Swapped.Clear();
        }
    }
}
