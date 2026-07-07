namespace RustIQ.Application.ServerQuery;

public sealed class BattleMetricsRateLimitException(
    IReadOnlyList<BattleMetricsServerSummary> partialSummaries,
    string? retryAfter) : Exception("BattleMetrics rate limit reached.")
{
    public IReadOnlyList<BattleMetricsServerSummary> PartialSummaries { get; } = partialSummaries;

    public string? RetryAfter { get; } = retryAfter;
}
