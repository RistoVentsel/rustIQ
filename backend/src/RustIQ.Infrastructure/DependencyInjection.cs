using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RustIQ.Application.News;
using RustIQ.Application.Maps;
using RustIQ.Application.Realtime;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Infrastructure.Data;
using RustIQ.Infrastructure.Maps;
using RustIQ.Infrastructure.News;
using RustIQ.Infrastructure.ServerQuery;
using RustIQ.Infrastructure.Servers;

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
        services.AddSingleton<IRustServerCatalog, FileBackedRustServerCatalog>();
        services.AddSingleton<ISteamServerDiscoveryClient, SteamServerDiscoveryClient>();
        services.AddSingleton<IBattleMetricsServerDirectoryClient, BattleMetricsServerDirectoryClient>();
        services.AddSingleton<IBattleMetricsPlayerCountClient, BattleMetricsPlayerCountClient>();
        services.AddSingleton<IRustServerQueryClient, SteamRustServerQueryClient>();
        services.AddHttpClient("BattleMetricsApi", client =>
        {
            client.BaseAddress = new Uri("https://api.battlemetrics.com/");
        });
        services.AddHttpClient<IRustServerMapMetadataClient, BattleMetricsRustServerMapMetadataClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.battlemetrics.com/");
        });
        services.AddHttpClient<IRustPatchNoteClient, FacepunchRustPatchNoteClient>(client =>
        {
            client.BaseAddress = new Uri("https://rust.facepunch.com/");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RustIQ/1.0");
        });

        return services;
    }
}
