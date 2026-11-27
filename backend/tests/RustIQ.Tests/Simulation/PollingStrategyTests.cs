using RustIQ.Application.Servers;
using RustIQ.Application.Servers.Polling;
using RustIQ.Domain.Servers;

namespace RustIQ.Tests.Simulation;

public sealed class PollingStrategyTests
{
    private const long Minute = 60_000;
    private const long Now = 1_000 * Minute;

    [Fact]
    public void PollStateRules_AnsweredPollUpdatesCountsAndMarksOnline()
    {
        var server = Server(0);

        var recorded = PollStateRules.Apply(server, new PollOutcome(Now, true, 120, 100, 3));

        Assert.Equal(120, recorded);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(120, server.MaxPlayers);
        Assert.Equal(3, server.QueuePlayers);
        Assert.Equal(Now, server.LastSeenAtMs);
        Assert.Equal(Now, server.LastCheckedAtMs);
    }

    [Fact]
    public void PollStateRules_FailedPollKeepsTheServerOnlineInsideTheGrace()
    {
        var server = Server(0);
        PollStateRules.Apply(server, new PollOutcome(Now, true, 50, 100, 0));

        var recorded = PollStateRules.Apply(server, new PollOutcome(Now + 600_000, false, 0, 0, 0));
        Assert.Equal(50, recorded);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(Now + 600_000, server.LastCheckedAtMs);

        recorded = PollStateRules.Apply(server, new PollOutcome(Now + 600_001, false, 0, 0, 0));
        Assert.Equal(0, recorded);
        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal(Now, server.LastSeenAtMs);
    }

    [Fact]
    public void PollStateRules_NeverSeenServerGoesOfflineOnTheFirstFailure()
    {
        var server = Server(0);

        PollStateRules.Apply(server, new PollOutcome(Now, false, 0, 0, 0));

        Assert.Equal(ServerStatus.Offline, server.Status);
    }

    [Fact]
    public void RecentPeaks_AnswerTheWindowQuestionAfterDroppingDominatedSnapshots()
    {
        var server = Server(0);
        server.AddSnapshot(Now - 50 * Minute, 150, 120 * Minute);
        server.AddSnapshot(Now - 30 * Minute, 80, 120 * Minute);
        server.AddSnapshot(Now - 10 * Minute, 90, 120 * Minute);

        Assert.True(server.HasSnapshotWithPlayersSince(Now - 60 * Minute, 100));
        Assert.False(server.HasSnapshotWithPlayersSince(Now - 40 * Minute, 100));
        Assert.True(server.HasSnapshotWithPlayersSince(Now - 40 * Minute, 90));
        Assert.False(server.HasSnapshotWithPlayersSince(Now - 5 * Minute, 1));
    }

    [Fact]
    public void FourLane_SortsServersIntoTheirLanes()
    {
        var strategy = new FourLanePollingStrategy();
        var popularByPlayers = Online(0, players: 150);
        var popularByQueue = Online(1, players: 10, queue: 2);
        var popularByRank = Online(2, players: 10, rank: 250);
        var popularByRecentPeak = Online(3, players: 10);
        popularByRecentPeak.AddSnapshot(Now - 30 * Minute, 120, 120 * Minute);
        var normal = Online(4, players: 10, rank: 251);
        var slow = Offline(5, lastSeen: Now - 2 * 60 * Minute);
        var veryStale = Offline(6, lastSeen: Now - 25 * 60 * Minute);
        var neverSeen = Offline(7, lastSeen: null);
        var popularOfflineGrace = Offline(8, lastSeen: Now - 30 * Minute, rank: 5);
        var servers = new[] { popularByPlayers, popularByQueue, popularByRank, popularByRecentPeak, normal, slow, veryStale, neverSeen, popularOfflineGrace };

        Assert.Equal([0, 1, 2, 3, 8], Indexes(strategy.SelectForLane(ServerRefreshLane.Fast, Now, Now, 100, servers)).Order());
        Assert.Equal([4], Indexes(strategy.SelectForLane(ServerRefreshLane.NormalOnline, Now, Now, 100, servers)));
        Assert.Equal([5], Indexes(strategy.SelectForLane(ServerRefreshLane.SlowOffline, Now, Now, 100, servers)));
        Assert.Equal([6, 7], Indexes(strategy.SelectForLane(ServerRefreshLane.VeryStaleOffline, Now, Now, 100, servers)).Order());
    }

