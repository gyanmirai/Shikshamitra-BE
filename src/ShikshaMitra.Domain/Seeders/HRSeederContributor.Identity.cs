using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<IdentityUser?> FindUserByEmailOrLegacyAliasAsync(string email)
	{
		var emails = new List<string> { email };
		if (email.EndsWith("@ddvs.com", StringComparison.OrdinalIgnoreCase))
		{
			emails.Add(email[..^"@ddvs.com".Length] + "@school.edu");
		}

		if (string.Equals(email, "nilesh.joshi@ddvs.com", StringComparison.OrdinalIgnoreCase))
		{
			emails.Add("clerk@school.edu");
		}

		foreach (var candidateEmail in emails.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var normalizedEmail = _lookupNormalizer.NormalizeEmail(candidateEmail);
			var user = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
			if (user != null)
			{
				return user;
			}
		}

		return null;
	}

	private async Task UpdateSeededEmployeeUserAsync(
		IdentityUser user,
		string firstName,
		string lastName,
		string email,
		string mobileNo)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;

		if (!string.Equals(managedUser.Email, email, StringComparison.OrdinalIgnoreCase))
		{
			var setEmailResult = await _userManager.SetEmailAsync(managedUser, email);
			if (!setEmailResult.Succeeded)
			{
				throw new Exception(
					$"Failed to update seeded HR user email '{email}': " +
					string.Join(", ", setEmailResult.Errors.Select(e => e.Description)));
			}
		}

		var userChanged = false;
		if (!string.Equals(managedUser.Name, firstName, StringComparison.Ordinal))
		{
			managedUser.Name = firstName;
			userChanged = true;
		}

		if (!string.Equals(managedUser.Surname, lastName, StringComparison.Ordinal))
		{
			managedUser.Surname = lastName;
			userChanged = true;
		}

		if (!managedUser.EmailConfirmed)
		{
			managedUser.SetEmailConfirmed(true);
			userChanged = true;
		}

		if (!string.Equals(managedUser.PhoneNumber, mobileNo, StringComparison.Ordinal) ||
		    managedUser.PhoneNumberConfirmed)
		{
			managedUser.SetPhoneNumber(mobileNo, false);
			userChanged = true;
		}

		if (userChanged)
		{
			var updateResult = await _userManager.UpdateAsync(managedUser);
			if (!updateResult.Succeeded)
			{
				throw new Exception(
					$"Failed to update seeded HR user '{email}': " +
					string.Join(", ", updateResult.Errors.Select(e => e.Description)));
			}
		}

		await ResetPasswordAsync(managedUser, TempPassword);
	}

	private async Task ResetPasswordAsync(IdentityUser user, string password)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		if (!string.IsNullOrWhiteSpace(managedUser.PasswordHash) &&
		    await _userManager.CheckPasswordAsync(managedUser, password))
		{
			return;
		}

		IdentityResult result;
		if (string.IsNullOrWhiteSpace(managedUser.PasswordHash))
		{
			result = await _userManager.AddPasswordAsync(managedUser, password);
		}
		else
		{
			var removeResult = await _userManager.RemovePasswordAsync(managedUser);
			if (!removeResult.Succeeded)
			{
				throw new Exception(
					$"Failed to remove existing password for seeded HR user '{managedUser.Email}': " +
					string.Join(", ", removeResult.Errors.Select(e => e.Description)));
			}

			result = await _userManager.AddPasswordAsync(managedUser, password);
		}

		if (!result.Succeeded)
		{
			throw new Exception(
				$"Failed to set password for seeded HR user '{managedUser.Email}': " +
				string.Join(", ", result.Errors.Select(e => e.Description)));
		}
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
