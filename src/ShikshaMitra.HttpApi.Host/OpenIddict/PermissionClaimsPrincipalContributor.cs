using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.OpenIddict;

public class PermissionClaimsPrincipalContributor
	: IAbpClaimsPrincipalContributor, ITransientDependency
{
	private static readonly string[] ModulePermissions = ShikshaMitraModulePermissions.All;
	private static readonly string[] SubModulePermissions = ShikshaMitraSubModulePermissions.All;

	public async Task ContributeAsync(AbpClaimsPrincipalContributorContext context)
	{
		var identity = context.ClaimsPrincipal.Identities.FirstOrDefault();
		if (identity is not ClaimsIdentity claimsIdentity || !claimsIdentity.IsAuthenticated)
			return;

		var userId = context.ClaimsPrincipal.FindUserId();
		if (userId == null)
			return;

		var permissionChecker = context.ServiceProvider.GetRequiredService<IPermissionChecker>();

		var grantedPermissions = await GetGrantedModulePermissionsAsync(permissionChecker);

		foreach (var permission in grantedPermissions)
		{
			claimsIdentity.AddClaim(new Claim("module", permission));
		}

		var subModuleGrantedPermissions = await GetGrantedSubModulePermissionsAsync(permissionChecker);
		foreach (var subPermission in subModuleGrantedPermissions)
		{
			claimsIdentity.AddClaim(new Claim("submodule", subPermission));
		}
	}

	private static async Task<IEnumerable<string>> GetGrantedModulePermissionsAsync(IPermissionChecker permissionChecker)
	{
		var granted = new List<string>();
		foreach (var permission in ModulePermissions)
		{
			if (await permissionChecker.IsGrantedAsync(permission))
			{
				granted.Add(permission);
			}
		}
		return granted;
	}

	private static async Task<IEnumerable<string>> GetGrantedSubModulePermissionsAsync(IPermissionChecker permissionChecker)
	{
		var granted = new List<string>();
		foreach (var permission in SubModulePermissions)
		{
			if (await permissionChecker.IsGrantedAsync(permission))
			{
				granted.Add(permission);
			}
		}
		return granted;
	}
}
