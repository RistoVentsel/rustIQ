namespace RustIQ.Infrastructure.ServerQuery;

public interface IBattleMetricsPublicPageFetcher
{
    Task<string?> FetchRustSettingsAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken);

    Task<string?> SearchRustServersAsync(
        string query,
        CancellationToken cancellationToken);
}
