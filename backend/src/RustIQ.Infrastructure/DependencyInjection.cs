using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Infrastructure.Data;
using RustIQ.Infrastructure.Maps;
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
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddSingleton<IRustServerQueryClient, SteamRustServerQueryClient>();
        services.AddHttpClient<IRustServerMapMetadataClient, BattleMetricsRustServerMapMetadataClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.battlemetrics.com/");
        });

        return services;
    }
}
