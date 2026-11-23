using System;
using System.Security.Claims;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using ShikshaMitra.Students.Portal;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.Controllers;

public sealed class StudentPortalSessionClaimsHandler(StudentAppAccessEnforcement access, ICurrentTenant tenant)
	: IAbpOpenIddictClaimsPrincipalHandler, ITransientDependency
{
	public async Task HandleAsync(AbpOpenIddictClaimsPrincipalHandlerContext context)
	{
		var principal = context.Principal;
		var revision = principal.FindFirst(StudentAppAccessEnforcement.SessionClaim)?.Value;
		// An authorization request creates a new code. An exchange must carry the revision
		// from its authenticated code or refresh token and must never pick up the current one here.
		if (context.OpenIddictRequest.GrantType == null &&
			Guid.TryParse(principal.FindFirst(AbpClaimTypes.TenantId)?.Value, out var tenantId) &&
			Guid.TryParse(principal.FindFirst(AbpClaimTypes.UserId)?.Value, out var userId))
		{
			using var scope = tenant.Change(tenantId);
			revision = await access.GetSessionRevisionAsync(userId);
		}
		ApplyRevision(principal, revision);
	}

	internal static void ApplyRevision(ClaimsPrincipal principal, string? revision)
	{
		if (revision == null) return;
		principal.SetClaim(StudentAppAccessEnforcement.SessionClaim, revision);
		principal.FindFirst(StudentAppAccessEnforcement.SessionClaim)?
			.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
	}
}
