using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.OpenIddict;

public class PermissionClaimsPrincipalContributor
	: IAbpClaimsPrincipalContributor, ITransientDependency
{
	private static readonly string[] ModulePermissions = ShikshaMitraModulePermissions.All;
	private static readonly string[] SubModulePermissions = ShikshaMitraSubModulePermissions.All;
	private static readonly HashSet<string> ManagedPermissions = new(
		ModulePermissions.Concat(SubModulePermissions),
		StringComparer.OrdinalIgnoreCase
	);

	public async Task ContributeAsync(AbpClaimsPrincipalContributorContext context)
	{
		var identity = context.ClaimsPrincipal.Identities.FirstOrDefault();
		if (identity is not ClaimsIdentity claimsIdentity || !claimsIdentity.IsAuthenticated)
			return;

		var userId = context.ClaimsPrincipal.FindUserId();
		if (userId == null)
			return;

		// Fetch all permissions with parallel execution
		var grantedPermissions = await GetGrantedPermissionsAsync(context.ServiceProvider, userId.Value);

		var grantedModuleMask = BuildPermissionBitmask(ModulePermissions, grantedPermissions);
		claimsIdentity.AddClaim(new Claim("module_mask", grantedModuleMask));

		var grantedSubModuleMask = BuildPermissionBitmask(SubModulePermissions, grantedPermissions);
		claimsIdentity.AddClaim(new Claim("submodule_mask", grantedSubModuleMask));
	}

	private static async Task<HashSet<string>> GetGrantedPermissionsAsync(IServiceProvider serviceProvider, Guid userId)
	{
		var permissionGrantRepository = serviceProvider.GetRequiredService<IPermissionGrantRepository>();
		var currentPrincipalAccessor = serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
		
		// Get user's roles
		var roles = currentPrincipalAccessor.Principal?.FindAll(AbpClaimTypes.Role)
			.Select(c => c.Value)
			.ToList() ?? new List<string>();
		
		// Fetch user permissions and all role permissions IN PARALLEL
		var userGrantsTask = permissionGrantRepository.GetListAsync(
			providerName: "U",
			providerKey: userId.ToString()
		);
		
		var roleGrantsTasks = roles.Select(role =>
			permissionGrantRepository.GetListAsync(providerName: "R", providerKey: role)
		).ToList();
		
		// Wait for all queries to complete in parallel
		await Task.WhenAll(roleGrantsTasks.Append(userGrantsTask));
		
		// Process results
		var rolePermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var userPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var userManagedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		
		// Collect role permissions
		foreach (var roleGrantsTask in roleGrantsTasks)
		{
			foreach (var grant in roleGrantsTask.Result)
			{
				rolePermissions.Add(grant.Name);
			}
		}
		
		// Collect user permissions
		foreach (var grant in userGrantsTask.Result)
		{
			userPermissions.Add(grant.Name);
			if (ManagedPermissions.Contains(grant.Name))
			{
				userManagedPermissions.Add(grant.Name);
			}
		}
		
		// Build final set with user-override logic
		var finalPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		
		bool userControlsPermissions = userManagedPermissions.Any();
		
		if (userControlsPermissions)
		{
			// User has explicit control over managed permissions
			// For managed permissions: use ONLY user grants
			// For non-managed permissions: use union of role + user
			foreach (var permission in rolePermissions)
			{
				if (!ManagedPermissions.Contains(permission))
				{
					finalPermissions.Add(permission);
				}
			}
			
			foreach (var permission in userPermissions)
			{
				finalPermissions.Add(permission);
			}
		}
		else
		{
			// User has no explicit control - use all role permissions + any user permissions
			foreach (var permission in rolePermissions)
			{
				finalPermissions.Add(permission);
			}
			
			foreach (var permission in userPermissions)
			{
				finalPermissions.Add(permission);
			}
		}

		return finalPermissions;
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

		var bitString = string.Join(string.Empty, bits.Cast<bool>().Select(b => b ? '1' : '0'));
		return bitString;
	}
}
