using System.Collections.Concurrent;
using RustIQ.Domain.Study;

namespace RustIQ.Application.Study;

/// <summary>
/// Remembers the last wipe signal stored per sample server and hands back a new one only when the
/// server's wipe related answer has changed, so a wipe shows up as one row instead of thousands.
/// </summary>
public sealed class StudyWipeSignalTracker(IReadOnlyDictionary<Guid, StudyWipeSignal> latestSignals)
{
    private readonly ConcurrentDictionary<Guid, StudyWipeSignal> _latestSignals = new(latestSignals);

    public StudyWipeSignal? Observe(
        Guid serverId,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? lastWipeAtUtc,
        DateTimeOffset? advertisedNextWipeAtUtc,
        string? keywords)
    {
        var stableKeywords = StudyKeywords.WithoutVolatileTokens(keywords);

        if (_latestSignals.TryGetValue(serverId, out var previous)
            && previous.LastWipeAtUtc == lastWipeAtUtc
            && previous.AdvertisedNextWipeAtUtc == advertisedNextWipeAtUtc
            && string.Equals(previous.Keywords, stableKeywords, StringComparison.Ordinal))
        {
            return null;
        }

        var signal = new StudyWipeSignal
        {
            ServerId = serverId,
            ObservedAtUtc = observedAtUtc,
            LastWipeAtUtc = lastWipeAtUtc,
            AdvertisedNextWipeAtUtc = advertisedNextWipeAtUtc,
            Keywords = stableKeywords
        };
        _latestSignals[serverId] = signal;

        return signal;
    }
}
