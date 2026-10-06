using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentPortalSiblingIdentity_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Unconfirmed_parent_requires_a_known_initial_setup_request_on_its_exact_verified_link()
	{
		var tenantId = Guid.NewGuid(); using var school = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var user = new IdentityUser(Guid.NewGuid(), "parent@example.test", "parent@example.test", tenantId);
		var studentId = await CreateOwnerFixtureAsync(user);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), tenantId, studentId, user.Id, "guardian", "father", Guid.NewGuid());
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true));
		var service = GetRequiredService<StudentPortalSiblingIdentityService>();
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeFalse();
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), tenantId, Guid.NewGuid(), link.StudentId,
			link.Id, user.Id, "guardian", user.Email, Guid.NewGuid(), DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().InsertAsync(request, true));
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var repo = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			var saved = await repo.GetAsync(request.Id); saved.Cancel("Owner review required"); await repo.UpdateAsync(saved, true);
		});
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeFalse();
	}

	[Fact]
	public async Task Confirmed_parent_still_requires_a_current_verified_child_link_and_matching_tenant()
	{
		var tenantId = Guid.NewGuid(); using var school = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var user = new IdentityUser(Guid.NewGuid(), "verified@example.test", "verified@example.test", tenantId); user.SetEmailConfirmed(true);
		var service = GetRequiredService<StudentPortalSiblingIdentityService>();
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeFalse();
		var studentId = await CreateOwnerFixtureAsync(user);
		var link = new StudentPortalAccountLink(Guid.NewGuid(), tenantId, studentId, user.Id, "guardian", "mother", Guid.NewGuid());
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true));
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeTrue();
		using var otherSchool = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
		(await WithUnitOfWorkAsync(() => service.HasVerifiedGuardianIdentityAsync(user))).ShouldBeFalse();
	}

	private Task<Guid> CreateOwnerFixtureAsync(IdentityUser user) => WithUnitOfWorkAsync(async () =>
	{
		(await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
		var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
			await GetRequiredService<AcademicYearManager>().CreateAsync("Sibling QA", DateTime.Today.AddDays(-30), DateTime.Today.AddDays(300)), true);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
			await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE), true);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
			await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"), true);
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
			await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"), true);
		var student = await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(new Student(Guid.NewGuid(), user.TenantId,
			"Sibling", null, "QA", year.Id, board.Id, medium.Id, stage.Id, status: StudentLifeCycleStatusEnum.Enrolled), true);
		return student.Id;
	});
}
