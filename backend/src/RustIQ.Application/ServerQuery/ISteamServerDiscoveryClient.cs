namespace RustIQ.Application.ServerQuery;

public interface ISteamServerDiscoveryClient
{
    Task<IReadOnlyList<ConfiguredRustServer>> DiscoverServersAsync(
        int maxServers,
        CancellationToken cancellationToken);
}
