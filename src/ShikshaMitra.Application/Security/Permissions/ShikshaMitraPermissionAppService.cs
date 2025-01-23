using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SimpleStateChecking;

namespace ShikshaMitra.Security.Permissions;

[RemoteService(false)]
public class ShikshaMitraPermissionAppService : ApplicationService, IShikshaMitraPermissionAppService
{
	protected PermissionManagementOptions Options { get; }
	protected IPermissionManager PermissionManager { get; }
	protected IPermissionDefinitionManager PermissionDefinitionManager { get; }
	protected ISimpleStateCheckerManager<PermissionDefinition> SimpleStateCheckerManager { get; }

	public ShikshaMitraPermissionAppService(
		IPermissionManager permissionManager,
		IPermissionDefinitionManager permissionDefinitionManager,
		IOptions<PermissionManagementOptions> options,
		ISimpleStateCheckerManager<PermissionDefinition> simpleStateCheckerManager)
	{
		Options = options.Value;
		PermissionManager = permissionManager;
		PermissionDefinitionManager = permissionDefinitionManager;
		SimpleStateCheckerManager = simpleStateCheckerManager;
	}

	public virtual async Task<GetPermissionListResultDto> GetAsync(string providerName, string providerKey)
	{
		await CheckProviderPolicy(providerName);

		var result = new GetPermissionListResultDto
		{
			EntityDisplayName = providerKey,
			Groups = new List<PermissionGroupDto>()
		};

		var multiTenancySide = CurrentTenant.GetMultiTenancySide();

		foreach (var group in await PermissionDefinitionManager.GetGroupsAsync())
		{
			var groupDto = CreatePermissionGroupDto(group);

			var neededCheckPermissions = new List<PermissionDefinition>();

			var permissions = group.GetPermissionsWithChildren()
				.Where(x => x.IsEnabled)
				.Where(x => !x.Providers.Any() || x.Providers.Contains(providerName))
				.Where(x => x.MultiTenancySide.HasFlag(multiTenancySide));

			foreach (var permission in permissions)
			{
				if (permission.Parent != null && !neededCheckPermissions.Contains(permission.Parent))
				{
					continue;
				}

				if (await SimpleStateCheckerManager.IsEnabledAsync(permission))
				{
					neededCheckPermissions.Add(permission);
				}
			}

			if (!neededCheckPermissions.Any())
			{
				continue;
			}

			var grantInfoDtos = neededCheckPermissions
				.Select(CreatePermissionGrantInfoDto)
				.ToList();

			var multipleGrantInfo = await PermissionManager.GetAsync(neededCheckPermissions.Select(x => x.Name).ToArray(), providerName, providerKey);

			foreach (var grantInfo in multipleGrantInfo.Result)
			{
				var grantInfoDto = grantInfoDtos.First(x => x.Name == grantInfo.Name);

				grantInfoDto.IsGranted = grantInfo.IsGranted;

				foreach (var provider in grantInfo.Providers)
				{
					grantInfoDto.GrantedProviders.Add(new ProviderInfoDto
					{
						ProviderName = provider.Name,
						ProviderKey = provider.Key,
					});
				}

				groupDto.Permissions.Add(grantInfoDto);
			}

			if (groupDto.Permissions.Any())
			{
				result.Groups.Add(groupDto);
			}
		}

		return result;
	}

	private PermissionGrantInfoDto CreatePermissionGrantInfoDto(PermissionDefinition permission)
	{
		return new PermissionGrantInfoDto
		{
			Name = permission.Name,
			DisplayName = permission.DisplayName?.Localize(StringLocalizerFactory),
			ParentName = permission.Parent?.Name,
			AllowedProviders = permission.Providers,
			GrantedProviders = new List<ProviderInfoDto>()
		};
	}

	private PermissionGroupDto CreatePermissionGroupDto(PermissionGroupDefinition group)
	{
		var localizableDisplayName = group.DisplayName as LocalizableString;

		return new PermissionGroupDto
		{
			Name = group.Name,
			DisplayName = group.DisplayName?.Localize(StringLocalizerFactory),
			DisplayNameKey = localizableDisplayName?.Name,
			DisplayNameResource = localizableDisplayName?.ResourceType != null
				? LocalizationResourceNameAttribute.GetName(localizableDisplayName.ResourceType)
				: null,
			Permissions = new List<PermissionGrantInfoDto>()
		};
	}

	public virtual async Task UpdateAsync(string providerName, string providerKey, UpdatePermissionsDto input)
	{
		await CheckProviderPolicy(providerName);

		foreach (var permissionDto in input.Permissions)
		{
			await PermissionManager.SetAsync(permissionDto.Name, providerName, providerKey, permissionDto.IsGranted);
		}
	}

