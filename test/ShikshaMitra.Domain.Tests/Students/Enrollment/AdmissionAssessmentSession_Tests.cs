using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSession_Tests
{
    private static readonly DateTime Starts = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Capacity_Cannot_Be_Overbooked_And_Released_Seats_Can_Be_Reserved()
    {
        var session = Session(1);
        session.Reserve(Starts.AddHours(-1));
        session.BookedCount.ShouldBe(1);
        var error = Should.Throw<BusinessException>(() => session.Reserve(Starts.AddHours(-1)));
        error.Code.ShouldBe(AdmissionAssessmentErrorCodes.CapacityExceeded);
        session.BookedCount.ShouldBe(1);
        session.Release();
        session.Reserve(Starts.AddHours(-1));
        session.BookedCount.ShouldBe(1);
        session.Release();
        session.Release();
        session.BookedCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Session_At_Or_Before_Current_Time_Cannot_Be_Booked(int hoursAfterStart)
    {
        var session = Session();
        Should.Throw<BusinessException>(() => session.Reserve(Starts.AddHours(hoursAfterStart)));
        session.BookedCount.ShouldBe(0);
    }

    [Fact]
    public void Session_With_Bookings_Cannot_Be_Cancelled_Or_Reduced_Below_Occupancy()
    {
        var session = Session(2);
        session.Reserve(Starts.AddHours(-1));
        session.Reserve(Starts.AddHours(-1));
        Should.Throw<BusinessException>(() => session.Cancel());
        Should.Throw<BusinessException>(() => session.Update("Exam", Starts, Starts.AddHours(1), "Hall", 1));
        session.Capacity.ShouldBe(2);
        session.BookedCount.ShouldBe(2);
        session.Release();
        session.Release();
        session.Cancel();
        Should.Throw<BusinessException>(() => session.Reserve(Starts.AddHours(-1)));
        Should.Throw<BusinessException>(() => session.Update("Exam", Starts, Starts.AddHours(1), "Hall", 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Invalid_Capacity_Is_Rejected(int capacity)
    {
        Should.Throw<BusinessException>(() => Session(capacity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Session_Must_End_After_It_Starts(int durationHours)
    {
        Should.Throw<BusinessException>(() => Session().Update(
            "Exam", Starts, Starts.AddHours(durationHours), "Hall", 10));
    }

    private static AdmissionAssessmentSession Session(int capacity = 10) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Exam", Starts, Starts.AddHours(1), "Hall", capacity);
}
