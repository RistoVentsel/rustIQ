namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsApiOptions
{
    public const string SectionName = "BattleMetricsApi";

    public bool Enabled { get; init; }

    public int MinimumRequestIntervalMilliseconds { get; init; } = 1100;
}
