using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Api.Workers;

/// <summary>
/// Stores each sample server's A2S rules description at a fixed interval. Servers often announce
/// their wipe schedule or the next wipe date only in that text, and it changes over a wipe cycle, so
/// it has to be captured while the study runs to be usable by the predictor afterwards.
/// </summary>
public sealed class StudyServerDescriptionWorker(
    IServiceScopeFactory serviceScopeFactory,
    IRustServerDescriptionClient descriptionClient,
    IOptions<StudyOptions> options,
    ILogger<StudyServerDescriptionWorker> logger)
    : BackgroundService
{
    // A rules query is a few packets; a handful at a time keeps this a background trickle next to
    // the probes.
    private const int Concurrency = 8;
    private static readonly TimeSpan SampleWaitDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.DescriptionSnapshotsEnabled)
        {
            logger.LogInformation("Study server description worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(options.Value.DescriptionSnapshotIntervalHours);
        logger.LogInformation("Study server description worker started. Interval: {Interval}.", interval);

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

        try
        {
            using var semaphore = new SemaphoreSlim(Concurrency);
            var descriptions = await Task.WhenAll(sample.Select(async server =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await descriptionClient.GetDescriptionAsync(
                        new ConfiguredRustServer
                        {
                            Host = server.Host,
                            QueryPort = server.QueryPort
                        },
                        cancellationToken);

                    return new StudyServerDescription
                    {
                        ServerId = server.ServerId,
                        CapturedAtUtc = DateTimeOffset.UtcNow,
                        Answered = result.Answered,
                        Description = Truncate(result.Description)
                    };
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
            repository.AddServerDescriptions(descriptions);
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Study server description capture completed. SampleServers: {SampleServers}. Answered: {Answered}. WithDescription: {WithDescription}.",
                sample.Count,
                descriptions.Count(description => description.Answered),
                descriptions.Count(description => description.Description is not null));
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogError(exception, "Study server description capture failed.");
        }
    }

    private static string? Truncate(string? description)
    {
        const int maximumLength = 8192;
        return description is { Length: > maximumLength } ? description[..maximumLength] : description;
    }
}
