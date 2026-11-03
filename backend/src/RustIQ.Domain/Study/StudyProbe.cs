namespace RustIQ.Domain.Study;

/// <summary>
/// One dense A2S_INFO probe of a sample server. Failed probes are kept too: the share of unanswered
/// queries is one of the measured metrics, and a gap in answers is not the same as a gap in probing.
/// </summary>
public sealed class StudyProbe
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ProbedAtUtc { get; set; }

    public bool Succeeded { get; set; }

    public int? Players { get; set; }

    public int? MaxPlayers { get; set; }

    public int? QueuePlayers { get; set; }

    public int LatencyMilliseconds { get; set; }
}
