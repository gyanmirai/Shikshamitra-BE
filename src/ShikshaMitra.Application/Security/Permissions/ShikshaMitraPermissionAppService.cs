using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Volo.Abp.SimpleStateChecking;

namespace ShikshaMitra.Security.Permissions;

[RemoteService(false)]
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionAppService), typeof(IShikshaMitraPermissionAppService), typeof(ShikshaMitraPermissionAppService))]
public class ShikshaMitraPermissionAppService : ApplicationService, IPermissionAppService, IShikshaMitraPermissionAppService
{
	protected PermissionManagementOptions Options { get; }
	protected IPermissionManager PermissionManager { get; }
	protected IPermissionDefinitionManager PermissionDefinitionManager { get; }
	protected IPermissionChecker PermissionChecker { get; }
	protected ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }
	protected ISimpleStateCheckerManager<PermissionDefinition> SimpleStateCheckerManager { get; }

	public ShikshaMitraPermissionAppService(
		IPermissionManager permissionManager,
		IPermissionDefinitionManager permissionDefinitionManager,
		IOptions<PermissionManagementOptions> options,
		IPermissionChecker permissionChecker,
		ICurrentPrincipalAccessor currentPrincipalAccessor,
		ISimpleStateCheckerManager<PermissionDefinition> simpleStateCheckerManager)
	{
		Options = options.Value;
		PermissionManager = permissionManager;
		PermissionDefinitionManager = permissionDefinitionManager;
		PermissionChecker = permissionChecker;
		CurrentPrincipalAccessor = currentPrincipalAccessor;
		SimpleStateCheckerManager = simpleStateCheckerManager;
	}

	public virtual async Task<GetPermissionListResultDto> GetAsync(string providerName, string providerKey)
	{
		var isCurrentUserRead = IsCurrentUserProvider(providerName, providerKey);

		await CheckProviderPolicy(providerName, providerKey, allowCurrentUserRead: true);

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

			var permissionNames = neededCheckPermissions.Select(x => x.Name).ToArray();
			var effectiveGrantLookup = isCurrentUserRead
				? await GetCurrentUserEffectivePermissionLookupAsync(permissionNames)
				: null;
			var multipleGrantInfo = effectiveGrantLookup == null
				? await PermissionManager.GetAsync(permissionNames, providerName, providerKey)
				: null;

			foreach (var grantInfoDto in grantInfoDtos)
			{
				if (effectiveGrantLookup != null)
				{
					grantInfoDto.IsGranted = effectiveGrantLookup.GetValueOrDefault(grantInfoDto.Name);
					groupDto.Permissions.Add(grantInfoDto);
					continue;
				}

				var grantInfo = multipleGrantInfo!.Result.First(x => x.Name == grantInfoDto.Name);
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
		await CheckProviderPolicy(providerName, providerKey);

		foreach (var permissionDto in input.Permissions)
		{
			var permissionName = await ResolveCrudPermissionNameAsync(permissionDto.Name);
			if (permissionName == null)
			{
				continue;
			}

			await PermissionManager.SetAsync(permissionName, providerName, providerKey, permissionDto.IsGranted);
		}
	}

	private async Task<string?> ResolveCrudPermissionNameAsync(string permissionName)
	{
		if (await PermissionDefinitionManager.GetOrNullAsync(permissionName) != null)
		{
			return permissionName;
		}

		var separator = permissionName.LastIndexOf('.');
		if (separator < 0)
		{
			return null;
		}

		var baseName = permissionName[..separator];
		var action = permissionName[(separator + 1)..];
		var candidates = action switch
		{
			"Add" => new[] { $"{baseName}.Create", $"{baseName}.Edit" },
			"View" => new[] { baseName },
			_ => Array.Empty<string>()
		};

		foreach (var candidate in candidates)
		{
			if (await PermissionDefinitionManager.GetOrNullAsync(candidate) != null)
			{
				return candidate;
			}
		}

		return null;
	}

	protected virtual async Task CheckProviderPolicy(
		string providerName,
		string providerKey,
		bool allowCurrentUserRead = false)
	{
		if (allowCurrentUserRead && IsCurrentUserProvider(providerName, providerKey))
		{
			return;
		}

		if (allowCurrentUserRead &&
			providerName.Equals("U", StringComparison.OrdinalIgnoreCase) &&
			IsAuthenticatedUser())
		{
			return;
		}

		var policyName = Options.ProviderPolicies.GetOrDefault(providerName);
		if (policyName.IsNullOrEmpty())
		{
			throw new AbpException($"No policy defined to get/set permissions for the provider '{providerName}'. Use {nameof(PermissionManagementOptions)} to map the policy.");
		}

		await AuthorizationService.CheckAsync(policyName);

		if (!await PermissionChecker.IsGrantedAsync(policyName))
		{
			throw new AbpAuthorizationException();
		}
	}

	private async Task<Dictionary<string, bool>> GetCurrentUserEffectivePermissionLookupAsync(string[] permissionNames)
	{
		var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		foreach (var permissionName in permissionNames)
		{
			result[permissionName] = await PermissionChecker.IsGrantedAsync(permissionName);
		}

		return result;
	}

	private bool IsCurrentUserProvider(string providerName, string providerKey)
	{
		if (!providerName.Equals("U", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return GetCurrentUserIdClaimValues()
			.Any(userId => providerKey.Equals(userId, StringComparison.OrdinalIgnoreCase));
	}

	private IEnumerable<string> GetCurrentUserIdClaimValues()
	{
		if (CurrentUser.Id.HasValue)
		{
			yield return CurrentUser.Id.Value.ToString();
		}

		foreach (var userId in GetUserIdClaimValues(CurrentPrincipalAccessor.Principal))
		{
			yield return userId;
		}

	}

	private bool IsAuthenticatedUser() =>
		CurrentUser.IsAuthenticated ||
		(CurrentPrincipalAccessor.Principal?.Identity?.IsAuthenticated ?? false);

	private static IEnumerable<string> GetUserIdClaimValues(ClaimsPrincipal? principal)
	{
		foreach (var claimType in GetUserIdClaimTypes())
		{
			var value = principal?.FindFirst(claimType)?.Value;
			if (!value.IsNullOrWhiteSpace())
			{
				yield return value;
			}
		}
	}

	private static string[] GetUserIdClaimTypes() =>
	[
		AbpClaimTypes.UserId,
		ClaimTypes.NameIdentifier,
		"sub",
		"user_id",
		"userid",
		"uid",
		"nameid",
		"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
	];

	public virtual async Task<GetNestedPermissionListResultDto> GetNestedAsync(string providerName, string providerKey)
	{
		await CheckProviderPolicy(providerName, providerKey, allowCurrentUserRead: true);

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
				.Where(x => x.IsEnabled && (!x.Providers.Any() || x.Providers.Contains(providerName)) && x.MultiTenancySide.HasFlag(multiTenancySide))
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
