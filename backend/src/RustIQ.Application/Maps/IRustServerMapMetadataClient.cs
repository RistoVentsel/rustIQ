namespace RustIQ.Application.Maps;

public interface IRustServerMapMetadataClient
{
    Task<RustServerMapMetadata> GetMapMetadataAsync(CancellationToken cancellationToken);
}
