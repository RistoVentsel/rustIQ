using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers;

public interface IServerRefreshService
{
    Task RefreshServersAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken);

    Task RefreshAllServersAsync(
        CancellationToken cancellationToken);

    Task<int> RefreshBattleMetricsDirectoryServersAsync(
        CancellationToken cancellationToken);

    Task RefreshServerAsync(
        Server server,
        CancellationToken cancellationToken);
}
