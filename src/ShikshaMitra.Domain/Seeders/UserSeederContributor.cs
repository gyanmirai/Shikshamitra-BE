using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class UserSeederContributor : BaseSeederContributor
{
	private const string SeedFlagKey = "SeedTestLogins";

	private readonly IConfiguration _configuration;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly ILookupNormalizer _lookupNormalizer;

	public UserSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IConfiguration configuration,
		IdentityUserManager userManager,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		ILookupNormalizer lookupNormalizer)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_configuration = configuration;
		_userManager = userManager;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_lookupNormalizer = lookupNormalizer;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// TestLoginSeederContributor already seeds these users when this flag is enabled.
		// Skipping here prevents duplicate username collisions (e.g. teacher1).
		if (_configuration.GetValue<bool>(SeedFlagKey, false))
		{
			return;
		}

		var tenantId = CurrentTenant.Id;

		var users = new List<(string UserName, string Email, string Name, string Surname, string RoleName)>
		{
			("teacher1", "teacher1@school.com", "John", "Doe", "Teacher"),
			("teacher2", "teacher2@school.com", "Jane", "Smith", "Teacher"),
			("principal", "principal@school.com", "Robert", "Johnson", "Principal"),
			("hr", "hr@school.com", "Sarah", "Williams", "HR Manager")
		};

		foreach (var userInfo in users)
		{
			var existingUser = await _userRepository.FindByNormalizedUserNameAsync(
				_lookupNormalizer.NormalizeName(userInfo.UserName)
			);
			if (existingUser != null)
			{
				await EnsureRoleMembershipAsync(existingUser, userInfo.RoleName);
				continue;
			}

			var existingByEmail = await _userRepository.FindByNormalizedEmailAsync(
				_lookupNormalizer.NormalizeEmail(userInfo.Email)
			);
			if (existingByEmail != null)
			{
				await EnsureRoleMembershipAsync(existingByEmail, userInfo.RoleName);
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

			var result = await _userManager.CreateAsync(user, "Temp@123");
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

	private async Task EnsureRoleMembershipAsync(IdentityUser user, string roleName)
	{
		var normalizedRoleName = _lookupNormalizer.NormalizeName(roleName);
		var role = await _roleRepository.FindByNormalizedNameAsync(normalizedRoleName);
		if (role == null || string.IsNullOrWhiteSpace(role.Name))
		{
			return;
		}

		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		if (await _userManager.IsInRoleAsync(managedUser, role.Name))
		{
			return;
		}

		await _userManager.AddToRoleAsync(managedUser, role.Name);
	}
}
