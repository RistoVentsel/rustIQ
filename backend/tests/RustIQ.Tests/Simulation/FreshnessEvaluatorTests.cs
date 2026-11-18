using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;
using RustIQ.Simulator.Evaluation;

namespace RustIQ.Tests.Simulation;

public sealed class FreshnessEvaluatorTests
{
    private const long Step = FreshnessEvaluator.DefaultGridStepMs;

    [Fact]
    public void Evaluate_MeasuresAgeAndErrorAgainstTheProbeAtEachGridPoint()
    {
        // Grid points at 0 and 15 min. Truth 10 then 20; the last poll (at -60 s) showed 12.
        var truth = Probes((0, true, 10), (Step - 5_000, true, 20));
        var shown = new ShownSeries([-60_000], [12]);

        var summary = Evaluate([new EvaluationInput(StudyStratum.Popular, truth, shown)], 0, 2 * Step);

        var popular = summary.Single(row => row.Group == "Popular");
        Assert.Equal(2, popular.Points);
        Assert.Equal(2, popular.PointsWithValue);
        Assert.Equal((60 + 960) / 2.0, popular.AoiMeanSeconds);
        Assert.Equal((2 + 8) / 2.0, popular.PlayerMae);
        Assert.Equal(0.5, popular.ShareOffByMoreThan5);
        // (10 * 60 + 20 * 960) / 30
        Assert.Equal(660, popular.PlayerWeightedAoiSeconds);
    }

    [Fact]
    public void Evaluate_CountsOnlyPointsWithAnAnsweredProbeInTheLast20Seconds()
    {
        var truth = Probes(
            (-20_000, true, 5),     // exactly 20 s before t = 0: outside (t - 20 s, t]
            (Step - 1_000, false, 0), // failed probe at the second point
            (2 * Step + 1, true, 7)); // after the third point
        var shown = new ShownSeries([-100_000], [5]);

        var summary = Evaluate([new EvaluationInput(StudyStratum.Small, truth, shown)], 0, 3 * Step);

        Assert.Equal(0, summary.Single(row => row.Group == "Small").Points);
    }

    [Fact]
    public void Evaluate_UsesTheLatestAnsweredProbeInTheWindow()
    {
        var truth = Probes((-15_000, true, 3), (-1_000, false, 0));

        Assert.Equal(3, FreshnessEvaluator.TruthAt(truth, 0));
    }

    [Fact]
    public void Evaluate_KeepsPointsWithoutAPollOutOfTheAverages()
    {
        var truth = Probes((0, true, 10), (Step, true, 10));
        var shown = new ShownSeries([Step - 30_000], [10]);

        var summary = Evaluate([new EvaluationInput(StudyStratum.Medium, truth, shown)], 0, 2 * Step);

        var medium = summary.Single(row => row.Group == "Medium");
        Assert.Equal(2, medium.Points);
        Assert.Equal(1, medium.PointsWithValue);
        Assert.Equal(30, medium.AoiMeanSeconds);
    }

    [Fact]
    public void Evaluate_AllRowCombinesTheStrata()
    {
        var input = new[]
        {
            new EvaluationInput(StudyStratum.Popular, Probes((0, true, 1)), new ShownSeries([0], [1])),
            new EvaluationInput(StudyStratum.Small, Probes((0, true, 1)), new ShownSeries([-10_000], [1]))
        };

        var all = Evaluate(input, 0, Step).Single(row => row.Group == FreshnessEvaluator.AllGroup);

        Assert.Equal(2, all.Points);
        Assert.Equal(5, all.AoiMeanSeconds);
    }

    [Fact]
    public void PercentileCont_InterpolatesLikePostgres()
    {
        double[] values = [1, 2, 3, 4];

        Assert.Equal(2.5, FreshnessEvaluator.PercentileCont(values, 0.5));
        Assert.Equal(3.85, FreshnessEvaluator.PercentileCont(values, 0.95), 10);
        Assert.Equal(4, FreshnessEvaluator.PercentileCont(values, 1));
    }

    [Fact]
    public void ShownSeries_FromSnapshotsSkipsWatchedAndUnansweredPolls()
    {
        var snapshots = new SnapshotSeries(
            [1, 2, 3, 4],
            [10, 20, 30, 40],
            [100, 100, 100, 100],
            [true, true, true, true],
            [1, 1, 0, 1],
            [ServerPollSource.FastLane, ServerPollSource.Watched, ServerPollSource.NormalOnlineLane, ServerPollSource.Discovery]);

        var shown = ShownSeries.FromSnapshots(snapshots);

        Assert.Equal([1L, 4L], shown.TimesMs);
        Assert.Equal([(short)10, (short)40], shown.Players);
    }

    private static IReadOnlyList<FreshnessSummary> Evaluate(IReadOnlyList<EvaluationInput> input, long from, long to) =>
        FreshnessEvaluator.Evaluate(input, from, to);

    private static ProbeSeries Probes(params (long Time, bool Ok, short Players)[] probes) =>
        new(
            probes.Select(probe => probe.Time).ToArray(),
            probes.Select(probe => probe.Ok).ToArray(),
            probes.Select(probe => probe.Players).ToArray(),
            new short[probes.Length]);
}
