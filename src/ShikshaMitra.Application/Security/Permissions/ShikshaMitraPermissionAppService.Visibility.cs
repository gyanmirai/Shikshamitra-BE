using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.Permissions.Exceptions;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Security.Permissions;

public partial class ShikshaMitraPermissionAppService
{
	private async Task ValidateVisibilityPrerequisitesAsync(string providerName, string providerKey,
		UpdatePermissionDto[] permissions, UserPermissionOverrideDto[]? overrides)
	{
		var requests = permissions.Where(item => item.IsGranted)
			.Select(item => (item.Name, Paths: NavigationVisibilityPrerequisites.GetPaths(item.Name)))
			.Where(item => item.Paths.Length > 0).ToArray();
		if (requests.Length == 0) return;
		var requiredNames = requests.SelectMany(item => item.Paths).SelectMany(path => path)
			.Distinct(StringComparer.Ordinal).ToArray();
		var available = new HashSet<string>(StringComparer.Ordinal);
		foreach (var name in requiredNames)
		{
			var definition = await PermissionDefinitionManager.GetOrNullAsync(name);
			var enabled = definition?.MultiTenancySide.HasFlag(CurrentTenant.GetMultiTenancySide()) == true;
			for (var current = definition; current != null && enabled; current = current.Parent)
				enabled = current.IsEnabled && await SimpleStateCheckerManager.IsEnabledAsync(current);
			if (enabled) available.Add(name);
		}
		var grants = available.Count == 0 ? null : (await PermissionManager.GetAsync(available.ToArray(), providerName, providerKey))
			.Result.ToDictionary(grant => grant.Name, StringComparer.Ordinal);
		var denied = providerName == "U" ? await _denies.GetDeniedAsync(Guid.Parse(providerKey)) : [];
		var pending = overrides != null
			? overrides.ToDictionary(item => item.Name, item => item.State, StringComparer.Ordinal)
			: permissions.ToDictionary(item => item.Name, item => item.IsGranted ? "allow" : "inherit", StringComparer.Ordinal);
		bool Visible(string name)
		{
			if (!available.Contains(name)) return false;
			if (pending.TryGetValue(name, out var state))
			{
				if (state == "allow") return true;
				if (state == "deny") return false;
				return grants![name].Providers.Any(source => source.Name != providerName || source.Key != providerKey);
			}
			return !denied.Contains(name) && grants![name].IsGranted;
		}
		foreach (var request in requests)
			if (!request.Paths.Any(path => path.All(Visible)))
				throw new VisibilityPrerequisiteMissingException(request.Name);
	}
}
