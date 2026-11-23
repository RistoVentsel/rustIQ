namespace RustIQ.Application.Servers.Polling;

public interface IPollingStrategy
{
    string Name { get; }

    void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll);

    void OnPolled(PolledServer server, PollOutcome outcome)
    {
    }

    void OnServerReset(PolledServer server)
    {
    }
}

public interface IPollExecutor
{
    void Poll(IReadOnlyList<PolledServer> servers, string lane);
}

public readonly record struct PollOutcome(long AtMs, bool Answered, int Players, int MaxPlayers, int QueuePlayers);
