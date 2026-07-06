namespace RustIQ.Application.ServerQuery;

public interface IRustServerQueryClient
{
    Task<RustServerQueryResult> QueryServerAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(CancellationToken cancellationToken);
}
