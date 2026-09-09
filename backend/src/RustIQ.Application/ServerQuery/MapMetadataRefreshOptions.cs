namespace RustIQ.Application.ServerQuery;

public sealed class MapMetadataRefreshOptions
{
    public const string SectionName = "MapMetadataRefresh";

    public bool Enabled { get; init; }

    public bool RunOnStartup { get; init; } = true;

    public int StartupStaleAfterHours { get; init; } = 8;

    public string TimeZoneId { get; init; } = "Europe/Tallinn";

    public int[] LocalHours { get; init; } = [];

    // The selection is bounded so one run cannot pull the whole server table into memory. Discovery
    // registers far more servers than a scheduled run could ever query, and an unbounded run would
    // never finish a pass - the leftovers are picked up by the next run.
    public int MaxServersPerRun { get; init; } = 1000;

    public int BatchSize { get; init; } = 25;

    public int DelayBetweenServersMilliseconds { get; init; }

    public int DelayBetweenBatchesSeconds { get; init; } = 1;
}
