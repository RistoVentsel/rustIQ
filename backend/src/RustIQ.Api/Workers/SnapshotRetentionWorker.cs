using Microsoft.Extensions.Options;
using RustIQ.Application.Servers;

namespace RustIQ.Api.Workers;

public sealed class SnapshotRetentionWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SnapshotRetentionOptions> options,
    ILogger<SnapshotRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Snapshot retention worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(options.Value.CleanupIntervalHours);

        logger.LogInformation(
            "Snapshot retention worker started. RetentionDays: {RetentionDays}. CleanupInterval: {CleanupInterval}.",
            options.Value.RetentionDays,
            interval);

        await CleanupOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupOnceAsync(stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<SnapshotRetentionService>();
            var deletedCount = await retentionService.CleanupOldSnapshotsAsync(cancellationToken);

            logger.LogInformation(
                "Snapshot retention cleanup completed. DeletedSnapshots: {DeletedSnapshots}.",
                deletedCount);
        }
        catch (Exception exception)
        {
            if (WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                return;
            }

            logger.LogError(exception, "Snapshot retention cleanup failed.");
        }
    }
}
