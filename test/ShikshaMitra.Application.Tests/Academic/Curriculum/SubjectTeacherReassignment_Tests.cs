using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectTeacherReassignment_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Allocation_Save_Updates_Slots_Without_Changing_Paired_Subjects_Or_Publication(bool published)
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await SeedAllocationAsync();
				var timetable = await AddTimetableAsync(published: published);
				var slot = timetable.Slots.Single();
				var originalId = slot.Id;
				var originalRoom = slot.Subjects.First().RoomId;
				var paired = slot.Subjects.Last();
				await ChangeTeacherAsync();
				slot.Subjects.First().TeacherId.ShouldBe(NewTeacher);
				paired.TeacherId.ShouldBe(OtherTeacher);
				slot.Subjects.First().RoomId.ShouldBe(originalRoom);
				slot.Id.ShouldBe(originalId);
				slot.Sequence.ShouldBe(5);
				slot.StartTime.ShouldBe(new TimeOnly(9, 0));
				timetable.Status.ShouldBe(published ? TimetableStatus.Published : TimetableStatus.Draft);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var persisted = await Timetables.FindByStandardAndDivisionAsync(StandardId, DivisionId);
				persisted!.Slots.Single().Subjects.Single(s => s.SubjectId == SubjectId).TeacherId.ShouldBe(NewTeacher);
			});
		}
	}

	[Fact]
	public async Task Transfers_Today_And_Future_Plans_But_Preserves_Past_And_Recorded_Lessons()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await AddTimetableAsync();
			var today = await AddPlanAsync(Today);
			var future = await AddPlanAsync(Today.AddDays(1));
			var past = await AddPlanAsync(Today.AddDays(-1));
			var recorded = await AddPlanAsync(Today);
			var draftRecorded = await AddPlanAsync(Today.AddDays(1));
			var diary = new TeacherDiary(Guid.NewGuid(), TenantId, recorded.Id, OldTeacher, Today.UtcDateTime);
			diary.Submit();
			var diaries = GetRequiredService<IRepository<TeacherDiary, Guid>>();
			await diaries.InsertAsync(diary, autoSave: true);
			await diaries.InsertAsync(new TeacherDiary(Guid.NewGuid(), TenantId, draftRecorded.Id, OldTeacher, Today.UtcDateTime), autoSave: true);
			await ChangeTeacherAsync();
			today.TeacherId.ShouldBe(NewTeacher);
			future.TeacherId.ShouldBe(NewTeacher);
			past.TeacherId.ShouldBe(OldTeacher);
			recorded.TeacherId.ShouldBe(OldTeacher);
			draftRecorded.TeacherId.ShouldBe(OldTeacher);
			diary.TeacherId.ShouldBe(OldTeacher);
			diary.Status.ShouldBe(TeacherDiaryStatus.Submitted);
		});
	}

	[Fact]
	public async Task Resaving_Repairs_Stale_Slots_And_Leaves_Eligible_Secondary_Teachers()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var stale = await AddTimetableAsync();
			var secondary = await AddTimetableAsync(Guid.NewGuid(), day: TimetableDayOfWeek.Wednesday, teacherId: OtherTeacher);
			// Simulate an allocation saved before synchronization was implemented.
			var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
			var assignment = await assignments.GetAsync(AssignmentId);
			assignment.SetPrimaryTeacher(NewTeacher);
			await assignments.UpdateAsync(assignment, autoSave: true);
			await ChangeTeacherAsync();
			stale.Slots.Single().Subjects.First().TeacherId.ShouldBe(NewTeacher);
			secondary.Slots.Single().Subjects.First().TeacherId.ShouldBe(OtherTeacher);
		});
	}

	[Fact]
	public async Task Draft_Allocation_Still_Provides_The_Saved_Slots_Teacher_Lookup_Reference()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			await AddTimetableAsync();
			await ChangeTeacherAsync(SubjectStatus.Draft);
			var timetable = await GetRequiredService<ITimetableAppService>().GetByStandardAndDivisionAsync(StandardId, DivisionId);
			var subject = timetable.Days["TUESDAY"].Single().Subjects.Single(s => s.SubjectId == SubjectId);
			subject.SubjectAssignmentId.ShouldBe(AssignmentId);
			subject.TeacherId.ShouldBe(NewTeacher);
			timetable.SubjectSummary.ShouldBeEmpty();
		});
	}
}
