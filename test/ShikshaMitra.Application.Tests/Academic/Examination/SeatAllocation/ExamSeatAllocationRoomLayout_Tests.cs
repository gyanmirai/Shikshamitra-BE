using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Theory]
	[InlineData("column-wise", 1, 2)]
	[InlineData("back-to-front", 4, 3)]
	public async Task Unequal_room_columns_should_drive_automatic_preview_and_apply(string direction, int firstSeat, int secondSeat)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, room, students) = await CreateAllocatedSessionPlanAsync();
				room.SetColumnLayout([1, 3], 1);
				await _roomRepository.UpdateAsync(room, autoSave: true);
				var preview = await _seatAllocationAppService.AutoAllocatePreviewAsync(exam.Id,
					new AutoAllocateRequestDto { SeatingPlanId = planId, RoomIds = [room.Id], FillDirection = direction });
				preview.Assignments.Select(row => row.SeatNumber).ShouldBe(new[] { firstSeat, secondSeat });
				preview.EmptySeats.ShouldBe(2);
				await _seatAllocationAppService.ApplyAutoAllocatePreviewAsync(exam.Id,
					new ApplyAutoAllocatePreviewInputDto { SeatingPlanId = planId, RoomIds = [room.Id], AllocationRulesJson = "{}", Assignments = preview.Assignments });
				var layout = await _seatAllocationAppService.GetSeatLayoutAsync(exam.Id,
					new GetSeatLayoutInput { SeatingPlanId = planId, RoomId = room.Id, PageSize = 100 });
				layout.TotalSeats.ShouldBe(4);
				layout.Room!.ColumnRows.ShouldBe(new[] { 1, 3 });
				layout.Items.Where(row => row.StudentId.HasValue).Select(row => row.SeatNumber)
					.ShouldBe(new[] { firstSeat, secondSeat }, ignoreOrder: true);
				layout.Items.Where(row => row.StudentId.HasValue).Select(row => row.StudentId!.Value)
					.ShouldBe(students.Select(student => student.Id), ignoreOrder: true);
			}
		});
	}

	[Fact]
	public async Task Unequal_room_columns_should_reject_nonexistent_manual_seats_without_changing_saved_assignments()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, room, students) = await CreateAllocatedSessionPlanAsync();
				room.SetColumnLayout([1, 3], 1);
				await _roomRepository.UpdateAsync(room, autoSave: true);
				await Should.ThrowAsync<UserFriendlyException>(() => _seatAllocationAppService.SaveSeatAllocationAsync(exam.Id,
                    new SaveSeatAllocationRequestDto { SeatingPlanId = planId, RoomId = room.Id,
                        Students = [new SaveSeatStudentDto { StudentId = students[0].Id, SeatNumber = 5, IsRoomAssign = true }] }));
				var layout = await _seatAllocationAppService.GetSeatLayoutAsync(exam.Id,
					new GetSeatLayoutInput { SeatingPlanId = planId, RoomId = room.Id, PageSize = 100 });
				layout.Items.Where(row => row.StudentId.HasValue).Select(row => row.SeatNumber).ShouldBe(new[] { 1, 2 });
			}
		});
	}

	[Fact]
	public async Task Room_capacity_should_exclude_reserved_disabled_and_nonexistent_seat_markers()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, planId, room, _) = await CreateAllocatedSessionPlanAsync();
				room.SetColumnLayout([1, 3], 1);
				room.DisabledSeatNumbersCsv = "1,99";
				room.ReservedSeatNumbersCsv = "2";
				await _roomRepository.UpdateAsync(room, autoSave: true);
				var preview = await _seatAllocationAppService.AutoAllocatePreviewAsync(exam.Id,
					new AutoAllocateRequestDto { SeatingPlanId = planId, RoomIds = [room.Id] });
				preview.Assignments.Select(row => row.SeatNumber).ShouldBe(new[] { 3, 4 });
				preview.EmptySeats.ShouldBe(0);
				preview.UtilizationPercent.ShouldBe(100);
				room.ReservedSeatNumbersCsv = "2,3,4";
				await _roomRepository.UpdateAsync(room, autoSave: true);
				preview = await _seatAllocationAppService.AutoAllocatePreviewAsync(exam.Id,
					new AutoAllocateRequestDto { SeatingPlanId = planId, RoomIds = [room.Id] });
				preview.Assignments.ShouldBeEmpty();
				preview.UnallocatedStudentIds.Count.ShouldBe(2);
				preview.EmptySeats.ShouldBe(0);
			}
		});
	}
}
