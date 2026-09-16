using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<IdentityUser?> FindUserByEmailAsync(string email)
	{
		var normalizedEmail = _lookupNormalizer.NormalizeEmail(email);
		return await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
	}

	private async Task EnsureUserInRoleAsync(IdentityUser user, string roleName)
	{
		var role = await FindRoleSeedInfoAsync(roleName);
		if (role == null || string.IsNullOrWhiteSpace(role.Name))
		{
			return;
		}

		var isInRole = await _userManager.IsInRoleAsync(user, role.Name);
		if (isInRole)
		{
			return;
		}

		var addRoleResult = await _userManager.AddToRoleAsync(user, role.Name);
		if (!addRoleResult.Succeeded)
		{
			throw new Exception(
				$"Failed to assign role '{role.Name}' to seeded user '{user.UserName}': " +
				string.Join(", ", addRoleResult.Errors.Select(e => e.Description))
			);
		}
	}

	private async Task<SeedRoleInfo?> FindRoleSeedInfoAsync(string roleName)
	{
		var normalizedRoleName = _lookupNormalizer.NormalizeName(roleName);
		_roleSeedInfoByNormalizedName ??= (await _identityRoleRepository.GetQueryableAsync())
			.Select(role => new SeedRoleInfo(role.Id, role.Name))
			.AsEnumerable()
			.Where(role => !string.IsNullOrWhiteSpace(role.Name))
			.ToDictionary(
				role => _lookupNormalizer.NormalizeName(role.Name),
				role => role,
				StringComparer.OrdinalIgnoreCase);

		return _roleSeedInfoByNormalizedName.GetValueOrDefault(normalizedRoleName);
	}

	private sealed record SeedRoleInfo(Guid Id, string Name);
}
