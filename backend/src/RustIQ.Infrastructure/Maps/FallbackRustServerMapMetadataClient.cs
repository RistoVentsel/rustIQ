using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.Maps;

public sealed class FallbackRustServerMapMetadataClient(
    A2sRustServerMapMetadataClient a2sClient,
    IBattleMetricsWipeMetadataClient battleMetricsClient)
    : IRustServerMapMetadataClient
{
    public async Task<RustServerMapMetadata> GetMapMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        var a2sMetadata = await a2sClient.GetMapMetadataAsync(configuredServer, cancellationToken);
        var hasCurrentNextWipe = configuredServer.NextWipeAtUtc >= a2sMetadata.FetchedAtUtc;

        if (a2sMetadata.NextWipeAtUtc is not null || hasCurrentNextWipe)
        {
            return a2sMetadata;
        }

        var battleMetricsMetadata = await battleMetricsClient.GetWipeMetadataAsync(
            configuredServer,
            cancellationToken);
        if (battleMetricsMetadata is null)
        {
            return a2sMetadata;
        }

        return a2sMetadata with
        {
            LastWipeAtUtc = a2sMetadata.LastWipeAtUtc ?? battleMetricsMetadata.LastWipeAtUtc,
            NextWipeAtUtc = a2sMetadata.NextWipeAtUtc ?? battleMetricsMetadata.NextWipeAtUtc,
            BattleMetricsServerId = battleMetricsMetadata.BattleMetricsServerId
        };
    }
}
