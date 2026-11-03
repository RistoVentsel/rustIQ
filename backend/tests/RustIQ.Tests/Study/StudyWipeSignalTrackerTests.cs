using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Tests.Study;

public sealed class StudyWipeSignalTrackerTests
{
    private const string Keywords = "mp200,cp61,ptrak,qp0,$r?,v2633,^b,^o^z,NA,^t,born1789048903,gmrust,cs163794,ts16";

    private static readonly Guid ServerId = Guid.NewGuid();
    private static readonly DateTimeOffset BornAtUtc = DateTimeOffset.FromUnixTimeSeconds(1789048903);
    private static readonly DateTimeOffset NextWipeAtUtc = DateTimeOffset.Parse("2026-11-05T19:00:00Z");
    private static readonly DateTimeOffset ObservedAtUtc = DateTimeOffset.Parse("2026-11-04T12:00:00Z");

    [Fact]
    public void WithoutVolatileTokens_DropsCountsAndRelayButKeepsWipeTags()
    {
        Assert.Equal(
            "ptrak,v2633,^b,^o^z,NA,^t,born1789048903,gmrust,cs163794,ts16",
            StudyKeywords.WithoutVolatileTokens(Keywords));
    }

    [Fact]
    public void WithoutVolatileTokens_KeepsWordTagsThatOnlyStartLikeACount()
    {
        Assert.Equal("cpvp,born1", StudyKeywords.WithoutVolatileTokens("cpvp,cp12,born1"));
    }

    [Fact]
    public void Observe_StoresOnlyTheFirstOfIdenticalAnswers()
    {
        var tracker = new StudyWipeSignalTracker(new Dictionary<Guid, StudyWipeSignal>());

        var first = tracker.Observe(ServerId, ObservedAtUtc, BornAtUtc, NextWipeAtUtc, Keywords);
        // Only the player counts differ, which must not count as a change. Large servers move their
        // max players with demand, as seen on live servers during local testing.
        var second = tracker.Observe(
            ServerId,
            ObservedAtUtc.AddSeconds(15),
            BornAtUtc,
            NextWipeAtUtc,
            Keywords.Replace("cp61", "cp64").Replace("mp200", "mp250"));

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Observe_StoresANewSignalWhenTheServerWipes()
    {
        var tracker = new StudyWipeSignalTracker(new Dictionary<Guid, StudyWipeSignal>());
        tracker.Observe(ServerId, ObservedAtUtc, BornAtUtc, NextWipeAtUtc, Keywords);
        var wipedAtUtc = NextWipeAtUtc.AddMinutes(4);

        var signal = tracker.Observe(
            ServerId,
            wipedAtUtc.AddSeconds(20),
            wipedAtUtc,
            NextWipeAtUtc.AddDays(14),
            Keywords.Replace("born1789048903", $"born{wipedAtUtc.ToUnixTimeSeconds()}"));

        Assert.NotNull(signal);
        Assert.Equal(wipedAtUtc, signal.LastWipeAtUtc);
    }

    [Fact]
    public void Observe_ContinuesFromTheSignalsStoredBeforeARestart()
    {
        var tracker = new StudyWipeSignalTracker(new Dictionary<Guid, StudyWipeSignal>
        {
            [ServerId] = new()
            {
                ServerId = ServerId,
                ObservedAtUtc = ObservedAtUtc,
                LastWipeAtUtc = BornAtUtc,
                AdvertisedNextWipeAtUtc = NextWipeAtUtc,
                Keywords = StudyKeywords.WithoutVolatileTokens(Keywords)
            }
        });

        Assert.Null(tracker.Observe(ServerId, ObservedAtUtc.AddHours(1), BornAtUtc, NextWipeAtUtc, Keywords));
    }
}
