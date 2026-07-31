namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsWipeFallbackOptions
{
    public const string SectionName = "BattleMetricsWipeFallback";

    public bool Enabled { get; init; } = true;

    public int RequestTimeoutSeconds { get; init; } = 20;
}
