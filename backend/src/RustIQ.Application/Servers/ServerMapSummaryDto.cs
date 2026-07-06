namespace RustIQ.Application.Servers;

public sealed record ServerMapSummaryDto(
    string? MapName,
    int? Seed,
    int? Size,
    string? RustMapsUrl,
    string? ThumbnailUrl);
