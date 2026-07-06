namespace RustIQ.Application.Servers;

public sealed record ServerMapMetadataDto(
    string? MapName,
    int? Seed,
    int? Size,
    string? RustMapsUrl,
    string? ThumbnailUrl,
    string? MapFileUrl,
    string Source,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset FetchedAtUtc);
