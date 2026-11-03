namespace RustIQ.Domain.Study;

/// <summary>
/// Cumulative resource counters of one process, written once a minute. The API and the study
/// collector run as separate processes with their own Role, so the cost of the production polling
/// strategy can be read without the measurement's own load mixed in. Rates come from the difference
/// between consecutive rows of the same role; a counter going down means the process restarted.
/// </summary>
public sealed class StudyResourceSample
{
    public long Id { get; set; }

    public string Role { get; set; } = string.Empty;

    public DateTimeOffset SampledAtUtc { get; set; }

    public DateTimeOffset ProcessStartedAtUtc { get; set; }

    public long ProcessCpuMilliseconds { get; set; }

    public long WorkingSetBytes { get; set; }

    // From /proc/net/dev, so only on Linux; inside a container that is the container's own traffic.
    public long? NetworkReceivedBytes { get; set; }

    public long? NetworkSentBytes { get; set; }

    public long A2sPacketsSent { get; set; }

    public long BattleMetricsRequests { get; set; }
}
