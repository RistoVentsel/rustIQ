using RustIQ.Domain.Servers;

namespace RustIQ.Application.Servers.Polling;

public static class PollStateRules
{
    public const long DefaultOfflineThresholdMs = 600_000;

    public static int Apply(PolledServer server, PollOutcome outcome, long offlineThresholdMs = DefaultOfflineThresholdMs)
    {
        server.LastCheckedAtMs = outcome.AtMs;

        if (outcome.Answered)
        {
            int players = Math.Max(0, outcome.Players);
            server.Players = players;
            server.MaxPlayers = Math.Max(players, Math.Max(0, outcome.MaxPlayers));
            server.QueuePlayers = Math.Max(0, outcome.QueuePlayers);
            server.Status = ServerStatus.Online;
            server.LastSeenAtMs = outcome.AtMs;
            return server.Players;
        }

        if (!server.LastSeenAtMs.HasValue || outcome.AtMs - server.LastSeenAtMs.Value > offlineThresholdMs)
        {
            server.Status = ServerStatus.Offline;
            server.Players = 0;
        }

        return server.Players;
    }
}
