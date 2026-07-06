namespace RustIQ.Application.Servers;

public sealed record ServerSnapshotDto(
    DateTimeOffset CheckedAtUtc,
    int Players,
    int MaxPlayers,
    bool IsOnline,
    long? PingMilliseconds);
