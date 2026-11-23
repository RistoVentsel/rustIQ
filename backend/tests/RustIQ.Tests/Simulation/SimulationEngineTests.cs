using RustIQ.Application.Servers;
using RustIQ.Application.Servers.Polling;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Engine;

namespace RustIQ.Tests.Simulation;

public sealed class SimulationEngineTests
{
    private const long Start = 1_800_000_000_000;
    private const long Minute = 60_000;

    private static readonly Guid SampleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void Oracle_AnswersSampleServersFromTheLatestProbeInTheLast20Seconds()
    {
        var data = Data(probes: Probes((Start - 5_000, true, 40), (Start + 10_000, false, 0)));
        var oracle = new ResponseOracle(data);
        var state = new PolledServer(0, SampleId, RustServerType.Community, 0);

        var answered = oracle.Answer(0, Start, state);
        Assert.True(answered.Answered);
        Assert.Equal(40, answered.Players);
        Assert.Equal(100, answered.MaxPlayers);

        Assert.False(oracle.Answer(0, Start + 10_000, state).Answered);
        Assert.False(oracle.Answer(0, Start + 15_000 + 20_000 + 1, state).Answered);
    }

    [Fact]
    public void Oracle_AnswersOtherServersFromTheNearestSnapshot()
    {
        var data = Data(otherSnapshots: Snapshots(
            (Start, 10, true, ServerPollSource.NormalOnlineLane),
            (Start + 10 * Minute, 0, false, ServerPollSource.NormalOnlineLane)));
        var oracle = new ResponseOracle(data);
        var state = new PolledServer(1, OtherId, RustServerType.Community, 0);

        Assert.True(oracle.Answer(1, Start + 4 * Minute, state).Answered);
        Assert.Equal(10, oracle.Answer(1, Start - 60 * Minute, state).Players);
        Assert.False(oracle.Answer(1, Start + 6 * Minute, state).Answered);
        Assert.False(oracle.Answer(99, Start, state).Answered);
    }

    [Fact]
    public void Engine_FillsThePopulationWithPhantomsAndRecordsSampleServerPolls()
    {
        var probes = Probes(Enumerable.Range(0, 40).Select(i => (Start + i * 15_000L, true, (short)20)).ToArray());
        var data = Data(probes: probes, totalServers: 5);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(4), Options(Start, Start + 10 * Minute));

        var result = engine.Run();

