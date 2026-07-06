using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed record ServerListItemDto(
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
    DateTimeOffset UpdatedAtUtc,
    int UpdatedSecondsAgo);
