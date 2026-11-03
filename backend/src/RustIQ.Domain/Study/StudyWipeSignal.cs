namespace RustIQ.Domain.Study;

/// <summary>
/// The wipe related part of a sample server's A2S answer, stored only when it changes. A new row with
/// a later LastWipeAtUtc marks a wipe; the rows before it are the inputs a predictor could have used.
/// </summary>
public sealed class StudyWipeSignal
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }

    // The server's "born" keyword: when the current save was created.
    public DateTimeOffset? LastWipeAtUtc { get; set; }

    // What RustIQ derives from the server's own wipe schedule tags alone, without BattleMetrics.
    public DateTimeOffset? AdvertisedNextWipeAtUtc { get; set; }

    // Keywords minus the tokens that change on every answer (player and queue counts).
    public string? Keywords { get; set; }
}
