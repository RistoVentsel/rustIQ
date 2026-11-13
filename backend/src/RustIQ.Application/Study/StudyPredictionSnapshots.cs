using RustIQ.Application.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Application.Study;

/// <summary>
/// The wipe fields of one sample server as stored in the servers table at capture time.
/// </summary>
public sealed record StudyServerWipeState(Guid ServerId, DateTimeOffset? NextWipeAtUtc, DateTimeOffset? LastWipeAtUtc);

public static class StudyPredictionSnapshots
{
    /// <summary>
    /// Builds one snapshot per sample server. The displayed value is derived with the same rule the
    /// server pages use, so the snapshot records what a visitor saw, not only what was stored.
    /// </summary>
    public static IReadOnlyList<StudyPredictionSnapshot> Create(
        IEnumerable<StudyServerWipeState> states,
        DateTimeOffset capturedAtUtc)
    {
        return states
            .Select(state => new StudyPredictionSnapshot
            {
                ServerId = state.ServerId,
                CapturedAtUtc = capturedAtUtc,
                NextWipeAtUtc = state.NextWipeAtUtc,
                DisplayedNextWipeAtUtc = ServerReadService.GetUpcomingNextWipeAtUtc(state.NextWipeAtUtc, capturedAtUtc),
                LastWipeAtUtc = state.LastWipeAtUtc
            })
            .ToList();
    }
}
