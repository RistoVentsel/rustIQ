using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerPollingServiceTests
{
    private static readonly TimeSpan DefaultOfflineThreshold = TimeSpan.FromSeconds(60);

    // The production default: it has to exceed the 3-minute interval at which online servers poll.
    private static readonly TimeSpan LaneOfflineThreshold = TimeSpan.FromSeconds(600);
    private const ServerSummarySource DefaultSummarySource = ServerSummarySource.A2S;
    private const ServerPlayerCountSource DefaultPlayerCountSource = ServerPlayerCountSource.A2S;

    [Fact]
    public async Task RefreshMapMetadataAsync_UpdatesOnlyMapDataWithoutCreatingSnapshot()
    {
        var serverId = Guid.NewGuid();
        var fetchedAtUtc = DateTimeOffset.Parse("2026-07-18T15:00:00Z");
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            BattleMetricsServerId = 42,
            Host = "127.0.0.1",
            QueryPort = 28015,
            Map = "Old Map",
            CreatedAtUtc = fetchedAtUtc.AddDays(-1),
            UpdatedAtUtc = fetchedAtUtc.AddDays(-1)
        });
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                "New Map",
                7331,
                4250,
                "https://rustmaps.com/map/new",
                "https://content.rustmaps.com/new.webp",
                null,
                "BattleMetrics",
                fetchedAtUtc,
                fetchedAtUtc,
                null));
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        var updated = await service.RefreshMapMetadataAsync(
            new ConfiguredRustServer
            {
                BattleMetricsServerId = 42,
                Host = "127.0.0.1",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.True(updated);
        Assert.Empty(repository.Snapshots);
        Assert.Equal("New Map", repository.Servers[0].Map);
        Assert.Equal("New Map", repository.Servers[0].MapMetadata?.MapName);
        Assert.Equal(7331, repository.Servers[0].MapMetadata?.Seed);
        Assert.Equal(fetchedAtUtc, repository.Servers[0].MapMetadata?.FetchedAtUtc);
    }

    [Fact]
    public async Task RefreshMapMetadataAsync_UpdatesNextWipeWithoutCreatingMapOrSnapshot()
    {
        var serverId = Guid.NewGuid();
        var nextWipeAtUtc = DateTimeOffset.Parse("2030-07-31T16:00:00Z");
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "127.0.0.1",
            QueryPort = 28015,
            CreatedAtUtc = nextWipeAtUtc.AddDays(-1),
            UpdatedAtUtc = nextWipeAtUtc.AddDays(-1)
        });
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                MapName: null,
                Seed: null,
                Size: null,
                RustMapsUrl: null,
                ThumbnailUrl: null,
                MapFileUrl: null,
                Source: "A2S",
                SourceUpdatedAtUtc: null,
                FetchedAtUtc: nextWipeAtUtc.AddHours(-1),
                Error: null,
                nextWipeAtUtc));
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        var updated = await service.RefreshMapMetadataAsync(
            new ConfiguredRustServer
            {
                Host = "127.0.0.1",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.True(updated);
        Assert.Empty(repository.Snapshots);
        Assert.Null(repository.Servers[0].MapMetadata);
        Assert.Equal(nextWipeAtUtc, repository.Servers[0].NextWipeAtUtc);
    }

    [Fact]
    public async Task RefreshMapMetadataAsync_AppliesWipeFallbackWithoutReplacingMapSource()
    {
        var fetchedAtUtc = DateTimeOffset.UtcNow;
        var lastWipeAtUtc = fetchedAtUtc.AddDays(-1);
        var nextWipeAtUtc = fetchedAtUtc.AddDays(6);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "127.0.0.1",
            QueryPort = 28015,
            Map = "Procedural Map",
            CreatedAtUtc = fetchedAtUtc.AddDays(-7),
            UpdatedAtUtc = fetchedAtUtc.AddDays(-7)
        });
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                "Procedural Map",
                7331,
                4250,
                null,
                null,
                null,
                "A2S",
                null,
                fetchedAtUtc,
                null,
                nextWipeAtUtc,
                lastWipeAtUtc,
                42));
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        var updated = await service.RefreshMapMetadataAsync(
            new ConfiguredRustServer
            {
                Host = "127.0.0.1",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.True(updated);
        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(lastWipeAtUtc, server.LastWipeAtUtc);
        Assert.Equal(nextWipeAtUtc, server.NextWipeAtUtc);
        Assert.Equal("A2S", server.MapMetadata?.Source);
        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task RefreshMapMetadataAsync_PreservesEarlierFutureNextWipe()
    {
        var existingNextWipeAtUtc = DateTimeOffset.Parse("2030-07-31T15:00:00Z");
        var inferredNextWipeAtUtc = existingNextWipeAtUtc.AddHours(1);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "127.0.0.1",
            QueryPort = 28015,
            NextWipeAtUtc = existingNextWipeAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                MapName: null,
                Seed: null,
                Size: null,
                RustMapsUrl: null,
                ThumbnailUrl: null,
                MapFileUrl: null,
                Source: "A2S",
                SourceUpdatedAtUtc: null,
                FetchedAtUtc: DateTimeOffset.UtcNow,
                Error: null,
                inferredNextWipeAtUtc));
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        await service.RefreshMapMetadataAsync(
            new ConfiguredRustServer
            {
                Host = "127.0.0.1",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.Equal(existingNextWipeAtUtc, repository.Servers[0].NextWipeAtUtc);
        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotFetchMissingMapMetadataWhenRefreshIsDisabled()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-29T08:00:00Z");
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            BattleMetricsServerId = 42,
            Host = "example.test",
            QueryPort = 28015,
            Name = "Tracked Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            CreatedAtUtc = checkedAtUtc.AddDays(-1),
            UpdatedAtUtc = checkedAtUtc.AddMinutes(-10)
        });
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "example.test",
                28015,
                "Map that should not be fetched",
                1234,
                4500,
                null,
                null,
                null,
                "BattleMetrics",
                null,
                checkedAtUtc,
                null));
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(
                QueryResult(
                    "example.test",
                    28015,
                    "Tracked Server",
                    "Procedural Map",
                    10,
                    100,
                    true,
                    10,
                    checkedAtUtc,
                    null)),
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        await service.PollConfiguredServerAsync(
            new ConfiguredRustServer
            {
                BattleMetricsServerId = 42,
                Host = "example.test",
                QueryPort = 28015,
                PlayerCountSource = ServerPlayerCountSource.BattleMetrics
            },
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        Assert.Equal(0, mapClient.CallCount);
        Assert.Null(repository.Servers.Single().MapMetadata);
        Assert.Single(repository.Snapshots);
    }

    [Fact]
    public async Task EnsureConfiguredServersExistAsync_SeedsConfiguredServersWithUnknownStatus()
    {
        var repository = new InMemoryServerRepository();
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);
        var configuredServers = new[]
        {
            new ConfiguredRustServer { Name = "Tracked Server One", Host = "one.test", QueryPort = 28015 },
            new ConfiguredRustServer { Name = "Tracked Server Two", Host = "two.test", QueryPort = 28016 }
        };

        await service.EnsureConfiguredServersExistAsync(configuredServers, CancellationToken.None);

        Assert.Equal(2, repository.Servers.Count);
        Assert.All(repository.Servers, server => Assert.Equal(ServerStatus.Unknown, server.Status));
        Assert.Contains(repository.Servers, server => server.Name == "Tracked Server One");
        Assert.Contains(repository.Servers, server => server.Name == "Tracked Server Two");
    }

    [Fact]
    public async Task EnsureConfiguredServersExistAsync_UpdatesPlaceholderNameBeforeFirstSuccessfulPoll()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "one.test",
            QueryPort = 28015,
            Name = "Old Placeholder",
            Status = ServerStatus.Unknown,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.EnsureConfiguredServersExistAsync(
            [new ConfiguredRustServer { Name = "New Placeholder", Host = "one.test", QueryPort = 28015 }],
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal("New Placeholder", server.Name);
    }

    [Fact]
    public async Task EnsureConfiguredServersExistAsync_DoesNotOverrideLiveServerNameAfterSuccessfulPoll()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "one.test",
            QueryPort = 28015,
            Name = "Actual Live Name",
            Status = ServerStatus.Online,
            LastSeenAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.EnsureConfiguredServersExistAsync(
            [new ConfiguredRustServer { Name = "Configured Label", Host = "one.test", QueryPort = 28015 }],
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal("Actual Live Name", server.Name);
    }

    [Fact]
    public async Task EnsureConfiguredServersExistAsync_DoesNotDowngradeBattleMetricsIdentityFromSteamDiscovery()
    {
        var lastWipeAtUtc = DateTimeOffset.Parse("2026-07-07T08:00:00Z");
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 42,
            Host = "one.test",
            QueryPort = 28015,
            Country = "GB",
            LastWipeAtUtc = lastWipeAtUtc,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            Status = ServerStatus.Online,
            LastSeenAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var service = new ServerPollingService(
            new FakeRustServerQueryClient(),
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.EnsureConfiguredServersExistAsync(
            [
                new ConfiguredRustServer
                {
                    Host = "one.test",
                    QueryPort = 28015,
                    DiscoverySource = ServerDiscoverySource.Steam,
                    SummarySource = ServerSummarySource.A2S,
                    PlayerCountSource = ServerPlayerCountSource.A2S
                }
            ],
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
        Assert.Equal("GB", server.Country);
        Assert.Equal(lastWipeAtUtc, server.LastWipeAtUtc);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_StoresLatestStateSnapshotMapMetadataAndCurrentPlayers()
    {
        var configuredServer = new ConfiguredRustServer
        {
            Name = "Tracked Server",
            Host = "127.0.0.1",
            QueryPort = 28015
        };
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var fetchedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:01Z");
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "127.0.0.1",
                28015,
                "RustIQ Test Server",
                "Procedural Map",
                2,
                100,
                true,
                42,
                checkedAtUtc,
                null),
            [
                new RustServerPlayerResult("Alice", 120),
                new RustServerPlayerResult("Bob", 60),
                new RustServerPlayerResult(null, 1)
            ]);
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                "Moose Maps",
                1337,
                4500,
                "https://rustmaps.com/map/test",
                "https://content.rustmaps.com/thumbnail.webp",
                "https://maps.rustmaps.com/test.map",
                "BattleMetrics",
                fetchedAtUtc,
                fetchedAtUtc,
                null));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(server.Id, result.Id);
        Assert.Equal("RustIQ Test Server", server.Name);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(2, server.Players);
        Assert.Equal(100, server.MaxPlayers);
        Assert.Equal(checkedAtUtc, server.LastSeenAtUtc);
        Assert.Equal(checkedAtUtc, server.LastCheckedAtUtc);
        Assert.Equal(server.Id, snapshot.ServerId);
        Assert.True(snapshot.IsOnline);
        Assert.Equal(42, snapshot.PingMilliseconds);
        Assert.Equal("Moose Maps", server.MapMetadata?.MapName);
        Assert.Equal(1337, server.MapMetadata?.Seed);
        Assert.Equal(4500, server.MapMetadata?.Size);
        Assert.Equal("https://rustmaps.com/map/test", server.MapMetadata?.RustMapsUrl);
        Assert.Equal(2, repository.CurrentPlayers.Count);
        Assert.DoesNotContain(repository.CurrentPlayers, player => player.PlayerName is null);
        Assert.Equal("Moose Maps", result.MapMetadata?.MapName);
        Assert.Equal(2, result.CurrentPlayers.Count);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_ClearsCurrentPlayersWhenPlayerQueryReturnsNoNames()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "example.test",
            QueryPort = 28015,
            Name = "Existing Server",
            PlayerListSource = PlayerListSource.A2S,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        repository.CurrentPlayers.Add(new ServerPlayerCurrent
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            PlayerName = "StalePlayer",
            Score = 1,
            DurationSeconds = 30,
            SeenAtUtc = DateTimeOffset.Parse("2026-07-06T11:59:00Z")
        });
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                "Existing Server",
                "Procedural Map",
                0,
                100,
                true,
                10,
                DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
                null),
            [
                new RustServerPlayerResult(null, 1),
                new RustServerPlayerResult(" ", 1)
            ]);
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        var result = await service.PollConfiguredServerAsync(
            new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 },
            DefaultOfflineThreshold,
            refreshCurrentPlayers: true,
            refreshMapMetadata: false,
            CancellationToken.None);

        Assert.Empty(repository.CurrentPlayers);
        Assert.Empty(result.CurrentPlayers);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_CanPollMultipleConfiguredServersIndependently()
    {
        var firstConfiguredServer = new ConfiguredRustServer { Name = "Alpha", Host = "alpha.test", QueryPort = 28015 };
        var secondConfiguredServer = new ConfiguredRustServer { Name = "Beta", Host = "beta.test", QueryPort = 28016 };
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            [
                QueryResult("alpha.test", 28015, "Alpha Live", "Procedural Map", 20, 100, true, 25, DateTimeOffset.Parse("2026-07-06T12:00:00Z"), null),
                QueryResult("beta.test", 28016, null, null, 0, 0, false, 50, DateTimeOffset.Parse("2026-07-06T12:00:05Z"), "timeout")
            ],
            new Dictionary<(string Host, int QueryPort), IReadOnlyList<RustServerPlayerResult>>
            {
                [("alpha.test", 28015)] =
                [
                    new RustServerPlayerResult("Alice", 120)
                ]
            });
        var mapClient = new FakeRustServerMapMetadataClient(
            new Dictionary<(string Host, int QueryPort), RustServerMapMetadata>
            {
                [("alpha.test", 28015)] = new(
                    "alpha.test",
                    28015,
                    "Alpha Map",
                    1234,
                    4250,
                    "https://rustmaps.com/map/alpha",
                    "https://content.rustmaps.com/alpha.webp",
                    null,
                    "BattleMetrics",
                    DateTimeOffset.Parse("2026-07-06T12:00:01Z"),
                    DateTimeOffset.Parse("2026-07-06T12:00:01Z"),
                    null)
            });
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        await service.EnsureConfiguredServersExistAsync(
            [firstConfiguredServer, secondConfiguredServer],
            CancellationToken.None);

        var firstResult = await service.PollConfiguredServerAsync(
            firstConfiguredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            CancellationToken.None);
        var secondResult = await service.PollConfiguredServerAsync(
            secondConfiguredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            CancellationToken.None);

        Assert.Equal(2, repository.Servers.Count);
        Assert.Equal(ServerStatus.Online, firstResult.Status);
        Assert.Equal(ServerStatus.Offline, secondResult.Status);
        Assert.Equal(2, repository.Snapshots.Count);

        var firstServer = repository.Servers.Single(server => server.Host == "alpha.test");
        var secondServer = repository.Servers.Single(server => server.Host == "beta.test");
        Assert.Equal("Alpha Live", firstServer.Name);
        Assert.Equal(20, firstServer.Players);
        Assert.Equal("Alpha Map", firstServer.MapMetadata?.MapName);
        Assert.Equal("Beta", secondServer.Name);
        Assert.Equal(ServerStatus.Offline, secondServer.Status);
        Assert.Equal(0, secondServer.Players);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotImmediatelyMarkServerOfflineAfterOneFailedQuery()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var failedCheck = firstCheck.AddSeconds(30);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult("example.test", 28015, "Online", "Procedural Map", 12, 100, true, 10, firstCheck, null),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, failedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);
        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(12, server.Players);
        Assert.Equal(firstCheck, server.LastSeenAtUtc);
        Assert.Equal(failedCheck, server.LastCheckedAtUtc);
        Assert.Equal(ServerStatus.Online, result.Status);
        Assert.Equal(2, repository.Snapshots.Count);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_MarksServerOfflineAfterOfflineThresholdExpires()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var failedCheck = firstCheck.AddSeconds(61);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult("example.test", 28015, "Online", "Procedural Map", 12, 100, true, 10, firstCheck, null),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, failedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);
        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal(0, server.Players);
        Assert.Equal(firstCheck, server.LastSeenAtUtc);
        Assert.Equal(failedCheck, server.LastCheckedAtUtc);
        Assert.Equal(ServerStatus.Offline, result.Status);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_KeepsServerOnlineAcrossRepeatedFailuresAtRealisticPollSpacing()
    {
        // The lane that polls online servers runs every 3 minutes, so consecutive failures arrive
        // 3 minutes apart. With the old 60s threshold the very first one flipped a busy server
        // offline, because the gap between polls already exceeded the grace period.
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult("example.test", 28015, "Busy Server", "Procedural Map", 220, 250, true, 10, firstCheck, null),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, firstCheck.AddMinutes(3), "timeout"),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, firstCheck.AddMinutes(6), "timeout"),
            QueryResult("example.test", 28015, "Busy Server", "Procedural Map", 224, 250, true, 12, firstCheck.AddMinutes(9), null));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        var results = new List<ServerDto>();
        for (var poll = 0; poll < 4; poll++)
        {
            results.Add(await service.PollConfiguredServerAsync(
                configuredServer,
                LaneOfflineThreshold,
                refreshCurrentPlayers: false,
                refreshMapMetadata: false,
                CancellationToken.None));
        }

        Assert.Equal(
            [ServerStatus.Online, ServerStatus.Online, ServerStatus.Online, ServerStatus.Online],
            results.Select(result => result.Status));

        var server = Assert.Single(repository.Servers);
        Assert.Equal(224, server.Players);
        Assert.Equal(firstCheck.AddMinutes(9), server.LastSeenAtUtc);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_RecordsLastKnownStateInSnapshotsTakenDuringTheOfflineGrace()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var failedCheck = firstCheck.AddMinutes(3);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult("example.test", 28015, "Busy Server", "Procedural Map", 220, 250, true, 10, firstCheck, null),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, failedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.PollConfiguredServerAsync(
            configuredServer,
            LaneOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);
        await service.PollConfiguredServerAsync(
            configuredServer,
            LaneOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        // A server still listed as online must not have a 0-player offline point in its history, or
        // the graph dips and the ranking average drops for an outage that never happened.
        var graceSnapshot = repository.Snapshots.Single(snapshot => snapshot.CheckedAtUtc == failedCheck);
        Assert.Equal(220, graceSnapshot.Players);
        Assert.Equal(250, graceSnapshot.MaxPlayers);
        Assert.True(graceSnapshot.IsOnline);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_MarksServerOfflineOnceUnreachableForLongerThanTheThreshold()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var lastFailedCheck = firstCheck.Add(LaneOfflineThreshold).AddMinutes(1);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult("example.test", 28015, "Busy Server", "Procedural Map", 220, 250, true, 10, firstCheck, null),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, firstCheck.AddMinutes(3), "timeout"),
            QueryResult("example.test", 28015, null, null, 0, 0, false, null, lastFailedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        for (var poll = 0; poll < 2; poll++)
        {
            await service.PollConfiguredServerAsync(
                configuredServer,
                LaneOfflineThreshold,
                refreshCurrentPlayers: false,
                refreshMapMetadata: false,
                CancellationToken.None);
        }

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            LaneOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal(0, server.Players);
        Assert.Equal(ServerStatus.Offline, result.Status);
        Assert.Equal(firstCheck, server.LastSeenAtUtc);

        var offlineSnapshot = repository.Snapshots.Single(snapshot => snapshot.CheckedAtUtc == lastFailedCheck);
        Assert.False(offlineSnapshot.IsOnline);
        Assert.Equal(0, offlineSnapshot.Players);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotStoreUnusableMapMetadata()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                "RustIQ Test Server",
                "Procedural Map",
                1,
                100,
                true,
                10,
                DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
                null));
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "example.test",
                28015,
                null,
                null,
                null,
                null,
                null,
                null,
                "BattleMetrics",
                null,
                DateTimeOffset.Parse("2026-07-06T12:00:01Z"),
                "metadata unavailable"));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            mapClient,
            repository);

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: true,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Null(server.MapMetadata);
        Assert.Null(result.MapMetadata);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_NormalizesOnlinePlayerCountsWhenPlayersExceedMaxPlayers()
    {
        var configuredServer = new ConfiguredRustServer { Host = "example.test", QueryPort = 28015 };
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                "Busy Server",
                "Procedural Map",
                105,
                32,
                true,
                20,
                DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
                null));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(105, server.Players);
        Assert.Equal(105, server.MaxPlayers);
        Assert.Equal(105, snapshot.Players);
        Assert.Equal(105, snapshot.MaxPlayers);
        Assert.Equal(105, result.Players);
        Assert.Equal(105, result.MaxPlayers);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotEraseExistingNameOrMapWhenOnlineQueryOmitsThem()
    {
        var configuredServer = new ConfiguredRustServer { Name = "Configured Name", Host = "example.test", QueryPort = 28015 };
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "example.test",
            QueryPort = 28015,
            Name = "Existing Live Name",
            Map = "Existing Map",
            Status = ServerStatus.Online,
            LastSeenAtUtc = DateTimeOffset.Parse("2026-07-06T11:59:00Z"),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                null,
                null,
                10,
                100,
                true,
                15,
                DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
                null));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal("Existing Live Name", server.Name);
        Assert.Equal("Existing Map", server.Map);
        Assert.Equal("Existing Live Name", result.Name);
        Assert.Equal("Existing Map", result.Map);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_UsesBattleMetricsPlayerCountWhenConfigured()
    {
        var configuredServer = new ConfiguredRustServer
        {
            Host = "example.test",
            QueryPort = 28015,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics
        };
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                "Busy Server",
                "Procedural Map",
                10,
                50,
                true,
                20,
                checkedAtUtc,
                null));
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient(
            new Dictionary<(string Host, int QueryPort), long?> { [("example.test", 28015)] = 42 },
            new Dictionary<long, BattleMetricsPlayerCountResult?>
            {
                [42] = new BattleMetricsPlayerCountResult(
                    42,
                    77,
                    200,
                    4,
                    RustServerType.Community,
                    true,
                    Country: null,
                    Latitude: null,
                    Longitude: null,
                    LastWipeAtUtc: null,
                    NextWipeAtUtc: null,
                    checkedAtUtc)
            });
        var service = new ServerPollingService(
            queryClient,
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);

        var result = await service.PollConfiguredServerAsync(
            configuredServer,
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
        Assert.Equal(77, server.Players);
        Assert.Equal(200, server.MaxPlayers);
        Assert.Equal(4, server.QueuePlayers);
        Assert.Equal(77, result.Players);
        Assert.Equal(200, result.MaxPlayers);
        Assert.Equal(4, result.QueuePlayers);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_FallsBackToFreshA2sDataWhenBattleMetricsIsUnavailable()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-29T08:00:00Z");
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 42,
            Host = "example.test",
            QueryPort = 28015,
            Name = "Stale Server",
            Players = 77,
            MaxPlayers = 200,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            LastSeenAtUtc = checkedAtUtc.AddDays(-2),
            CreatedAtUtc = checkedAtUtc.AddDays(-10),
            UpdatedAtUtc = checkedAtUtc.AddDays(-2)
        });
        var queryClient = new FakeRustServerQueryClient(
            QueryResult(
                "example.test",
                28015,
                "Fresh Server",
                "Procedural Map",
                12,
                100,
                true,
                15,
                checkedAtUtc,
                null));
        var service = new ServerPollingService(
            queryClient,
            new FakeBattleMetricsPlayerCountClient(),
            new FakeRustServerMapMetadataClient(),
            repository);

        var result = await service.PollConfiguredServerAsync(
            new ConfiguredRustServer
            {
                BattleMetricsServerId = 42,
                Host = "example.test",
                QueryPort = 28015,
                PlayerCountSource = ServerPlayerCountSource.BattleMetrics
            },
            DefaultOfflineThreshold,
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(12, server.Players);
        Assert.Equal(100, server.MaxPlayers);
        Assert.Equal(ServerPlayerCountSource.A2S, server.PlayerCountSource);
        Assert.Equal(checkedAtUtc, server.LastSeenAtUtc);
        Assert.Equal(12, result.Players);
        Assert.Single(repository.Snapshots);
    }

    private static RustServerQueryResult QueryResult(
        string host,
        int queryPort,
        string? name,
        string? map,
        int players,
        int maxPlayers,
        bool isOnline,
        long? pingMilliseconds,
        DateTimeOffset checkedAtUtc,
        string? error)
    {
        return new RustServerQueryResult(
            BattleMetricsServerId: null,
            Address: null,
            Host: host,
            GamePort: null,
            QueryPort: queryPort,
            Country: null,
            Latitude: null,
            Longitude: null,
            LastWipeAtUtc: null,
            NextWipeAtUtc: null,
            Name: name,
            Map: map,
            Players: players,
            MaxPlayers: maxPlayers,
            QueuePlayers: null,
            IsOnline: isOnline,
            PingMilliseconds: pingMilliseconds,
            CheckedAtUtc: checkedAtUtc,
            PlayerCountSource: DefaultPlayerCountSource,
            SummarySource: DefaultSummarySource,
            Error: error);
    }
}
