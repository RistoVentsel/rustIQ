using RustIQ.Domain.Servers;

namespace RustIQ.Application.ServerQuery;

public sealed record RustServerQueryResult(
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
    bool IsOnline,
    long? PingMilliseconds,
    DateTimeOffset CheckedAtUtc,
    ServerPlayerCountSource PlayerCountSource,
    ServerSummarySource SummarySource,
    string? Error);
