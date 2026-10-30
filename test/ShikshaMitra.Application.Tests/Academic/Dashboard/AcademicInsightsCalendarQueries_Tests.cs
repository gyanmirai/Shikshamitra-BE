using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Dashboard;

public class AcademicInsightsCalendarQueries_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(0)]
	[InlineData(7)]
	public Task Calendar_Preview_Excludes_Generated_Weekends_Before_Selecting_Five(int eventCount) => WithUnitOfWorkAsync(async () =>
	{
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var medium = Guid.NewGuid();
		var division = Guid.NewGuid(); var standard = Guid.NewGuid();
		using var selectedTenant = GetRequiredService<ICurrentTenant>().Change(tenant);
		var today = Mobile.MobileAcademicContextAppService.SchoolToday;
		var calendar = new AcademicCalendar(Guid.NewGuid(), tenant, medium, year, "Preview calendar", today, today.AddYears(1));
		await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
		var days = new List<AcademicCalendarDay>();
		for (var i = 0; i < 12; i++)
			days.Add(new(Guid.NewGuid(), tenant, calendar.Id, today.AddDays(i * 7), DayType.Holiday,
				"Sunday Weekend", "System generated weekend holiday"));
		var firstSunday = today.AddDays(90);
		firstSunday = firstSunday.AddDays((7 - (int)firstSunday.DayOfWeek) % 7);
		for (var i = 0; i < eventCount; i++)
			days.Add(new(Guid.NewGuid(), tenant, calendar.Id, firstSunday.AddDays(i),
				i == 1 ? DayType.Holiday : DayType.Event, i == 0 ? "Weekend Science Fair" : $"Event {i}"));
		days.Add(new(Guid.NewGuid(), tenant, calendar.Id, today.AddDays(-1), DayType.Event, "Past event"));
		days.Add(new(Guid.NewGuid(), tenant, calendar.Id, today, DayType.Event, "Other division event",
			appliesToAll: false, standardId: standard, divisionId: Guid.NewGuid()));
		await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertManyAsync(days, autoSave: true);
		var context = new AcademicInsightsContext(new() { AcademicYearId = year, BoardId = Guid.NewGuid(), EducationMediumId = medium },
			tenant, new UserScopeContext { TenantId = tenant }, false, [new(division, standard, Guid.NewGuid(), "Class A")]);
		var result = await GetRequiredService<AcademicInsightsOperationalReader>().CalendarAsync(context);
		result.Status.ShouldBe(eventCount == 0 ? "empty" : "ready");
		result.Metrics.Single(x => x.Key == "upcoming").Value.ShouldBe(eventCount);
		result.Items.Count.ShouldBe(Math.Min(eventCount, 5));
		if (eventCount > 0)
		{
			result.Items.First().Label.ShouldBe("Weekend Science Fair");
			result.Items.First().Date.ShouldBe(firstSunday);
			result.Items.Select(x => x.Label).ShouldBe(["Weekend Science Fair", "Event 1", "Event 2", "Event 3", "Event 4"]);
		}
	});
}
