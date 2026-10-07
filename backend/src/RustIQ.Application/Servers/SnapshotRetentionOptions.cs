namespace RustIQ.Application.Servers;

public sealed class SnapshotRetentionOptions
{
    public const string SectionName = "SnapshotRetention";

    public bool Enabled { get; init; } = true;

    public int RetentionDays { get; init; } = 14;

    public int CleanupIntervalHours { get; init; } = 12;

    // Endpoints RustIQ has never once reached are dropped once discovery has stopped listing them for
    // this long, which is what stops discovery filling the table with dead endpoints that consume
    // refresh lane capacity forever.
    public int UnreachableServerRetentionDays { get; init; } = 3;

    // Servers RustIQ has reached before are dropped, history included, once they have neither
    // answered a query nor been listed by discovery for this long. A server that is still up answers
    // queries, so it can never match no matter which servers discovery happens to return.
    public int OfflineServerRetentionDays { get; init; } = 7;

    // Bounded so one run cannot turn into a single enormous cascading delete. Applies to each of the
    // two prune rules separately.
    public int MaxUnreachableServerDeletesPerRun { get; init; } = 5000;
}
