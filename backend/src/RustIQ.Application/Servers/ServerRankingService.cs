namespace RustIQ.Application.Servers;

public sealed class ServerRankingService(
    IServerRepository serverRepository,
    ServerRankingOptions options)
{
    public Task<int> GenerateLatestRankingsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return serverRepository.ReplaceLatestServerRankingsAsync(
            now,
            Math.Max(1, options.MaxRankedServers),
            Math.Max(1, options.RankingWindowDays),
            Math.Max(1, options.MinimumSnapshots),
            Math.Max(1, options.RecentOnlineWindowHours),
            Math.Max(0, options.MinimumMaxPlayers),
            cancellationToken);
    }
}
