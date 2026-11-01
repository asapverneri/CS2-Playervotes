using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Playervotes;

/// <summary>
/// Debug-only timing of game-thread work, switched only by the plugin's own <c>EnablePerfMonitor</c> config property
/// (separate from <c>EnableDebugLogs</c>, passed in through <see cref="Configure"/>). When it is off, every call is one flag check.
/// Logs sections slower than the threshold, and late ticks with the slowest sections of that tick, the time spent
/// outside this plugin, GC collections and game-thread allocations. A hitch can then be pinned to this plugin,
/// another plugin/the engine, or the GC.
/// All members except <see cref="Background"/> are game-thread only.
/// </summary>
internal static class PerfMonitor
{
    private const int TopCount = 3;
    private const int MaxLogsPerSecond = 20;
    private const double LateTickFactor = 2.0;
    private const double PauseMs = 5000;
    private const double SlowBackgroundMs = 250;

    private static readonly string?[] TopNames = new string?[TopCount];
    private static readonly Delegate?[] TopHandlers = new Delegate?[TopCount];
    private static readonly double[] TopMs = new double[TopCount];

    private static ILogger? _logger;
    private static int _gameThreadId;
    private static double _thresholdMs = 2.0;
    private static int _depth;

    private static long _lastTick;
    private static double _measuredMs;
    private static int _sections;
    private static int _gen0, _gen1, _gen2;
    private static long _allocated;

    private static long _logWindowStart;
    private static int _logsInWindow;
    private static int _suppressed;

    public static bool Enabled { get; private set; }

    /// <summary>
    /// Call from <c>Load</c> (game thread) and after every config reload, with <paramref name="enabled"/> taken
    /// from the config's <c>EnablePerfMonitor</c>. This is the only way the monitor is turned on or off.
    /// </summary>
    public static void Configure(ILogger logger, bool enabled, double sectionThresholdMs)
    {
        _logger = logger;
        _gameThreadId = Environment.CurrentManagedThreadId;
        _thresholdMs = sectionThresholdMs > 0 ? sectionThresholdMs : 2.0;
        Enabled = enabled;
        Reset();
    }

    /// <summary>Forgets the previous tick (map start, config reload), so loading time is not reported as a late tick.</summary>
    public static void Reset()
    {
        _lastTick = 0;
        _depth = 0;
        ClearTick();
    }

    /// <summary>
    /// Called from an <c>OnTick</c> listener that is registered only while <c>EnablePerfMonitor</c> is on.
    /// Compares the time since the previous tick with the tick interval and logs a late tick.
    /// </summary>
    public static void OnTick(float tickIntervalSeconds)
    {
        if (!Enabled)
            return;

        var now = Stopwatch.GetTimestamp();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocated = GC.GetAllocatedBytesForCurrentThread();

        if (_lastTick != 0 && tickIntervalSeconds > 0)
        {
            var gapMs = Stopwatch.GetElapsedTime(_lastTick, now).TotalMilliseconds;
            var limitMs = tickIntervalSeconds * 1000.0 * LateTickFactor;
            if (gapMs > limitMs && gapMs < PauseMs && CanLog())
            {
                _logger?.LogInformation(
                    "[perf] Late tick: {Gap:F1} ms since the previous tick (limit {Limit:F1} ms). This plugin: {Measured:F2} ms in {Sections} sections, slowest: {Top}. Outside this plugin: {Outside:F1} ms. GC gen0/gen1/gen2: +{Gen0}/+{Gen1}/+{Gen2}, game-thread allocations: {AllocatedKb} KB.",
                    gapMs, limitMs, _measuredMs, _sections, DescribeTop(), Math.Max(0, gapMs - _measuredMs),
                    gen0 - _gen0, gen1 - _gen1, gen2 - _gen2, (allocated - _allocated) / 1024);
            }
        }

        _lastTick = now;
        _gen0 = gen0;
        _gen1 = gen1;
        _gen2 = gen2;
        _allocated = allocated;
        _depth = 0;
        ClearTick();
    }

    /// <summary>
    /// Logs a slow background operation (SQL, HTTP, file I/O). Safe from any thread; measure with
    /// <c>Stopwatch.GetTimestamp()</c> around the awaited call.
    /// </summary>
    public static void Background(string operation, double elapsedMs)
    {
        if (Enabled && elapsedMs >= SlowBackgroundMs)
            _logger?.LogInformation("[perf] Background {Operation} took {Elapsed:F0} ms.", operation, elapsedMs);
    }

