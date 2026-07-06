namespace RustIQ.Application.ServerQuery;

public sealed record RustServerPlayerResult(
    string? Name,
    int Score,
    double DurationSeconds);
