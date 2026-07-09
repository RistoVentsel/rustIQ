namespace RustIQ.Api.Realtime;

public sealed class WatchedServerRegistry : IWatchedServerRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<Guid>> _serverIdsByConnection = [];
    private readonly Dictionary<Guid, int> _watchCountsByServerId = [];

    public void Watch(string connectionId, Guid serverId)
    {
        lock (_lock)
        {
            if (!_serverIdsByConnection.TryGetValue(connectionId, out var serverIds))
            {
                serverIds = [];
                _serverIdsByConnection[connectionId] = serverIds;
            }

            if (!serverIds.Add(serverId))
            {
                return;
            }

            _watchCountsByServerId[serverId] = _watchCountsByServerId.GetValueOrDefault(serverId) + 1;
        }
    }

    public void Unwatch(string connectionId, Guid serverId)
    {
        lock (_lock)
        {
            if (!_serverIdsByConnection.TryGetValue(connectionId, out var serverIds)
                || !serverIds.Remove(serverId))
            {
                return;
            }

            if (serverIds.Count == 0)
            {
                _serverIdsByConnection.Remove(connectionId);
            }

            DecrementWatchCount(serverId);
        }
    }

    public void UnwatchAll(string connectionId)
    {
        lock (_lock)
        {
            if (!_serverIdsByConnection.Remove(connectionId, out var serverIds))
            {
                return;
            }

            foreach (var serverId in serverIds)
            {
                DecrementWatchCount(serverId);
            }
        }
    }

    public IReadOnlyList<Guid> GetWatchedServerIds()
    {
        lock (_lock)
        {
            return _watchCountsByServerId.Keys.ToArray();
        }
    }

    private void DecrementWatchCount(Guid serverId)
    {
        if (!_watchCountsByServerId.TryGetValue(serverId, out var watchCount))
        {
            return;
        }

        if (watchCount <= 1)
        {
            _watchCountsByServerId.Remove(serverId);
            return;
        }

        _watchCountsByServerId[serverId] = watchCount - 1;
    }
}
