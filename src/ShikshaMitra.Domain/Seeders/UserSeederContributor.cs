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
		// Check if users already exist
		if (await _userRepository.GetCountAsync() > 1) // More than 1 (admin already exists)
		{
			return;
		}

		var tenantId = CurrentTenant.Id;

		// Define users with hardcoded GUIDs
		var users = new List<(Guid Id, string UserName, string Email, string Name, string Surname, string RoleName)>
		{
			(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "teacher1", "teacher1@school.com", "John", "Doe", "Teacher"),
			(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "teacher2", "teacher2@school.com", "Jane", "Smith", "Teacher"),
			(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "principal", "principal@school.com", "Robert", "Johnson", "Principal"),
			(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "hr", "hr@school.com", "Sarah", "Williams", "HR Manager")
		};

		foreach (var userInfo in users)
		{
			// Check if user already exists by username
			var existingUser = await _userRepository.FindByNormalizedUserNameAsync(_lookupNormalizer.NormalizeName(userInfo.UserName));
			if (existingUser != null)
			{
				continue;
			}

			// Create user with hardcoded ID using IdentityUserManager
			var user = new IdentityUser(
				userInfo.Id,
				userInfo.UserName,
				userInfo.Email,
				tenantId
			);

			user.Name = userInfo.Name;
			user.Surname = userInfo.Surname;

			// Use the manager to create user with password (this handles normalization and hashing)
			var result = await _userManager.CreateAsync(user, "Temp@123");
			if (!result.Succeeded)
			{
				continue;
			}

			// Confirm email
			user.SetEmailConfirmed(true);
			await _userRepository.UpdateAsync(user);

			// Assign role
			var role = await _roleRepository.FindByNormalizedNameAsync(_lookupNormalizer.NormalizeName(userInfo.RoleName));
			if (role != null)
			{
				user.AddRole(role.Id);
				await _userRepository.UpdateAsync(user);
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}