    internal static bool IsGameThread => Environment.CurrentManagedThreadId == _gameThreadId;

    internal static int Enter() => ++_depth;

    internal static void Exit(string section, Delegate? handler, double elapsedMs, bool outermost)
    {
        if (_depth > 0)
            _depth--;

        // Nested sections are already inside the outer one's time.
        if (outermost)
        {
            _measuredMs += elapsedMs;
            _sections++;
        }

        var slot = 0;
        for (var i = 1; i < TopCount; i++)
        {
            if (TopMs[i] < TopMs[slot])
                slot = i;
        }

        if (elapsedMs > TopMs[slot])
        {
            TopNames[slot] = section;
            TopHandlers[slot] = handler;
            TopMs[slot] = elapsedMs;
        }

        if (elapsedMs >= _thresholdMs && CanLog())
            _logger?.LogInformation("[perf] {Section} took {Elapsed:F2} ms on the game thread.", Describe(section, handler), elapsedMs);
    }

    /// <summary>At most <see cref="MaxLogsPerSecond"/> perf logs per second, so logging does not cause the next hitch.</summary>
    private static bool CanLog()
    {
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_logWindowStart, now).TotalSeconds >= 1)
        {
            if (_suppressed > 0)
                _logger?.LogInformation("[perf] {Count} perf logs suppressed in the last second.", _suppressed);
            _logWindowStart = now;
            _logsInWindow = 0;
            _suppressed = 0;
        }

        if (_logsInWindow < MaxLogsPerSecond)
        {
            _logsInWindow++;
            return true;
        }

        _suppressed++;
        return false;
    }

    private static void ClearTick()
    {
        _measuredMs = 0;
        _sections = 0;
        Array.Clear(TopNames);
        Array.Clear(TopHandlers);
        Array.Clear(TopMs);
    }

    private static string DescribeTop()
    {
        var order = Enumerable.Range(0, TopCount).Where(i => TopNames[i] != null).OrderByDescending(i => TopMs[i]);
        var text = new StringBuilder();
        foreach (var i in order)
        {
            if (text.Length > 0)
                text.Append(", ");
            text.Append(Describe(TopNames[i]!, TopHandlers[i])).Append(' ')
                .Append(TopMs[i].ToString("F2", CultureInfo.InvariantCulture)).Append(" ms");
        }

        return text.Length == 0 ? "none" : text.ToString();
    }

    private static string Describe(string section, Delegate? handler)
    {
        if (handler == null)
            return section;

        var method = handler.Method;
        var type = method.DeclaringType;
        return type == null
            ? $"{section} subscriber {method.Name}"
            : $"{section} subscriber {type.Assembly.GetName().Name}:{type.FullName}.{method.Name}";
    }
}

/// <summary>
/// Measures one section of game-thread work: <c>using var _ = PerfScope.Measure("player_spawn");</c>.
/// A struct on the stack, so it does not allocate. Never keep it across an <c>await</c>.
/// </summary>
internal readonly struct PerfScope : IDisposable
{
    private readonly string? _section;
    private readonly Delegate? _handler;
    private readonly long _start;
    private readonly bool _outermost;

    private PerfScope(string section, Delegate? handler)
    {
        _section = section;
        _handler = handler;
        _outermost = PerfMonitor.Enter() == 1;
        _start = Stopwatch.GetTimestamp();
    }

    /// <summary>Starts measuring a named section. <paramref name="section"/> must be a constant string.</summary>
    public static PerfScope Measure(string section) =>
        PerfMonitor.Enabled && PerfMonitor.IsGameThread ? new PerfScope(section, null) : default;

    /// <summary>Starts measuring one subscriber of an event this plugin raises. The subscriber is named only when it is slow.</summary>
    public static PerfScope Measure(string source, Delegate handler) =>
        PerfMonitor.Enabled && PerfMonitor.IsGameThread ? new PerfScope(source, handler) : default;

    public void Dispose()
    {
        if (_section == null || !PerfMonitor.IsGameThread)
            return;

        PerfMonitor.Exit(_section, _handler, Stopwatch.GetElapsedTime(_start).TotalMilliseconds, _outermost);
    }
}
