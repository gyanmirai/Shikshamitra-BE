using System;

namespace ShikshaMitra.Academic.Curriculum;

internal static class LessonPlanCalendarDate
{
    private const string InstitutionTimeZoneId = "Asia/Kolkata";
    private const string WindowsInstitutionTimeZoneId = "India Standard Time";
    private static readonly TimeZoneInfo InstitutionTimeZone = GetInstitutionTimeZone();

    public static DateTime FromStoredInstant(DateTime value)
    {
        // PlannedDate is persisted as a UTC instant. Some providers can materialize
        // UTC-backed values as Unspecified, so that kind must not be treated as server-local.
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, InstitutionTimeZone).Date;
    }

    private static TimeZoneInfo GetInstitutionTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(InstitutionTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsInstitutionTimeZoneId);
        }
    }
}
