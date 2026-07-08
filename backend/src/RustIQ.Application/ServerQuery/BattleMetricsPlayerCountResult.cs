using RustIQ.Domain.Servers;

namespace RustIQ.Application.ServerQuery;

public sealed record BattleMetricsPlayerCountResult(
    long BattleMetricsServerId,
    int Players,
    int MaxPlayers,
    int? QueuePlayers,
    RustServerType ServerType,
    bool IsOnline,
    string? Country,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    DateTimeOffset CheckedAtUtc);
