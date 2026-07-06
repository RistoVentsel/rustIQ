using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public interface IServerRepository
{
    Task<int> CountAsync(
        string? search,
        ServerStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Server>> GetPagedAsync(
        string? search,
        ServerStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<Server?> GetByIdWithMapMetadataAsync(
        Guid serverId,
        CancellationToken cancellationToken);

    Task<Server?> GetByHostAndQueryPortAsync(
        string host,
        int queryPort,
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

    void Add(Server server);

    void AddSnapshot(ServerSnapshot snapshot);

    Task ReplaceCurrentPlayersAsync(
        Guid serverId,
        IReadOnlyList<ServerPlayerCurrent> currentPlayers,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
