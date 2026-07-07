namespace RustIQ.Application.Servers;

public sealed class SnapshotRetentionOptions
{
    public const string SectionName = "SnapshotRetention";

    public bool Enabled { get; init; } = true;

    public int RetentionDays { get; init; } = 14;

    public int CleanupIntervalHours { get; init; } = 12;
}
