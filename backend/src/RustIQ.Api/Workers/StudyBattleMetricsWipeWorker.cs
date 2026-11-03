using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Api.Workers;

/// <summary>
/// Asks BattleMetrics' public page for each sample server's next wipe at a fixed interval. These are
/// the reference predictions the A2S based predictor is compared with, and BattleMetrics shows only
/// the current value, so anything not captured before a wipe is gone for good.
/// </summary>
public sealed class StudyBattleMetricsWipeWorker(
    IServiceScopeFactory serviceScopeFactory,
    IBattleMetricsWipeMetadataClient wipeMetadataClient,
    IOptions<StudyOptions> options,
    ILogger<StudyBattleMetricsWipeWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SampleWaitDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.BattleMetricsSnapshotsEnabled)
        {
            logger.LogInformation("Study BattleMetrics wipe worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(options.Value.BattleMetricsSnapshotIntervalHours);
        logger.LogInformation("Study BattleMetrics wipe worker started. Interval: {Interval}.", interval);

        try
        {
            await CaptureOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CaptureOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CaptureOnceAsync(CancellationToken cancellationToken)
    {
        // The probe worker selects the sample; on a first start this waits until it has.
        IReadOnlyList<StudySampleServer> sample;
        while (true)
        {
            await using (var scope = serviceScopeFactory.CreateAsyncScope())
            {
                sample = await scope.ServiceProvider
                    .GetRequiredService<IStudyRepository>()
                    .GetSampleAsync(cancellationToken);
            }

            if (sample.Count > 0)
            {
                break;
            }

            await Task.Delay(SampleWaitDelay, cancellationToken);
        }

        var found = 0;
        foreach (var server in sample)
        {
            try
            {
                // Requests are paced by the shared BattleMetrics pacer, so this loop needs no delay of
                // its own and cannot outrun the limit the rest of RustIQ keeps to.
                var metadata = await wipeMetadataClient.GetWipeMetadataAsync(
                    new ConfiguredRustServer
                    {
                        BattleMetricsServerId = server.BattleMetricsServerId,
                        Name = server.Name,
                        Host = server.Host,
                        QueryPort = server.QueryPort
                    },
                    cancellationToken);

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
                repository.AddBattleMetricsWipeSnapshot(new StudyBattleMetricsWipeSnapshot
                {
                    ServerId = server.ServerId,
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                    BattleMetricsServerId = metadata?.BattleMetricsServerId ?? server.BattleMetricsServerId,
                    NextWipeAtUtc = metadata?.NextWipeAtUtc
                });
                await repository.SaveChangesAsync(cancellationToken);

                if (metadata is not null)
                {
                    found++;
                    if (server.BattleMetricsServerId is null)
                    {
                        // Saves the search request on every later round.
                        await repository.SetBattleMetricsServerIdAsync(
                            server.ServerId,
                            metadata.BattleMetricsServerId,
                            cancellationToken);
                    }
                }
            }
            catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                logger.LogWarning(
                    exception,
                    "Study BattleMetrics wipe capture failed for {Host}:{QueryPort}.",
                    server.Host,
                    server.QueryPort);
            }
        }

        logger.LogInformation(
            "Study BattleMetrics wipe capture completed. SampleServers: {SampleServers}. WithNextWipe: {WithNextWipe}.",
            sample.Count,
            found);
    }
}
