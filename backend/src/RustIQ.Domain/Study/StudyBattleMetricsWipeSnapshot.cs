namespace RustIQ.Domain.Study;

/// <summary>
/// What BattleMetrics' public page said a sample server's next wipe would be, at the time it was asked.
/// BattleMetrics keeps no history of this, so it has to be captured before the wipe happens.
/// </summary>
public sealed class StudyBattleMetricsWipeSnapshot
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public long? BattleMetricsServerId { get; set; }

    public DateTimeOffset? NextWipeAtUtc { get; set; }
}
