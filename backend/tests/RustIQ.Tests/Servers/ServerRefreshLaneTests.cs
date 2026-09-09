using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerRefreshLaneTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-10T12:00:00Z");

    [Fact]
    public async Task GetServersForRefreshLaneAsync_TreatsAFailedPollAsHavingTakenItsTurn()
    {
        var repository = new InMemoryServerRepository();

        // A server whose poll just failed. LastCheckedAtUtc moved, LastPlayerCountUpdatedAtUtc did not,
        // because only a successful poll advances that one. Ordering the lanes by it meant servers that
        // keep failing never left the front of the queue: the same batch was re-polled every tick while
        // everything behind them went weeks without a single query.
        var justFailed = CreateServer("just-failed.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddDays(-30));
        justFailed.LastCheckedAtUtc = Now.AddSeconds(-5);
        justFailed.LastPlayerCountUpdatedAtUtc = Now.AddDays(-30);

        var waitingItsTurn = CreateServer("waiting.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddDays(-30));
        waitingItsTurn.LastCheckedAtUtc = Now.AddHours(-3);
        waitingItsTurn.LastPlayerCountUpdatedAtUtc = Now.AddDays(-30);

        repository.Servers.AddRange([justFailed, waitingItsTurn]);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.VeryStaleOffline, Now.AddHours(-1));

        Assert.Equal("waiting.test", Assert.Single(servers).Host);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_ReachesServersThatHaveNeverBeenPolled()
    {
        var repository = new InMemoryServerRepository();

        // Discovery stamps UpdatedAtUtc on every cycle, so a never-polled server looked permanently up
        // to date while it waited. Falling back to CreatedAtUtc, which never moves, is what lets the
        // discovery backlog actually drain.
        var neverPolled = CreateServer("never-polled.test", ServerStatus.Unknown, players: 0);
        neverPolled.LastCheckedAtUtc = null;
        neverPolled.LastPlayerCountUpdatedAtUtc = null;
        neverPolled.CreatedAtUtc = Now.AddDays(-2);
        neverPolled.UpdatedAtUtc = Now.AddSeconds(-30);
        repository.Servers.Add(neverPolled);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.VeryStaleOffline, Now.AddHours(-1));

        Assert.Equal("never-polled.test", Assert.Single(servers).Host);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_FastLaneIncludesPopularServersAndRestartGrace()
    {
        var repository = new InMemoryServerRepository();
        var ranked = CreateServer("ranked.test", ServerStatus.Online, players: 10);
        var populated = CreateServer("populated.test", ServerStatus.Online, players: 120);
        var recentPeak = CreateServer("recent-peak.test", ServerStatus.Online, players: 10);
        var restarting = CreateServer("restarting.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddMinutes(-10));
        var queued = CreateServer("queued.test", ServerStatus.Online, players: 30);
        queued.QueuePlayers = 5;
        var normal = CreateServer("normal.test", ServerStatus.Online, players: 30);
        var notDue = CreateServer("not-due.test", ServerStatus.Online, players: 200, lastRefreshedAtUtc: Now.AddSeconds(-10));
        repository.Servers.AddRange([ranked, populated, recentPeak, restarting, queued, normal, notDue]);
        repository.Rankings.Add(CreateRanking(ranked.Id, rank: 12));
        repository.Snapshots.Add(CreateSnapshot(recentPeak.Id, Now.AddMinutes(-30), players: 140));
        repository.Snapshots.Add(CreateSnapshot(restarting.Id, Now.AddMinutes(-45), players: 150));

        var servers = await GetLaneAsync(repository, ServerRefreshLane.Fast, Now.AddSeconds(-45));

        Assert.Equal(
            ["populated.test", "queued.test", "ranked.test", "recent-peak.test", "restarting.test"],
            servers.Select(server => server.Host).Order());
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_FastLaneIncludesOfficialServersRegardlessOfPopularity()
    {
        var repository = new InMemoryServerRepository();
        var officialOffline = CreateServer("official-offline.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddDays(-20));
        officialOffline.ServerType = RustServerType.Official;
        var officialOnlineLowPop = CreateServer("official-online.test", ServerStatus.Online, players: 5);
        officialOnlineLowPop.ServerType = RustServerType.Official;
        var communityLowPop = CreateServer("community.test", ServerStatus.Online, players: 5);
        repository.Servers.AddRange([officialOffline, officialOnlineLowPop, communityLowPop]);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.Fast, Now.AddSeconds(-45));

        Assert.Equal(
            ["official-offline.test", "official-online.test"],
            servers.Select(server => server.Host).Order());
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_FastLaneNeverLetsOfficialServersStarvePopularServers()
    {
        var repository = new InMemoryServerRepository();
        var officialStuck = CreateServer(
            "official-stuck.test",
            ServerStatus.Offline,
            players: 0,
            lastSeenAtUtc: Now.AddDays(-20),
            lastRefreshedAtUtc: Now.AddDays(-20));
        officialStuck.ServerType = RustServerType.Official;
        var populated = CreateServer(
            "populated.test",
            ServerStatus.Online,
            players: 120,
            lastRefreshedAtUtc: Now.AddMinutes(-1));
        repository.Servers.AddRange([officialStuck, populated]);

        var servers = await repository.GetServersForRefreshLaneAsync(
            ServerRefreshLane.Fast,
            Now.AddSeconds(-45),
            Now,
            take: 1,
            popularRankThreshold: 250,
            popularPlayerThreshold: 100,
            popularPeakPlayerThreshold: 100,
            recentPeakWindow: TimeSpan.FromMinutes(60),
            popularOfflineGrace: TimeSpan.FromMinutes(60),
            slowOfflineAfter: TimeSpan.FromMinutes(60),
            veryStaleOfflineAfter: TimeSpan.FromHours(24),
            CancellationToken.None);

        var server = Assert.Single(servers);
        Assert.Equal("populated.test", server.Host);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_NormalOnlineExcludesFastLaneServers()
    {
        var repository = new InMemoryServerRepository();
        var normal = CreateServer("normal.test", ServerStatus.Online, players: 30, lastRefreshedAtUtc: Now.AddMinutes(-4));
        var populated = CreateServer("populated.test", ServerStatus.Online, players: 120, lastRefreshedAtUtc: Now.AddMinutes(-4));
        repository.Servers.AddRange([normal, populated]);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.NormalOnline, Now.AddMinutes(-3));

        var server = Assert.Single(servers);
        Assert.Equal("normal.test", server.Host);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_IncludesServersUsingA2sFallback()
    {
        var repository = new InMemoryServerRepository();
        var server = CreateServer(
            "a2s-fallback.test",
            ServerStatus.Online,
            players: 30,
            lastRefreshedAtUtc: Now.AddMinutes(-4));
        server.PlayerCountSource = ServerPlayerCountSource.A2S;
        server.BattleMetricsServerId = null;
        repository.Servers.Add(server);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.NormalOnline, Now.AddMinutes(-3));

        Assert.Equal(server.Id, Assert.Single(servers).Id);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_SlowOfflineIncludesServersWithoutBattleMetricsId()
    {
        var repository = new InMemoryServerRepository();
        var steamDiscovered = CreateServer(
            "steam-only.test",
            ServerStatus.Offline,
            players: 0,
            lastSeenAtUtc: Now.AddHours(-2),
            lastRefreshedAtUtc: Now.AddMinutes(-30));
        steamDiscovered.BattleMetricsServerId = null;
        steamDiscovered.DiscoverySource = ServerDiscoverySource.Steam;
        steamDiscovered.PlayerCountSource = ServerPlayerCountSource.A2S;
        repository.Servers.Add(steamDiscovered);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.SlowOffline, Now.AddMinutes(-20));

        Assert.Equal(steamDiscovered.Id, Assert.Single(servers).Id);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_ExcludesServersWithoutQueryableEndpoint()
    {
        var repository = new InMemoryServerRepository();
        var missingHost = CreateServer(string.Empty, ServerStatus.Online, players: 30, lastRefreshedAtUtc: Now.AddMinutes(-4));
        var missingQueryPort = CreateServer("no-query-port.test", ServerStatus.Online, players: 30, lastRefreshedAtUtc: Now.AddMinutes(-4));
        missingQueryPort.QueryPort = 0;
        repository.Servers.AddRange([missingHost, missingQueryPort]);

        var servers = await GetLaneAsync(repository, ServerRefreshLane.NormalOnline, Now.AddMinutes(-3));

        Assert.Empty(servers);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_SlowOfflineExcludesRecentPopularAndVeryStaleServers()
    {
        var repository = new InMemoryServerRepository();
        var slow = CreateServer("slow.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddHours(-2), lastRefreshedAtUtc: Now.AddMinutes(-30));
        var recentRestart = CreateServer("recent-restart.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddMinutes(-20), lastRefreshedAtUtc: Now.AddMinutes(-30));
        var veryStale = CreateServer("very-stale.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddHours(-25), lastRefreshedAtUtc: Now.AddMinutes(-30));
        var ranked = CreateServer("ranked.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddHours(-2), lastRefreshedAtUtc: Now.AddMinutes(-30));
        repository.Servers.AddRange([slow, recentRestart, veryStale, ranked]);
        repository.Rankings.Add(CreateRanking(ranked.Id, rank: 10));

        var servers = await GetLaneAsync(repository, ServerRefreshLane.SlowOffline, Now.AddMinutes(-20));

        var server = Assert.Single(servers);
        Assert.Equal("slow.test", server.Host);
    }

    [Fact]
    public async Task GetServersForRefreshLaneAsync_VeryStaleOfflineIncludesOldLowPopulationServers()
    {
        var repository = new InMemoryServerRepository();
        var old = CreateServer("old.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddHours(-25), lastRefreshedAtUtc: Now.AddHours(-2));
        var neverSeen = CreateServer("never-seen.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: null, lastRefreshedAtUtc: Now.AddHours(-2));
        var ranked = CreateServer("ranked.test", ServerStatus.Offline, players: 0, lastSeenAtUtc: Now.AddHours(-25), lastRefreshedAtUtc: Now.AddHours(-2));
        repository.Servers.AddRange([old, neverSeen, ranked]);
        repository.Rankings.Add(CreateRanking(ranked.Id, rank: 10));

        var servers = await GetLaneAsync(repository, ServerRefreshLane.VeryStaleOffline, Now.AddHours(-1));

        Assert.Equal(["never-seen.test", "old.test"], servers.Select(server => server.Host).Order());
    }

    private static Task<IReadOnlyList<Server>> GetLaneAsync(
        InMemoryServerRepository repository,
        ServerRefreshLane refreshLane,
        DateTimeOffset dueBeforeUtc)
    {
        return repository.GetServersForRefreshLaneAsync(
            refreshLane,
            dueBeforeUtc,
            Now,
            take: 100,
            popularRankThreshold: 250,
            popularPlayerThreshold: 100,
            popularPeakPlayerThreshold: 100,
            recentPeakWindow: TimeSpan.FromMinutes(60),
            popularOfflineGrace: TimeSpan.FromMinutes(60),
            slowOfflineAfter: TimeSpan.FromMinutes(60),
            veryStaleOfflineAfter: TimeSpan.FromHours(24),
            CancellationToken.None);
    }

    private static Server CreateServer(
        string host,
        ServerStatus status,
        int players,
        DateTimeOffset? lastSeenAtUtc = null,
        DateTimeOffset? lastRefreshedAtUtc = null)
    {
        return new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = Random.Shared.NextInt64(1, long.MaxValue),
            Host = host,
            QueryPort = 28015,
            Players = players,
            MaxPlayers = 200,
            Status = status,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            LastSeenAtUtc = lastSeenAtUtc,
            LastCheckedAtUtc = lastRefreshedAtUtc ?? Now.AddMinutes(-10),
            LastPlayerCountUpdatedAtUtc = lastRefreshedAtUtc ?? Now.AddMinutes(-10),
            CreatedAtUtc = Now.AddDays(-1),
            UpdatedAtUtc = Now.AddMinutes(-10)
        };
    }

    private static ServerSnapshot CreateSnapshot(Guid serverId, DateTimeOffset checkedAtUtc, int players)
    {
        return new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            CheckedAtUtc = checkedAtUtc,
            Players = players,
            MaxPlayers = 200,
            IsOnline = players > 0
        };
    }

    private static ServerRanking CreateRanking(Guid serverId, int rank)
    {
        return new ServerRanking
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Rank = rank,
            AveragePlayers = 100,
            PeakPlayers = 150,
            SnapshotCount = 10,
            WindowStartUtc = Now.AddDays(-7),
            WindowEndUtc = Now,
            GeneratedAtUtc = Now
        };
    }
}
