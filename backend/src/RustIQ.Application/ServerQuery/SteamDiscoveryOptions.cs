namespace RustIQ.Application.ServerQuery;

public sealed class SteamDiscoveryOptions
{
    public const string SectionName = "SteamDiscovery";

    public bool Enabled { get; init; } = true;

    public string Host { get; init; } = "hl2master.steampowered.com";

    public int Port { get; init; } = 27011;

    public int TimeoutMilliseconds { get; init; } = 5000;

    public int MaxServersPerRefresh { get; init; } = 5000;

    public int RefreshIntervalMinutes { get; init; } = 5;

    public string Filter { get; init; } = "\\appid\\252490";

    public string[] FallbackMasterServers { get; init; } = ["208.64.200.65:27015"];

    public bool AllowBattleMetricsFallback { get; init; } = true;
}
