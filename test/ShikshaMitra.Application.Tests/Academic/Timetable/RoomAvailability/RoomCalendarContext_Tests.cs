using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService_Tests
{
	private static readonly DateTime GhostHolidayDate = new(2026, 10, 31);

	[Fact]
	public async Task Room_Calendar_Should_Ignore_Superseded_Vacation_In_All_Conflict_Paths()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var medium = Guid.NewGuid();
			await SeedRoomCalendarAsync(medium, 1, DayType.Vacation);
			await SeedRoomCalendarAsync(medium, 2);
			var service = GetRequiredService<RoomConflictService>();
			var start = new TimeSpan(18, 0, 0);
			var end = new TimeSpan(19, 0, 0);
			(await service.CheckConflictsAsync(room.Id, GhostHolidayDate, start, end)).HolidayCalendar.Passed.ShouldBeTrue();
			(await service.CheckRoomsAsync([room], GhostHolidayDate, start, end))[room.Id].HolidayCalendar.Passed.ShouldBeTrue();
			(await service.CheckRoomPeriodsAsync(room, GhostHolidayDate, [(start, end)])).Single().HolidayCalendar.Passed.ShouldBeTrue();
			var window = new RoomConflictWindow(room.Id, GhostHolidayDate, start, end);
			(await (await service.CreateSessionAsync([window])).CheckAsync(window)).HolidayCalendar.Passed.ShouldBeTrue();
		});
	}

	[Fact]
	public async Task Room_Availability_Should_Use_Requested_Medium_And_Live_Calendar_Days()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			await SeedRoomCalendarAsync(Guid.NewGuid(), 3, DayType.Vacation);
			var medium = Guid.NewGuid();
			var calendar = await SeedRoomCalendarAsync(medium, 2);
			var filter = JsonSerializer.Deserialize<RoomAvailabilityFilterDto>(JsonSerializer.Serialize(new
			{
				Date = GhostHolidayDate, StartTime = "18:00:00", EndTime = "19:00:00",
				AcademicMediumId = medium, IncludeOccupied = true
			}))!;
			(await _roomAvailabilityAppService.GetAvailabilityAsync(filter)).Single(row => row.RoomId == room.Id).Status.ShouldBe("available");
			var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
			var holiday = await days.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), null, calendar.Id,
				GhostHolidayDate, DayType.Holiday, "Actual holiday", autoScanImpacts: false), autoSave: true);
			(await _roomAvailabilityAppService.GetAvailabilityAsync(filter)).Single(row => row.RoomId == room.Id).ConflictReason.ShouldNotBeNull().ShouldContain("Actual holiday");
			await days.DeleteAsync(holiday, autoSave: true);
			(await _roomAvailabilityAppService.GetAvailabilityAsync(filter)).Single(row => row.RoomId == room.Id).Status.ShouldBe("available");
		});
	}

	[Fact]
	public async Task Room_Session_Should_Keep_Calendar_Context_Per_Window_On_The_Same_Date()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var closedMedium = Guid.NewGuid();
			var openMedium = Guid.NewGuid();
			await SeedRoomCalendarAsync(closedMedium, 1, DayType.Vacation);
			await SeedRoomCalendarAsync(openMedium, 2);
			var closed = new RoomConflictWindow(room.Id, GhostHolidayDate, new TimeSpan(18, 0, 0),
				new TimeSpan(19, 0, 0), AcademicMediumId: closedMedium);
			var open = closed with { AcademicMediumId = openMedium };
			var session = await GetRequiredService<RoomConflictService>().CreateSessionAsync([closed, open]);
			(await session.CheckAsync(closed)).HolidayCalendar.Passed.ShouldBeFalse();
			(await session.CheckAsync(open)).HolidayCalendar.Passed.ShouldBeTrue();
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Room_Booking_Should_Use_The_Timeline_Medium_And_Persist_It(bool recurring)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = Guid.NewGuid();
			var (room, _) = await SeedRoomAndReservationAsync(actor);
			var medium = Guid.NewGuid();
			await SeedRoomCalendarAsync(medium, 1);
			await SeedRoomCalendarAsync(Guid.NewGuid(), 2, DayType.Vacation);
			await _userScopeAssignmentRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), null,
				actor, description: "Room calendar booking regression"), autoSave: true);
			await _userScopeService.InvalidateUserScopeCacheAsync(actor);
			Guid reservationId;
			using (_currentPrincipalAccessor.Change(BuildPrincipal(actor, "HOD")))
			{
				var schedule = await _roomAvailabilityAppService.GetRoomScheduleAsync(room.Id, GhostHolidayDate, medium);
				schedule.Slots.ShouldAllBe(slot => slot.Status == "free");
				var reservation = await _roomAvailabilityAppService.CreateReservationAsync(new CreateRoomReservationDto
				{
					RoomId = room.Id, Date = GhostHolidayDate, AcademicMediumId = medium,
					StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(19, 0, 0),
					Purpose = "Room calendar regression", ReservationType = RoomReservationType.Meeting,
					IsRecurring = recurring, RecurringConfig = recurring ? new RecurringConfigDto
					{ RepeatType = "daily", UntilDate = GhostHolidayDate.AddDays(1) } : null
				});
				reservation.Status.ShouldBe(RoomReservationStatus.PendingApproval);
				reservationId = reservation.Id;
			}
			var database = await RoomDatabaseAsync();
			await database.SaveChangesAsync();
			database.ChangeTracker.Clear();
			var stored = await _reservationRepository.GetAsync(reservationId);
			stored.ExtraProperties["CalendarMediumId"].ToString().ShouldBe(medium.ToString());
			if (recurring)
			{
				var children = await _reservationRepository.GetListAsync(row =>
					row.RecurringConfigId == stored.RecurringConfigId && !row.IsRecurring);
				children.Count.ShouldBe(1);
				children.ShouldAllBe(row => row.ExtraProperties["CalendarMediumId"].ToString() == medium.ToString());
			}
			using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "admin")))
			{
				await _roomAvailabilityAppService.ApproveReservationAsync(reservationId);
				(await _reservationRepository.GetAsync(reservationId)).Status.ShouldBe(RoomReservationStatus.Approved);
			}
		});
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	public async Task Room_Calendar_Should_Match_Only_The_Exact_Date(int offset)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var calendar = await SeedRoomCalendarAsync(Guid.NewGuid(), 1);
			await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new AcademicCalendarDay(
				Guid.NewGuid(), null, calendar.Id, GhostHolidayDate.AddDays(offset), DayType.Vacation, "Adjacent vacation",
				autoScanImpacts: false), autoSave: true);
			(await GetRequiredService<RoomConflictService>().CheckConflictsAsync(room.Id,
				GhostHolidayDate, new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0))).HolidayCalendar.Passed.ShouldBeTrue();
		});
	}

	[Fact]
	public async Task Room_Calendar_Should_Find_Blocking_Marker_When_Working_Marker_Also_Exists()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var calendar = await SeedRoomCalendarAsync(Guid.NewGuid(), 1, DayType.WorkingDay);
			await GetRequiredService<IRepository<AcademicCalendarDay, Guid>>().InsertAsync(new AcademicCalendarDay(
				Guid.NewGuid(), null, calendar.Id, GhostHolidayDate, DayType.Vacation, "Actual vacation", autoScanImpacts: false), autoSave: true);
			var result = await GetRequiredService<RoomConflictService>().CheckConflictsAsync(room.Id,
				GhostHolidayDate, new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0));
			result.HolidayCalendar.Passed.ShouldBeFalse();
			result.HolidayCalendar.Message.ShouldNotBeNull().ShouldContain("Actual vacation");
		});
	}

	private async Task<AcademicCalendar> SeedRoomCalendarAsync(Guid medium, int order, DayType? type = null,
		bool publish = true, bool isDefault = false)
	{
		var mediums = GetRequiredService<IRepository<EducationMedium, Guid>>();
		if (!await mediums.AnyAsync(row => row.Id == medium))
		{
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(new AcademicYear(
				Guid.NewGuid(), tenant, $"Room year {order}", GhostHolidayDate.AddMonths(-6), GhostHolidayDate.AddMonths(6)), autoSave: true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(new EducationBoard(
				Guid.NewGuid(), tenant, EducationBoardType.CBSE, year.Id), autoSave: true);
			await mediums.InsertAsync(new EducationMedium(medium, tenant, $"Room medium {order}", board.Id), autoSave: true);
		}
		var calendar = new AcademicCalendar(new Guid($"00000000-0000-0000-0000-{order:000000000000}"), null,
			medium, null, $"Room calendar {order}", GhostHolidayDate.AddDays(-10), GhostHolidayDate.AddDays(10), isDefault: isDefault);
		typeof(AcademicCalendar).GetProperty(nameof(AcademicCalendar.CreationTime))!.SetValue(calendar, GhostHolidayDate.AddDays(order - 20));
		if (publish) calendar.Publish(Guid.NewGuid());
		if (type.HasValue) calendar.Days.Add(new AcademicCalendarDay(Guid.NewGuid(), null, calendar.Id,
			GhostHolidayDate, type.Value, "Diwali vacation", autoScanImpacts: false));
		return await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
	}

	[Theory]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(false, false, false)]
	public async Task Room_Calendar_Should_Match_Picker_Published_Default_And_Newest_Priority(
		bool published, bool defaultDraft, bool blocked)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var medium = Guid.NewGuid();
			await SeedRoomCalendarAsync(medium, 1, DayType.Vacation, published, defaultDraft);
			await SeedRoomCalendarAsync(medium, 2, publish: false, isDefault: published);
			(await GetRequiredService<RoomConflictService>().CheckConflictsAsync(room.Id, GhostHolidayDate,
				new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0), academicMediumId: medium)).HolidayCalendar.Passed.ShouldBe(!blocked);
		});
	}

	[Fact]
	public async Task Room_Calendar_Should_Reject_Unknown_Medium_Instead_Of_Bypassing_Holidays()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			await SeedRoomCalendarAsync(Guid.NewGuid(), 1, DayType.Vacation);
			await Should.ThrowAsync<EntityNotFoundException>(() => GetRequiredService<RoomConflictService>()
				.CheckConflictsAsync(room.Id, GhostHolidayDate, new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0),
					academicMediumId: Guid.NewGuid()));
		});
	}

	[Theory]
	[InlineData("archived")]
	[InlineData("cancelled")]
	[InlineData("deleted")]
	[InlineData("other-division")]
	public async Task Room_Calendar_Should_Ignore_Inapplicable_Or_Inactive_Markers(string kind)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var medium = Guid.NewGuid();
			var calendar = await SeedRoomCalendarAsync(medium, 1);
			var days = GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
			var day = new AcademicCalendarDay(Guid.NewGuid(), null, calendar.Id, GhostHolidayDate,
				DayType.Vacation, "Inactive vacation", appliesToAll: kind != "other-division",
				divisionId: kind == "other-division" ? Guid.NewGuid() : null, autoScanImpacts: false);
			if (kind == "cancelled") day.Cancel("Regression");
			await days.InsertAsync(day, autoSave: true);
			if (kind == "deleted") await days.DeleteAsync(day, autoSave: true);
			if (kind == "archived")
			{
				calendar.Archive();
				await GetRequiredService<IRepository<AcademicCalendar, Guid>>().UpdateAsync(calendar, autoSave: true);
			}
			(await GetRequiredService<RoomConflictService>().CheckConflictsAsync(room.Id, GhostHolidayDate,
				new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0), academicMediumId: medium)).HolidayCalendar.Passed.ShouldBeTrue();
		});
	}

	[Fact]
	public async Task Room_Calendar_Should_Reject_Another_Tenants_Medium()
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		var medium = Guid.NewGuid();
		using (tenant.Change(Guid.NewGuid()))
			await WithUnitOfWorkAsync(async () => { await SeedRoomCalendarAsync(medium, 1); });
		using (tenant.Change(Guid.NewGuid()))
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var service = GetRequiredService<RoomConflictService>();
			await Should.ThrowAsync<EntityNotFoundException>(() => service.CheckRoomPeriodsAsync(room, GhostHolidayDate,
				[(new TimeSpan(18, 0, 0), new TimeSpan(19, 0, 0))], medium));
			var window = new RoomConflictWindow(room.Id, GhostHolidayDate, new TimeSpan(18, 0, 0),
				new TimeSpan(19, 0, 0), AcademicMediumId: medium);
			await Should.ThrowAsync<EntityNotFoundException>(() => service.CreateSessionAsync([window]));
		});
	}
}
