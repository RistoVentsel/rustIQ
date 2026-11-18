using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// What a polling strategy may know about one server: the fields production keeps on the server row,
/// its current rank and its recent snapshots. Times are Unix milliseconds UTC, which keeps the
/// per-tick scans over ~20,000 servers cheap.
/// </summary>
public sealed class PolledServer
{
    // Recent snapshots with strictly falling player counts from oldest to newest: an older snapshot with
    // no more players than a newer one can never decide "any snapshot since X had at least N players".
    private readonly List<(long AtMs, int Players)> _recentPeaks = [];

    public PolledServer(int index, Guid id, RustServerType serverType, long createdAtMs)
    {
        Index = index;
        Id = id;
        ServerType = serverType;
        CreatedAtMs = createdAtMs;
    }

    /// <summary>Position in the server list; strategies may key their own estimates by it.</summary>
    public int Index { get; }

    public Guid Id { get; private set; }

    public RustServerType ServerType { get; private set; }

    public long CreatedAtMs { get; private set; }

    /// <summary>Whether the server is in the tracked population right now. Strategies skip the rest.</summary>
    public bool Exists { get; set; }

    public long? LastCheckedAtMs { get; set; }

    public long? LastSeenAtMs { get; set; }

    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    public int QueuePlayers { get; set; }

    /// <summary>The server's rank in the latest ranking, or null when it is not ranked.</summary>
    public int? Rank { get; set; }

    /// <summary>RustIQ's stored prediction of the next wipe (<c>NextWipeAtUtc</c>), if any.</summary>
    public long? NextWipeAtMs { get; set; }

    /// <summary><c>LastCheckedAtUtc ?? CreatedAtUtc</c>: what due-ness and lane order run off.</summary>
    public long DueKeyMs => LastCheckedAtMs ?? CreatedAtMs;

    /// <summary>
    /// Records a snapshot (time, recorded players) for the recent-peak rules. Snapshots older than
    /// <paramref name="keepForMs"/> are forgotten, so it must cover the longest window asked about.
    /// </summary>
    public void AddSnapshot(long atMs, int players, long keepForMs)
    {
        while (_recentPeaks.Count > 0 && _recentPeaks[^1].Players <= players)
        {
            _recentPeaks.RemoveAt(_recentPeaks.Count - 1);
        }

        _recentPeaks.Add((atMs, players));
        var expired = 0;
        while (expired < _recentPeaks.Count && _recentPeaks[expired].AtMs < atMs - keepForMs)
        {
            expired++;
        }

        if (expired > 0)
        {
            _recentPeaks.RemoveRange(0, expired);
        }
    }

    /// <summary>Whether any snapshot at or after <paramref name="sinceMs"/> recorded at least <paramref name="players"/>.</summary>
    public bool HasSnapshotWithPlayersSince(long sinceMs, int players)
    {
        // The first kept snapshot in the window has the window's highest count.
        foreach (var (atMs, recorded) in _recentPeaks)
        {
            if (atMs >= sinceMs)
            {
                return recorded >= players;
            }
        }

        return false;
    }

    /// <summary>Turns this slot into a new, never polled server (used for the phantom population).</summary>
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
