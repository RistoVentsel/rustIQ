using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class FacepunchServerDirectoryClientTests
{
    // Shaped exactly like a live api.facepunch.com/api/public/serverlist response, down to the
    // lowercase field names inside an upper case "Servers" array.
    private const string ServerListJson = """
    {
      "PackedTimestamp": 1789361918,
      "Version": 2633,
      "Servers": [
        {
          "hostname": "Rustafied.com - AU Monday",
          "map": "Rustafied Custom Map",
          "players": 707,
          "maxplayers": 600,
          "tags": "mp600,cp583,ptrak,qp117,$rau1,v2633,^w,^v,OC,born1789361918,gmrust,cs163794,ts8",
          "ip": "103.193.80.196",
          "port": 28015,
          "query_port": 28018,
          "loc": "AU",
          "lo": 151.1916,
          "la": -33.9253,
          "con": "OC"
        },
        {
          "hostname": "[EU] Facepunch 15",
          "map": "Procedural Map",
          "players": 172,
          "maxplayers": 300,
          "tags": "mp300,cp172,ptrak,qp0,$r?,v2633,^m,^v,^t,born1788457930,gmrust,cs163794,ts8",
          "ip": "79.137.98.17",
          "port": 28015,
          "query_port": 28016,
          "loc": "fr",
          "lo": 2.3387,
          "la": 48.8582,
          "con": "EU"
        }
      ]
    }
    """;

    [Fact]
    public async Task GetRustServersAsync_ReadsTheLocationSteamAndA2sCannotSupply()
    {
        var client = CreateClient(new StubHandler(ServerListJson));

        var servers = await client.GetRustServersAsync(15000, CancellationToken.None);

        Assert.Equal(2, servers.Count);

        var first = servers[0];
        Assert.Equal("AU", first.Country);
        Assert.Equal(-33.9253, first.Latitude);
        Assert.Equal(151.1916, first.Longitude);

        // "query_port" is what RustIQ polls over A2S; "port" is what players join.
        Assert.Equal("103.193.80.196", first.Host);
        Assert.Equal(28018, first.QueryPort);
        Assert.Equal(28015, first.GamePort);
        Assert.Equal("Rustafied.com - AU Monday", first.Name);
        Assert.Equal("Rustafied Custom Map", first.Map);
        Assert.Equal(117, first.QueuePlayers);
        Assert.Equal(ServerDiscoverySource.Facepunch, first.DiscoverySource);
    }

    [Fact]
    public async Task GetRustServersAsync_NormalisesTheCountryToTheCodeTheRestOfRustIqStores()
    {
        var client = CreateClient(new StubHandler(ServerListJson));

        var servers = await client.GetRustServersAsync(15000, CancellationToken.None);

        Assert.Equal("FR", servers[1].Country);
    }

    [Fact]
    public async Task GetRustServersAsync_ReadsTheServerTypeFromTheSameListing()
    {
        var client = CreateClient(new StubHandler(ServerListJson));

        var servers = await client.GetRustServersAsync(15000, CancellationToken.None);

        // Both are Official: the first is Rustafied's, the second is one Facepunch runs.
        Assert.Equal(RustServerType.Official, servers[0].ServerType);
        Assert.Equal(RustServerType.Official, servers[1].ServerType);
    }

    [Fact]
    public async Task GetRustServersAsync_DropsPlaceholderCoordinatesAndCountries()
    {
        var client = CreateClient(new StubHandler("""
        {
          "Servers": [
            {
              "hostname": "No fix",
              "ip": "203.0.113.30",
              "port": 28015,
              "query_port": 28016,
              "loc": "",
              "la": 0,
              "lo": 0
            }
          ]
        }
        """));

        var server = Assert.Single(await client.GetRustServersAsync(15000, CancellationToken.None));

        // 0,0 is a point in the Atlantic, not a location, and an empty code would render as a broken
        // flag rather than as "unknown".
        Assert.Null(server.Country);
        Assert.Null(server.Latitude);
        Assert.Null(server.Longitude);
    }

    [Fact]
    public async Task GetRustServersAsync_SkipsEntriesWithoutAUsableEndpoint()
    {
        var client = CreateClient(new StubHandler("""
        {
          "Servers": [
            { "hostname": "No address", "query_port": 28016 },
            { "hostname": "No query port", "ip": "203.0.113.31" },
            { "hostname": "Zero query port", "ip": "203.0.113.32", "query_port": 0 },
            { "hostname": "Fine", "ip": "203.0.113.33", "query_port": 28016 }
          ]
        }
        """));

        var server = Assert.Single(await client.GetRustServersAsync(15000, CancellationToken.None));

        Assert.Equal("203.0.113.33", server.Host);
    }

    [Fact]
    public async Task GetRustServersAsync_KeepsOneEntryPerEndpointAndHonoursTheCap()
    {
        var client = CreateClient(new StubHandler("""
        {
          "Servers": [
            { "hostname": "First listing", "ip": "203.0.113.40", "query_port": 28016 },
            { "hostname": "Repeat listing", "ip": "203.0.113.40", "query_port": 28016 },
            { "hostname": "Beyond the cap", "ip": "203.0.113.41", "query_port": 28016 }
          ]
        }
        """));

        var servers = await client.GetRustServersAsync(1, CancellationToken.None);

        Assert.Equal("First listing", Assert.Single(servers).Name);
    }

    [Fact]
    public async Task GetRustServersAsync_ReturnsEmptyWhenTheListIsMissing()
    {
        var client = CreateClient(new StubHandler("""{"Version":2633}"""));

        Assert.Empty(await client.GetRustServersAsync(15000, CancellationToken.None));
    }

    [Fact]
    public async Task GetRustServersAsync_RequestsThePublicListWithoutAKey()
    {
        var handler = new StubHandler(ServerListJson);

        await CreateClient(handler).GetRustServersAsync(15000, CancellationToken.None);

        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Equal("https://api.facepunch.com/api/public/serverlist", requestUri);
    }

    private static FacepunchServerDirectoryClient CreateClient(StubHandler handler)
    {
        return new FacepunchServerDirectoryClient(
            new StubHttpClientFactory(handler),
            Options.Create(new FacepunchDiscoveryOptions()));
    }

    private sealed class StubHttpClientFactory(StubHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.facepunch.com/")
            };
        }
    }

    private sealed class StubHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        private readonly List<string> _requestUris = [];

        public IReadOnlyList<string> RequestUris => _requestUris;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestUris.Add(request.RequestUri!.ToString());

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
