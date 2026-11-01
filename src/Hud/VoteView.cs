namespace Playervotes.Hud;

/// <summary>Everything one viewer sees on the vote panel, already translated and formatted.</summary>
public sealed record VoteView
{
    public string Title { get; init; } = "";
    public string Time { get; init; } = "";
    public string Type { get; init; } = "";
    public string Target { get; init; } = "";
    public string Starter { get; init; } = "";
    public string YesLabel { get; init; } = "";
    public string NoLabel { get; init; } = "";
    public string YesCount { get; init; } = "0";
    public string NoCount { get; init; } = "0";
    public string Need { get; init; } = "";
    public string Hint { get; init; } = "";
    public string Result { get; init; } = "";

    /// <summary>Yes votes against the required number, 0-10 (one class per 10%).</summary>
    public int FillStep { get; init; }

    public VoteChoice Choice { get; init; }
    public bool IsTarget { get; init; }
    public VoteOutcome Outcome { get; init; }

    public static string Clean(string? text, int max = 32)
    {
        if (string.IsNullOrEmpty(text))
            return "-";

        var chars = new char[Math.Min(text.Length, max)];
        int n = 0;
        foreach (var c in text)
        {
            if (n == chars.Length) break;
            if (c >= ' ') chars[n++] = c;
        }

        return n == 0 ? "-" : new string(chars, 0, n);
    }
}
