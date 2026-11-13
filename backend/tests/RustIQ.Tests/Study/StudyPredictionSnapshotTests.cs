using RustIQ.Application.Study;

namespace RustIQ.Tests.Study;

public sealed class StudyPredictionSnapshotTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_KeepsUpcomingNextWipeAsDisplayed()
    {
        var serverId = Guid.NewGuid();
        var nextWipe = CapturedAt.AddDays(1);
        var lastWipe = CapturedAt.AddDays(-6);

        var snapshot = Assert.Single(StudyPredictionSnapshots.Create(
            [new StudyServerWipeState(serverId, nextWipe, lastWipe)],
            CapturedAt));

        Assert.Equal(serverId, snapshot.ServerId);
        Assert.Equal(CapturedAt, snapshot.CapturedAtUtc);
        Assert.Equal(nextWipe, snapshot.NextWipeAtUtc);
        Assert.Equal(nextWipe, snapshot.DisplayedNextWipeAtUtc);
        Assert.Equal(lastWipe, snapshot.LastWipeAtUtc);
    }

    [Fact]
    public void Create_StoresPastNextWipeButShowsNone()
    {
        // The pages hide a next wipe that has already passed; the snapshot keeps the stored value
        // apart, so both what was stored and what was shown stay visible in the data.
        var pastWipe = CapturedAt.AddMinutes(-1);

        var snapshot = Assert.Single(StudyPredictionSnapshots.Create(
            [new StudyServerWipeState(Guid.NewGuid(), pastWipe, null)],
            CapturedAt));

        Assert.Equal(pastWipe, snapshot.NextWipeAtUtc);
        Assert.Null(snapshot.DisplayedNextWipeAtUtc);
    }

    [Fact]
    public void Create_TreatsNextWipeAtCaptureTimeAsStillShown()
    {
        var snapshot = Assert.Single(StudyPredictionSnapshots.Create(
            [new StudyServerWipeState(Guid.NewGuid(), CapturedAt, null)],
            CapturedAt));

        Assert.Equal(CapturedAt, snapshot.DisplayedNextWipeAtUtc);
    }

    [Fact]
    public void Create_ReturnsOneSnapshotPerServer()
    {
        var states = Enumerable.Range(0, 3)
            .Select(_ => new StudyServerWipeState(Guid.NewGuid(), null, null))
            .ToList();

        var snapshots = StudyPredictionSnapshots.Create(states, CapturedAt);

        Assert.Equal(states.Select(state => state.ServerId), snapshots.Select(snapshot => snapshot.ServerId));
        Assert.All(snapshots, snapshot => Assert.Null(snapshot.DisplayedNextWipeAtUtc));
    }
}
