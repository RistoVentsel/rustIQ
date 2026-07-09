using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public interface IServerRepository
{
    Task<int> CountAsync(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        IReadOnlySet<RustServerType> serverTypes,
        string wipeFilter,
        DateTimeOffset now,
        string sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ServerSnapshotTrend>> GetSnapshotTrendsAsync(
        IReadOnlyList<Guid> serverIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ServerRankingSummary>> GetLatestRankingsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken);

    Task<int> ReplaceLatestServerRankingsAsync(
        DateTimeOffset now,
        int maxRankedServers,
        int rankingWindowDays,
        int minimumSnapshots,
        int recentOnlineWindowHours,
        int minimumMaxPlayers,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetTrackedByIdsAsync(
        IReadOnlyList<Guid> serverIds,
        CancellationToken cancellationToken);

    Task<Server?> GetByIdWithMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken);

    Task<Server?> GetByHostAndQueryPortAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken);

    Task<Server?> GetByBattleMetricsServerIdAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetServersByPlayerCountSourceAsync(
        ServerPlayerCountSource playerCountSource,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetServersForPlayerCountRefreshAsync(
        ServerPlayerCountSource playerCountSource,
        DateTimeOffset staleBeforeUtc,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ServerSnapshot>> GetSnapshotsAsync(
        Guid serverId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ServerPlayerCurrent>> GetCurrentPlayersAsync(
        Guid serverId,
        CancellationToken cancellationToken);

    Task<ServerMapMetadata?> GetMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken);

    Task<int> DeleteSnapshotsOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken);

    void Add(Server server);

    void AddSnapshot(ServerSnapshot snapshot);

    Task ReplaceCurrentPlayersAsync(
        Guid serverId,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
