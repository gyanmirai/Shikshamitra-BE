using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceScopeAccessService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private readonly FinanceScopeAccessService _access;
	private readonly ICurrentTenant _tenant;
	private readonly ICurrentPrincipalAccessor _principal;
	private readonly IRepository<AcademicYear, Guid> _years;
	private readonly IRepository<IdentityUser, Guid> _users;
	private readonly IRepository<IdentityRole, Guid> _roles;

	public FinanceScopeAccessService_Tests()
	{
		_access = GetRequiredService<FinanceScopeAccessService>();
		_tenant = GetRequiredService<ICurrentTenant>();
		_principal = GetRequiredService<ICurrentPrincipalAccessor>();
		_years = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_users = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_roles = GetRequiredService<IRepository<IdentityRole, Guid>>();
	}

	[Fact]
	public async Task Database_administrator_can_configure_a_valid_year_before_its_first_student()
	{
		await RunAsync(true, async yearId =>
		{
			await _access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null);
			(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, null, null)).ShouldBeTrue();
			// The setup exception does not turn an unknown student into an accessible one.
			await Should.ThrowAsync<AbpAuthorizationException>(() => _access.EnsureStudentAccessibleAsync(Guid.NewGuid()));
		});
	}

	[Fact]
	public Task An_administrator_claim_without_database_membership_does_not_grant_setup_access() =>
		RunAsync(false, AssertYearDeniedAsync);

	[Fact]
	public Task Missing_and_foreign_tenant_years_remain_inaccessible() =>
		RunAsync(true, async _ =>
		{
			await AssertYearDeniedAsync(Guid.NewGuid());
			var foreignYear = Guid.NewGuid();
			using (_tenant.Change(Guid.NewGuid()))
			{
				await _years.InsertAsync(new AcademicYear(foreignYear, _tenant.Id, "Foreign QA year",
					new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			}
			await AssertYearDeniedAsync(foreignYear);
		});

	[Fact]
	public Task Class_context_still_requires_a_scoped_student() =>
		RunAsync(true, async yearId =>
		{
			var classId = Guid.NewGuid();
			(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, classId, null)).ShouldBeFalse();
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, classId, null));
		});

	[Fact]
	public Task Missing_identity_remains_denied() =>
		RunAsync(true, async yearId =>
		{
			using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			{
				await Should.ThrowAsync<AbpAuthorizationException>(() =>
					_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null));
			}
		});

	[Fact]
	public Task Work_context_reports_database_admin_membership_without_exposing_students() =>
		RunAsync(true, async yearId =>
		{
			var context = await GetRequiredService<IFinanceWorkContextAppService>().GetAsync(yearId);
			context.AcademicYearId.ShouldBe(yearId);
			context.CanViewAllTenant.ShouldBeTrue();
			context.ScopeOptions.ShouldBeEmpty();
		});

	[Fact]
	public Task Reviewer_must_never_decide_own_request_even_if_database_admin() =>
		RunAsync(true, async yearId =>
		{
			var actorId = GetRequiredService<Volo.Abp.Users.ICurrentUser>().Id!.Value;
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				GetRequiredService<FinanceReviewerAccessService>().EnsureReviewerCanDecideAsync(
					actorId, null, Guid.NewGuid(), yearId,
					ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
					ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve));
		});

	[Fact]
	public Task Inactive_database_admin_cannot_resolve_request_students() =>
		RunAsync(true, async yearId =>
		{
			var actorId = GetRequiredService<Volo.Abp.Users.ICurrentUser>().Id!.Value;
			var user = await _users.GetAsync(actorId);
			user.SetIsActive(false);
			await _users.UpdateAsync(user, true);
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				_access.GetRequestScopedStudentsQueryForUserAsync(actorId, yearId));
			await Should.ThrowAsync<AbpAuthorizationException>(() =>
				GetRequiredService<IFinanceWorkContextAppService>().GetAsync(yearId));
		});

	[Fact]
	public Task Family_maker_without_employee_uses_open_checker_route_while_missing_maker_needs_admin() =>
		RunAsync(false, async yearId =>
		{
			var actorId = GetRequiredService<Volo.Abp.Users.ICurrentUser>().Id!.Value;
			var reviewers = GetRequiredService<FinanceReviewerAccessService>();
			var view = ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View;
			var decision = ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Approve;
			(await reviewers.FindEligibleReviewerAsync(actorId, Guid.NewGuid(), yearId, view, decision))
				.ShouldBe(Guid.Empty);
			(await reviewers.FindEligibleReviewerAsync(Guid.NewGuid(), Guid.NewGuid(), yearId, view, decision))
				.ShouldBeNull();
		});

	[Fact]
	public async Task Class_teacher_request_and_generic_scope_are_capped_but_reviewer_scope_uses_effective_grant()
	{
		using (_tenant.Change(TestTenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var userId = Guid.NewGuid();
				await _users.InsertAsync(new IdentityUser(userId, $"teacher-{userId:N}", $"{userId:N}@example.test", TestTenantId), true);
				var year = await _years.InsertAsync(new AcademicYear(Guid.NewGuid(), TestTenantId,
					$"Finance-{userId:N}"[..20], new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
				var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
					await GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE), true);
				var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
					await GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "English"), true);
				var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
					await GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Primary"), true);
				var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
					await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Class 8", 12, null), true);
				var assigned = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 30), true);
				var other = await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
					await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "B", 30), true);
				var role = await _roles.InsertAsync(new IdentityRole(Guid.NewGuid(), $"teacher-{userId:N}", TestTenantId), true);
				var suffix = userId.ToString("N");
				var employee = await GetRequiredService<IRepository<Employee, Guid>>().InsertAsync(
					new Employee(Guid.NewGuid(), TestTenantId, userId, "TE" + suffix,
						new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Teacher", FirstName: "Finance", MiddleName: null,
							ShortName: null, EmployeeStatusType: EmployeeStatusType.Active, DateOfBirth: null, FamilyCast: null,
							CastType: null, GenderType: null, Religion: null, MaritalStatus: null, BloodGroup: null,
							MobileNo: null, SecondaryMobileNo: null, Email: suffix + "@example.test",
							PanNo: suffix[..10].ToUpperInvariant(), AdharNo: suffix[10..22], Address1: null, Address2: null,
							Address3: null, Department: null, Designation: "Teacher", DateOfJoining: null,
							EmployeeGroup: EmployeeGroupType.Teaching, Qualification: null, RoleId: role.Id,
							IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null), null, null), true);
				await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
					new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: userId,
						academicYearId: year.Id, description: "Broad grant for cap test"), true);
				await GetRequiredService<IRepository<ClassTeacher, Guid>>().InsertAsync(
					new ClassTeacher(Guid.NewGuid(), TestTenantId, standard.Id, assigned.Id, employee.Id,
						medium.Id, year.Id, board.Id, medium.Id, stage.Id), true);
				var students = GetRequiredService<IRepository<Student, Guid>>();
				var allowed = await students.InsertAsync(new Student(Guid.NewGuid(), TestTenantId,
					"Allowed", null, "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, assigned.Id), true);
				var otherStudent = await students.InsertAsync(new Student(Guid.NewGuid(), TestTenantId,
					"Denied", null, "Student", year.Id, board.Id, medium.Id, stage.Id, standard.Id, other.Id), true);

				var result = (await _access.GetRequestScopedStudentsQueryForUserAsync(userId, year.Id))
					.Select(student => student.Id).ToArray();
				result.ShouldBe(new[] { allowed.Id });
				(await _access.GetRequestScopedStudentsQueryForUserAsync(
					userId, year.Id, capTeacherAssignments: false))
					.Select(student => student.Id).ToArray().OrderBy(id => id)
					.ShouldBe(new[] { allowed.Id, otherStudent.Id }.OrderBy(id => id));
				using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity([
					new Claim(AbpClaimTypes.UserId, userId.ToString()),
					new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())], "Test"))))
				{
					(await _access.GetScopedStudentIdsQueryAsync()).ToArray().ShouldBe(new[] { allowed.Id });
				}
			});
		}
	}

	private async Task AssertYearDeniedAsync(Guid yearId)
	{
		(await _access.IsAcademicContextAccessibleAsync(yearId, null, null, null, null)).ShouldBeFalse();
		await Should.ThrowAsync<AbpAuthorizationException>(() =>
			_access.EnsureAcademicContextAccessibleAsync(yearId, null, null, null, null));
	}

	private async Task RunAsync(bool databaseAdministrator, Func<Guid, Task> assertions)
	{
		using (_tenant.Change(TestTenantId))
		{
			var userId = Guid.NewGuid();
			using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity([
				new Claim(AbpClaimTypes.UserId, userId.ToString()),
				new Claim(AbpClaimTypes.UserName, $"finance-qa-{userId:N}"),
				new Claim(AbpClaimTypes.Role, "admin"),
				new Claim(AbpClaimTypes.TenantId, TestTenantId.ToString())
			], "Test"))))
			{
				await WithUnitOfWorkAsync(async () =>
				{
					var user = new IdentityUser(userId, $"finance-qa-{userId:N}", $"{userId:N}@example.test", TestTenantId);
					if (databaseAdministrator)
					{
						var role = (await _roles.GetListAsync(item => item.Name == "admin")).FirstOrDefault();
						if (role is null)
							role = await _roles.InsertAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId), true);
						user.AddRole(role.Id);
					}
					await _users.InsertAsync(user, true);
					var year = await _years.InsertAsync(new AcademicYear(Guid.NewGuid(), TestTenantId, $"QA-{userId:N}"[..20],
						new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
					await assertions(year.Id);
				});
			}
		}
	}
}
