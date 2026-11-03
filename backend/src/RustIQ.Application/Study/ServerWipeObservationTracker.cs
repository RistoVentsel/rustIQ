using System.Collections.Concurrent;

namespace RustIQ.Application.Study;

/// <summary>
/// Remembers the last "born" time seen per server in this process, so the polling path writes a wipe
/// observation only when it changes. Kept in memory instead of compared with the stored LastWipeAtUtc,
/// because map metadata refreshes also write that column and could make a real wipe look unchanged.
/// After a restart every server's first answer is recorded once more, flagged as such.
/// </summary>
public sealed class ServerWipeObservationTracker
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastBornAtUtc = new();

    public WipeObservationKind? Observe(Guid serverId, DateTimeOffset bornAtUtc)
    {
        var isFirst = true;
        var changed = true;

        _lastBornAtUtc.AddOrUpdate(
            serverId,
            bornAtUtc,
            (_, previous) =>
            {
                isFirst = false;
                changed = previous != bornAtUtc;
                return bornAtUtc;
            });

        if (!changed)
        {
            return null;
        }

        return isFirst ? WipeObservationKind.FirstSinceStart : WipeObservationKind.Changed;
    }
}

public enum WipeObservationKind
{
    FirstSinceStart,
    Changed
}
