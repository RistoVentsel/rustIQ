using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeRustServerQueryClient : IRustServerQueryClient
{
    private readonly Dictionary<(string Host, int QueryPort), Queue<RustServerQueryResult>> _queryResultsByServer;
    private readonly Dictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>> _playersByServer;

    public FakeRustServerQueryClient(
        params RustServerQueryResult[] queryResults)
        : this(
            queryResults,
            new Dictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>>())
    {
    }

    public FakeRustServerQueryClient(
        RustServerQueryResult queryResult,
        IReadOnlyList<RustServerPlayerResult> players)
        : this([queryResult], new Dictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>>
        {
            [(queryResult.Host, queryResult.QueryPort)] = players
        })
    {
    }

    public FakeRustServerQueryClient(
        IReadOnlyList<RustServerQueryResult> queryResults,
        IReadOnlyDictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>> playersByServer)
    {
        _queryResultsByServer = queryResults
            .GroupBy(result => (result.Host, result.QueryPort))
            .ToDictionary(
                group => group.Key,
                group => new Queue<RustServerQueryResult>(group));
        _playersByServer = new Dictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>>(playersByServer);
    }

    public Task<RustServerQueryResult> QueryServerAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var key = (configuredServer.Host, configuredServer.QueryPort);

        if (!_queryResultsByServer.TryGetValue(key, out var queue) || queue.Count == 0)
        {
            throw new InvalidOperationException(
                $"No fake query result was configured for {configuredServer.Host}:{configuredServer.QueryPort}.");
        }

        return Task.FromResult(queue.Dequeue());
    }

    public Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var key = (configuredServer.Host, configuredServer.QueryPort);

        return Task.FromResult(
            _playersByServer.TryGetValue(key, out var players)
                ? players
                : (IReadOnlyList<RustServerPlayerResult>)[]);
    }
}
