using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Timers;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace Playervotes;

public partial class Playervotes : BasePlugin, IPluginConfig<PlayervotesConfig>
{
    public override string ModuleName => "Playervotes";
    public override string ModuleDescription => "Lightweight playervotes for cs2";
    public override string ModuleAuthor => "verneri";
    public override string ModuleVersion => "2.5";

    private const int MaxSlots = 64;

    public PlayervotesConfig Config { get; set; } = new();

    private static readonly PluginCapability<IMenuApi?> MenuApi = new("menu:nfcore");
    private IMenuApi? _menu;
    private bool _unloading;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try { _menu = MenuApi.Get(); }
        catch { _menu = null; }

        if (_menu == null)
            Logger.LogError("MenuManager API (menu:nfcore) is not available. Player vote menus will not open.");
    }

    public void OnConfigParsed(PlayervotesConfig config)
    {
        // A version 1 file was read into the defaults; the migration carries its values over
        Config = ConfigMigration.TryMigrate(config.GetConfigPath(), Logger) ?? config;

        var report = new ConfigUpdater.Report();
        if (ConfigUpdater.UpdateFile(config.GetConfigPath(),
                text => ConfigUpdater.AddMissing(text, new PlayervotesConfig(), ConfigUpdater.Options, report), Logger))
            Logger.LogInformation("Config updated with new settings: {Keys}", string.Join(", ", report.Added));
        if (report.Unknown.Count > 0)
            Logger.LogWarning("Config has settings this version does not use: {Keys}", string.Join(", ", report.Unknown));
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Loaded (version {Version})", ModuleVersion);

        ApplyDebugSettings();
        RegisterListener<Listeners.OnMapStart>(_ => PerfMonitor.Reset());
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        RegisterCommands();

        // Runs for the plugin's lifetime and is never killed: killing timers from timer callbacks corrupts CSS's timer list
        AddTimer(1.0f, OnVoteSecond, TimerFlags.REPEAT);

        StartPanel();
    }

    public override void Unload(bool hotReload)
    {
        _unloading = true;
        HookVoteTick(false);
        _vote = null;
        StopPanel();
    }
}
