using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public partial class HrSetupReviewApi_Tests
{
	[Theory]
	[InlineData(WorkCalendarHolidaySyncMode.LinkedWithOverrides, true)]
	[InlineData(WorkCalendarHolidaySyncMode.Snapshot, false)]
	[InlineData(WorkCalendarHolidaySyncMode.Independent, false)]
	public async Task Only_linked_preview_dependencies_stale_calendar_review_on_central_add_or_update(WorkCalendarHolidaySyncMode mode, bool linked)
	{
		var school = await SeedAsync(); using var client = As(school);
		await SetReviewCalendarModeAsync(school, mode);
		var initial = await GetAsync(client, school, "calendar");
		(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(initial))).EnsureSuccessStatusCode();
		var reviewed = await GetAsync(client, school, "calendar");
		var holidayId = await AddReviewCentralHolidayAsync(school);
		var added = await GetAsync(client, school, "calendar");
		added.ReviewState.ShouldBe(linked ? "stale" : "reviewed");
		(added.Fingerprint == reviewed.Fingerprint).ShouldBe(!linked);
		// The owning reader creates random temporary IDs. Identical reads must still hash identically.
		(await GetAsync(client, school, "calendar")).Fingerprint.ShouldBe(added.Fingerprint);
		if (linked) {
			(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(reviewed))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(added))).EnsureSuccessStatusCode();
		}
		var beforeUpdate = await GetAsync(client, school, "calendar");
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<Holiday, Guid>>();
			var holiday = await repository.GetAsync(holidayId);
			holiday.Update(holidayName: "Updated linked preview holiday", startDate: new DateOnly(2026, 6, 11), endDate: new DateOnly(2026, 6, 12));
			await repository.UpdateAsync(holiday, true);
		});
		var updated = await GetAsync(client, school, "calendar");
		updated.ReviewState.ShouldBe(linked ? "stale" : "reviewed");
		(updated.Fingerprint == beforeUpdate.Fingerprint).ShouldBe(!linked);
		if (linked) (await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(beforeUpdate))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task A_materialized_school_override_keeps_unrelated_central_changes_out_of_review_hash()
	{
		var school = await SeedAsync(); using var client = As(school);
		var holidayId = await AddReviewCentralHolidayAsync(school);
		await InAsync(school.TenantId, async services => {
			await services.GetRequiredService<IRepository<WorkCalendarHoliday, Guid>>().InsertAsync(new WorkCalendarHoliday(
				Guid.NewGuid(), school.TenantId, school.YearId, school.CalendarId, new DateOnly(2026, 6, 10), null,
				"Deliberate school override", WorkCalendarHolidaySource.CentralCalendar, holidayId, true, null), true);
		});
		var initial = await GetAsync(client, school, "calendar");
		(await client.PostAsJsonAsync(Url(school.YearId, "calendar"), Record(initial))).EnsureSuccessStatusCode();
		await InAsync(school.TenantId, async services => {
			var repository = services.GetRequiredService<IRepository<Holiday, Guid>>();
			var holiday = await repository.GetAsync(holidayId); holiday.Update(holidayName: "Central row hidden by school override");
			await repository.UpdateAsync(holiday, true);
		});
		var after = await GetAsync(client, school, "calendar");
		after.Fingerprint.ShouldBe(initial.Fingerprint); after.ReviewState.ShouldBe("reviewed");
	}

	private Task SetReviewCalendarModeAsync(School school, WorkCalendarHolidaySyncMode mode) => InAsync(school.TenantId, async services => {
		var repository = services.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		var calendar = await repository.GetAsync(school.CalendarId);
		calendar.Update(calendar.Name, calendar.Status, calendar.EffectiveFrom, calendar.EffectiveTo, calendar.WeeklyOffPattern,
			calendar.GetCustomWeeklyOffs(), mode); await repository.UpdateAsync(calendar, true);
	});
	private async Task<Guid> AddReviewCentralHolidayAsync(School school)
	{
		var id = Guid.Empty;
		await InAsync(school.TenantId, async services => {
			var type = HolidayType.Create(Guid.NewGuid(), "Synthetic review holiday", "#112233", tenantId: school.TenantId);
			await services.GetRequiredService<IRepository<HolidayType, Guid>>().InsertAsync(type, true);
			var holiday = Holiday.Create(school.TenantId, school.YearId, "Unmaterialized linked preview holiday", type.Id,
				new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 10));
			await services.GetRequiredService<IRepository<Holiday, Guid>>().InsertAsync(holiday, true); id = holiday.Id;
		});
		return id;
	}
}
