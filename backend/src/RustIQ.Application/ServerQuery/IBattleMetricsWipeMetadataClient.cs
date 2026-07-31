namespace RustIQ.Application.ServerQuery;

public interface IBattleMetricsWipeMetadataClient
{
    Task<BattleMetricsWipeMetadata?> GetWipeMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken);
}
