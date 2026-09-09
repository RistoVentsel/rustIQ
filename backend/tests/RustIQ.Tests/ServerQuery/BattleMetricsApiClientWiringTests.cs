using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RustIQ.Infrastructure;

namespace RustIQ.Tests.ServerQuery;

/// <summary>
/// The BattleMetrics HttpClient is configured in DI rather than in the client class, so nothing else
/// covers it. These assert the request shape without going near the network.
/// </summary>
public sealed class BattleMetricsApiClientWiringTests
{
    [Fact]
    public void ClientSendsAUserAgentAndNoAuthorizationWithoutAToken()
    {
        using var provider = BuildProvider(token: null);

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("BattleMetricsApi");

        Assert.Equal(new Uri("https://api.battlemetrics.com/"), client.BaseAddress);
        Assert.Null(client.DefaultRequestHeaders.Authorization);

        // HttpClient sends no User-Agent by default, and public APIs commonly reject callers without one.
        Assert.Contains("RustIQ", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void ClientSendsBearerTokenWhenOneIsConfigured()
    {
        using var provider = BuildProvider(token: "  secret-token  ");

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("BattleMetricsApi");

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization!.Scheme);

        // Trimmed, because a token pasted into an env file usually arrives with stray whitespace.
        Assert.Equal("secret-token", client.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public void ClientStaysAnonymousWhenTheTokenIsBlank()
    {
        // An unset RUSTIQ_BATTLEMETRICS_TOKEN arrives as an empty string through compose, which must
        // not turn into an empty Bearer header.
        using var provider = BuildProvider(token: "   ");

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("BattleMetricsApi");

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    private static ServiceProvider BuildProvider(string? token)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=none;Username=none;Password=none",
            ["BattleMetricsApi:Enabled"] = "true",
            ["BattleMetricsApi:Token"] = token
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<RustIQ.Application.ServerQuery.BattleMetricsApiOptions>(
            configuration.GetSection(RustIQ.Application.ServerQuery.BattleMetricsApiOptions.SectionName));
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}
