namespace RustIQ.Application.ServerQuery;

public sealed class RustServerQueryOptions
{
    public const string SectionName = "RustServerQuery";

    public int TimeoutMilliseconds { get; init; } = 3000;

    // Attempts per query, each bounded by TimeoutMilliseconds, so an unreachable server costs at most
    // MaxQueryAttempts * TimeoutMilliseconds. A dropped UDP packet is common enough that one silent
    // attempt is not evidence a server is down; set this to 1 to go back to a single attempt.
    public int MaxQueryAttempts { get; init; } = 2;

    // A failed query means "could not reach it this time", not "it is down": UDP packets get dropped
    // and Rust servers restart daily. A server is only called offline once it has been unreachable for
    // this long, so this MUST stay well above the longest interval at which online servers are polled
    // (BattleMetricsSummaryRefresh:NormalOnlineRefreshIntervalMinutes, 3 minutes). At 60s it sat below
    // that interval, so every poll was already past the threshold and a single dropped packet was
    // enough to flip a busy server offline.
    public int OfflineThresholdSeconds { get; init; } = 600;

    // Servers open on a detail page are polled every few seconds, so repeated failure is established
    // much sooner there. Kept separate so the long lane grace does not leave a live page claiming a
    // server is online for ten minutes after it genuinely dies.
    public int WatchedOfflineThresholdSeconds { get; init; } = 60;

    public List<ConfiguredRustServer> Servers { get; init; } = [];
}
