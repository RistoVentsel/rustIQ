namespace RustIQ.Application.Servers;

public sealed class ServerRankingOptions
{
    public const string SectionName = "ServerRanking";

    public bool Enabled { get; init; } = true;

    public int MaxRankedServers { get; init; } = 250;

    public int RankingWindowDays { get; init; } = 7;

    public int RefreshIntervalHours { get; init; } = 1;

    public int MinimumSnapshots { get; init; } = 6;

    public int RecentOnlineWindowHours { get; init; } = 24;

    public int MinimumMaxPlayers { get; init; } = 1;
}
