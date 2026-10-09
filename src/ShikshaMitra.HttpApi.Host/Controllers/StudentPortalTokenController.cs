using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.HostTenants;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict.Controllers;

namespace ShikshaMitra.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TokenController), IncludeSelf = true)]
public class StudentPortalTokenController : TokenController
{
	private Guid? verifiedTemporaryUserId;
	private StudentPortalLoginService PortalLogin => LazyServiceProvider.LazyGetRequiredService<StudentPortalLoginService>();
	private TenantActivityChecker TenantActivity => LazyServiceProvider.LazyGetRequiredService<TenantActivityChecker>();

	protected override async Task ReplaceEmailToUsernameOfInputIfNeeds(OpenIddictRequest request)
	{
		await base.ReplaceEmailToUsernameOfInputIfNeeds(request);
		var suppliedUsername = request.Username;
		if (string.IsNullOrWhiteSpace(suppliedUsername) || await UserManager.FindByNameAsync(suppliedUsername) != null) return;
		var username = await PortalLogin.FindMobileUsernameAsync(suppliedUsername);
		if (username != null) request.Username = username;
	}

	protected override async Task<IActionResult> HandleShouldChangePasswordOnNextLoginAsync(OpenIddictRequest request, IdentityUser user, string currentPassword)
	{
		if (!await PortalLogin.IsPortalUserAsync(user)) return await base.HandleShouldChangePasswordOnNextLoginAsync(request, user, currentPassword);
		// The stock handler returns a password-change token on a NotAllowed result.
		// Require proof of the mailed temporary secret before issuing that token.
		if (await UserManager.IsLockedOutAsync(user) || !await PortalLogin.CanAccessAsync(user)) return Deny();
		if (!await UserManager.CheckPasswordAsync(user, currentPassword))
		{
			await UserManager.AccessFailedAsync(user);
			return Deny();
		}
		await UserManager.ResetAccessFailedCountAsync(user);
		verifiedTemporaryUserId = user.Id;
		return await base.HandleShouldChangePasswordOnNextLoginAsync(request, user, currentPassword);
	}

	protected override async Task<IActionResult> SetSuccessResultAsync(OpenIddictRequest request, IdentityUser user)
	{
		if (!await TenantActivity.IsActiveAsync(user.TenantId)) return DenyInactiveTenant();
		if (!await PortalLogin.IsPortalUserAsync(user)) return await base.SetSuccessResultAsync(request, user);
		if (!await PortalLogin.CanAccessAsync(user) || user.ShouldChangePasswordOnNextLogin) return Deny();
		if (!user.EmailConfirmed && verifiedTemporaryUserId == user.Id)
		{
			// Reached only after ABP validated the change token and changed the password.
			user.SetEmailConfirmed(true);
			if (!(await UserManager.UpdateAsync(user)).Succeeded) return Deny();
		}
		if (!user.EmailConfirmed) return Deny();
		if (verifiedTemporaryUserId == user.Id && !await base.PreSignInCheckAsync(user)) return Deny();
		if (verifiedTemporaryUserId == user.Id && await IsTfaEnabledAsync(user))
		{
			verifiedTemporaryUserId = null;
			return await HandleTwoFactorLoginAsync(request, user);
		}
		return await base.SetSuccessResultAsync(request, user);
	}

	protected override async Task<bool> PreSignInCheckAsync(IdentityUser user)
	{
		if (!await TenantActivity.IsActiveAsync(user.TenantId)) return false;
		if (!await base.PreSignInCheckAsync(user)) return false;
		return !await PortalLogin.IsPortalUserAsync(user) ||
			user.EmailConfirmed && !user.ShouldChangePasswordOnNextLogin && await PortalLogin.CanAccessAsync(user);
	}

	private ForbidResult Deny() => Forbid(new AuthenticationProperties(new Dictionary<string, string?>
	{
		[OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
		[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Invalid username or password, or portal access is unavailable."
	}), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

	private ForbidResult DenyInactiveTenant() => Forbid(new AuthenticationProperties(new Dictionary<string, string?>
	{
		[OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
		[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "This school is inactive. Contact the platform administrator."
	}), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
