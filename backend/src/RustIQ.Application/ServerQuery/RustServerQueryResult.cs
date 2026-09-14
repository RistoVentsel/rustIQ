using RustIQ.Domain.Servers;

namespace RustIQ.Application.ServerQuery;

public sealed record RustServerQueryResult(
    long? BattleMetricsServerId,
    string? Address,
    string Host,
    int? GamePort,
    int QueryPort,
    string? Country,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    string? Name,
    string? Map,
    int Players,
    int MaxPlayers,
    int? QueuePlayers,
    bool IsOnline,
    long? PingMilliseconds,
    DateTimeOffset CheckedAtUtc,
    ServerPlayerCountSource PlayerCountSource,
    ServerSummarySource SummarySource,
    RustServerType ServerType,
    string? Error);
