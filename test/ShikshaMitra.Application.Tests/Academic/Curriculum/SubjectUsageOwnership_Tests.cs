using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using SchoolTimetable = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectUsageOwnership_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Usage_Should_Exclude_Children_Of_Deleted_And_Other_Tenant_Parents()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var tenant = GetRequiredService<ICurrentTenant>();
			var tenantId = Guid.NewGuid();
			using (tenant.Change(tenantId))
			{
				var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
				var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
				var subject = new Subject(Guid.NewGuid(), tenantId, "Ownership subject", "#00A76F", stage.EducationMediumId);
				var group = new ElectiveGroup(Guid.NewGuid(), tenantId, "Ownership group", "#00A76F", stage.EducationMediumId);
				await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
				await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
				var parents = await InsertParentsAsync(tenantId, subject.Id, group.Id);
				var subjectService = GetRequiredService<ISubjectAppService>();
				var groupService = GetRequiredService<IElectiveGroupAppService>();
				var activeUsage = await subjectService.CheckUsagesAsync(subject.Id);
				activeUsage.TimetableSlots.ShouldBe(1);
				activeUsage.WeeklyLectureSlots.ShouldBe(1);
				activeUsage.ExamSubjects.ShouldBe(1);
				var activeGroup = (await groupService.GetListAsync(stage.EducationMediumId)).Single(item => item.Id == group.Id);
				activeGroup.TimetablePeriodsCount.ShouldBe(2);
				activeGroup.ExamSchedulesCount.ShouldBe(1);

				using (tenant.Change(Guid.NewGuid()))
				{
					// Deliberately reference the same IDs to prove ownership is enforced by the parent filter.
					await InsertParentsAsync(tenant.Id!.Value, subject.Id, group.Id);
				}
				await GetRequiredService<IRepository<SchoolTimetable, Guid>>().DeleteAsync(parents.Timetable, autoSave: true);
				await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>().DeleteAsync(parents.WeeklySlot, autoSave: true);
				await GetRequiredService<IRepository<Exam, Guid>>().DeleteAsync(parents.Exam, autoSave: true);

				var usage = await subjectService.CheckUsagesAsync(subject.Id);
				usage.TimetableSlots.ShouldBe(0);
				usage.WeeklyLectureSlots.ShouldBe(0);
				usage.ExamSubjects.ShouldBe(0);
				usage.HasUsages.ShouldBeFalse();
				var groupDto = (await groupService.GetListAsync(stage.EducationMediumId)).Single(item => item.Id == group.Id);
				groupDto.TimetablePeriodsCount.ShouldBe(0);
				groupDto.ExamSchedulesCount.ShouldBe(0);
				groupDto.IsUsed.ShouldBeFalse();
				groupDto.CanDelete.ShouldBeTrue();

				// Deletion uses the same ownership rules as the displayed usage count.
				await subjectService.DeleteAsync(subject.Id);
				await GetRequiredService<IUnitOfWorkManager>().Current!.SaveChangesAsync();
				(await GetRequiredService<IRepository<Subject, Guid>>().FindAsync(subject.Id)).ShouldBeNull();
			}
		});
	}

	private async Task<(SchoolTimetable Timetable, WeeklyLectureSlot WeeklySlot, Exam Exam)> InsertParentsAsync(
		Guid tenantId, Guid subjectId, Guid groupId)
	{
		var timetable = new SchoolTimetable(Guid.NewGuid(), tenantId, Guid.NewGuid(), Guid.NewGuid());
		var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 1,
			new TimeOnly(9, 0), new TimeOnly(9, 30), SlotType.Period, subjectType: SubjectType.Elective, electiveGroupId: groupId);
		slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, subjectId, Guid.NewGuid()));
		timetable.AddSlot(slot);
		await GetRequiredService<IRepository<SchoolTimetable, Guid>>().InsertAsync(timetable, autoSave: true);
		var weekly = new WeeklyLectureSlot(Guid.NewGuid(), tenantId, Guid.NewGuid());
		weekly.AddSubject(Guid.NewGuid(), subjectId, null, 1);
		weekly.AddSubject(Guid.NewGuid(), null, groupId, 1);
		await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>().InsertAsync(weekly, autoSave: true);
		var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "Usage exam", Guid.NewGuid(), tenantId);
		exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, subjectId));
		exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, groupId));
		await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
		return (timetable, weekly, exam);
	}
}
