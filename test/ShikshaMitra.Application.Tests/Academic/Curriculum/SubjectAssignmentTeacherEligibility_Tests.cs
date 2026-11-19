using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Search;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentTeacherEligibility_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public async Task Update_Should_Validate_Teachers_Against_Stored_Class_Instead_Of_Payload_Class()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var sibling = await GetRequiredService<StandardManager>().CreateAsync(standard.EducationMediumStageId, "Other Class", 1, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(sibling, autoSave: true);
				var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(NewTeacher);
				var scopes = GetRequiredService<IUserScopeAssignmentRepository>();
				await scopes.DeleteAsync(scope => scope.UserId == teacher.UserId, autoSave: true);
				await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, teacher.UserId,
					standardId: sibling.Id), autoSave: true);
				var exception = await Should.ThrowAsync<BusinessException>(() => Allocations.UpdateAsync(AssignmentId,
					AcademicMediumId, new CreateUpdateSubjectAssignmentDto
					{
						ClassId = sibling.Id, Status = SubjectStatus.Published,
						Subjects = [new SubjectAssignmentDetailDto { SubjectId = SubjectId, SubjectCode = "TTTT", TeacherIds = [NewTeacher] }]
					}));
				exception.Code.ShouldBe(CurriculumErrorCodes.SubjectAssignmentTeacherNotInClass);
				exception.Data["ClassName"].ShouldBe("Progress Class");
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Create_Should_Keep_Caller_Class_Scope_Enforcement_Before_Teacher_Eligibility(bool includeTeacher)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var scopes = GetRequiredService<IUserScopeAssignmentRepository>();
				await scopes.DeleteAsync(scope => scope.UserId == ActorId, autoSave: true);
				await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, ActorId,
					standardId: StandardId), autoSave: true);
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var sibling = await GetRequiredService<StandardManager>().CreateAsync(standard.EducationMediumStageId, "Nursery", 1, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(sibling, autoSave: true);
				var exception = await Should.ThrowAsync<BusinessException>(() => Allocations.CreateAsync(AcademicMediumId,
					new CreateUpdateSubjectAssignmentDto
					{
						ClassId = sibling.Id, Status = SubjectStatus.Published,
						Subjects = [new SubjectAssignmentDetailDto
						{
							SubjectId = SubjectId, SubjectCode = "TTTT", TeacherIds = includeTeacher ? [NewTeacher] : []
						}]
					}));
				exception.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
			}
		});
	}

	[Fact]
	public async Task Lookup_Should_Filter_Before_Pagination_And_Keep_Class_Division_And_Parent_Mappings()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
				var sibling = await GetRequiredService<StandardManager>().CreateAsync(standard.EducationMediumStageId, "Nursery", 1, null);
				await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(sibling, autoSave: true);
				var scopes = GetRequiredService<IUserScopeAssignmentRepository>();
				var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(NewTeacher);
				var search = GetRequiredService<IEmployeeSearchAppService>();
				var result = await search.GetFilteredListAsync(new GetEmployeeListInput
				{
					StandardId = sibling.Id, IsTeaching = true, Status = "Active", MaxResultCount = 1
				});
				result.TotalCount.ShouldBe(0);
				result.Items.ShouldBeEmpty();
				(await scopes.GetUserIdsByStandardAsync(StandardId)).ShouldContain(teacher.UserId!.Value);
				await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, teacher.UserId,
					educationMediumId: AcademicMediumId, standardId: StandardId), autoSave: true);
				(await scopes.GetUserIdsByStandardAsync(sibling.Id)).ShouldNotContain(teacher.UserId.Value);
				using (Tenant.Change(Guid.NewGuid()))
				{
					await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), Tenant.Id, teacher.UserId,
						standardId: sibling.Id), autoSave: true);
				}
				(await scopes.GetUserIdsByStandardAsync(sibling.Id)).ShouldNotContain(teacher.UserId.Value);

				var parentGrant = new UserScopeAssignment(Guid.NewGuid(), TenantId, teacher.UserId,
					educationMediumStageId: standard.EducationMediumStageId);
				await scopes.InsertAsync(parentGrant, autoSave: true);
				result = await search.GetFilteredListAsync(new GetEmployeeListInput
				{
					StandardId = sibling.Id, IsTeaching = true, Status = "Active", MaxResultCount = 1
				});
				result.TotalCount.ShouldBe(1);
				result.Items.Single().Id.ShouldBe(NewTeacher);
				parentGrant.Deactivate();
				await scopes.UpdateAsync(parentGrant, autoSave: true);
				(await scopes.GetUserIdsByStandardAsync(sibling.Id)).ShouldNotContain(teacher.UserId.Value);

				await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, teacher.UserId,
					standardId: StandardId, divisionId: DivisionId), autoSave: true);
				(await scopes.GetUserIdsByStandardAsync(StandardId)).ShouldContain(teacher.UserId.Value);
				(await scopes.GetUserIdsByStandardAsync(sibling.Id)).ShouldNotContain(teacher.UserId.Value);
			}
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Save_Should_Reject_Unmapped_Teacher_With_Field_Detail_And_No_Partial_Write(bool update)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using (Tenant.Change(TenantId))
			using (AsAdmin())
			{
				await SeedAllocationAsync();
				var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(NewTeacher);
				var scopes = GetRequiredService<IUserScopeAssignmentRepository>();
				var mapping = (await scopes.GetListAsync(scope => scope.UserId == teacher.UserId)).Single();
				mapping.Deactivate();
				await scopes.UpdateAsync(mapping, autoSave: true);
				var subject = new Subject(Guid.NewGuid(), TenantId, "Computer Science", "#112233", AcademicMediumId);
				await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, autoSave: true);
				var input = new CreateUpdateSubjectAssignmentDto
				{
					ClassId = StandardId, Status = SubjectStatus.Published,
					Subjects = [new SubjectAssignmentDetailDto
					{
						SubjectId = update ? SubjectId : subject.Id, SubjectCode = "TTTT", TeacherIds = [NewTeacher]
					}]
				};
				var exception = await Should.ThrowAsync<BusinessException>(async () =>
				{
					if (update) await Allocations.UpdateAsync(AssignmentId, AcademicMediumId, input);
					else await Allocations.CreateAsync(AcademicMediumId, input);
				});
				exception.Code.ShouldBe(CurriculumErrorCodes.SubjectAssignmentTeacherNotInClass);
				exception.Data["TeacherName"].ShouldBe("New Teacher");
				exception.Data["SubjectId"].ShouldBe(input.Subjects.Single().SubjectId);
				exception.Data["ClassName"].ShouldBe("Progress Class");
				exception.Data["Field"].ShouldBe("teacherIds");
				var assignments = GetRequiredService<IRepository<AssignedSubject, Guid>>();
				(await assignments.CountAsync(assignment => assignment.SubjectId == subject.Id)).ShouldBe(0);
				(await assignments.GetAsync(AssignmentId)).Teachers.ShouldContain(t => t.TeacherId == OldTeacher);
			}
		});
	}
}
