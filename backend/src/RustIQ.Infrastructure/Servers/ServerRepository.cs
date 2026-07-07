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
        CancellationToken cancellationToken)
    {
        return CountDeduplicatedAsync(search, status, cancellationToken);
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
        string sortBy,
        bool sortDescending,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var servers = await ApplyServerFilters(dbContext.Servers.AsNoTracking(), search, status)
            .ToListAsync(cancellationToken);

        return ApplyServerSort(DeduplicateServers(servers), sortBy, sortDescending)
            .Skip(skip)
            .Take(take)
            .ToArray();
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

    public async Task<IReadOnlyList<Server>> GetServersForPlayerCountRefreshAsync(
        ServerPlayerCountSource playerCountSource,
        DateTimeOffset staleBeforeUtc,
        int take,
        CancellationToken cancellationToken)
    {
        return await dbContext.Servers
            .AsNoTracking()
            .Where(server =>
                server.PlayerCountSource == playerCountSource
                && server.BattleMetricsServerId != null
                && ((server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc) <= staleBeforeUtc))
            .OrderBy(server => server.LastPlayerCountUpdatedAtUtc ?? server.UpdatedAtUtc)
            .ThenByDescending(server => server.Players)
            .Take(take)
            .ToListAsync(cancellationToken);
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
            .OrderBy(player => player.PlayerName)
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
        ServerStatus? status)
    {
        if (status is not null)
        {
            query = query.Where(server => server.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(server =>
                EF.Functions.ILike(server.Host, pattern)
                || (server.Name != null && EF.Functions.ILike(server.Name, pattern))
                || (server.Map != null && EF.Functions.ILike(server.Map, pattern)));
        }

        return query;
    }

    private async Task<int> CountDeduplicatedAsync(
        string? search,
        ServerStatus? status,
        CancellationToken cancellationToken)
    {
        var servers = await ApplyServerFilters(dbContext.Servers.AsNoTracking(), search, status)
            .ToListAsync(cancellationToken);

        return DeduplicateServers(servers).Count();
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
            ServerReadService.SortByNextWipe => sortDescending
                ? servers.OrderByDescending(server => server.NextWipeAtUtc ?? DateTimeOffset.MinValue)
                : servers.OrderBy(server => server.NextWipeAtUtc ?? DateTimeOffset.MaxValue),
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
