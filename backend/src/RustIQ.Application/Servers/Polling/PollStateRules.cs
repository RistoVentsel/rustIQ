using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// What a poll changes on a server, the same rules as <c>ServerPollingService.ApplyLatestServerState</c>
/// (and its <c>NormalizeQueryResult</c> and <c>CreateSnapshot</c>), on a <see cref="PolledServer"/>.
/// </summary>
public static class PollStateRules
{
    public const long DefaultOfflineThresholdMs = 600_000;

    /// <summary>Applies the poll and returns the players the snapshot records (the resulting state).</summary>
    public static int Apply(PolledServer server, PollOutcome outcome, long offlineThresholdMs = DefaultOfflineThresholdMs)
    {
        server.LastCheckedAtMs = outcome.AtMs;

        if (outcome.Answered)
        {
            var players = Math.Max(0, outcome.Players);
            server.Players = players;
            server.MaxPlayers = Math.Max(players, Math.Max(0, outcome.MaxPlayers));
            server.QueuePlayers = Math.Max(0, outcome.QueuePlayers);
            server.Status = ServerStatus.Online;
            server.LastSeenAtMs = outcome.AtMs;
            return server.Players;
        }

        // Inside the offline grace a failed query keeps the server online with its last counts.
        if (server.LastSeenAtMs is null || outcome.AtMs - server.LastSeenAtMs.Value > offlineThresholdMs)
        {
            server.Status = ServerStatus.Offline;
            server.Players = 0;
        }

        return server.Players;
    }
}
