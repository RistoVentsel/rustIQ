using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.TestDoubles;

internal sealed class FakeRustServerMapMetadataClient : IRustServerMapMetadataClient
{
    private readonly Dictionary<(string Host, int QueryPort), RustServerMapMetadata> _metadataByServer;
    private int _callCount;

    public int CallCount => _callCount;

    public FakeRustServerMapMetadataClient(
        RustServerMapMetadata? metadata = null)
        : this(metadata is null
            ? []
            : new Dictionary<(string Host, int QueryPort), RustServerMapMetadata>
            {
                [(metadata.Host, metadata.QueryPort)] = metadata
            })
    {
    }

    public FakeRustServerMapMetadataClient(
        IReadOnlyDictionary<(string Host, int QueryPort), RustServerMapMetadata> metadataByServer)
    {
        _metadataByServer = new Dictionary<(string Host, int QueryPort), RustServerMapMetadata>(metadataByServer);
    }

    public Task<RustServerMapMetadata> GetMapMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);

        if (_metadataByServer.TryGetValue((configuredServer.Host, configuredServer.QueryPort), out var metadata))
        {
            return Task.FromResult(metadata);
        }

        return Task.FromResult(new RustServerMapMetadata(
            configuredServer.Host,
            configuredServer.QueryPort,
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
