using System.Diagnostics;
using Microsoft.Extensions.Options;
using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Api.Workers;

/// <summary>
/// Records once a minute what this process costs (CPU, memory, network, A2S packets, BattleMetrics
/// requests), and in the one process that has RecordDatabaseMeasurements on also the database wide
/// figures: row writes per table, the server population and the sample servers' ranks.
/// </summary>
public sealed class StudyMeasurementWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<StudyOptions> options,
    ILogger<StudyMeasurementWorker> logger)
    : BackgroundService
{
    private const string ProcNetDevPath = "/proc/net/dev";

    private DateTimeOffset? _lastSampleRankingUtc;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.MeasurementEnabled)
        {
            logger.LogInformation("Study measurement worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(options.Value.MeasurementIntervalSeconds);
        logger.LogInformation(
            "Study measurement worker started. Role: {Role}. Interval: {Interval}. DatabaseMeasurements: {DatabaseMeasurements}.",
            options.Value.MeasurementRole,
            interval,
            options.Value.RecordDatabaseMeasurements);

        try
        {
            await SampleOnceAsync(stoppingToken);

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SampleOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SampleOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var resourceSample = ReadResourceSample(now);

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();

            StudyPopulationSample[] populationSamples = [];
            IReadOnlyList<StudyTableWriteSample> tableWriteSamples = [];
            IReadOnlyList<StudySampleRanking> sampleRankings = [];

            if (options.Value.RecordDatabaseMeasurements)
            {
                populationSamples = [await repository.GetPopulationAsync(now, cancellationToken)];
                tableWriteSamples = await repository.GetTableWritesAsync(now, cancellationToken);

                var rankingInterval = TimeSpan.FromMinutes(options.Value.SampleRankingIntervalMinutes);
                if (_lastSampleRankingUtc is null || now - _lastSampleRankingUtc.Value >= rankingInterval)
                {
                    sampleRankings = await repository.GetSampleRankingsAsync(now, cancellationToken);
                    _lastSampleRankingUtc = now;
                }
            }

            repository.AddMeasurements([resourceSample], populationSamples, tableWriteSamples, sampleRankings);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogError(exception, "Study measurement sample failed.");
        }
    }

    private StudyResourceSample ReadResourceSample(DateTimeOffset now)
    {
        using var process = Process.GetCurrentProcess();
        var network = ReadNetworkCounters();

        return new StudyResourceSample
        {
            Role = options.Value.MeasurementRole,
            SampledAtUtc = now,
            ProcessStartedAtUtc = process.StartTime.ToUniversalTime(),
            ProcessCpuMilliseconds = (long)process.TotalProcessorTime.TotalMilliseconds,
            WorkingSetBytes = process.WorkingSet64,
            NetworkReceivedBytes = network?.ReceivedBytes,
            NetworkSentBytes = network?.SentBytes,
            A2sPacketsSent = TrafficCounters.A2sPacketsSent,
            BattleMetricsRequests = TrafficCounters.BattleMetricsRequests
        };
    }

    private static (long ReceivedBytes, long SentBytes)? ReadNetworkCounters()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists(ProcNetDevPath))
        {
            return null;
        }

        try
        {
            return ProcNetDev.Parse(File.ReadAllText(ProcNetDevPath));
        }
        catch (IOException)
        {
            return null;
        }
    }
}
