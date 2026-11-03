namespace RustIQ.Domain.Study;

/// <summary>
/// A server picked for the dense ground truth sample. The sample is chosen once and then frozen, so
/// every strategy is later compared against the same servers. Host and port are copied here instead
/// of relying on the servers table, because retention may delete a server row mid-study.
/// </summary>
public sealed class StudySampleServer
{
    public Guid ServerId { get; set; }

    public string Host { get; set; } = string.Empty;

    public int QueryPort { get; set; }

    public string? Name { get; set; }

    public string? Country { get; set; }

    public StudyStratum Stratum { get; set; }

    // Highest player count seen in the 24 hours before selection; this is what the stratum is based on.
    public int PeakPlayersAtSelection { get; set; }

    public long? BattleMetricsServerId { get; set; }

    public DateTimeOffset SelectedAtUtc { get; set; }
}
