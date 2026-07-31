namespace RustIQ.Application.Maps;

public sealed record RustServerMapMetadata(
    string Host,
    int QueryPort,
    string? MapName,
    int? Seed,
    int? Size,
    string? RustMapsUrl,
    string? ThumbnailUrl,
    string? MapFileUrl,
    string Source,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset FetchedAtUtc,
    string? Error,
    DateTimeOffset? NextWipeAtUtc = null,
    DateTimeOffset? LastWipeAtUtc = null,
    long? BattleMetricsServerId = null);
