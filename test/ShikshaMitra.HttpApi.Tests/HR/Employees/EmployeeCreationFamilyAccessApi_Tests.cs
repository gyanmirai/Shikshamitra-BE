using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.FamilyInfo;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
[TestTier(TestTraits.Tiers.Critical)]
public class EmployeeCreationFamilyAccessApi_Tests(ShikshaMitraApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory)
{
	private const string StaffAdd = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add;
	private const string StaffEdit = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit;
	private const string StaffView = ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View;

	[Fact]
	public async Task Scoped_Teacher_Can_Save_And_Read_Own_New_Staff_Family_Before_Academic_Assignment()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var url = $"/api/employees/{employeeId}/family-info";

		var save = await creator.PutAsJsonAsync(url, FamilyRequest(), JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.NoContent, await save.Content.ReadAsStringAsync());
		var read = await creator.GetAsync(url);
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		var family = await read.Content.ReadFromJsonAsync<FamilyInfoResponse>(JsonOptions);
		family!.FatherDetails!.Name.ShouldBe("Synthetic Draft Father");
	}

	[Fact]
	public async Task Other_Scoped_Teacher_Cannot_Read_Or_Edit_Someone_Elses_Unassigned_Staff()
	{
		using var creator = await CreateScopedTeacherAsync();
		using var other = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var url = $"/api/employees/{employeeId}/family-info";

		(await other.PutAsJsonAsync(url, FamilyRequest(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await other.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData(StaffAdd)]
	[InlineData(StaffEdit)]
	public async Task Removing_Add_Or_Edit_Blocks_Creator_Draft_Write(string removedPermission)
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		creator.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		creator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			$"{StaffView},{(removedPermission == StaffAdd ? StaffEdit : StaffAdd)}");

		(await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Missing_Organization_Scope_Does_Not_Grant_Creator_Access()
	{
		using var creator = await CreateScopedTeacherAsync(withScope: false);
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		(await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task In_Scope_Assigned_Staff_Remains_Editable_Without_Add()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		await AssignStageAsync(employeeId, Factory.SeededMediumStageId);
		creator.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		creator.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{StaffView},{StaffEdit}");

		var response = await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Creator_Cannot_Read_Or_Write_Draft_Through_Another_Tenant()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		creator.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		var url = $"/api/employees/{employeeId}/family-info";

		(await creator.PutAsJsonAsync(url, FamilyRequest(), JsonOptions)).StatusCode
			.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
		(await creator.GetAsync(url)).StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Removing_Organization_Assignment_Does_Not_Reopen_Creator_Draft_Access()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		await AssignStageAsync(employeeId, Factory.SeededMediumStageId);
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
				.DeleteAsync(x => x.EmployeeId == employeeId, autoSave: true);
			await unit.CompleteAsync();
		}

		(await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Scope_Revocation_Blocks_Previously_Accessible_Draft_On_Next_Request()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		var url = $"/api/employees/{employeeId}/family-info";
		(await creator.PutAsJsonAsync(url, FamilyRequest(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var userId = Guid.Parse(creator.DefaultRequestHeaders.GetValues(TestAuthHandler.TestUserIdHeader).Single());
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			var assignment = (await repository.GetQueryableAsync()).Single(x => x.UserId == userId);
			await repository.UpdateAsync(assignment.Deactivate(), autoSave: true);
			await unit.CompleteAsync();
		}
		(await creator.PutAsJsonAsync(url, FamilyRequest(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await creator.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Active_Unassigned_Staff_Does_Not_Use_Creator_Draft_Access()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, employeeId);

		(await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Assigned_Out_Of_Scope_Staff_Does_Not_Use_Creator_Draft_Access()
	{
		using var creator = await CreateScopedTeacherAsync();
		var employeeId = await CreateUnassignedEmployeeAsync(creator);
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var otherStageId = (await scope.ServiceProvider.GetRequiredService<IRepository<EducationMediumStage, Guid>>()
			.GetQueryableAsync()).Where(x => x.Id != Factory.SeededMediumStageId).Select(x => x.Id).First();
		await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
			.InsertAsync(new EmployeeOrganizationAssignment(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), employeeId, otherStageId), autoSave: true);
		await unit.CompleteAsync();

		(await creator.PutAsJsonAsync($"/api/employees/{employeeId}/family-info", FamilyRequest(), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<HttpClient> CreateScopedTeacherAsync(bool withScope = true)
	{
		using var scope = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var id = Guid.NewGuid();
		var name = "draft-creator-" + id.ToString("N");
		var user = new IdentityUser(id, name, name + "@example.invalid", tenantId);
		user.AddRole(Factory.SeededRoleId);
		await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(user, autoSave: true);
		if (withScope)
		{
			await scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
				new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: id,
					educationMediumStageId: Factory.SeededMediumStageId), autoSave: true);
		}
		await unit.CompleteAsync();
		var client = CreateClientAs(id);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{StaffView},{StaffAdd},{StaffEdit}");
		return client;
	}

	private async Task AssignStageAsync(Guid employeeId, Guid stageId)
	{
		using var scope = Factory.Services.CreateScope();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await scope.ServiceProvider.GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>()
			.InsertAsync(new EmployeeOrganizationAssignment(Guid.NewGuid(), tenantId, employeeId, stageId), autoSave: true);
		await unit.CompleteAsync();
	}

	private async Task<Guid> CreateUnassignedEmployeeAsync(HttpClient client)
	{
		// Do not use CreateEmployeeAsync: it assigns academic scope after the personal-info save.
		using var body = new MultipartFormDataContent();
		body.Add(new StringContent(JsonSerializer.Serialize(EmployeeTestHelper.BuildPersonalInfoRequest("DraftFamily"), JsonOptions)), "data");
		var response = await client.PostAsync("/api/employees/personal-info", body);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<PersonalInfoResponse>(JsonOptions))!.Id;
	}

	private static FamilyInfoRequest FamilyRequest() => new()
	{
		FatherDetails = new FamilyMemberInfo { Name = "Synthetic Draft Father" }
	};
}
