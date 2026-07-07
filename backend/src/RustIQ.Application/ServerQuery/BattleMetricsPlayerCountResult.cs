namespace RustIQ.Application.ServerQuery;

public sealed record BattleMetricsPlayerCountResult(
    long BattleMetricsServerId,
    int Players,
    int MaxPlayers,
    bool IsOnline,
    string? Country,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    DateTimeOffset CheckedAtUtc);
