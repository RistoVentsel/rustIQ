using RustIQ.Application.Study;
using RustIQ.Domain.Study;

namespace RustIQ.Tests.Study;

public sealed class StudySampleSelectorTests
{
    private static readonly DateTimeOffset SelectedAtUtc = DateTimeOffset.Parse("2026-11-04T12:00:00Z");

    [Fact]
    public void Select_PutsEachServerInTheStratumOfItsPeakPlayerCount()
    {
        var options = new StudyOptions { ServersPerStratum = 10 };

        var sample = StudySampleSelector.Select(
            [
                Candidate("popular.test", 250, "DE"),
                Candidate("medium.test", 40, "US"),
                Candidate("small.test", 3, "GB"),
                Candidate("empty.test", 0, "FR")
            ],
            options,
            SelectedAtUtc);

        Assert.Equal(StudyStratum.Popular, sample.Single(server => server.Host == "popular.test").Stratum);
        Assert.Equal(StudyStratum.Medium, sample.Single(server => server.Host == "medium.test").Stratum);
        Assert.Equal(StudyStratum.Small, sample.Single(server => server.Host == "small.test").Stratum);
        Assert.DoesNotContain(sample, server => server.Host == "empty.test");
        Assert.All(sample, server => Assert.Equal(SelectedAtUtc, server.SelectedAtUtc));
    }

    [Fact]
    public void Select_TakesAtMostTheConfiguredNumberPerStratum()
    {
        var options = new StudyOptions { ServersPerStratum = 5, MaxCountryShare = 1 };
        var candidates = Enumerable.Range(0, 20)
            .Select(index => Candidate($"popular{index}.test", 150, "DE"))
            .ToArray();

        var sample = StudySampleSelector.Select(candidates, options, SelectedAtUtc);

        Assert.Equal(5, sample.Count);
    }

    [Fact]
    public void Select_SpreadsAStratumAcrossCountriesWhileOthersHaveCandidates()
    {
        // A cap of 3 per country: Germany has plenty, the other two only just enough to fill the rest.
        var options = new StudyOptions { ServersPerStratum = 8, MaxCountryShare = 0.375 };
        var candidates = Enumerable.Range(0, 20)
            .Select(index => Candidate($"de{index}.test", 150, "DE"))
            .Concat(Enumerable.Range(0, 3).Select(index => Candidate($"us{index}.test", 150, "US")))
            .Concat(Enumerable.Range(0, 2).Select(index => Candidate($"fi{index}.test", 150, "FI")))
            .ToArray();

        var sample = StudySampleSelector.Select(candidates, options, SelectedAtUtc);

        Assert.Equal(8, sample.Count);
        Assert.Equal(3, sample.Count(server => server.Country == "DE"));
        Assert.Equal(3, sample.Count(server => server.Country == "US"));
        Assert.Equal(2, sample.Count(server => server.Country == "FI"));
    }

    [Fact]
    public void Select_FillsTheStratumPastTheCountryCapWhenNoOtherCountryIsLeft()
    {
        var options = new StudyOptions { ServersPerStratum = 6, MaxCountryShare = 0.25 };
        var candidates = Enumerable.Range(0, 10)
            .Select(index => Candidate($"de{index}.test", 150, "DE"))
            .Append(Candidate("us.test", 150, "US"))
            .ToArray();

        var sample = StudySampleSelector.Select(candidates, options, SelectedAtUtc);

        Assert.Equal(6, sample.Count);
        Assert.Contains(sample, server => server.Country == "US");
    }

    [Fact]
    public void Select_GivesTheSameSampleForTheSameCandidatesInAnyOrder()
    {
        var options = new StudyOptions { ServersPerStratum = 5 };
        var candidates = Enumerable.Range(0, 30)
            .Select(index => Candidate($"server{index}.test", 20 + index * 5, index % 3 == 0 ? "DE" : "US"))
            .ToArray();

        var first = StudySampleSelector.Select(candidates, options, SelectedAtUtc);
        var second = StudySampleSelector.Select(candidates.Reverse().ToArray(), options, SelectedAtUtc);

        Assert.Equal(
            first.Select(server => server.ServerId).Order(),
            second.Select(server => server.ServerId).Order());
    }

    private static StudySelectionCandidate Candidate(string host, int peakPlayers, string country)
    {
        // Derived from the host so the same server keeps the same id across calls.
        var id = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(host)));
        return new StudySelectionCandidate(id, host, 28015, host, country, null, peakPlayers);
    }
}
