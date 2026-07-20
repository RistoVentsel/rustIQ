using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.TestDoubles;

internal sealed class InMemoryServerRepository : IServerRepository
{
    public List<Server> Servers { get; } = [];

    public List<ServerSnapshot> Snapshots { get; } = [];

    public List<ServerRanking> Rankings { get; } = [];

    public List<ServerPlayerCurrent> CurrentPlayers { get; } = [];

    public int? LastPagedTake { get; private set; }

    public int? LastSnapshotLimit { get; private set; }

    public Task<int> CountAsync(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DeduplicateServers(ApplyServerFilters(search, status, serverTypes, wipeFilter, now)).Count());
    }

    public Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now,
        string sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        LastPagedTake = take;
        var servers = ApplyServerSort(
                DeduplicateServers(ApplyServerFilters(search, status, serverTypes, wipeFilter, now)),
                sortBy,
                sortDescending,
                now)
            .Skip(skip)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Server>>(servers);
    }

    public Task<IReadOnlyDictionary<Guid, ServerSnapshotTrend>> GetSnapshotTrendsAsync(
        IReadOnlyList<Guid> serverIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        var trends = Snapshots
            .Where(snapshot => serverIds.Contains(snapshot.ServerId) && snapshot.CheckedAtUtc >= sinceUtc)
            .GroupBy(snapshot => snapshot.ServerId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var orderedSnapshots = group.OrderBy(snapshot => snapshot.CheckedAtUtc).ToArray();
                    var first = orderedSnapshots[0];
                    var last = orderedSnapshots[^1];

                    return new ServerSnapshotTrend(
                        group.Key,
                        last.Players - first.Players,
                        orderedSnapshots.Max(snapshot => snapshot.Players));
                });

        return Task.FromResult<IReadOnlyDictionary<Guid, ServerSnapshotTrend>>(trends);
    }

    public Task<IReadOnlyDictionary<Guid, ServerRankingSummary>> GetLatestRankingsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken)
    {
        var rankings = Rankings
            .Where(ranking => serverIds.Contains(ranking.ServerId))
            .ToDictionary(
                ranking => ranking.ServerId,
                ranking => new ServerRankingSummary(
                    ranking.ServerId,
                    ranking.Rank,
                    ranking.AveragePlayers,
                    ranking.PeakPlayers,
                    ranking.SnapshotCount,
                    ranking.WindowStartUtc,
                    ranking.WindowEndUtc,
                    ranking.GeneratedAtUtc));

        return Task.FromResult<IReadOnlyDictionary<Guid, ServerRankingSummary>>(rankings);
    }

    public Task<int> ReplaceLatestServerRankingsAsync(
        DateTimeOffset now,
        int maxRankedServers,
        int rankingWindowDays,
        int minimumSnapshots,
        int recentOnlineWindowHours,
        int minimumMaxPlayers,
        CancellationToken cancellationToken)
    {
        var windowStartUtc = now.AddDays(-rankingWindowDays);
        var recentOnlineCutoffUtc = now.AddHours(-recentOnlineWindowHours);
        var preferredServerIds = DeduplicateServers(Servers
                .Where(server =>
                    server.MaxPlayers >= minimumMaxPlayers
                    && !string.IsNullOrWhiteSpace(server.Name)
                    && server.LastSeenAtUtc is not null
                    && server.LastSeenAtUtc >= recentOnlineCutoffUtc))
            .Select(server => server.Id)
            .ToHashSet();

        var nextRankings = Snapshots
            .Where(snapshot =>
                preferredServerIds.Contains(snapshot.ServerId)
                && snapshot.CheckedAtUtc >= windowStartUtc
                && snapshot.CheckedAtUtc <= now)
            .GroupBy(snapshot => snapshot.ServerId)
            .Select(group => new
            {
                ServerId = group.Key,
                AveragePlayers = group.Average(snapshot => snapshot.Players),
                PeakPlayers = group.Max(snapshot => snapshot.Players),
                SnapshotCount = group.Count()
            })
            .Where(server => server.SnapshotCount >= minimumSnapshots)
            .OrderByDescending(server => server.AveragePlayers)
            .ThenByDescending(server => server.PeakPlayers)
            .ThenBy(server => server.ServerId)
            .Take(maxRankedServers)
            .Select((server, index) => new ServerRanking
            {
                Id = Guid.NewGuid(),
                ServerId = server.ServerId,
                Rank = index + 1,
                AveragePlayers = server.AveragePlayers,
                PeakPlayers = server.PeakPlayers,
                SnapshotCount = server.SnapshotCount,
                WindowStartUtc = windowStartUtc,
                WindowEndUtc = now,
                GeneratedAtUtc = now
            })
            .ToArray();

        Rankings.Clear();
        Rankings.AddRange(nextRankings);

        return Task.FromResult(Rankings.Count);
    }

    public Task<IReadOnlyList<Server>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Server>>(DeduplicateServers(Servers).ToArray());
    }

    public Task<IReadOnlyList<Server>> GetTrackedByIdsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Server>>(
            Servers.Where(server => serverIds.Contains(server.Id)).ToArray());
    }

    public Task<Server?> GetByIdWithMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Servers.FirstOrDefault(server => server.Id == serverId));
    }

    public Task<Server?> GetByHostAndQueryPortAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Servers.FirstOrDefault(server =>
            server.Host == host && server.QueryPort == queryPort));
    }

    public Task<Server?> GetByBattleMetricsServerIdAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Servers.FirstOrDefault(server =>
            server.BattleMetricsServerId == battleMetricsServerId));
    }

    public Task<IReadOnlyList<Server>> GetServersByPlayerCountSourceAsync(
        ServerPlayerCountSource playerCountSource,
        int take,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Server>>(
            Servers
                .Where(server => server.PlayerCountSource == playerCountSource)
                .OrderByDescending(server => server.UpdatedAtUtc)
            .Take(take)
            .ToArray());
    }

    public Task<IReadOnlyList<Server>> GetServersForPlayerCountRefreshAsync(
        ServerPlayerCountSource playerCountSource,
        DateTimeOffset staleBeforeUtc,
        int take,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Server>>(Servers
            .Where(server =>
                server.PlayerCountSource == playerCountSource
                && server.BattleMetricsServerId != null
                && ((server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc) <= staleBeforeUtc))
            .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
            .ThenByDescending(server => server.Players)
            .Take(take)
            .ToArray());
    }

    public Task<IReadOnlyList<Server>> GetServersForRefreshLaneAsync(
        ServerRefreshLane refreshLane,
        DateTimeOffset dueBeforeUtc,
        DateTimeOffset now,
        int take,
        int popularRankThreshold,
        int popularPlayerThreshold,
        int popularPeakPlayerThreshold,
        TimeSpan recentPeakWindow,
        TimeSpan popularOfflineGrace,
        TimeSpan slowOfflineAfter,
        TimeSpan veryStaleOfflineAfter,
        CancellationToken cancellationToken)
    {
        var recentPeakSinceUtc = now.Subtract(recentPeakWindow);
        var popularOfflineGraceSinceUtc = now.Subtract(popularOfflineGrace);
        var slowOfflineBeforeUtc = now.Subtract(slowOfflineAfter);
        var veryStaleOfflineBeforeUtc = now.Subtract(veryStaleOfflineAfter);

        var query = Servers
            .Where(server =>
                server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics
                && server.BattleMetricsServerId != null
                && ((server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc) <= dueBeforeUtc));

        query = refreshLane switch
        {
            ServerRefreshLane.Fast => query.Where(server =>
                IsPopular(server, popularRankThreshold, popularPlayerThreshold, popularPeakPlayerThreshold, recentPeakSinceUtc)
                || IsRecentlyPopularOffline(server, popularRankThreshold, popularPeakPlayerThreshold, popularOfflineGraceSinceUtc)),
            ServerRefreshLane.NormalOnline => query.Where(server =>
                server.Status == ServerStatus.Online
                && !IsPopular(server, popularRankThreshold, popularPlayerThreshold, popularPeakPlayerThreshold, recentPeakSinceUtc)),
            ServerRefreshLane.SlowOffline => query.Where(server =>
                server.Status != ServerStatus.Online
                && server.LastSeenAtUtc is not null
                && server.LastSeenAtUtc < slowOfflineBeforeUtc
                && server.LastSeenAtUtc >= veryStaleOfflineBeforeUtc
                && !IsPopular(server, popularRankThreshold, popularPlayerThreshold, popularPeakPlayerThreshold, recentPeakSinceUtc)),
            ServerRefreshLane.VeryStaleOffline => query.Where(server =>
                server.Status != ServerStatus.Online
                && (server.LastSeenAtUtc is null || server.LastSeenAtUtc < veryStaleOfflineBeforeUtc)
                && !IsPopular(server, popularRankThreshold, popularPlayerThreshold, popularPeakPlayerThreshold, recentPeakSinceUtc)),
            _ => []
        };

        return Task.FromResult<IReadOnlyList<Server>>(query
            .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
            .ThenByDescending(server => server.Players)
            .Take(take)
            .ToArray());
    }

    public Task<IReadOnlyList<Server>> GetServersForMapMetadataRefreshAsync(
        DateTimeOffset fetchedBeforeUtc,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Server>>(Servers
            .Where(server =>
                server.BattleMetricsServerId is not null
                && (server.MapMetadata is null
                    || server.MapMetadata.FetchedAtUtc < fetchedBeforeUtc))
            .OrderByDescending(server => server.Status == ServerStatus.Online)
            .ThenByDescending(server => server.Players)
            .ThenBy(server => server.Id)
            .ToArray());
    }

    public Task<IReadOnlyList<ServerSnapshot>> GetSnapshotsAsync(
        Guid serverId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken)
    {
        LastSnapshotLimit = limit;
        var snapshots = Snapshots
            .Where(snapshot => snapshot.ServerId == serverId)
            .Where(snapshot => from is null || snapshot.CheckedAtUtc >= from.Value)
            .Where(snapshot => to is null || snapshot.CheckedAtUtc <= to.Value)
            .OrderByDescending(snapshot => snapshot.CheckedAtUtc)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ServerSnapshot>>(snapshots);
    }

    public Task<IReadOnlyList<ServerPlayerCurrent>> GetCurrentPlayersAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var players = CurrentPlayers
            .Where(player => player.ServerId == serverId)
            .OrderByDescending(player => player.DurationSeconds)
            .ThenBy(player => player.PlayerName)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ServerPlayerCurrent>>(players);
    }

    public Task<ServerMapMetadata?> GetMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Servers
            .FirstOrDefault(server => server.Id == serverId)
            ?.MapMetadata);
    }

    public Task<int> DeleteSnapshotsOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var deletedCount = Snapshots.RemoveAll(snapshot => snapshot.CheckedAtUtc < cutoffUtc);
        return Task.FromResult(deletedCount);
    }

    public void Add(Server server)
    {
        Servers.Add(server);
    }

    public void AddSnapshot(ServerSnapshot snapshot)
    {
        Snapshots.Add(snapshot);
    }

    public Task ReplaceCurrentPlayersAsync(
        Guid serverId,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers,
        CancellationToken cancellationToken)
    {
        CurrentPlayers.RemoveAll(player => player.ServerId == serverId);
        CurrentPlayers.AddRange(currentPlayers);

        var server = Servers.FirstOrDefault(server => server.Id == serverId);
        if (server is not null)
        {
            server.CurrentPlayers = currentPlayers.ToList();
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private IEnumerable<Server> ApplyServerFilters(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now)
    {
        var query = Servers.AsEnumerable();

        if (status is not null)
        {
            query = query.Where(server => server.Status == status);
        }

        if (serverTypes.Count > 0)
        {
            query = query.Where(server => serverTypes.Contains(server.ServerType));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(server =>
                server.Host.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (server.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (server.Map?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        query = wipeFilter switch
        {
            ServerReadService.WipeFilterNext24Hours => query.Where(server =>
                server.NextWipeAtUtc is not null
                && server.NextWipeAtUtc >= now
                && server.NextWipeAtUtc <= now.AddHours(24)),
            ServerReadService.WipeFilterNext7Days => query.Where(server =>
                server.NextWipeAtUtc is not null
                && server.NextWipeAtUtc >= now
                && server.NextWipeAtUtc <= now.AddDays(7)),
            ServerReadService.WipeFilterWiped24Hours => query.Where(server =>
                server.LastWipeAtUtc is not null
                && server.LastWipeAtUtc >= now.AddHours(-24)
                && server.LastWipeAtUtc <= now),
            ServerReadService.WipeFilterHasWipeData => query.Where(server =>
                server.LastWipeAtUtc is not null || server.NextWipeAtUtc is not null),
            _ => query
        };

        return query;
    }

    private static IReadOnlyList<Server> DeduplicateServers(IEnumerable<Server> servers)
    {
        return servers
            .GroupBy(server => $"{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}")
            .Select(group => group
                .OrderByDescending(server => server.BattleMetricsServerId.HasValue)
                .ThenByDescending(server => server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics)
                .ThenByDescending(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
                .First())
            .ToArray();
    }

    private IOrderedEnumerable<Server> ApplyServerSort(
        IEnumerable<Server> servers,
        string sortBy,
        bool sortDescending,
        DateTimeOffset now)
    {
        var sorted = sortBy switch
        {
            ServerReadService.SortByLastWipe => sortDescending
                ? servers.OrderByDescending(server => server.LastWipeAtUtc ?? DateTimeOffset.MinValue)
                : servers.OrderBy(server => server.LastWipeAtUtc ?? DateTimeOffset.MaxValue),
            ServerReadService.SortByRank => sortDescending
                ? servers
                    .OrderBy(server => GetRank(server.Id) is null)
                    .ThenByDescending(server => GetRank(server.Id))
                : servers
                    .OrderBy(server => GetRank(server.Id) is null)
                    .ThenBy(server => GetRank(server.Id)),
            ServerReadService.SortByNextWipe => sortDescending
                ? servers
                    .OrderBy(server => server.NextWipeAtUtc is null || server.NextWipeAtUtc < now)
                    .ThenByDescending(server => server.NextWipeAtUtc >= now ? server.NextWipeAtUtc : null)
                : servers
                    .OrderBy(server => server.NextWipeAtUtc is null || server.NextWipeAtUtc < now)
                    .ThenBy(server => server.NextWipeAtUtc >= now ? server.NextWipeAtUtc : null),
            ServerReadService.SortByLocation => sortDescending
                ? servers.OrderByDescending(server => server.Country ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : servers.OrderBy(server => server.Country ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            ServerReadService.SortByServerType => sortDescending
                ? servers.OrderByDescending(server => ServerTypeSortRank(server.ServerType))
                : servers.OrderBy(server => ServerTypeSortRank(server.ServerType)),
            _ => sortDescending
                ? servers.OrderByDescending(server => server.Players)
                : servers.OrderBy(server => server.Players)
        };

        return sorted
            .ThenByDescending(server => server.Status == ServerStatus.Online)
            .ThenBy(server => server.Name)
            .ThenBy(server => server.Host);
    }

    private static int ServerTypeSortRank(RustServerType serverType)
    {
        return serverType switch
        {
            RustServerType.Official => 0,
            RustServerType.Community => 1,
            RustServerType.Modded => 2,
            _ => 3
        };
    }

    private int? GetRank(Guid serverId)
    {
        return Rankings.FirstOrDefault(ranking => ranking.ServerId == serverId)?.Rank;
    }

    private bool IsPopular(
        Server server,
        int popularRankThreshold,
        int popularPlayerThreshold,
        int popularPeakPlayerThreshold,
        DateTimeOffset recentPeakSinceUtc)
    {
        return server.Players >= popularPlayerThreshold
               || GetRank(server.Id) <= popularRankThreshold
               || HasPeakSince(server.Id, popularPeakPlayerThreshold, recentPeakSinceUtc);
    }

    private bool IsRecentlyPopularOffline(
        Server server,
        int popularRankThreshold,
        int popularPeakPlayerThreshold,
        DateTimeOffset popularOfflineGraceSinceUtc)
    {
        return server.Status != ServerStatus.Online
               && server.LastSeenAtUtc is not null
               && server.LastSeenAtUtc >= popularOfflineGraceSinceUtc
               && (GetRank(server.Id) <= popularRankThreshold
                   || HasPeakSince(server.Id, popularPeakPlayerThreshold, popularOfflineGraceSinceUtc));
    }

    private bool HasPeakSince(Guid serverId, int popularPeakPlayerThreshold, DateTimeOffset sinceUtc)
    {
        return Snapshots.Any(snapshot =>
            snapshot.ServerId == serverId
            && snapshot.CheckedAtUtc >= sinceUtc
            && snapshot.Players >= popularPeakPlayerThreshold);
    }
}
