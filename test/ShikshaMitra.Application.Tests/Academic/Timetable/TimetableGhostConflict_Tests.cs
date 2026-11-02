using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableGhostConflict_Tests : SubjectTeacherReassignmentTestBase
{
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
