using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectTeacherReassignmentSafety_Tests : SubjectTeacherReassignmentTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task A_Teacher_Conflict_Does_Not_Persist_The_Allocation_Or_Timetable_Change(bool pairedConflict)
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await SeedAllocationAsync();
				var timetable = await AddTimetableAsync();
				if (pairedConflict)
				{
					timetable.Slots.Single().Subjects.Single(s => s.SubjectId != SubjectId).AssignTeacher(NewTeacher);
					await Timetables.UpdateAsync(timetable, autoSave: true);
				}
				else await AddTimetableAsync(Guid.NewGuid(), teacherId: NewTeacher);
			});
			var failure = await Should.ThrowAsync<BusinessException>(() => WithTransactionalUnitOfWorkAsync(async () => { await ChangeTeacherAsync(); }));
			failure.Code.ShouldBe(TimetableErrorCodes.TeacherConflict);
			await WithUnitOfWorkAsync(async () =>
			{
				var timetable = await Timetables.FindByStandardAndDivisionAsync(StandardId, DivisionId);
				timetable!.Slots.Single().Subjects.Single(s => s.SubjectId == SubjectId).TeacherId.ShouldBe(OldTeacher);
				var assignment = await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetAsync(AssignmentId);
				assignment.Teachers.Single(t => t.TeacherType == SubjectTeacherType.Primary).TeacherId.ShouldBe(OldTeacher);
			});
		}
	}

	[Fact]
	public async Task A_Stale_Tab_Cannot_Create_A_Plan_For_The_Former_Timetable_Teacher()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var timetable = await AddTimetableAsync();
			await ChangeTeacherAsync();
			var input = Input();
			input.TeacherId = OldTeacher;
			input.TimetableSlotId = timetable.Slots.Single().Id;
			var error = await Should.ThrowAsync<BusinessException>(() => Service.CreateAsync(input));
			error.Code.ShouldBe(CurriculumErrorCodes.TimetableTeacherChanged);
			input.TeacherId = NewTeacher;
			var created = await Service.CreateAsync(input);
			created.TeacherId.ShouldBe(NewTeacher);
		});
	}

	[Fact]
	public async Task Duplicate_Future_Lesson_Plans_Block_The_Transfer()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				await SeedAllocationAsync();
				var timetable = await AddTimetableAsync();
				var slotId = timetable.Slots.Single().Id;
				await AddPlanAsync(Today.AddDays(1), slotId);
				await AddPlanAsync(Today.AddDays(1), slotId, NewTeacher);
			});
			var error = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => ChangeTeacherAsync()));
			error.Code.ShouldBe(CurriculumErrorCodes.LessonPlanReassignmentConflict);
		}
	}

	[Fact]
	public async Task An_Allocation_From_Another_Tenant_Cannot_Be_Reassigned()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId)) await WithUnitOfWorkAsync(SeedAllocationAsync);
		using (Tenant.Change(Guid.NewGuid()))
		{
			await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => ChangeTeacherAsync()));
		}
	}

	[Fact]
	public async Task New_Teacher_Gains_The_Timeline_Slot_And_Old_Teacher_Loses_It()
	{
		using (AsAdmin())
		using (Tenant.Change(TenantId))
		await WithUnitOfWorkAsync(async () =>
		{
			await SeedAllocationAsync();
			var timetable = await AddTimetableAsync();
			var target = Today.AddDays(1);
			while (target.DayOfWeek != DayOfWeek.Tuesday) target = target.AddDays(1);
			var plan = await AddPlanAsync(target, timetable.Slots.Single().Id);
			await ChangeTeacherAsync();
			await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
			var oldTimeline = await Service.GetTimelineAsync(target, OldTeacher);
			oldTimeline.Slots.ShouldNotContain(s => s.SubjectId == SubjectId && s.StandardId == StandardId);
			var newTimeline = await Service.GetTimelineAsync(target, NewTeacher);
			var newSlot = newTimeline.Slots.Single(s => s.SubjectId == SubjectId && s.StandardId == StandardId);
			newSlot.LessonPlan.ShouldNotBeNull();
			newSlot.LessonPlan.Id.ShouldBe(plan.Id);
			newSlot.LessonPlan.TeacherId.ShouldBe(NewTeacher);
		});
	}
}
