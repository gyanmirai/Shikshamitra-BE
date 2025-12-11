using System;
using System.Collections.Generic;
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

		// Define users (using auto-generated GUIDs to avoid conflicts)
		var users = new List<(string UserName, string Email, string Name, string Surname, string RoleName)>
		{
			("teacher1", "teacher1@school.com", "John", "Doe", "Teacher"),
			("teacher2", "teacher2@school.com", "Jane", "Smith", "Teacher"),
			("principal", "principal@school.com", "Robert", "Johnson", "Principal"),
			("hr", "hr@school.com", "Sarah", "Williams", "HR Manager")
		};

		foreach (var userInfo in users)
		{
			// Check if user already exists by username
			var existingUser = await _userRepository.FindByNormalizedUserNameAsync(_lookupNormalizer.NormalizeName(userInfo.UserName));
			if (existingUser != null)
			{
				continue;
			}

			// Create user with auto-generated ID
			var user = new IdentityUser(
				GuidGenerator.Create(),
				userInfo.UserName,
				userInfo.Email,
				tenantId
			);

			user.Name = userInfo.Name;
			user.Surname = userInfo.Surname;
			user.SetEmailConfirmed(true);

			// Use the manager to create user with password
			var result = await _userManager.CreateAsync(user, "Temp@123");
			if (!result.Succeeded)
			{
				continue;
			}

			// Assign role
			var role = await _roleRepository.FindByNormalizedNameAsync(_lookupNormalizer.NormalizeName(userInfo.RoleName));
			if (role != null)
			{
				await _userManager.AddToRoleAsync(user, role.Name);
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
