namespace RustIQ.Domain.Study;

/// <summary>
/// A sample server's description from its A2S rules at the time it was asked. Production parses wipe
/// schedules out of this text but keeps only the resulting time, so the text itself is stored here
/// for the predictor and for checking that parsing afterwards.
/// </summary>
public sealed class StudyServerDescription
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public bool Answered { get; set; }

    public string? Description { get; set; }
}
