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
    public Action<long, IReadOnlyList<PolledServer>>? OnTick { get; init; }
    public long OfflineThresholdMs { get; init; } = PollStateRules.DefaultOfflineThresholdMs;
    public long RecentSnapshotKeepMs { get; init; } = 2 * 3_600_000L;
    public ServerRankingOptions RankingOptions { get; init; } = new ServerRankingOptions();
    public WorkerTiming? Timing { get; init; } = new WorkerTiming();
}

public sealed class WorkerTiming
{
    public int Concurrency { get; init; } = 32;
    public double AnsweredPollSeconds { get; init; } = 0.5;
    public double UnansweredPollSeconds { get; init; } = 6;
    public long JitterMs { get; init; } = 50;
}

public sealed class SimulationEngine : IPollExecutor
{
    public const string WatchedLane = "Watched";
    public const string DiscoveryLane = "Discovery";

    public const int AnsweringPhantomMaxPlayers = 100;

    private const long InitialPhantomSpreadMs = 75 * 60_000L;

    private readonly SimulatorData _data;
    private readonly IPollingStrategy _strategy;
    private readonly SimulationOptions _options;
    private readonly ResponseOracle _oracle;
    private readonly List<PolledServer> _servers = new List<PolledServer>();
    private readonly List<ServerHistory?> _histories = new List<ServerHistory?>();
    private readonly SimulationResult _result;
    private readonly Random _random;
    private readonly int _knownCount;
    private readonly Queue<PolledServer> _activePhantoms = new Queue<PolledServer>();
    private readonly Queue<PolledServer> _answeringPhantoms = new Queue<PolledServer>();
    private readonly Queue<PolledServer> _freePhantomSlots = new Queue<PolledServer>();
    private readonly HashSet<int> _answeringPhantomIndexes = new HashSet<int>();
    private long[] _knownAnsweringHourMs = new long[0];
    private int[] _knownAnsweringPerHour = new int[0];
    private bool _phantomsFilled;
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

    public IReadOnlyList<PolledServer> Servers
    {
        get { return _servers; }
    }

    public SimulationResult Run()
    {
        Lifetime[] lifetimes = BuildKnownServers();
        List<ExistenceEvent> existenceEvents = ExistenceEvents(lifetimes);
        List<ExogenousPoll> exogenous = ExogenousPolls();
        List<WipeEvent> wipeEvents = NextWipeEvents();

        IReadOnlyList<long> momentList;
        if (_options.Ranking == RankingMode.Frozen)
        {
            momentList = RankingReplay.FrozenMoments(_data);
        }
        else
        {
            momentList = RankingReplay.HourlyMoments(_data, _options.FromMs, _options.ToMs);
        }

        long[] moments = new long[momentList.Count];
        for (int i = 0; i < momentList.Count; i++)
        {
            moments[i] = momentList[i];
        }

        Array.Sort(moments);

        PopulationPoint[] population = new PopulationPoint[_data.PopulationSamples.Count];
        for (int i = 0; i < _data.PopulationSamples.Count; i++)
        {
            PopulationSample sample = _data.PopulationSamples[i];
            PopulationPoint point = new PopulationPoint();
            point.AtMs = sample.SampledAtUtc.ToUnixTimeMilliseconds();
            point.TotalServers = sample.TotalServers;
            point.OnlineServers = sample.OnlineServers;
            population[i] = point;
        }

        CountKnownAnswering(lifetimes);

        int existenceAt = 0;
        int exogenousAt = 0;
        int wipeAt = 0;
        int populationAt = 0;

        int momentAt = 0;
        while (momentAt < moments.Length && moments[momentAt] <= _options.FromMs)
        {
            momentAt++;
        }

        if (momentAt > 0)
        {
            ApplyExistence(existenceEvents, ref existenceAt, _options.FromMs);
            Regenerate(moments[momentAt - 1], true);
        }

        long fireMs = _options.FromMs;
        while (fireMs < _options.ToMs)
        {
            long jitter = 0;
            WorkerTiming? timing = _options.Timing;
            if (timing != null && timing.JitterMs > 0)
            {
                jitter = _random.NextInt64(timing.JitterMs);
            }

            _nowMs = fireMs + jitter;
            _workCursorMs = _nowMs;
            ApplyExistence(existenceEvents, ref existenceAt, _nowMs);
            UpdatePhantoms(population, ref populationAt);

            while (wipeAt < wipeEvents.Count && wipeEvents[wipeAt].AtMs <= _nowMs)
            {
                WipeEvent wipeEvent = wipeEvents[wipeAt];
                wipeAt++;
                _servers[wipeEvent.ServerIndex].NextWipeAtMs = wipeEvent.NextWipeAtMs;
            }

            while (exogenousAt < exogenous.Count && exogenous[exogenousAt].AtMs <= _nowMs)
            {
                ApplyExogenous(exogenous[exogenousAt]);
                exogenousAt++;
            }

            while (momentAt < moments.Length && moments[momentAt] <= _nowMs)
            {
                Regenerate(moments[momentAt], false);
                momentAt++;
            }

            _strategy.Tick(_nowMs, _servers, this);
            if (_options.OnTick != null)
            {
                _options.OnTick(_nowMs, _servers);
            }

            _result.RecordPopulation(_nowMs, _knownExisting, _activePhantoms.Count + _answeringPhantoms.Count);

            long nextGrid = _options.FromMs + ((fireMs - _options.FromMs) / _options.TickMs + 1) * _options.TickMs;
            if (_workCursorMs > nextGrid)
            {
                fireMs = _workCursorMs;
            }
            else
            {
                fireMs = nextGrid;
            }

            _result.RecordTick(fireMs - _nowMs);
        }

        return _result;
    }

