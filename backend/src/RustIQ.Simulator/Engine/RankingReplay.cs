using RustIQ.Application.Servers;
using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Engine;

public enum RankingMode
{
    Frozen,
    Hourly
}

public static class RankingReplay
{
    public static IReadOnlyList<long> FrozenMoments(SimulatorData data)
    {
        Dictionary<DateTimeOffset, Dictionary<Guid, int?>> ranksByTime = new Dictionary<DateTimeOffset, Dictionary<Guid, int?>>();
        List<DateTimeOffset> times = new List<DateTimeOffset>();
        foreach (SampleRankingRow row in data.SampleRankings)
        {
            Dictionary<Guid, int?>? ranks;
            if (!ranksByTime.TryGetValue(row.SampledAtUtc, out ranks))
            {
                ranks = new Dictionary<Guid, int?>();
                ranksByTime.Add(row.SampledAtUtc, ranks);
                times.Add(row.SampledAtUtc);
            }

            ranks.Add(row.ServerId, row.Rank);
        }

        times.Sort();

        long? offsetMs = MinuteOffsetMs(data);

        List<long> moments = new List<long>();
        for (int i = 1; i < times.Count; i++)
        {
            Dictionary<Guid, int?> previous = ranksByTime[times[i - 1]];
            Dictionary<Guid, int?> current = ranksByTime[times[i]];

            bool changed = false;
            foreach (KeyValuePair<Guid, int?> pair in current)
            {
                int? rank;
                if (!previous.TryGetValue(pair.Key, out rank) || rank != pair.Value)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                continue;
            }

            long lowMs = times[i - 1].ToUnixTimeMilliseconds();
            long highMs = times[i].ToUnixTimeMilliseconds();
            long? moment = null;
            if (offsetMs != null)
            {
                long candidate = highMs - Mod(highMs - offsetMs.Value, 3_600_000);
                if (candidate > lowMs)
                {
                    moment = candidate;
                }
            }

            if (moment != null)
            {
                moments.Add(moment.Value);
            }
            else
            {
                moments.Add(lowMs + (highMs - lowMs) / 2);
            }
        }

        return moments;
    }

    public static IReadOnlyList<long> HourlyMoments(SimulatorData data, long fromMs, long toMs)
    {
        long offset = 0;
        long? offsetMs = MinuteOffsetMs(data);
        if (offsetMs != null)
        {
            offset = offsetMs.Value;
        }

        long first = fromMs - Mod(fromMs - offset, 3_600_000) - 3_600_000;
        List<long> moments = new List<long>();
        for (long t = first; t < toMs; t += 3_600_000)
        {
            moments.Add(t);
        }

        return moments;
    }

    public static IReadOnlyList<int> Compute(
        long atMs,
        IReadOnlyList<PolledServer> servers,
        IReadOnlyList<ServerHistory?> histories,
        ServerRankingOptions options)
    {
        long windowStart = atMs - options.RankingWindowDays * 86_400_000L;
        long recentCutoff = atMs - options.RecentOnlineWindowHours * 3_600_000L;
        List<Candidate> candidates = new List<Candidate>();

        for (int i = 0; i < histories.Count; i++)
        {
            ServerHistory? history = histories[i];
            if (history == null || !servers[i].Exists)
            {
                continue;
            }

            long? lastSeen = history.LastAnsweredAtOrBefore(atMs);
            if (lastSeen == null || lastSeen.Value < recentCutoff)
            {
                continue;
            }

            int maxPlayers = 0;
            int? lastMaxPlayers = history.MaxPlayersAtOrBefore(atMs);
            if (lastMaxPlayers != null)
            {
                maxPlayers = lastMaxPlayers.Value;
            }

            if (maxPlayers < options.MinimumMaxPlayers)
            {
                continue;
            }

            (int Count, double Average, int Peak) window = history.Window(windowStart, atMs);
            if (window.Count >= options.MinimumSnapshots)
            {
                Candidate candidate = new Candidate();
                candidate.Index = i;
                candidate.Average = window.Average;
                candidate.Peak = window.Peak;
                candidate.Id = servers[i].Id.ToString();
                candidates.Add(candidate);
            }
        }

        candidates.Sort(CompareCandidates);

        int takeCount = candidates.Count;
        if (options.MaxRankedServers < takeCount)
        {
            takeCount = options.MaxRankedServers;
        }

        if (takeCount < 0)
        {
            takeCount = 0;
        }

        int[] ranked = new int[takeCount];
        for (int k = 0; k < takeCount; k++)
        {
            ranked[k] = candidates[k].Index;
        }

        return ranked;
    }

    private static int CompareCandidates(Candidate a, Candidate b)
    {
        int byAverage = b.Average.CompareTo(a.Average);
        if (byAverage != 0)
        {
            return byAverage;
        }

        if (a.Peak > b.Peak)
        {
            return -1;
        }

        if (a.Peak < b.Peak)
        {
            return 1;
        }

        int byId = string.CompareOrdinal(a.Id, b.Id);
        if (byId != 0)
        {
            return byId;
        }

        if (a.Index < b.Index)
        {
            return -1;
        }

        if (a.Index > b.Index)
        {
            return 1;
        }

        return 0;
    }

    private static long? MinuteOffsetMs(SimulatorData data)
    {
        if (data.Rankings.Count == 0)
        {
            return null;
        }

        DateTimeOffset latest = data.Rankings[0].GeneratedAtUtc;
        for (int i = 1; i < data.Rankings.Count; i++)
        {
            if (data.Rankings[i].GeneratedAtUtc > latest)
            {
                latest = data.Rankings[i].GeneratedAtUtc;
            }
        }

        long generated = latest.ToUnixTimeMilliseconds();
        return Mod(generated, 3_600_000);
    }

    private static long Mod(long value, long modulus)
    {
        return ((value % modulus) + modulus) % modulus;
    }

    private sealed class Candidate
    {
        public int Index;
        public double Average;
        public int Peak;
        public string Id = "";
    }
}
