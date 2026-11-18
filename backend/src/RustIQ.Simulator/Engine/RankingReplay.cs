using RustIQ.Application.Servers;
using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Engine;

public enum RankingMode
{
    /// <summary>Regenerate only when production did (the ranking was mostly frozen, plan 3 and 4.4).</summary>
    Frozen,

    /// <summary>Regenerate every hour, as the code intends.</summary>
    Hourly
}

/// <summary>
/// When the ranking is regenerated, and the ranking rule of <c>ServerRepository.ReplaceLatestServerRankingsAsync</c>
/// on the simulated history. Name and host:port dedup are not in the export and are ignored.
/// </summary>
public static class RankingReplay
{
    /// <summary>
    /// Production's regeneration moments, from the hours in which any sample rank changed. The exact
    /// minute is taken from <c>server_rankings.generated_at</c> (the ranking worker ran at the same
    /// minute past every hour): the latest such minute at or before the sample that shows the change and
    /// after the sample before it. Without one the midpoint between the two samples is used.
    /// </summary>
    public static IReadOnlyList<long> FrozenMoments(SimulatorData data)
    {
        var samples = data.SampleRankings
            .GroupBy(row => row.SampledAtUtc)
            .OrderBy(group => group.Key)
            .Select(group => (AtMs: group.Key.ToUnixTimeMilliseconds(),
                Ranks: group.ToDictionary(row => row.ServerId, row => row.Rank)))
            .ToArray();
        var offsetMs = MinuteOffsetMs(data);

        var moments = new List<long>();
        for (var i = 1; i < samples.Length; i++)
        {
            var previous = samples[i - 1].Ranks;
            var changed = samples[i].Ranks.Any(pair => !previous.TryGetValue(pair.Key, out var rank) || rank != pair.Value);
            if (!changed)
            {
                continue;
            }

            var lowMs = samples[i - 1].AtMs;
            var highMs = samples[i].AtMs;
            long? moment = null;
            if (offsetMs is { } offset)
            {
                var candidate = highMs - Mod(highMs - offset, 3_600_000);
                if (candidate > lowMs)
                {
                    moment = candidate;
                }
            }

            moments.Add(moment ?? lowMs + (highMs - lowMs) / 2);
        }

        return moments;
    }

    /// <summary>Every hour at production's minute past the hour, from one hour before <paramref name="fromMs"/>.</summary>
    public static IReadOnlyList<long> HourlyMoments(SimulatorData data, long fromMs, long toMs)
    {
        var offset = MinuteOffsetMs(data) ?? 0;
        var first = fromMs - Mod(fromMs - offset, 3_600_000) - 3_600_000;
        var moments = new List<long>();
        for (var t = first; t < toMs; t += 3_600_000)
        {
            moments.Add(t);
        }

        return moments;
    }

    /// <summary>
    /// The ranking at <paramref name="atMs"/>: among existing servers seen in the last 24 h with
    /// MaxPlayers &gt;= 1, those with at least 6 snapshots in the last 7 days, by average players,
    /// then peak, then id. Returns server indexes in rank order (best first), at most MaxRankedServers.
    /// </summary>
    public static IReadOnlyList<int> Compute(
        long atMs,
        IReadOnlyList<PolledServer> servers,
        IReadOnlyList<ServerHistory?> histories,
        ServerRankingOptions options)
    {
        var windowStart = atMs - options.RankingWindowDays * 86_400_000L;
        var recentCutoff = atMs - options.RecentOnlineWindowHours * 3_600_000L;
        var candidates = new List<(int Index, double Average, int Peak, string Id)>();

        for (var i = 0; i < histories.Count; i++)
        {
            var history = histories[i];
            if (history is null || !servers[i].Exists)
            {
                continue;
            }

            if (history.LastAnsweredAtOrBefore(atMs) is not { } lastSeen || lastSeen < recentCutoff)
            {
                continue;
            }

            if ((history.MaxPlayersAtOrBefore(atMs) ?? 0) < options.MinimumMaxPlayers)
            {
                continue;
            }

            var (count, average, peak) = history.Window(windowStart, atMs);
            if (count >= options.MinimumSnapshots)
            {
                candidates.Add((i, average, peak, servers[i].Id.ToString()));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Average)
            .ThenByDescending(candidate => candidate.Peak)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Take(options.MaxRankedServers)
            .Select(candidate => candidate.Index)
            .ToArray();
    }

    private static long? MinuteOffsetMs(SimulatorData data)
    {
        if (data.Rankings.Count == 0)
        {
            return null;
        }

        var generated = data.Rankings.Max(row => row.GeneratedAtUtc).ToUnixTimeMilliseconds();
        return Mod(generated, 3_600_000);
    }

    private static long Mod(long value, long modulus) => ((value % modulus) + modulus) % modulus;
}
