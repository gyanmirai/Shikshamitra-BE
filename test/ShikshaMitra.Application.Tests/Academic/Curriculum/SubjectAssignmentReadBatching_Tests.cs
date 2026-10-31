using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentReadBatching_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Create_Should_Map_Unified_Teachers_To_Internal_Storage()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var subject = new Subject(Guid.NewGuid(), TenantId, "Science", "#112233", AcademicMediumId);
				await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);

				await Allocations.CreateAsync(AcademicMediumId, new CreateUpdateSubjectAssignmentDto
				{
					ClassId = StandardId,
					Status = SubjectStatus.Draft,
					Subjects =
					[
						new SubjectAssignmentDetailDto
						{
							SubjectId = subject.Id,
							SubjectCode = "SCI",
							TeacherIds = [NewTeacher, OldTeacher]
						}
					]
				});

				var stored = (await GetRequiredService<IRepository<AssignedSubject, Guid>>()
					.GetListAsync(assignment => assignment.SubjectId == subject.Id)).Single();
				stored.Teachers.Single(teacher => teacher.TeacherType == SubjectTeacherType.Primary)
					.TeacherId.ShouldBe(NewTeacher);
				stored.Teachers.Single(teacher => teacher.TeacherType == SubjectTeacherType.Secondary)
					.TeacherId.ShouldBe(OldTeacher);
			}
		});
	}

	[Fact]
	public async Task Unified_Teacher_List_Should_Preserve_Existing_Internal_Primary_And_Return_Codes()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var result = await Allocations.UpdateAsync(AssignmentId, AcademicMediumId,
					new CreateUpdateSubjectAssignmentDto
					{
						ClassId = StandardId,
						Status = SubjectStatus.Draft,
						Subjects =
						[
							new SubjectAssignmentDetailDto
							{
								SubjectId = SubjectId,
								SubjectCode = "MATH",
								TeacherIds = [NewTeacher, OldTeacher]
							}
						]
					});

				var stored = await GetRequiredService<IRepository<AssignedSubject, Guid>>()
					.GetAsync(AssignmentId);
				stored.Teachers.Single(teacher => teacher.TeacherType == SubjectTeacherType.Primary)
					.TeacherId.ShouldBe(OldTeacher);
				stored.Teachers.Single(teacher => teacher.TeacherType == SubjectTeacherType.Secondary)
					.TeacherId.ShouldBe(NewTeacher);

				var teachers = result.ShouldNotBeNull().Subjects.Single().Teachers;
				teachers.Select(teacher => teacher.Id).ShouldBe([
					OldTeacher.ToString(),
					NewTeacher.ToString()
				]);
				teachers.Select(teacher => teacher.EmployeeCode).ShouldBe(["ALLOC001", "ALLOC002"]);
				var listItem = (await Allocations.GetListAsync(StandardId, 0, 100, includeArchived: true))
					.Items.Single(item => item.Id == AssignmentId);
				listItem.TeacherNames.ShouldBe(["ALLOC001 - Old Teacher", "ALLOC002 - New Teacher"]);
			}
		});
	}

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

	[Fact]
	public async Task List_And_Detail_Should_Keep_Subject_Teacher_Access_When_User_Is_Also_A_Class_Teacher()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			{
				await SeedContextAsync();
				await SeedLessonPlanOwnerAsync();

				var assignmentRepository = GetRequiredService<IRepository<AssignedSubject, Guid>>();
				var assignedSubject = (await assignmentRepository.GetListAsync(
					assignment => assignment.StandardId == StandardId)).Single();
				assignedSubject.Status = SubjectStatus.Published;
				assignedSubject.SetPrimaryTeacher(TeacherEmployeeId);
				await assignmentRepository.UpdateAsync(assignedSubject, autoSave: true);

				var secondDivision = await GetRequiredService<DivisionManager>()
					.CreateAsync(StandardId, "C", 35);
				await GetRequiredService<IRepository<Division, Guid>>()
					.InsertAsync(secondDivision, autoSave: true);
				var otherDivisionSubject = await AddAssignmentAsync("Other Division Subject");
				otherDivisionSubject.Status = SubjectStatus.Published;
				var unrelatedSubject = await AddAssignmentAsync("Unrelated Subject");
				unrelatedSubject.Status = SubjectStatus.Published;
				await assignmentRepository.UpdateManyAsync(
					[otherDivisionSubject, unrelatedSubject], autoSave: true);

				var allocationRepository = GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>();
				var targetAllocation = new TeacherSubjectAllocation(
					Guid.NewGuid(), StandardId, DivisionId, SubjectId);
				targetAllocation.Teachers.Add(new AllocatedTeacher(targetAllocation.Id, TeacherEmployeeId));
				var otherAllocation = new TeacherSubjectAllocation(
					Guid.NewGuid(), StandardId, secondDivision.Id, otherDivisionSubject.SubjectId);
				otherAllocation.Teachers.Add(new AllocatedTeacher(otherAllocation.Id, TeacherEmployeeId));
				await allocationRepository.InsertManyAsync(
					[targetAllocation, otherAllocation], autoSave: true);

				var currentStandard = await GetRequiredService<IRepository<Standard, Guid>>()
					.GetAsync(StandardId);
				var classTeacherStandard = await GetRequiredService<StandardManager>()
					.CreateAsync(currentStandard.EducationMediumStageId, $"Owned Class {Guid.NewGuid():N}", 13, null);
				await GetRequiredService<IRepository<Standard, Guid>>()
					.InsertAsync(classTeacherStandard, autoSave: true);
				var classTeacherDivision = await GetRequiredService<DivisionManager>()
					.CreateAsync(classTeacherStandard.Id, "A", 35);
				await GetRequiredService<IRepository<Division, Guid>>()
					.InsertAsync(classTeacherDivision, autoSave: true);
				await GetRequiredService<IRepository<ClassTeacherEntity, Guid>>().InsertAsync(
					new ClassTeacherEntity(
						Guid.NewGuid(),
						TenantId,
						classTeacherStandard.Id,
						classTeacherDivision.Id,
						TeacherEmployeeId,
						AcademicMediumId),
					autoSave: true);
				await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						TenantId,
						userId: TeacherUserId,
						standardId: classTeacherStandard.Id,
						divisionId: classTeacherDivision.Id),
					autoSave: true);

				using (GetRequiredService<ICurrentPrincipalAccessor>().Change(
					new ClaimsPrincipal(new ClaimsIdentity(
					[
						new Claim(AbpClaimTypes.UserId, TeacherUserId.ToString()),
						new Claim(AbpClaimTypes.Role, "teacher"),
						new Claim(AbpClaimTypes.TenantId, TenantId.ToString())
					],
					"Test"))))
				{
					var result = await Allocations.GetListAsync(
						StandardId,
						0,
						10,
						SubjectStatus.Published,
						divisionId: DivisionId);

					result.Items.Single().Id.ShouldBe(assignedSubject.Id);
					result.Items.ShouldNotContain(item => item.Id == otherDivisionSubject.Id);
					result.Items.ShouldNotContain(item => item.Id == unrelatedSubject.Id);

					var detail = await Allocations.GetAsync(assignedSubject.Id);
					detail.Subjects.Single().Teachers.Single().Id.ShouldBe(TeacherEmployeeId.ToString());

					var denied = await Should.ThrowAsync<BusinessException>(
						() => Allocations.GetAsync(unrelatedSubject.Id));
					denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
				}
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
