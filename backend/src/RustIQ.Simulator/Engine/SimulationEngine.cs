using RustIQ.Application.Servers;
using RustIQ.Application.Servers.Polling;
using RustIQ.Domain.Servers;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Engine;

public sealed class SimulationOptions
{
    public required long FromMs { get; init; }
    public required long ToMs { get; init; }
    public long TickMs { get; init; } = 15_000;
    public int Seed { get; init; } = 1;
    public RankingMode Ranking { get; init; } = RankingMode.Frozen;
    public long OfflineThresholdMs { get; init; } = PollStateRules.DefaultOfflineThresholdMs;

    /// <summary>How long recent snapshots are kept for the recent-peak rules; covers their 60 min windows.</summary>
    public long RecentSnapshotKeepMs { get; init; } = 2 * 3_600_000L;

    public ServerRankingOptions RankingOptions { get; init; } = new();

    /// <summary>How long polls take and how the worker's timer behaves; null polls instantly at the tick.</summary>
    public WorkerTiming? Timing { get; init; } = new();
}

/// <summary>
/// The production worker's timing, which decides how often the lanes really run:
/// <list type="bullet">
/// <item>A tick's lanes run one after another; a lane polls its batch with <see cref="Concurrency"/>
/// queries at a time, an answered query taking <see cref="AnsweredPollSeconds"/> and an unanswered one
/// <see cref="UnansweredPollSeconds"/> (two attempts of 3 s).</item>
/// <item>The timer ticks every 15 s, but a tick that is still working lets the next one wait; PeriodicTimer
/// keeps one missed tick, which fires as soon as the work ends.</item>
/// <item><c>now</c> is read a few milliseconds after the tick fires (<see cref="JitterMs"/>), so a lane
/// whose interval is a multiple of the tick is not due on about half of its ticks.</item>
/// </list>
/// </summary>
public sealed class WorkerTiming
{
    public int Concurrency { get; init; } = 32;
    public double AnsweredPollSeconds { get; init; } = 0.5;
    public double UnansweredPollSeconds { get; init; } = 6;
    public long JitterMs { get; init; } = 50;
}

/// <summary>
/// Replays a period in 15 s steps (plan 5.2). Each step: servers appear and disappear, the phantom
/// population follows <c>study_population_samples</c>, exogenous polls (Watched, Discovery) are replayed,
/// the ranking is regenerated when due, and then the strategy polls. A strategy only learns what polls
/// return; it never reads probes or production snapshots.
/// </summary>
public sealed class SimulationEngine : IPollExecutor
{
    public const string WatchedLane = nameof(ServerPollSource.Watched);
    public const string DiscoveryLane = nameof(ServerPollSource.Discovery);

    private readonly SimulatorData _data;
    private readonly IPollingStrategy _strategy;
    private readonly SimulationOptions _options;
    private readonly ResponseOracle _oracle;
    private readonly List<PolledServer> _servers = [];
    private readonly List<ServerHistory?> _histories = [];
    private readonly SimulationResult _result;
    private readonly Random _random;
    private readonly int _knownCount;
    private readonly Queue<PolledServer> _activePhantoms = new();
    private readonly Queue<PolledServer> _freePhantomSlots = new();
    private long _nowMs;
    private long _workCursorMs;
    private int _knownExisting;

    public SimulationEngine(SimulatorData data, IPollingStrategy strategy, SimulationOptions options)
    {
        _data = data;
        _strategy = strategy;
        _options = options;
        _oracle = new ResponseOracle(data);
        _random = new Random(options.Seed);
        _knownCount = data.ServerIndex.Count;
        _result = new SimulationResult(strategy.Name, options.FromMs, options.ToMs, data.SampleServers.Count);
    }

    public IReadOnlyList<PolledServer> Servers => _servers;

