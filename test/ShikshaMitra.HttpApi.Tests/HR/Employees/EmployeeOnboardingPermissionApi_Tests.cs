using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EmployeeOnboardingPermissionApi_Tests(ShikshaMitraApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	private const string DefaultMarker = "__employee_onboarding_default_policy_assignment__";
	private const string RoleLookupUrl = "/api/employees/assignable-roles";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[InlineData(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Staff_Writers_Should_Read_Only_Assignable_Tenant_Roles_Without_Identity_Access(string permission)
	{
		var localRole = await InsertRoleAsync(TenantId, true, EmployeeRoleAudience.Employee);
		var foreignRole = await InsertRoleAsync(Guid.NewGuid(), true, EmployeeRoleAudience.Employee);
		var unapprovedRole = await InsertRoleAsync(TenantId, false, EmployeeRoleAudience.Employee);
		var studentRole = await InsertRoleAsync(TenantId, true, EmployeeRoleAudience.Student);
		using var client = CreatePermissionClient(permission);

		var response = await client.GetAsync(RoleLookupUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var roles = await response.Content.ReadFromJsonAsync<ListResultDto<EmployeeAssignableRoleDto>>(JsonOptions);
		roles.ShouldNotBeNull();
		roles.Items.ShouldContain(role => role.Id == localRole);
		roles.Items.ShouldNotContain(role => role.Id == foreignRole || role.Id == unapprovedRole || role.Id == studentRole);
		roles.Items.ShouldAllBe(role => role.RoleAudience == EmployeeRoleAudience.Employee && role.EmployeeOnboardingAssignable);
		using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		json.RootElement.GetProperty("items")[0].TryGetProperty("extraProperties", out _).ShouldBeFalse();
		(await client.GetAsync("/api/identity/roles/all")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Staff_View_Alone_Should_Not_Expose_Assignable_Roles()
	{
		using var client = CreatePermissionClient(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);

		(await client.GetAsync(RoleLookupUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("/api/hr/payroll/policies")]
	[InlineData("/api/hr/attendance/policies")]
	[InlineData("/api/leave-policies")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Staff_Add_Should_Not_Grant_Policy_Administration_Reads(string url)
	{
		using var client = CreatePermissionClient(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add);

		var response = await client.GetAsync($"{url}?academicYearId={Factory.SeededAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Missing_Policy_Actions_Should_Return_Specific_403_Before_Creating_Records(bool assign, bool preview)
	{
		var permissions = new[]
		{
			ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add,
			assign ? ShikshaMitraSubmodulePermissions.HR.Policies.Assign : "",
			preview ? ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact : ""
		};
		using var client = CreatePermissionClient(permissions);
		var input = EmployeeTestHelper.BuildPersonalInfoRequest("PolicyAccessDenied");
		input.PolicyOverride = new PersonalInfoPolicyOverrideRequest
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			AttendancePolicyId = Guid.NewGuid(),
			PayrollPolicyId = Guid.NewGuid(),
			LeavePolicyId = Guid.NewGuid(),
			EffectiveFrom = DateTime.UtcNow.Date,
			ConfirmationToken = DefaultMarker
		};

		using var response = await CreateEmployeeAsync(client, input);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		problem.RootElement.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.EmployeePolicyAccessRequired);
		var missing = problem.RootElement.GetProperty("data").GetProperty("missingPermissions")
			.EnumerateArray().Select(value => value.GetString()).ToArray();
		missing.Contains(ShikshaMitraSubmodulePermissions.HR.Policies.Assign).ShouldBe(!assign);
		missing.Contains(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact).ShouldBe(!preview);
		await AssertNoStaffRecordsAsync(input.Email!);

		input.PolicyOverride = null;
		using var withoutOverride = await CreateEmployeeAsync(client, input);
		withoutOverride.EnsureSuccessStatusCode();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Staff_Create_With_Policy_Actions_Should_Persist_Assignment_Without_Identity_Management()
	{
		using var client = CreatePermissionClient(
			ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add,
			ShikshaMitraSubmodulePermissions.HR.Policies.Assign,
			ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact);
		var input = EmployeeTestHelper.BuildPersonalInfoRequest("PolicyAccessAllowed");
		input.PolicyOverride = await InsertPoliciesAsync();

		using var response = await CreateEmployeeAsync(client, input);

		response.EnsureSuccessStatusCode();
		var created = await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions);
		created.ShouldNotBeNull();
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var assignments = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		(await assignments.AnyAsync(assignment => assignment.TargetKey == created.Id.ToString("D") &&
			assignment.AttendancePolicyId == input.PolicyOverride.AttendancePolicyId &&
			assignment.PayrollPolicyId == input.PolicyOverride.PayrollPolicyId &&
			assignment.LeavePolicyId == input.PolicyOverride.LeavePolicyId)).ShouldBeTrue();
		await uow.CompleteAsync();
	}

	private HttpClient CreatePermissionClient(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, string.Join(",", permissions));
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		return client;
	}

	private static async Task<HttpResponseMessage> CreateEmployeeAsync(HttpClient client, PersonalInfoRequest input)
	{
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(JsonSerializer.Serialize(input, JsonOptions)), "data");
		return await client.PostAsync("/api/employees/personal-info", content);
	}

	private async Task AssertNoStaffRecordsAsync(string email)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var users = scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>();
		var employees = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		(await users.FindByNormalizedEmailAsync(email.ToUpperInvariant())).ShouldBeNull();
		(await employees.AnyAsync(employee => employee.Email == email)).ShouldBeFalse();
		await uow.CompleteAsync();
	}

	private async Task<Guid> InsertRoleAsync(Guid tenantId, bool assignable, string audience)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var roles = scope.ServiceProvider.GetRequiredService<IIdentityRoleRepository>();
		var role = new IdentityRole(Guid.NewGuid(), $"Staff lookup {Guid.NewGuid():N}", tenantId);
		role.SetProperty(EmployeeRoleAudience.ExtraPropertyName, audience);
		role.SetProperty(EmployeeGroupAudience.ExtraPropertyName, EmployeeGroupAudience.Teaching);
		role.SetProperty(EmployeeOnboardingRoleAssignment.ExtraPropertyName, assignable);
		await roles.InsertAsync(role, autoSave: true);
		await uow.CompleteAsync();
		return role.Id;
	}

	private async Task<PersonalInfoPolicyOverrideRequest> InsertPoliciesAsync()
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var year = await scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>()
			.GetAsync(Factory.SeededAcademicYearId);
		var calendar = new WorkCalendarPolicy(Guid.NewGuid(), TenantId, year.Id, $"Staff calendar {Guid.NewGuid():N}",
			HrPolicyStatus.Active, year.StartDate, year.EndDate, AttendanceWeeklyOffPattern.SaturdaySunday, [],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides);
		await scope.ServiceProvider.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>().InsertAsync(calendar, true);
		var payroll = new PayrollPolicy(Guid.NewGuid(), TenantId, year.Id, calendar.Id, $"Staff payroll {Guid.NewGuid():N}",
			HrPolicyStatus.Active, false, DayCalculationType.WorkingDays, null, [], SaturdayWorkPolicy.FullDay, []);
		await scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>().InsertAsync(payroll, true);
		var attendance = new AttendancePolicy(Guid.NewGuid(), TenantId, year.Id, calendar.Id, $"Staff attendance {Guid.NewGuid():N}",
			HrPolicyStatus.Active, false, AttendanceCaptureMode.ManualStatus, 80, 50, 0,
			AttendanceMissingPunchBehavior.RequiresReview, true, 0, 0, AttendancePenaltyType.None, 0,
			false, 0, false, null, null, AttendanceHolidayWorkBehavior.CompOff, true);
		await scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>().InsertAsync(attendance, true);
		var leave = new LeavePolicy(Guid.NewGuid(), TenantId, year.Id, $"Staff leave {Guid.NewGuid():N}",
			year.StartDate, year.EndDate, HrPolicyStatus.Active);
		await scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>().InsertAsync(leave, true);
		await uow.CompleteAsync();
		return new PersonalInfoPolicyOverrideRequest
		{
			AcademicYearId = year.Id,
			AttendancePolicyId = attendance.Id,
			PayrollPolicyId = payroll.Id,
			LeavePolicyId = leave.Id,
			EffectiveFrom = year.StartDate,
			ConfirmationToken = DefaultMarker
		};
	}
}
