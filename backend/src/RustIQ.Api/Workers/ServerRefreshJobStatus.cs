namespace RustIQ.Api.Workers;

public sealed record ServerRefreshJobStatus(
    bool IsQueuedOrRunning,
    bool IsRunning,
    DateTimeOffset? QueuedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? FailedAtUtc,
    string? LastErrorMessage);
