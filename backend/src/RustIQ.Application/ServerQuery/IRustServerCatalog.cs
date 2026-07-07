namespace RustIQ.Application.ServerQuery;

public interface IRustServerCatalog
{
    Task<IReadOnlyList<ConfiguredRustServer>> GetConfiguredServersAsync(CancellationToken cancellationToken);
}
