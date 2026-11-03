namespace RustIQ.Domain.Servers;

/// <summary>
/// Which part of RustIQ asked for a poll. Recorded on every snapshot so the polling strategy can be
/// measured per lane: data freshness and query volume both depend on who polled and how often.
/// </summary>
public enum ServerPollSource
{
    Unknown,
    Configured,
    Discovery,
    FastLane,
    NormalOnlineLane,
    SlowOfflineLane,
    VeryStaleOfflineLane,
    Watched,
    RefreshJob,
    BattleMetricsDirectory
}
