using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
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

[Authorize]
[RemoteService(false)]
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionAppService), typeof(IShikshaMitraPermissionAppService), typeof(ShikshaMitraPermissionAppService))]
public partial class ShikshaMitraPermissionAppService : ApplicationService, IPermissionAppService, IShikshaMitraPermissionAppService
{
	private readonly IIdentityRoleRepository _roles;
	private readonly IIdentityUserRepository _users;
	private readonly IUnitOfWorkManager _unitOfWorkManager;
	private readonly IServiceProvider _services;
	private readonly IUserPermissionDenyStore _denies;
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
		ISimpleStateCheckerManager<PermissionDefinition> simpleStateCheckerManager,
		IIdentityRoleRepository roles,
		IIdentityUserRepository users,
		IUnitOfWorkManager unitOfWorkManager,
		IServiceProvider services,
		IUserPermissionDenyStore denies)
	{
		_roles = roles;
		_users = users;
		_unitOfWorkManager = unitOfWorkManager;
		_services = services;
		_denies = denies;
		Options = options.Value;
		PermissionManager = permissionManager;
		PermissionDefinitionManager = permissionDefinitionManager;
		PermissionChecker = permissionChecker;
		CurrentPrincipalAccessor = currentPrincipalAccessor;
		SimpleStateCheckerManager = simpleStateCheckerManager;
	}

	public virtual async Task<GetPermissionListResultDto> GetAsync(string providerName, string providerKey)
	{
		var timer = Stopwatch.StartNew();
		var isCurrentUserRead = IsCurrentUserProvider(providerName, providerKey);

		await CheckProviderPolicy(providerName, providerKey, allowCurrentUserRead: true);
		var policyMs = timer.ElapsedMilliseconds;

		var result = new GetPermissionListResultDto
		{
			EntityDisplayName = providerKey,
			Groups = new List<PermissionGroupDto>()
		};

		var multiTenancySide = CurrentTenant.GetMultiTenancySide();
		var enabledGroups = new List<(PermissionGroupDto Group, List<PermissionGrantInfoDto> Permissions)>();
		long localizationTicks = 0;
		var groups = await PermissionDefinitionManager.GetGroupsAsync();
		var stateDefinitions = new HashSet<PermissionDefinition>();
		foreach (var permission in groups.SelectMany(group => group.GetPermissionsWithChildren()))
		{
			if (!permission.MultiTenancySide.HasFlag(multiTenancySide)) continue;
			for (var current = permission; current != null; current = current.Parent)
			{
				if (!current.IsEnabled) break;
				stateDefinitions.Add(current);
			}
		}
		var stateResults = await SimpleStateCheckerManager.IsEnabledAsync(stateDefinitions.ToArray());

		foreach (var group in groups)
		{
			var localizationStarted = Stopwatch.GetTimestamp();
			var groupDto = CreatePermissionGroupDto(group);
			localizationTicks += Stopwatch.GetTimestamp() - localizationStarted;

			var neededCheckPermissions = new List<PermissionDefinition>();

			var permissions = group.GetPermissionsWithChildren();

			foreach (var permission in permissions)
			{
				var enabled = permission.MultiTenancySide.HasFlag(multiTenancySide);
				for (var current = permission; current != null && enabled; current = current.Parent)
					enabled = current.IsEnabled && stateResults[current];
				if (enabled)
				{
					neededCheckPermissions.Add(permission);
				}
			}

			if (!neededCheckPermissions.Any())
			{
				continue;
			}

			localizationStarted = Stopwatch.GetTimestamp();
			var grantInfoDtos = neededCheckPermissions
				.Select(CreatePermissionGrantInfoDto)
				.ToList();
			localizationTicks += Stopwatch.GetTimestamp() - localizationStarted;

			enabledGroups.Add((groupDto, grantInfoDtos));
		}

		var denied = providerName == "U" ? await _denies.GetDeniedAsync(Guid.Parse(providerKey)) : new HashSet<string>();
		var permissionNames = enabledGroups.SelectMany(group => group.Permissions).Select(permission => permission.Name).ToArray();
		if (permissionNames.Length == 0) return result;
		var catalogueMs = timer.ElapsedMilliseconds - policyMs;

		var effectiveGrantLookup = isCurrentUserRead
			? (await PermissionChecker.IsGrantedAsync(permissionNames)).Result
			: null;
		var effectiveMs = timer.ElapsedMilliseconds - policyMs - catalogueMs;
		var grantInfoLookup = (await PermissionManager.GetAsync(permissionNames, providerName, providerKey))
			.Result.ToDictionary(grant => grant.Name, StringComparer.Ordinal);
		var grantSourcesMs = timer.ElapsedMilliseconds - policyMs - catalogueMs - effectiveMs;

		foreach (var (groupDto, grantInfoDtos) in enabledGroups)
		{
			foreach (var grantInfoDto in grantInfoDtos)
			{
				var grantInfo = grantInfoLookup[grantInfoDto.Name];
				grantInfoDto.IsGranted = !denied.Contains(grantInfoDto.Name) && (effectiveGrantLookup != null
					? effectiveGrantLookup[grantInfoDto.Name] == PermissionGrantResult.Granted
					: grantInfo.IsGranted);

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

			result.Groups.Add(groupDto);
		}
		if (timer.ElapsedMilliseconds >= 1000)
			Logger.LogWarning(
				"Permission catalogue read took {TotalMs} ms: policy={PolicyMs}, catalogue={CatalogueMs}, localization={LocalizationMs}, effective={EffectiveMs}, grantSources={GrantSourcesMs}, groups={GroupCount}, permissions={PermissionCount}, selfRead={SelfRead}",
				timer.ElapsedMilliseconds, policyMs, catalogueMs,
				TimeSpan.FromSeconds(localizationTicks / (double)Stopwatch.Frequency).TotalMilliseconds,
				effectiveMs, grantSourcesMs, result.Groups.Count, permissionNames.Length, isCurrentUserRead);

		return result;
	}

	private PermissionGrantInfoDto CreatePermissionGrantInfoDto(PermissionDefinition permission)
	{
		return new PermissionGrantInfoDto
		{
			Name = permission.Name,
			DisplayName = (permission.DisplayName?.Localize(StringLocalizerFactory))!,
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
			DisplayName = (group.DisplayName?.Localize(StringLocalizerFactory))!,
			DisplayNameKey = localizableDisplayName?.Name,
			DisplayNameResource = localizableDisplayName?.ResourceType != null
				? LocalizationResourceNameAttribute.GetName(localizableDisplayName.ResourceType)
				: null,
			Permissions = new List<PermissionGrantInfoDto>()
		};
	}

	public virtual Task UpdateAsync(string providerName, string providerKey, UpdatePermissionsDto input) =>
		UpdatePermissionsAsync(providerName, providerKey, input);

	private async Task UpdatePermissionsAsync(string providerName, string providerKey, UpdatePermissionsDto input,
		UserPermissionOverrideDto[]? overrides = null)
	{
		// Reuse the request transaction. A direct invocation still owns a transaction when needed.
		using var ownedTransaction = _unitOfWorkManager.Current?.Options.IsTransactional == true
			? null : _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var transaction = ownedTransaction ?? _unitOfWorkManager.Current!;
		try
		{
			await CheckProviderPolicy(providerName, providerKey);
			if (input?.Permissions == null || input.Permissions.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name)) ||
				input.Permissions.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != input.Permissions.Length)
				throw InvalidPermissions();

			// Validate the complete batch before the first write. Never infer an action from a CRUD alias.
			foreach (var item in input.Permissions)
			{
				var definition = await PermissionDefinitionManager.GetOrNullAsync(item.Name);
				if (definition == null || definition.Name != item.Name ||
					!definition.MultiTenancySide.HasFlag(CurrentTenant.GetMultiTenancySide()) ||
					(definition.Providers.Any() && !definition.Providers.Contains(providerName)))
					throw InvalidPermissions();
				for (var current = definition; current != null; current = current.Parent)
					if (!current.IsEnabled || !await SimpleStateCheckerManager.IsEnabledAsync(current))
						throw InvalidPermissions();
			}
			await ValidateVisibilityPrerequisitesAsync(providerName, providerKey, input.Permissions, overrides);
			// ABP 9 PermissionManager.SetAsync compares effective access, so an inherited grant can
			// suppress a requested direct grant. Use its registered management provider after validation.
			var provider = Options.ManagementProviders
				.Select(type => (IPermissionManagementProvider)_services.GetRequiredService(type))
				.SingleOrDefault(candidate => candidate.Name == providerName)
				?? throw new AbpException("The permission management provider is unavailable.");
			foreach (var item in input.Permissions)
			{
				await provider.SetAsync(item.Name, providerKey, item.IsGranted);
				if (providerName == "U") await _denies.SetDeniedAsync(Guid.Parse(providerKey), item.Name, false);
			}
			if (ownedTransaction != null) await ownedTransaction.CompleteAsync();
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}

	private static AbpValidationException InvalidPermissions() =>
		new("One or more permissions are invalid or no longer available.",
			new List<ValidationResult> { new("Choose permissions from the current catalogue.", new[] { "permissions" }) });

	private async Task ValidateProviderAsync(string providerName, string providerKey)
	{
		if (providerName == "U" && Guid.TryParse(providerKey, out var userId))
		{
			var user = await _users.FindAsync(userId);
			if (user == null || user.TenantId != CurrentTenant.Id)
				throw new EntityNotFoundException(typeof(IdentityUser), userId);
			return;
		}
		if (providerName == "R" && !string.IsNullOrWhiteSpace(providerKey))
		{
			var role = await _roles.FindByNormalizedNameAsync(providerKey.ToUpperInvariant());
			if (role == null || role.TenantId != CurrentTenant.Id || role.Name != providerKey)
				throw new EntityNotFoundException(typeof(IdentityRole), providerKey);
			return;
		}
		throw InvalidPermissions();
	}

	protected virtual async Task CheckProviderPolicy(
		string providerName,
		string providerKey,
		bool allowCurrentUserRead = false)
	{
		if (!CurrentUser.IsAuthenticated) throw new AbpAuthorizationException();
		if (providerName is not ("R" or "U")) throw InvalidPermissions();
		if (allowCurrentUserRead && IsCurrentUserProvider(providerName, providerKey))
		{
			await ValidateProviderAsync(providerName, providerKey);
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
		await ValidateProviderAsync(providerName, providerKey);
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
		// Both response shapes use the same validated catalogue and grant-source calculation.
		var flat = await GetAsync(providerName, providerKey);
		return new GetNestedPermissionListResultDto
		{
			EntityDisplayName = flat.EntityDisplayName,
			Groups = flat.Groups.Select(group =>
			{
				var nodes = group.Permissions.ToDictionary(p => p.Name, p => new PermissionWithChildrenDto
				{
					Name = p.Name, DisplayName = p.DisplayName, IsGranted = p.IsGranted,
					GrantedProviders = p.GrantedProviders
				});
				var roots = new List<PermissionWithChildrenDto>();
				foreach (var permission in group.Permissions)
				{
					if (permission.ParentName != null && nodes.TryGetValue(permission.ParentName, out var parent))
						parent.Children.Add(nodes[permission.Name]);
					else roots.Add(nodes[permission.Name]);
				}
				return new NestedPermissionGroupDto
				{
					Name = group.Name, DisplayName = group.DisplayName,
					DisplayNameKey = group.DisplayNameKey, DisplayNameResource = group.DisplayNameResource,
					Permissions = roots
				};
			}).ToList()
		};
	}
}
