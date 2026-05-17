using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
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
	public async Task Principal_Should_Not_Call_Permission_Management_Apis()
	{
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "Principal",
			permissions: "");

		var response = await client.GetAsync($"/api/permission-management/permissions?providerName=U&providerKey={Guid.NewGuid()}");

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
		using var client = CreateClient(
			tenantId: TestTenantId.ToString(),
			roles: "admin",
			permissions: IdentityPermissions.Users.ManagePermissions);

		var response = await client.GetAsync($"/api/permission-management/permissions?providerName=U&providerKey={Guid.NewGuid()}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private HttpClient CreateClient(string tenantId, string permissions, string roles = "admin")
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenantId);
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
