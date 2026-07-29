using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerRankingServiceTests
{
    [Fact]
    public async Task GenerateLatestRankingsAsync_PreservesLastGoodRankingsWhenNoServersQualify()
    {
        var now = DateTimeOffset.UtcNow;
        var serverId = Guid.NewGuid();
        var repository = new InMemoryServerRepository();
        repository.Rankings.Add(new ServerRanking
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            Rank = 1,
            AveragePlayers = 100,
            PeakPlayers = 150,
            SnapshotCount = 12,
            WindowStartUtc = now.AddDays(-7),
            WindowEndUtc = now.AddHours(-1),
            GeneratedAtUtc = now.AddHours(-1)
        });
        var service = new ServerRankingService(
            repository,
            new ServerRankingOptions
            {
                Enabled = true,
                MaxRankedServers = 250,
                RankingWindowDays = 7,
                MinimumSnapshots = 6,
                RecentOnlineWindowHours = 24,
                MinimumMaxPlayers = 1
            });

        var generatedCount = await service.GenerateLatestRankingsAsync(CancellationToken.None);

        Assert.Equal(0, generatedCount);
        var ranking = Assert.Single(repository.Rankings);
        Assert.Equal(serverId, ranking.ServerId);
        Assert.Equal(1, ranking.Rank);
    }
}
