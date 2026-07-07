using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeBattleMetricsPlayerCountClient : IBattleMetricsPlayerCountClient
{
    private readonly Dictionary<(string Host, int QueryPort), long?> _idsByEndpoint;
    private readonly Dictionary<long, BattleMetricsPlayerCountResult?> _playerCountsById;
    public int FindServerIdCallCount { get; private set; }
    public int GetPlayerCountCallCount { get; private set; }

    public FakeBattleMetricsPlayerCountClient()
        : this(
            new Dictionary<(string Host, int QueryPort), long?>(),
            new Dictionary<long, BattleMetricsPlayerCountResult?>())
    {
    }

    public FakeBattleMetricsPlayerCountClient(
        IReadOnlyDictionary<(string Host, int QueryPort), long?> idsByEndpoint,
        IReadOnlyDictionary<long, BattleMetricsPlayerCountResult?> playerCountsById)
    {
        _idsByEndpoint = new Dictionary<(string Host, int QueryPort), long?>(idsByEndpoint);
        _playerCountsById = new Dictionary<long, BattleMetricsPlayerCountResult?>(playerCountsById);
    }

    public Task<long?> FindServerIdAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken)
    {
        FindServerIdCallCount++;

        return Task.FromResult(
            _idsByEndpoint.TryGetValue((host, queryPort), out var id)
                ? id
                : null);
    }

    public Task<BattleMetricsPlayerCountResult?> GetPlayerCountAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        GetPlayerCountCallCount++;

        return Task.FromResult(
            _playerCountsById.TryGetValue(battleMetricsServerId, out var result)
                ? result
                : null);
    }
}
