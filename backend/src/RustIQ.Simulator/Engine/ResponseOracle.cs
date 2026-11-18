using RustIQ.Application.Servers.Polling;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Engine;

/// <summary>
/// Answers a simulated poll (plan 5.3):
/// a sample server from its probes, any other known server from the production snapshot nearest in
/// time, and a phantom never.
/// </summary>
public sealed class ResponseOracle
{
    public const long ProbeWindowMs = 20_000;

    private readonly SimulatorData _data;
    private readonly int[] _sampleIdxByServerIndex;

    public ResponseOracle(SimulatorData data)
    {
        _data = data;
        _sampleIdxByServerIndex = Enumerable.Repeat(0, data.ServerIndex.Count).ToArray();
        foreach (var sample in data.SampleServers)
        {
            _sampleIdxByServerIndex[sample.ServerIndex] = sample.ServerIdx;
        }
    }

    /// <summary>The sample's server_idx (1-based) of a known server, or 0.</summary>
    public int SampleIdxOf(int serverIndex) =>
        serverIndex < _sampleIdxByServerIndex.Length ? _sampleIdxByServerIndex[serverIndex] : 0;

    public PollOutcome Answer(int serverIndex, long atMs, PolledServer state)
    {
        if (serverIndex >= _data.ServerIndex.Count)
        {
            return new PollOutcome(atMs, false, 0, 0, 0);
        }

        var sampleIdx = _sampleIdxByServerIndex[serverIndex];
        var snapshots = _data.Snapshots[serverIndex];
        if (sampleIdx > 0)
        {
            var probes = _data.Probes[sampleIdx - 1];
            var i = FreshnessEvaluator.LastAtOrBefore(probes.TimesMs, atMs);
            if (i < 0 || probes.TimesMs[i] <= atMs - ProbeWindowMs || !probes.Ok[i])
            {
                return new PollOutcome(atMs, false, 0, 0, 0);
            }

            // Probes carry no max players; take production's nearest value.
            var nearest = Nearest(snapshots.TimesMs, atMs);
            var maxPlayers = nearest >= 0 ? snapshots.MaxPlayers[nearest] : state.MaxPlayers;
            return new PollOutcome(atMs, true, probes.Players[i], maxPlayers, probes.Queue[i]);
        }

        var n = Nearest(snapshots.TimesMs, atMs);
        if (n < 0 || !snapshots.Answered(n))
        {
            return new PollOutcome(atMs, false, 0, 0, 0);
        }

        // Snapshots have no queue column; a non-sample server never shows a queue (a known limitation).
        return new PollOutcome(atMs, true, snapshots.Players[n], snapshots.MaxPlayers[n], 0);
    }

    /// <summary>Index of the element nearest to t (either side, the earlier one on a tie), or -1.</summary>
    public static int Nearest(long[] sortedTimes, long t)
    {
        if (sortedTimes.Length == 0)
        {
            return -1;
        }

        var before = FreshnessEvaluator.LastAtOrBefore(sortedTimes, t);
        var after = before + 1;
        if (before < 0) return 0;
        if (after >= sortedTimes.Length) return before;
        return t - sortedTimes[before] <= sortedTimes[after] - t ? before : after;
    }
}
