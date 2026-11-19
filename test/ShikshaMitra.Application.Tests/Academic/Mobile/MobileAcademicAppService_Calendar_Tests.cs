using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false, DateTimeKind.Utc)]
	[InlineData(true, DateTimeKind.Utc)]
	[InlineData(false, DateTimeKind.Local)]
	[InlineData(true, DateTimeKind.Local)]
	[InlineData(false, DateTimeKind.Unspecified)]
	[InlineData(true, DateTimeKind.Unspecified)]
	public Task Calendar_and_dashboard_preserve_school_event_times_for_database_date_kinds(bool parent, DateTimeKind kind) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent);
		using var identity = SignIn(f.User.Id);
		var date = MobileAcademicContextAppService.SchoolToday;
		var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, f.Student.AcademicMediumId, f.Year.Id,
			"Published calendar", f.Year.StartDate, f.Year.EndDate);
		calendar.Publish(f.User.Id);
		await Insert(calendar);
		var timed = await Insert(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id,
			DateTime.SpecifyKind(date, kind), DayType.Event, "School event",
			startTime: new TimeSpan(9, 15, 0), endTime: new TimeSpan(10, 45, 0)));
		var holiday = await Insert(new AcademicCalendarDay(Guid.NewGuid(), tenantId, calendar.Id,
			DateTime.SpecifyKind(date, kind), DayType.Holiday, "School holiday"));
		timed.Date.Kind.ShouldBe(kind);
		var service = GetRequiredService<IMobileCalendarAppService>();
		var input = new MobileAcademicInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id };
		var expectedStart = new DateTimeOffset(date.AddHours(9).AddMinutes(15), TimeSpan.FromHours(5.5));
		var expectedEnd = new DateTimeOffset(date.AddHours(10).AddMinutes(45), TimeSpan.FromHours(5.5));

		void AssertTime(MobileSchoolMarkerDto marker)
		{
			marker.Date.ShouldBe(DateOnly.FromDateTime(date));
			marker.StartAt.ShouldBe(expectedStart);
			marker.EndAt.ShouldBe(expectedEnd);
			marker.StartAt!.Value.Offset.ShouldBe(TimeSpan.FromHours(5.5));
			marker.Type.ShouldBe("event");
			marker.SchoolClosed.ShouldBeFalse();
		}

		var month = await service.GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Year = date.Year, Month = date.Month });
		month.Count.ShouldBe(2);
		AssertTime(month.Single(x => x.Id == timed.Id));
		var untimed = month.Single(x => x.Id == holiday.Id);
		untimed.StartAt.ShouldBeNull();
		untimed.EndAt.ShouldBeNull();
		untimed.SchoolClosed.ShouldBeTrue();
		AssertTime(await service.GetMarkerAsync(timed.Id, input));
		var dashboard = await GetRequiredService<IMobileDashboardAppService>().GetAsync(input);
		AssertTime(dashboard.UpcomingEvents.Single(x => x.Id == timed.Id));
	});
}
