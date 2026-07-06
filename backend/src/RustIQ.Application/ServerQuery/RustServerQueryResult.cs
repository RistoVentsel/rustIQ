namespace RustIQ.Application.ServerQuery;

public sealed record RustServerQueryResult(
    string Host,
    int QueryPort,
    string? Name,
    string? Map,
    int Players,
    int MaxPlayers,
    bool IsOnline,
    long? PingMilliseconds,
    DateTimeOffset CheckedAtUtc,
    string? Error);
