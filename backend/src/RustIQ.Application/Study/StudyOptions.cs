namespace RustIQ.Application.Study;

/// <summary>
/// The thesis measurement: a frozen, stratified sample of servers probed far more often than any
/// polling strategy under comparison, plus regular captures of what BattleMetrics says about their
/// next wipe. Off by default; nothing here changes what RustIQ shows or how it polls.
/// </summary>
public sealed class StudyOptions
{
    public const string SectionName = "Study";

    public bool Enabled { get; init; }

    // Must stay below the fastest lane interval (BattleMetricsSummaryRefresh:FastLaneRefreshIntervalSeconds),
    // or the sample is no denser than what it is supposed to be the ground truth for.
    public int ProbeIntervalSeconds { get; init; } = 15;

    public int ProbeConcurrency { get; init; } = 32;

    public int FlushIntervalSeconds { get; init; } = 5;

    public int ServersPerStratum { get; init; } = 150;

    // Strata are by the highest player count seen in the 24 hours before selection.
    public int PopularMinPeakPlayers { get; init; } = 100;

    public int MediumMinPeakPlayers { get; init; } = 10;

    public int SmallMinPeakPlayers { get; init; } = 1;

    // No single country may take more than this share of a stratum while other countries still have
    // candidates, so the sample spreads across regions instead of mirroring the largest host country.
    public double MaxCountryShare { get; init; } = 0.25;

    // Fixed so the same candidates always give the same sample, which keeps the selection repeatable.
    public int SelectionSeed { get; init; } = 20261104;

    public int SelectionPeakWindowHours { get; init; } = 24;

    public bool BattleMetricsSnapshotsEnabled { get; init; } = true;

    public int BattleMetricsSnapshotIntervalHours { get; init; } = 12;

    // Resource sampling is separate from Enabled: the API process records its own cost too, while the
    // probes run only in the study process.
    public bool MeasurementEnabled { get; init; }

    // Tells the processes apart in study_resource_samples, e.g. "api" and "study".
    public string MeasurementRole { get; init; } = "api";

    public int MeasurementIntervalSeconds { get; init; } = 60;

    // Database wide figures (table writes, server population, sample ranks) are the same whichever
    // process reads them, so only one process should record them.
    public bool RecordDatabaseMeasurements { get; init; }

    public int SampleRankingIntervalMinutes { get; init; } = 60;
}
