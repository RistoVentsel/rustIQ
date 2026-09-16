using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class SteamWebApiServerDiscoveryClientTests
{
    private const string ServersJson = """
    {
      "response": {
        "servers": [
          {
            "addr": "203.0.113.10:28016",
            "gameport": 28015,
            "name": "Warbandits 3x AU",
            "map": "Procedural Map",
            "players": 220,
            "max_players": 250,
            "gametype": "mp250,cp220,qp14,^b,^o^z,OC,born1757000000,gmrust"
          },
          {
            "addr": "203.0.113.11:28015",
            "gameport": 28015,
            "name": "Vanilla EU",
            "map": "Barren",
            "players": 12,
            "max_players": 100,
            "gametype": "mp100,cp12,qp0,^m,^v,EU,gmrust"
          }
        ]
      }
    }
    """;

    [Fact]
    public async Task DiscoverServersAsync_ReadsTheServerListFromTheWebApi()
    {
        var handler = new StubHandler(ServersJson);
        var client = CreateClient(handler, apiKey: "test-key");

        var servers = await client.DiscoverServersAsync(5000, CancellationToken.None);

        Assert.Equal(2, servers.Count);

        var first = servers[0];

        // "addr" carries the query port, which is what RustIQ polls; "gameport" is what players join.
        Assert.Equal("203.0.113.10", first.Host);
        Assert.Equal(28016, first.QueryPort);
        Assert.Equal(28015, first.GamePort);
        Assert.Equal("Warbandits 3x AU", first.Name);
        Assert.Equal("Procedural Map", first.Map);

        // Discovery now supplies a name up front, so new servers no longer sit in the list unnamed
        // until their first poll.
        Assert.Equal(ServerDiscoverySource.Steam, first.DiscoverySource);
        Assert.Equal(ServerPlayerCountSource.A2S, first.PlayerCountSource);
        Assert.Null(first.BattleMetricsServerId);
    }

    [Fact]
    public async Task DiscoverServersAsync_ReadsQueueDepthFromTheKeywordString()
    {
        var handler = new StubHandler(ServersJson);
        var client = CreateClient(handler, apiKey: "test-key");

        var servers = await client.DiscoverServersAsync(5000, CancellationToken.None);

        // Steam calls the A2S keyword string "gametype"; Rust reports its queue in there.
        Assert.Equal(14, servers[0].QueuePlayers);
        Assert.Equal(0, servers[1].QueuePlayers);
    }

    [Fact]
    public async Task DiscoverServersAsync_ReadsTheServerTypeFromTheKeywordString()
    {
        var handler = new StubHandler(ServersJson);
        var client = CreateClient(handler, apiKey: "test-key");

        var servers = await client.DiscoverServersAsync(5000, CancellationToken.None);

        // Steam discovery replaced BattleMetrics, which used to be the only thing that set this.
        // Without reading the tags here every discovered server would sit at Unknown.
        Assert.Equal(RustServerType.Modded, servers[0].ServerType);
        Assert.Equal(RustServerType.Community, servers[1].ServerType);
    }

    [Fact]
    public async Task DiscoverServersAsync_SendsTheKeyFilterAndLimit()
    {
        var handler = new StubHandler(ServersJson);
        var client = CreateClient(handler, apiKey: "test-key", maxServers: 250);

        await client.DiscoverServersAsync(5000, CancellationToken.None);

        var requestUri = Assert.Single(handler.RequestUris);
        Assert.Contains("IGameServersService/GetServerList", requestUri);
        Assert.Contains("key=test-key", requestUri);
        Assert.Contains("filter=%5Cappid%5C252490", requestUri);

        // The caller asked for 5000 but the configured cap is lower, so the cap wins.
        Assert.Contains("limit=250", requestUri);
    }

    [Fact]
    public async Task DiscoverServersAsync_SkipsEntriesWithoutAUsableEndpoint()
    {
        var handler = new StubHandler("""
        {
          "response": {
            "servers": [
              { "addr": "", "name": "No address" },
              { "addr": "203.0.113.12", "name": "No port" },
              { "addr": "203.0.113.13:0", "name": "Zero port" },
              { "addr": "203.0.113.14:28015", "name": "Fine" }
            ]
          }
        }
        """);
        var client = CreateClient(handler, apiKey: "test-key");

        var servers = await client.DiscoverServersAsync(5000, CancellationToken.None);

        Assert.Equal("203.0.113.14", Assert.Single(servers).Host);
    }

    [Fact]
    public async Task DiscoverServersAsync_KeepsOneEntryPerEndpoint()
    {
        var handler = new StubHandler("""
        {
          "response": {
            "servers": [
              { "addr": "203.0.113.20:28015", "name": "First listing" },
              { "addr": "203.0.113.20:28015", "name": "Repeat listing" }
            ]
          }
        }
        """);
        var client = CreateClient(handler, apiKey: "test-key");

        var servers = await client.DiscoverServersAsync(5000, CancellationToken.None);

        Assert.Equal("First listing", Assert.Single(servers).Name);
    }

    [Fact]
    public async Task DiscoverServersAsync_ReturnsEmptyWhenSteamSendsNoServers()
    {
        var handler = new StubHandler("""{"response":{}}""");
        var client = CreateClient(handler, apiKey: "test-key");

        Assert.Empty(await client.DiscoverServersAsync(5000, CancellationToken.None));
    }

    [Fact]
    public async Task DiscoverServersAsync_ExplainsAMissingApiKeyWithoutCallingSteam()
    {
        var handler = new StubHandler(ServersJson);
        var client = CreateClient(handler, apiKey: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DiscoverServersAsync(5000, CancellationToken.None));

        Assert.Contains("steamcommunity.com/dev/apikey", exception.Message);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task DiscoverServersAsync_ExplainsARejectedApiKey()
    {
        var handler = new StubHandler("""{"error":"forbidden"}""", HttpStatusCode.Forbidden);
        var client = CreateClient(handler, apiKey: "wrong-key");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DiscoverServersAsync(5000, CancellationToken.None));

        // A rejected key has to be distinguishable from "Steam is down" in the logs.
        Assert.Contains("SteamDiscovery:ApiKey", exception.Message);
    }

    private static SteamWebApiServerDiscoveryClient CreateClient(
        StubHandler handler,
        string? apiKey,
        int maxServers = 5000)
    {
        var options = Options.Create(new SteamDiscoveryOptions
        {
            ApiKey = apiKey,
            MaxServersPerRefresh = maxServers,
            Filter = "\\appid\\252490"
        });

        return new SteamWebApiServerDiscoveryClient(new StubHttpClientFactory(handler), options);
    }

    private sealed class StubHttpClientFactory(StubHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.steampowered.com/")
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
