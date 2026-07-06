using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeRustServerQueryClient : IRustServerQueryClient
{
    private readonly Queue<RustServerQueryResult> _queryResults;
    private readonly IReadOnlyList<RustServerPlayerResult> _players;

    public FakeRustServerQueryClient(
        params RustServerQueryResult[] queryResults)
        : this(queryResults, [])
    {
    }

    public FakeRustServerQueryClient(
        RustServerQueryResult queryResult,
        IReadOnlyList<RustServerPlayerResult> players)
        : this([queryResult], players)
    {
    }

    private FakeRustServerQueryClient(
        IReadOnlyList<RustServerQueryResult> queryResults,
        IReadOnlyList<RustServerPlayerResult> players)
    {
        _queryResults = new Queue<RustServerQueryResult>(queryResults);
        _players = players;
    }

    public Task<RustServerQueryResult> QueryServerAsync(CancellationToken cancellationToken)
    {
        if (_queryResults.Count == 0)
        {
            throw new InvalidOperationException("No fake query result was configured.");
        }

        return Task.FromResult(_queryResults.Dequeue());
    }

    public Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_players);
    }
}
