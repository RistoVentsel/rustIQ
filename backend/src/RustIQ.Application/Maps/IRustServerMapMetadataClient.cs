using RustIQ.Application.ServerQuery;

namespace RustIQ.Application.Maps;

public interface IRustServerMapMetadataClient
{
    Task<RustServerMapMetadata> GetMapMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken);
}
