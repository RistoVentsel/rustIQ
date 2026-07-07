namespace RustIQ.Application.ServerQuery;

public interface IBattleMetricsServerDirectoryClient
{
    Task<IReadOnlyList<ConfiguredRustServer>> GetRustServersAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BattleMetricsServerSummary>> GetRustServerSummariesAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken);
}