    void IPollExecutor.Poll(IReadOnlyList<PolledServer> servers, string lane)
    {
        WorkerTiming? timing = _options.Timing;
        if (timing == null)
        {
            foreach (PolledServer server in servers)
            {
                Apply(server, AnswerOf(server, _nowMs), lane, false);
            }

            return;
        }

        long laneStart = _workCursorMs;
        double queuedSeconds = 0.0;
        double laneSeconds = 0.0;
        foreach (PolledServer server in servers)
        {
            bool answered = AnswerOf(server, laneStart).Answered;
            double cost;
            if (answered)
            {
                cost = timing.AnsweredPollSeconds;
            }
            else
            {
                cost = timing.UnansweredPollSeconds;
            }

            queuedSeconds += cost;
            double doneSeconds = Math.Max(cost, queuedSeconds / timing.Concurrency);
            laneSeconds = Math.Max(laneSeconds, doneSeconds);

            long atMs = laneStart + (long)(doneSeconds * 1000);
            Apply(server, AnswerOf(server, atMs), lane, false);
        }

        _workCursorMs = laneStart + (long)(laneSeconds * 1000);
    }

    private void Apply(PolledServer server, PollOutcome outcome, string lane, bool exogenous)
    {
        int recordedPlayers;
        long? lastChecked = server.LastCheckedAtMs;
        if (lastChecked != null && outcome.AtMs < lastChecked.Value)
        {
            if (outcome.Answered)
            {
                recordedPlayers = outcome.Players;
            }
            else
            {
                recordedPlayers = server.Players;
            }
        }
        else
        {
            recordedPlayers = PollStateRules.Apply(server, outcome, _options.OfflineThresholdMs);
        }

        server.AddSnapshot(outcome.AtMs, recordedPlayers, _options.RecentSnapshotKeepMs);

        ServerHistory? history = _histories[server.Index];
        if (history != null)
        {
            history.Add(outcome.AtMs, recordedPlayers, server.MaxPlayers, outcome.Answered);
        }

        int sampleIdx = 0;
        if (server.Index < _knownCount)
        {
            sampleIdx = _oracle.SampleIdxOf(server.Index);
        }

        _result.RecordPoll(outcome.AtMs, lane, outcome.Answered, exogenous, sampleIdx);

        if (sampleIdx > 0 && outcome.Answered && lane != WatchedLane)
        {
            _result.RecordShown(sampleIdx, outcome.AtMs, outcome.Players);
        }

        _strategy.OnPolled(server, outcome);
    }