    [Fact]
    public void FourLane_TakesOnlyDueServersInLastCheckedThenPlayersOrder()
    {
        var strategy = new FourLanePollingStrategy();
        var recent = Online(0, players: 200, lastChecked: Now - 10_000);
        var oldQuiet = Online(1, players: 120, lastChecked: Now - 300_000);
        var oldBusy = Online(2, players: 300, lastChecked: Now - 300_000);
        var older = Online(3, players: 110, lastChecked: Now - 400_000);

        var selected = strategy.SelectForLane(ServerRefreshLane.Fast, Now - 45_000, Now, 2, [recent, oldQuiet, oldBusy, older]);

        Assert.Equal([3, 2], Indexes(selected));
    }

    [Fact]
    public void FourLane_FillsLeftoverFastCapacityWithOfficialServers()
    {
        var strategy = new FourLanePollingStrategy();
        var popular = Online(0, players: 150);
        var official = Online(1, players: 5, type: RustServerType.Official);
        var community = Online(2, players: 5);

        var selected = strategy.SelectForLane(ServerRefreshLane.Fast, Now, Now, 10, [official, popular, community]);

        Assert.Equal([0, 1], Indexes(selected));
    }

    [Fact]
    public void FourLane_RunsEachLaneOnItsIntervalWithItsBatch()
    {
        var strategy = new FourLanePollingStrategy(new FourLaneOptions { FastLaneBatchSize = 1, BatchScale = 2 });
        var servers = Enumerable.Range(0, 5).Select(i => Online(i, players: 150, lastChecked: 0)).ToArray();
        var executor = new RecordingExecutor();

        strategy.Tick(Now, servers, executor);
        strategy.Tick(Now + 15_000, servers, executor);
        strategy.Tick(Now + 44_999, servers, executor);
        strategy.Tick(Now + 45_000, servers, executor);

        Assert.Equal([("Fast", 2), ("Fast", 2)], executor.Calls.Select(call => (call.Lane, call.Servers.Count)));
    }

    [Fact]
    public void FourLane_WithoutOfflineLanesPollsOnlyOnlineServers()
    {
        var strategy = new FourLanePollingStrategy(new FourLaneOptions
        {
            NormalOnlineLaneBatchSize = 3,
            SlowOfflineLaneBatchSize = 0,
            VeryStaleOfflineLaneBatchSize = 0
        });
        var online = Enumerable.Range(0, 4).Select(i => Online(i, players: 10)).ToArray();
        var offline = new[] { Offline(4, lastSeen: Now - 2 * 60 * Minute), Offline(5, lastSeen: null) };
        var executor = new RecordingExecutor();

        strategy.Tick(Now, [.. online, .. offline], executor);

        Assert.Equal([("NormalOnline", 3)], executor.Calls.Select(call => (call.Lane, call.Servers.Count)));
    }

    [Fact]
    public void FourLane_SkipsServersAnEarlierLanePolledInTheSameTick()
    {
        var strategy = new FourLanePollingStrategy();
        var server = Offline(0, lastSeen: null);
        server.Players = 150;
        var executor = new RecordingExecutor();

        strategy.Tick(Now, [server], executor);

        Assert.Equal("Fast", executor.Calls.Single().Lane);
    }

