using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Application.Study;
using RustIQ.Api.Endpoints;
using RustIQ.Api.Realtime;
using RustIQ.Api.Workers;
using RustIQ.Infrastructure;
using RustIQ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
const string DevCorsPolicy = "DevCorsPolicy";

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 240,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});
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
    .Validate(options => options.MaxQueryAttempts > 0, "Rust server query attempts must be greater than 0.")
    .Validate(options => options.OfflineThresholdSeconds > 0, "Rust server offline threshold must be greater than 0.")
    .Validate(options => options.WatchedOfflineThresholdSeconds > 0, "Rust server watched offline threshold must be greater than 0.")
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
    .AddOptions<BattleMetricsApiOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsApiOptions.SectionName))
    .Validate(
        options => options.MinimumRequestIntervalMilliseconds > 0,
        "BattleMetrics request interval must be greater than 0.")
    .ValidateOnStart();
builder.Services
    .AddOptions<BattleMetricsWipeFallbackOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsWipeFallbackOptions.SectionName))
    .Validate(
        options => !options.Enabled || options.RequestTimeoutSeconds > 0,
        "BattleMetrics public wipe fallback timeout must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SteamDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(SteamDiscoveryOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Filter), "Steam discovery filter is required when enabled.")
    .Validate(options => !options.Enabled || options.TimeoutMilliseconds > 0, "Steam discovery timeout must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.MaxServersPerRefresh > 0, "Steam discovery max servers must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RefreshIntervalMinutes > 0, "Steam discovery refresh interval minutes must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<FacepunchDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(FacepunchDiscoveryOptions.SectionName))
    .Validate(options => !options.Enabled || options.TimeoutMilliseconds > 0, "Facepunch discovery timeout must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.MaxServersPerRefresh > 0, "Facepunch discovery max servers must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<BattleMetricsDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsDiscoveryOptions.SectionName))
    .Validate(options => !options.Enabled || options.PageSize is > 0 and <= 100, "BattleMetrics discovery page size must be between 1 and 100 when enabled.")
    .Validate(options => !options.Enabled || options.MaxPages > 0, "BattleMetrics discovery max pages must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<BattleMetricsSummaryRefreshOptions>()
    .Bind(builder.Configuration.GetSection(BattleMetricsSummaryRefreshOptions.SectionName))
    .Validate(options => !options.Enabled || options.WorkerTickSeconds > 0, "BattleMetrics summary refresh worker tick seconds must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.DirectoryRefreshIntervalMinutes > 0, "BattleMetrics directory refresh interval minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.FastLaneRefreshIntervalSeconds > 0, "BattleMetrics fast lane refresh interval seconds must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.NormalOnlineRefreshIntervalMinutes > 0, "BattleMetrics normal online lane refresh interval minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.SlowOfflineRefreshIntervalMinutes > 0, "BattleMetrics slow offline lane refresh interval minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.VeryStaleOfflineRefreshIntervalMinutes > 0, "BattleMetrics very stale offline lane refresh interval minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.PopularOfflineGraceMinutes > 0, "BattleMetrics popular offline grace minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.SlowOfflineAfterMinutes > 0, "BattleMetrics slow offline after minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.VeryStaleOfflineAfterHours > 0, "BattleMetrics very stale offline after hours must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.PopularRankThreshold > 0, "BattleMetrics popular rank threshold must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.PopularPlayerThreshold > 0, "BattleMetrics popular player threshold must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.PopularPeakPlayerThreshold > 0, "BattleMetrics popular peak player threshold must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RecentPeakWindowMinutes > 0, "BattleMetrics recent peak window minutes must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.RefreshConcurrency > 0, "BattleMetrics refresh concurrency must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.FastLaneBatchSize > 0, "BattleMetrics fast lane batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.NormalOnlineLaneBatchSize > 0, "BattleMetrics normal online lane batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.SlowOfflineLaneBatchSize > 0, "BattleMetrics slow offline lane batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.VeryStaleOfflineLaneBatchSize > 0, "BattleMetrics very stale offline lane batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.SnapshotIntervalMinutes > 0, "BattleMetrics summary refresh snapshot interval minutes must be greater than 0 when enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<MapMetadataRefreshOptions>()
    .Bind(builder.Configuration.GetSection(MapMetadataRefreshOptions.SectionName))
    .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.TimeZoneId), "Map metadata refresh time zone is required when enabled.")
    .Validate(options => !options.Enabled || TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZoneId, out _), "Map metadata refresh time zone must be recognized by the host when enabled.")
    .Validate(options => !options.Enabled || options.LocalHours.Length > 0, "Map metadata refresh requires at least one local hour when enabled.")
    .Validate(options => !options.Enabled || options.LocalHours.All(hour => hour is >= 0 and <= 23), "Map metadata refresh local hours must be between 0 and 23.")
    .Validate(options => !options.Enabled || options.LocalHours.Distinct().Count() == options.LocalHours.Length, "Map metadata refresh local hours must be unique.")
    .Validate(options => !options.Enabled || options.StartupStaleAfterHours > 0, "Map metadata startup stale-after hours must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.BatchSize > 0, "Map metadata refresh batch size must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.MaxServersPerRun > 0, "Map metadata refresh server cap must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.PriorityServersPerRun >= 0, "Map metadata refresh priority server count cannot be negative.")
    .Validate(options => !options.Enabled || options.DelayBetweenServersMilliseconds >= 0, "Map metadata refresh server delay must not be negative.")
    .Validate(options => !options.Enabled || options.DelayBetweenBatchesSeconds >= 0, "Map metadata refresh batch delay must not be negative.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SnapshotRetentionOptions>()
    .Bind(builder.Configuration.GetSection(SnapshotRetentionOptions.SectionName))
    .Validate(options => !options.Enabled || options.RetentionDays > 0, "Snapshot retention days must be greater than 0 when retention is enabled.")
    .Validate(options => !options.Enabled || options.CleanupIntervalHours > 0, "Snapshot retention cleanup interval hours must be greater than 0 when retention is enabled.")
    .Validate(options => !options.Enabled || options.UnreachableServerRetentionDays > 0, "Unreachable server retention days must be greater than 0 when retention is enabled.")
    .Validate(options => !options.Enabled || options.MaxUnreachableServerDeletesPerRun > 0, "Unreachable server delete cap must be greater than 0 when retention is enabled.")
    .ValidateOnStart();
builder.Services
    .AddOptions<ServerRankingOptions>()
    .Bind(builder.Configuration.GetSection(ServerRankingOptions.SectionName))
    .Validate(options => !options.Enabled || options.MaxRankedServers > 0, "Server ranking max ranked servers must be greater than 0 when ranking is enabled.")
    .Validate(options => !options.Enabled || options.RankingWindowDays > 0, "Server ranking window days must be greater than 0 when ranking is enabled.")
    .Validate(options => !options.Enabled || options.RefreshIntervalHours > 0, "Server ranking refresh interval hours must be greater than 0 when ranking is enabled.")
    .Validate(options => !options.Enabled || options.MinimumSnapshots > 0, "Server ranking minimum snapshots must be greater than 0 when ranking is enabled.")
    .Validate(options => !options.Enabled || options.RecentOnlineWindowHours > 0, "Server ranking recent online window hours must be greater than 0 when ranking is enabled.")
    .Validate(options => !options.Enabled || options.MinimumMaxPlayers >= 0, "Server ranking minimum max players must not be negative.")
    .ValidateOnStart();
builder.Services
    .AddOptions<StudyOptions>()
    .Bind(builder.Configuration.GetSection(StudyOptions.SectionName))
    .Validate(options => !options.Enabled || options.ProbeIntervalSeconds > 0, "Study probe interval seconds must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.ProbeConcurrency > 0, "Study probe concurrency must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.FlushIntervalSeconds > 0, "Study flush interval seconds must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.ServersPerStratum > 0, "Study servers per stratum must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || options.SmallMinPeakPlayers > 0, "Study small stratum minimum must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || (options.SmallMinPeakPlayers < options.MediumMinPeakPlayers && options.MediumMinPeakPlayers < options.PopularMinPeakPlayers), "Study stratum thresholds must increase from small to medium to popular.")
    .Validate(options => !options.Enabled || options.MaxCountryShare is > 0 and <= 1, "Study max country share must be greater than 0 and at most 1 when enabled.")
    .Validate(options => !options.Enabled || options.SelectionPeakWindowHours > 0, "Study selection peak window hours must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || !options.BattleMetricsSnapshotsEnabled || options.BattleMetricsSnapshotIntervalHours > 0, "Study BattleMetrics snapshot interval hours must be greater than 0 when enabled.")
    .Validate(options => !options.Enabled || !options.DescriptionSnapshotsEnabled || options.DescriptionSnapshotIntervalHours > 0, "Study description snapshot interval hours must be greater than 0 when enabled.")
    .Validate(options => !options.MeasurementEnabled || !string.IsNullOrWhiteSpace(options.MeasurementRole), "Study measurement role is required when measurement is enabled.")
    .Validate(options => !options.MeasurementEnabled || options.MeasurementRole.Length <= 32, "Study measurement role must be at most 32 characters.")
    .Validate(options => !options.MeasurementEnabled || options.MeasurementIntervalSeconds > 0, "Study measurement interval seconds must be greater than 0 when measurement is enabled.")
    .Validate(options => !options.MeasurementEnabled || options.SampleRankingIntervalMinutes > 0, "Study sample ranking interval minutes must be greater than 0 when measurement is enabled.")
    .ValidateOnStart();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IWatchedServerRegistry, WatchedServerRegistry>();
builder.Services.AddSingleton(Channel.CreateUnbounded<bool>());
builder.Services.AddSingleton<ServerRefreshJobQueue>();
builder.Services.AddSingleton<IServerRefreshJobQueue>(serviceProvider =>
    serviceProvider.GetRequiredService<ServerRefreshJobQueue>());
builder.Services.AddHostedService<ServerPollingWorker>();
builder.Services.AddHostedService<WatchedServerRefreshWorker>();
builder.Services.AddHostedService<SteamDiscoveryWorker>();
builder.Services.AddHostedService<BattleMetricsPlayerCountRefreshWorker>();
builder.Services.AddHostedService<MapMetadataRefreshWorker>();
builder.Services.AddHostedService<SnapshotRetentionWorker>();
builder.Services.AddHostedService<ServerRankingWorker>();
builder.Services.AddHostedService<StudyProbeWorker>();
builder.Services.AddHostedService<StudyBattleMetricsWipeWorker>();
builder.Services.AddHostedService<StudyMeasurementWorker>();
builder.Services.AddHostedService<StudyServerDescriptionWorker>();
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
app.UseRateLimiter();

app.MapServerEndpoints();
app.MapPatchNoteEndpoints();
app.MapHub<ServerUpdatesHub>("/hubs/servers");

app.Run();

static string GetRateLimitPartitionKey(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
