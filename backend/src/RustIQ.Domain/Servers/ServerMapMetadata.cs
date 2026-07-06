namespace RustIQ.Domain.Servers;

public sealed class ServerMapMetadata
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public string? MapName { get; set; }

    public int? Seed { get; set; }

    public int? Size { get; set; }

    public string? RustMapsUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? MapFileUrl { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }

    public DateTimeOffset FetchedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Server? Server { get; set; }
}
