namespace RustIQ.Application.ServerQuery;

public sealed class SteamDiscoveryOptions
{
    public const string SectionName = "SteamDiscovery";

    public bool Enabled { get; init; } = true;

    // Required. Create one at https://steamcommunity.com/dev/apikey - it is free.
    public string? ApiKey { get; init; }

    public int TimeoutMilliseconds { get; init; } = 30000;

    // Steam returns the whole filtered list in one response, so this is bounded by what the rest of
    // the refresh can absorb rather than by the API: every discovered server costs one lookup in
    // ServerPollingService.EnsureConfiguredServersExistAsync.
    public int MaxServersPerRefresh { get; init; } = 5000;

    // Kept in step with MaxNewServerPollsPerRefresh: the first polls of newly discovered servers
    // are sequential, so this interval has to cover them. RefreshBudgetTests pins the relationship.
    public int RefreshIntervalMinutes { get; init; } = 10;

    public string Filter { get; init; } = "\\appid\\252490";

    // Newly discovered servers are polled once so they get a status instead of sitting unknown.
    // These polls are sequential, and one can cost RustServerQuery:MaxQueryAttempts *
    // RustServerQuery:TimeoutMilliseconds, so the cap has to stay below RefreshIntervalMinutes at
    // that rate (90 * 6s = 9 minutes against a 10 minute interval). The refresh lanes pick up
    // whatever is left over.
    public int MaxNewServerPollsPerRefresh { get; init; } = 90;

    public bool AllowBattleMetricsFallback { get; init; } = true;
}
