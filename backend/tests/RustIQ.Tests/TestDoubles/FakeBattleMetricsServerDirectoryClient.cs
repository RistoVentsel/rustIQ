using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeBattleMetricsServerDirectoryClient : IBattleMetricsServerDirectoryClient
{
    private readonly IReadOnlyList<BattleMetricsServerSummary> _summaries;
    private readonly Exception? _exception;

    public FakeBattleMetricsServerDirectoryClient(
        IReadOnlyList<BattleMetricsServerSummary>? summaries = null,
        Exception? exception = null)
    {
        _summaries = summaries ?? [];
        _exception = exception;
    }

    public Task<IReadOnlyList<ConfiguredRustServer>> GetRustServersAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfiguredRustServer> configuredServers = _summaries
            .Select(summary => new ConfiguredRustServer
            {
                BattleMetricsServerId = summary.BattleMetricsServerId,
                Name = summary.Name,
                Address = summary.Address,
                Host = summary.Host,
                GamePort = summary.GamePort,
                QueryPort = summary.QueryPort,
                Country = summary.Country,
                Latitude = summary.Latitude,
                Longitude = summary.Longitude,
                LastWipeAtUtc = summary.LastWipeAtUtc,
                NextWipeAtUtc = summary.NextWipeAtUtc
            })
            .ToArray();

        return Task.FromResult(configuredServers);
    }

    public Task<IReadOnlyList<BattleMetricsServerSummary>> GetRustServerSummariesAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken)
    {
        if (_exception is not null)
        {
            return Task.FromException<IReadOnlyList<BattleMetricsServerSummary>>(_exception);
        }

        return Task.FromResult(_summaries);
    }
}
