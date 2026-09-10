using System;
using ShikshaMitra.Academic.Calendar;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarDay_Tests
{
    [Fact]
    public void Constructor_Should_Respect_AutoScanImpacts_False()
    {
        var day = new AcademicCalendarDay(
            Guid.NewGuid(),
            tenantId: null,
            Guid.NewGuid(),
            new DateTime(2026, 7, 20),
            DayType.EmergencyClosure,
            "Emergency closure",
            autoScanImpacts: false);

        day.AutoScanImpacts.ShouldBeFalse();
    }

    [Fact]
    public void Update_Should_Change_Marker_Date()
    {
        var day = new AcademicCalendarDay(
            Guid.NewGuid(),
            tenantId: null,
            Guid.NewGuid(),
            new DateTime(2026, 7, 20, 14, 30, 0),
            DayType.Event,
            "Sports Day");

        day.Update(
            new DateTime(2026, 7, 25, 9, 15, 0),
            DayType.Holiday,
            "Holiday",
            "Updated marker",
            appliesToAll: true,
            standardId: null,
            divisionId: null,
            HolidayCategory.National,
            startTime: null,
            endTime: null,
            changeReason: "Moved from edit marker modal");

        day.Date.ShouldBe(new DateTime(2026, 7, 25));
        day.DayType.ShouldBe(DayType.Holiday);
        day.Title.ShouldBe("Holiday");
        day.Description.ShouldBe("Updated marker");
        day.ChangeReason.ShouldBe("Moved from edit marker modal");
    }

    [Fact]
    public void Constructor_And_Update_Should_Trim_Title()
    {
        var day = new AcademicCalendarDay(
            Guid.NewGuid(),
            tenantId: null,
            Guid.NewGuid(),
            new DateTime(2026, 9, 14),
            DayType.Holiday,
            "  Ganesh Chaturthi  ");

        day.Title.ShouldBe("Ganesh Chaturthi");

        day.Update(
            new DateTime(2026, 9, 14),
            DayType.Holiday,
            "  Festival Holiday  ",
            "A festival celebrating Ganesha",
            appliesToAll: true,
            standardId: null,
            divisionId: null,
            HolidayCategory.Festival,
            startTime: null,
            endTime: null,
            changeReason: "Updated title");

        day.Title.ShouldBe("Festival Holiday");
    }

    [Fact]
    public void Constructor_Should_Reject_A_Whitespace_Only_Title()
    {
        Should.Throw<ArgumentException>(() => new AcademicCalendarDay(
            Guid.NewGuid(),
            tenantId: null,
            Guid.NewGuid(),
            new DateTime(2026, 9, 14),
            DayType.Holiday,
            "   "));
    }
}