        Assert.Equal(5, engine.Servers.Count);
        Assert.Equal(3, engine.Servers.Count(server => server.Index >= 2 && server.Exists));
        Assert.Equal((2.0, 3.0), result.AveragePopulation(Start, Start + 10 * Minute));
        Assert.Equal(40, result.Lanes[UniformPollingStrategy.Lane].Polls.Sum());
        Assert.Equal(
            new long[] { 0, 2, 7, 12, 17, 22, 27, 32, 37 },
            result.Shown(1).TimesMs.Select(t => (t - Start) / 15_000));
    }

    [Fact]
    public void Engine_LetsPhantomsAnswerSoTheOnlineCountFollowsProduction()
    {
        var probes = Probes(Enumerable.Range(0, 8).Select(i => (Start + i * 15_000L, true, (short)20)).ToArray());
        var data = Data(probes: probes, population: [Population(Start, 6, 3)]);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(24), Options(Start, Start + 2 * Minute));

        engine.Run();

        var phantoms = engine.Servers.Where(server => server.Index >= 2 && server.Exists).ToArray();
        Assert.Equal(4, phantoms.Length);
        Assert.Equal(2, phantoms.Count(server => server.Status == ServerStatus.Online));
        Assert.Equal(2, phantoms.Count(server => server.Status == ServerStatus.Offline));
        Assert.All(phantoms.Where(server => server.Status == ServerStatus.Online), server => Assert.Equal(0, server.Players));
    }

    [Fact]
    public void Engine_TurnsAnsweringPhantomsSilentWhenProductionsOnlineCountFalls()
    {
        var probes = Probes(Enumerable.Range(0, 60).Select(i => (Start + i * 15_000L, true, (short)20)).ToArray());
        var data = Data(probes: probes, population:
        [
            Population(Start, 6, 3),
            Population(Start + 2 * Minute, 6, 1)
        ]);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(24), Options(Start, Start + 14 * Minute));

        engine.Run();

        var phantoms = engine.Servers.Where(server => server.Index >= 2 && server.Exists).ToArray();
        Assert.Equal(4, phantoms.Length);
        Assert.All(phantoms, server => Assert.Equal(ServerStatus.Offline, server.Status));
        Assert.Equal(2, phantoms.Count(server => server.LastSeenAtMs is not null));
    }

    [Fact]
    public void Engine_ReplaysDiscoveryAndWatchedPollsOutsideTheBudget()
    {
        var probes = Probes((Start, true, 20));
        var sampleSnapshots = Snapshots(
            (Start + Minute, 25, true, ServerPollSource.Watched),
            (Start + 2 * Minute, 26, true, ServerPollSource.Discovery));
        var data = Data(probes: probes, sampleSnapshots: sampleSnapshots);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(0), Options(Start, Start + 5 * Minute));

        var result = engine.Run();

        Assert.Equal(1, result.Lanes[SimulationEngine.WatchedLane].Polls.Sum());
        Assert.True(result.Lanes[SimulationEngine.WatchedLane].Exogenous);
        Assert.Equal([Start + 2 * Minute], result.Shown(1).TimesMs);
        Assert.Equal(Start + 2 * Minute, engine.Servers[0].LastCheckedAtMs);
    }

    [Fact]
    public void Engine_StartsFromTheLastSnapshotBeforeThePeriod()
    {
        var sampleSnapshots = Snapshots(
            (Start - 30 * Minute, 30, true, ServerPollSource.NormalOnlineLane),
            (Start - 10 * Minute, 30, false, ServerPollSource.NormalOnlineLane));
        var data = Data(sampleSnapshots: sampleSnapshots);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(0), Options(Start, Start + Minute));

        engine.Run();

        var sample = engine.Servers[0];
        Assert.Equal(Start - 10 * Minute, sample.LastCheckedAtMs);
        Assert.Equal(Start - 30 * Minute, sample.LastSeenAtMs);
        Assert.Equal(ServerStatus.Online, sample.Status);

        var other = engine.Servers[1];
        Assert.Null(other.LastCheckedAtMs);
        Assert.Equal(ServerStatus.Unknown, other.Status);
    }

    [Fact]
    public void Engine_LateTicksFollowTheWorkerTiming()
    {
        var data = Data(totalServers: 130);
        var engine = new SimulationEngine(data, new UniformPollingStrategy(128 * 4), new SimulationOptions
        {
            FromMs = Start,
            ToMs = Start + 2 * Minute,
            Timing = new WorkerTiming { JitterMs = 0 }
        });

        var result = engine.Run();

        Assert.True(result.LateTicks > 0);
        Assert.True(result.Ticks < 8);
    }

    [Fact]
    public void RankingReplay_RanksByAveragePlayersAmongRecentlySeenServers()
    {
        var servers = Enumerable.Range(0, 3)
            .Select(i => new PolledServer(i, Guid.Parse($"00000000-0000-0000-0000-{i:D12}"), RustServerType.Community, 0) { Exists = true })
            .ToArray();
        var histories = new ServerHistory?[3];
        for (var i = 0; i < 3; i++)
        {
            histories[i] = new ServerHistory();
        }

        for (var k = 0; k < 6; k++)
        {
            histories[0]!.Add(Start - k * Minute, 50, 100, true);
            histories[1]!.Add(Start - k * Minute, 80, 100, true);
            histories[2]!.Add(Start - 2 * 86_400_000L - k * Minute, 200, 100, true);
        }

        var ranked = RankingReplay.Compute(Start, servers, histories, new ServerRankingOptions());

        Assert.Equal([1, 0], ranked);
    }

    [Fact]
    public void RankingReplay_FrozenMomentsAreTheHoursWhenSampleRanksChanged()
    {
        var at = (string text) => DateTimeOffset.Parse(text);
        var data = Data() with
        {
            Rankings = [new ServerRankingRow(SampleId, 1, 100, at("2026-11-09T13:15:46Z"))],
            SampleRankings =
            [
                new SampleRankingRow(1, at("2026-11-09T11:35:00Z"), SampleId, 3),
                new SampleRankingRow(2, at("2026-11-09T12:35:00Z"), SampleId, 3),
                new SampleRankingRow(3, at("2026-11-09T13:35:00Z"), SampleId, 1),
                new SampleRankingRow(4, at("2026-11-09T14:35:00Z"), SampleId, null)
            ]
        };

        var moments = RankingReplay.FrozenMoments(data);

        Assert.Equal(
            [at("2026-11-09T13:15:46Z").ToUnixTimeMilliseconds(), at("2026-11-09T14:15:46Z").ToUnixTimeMilliseconds()],
            moments);
    }

    [Fact]
    public void ServerHistory_KeepsTimeOrderAndAnswersWindows()
    {
        var history = new ServerHistory();
        history.Add(10, 5, 50, true);
        history.Add(30, 7, 60, false);
        history.Add(20, 9, 55, true);

        Assert.Equal((2, 8.0, 9), history.Window(15, 30));
        Assert.Equal(20, history.LastAnsweredAtOrBefore(30));
        Assert.Equal(55, history.MaxPlayersAtOrBefore(25));
    }

    private static SimulationOptions Options(long from, long to) => new()
    {
        FromMs = from,
        ToMs = to,
        Timing = null
    };

    private static SimulatorData Data(
        ProbeSeries? probes = null,
        SnapshotSeries? sampleSnapshots = null,
        SnapshotSeries? otherSnapshots = null,
        int totalServers = 0,
        PopulationSample[]? population = null)
    {
        var index = new ServerIndex();
        index.GetOrAdd(SampleId);
        index.GetOrAdd(OtherId);
        var created = DateTimeOffset.FromUnixTimeMilliseconds(Start - 86_400_000);
        return new SimulatorData
        {
            Manifests = [],
            SampleServers = [new SampleServer(1, SampleId, 0, StudyStratum.Popular, 200, "DE", created)],
            Probes = [probes ?? ProbeSeries.Empty],
            ServerIndex = index,
            Servers =
            [
                Record(SampleId, created),
                Record(OtherId, created)
            ],
            Snapshots =
            [
                sampleSnapshots ?? Snapshots((Start - 3 * 86_400_000L, 20, true, ServerPollSource.FastLane)),
                otherSnapshots ?? SnapshotSeries.Empty
            ],
            Rankings = [],
            SampleRankings = [],
            PopulationSamples = population ?? (totalServers == 0
                ? []
                : [new PopulationSample(1, DateTimeOffset.FromUnixTimeMilliseconds(Start), totalServers, 0, 0)]),
            TableWriteSamples = [],
            WipeObservations = [],
            PredictionSnapshots = []
        };
    }

    private static PopulationSample Population(long atMs, int total, int online) =>
        new(atMs, DateTimeOffset.FromUnixTimeMilliseconds(atMs), total, online, 0);

    private static ServerRecord Record(Guid id, DateTimeOffset created) =>
        new(id, RustServerType.Community, ServerStatus.Online, 0, 100, 0, created, null, null, null, null, null, true);

    private static ProbeSeries Probes(params (long Time, bool Ok, short Players)[] probes) =>
        new(probes.Select(p => p.Time).ToArray(), probes.Select(p => p.Ok).ToArray(),
            probes.Select(p => p.Players).ToArray(), new short[probes.Length]);

    private static SnapshotSeries Snapshots(params (long Time, short Players, bool Ok, ServerPollSource Source)[] rows) =>
        new(rows.Select(r => r.Time).ToArray(),
            rows.Select(r => r.Players).ToArray(),
            rows.Select(_ => (short)100).ToArray(),
            rows.Select(_ => true).ToArray(),
            rows.Select(r => r.Ok ? (sbyte)1 : (sbyte)0).ToArray(),
            rows.Select(r => r.Source).ToArray());
}
