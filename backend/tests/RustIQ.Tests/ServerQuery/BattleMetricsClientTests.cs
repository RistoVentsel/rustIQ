using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class BattleMetricsClientTests
{
    [Fact]
    public async Task GetWipeMetadataAsync_ReadsUpcomingMapWipeFromPublicSettingsPage()
    {
        var nextWipeAtUtc = DateTimeOffset.UtcNow.AddDays(6);
        var blueprintWipeAtUtc = nextWipeAtUtc.AddDays(-1);
        var client = new BattleMetricsPublicWipeMetadataClient(
            new StubBattleMetricsPublicPageFetcher($$"""
                <h4>Upcoming Wipes</h4>
                <p>The following dates/times are converted to your time zone.</p>
                <ul>
                  <li><strong>Blueprints</strong> - <time dateTime="{{blueprintWipeAtUtc:O}}">ignored</time></li>
                  <li><strong>Map</strong> - <time dateTime="{{nextWipeAtUtc:O}}">next map wipe</time></li>
                </ul>
                """),
            Options.Create(new BattleMetricsWipeFallbackOptions { Enabled = true }));

        var result = await client.GetWipeMetadataAsync(
            new ConfiguredRustServer
            {
                BattleMetricsServerId = 42,
                Host = "203.0.113.10",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(42, result.BattleMetricsServerId);
        Assert.Null(result.LastWipeAtUtc);
        Assert.Equal(nextWipeAtUtc, result.NextWipeAtUtc);
    }

    [Fact]
    public void FindServerId_MatchesExactHtmlDecodedServerName()
    {
        const string html = """
            <a title="Different Server" href="/servers/rust/1">Different Server</a>
            <a title="EU | Helli&#x27;s Aimtrain" href="/servers/rust/42">EU | Helli's Aimtrain</a>
            """;

        var result = BattleMetricsPublicWipeMetadataClient.FindServerId(
            html,
            "EU | Helli's Aimtrain");

        Assert.Equal(42, result);
    }

    [Fact]
    public void FindServerId_IgnoresChangingWipeTextAndUsesFirstCurrentDuplicate()
    {
        const string html = """
            <a title="Bestrust Solo/Duo 2x #3 | JUST FULLWIPED 31.07 07:31" href="/servers/rust/42">Current</a>
            <a title="Bestrust Solo/Duo 2x #3 | Wipe 24.07" href="/servers/rust/41">Stale duplicate</a>
            """;

        var result = BattleMetricsPublicWipeMetadataClient.FindServerId(
            html,
            "Bestrust Solo/Duo 2x #3 | Wipe 24.07");

        Assert.Equal(42, result);
    }

    [Fact]
    public void FindServerId_DoesNotTreatMultiplierAsWipeDate()
    {
        const string html = """
            <a title="RUST ROOM 1.7x | MAIN | Wipe 31.07" href="/servers/rust/42">Wrong multiplier</a>
            """;

        var result = BattleMetricsPublicWipeMetadataClient.FindServerId(
            html,
            "RUST ROOM 1.5x | MAIN | Wipe 24.07");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWipeMetadataAsync_SearchesByEndpointWhenServerIdIsMissing()
    {
        var nextWipeAtUtc = DateTimeOffset.UtcNow.AddDays(5);
        var fetcher = new StubBattleMetricsPublicPageFetcher(
            $$"""
              <h4>Upcoming Wipes</h4>
              <ul><li><strong>Map</strong> - <time dateTime="{{nextWipeAtUtc:O}}">next</time></li></ul>
              """,
            """
              <a title="Bestrust Solo/Duo 2x #3 | JUST FULLWIPED 31.07 07:31" href="/servers/rust/42">Server</a>
              """);
        var client = new BattleMetricsPublicWipeMetadataClient(
            fetcher,
            Options.Create(new BattleMetricsWipeFallbackOptions { Enabled = true }));

        var result = await client.GetWipeMetadataAsync(
            new ConfiguredRustServer
            {
                Name = "Bestrust Solo/Duo 2x #3 | Wipe 24.07",
                Host = "205.178.168.148",
                QueryPort = 28015
            },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(42, result.BattleMetricsServerId);
        Assert.Equal(nextWipeAtUtc, result.NextWipeAtUtc);
        Assert.Equal("205.178.168.148:28015", fetcher.LastSearchQuery);
    }

    [Fact]
    public async Task FindServerIdAsync_ReadsTopLevelResourceId()
    {
        using var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "data": [
                {
                  "type": "server",
                  "id": "42",
                  "attributes": {
                    "ip": "203.0.113.10",
                    "portQuery": 28015
                  }
                }
              ]
            }
            """));
        var client = new BattleMetricsPlayerCountClient(new StubHttpClientFactory(handler));

        var serverId = await client.FindServerIdAsync("203.0.113.10", 28015, CancellationToken.None);

        Assert.Equal(42, serverId);
    }

    [Fact]
    public async Task GetRustServersAsync_ReadsTopLevelResourceId()
    {
        using var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "data": [
                {
                  "type": "server",
                  "id": "42",
                  "attributes": {
                    "name": "BattleMetrics Server",
                    "address": "203.0.113.10:28015",
                    "ip": "203.0.113.10",
                    "port": 28015,
                    "portQuery": 28015,
                    "country": "GB",
                    "location": [-0.12, 51.5],
                    "details": {
                      "rust_type": "official",
                      "rust_last_wipe": "2026-07-07T08:00:00Z",
                      "rust_next_wipe": "2026-07-14T08:00:00Z"
                    }
                  }
                }
              ],
              "links": {}
            }
            """));
        var client = new BattleMetricsServerDirectoryClient(new StubHttpClientFactory(handler));

        var servers = await client.GetRustServersAsync(100, 1, CancellationToken.None);

        var server = Assert.Single(servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal("BattleMetrics Server", server.Name);
        Assert.Equal("203.0.113.10", server.Host);
        Assert.Equal(28015, server.QueryPort);
        Assert.Equal("GB", server.Country);
        Assert.Equal(RustServerType.Official, server.ServerType);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
    }

    [Fact]
    public async Task GetPlayerCountAsync_ReadsServerType()
    {
        using var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "data": {
                "type": "server",
                "id": "42",
                "attributes": {
                  "players": 77,
                  "maxPlayers": 200,
                  "status": "online",
                  "details": {
                    "rust_type": "modded",
                    "rust_queued_players": 6
                  }
                }
              }
            }
            """));
        var client = new BattleMetricsPlayerCountClient(new StubHttpClientFactory(handler));

        var result = await client.GetPlayerCountAsync(42, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(RustServerType.Modded, result.ServerType);
        Assert.Equal(6, result.QueuePlayers);
    }

    [Fact]
    public async Task GetPlayerCountAsync_PrefersRustBornWhenRustLastWipeIsStale()
    {
        var staleLastWipe = DateTimeOffset.UtcNow.AddDays(-365);
        var currentBornAt = DateTimeOffset.UtcNow.AddDays(-1);
        var nextMapWipe = DateTimeOffset.UtcNow.AddDays(6);
        var nextFullWipe = DateTimeOffset.UtcNow.AddDays(27);

        using var handler = new StubHttpMessageHandler(_ => JsonResponse($$"""
            {
              "data": {
                "type": "server",
                "id": "4729828",
                "attributes": {
                  "players": 719,
                  "maxPlayers": 750,
                  "status": "online",
                  "details": {
                    "rust_last_wipe": "{{staleLastWipe:O}}",
                    "rust_born": "{{currentBornAt:O}}",
                    "rust_next_wipe_map": "{{nextMapWipe:O}}",
                    "rust_next_wipe_full": "{{nextFullWipe:O}}",
                    "rust_wipes": [
                      {
                        "type": "map",
                        "timestamp": "{{nextMapWipe:O}}"
                      },
                      {
                        "type": "full",
                        "timestamp": "{{nextFullWipe:O}}"
                      }
                    ]
                  }
                }
              }
            }
            """));
        var client = new BattleMetricsPlayerCountClient(new StubHttpClientFactory(handler));

        var result = await client.GetPlayerCountAsync(4729828, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(currentBornAt, result.LastWipeAtUtc);
        Assert.Equal(nextMapWipe, result.NextWipeAtUtc);
    }

    [Fact]
    public async Task GetPlayerCountAsync_PrefersNewerRustBornOverRecentForcedWipe()
    {
        var forcedWipeAt = DateTimeOffset.UtcNow.AddDays(-9);
        var currentBornAt = DateTimeOffset.UtcNow.AddHours(-2);

        using var handler = new StubHttpMessageHandler(_ => JsonResponse($$"""
            {
              "data": {
                "type": "server",
                "id": "42",
                "attributes": {
                  "players": 10,
                  "maxPlayers": 100,
                  "status": "online",
                  "details": {
                    "rust_last_wipe": "{{forcedWipeAt:O}}",
                    "rust_born": "{{currentBornAt:O}}"
                  }
                }
              }
            }
            """));
        var client = new BattleMetricsPlayerCountClient(new StubHttpClientFactory(handler));

        var result = await client.GetPlayerCountAsync(42, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(currentBornAt, result.LastWipeAtUtc);
    }

    [Fact]
    public async Task GetRustServerSummariesAsync_ThrowsRateLimitExceptionWithPartialSummaries()
    {
        var callCount = 0;
        using var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            if (callCount == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            return JsonResponse("""
                {
                  "data": [
                    {
                      "type": "server",
                      "id": "42",
                      "attributes": {
                        "name": "BattleMetrics Server",
                        "address": "203.0.113.10:28015",
                        "ip": "203.0.113.10",
                        "port": 28015,
                        "portQuery": 28015,
                        "players": 77,
                        "maxPlayers": 200,
                        "status": "online"
                      }
                    }
                  ],
                  "links": {
                    "next": "https://api.battlemetrics.com/servers?filter[game]=rust&page[size]=100&page[offset]=100"
                  }
                }
                """);
        });
        var client = new BattleMetricsServerDirectoryClient(new StubHttpClientFactory(handler));

        var exception = await Assert.ThrowsAsync<BattleMetricsRateLimitException>(() =>
            client.GetRustServerSummariesAsync(100, 20, CancellationToken.None));

        var summary = Assert.Single(exception.PartialSummaries);
        Assert.Equal(42, summary.BattleMetricsServerId);
        Assert.Equal(77, summary.Players);
        Assert.Equal(2, callCount);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.battlemetrics.com/")
            };
        }
    }

    private sealed class StubBattleMetricsPublicPageFetcher(
        string html,
        string? searchHtml = null)
        : IBattleMetricsPublicPageFetcher
    {
        public string? LastSearchQuery { get; private set; }

        public Task<string?> FetchRustSettingsAsync(
            long battleMetricsServerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(html);
        }

        public Task<string?> SearchRustServersAsync(
            string query,
            CancellationToken cancellationToken)
        {
            LastSearchQuery = query;
            return Task.FromResult<string?>(searchHtml ?? html);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(send(request));
        }
    }
}
