using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarStatistics_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task List_And_Detail_Should_Count_Active_Day_Types_Without_Mixing_Calendars()
	{
		var calendarRepository = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
		var service = GetRequiredService<IAcademicCalendarAppService>();
		var mediumId = Guid.NewGuid();
		var calendar = CreateCalendar(mediumId, "Academic statistics");
		var emptyCalendar = CreateCalendar(mediumId, "Empty calendar");
		var otherCalendar = CreateCalendar(Guid.NewGuid(), "Another medium");
		var dayTypes = new[] { DayType.WorkingDay, DayType.Holiday, DayType.Vacation, DayType.HalfDay,
			DayType.Event, DayType.Exam, DayType.PTM, DayType.EmergencyClosure };
		for (var index = 0; index < dayTypes.Length; index++)
		{
			calendar.Days.Add(CreateDay(calendar.Id, dayTypes[index], index + 1));
			var cancelled = CreateDay(calendar.Id, dayTypes[index], index + 10);
			cancelled.Cancel("Excluded from active statistics");
			calendar.Days.Add(cancelled);
			otherCalendar.Days.Add(CreateDay(otherCalendar.Id, dayTypes[index], index + 1));
		}

		await WithUnitOfWorkAsync(async () =>
		{
			await calendarRepository.InsertAsync(calendar);
			await calendarRepository.InsertAsync(emptyCalendar);
			await calendarRepository.InsertAsync(otherCalendar);
		});

		var result = await service.GetCalendarsAsync(mediumId, CalendarStatus.Draft);
		result.TotalCount.ShouldBe(2);
		AssertCounts(result.Items.Single(item => item.Id == calendar.Id), 1);
		AssertCounts(result.Items.Single(item => item.Id == emptyCalendar.Id), 0);
		AssertCounts(await service.GetCalendarByIdAsync(calendar.Id), 1);
	}

	[Fact]
	public async Task Monthly_Days_Should_Keep_Vacation_End_Date_In_The_Next_Month()
	{
		var repository = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
		var service = GetRequiredService<IAcademicCalendarAppService>();
		var calendar = CreateCalendar(Guid.NewGuid(), "Cross-month vacation");
		for (var date = new DateTime(2026, 1, 30); date <= new DateTime(2026, 2, 2); date = date.AddDays(1))
		{
			calendar.Days.Add(new AcademicCalendarDay(Guid.NewGuid(), null, calendar.Id, date,
				DayType.Vacation, "Winter vacation"));
		}
		await WithUnitOfWorkAsync(() => repository.InsertAsync(calendar));

		var result = await service.GetCalendarDaysAsync(calendar.Id, DayType.Vacation, 1, 2026);

		result.Count.ShouldBe(2);
		result.All(day => day.EndDate == new DateTime(2026, 2, 2)).ShouldBeTrue();
	}

	private static AcademicCalendar CreateCalendar(Guid mediumId, string name) =>
		new(Guid.NewGuid(), null, mediumId, null, name, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

	private static AcademicCalendarDay CreateDay(Guid calendarId, DayType dayType, int day) =>
		new(Guid.NewGuid(), null, calendarId, new DateTime(2026, 1, day), dayType, dayType.ToString());

	private static void AssertCounts(AcademicCalendarDto calendar, int expected)
	{
		calendar.TotalWorkingDays.ShouldBe(expected);
		calendar.TotalHolidays.ShouldBe(expected);
		calendar.TotalVacationDays.ShouldBe(expected);
		calendar.TotalHalfDays.ShouldBe(expected);
		calendar.TotalEvents.ShouldBe(expected);
		calendar.TotalExamDays.ShouldBe(expected);
		calendar.TotalPtmDays.ShouldBe(expected);
		calendar.TotalEmergencyClosures.ShouldBe(expected);
	}

	[Fact]
	public async Task Audit_Summary_Should_Aggregate_Selected_Modules_And_Keep_Actor_Snapshots()
	{
		var tenantId = Guid.NewGuid();
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		{
			var repository = GetRequiredService<IRepository<AcademicCalendarAuditLog, Guid>>();
			var service = GetRequiredService<IAcademicCalendarAppService>();
			var actorId = Guid.NewGuid();
			var now = DateTime.UtcNow;
			var latest = CreateAudit(tenantId, actorId, now.AddHours(-1), "CalendarManagement", "Calendar manager", null);
			await WithUnitOfWorkAsync(async () =>
			{
				await repository.InsertAsync(latest);
				await repository.InsertAsync(CreateAudit(tenantId, actorId, now.AddDays(-2), "EmergencyClosure", null, "Principal"));
				await repository.InsertAsync(CreateAudit(tenantId, Guid.NewGuid(), now.AddDays(-3), "CalendarManagement", "Teacher", "Teacher"));
				await repository.InsertAsync(CreateAudit(tenantId, actorId, now.AddDays(-10), "EmergencyClosure", "Earlier name", "Earlier role"));
				await repository.InsertAsync(CreateAudit(tenantId, actorId, now.AddDays(-20), "CalendarManagement", "Old name", "Old role"));
				await repository.InsertAsync(CreateAudit(tenantId, actorId, now, "UnrelatedModule", "Unrelated name", "Unrelated role"));
			});

			var result = await service.GetAuditLogsSummaryAsync();

			result.TotalAuditEvents.ShouldBe(5);
			result.ChangesThisWeek.ShouldBe(3);
			result.ChangesLastWeek.ShouldBe(1);
			result.CriticalChanges.ShouldBe(0);
			result.LastSyncedAt.ShouldBe(latest.ChangedAt);
			result.MostActiveUser.ShouldNotBeNull();
			result.MostActiveUser.UserId.ShouldBe(actorId);
			result.MostActiveUser.ActionCount.ShouldBe(2);
			result.MostActiveUser.Name.ShouldBe("Calendar manager");
			result.MostActiveUser.Role.ShouldBe("Principal");
		}
	}

	[Fact]
	public async Task Audit_Summary_Should_Keep_Empty_Tenant_Defaults()
	{
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
		{
			var before = DateTime.UtcNow;
			var result = await GetRequiredService<IAcademicCalendarAppService>().GetAuditLogsSummaryAsync();
			result.TotalAuditEvents.ShouldBe(0);
			result.ChangesThisWeek.ShouldBe(0);
			result.ChangesLastWeek.ShouldBe(0);
			result.MostActiveUser.ShouldBeNull();
			result.LastSyncedAt.ShouldNotBeNull();
			result.LastSyncedAt.Value.ShouldBeGreaterThanOrEqualTo(before);
		}
	}

	private static AcademicCalendarAuditLog CreateAudit(Guid tenantId, Guid actorId, DateTime changedAt,
		string module, string? name, string? role)
	{
		var audit = new AcademicCalendarAuditLog(Guid.NewGuid(), tenantId, Guid.NewGuid(), CalendarAuditAction.DayAdded,
			new string('a', 10000), new string('b', 10000), actorId, module: module,
			actorNameSnapshot: name, actorRoleSnapshot: role);
		typeof(AcademicCalendarAuditLog).GetProperty(nameof(AcademicCalendarAuditLog.ChangedAt))!.SetValue(audit, changedAt);
		return audit;
	}
}
