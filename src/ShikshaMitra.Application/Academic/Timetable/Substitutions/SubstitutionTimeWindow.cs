using System;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

internal static class SubstitutionTimeWindow
{
    public static bool IsOpen(
        DateTime date,
        TimeOnly endTime,
        DateTime utcNow,
        TimeZoneInfo institutionTimeZone) =>
        NormalizeUtc(utcNow) <= ToUtc(date, endTime, institutionTimeZone);

    public static bool HasStarted(
        DateTime date,
        TimeOnly startTime,
        DateTime utcNow,
        TimeZoneInfo institutionTimeZone) =>
        NormalizeUtc(utcNow) >= ToUtc(date, startTime, institutionTimeZone);

    internal static DateTime ToUtc(
        DateTime date,
        TimeOnly time,
        TimeZoneInfo institutionTimeZone)
    {
        var institutionLocalTime = DateTime.SpecifyKind(
            date.Date.Add(time.ToTimeSpan()),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(institutionLocalTime, institutionTimeZone);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
