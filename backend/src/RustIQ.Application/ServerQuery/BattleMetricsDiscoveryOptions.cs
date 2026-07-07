namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsDiscoveryOptions
{
    public const string SectionName = "BattleMetricsDiscovery";

    public bool Enabled { get; init; }

    public int PageSize { get; init; } = 100;

    public int MaxPages { get; init; } = 1;

    public int RefreshIntervalMinutes { get; init; } = 15;
}
