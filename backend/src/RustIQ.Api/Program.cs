using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Api.Endpoints;
using RustIQ.Api.Realtime;
using RustIQ.Api.Workers;
using RustIQ.Infrastructure;
using RustIQ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
const string DevCorsPolicy = "DevCorsPolicy";

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalhost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
                var isLoopback = string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);

                return (isLocalhost || isLoopback)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services
    .AddOptions<RustServerQueryOptions>()
    .Bind(builder.Configuration.GetSection(RustServerQueryOptions.SectionName))
    .Validate(options => options.TimeoutMilliseconds > 0, "Rust server query timeout must be greater than 0.")
    .Validate(options => options.OfflineThresholdSeconds > 0, "Rust server offline threshold must be greater than 0.")
    .Validate(options => options.Servers.All(server => !string.IsNullOrWhiteSpace(server.Host)), "Each configured Rust server must have a host.")
    .Validate(options => options.Servers.All(server => server.QueryPort is > 0 and <= 65535), "Each configured Rust server query port must be between 1 and 65535.")
    .Validate(options => options.Servers
        .GroupBy(server => $"{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}")
        .All(group => group.Count() == 1), "Configured Rust servers must be unique by host and query port.")
    .ValidateOnStart();
builder.Services
    .AddOptions<RustServerCatalogImportOptions>()
    .Bind(builder.Configuration.GetSection(RustServerCatalogImportOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.JsonFilePath), "Rust server catalog import requires JsonFilePath when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SteamDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(SteamDiscoveryOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host), "Steam discovery host is required when enabled.")
    .Validate(options => !options.Enabled || options.Port is > 0 and <= 65535, "Steam discovery port must be between 1 and 65535 when enabled.")
    .Validate(options => !options.Enabled || options.TimeoutMilliseconds > 0, "Steam discovery timeout must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.MaxServersPerRefresh > 0, "Steam discovery max servers must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RefreshIntervalMinutes > 0, "Steam discovery refresh interval minutes must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<BattleMetricsDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsDiscoveryOptions.SectionName))
    .Validate(options => !options.Enabled || options.PageSize is > 0 and <= 100, "BattleMetrics discovery page size must be between 1 and 100 when enabled.")
    .Validate(options => !options.Enabled || options.MaxPages > 0, "BattleMetrics discovery max pages must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RefreshIntervalMinutes > 0, "BattleMetrics discovery refresh interval minutes must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<BattleMetricsSummaryRefreshOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsSummaryRefreshOptions.SectionName))
    .Validate(options => !options.Enabled || options.BatchSize > 0, "BattleMetrics summary refresh batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RefreshIntervalSeconds > 0, "BattleMetrics summary refresh interval seconds must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.StaleAfterSeconds > 0, "BattleMetrics summary refresh stale-after seconds must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SnapshotRetentionOptions>()
    .Bind(builder.Configuration.GetSection(SnapshotRetentionOptions.SectionName))
    .Validate(options => !options.Enabled || options.RetentionDays > 0, "Snapshot retention days must be greater than 0 when retention is enabled.")
    .Validate(options => !options.Enabled || options.CleanupIntervalHours > 0, "Snapshot retention cleanup interval hours must be greater than 0 when retention is enabled.")
    .ValidateOnStart();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(Channel.CreateUnbounded<bool>());
builder.Services.AddSingleton<ServerRefreshJobQueue>();
builder.Services.AddSingleton<IServerRefreshJobQueue>(serviceProvider =>
    serviceProvider.GetRequiredService<ServerRefreshJobQueue>());
builder.Services.AddHostedService<ServerPollingWorker>();
builder.Services.AddHostedService<SteamDiscoveryWorker>();
builder.Services.AddHostedService<BattleMetricsPlayerCountRefreshWorker>();
builder.Services.AddHostedService<SnapshotRetentionWorker>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<ServerRefreshJobQueue>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RustIqDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(DevCorsPolicy);

app.MapServerEndpoints();
app.MapHub<ServerUpdatesHub>("/hubs/servers");

app.Run();
