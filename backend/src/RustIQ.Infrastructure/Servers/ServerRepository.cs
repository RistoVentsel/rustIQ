using Microsoft.EntityFrameworkCore;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.Data;

namespace RustIQ.Infrastructure.Servers;

public sealed class ServerRepository(RustIqDbContext dbContext) : IServerRepository
{
    public Task<int> CountAsync(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ApplyServerFilters(dbContext.Servers.AsNoTracking(), search, status, serverTypes, wipeFilter, now)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var servers = await dbContext.Servers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return DeduplicateServers(servers);
    }

    public async Task<IReadOnlyList<Server>> GetTrackedByIdsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Servers
            .Where(server => serverIds.Contains(server.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetPagedAsync(
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
        var filteredServers = ApplyServerFilters(
            dbContext.Servers.AsNoTracking(),
            search,
            status,
            serverTypes,
            wipeFilter,
            now);

        return await ApplyServerSort(
                filteredServers,
                sortBy,
                sortDescending,
                now)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ServerSnapshotTrend>> GetSnapshotTrendsAsync(
        IReadOnlyList<Guid> serverIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        if (serverIds.Count == 0)
        {
            return new Dictionary<Guid, ServerSnapshotTrend>();
        }

        var snapshots = await dbContext.ServerSnapshots
            .AsNoTracking()
            .Where(snapshot => serverIds.Contains(snapshot.ServerId) && snapshot.CheckedAtUtc >= sinceUtc)
            .OrderBy(snapshot => snapshot.ServerId)
            .ThenBy(snapshot => snapshot.CheckedAtUtc)
            .ToListAsync(cancellationToken);

        return snapshots
            .GroupBy(snapshot => snapshot.ServerId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var orderedSnapshots = group.ToArray();
                    var first = orderedSnapshots[0];
                    var last = orderedSnapshots[^1];

                    return new ServerSnapshotTrend(
                        group.Key,
                        last.Players - first.Players,
                        orderedSnapshots.Max(snapshot => snapshot.Players));
                });
    }

    public async Task<IReadOnlyDictionary<Guid, ServerRankingSummary>> GetLatestRankingsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken)
    {
        if (serverIds.Count == 0)
        {
            return new Dictionary<Guid, ServerRankingSummary>();
        }

        return await dbContext.ServerRankings
            .AsNoTracking()
            .Where(ranking => serverIds.Contains(ranking.ServerId))
            .ToDictionaryAsync(
                ranking => ranking.ServerId,
                ranking => new ServerRankingSummary(
                    ranking.ServerId,
                    ranking.Rank,
                    ranking.AveragePlayers,
                    ranking.PeakPlayers,
                    ranking.SnapshotCount,
                    ranking.WindowStartUtc,
                    ranking.WindowEndUtc,
                    ranking.GeneratedAtUtc),
                cancellationToken);
    }

    public async Task<int> ReplaceLatestServerRankingsAsync(
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

        var preferredServerIds = dbContext.Servers
            .AsNoTracking()
            .Where(server =>
                server.MaxPlayers >= minimumMaxPlayers
                && server.Name != null
                && server.Name != string.Empty
                && (server.LastSeenAtUtc != null && server.LastSeenAtUtc >= recentOnlineCutoffUtc))
            .GroupBy(server => new
            {
                Host = server.Host.Trim().ToLower(),
                server.QueryPort
            })
            .Select(group => group
                .OrderByDescending(server => server.BattleMetricsServerId != null)
                .ThenByDescending(server => server.PlayerCountSource == ServerPlayerCountSource.BattleMetrics)
                .ThenByDescending(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
                .ThenBy(server => server.Id)
                .Select(server => server.Id)
                .First());

        var rankedServers = await dbContext.ServerSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                preferredServerIds.Contains(snapshot.ServerId)
                && snapshot.CheckedAtUtc >= windowStartUtc
                && snapshot.CheckedAtUtc <= now)
            .GroupBy(snapshot => snapshot.ServerId)
            .Select(group => new
            {
                ServerId = group.Key,
                AveragePlayers = group.Average(snapshot => (double)snapshot.Players),
                PeakPlayers = group.Max(snapshot => snapshot.Players),
                SnapshotCount = group.Count()
            })
            .Where(server => server.SnapshotCount >= minimumSnapshots)
            .OrderByDescending(server => server.AveragePlayers)
            .ThenByDescending(server => server.PeakPlayers)
            .ThenBy(server => server.ServerId)
            .Take(maxRankedServers)
            .ToListAsync(cancellationToken);

        var nextRankings = rankedServers
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

        // A transient ingestion outage must not erase the last usable ranking set.
        if (nextRankings.Length == 0)
        {
            return 0;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.ServerRankings.ExecuteDeleteAsync(cancellationToken);

        if (nextRankings.Length > 0)
        {
            dbContext.ServerRankings.AddRange(nextRankings);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return nextRankings.Length;
    }

    public Task<Server?> GetByIdWithMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return dbContext.Servers
            .AsNoTracking()
            .Include(server => server.MapMetadata)
            .FirstOrDefaultAsync(server => server.Id == serverId, cancellationToken);
    }

    public Task<Server?> GetByHostAndQueryPortAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken)
    {
        return dbContext.Servers
            .Include(server => server.MapMetadata)
            .FirstOrDefaultAsync(
                server => server.Host == host && server.QueryPort == queryPort,
                cancellationToken);
    }

    public Task<Server?> GetByBattleMetricsServerIdAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        return dbContext.Servers
            .Include(server => server.MapMetadata)
            .FirstOrDefaultAsync(
                server => server.BattleMetricsServerId == battleMetricsServerId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetServersByPlayerCountSourceAsync(
        ServerPlayerCountSource playerCountSource,
        int take,
        CancellationToken cancellationToken)
    {
        return await dbContext.Servers
            .AsNoTracking()
            .Where(server => server.PlayerCountSource == playerCountSource)
            .OrderByDescending(server => server.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetServersForRefreshLaneAsync(
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

        // Eligibility is deliberately based on having a queryable endpoint, not on having a
        // BattleMetricsServerId: the lanes poll servers over A2S via Host:QueryPort, and most servers
        // (everything Steam discovery finds) never get a BattleMetrics id at all. Gating on that id
        // silently froze those servers on whatever status they were last given.
        var query = dbContext.Servers
            .AsNoTracking()
            .Where(server =>
                server.Host != string.Empty
                && server.QueryPort > 0
                && ((server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc) <= dueBeforeUtc));

        if (refreshLane == ServerRefreshLane.Fast)
        {
            // Official servers are always eligible for the fast lane, but they must never be able to
            // crowd out servers that are popular right now: a stuck/never-succeeding official server would
            // otherwise permanently occupy a batch slot (it stays "due" every tick) and starve real traffic.
            // Popular servers are selected first up to `take`; officials only fill any capacity left over.
            var popularCandidates = await query
                .Where(server =>
                    server.Players >= popularPlayerThreshold
                    || (server.Status == ServerStatus.Online && server.QueuePlayers > 0)
                    || dbContext.ServerRankings.Any(ranking =>
                        ranking.ServerId == server.Id && ranking.Rank <= popularRankThreshold)
                    || dbContext.ServerSnapshots.Any(snapshot =>
                        snapshot.ServerId == server.Id
                        && snapshot.CheckedAtUtc >= recentPeakSinceUtc
                        && snapshot.Players >= popularPeakPlayerThreshold)
                    || (server.Status != ServerStatus.Online
                        && server.LastSeenAtUtc != null
                        && server.LastSeenAtUtc >= popularOfflineGraceSinceUtc
                        && (dbContext.ServerRankings.Any(ranking =>
                                ranking.ServerId == server.Id && ranking.Rank <= popularRankThreshold)
                            || dbContext.ServerSnapshots.Any(snapshot =>
                                snapshot.ServerId == server.Id
                                && snapshot.CheckedAtUtc >= popularOfflineGraceSinceUtc
                                && snapshot.Players >= popularPeakPlayerThreshold))))
                .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
                .ThenByDescending(server => server.Players)
                .Take(take)
                .ToListAsync(cancellationToken);

            var remaining = take - popularCandidates.Count;
            if (remaining <= 0)
            {
                return popularCandidates;
            }

            var popularCandidateIds = popularCandidates.Select(server => server.Id).ToArray();
            var officialCandidates = await query
                .Where(server =>
                    server.ServerType == RustServerType.Official
                    && !popularCandidateIds.Contains(server.Id))
                .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
                .ThenByDescending(server => server.Players)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            return popularCandidates.Concat(officialCandidates).ToList();
        }

        query = refreshLane switch
        {
            ServerRefreshLane.NormalOnline => query.Where(server =>
                server.Status == ServerStatus.Online
                && server.Players < popularPlayerThreshold
                && (server.QueuePlayers == null || server.QueuePlayers <= 0)
                && !dbContext.ServerRankings.Any(ranking =>
                    ranking.ServerId == server.Id && ranking.Rank <= popularRankThreshold)
                && !dbContext.ServerSnapshots.Any(snapshot =>
                    snapshot.ServerId == server.Id
                    && snapshot.CheckedAtUtc >= recentPeakSinceUtc
                    && snapshot.Players >= popularPeakPlayerThreshold)),
            ServerRefreshLane.SlowOffline => query.Where(server =>
                server.Status != ServerStatus.Online
                && server.LastSeenAtUtc != null
                && server.LastSeenAtUtc < slowOfflineBeforeUtc
                && server.LastSeenAtUtc >= veryStaleOfflineBeforeUtc
                && !dbContext.ServerRankings.Any(ranking =>
                    ranking.ServerId == server.Id && ranking.Rank <= popularRankThreshold)
                && !dbContext.ServerSnapshots.Any(snapshot =>
                    snapshot.ServerId == server.Id
                    && snapshot.CheckedAtUtc >= recentPeakSinceUtc
                    && snapshot.Players >= popularPeakPlayerThreshold)),
            ServerRefreshLane.VeryStaleOffline => query.Where(server =>
                server.Status != ServerStatus.Online
                && (server.LastSeenAtUtc == null || server.LastSeenAtUtc < veryStaleOfflineBeforeUtc)
                && !dbContext.ServerRankings.Any(ranking =>
                    ranking.ServerId == server.Id && ranking.Rank <= popularRankThreshold)
                && !dbContext.ServerSnapshots.Any(snapshot =>
                    snapshot.ServerId == server.Id
                    && snapshot.CheckedAtUtc >= recentPeakSinceUtc
                    && snapshot.Players >= popularPeakPlayerThreshold)),
            _ => query.Where(_ => false)
        };

        return await query
            .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
            .ThenByDescending(server => server.Players)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<MapMetadataRefreshBatch> GetServersForMapMetadataRefreshAsync(
        DateTimeOffset fetchedBeforeUtc,
        int take,
        int priorityTake,
        Guid? rotationCursor,
        CancellationToken cancellationToken)
    {
        var candidates = dbContext.Servers
            .AsNoTracking()
            .Where(server =>
                server.NextWipeAtUtc == null
                    || server.MapMetadata == null
                    || server.MapMetadata.FetchedAtUtc < fetchedBeforeUtc);

        // Ordered by how alive and busy a server is, never by whether it is missing data. Leading with
        // "no wipe data first" put the servers that never answer A2S_RULES at the front of every run -
        // nothing is recorded when a query fails, so they stayed missing, stayed candidates, and stayed
        // first. Harmless while the run was unbounded and it eventually walked past them; once the run
        // was capped it meant the same dead ends every time and nothing else ever reached.
        var priorityServers = await candidates
            .Where(server => server.Status == ServerStatus.Online)
            .OrderByDescending(server => server.Players)
            .ThenBy(server => server.Id)
            .Take(Math.Clamp(priorityTake, 0, take))
            .ToListAsync(cancellationToken);

        var remaining = take - priorityServers.Count;
        if (remaining <= 0)
        {
            return new MapMetadataRefreshBatch(priorityServers, rotationCursor);
        }

        var priorityIds = priorityServers
            .Select(server => server.Id)
            .ToArray();
        var rotationServers = await TakeRotationSliceAsync(
            candidates,
            priorityIds,
            remaining,
            rotationCursor,
            cancellationToken);

        var servers = priorityServers
            .Concat(rotationServers)
            .ToArray();
        var nextRotationCursor = rotationServers.Count > 0
            ? rotationServers[^1].Id
            : rotationCursor;

        return new MapMetadataRefreshBatch(servers, nextRotationCursor);
    }

    /// <summary>
    /// A keyset walk over the candidates in id order that resumes where the previous run stopped and
    /// wraps once it runs off the end, so every candidate is reached eventually no matter how many of
    /// them never answer.
    /// </summary>
    private static async Task<IReadOnlyList<Server>> TakeRotationSliceAsync(
        IQueryable<Server> candidates,
        Guid[] excludedServerIds,
        int take,
        Guid? rotationCursor,
        CancellationToken cancellationToken)
    {
        var remainingCandidates = candidates
            .Where(server => !excludedServerIds.Contains(server.Id));

        var afterCursor = remainingCandidates;
        if (rotationCursor is Guid cursor)
        {
            afterCursor = afterCursor.Where(server => server.Id > cursor);
        }

        var slice = await afterCursor
            .OrderBy(server => server.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (slice.Count >= take || rotationCursor is not Guid wrapBefore)
        {
            return slice;
        }

        var wrapped = await remainingCandidates
            .Where(server => server.Id <= wrapBefore)
            .OrderBy(server => server.Id)
            .Take(take - slice.Count)
            .ToListAsync(cancellationToken);

        slice.AddRange(wrapped);
        return slice;
    }

    public async Task<IReadOnlyList<ServerSnapshot>> GetSnapshotsAsync(
        Guid serverId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ServerSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ServerId == serverId);

        if (from is not null)
        {
            query = query.Where(snapshot => snapshot.CheckedAtUtc >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(snapshot => snapshot.CheckedAtUtc <= to.Value);
        }

        return await query
            .OrderByDescending(snapshot => snapshot.CheckedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServerPlayerCurrent>> GetCurrentPlayersAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ServerPlayersCurrent
            .AsNoTracking()
            .Where(player => player.ServerId == serverId)
            .OrderByDescending(player => player.DurationSeconds)
            .ThenBy(player => player.PlayerName)
            .ToListAsync(cancellationToken);
    }

    public Task<ServerMapMetadata?> GetMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        return dbContext.ServerMapMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(metadata => metadata.ServerId == serverId, cancellationToken);
    }

    public async Task<int> DeleteUnreachableServersOlderThanAsync(
        DateTimeOffset createdBeforeUtc,
        int maxServers,
        CancellationToken cancellationToken)
    {
        // LastSeenAtUtc is only ever set by a successful query, so a null one means RustIQ has never
        // reached this endpoint. Servers that were online and went down keep their row and history.
        var serverIds = await dbContext.Servers
            .AsNoTracking()
            .Where(server =>
                server.LastSeenAtUtc == null
                && server.CreatedAtUtc <= createdBeforeUtc)
            .OrderBy(server => server.CreatedAtUtc)
            .Select(server => server.Id)
            .Take(maxServers)
            .ToListAsync(cancellationToken);

        if (serverIds.Count == 0)
        {
            return 0;
        }

        // Snapshots, rankings, current players and map metadata all cascade in the database, which
        // ExecuteDelete relies on because it bypasses the change tracker.
        return await dbContext.Servers
            .Where(server => serverIds.Contains(server.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteSnapshotsOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        return await dbContext.ServerSnapshots
            .Where(snapshot => snapshot.CheckedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void Add(Server server)
    {
        dbContext.Servers.Add(server);
    }

    public void AddSnapshot(ServerSnapshot snapshot)
    {
        dbContext.ServerSnapshots.Add(snapshot);
    }

    public async Task ReplaceCurrentPlayersAsync(
        Guid serverId,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers,
        CancellationToken cancellationToken)
    {
        var existingPlayers = await dbContext.ServerPlayersCurrent
            .Where(player => player.ServerId == serverId)
            .ToListAsync(cancellationToken);

        dbContext.ServerPlayersCurrent.RemoveRange(existingPlayers);

        if (currentPlayers.Count > 0)
        {
            dbContext.ServerPlayersCurrent.AddRange(currentPlayers);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Server> ApplyServerFilters(
        IQueryable<Server> query,
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now)
    {
        if (status is not null)
        {
            query = query.Where(server => server.Status == status);
        }
        else
        {
            // A server RustIQ has never reached has no status to report, so it stays out of the
            // browsable list until a poll gives it one. Discovery registers far more servers than the
            // refresh lanes can check straight away, and listing those alongside verified servers
            // would state something RustIQ does not know. Requesting Unknown explicitly still
            // returns them, so the unverified backlog stays inspectable.
            query = query.Where(server => server.Status != ServerStatus.Unknown);
        }

        if (serverTypes.Count > 0)
        {
            query = query.Where(server => serverTypes.Contains(server.ServerType));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(server =>
                EF.Functions.ILike(server.Host, pattern)
                || (server.Name != null && EF.Functions.ILike(server.Name, pattern))
                || (server.Map != null && EF.Functions.ILike(server.Map, pattern)));
        }

        query = wipeFilter switch
        {
            ServerReadService.WipeFilterNext24Hours => query.Where(server =>
                server.NextWipeAtUtc != null
                && server.NextWipeAtUtc >= now
                && server.NextWipeAtUtc <= now.AddHours(24)),
            ServerReadService.WipeFilterNext7Days => query.Where(server =>
                server.NextWipeAtUtc != null
                && server.NextWipeAtUtc >= now
                && server.NextWipeAtUtc <= now.AddDays(7)),
            ServerReadService.WipeFilterWiped24Hours => query.Where(server =>
                server.LastWipeAtUtc != null
                && server.LastWipeAtUtc >= now.AddHours(-24)
                && server.LastWipeAtUtc <= now),
            ServerReadService.WipeFilterHasWipeData => query.Where(server =>
                server.LastWipeAtUtc != null || server.NextWipeAtUtc != null),
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

    private IOrderedQueryable<Server> ApplyServerSort(
        IQueryable<Server> servers,
        string sortBy,
        bool sortDescending,
        DateTimeOffset now)
    {
        var sorted = sortBy switch
        {
            ServerReadService.SortByRank => sortDescending
                ? servers
                    .OrderBy(server => dbContext.ServerRankings
                        .Where(ranking => ranking.ServerId == server.Id)
                        .Select(ranking => (int?)ranking.Rank)
                        .FirstOrDefault() == null)
                    .ThenByDescending(server => dbContext.ServerRankings
                        .Where(ranking => ranking.ServerId == server.Id)
                        .Select(ranking => (int?)ranking.Rank)
                        .FirstOrDefault())
                : servers
                    .OrderBy(server => dbContext.ServerRankings
                        .Where(ranking => ranking.ServerId == server.Id)
                        .Select(ranking => (int?)ranking.Rank)
                        .FirstOrDefault() == null)
                    .ThenBy(server => dbContext.ServerRankings
                        .Where(ranking => ranking.ServerId == server.Id)
                        .Select(ranking => (int?)ranking.Rank)
                        .FirstOrDefault()),
            ServerReadService.SortByLastWipe => sortDescending
                ? servers
                    .OrderBy(server => server.LastWipeAtUtc == null)
                    .ThenByDescending(server => server.LastWipeAtUtc)
                : servers
                    .OrderBy(server => server.LastWipeAtUtc == null)
                    .ThenBy(server => server.LastWipeAtUtc),
            ServerReadService.SortByNextWipe => sortDescending
                ? servers
                    .OrderBy(server => server.NextWipeAtUtc == null || server.NextWipeAtUtc < now)
                    .ThenByDescending(server => server.NextWipeAtUtc >= now ? server.NextWipeAtUtc : null)
                : servers
                    .OrderBy(server => server.NextWipeAtUtc == null || server.NextWipeAtUtc < now)
                    .ThenBy(server => server.NextWipeAtUtc >= now ? server.NextWipeAtUtc : null),
            ServerReadService.SortByLocation => sortDescending
                ? servers.OrderByDescending(server => server.Country ?? string.Empty)
                : servers.OrderBy(server => server.Country ?? string.Empty),
            ServerReadService.SortByServerType => sortDescending
                ? servers.OrderByDescending(server =>
                    server.ServerType == RustServerType.Official ? 0
                    : server.ServerType == RustServerType.Community ? 1
                    : server.ServerType == RustServerType.Modded ? 2
                    : 3)
                : servers.OrderBy(server =>
                    server.ServerType == RustServerType.Official ? 0
                    : server.ServerType == RustServerType.Community ? 1
                    : server.ServerType == RustServerType.Modded ? 2
                    : 3),
            _ => sortDescending
                ? servers.OrderByDescending(server => server.Players)
                : servers.OrderBy(server => server.Players)
        };

        return sorted
            .ThenByDescending(server => server.Status == ServerStatus.Online)
            .ThenBy(server => server.Name)
            .ThenBy(server => server.Host);
    }
}
