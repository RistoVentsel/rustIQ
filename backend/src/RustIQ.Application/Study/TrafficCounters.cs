namespace RustIQ.Application.Study;

/// <summary>
/// Process wide counts of outgoing queries, read by the study measurement to report query volume
/// and external requests per process. Static because they count what the process sends, whichever
/// client or scope sent it, and must cost nothing on the hot path.
/// </summary>
public static class TrafficCounters
{
    private static long _a2sPacketsSent;
    private static long _battleMetricsRequests;

    public static long A2sPacketsSent => Interlocked.Read(ref _a2sPacketsSent);

    public static long BattleMetricsRequests => Interlocked.Read(ref _battleMetricsRequests);

    // Every UDP request packet, retries and challenge echoes included, since each one costs the game
    // server an answer.
    public static void CountA2sPacketSent()
    {
        Interlocked.Increment(ref _a2sPacketsSent);
    }

    public static void CountBattleMetricsRequest()
    {
        Interlocked.Increment(ref _battleMetricsRequests);
    }
}