    [Fact]
    public void Uniform_PollsTheOldestServersWithinTheBudget()
    {
        var strategy = new UniformPollingStrategy(queriesPerMinute: 8);
        var servers = new[]
        {
            Online(0, players: 1, lastChecked: Now - 1_000),
            Online(1, players: 1, lastChecked: Now - 9_000),
            Online(2, players: 1, lastChecked: Now - 5_000),
            Offline(3, lastSeen: null)
        };
        servers[3].Exists = false;
        var executor = new RecordingExecutor();

        strategy.Tick(Now, servers, executor);

        Assert.Equal([1, 2], Indexes(executor.Calls.Single().Servers));
    }

    [Fact]
    public void PollBudget_FollowsElapsedTimeAndCarriesFractions()
    {
        var budget = new PollBudget(queriesPerMinute: 10, tickSeconds: 15);

        Assert.Equal(2, budget.Next(0));
        Assert.Equal(3, budget.Next(15_000));
        Assert.Equal(10, budget.Next(75_000));
    }

    [Fact]
    public void Priority_RespectsTheMinimumIntervalAndBacksOffUnansweredServers()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { MinIntervalSeconds = 45, MaxBackoffSeconds = 100 });
        var server = Online(0, players: 10, lastChecked: Now - 44_000);

        Assert.Equal(0, strategy.Score(server, Now));
        Assert.True(strategy.Score(server, Now + 1_000) > 0);

        strategy.OnPolled(server, new PollOutcome(Now, false, 0, 0, 0));
        strategy.OnPolled(server, new PollOutcome(Now, false, 0, 0, 0));
        server.LastCheckedAtMs = Now;
        Assert.Equal(0, strategy.Score(server, Now + 99_000));
        Assert.True(strategy.Score(server, Now + 100_000) > 0);
    }

    [Fact]
    public void Priority_BackoffCapShrinksWithTheServersWeight()
    {
        var options = new PriorityOptions { MinIntervalSeconds = 165, MaxBackoffSeconds = 6 * 3600, MaxAgeBudgetShare = 0 };
        var strategy = new PriorityPollingStrategy(100, options);
        var busy = Offline(0, lastSeen: Now - 3_600_000);
        var empty = Offline(1, lastSeen: Now - 3_600_000);
        strategy.OnPolled(busy, new PollOutcome(Now - 3_600_000, true, 299, 300, 0));
        strategy.OnPolled(empty, new PollOutcome(Now - 3_600_000, true, 0, 100, 0));
        for (var i = 0; i < 20; i++)
        {
            strategy.OnPolled(busy, new PollOutcome(Now, false, 0, 0, 0));
            strategy.OnPolled(empty, new PollOutcome(Now, false, 0, 0, 0));
        }

        busy.LastCheckedAtMs = Now;
        empty.LastCheckedAtMs = Now;

        Assert.True(strategy.Score(busy, Now + 165_000) > 0);
        Assert.Equal(0, strategy.Score(empty, Now + 6 * 3_600_000 - 1_000));
        Assert.True(strategy.Score(empty, Now + 6 * 3_600_000) > 0);

        var flat = new PriorityPollingStrategy(100, new PriorityOptions { MinIntervalSeconds = 165, MaxAgeBudgetShare = 0, BackoffScalesWithWeight = false });
        flat.OnPolled(busy, new PollOutcome(Now - 3_600_000, true, 299, 300, 0));
        for (var i = 0; i < 20; i++)
        {
            flat.OnPolled(busy, new PollOutcome(Now, false, 0, 0, 0));
        }

        Assert.Equal(0, flat.Score(busy, Now + 3_600_000));
    }

    [Fact]
    public void Priority_ExpectedAgeKeepsGrowingSoQuietServersAreReached()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { MinIntervalSeconds = 45, MaxAgeBudgetShare = 0 });
        var busy = Online(0, players: 300, lastChecked: Now - 60_000);
        var quiet = Online(1, players: 1, lastChecked: Now - 60_000);
        strategy.OnPolled(busy, new PollOutcome(Now - 60_000, true, 300, 400, 0));
        strategy.OnPolled(quiet, new PollOutcome(Now - 60_000, true, 1, 50, 0));

        Assert.True(strategy.Score(busy, Now) > strategy.Score(quiet, Now));
        quiet.LastCheckedAtMs = Now - 86_400_000;
        Assert.True(strategy.Score(quiet, Now) > strategy.Score(busy, Now));
    }

    [Fact]
    public void Priority_ChangeProbabilityScoreIsBoundedByTheWeight()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { Score = PriorityScore.ChangeProbability, MaxAgeBudgetShare = 0 });
        var quiet = Online(0, players: 1, lastChecked: Now - 86_400_000);
        strategy.OnPolled(quiet, new PollOutcome(Now - 86_400_000, true, 1, 50, 0));

        Assert.True(strategy.Score(quiet, Now) <= 2);
    }

    [Fact]
    public void Priority_ExpectedAgeIsStableForTinyRates()
    {
        Assert.Equal(0.5 * 1e-8 * 100 * 100, PriorityPollingStrategy.ExpectedAge(1e-8, 100), 10);
        Assert.Equal(100 - (1 - Math.Exp(-1)) / 0.01, PriorityPollingStrategy.ExpectedAge(0.01, 100), 9);
        Assert.True(PriorityPollingStrategy.ExpectedAge(1e6, 100) <= 100);
    }

    [Fact]
    public void Priority_BoostsServersNearAPredictedWipe()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { WipeBoost = 3 });
        var server = Online(0, players: 10, lastChecked: Now - 600_000);
        var plain = strategy.Score(server, Now);

        server.NextWipeAtMs = Now + 3_600_000;
        Assert.Equal(plain * 3, strategy.Score(server, Now), 9);
        server.NextWipeAtMs = Now + 3_600_001;
        Assert.Equal(plain, strategy.Score(server, Now), 9);
    }

    [Fact]
    public void Priority_WipeBoostIsOffByDefault()
    {
        var strategy = new PriorityPollingStrategy(100);
        var server = Online(0, players: 10, lastChecked: Now - 600_000);
        var plain = strategy.Score(server, Now);

        server.NextWipeAtMs = Now;
        Assert.Equal(plain, strategy.Score(server, Now), 9);
    }

    [Fact]
    public void Priority_MaxAgePutsOverdueOnlineServersFirstOldestFirst()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { MaxAgeBudgetShare = 0, MaxAgeSeconds = 80 * 60 });
        var busy = Online(0, players: 300, lastChecked: Now - 120_000);
        strategy.OnPolled(busy, new PollOutcome(Now - 120_000, true, 300, 400, 0));
        var overdue = Online(1, players: 1, lastChecked: Now - 80 * Minute);
        var moreOverdue = Online(2, players: 1, lastChecked: Now - 90 * Minute);
        var offlineOld = Offline(3, lastSeen: null);
        offlineOld.LastCheckedAtMs = Now - 600 * Minute;

        Assert.True(strategy.Score(overdue, Now) > strategy.Score(busy, Now));
        Assert.True(strategy.Score(moreOverdue, Now) > strategy.Score(overdue, Now));
        Assert.True(strategy.Score(offlineOld, Now) < PriorityPollingStrategy.OverdueScore);

        var executor = new RecordingExecutor();
        new PriorityPollingStrategy(8, new PriorityOptions { MaxAgeBudgetShare = 0, MaxAgeSeconds = 80 * 60 })
            .Tick(Now, [busy, overdue, moreOverdue, offlineOld], executor);
        Assert.Equal([2, 1], Indexes(executor.Calls.Single().Servers));
    }

    [Fact]
    public void Priority_MaxAgeFollowsTheBudgetAndCoversRecentlySeenOfflineServers()
    {
        var strategy = new PriorityPollingStrategy(600, new PriorityOptions { MaxAgeBudgetShare = 0.5, MinIntervalSeconds = 0.1 });
        var servers = new[]
        {
            Online(0, players: 1, lastChecked: Now - 1_000),
            Online(1, players: 1, lastChecked: Now - 1_000),
            Online(2, players: 1, lastChecked: Now - 1_000)
        };
        strategy.UpdateMaxAge(servers, Now);
        Assert.Equal(0.6, strategy.AverageMaxAgeSeconds, 9);

        var recentlyOffline = Offline(3, lastSeen: Now - 23 * 3_600_000L);
        recentlyOffline.LastCheckedAtMs = Now - 1_000;
        for (var i = 0; i < 20; i++)
        {
            strategy.OnPolled(recentlyOffline, new PollOutcome(Now - 1_000, false, 0, 0, 0));
        }

        Assert.True(strategy.Score(recentlyOffline, Now) >= PriorityPollingStrategy.OverdueScore);

        var longOffline = Offline(4, lastSeen: Now - 25 * 3_600_000L);
        longOffline.LastCheckedAtMs = Now - 1_000;
        for (var i = 0; i < 20; i++)
        {
            strategy.OnPolled(longOffline, new PollOutcome(Now - 1_000, false, 0, 0, 0));
        }

        Assert.Equal(0, strategy.Score(longOffline, Now));
    }

    [Fact]
    public void Priority_MaxAgeZeroTurnsTheGuaranteeOff()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { MaxAgeBudgetShare = 0, MaxAgeSeconds = 0 });
        var old = Online(0, players: 1, lastChecked: Now - 600 * Minute);

        Assert.True(strategy.Score(old, Now) < PriorityPollingStrategy.OverdueScore);
    }

    [Fact]
    public void Priority_PredictedWipesCanBeTurnedOff()
    {
        var strategy = new PriorityPollingStrategy(100, new PriorityOptions { WipeBoost = 3, UsePredictedWipes = false });
        var server = Online(0, players: 10, lastChecked: Now - 600_000);
        var plain = strategy.Score(server, Now);

        server.NextWipeAtMs = Now;
        Assert.Equal(plain, strategy.Score(server, Now), 9);
    }

    [Fact]
    public void TopK_KeepsTheFirstKInOrder()
    {
        var topK = new TopK<int>(Comparer<int>.Default);
        topK.Reset(3);
        foreach (var value in new[] { 9, 4, 7, 1, 8, 2, 6 })
        {
            topK.Offer(value);
        }

        Assert.Equal([1, 2, 4], topK.ToSortedList());
    }

    private static PolledServer Server(int index, RustServerType type = RustServerType.Community) =>
        new(index, Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), type, 0) { Exists = true };

    private static PolledServer Online(int index, int players, int queue = 0, int? rank = null, long lastChecked = 0,
        RustServerType type = RustServerType.Community)
    {
        var server = Server(index, type);
        server.Status = ServerStatus.Online;
        server.Players = players;
        server.QueuePlayers = queue;
        server.Rank = rank;
        server.LastSeenAtMs = lastChecked;
        server.LastCheckedAtMs = lastChecked;
        return server;
    }

    private static PolledServer Offline(int index, long? lastSeen, int? rank = null)
    {
        var server = Server(index);
        server.Status = ServerStatus.Offline;
        server.LastSeenAtMs = lastSeen;
        server.LastCheckedAtMs = 0;
        server.Rank = rank;
        return server;
    }

    private static int[] Indexes(IEnumerable<PolledServer> servers) => servers.Select(server => server.Index).ToArray();

    private sealed class RecordingExecutor : IPollExecutor
    {
        public List<(string Lane, IReadOnlyList<PolledServer> Servers)> Calls { get; } = [];

        public void Poll(IReadOnlyList<PolledServer> servers, string lane) => Calls.Add((lane, servers.ToArray()));
    }
}
