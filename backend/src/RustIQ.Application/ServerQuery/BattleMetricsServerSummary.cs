namespace RustIQ.Application.ServerQuery;

public sealed record BattleMetricsServerSummary(
    long BattleMetricsServerId,
    string? Name,
    string? Address,
    string Host,
    int? GamePort,
    int QueryPort,
    string? Country,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    int Players,
    int MaxPlayers,
    int? QueuePlayers,
    bool IsOnline,
    DateTimeOffset CheckedAtUtc);
