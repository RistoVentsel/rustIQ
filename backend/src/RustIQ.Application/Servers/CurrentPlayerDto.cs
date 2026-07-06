namespace RustIQ.Application.Servers;

public sealed record CurrentPlayerDto(
    string? PlayerName,
    int Score,
    double DurationSeconds,
    DateTimeOffset SeenAtUtc);
