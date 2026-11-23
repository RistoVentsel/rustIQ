using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Evaluation;

public sealed record ShownSeries(long[] TimesMs, short[] Players)
{
    public static readonly ShownSeries Empty = new ShownSeries(Array.Empty<long>(), Array.Empty<short>());

    public static ShownSeries FromSnapshots(SnapshotSeries snapshots)
    {
        List<long> times = new List<long>();
        List<short> players = new List<short>();
        for (int i = 0; i < snapshots.Count; i++)
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

public static class FreshnessEvaluator
{
    public const long TruthWindowMs = 20_000;
    public const long DefaultGridStepMs = 15 * 60 * 1000;
    public const string AllGroup = "All";

    public static IReadOnlyList<FreshnessSummary> Evaluate(
        IReadOnlyList<EvaluationInput> servers,
        long fromMs,
        long toMs,
        long gridStepMs = DefaultGridStepMs)
    {
        StudyStratum[] strata = Enum.GetValues<StudyStratum>();
        Dictionary<StudyStratum, Accumulator> groups = new Dictionary<StudyStratum, Accumulator>();
        foreach (StudyStratum stratum in strata)
        {
            groups.Add(stratum, new Accumulator());
        }

        Accumulator all = new Accumulator();

        foreach (EvaluationInput server in servers)
        {
            Accumulator accumulator = groups[server.Stratum];
            for (long t = fromMs; t < toMs; t += gridStepMs)
            {
                int? truth = TruthAt(server.Truth, t);
                if (truth == null)
                {
                    continue;
                }

                int shownIndex = LastAtOrBefore(server.Shown.TimesMs, t);
                if (shownIndex < 0)
                {
                    accumulator.AddWithoutValue();
                    all.AddWithoutValue();
                    continue;
                }

                double aoiSeconds = (t - server.Shown.TimesMs[shownIndex]) / 1000.0;
                int error = server.Shown.Players[shownIndex] - truth.Value;
                accumulator.Add(aoiSeconds, error, truth.Value);
                all.Add(aoiSeconds, error, truth.Value);
            }
        }

        List<FreshnessSummary> summaries = new List<FreshnessSummary>();
        foreach (StudyStratum stratum in strata)
        {
            summaries.Add(groups[stratum].Summarize(stratum.ToString()));
        }

        summaries.Add(all.Summarize(AllGroup));
        return summaries.ToArray();
    }

    public static int? TruthAt(ProbeSeries probes, long t)
    {
        int i = LastAtOrBefore(probes.TimesMs, t);
        while (i >= 0 && probes.TimesMs[i] > t - TruthWindowMs)
        {
            if (probes.Ok[i])
            {
                return probes.Players[i];
            }

            i--;
        }

        return null;
    }

    public static int LastAtOrBefore(long[] sortedTimes, long t)
    {
        int low = 0;
        int high = sortedTimes.Length - 1;
        int result = -1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
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

    public static double PercentileCont(IReadOnlyList<double> sortedValues, double fraction)
    {
        if (sortedValues.Count == 0)
        {
            throw new ArgumentException("No values.", nameof(sortedValues));
        }

        double position = fraction * (sortedValues.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * (position - lower);
    }

    private sealed class Accumulator
    {
        private readonly List<double> _aoi = new List<double>();
        private long _pointsWithoutValue;
        private double _absoluteErrorSum;
        private long _offBy5;
        private double _weightedAoiSum;
        private long _playerSum;

        public void AddWithoutValue()
        {
            _pointsWithoutValue++;
        }

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
            int withValue = _aoi.Count;
            if (withValue == 0)
            {
                return new FreshnessSummary(group, _pointsWithoutValue, 0, null, null, null, null, null, null);
            }

            double[] sorted = _aoi.ToArray();
            Array.Sort(sorted);

            double sum = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                sum += sorted[i];
            }

            double mean = sum / sorted.Length;

            double? playerWeightedAoi = null;
            if (_playerSum != 0)
            {
                playerWeightedAoi = _weightedAoiSum / _playerSum;
            }

            return new FreshnessSummary(
                group,
                withValue + _pointsWithoutValue,
                withValue,
                mean,
                PercentileCont(sorted, 0.5),
                PercentileCont(sorted, 0.95),
                _absoluteErrorSum / withValue,
                (double)_offBy5 / withValue,
                playerWeightedAoi);
        }
    }
}
