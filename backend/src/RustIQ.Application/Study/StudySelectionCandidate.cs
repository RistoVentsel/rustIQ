namespace RustIQ.Application.Study;

public sealed record StudySelectionCandidate(
    Guid ServerId,
    string Host,
    int QueryPort,
    string? Name,
    string? Country,
    long? BattleMetricsServerId,
    int PeakPlayers);
