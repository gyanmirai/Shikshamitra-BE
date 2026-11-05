using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceReviewerAccessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Direct_manager_can_review_but_unrelated_checker_cannot_and_family_maker_is_open()
	{
		var tenantId = Guid.NewGuid();
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		{
			var fixture = await WithUnitOfWorkAsync(async () =>
			{
				var year = await Insert(new AcademicYear(Guid.NewGuid(), tenantId, "Finance reviewer year",
					new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)));
				var board = await Insert(await GetRequiredService<EducationBoardManager>()
					.CreateAsync(year.Id, EducationBoardType.CBSE));
				var medium = await Insert(await GetRequiredService<EducationMediumManager>()
					.CreateAsync(board.Id, "English"));
				var stage = await Insert(await GetRequiredService<EducationMediumStageManager>()
					.CreateAsync(medium.Id, "Primary"));
				var standard = await Insert(await GetRequiredService<StandardManager>()
					.CreateAsync(stage.Id, "Class 8", 12, null));
				var division = await Insert(await GetRequiredService<DivisionManager>()
					.CreateAsync(standard.Id, "A", 30));
				var student = await Insert(new Student(Guid.NewGuid(), tenantId, "Scoped", null, "Student",
					year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id));

				var maker = await CreateUserAsync("maker", tenantId);
				var manager = await CreateUserAsync("manager", tenantId);
				var unrelated = await CreateUserAsync("unrelated", tenantId);
				var administrator = await CreateUserAsync("administrator", tenantId);
				var adminRoleName = GetRequiredService<IOptions<UserScopeOptions>>().Value.AdminRoles[0];
				var adminRole = await Insert(new IdentityRole(Guid.NewGuid(), adminRoleName, tenantId));
				administrator.User.AddRole(adminRole.Id);
				await GetRequiredService<IRepository<IdentityUser, Guid>>().UpdateAsync(
					administrator.User, true);
				var family = await CreateUserAsync("family", tenantId);
				var makerEmployee = await CreateEmployeeAsync(maker.User.Id, maker.Role.Id, tenantId, EmployeeGroupType.Teaching);
				var managerEmployee = await CreateEmployeeAsync(manager.User.Id, manager.Role.Id, tenantId, EmployeeGroupType.NonTeaching);
				await CreateEmployeeAsync(unrelated.User.Id, unrelated.Role.Id, tenantId, EmployeeGroupType.NonTeaching);
				makerEmployee.AssignReportingManager(managerEmployee.Id);
				await GetRequiredService<IRepository<Employee, Guid>>().UpdateAsync(makerEmployee, true);
				await Insert(new ClassTeacher(Guid.NewGuid(), tenantId, standard.Id, division.Id, makerEmployee.Id,
					medium.Id, year.Id, board.Id, medium.Id, stage.Id));
				foreach (var userId in new[] { maker.User.Id, manager.User.Id, unrelated.User.Id })
					await Insert(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId,
						academicYearId: year.Id, description: "Reviewer test scope"));
				var permissions = GetRequiredService<IPermissionManager>();
				foreach (var roleName in new[] { manager.Role.Name, unrelated.Role.Name,
					administrator.Role.Name })
				{
					await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
						"R", roleName, true);
					await permissions.SetAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
						"R", roleName, true);
				}
				return new ReviewerFixture(year.Id, student.Id, maker.User.Id, manager.User.Id,
					unrelated.User.Id, administrator.User.Id, family.User.Id);
			});

			await WithUnitOfWorkAsync(async () =>
			{
				var access = GetRequiredService<FinanceReviewerAccessService>();
				var view = ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View;
				var approve = ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve;
				var route = await access.FindEligibleReviewerAsync(fixture.MakerId, fixture.StudentId,
					fixture.YearId, view, approve);
				route.ShouldBe(fixture.ManagerId);
				using (SignIn(fixture.ManagerId, tenantId))
					await access.EnsureReviewerCanDecideAsync(fixture.MakerId, route,
						fixture.StudentId, fixture.YearId, view, approve);
				using (SignIn(fixture.UnrelatedId, tenantId))
					await Should.ThrowAsync<AbpAuthorizationException>(() => access.EnsureReviewerCanDecideAsync(
						fixture.MakerId, route, fixture.StudentId, fixture.YearId, view, approve));
				using (SignIn(fixture.AdministratorId, tenantId))
					await access.EnsureReviewerCanDecideAsync(fixture.MakerId, route,
						fixture.StudentId, fixture.YearId, view, approve);

				var familyRoute = await access.FindEligibleReviewerAsync(fixture.FamilyId,
					fixture.StudentId, fixture.YearId, view, approve);
				familyRoute.ShouldBe(Guid.Empty);
				using (SignIn(fixture.UnrelatedId, tenantId))
					await access.EnsureReviewerCanDecideAsync(fixture.FamilyId, familyRoute,
						fixture.StudentId, fixture.YearId, view, approve);
			});
		}
	}

	private async Task<T> Insert<T>(T entity) where T : class, IEntity<Guid> =>
		await GetRequiredService<IRepository<T, Guid>>().InsertAsync(entity, true);

	private async Task<(IdentityUser User, IdentityRole Role)> CreateUserAsync(string label, Guid tenantId)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var role = await Insert(new IdentityRole(Guid.NewGuid(), $"finance-{label}-{suffix}", tenantId));
		var user = new IdentityUser(Guid.NewGuid(), $"finance-{label}-{suffix}",
			$"{suffix}@example.test", tenantId);
		user.AddRole(role.Id);
		return (await Insert(user), role);
	}

	private async Task<Employee> CreateEmployeeAsync(Guid userId, Guid roleId, Guid tenantId, EmployeeGroupType group)
	{
		var suffix = Guid.NewGuid().ToString("N");
		return await Insert(new Employee(Guid.NewGuid(), tenantId, userId, "FE" + suffix,
			new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Reviewer", FirstName: "Finance",
				MiddleName: null, ShortName: null, EmployeeStatusType: EmployeeStatusType.Active,
				DateOfBirth: null, FamilyCast: null, CastType: null, GenderType: null, Religion: null,
				MaritalStatus: null, BloodGroup: null, MobileNo: null, SecondaryMobileNo: null,
				Email: $"{suffix}@example.test", PanNo: suffix[..10].ToUpperInvariant(),
				AdharNo: suffix[10..22], Address1: null, Address2: null, Address3: null,
				Department: null, Designation: "Reviewer", DateOfJoining: null, EmployeeGroup: group,
				Qualification: null, RoleId: roleId, IdentificationMark1: null, City: null,
				State: null, Country: null, PinCode: null, ShiftId: null), null, null));
	}

	private IDisposable SignIn(Guid userId, Guid tenantId) =>
		GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "FinanceReviewerTest")));

	private sealed record ReviewerFixture(Guid YearId, Guid StudentId, Guid MakerId, Guid ManagerId,
		Guid UnrelatedId, Guid AdministratorId, Guid FamilyId);
}