    public SimulationResult Run()
    {
        var lifetimes = BuildKnownServers();
        var existenceEvents = ExistenceEvents(lifetimes);
        var exogenous = ExogenousPolls();
        var wipeEvents = NextWipeEvents();
        var moments = (_options.Ranking == RankingMode.Frozen
                ? RankingReplay.FrozenMoments(_data)
                : RankingReplay.HourlyMoments(_data, _options.FromMs, _options.ToMs))
            .OrderBy(moment => moment)
            .ToArray();
        var population = _data.PopulationSamples
            .Select(sample => (AtMs: sample.SampledAtUtc.ToUnixTimeMilliseconds(), sample.TotalServers))
            .ToArray();

        int existenceAt = 0, exogenousAt = 0, wipeAt = 0, populationAt = 0;

        // The ranking production had at the start: the last regeneration before the period, recomputed
        // from the history before it (empty when the export starts at the period start).
        var momentAt = 0;
        while (momentAt < moments.Length && moments[momentAt] <= _options.FromMs)
        {
            momentAt++;
        }

        if (momentAt > 0)
        {
            ApplyExistence(existenceEvents, ref existenceAt, _options.FromMs);
            Regenerate(moments[momentAt - 1], initial: true);
        }

        var fireMs = _options.FromMs;
        while (fireMs < _options.ToMs)
        {
            _nowMs = fireMs + (_options.Timing is { JitterMs: > 0 } timing ? _random.NextInt64(timing.JitterMs) : 0);
            _workCursorMs = _nowMs;
            ApplyExistence(existenceEvents, ref existenceAt, _nowMs);
            UpdatePhantoms(population, ref populationAt);

            while (wipeAt < wipeEvents.Count && wipeEvents[wipeAt].AtMs <= _nowMs)
            {
                var (_, serverIndex, nextWipe) = wipeEvents[wipeAt++];
                _servers[serverIndex].NextWipeAtMs = nextWipe;
            }

            while (exogenousAt < exogenous.Count && exogenous[exogenousAt].AtMs <= _nowMs)
            {
                ApplyExogenous(exogenous[exogenousAt++]);
            }

            while (momentAt < moments.Length && moments[momentAt] <= _nowMs)
            {
                Regenerate(moments[momentAt++], initial: false);
            }

            _strategy.Tick(_nowMs, _servers, this);
            _result.RecordPopulation(_nowMs, _knownExisting, _activePhantoms.Count);

            // The next timer tick; if the work ran past it, the one kept missed tick fires when the work ends.
            var nextGrid = _options.FromMs + ((fireMs - _options.FromMs) / _options.TickMs + 1) * _options.TickMs;
            fireMs = _workCursorMs > nextGrid ? _workCursorMs : nextGrid;
            _result.RecordTick(fireMs - _nowMs);
        }

        return _result;
    }

    void IPollExecutor.Poll(IReadOnlyList<PolledServer> servers, string lane)
    {
        var timing = _options.Timing;
        if (timing is null)
        {
            foreach (var server in servers)
            {
                Apply(server, _oracle.Answer(server.Index, _nowMs, server), lane, exogenous: false);
            }

            return;
        }

        // The lane starts when the previous lane of this tick finished. With N queries in flight, the
        // i-th finishes after its own duration or after 1/N of the work queued up to it, whichever is later.
        var laneStart = _workCursorMs;
        var queuedSeconds = 0.0;
        var laneSeconds = 0.0;
        foreach (var server in servers)
        {
            var answered = _oracle.Answer(server.Index, laneStart, server).Answered;
            var cost = answered ? timing.AnsweredPollSeconds : timing.UnansweredPollSeconds;
            queuedSeconds += cost;
            var doneSeconds = Math.Max(cost, queuedSeconds / timing.Concurrency);
            laneSeconds = Math.Max(laneSeconds, doneSeconds);

            var atMs = laneStart + (long)(doneSeconds * 1000);
            Apply(server, _oracle.Answer(server.Index, atMs, server), lane, exogenous: false);
        }

        _workCursorMs = laneStart + (long)(laneSeconds * 1000);
    }

    private void Apply(PolledServer server, PollOutcome outcome, string lane, bool exogenous)
    {
        int recordedPlayers;
        if (server.LastCheckedAtMs is { } lastChecked && outcome.AtMs < lastChecked)
        {
            // A replayed poll older than a poll of the previous (long) tick: in production the later
            // write wins, so it leaves the state alone but is still a snapshot.
            recordedPlayers = outcome.Answered ? outcome.Players : server.Players;
        }
        else
        {
            recordedPlayers = PollStateRules.Apply(server, outcome, _options.OfflineThresholdMs);
        }

        server.AddSnapshot(outcome.AtMs, recordedPlayers, _options.RecentSnapshotKeepMs);
        _histories[server.Index]?.Add(outcome.AtMs, recordedPlayers, server.MaxPlayers, outcome.Answered);

        var sampleIdx = server.Index < _knownCount ? _oracle.SampleIdxOf(server.Index) : 0;
        _result.RecordPoll(outcome.AtMs, lane, outcome.Answered, exogenous, sampleIdx);

        // What the sample server shows: the strategy's own answered polls and replayed Discovery polls.
        if (sampleIdx > 0 && outcome.Answered && lane != WatchedLane)
        {
            _result.RecordShown(sampleIdx, outcome.AtMs, outcome.Players);
        }

        _strategy.OnPolled(server, outcome);
    }

