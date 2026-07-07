using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeRustServerCatalog : IRustServerCatalog
{
    private readonly IReadOnlyList<ConfiguredRustServer> _configuredServers;

    public FakeRustServerCatalog(IReadOnlyList<ConfiguredRustServer>? configuredServers = null)
    {
        _configuredServers = configuredServers ?? [];
    }

    public Task<IReadOnlyList<ConfiguredRustServer>> GetConfiguredServersAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_configuredServers);
    }
}
