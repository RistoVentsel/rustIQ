namespace RustIQ.Domain.Study;

/// <summary>
/// A change in the "born" time a server reports over A2S, recorded by the production polling for
/// every server rather than only the study sample. With the previous values it is one wipe: when it
/// happened, and what RustIQ was showing as the next wipe right before it.
/// </summary>
public sealed class ServerWipeObservation
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset LastWipeAtUtc { get; set; }

    // What RustIQ had stored before this answer. Both are null on the first answer a process sees
    // from a server, and that row is a known starting point rather than a wipe.
    public DateTimeOffset? PreviousLastWipeAtUtc { get; set; }

    public DateTimeOffset? PreviousNextWipeAtUtc { get; set; }

    public bool IsFirstObservationSinceStart { get; set; }

    public string? Keywords { get; set; }
}
