using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;

namespace OfflineMod;

/// <summary>
/// Keeps a small ring of the players who most recently disconnected from THIS server, so an admin
/// can still ban/mute/gag them after they've left. Captured at OnClientDisconnecting (data still
/// intact). Newest-first, deduped by SteamID.
/// </summary>
internal sealed class RecentLeaversTracker : IClientListener
{
    public int ListenerVersion  => IClientListener.ApiVersion;
    public int ListenerPriority => 0;

    internal readonly record struct Leaver(ulong SteamId, string Name, string Ip, DateTime Time);

    private const int MaxLeavers = 32;

    private readonly InterfaceBridge       _bridge;
    private readonly LinkedList<Leaver>    _leavers = new();
    private readonly object                _lock    = new();
    private bool                           _installed;

    public RecentLeaversTracker(InterfaceBridge bridge) => _bridge = bridge;

    public void Start()
    {
        _bridge.ClientManager.InstallClientListener(this);
        _installed = true;
    }

    public void Stop()
    {
        if (_installed)
            _bridge.ClientManager.RemoveClientListener(this);
        _installed = false;
    }

    public void OnClientDisconnecting(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client.IsFakeClient || client.IsHltv)
            return;

        var steamId = (ulong) client.SteamId;
        if (steamId == 0)
            return;

        var leaver = new Leaver(steamId, client.Name ?? steamId.ToString(), client.GetAddress(false) ?? "", DateTime.UtcNow);

        lock (_lock)
        {
            for (var node = _leavers.First; node is not null; node = node.Next)
            {
                if (node.Value.SteamId == steamId)
                {
                    _leavers.Remove(node);
                    break;
                }
            }

            _leavers.AddFirst(leaver);

            while (_leavers.Count > MaxLeavers)
                _leavers.RemoveLast();
        }
    }

    public IReadOnlyList<Leaver> GetRecent()
    {
        lock (_lock)
        {
            return _leavers.ToList();
        }
    }
}
