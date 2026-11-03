namespace RustIQ.Domain.Study;

/// <summary>
/// A sample server's place in RustIQ's ranking at a moment. The fast lane admits servers by rank, and
/// server_rankings only keeps the latest ranking, so without this the current strategy's lane choice
/// could not be reproduced in the simulation.
/// </summary>
public sealed class StudySampleRanking
{
    public long Id { get; set; }

    public DateTimeOffset SampledAtUtc { get; set; }

    public Guid ServerId { get; set; }

    // Null when the server was not ranked at all at that moment.
    public int? Rank { get; set; }
}
