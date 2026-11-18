using System.Globalization;
using System.Text;

namespace RustIQ.Simulator.Evaluation;

public static class SummaryCsv
{
    public const string Header =
        "group,points,points_with_value,aoi_mean_s,aoi_median_s,aoi_p95_s,player_mae,share_off_gt5,player_weighted_aoi_s";

    public static string Format(IEnumerable<FreshnessSummary> rows)
    {
        var builder = new StringBuilder().AppendLine(Header);
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                row.Group,
                row.Points.ToString(CultureInfo.InvariantCulture),
                row.PointsWithValue.ToString(CultureInfo.InvariantCulture),
                Number(row.AoiMeanSeconds),
                Number(row.AoiMedianSeconds),
                Number(row.AoiP95Seconds),
                Number(row.PlayerMae),
                Number(row.ShareOffByMoreThan5),
                Number(row.PlayerWeightedAoiSeconds)));
        }

        return builder.ToString();
    }

    /// <summary>A readable table for the console.</summary>
    public static string FormatTable(IEnumerable<FreshnessSummary> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"{"Group",-8} {"Points",8} {"Valued",8} {"Median",8} {"Mean",8} {"p95",8} {"MAE",6} {">5 off",7} {"PW AoI",8}");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{row.Group,-8} {row.Points,8} {row.PointsWithValue,8} {row.AoiMedianSeconds,8:0} {row.AoiMeanSeconds,8:0} " +
                $"{row.AoiP95Seconds,8:0} {row.PlayerMae,6:0.00} {row.ShareOffByMoreThan5 * 100,6:0.0}% {row.PlayerWeightedAoiSeconds,8:0}"));
        }

        return builder.ToString();
    }

    private static string Number(double? value) =>
        value?.ToString("0.######", CultureInfo.InvariantCulture) ?? "";
}
