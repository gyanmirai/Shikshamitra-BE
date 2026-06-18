using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class UserSeederContributor : BaseSeederContributor
{
	private const string TempPassword = "Temp@123";

	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly ILookupNormalizer _lookupNormalizer;

	public UserSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IdentityUserManager userManager,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		ILookupNormalizer lookupNormalizer)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_userManager = userManager;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_lookupNormalizer = lookupNormalizer;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var tenantId = CurrentTenant.Id;
		var users = BuildUserSpecs();

		foreach (var userInfo in users)
		{
			var existingUser = await FindExistingUserAsync(userInfo);
			if (existingUser != null)
			{
				await UpdateExistingUserAsync(existingUser, userInfo);
				continue;
			}

			var user = new IdentityUser(
				GuidGenerator.Create(),
				userInfo.UserName,
				userInfo.Email,
				tenantId
			);

			user.Name = userInfo.Name;
			user.Surname = userInfo.Surname;
			user.SetEmailConfirmed(true);

			var result = await _userManager.CreateAsync(user, TempPassword);
			if (!result.Succeeded)
			{
				// Another seeder may have created the same user between existence check and create.
				// Keep seeding idempotent by continuing.
				continue;
			}

			await EnsureRoleMembershipAsync(user, userInfo.RoleName);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	internal static IReadOnlyList<string> BuildSeededLoginEmails() =>
		BuildUserSpecs()
			.Select(x => x.Email)
			.ToList();

	private static IReadOnlyList<BaselineUserSpec> BuildUserSpecs() =>
		new List<BaselineUserSpec>
		{
			new("admin-ddvs", "admin-ddvs@ddvs.com", "Admin", "DDVS", "admin", ["admin1"], ["admin1@gmail.com", "ddvs@gmail.com"]),
			new("admin-ops", "admin-ops@ddvs.com", "Admin", "Operations", "admin", ["admin2"], ["admin2@ddvs.com"]),
			new("teacher-demo-1", "teacher-demo-1@ddvs.com", "Teacher", "Demo 1", "Teacher", ["teacher1"], ["teacher1@school.com"]),
			new("teacher-demo-2", "teacher-demo-2@ddvs.com", "Teacher", "Demo 2", "Teacher", ["teacher2"], ["teacher2@school.com"]),
			new("principal-demo-gseb", "principal-demo-gseb@ddvs.com", "Principal", "Demo GSEB", "Principal", ["principal"], ["principal@school.com"]),
			new("hr-manager", "hr-manager@ddvs.com", "HR", "Manager", "HR Manager", ["hr"], ["hr@school.com"])
		};

	private async Task<IdentityUser?> FindExistingUserAsync(BaselineUserSpec userInfo)
	{
		var emails = new[] { userInfo.Email }.Concat(userInfo.AliasEmails);
		foreach (var email in emails)
		{
			var user = await _userRepository.FindByNormalizedEmailAsync(_lookupNormalizer.NormalizeEmail(email));
			if (user != null)
			{
				return user;
			}
		}

		var userNames = new[] { userInfo.UserName }.Concat(userInfo.AliasUserNames);
		foreach (var userName in userNames)
		{
			var user = await _userRepository.FindByNormalizedUserNameAsync(_lookupNormalizer.NormalizeName(userName));
			if (user != null)
			{
				return user;
			}
		}

		return null;
	}

	private async Task UpdateExistingUserAsync(IdentityUser user, BaselineUserSpec userInfo)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;

		if (!string.Equals(managedUser.UserName, userInfo.UserName, System.StringComparison.Ordinal))
		{
			var setUserNameResult = await _userManager.SetUserNameAsync(managedUser, userInfo.UserName);
			if (!setUserNameResult.Succeeded)
			{
				throw new System.Exception(
					$"Failed to update username for seeded user '{userInfo.Email}': " +
					string.Join(", ", setUserNameResult.Errors.Select(e => e.Description)));
			}
		}

		if (!string.Equals(managedUser.Email, userInfo.Email, System.StringComparison.OrdinalIgnoreCase))
		{
			var setEmailResult = await _userManager.SetEmailAsync(managedUser, userInfo.Email);
			if (!setEmailResult.Succeeded)
			{
				throw new System.Exception(
					$"Failed to update email for seeded user '{userInfo.Email}': " +
					string.Join(", ", setEmailResult.Errors.Select(e => e.Description)));
			}
		}

		var userChanged = false;
		if (!string.Equals(managedUser.Name, userInfo.Name, System.StringComparison.Ordinal))
		{
			managedUser.Name = userInfo.Name;
			userChanged = true;
		}

		if (!string.Equals(managedUser.Surname, userInfo.Surname, System.StringComparison.Ordinal))
		{
			managedUser.Surname = userInfo.Surname;
			userChanged = true;
		}

		if (!managedUser.EmailConfirmed)
		{
			managedUser.SetEmailConfirmed(true);
			userChanged = true;
		}

		if (userChanged)
		{
			var updateResult = await _userManager.UpdateAsync(managedUser);
			if (!updateResult.Succeeded)
			{
				throw new System.Exception(
					$"Failed to update seeded user '{userInfo.Email}': " +
					string.Join(", ", updateResult.Errors.Select(e => e.Description)));
			}
		}

		await EnsureRoleMembershipAsync(managedUser, userInfo.RoleName);
	}

	private async Task EnsureRoleMembershipAsync(IdentityUser user, string roleName)
	{
		var normalizedRoleName = _lookupNormalizer.NormalizeName(roleName);
		var role = await _roleRepository.FindByNormalizedNameAsync(normalizedRoleName);
		if (role == null || string.IsNullOrWhiteSpace(role.Name))
		{
			throw new System.Exception(
				$"Required seeded role '{roleName}' was not found while assigning baseline user '{user.Email}'.");
		}

		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		if (await _userManager.IsInRoleAsync(managedUser, role.Name))
		{
			return;
		}

		var addResult = await _userManager.AddToRoleAsync(managedUser, role.Name);
		if (!addResult.Succeeded)
		{
			throw new System.Exception(
				$"Failed to assign role '{role.Name}' to baseline user '{managedUser.Email}': " +
				string.Join(", ", addResult.Errors.Select(e => e.Description)));
		}
	}

	private sealed record BaselineUserSpec(
		string UserName,
		string Email,
		string Name,
		string Surname,
		string RoleName,
		IReadOnlyList<string> AliasUserNames,
		IReadOnlyList<string> AliasEmails);
}
