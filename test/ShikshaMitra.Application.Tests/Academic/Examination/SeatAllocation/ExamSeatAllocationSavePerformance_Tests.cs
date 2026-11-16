using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService_Tests
{
	[Fact]
	public async Task Bulk_Seating_Save_Should_Restore_Swap_And_Validate_All_Rooms_Before_Seat_Changes()
	{
		var fixture = await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, plans, firstRoom, students) = await CreateAllocatedSeparateSessionPlansAsync();
				var secondRoom = await CreateRoomAsync();
				foreach (var subject in exam.ExamSubjects)
					foreach (var assignment in subject.RoomAssignments)
						assignment.UpdateRooms([firstRoom.Id, secondRoom.Id], firstRoom.Capacity + secondRoom.Capacity);
				await _examRepository.UpdateAsync(exam, autoSave: true);
				var removed = (await _seatAllocationRepository.GetListAsync(row => row.SeatingPlanId == plans[0]))
					.Single(row => row.StudentId == students[1].Id);
				await _seatAllocationRepository.DeleteAsync(removed, autoSave: true);
				return (ExamId: exam.Id, PlanId: plans[0], FirstRoom: firstRoom.Id, SecondRoom: secondRoom.Id,
					FirstStudent: students[0].Id, SecondStudent: students[1].Id, RestoredRow: removed.Id);
			}
		});

		Task<SeatingPlanDraftResponseDto> SaveAsync(List<SaveSeatingPlanBulkRoomDto> rooms) => WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
				return await _seatAllocationAppService.SaveSeatingPlanBulkAsync(fixture.ExamId, new SaveSeatingPlanBulkInputDto
				{
					SeatingPlanId = fixture.PlanId, RoomIds = [fixture.FirstRoom, fixture.SecondRoom],
					AllocationRulesJson = "{}", Rooms = rooms
				});
		});
		SaveSeatingPlanBulkRoomDto Room(Guid roomId, Guid studentId, int seat) => new()
		{
			RoomId = roomId, Students = [new() { StudentId = studentId, SeatNumber = seat, IsRoomAssign = true }]
		};

		await SaveAsync([Room(fixture.FirstRoom, fixture.SecondStudent, 2), Room(fixture.SecondRoom, fixture.FirstStudent, 1)]);
		await SaveAsync([Room(fixture.FirstRoom, fixture.FirstStudent, 1), Room(fixture.SecondRoom, fixture.SecondStudent, 2)]);
		var exception = await Should.ThrowAsync<UserFriendlyException>(() => SaveAsync(
			[Room(fixture.FirstRoom, fixture.SecondStudent, 1), Room(fixture.SecondRoom, fixture.FirstStudent, int.MaxValue)]));
		exception.Message.ShouldBe("One or more selected seats are outside the room layout or unavailable.");
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				var saved = await _seatAllocationRepository.GetListAsync(row => row.SeatingPlanId == fixture.PlanId);
				saved.Count.ShouldBe(2);
				var first = saved.Single(row => row.StudentId == fixture.FirstStudent);
				first.RoomId.ShouldBe(fixture.FirstRoom);
				first.SeatNumber.ShouldBe(1);
				var second = saved.Single(row => row.StudentId == fixture.SecondStudent);
				second.Id.ShouldBe(fixture.RestoredRow);
				second.RoomId.ShouldBe(fixture.SecondRoom);
				second.SeatNumber.ShouldBe(2);
			}
		});
	}

	[Theory]
	[InlineData("overlap", true)]
	[InlineData("adjacent", false)]
	[InlineData("missing-time", true)]
	[InlineData("deleted-parent", false)]
	[InlineData("foreign-parent", false)]
	public async Task Seat_Save_Should_Keep_Overlap_And_Parent_Filter_Boundaries(string scenario, bool conflictExpected)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, plans, room, students) = await CreateAllocatedSeparateSessionPlansAsync();
				var selectedSubjectId = (await _seatingPlanSubjectRepository.GetListAsync(row => row.SeatingPlanId == plans[0])).Single().ExamSubjectId;
				var currentSubject = exam.ExamSubjects.Single(subject => subject.Id == selectedSubjectId);
				var date = currentSubject.ExamDate!.Value.Date;
				var other = new Exam(Guid.NewGuid(), exam.ExamTypeId, "Other conflict candidate", exam.StandardId,
					scenario == "foreign-parent" ? Guid.NewGuid() : TestTenantId);
				var otherSubject = new ExamSubject(Guid.NewGuid(), other.Id, currentSubject.SubjectId, date,
					scenario == "missing-time" ? null : date.AddHours(scenario == "adjacent" ? 12 : 10),
					scenario == "missing-time" ? null : date.AddHours(13), 100, 33);
				other.AddSubject(otherSubject);
				await _examRepository.InsertAsync(other, autoSave: true);
				var otherPlan = new ExamSeatingPlan(Guid.NewGuid(), TestTenantId, other.Id, "Other plan", SeatingPlanScopeType.AllSubjects);
				await _seatingPlanRepository.InsertAsync(otherPlan, autoSave: true);
				await _seatingPlanSubjectRepository.InsertAsync(new ExamSeatingPlanSubject(Guid.NewGuid(), TestTenantId,
					otherPlan.Id, other.Id, otherSubject.Id, otherSubject.SubjectId), autoSave: true);
				await _seatAllocationRepository.InsertAsync(new ExamSeatAllocation(Guid.NewGuid(), TestTenantId,
					otherPlan.Id, other.Id, students[0].Id, room.Id, 1, null, true), autoSave: true);
				if (scenario == "deleted-parent") await _examRepository.DeleteAsync(other, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				Task<SaveSeatAllocationResponseDto> Save() => _seatAllocationAppService.SaveSeatAllocationAsync(exam.Id,
					new SaveSeatAllocationRequestDto { SeatingPlanId = plans[0], RoomId = room.Id,
						Students = [new() { StudentId = students[0].Id, SeatNumber = 1, IsRoomAssign = true }] });
				if (conflictExpected)
					(await Should.ThrowAsync<UserFriendlyException>(Save)).Code.ShouldBe("ROOM_SEAT_TIME_CONFLICT");
				else (await Save()).SavedCount.ShouldBe(1);
			}
		});
	}

	[Fact]
	public async Task Bulk_Seating_Save_Should_Reuse_Roster_Across_Submitted_Rooms()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, plans, firstRoom, students) = await CreateAllocatedSeparateSessionPlansAsync();
				var secondRoom = await CreateRoomAsync();
				var thirdRoom = await CreateRoomAsync();
				var roomIds = new List<Guid> { firstRoom.Id, secondRoom.Id, thirdRoom.Id };
				foreach (var subject in exam.ExamSubjects)
					foreach (var assignment in subject.RoomAssignments)
						assignment.UpdateRooms(roomIds, roomIds.Count * firstRoom.Capacity);
				await _examRepository.UpdateAsync(exam, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();

				async Task<int> SaveAndCountRosterReadsAsync(List<SaveSeatingPlanBulkRoomDto> rooms)
				{
					db.ChangeTracker.Clear();
					using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
					await _seatAllocationAppService.SaveSeatingPlanBulkAsync(exam.Id, new SaveSeatingPlanBulkInputDto
					{
						SeatingPlanId = plans[0], RoomIds = roomIds, AllocationRulesJson = "{}", Rooms = rooms
					});
					return capture.Commands.Count(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal));
				}

				var first = new SaveSeatingPlanBulkRoomDto
				{
					RoomId = firstRoom.Id,
					Students = students.Select((student, index) => new SaveSeatStudentDto
						{ StudentId = student.Id, SeatNumber = index + 1, IsRoomAssign = true }).ToList()
				};
				var oneRoomReads = await SaveAndCountRosterReadsAsync([first]);
				var threeRoomReads = await SaveAndCountRosterReadsAsync(
					[first, new() { RoomId = secondRoom.Id, Students = [] }, new() { RoomId = thirdRoom.Id, Students = [] }]);
				oneRoomReads.ShouldBeGreaterThan(0);
				threeRoomReads.ShouldBe(oneRoomReads, "Adding room patches must not reload the eligible plan roster per room.");
			}
		});
	}

	[Fact]
	public async Task Bulk_Seating_Save_Should_Not_Hydrate_Unrelated_Exam_History()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (_currentTenant.Change(TestTenantId))
			{
				await EnsureAdminUserExistsAsync();
				var (exam, plans, room, students) = await CreateAllocatedSeparateSessionPlansAsync();
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				async Task<long> SaveAndCountExamRowsAsync()
				{
					db.ChangeTracker.Clear();
					using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
					var saved = await _seatAllocationAppService.SaveSeatingPlanBulkAsync(exam.Id, new SaveSeatingPlanBulkInputDto
					{
						SeatingPlanId = plans[0], RoomIds = [room.Id], AllocationRulesJson = "{}",
						Rooms = [new() { RoomId = room.Id, Students = students.Select((student, index) =>
							new SaveSeatStudentDto { StudentId = student.Id, SeatNumber = index + 1, IsRoomAssign = true }).ToList() }]
					});
					saved.AllocatedStudents.ShouldBe(students.Count);
					return capture.Reads.Where(read => read.Sql.Contains("\"academic_Exams\"", StringComparison.Ordinal))
						.Sum(read => read.Rows);
				}
				var before = await SaveAndCountExamRowsAsync();
				var (unrelated, _) = await CreateSessionExamAsync(scheduleSecondSubject: true);
				foreach (var subject in unrelated.ExamSubjects)
					subject.UpdateSchedule(new DateTime(2035, 1, 1), new DateTime(2035, 1, 1, 9, 0, 0),
						new DateTime(2035, 1, 1, 10, 0, 0), subject.TotalMarks, subject.PassingMarks, subject.Notes);
				await _examRepository.UpdateAsync(unrelated, autoSave: true);
				var after = await SaveAndCountExamRowsAsync();
				before.ShouldBeGreaterThan(0);
				after.ShouldBe(before, "An unrelated examination on another date must not increase reader rows for this save.");
			}
		});
	}
}
