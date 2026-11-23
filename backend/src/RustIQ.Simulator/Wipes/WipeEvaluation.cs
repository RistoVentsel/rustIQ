using System.Globalization;
using System.Text;
using RustIQ.Application.Servers.Wipes;
using RustIQ.Infrastructure.ServerQuery;
using RustIQ.Simulator.Data;

namespace RustIQ.Simulator.Wipes;

public static class WipeEvaluation
{
    private const string SampleHeader = "server_idx,server_id,stratum,peak_players,country,selected_at";
    private const string ObservationHeader = "Id,ServerId,ObservedAtUtc,LastWipeAtUtc,PreviousLastWipeAtUtc,PreviousNextWipeAtUtc,IsFirstObservationSinceStart,Keywords";
    private const string DescriptionHeader = "Id,ServerId,CapturedAtUtc,Answered,Description";
    private const string PredictionHeader = "Id,ServerId,CapturedAtUtc,NextWipeAtUtc,DisplayedNextWipeAtUtc,LastWipeAtUtc";
    private const string RoundHeader = "server_id,round,checked_utc,found,next_map_wipe_utc";

    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    private static readonly string[] Strata = { "All", "Popular", "Medium", "Small" };

    public static void Run(CommandLine commandLine, Action<string> log)
    {
        string sampleDir = commandLine.Required("sample");
        string studyDir = commandLine.Required("study");
        DateTimeOffset fromUtc = commandLine.Time("from", new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero));
        DateTimeOffset toUtc = commandLine.Time("to", new DateTimeOffset(2026, 11, 22, 21, 0, 0, TimeSpan.Zero));
        string roundName = commandLine.Optional("same-moment-round") ?? "3";
        string outDir = commandLine.Required("out");
        Directory.CreateDirectory(outDir);

        List<SampleServer> servers = new List<SampleServer>();
        CsvFile.Read(Path.Combine(sampleDir, "study_sample_servers.csv.gz"), SampleHeader, row =>
        {
            SampleServer server = new SampleServer();
            server.Idx = row.Int(0);
            server.Id = row.Guid(1);
            server.Stratum = row.String(2);
            servers.Add(server);
        });
        servers.Sort((a, b) => a.Idx.CompareTo(b.Idx));

        Dictionary<Guid, SampleServer> serversById = new Dictionary<Guid, SampleServer>();
        foreach (SampleServer server in servers)
        {
            serversById[server.Id] = server;
        }

        CsvFile.Read(Path.Combine(studyDir, "study_server_wipe_observations.csv.gz"), ObservationHeader, row =>
        {
            Guid serverId = row.Guid(1);
            if (!serversById.ContainsKey(serverId) || row.IsEmpty(3))
            {
                return;
            }

            Observation observation = new Observation();
            observation.Id = row.Long(0);
            observation.At = row.Timestamp(2);
            observation.Born = row.Timestamp(3);
            observation.PreviousNextWipe = row.NullableTimestamp(5);
            observation.Keywords = row.NullableString(7);
            serversById[serverId].Observations.Add(observation);
        });

        CsvFile.Read(Path.Combine(studyDir, "study_server_descriptions.csv.gz"), DescriptionHeader, row =>
        {
            Guid serverId = row.Guid(1);
            string? text = row.NullableString(4);
            if (serversById.ContainsKey(serverId) && row.NullableBool(3) == true && !string.IsNullOrEmpty(text))
            {
                serversById[serverId].Descriptions.Add(new Description(row.Timestamp(2), text));
            }
        });

        CsvFile.Read(Path.Combine(studyDir, "study_prediction_snapshots.csv.gz"), PredictionHeader, row =>
        {
            Guid serverId = row.Guid(1);
            if (serversById.ContainsKey(serverId))
            {
                serversById[serverId].Snapshots.Add(new Snapshot(row.Timestamp(2), row.NullableTimestamp(3)));
            }
        });

        CsvFile.Read(Path.Combine(studyDir, "bm_rounds.csv"), RoundHeader, row =>
        {
            Guid serverId = row.Guid(0);
            if (serversById.ContainsKey(serverId) && !row.IsEmpty(2))
            {
                serversById[serverId].Rounds.Add(new Round(row.String(1), row.Timestamp(2), row.NullableTimestamp(4)));
            }
        });

