namespace RustIQ.Application.Servers;

public sealed record CurrentPlayerDto(
    string? PlayerName,
    double DurationSeconds,
    DateTimeOffset SeenAtUtc);
