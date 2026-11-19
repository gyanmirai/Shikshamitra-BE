using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.TenantSettings;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.TenantSettings)]
public class TenantAuthorizationApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/multi-tenancy/tenants";
	private static readonly Guid TestTenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

	public TenantAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_With_View_Permission_Should_List_Only_Current_Tenant()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: TenantManagementPermissions.Tenants.Default);

		var response = await client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TenantDto>>(JsonOptions);
		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(1);
		result.Items.Count.ShouldBe(1);
		result.Items[0].Id.ShouldBe(TestTenantId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_Without_View_Permission_Should_View_Current_Tenant()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "Principal",
			permissions: "");

		var response = await client.GetAsync($"{BaseUrl}/{TestTenantId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
		result.ShouldNotBeNull();
		result.Id.ShouldBe(TestTenantId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_Without_View_Permission_Should_List_Only_Current_Tenant()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "Principal",
			permissions: "");

		var response = await client.GetAsync(BaseUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TenantDto>>(JsonOptions);
		result.ShouldNotBeNull();
		result.TotalCount.ShouldBe(1);
		result.Items.Count.ShouldBe(1);
		result.Items[0].Id.ShouldBe(TestTenantId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_Should_Not_View_Another_Tenant()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: TenantManagementPermissions.Tenants.Default);

		var response = await client.GetAsync($"{BaseUrl}/{OtherTenantId}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_Admin_Should_Update_Only_Current_Tenant()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: TenantManagementPermissions.Tenants.Update);

		var response = await client.PutAsJsonAsync(
			$"{BaseUrl}/{OtherTenantId}",
			BuildTenantUpdatePayload("OtherTenant"),
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_Should_Not_Delete_Tenants()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: TenantManagementPermissions.Tenants.Delete);

		var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{OtherTenantId}");

		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Host_User_With_Tenant_Permissions_Should_Create_And_Delete_Tenant()
	{
		using var client = CreateClient(
			tenantId: "host",
			permissions: string.Join(",",
				TenantManagementPermissions.Tenants.Default,
				TenantManagementPermissions.Tenants.Create,
				TenantManagementPermissions.Tenants.Delete));
		var tenantName = $"hostcreated{Guid.NewGuid():N}";

		var createResponse = await client.PostAsJsonAsync(
			BaseUrl,
			BuildTenantCreatePayload(tenantName),
			JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await createResponse.Content.ReadAsStringAsync());
		var createdTenant = await createResponse.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
		createdTenant.ShouldNotBeNull();

		var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{createdTenant.Id}");

		deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task User_Should_Read_Own_Permissions_Without_ManagePermissions()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			userId: TestAuthHandler.TestUserId,
			roles: "Principal",
			permissions: "");

		var response = await client.GetAsync($"/api/permission-management/permissions?providerName=U&providerKey={TestAuthHandler.TestUserId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task User_Should_Not_Read_Another_Users_Permissions_Without_ManagePermissions()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			userId: TestAuthHandler.TestUserId,
			roles: "Principal",
			permissions: "");

		var response = await client.GetAsync(
			$"/api/permission-management/permissions?providerName=U&providerKey={Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Principal_Should_Not_Update_Permission_Management_Apis()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "Principal",
			permissions: "");

		var response = await client.PutAsJsonAsync(
			$"/api/permission-management/permissions?providerName=U&providerKey={Guid.NewGuid()}",
			new { Permissions = Array.Empty<object>() },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_User_Should_Not_Create_Tenants()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: TenantManagementPermissions.Tenants.Create);
		var tenantName = $"tenantblocked{Guid.NewGuid():N}";

		var createResponse = await client.PostAsJsonAsync(
			BaseUrl,
			BuildTenantCreatePayload(tenantName),
			JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await createResponse.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_Admin_With_Permission_Should_Call_Permission_Management_Apis()
	{
		var targetUserId = await CreateTenantUserAsync();
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "admin",
			permissions: IdentityPermissions.Users.ManagePermissions);

		var response = await client.GetAsync($"/api/permission-management/permissions?providerName=U&providerKey={targetUserId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Tenant_Admin_Should_Update_And_Read_User_Module_Visibility()
	{
		var targetUserId = await CreateTenantUserAsync();
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "admin",
			permissions: IdentityPermissions.Users.ManagePermissions);
		var url = $"/api/permission-management/permissions?providerName=U&providerKey={targetUserId}";

		var updateResponse = await client.PutAsJsonAsync(
			url,
			new
			{
				Permissions = new[]
				{
					new { Name = ShikshaMitraModulePermissions.HR, IsGranted = true }
				}
			},
			JsonOptions);

		updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, await updateResponse.Content.ReadAsStringAsync());

		var getResponse = await client.GetAsync(url);

		getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await getResponse.Content.ReadAsStringAsync());
		var permissions = await getResponse.Content.ReadFromJsonAsync<GetPermissionListResultDto>(JsonOptions);
		permissions.ShouldNotBeNull();
		permissions.Groups
			.SelectMany(group => group.Permissions)
			.Single(permission => permission.Name == ShikshaMitraModulePermissions.HR)
			.IsGranted.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Create_Role_With_Color_Http_Api()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			permissions: string.Join(",", IdentityPermissions.Roles.Default, IdentityPermissions.Roles.Create));

		var payload = new Dictionary<string, object?>
		{
			["Name"] = "ApiTestRoleWithColor",
			["IsDefault"] = true,
			["IsPublic"] = true,
			// Match the role screen's wire payload for ABP extensible DTOs.
			["color"] = "#abcdef",
			["employeeOnboardingAssignable"] = false,
			["ExtraProperties"] = new Dictionary<string, object?>
			{
				["color"] = "#abcdef",
				["employeeOnboardingAssignable"] = false
			}
		};

		var response = await client.PostAsJsonAsync("/api/identity/roles", payload, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var responseJson = await response.Content.ReadAsStringAsync();
		var createdRoleDto = JsonSerializer.Deserialize<IdentityRoleDto>(responseJson, JsonOptions);
		createdRoleDto.ShouldNotBeNull();
		
		using var responseDocument = JsonDocument.Parse(responseJson);
		// Verify the HTTP contract directly; System.Text.Json does not populate ABP's
		// extensible dictionary in this DTO with the host's Newtonsoft converters.
		responseDocument.RootElement.GetProperty("extraProperties").GetProperty("color")
			.GetString().ShouldBe("#abcdef", responseJson);
	}

	private async Task<Guid> CreateTenantUserAsync()
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TestTenantId);
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var userId = Guid.NewGuid();
		await scope.ServiceProvider.GetRequiredService<IIdentityUserRepository>().InsertAsync(
			new IdentityUser(userId, $"permission-{userId:N}", $"permission-{userId:N}@example.test", TestTenantId));
		await uow.CompleteAsync();
		return userId;
	}

	private HttpClient CreateClient(string tenantId, string permissions, string roles = "admin", string? userId = null)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId);
		if (!string.IsNullOrWhiteSpace(userId))
		{
			client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId);
		}

		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, roles);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.IsNullOrWhiteSpace(permissions) ? "__none__" : permissions);
		return client;
	}

	private static Dictionary<string, object?> BuildTenantCreatePayload(string tenantName)
	{
		var payload = BuildRequiredTenantPayload(tenantName);
		payload["AdminEmailAddress"] = $"{tenantName}@ddvs.com";
		payload["AdminPassword"] = "Temp@123";
		return payload;
	}

	private static Dictionary<string, object?> BuildTenantUpdatePayload(string tenantName)
	{
		var payload = BuildRequiredTenantPayload(tenantName);
		payload["ConcurrencyStamp"] = string.Empty;
		return payload;
	}

	private static Dictionary<string, object?> BuildRequiredTenantPayload(string tenantName)
	{
		return new Dictionary<string, object?>
		{
			["Name"] = tenantName,
			["ExtraProperties"] = new Dictionary<string, object?>
			{
				["Email"] = $"{tenantName}@ddvs.com",
				["InstituteName"] = "DDVS Test Institute",
				["Address1"] = "Address line 1",
				["Address2"] = "Address line 2",
				["Address3"] = "Address line 3",
				["City"] = "Ahmedabad",
				["State"] = "Gujarat",
				["PostalCode"] = "380001",
				["Country"] = "India",
				["ContactNumber"] = "9876543210"
			}
		};
	}
}
