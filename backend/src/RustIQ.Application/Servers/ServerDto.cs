using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed record ServerDto(
    Guid Id,
    string Host,
    int QueryPort,
    string? Name,
    string? Map,
    int Players,
    int MaxPlayers,
    ServerStatus Status,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ServerMapMetadataDto? MapMetadata,
    ServerSnapshotDto LatestSnapshot,
    IReadOnlyList<CurrentPlayerDto> CurrentPlayers);
