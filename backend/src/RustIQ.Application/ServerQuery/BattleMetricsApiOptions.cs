namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsApiOptions
{
    public const string SectionName = "BattleMetricsApi";

    public bool Enabled { get; init; }

    // Optional. BattleMetrics serves the public server list without credentials, but rate limits
    // anonymous callers hard; a free personal token moves the same requests onto the caller's own
    // budget. Left empty, requests go out unauthenticated exactly as before.
    public string? Token { get; init; }

    public int MinimumRequestIntervalMilliseconds { get; init; } = 1100;
}
