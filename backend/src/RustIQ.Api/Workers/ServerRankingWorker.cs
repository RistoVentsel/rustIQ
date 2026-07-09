using Microsoft.Extensions.Options;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class ServerRankingWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ServerRankingOptions> options,
    ILogger<ServerRankingWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Server ranking worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(options.Value.RefreshIntervalHours);

        logger.LogInformation(
            "Server ranking worker started. MaxRankedServers: {MaxRankedServers}. RefreshInterval: {RefreshInterval}.",
            options.Value.MaxRankedServers,
            interval);

        await GenerateOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await GenerateOnceAsync(stoppingToken);
        }
    }

    private async Task GenerateOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var rankingService = scope.ServiceProvider.GetRequiredService<ServerRankingService>();
            var rankedCount = await rankingService.GenerateLatestRankingsAsync(cancellationToken);

            logger.LogInformation(
                "Server rankings generated. RankedServers: {RankedServers}.",
                rankedCount);
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "Server ranking generation failed.");
        }
    }
}
