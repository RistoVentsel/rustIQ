namespace RustIQ.Application.ServerQuery;

public sealed record BattleMetricsPlayerCountResult(
    long BattleMetricsServerId,
    int Players,
    int MaxPlayers,
    bool IsOnline,
    DateTimeOffset CheckedAtUtc);
