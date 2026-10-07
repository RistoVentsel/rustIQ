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
            "Snapshot retention worker started. RetentionDays: {RetentionDays}. UnreachableServerRetentionDays: {UnreachableServerRetentionDays}. OfflineServerRetentionDays: {OfflineServerRetentionDays}. CleanupInterval: {CleanupInterval}.",
            options.Value.RetentionDays,
            options.Value.UnreachableServerRetentionDays,
            options.Value.OfflineServerRetentionDays,
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
            var deletedSnapshots = await retentionService.CleanupOldSnapshotsAsync(cancellationToken);
            var deletedServers = await retentionService.PruneUnreachableServersAsync(cancellationToken);
            var deletedOfflineServers = await retentionService.PruneOfflineServersAsync(cancellationToken);

            logger.LogInformation(
                "Retention cleanup completed. DeletedSnapshots: {DeletedSnapshots}. DeletedUnreachableServers: {DeletedUnreachableServers}. DeletedOfflineServers: {DeletedOfflineServers}.",
                deletedSnapshots,
                deletedServers,
                deletedOfflineServers);
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
