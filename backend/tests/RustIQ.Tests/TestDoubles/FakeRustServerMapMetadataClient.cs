using RustIQ.Application.Maps;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeRustServerMapMetadataClient(
    RustServerMapMetadata? metadata = null)
    : IRustServerMapMetadataClient
{
    public Task<RustServerMapMetadata> GetMapMetadataAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(metadata ?? new RustServerMapMetadata(
            "example.test",
            28015,
            null,
            null,
            null,
            null,
            null,
            null,
            "Test",
            null,
            DateTimeOffset.UtcNow,
            "not configured"));
    }
}
