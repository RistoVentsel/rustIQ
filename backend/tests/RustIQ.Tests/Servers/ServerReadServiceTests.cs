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
            CancellationToken.None);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(100, result.Items.Count);
        Assert.Equal(100, repository.LastPagedTake);
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

        var result = await service.GetServerAsync(serverId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.InRange(result.UpdatedSecondsAgo, 0, 30);
        Assert.InRange(result.LastSeenSecondsAgo!.Value, 0, 30);
        Assert.Equal("Moose Maps", result.MapMetadata?.MapName);
        Assert.Equal(1337, result.MapMetadata?.Seed);
        Assert.Equal(4500, result.MapMetadata?.Size);
        Assert.Equal("https://rustmaps.com/map/test", result.MapMetadata?.RustMapsUrl);
        Assert.Equal("https://content.rustmaps.com/thumbnail.webp", result.MapMetadata?.ThumbnailUrl);
    }
}
