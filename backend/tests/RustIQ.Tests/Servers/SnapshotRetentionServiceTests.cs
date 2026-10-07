using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class SnapshotRetentionServiceTests
{
    [Fact]
    public async Task PruneUnreachableServersAsync_KeepsUnreachableServersThatDiscoveryStillLists()
    {
        var repository = new InMemoryServerRepository();

        // Never reachable over A2S but Steam still lists it - a server refusing queries rather than one
        // that has gone away. Deleting it would only make discovery add it straight back.
        var stillListed = ServerRow("refuses-queries.test", createdDaysAgo: 400, lastSeenAtUtc: null);
        stillListed.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        repository.Servers.Add(stillListed);
        var service = CreateService(repository);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_DropsUnreachableServersDiscoveryHasStoppedListing()
    {
        var repository = new InMemoryServerRepository();
        var goneAway = ServerRow("gone-away.test", createdDaysAgo: 400, lastSeenAtUtc: null);
        goneAway.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-90);
        repository.Servers.Add(goneAway);
        var service = CreateService(repository);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Empty(repository.Servers);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_DropsEndpointsNeverReachedInsideTheWindow()
    {
        var repository = new InMemoryServerRepository();
        var neverReached = ServerRow("never-reached.test", createdDaysAgo: 40, lastSeenAtUtc: null);
        repository.Servers.Add(neverReached);
        var service = CreateService(repository);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Empty(repository.Servers);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_LeavesServersThatWereOnlineToTheOfflineRule()
    {
        var repository = new InMemoryServerRepository();

        // A server with history is PruneOfflineServersAsync's to judge, never this rule's.
        var longDead = ServerRow(
            "was-online.test",
            createdDaysAgo: 400,
            lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-300));
        repository.Servers.Add(longDead);
        var service = CreateService(repository);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_LeavesTheDiscoveryBacklogAlone()
    {
        var repository = new InMemoryServerRepository();

        // Freshly discovered servers have not been polled yet, so they look identical to unreachable
        // ones apart from their age. Deleting those would fight discovery in a loop.
        var justDiscovered = ServerRow("just-discovered.test", createdDaysAgo: 1, lastSeenAtUtc: null);
        repository.Servers.Add(justDiscovered);
        var service = CreateService(repository);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_TakesTheChildRowsWithIt()
    {
        var repository = new InMemoryServerRepository();
        var neverReached = ServerRow("never-reached.test", createdDaysAgo: 40, lastSeenAtUtc: null);
        repository.Servers.Add(neverReached);
        repository.Snapshots.Add(new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = neverReached.Id,
            CheckedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Players = 0,
            MaxPlayers = 0,
            IsOnline = false
        });
        var service = CreateService(repository);

        await service.PruneUnreachableServersAsync(CancellationToken.None);

        // In production this is the database cascade; the double mirrors it so the tests agree.
        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_StopsAtTheDeleteCap()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 10; index++)
        {
            repository.Servers.Add(ServerRow($"dead-{index}.test", createdDaysAgo: 40, lastSeenAtUtc: null));
        }

        var service = CreateService(repository, maxDeletesPerRun: 4);

        var deleted = await service.PruneUnreachableServersAsync(CancellationToken.None);

        Assert.Equal(4, deleted);
        Assert.Equal(6, repository.Servers.Count);
    }

    [Fact]
    public async Task PruneUnreachableServersAsync_ReturnsZeroWhenRetentionDisabled()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(ServerRow("never-reached.test", createdDaysAgo: 40, lastSeenAtUtc: null));
        var service = CreateService(repository, enabled: false);

        Assert.Equal(0, await service.PruneUnreachableServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_DropsServersSilentAndUnlistedForTheWholeWindow()
    {
        var repository = new InMemoryServerRepository();
        var wentAway = ServerRow("went-away.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        wentAway.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
        repository.Servers.Add(wentAway);
        repository.Snapshots.Add(new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = wentAway.Id,
            CheckedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            Players = 12,
            MaxPlayers = 100,
            IsOnline = true
        });
        var service = CreateService(repository);

        var deleted = await service.PruneOfflineServersAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Empty(repository.Servers);
        Assert.Empty(repository.Snapshots);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_KeepsSilentServersDiscoveryStillLists()
    {
        var repository = new InMemoryServerRepository();

        // Still listed, so deleting it would only make discovery add it straight back.
        var stillListed = ServerRow("still-listed.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        stillListed.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        repository.Servers.Add(stillListed);
        var service = CreateService(repository);

        Assert.Equal(0, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_KeepsServersSeenInsideTheWindow()
    {
        var repository = new InMemoryServerRepository();

        // A short outage, even with discovery gone quiet too, keeps the server and its history.
        var briefOutage = ServerRow("brief-outage.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-2));
        briefOutage.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
        repository.Servers.Add(briefOutage);
        var service = CreateService(repository);

        Assert.Equal(0, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_LeavesNeverReachedServersToTheUnreachableRule()
    {
        var repository = new InMemoryServerRepository();
        var neverReached = ServerRow("never-reached.test", createdDaysAgo: 40, lastSeenAtUtc: null);
        repository.Servers.Add(neverReached);
        var service = CreateService(repository);

        Assert.Equal(0, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_NeverDropsAServerMarkedOnline()
    {
        var repository = new InMemoryServerRepository();
        var online = ServerRow("online.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        online.Status = ServerStatus.Online;
        online.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
        repository.Servers.Add(online);
        var service = CreateService(repository);

        Assert.Equal(0, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_StopsAtTheDeleteCap()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 10; index++)
        {
            var server = ServerRow($"gone-{index}.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
            server.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
            repository.Servers.Add(server);
        }

        var service = CreateService(repository, maxDeletesPerRun: 4);

        Assert.Equal(4, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Equal(6, repository.Servers.Count);
    }

    [Fact]
    public async Task PruneOfflineServersAsync_ReturnsZeroWhenRetentionDisabled()
    {
        var repository = new InMemoryServerRepository();
        var server = ServerRow("gone.test", createdDaysAgo: 60, lastSeenAtUtc: DateTimeOffset.UtcNow.AddDays(-10));
        server.LastDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
        repository.Servers.Add(server);
        var service = CreateService(repository, enabled: false);

        Assert.Equal(0, await service.PruneOfflineServersAsync(CancellationToken.None));
        Assert.Single(repository.Servers);
    }

    private static SnapshotRetentionService CreateService(
        InMemoryServerRepository repository,
        bool enabled = true,
        int maxDeletesPerRun = 5000)
    {
        return new SnapshotRetentionService(repository, new SnapshotRetentionOptions
        {
            Enabled = enabled,
            RetentionDays = 14,
            UnreachableServerRetentionDays = 3,
            OfflineServerRetentionDays = 7,
            MaxUnreachableServerDeletesPerRun = maxDeletesPerRun
        });
    }

    private static Server ServerRow(string host, int createdDaysAgo, DateTimeOffset? lastSeenAtUtc)
    {
        var createdAtUtc = DateTimeOffset.UtcNow.AddDays(-createdDaysAgo);
        return new Server
        {
            Id = Guid.NewGuid(),
            Host = host,
            QueryPort = 28015,
            Status = lastSeenAtUtc is null ? ServerStatus.Unknown : ServerStatus.Offline,
            LastSeenAtUtc = lastSeenAtUtc,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_DeletesSnapshotsOlderThanRetentionWindow()
    {
        var repository = new InMemoryServerRepository();
        var serverId = Guid.NewGuid();
        repository.Snapshots.AddRange(
        [
            new ServerSnapshot
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                CheckedAtUtc = DateTimeOffset.UtcNow.AddDays(-20),
                Players = 1,
                MaxPlayers = 100,
                IsOnline = true
            },
            new ServerSnapshot
            {
                Id = Guid.NewGuid(),
                ServerId = serverId,
                CheckedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                Players = 2,
                MaxPlayers = 100,
                IsOnline = true
            }
        ]);
        var service = new SnapshotRetentionService(
            repository,
            new SnapshotRetentionOptions
            {
                Enabled = true,
                RetentionDays = 14,
                CleanupIntervalHours = 12
            });

        var deletedCount = await service.CleanupOldSnapshotsAsync(CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.Single(repository.Snapshots);
        Assert.True(repository.Snapshots[0].CheckedAtUtc > DateTimeOffset.UtcNow.AddDays(-14));
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_ReturnsZeroWhenRetentionDisabled()
    {
        var repository = new InMemoryServerRepository();
        repository.Snapshots.Add(new ServerSnapshot
        {
            Id = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            CheckedAtUtc = DateTimeOffset.UtcNow.AddDays(-20),
            Players = 1,
            MaxPlayers = 100,
            IsOnline = true
        });
        var service = new SnapshotRetentionService(
            repository,
            new SnapshotRetentionOptions
            {
                Enabled = false,
                RetentionDays = 14,
                CleanupIntervalHours = 12
            });

        var deletedCount = await service.CleanupOldSnapshotsAsync(CancellationToken.None);

        Assert.Equal(0, deletedCount);
        Assert.Single(repository.Snapshots);
    }
}
