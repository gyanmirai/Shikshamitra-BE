using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class TestLoginSeederContributor : BaseSeederContributor
{
	private const string SeedFlagKey = "SeedTestLogins";
	private const string TempPassword = "Temp@123";
	private static readonly Guid DdvsTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private const string LinkedStudentEmail = "student1@ddvs.com";

	private readonly IConfiguration _configuration;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly ILookupNormalizer _lookupNormalizer;
	private readonly IStudentRepository _studentRepository;

	public TestLoginSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IConfiguration configuration,
		IdentityUserManager userManager,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		ILookupNormalizer lookupNormalizer,
		IStudentRepository studentRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_configuration = configuration;
		_userManager = userManager;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_lookupNormalizer = lookupNormalizer;
		_studentRepository = studentRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (!IsEnabled(context))
		{
			return;
		}

		if (CurrentTenant.Id != DdvsTenantId)
		{
			return;
		}

		var linkedStudentEmail = await EnsureLinkedStudentEmailAsync();

		var credentialSpecs = new List<TestLoginSpec>
		{
			new("admin1", "admin1@gmail.com", "Admin", "One", "admin", ["ddvs@gmail.com"], []),
			new("admin2", "admin2@ddvs.com", "Admin", "Two", "admin", [], []),
			new("principle1", "principle1@ddvs.com", "Principal", "User", "Principal", ["rajesh.kumar@school.edu"], []),
			new("teacher1", "teacher1@ddvs.com", "Teacher", "User", "Teacher", ["vikram.singh@school.edu"], []),
			new("clerk1", "clerk1@ddvs.com", "Clerk", "User", "Clerk", ["clerk@school.edu"], []),
			new("finance1", "finance1@ddvs.com", "Finance", "User", "Finance Officer", ["suresh.patil@school.edu"], [])
		};

		if (!string.IsNullOrWhiteSpace(linkedStudentEmail))
		{
			credentialSpecs.Add(new TestLoginSpec(
				"student1",
				linkedStudentEmail,
				"Student",
				"User",
				"Student",
				["student.demo@school.edu"],
				["studentdemo"]));
		}

		foreach (var spec in credentialSpecs)
		{
			await EnsureUserWithTempPasswordAsync(spec);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private bool IsEnabled(DataSeedContext context)
	{
		if (context.Properties.TryGetValue(SeedFlagKey, out var value) &&
		    TryConvertToBool(value, out var enabledFromContext))
		{
			return enabledFromContext;
		}

		return _configuration.GetValue<bool>(SeedFlagKey, false);
	}

	private static bool TryConvertToBool(object? value, out bool result)
	{
		switch (value)
		{
			case bool boolValue:
				result = boolValue;
				return true;
			case string stringValue when bool.TryParse(stringValue, out var parsed):
				result = parsed;
				return true;
			default:
				result = false;
				return false;
		}
	}

	private async Task<string?> EnsureLinkedStudentEmailAsync()
	{
		var students = await _studentRepository.GetListAsync();
		var student = students.Count > 0 ? students[0] : null;
		if (student?.ContactDetail == null)
		{
			return null;
		}

		if (!string.Equals(student.ContactDetail.Email, LinkedStudentEmail, StringComparison.OrdinalIgnoreCase))
		{
			student.ContactDetail.UpdateContactInfo(
				student.ContactDetail.PrimaryContactNo,
				student.ContactDetail.SecondaryContactNo,
				LinkedStudentEmail,
				student.ContactDetail.Address1,
				student.ContactDetail.Address2,
				student.ContactDetail.Address3,
				student.ContactDetail.City,
				student.ContactDetail.State,
				student.ContactDetail.Country,
				student.ContactDetail.Pincode);

			await _studentRepository.UpdateAsync(student, autoSave: true);
		}

		return LinkedStudentEmail;
	}

	private async Task EnsureUserWithTempPasswordAsync(TestLoginSpec spec)
	{
		var user = await FindUserAsync(spec);

		if (user == null)
		{
			user = new IdentityUser(
				GuidGenerator.Create(),
				spec.UserName,
				spec.Email,
				CurrentTenant.Id);
			user.Name = spec.FirstName;
			user.Surname = spec.LastName;
			user.SetEmailConfirmed(true);

			var createResult = await _userManager.CreateAsync(user, TempPassword);
			if (!createResult.Succeeded)
			{
				throw new Exception(
					$"Failed to create seeded test user '{spec.Email}': " +
					string.Join(", ", createResult.Errors));
			}
		}
		else
		{
			var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;

			if (!string.Equals(managedUser.UserName, spec.UserName, StringComparison.Ordinal))
			{
				var setUserNameResult = await _userManager.SetUserNameAsync(managedUser, spec.UserName);
				if (!setUserNameResult.Succeeded)
				{
					throw new Exception(
						$"Failed to update username for seeded test user '{spec.Email}': " +
						string.Join(", ", setUserNameResult.Errors));
				}
			}

			if (!string.Equals(managedUser.Email, spec.Email, StringComparison.OrdinalIgnoreCase))
			{
				var setEmailResult = await _userManager.SetEmailAsync(managedUser, spec.Email);
				if (!setEmailResult.Succeeded)
				{
					throw new Exception(
						$"Failed to update email for seeded test user '{spec.Email}': " +
						string.Join(", ", setEmailResult.Errors));
				}
			}

			managedUser.SetEmailConfirmed(true);
			managedUser.Name = spec.FirstName;
			managedUser.Surname = spec.LastName;
			await _userManager.UpdateAsync(managedUser);

			await ResetPasswordAsync(managedUser, TempPassword);
		}

		await EnsureRoleMembershipAsync(user, spec.RoleName);
	}

	private async Task<IdentityUser?> FindUserAsync(TestLoginSpec spec)
	{
		var normalizedEmail = _lookupNormalizer.NormalizeEmail(spec.Email);
		var byEmail = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (byEmail != null)
		{
			return byEmail;
		}

		foreach (var aliasEmail in spec.AliasEmails)
		{
			var normalizedAliasEmail = _lookupNormalizer.NormalizeEmail(aliasEmail);
			var byAliasEmail = await _userRepository.FindByNormalizedEmailAsync(normalizedAliasEmail);
			if (byAliasEmail != null)
			{
				return byAliasEmail;
			}
		}

		var normalizedUserName = _lookupNormalizer.NormalizeName(spec.UserName);
		var byUserName = await _userRepository.FindByNormalizedUserNameAsync(normalizedUserName);
		if (byUserName != null)
		{
			return byUserName;
		}

		foreach (var aliasUserName in spec.AliasUserNames)
		{
			var normalizedAliasUserName = _lookupNormalizer.NormalizeName(aliasUserName);
			var byAliasUserName = await _userRepository.FindByNormalizedUserNameAsync(normalizedAliasUserName);
			if (byAliasUserName != null)
			{
				return byAliasUserName;
			}
		}

		return null;
	}

	private async Task ResetPasswordAsync(IdentityUser user, string password)
	{
		var managedUser = await _userManager.FindByIdAsync(user.Id.ToString()) ?? user;
		IdentityResult result;
		if (string.IsNullOrWhiteSpace(managedUser.PasswordHash))
		{
			result = await _userManager.AddPasswordAsync(managedUser, password);
		}
		else
		{
			var token = await _userManager.GeneratePasswordResetTokenAsync(managedUser);
			result = await _userManager.ResetPasswordAsync(managedUser, token, password);
		}

		if (!result.Succeeded)
		{
			throw new Exception(
				$"Failed to set password for seeded test user '{managedUser.Email}': " +
				string.Join(", ", result.Errors));
		}
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

		var addResult = await _userManager.AddToRoleAsync(managedUser, role.Name);
		if (!addResult.Succeeded)
		{
			throw new Exception(
				$"Failed to assign role '{role.Name}' to seeded test user '{managedUser.Email}': " +
				string.Join(", ", addResult.Errors));
		}
	}

	private sealed record TestLoginSpec(
		string UserName,
		string Email,
		string FirstName,
		string LastName,
		string RoleName,
		IReadOnlyList<string> AliasEmails,
		IReadOnlyList<string> AliasUserNames);
}
