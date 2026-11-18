using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class AcademicNotificationWorkingDays_Tests
{
	[Fact]
	public async Task Reader_uses_published_active_closures_for_the_exact_school_year_medium_and_division()
	{
		var school = Guid.NewGuid();
		var foreignSchool = Guid.NewGuid();
		var year = new AcademicYear(Guid.NewGuid(), school, "2026–27", Date(1), Date(30));
		var medium = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = Guid.NewGuid();
		var otherDivision = Guid.NewGuid();
		var published = Calendar(school, medium, year.Id, published: true);
		var draft = Calendar(school, medium, year.Id, published: false);
		var foreign = Calendar(foreignSchool, medium, year.Id, published: true);
		var otherMedium = Calendar(school, Guid.NewGuid(), year.Id, published: true);
		var otherYear = Calendar(school, medium, Guid.NewGuid(), published: true);
		var cancelled = Day(published, 18);
		cancelled.Cancel("Reopened");
		var calendarDays = new[] {
			Day(published, 15), Day(published, 16, standard), Day(published, 17, standard, division), cancelled,
			Day(published, 19, standard, otherDivision), Day(draft, 20), Day(foreign, 21), Day(otherMedium, 22), Day(otherYear, 23)
		};
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(school);
		var configs = Substitute.For<ILectureSlotConfigRepository>();
		var config = new LectureSlotConfig(Guid.NewGuid(), school, standard, "08:00–15:00");
		config.AddSlotGroup(new LectureSlotGroup(Guid.NewGuid(), config.Id, 5, 0, 4));
		configs.GetQueryableAsync().Returns(new[] { config }.AsQueryable());
		var timetables = Substitute.For<ITimetableRepository>();
		timetables.GetQueryableAsync().Returns(Array.Empty<Timetable>().AsQueryable());
		var reader = new AcademicNotificationWorkingDays(tenant, new AsyncQueryableExecuter([]), Repository(year),
			Repository(published, draft, foreign, otherMedium, otherYear), Repository(calendarDays), configs, timetables);
		var scope = NotificationRuleScope.Create(year.Id, Guid.NewGuid(), medium, standard, division);

		var days = await reader.ReadAsync(scope, new DateOnly(2026, 11, 25), default);

		days.ShouldNotBeNull();
		days.PublishedRanges.Count.ShouldBe(1);
		days.CalendarDays.Select(x => x.Date.Day).Order().ShouldBe(new[] { 15, 16, 17 });
		days.Weekdays.Order().ShouldBe(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday });
	}

	private static AcademicCalendar Calendar(Guid school, Guid medium, Guid year, bool published)
	{
		var calendar = new AcademicCalendar(Guid.NewGuid(), school, medium, year, "School calendar", Date(1), Date(30));
		if (published) calendar.Publish(Guid.NewGuid());
		return calendar;
	}

	private static AcademicCalendarDay Day(AcademicCalendar calendar, int day, Guid? standard = null, Guid? division = null) =>
		new(Guid.NewGuid(), calendar.TenantId, calendar.Id, Date(day), DayType.Holiday, "School closure",
			appliesToAll: !standard.HasValue, standardId: standard, divisionId: division);

	private static DateTime Date(int day) => new(2026, 11, day, 0, 0, 0, DateTimeKind.Utc);

	private static IRepository<T, Guid> Repository<T>(params T[] rows) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(rows.AsQueryable());
		return repository;
	}
}
