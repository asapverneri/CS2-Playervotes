namespace Playervotes;

public enum VoteType { Kick, Ban, Mute, Gag, Silence }

public enum VoteChoice { None, Yes, No }

public enum VoteOutcome { Pending, Passed, Failed, Cancelled }

public readonly record struct VoteTally(int Yes, int No, int Eligible, int Required);

/// <summary>
/// The one vote that can run at a time. Pure state: the plugin feeds it the current voters and it
/// says whether the vote passed, failed or is still open. Votes are keyed by SteamID64.
/// </summary>
internal sealed class VoteSession
{
    private readonly HashSet<ulong> _yes = new();
    private readonly HashSet<ulong> _no = new();

    public VoteSession(VoteType type, ulong targetSteamId, string targetName, string starterName, float percentage, DateTime endsAt)
    {
        Type = type;
        TargetSteamId = targetSteamId;
        TargetName = targetName;
        StarterName = starterName;
        Percentage = percentage;
        EndsAt = endsAt;
    }

    public VoteType Type { get; }
    public ulong TargetSteamId { get; }
    public string TargetName { get; }
    public string StarterName { get; }
    public float Percentage { get; }
    public DateTime EndsAt { get; }

    /// <summary>The target disconnected and the vote goes on (voteban with an offline ban command).</summary>
    public bool TargetLeft { get; set; }

    public VoteOutcome Outcome { get; private set; } = VoteOutcome.Pending;
    public bool IsOpen => Outcome == VoteOutcome.Pending;

    /// <summary>When the result card disappears, set by <see cref="Finish"/>.</summary>
    public DateTime ClosesAt { get; private set; }

    public VoteTally Tally { get; private set; }

    public VoteChoice ChoiceOf(ulong steamId) =>
        _yes.Contains(steamId) ? VoteChoice.Yes : _no.Contains(steamId) ? VoteChoice.No : VoteChoice.None;

    /// <summary>False when this player already voted.</summary>
    public bool Cast(ulong steamId, bool yes)
    {
        if (_yes.Contains(steamId) || _no.Contains(steamId))
            return false;

        (yes ? _yes : _no).Add(steamId);
        return true;
    }

    /// <summary>
    /// Counts only the votes of players who can vote right now, so someone who left no longer counts
    /// and the required number follows the current player count.
    /// </summary>
    public VoteTally Count(IReadOnlyCollection<ulong> eligible)
    {
        int yes = 0, no = 0;
        foreach (var id in eligible)
        {
            if (_yes.Contains(id)) yes++;
            else if (_no.Contains(id)) no++;
        }

        Tally = new VoteTally(yes, no, eligible.Count, Required(Percentage, eligible.Count));
        return Tally;
    }

    public VoteOutcome Decide(VoteTally tally, DateTime now)
    {
        if (tally.Yes >= tally.Required)
            return VoteOutcome.Passed;

        int undecided = tally.Eligible - tally.Yes - tally.No;
        if (tally.Yes + undecided < tally.Required || now >= EndsAt)
            return VoteOutcome.Failed;

        return VoteOutcome.Pending;
    }

    public void Finish(VoteOutcome outcome, DateTime closesAt)
    {
        Outcome = outcome;
        ClosesAt = closesAt;
    }

    public int SecondsLeft(DateTime now) => Math.Max(0, (int)Math.Ceiling((EndsAt - now).TotalSeconds));

    public static int Required(float percentage, int eligible)
    {
        // percentage * count first, so 80% of 5 is exactly 4 and not 4.0000001
        int required = (int)Math.Ceiling(Math.Clamp(percentage, 0f, 100f) * eligible / 100.0);
        return Math.Max(1, required);
    }
}
