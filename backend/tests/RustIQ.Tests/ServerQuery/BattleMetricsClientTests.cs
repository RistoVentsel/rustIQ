using System.Net;
using System.Text;
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
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
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
