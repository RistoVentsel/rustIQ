using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Api.Workers;

/// <summary>
/// Collects the thesis ground truth: every sample server is probed once per ProbeIntervalSeconds,
/// spread evenly over the interval so the load is a steady trickle rather than a burst per tick.
/// Results go to the study tables only; the servers table and its snapshots are never touched.
/// </summary>
public sealed class StudyProbeWorker(
    IServiceScopeFactory serviceScopeFactory,
    IRustServerQueryClient queryClient,
    IOptions<StudyOptions> options,
    ILogger<StudyProbeWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan SchedulerTick = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SelectionRetryDelay = TimeSpan.FromMinutes(1);

    // Rows kept for a retry after a failed write. Bounded so a database that stays down cannot take
    // the API's memory with it; past this the oldest unsaved probes are dropped and logged.
    private const int MaximumPendingRows = 200_000;

    private readonly ConcurrentQueue<StudyProbe> _pendingProbes = new();
    private readonly ConcurrentQueue<StudyWipeSignal> _pendingSignals = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Study probe worker is disabled.");
            return;
        }

        var sample = await LoadOrSelectSampleAsync(stoppingToken);
        if (sample.Count == 0)
        {
            return;
        }

        StudyWipeSignalTracker tracker;
        await using (var scope = serviceScopeFactory.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
            tracker = new StudyWipeSignalTracker(await repository.GetLatestWipeSignalsAsync(stoppingToken));
        }

        var interval = TimeSpan.FromSeconds(options.Value.ProbeIntervalSeconds);
        var flushInterval = TimeSpan.FromSeconds(options.Value.FlushIntervalSeconds);

        logger.LogInformation(
            "Study probe worker started. SampleServers: {SampleServers}. ProbeInterval: {ProbeInterval}. Concurrency: {Concurrency}.",
            sample.Count,
            interval,
            options.Value.ProbeConcurrency);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var nextDueUtc = sample
            .Select((_, index) => startedAtUtc + interval * ((double)index / sample.Count))
            .ToArray();
        var inFlight = new Task?[sample.Count];
        using var semaphore = new SemaphoreSlim(Math.Max(1, options.Value.ProbeConcurrency));
        var lastFlushUtc = startedAtUtc;

        try
        {
            using var timer = new PeriodicTimer(SchedulerTick);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTimeOffset.UtcNow;
                for (var index = 0; index < sample.Count; index++)
                {
                    if (nextDueUtc[index] > now)
                    {
                        continue;
                    }

                    // A probe still waiting on a slow server skips its slot instead of stacking a
                    // second one on top, which would double the load on exactly the struggling server.
                    if (inFlight[index] is not { IsCompleted: false })
                    {
                        inFlight[index] = ProbeAsync(sample[index], tracker, semaphore, stoppingToken);
                    }

                    // Missed slots are skipped, not caught up in a burst.
                    do
                    {
                        nextDueUtc[index] += interval;
                    }
                    while (nextDueUtc[index] <= now);
                }

                if (now - lastFlushUtc >= flushInterval)
                {
                    await FlushAsync(stoppingToken);
                    lastFlushUtc = now;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                await Task.WhenAll(inFlight.OfType<Task>());
            }
            catch (Exception exception) when (WorkerExceptionHandling.IsExpectedShutdownException(exception, stoppingToken))
            {
            }

            // The host is stopping, so its token is already cancelled; the last batch still deserves
            // a write.
            await FlushAsync(CancellationToken.None);
        }
    }

    private async Task<IReadOnlyList<StudySampleServer>> LoadOrSelectSampleAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();

                var sample = await repository.GetSampleAsync(cancellationToken);
                if (sample.Count > 0)
                {
                    return sample;
                }

                var now = DateTimeOffset.UtcNow;
                var candidates = await repository.GetSelectionCandidatesAsync(
                    seenAfterUtc: now.AddHours(-1),
                    peakWindowStartUtc: now.AddHours(-options.Value.SelectionPeakWindowHours),
                    options.Value.SmallMinPeakPlayers,
                    cancellationToken);
                var selected = StudySampleSelector.Select(candidates, options.Value, now);

                if (selected.Count > 0)
                {
                    repository.AddSample(selected);
                    await repository.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Study sample selected. Candidates: {Candidates}. Popular: {Popular}. Medium: {Medium}. Small: {Small}.",
                        candidates.Count,
                        selected.Count(server => server.Stratum == StudyStratum.Popular),
                        selected.Count(server => server.Stratum == StudyStratum.Medium),
                        selected.Count(server => server.Stratum == StudyStratum.Small));

                    return selected;
                }

                logger.LogWarning("Study sample selection found no candidates; retrying in {Delay}.", SelectionRetryDelay);
            }
            catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
            {
                logger.LogError(exception, "Study sample selection failed; retrying in {Delay}.", SelectionRetryDelay);
            }

            try
            {
                await Task.Delay(SelectionRetryDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        return [];
    }

    private async Task ProbeAsync(
        StudySampleServer server,
        StudyWipeSignalTracker tracker,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Nothing but the endpoint is passed on, so every wipe value in the answer comes from the
            // server itself and none is carried over from what RustIQ or BattleMetrics already knew.
            var result = await queryClient.QueryServerAsync(
                new ConfiguredRustServer
                {
                    Host = server.Host,
                    QueryPort = server.QueryPort
                },
                cancellationToken);

            _pendingProbes.Enqueue(new StudyProbe
            {
                ServerId = server.ServerId,
                ProbedAtUtc = result.CheckedAtUtc,
                Succeeded = result.IsOnline,
                Players = result.IsOnline ? result.Players : null,
                MaxPlayers = result.IsOnline ? result.MaxPlayers : null,
                QueuePlayers = result.IsOnline ? result.QueuePlayers : null,
                LatencyMilliseconds = (int)Math.Min(int.MaxValue, result.PingMilliseconds ?? 0)
            });

            if (result.IsOnline)
            {
                var signal = tracker.Observe(
                    server.ServerId,
                    result.CheckedAtUtc,
                    result.LastWipeAtUtc,
                    result.NextWipeAtUtc,
                    result.Keywords);
                if (signal is not null)
                {
                    _pendingSignals.Enqueue(signal);
                }
            }
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogWarning(
                exception,
                "Study probe failed for {Host}:{QueryPort}.",
                server.Host,
                server.QueryPort);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var probes = Drain(_pendingProbes);
        var signals = Drain(_pendingSignals);
        if (probes.Count == 0 && signals.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IStudyRepository>();
            repository.AddProbes(probes);
            repository.AddWipeSignals(signals);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!WorkerExceptionHandling.IsExpectedShutdownException(exception, cancellationToken))
        {
            logger.LogError(
                exception,
                "Writing study results failed; keeping {Probes} probes and {Signals} wipe signals for the next flush.",
                probes.Count,
                signals.Count);

            Requeue(_pendingProbes, probes, probe => probe.Id = 0);
            Requeue(_pendingSignals, signals, signal => signal.Id = 0);
        }
    }

    private static List<T> Drain<T>(ConcurrentQueue<T> queue)
    {
        var items = new List<T>();
        while (queue.TryDequeue(out var item))
        {
            items.Add(item);
        }

        return items;
    }

    private void Requeue<T>(ConcurrentQueue<T> queue, List<T> items, Action<T> resetKey)
    {
        foreach (var item in items)
        {
            // The failed context may have stamped a key on the entity; a fresh insert must start clean.
            resetKey(item);
            queue.Enqueue(item);
        }

        var dropped = 0;
        while (queue.Count > MaximumPendingRows && queue.TryDequeue(out _))
        {
            dropped++;
        }

        if (dropped > 0)
        {
            logger.LogError("Dropped {Dropped} unsaved study rows to stay under the pending row cap.", dropped);
        }
    }
}