    private void ApplyExogenous(ExogenousPoll poll)
    {
        PolledServer server = _servers[poll.ServerIndex];
        if (!server.Exists)
        {
            return;
        }

        PollOutcome outcome = new PollOutcome(poll.AtMs, poll.Answered, poll.Players, poll.MaxPlayers, 0);
        Apply(server, outcome, poll.Lane, true);
    }

    private void Regenerate(long atMs, bool initial)
    {
        IReadOnlyList<int> ranked = RankingReplay.Compute(atMs, _servers, _histories, _options.RankingOptions);

        if (ranked.Count > 0)
        {
            foreach (PolledServer server in _servers)
            {
                server.Rank = null;
            }

            for (int rank = 0; rank < ranked.Count; rank++)
            {
                _servers[ranked[rank]].Rank = rank + 1;
            }
        }

        int?[] sampleRanks = new int?[_data.SampleServers.Count];
        for (int i = 0; i < _data.SampleServers.Count; i++)
        {
            SampleServer sample = _data.SampleServers[i];
            sampleRanks[i] = _servers[sample.ServerIndex].Rank;
        }

        _result.RecordRanking(atMs, initial, ranked.Count, sampleRanks);
    }

    private Lifetime[] BuildKnownServers()
    {
        Lifetime[] lifetimes = new Lifetime[_knownCount];
        long historyStart = _options.FromMs - _options.RankingOptions.RankingWindowDays * 86_400_000L;

        for (int i = 0; i < _knownCount; i++)
        {
            ServerRecord? record = _data.Servers[i];
            SnapshotSeries snapshots = _data.Snapshots[i];

            long createdMs = long.MaxValue;
            if (record != null)
            {
                createdMs = record.CreatedAtUtc.ToUnixTimeMilliseconds();
            }

            long firstSnapshotMs = long.MaxValue;
            if (snapshots.Count > 0)
            {
                firstSnapshotMs = snapshots.TimesMs[0];
            }

            long start = Math.Min(createdMs, firstSnapshotMs);

            long end;
            if (record != null && record.InLatestExport)
            {
                end = long.MaxValue;
            }
            else if (snapshots.Count > 0)
            {
                end = snapshots.TimesMs[snapshots.TimesMs.Length - 1];
            }
            else
            {
                end = long.MinValue;
            }

            Lifetime lifetime = new Lifetime();
            lifetime.StartMs = start;
            lifetime.EndMs = end;
            lifetimes[i] = lifetime;

            RustServerType serverType = RustServerType.Unknown;
            if (record != null)
            {
                serverType = record.ServerType;
            }

            long createdAtMs = start;
            if (start == long.MaxValue)
            {
                createdAtMs = _options.FromMs;
            }

            PolledServer server = new PolledServer(i, _data.ServerIndex.Ids[i], serverType, createdAtMs);

            ServerHistory history = new ServerHistory();
            int lastBefore = -1;
            long? lastSeen = null;
            for (int s = 0; s < snapshots.Count && snapshots.TimesMs[s] < _options.FromMs; s++)
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

            if (lastBefore >= 0)
            {
                server.LastCheckedAtMs = snapshots.TimesMs[lastBefore];
                server.LastSeenAtMs = lastSeen;
                if (snapshots.Online[lastBefore])
                {
                    server.Status = ServerStatus.Online;
                }
                else
                {
                    server.Status = ServerStatus.Offline;
                }

                server.Players = snapshots.Players[lastBefore];
                server.MaxPlayers = snapshots.MaxPlayers[lastBefore];
            }

            _servers.Add(server);
            _histories.Add(history);
        }

        return lifetimes;
    }

