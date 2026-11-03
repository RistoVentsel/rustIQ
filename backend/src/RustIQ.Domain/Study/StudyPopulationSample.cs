namespace RustIQ.Domain.Study;

/// <summary>
/// How many servers RustIQ was tracking at a moment. The lanes share fixed batch sizes across all
/// servers, so how often one server is polled depends on this; a simulation over the study sample
/// scales the query budget down by sample size over these totals.
/// </summary>
public sealed class StudyPopulationSample
{
    public long Id { get; set; }

    public DateTimeOffset SampledAtUtc { get; set; }

    public int TotalServers { get; set; }

    public int OnlineServers { get; set; }

    public int OnlineServersWithPlayers { get; set; }
}
