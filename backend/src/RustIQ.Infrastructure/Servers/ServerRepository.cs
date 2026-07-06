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
        return ApplyServerFilters(dbContext.Servers.AsNoTracking(), search, status)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await ApplyServerFilters(dbContext.Servers.AsNoTracking(), search, status)
            .OrderByDescending(server => server.Status == ServerStatus.Online)
            .ThenByDescending(server => server.Players)
            .ThenBy(server => server.Name)
            .ThenBy(server => server.Host)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
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
}
