using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.TestDoubles;

internal sealed class InMemoryServerRepository : IServerRepository
{
    public List<Server> Servers { get; } = [];

    public List<ServerSnapshot> Snapshots { get; } = [];

    public List<ServerPlayerCurrent> CurrentPlayers { get; } = [];

    public int? LastPagedTake { get; private set; }

    public int? LastSnapshotLimit { get; private set; }

    public Task<int> CountAsync(
        string? search,
        ServerStatus? status,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(DeduplicateServers(ApplyServerFilters(search, status)).Count());
    }

    public Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        string sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        LastPagedTake = take;
        var servers = ApplyServerSort(DeduplicateServers(ApplyServerFilters(search, status)), sortBy, sortDescending)
            .Skip(skip)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Server>>(servers);
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
            .OrderBy(player => player.PlayerName)
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
        ServerStatus? status)
    {
        var query = Servers.AsEnumerable();

        if (status is not null)
        {
            query = query.Where(server => server.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(server =>
                server.Host.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (server.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (server.Map?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

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

    private static IOrderedEnumerable<Server> ApplyServerSort(
        IEnumerable<Server> servers,
        string sortBy,
        bool sortDescending)
    {
        var sorted = sortBy switch
        {
            ServerReadService.SortByLastWipe => sortDescending
                ? servers.OrderByDescending(server => server.LastWipeAtUtc ?? DateTimeOffset.MinValue)
                : servers.OrderBy(server => server.LastWipeAtUtc ?? DateTimeOffset.MaxValue),
            ServerReadService.SortByLocation => sortDescending
                ? servers.OrderByDescending(server => server.Country ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : servers.OrderBy(server => server.Country ?? string.Empty, StringComparer.OrdinalIgnoreCase),
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
