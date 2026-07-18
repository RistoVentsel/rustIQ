namespace RustIQ.Api.Workers;

public static class MapMetadataRefreshSchedule
{
    public static DateTimeOffset GetNextRunUtc(
        DateTimeOffset nowUtc,
        TimeZoneInfo timeZone,
        IReadOnlyCollection<int> localHours)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);

        for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
        {
            foreach (var hour in localHours.Order())
            {
                var localRun = DateTime.SpecifyKind(
                    localNow.Date.AddDays(dayOffset).AddHours(hour),
                    DateTimeKind.Unspecified);

                while (timeZone.IsInvalidTime(localRun))
                {
                    localRun = localRun.AddMinutes(1);
                }

                var runUtc = new DateTimeOffset(
                    TimeZoneInfo.ConvertTimeToUtc(localRun, timeZone),
                    TimeSpan.Zero);
                if (runUtc > nowUtc)
                {
                    return runUtc;
                }
            }
        }

        throw new InvalidOperationException("No future map metadata refresh time could be calculated.");
    }
}
