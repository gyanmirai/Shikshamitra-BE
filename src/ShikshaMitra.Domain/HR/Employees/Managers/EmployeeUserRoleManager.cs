using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Data;
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
	public async Task<IdentityRole> EnsureUserInRoleAsync(
		Guid userId,
		Guid roleId,
		EmployeeGroupType? employeeGroup = null)
	{
		var user = await GetUserAsync(userId);
		var role = await EnsureRoleIsAssignableAsync(roleId, employeeGroup);

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

	public async Task SwapRoleAsync(
		Guid userId,
		Guid currentRoleId,
		Guid newRoleId,
		EmployeeGroupType? employeeGroup = null)
	{
		var user = await GetUserAsync(userId);
		var oldRole = await GetRoleAsync(currentRoleId);
		var newRole = await EnsureRoleIsAssignableAsync(newRoleId, employeeGroup);

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

	public async Task<IdentityRole> EnsureRoleIsAssignableAsync(
		Guid roleId,
		EmployeeGroupType? employeeGroup)
	{
		var role = await GetRoleAsync(roleId);
		EnsureEmployeeAssignableRole(role);
		EnsureEmployeeGroupAssignableRole(role, employeeGroup);
		return role;
	}

	public async Task EnsureExistingRoleIsCompatibleAsync(
		Guid roleId,
		EmployeeGroupType? employeeGroup)
	{
		var role = await GetRoleAsync(roleId);
		EnsureEmployeeGroupAssignableRole(role, employeeGroup);
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

	private static void EnsureEmployeeAssignableRole(IdentityRole role)
	{
		var roleName = GetRoleName(role).Trim();
		var audience = role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName);
		if (!string.Equals(audience, EmployeeRoleAudience.Employee, StringComparison.Ordinal))
		{
			throw new BusinessException(
				HRErrorCodes.EmployeeRoleNotAllowed,
				"Only roles with the Employee audience can be assigned to employees.")
				.WithData("roleId", role.Id)
				.WithData("roleAudience", audience ?? EmployeeRoleAudience.ForRoleName(roleName));
		}

		if (EmployeeOnboardingRoleAssignment.IsReservedSystemRole(roleName) ||
			!role.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName))
		{
			throw new BusinessException(
				HRErrorCodes.EmployeeRoleAssignmentNotAllowed,
				"The selected role is not explicitly approved for employee onboarding.")
				.WithData("roleId", role.Id)
				.WithData("roleName", roleName);
		}
	}

	private static void EnsureEmployeeGroupAssignableRole(
		IdentityRole role,
		EmployeeGroupType? employeeGroup)
	{
		var groupAudience = role.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName);
		if (EmployeeGroupAudience.IsAssignableTo(groupAudience, employeeGroup))
		{
			return;
		}

		var roleName = GetRoleName(role).Trim();
		throw new BusinessException(
			HRErrorCodes.EmployeeRoleGroupNotAllowed,
			"The selected role is not available for the selected employee group.")
			.WithData("roleId", role.Id)
			.WithData("roleName", roleName)
			.WithData("employeeGroup", employeeGroup)
			.WithData(
				"employeeGroupAudience",
				EmployeeGroupAudience.ResolveOrDefault(groupAudience));
	}
}
