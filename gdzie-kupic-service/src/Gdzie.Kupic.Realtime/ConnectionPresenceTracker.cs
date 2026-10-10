namespace Gdzie.Kupic.Realtime;

using Gdzie.Kupic.Notifications;

/// <summary>In-memory set of open hub connections per user (single API instance).</summary>
public sealed class ConnectionPresenceTracker : IPresenceTracker
{
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];
    private readonly object _gate = new();

    public bool IsOnline(Guid userId)
    {
        lock (_gate) return _connections.ContainsKey(userId);
    }

    public void Connected(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set)) _connections[userId] = set = [];
            set.Add(connectionId);
        }
    }

    public void Disconnected(Guid userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set)) return;

            set.Remove(connectionId);
            if (set.Count == 0) _connections.Remove(userId);
        }
    }
}
