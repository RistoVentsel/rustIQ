using RustIQ.Domain.Servers;

namespace RustIQ.Application.Realtime;

public sealed record ServerUpdateMessage(
    Guid ServerId,
    int Players,
    int MaxPlayers,
    int? QueuePlayers,
    ServerStatus Status,
    DateTimeOffset CheckedAtUtc,
    int UpdatedSecondsAgo,
    string? MapName,
    int? Seed,
    int? Size,
    string? RustMapsUrl,
    string? ThumbnailUrl);
