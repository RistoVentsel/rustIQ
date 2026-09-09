namespace RustIQ.Application.ServerQuery;

public sealed class SteamDiscoveryOptions
{
    public const string SectionName = "SteamDiscovery";

    public bool Enabled { get; init; } = true;

    public string Host { get; init; } = "hl2master.steampowered.com";

    public int Port { get; init; } = 27011;

    public int TimeoutMilliseconds { get; init; } = 5000;

    public int MaxServersPerRefresh { get; init; } = 1000;

    // Valve rate limits master server queries, and a full walk needs one request per ~231 servers.
    public int RequestDelayMilliseconds { get; init; } = 250;

    // Newly discovered servers are polled once so they get a name and a real status instead of
    // sitting in the list as unknown. These polls are sequential, and one can cost
    // RustServerQuery:MaxQueryAttempts * RustServerQuery:TimeoutMilliseconds, so the cap has to stay
    // below RefreshIntervalMinutes at that rate (90 * 6s = 9 minutes against a 10 minute interval).
    // The refresh lanes pick up whatever is left over.
    public int MaxNewServerPollsPerRefresh { get; init; } = 90;

    // Kept in step with MaxNewServerPollsPerRefresh: the first polls of newly discovered servers
    // are sequential, so this interval has to cover them. RefreshBudgetTests pins the relationship.
    public int RefreshIntervalMinutes { get; init; } = 10;

    public string Filter { get; init; } = "\\appid\\252490";

    public string[] FallbackMasterServers { get; init; } = ["208.64.200.65:27015"];

    public bool AllowBattleMetricsFallback { get; init; } = true;
}
