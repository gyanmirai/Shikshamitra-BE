using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

[Collection(ApiTestCollection.Name)]
[Trait("Module", "Students")]
[Trait("Tier", "Critical")]
public sealed class StudentSiblingParentApiSecurity_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Fact]
	public async Task Parent_setup_retry_rejects_anonymous_requests()
	{
		using var client = CreateAnonymousClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		using var response = await client.PostAsJsonAsync(RetryUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	[InlineData(Volo.Abp.Identity.IdentityPermissions.Users.Update)]
	[InlineData("none")]
	public async Task Parent_setup_retry_requires_exact_onboarding_add_permission(string permission)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		using var response = await client.PostAsJsonAsync(RetryUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Parent_setup_retry_with_exact_permission_still_rejects_unknown_case_at_scope_boundary()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		using var response = await client.PostAsJsonAsync(RetryUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Parent_setup_retry_with_exact_permission_validates_stamp_before_processing()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		using var response = await client.PostAsJsonAsync(RetryUrl(), new { concurrencyStamp = "" });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
	}

	private static string RetryUrl() => $"/api/student/onboarding/{Guid.NewGuid()}/credentials/retry-parent-setup";

	[Fact]
	public async Task Parent_link_rejects_anonymous_requests()
	{
		using var client = CreateAnonymousClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		using var response = await client.PostAsJsonAsync(LinkUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[InlineData(Volo.Abp.Identity.IdentityPermissions.Users.Update)]
	[InlineData("none")]
	public async Task Parent_link_requires_both_exact_permissions(string permission)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
		using var response = await client.PostAsJsonAsync(LinkUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Parent_link_with_both_permissions_still_checks_case_scope()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ParentLinkPermissions);
		using var response = await client.PostAsJsonAsync(LinkUrl(), new { concurrencyStamp = "test-stamp" });
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Parent_link_with_both_permissions_validates_stamp_before_processing()
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add("__tenant", TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ParentLinkPermissions);
		using var response = await client.PostAsJsonAsync(LinkUrl(), new { concurrencyStamp = "" });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
	}

	private static string ParentLinkPermissions => string.Join(",",
		ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit, Volo.Abp.Identity.IdentityPermissions.Users.Update);
	private static string LinkUrl() => $"/api/student/onboarding/{Guid.NewGuid()}/credentials/link-parent";
}
