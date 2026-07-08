namespace RustIQ.Application.Servers;

public sealed record ServerSnapshotTrend(
    Guid ServerId,
    int? PlayersDeltaLastHour,
    int? PlayersPeakLastHour);
