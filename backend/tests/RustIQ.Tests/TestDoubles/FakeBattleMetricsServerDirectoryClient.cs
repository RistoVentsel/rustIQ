using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeBattleMetricsServerDirectoryClient : IBattleMetricsServerDirectoryClient
{
    private readonly IReadOnlyList<BattleMetricsServerSummary> _summaries;

    public FakeBattleMetricsServerDirectoryClient(
        IReadOnlyList<BattleMetricsServerSummary>? summaries = null)
    {
        _summaries = summaries ?? [];
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
        return Task.FromResult(_summaries);
    }
}