    private void ApplyExogenous(ExogenousPoll poll)
    {
        var server = _servers[poll.ServerIndex];
        if (!server.Exists)
        {
            return;
        }

        // Replayed as production recorded it: its answer, or a failed query.
        Apply(server, new PollOutcome(poll.AtMs, poll.Answered, poll.Players, poll.MaxPlayers, 0), poll.Lane, exogenous: true);
    }

    private void Regenerate(long atMs, bool initial)
    {
        var ranked = RankingReplay.Compute(atMs, _servers, _histories, _options.RankingOptions);

        // Production keeps the old ranking when the new one is empty.
        if (ranked.Count > 0)
        {
            foreach (var server in _servers)
            {
                server.Rank = null;
            }

            for (var rank = 0; rank < ranked.Count; rank++)
            {
                _servers[ranked[rank]].Rank = rank + 1;
            }
        }

        _result.RecordRanking(atMs, initial, ranked.Count, _data.SampleServers
            .Select(sample => _servers[sample.ServerIndex].Rank)
            .ToArray());
    }

    /// <summary>
    /// Creates a slot per known server with its starting state. Only immutable fields come from the
    /// <c>servers</c> file (type, created_at); the state comes from the last snapshot before the period.
    /// </summary>
    private (long StartMs, long EndMs)[] BuildKnownServers()
    {
        var lifetimes = new (long StartMs, long EndMs)[_knownCount];
        var historyStart = _options.FromMs - _options.RankingOptions.RankingWindowDays * 86_400_000L;

        for (var i = 0; i < _knownCount; i++)
        {
            var record = _data.Servers[i];
            var snapshots = _data.Snapshots[i];
            long? firstSnapshot = snapshots.Count > 0 ? snapshots.TimesMs[0] : null;
            long? lastSnapshot = snapshots.Count > 0 ? snapshots.TimesMs[^1] : null;

            // A known server exists from created_at or its first snapshot, whichever is earlier, until it is
            // in the latest servers file; a pruned one until its last snapshot.
            long start = Math.Min(
                record?.CreatedAtUtc.ToUnixTimeMilliseconds() ?? long.MaxValue,
                firstSnapshot ?? long.MaxValue);
            long end = record?.InLatestExport == true ? long.MaxValue : lastSnapshot ?? long.MinValue;
            lifetimes[i] = (start, end);

            var server = new PolledServer(
                i,
                _data.ServerIndex.Ids[i],
                record?.ServerType ?? RustServerType.Unknown,
                start == long.MaxValue ? _options.FromMs : start);

            var history = new ServerHistory();
            var lastBefore = -1;
            long? lastSeen = null;
            for (var s = 0; s < snapshots.Count && snapshots.TimesMs[s] < _options.FromMs; s++)
            {
                lastBefore = s;
                if (snapshots.Answered(s))
                {
                    lastSeen = snapshots.TimesMs[s];
                }

                if (snapshots.TimesMs[s] >= historyStart)
                {
                    history.Add(snapshots.TimesMs[s], snapshots.Players[s], snapshots.MaxPlayers[s], snapshots.Answered(s));
                }

                if (snapshots.TimesMs[s] >= _options.FromMs - _options.RecentSnapshotKeepMs)
                {
                    server.AddSnapshot(snapshots.TimesMs[s], snapshots.Players[s], _options.RecentSnapshotKeepMs);
                }
            }

            // Without a snapshot before the period the server starts as never polled.
            if (lastBefore >= 0)
            {
                server.LastCheckedAtMs = snapshots.TimesMs[lastBefore];
                server.LastSeenAtMs = lastSeen;
                server.Status = snapshots.Online[lastBefore] ? ServerStatus.Online : ServerStatus.Offline;
                server.Players = snapshots.Players[lastBefore];
                server.MaxPlayers = snapshots.MaxPlayers[lastBefore];
            }

            _servers.Add(server);
            _histories.Add(history);
        }

        return lifetimes;
    }

    private static List<(long AtMs, int Index, bool Appears)> ExistenceEvents((long StartMs, long EndMs)[] lifetimes)
    {
        var events = new List<(long AtMs, int Index, bool Appears)>();
        for (var i = 0; i < lifetimes.Length; i++)
        {
            var (start, end) = lifetimes[i];
            if (start == long.MaxValue || end < start)
            {
                continue;
            }

            events.Add((start, i, true));
            if (end != long.MaxValue)
            {
                // Exists through its last snapshot; gone from the next step on.
                events.Add((end + 1, i, false));
            }
        }

        return events.OrderBy(e => e.AtMs).ThenBy(e => e.Index).ThenBy(e => e.Appears).ToList();
    }

