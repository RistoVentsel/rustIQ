using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Engine;

public sealed class ResponseOracle
{
    public const long ProbeWindowMs = 20_000;

    private readonly SimulatorData _data;
    private readonly int[] _sampleIdxByServerIndex;

    public ResponseOracle(SimulatorData data)
    {
        _data = data;
        _sampleIdxByServerIndex = new int[data.ServerIndex.Count];
        foreach (SampleServer sample in data.SampleServers)
        {
            _sampleIdxByServerIndex[sample.ServerIndex] = sample.ServerIdx;
        }
    }

    public int SampleIdxOf(int serverIndex)
    {
        if (serverIndex < _sampleIdxByServerIndex.Length)
        {
            return _sampleIdxByServerIndex[serverIndex];
        }

        return 0;
    }

    public PollOutcome Answer(int serverIndex, long atMs, PolledServer state)
    {
        if (serverIndex >= _data.ServerIndex.Count)
        {
            return new PollOutcome(atMs, false, 0, 0, 0);
        }

        int sampleIdx = _sampleIdxByServerIndex[serverIndex];
        SnapshotSeries snapshots = _data.Snapshots[serverIndex];
        if (sampleIdx > 0)
        {
            ProbeSeries probes = _data.Probes[sampleIdx - 1];
            int i = FreshnessEvaluator.LastAtOrBefore(probes.TimesMs, atMs);
            if (i < 0 || probes.TimesMs[i] <= atMs - ProbeWindowMs || !probes.Ok[i])
            {
                return new PollOutcome(atMs, false, 0, 0, 0);
            }

            int nearest = Nearest(snapshots.TimesMs, atMs);
            int maxPlayers;
            if (nearest >= 0)
            {
                maxPlayers = snapshots.MaxPlayers[nearest];
            }
            else
            {
                maxPlayers = state.MaxPlayers;
            }

            return new PollOutcome(atMs, true, probes.Players[i], maxPlayers, probes.Queue[i]);
        }

        int n = Nearest(snapshots.TimesMs, atMs);
        if (n < 0 || !snapshots.Answered(n))
        {
            return new PollOutcome(atMs, false, 0, 0, 0);
        }

        return new PollOutcome(atMs, true, snapshots.Players[n], snapshots.MaxPlayers[n], 0);
    }

    public static int Nearest(long[] sortedTimes, long t)
    {
        if (sortedTimes.Length == 0)
        {
            return -1;
        }

        int before = FreshnessEvaluator.LastAtOrBefore(sortedTimes, t);
        int after = before + 1;
        if (before < 0)
        {
            return 0;
        }

        if (after >= sortedTimes.Length)
        {
            return before;
        }

        if (t - sortedTimes[before] <= sortedTimes[after] - t)
        {
            return before;
        }

        return after;
    }
}
