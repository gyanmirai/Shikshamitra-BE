using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableElectiveAssignments_Tests : LessonPlanProgressTestBase
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public async Task Read_Should_Leave_New_Elective_Unassigned_And_Preserve_Saved_Assignments(
		bool hasSlotConfiguration, bool secondSubjectSaved)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedElectiveTimetableAsync(hasSlotConfiguration, secondSubjectSaved);
				var service = GetRequiredService<ITimetableAppService>();
				var response = await service.GetByStandardAndDivisionAsync(StandardId, DivisionId);
				var input = new SaveTimetableDto { Status = TimetableStatus.Draft };
				foreach (var day in new[] { TimetableDayOfWeek.Thursday, TimetableDayOfWeek.Friday, TimetableDayOfWeek.Saturday })
				{
					var slot = response.Days[day.ToString().ToUpperInvariant()].ShouldHaveSingleItem();
					slot.Subjects.Count.ShouldBe(2);
					var original = slot.Subjects.Single(subject => subject.SubjectId == SubjectId);
					original.TeacherId.ShouldBe(TeacherEmployeeId);
					original.RoomId.ShouldBe(fixture.RoomId);
					var added = slot.Subjects.Single(subject => subject.SubjectId == fixture.SecondSubjectId);
					added.TeacherId.ShouldBe(secondSubjectSaved ? fixture.SecondTeacherId : null);
					added.RoomId.ShouldBe(secondSubjectSaved ? fixture.SecondRoomId : null);
					input.Timetable[day] = [new TimetableSlotSaveDto
					{
						Sequence = slot.Sequence, StartTime = TimeOnly.Parse(slot.StartTime),
						EndTime = TimeOnly.Parse(slot.EndTime), Type = slot.Type,
						SubjectType = slot.SubjectType, ElectiveGroupId = slot.ElectiveGroupId,
						Subjects = slot.Subjects.Select(subject => new TimetableSubjectSaveDto
						{
							SubjectId = subject.SubjectId, TeacherId = subject.TeacherId, RoomId = subject.RoomId
						}).ToList()
					}];
				}
				await service.ValidateAsync(StandardId, DivisionId, input);
				var persisted = await GetRequiredService<ITimetableRepository>()
					.FindByStandardAndDivisionAsync(StandardId, DivisionId);
				persisted!.Slots.ShouldAllBe(slot => slot.Subjects.Count == (secondSubjectSaved ? 2 : 1));
			}
		});
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Validation_Should_Still_Reject_Explicit_Duplicate_Teacher_Or_Room(bool duplicateTeacher)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsLessonPlanOwner())
			{
				var fixture = await SeedElectiveTimetableAsync(false, true);
				var input = new SaveTimetableDto
				{
					Status = TimetableStatus.Draft,
					Timetable = { [TimetableDayOfWeek.Thursday] = [new TimetableSlotSaveDto
					{
						Sequence = 1, StartTime = new TimeOnly(15, 30), EndTime = new TimeOnly(16, 0),
						Type = SlotType.Period, SubjectType = SubjectType.Elective,
						ElectiveGroupId = fixture.GroupId,
						Subjects = [
							new() { SubjectId = SubjectId, TeacherId = TeacherEmployeeId, RoomId = fixture.RoomId },
							new() { SubjectId = fixture.SecondSubjectId,
								TeacherId = duplicateTeacher ? TeacherEmployeeId : fixture.SecondTeacherId,
								RoomId = duplicateTeacher ? fixture.SecondRoomId : fixture.RoomId }
						]
					}] }
				};
				var error = await Should.ThrowAsync<BusinessException>(() =>
					GetRequiredService<ITimetableAppService>().ValidateAsync(StandardId, DivisionId, input));
				error.Code.ShouldBe(duplicateTeacher ? TimetableErrorCodes.TeacherConflict : TimetableErrorCodes.RoomConflict);
			}
		});
	}

	private async Task<ElectiveFixture> SeedElectiveTimetableAsync(bool hasSlotConfiguration, bool secondSubjectSaved)
	{
		await SeedContextAsync();
		await SeedLessonPlanOwnerAsync();
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), TenantId, TeacherUserId), autoSave: true);
		var second = await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(
			new Subject(Guid.NewGuid(), TenantId, "Pt", "#112233", AcademicMediumId), autoSave: true);
		var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Computer Science | Pt", "#112233", AcademicMediumId);
		group.AddSubject(SubjectId);
		group.AddSubject(second.Id);
		await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
		var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(standard.EducationMediumStageId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(AcademicMediumId);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
		foreach (var subjectId in new[] { SubjectId, second.Id })
		{
			var student = await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(
				new Student(Guid.NewGuid(), TenantId, "Elective", null, "Student", board.AcademicYearId,
					board.Id, medium.Id, stage.Id, StandardId, DivisionId), autoSave: true);
			await GetRequiredService<IRepository<StudentElectiveSelection, Guid>>().InsertAsync(
				new StudentElectiveSelection(Guid.NewGuid(), TenantId, student.Id, group.Id, subjectId, medium.Id), autoSave: true);
		}
		var fixture = new ElectiveFixture(group.Id, second.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var timetable = new Timetables.Timetable(Guid.NewGuid(), TenantId, StandardId, DivisionId);
		foreach (var day in new[] { TimetableDayOfWeek.Thursday, TimetableDayOfWeek.Friday, TimetableDayOfWeek.Saturday })
		{
			var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, day, 1,
				new TimeOnly(15, 30), new TimeOnly(16, 0), SlotType.Period,
				subjectType: SubjectType.Elective, electiveGroupId: group.Id);
			slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, SubjectId, TeacherEmployeeId, fixture.RoomId));
			if (secondSubjectSaved)
			{
				slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, second.Id, fixture.SecondTeacherId, fixture.SecondRoomId));
			}
			timetable.AddSlot(slot);
		}
		await GetRequiredService<ITimetableRepository>().InsertAsync(timetable, autoSave: true);
		if (hasSlotConfiguration)
		{
			var config = new LectureSlotConfig(Guid.NewGuid(), TenantId, StandardId, "15:30-16:00");
			var slotGroup = new LectureSlotGroup(Guid.NewGuid(), config.Id, 1, 3, 5, [3, 4, 5]);
			typeof(LectureSlotGroup).GetProperty(nameof(LectureSlotGroup.TenantId))!.SetValue(slotGroup, TenantId);
			slotGroup.AddTimeSlot(new LectureSlotTimeSlot(Guid.NewGuid(), slotGroup.Id, new TimeOnly(15, 30), new TimeOnly(16, 0), SlotType.Period));
			config.AddSlotGroup(slotGroup);
			await GetRequiredService<ILectureSlotConfigRepository>().InsertAsync(config, autoSave: true);
		}
		return fixture;
	}

	private sealed record ElectiveFixture(Guid GroupId, Guid SecondSubjectId, Guid RoomId, Guid SecondTeacherId, Guid SecondRoomId);
}
