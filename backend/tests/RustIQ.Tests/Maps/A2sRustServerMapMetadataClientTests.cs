using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.Maps;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Maps;

/// <summary>
/// Transport-level cover for the A2S_RULES client. RustServerRuleParserTests covers the parsing; these
/// exercise the socket exchange, which shares its packet layer with the server query client.
/// </summary>
public sealed class A2sRustServerMapMetadataClientTests
{
    [Fact]
    public async Task GetMapMetadataAsync_ReadsRulesThroughTheChallengeHandshake()
    {
        using var server = new FakeA2sServer
        {
            RequireChallenge = true,
            Rules = new Dictionary<string, string>
            {
                ["world.seed"] = "1234567",
                ["world.size"] = "4250",
                ["level_url"] = "https://maps.rustmaps.com/234/abcdef/map.map",
                ["map_image_url"] = "https://content.rustmaps.com/alpha.webp"
            }
        };
        var client = CreateClient();

        var metadata = await client.GetMapMetadataAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.Null(metadata.Error);
        Assert.Equal(1234567, metadata.Seed);
        Assert.Equal(4250, metadata.Size);
        Assert.Equal("https://content.rustmaps.com/alpha.webp", metadata.ThumbnailUrl);
        Assert.Equal("A2S", metadata.Source);
    }

    [Fact]
    public async Task GetMapMetadataAsync_ReportsUnavailableForASilentServer()
    {
        using var server = new FakeA2sServer { Silent = true };
        var client = CreateClient(timeoutMilliseconds: 300);

        var metadata = await client.GetMapMetadataAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.NotNull(metadata.Error);
        Assert.Null(metadata.Seed);
        Assert.Null(metadata.Size);
    }

    private static A2sRustServerMapMetadataClient CreateClient(int timeoutMilliseconds = 2000)
    {
        return new A2sRustServerMapMetadataClient(Options.Create(new RustServerQueryOptions
        {
            TimeoutMilliseconds = timeoutMilliseconds
        }));
    }

    private static ConfiguredRustServer ConfiguredServer(int queryPort)
    {
        return new ConfiguredRustServer
        {
            Host = "127.0.0.1",
            QueryPort = queryPort
        };
    }
}
