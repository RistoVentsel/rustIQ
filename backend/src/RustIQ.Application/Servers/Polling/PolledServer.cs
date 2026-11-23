using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

public sealed class PolledServer
{
    private readonly List<(long AtMs, int Players)> _recentPeaks = new List<(long AtMs, int Players)>();

    public PolledServer(int index, Guid id, RustServerType serverType, long createdAtMs)
    {
        Index = index;
        Id = id;
        ServerType = serverType;
        CreatedAtMs = createdAtMs;
    }

    public int Index { get; }

    public Guid Id { get; private set; }

    public RustServerType ServerType { get; private set; }

    public long CreatedAtMs { get; private set; }

    public bool Exists { get; set; }

    public long? LastCheckedAtMs { get; set; }

    public long? LastSeenAtMs { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    public int QueuePlayers { get; set; }

    public int? Rank { get; set; }

    public long? NextWipeAtMs { get; set; }

    public long DueKeyMs
    {
        get
        {
            long? lastChecked = LastCheckedAtMs;
            if (lastChecked.HasValue)
            {
                return lastChecked.Value;
            }

            return CreatedAtMs;
        }
    }

    public void AddSnapshot(long atMs, int players, long keepForMs)
    {
        while (_recentPeaks.Count > 0 && _recentPeaks[_recentPeaks.Count - 1].Players <= players)
        {
            _recentPeaks.RemoveAt(_recentPeaks.Count - 1);
        }

        _recentPeaks.Add((atMs, players));
        int expired = 0;
        while (expired < _recentPeaks.Count && _recentPeaks[expired].AtMs < atMs - keepForMs)
        {
            expired++;
        }

        if (expired > 0)
        {
            _recentPeaks.RemoveRange(0, expired);
        }
    }

    public bool HasSnapshotWithPlayersSince(long sinceMs, int players)
    {
        for (int i = 0; i < _recentPeaks.Count; i++)
        {
            if (_recentPeaks[i].AtMs >= sinceMs)
            {
                return _recentPeaks[i].Players >= players;
            }
        }

        return false;
    }

    public void Reset(Guid id, RustServerType serverType, long createdAtMs)
    {
        Id = id;
        ServerType = serverType;
        CreatedAtMs = createdAtMs;
        LastCheckedAtMs = null;
        LastSeenAtMs = null;
        Status = ServerStatus.Unknown;
        Players = 0;
        MaxPlayers = 0;
        QueuePlayers = 0;
        Rank = null;
        NextWipeAtMs = null;
        _recentPeaks.Clear();
    }
}