	protected virtual async Task CheckProviderPolicy(string providerName)
	{
		var policyName = Options.ProviderPolicies.GetOrDefault(providerName);
		if (policyName.IsNullOrEmpty())
		{
			throw new AbpException($"No policy defined to get/set permissions for the provider '{providerName}'. Use {nameof(PermissionManagementOptions)} to map the policy.");
		}

		await AuthorizationService.CheckAsync(policyName);
	}

	public virtual async Task<GetNestedPermissionListResultDto> GetNestedAsync(string providerName, string providerKey)
	{
		await CheckProviderPolicy(providerName);

		var result = new GetNestedPermissionListResultDto
		{
			EntityDisplayName = providerKey
		};

		var multiTenancySide = CurrentTenant.GetMultiTenancySide();

		// Gather all permission definitions once
		var allGroups = await PermissionDefinitionManager.GetGroupsAsync();

		// Prepare to fetch all necessary permission names at once
		var allPermissionDefinitions = new List<PermissionDefinition>();

		foreach (var group in allGroups)
		{
			var definitions = group
				.GetPermissionsWithChildren()
				.Where(x => x.IsEnabled)
				.Where(x => !x.Providers.Any() || x.Providers.Contains(providerName))
				.Where(x => x.MultiTenancySide.HasFlag(multiTenancySide))
				.ToList();

			allPermissionDefinitions.AddRange(definitions);
		}

		// Remove duplicates
		allPermissionDefinitions = allPermissionDefinitions
			.GroupBy(x => x.Name)
			.Select(x => x.First())
			.ToList();

		// Query the grant info for these permissions
		var multipleGrantInfo = await PermissionManager.GetAsync(
			allPermissionDefinitions.Select(x => x.Name).ToArray(),
			providerName,
			providerKey
		);

		// Build a dictionary for quick lookup of grant results
		Dictionary<string, PermissionWithGrantedProviders>? grantLookup = multipleGrantInfo.Result.ToDictionary(x => x.Name, x => x);

		// Build the final result
		foreach (var group in allGroups)
		{
			var groupDto = new NestedPermissionGroupDto
			{
				Name = group.Name,
				DisplayName = group.DisplayName?.Localize(StringLocalizerFactory),
				DisplayNameKey = (group.DisplayName as LocalizableString)?.Name,
				DisplayNameResource = (group.DisplayName as LocalizableString)?.ResourceType != null
					? LocalizationResourceNameAttribute.GetName((group.DisplayName as LocalizableString)?.ResourceType)
					: null
			};

			// Get the permission definitions relevant to this group
			var groupPermissionDefinitions = group
				.GetPermissionsWithChildren()
				.Where(x => x.IsEnabled)
				.Where(x => !x.Providers.Any() || x.Providers.Contains(providerName))
				.Where(x => x.MultiTenancySide.HasFlag(multiTenancySide))
				.ToList();

			// We only process top-level permissions here (Parent == null) or 
			// those with a parent that is not in this group. 
			// If you want to strictly nest within each group, we limit the nesting to that group's set.
			var topLevelPermissions = groupPermissionDefinitions
				.Where(p => p.Parent == null || !groupPermissionDefinitions.Contains(p.Parent))
				.ToList();

			foreach (var permission in topLevelPermissions)
			{
				// Build the node and its children recursively
				var permissionDto = BuildPermissionWithChildrenDto(permission, groupPermissionDefinitions, grantLookup);
				if (permissionDto != null)
				{
					groupDto.Permissions.Add(permissionDto);
				}
			}

			if (groupDto.Permissions.Any())
			{
				result.Groups.Add(groupDto);
			}
		}

		return result;
	}

	private PermissionWithChildrenDto? BuildPermissionWithChildrenDto(
		PermissionDefinition permission,
		List<PermissionDefinition> groupPermissions,
		Dictionary<string, PermissionWithGrantedProviders> grantLookup)
	{
		// If this permission is not in the dictionary, we skip it
		if (!grantLookup.TryGetValue(permission.Name, out var grantInfo))
		{
			// Not granted or not found, can decide if you want to include it or not
			// Return null or build with IsGranted = false
			return null;
		}

		var permissionDto = new PermissionWithChildrenDto
		{
			Name = permission.Name,
			DisplayName = permission.DisplayName?.Localize(StringLocalizerFactory),
			IsGranted = grantInfo.IsGranted
		};

		foreach (var provider in grantInfo.Providers)
		{
			permissionDto.GrantedProviders.Add(new ProviderInfoDto
			{
				ProviderName = provider.Name,
				ProviderKey = provider.Key
			});
		}

		// Find children in the same group
		var childPermissions = groupPermissions
			.Where(p => p.Parent == permission)
			.ToList();

		foreach (var child in childPermissions)
		{
			var childDto = BuildPermissionWithChildrenDto(child, groupPermissions, grantLookup);
			if (childDto != null)
			{
				permissionDto.Children.Add(childDto);
			}
		}

		return permissionDto;
	}
}