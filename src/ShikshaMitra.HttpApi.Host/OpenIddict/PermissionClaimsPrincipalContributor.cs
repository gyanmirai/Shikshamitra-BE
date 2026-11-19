using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.OpenIddict;

public class PermissionClaimsPrincipalContributor
	: IAbpClaimsPrincipalContributor, ITransientDependency
{
	private static readonly string[] ModulePermissions = ShikshaMitraModulePermissions.All;
	private const char PermissionGrantedBit = '1';
	private const char PermissionDeniedBit = '0';
	private const string ModuleMaskClaimType = "module_mask";

	public async Task ContributeAsync(AbpClaimsPrincipalContributorContext context)
	{
		var claimsIdentity = context.ClaimsPrincipal.Identities.FirstOrDefault(identity => identity.IsAuthenticated);
		if (claimsIdentity == null)
			return;

		var permissionChecker = context.ServiceProvider.GetRequiredService<IPermissionChecker>();

		// During sign-in the request principal may still be anonymous or belong to a different user.
		var grants = await permissionChecker.IsGrantedAsync(context.ClaimsPrincipal, ModulePermissions);
		var moduleMask = new string(ModulePermissions.Select(permission =>
			grants.Result[permission] == PermissionGrantResult.Granted ? PermissionGrantedBit : PermissionDeniedBit).ToArray());

		foreach (var identity in context.ClaimsPrincipal.Identities)
			foreach (var claim in identity.FindAll(ModuleMaskClaimType).ToArray())
				identity.RemoveClaim(claim);
		claimsIdentity.AddClaim(new Claim(ModuleMaskClaimType, moduleMask));
	}
}