        int withObservations = 0;
        int withDescriptions = 0;
        int withSnapshots = 0;
        int withRounds = 0;
        foreach (SampleServer server in servers)
        {
            server.Observations.Sort((a, b) =>
            {
                if (a.At != b.At)
                {
                    return a.At.CompareTo(b.At);
                }

                return a.Id.CompareTo(b.Id);
            });
            server.Descriptions.Sort((a, b) => a.At.CompareTo(b.At));
            server.Snapshots.Sort((a, b) => a.At.CompareTo(b.At));

            if (server.Observations.Count > 0) withObservations++;
            if (server.Descriptions.Count > 0) withDescriptions++;
            if (server.Snapshots.Count > 0) withSnapshots++;
            if (server.Rounds.Count > 0) withRounds++;
        }

        log($"wipe observations of {withObservations} sample servers, descriptions of {withDescriptions}, " +
            $"prediction snapshots of {withSnapshots}, BattleMetrics rounds of {withRounds}");

        List<WipeEvent> events = new List<WipeEvent>();
        List<SameMoment> sameMoments = new List<SameMoment>();

        foreach (SampleServer server in servers)
        {
            List<KnownWipe> history = FindKnownWipes(server.Observations);

            List<DateTimeOffset> allWipes = new List<DateTimeOffset>();
            for (int i = 0; i < history.Count; i++)
            {
                allWipes.Add(history[i].Born);
            }

            double? cycle = MedianGap(allWipes);

            for (int i = 0; i < history.Count; i++)
            {
                KnownWipe wipe = history[i];
                if (!wipe.IsChange || wipe.Born < fromUtc || wipe.Born > toUtc)
                {
                    continue;
                }

                DateTimeOffset at = wipe.Born.AddMinutes(-1);

                List<DateTimeOffset> knownBefore = new List<DateTimeOffset>();
                for (int j = 0; j < i; j++)
                {
                    knownBefore.Add(history[j].Born);
                }

                List<Observation> rowsBefore = new List<Observation>();
                for (int j = 0; j < wipe.RowIndex; j++)
                {
                    rowsBefore.Add(server.Observations[j]);
                }

                DateTimeOffset? described = DescribedNextWipe(server.Descriptions, knownBefore, at);
                WipePrediction predicted = WipePredictor.Predict(knownBefore, Tag(LastKeywords(rowsBefore)), described, at);

                Round? round = null;
                foreach (Round r in server.Rounds)
                {
                    if (r.CheckedAt < wipe.Born && r.CheckedAt >= wipe.Born.AddDays(-7))
                    {
                        if (round == null || r.CheckedAt > round.CheckedAt)
                        {
                            round = r;
                        }
                    }
                }

                WipeEvent e = new WipeEvent();
                e.ServerIdx = server.Idx;
                e.Stratum = server.Stratum;
                e.Wipe = wipe.Born;
                e.Observed = server.Observations[wipe.RowIndex].At;
                e.Category = Category(wipe.Born, cycle);
                e.MedianGapHours = cycle;
                e.App = server.Observations[wipe.RowIndex].PreviousNextWipe;
                e.AppFallback = e.App != null && IsAppFallback(e.App.Value, LastKeywords(rowsBefore), described);
                e.Predicted = predicted;
                e.Round = round;
                events.Add(e);
            }

            Round? sameRound = null;
            foreach (Round r in server.Rounds)
            {
                if (r.Name == roundName)
                {
                    sameRound = r;
                    break;
                }
            }

            if (sameRound != null)
            {
                DateTimeOffset? nextWipe = null;
                for (int i = 0; i < history.Count; i++)
                {
                    if (history[i].IsChange && history[i].Born > sameRound.CheckedAt && history[i].Born <= toUtc)
                    {
                        nextWipe = history[i].Born;
                        break;
                    }
                }

                List<Observation> rowsBefore = new List<Observation>();
                foreach (Observation observation in server.Observations)
                {
                    if (observation.At < sameRound.CheckedAt)
                    {
                        rowsBefore.Add(observation);
                    }
                }

                List<KnownWipe> historyBefore = FindKnownWipes(rowsBefore);
                List<DateTimeOffset> knownBefore = new List<DateTimeOffset>();
                foreach (KnownWipe known in historyBefore)
                {
                    knownBefore.Add(known.Born);
                }

                DateTimeOffset? described = DescribedNextWipe(server.Descriptions, knownBefore, sameRound.CheckedAt);

                DateTimeOffset? app = null;
                foreach (Snapshot snapshot in server.Snapshots)
                {
                    if (snapshot.At <= sameRound.CheckedAt)
                    {
                        app = snapshot.Next;
                    }
                }

                SameMoment s = new SameMoment();
                s.ServerIdx = server.Idx;
                s.Stratum = server.Stratum;
                s.CheckedAt = sameRound.CheckedAt;
                s.Wipe = nextWipe;
                s.Category = nextWipe == null ? "" : Category(nextWipe.Value, cycle);
                s.BattleMetrics = sameRound.NextWipe;
                s.App = app;
                s.Predicted = WipePredictor.Predict(knownBefore, Tag(LastKeywords(rowsBefore)), described, sameRound.CheckedAt);
                sameMoments.Add(s);
            }
        }

