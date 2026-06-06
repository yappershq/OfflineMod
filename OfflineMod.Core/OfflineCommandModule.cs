using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Sharp.Modules.AdminCommands.Shared;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.MenuManager.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace OfflineMod;

/// <summary>
/// `!offline` admin command → menu of recently-disconnected players → ban/mute/gag the chosen one
/// via AdminCommands' offline-by-SteamID path (persists + enforced on next connect everywhere).
/// </summary>
internal sealed class OfflineCommandModule
{
    private const string ModuleId   = "OfflineMod";
    private const string PermPunish = "@offlinemod/punish";

    private readonly InterfaceBridge              _bridge;
    private readonly RecentLeaversTracker         _tracker;
    private readonly ILogger<OfflineCommandModule> _logger;

    private IClientManager.DelegateClientCommand? _fallback;
    private bool                                  _usedRegistry;

    public OfflineCommandModule(InterfaceBridge bridge, RecentLeaversTracker tracker, ILogger<OfflineCommandModule> logger)
    {
        _bridge  = bridge;
        _tracker = tracker;
        _logger  = logger;
    }

    public void Start()
    {
        if (_bridge.MenuManager is null)
        {
            _logger.LogWarning("[OfflineMod] MenuManager unavailable — !offline menu disabled");
            return;
        }

        if (_bridge.AdminManager is { } am)
        {
            am.MountAdminManifest(ModuleId, () => new AdminTableManifest(
                new Dictionary<string, HashSet<string>> { ["offlinemod"] = [PermPunish] }, [], []));

            am.GetCommandRegistry(ModuleId)
              .RegisterAdminCommand("offline", OnOfflineCommand, ImmutableArray.Create(PermPunish));

            _usedRegistry = true;
            _logger.LogInformation("[OfflineMod] !offline registered (perm {Perm})", PermPunish);
        }
        else
        {
            _fallback = (client, command) =>
            {
                OnOfflineCommand(client, command);
                return ECommandAction.Handled;
            };
            _bridge.ClientManager.InstallCommandCallback("offline", _fallback);
            _logger.LogWarning("[OfflineMod] AdminManager unavailable — !offline registered WITHOUT permission check");
        }
    }

    public void Stop()
    {
        if (!_usedRegistry && _fallback is not null)
            _bridge.ClientManager.RemoveCommandCallback("offline", _fallback);
    }

    private void OnOfflineCommand(IGameClient? invoker, StringCommand command)
    {
        if (invoker is null || invoker.IsFakeClient)
            return;

        ShowLeaversMenu(invoker);
    }

    private void ShowLeaversMenu(IGameClient admin)
    {
        if (_bridge.MenuManager is not { } menu)
            return;

        var leavers = _tracker.GetRecent();
        var builder = Menu.Create().Title($"Recent leavers ({leavers.Count})");

        if (leavers.Count == 0)
        {
            builder.Item("(no recent disconnects)", _ => { });
        }
        else
        {
            foreach (var l in leavers)
            {
                var captured = l;
                builder.Item($"{captured.Name}  ·  {Ago(captured.Time)}", _ => ShowPunishMenu(admin, captured));
            }
        }

        menu.DisplayMenu(admin, builder.Build());
    }

    private void ShowPunishMenu(IGameClient admin, RecentLeaversTracker.Leaver l)
    {
        if (_bridge.MenuManager is not { } menu)
            return;

        var builder = Menu.Create().Title($"Punish {l.Name}")
            .Item("Ban — 1 day",     _ => Apply(admin, l, AdminOperationType.Ban,  TimeSpan.FromDays(1)))
            .Item("Ban — 1 week",    _ => Apply(admin, l, AdminOperationType.Ban,  TimeSpan.FromDays(7)))
            .Item("Ban — 1 month",   _ => Apply(admin, l, AdminOperationType.Ban,  TimeSpan.FromDays(30)))
            .Item("Ban — permanent", _ => Apply(admin, l, AdminOperationType.Ban,  null))
            .Item("Mute — 1 hour",   _ => Apply(admin, l, AdminOperationType.Mute, TimeSpan.FromHours(1)))
            .Item("Mute — 1 day",    _ => Apply(admin, l, AdminOperationType.Mute, TimeSpan.FromDays(1)))
            .Item("Gag — 1 hour",    _ => Apply(admin, l, AdminOperationType.Gag,  TimeSpan.FromHours(1)))
            .Item("Gag — 1 day",     _ => Apply(admin, l, AdminOperationType.Gag,  TimeSpan.FromDays(1)))
            .Item("« Back",          _ => ShowLeaversMenu(admin));

        menu.DisplayMenu(admin, builder.Build());
    }

    private void Apply(IGameClient admin, RecentLeaversTracker.Leaver l, AdminOperationType type, TimeSpan? duration)
    {
        _bridge.MenuManager?.QuitMenu(admin);

        if (_bridge.AdminService is not { } svc)
        {
            admin.Print(HudPrintChannel.Chat, " [OfflineMod] AdminCommands unavailable — cannot apply.");
            return;
        }

        SteamID target = l.SteamId;
        svc.Apply(admin, target, type, duration, $"[OfflineMod] {Verb(type)} (offline) by admin");

        var span = duration is { } d ? $"for {Human(d)}" : "permanently";
        admin.Print(HudPrintChannel.Chat, $" [OfflineMod] {Verb(type)} {l.Name} <{l.SteamId}> {span}.");
        _logger.LogInformation("[OfflineMod] {Admin} -> {Verb} {Name} ({Steam}) {Span}",
            (ulong) admin.SteamId, Verb(type), l.Name, l.SteamId, span);
    }

    private static string Verb(AdminOperationType type) =>
        type == AdminOperationType.Ban ? "Banned" : type == AdminOperationType.Mute ? "Muted" : "Gagged";

    private static string Ago(DateTime utc)
    {
        var s = (int) (DateTime.UtcNow - utc).TotalSeconds;
        if (s < 60) return $"{s}s ago";
        if (s < 3600) return $"{s / 60}m ago";
        return $"{s / 3600}h ago";
    }

    private static string Human(TimeSpan d)
    {
        if (d.TotalDays >= 1) return $"{(int) d.TotalDays}d";
        if (d.TotalHours >= 1) return $"{(int) d.TotalHours}h";
        return $"{(int) d.TotalMinutes}m";
    }
}
