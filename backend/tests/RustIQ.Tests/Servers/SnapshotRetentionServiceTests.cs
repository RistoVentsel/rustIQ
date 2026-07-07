using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class SnapshotRetentionServiceTests
{
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
