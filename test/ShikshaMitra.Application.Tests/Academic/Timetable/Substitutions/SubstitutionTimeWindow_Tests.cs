using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionTimeWindow_Tests
{
    private static readonly TimeZoneInfo InstitutionTimeZone = GetInstitutionTimeZone();
    private static readonly DateTime DutyDate = new(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToUtc_Should_Interpret_Period_Time_As_Institution_Local_Time()
    {
        var result = SubstitutionTimeWindow.ToUtc(
            DutyDate,
            new TimeOnly(10, 45),
            InstitutionTimeZone);

        result.ShouldBe(new DateTime(2026, 8, 29, 5, 15, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(5, 14, true)]
    [InlineData(5, 15, true)]
    [InlineData(5, 16, false)]
    public void IsOpen_Should_Use_The_Converted_Utc_Deadline(int hour, int minute, bool expected)
    {
        var result = SubstitutionTimeWindow.IsOpen(
            DutyDate,
            new TimeOnly(10, 45),
            new DateTime(2026, 8, 29, hour, minute, 0, DateTimeKind.Utc),
            InstitutionTimeZone);

        result.ShouldBe(expected);
    }

    [Fact]
    public void HasStarted_Should_Use_The_Converted_Utc_Start_Time()
    {
        SubstitutionTimeWindow.HasStarted(
            DutyDate,
            new TimeOnly(9, 0),
            new DateTime(2026, 8, 29, 3, 29, 0, DateTimeKind.Utc),
            InstitutionTimeZone).ShouldBeFalse();

        SubstitutionTimeWindow.HasStarted(
            DutyDate,
            new TimeOnly(9, 0),
            new DateTime(2026, 8, 29, 3, 30, 0, DateTimeKind.Utc),
            InstitutionTimeZone).ShouldBeTrue();
    }

    private static TimeZoneInfo GetInstitutionTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
    }
}