    private static List<ExistenceEvent> ExistenceEvents(Lifetime[] lifetimes)
    {
        List<ExistenceEvent> events = new List<ExistenceEvent>();
        for (int i = 0; i < lifetimes.Length; i++)
        {
            long start = lifetimes[i].StartMs;
            long end = lifetimes[i].EndMs;
            if (start == long.MaxValue || end < start)
            {
                continue;
            }

            ExistenceEvent appear = new ExistenceEvent();
            appear.AtMs = start;
            appear.Index = i;
            appear.Appears = true;
            events.Add(appear);

            if (end != long.MaxValue)
            {
                ExistenceEvent disappear = new ExistenceEvent();
                disappear.AtMs = end + 1;
                disappear.Index = i;
                disappear.Appears = false;
                events.Add(disappear);
            }
        }

        events.Sort(CompareExistenceEvents);
        return events;
    }

    private static int CompareExistenceEvents(ExistenceEvent a, ExistenceEvent b)
    {
        if (a.AtMs < b.AtMs)
        {
            return -1;
        }

        if (a.AtMs > b.AtMs)
        {
            return 1;
        }

        if (a.Index < b.Index)
        {
            return -1;
        }

        if (a.Index > b.Index)
        {
            return 1;
        }

        if (!a.Appears && b.Appears)
        {
            return -1;
        }

        if (a.Appears && !b.Appears)
        {
            return 1;
        }

        return 0;
    }

    private void ApplyExistence(List<ExistenceEvent> events, ref int at, long nowMs)
    {
        while (at < events.Count && events[at].AtMs <= nowMs)
        {
            ExistenceEvent existenceEvent = events[at];
            at++;
            PolledServer server = _servers[existenceEvent.Index];
            if (server.Exists != existenceEvent.Appears)
            {
                server.Exists = existenceEvent.Appears;
                if (existenceEvent.Appears)
                {
                    _knownExisting += 1;
                }
                else
                {
                    _knownExisting -= 1;
                }
            }
        }
    }

    private PollOutcome AnswerOf(PolledServer server, long atMs)
    {
        if (server.Index >= _knownCount)
        {
            if (_answeringPhantomIndexes.Contains(server.Index))
            {
                return new PollOutcome(atMs, true, 0, AnsweringPhantomMaxPlayers, 0);
            }

            return new PollOutcome(atMs, false, 0, 0, 0);
        }

        return _oracle.Answer(server.Index, atMs, server);
    }

    private void CountKnownAnswering(Lifetime[] lifetimes)
    {
        int hours = (int)((_options.ToMs - _options.FromMs + 3_599_999L) / 3_600_000L);
        _knownAnsweringHourMs = new long[hours];
        _knownAnsweringPerHour = new int[hours];
        for (int h = 0; h < hours; h++)
        {
            long atMs = _options.FromMs + h * 3_600_000L;
            int answering = 0;
            for (int i = 0; i < _knownCount; i++)
            {
                long start = lifetimes[i].StartMs;
                long end = lifetimes[i].EndMs;
                if (start <= atMs && atMs <= end && _oracle.Answer(i, atMs, _servers[i]).Answered)
                {
                    answering++;
                }
            }

            _knownAnsweringHourMs[h] = atMs;
            _knownAnsweringPerHour[h] = answering;
        }
    }

    private int KnownAnsweringAt(long atMs)
    {
        if (_knownAnsweringPerHour.Length == 0)
        {
            return 0;
        }

        long hour = (atMs - _options.FromMs) / 3_600_000L;
        if (hour < 0)
        {
            hour = 0;
        }

        if (hour > _knownAnsweringPerHour.Length - 1)
        {
            hour = _knownAnsweringPerHour.Length - 1;
        }

        return _knownAnsweringPerHour[(int)hour];
    }

