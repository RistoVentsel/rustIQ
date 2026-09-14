namespace RustIQ.Application.ServerQuery;

public interface IFacepunchServerDirectoryClient
{
    Task<IReadOnlyList<ConfiguredRustServer>> GetRustServersAsync(
        int maxServers,
        CancellationToken cancellationToken);
}
