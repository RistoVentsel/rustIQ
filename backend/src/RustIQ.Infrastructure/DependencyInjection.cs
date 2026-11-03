using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RustIQ.Application.News;
using RustIQ.Application.Maps;
using RustIQ.Application.Realtime;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Application.Study;
using RustIQ.Infrastructure.Data;
using RustIQ.Infrastructure.Maps;
using RustIQ.Infrastructure.News;
using RustIQ.Infrastructure.ServerQuery;
using RustIQ.Infrastructure.Servers;
using RustIQ.Infrastructure.Study;

namespace RustIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<RustIqDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        services.AddScoped<ServerPollingService>();
        services.AddScoped<ServerReadService>();
        services.AddScoped<IServerRefreshService>(serviceProvider =>
            new ServerRefreshService(
                serviceProvider.GetRequiredService<IServerRepository>(),
                serviceProvider.GetRequiredService<IRustServerCatalog>(),
                serviceProvider.GetRequiredService<IBattleMetricsServerDirectoryClient>(),
                serviceProvider.GetRequiredService<IBattleMetricsPlayerCountClient>(),
                serviceProvider.GetRequiredService<ServerPollingService>(),
                serviceProvider.GetRequiredService<IOptions<BattleMetricsDiscoveryOptions>>().Value.PageSize,
                serviceProvider.GetRequiredService<IOptions<BattleMetricsDiscoveryOptions>>().Value.MaxPages,
                TimeSpan.FromSeconds(serviceProvider
                    .GetRequiredService<IOptions<RustServerQueryOptions>>()
                    .Value
                    .OfflineThresholdSeconds)));
        services.AddScoped<SnapshotRetentionService>(serviceProvider =>
            new SnapshotRetentionService(
                serviceProvider.GetRequiredService<IServerRepository>(),
                serviceProvider.GetRequiredService<IOptions<SnapshotRetentionOptions>>().Value));
        services.AddScoped<ServerRankingService>(serviceProvider =>
            new ServerRankingService(
                serviceProvider.GetRequiredService<IServerRepository>(),
                serviceProvider.GetRequiredService<IOptions<ServerRankingOptions>>().Value));
        services.AddSingleton<ServerUpdateMessageFactory>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddSingleton<ServerWipeObservationTracker>();
        services.AddSingleton<IRustServerCatalog, FileBackedRustServerCatalog>();
        services.AddSingleton<IBattleMetricsServerDirectoryClient, BattleMetricsServerDirectoryClient>();
        services.AddSingleton<IBattleMetricsPlayerCountClient, BattleMetricsPlayerCountClient>();
        services.AddSingleton<IBattleMetricsPublicPageFetcher, CurlBattleMetricsPublicPageFetcher>();
        services.AddSingleton<IBattleMetricsWipeMetadataClient, BattleMetricsPublicWipeMetadataClient>();
        services.AddSingleton<IRustServerQueryClient, SteamRustServerQueryClient>();
        services.AddSingleton<A2sRustServerMapMetadataClient>();
        services.AddSingleton<IRustServerMapMetadataClient, FallbackRustServerMapMetadataClient>();
        services.AddSingleton(serviceProvider =>
            new BattleMetricsRequestPacer(TimeSpan.FromMilliseconds(serviceProvider
                .GetRequiredService<IOptions<BattleMetricsApiOptions>>()
                .Value
                .MinimumRequestIntervalMilliseconds)));
        services.AddTransient<BattleMetricsRequestPacingHandler>();
        services
            .AddHttpClient("BattleMetricsApi", (serviceProvider, client) =>
            {
                var battleMetricsApiOptions = serviceProvider
                    .GetRequiredService<IOptions<BattleMetricsApiOptions>>()
                    .Value;

                client.BaseAddress = new Uri("https://api.battlemetrics.com/");

                // Sent explicitly: HttpClient sends no User-Agent by default and public APIs commonly
                // reject anonymous callers that omit one.
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RustIQ/1.0");

                if (!string.IsNullOrWhiteSpace(battleMetricsApiOptions.Token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        battleMetricsApiOptions.Token.Trim());
                }
            })
            .AddHttpMessageHandler<BattleMetricsRequestPacingHandler>();
        services.AddHttpClient(
            SteamWebApiServerDiscoveryClient.HttpClientName,
            (serviceProvider, client) =>
            {
                var steamDiscoveryOptions = serviceProvider
                    .GetRequiredService<IOptions<SteamDiscoveryOptions>>()
                    .Value;

                client.BaseAddress = new Uri("https://api.steampowered.com/");
                client.Timeout = TimeSpan.FromMilliseconds(steamDiscoveryOptions.TimeoutMilliseconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RustIQ/1.0");
            });
        services.AddSingleton<ISteamServerDiscoveryClient, SteamWebApiServerDiscoveryClient>();
        services.AddHttpClient(
            FacepunchServerDirectoryClient.HttpClientName,
            (serviceProvider, client) =>
            {
                var facepunchDiscoveryOptions = serviceProvider
                    .GetRequiredService<IOptions<FacepunchDiscoveryOptions>>()
                    .Value;

                client.BaseAddress = new Uri("https://api.facepunch.com/");
                client.Timeout = TimeSpan.FromMilliseconds(facepunchDiscoveryOptions.TimeoutMilliseconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("RustIQ/1.0");
            });
        services.AddSingleton<IFacepunchServerDirectoryClient, FacepunchServerDirectoryClient>();
        services.AddHttpClient<IRustPatchNoteClient, FacepunchRustPatchNoteClient>(client =>
        {
            client.BaseAddress = new Uri("https://rust.facepunch.com/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RustIQ/1.0");
        });

        return services;
    }
}
