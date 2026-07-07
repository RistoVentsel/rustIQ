using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public sealed record ServerListItemDto(
    Guid Id,
    long? BattleMetricsServerId,
    string? Address,
    string Host,
    int? GamePort,
    int QueryPort,
    string? Country,
    string? Name,
    string? Map,
    int Players,
    int MaxPlayers,
    ServerStatus Status,
    ServerDiscoverySource DiscoverySource,
    ServerSummarySource SummarySource,
    ServerPlayerCountSource PlayerCountSource,
    ServerDataQuality DataQuality,
    string? LastValidationReason,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastDiscoveredAtUtc,
    DateTimeOffset? LastPlayerCountUpdatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int UpdatedSecondsAgo);
