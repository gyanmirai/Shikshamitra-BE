using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Security.Claims;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SimpleStateChecking;

namespace ShikshaMitra.Security.Permissions;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionChecker))]
public class ShikshaMitraPermissionChecker : PermissionChecker
{
	public ShikshaMitraPermissionChecker(
		ICurrentPrincipalAccessor currentPrincipalAccessor,
		IPermissionDefinitionManager permissionDefinitionManager,
		ICurrentTenant currentTenant,
		IPermissionValueProviderManager permissionValueProviderManager,
		ISimpleStateCheckerManager<PermissionDefinition> stateCheckerManager)
		: base(currentPrincipalAccessor, permissionDefinitionManager, currentTenant, permissionValueProviderManager, stateCheckerManager)
	{
	}

	public override async Task<bool> IsGrantedAsync(string name)
	{
		if (await base.IsGrantedAsync(name))
		{
			return true;
		}

		if (name.EndsWith(".Create"))
		{
			var addPermissionName = name[..^7] + ".Add";
			if (await base.IsGrantedAsync(addPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".Add"))
		{
			var createPermissionName = name[..^4] + ".Create";
			if (await base.IsGrantedAsync(createPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".View"))
		{
			var baseName = name[..^5];
			if (baseName == "AbpIdentity.Users" || baseName == "AbpIdentity.Roles" ||
			    baseName == "HR.PerformanceManagement.ReviewEmployees" ||
			    baseName == "HR.PerformanceManagement.ManageCycles")
			{
				if (await base.IsGrantedAsync(baseName))
				{
					return true;
				}
			}
		}

		if (name == "AbpIdentity.Users" || name == "AbpIdentity.Roles" ||
		    name == "HR.PerformanceManagement.ReviewEmployees" ||
		    name == "HR.PerformanceManagement.ManageCycles")
		{
			if (await base.IsGrantedAsync(name + ".View") ||
			    await base.IsGrantedAsync(name + ".Add") ||
			    await base.IsGrantedAsync(name + ".Delete"))
			{
				return true;
			}
		}

		return false;
	}

	public override async Task<bool> IsGrantedAsync(ClaimsPrincipal claimsPrincipal, string name)
	{
		if (await base.IsGrantedAsync(claimsPrincipal, name))
		{
			return true;
		}

		if (name.EndsWith(".Create"))
		{
			var addPermissionName = name[..^7] + ".Add";
			if (await base.IsGrantedAsync(claimsPrincipal, addPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".Add"))
		{
			var createPermissionName = name[..^4] + ".Create";
			if (await base.IsGrantedAsync(claimsPrincipal, createPermissionName))
			{
				return true;
			}
		}

		if (name.EndsWith(".View"))
		{
			var baseName = name[..^5];
			if (baseName == "AbpIdentity.Users" || baseName == "AbpIdentity.Roles" ||
			    baseName == "HR.PerformanceManagement.ReviewEmployees" ||
			    baseName == "HR.PerformanceManagement.ManageCycles")
			{
				if (await base.IsGrantedAsync(claimsPrincipal, baseName))
				{
					return true;
				}
			}
		}

		if (name == "AbpIdentity.Users" || name == "AbpIdentity.Roles" ||
		    name == "HR.PerformanceManagement.ReviewEmployees" ||
		    name == "HR.PerformanceManagement.ManageCycles")
		{
			if (await base.IsGrantedAsync(claimsPrincipal, name + ".View") ||
			    await base.IsGrantedAsync(claimsPrincipal, name + ".Add") ||
			    await base.IsGrantedAsync(claimsPrincipal, name + ".Delete"))
			{
				return true;
			}
		}

		return false;
	}
}
