using RustIQ.Domain.Study;

namespace RustIQ.Application.Study;

public static class StudySampleSelector
{
    public static IReadOnlyList<StudySampleServer> Select(
        IReadOnlyList<StudySelectionCandidate> candidates,
        StudyOptions options,
        DateTimeOffset selectedAtUtc)
    {
        // Sorted before shuffling, so the result depends on the seed and the candidates only, never on
        // the order the database happened to return them in.
        var orderedCandidates = candidates
            .OrderBy(candidate => candidate.Host, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.QueryPort)
            .ToArray();
        new Random(options.SelectionSeed).Shuffle(orderedCandidates);

        var sample = new List<StudySampleServer>();
        foreach (var stratum in Enum.GetValues<StudyStratum>())
        {
            var stratumCandidates = orderedCandidates
                .Where(candidate => GetStratum(candidate.PeakPlayers, options) == stratum)
                .ToArray();

            sample.AddRange(SelectStratum(stratumCandidates, options)
                .Select(candidate => new StudySampleServer
                {
                    ServerId = candidate.ServerId,
                    Host = candidate.Host,
                    QueryPort = candidate.QueryPort,
                    Name = candidate.Name,
                    Country = candidate.Country,
                    Stratum = stratum,
                    PeakPlayersAtSelection = candidate.PeakPlayers,
                    BattleMetricsServerId = candidate.BattleMetricsServerId,
                    SelectedAtUtc = selectedAtUtc
                }));
        }

        return sample;
    }

    public static StudyStratum? GetStratum(int peakPlayers, StudyOptions options)
    {
        if (peakPlayers >= options.PopularMinPeakPlayers)
        {
            return StudyStratum.Popular;
        }

        if (peakPlayers >= options.MediumMinPeakPlayers)
        {
            return StudyStratum.Medium;
        }

        return peakPlayers >= options.SmallMinPeakPlayers ? StudyStratum.Small : null;
    }

    private static List<StudySelectionCandidate> SelectStratum(
        IReadOnlyList<StudySelectionCandidate> candidates,
        StudyOptions options)
    {
        var target = Math.Min(Math.Max(0, options.ServersPerStratum), candidates.Count);
        var selected = new List<StudySelectionCandidate>();
        var selectedIds = new HashSet<Guid>();
        var countPerCountry = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // The cap only spreads the sample; it must not leave a stratum short. When the countries still
        // under it run out of candidates, it is raised one step at a time, so the extra places go to
        // the least represented countries first instead of all to the largest one.
        for (var countryCap = Math.Max(1, (int)Math.Ceiling(target * options.MaxCountryShare));
             selected.Count < target;
             countryCap++)
        {
            foreach (var candidate in candidates)
            {
                if (selected.Count >= target)
                {
                    break;
                }

                var country = candidate.Country ?? string.Empty;
                var count = countPerCountry.GetValueOrDefault(country);
                if (count >= countryCap || !selectedIds.Add(candidate.ServerId))
                {
                    continue;
                }

                selected.Add(candidate);
                countPerCountry[country] = count + 1;
            }
        }

        return selected;
    }
}
