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
        return Task.FromResult(ApplyServerFilters(search, status).Count());
    }

    public Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        LastPagedTake = take;
        var servers = ApplyServerFilters(search, status)
            .OrderByDescending(server => server.Status == ServerStatus.Online)
            .ThenByDescending(server => server.Players)
            .ThenBy(server => server.Name)
            .ThenBy(server => server.Host)
            .Skip(skip)
            .Take(take)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Server>>(servers);
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
}
