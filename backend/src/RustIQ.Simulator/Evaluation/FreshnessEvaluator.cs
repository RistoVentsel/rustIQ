using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Evaluation;

/// <summary>
/// What one sample server showed: the times and player counts of its answered polls that count for
/// freshness (Watched polls excluded, Discovery polls included), sorted by time.
/// </summary>
public sealed record ShownSeries(long[] TimesMs, short[] Players)
{
    public static readonly ShownSeries Empty = new([], []);

    /// <summary>Answered production polls of one server, without Watched polls.</summary>
    public static ShownSeries FromSnapshots(SnapshotSeries snapshots)
    {
        var times = new List<long>();
        var players = new List<short>();
        for (var i = 0; i < snapshots.Count; i++)
        {
            if (snapshots.Ok[i] == 1 && snapshots.Sources[i] != ServerPollSource.Watched)
            {
                times.Add(snapshots.TimesMs[i]);
                players.Add(snapshots.Players[i]);
            }
        }

        return new ShownSeries(times.ToArray(), players.ToArray());
    }
}

public sealed record EvaluationInput(StudyStratum Stratum, ProbeSeries Truth, ShownSeries Shown);

public sealed record FreshnessSummary(
    string Group,
    long Points,
    long PointsWithValue,
    double? AoiMeanSeconds,
    double? AoiMedianSeconds,
    double? AoiP95Seconds,
    double? PlayerMae,
    double? ShareOffByMoreThan5,
    double? PlayerWeightedAoiSeconds);

/// <summary>
/// The freshness measurement of the thesis (Appendix 2, query 1), identical for real and simulated polls:
/// <list type="number">
/// <item>Observation grid every 15 min from the period start, for every sample server.</item>
/// <item>A point counts only if the server has a probe with ok = 1 in (t - 20 s, t]; its players are the truth.</item>
/// <item>Shown is the latest answered poll at or before t.</item>
/// <item>AoI = t - time of that poll. A point without one counts in points, not in the averages.</item>
/// </list>
/// </summary>
public static class FreshnessEvaluator
{
    public const long TruthWindowMs = 20_000;
    public const long DefaultGridStepMs = 15 * 60 * 1000;
    public const string AllGroup = "All";

    /// <summary>Evaluates the grid points t with from &lt;= t &lt; to.</summary>
    public static IReadOnlyList<FreshnessSummary> Evaluate(
        IReadOnlyList<EvaluationInput> servers,
        long fromMs,
        long toMs,
        long gridStepMs = DefaultGridStepMs)
    {
        var groups = Enum.GetValues<StudyStratum>()
            .ToDictionary(stratum => stratum, _ => new Accumulator());
        var all = new Accumulator();

        foreach (var server in servers)
        {
            var accumulator = groups[server.Stratum];
            for (var t = fromMs; t < toMs; t += gridStepMs)
            {
                var truth = TruthAt(server.Truth, t);
                if (truth is null)
                {
                    continue;
                }

                var shownIndex = LastAtOrBefore(server.Shown.TimesMs, t);
                if (shownIndex < 0)
                {
                    accumulator.AddWithoutValue();
                    all.AddWithoutValue();
                    continue;
                }

                var aoiSeconds = (t - server.Shown.TimesMs[shownIndex]) / 1000.0;
                var error = server.Shown.Players[shownIndex] - truth.Value;
                accumulator.Add(aoiSeconds, error, truth.Value);
                all.Add(aoiSeconds, error, truth.Value);
            }
        }

        return Enum.GetValues<StudyStratum>()
            .Select(stratum => groups[stratum].Summarize(stratum.ToString()))
            .Append(all.Summarize(AllGroup))
            .ToArray();
    }

    /// <summary>The players of the latest answered probe in (t - 20 s, t], or null if there is none.</summary>
    public static int? TruthAt(ProbeSeries probes, long t)
    {
        for (var i = LastAtOrBefore(probes.TimesMs, t); i >= 0 && probes.TimesMs[i] > t - TruthWindowMs; i--)
        {
            if (probes.Ok[i])
            {
                return probes.Players[i];
            }
        }

        return null;
    }

    /// <summary>Index of the last element &lt;= t in a sorted array, or -1.</summary>
    public static int LastAtOrBefore(long[] sortedTimes, long t)
    {
        int low = 0, high = sortedTimes.Length - 1, result = -1;
        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            if (sortedTimes[mid] <= t)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return result;
    }

    /// <summary>PostgreSQL <c>percentile_cont</c>: linear interpolation between the closest ranks.</summary>
    public static double PercentileCont(IReadOnlyList<double> sortedValues, double fraction)
    {
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("No values.", nameof(sortedValues));
        }

        var position = fraction * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * (position - lower);
    }

    private sealed class Accumulator
    {
        private readonly List<double> _aoi = [];
        private long _pointsWithoutValue;
        private double _absoluteErrorSum;
        private long _offBy5;
        private double _weightedAoiSum;
        private long _playerSum;

        public void AddWithoutValue() => _pointsWithoutValue++;

        public void Add(double aoiSeconds, int error, int truePlayers)
        {
            _aoi.Add(aoiSeconds);
            _absoluteErrorSum += Math.Abs(error);
            if (Math.Abs(error) > 5)
            {
                _offBy5++;
            }

            _weightedAoiSum += truePlayers * aoiSeconds;
            _playerSum += truePlayers;
        }

        public FreshnessSummary Summarize(string group)
        {
            var withValue = _aoi.Count;
            if (withValue == 0)
            {
                return new FreshnessSummary(group, _pointsWithoutValue, 0, null, null, null, null, null, null);
            }

            var sorted = _aoi.ToArray();
            Array.Sort(sorted);
            return new FreshnessSummary(
                group,
                withValue + _pointsWithoutValue,
                withValue,
                sorted.Average(),
                PercentileCont(sorted, 0.5),
                PercentileCont(sorted, 0.95),
                _absoluteErrorSum / withValue,
                (double)_offBy5 / withValue,
                _playerSum == 0 ? null : _weightedAoiSum / _playerSum);
        }
    }
}
