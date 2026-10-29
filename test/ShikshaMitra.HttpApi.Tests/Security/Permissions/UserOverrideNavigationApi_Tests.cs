using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Fact]
	public async Task Direct_module_and_action_allows_without_role_grants_reach_self_catalogue_and_token_claims()
	{
		var (role, user) = await CreateTargetAsync();
		var names = ShikshaMitraModulePermissions.All.Concat(new[] { View, Edit,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View })
			.Concat(new[] { View, Edit, ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View }
				.SelectMany(name => NavigationVisibilityPrerequisites.GetPaths(name).FirstOrDefault() ?? []))
			.Distinct().ToArray();
		await SetOverridesAsync(user, names.Select(name => (name, "allow")).ToArray());
		(await ReadAsync(Url("R", role))).ShouldAllBe(permission => !permission.IsGranted);

		var (catalogue, mask) = await ReadEffectiveSelfCatalogueAndMaskAsync(user, role);
		var permissions = catalogue.Groups.SelectMany(group => group.Permissions).ToDictionary(permission => permission.Name);
		foreach (var name in names)
		{
			permissions[name].IsGranted.ShouldBeTrue(name);
			permissions[name].GrantedProviders.ShouldContain(provider => provider.ProviderName == "U" && provider.ProviderKey == user.ToString());
			permissions[name].GrantedProviders.ShouldNotContain(provider => provider.ProviderName == "R");
			await AssertRealCheckerAsync(user, role, name, true);
		}
		mask.ShouldBe(new string('1', ShikshaMitraModulePermissions.All.Length));
	}

	[Fact]
	public async Task Action_allow_without_visibility_is_rejected_without_implicitly_granting_navigation()
	{
		var (role, user) = await CreateTargetAsync();
		const string refundView = ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View;
		using var rejected = await Client.PutAsJsonAsync(OverrideUrl(user), new { permissions =
			new[] { View, Edit, refundView }.Select(name => new { name, state = "allow" }) });
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());

		var (catalogue, mask) = await ReadEffectiveSelfCatalogueAndMaskAsync(user, role);
		var permissions = catalogue.Groups.SelectMany(group => group.Permissions).ToDictionary(permission => permission.Name);
		permissions[View].IsGranted.ShouldBeFalse();
		permissions[Edit].IsGranted.ShouldBeFalse();
		permissions[refundView].IsGranted.ShouldBeFalse();
		permissions[ShikshaMitraModulePermissions.HR].IsGranted.ShouldBeFalse();
		permissions[ShikshaMitraModulePermissions.Finance].IsGranted.ShouldBeFalse();
		permissions[ShikshaMitraModulePermissions.Dashboard].IsGranted.ShouldBeFalse();
		mask.ShouldBe(new string('0', ShikshaMitraModulePermissions.All.Length));
		await AssertRealCheckerAsync(user, role, ShikshaMitraModulePermissions.HR, false);
		await AssertRealCheckerAsync(user, role, ShikshaMitraModulePermissions.Dashboard, false);
	}

	[Fact]
	public async Task Module_allow_deny_and_follow_role_remain_consistent_in_self_catalogue_and_token_claims()
	{
		var (role, user) = await CreateTargetAsync();
		const string module = ShikshaMitraModulePermissions.Dashboard;
		await SetAsync(Url("R", role), (module, true));
		await SetOverridesAsync(user, (module, "deny"));
		await AssertModuleVisibilityAsync(user, role, module, false);
		await SetOverridesAsync(user, (module, "allow"));
		await SetAsync(Url("R", role), (module, false));
		await AssertModuleVisibilityAsync(user, role, module, true);
		await SetOverridesAsync(user, (module, "inherit"));
		await AssertModuleVisibilityAsync(user, role, module, false);
		await SetAsync(Url("R", role), (module, true));
		await AssertModuleVisibilityAsync(user, role, module, true);
	}

	private async Task AssertModuleVisibilityAsync(Guid user, string role, string module, bool expected)
	{
		var (catalogue, mask) = await ReadEffectiveSelfCatalogueAndMaskAsync(user, role);
		catalogue.Groups.SelectMany(group => group.Permissions).Single(permission => permission.Name == module).IsGranted.ShouldBe(expected);
		(mask[Array.IndexOf(ShikshaMitraModulePermissions.All, module)] == '1').ShouldBe(expected);
		await AssertRealCheckerAsync(user, role, module, expected);
	}

	private async Task<(GetPermissionListResultDto Catalogue, string Mask)> ReadEffectiveSelfCatalogueAndMaskAsync(Guid user, string role)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
		{
			new Claim(AbpClaimTypes.UserId, user.ToString()),
			new Claim(AbpClaimTypes.Role, role),
			new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)
		}, "test"));
		using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
		IPermissionChecker checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(services);
		var app = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionAppService>(services, checker);
		app.LazyServiceProvider = services.GetRequiredService<IAbpLazyServiceProvider>();
		var catalogue = await app.GetAsync("U", user.ToString());
		var factory = new AbpClaimsPrincipalFactory(new EffectiveCheckerServiceProvider(services, checker),
			services.GetRequiredService<IOptions<AbpClaimsPrincipalFactoryOptions>>());
		var tokenPrincipal = await factory.CreateAsync(principal);
		return (catalogue, tokenPrincipal.Claims.Single(claim => claim.Type == "module_mask").Value);
	}

	private sealed class EffectiveCheckerServiceProvider(IServiceProvider services, IPermissionChecker checker) : IServiceProvider
	{
		public object? GetService(Type serviceType) => serviceType == typeof(IPermissionChecker) ? checker : services.GetService(serviceType);
	}
}
