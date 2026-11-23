using RustIQ.Simulator.Evaluation;

namespace RustIQ.Simulator.Engine;

public sealed class SimulationResult
{
    private readonly Dictionary<string, LaneLog> _lanes = new Dictionary<string, LaneLog>(StringComparer.Ordinal);
    private readonly List<long>[] _shownTimes;
    private readonly List<short>[] _shownPlayers;
    private readonly List<RankingRegeneration> _rankings = new List<RankingRegeneration>();
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

        _shownTimes = new List<long>[sampleCount];
        _shownPlayers = new List<short>[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            _shownTimes[i] = new List<long>();
            _shownPlayers[i] = new List<short>();
        }

        _knownPerMinute = new long[_minutes];
        _phantomsPerMinute = new long[_minutes];
        _ticksPerMinute = new int[_minutes];
    }

    public string Strategy { get; }
    public long FromMs { get; }
    public long ToMs { get; }
    public int SampleCount { get; }

    public int Minutes
    {
        get { return _minutes; }
    }

    public IReadOnlyDictionary<string, LaneLog> Lanes
    {
        get { return _lanes; }
    }

    public IReadOnlyList<RankingRegeneration> Rankings
    {
        get { return _rankings; }
    }

    public long Ticks { get; private set; }

    public long LateTicks { get; private set; }

    public void RecordPoll(long atMs, string lane, bool answered, bool exogenous, int sampleIdx)
    {
        LaneLog? log;
        if (!_lanes.TryGetValue(lane, out log))
        {
            log = new LaneLog(lane, exogenous, _minutes, SampleCount);
            _lanes.Add(lane, log);
        }

        int minute = (int)((atMs - FromMs) / 60_000);
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

    public void RecordRanking(long atMs, bool initial, int rankedCount, int?[] sampleRanks)
    {
        _rankings.Add(new RankingRegeneration(atMs, initial, rankedCount, sampleRanks));
    }

    public void RecordTick(long gapMs)
    {
        Ticks++;
        if (gapMs > 15_500)
        {
            LateTicks++;
        }
    }

    public void RecordPopulation(long atMs, int known, int phantoms)
    {
        int minute = (int)((atMs - FromMs) / 60_000);
        if (minute >= 0 && minute < _minutes)
        {
            _knownPerMinute[minute] += known;
            _phantomsPerMinute[minute] += phantoms;
            _ticksPerMinute[minute]++;
        }
    }

    public ShownSeries Shown(int sampleIdx)
    {
        long[] times = _shownTimes[sampleIdx - 1].ToArray();
        short[] players = _shownPlayers[sampleIdx - 1].ToArray();
        Array.Sort(times, players);
        return new ShownSeries(times, players);
    }

    public (double Known, double Phantoms) AveragePopulation(long fromMs, long toMs)
    {
        double known = 0;
        double phantoms = 0;
        long ticks = 0;
        foreach (int minute in MinuteRange(fromMs, toMs))
        {
            known += _knownPerMinute[minute];
            phantoms += _phantomsPerMinute[minute];
            ticks += _ticksPerMinute[minute];
        }

        if (ticks == 0)
        {
            return (0, 0);
        }

        return (known / ticks, phantoms / ticks);
    }

    public IEnumerable<int> MinuteRange(long fromMs, long toMs)
    {
        int first = (int)Math.Max(0, (fromMs - FromMs) / 60_000);
        int last = (int)Math.Min(_minutes, (toMs - FromMs + 59_999) / 60_000);
        List<int> minutes = new List<int>();
        for (int minute = first; minute < last; minute++)
        {
            minutes.Add(minute);
        }

        return minutes;
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
        SamplePolls = new List<long>[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            SamplePolls[i] = new List<long>();
        }
    }

    public string Lane { get; }

    public bool Exogenous { get; }

    public int[] Polls { get; }
    public int[] Unanswered { get; }

    public List<long>[] SamplePolls { get; }
}

public sealed class RankingRegeneration
{
    public RankingRegeneration(long atMs, bool initial, int rankedCount, int?[] sampleRanks)
    {
        AtMs = atMs;
        Initial = initial;
        RankedCount = rankedCount;
        SampleRanks = sampleRanks;
    }

    public long AtMs { get; }
    public bool Initial { get; }
    public int RankedCount { get; }
    public int?[] SampleRanks { get; }
}