    private void ApplyExistence(List<(long AtMs, int Index, bool Appears)> events, ref int at, long nowMs)
    {
        while (at < events.Count && events[at].AtMs <= nowMs)
        {
            var (_, index, appears) = events[at++];
            var server = _servers[index];
            if (server.Exists != appears)
            {
                server.Exists = appears;
                _knownExisting += appears ? 1 : -1;
            }
        }
    }

    /// <summary>
    /// Fills the part of the population the export lost (plan 5.5): phantoms(t) = max(0, TotalServers(t) - known(t)).
    /// Phantoms never answer, start never polled and join and leave first in, first out.
    /// </summary>
    private void UpdatePhantoms((long AtMs, int TotalServers)[] population, ref int at)
    {
        if (population.Length == 0)
        {
            return;
        }

        while (at + 1 < population.Length && population[at + 1].AtMs <= _nowMs)
        {
            at++;
        }

        var target = Math.Max(0, population[at].TotalServers - _knownExisting);
        while (_activePhantoms.Count > target)
        {
            var leaving = _activePhantoms.Dequeue();
            leaving.Exists = false;
            _freePhantomSlots.Enqueue(leaving);
        }

        while (_activePhantoms.Count < target)
        {
            var slot = _freePhantomSlots.Count > 0
                ? _freePhantomSlots.Dequeue()
                : AddPhantomSlot();
            slot.Reset(NewGuid(), RustServerType.Unknown, _nowMs);
            slot.Exists = true;
            _strategy.OnServerReset(slot);
            _activePhantoms.Enqueue(slot);
        }
    }

    private PolledServer AddPhantomSlot()
    {
        var slot = new PolledServer(_servers.Count, Guid.Empty, RustServerType.Unknown, _nowMs);
        _servers.Add(slot);
        _histories.Add(null);
        return slot;
    }

    private Guid NewGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        _random.NextBytes(bytes);
        return new Guid(bytes);
    }

    private List<ExogenousPoll> ExogenousPolls()
    {
        var polls = new List<ExogenousPoll>();
        for (var i = 0; i < _knownCount; i++)
        {
            var snapshots = _data.Snapshots[i];
            for (var s = 0; s < snapshots.Count; s++)
            {
                var source = snapshots.Sources[s];
                if ((source == ServerPollSource.Watched || source == ServerPollSource.Discovery)
                    && snapshots.TimesMs[s] >= _options.FromMs
                    && snapshots.TimesMs[s] < _options.ToMs)
                {
                    polls.Add(new ExogenousPoll(
                        snapshots.TimesMs[s],
                        i,
                        source.ToString(),
                        snapshots.Answered(s),
                        snapshots.Players[s],
                        snapshots.MaxPlayers[s]));
                }
            }
        }

        return polls.OrderBy(poll => poll.AtMs).ThenBy(poll => poll.ServerIndex).ToList();
    }

    /// <summary>
    /// RustIQ's stored next wipe over time, for the strategies that use it. A wipe observation records
    /// what RustIQ showed just before it (<c>previous_next_wipe_at</c>); that value is taken to hold from
    /// the server's previous observation on.
    /// </summary>
    private List<(long AtMs, int ServerIndex, long? NextWipeAtMs)> NextWipeEvents()
    {
        var events = new List<(long AtMs, int ServerIndex, long? NextWipeAtMs)>();
        foreach (var group in _data.WipeObservations.GroupBy(row => row.ServerId))
        {
            if (!_data.ServerIndex.TryGet(group.Key, out var index))
            {
                continue;
            }

            var previousAt = long.MinValue;
            foreach (var observation in group.OrderBy(row => row.ObservedAtUtc).ThenBy(row => row.Id))
            {
                events.Add((previousAt, index, observation.PreviousNextWipeAtUtc?.ToUnixTimeMilliseconds()));
                previousAt = observation.ObservedAtUtc.ToUnixTimeMilliseconds();
            }
        }

        return events.OrderBy(e => e.AtMs).ThenBy(e => e.ServerIndex).ToList();
    }

    private readonly record struct ExogenousPoll(
        long AtMs,
        int ServerIndex,
        string Lane,
        bool Answered,
        int Players,
        int MaxPlayers);
}
