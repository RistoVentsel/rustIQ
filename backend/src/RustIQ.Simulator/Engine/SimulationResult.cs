using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Engine;

/// <summary>Everything a run logs: polls per minute and lane, the sample servers' polls, rankings.</summary>
public sealed class SimulationResult
{
    private readonly Dictionary<string, LaneLog> _lanes = new(StringComparer.Ordinal);
    private readonly List<long>[] _shownTimes;
    private readonly List<short>[] _shownPlayers;
    private readonly List<RankingRegeneration> _rankings = [];
    private readonly int _minutes;
    private readonly long[] _knownPerMinute;
    private readonly long[] _phantomsPerMinute;
    private readonly int[] _ticksPerMinute;

    public SimulationResult(string strategy, long fromMs, long toMs, int sampleCount)
    {
        Strategy = strategy;
        FromMs = fromMs;
        ToMs = toMs;
        SampleCount = sampleCount;
        _minutes = (int)Math.Ceiling((toMs - fromMs) / 60_000.0);
        _shownTimes = Enumerable.Range(0, sampleCount).Select(_ => new List<long>()).ToArray();
        _shownPlayers = Enumerable.Range(0, sampleCount).Select(_ => new List<short>()).ToArray();
        _knownPerMinute = new long[_minutes];
        _phantomsPerMinute = new long[_minutes];
        _ticksPerMinute = new int[_minutes];
    }

    public string Strategy { get; }
    public long FromMs { get; }
    public long ToMs { get; }
    public int SampleCount { get; }
    public int Minutes => _minutes;
    public IReadOnlyDictionary<string, LaneLog> Lanes => _lanes;
    public IReadOnlyList<RankingRegeneration> Rankings => _rankings;

    public void RecordPoll(long atMs, string lane, bool answered, bool exogenous, int sampleIdx)
    {
        if (!_lanes.TryGetValue(lane, out var log))
        {
            log = new LaneLog(lane, exogenous, _minutes, SampleCount);
            _lanes.Add(lane, log);
        }

        var minute = (int)((atMs - FromMs) / 60_000);
        if (minute < 0 || minute >= _minutes)
        {
            return;
        }

        log.Polls[minute]++;
        if (!answered)
        {
            log.Unanswered[minute]++;
        }

        if (sampleIdx > 0)
        {
            log.SamplePolls[sampleIdx - 1].Add(atMs);
        }
    }

    public void RecordShown(int sampleIdx, long atMs, int players)
    {
        _shownTimes[sampleIdx - 1].Add(atMs);
        _shownPlayers[sampleIdx - 1].Add((short)players);
    }

    public void RecordRanking(long atMs, bool initial, int rankedCount, int?[] sampleRanks) =>
        _rankings.Add(new RankingRegeneration(atMs, initial, rankedCount, sampleRanks));

    /// <summary>Gap to the next tick, for the tick-gap statistics.</summary>
    public void RecordTick(long gapMs)
    {
        Ticks++;
        if (gapMs > 15_500)
        {
            LateTicks++;
        }
    }

    public long Ticks { get; private set; }

    public long LateTicks { get; private set; }

    public void RecordPopulation(long atMs, int known, int phantoms)
    {
        var minute = (int)((atMs - FromMs) / 60_000);
        if (minute >= 0 && minute < _minutes)
        {
            _knownPerMinute[minute] += known;
            _phantomsPerMinute[minute] += phantoms;
            _ticksPerMinute[minute]++;
        }
    }

    /// <summary>The sample server's shown polls, sorted by time (replayed polls can arrive out of order).</summary>
    public ShownSeries Shown(int sampleIdx)
    {
        var times = _shownTimes[sampleIdx - 1].ToArray();
        var players = _shownPlayers[sampleIdx - 1].ToArray();
        Array.Sort(times, players);
        return new ShownSeries(times, players);
    }

    /// <summary>Average known and phantom servers over the minutes in [fromMs, toMs).</summary>
    public (double Known, double Phantoms) AveragePopulation(long fromMs, long toMs)
    {
        double known = 0, phantoms = 0;
        long ticks = 0;
        foreach (var minute in MinuteRange(fromMs, toMs))
        {
            known += _knownPerMinute[minute];
            phantoms += _phantomsPerMinute[minute];
            ticks += _ticksPerMinute[minute];
        }

        return ticks == 0 ? (0, 0) : (known / ticks, phantoms / ticks);
    }

    public IEnumerable<int> MinuteRange(long fromMs, long toMs)
    {
        var first = (int)Math.Max(0, (fromMs - FromMs) / 60_000);
        var last = (int)Math.Min(_minutes, (toMs - FromMs + 59_999) / 60_000);
        for (var minute = first; minute < last; minute++)
        {
            yield return minute;
        }
    }
}

public sealed class LaneLog
{
    public LaneLog(string lane, bool exogenous, int minutes, int sampleCount)
    {
        Lane = lane;
        Exogenous = exogenous;
        Polls = new int[minutes];
        Unanswered = new int[minutes];
        SamplePolls = Enumerable.Range(0, sampleCount).Select(_ => new List<long>()).ToArray();
    }

    public string Lane { get; }

    /// <summary>Replayed Watched and Discovery polls: outside every strategy's budget.</summary>
    public bool Exogenous { get; }

    public int[] Polls { get; }
    public int[] Unanswered { get; }

    /// <summary>Poll times per sample server (index server_idx - 1).</summary>
    public List<long>[] SamplePolls { get; }
}

public sealed record RankingRegeneration(long AtMs, bool Initial, int RankedCount, int?[] SampleRanks);
