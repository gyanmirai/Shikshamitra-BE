using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HostTenants;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Collection(InitialSetupTestCollection.Name), Trait("Module", "HR"), Trait("Tier", "Critical")]
public class FirstEmployeeReportingManagerApi_Tests(InitialSetupTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/host/tenants";
	private readonly Guid hostUserId = Guid.NewGuid();
	private const string Password = "New-School-Secret-29!";

	[Fact]
	public async Task First_staff_can_choose_bootstrapped_admin_without_fabricating_an_employee_identity()
	{
		using var host = await HostAsync();
		var tenant = await CreateAsync(host);
		Guid adminId = default, hrRoleId = default, shiftId = default;
		await InTenantAsync(tenant.Id, async services =>
		{
			adminId = (await services.GetRequiredService<IdentityUserManager>().FindByNameAsync("admin"))!.Id;
			hrRoleId = (await services.GetRequiredService<IdentityRoleManager>().FindByNameAsync("HR Manager"))!.Id;
			(await services.GetRequiredService<HR.Employees.IEmployeeRepository>().GetCountAsync()).ShouldBe(0);
			var shift = await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(new ShiftManagement
			{
				TenantId = tenant.Id, ShiftName = "Synthetic first staff shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0)
			}, autoSave: true);
			shiftId = shift.Id;
		});
		using var school = SchoolClient(tenant.Id, adminId);
		var options = (await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>(
			"/api/employees/reporting-manager/options"))!;
		options.Count.ShouldBe(1);
		options[0].Id.ShouldBe(adminId); options[0].Kind.ShouldBe("TenantAdministrator");
		var request = HR.Employees.EmployeeTestHelper.BuildPersonalInfoRequest("FirstSyntheticHR");
		request.RoleId = hrRoleId; request.ShiftId = shiftId; request.IsTeaching = false;
		using var content = new MultipartFormDataContent();
		content.Add(new StringContent(JsonSerializer.Serialize(request, JsonOptions)), "data");
		var created = await school.PostAsync("/api/employees/personal-info", content);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var staff = (await created.Content.ReadFromJsonAsync<HR.Employees.PersonalInfo.PersonalInfoResponse>(JsonOptions))!;
		staff.Id.ShouldNotBe(adminId);
		var url = $"/api/employees/{staff.Id}/reporting-manager";
		foreach (var missing in new[] { "", Guid.Empty.ToString() })
			(await school.PutAsJsonAsync(url, new { reportingManagerId = missing, reportingManagerKind = "TenantAdministrator" })).StatusCode
				.ShouldBe(HttpStatusCode.BadRequest);
		// An identity ID cannot be passed off as an employee ID, even for the administrator.
		(await school.PutAsJsonAsync(url, new { reportingManagerId = adminId })).IsSuccessStatusCode.ShouldBeFalse();
		var saved = await school.PutAsJsonAsync(url, new { reportingManagerId = adminId, reportingManagerKind = "TenantAdministrator" });
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var reloaded = (await school.GetFromJsonAsync<HR.Employees.ReportingManagerInfo.ReportingManagerResponse>(url))!;
		reloaded.ReportingManager.ShouldNotBeNull();
		reloaded.ReportingManager.Id.ShouldBe(adminId); reloaded.ReportingManager.Kind.ShouldBe("TenantAdministrator");
		await InTenantAsync(tenant.Id, async services =>
		{
			var repository = services.GetRequiredService<HR.Employees.IEmployeeRepository>();
			(await repository.GetCountAsync()).ShouldBe(1);
			var persisted = await repository.GetAsync(staff.Id, includeDetails: false);
			persisted.ReportingManagerId.ShouldBeNull(); persisted.ReportingManagerUserId.ShouldBe(adminId);
			persisted.UserId.ShouldBe(staff.UserId);
			var users = services.GetRequiredService<IdentityUserManager>();
			var admin = (await users.FindByIdAsync(adminId.ToString()))!;
			(await users.GetRolesAsync(admin)).ShouldBe(new[] { "admin" });
		});
		var otherTenant = await CreateAsync(host);
		Guid foreignAdminId = default;
		await InTenantAsync(otherTenant.Id, async services =>
			foreignAdminId = (await services.GetRequiredService<IdentityUserManager>().FindByNameAsync("admin"))!.Id);
		options = (await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>(
			"/api/employees/reporting-manager/options"))!;
		options.ShouldNotContain(option => option.Id == foreignAdminId || option.Id == staff.Id);
		(await school.PutAsJsonAsync(url, new { reportingManagerId = foreignAdminId, reportingManagerKind = "TenantAdministrator" }))
			.IsSuccessStatusCode.ShouldBeFalse();
		(await school.GetFromJsonAsync<HR.Employees.ReportingManagerInfo.ReportingManagerResponse>(url))!
			.ReportingManager!.Id.ShouldBe(adminId);
		using var denied = SchoolClient(tenant.Id, adminId);
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.GetAsync("/api/employees/reporting-manager/options")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await denied.PutAsJsonAsync(url, new { reportingManagerId = adminId, reportingManagerKind = "TenantAdministrator" }))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Administrator_candidates_require_an_active_unlocked_account()
	{
		using var host = await HostAsync(); var tenant = await CreateAsync(host); Guid adminId = default;
		await InTenantAsync(tenant.Id, async services =>
			adminId = (await services.GetRequiredService<IdentityUserManager>().FindByNameAsync("admin"))!.Id);
		using var school = SchoolClient(tenant.Id, adminId);
		await InTenantAsync(tenant.Id, async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>(); var admin = (await users.FindByNameAsync("admin"))!;
			(await users.SetLockoutEnabledAsync(admin, true)).Succeeded.ShouldBeTrue();
			(await users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddHours(1))).Succeeded.ShouldBeTrue();
		});
		(await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>("/api/employees/reporting-manager/options"))!
			.ShouldNotContain(option => option.Id == adminId);
		await InTenantAsync(tenant.Id, async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>(); var admin = (await users.FindByNameAsync("admin"))!;
			(await users.SetLockoutEndDateAsync(admin, null)).Succeeded.ShouldBeTrue();
			admin.SetIsActive(false); (await users.UpdateAsync(admin)).Succeeded.ShouldBeTrue();
		});
		(await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>("/api/employees/reporting-manager/options"))!
			.ShouldNotContain(option => option.Id == adminId);
		await InTenantAsync(tenant.Id, async services =>
		{
			var users = services.GetRequiredService<IdentityUserManager>(); var admin = (await users.FindByNameAsync("admin"))!;
			admin.SetIsActive(true); (await users.UpdateAsync(admin)).Succeeded.ShouldBeTrue();
		});
		(await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>("/api/employees/reporting-manager/options"))!
			.ShouldContain(option => option.Id == adminId);
	}

	[Fact]
	public async Task Existing_linked_admin_uses_employee_id_and_preserves_status_self_and_cycle_rules()
	{
		using var host = await HostAsync(); var tenant = await CreateAsync(host);
		Guid adminId = default, adminEmployeeId = Guid.NewGuid(), hrRoleId = default, shiftId = default;
		await InTenantAsync(tenant.Id, async services =>
		{
			var admin = (await services.GetRequiredService<IdentityUserManager>().FindByNameAsync("admin"))!;
			adminId = admin.Id;
			hrRoleId = (await services.GetRequiredService<IdentityRoleManager>().FindByNameAsync("HR Manager"))!.Id;
			var adminRole = (await services.GetRequiredService<IdentityRoleManager>().FindByNameAsync("admin"))!;
			var shift = await services.GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(new ShiftManagement
			{ TenantId = tenant.Id, ShiftName = "Synthetic existing staff shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0) }, autoSave: true);
			shiftId = shift.Id;
			// Persist a synthetic legacy profile with different employee and login IDs.
			// No production creation path or identity memberships are changed.
			var employee = (HR.Employees.Employee)Activator.CreateInstance(typeof(HR.Employees.Employee), nonPublic: true)!;
			void Set(string name, object value) => typeof(HR.Employees.Employee).GetProperty(name)!.SetValue(employee, value);
			Set("Id", adminEmployeeId); Set("TenantId", tenant.Id); Set("UserId", adminId); Set("RoleId", adminRole.Id);
			Set("EmployeeCode", "EXISTING-ADMIN"); Set("FirstName", "Existing"); Set("LastName", "Administrator");
			Set("PanNo", "ABCDE1234F"); Set("AdharNo", "999999999999");
			Set("EmployeeStatusType", Common.EmployeeStatusType.Active);
			await services.GetRequiredService<HR.Employees.IEmployeeRepository>().InsertAsync(employee, autoSave: true);
		});
		using var school = SchoolClient(tenant.Id, adminId);
		var options = (await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>("/api/employees/reporting-manager/options"))!;
		options.ShouldContain(option => option.Id == adminEmployeeId && option.Kind == "Employee");
		options.ShouldNotContain(option => option.Id == adminId);
		var request = HR.Employees.EmployeeTestHelper.BuildPersonalInfoRequest("ExistingTenantHR");
		request.RoleId = hrRoleId; request.ShiftId = shiftId; request.IsTeaching = false;
		using var content = new MultipartFormDataContent(); content.Add(new StringContent(JsonSerializer.Serialize(request, JsonOptions)), "data");
		var created = await school.PostAsync("/api/employees/personal-info", content);
		created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
		var staff = (await created.Content.ReadFromJsonAsync<HR.Employees.PersonalInfo.PersonalInfoResponse>(JsonOptions))!;
		var url = $"/api/employees/{staff.Id}/reporting-manager";
		(await school.PutAsJsonAsync(url, new { reportingManagerId = adminEmployeeId })).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await school.PutAsJsonAsync(url, new { reportingManagerId = adminId, reportingManagerKind = "TenantAdministrator" })).IsSuccessStatusCode.ShouldBeFalse();
		await InTenantAsync(tenant.Id, async services =>
		{
			var repository = services.GetRequiredService<HR.Employees.IEmployeeRepository>();
			var employee = await repository.GetAsync(staff.Id, includeDetails: false);
			employee.ReportingManagerId.ShouldBe(adminEmployeeId); employee.ReportingManagerUserId.ShouldBeNull();
			await services.GetRequiredService<HR.Employees.EmployeeManager>().UpdateEmployeeStatusAsync(staff.Id, Common.EmployeeStatusType.Active, "Synthetic fixture");
		});
		(await school.PutAsJsonAsync(url, new { reportingManagerId = staff.Id })).IsSuccessStatusCode.ShouldBeFalse();
		(await school.PutAsJsonAsync($"/api/employees/{adminEmployeeId}/reporting-manager", new { reportingManagerId = staff.Id })).IsSuccessStatusCode.ShouldBeFalse();
		await InTenantAsync(tenant.Id, async services =>
			await services.GetRequiredService<HR.Employees.EmployeeManager>().UpdateEmployeeStatusAsync(adminEmployeeId, Common.EmployeeStatusType.Inactive, "Synthetic fixture"));
		options = (await school.GetFromJsonAsync<List<HR.Employees.ReportingManager.EmployeeBasicDto>>("/api/employees/reporting-manager/options"))!;
		options.ShouldNotContain(option => option.Id == adminEmployeeId || option.Id == adminId);
		(await school.PutAsJsonAsync(url, new { reportingManagerId = adminEmployeeId })).IsSuccessStatusCode.ShouldBeFalse();
		var reloaded = (await school.GetFromJsonAsync<HR.Employees.ReportingManagerInfo.ReportingManagerResponse>(url))!;
		reloaded.ReportingManager!.Id.ShouldBe(adminEmployeeId); reloaded.ReportingManager.Kind.ShouldBe("Employee");
	}

	private HttpClient SchoolClient(Guid tenantId, Guid userId)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		return client;
	}

	private async Task<HttpClient> HostAsync()
	{
		await InTenantAsync(null, async services =>
		{
			var repository = services.GetRequiredService<IIdentityUserRepository>();
			if (await repository.FindAsync(hostUserId) == null)
				await repository.InsertAsync(new IdentityUser(hostUserId, "host" + hostUserId.ToString("N"), hostUserId + "@example.test"));
		});
		return HostClient();
	}
	private HttpClient HostClient(string? grants = null)
	{
		var client = Factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, "host");
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, hostUserId.ToString());
		if (grants != null) client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grants);
		return client;
	}
	private async Task InTenantAsync(Guid? tenantId, Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider); await unit.CompleteAsync();
	}
	private static CreateHostTenantInput Input(string code) => new() { Name = code, InstituteName = "Synthetic Host School", Email = code + "@example.test",
		ContactNumber = "9876543210", Address1 = "Synthetic Test Road", City = "Surat", State = "Gujarat", Country = "India", PostalCode = "395001",
		AdminEmailAddress = "admin@" + code + ".example.test", AdminPassword = Password };
	private async Task<HostTenantDto> CreateAsync(HttpClient host, string? code = null)
	{
		var response = await host.PostAsJsonAsync(Url, Input(code ?? "host" + Guid.NewGuid().ToString("N")));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<HostTenantDto>())!;
	}
}