        File.WriteAllText(Path.Combine(outDir, "wipes.csv"), WipesCsv(events));
        File.WriteAllText(Path.Combine(outDir, "same_moment.csv"), SameMomentCsv(sameMoments));
        string summary = Summary(events, sameMoments);
        File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary);

        int pairs = 0;
        foreach (SameMoment s in sameMoments)
        {
            if (s.Wipe != null) pairs++;
        }

        log($"{events.Count} wipes from {fromUtc:yyyy-MM-dd HH:mm} to {toUtc:yyyy-MM-dd HH:mm} UTC, " +
            $"{pairs} same-moment pairs; wrote {outDir}");
        Console.Write(summary);
    }

    private static DateTimeOffset? DescribedNextWipe(List<Description> descriptions, List<DateTimeOffset> knownWipes, DateTimeOffset at)
    {
        string? text = null;
        foreach (Description description in descriptions)
        {
            if (description.At < at)
            {
                text = description.Text;
            }
        }

        if (text == null)
        {
            return null;
        }

        DateTimeOffset? lastWipe = null;
        if (knownWipes.Count > 0)
        {
            lastWipe = knownWipes[knownWipes.Count - 1];
        }

        Dictionary<string, string> rules = new Dictionary<string, string>();
        rules["description"] = text;
        return RustWipeScheduleParser.GetNextWipeAtUtc(rules, lastWipe, at);
    }

    private static string? LastKeywords(List<Observation> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        return rows[rows.Count - 1].Keywords;
    }

    private static bool IsAppFallback(DateTimeOffset app, string? keywords, DateTimeOffset? described)
    {
        if (Tag(keywords) != WipeTagFrequency.None || described != null)
        {
            return false;
        }

        DateTime local = TimeZoneInfo.ConvertTime(app, London).DateTime;
        return local.DayOfWeek == DayOfWeek.Thursday && local.Day <= 7 && local.TimeOfDay == TimeSpan.FromHours(19);
    }

    public static WipeTagFrequency Tag(string? keywords)
    {
        if (RustServerKeywordParser.HasBrowserTag(keywords, 'w') || RustServerKeywordParser.HasWordTag(keywords, "weekly"))
        {
            return WipeTagFrequency.Weekly;
        }

        if (RustServerKeywordParser.HasBrowserTag(keywords, 'b') || RustServerKeywordParser.HasWordTag(keywords, "biweekly"))
        {
            return WipeTagFrequency.Biweekly;
        }

        if (RustServerKeywordParser.HasBrowserTag(keywords, 'm') || RustServerKeywordParser.HasWordTag(keywords, "monthly"))
        {
            return WipeTagFrequency.Monthly;
        }

        return WipeTagFrequency.None;
    }

    private static List<KnownWipe> FindKnownWipes(List<Observation> rows)
    {
        List<KnownWipe> known = new List<KnownWipe>();
        DateTimeOffset? max = null;

        for (int i = 0; i < rows.Count; i++)
        {
            if (max == null || rows[i].Born > max.Value)
            {
                KnownWipe wipe = new KnownWipe();
                wipe.Born = rows[i].Born;
                wipe.RowIndex = i;
                wipe.IsChange = max != null;
                known.Add(wipe);
                max = rows[i].Born;
            }
        }

        return known;
    }

    private static double? MedianGap(List<DateTimeOffset> wipes)
    {
        if (wipes.Count < 2)
        {
            return null;
        }

        List<double> gaps = new List<double>();
        for (int i = 1; i < wipes.Count; i++)
        {
            gaps.Add((wipes[i] - wipes[i - 1]).TotalHours);
        }

        return Percentile(gaps, 0.5);
    }

    private static string Category(DateTimeOffset born, double? medianGapHours)
    {
        DateTime local = TimeZoneInfo.ConvertTime(born, London).DateTime;
        if (local.DayOfWeek == DayOfWeek.Thursday && local.Day <= 7 && local.Hour >= 18 && local.Hour < 21)
        {
            return "forced";
        }

        if (medianGapHours != null && medianGapHours.Value < 24)
        {
            return "short";
        }

        return "regular";
    }

    private static bool InCategory(WipeEvent e, string category)
    {
        if (category == "regular+short6")
        {
            bool under6 = e.MedianGapHours != null && e.MedianGapHours.Value < 6;
            return e.Category != "forced" && !under6;
        }

        return e.Category == category;
    }

    private static string Summary(List<WipeEvent> events, List<SameMoment> sameMoments)
    {
        StringBuilder text = new StringBuilder("comparison,category,stratum,prediction,wipes,with_value,own_data,median_h,mean_h,p95_h\n");
        string[] categories = { "regular", "forced", "short", "regular+short6" };
        WipePredictionSource[] sources = { WipePredictionSource.History, WipePredictionSource.WeeklyPattern, WipePredictionSource.Description, WipePredictionSource.Tag };

        foreach (string category in categories)
        {
            foreach (string stratum in Strata)
            {
                List<WipeEvent> group = new List<WipeEvent>();
                foreach (WipeEvent e in events)
                {
                    if (InCategory(e, category) && (stratum == "All" || e.Stratum == stratum))
                    {
                        group.Add(e);
                    }
                }

                List<double> appOwn = new List<double>();
                List<double> appFallback = new List<double>();
                List<double> predictorOwn = new List<double>();
                List<double> predictorFallback = new List<double>();
                foreach (WipeEvent e in group)
                {
                    if (e.App != null && !e.AppFallback) appOwn.Add(Error(e.App.Value, e.Wipe));
                    if (e.App != null && e.AppFallback) appFallback.Add(Error(e.App.Value, e.Wipe));
                    if (e.Predicted.FromServerData) predictorOwn.Add(Error(e.Predicted.NextWipeAtUtc, e.Wipe));
                    else predictorFallback.Add(Error(e.Predicted.NextWipeAtUtc, e.Wipe));
                }

                AddLine(text, "per_wipe", category, stratum, "app_own", group.Count, appOwn, appOwn.Count);
                AddLine(text, "per_wipe", category, stratum, "app_fallback", group.Count, appFallback, 0);
                AddLine(text, "per_wipe", category, stratum, "predictor_own", group.Count, predictorOwn, predictorOwn.Count);
                AddLine(text, "per_wipe", category, stratum, "predictor_fallback", group.Count, predictorFallback, 0);

                foreach (WipePredictionSource source in sources)
                {
                    List<double> errors = new List<double>();
                    foreach (WipeEvent e in group)
                    {
                        if (e.Predicted.Source == source) errors.Add(Error(e.Predicted.NextWipeAtUtc, e.Wipe));
                    }

                    AddLine(text, "per_wipe", category, stratum, "predictor_" + source.ToString().ToLowerInvariant(), group.Count, errors, 0);
                }

                int withRound = 0;
                List<double> bm = new List<double>();
                List<double> bmApp = new List<double>();
                List<double> bmPredictor = new List<double>();
                foreach (WipeEvent e in group)
                {
                    if (e.Round == null) continue;
                    withRound++;
                    if (e.Round.NextWipe != null) bm.Add(Error(e.Round.NextWipe.Value, e.Wipe));
                    if (e.App != null && !e.AppFallback) bmApp.Add(Error(e.App.Value, e.Wipe));
                    if (e.Predicted.FromServerData) bmPredictor.Add(Error(e.Predicted.NextWipeAtUtc, e.Wipe));
                }

                AddLine(text, "per_wipe_bm_subsample", category, stratum, "battlemetrics", withRound, bm, bm.Count);
                AddLine(text, "per_wipe_bm_subsample", category, stratum, "app_own", withRound, bmApp, bmApp.Count);
                AddLine(text, "per_wipe_bm_subsample", category, stratum, "predictor_own", withRound, bmPredictor, bmPredictor.Count);
            }
        }

        foreach (string stratum in Strata)
        {
            int count = 0;
            List<double> bm = new List<double>();
            List<double> app = new List<double>();
            List<double> predictorOwn = new List<double>();
            List<double> predictorAll = new List<double>();
            foreach (SameMoment s in sameMoments)
            {
                if (s.Wipe == null || s.Category == "forced") continue;
                if (stratum != "All" && s.Stratum != stratum) continue;

                count++;
                if (s.BattleMetrics != null) bm.Add(Error(s.BattleMetrics.Value, s.Wipe.Value));
                if (s.App != null) app.Add(Error(s.App.Value, s.Wipe.Value));
                if (s.Predicted.FromServerData) predictorOwn.Add(Error(s.Predicted.NextWipeAtUtc, s.Wipe.Value));
                predictorAll.Add(Error(s.Predicted.NextWipeAtUtc, s.Wipe.Value));
            }

            AddLine(text, "same_moment", "not_forced", stratum, "battlemetrics", count, bm, bm.Count);
            AddLine(text, "same_moment", "not_forced", stratum, "app", count, app, app.Count);
            AddLine(text, "same_moment", "not_forced", stratum, "predictor_own", count, predictorOwn, predictorOwn.Count);
            AddLine(text, "same_moment", "not_forced", stratum, "predictor_all", count, predictorAll, predictorAll.Count);
        }

        return text.ToString();
    }

    private static void AddLine(StringBuilder text, string comparison, string category, string stratum, string prediction,
        int wipes, List<double> errors, int ownData)
    {
        double median = double.NaN;
        double mean = double.NaN;
        double p95 = double.NaN;
        if (errors.Count > 0)
        {
            median = Percentile(errors, 0.5);
            p95 = Percentile(errors, 0.95);
            double sum = 0;
            foreach (double error in errors)
            {
                sum += error;
            }

            mean = sum / errors.Count;
        }

        text.Append(comparison).Append(',').Append(category).Append(',').Append(stratum).Append(',').Append(prediction).Append(',');
        text.Append(wipes).Append(',').Append(errors.Count).Append(',').Append(ownData).Append(',');
        text.Append(Number(median)).Append(',').Append(Number(mean)).Append(',').Append(Number(p95)).Append('\n');
    }

    private static double Error(DateTimeOffset predicted, DateTimeOffset actual)
    {
        return Math.Abs((predicted - actual).TotalHours);
    }

    public static double Percentile(List<double> values, double p)
    {
        List<double> sorted = new List<double>(values);
        sorted.Sort();

        double rank = p * (sorted.Count - 1);
        int low = (int)Math.Floor(rank);
        int high = (int)Math.Ceiling(rank);
        return sorted[low] + (sorted[high] - sorted[low]) * (rank - low);
    }

    private static string WipesCsv(List<WipeEvent> events)
    {
        StringBuilder text = new StringBuilder("server_idx,stratum,wipe_utc,observed_utc,category,median_gap_h,app_utc,app_fallback," +
                                               "predictor_utc,predictor_source,bm_round,bm_checked_utc,bm_utc,err_app_h,err_predictor_h,err_bm_h,bm_age_h\n");
        foreach (WipeEvent e in events)
        {
            string errApp = e.App == null ? "" : Number(Error(e.App.Value, e.Wipe));
            string errBm = "";
            string bmAge = "";
            string roundName = "";
            DateTimeOffset? roundChecked = null;
            DateTimeOffset? roundNext = null;
            if (e.Round != null)
            {
                roundName = e.Round.Name;
                roundChecked = e.Round.CheckedAt;
                roundNext = e.Round.NextWipe;
                bmAge = Number((e.Wipe - e.Round.CheckedAt).TotalHours);
                if (e.Round.NextWipe != null)
                {
                    errBm = Number(Error(e.Round.NextWipe.Value, e.Wipe));
                }
            }

            string gap = e.MedianGapHours == null ? "" : Number(e.MedianGapHours.Value);
            string fallback = e.AppFallback ? "1" : "0";

            text.Append(e.ServerIdx).Append(',').Append(e.Stratum).Append(',').Append(Iso(e.Wipe)).Append(',').Append(Iso(e.Observed)).Append(',');
            text.Append(e.Category).Append(',').Append(gap).Append(',').Append(Iso(e.App)).Append(',').Append(fallback).Append(',');
            text.Append(Iso(e.Predicted.NextWipeAtUtc)).Append(',').Append(e.Predicted.Source).Append(',');
            text.Append(roundName).Append(',').Append(Iso(roundChecked)).Append(',').Append(Iso(roundNext)).Append(',');
            text.Append(errApp).Append(',').Append(Number(Error(e.Predicted.NextWipeAtUtc, e.Wipe))).Append(',');
            text.Append(errBm).Append(',').Append(bmAge).Append('\n');
        }

        return text.ToString();
    }

    private static string SameMomentCsv(List<SameMoment> rows)
    {
        StringBuilder text = new StringBuilder("server_idx,stratum,checked_utc,wipe_utc,category,bm_utc,app_utc,predictor_utc,predictor_source\n");
        foreach (SameMoment s in rows)
        {
            text.Append(s.ServerIdx).Append(',').Append(s.Stratum).Append(',').Append(Iso(s.CheckedAt)).Append(',');
            text.Append(Iso(s.Wipe)).Append(',').Append(s.Category).Append(',').Append(Iso(s.BattleMetrics)).Append(',');
            text.Append(Iso(s.App)).Append(',').Append(Iso(s.Predicted.NextWipeAtUtc)).Append(',').Append(s.Predicted.Source).Append('\n');
        }

        return text.ToString();
    }

    private static string Number(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Iso(DateTimeOffset? value)
    {
        if (value == null)
        {
            return "";
        }

        return value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private sealed class SampleServer
    {
        public int Idx;
        public Guid Id;
        public string Stratum = "";
        public List<Observation> Observations = new List<Observation>();
        public List<Description> Descriptions = new List<Description>();
        public List<Snapshot> Snapshots = new List<Snapshot>();
        public List<Round> Rounds = new List<Round>();
    }

    private sealed class Observation
    {
        public long Id;
        public DateTimeOffset At;
        public DateTimeOffset Born;
        public DateTimeOffset? PreviousNextWipe;
        public string? Keywords;
    }

    private sealed class KnownWipe
    {
        public DateTimeOffset Born;
        public int RowIndex;
        public bool IsChange;
    }

    private sealed class Description
    {
        public Description(DateTimeOffset at, string text)
        {
            At = at;
            Text = text;
        }

        public DateTimeOffset At;
        public string Text;
    }

    private sealed class Snapshot
    {
        public Snapshot(DateTimeOffset at, DateTimeOffset? next)
        {
            At = at;
            Next = next;
        }

        public DateTimeOffset At;
        public DateTimeOffset? Next;
    }

    private sealed class Round
    {
        public Round(string name, DateTimeOffset checkedAt, DateTimeOffset? nextWipe)
        {
            Name = name;
            CheckedAt = checkedAt;
            NextWipe = nextWipe;
        }

        public string Name;
        public DateTimeOffset CheckedAt;
        public DateTimeOffset? NextWipe;
    }

    private sealed class WipeEvent
    {
        public int ServerIdx;
        public string Stratum = "";
        public DateTimeOffset Wipe;
        public DateTimeOffset Observed;
        public string Category = "";
        public double? MedianGapHours;
        public DateTimeOffset? App;
        public bool AppFallback;
        public WipePrediction Predicted = null!;
        public Round? Round;
    }

    private sealed class SameMoment
    {
        public int ServerIdx;
        public string Stratum = "";
        public DateTimeOffset CheckedAt;
        public DateTimeOffset? Wipe;
        public string Category = "";
        public DateTimeOffset? BattleMetrics;
        public DateTimeOffset? App;
        public WipePrediction Predicted = null!;
    }
}
