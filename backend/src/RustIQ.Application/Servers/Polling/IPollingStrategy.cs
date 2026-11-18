namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// Decides which servers to poll on each worker tick. Implementations are pure in-memory code: they
/// see only <see cref="PolledServer"/> state and the outcomes of polls, never the network or a database,
/// so the same class can run in the simulator and in a production worker.
/// </summary>
public interface IPollingStrategy
{
    string Name { get; }

    /// <summary>
    /// Runs one tick. The strategy polls through <paramref name="poll"/>, which queries the server and
    /// applies the result to its <see cref="PolledServer"/> before returning, exactly as production's
    /// lanes see the previous lane's results within a tick.
    /// </summary>
    void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll);

    /// <summary>
    /// Called after every poll that changed a server, the strategy's own and exogenous ones
    /// (a user on a detail page, discovery). Strategies with their own estimates update them here.
    /// </summary>
    void OnPolled(PolledServer server, PollOutcome outcome) { }

    /// <summary>Called when a server slot is reused for a different server.</summary>
    void OnServerReset(PolledServer server) { }
}

public interface IPollExecutor
{
    /// <summary>Polls a batch of servers for <paramref name="lane"/>; their state is updated on return.</summary>
    void Poll(IReadOnlyList<PolledServer> servers, string lane);
}

public readonly record struct PollOutcome(long AtMs, bool Answered, int Players, int MaxPlayers, int QueuePlayers);
