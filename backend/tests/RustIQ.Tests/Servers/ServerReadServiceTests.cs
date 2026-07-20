using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerReadServiceTests
{
    [Fact]
    public async Task GetServersAsync_ClampsPageSizeToMaximum()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 150; index++)
        {
            repository.Servers.Add(new Server
            {
                Id = Guid.NewGuid(),
                Host = $"server-{index}.test",
                QueryPort = 28015,
                Name = $"Server {index}",
                Players = index,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            search: null,
            status: null,
            page: 1,
            pageSize: 1_000,
            sortBy: ServerReadService.SortByPlayers,
            sortDirection: ServerReadService.SortDescending,
            serverTypes: null,
            refreshMode: ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal(100, repository.LastPagedTake);
    }

    [Fact]
    public async Task GetServersAsync_SortsPlayersDescendingByDefault()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "offline-high.test",
                QueryPort = 28015,
                Name = "Offline High",
                Players = 90,
                MaxPlayers = 100,
                Status = ServerStatus.Offline,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "online-low.test",
                QueryPort = 28015,
                Name = "Online Low",
                Players = 5,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "online-high.test",
                QueryPort = 28015,
                Name = "Online High",
                Players = 50,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        ]);

        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            null,
            null,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal("offline-high.test", result.Items[0].Host);
        Assert.Equal("online-high.test", result.Items[1].Host);
        Assert.Equal("online-low.test", result.Items[2].Host);
    }

    [Fact]
    public async Task GetServersAsync_SortsLocationAscending()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "estonia.test",
                QueryPort = 28015,
                Country = "EE",
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "canada.test",
                QueryPort = 28015,
                Country = "CA",
                Players = 50,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        ]);

        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByLocation,
            ServerReadService.SortAscending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal("canada.test", result.Items[0].Host);
        Assert.Equal("estonia.test", result.Items[1].Host);
    }

    [Fact]
    public async Task GetServersAsync_FiltersByServerTypes()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            CreateTypedServer("official.test", RustServerType.Official),
            CreateTypedServer("community.test", RustServerType.Community),
            CreateTypedServer("modded.test", RustServerType.Modded)
        ]);

        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            null,
            null,
            "official,modded",
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        Assert.Contains(result.Items, server => server.Host == "official.test");
        Assert.Contains(result.Items, server => server.Host == "modded.test");
        Assert.DoesNotContain(result.Items, server => server.Host == "community.test");
    }

    [Fact]
    public async Task GetServersAsync_SortsServerTypeAscending()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            CreateTypedServer("unknown.test", RustServerType.Unknown),
            CreateTypedServer("modded.test", RustServerType.Modded),
            CreateTypedServer("community.test", RustServerType.Community),
            CreateTypedServer("official.test", RustServerType.Official)
        ]);

        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByServerType,
            ServerReadService.SortAscending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal(
            ["official.test", "community.test", "modded.test", "unknown.test"],
            result.Items.Select(server => server.Host));
    }

    [Fact]
    public async Task GetServersAsync_SortsNextWipeAscendingAsClosestFirst()
    {
        var repository = CreateNextWipeSortRepository();
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByNextWipe,
            ServerReadService.SortAscending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal(
            ["near.test", "far.test", "expired.test", "unknown.test"],
            result.Items.Select(server => server.Host));
        Assert.Null(result.Items.Single(server => server.Host == "expired.test").NextWipeAtUtc);
    }

    [Fact]
    public async Task GetServersAsync_SortsNextWipeDescendingAsFurthestFirst()
    {
        var repository = CreateNextWipeSortRepository();
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByNextWipe,
            ServerReadService.SortDescending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal(
            ["far.test", "near.test", "expired.test", "unknown.test"],
            result.Items.Select(server => server.Host));
        Assert.Null(result.Items.Single(server => server.Host == "expired.test").NextWipeAtUtc);
    }

    [Fact]
    public async Task GetServersAsync_FiltersNextWipeWithin24Hours()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = CreateWipeFilterRepository(now);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByNextWipe,
            ServerReadService.SortAscending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None,
            ServerReadService.WipeFilterNext24Hours);

        Assert.Equal(["next-24h.test"], result.Items.Select(server => server.Host));
    }

    [Fact]
    public async Task GetServersAsync_FiltersNextWipeWithin7Days()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = CreateWipeFilterRepository(now);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            ServerReadService.SortByNextWipe,
            ServerReadService.SortAscending,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None,
            ServerReadService.WipeFilterNext7Days);

        Assert.Equal(["next-24h.test", "next-7d.test"], result.Items.Select(server => server.Host));
    }

    [Fact]
    public async Task GetServersAsync_FiltersWipedWithin24Hours()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = CreateWipeFilterRepository(now);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            null,
            null,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None,
            ServerReadService.WipeFilterWiped24Hours);

        var item = Assert.Single(result.Items);
        Assert.Equal("wiped-24h.test", item.Host);
    }

    [Fact]
    public async Task GetServersAsync_FiltersServersWithAnyWipeData()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = CreateWipeFilterRepository(now);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            null,
            null,
            null,
            ServerReadService.RefreshNoneMode,
            CancellationToken.None,
            ServerReadService.WipeFilterHasWipeData);

        Assert.Equal(4, result.TotalItems);
        Assert.DoesNotContain(result.Items, server => server.Host == "no-wipe.test");
    }

    [Fact]
    public async Task GetServersAsync_ReturnsPositivePlayerTrendFromRecentSnapshots()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(CreateServer("trend-up.test", serverId));
        repository.Snapshots.AddRange([
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-55), 10),
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-5), 24)
        ]);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshNoneMode, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(14, item.PlayersDeltaLastHour);
        Assert.Equal(24, item.PlayersPeakLastHour);
    }

    [Fact]
    public async Task GetServersAsync_ReturnsNegativePlayerTrendFromRecentSnapshots()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(CreateServer("trend-down.test", serverId));
        repository.Snapshots.AddRange([
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-50), 40),
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-10), 12)
        ]);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshNoneMode, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(-28, item.PlayersDeltaLastHour);
        Assert.Equal(40, item.PlayersPeakLastHour);
    }

    [Fact]
    public async Task GetServersAsync_ReturnsNullPlayerTrendWhenSnapshotsAreMissing()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(CreateServer("no-trend.test", Guid.NewGuid()));
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshNoneMode, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Null(item.PlayersDeltaLastHour);
        Assert.Null(item.PlayersPeakLastHour);
    }

    [Fact]
    public async Task GetServersAsync_IgnoresStaleSnapshotsForPlayerTrend()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(CreateServer("stale-trend.test", serverId));
        repository.Snapshots.Add(CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddHours(-2), 80));
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshNoneMode, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Null(item.PlayersDeltaLastHour);
        Assert.Null(item.PlayersPeakLastHour);
    }

    [Fact]
    public async Task GetServersAsync_ReturnsTrendFromOfflineRecentSnapshots()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(CreateServer("offline-trend.test", serverId));
        repository.Snapshots.AddRange([
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-45), 18, isOnline: false),
            CreateSnapshot(serverId, DateTimeOffset.UtcNow.AddMinutes(-5), 0, isOnline: false)
        ]);
        var service = new ServerReadService(repository);

        var result = await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshNoneMode, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(-18, item.PlayersDeltaLastHour);
        Assert.Equal(18, item.PlayersPeakLastHour);
    }

    [Fact]
    public async Task GetSnapshotsAsync_ClampsLimitToMaximum()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        for (var index = 0; index < 600; index++)
        {
            repository.Snapshots.Add(new ServerSnapshot
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                CheckedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:00Z").AddSeconds(index),
                Players = index,
                MaxPlayers = 600,
                IsOnline = true
            });
        }

        var service = new ServerReadService(repository);

        var result = await service.GetSnapshotsAsync(
            serverId,
            from: null,
            to: null,
            limit: 5_000,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(500, result.Count);
        Assert.Equal(500, repository.LastSnapshotLimit);
        Assert.Equal(599, result[0].Players);
    }

    [Fact]
    public async Task GetServerAsync_ReturnsSecondsAgoAndMapSummary()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            Name = "Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            LastSeenAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            LastCheckedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5),
            MapMetadata = new ServerMapMetadata
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                MapName = "Moose Maps",
                Seed = 1337,
                Size = 4500,
                RustMapsUrl = "https://rustmaps.com/map/test",
                ThumbnailUrl = "https://content.rustmaps.com/thumbnail.webp",
                Source = "BattleMetrics",
                FetchedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        });

        var service = new ServerReadService(repository);

        var result = await service.GetServerAsync(serverId, ServerReadService.RefreshNoneMode, CancellationToken.None);

        Assert.NotNull(result);
        Assert.InRange(result.UpdatedSecondsAgo, 0, 30);
        Assert.InRange(result.LastSeenSecondsAgo!.Value, 0, 30);
        Assert.Equal("Moose Maps", result.MapMetadata?.MapName);
        Assert.Equal(1337, result.MapMetadata?.Seed);
        Assert.Equal(4500, result.MapMetadata?.Size);
        Assert.Equal("https://rustmaps.com/map/test", result.MapMetadata?.RustMapsUrl);
        Assert.Equal("https://content.rustmaps.com/thumbnail.webp", result.MapMetadata?.ThumbnailUrl);
    }

    [Fact]
    public async Task GetServersAsync_RefreshesVisiblePageBeforeMappingWhenRefreshModeIsPage()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "server.test",
            QueryPort = 28015,
            Name = "Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var refreshService = new RecordingServerRefreshService();
        var service = new ServerReadService(repository, refreshService);

        await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshPageMode, CancellationToken.None);

        var refreshBatch = Assert.Single(refreshService.RefreshBatches);
        var refreshedServer = Assert.Single(refreshBatch);
        Assert.Equal("server.test", refreshedServer.Host);
        Assert.Equal(0, refreshService.RefreshAllCallCount);
    }

    [Fact]
    public async Task GetServersAsync_RefreshesAllServersBeforeMappingWhenRefreshModeIsAll()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "server.test",
            QueryPort = 28015,
            Name = "Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var refreshService = new RecordingServerRefreshService();
        var service = new ServerReadService(repository, refreshService);

        await service.GetServersAsync(null, null, 1, 25, null, null, null, ServerReadService.RefreshAllMode, CancellationToken.None);

        Assert.Equal(1, refreshService.RefreshAllCallCount);
    }

    [Fact]
    public async Task GetServersAsync_ReturnsCachedPageWhenRefreshAllFails()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "cached.test",
            QueryPort = 28015,
            Name = "Cached Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var refreshService = new RecordingServerRefreshService { ThrowOnRefreshAll = true };
        var service = new ServerReadService(repository, refreshService);

        var result = await service.GetServersAsync(
            null,
            null,
            1,
            25,
            null,
            null,
            null,
            ServerReadService.RefreshAllMode,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("cached.test", item.Host);
        Assert.Equal(1, refreshService.RefreshAllCallCount);
    }

    [Fact]
    public async Task GetServerAsync_RefreshesRequestedServerBeforeMapping()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            Name = "Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var refreshService = new RecordingServerRefreshService();
        var service = new ServerReadService(repository, refreshService);

        await service.GetServerAsync(serverId, ServerReadService.RefreshPageMode, CancellationToken.None);

        var refreshedServer = Assert.Single(refreshService.RefreshedServers);
        Assert.Equal(serverId, refreshedServer.Id);
    }

    [Fact]
    public async Task GetServerAsync_ReturnsCachedServerWhenRefreshFails()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "cached.test",
            QueryPort = 28015,
            Name = "Cached Server",
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var refreshService = new RecordingServerRefreshService { ThrowOnRefreshServer = true };
        var service = new ServerReadService(repository, refreshService);

        var result = await service.GetServerAsync(
            serverId,
            ServerReadService.RefreshPageMode,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("cached.test", result.Host);
        Assert.Empty(refreshService.RefreshedServers);
    }

    [Fact]
    public async Task GetCurrentPlayersAsync_ReadsCachedPlayersWithoutRefreshingRequestedServer()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        repository.CurrentPlayers.Add(new ServerPlayerCurrent
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            PlayerName = "Alice",
            Score = 10,
            DurationSeconds = 120,
            SeenAtUtc = DateTimeOffset.Parse("2026-07-08T12:00:00Z")
        });
        var refreshService = new RecordingServerRefreshService();
        var service = new ServerReadService(repository, refreshService);

        var result = await service.GetCurrentPlayersAsync(serverId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(refreshService.CurrentPlayersRefreshedServers);
        var player = Assert.Single(result.Players);
        Assert.Equal("Alice", player.PlayerName);
        Assert.Equal(DateTimeOffset.Parse("2026-07-08T12:00:00Z"), result.RefreshedAtUtc);
    }

    [Fact]
    public async Task GetCurrentPlayersAsync_ReturnsCachedPlayersWhenRefreshFails()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        repository.CurrentPlayers.Add(new ServerPlayerCurrent
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            PlayerName = "Alice",
            Score = 10,
            DurationSeconds = 120,
            SeenAtUtc = DateTimeOffset.Parse("2026-07-08T12:00:00Z")
        });
        var refreshService = new RecordingServerRefreshService { ThrowOnRefreshCurrentPlayers = true };
        var service = new ServerReadService(repository, refreshService);

        var result = await service.GetCurrentPlayersAsync(serverId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(refreshService.CurrentPlayersRefreshedServers);
        var player = Assert.Single(result.Players);
        Assert.Equal("Alice", player.PlayerName);
    }

    [Fact]
    public async Task GetCurrentPlayersAsync_ReturnsPlayersByDurationDescending()
    {
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = serverId,
            Host = "server.test",
            QueryPort = 28015,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        repository.CurrentPlayers.AddRange([
            new ServerPlayerCurrent
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                PlayerName = "Short",
                DurationSeconds = 60,
                SeenAtUtc = DateTimeOffset.Parse("2026-07-08T12:00:00Z")
            },
            new ServerPlayerCurrent
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                PlayerName = "Long",
                DurationSeconds = 7200,
                SeenAtUtc = DateTimeOffset.Parse("2026-07-08T12:00:00Z")
            },
            new ServerPlayerCurrent
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                PlayerName = "Medium",
                DurationSeconds = 3600,
                SeenAtUtc = DateTimeOffset.Parse("2026-07-08T12:00:00Z")
            }
        ]);
        var refreshService = new RecordingServerRefreshService();
        var service = new ServerReadService(repository, refreshService);

        var result = await service.GetCurrentPlayersAsync(serverId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Collection(
            result.Players,
            player => Assert.Equal("Long", player.PlayerName),
            player => Assert.Equal("Medium", player.PlayerName),
            player => Assert.Equal("Short", player.PlayerName));
    }

    private static Server CreateTypedServer(string host, RustServerType serverType)
    {
        return new Server
        {
            Id = Guid.NewGuid(),
            Host = host,
            QueryPort = 28015,
            ServerType = serverType,
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Server CreateServer(string host, Guid serverId)
    {
        return new Server
        {
            Id = serverId,
            Host = host,
            QueryPort = 28015,
            Players = 10,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static ServerSnapshot CreateSnapshot(
        Guid serverId,
        DateTimeOffset checkedAtUtc,
        int players,
        bool? isOnline = null)
    {
        return new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            CheckedAtUtc = checkedAtUtc,
            Players = players,
            MaxPlayers = 100,
            IsOnline = isOnline ?? players > 0
        };
    }

    private static InMemoryServerRepository CreateNextWipeSortRepository()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "near.test",
                QueryPort = 28015,
                NextWipeAtUtc = now.AddDays(1),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "far.test",
                QueryPort = 28015,
                NextWipeAtUtc = now.AddDays(7),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "expired.test",
                QueryPort = 28015,
                NextWipeAtUtc = now.AddYears(-2),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "unknown.test",
                QueryPort = 28015,
                NextWipeAtUtc = null,
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        ]);

        return repository;
    }

    private static InMemoryServerRepository CreateWipeFilterRepository(DateTimeOffset now)
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "next-24h.test",
                QueryPort = 28015,
                NextWipeAtUtc = now.AddHours(12),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "next-7d.test",
                QueryPort = 28015,
                NextWipeAtUtc = now.AddDays(3),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "wiped-24h.test",
                QueryPort = 28015,
                LastWipeAtUtc = now.AddHours(-8),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "old-wipe.test",
                QueryPort = 28015,
                LastWipeAtUtc = now.AddDays(-3),
                NextWipeAtUtc = now.AddDays(10),
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new Server
            {
                Id = Guid.NewGuid(),
                Host = "no-wipe.test",
                QueryPort = 28015,
                Players = 10,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        ]);

        return repository;
    }
}
