using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SimpleStateChecking;

namespace ShikshaMitra.Security.Permissions;

public class SecureAbpPermissionAppService(
	IPermissionManager permissionManager,
	IPermissionDefinitionManager permissionDefinitionManager,
	IOptions<PermissionManagementOptions> options,
	ISimpleStateCheckerManager<PermissionDefinition> simpleStateCheckerManager,
	IPermissionChecker permissionChecker,
	ShikshaMitraPermissionAppService permissions)
	: PermissionAppService(permissionManager, permissionDefinitionManager, options, simpleStateCheckerManager)
{
	public override Task<GetPermissionListResultDto> GetAsync(string providerName, string providerKey) =>
		permissions.GetAsync(providerName, providerKey);

	public override Task UpdateAsync(string providerName, string providerKey, UpdatePermissionsDto input) =>
		permissions.UpdateAsync(providerName, providerKey, input);

	protected override async Task CheckProviderPolicy(string providerName)
	{
		await base.CheckProviderPolicy(providerName);

		if (!options.Value.ProviderPolicies.TryGetValue(providerName, out var policyName) ||
			!await permissionChecker.IsGrantedAsync(policyName))
		{
			throw new AbpAuthorizationException();
		}
	}
}
