using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.TestDoubles;

internal sealed class RecordingServerRefreshService : IServerRefreshService
{
    public int RefreshAllCallCount { get; private set; }

    public List<IReadOnlyList<Server>> RefreshBatches { get; } = [];

    public List<Server> RefreshedServers { get; } = [];

    public Task RefreshServersAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        RefreshBatches.Add(servers.ToArray());
        return Task.CompletedTask;
    }

    public Task RefreshAllServersAsync(
        CancellationToken cancellationToken)
    {
        RefreshAllCallCount++;
        return Task.CompletedTask;
    }

    public Task RefreshServerAsync(
        Server server,
        CancellationToken cancellationToken)
    {
        RefreshedServers.Add(server);
        return Task.CompletedTask;
    }
}
