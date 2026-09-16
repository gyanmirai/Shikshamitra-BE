using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentReadBatching_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Kpi_And_Workload_Should_Preserve_Stored_Values_And_Not_Track_Assignment_Graphs()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(OldTeacher,
					$"teacher-{OldTeacher:N}", $"{OldTeacher:N}@test.local", TenantId) { Name = "Ada", Surname = "Lovelace" }, autoSave: true);
				var repository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
				var core = await repository.GetAsync(AssignmentId);
				core.Status = SubjectStatus.Published;
				core.WeeklyPeriods = 9;
				core.SetupHealthScore = 1;
				var elective = await AddAssignmentAsync("French");
				elective.SubjectType = SubjectType.Elective;
				elective.WeeklyPeriods = 20;
				elective.SetupHealthScore = 3;
				elective.SetPrimaryTeacher(OldTeacher);
				var optional = await AddAssignmentAsync("Sports");
				optional.SubjectType = SubjectType.Optional;
				optional.Status = SubjectStatus.Archived;
				optional.SetupHealthScore = 4;
				optional.SetSecondaryTeachers([OldTeacher]);
				await repository.UpdateManyAsync(new[] { core, elective, optional }, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();

				var kpi = await Allocations.GetKpiAsync(StandardId);
				kpi.TotalSubjects.ShouldBe(3);
				kpi.CoreSubjects.ShouldBe(1);
				kpi.ElectiveSubjects.ShouldBe(1);
				kpi.OptionalSubjects.ShouldBe(1);
				kpi.SubjectsWithTeacher.ShouldBe(2);
				kpi.SubjectsMissingTeacher.ShouldBe(1);
				kpi.PublishedSubjects.ShouldBe(1);
				kpi.DraftSubjects.ShouldBe(1);
				kpi.OverallHealthScore.ShouldBe(2);
				var empty = await Allocations.GetKpiAsync(Guid.NewGuid());
				empty.TotalSubjects.ShouldBe(0);
				empty.OverallHealthScore.ShouldBe(0);

				var workload = await Allocations.GetTeacherWorkloadAsync(OldTeacher);
				workload.TeacherName.ShouldBe("Ada Lovelace");
				workload.CurrentSubjectsCount.ShouldBe(2);
				workload.CurrentWeeklyPeriods.ShouldBe(29);
				workload.LoadStatus.ShouldBe("Overloaded");
				workload.AssignedSubjects.Select(item => item.AssignedSubjectId).ShouldBe(new[] { core.Id, elective.Id }.Order());
				workload.AssignedSubjects.Single(item => item.AssignedSubjectId == elective.Id).SubjectName.ShouldBe("French");
				workload.AssignedSubjects.ShouldAllBe(item => item.ClassName == "Progress Class");
				db.ChangeTracker.Entries<AssignedSubject>().ShouldBeEmpty();
				db.ChangeTracker.Entries<AssignedSubjectTeacher>().ShouldBeEmpty();
			}
		});
	}

	[Fact]
	public async Task List_Should_Batch_Group_Usage_And_Preserve_Group_Paging_Search_And_Read_Only_Health()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(OldTeacher,
					$"teacher-{OldTeacher:N}", $"{OldTeacher:N}@test.local", TenantId) { Name = "Ada", Surname = "Lovelace" }, autoSave: true);
				var group = new ElectiveGroup(Guid.NewGuid(), TenantId, "Languages", "#112233", AcademicMediumId);
				await GetRequiredService<IRepository<ElectiveGroup, Guid>>().InsertAsync(group, autoSave: true);
				var firstElective = await AddAssignmentAsync("French", group.Id);
				var secondElective = await AddAssignmentAsync("German", group.Id);
				var timetable = await AddTimetableAsync();
				var slot = new TimetableSlot(Guid.NewGuid(), timetable.Id, TimetableDayOfWeek.Monday, 1,
					new TimeOnly(10, 0), new TimeOnly(10, 30), SlotType.Period, subjectType: SubjectType.Elective, electiveGroupId: group.Id);
				slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, firstElective.SubjectId));
				slot.AddSubject(new TimetableSlotSubject(Guid.NewGuid(), slot.Id, secondElective.SubjectId));
				timetable.AddSlot(slot);
				await Timetables.UpdateAsync(timetable, autoSave: true);
				var exam = new Exam(Guid.NewGuid(), Guid.NewGuid(), "Combined exam", StandardId, TenantId);
				foreach (var id in new[] { SubjectId, firstElective.SubjectId, secondElective.SubjectId, group.Id })
					exam.AddSubject(new ExamSubject(Guid.NewGuid(), exam.Id, id));
				await GetRequiredService<IRepository<Exam, Guid>>().InsertAsync(exam, autoSave: true);
				var weekly = new WeeklyLectureSlot(Guid.NewGuid(), TenantId, StandardId);
				weekly.AddSubject(Guid.NewGuid(), SubjectId, null, 3);
				weekly.AddSubject(Guid.NewGuid(), null, group.Id, 4);
				await GetRequiredService<IRepository<WeeklyLectureSlot, Guid>>().InsertAsync(weekly, autoSave: true);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				await Allocations.GetListAsync(StandardId, 0, 100, includeArchived: true);
				int initialCount;
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					var result = await Allocations.GetListAsync(StandardId, 0, 100, includeArchived: true);
					result.TotalCount.ShouldBe(2);
					var core = result.Items.Single(item => item.Id == AssignmentId);
					core.DeleteDisabledReason.ShouldBe("This subject is already used in 1 syllabus chapter(s), 1 timetable slot(s), 1 exam(s).");
					core.WeeklyPeriods.ShouldBe(3);
					core.TotalSlots.ShouldBe(3);
					var elective = result.Items.Single(item => item.ElectiveGroupId == group.Id);
					elective.DeleteDisabledReason.ShouldBe("This subject group is already used in 3 timetable slot(s), 1 exam(s).");
					elective.WeeklyPeriods.ShouldBe(4);
					elective.OccupiedSlots.ShouldBe(2);
					initialCount = counter.Count;
				}
				for (var index = 0; index < 6; index++) await AddAssignmentAsync($"Unused {index}");
				SubjectAssignmentPagedResponse all;
				using (var counter = new AcademicQueryCounter(db.Database.GetDbConnection()))
				{
					all = await Allocations.GetListAsync(StandardId, 0, 100, includeArchived: true);
					all.TotalCount.ShouldBe(8);
					initialCount.ShouldBeGreaterThan(0);
					counter.Count.ShouldBe(initialCount);
				}
				var page = await Allocations.GetListAsync(StandardId, 1, 3, includeArchived: true);
				page.Items.Select(item => item.Id).ShouldBe(all.Items.Skip(3).Take(3).Select(item => item.Id));
				page.TotalPages.ShouldBe(3);
				var searched = await Allocations.GetListAsync(StandardId, 0, 100, includeArchived: true, teacherSearch: "  ADA  ");
				searched.Items.Single().Id.ShouldBe(AssignmentId);
				(await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetAsync(AssignmentId)).WeeklyPeriods.ShouldBe(0);
			}
		});
	}

	private async Task<AssignedSubject> AddAssignmentAsync(string name, Guid? groupId = null)
	{
		var subject = new Subject(Guid.NewGuid(), TenantId, name, "#112233", AcademicMediumId);
		await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
		return await GetRequiredService<IRepository<AssignedSubject, Guid>>().InsertAsync(new AssignedSubject(
			Guid.NewGuid(), TenantId, StandardId, subject.Id, name, subjectType: groupId.HasValue ? SubjectType.Elective : SubjectType.Core,
			electiveGroupId: groupId), autoSave: true);
	}
}
