using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeUserRoleManager(
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository,
	UserManager<IdentityUser> userManager,
	ILogger<EmployeeUserRoleManager> logger
) : DomainService
{
	public async Task<IdentityRole> EnsureUserInRoleAsync(Guid userId, Guid roleId)
	{
		var user = await GetUserAsync(userId);
		var role = await GetRoleAsync(roleId);

		logger.LogDebug(
			"[EmployeeUserRoleManager] Role validated: RoleId={RoleId}, RoleName={RoleName}",
			roleId,
			role.Name
		);

		var roleName = GetRoleName(role);
		var isAlreadyInRole = await userManager.IsInRoleAsync(user, roleName);
		if (isAlreadyInRole)
		{
			return role;
		}

		var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
		if (!addRoleResult.Succeeded)
		{
			var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
			logger.LogError(
				"[EmployeeUserRoleManager] Failed to assign role '{RoleName}' to user {UserId}. Errors: {Errors}",
				roleName,
				userId,
				errors
			);
			throw new BusinessException(HRErrorCodes.RoleAssignmentFailed)
				.WithData("details", errors);
		}

		return role;
	}

	public async Task SwapRoleAsync(Guid userId, Guid currentRoleId, Guid newRoleId)
	{
		var user = await GetUserAsync(userId);
		var oldRole = await GetRoleAsync(currentRoleId);
		var newRole = await GetRoleAsync(newRoleId);

		// Preserve the historical fail-fast role swap behavior used by employee updates.
		var removeResult = await userManager.RemoveFromRoleAsync(user, GetRoleName(oldRole));
		if (!removeResult.Succeeded)
		{
			throw new BusinessException(HRErrorCodes.RoleRemovalFailed);
		}

		var addResult = await userManager.AddToRoleAsync(user, GetRoleName(newRole));
		if (!addResult.Succeeded)
		{
			throw new BusinessException(HRErrorCodes.RoleAssignmentFailed);
		}
	}

	private async Task<IdentityUser> GetUserAsync(Guid userId)
	{
		return await userRepository.FindAsync(userId)
			?? throw new BusinessException(HRErrorCodes.UserNotFound);
	}

	private async Task<IdentityRole> GetRoleAsync(Guid roleId)
	{
		return await roleRepository.FindAsync(roleId)
			?? throw new BusinessException(HRErrorCodes.RoleNotFound);
	}

	private static string GetRoleName(IdentityRole role)
	{
		return role.Name ?? throw new BusinessException(HRErrorCodes.RoleNameMissing);
	}
}