    private void UpdatePhantoms(PopulationPoint[] population, ref int at)
    {
        if (population.Length == 0)
        {
            return;
        }

        while (at + 1 < population.Length && population[at + 1].AtMs <= _nowMs)
        {
            at++;
        }

        int total = Math.Max(0, population[at].TotalServers - _knownExisting);
        int answering = Math.Clamp(population[at].OnlineServers - KnownAnsweringAt(_nowMs), 0, total);
        int silent = total - answering;

        while (_answeringPhantoms.Count > answering && _activePhantoms.Count < silent)
        {
            PolledServer slot = _answeringPhantoms.Dequeue();
            _answeringPhantomIndexes.Remove(slot.Index);
            _activePhantoms.Enqueue(slot);
        }

        while (_activePhantoms.Count > silent && _answeringPhantoms.Count < answering)
        {
            PolledServer slot = _activePhantoms.Dequeue();
            _answeringPhantomIndexes.Add(slot.Index);
            _answeringPhantoms.Enqueue(slot);
        }

        Shrink(_activePhantoms, silent);
        Shrink(_answeringPhantoms, answering);

        while (_activePhantoms.Count < silent)
        {
            _activePhantoms.Enqueue(NewPhantom(false));
        }

        while (_answeringPhantoms.Count < answering)
        {
            _answeringPhantoms.Enqueue(NewPhantom(true));
        }

        _phantomsFilled = true;
    }

    private void Shrink(Queue<PolledServer> phantoms, int target)
    {
        while (phantoms.Count > target)
        {
            PolledServer leaving = phantoms.Dequeue();
            leaving.Exists = false;
            _answeringPhantomIndexes.Remove(leaving.Index);
            _freePhantomSlots.Enqueue(leaving);
        }
    }

    private PolledServer NewPhantom(bool answers)
    {
        PolledServer slot;
        if (_freePhantomSlots.Count > 0)
        {
            slot = _freePhantomSlots.Dequeue();
        }
        else
        {
            slot = AddPhantomSlot();
        }

        Guid id = NewGuid();
        slot.Reset(id, RustServerType.Unknown, _nowMs);
        if (answers)
        {
            _answeringPhantomIndexes.Add(slot.Index);
            if (!_phantomsFilled)
            {
                long checkedAt = _nowMs - _random.NextInt64(InitialPhantomSpreadMs);
                slot.LastCheckedAtMs = checkedAt;
                slot.LastSeenAtMs = checkedAt;
                slot.Status = ServerStatus.Online;
                slot.MaxPlayers = AnsweringPhantomMaxPlayers;
            }
        }

        slot.Exists = true;
        _strategy.OnServerReset(slot);
        return slot;
    }

    private PolledServer AddPhantomSlot()
    {
        PolledServer slot = new PolledServer(_servers.Count, Guid.Empty, RustServerType.Unknown, _nowMs);
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
        List<ExogenousPoll> polls = new List<ExogenousPoll>();
        for (int i = 0; i < _knownCount; i++)
        {
            SnapshotSeries snapshots = _data.Snapshots[i];
            for (int s = 0; s < snapshots.Count; s++)
            {
                ServerPollSource source = snapshots.Sources[s];
                bool exogenousSource = source == ServerPollSource.Watched || source == ServerPollSource.Discovery;
                if (exogenousSource
                    && snapshots.TimesMs[s] >= _options.FromMs
                    && snapshots.TimesMs[s] < _options.ToMs)
                {
                    ExogenousPoll poll = new ExogenousPoll();
                    poll.AtMs = snapshots.TimesMs[s];
                    poll.ServerIndex = i;
                    poll.Lane = source.ToString();
                    poll.Answered = snapshots.Answered(s);
                    poll.Players = snapshots.Players[s];
                    poll.MaxPlayers = snapshots.MaxPlayers[s];
                    poll.Order = polls.Count;
                    polls.Add(poll);
                }
            }
        }

        polls.Sort(CompareExogenousPolls);
        return polls;
    }

