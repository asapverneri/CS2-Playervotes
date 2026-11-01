using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace Playervotes;

public partial class Playervotes
{
    private readonly Listeners.OnTick _perfTick = () => PerfMonitor.OnTick(Server.TickInterval);
    private bool _perfTickHooked;

    /// <summary>The perf monitor follows EnablePerfMonitor only; the OnTick listener exists only while it is on.</summary>
    private void ApplyDebugSettings()
    {
        PerfMonitor.Configure(Logger, Config.EnablePerfMonitor, Config.PerfSlowSectionMs);
        if (Config.EnablePerfMonitor == _perfTickHooked)
            return;

        _perfTickHooked = Config.EnablePerfMonitor;
        if (_perfTickHooked)
            RegisterListener(_perfTick);
        else
            RemoveListener(_perfTick);
    }

    /// <summary>Diagnostic flow log, written only with EnableDebugLogs.</summary>
    internal void LogDebug(string message, params object?[] args)
    {
        if (Config.EnableDebugLogs)
            Logger.LogInformation("[debug] " + message, args);
    }
}
