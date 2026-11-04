using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.Permissions;

public class AdminPermissionAccess(IUserScopeResolutionRepository identities, IUnitOfWorkManager units) : ITransientDependency
{
	public const string RoleName = "admin";

	public async Task<bool> IsAdminAsync(Guid userId) =>
		(await GetRolesAsync(userId))?.Contains(RoleName, StringComparer.OrdinalIgnoreCase) == true;

	public async Task<List<string>?> GetRolesAsync(Guid userId)
	{
		using var owned = units.Current == null ? units.Begin(requiresNew: true, isTransactional: false) : null;
		var roles = await identities.GetRoleNamesAsync(userId);
		if (owned != null) await owned.CompleteAsync();
		return roles;
	}

	public Task<bool> IsAdminAsync(ClaimsPrincipal? principal) =>
		TryGetUserId(principal, out var userId) ? IsAdminAsync(userId) : Task.FromResult(false);

	public static bool TryGetUserId(ClaimsPrincipal? principal, out Guid userId) =>
		Guid.TryParse(principal?.FindFirst(AbpClaimTypes.UserId)?.Value ??
			principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
}
