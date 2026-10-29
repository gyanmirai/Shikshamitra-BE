using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	private const string HrVisibility = ShikshaMitraModulePermissions.HR;
	private const string EmployeeVisibility = ShikshaMitraSubModulePermissions.EmployeeRecords;

	[Theory]
	[InlineData("R")]
	[InlineData("U")]
	public async Task Permission_visibility_prerequisite_rejects_hidden_target_and_rolls_back_batch(string provider)
	{
		var (role, user) = await CreateTargetAsync();
		var url = Url(provider, provider == "R" ? role : user.ToString());
		using var response = await Client.PutAsJsonAsync(url, new { permissions = new[] {
			new { name = HrVisibility, isGranted = true }, new { name = Edit, isGranted = true } } });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain("VISIBILITY_PREREQUISITE_MISSING");
		(await response.Content.ReadAsStringAsync()).ShouldContain("Navigation Visibility");
		var grants = await ReadAsync(url);
		grants.Single(permission => permission.Name == Edit).IsGranted.ShouldBeFalse();
		grants.Single(permission => permission.Name == HrVisibility).IsGranted.ShouldBeFalse();
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_accepts_same_batch_visibility_independent_of_order()
	{
		var (role, _) = await CreateTargetAsync();
		var url = Url("R", role);
		await SetAsync(url, (Edit, true), (EmployeeVisibility, true), (HrVisibility, true));
		(await ReadAsync(url)).Single(permission => permission.Name == Edit).IsGranted.ShouldBeTrue();
		await SetAsync(url, (EmployeeVisibility, false));
		(await ReadAsync(url)).Single(permission => permission.Name == Edit).IsGranted.ShouldBeTrue();
		using var response = await Client.PutAsJsonAsync(url, new { permissions = new[] { new { name = View, isGranted = true } } });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		await SetAsync(url, (Edit, false));
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_accepts_inherited_visibility_but_preserves_user_deny()
	{
		var (role, user) = await CreateTargetAsync();
		await SetAsync(Url("R", role), (HrVisibility, true), (EmployeeVisibility, true));
		await SetOverridesAsync(user, (Edit, "allow"));
		await SetOverridesAsync(user, (EmployeeVisibility, "deny"));
		using var rejected = await Client.PutAsJsonAsync(OverrideUrl(user), new { permissions = new[] { new { name = View, state = "allow" } } });
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		await AssertOverrideAsync(user, View, "inherit", false);
		await AssertOverrideAsync(user, EmployeeVisibility, "deny", false);
		await SetOverridesAsync(user, (View, "allow"), (EmployeeVisibility, "inherit"));
		await AssertOverrideAsync(user, View, "allow", true);
		await AssertOverrideAsync(user, EmployeeVisibility, "inherit", true);
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_uses_pending_user_denial_before_any_writes()
	{
		var (role, user) = await CreateTargetAsync();
		await SetAsync(Url("R", role), (HrVisibility, true), (EmployeeVisibility, true));
		using var rejected = await Client.PutAsJsonAsync(OverrideUrl(user), new { permissions = new[] {
			new { name = View, state = "allow" }, new { name = EmployeeVisibility, state = "deny" } } });
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		await AssertOverrideAsync(user, View, "inherit", false);
		await AssertOverrideAsync(user, EmployeeVisibility, "inherit", true);
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_rejects_missing_module_even_when_feature_is_visible()
	{
		var (role, _) = await CreateTargetAsync();
		var url = Url("R", role);
		await SetAsync(url, (EmployeeVisibility, true));
		using var response = await Client.PutAsJsonAsync(url, new { permissions = new[] { new { name = Edit, isGranted = true } } });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		(await ReadAsync(url)).Single(permission => permission.Name == Edit).IsGranted.ShouldBeFalse();
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_accepts_user_visibility_allow_in_same_batch_as_action()
	{
		var (_, user) = await CreateTargetAsync();
		await SetOverridesAsync(user, (View, "allow"), (EmployeeVisibility, "allow"), (HrVisibility, "allow"));
		await AssertOverrideAsync(user, View, "allow", true);
		await AssertOverrideAsync(user, EmployeeVisibility, "allow", true);
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_honors_legacy_user_revoke_that_clears_a_visibility_denial()
	{
		var (role, user) = await CreateTargetAsync();
		await SetAsync(Url("R", role), (HrVisibility, true), (EmployeeVisibility, true));
		await SetOverridesAsync(user, (EmployeeVisibility, "deny"));
		await SetAsync(Url("U", user.ToString()), (View, true), (EmployeeVisibility, false));
		await AssertOverrideAsync(user, View, "allow", true);
		await AssertOverrideAsync(user, EmployeeVisibility, "inherit", true);
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_rejects_pending_role_visibility_revocation_atomically()
	{
		var (role, _) = await CreateTargetAsync();
		var url = Url("R", role);
		await SetAsync(url, (HrVisibility, true), (EmployeeVisibility, true));
		using var response = await Client.PutAsJsonAsync(url, new { permissions = new[] {
			new { name = View, isGranted = true }, new { name = EmployeeVisibility, isGranted = false } } });
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var grants = await ReadAsync(url);
		grants.Single(permission => permission.Name == View).IsGranted.ShouldBeFalse();
		grants.Single(permission => permission.Name == EmployeeVisibility).IsGranted.ShouldBeTrue();
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_does_not_invent_navigation_for_api_only_capabilities()
	{
		var (role, _) = await CreateTargetAsync();
		const string permission = "ShikshaMitra.UserScope.Assign";
		var url = Url("R", role);
		await SetAsync(url, (permission, true));
		(await ReadAsync(url)).Single(entry => entry.Name == permission).IsGranted.ShouldBeTrue();
	}

	[Fact]
	public async Task Permission_visibility_prerequisite_cannot_be_bypassed_by_unproxied_application_calls()
	{
		var (role, _) = await CreateTargetAsync();
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
			new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
			new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId) }, TestAuthHandler.SchemeName));
		using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
		var context = services.GetRequiredService<IHttpContextAccessor>();
		var previous = context.HttpContext;
		context.HttpContext = new DefaultHttpContext { User = principal };
		try
		{
			var app = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionAppService>(services);
			app.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
			var error = await Should.ThrowAsync<BusinessException>(() => app.UpdateAsync("R", role,
				new UpdatePermissionsDto { Permissions = [new UpdatePermissionDto { Name = Edit, IsGranted = true }] }));
			error.Code.ShouldBe("VISIBILITY_PREREQUISITE_MISSING");
		}
		finally
		{
			context.HttpContext = previous;
		}
	}
}
