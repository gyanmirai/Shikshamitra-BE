using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentHierarchyAccess_Tests : LessonPlanProgressTestBase
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public Task Scoped_Class_Creation_And_Read_Must_Use_Stored_Hierarchy_Without_Granting_Other_Classes(bool stageScope, bool classTeacherBranch) => WithUnitOfWorkAsync(async () =>
	{
		using var tenant = Tenant.Change(TenantId); await SeedContextAsync();
		var existing = await GetRequiredService<IRepository<Standard, Guid>>().GetAsync(StandardId);
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(existing.EducationMediumStageId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(stage.EducationMediumId);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
		var actor = new IdentityUser(Guid.NewGuid(), $"scope-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.local", TenantId);
		await GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(actor, true);
		await GetRequiredService<IUserScopeAssignmentRepository>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, actor.Id,
			academicYearId: board.AcademicYearId, educationBoardId: board.Id, educationMediumId: medium.Id,
			educationMediumStageId: stageScope ? stage.Id : null), true);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, actor.Id.ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "test")));
		var target = await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "LOCAL QA Scoped Class", 14, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(target, true);
		var subject = new Subject(Guid.NewGuid(), TenantId, "LOCAL QA Scoped Subject", "#112233", medium.Id);
		await GetRequiredService<IRepository<Subject, Guid>>().InsertAsync(subject, true);
		var classTeachers = Substitute.For<IClassTeacherAccessService>(); classTeachers.HasAssignmentAsync(actor.Id).Returns(classTeacherBranch);
		var service = ActivatorUtilities.CreateInstance<SubjectAssignmentAppService>(ServiceProvider, classTeachers);
		service.LazyServiceProvider = new AbpLazyServiceProvider(ServiceProvider);
		CreateUpdateSubjectAssignmentDto Input(Guid standard) => new() { ClassId = standard, Status = SubjectStatus.Draft,
			Subjects = [new() { SubjectId = subject.Id, SubjectCode = "LOCAL-QA", TeacherIds = [] }] };
		await service.CreateAsync(medium.Id, Input(target.Id));
		var saved = (await GetRequiredService<IRepository<AssignedSubject, Guid>>().GetListAsync(x => x.StandardId == target.Id)).Single();
		(await service.GetAsync(saved.Id)).Id.ShouldBe(saved.Id);
		(await service.GetListAsync(target.Id, 0, 10, SubjectStatus.Draft)).TotalCount.ShouldBe(1);
		var outsideMedium = await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Outside scope");
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(outsideMedium, true);
		var outsideStage = await GetRequiredService<EducationMediumStageManager>().CreateAsync(outsideMedium.Id, "Outside stage");
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(outsideStage, true);
		var outside = await GetRequiredService<StandardManager>().CreateAsync(outsideStage.Id, "Outside class", 14, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(outside, true);
		(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(outsideMedium.Id, Input(outside.Id)))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		var foreign = new Standard(Guid.NewGuid(), Guid.NewGuid(), "Foreign class", 14, stage.Id, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(foreign, true);
		(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(medium.Id, Input(foreign.Id)))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	});
}

public class SubjectAssignmentTeacherHierarchy_Tests : SubjectTeacherReassignmentTestBase
{
	[Fact]
	public Task Subject_Ownership_Must_Remain_Additive_For_Reads_Without_Authorizing_Mutations() => WithUnitOfWorkAsync(async () =>
	{
		using var tenant = Tenant.Change(TenantId); await SeedAllocationAsync();
		var teacher = await GetRequiredService<IRepository<Employee, Guid>>().GetAsync(OldTeacher);
		await GetRequiredService<IUserScopeAssignmentRepository>().DeleteAsync(x => x.UserId == teacher.UserId, true);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, teacher.UserId!.Value.ToString()), new Claim(AbpClaimTypes.TenantId, TenantId.ToString())], "test")));
		(await Allocations.GetAsync(AssignmentId)).Id.ShouldBe(AssignmentId);
		(await Allocations.GetListAsync(StandardId, 0, 10, SubjectStatus.Draft)).TotalCount.ShouldBe(1);
		(await Should.ThrowAsync<BusinessException>(() => Allocations.DeleteAsync(AssignmentId))).Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		(await GetRequiredService<IRepository<AssignedSubject, Guid>>().FindAsync(AssignmentId)).ShouldNotBeNull();
	});
}
