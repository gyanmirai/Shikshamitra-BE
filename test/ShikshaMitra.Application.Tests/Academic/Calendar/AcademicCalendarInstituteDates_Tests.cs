using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarInstituteDates_Tests
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly DateTime Start = new(2026, 3, 1);
	private static readonly DateTime MayEnd = new(2027, 5, 30);
	private static readonly DateTime JuneEnd = new(2027, 6, 30);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Saving_board_dates_should_update_existing_calendars_and_allow_summer_vacation(bool alreadyExtended)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var scope = await SeedAsync(alreadyExtended ? JuneEnd : MayEnd);
		await WithUnitOfWorkAsync(async () =>
		{
			var board = await GetRequiredService<IEducationBoardRepository>().GetAsync(scope.BoardId);
			await GetRequiredService<EducationBoardManager>().UpdateAsync(board, board.Type, Start, JuneEnd);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var calendars = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
			var calendar = await calendars.GetAsync(scope.CalendarId);
			calendar.StartDate.ShouldBe(Start);
			calendar.EndDate.ShouldBe(JuneEnd);
			calendar.Status.ShouldBe(CalendarStatus.Published);
			(await calendars.GetAsync(scope.ArchivedId)).EndDate.ShouldBe(MayEnd);
			(await calendars.GetAsync(scope.OtherBoardCalendarId)).EndDate.ShouldBe(MayEnd);
			var logs = await GetRequiredService<IRepository<AcademicCalendarAuditLog, Guid>>()
				.GetListAsync(log => log.AcademicCalendarId == scope.CalendarId && log.Action == CalendarAuditAction.CalendarUpdated);
			logs.Count.ShouldBe(1);
			logs[0].HasDiff.ShouldBeTrue();
			await GetRequiredService<IAcademicCalendarAppService>().CreateCalendarDayAsync(scope.CalendarId, new()
			{
				Date = new DateTimeOffset(2027, 5, 3, 0, 0, 0, TimeSpan.FromHours(5.5)),
				EndDate = new DateTimeOffset(2027, 6, 8, 0, 0, 0, TimeSpan.FromHours(5.5)),
				DayType = DayType.Vacation,
				Title = "Summer vacation",
				AppliesToAll = true
			});
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var days = await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>()
				.GetListAsync(day => day.AcademicCalendarId == scope.CalendarId && day.DayType == DayType.Vacation);
			days.Count.ShouldBe(37);
			days.Min(day => day.Date).ShouldBe(new DateTime(2027, 5, 3));
			days.Max(day => day.Date).ShouldBe(new DateTime(2027, 6, 8));
		});
	}

	[Fact]
	public async Task Extending_board_dates_should_generate_weekends_only_in_the_added_range()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var scope = await SeedAsync(MayEnd);
		await WithUnitOfWorkAsync(async () =>
		{
			var calendar = await GetRequiredService<IRepository<AcademicCalendar, Guid>>().GetAsync(scope.CalendarId);
			await GetRequiredService<IRepository<AcademicCalendarSetting, Guid>>().InsertAsync(
				new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, calendar.AcademicMediumId!.Value)
				{ WeekendRule = WeekendRule.SundayOnly });
			await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(
				new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, scope.CalendarId,
					new DateTime(2027, 5, 16), DayType.Holiday, "Existing Sunday"));
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var board = await GetRequiredService<IEducationBoardRepository>().GetAsync(scope.BoardId);
			await GetRequiredService<EducationBoardManager>().UpdateAsync(board, board.Type, Start, JuneEnd);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var days = await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>()
				.GetListAsync(day => day.AcademicCalendarId == scope.CalendarId);
			days.Select(day => day.Date).OrderBy(date => date).ShouldBe(new[]
			{
				new DateTime(2027, 5, 16),
				new DateTime(2027, 6, 6), new DateTime(2027, 6, 13),
				new DateTime(2027, 6, 20), new DateTime(2027, 6, 27)
			});
		});
	}

	[Fact]
	public async Task Shortening_dates_should_reject_before_changing_the_board_or_saved_markers()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var scope = await SeedAsync(MayEnd);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(
				new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, scope.CalendarId, MayEnd, DayType.Event, "Annual day"));
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var board = await GetRequiredService<IEducationBoardRepository>().GetAsync(scope.BoardId);
			var exception = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<EducationBoardManager>().UpdateAsync(board, board.Type, Start, new DateTime(2027, 5, 1)));
			exception.Code.ShouldBe("Academic:Calendar:DatesExcludeMarkers");
			board.EndDate.ShouldBe(MayEnd);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<AcademicCalendar, Guid>>().GetAsync(scope.CalendarId)).EndDate.ShouldBe(MayEnd);
			(await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().CountAsync(day => day.AcademicCalendarId == scope.CalendarId)).ShouldBe(1);
		});
	}

	[Fact]
	public async Task Extending_year_dates_should_extend_inherited_board_calendars_and_preserve_custom_board_dates()
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		var scope = await SeedAsync(MayEnd, MayEnd);
		await WithUnitOfWorkAsync(async () =>
		{
			var years = GetRequiredService<IAcademicYearRepository>();
			var year = await years.GetAsync(scope.YearId);
			await GetRequiredService<AcademicYearManager>().UpdateAsync(year, year.Name, Start, JuneEnd);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IEducationBoardRepository>().GetAsync(scope.BoardId)).EndDate.ShouldBe(JuneEnd);
			var calendars = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
			(await calendars.GetAsync(scope.CalendarId)).EndDate.ShouldBe(JuneEnd);
			(await calendars.GetAsync(scope.OtherBoardCalendarId)).EndDate.ShouldBe(MayEnd);
		});
	}

	private async Task<Scope> SeedAsync(DateTime boardEnd, DateTime? yearEnd = null)
	{
		var tenantId = GetRequiredService<ICurrentTenant>().Id;
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var otherBoardId = Guid.NewGuid();
		var otherMediumId = Guid.NewGuid();
		var calendar = new AcademicCalendar(Guid.NewGuid(), tenantId, mediumId, yearId, "English Academic Calendar", Start, MayEnd, isDefault: true);
		calendar.Publish(Guid.NewGuid());
		var archived = new AcademicCalendar(Guid.NewGuid(), tenantId, mediumId, yearId, "Archived calendar", Start, MayEnd);
		archived.Archive();
		var otherCalendar = new AcademicCalendar(Guid.NewGuid(), tenantId, otherMediumId, yearId, "Other board calendar", Start, MayEnd);
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new(yearId, tenantId, "2026-2027", Start, yearEnd ?? JuneEnd), true);
			var boards = GetRequiredService<IEducationBoardRepository>();
			await boards.InsertAsync(new(boardId, tenantId, EducationBoardType.CBSE, yearId, Start, boardEnd), true);
			await boards.InsertAsync(new(otherBoardId, tenantId, EducationBoardType.GSEB, yearId, Start.AddDays(1), MayEnd), true);
			var mediums = GetRequiredService<IRepository<EducationMedium, Guid>>();
			await mediums.InsertAsync(new(mediumId, tenantId, "English", boardId), true);
			await mediums.InsertAsync(new(otherMediumId, tenantId, "English", otherBoardId), true);
			var calendars = GetRequiredService<IRepository<AcademicCalendar, Guid>>();
			await calendars.InsertAsync(calendar);
			await calendars.InsertAsync(archived);
			await calendars.InsertAsync(otherCalendar);
		});
		return new(yearId, boardId, calendar.Id, archived.Id, otherCalendar.Id);
	}

	private sealed record Scope(Guid YearId, Guid BoardId, Guid CalendarId, Guid ArchivedId, Guid OtherBoardCalendarId);
}
