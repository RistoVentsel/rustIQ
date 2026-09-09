namespace RustIQ.Application.Servers;

public sealed class SnapshotRetentionOptions
{
    public const string SectionName = "SnapshotRetention";

    public bool Enabled { get; init; } = true;

    public int RetentionDays { get; init; } = 14;

    public int CleanupIntervalHours { get; init; } = 12;

    // Endpoints RustIQ has known about for this long and has never once reached are dropped. A server
    // that was online and went down keeps its row and its history - only rows with no successful
    // contact at all are eligible, which is what stops discovery filling the table with dead
    // endpoints that consume refresh lane capacity forever.
    public int UnreachableServerRetentionDays { get; init; } = 30;

    // Bounded so one run cannot turn into a single enormous cascading delete.
    public int MaxUnreachableServerDeletesPerRun { get; init; } = 5000;
}
