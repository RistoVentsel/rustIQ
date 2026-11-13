using Microsoft.Extensions.Options;
using RustIQ.Application.Study;

namespace RustIQ.Api.Workers;

/// <summary>
/// Records at a fixed interval what RustIQ shows as each sample server's next wipe. BattleMetrics
/// values are read by hand in rounds; with these rows each of them can be compared with RustIQ's
/// prediction from the same hour. Reads only: nothing RustIQ shows or polls is changed.
/// </summary>
public sealed class StudyPredictionSnapshotWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<StudyOptions> options,
    ILogger<StudyPredictionSnapshotWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SampleWaitDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.PredictionSnapshotsEnabled)
        {
            logger.LogInformation("Study prediction snapshot worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.PredictionSnapshotIntervalMinutes);
        logger.LogInformation("Study prediction snapshot worker started. Interval: {Interval}.", interval);

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
        try
        {
            IReadOnlyList<StudyServerWipeState> states;
            while (true)
            {
                // The probe worker selects the sample; on a first start this waits until it has.
                await using (var scope = serviceScopeFactory.CreateAsyncScope())
                {
                    states = await scope.ServiceProvider
                        .GetRequiredService<IStudyRepository>()
                        .GetSampleWipeStatesAsync(cancellationToken);
                }

                if (states.Count > 0)
                {
                    break;
                }

                await Task.Delay(SampleWaitDelay, cancellationToken);
            }

            var snapshots = StudyPredictionSnapshots.Create(states, DateTimeOffset.UtcNow);

            await using (var scope = serviceScopeFactory.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
                repository.AddPredictionSnapshots(snapshots);
                await repository.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "Study prediction snapshot completed. SampleServers: {SampleServers}. WithDisplayedNextWipe: {WithDisplayedNextWipe}.",
                snapshots.Count,
                snapshots.Count(snapshot => snapshot.DisplayedNextWipeAtUtc is not null));
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogError(exception, "Study prediction snapshot failed.");
        }
    }
}
