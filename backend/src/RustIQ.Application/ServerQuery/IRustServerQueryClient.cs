namespace RustIQ.Application.ServerQuery;

public interface IRustServerQueryClient
{
    Task<RustServerQueryResult> QueryServerAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken);
}
