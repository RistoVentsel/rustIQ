namespace RustIQ.Domain.Study;

/// <summary>
/// What RustIQ showed as a sample server's next wipe at one moment. The servers table keeps only the
/// latest value, so without these rows RustIQ's prediction could not be compared with a BattleMetrics
/// value read at the same time.
/// </summary>
public sealed class StudyPredictionSnapshot
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    // The stored value, which can already lie in the past.
    public DateTimeOffset? NextWipeAtUtc { get; set; }

    // The value the server pages show: the stored next wipe while it is still upcoming, otherwise none.
    public DateTimeOffset? DisplayedNextWipeAtUtc { get; set; }

    public DateTimeOffset? LastWipeAtUtc { get; set; }
}
