using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
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
		var identity = context.ClaimsPrincipal.Identities.FirstOrDefault();
		if (identity is not ClaimsIdentity claimsIdentity || !claimsIdentity.IsAuthenticated)
			return;

		var permissionChecker = context.ServiceProvider.GetRequiredService<IPermissionChecker>();

		// Get granted module permissions and add to claims
		var grantedModulePermissions = await GetGrantedPermissionsAsync(permissionChecker, ModulePermissions);
		var moduleMask = BuildPermissionBitmask(ModulePermissions, grantedModulePermissions);
		claimsIdentity.AddClaim(new Claim(ModuleMaskClaimType, moduleMask));
	}

	private static async Task<HashSet<string>> GetGrantedPermissionsAsync(
		IPermissionChecker permissionChecker,
		string[] permissions)
	{
		var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var permission in permissions)
		{
			if (await permissionChecker.IsGrantedAsync(permission))
			{
				granted.Add(permission);
			}
		}
		return granted;
	}

	private static string BuildPermissionBitmask(string[] permissions, HashSet<string> grantedPermissions)
	{
		var bits = new System.Collections.BitArray(permissions.Length);
		for (int i = 0; i < permissions.Length; i++)
		{
			if (grantedPermissions.Contains(permissions[i]))
			{
				bits[i] = true;
			}
		}

		return string.Join(string.Empty, bits.Cast<bool>().Select(b => b ? PermissionGrantedBit : PermissionDeniedBit));
	}
}
