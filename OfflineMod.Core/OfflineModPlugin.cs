using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace OfflineMod;

/// <summary>
/// OfflineMod — lets admins ban/mute/gag players who already disconnected. Tracks recent leavers
/// (this server) and exposes `!offline` → a menu to punish them via AdminCommands (offline-by-SteamID).
/// AdminCommands / AdminManager / MenuManager are resolved in OnAllModulesLoaded.
/// </summary>
public sealed class OfflineModPlugin : IModSharpModule
{
    public string DisplayName   => "OfflineMod";
    public string DisplayAuthor => "yappershq";

    private readonly ILogger<OfflineModPlugin> _logger;
    private readonly InterfaceBridge           _bridge;
    private readonly RecentLeaversTracker      _tracker;
    private readonly OfflineCommandModule      _commands;

    public OfflineModPlugin(
        ISharedSystem  sharedSystem,
        string         dllPath,
        string         sharpPath,
        Version        version,
        IConfiguration configuration,
        bool           hotReload)
    {
        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger   = loggerFactory.CreateLogger<OfflineModPlugin>();

        _bridge   = new InterfaceBridge(this, sharedSystem);
        _tracker  = new RecentLeaversTracker(_bridge);
        _commands = new OfflineCommandModule(_bridge, _tracker, loggerFactory.CreateLogger<OfflineCommandModule>());
    }

    public bool Init()
    {
        _tracker.Start();
        return true;
    }

    public void PostInit() { }

    public void OnAllModulesLoaded()
    {
        _bridge.ResolveModules();
        _commands.Start();

        _logger.LogInformation("[OfflineMod] Loaded (AdminCommands={Admin}, AdminManager={Mgr}, Menu={Menu})",
            _bridge.AdminService is not null, _bridge.AdminManager is not null, _bridge.MenuManager is not null);
    }

    public void Shutdown()
    {
        _commands.Stop();
        _tracker.Stop();
    }
}
