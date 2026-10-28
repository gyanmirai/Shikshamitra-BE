using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.OpenIddict;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Token_mask_uses_the_token_subject_instead_of_the_ambient_request_user(bool allowed)
	{
		var (role, user) = await CreateTargetAsync();
		var (otherRole, otherUser) = await CreateTargetAsync();
		await SetOverridesAsync(user, (ShikshaMitraModulePermissions.Finance, allowed ? "allow" : "deny"));
		await SetOverridesAsync(otherUser, (ShikshaMitraModulePermissions.Finance, allowed ? "deny" : "allow"));
		var principal = TokenSubject(user, role);
		await ContributeTokenMaskAsync(principal, TokenSubject(otherUser, otherRole));
		AssertFinanceMask(principal, allowed);
	}

	[Fact]
	public async Task Initial_sign_in_resolves_direct_module_allows_with_an_anonymous_ambient_request()
	{
		var (role, user) = await CreateTargetAsync();
		await SetOverridesAsync(user, (ShikshaMitraModulePermissions.Finance, "allow"));
		var principal = TokenSubject(user, role);
		await ContributeTokenMaskAsync(principal, new ClaimsPrincipal(new ClaimsIdentity()));
		AssertFinanceMask(principal, true);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Rebuilding_a_token_replaces_stale_masks_on_all_identities(bool allowed)
	{
		var (role, user) = await CreateTargetAsync();
		await SetOverridesAsync(user, (ShikshaMitraModulePermissions.Finance, allowed ? "allow" : "deny"));
		var principal = TokenSubject(user, role);
		((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("module_mask", new string(allowed ? '0' : '1', ShikshaMitraModulePermissions.All.Length)));
		principal.AddIdentity(new ClaimsIdentity(new[] { new Claim("module_mask", "stale-secondary-mask") }));
		await ContributeTokenMaskAsync(principal, principal);
		await ContributeTokenMaskAsync(principal, principal);
		AssertFinanceMask(principal, allowed);
	}

	[Fact]
	public async Task Token_mask_is_attached_to_the_authenticated_identity_when_anonymous_identity_is_first()
	{
		var (role, user) = await CreateTargetAsync();
		await SetOverridesAsync(user, (ShikshaMitraModulePermissions.Finance, "allow"));
		var identity = TokenSubject(user, role).Identities.Single();
		var anonymous = new ClaimsIdentity(new[] { new Claim("module_mask", "stale-anonymous-mask") });
		var principal = new ClaimsPrincipal(new[] { anonymous, identity });
		await ContributeTokenMaskAsync(principal, principal);
		AssertFinanceMask(principal, true);
		identity.FindAll("module_mask").ShouldHaveSingleItem();
		anonymous.HasClaim(claim => claim.Type == "module_mask").ShouldBeFalse();
	}

	private async Task ContributeTokenMaskAsync(ClaimsPrincipal subject, ClaimsPrincipal ambient)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		using var current = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(ambient);
		IPermissionChecker checker = ActivatorUtilities.CreateInstance<ShikshaMitraPermissionChecker>(services);
		await new PermissionClaimsPrincipalContributor().ContributeAsync(
			new AbpClaimsPrincipalContributorContext(subject, new EffectiveCheckerServiceProvider(services, checker)));
	}

	private static ClaimsPrincipal TokenSubject(Guid user, string role) => new(new ClaimsIdentity(new[]
	{
		new Claim(AbpClaimTypes.UserId, user.ToString()),
		new Claim(AbpClaimTypes.Role, role),
		new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)
	}, "test"));

	private static void AssertFinanceMask(ClaimsPrincipal principal, bool allowed)
	{
		var mask = principal.FindAll("module_mask").ShouldHaveSingleItem().Value;
		mask.Length.ShouldBe(ShikshaMitraModulePermissions.All.Length);
		(mask[Array.IndexOf(ShikshaMitraModulePermissions.All, ShikshaMitraModulePermissions.Finance)] == '1').ShouldBe(allowed);
		for (var i = 0; i < mask.Length; i++)
			if (ShikshaMitraModulePermissions.All[i] != ShikshaMitraModulePermissions.Finance) mask[i].ShouldBe('0');
	}
}
