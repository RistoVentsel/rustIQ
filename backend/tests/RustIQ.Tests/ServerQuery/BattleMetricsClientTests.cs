using System.Net;
using System.Text;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class BattleMetricsClientTests
{
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