    private static int CompareExogenousPolls(ExogenousPoll a, ExogenousPoll b)
    {
        if (a.AtMs < b.AtMs)
        {
            return -1;
        }

        if (a.AtMs > b.AtMs)
        {
            return 1;
        }

        if (a.ServerIndex < b.ServerIndex)
        {
            return -1;
        }

        if (a.ServerIndex > b.ServerIndex)
        {
            return 1;
        }

        if (a.Order < b.Order)
        {
            return -1;
        }

        if (a.Order > b.Order)
        {
            return 1;
        }

        return 0;
    }

    private List<WipeEvent> NextWipeEvents()
    {
        Dictionary<Guid, List<WipeObservationRow>> groups = new Dictionary<Guid, List<WipeObservationRow>>();
        List<Guid> groupOrder = new List<Guid>();
        foreach (WipeObservationRow row in _data.WipeObservations)
        {
            List<WipeObservationRow>? group;
            if (!groups.TryGetValue(row.ServerId, out group))
            {
                group = new List<WipeObservationRow>();
                groups.Add(row.ServerId, group);
                groupOrder.Add(row.ServerId);
            }

            group.Add(row);
        }

        List<WipeEvent> events = new List<WipeEvent>();
        foreach (Guid serverId in groupOrder)
        {
            int index;
            if (!_data.ServerIndex.TryGet(serverId, out index))
            {
                continue;
            }

            List<WipeObservationRow> observations = groups[serverId];
            SortObservations(observations);

            long previousAt = long.MinValue;
            foreach (WipeObservationRow observation in observations)
            {
                long? nextWipe = null;
                if (observation.PreviousNextWipeAtUtc != null)
                {
                    nextWipe = observation.PreviousNextWipeAtUtc.Value.ToUnixTimeMilliseconds();
                }

                WipeEvent wipeEvent = new WipeEvent();
                wipeEvent.AtMs = previousAt;
                wipeEvent.ServerIndex = index;
                wipeEvent.NextWipeAtMs = nextWipe;
                wipeEvent.Order = events.Count;
                events.Add(wipeEvent);

                previousAt = observation.ObservedAtUtc.ToUnixTimeMilliseconds();
            }
        }

        events.Sort(CompareWipeEvents);
        return events;
    }

    private static void SortObservations(List<WipeObservationRow> observations)
    {
        for (int i = 1; i < observations.Count; i++)
        {
            WipeObservationRow current = observations[i];
            int j = i - 1;
            while (j >= 0 && CompareObservations(observations[j], current) > 0)
            {
                observations[j + 1] = observations[j];
                j--;
            }

            observations[j + 1] = current;
        }
    }

    private static int CompareObservations(WipeObservationRow a, WipeObservationRow b)
    {
        int byTime = a.ObservedAtUtc.CompareTo(b.ObservedAtUtc);
        if (byTime != 0)
        {
            return byTime;
        }

        return a.Id.CompareTo(b.Id);
    }

    private static int CompareWipeEvents(WipeEvent a, WipeEvent b)
    {
        if (a.AtMs < b.AtMs)
        {
            return -1;
        }

        if (a.AtMs > b.AtMs)
        {
            return 1;
        }

        if (a.ServerIndex < b.ServerIndex)
        {
            return -1;
        }

        if (a.ServerIndex > b.ServerIndex)
        {
            return 1;
        }

        if (a.Order < b.Order)
        {
            return -1;
        }

        if (a.Order > b.Order)
        {
            return 1;
        }

        return 0;
    }

    private sealed class Lifetime
    {
        public long StartMs;
        public long EndMs;
    }

    private sealed class ExistenceEvent
    {
        public long AtMs;
        public int Index;
        public bool Appears;
    }

    private sealed class PopulationPoint
    {
        public long AtMs;
        public int TotalServers;
        public int OnlineServers;
    }

    private sealed class WipeEvent
    {
        public long AtMs;
        public int ServerIndex;
        public long? NextWipeAtMs;
        public int Order;
    }

    private sealed class ExogenousPoll
    {
        public long AtMs;
        public int ServerIndex;
        public string Lane = "";
        public bool Answered;
        public int Players;
        public int MaxPlayers;
        public int Order;
    }
}
