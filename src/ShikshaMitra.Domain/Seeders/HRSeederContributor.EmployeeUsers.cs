using ShikshaMitra.HR.Employees;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<Guid> CreateUserForEmployeeAsync(
		string firstName,
		string lastName,
		string email,
		string mobileNo,
		string roleName)
	{
		if (string.IsNullOrWhiteSpace(mobileNo) || mobileNo.Length < 4)
		{
			throw new Exception($"Invalid mobile number for employee {firstName} {lastName}");
		}

		var existingUserByEmail = await FindUserByEmailAsync(email);
		if (existingUserByEmail != null)
		{
			await UpdateSeededEmployeeUserAsync(existingUserByEmail, firstName, lastName, email, mobileNo);
			await EnsureUserInRoleAsync(existingUserByEmail, roleName);
			return existingUserByEmail.Id;
		}

		var username = await GenerateUniqueUsernameAsync(firstName, lastName);

		var user = new IdentityUser(
			GuidGenerator.Create(),
			username,
			email,
			CurrentTenant.Id
		);

		user.Name = firstName;
		user.Surname = lastName;
		user.SetEmailConfirmed(true);
		user.SetPhoneNumber(mobileNo, false);

		var userCreateResult = await _userManager.CreateAsync(user, TempPassword);
		if (!userCreateResult.Succeeded)
		{
			throw new Exception(
				"Failed to create user for seeding: " +
				string.Join(", ", userCreateResult.Errors.Select(e => e.Description))
			);
		}

		var createdUser = await _userRepository.FindAsync(user.Id);
		if (createdUser == null)
		{
			throw new Exception("User was not created successfully during seeding.");
		}

		await EnsureUserInRoleAsync(createdUser, roleName);

		return user.Id;
	}
}
