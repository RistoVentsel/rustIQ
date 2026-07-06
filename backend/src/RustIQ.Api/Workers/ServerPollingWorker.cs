using Microsoft.AspNetCore.SignalR;
using RustIQ.Application.Realtime;
using RustIQ.Application.Servers;
using RustIQ.Api.Realtime;

namespace RustIQ.Api.Workers;

public sealed class ServerPollingWorker(
    IServiceScopeFactory serviceScopeFactory,
    IHubContext<ServerUpdatesHub> hubContext,
    ILogger<ServerPollingWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan ServerInfoInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CurrentPlayersInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MapMetadataInterval = TimeSpan.FromMinutes(10);

    private DateTimeOffset _lastCurrentPlayersRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMapMetadataRefreshUtc = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Server polling worker started. Server info interval: {ServerInfoInterval}. Current players interval: {CurrentPlayersInterval}. Map metadata interval: {MapMetadataInterval}.",
            ServerInfoInterval,
            CurrentPlayersInterval,
            MapMetadataInterval);

        await PollOnceAsync(
            refreshCurrentPlayers: true,
            refreshMapMetadata: true,
            stoppingToken);

        using var timer = new PeriodicTimer(ServerInfoInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            var refreshCurrentPlayers = now - _lastCurrentPlayersRefreshUtc >= CurrentPlayersInterval;
            var refreshMapMetadata = now - _lastMapMetadataRefreshUtc >= MapMetadataInterval;

            await PollOnceAsync(refreshCurrentPlayers, refreshMapMetadata, stoppingToken);
        }
    }

    private async Task PollOnceAsync(
        bool refreshCurrentPlayers,
        bool refreshMapMetadata,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<ServerPollingService>();
            var result = await pollingService.PollConfiguredServerAsync(
                refreshCurrentPlayers,
                refreshMapMetadata,
                cancellationToken);
            var updateMessage = ToUpdateMessage(result);

            await hubContext.Clients
                .Group(ServerUpdatesHub.GetServerGroupName(result.Id))
                .SendAsync("ServerUpdated", updateMessage, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            if (refreshCurrentPlayers)
            {
                _lastCurrentPlayersRefreshUtc = now;
            }

            if (refreshMapMetadata)
            {
                _lastMapMetadataRefreshUtc = now;
            }

            logger.LogInformation(
                "Polled Rust server {Host}:{QueryPort}. Status: {Status}. Players: {Players}/{MaxPlayers}. Refreshed current players: {RefreshedCurrentPlayers}. Refreshed map metadata: {RefreshedMapMetadata}.",
                result.Host,
                result.QueryPort,
                result.Status,
                result.Players,
                result.MaxPlayers,
                refreshCurrentPlayers,
                refreshMapMetadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rust server polling cycle failed.");
        }
    }

    private static ServerUpdateMessage ToUpdateMessage(ServerDto server)
    {
        return new ServerUpdateMessage(
            server.Id,
            server.Players,
            server.MaxPlayers,
            server.Status,
            server.LatestSnapshot.CheckedAtUtc,
            SecondsAgo(server.UpdatedAtUtc, DateTimeOffset.UtcNow),
            server.MapMetadata?.MapName,
            server.MapMetadata?.Seed,
            server.MapMetadata?.Size,
            server.MapMetadata?.RustMapsUrl,
            server.MapMetadata?.ThumbnailUrl);
    }

    private static int SecondsAgo(DateTimeOffset value, DateTimeOffset now)
    {
        return Math.Max(0, (int)(now - value).TotalSeconds);
    }
}
