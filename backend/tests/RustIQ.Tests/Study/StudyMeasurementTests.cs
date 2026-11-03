using RustIQ.Application.Study;

namespace RustIQ.Tests.Study;

public sealed class StudyMeasurementTests
{
    [Fact]
    public void ProcNetDev_SumsEveryInterfaceExceptLoopback()
    {
        const string content = """
            Inter-|   Receive                                                |  Transmit
             face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
                lo:  5000      50    0    0    0     0          0         0     5000      50    0    0    0     0       0          0
              eth0: 1200000    900    0    0    0     0          0         0   340000     800    0    0    0     0       0          0
              eth1:    300      3    0    0    0     0          0         0       70       1    0    0    0     0       0          0
            """;

        var counters = ProcNetDev.Parse(content);

        Assert.Equal((1200300L, 340070L), counters);
    }

    [Fact]
    public void ProcNetDev_ReturnsNullWhenOnlyLoopbackIsListed()
    {
        Assert.Null(ProcNetDev.Parse(" lo: 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16\n"));
    }

    [Fact]
    public void ServerWipeObservationTracker_ReportsFirstAnswerThenOnlyChanges()
    {
        var tracker = new ServerWipeObservationTracker();
        var serverId = Guid.NewGuid();
        var bornAtUtc = DateTimeOffset.Parse("2026-11-03T05:03:11Z");

        Assert.Equal(WipeObservationKind.FirstSinceStart, tracker.Observe(serverId, bornAtUtc));
        Assert.Null(tracker.Observe(serverId, bornAtUtc));
        Assert.Equal(WipeObservationKind.Changed, tracker.Observe(serverId, bornAtUtc.AddDays(2)));
    }
}
