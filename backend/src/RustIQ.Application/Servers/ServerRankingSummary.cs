namespace RustIQ.Application.Servers;

public sealed record ServerRankingSummary(
    Guid ServerId,
    int Rank,
    double AveragePlayers,
    int PeakPlayers,
    int SnapshotCount,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    DateTimeOffset GeneratedAtUtc);
