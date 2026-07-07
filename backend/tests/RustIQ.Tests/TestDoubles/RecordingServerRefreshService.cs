using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.TestDoubles;

internal sealed class RecordingServerRefreshService : IServerRefreshService
{
    public int RefreshAllCallCount { get; private set; }

    public bool ThrowOnRefreshAll { get; set; }

    public bool ThrowOnRefreshServers { get; set; }

    public bool ThrowOnRefreshServer { get; set; }

    public List<IReadOnlyList<Server>> RefreshBatches { get; } = [];

    public List<Server> RefreshedServers { get; } = [];

    public Task RefreshServersAsync(
        IReadOnlyList<Server> servers,
        CancellationToken cancellationToken)
    {
        if (ThrowOnRefreshServers)
        {
            throw new InvalidOperationException("Refresh page failed.");
        }

        RefreshBatches.Add(servers.ToArray());
        return Task.CompletedTask;
    }

    public Task RefreshAllServersAsync(
        CancellationToken cancellationToken)
    {
        RefreshAllCallCount++;
        if (ThrowOnRefreshAll)
        {
            throw new InvalidOperationException("Refresh all failed.");
        }

        return Task.CompletedTask;
    }

    public Task RefreshServerAsync(
        Server server,
        CancellationToken cancellationToken)
    {
        if (ThrowOnRefreshServer)
        {
            throw new InvalidOperationException("Refresh server failed.");
        }

        RefreshedServers.Add(server);
        return Task.CompletedTask;
    }
}
