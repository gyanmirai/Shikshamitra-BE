using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Academic.Timetable;

public class TimetableConflictQuery_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public async Task Candidates_Should_Keep_Tenant_Isolation_And_Exclude_Current_Class_And_Unrelated_Slots()
	{
		var repository = GetRequiredService<ITimetableRepository>();
		var currentTenant = GetRequiredService<ICurrentTenant>();
		var tenantId = Guid.NewGuid();
		var otherTenantId = Guid.NewGuid();
		var standardId = Guid.NewGuid();
		var divisionId = Guid.NewGuid();
		var teacherId = Guid.NewGuid();
		var roomId = Guid.NewGuid();
		var otherClass = CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(), teacherId, roomId);
		var sameStandardOtherDivision = CreateTimetable(tenantId, standardId, Guid.NewGuid(), teacherId, roomId);

		using (currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await repository.InsertAsync(otherClass);
				await repository.InsertAsync(sameStandardOtherDivision);
				await repository.InsertAsync(CreateTimetable(tenantId, standardId, divisionId, teacherId, roomId));
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(), teacherId, roomId, TimetableDayOfWeek.Tuesday));
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(), teacherId, roomId,
					start: new TimeOnly(10, 0), end: new TimeOnly(11, 0)));
			});
		}

		using (currentTenant.Change(otherTenantId))
		{
			await WithUnitOfWorkAsync(() => repository.InsertAsync(
				CreateTimetable(otherTenantId, Guid.NewGuid(), Guid.NewGuid(), teacherId, roomId)));
		}

		using (currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var rows = await repository.GetConflictCandidatesAsync(standardId, divisionId,
					[TimetableDayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(10, 0), [teacherId], [roomId]);

				rows.Count.ShouldBe(2);
				rows.Select(row => row.DivisionId).ShouldBe(
					new[] { otherClass.DivisionId, sameStandardOtherDivision.DivisionId }, ignoreOrder: true);
			});
		}
	}

	[Fact]
	public async Task Room_Occupations_Should_Read_Only_Matching_Tenant_Room_Day_And_Time()
	{
		var repository = GetRequiredService<ITimetableRepository>();
		var currentTenant = GetRequiredService<ICurrentTenant>();
		var tenantId = Guid.NewGuid();
		var otherTenantId = Guid.NewGuid();
		var roomId = Guid.NewGuid();
		var matching = CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), roomId);

		using (currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await repository.InsertAsync(matching);
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(),
					Guid.NewGuid(), Guid.NewGuid()));
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(),
					Guid.NewGuid(), roomId, TimetableDayOfWeek.Tuesday));
				await repository.InsertAsync(CreateTimetable(tenantId, Guid.NewGuid(), Guid.NewGuid(),
					Guid.NewGuid(), roomId, start: new TimeOnly(10, 0), end: new TimeOnly(11, 0)));
			});
		}

		using (currentTenant.Change(otherTenantId))
		{
			await WithUnitOfWorkAsync(() => repository.InsertAsync(CreateTimetable(otherTenantId,
				Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), roomId)));
		}

		using (currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var rows = await repository.GetRoomOccupationsAsync([roomId], TimetableDayOfWeek.Monday,
					new TimeOnly(9, 0), new TimeOnly(10, 0));
				rows.Count.ShouldBe(1);
				rows[0].DivisionId.ShouldBe(matching.DivisionId);
				rows[0].RoomId.ShouldBe(roomId);
			});
		}
	}

	private static ShikshaMitra.Academic.Timetable.Timetables.Timetable CreateTimetable(
		Guid tenantId, Guid standardId, Guid divisionId, Guid teacherId, Guid roomId,
		TimetableDayOfWeek day = TimetableDayOfWeek.Monday, TimeOnly? start = null, TimeOnly? end = null)
	{
		var timetable = new ShikshaMitra.Academic.Timetable.Timetables.Timetable(Guid.NewGuid(), tenantId, standardId, divisionId);
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, 1,
			start ?? new TimeOnly(9, 0), end ?? new TimeOnly(10, 0), SlotType.Period);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, Guid.NewGuid(), teacherId, roomId));
		timetable.AddSlot(slot);
		return timetable;
	}
}
