using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableGhostConflict_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Published_Conflict_In_Another_Board_And_Medium_Should_Identify_Its_Academic_Context(bool roomConflict)
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var standards = GetRequiredService<IRepository<Standard, Guid>>();
			var source = await standards.GetAsync(StandardId);
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>()
				.GetAsync(source.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
				new EducationBoard(Guid.NewGuid(), TenantId, EducationBoardType.GSEB, year.Id), autoSave: true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
				new EducationMedium(Guid.NewGuid(), TenantId, "Gujarati", board.Id), autoSave: true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
				new EducationMediumStage(Guid.NewGuid(), TenantId, "Higher Secondary", null, medium.Id), autoSave: true);
			var otherClass = await standards.InsertAsync(
				new Standard(Guid.NewGuid(), TenantId, "Class 12", 16, stage.Id, null), autoSave: true);
			var division = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
				new Division(Guid.NewGuid(), TenantId, "A", otherClass.Id, 30, string.Empty), autoSave: true);
			var timetable = await AddTimetableAsync(division.Id, teacherId: NewTeacher, standardId: otherClass.Id);
			var manager = GetRequiredService<TimetableManager>();
			var error = await Should.ThrowAsync<BusinessException>(() => roomConflict
				? manager.CheckRoomConflictAsync(StandardId, DivisionId, TimetableDayOfWeek.Tuesday,
					new TimeOnly(9, 0), new TimeOnly(9, 30), [timetable.Slots.Single().Subjects.First().RoomId!.Value])
				: manager.CheckTeacherConflictAsync(StandardId, DivisionId, TimetableDayOfWeek.Tuesday,
					new TimeOnly(9, 0), new TimeOnly(9, 30), [NewTeacher]));

			error.Code.ShouldBe(roomConflict ? TimetableErrorCodes.RoomConflict : TimetableErrorCodes.TeacherConflict);
			using var details = JsonDocument.Parse((string)error.Data["ConflictDetails"]!);
			var detail = details.RootElement[0];
			detail.GetProperty("ConflictingEducationBoardName").GetString().ShouldBe("GSEB");
			detail.GetProperty("ConflictingEducationMediumName").GetString().ShouldBe("Gujarati");
			detail.GetProperty("ConflictingAcademicYearName").GetString().ShouldBe(year.Name);
			error.Data["message"]!.ToString().ShouldNotBeNull().ShouldContain($"Class 12 Division A ({year.Name} / GSEB / Gujarati)");
		});
	}

	[Theory]
	[InlineData(false, "validate")]
	[InlineData(false, "save")]
	[InlineData(false, "assign")]
	[InlineData(true, "validate")]
	[InlineData(true, "save")]
	[InlineData(true, "assign")]
	public async Task Assignment_Should_Ignore_Drafts_But_Reject_Published_Reservations(bool published, string operation)
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await SeedAllocationAsync();
				await AddTimetableAsync(published: false);
				await AddTimetableAsync(Guid.NewGuid(), published: published, teacherId: NewTeacher);
			});
			var service = GetRequiredService<ITimetableAppService>();
			var input = new SaveTimetableDto
			{
				Status = TimetableStatus.Draft,
				Timetable = { [TimetableDayOfWeek.Tuesday] = [new TimetableSlotSaveDto
				{
					Sequence = 5, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30),
					Type = SlotType.Period,
					Subjects = [new() { SubjectId = SubjectId, TeacherId = NewTeacher }]
				}] }
			};
			Task ExecuteAsync() => WithUnitOfWorkAsync(async () =>
			{
				if (operation == "validate") await service.ValidateAsync(StandardId, DivisionId, input);
				else if (operation == "save") await service.SaveAsync(StandardId, DivisionId, input);
				else await service.AssignTeachersAsync(new AssignTeachersDto
				{
					StandardId = StandardId, DivisionId = DivisionId, Day = "Tuesday",
					StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30),
					Subjects = [new() { SubjectId = SubjectId, TeacherId = NewTeacher }]
				});
			});

			if (published)
			{
				var error = await Should.ThrowAsync<BusinessException>(ExecuteAsync);
				error.Code.ShouldBe(TimetableErrorCodes.TeacherConflict);
			}
			else
			{
				await ExecuteAsync();
				await WithUnitOfWorkAsync(async () =>
				{
					var saved = await Timetables.FindByStandardAndDivisionAsync(StandardId, DivisionId);
					saved!.Slots.Single().Subjects.Single(s => s.SubjectId == SubjectId).TeacherId
						.ShouldBe(operation == "validate" ? OldTeacher : NewTeacher);
				});
			}
		}
	}
}
