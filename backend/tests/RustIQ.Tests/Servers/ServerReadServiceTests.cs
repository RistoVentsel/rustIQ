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
            ServerReadService.RefreshNoneMode,
            CancellationToken.None);

        Assert.Equal("canada.test", result.Items[0].Host);
        Assert.Equal("estonia.test", result.Items[1].Host);
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

        await service.GetServersAsync(null, null, 1, 25, null, null, ServerReadService.RefreshPageMode, CancellationToken.None);

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

        await service.GetServersAsync(null, null, 1, 25, null, null, ServerReadService.RefreshAllMode, CancellationToken.None);

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
    public async Task GetCurrentPlayersAsync_RefreshesRequestedServerBeforeReadingPlayers()
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
        var refreshedServer = Assert.Single(refreshService.CurrentPlayersRefreshedServers);
        Assert.Equal(serverId, refreshedServer.Id);
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
}
