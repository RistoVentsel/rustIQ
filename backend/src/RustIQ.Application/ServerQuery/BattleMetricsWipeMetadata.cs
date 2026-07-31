namespace RustIQ.Application.ServerQuery;

public sealed record BattleMetricsWipeMetadata(
    long BattleMetricsServerId,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc);
