using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomAvailabilityAppService_Tests
{
	[Theory]
	[InlineData(DayType.Vacation)]
	[InlineData(DayType.Holiday)]
	public async Task Weekly_Rooms_Should_Ignore_Dated_Closures_Without_Changing_Dated_Availability(DayType dayType)
	{
		var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
		var date = new DateTime(2030, 1, 7);
		await WithUnitOfWorkAsync(async () =>
		{
			var calendar = new AcademicCalendar(Guid.NewGuid(), null, Guid.NewGuid(), null,
				"Weekly room vacation", date, date.AddDays(7));
			calendar.Publish(Guid.NewGuid());
			calendar.Days.Add(new AcademicCalendarDay(Guid.NewGuid(), null, calendar.Id, date, dayType, "Vacation"));
			await GetRequiredService<IRepository<AcademicCalendar, Guid>>().InsertAsync(calendar, autoSave: true);
			var dated = await _roomAvailabilityAppService.GetAvailabilityAsync(new RoomAvailabilityFilterDto
			{ Date = date, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0) });
			dated.Single(row => row.RoomId == room.Id).Status.ShouldBe("occupied");
			var weekly = await _roomAvailabilityAppService.GetAvailabilityAsync(WeeklyFilter(date));
			weekly.Single(row => row.RoomId == room.Id).Status.ShouldBe("available");
			weekly.Single(row => row.RoomId == room.Id).ConflictReason.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Weekly_Rooms_Should_Ignore_One_Off_Reservations_And_Maintenance()
	{
		var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
		var date = new DateTime(2030, 1, 7);
		await WithUnitOfWorkAsync(async () =>
		{
			await _reservationRepository.InsertAsync(new RoomReservation(Guid.NewGuid(), null, room.Id, date,
				new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), "One-off meeting", RoomReservationType.Meeting), autoSave: true);
			await GetRequiredService<IRepository<RoomMaintenanceBlock, Guid>>().InsertAsync(
				new RoomMaintenanceBlock(Guid.NewGuid(), null, room.Id, "Repair", "One-off repair",
					MaintenanceSeverity.High, date.AddHours(8), date.AddHours(12)), autoSave: true);
			var dated = await _roomAvailabilityAppService.GetAvailabilityAsync(new RoomAvailabilityFilterDto
			{ Date = date, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0) });
			dated.Single(row => row.RoomId == room.Id).Status.ShouldNotBe("available");
			var weekly = await _roomAvailabilityAppService.GetAvailabilityAsync(WeeklyFilter(date));
			weekly.Single(row => row.RoomId == room.Id).Status.ShouldBe("available");
		});
	}

	[Theory]
	[InlineData(true, TimetableDayOfWeek.Monday, "available")]
	[InlineData(false, TimetableDayOfWeek.Monday, "occupied")]
	[InlineData(false, TimetableDayOfWeek.Tuesday, "available")]
	public async Task Weekly_Rooms_Should_Check_The_Requested_Weekday_And_Exclude_The_Current_Timetable(
		bool ownTimetable, TimetableDayOfWeek requestedDay, string expectedStatus)
	{
		var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
		var standardId = Guid.NewGuid();
		var divisionId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var timetable = new Timetables.Timetable(Guid.NewGuid(), null, standardId, divisionId);
			var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 1,
				new TimeOnly(9, 0), new TimeOnly(10, 0), SlotType.Period);
			slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid(), roomId: room.Id));
			timetable.AddSlot(slot);
			timetable.Publish();
			await GetRequiredService<ITimetableRepository>().InsertAsync(timetable, autoSave: true);
			var filter = WeeklyFilter(new DateTime(2030, 1, 8), requestedDay,
				ownTimetable ? standardId : Guid.NewGuid(), ownTimetable ? divisionId : Guid.NewGuid());
			var result = await _roomAvailabilityAppService.GetAvailabilityAsync(filter);
			result.Single(row => row.RoomId == room.Id).Status.ShouldBe(expectedStatus);
			if (expectedStatus == "occupied")
			{
				result.Single(row => row.RoomId == room.Id).ConflictReason.ShouldNotBeNull().ShouldContain("Monday");
				filter.IncludeOccupied = false;
				(await _roomAvailabilityAppService.GetAvailabilityAsync(filter)).ShouldNotContain(row => row.RoomId == room.Id);
			}
		});
	}

	private static RoomAvailabilityFilterDto WeeklyFilter(DateTime date,
		TimetableDayOfWeek day = TimetableDayOfWeek.Monday, Guid? standardId = null, Guid? divisionId = null) =>
		new()
		{
			Date = date, WeeklyTimetableDay = day, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0),
			StandardId = standardId, DivisionId = divisionId, IncludeOccupied = true
		};

	[Fact]
	public async Task Weekly_Rooms_Should_Not_Expose_Other_Tenant_Rooms()
	{
		var tenant = GetRequiredService<ICurrentTenant>();
		var firstTenant = Guid.NewGuid();
		Guid firstRoom;
		Guid secondRoom;
		using (tenant.Change(firstTenant))
		{
			firstRoom = (await SeedRoomAndReservationAsync(Guid.NewGuid())).room.Id;
		}
		using (tenant.Change(Guid.NewGuid()))
		{
			secondRoom = (await SeedRoomAndReservationAsync(Guid.NewGuid())).room.Id;
		}
		using (tenant.Change(firstTenant))
		await WithUnitOfWorkAsync(async () =>
		{
			var result = await _roomAvailabilityAppService.GetAvailabilityAsync(WeeklyFilter(default));
			result.ShouldContain(row => row.RoomId == firstRoom);
			result.ShouldNotContain(row => row.RoomId == secondRoom);
		});
	}

	[Theory]
	[InlineData(true, 9, "available")]
	[InlineData(false, 9, "occupied")]
	[InlineData(false, 10, "available")]
	public async Task Weekly_Rooms_Should_Preserve_Class_Shift_Conflicts_And_Allow_Boundary_Times(
		bool currentDivision, int startHour, string expectedStatus)
	{
		var tenantId = Guid.NewGuid();
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			var (room, _) = await SeedRoomAndReservationAsync(Guid.NewGuid());
			var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
			var shift = await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(
				new ShiftManagement(Guid.NewGuid(), tenantId, "Morning", new TimeOnly(8, 0), new TimeOnly(10, 0)), autoSave: true);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
				new Standard(Guid.NewGuid(), tenantId, "Class 9", 10, stageId, shift.Id), autoSave: true);
			var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
				new Division(Guid.NewGuid(), tenantId, "A", standard.Id, 30, room.Id.ToString()), autoSave: true);
			var filter = WeeklyFilter(default, TimetableDayOfWeek.Monday,
				currentDivision ? standard.Id : Guid.NewGuid(), currentDivision ? division.Id : Guid.NewGuid());
			filter.StartTime = new TimeSpan(startHour, 0, 0);
			filter.EndTime = new TimeSpan(startHour + 1, 0, 0);
			var result = await _roomAvailabilityAppService.GetAvailabilityAsync(filter);
			result.Single(row => row.RoomId == room.Id).Status.ShouldBe(expectedStatus);
			if (expectedStatus == "occupied") result.Single(row => row.RoomId == room.Id).ConflictReason.ShouldNotBeNull().ShouldContain("Morning");
		});
	}
}
