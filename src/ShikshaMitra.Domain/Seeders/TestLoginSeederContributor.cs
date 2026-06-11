using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.Linq;
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

	private readonly IConfiguration _configuration;
	private readonly IdentityUserManager _userManager;
	private readonly IIdentityUserRepository _userRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly ILookupNormalizer _lookupNormalizer;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly ILogger<TestLoginSeederContributor> _logger;
	private List<Employee>? _employeeAliasCache;

	public TestLoginSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IConfiguration configuration,
		IdentityUserManager userManager,
		IIdentityUserRepository userRepository,
		IIdentityRoleRepository roleRepository,
		ILookupNormalizer lookupNormalizer,
		IEmployeeRepository employeeRepository,
		ILogger<TestLoginSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_configuration = configuration;
		_userManager = userManager;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_lookupNormalizer = lookupNormalizer;
		_employeeRepository = employeeRepository;
		_logger = logger;
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

		var credentialSpecs = new List<TestLoginSpec>
		{
			new("admin-ddvs", "admin-ddvs@ddvs.com", "Admin", "DDVS", "admin", ["admin1@gmail.com", "ddvs@gmail.com"], ["admin1"]),
			new("admin-ops", "admin-ops@ddvs.com", "Admin", "Operations", "admin", ["admin2@ddvs.com"], ["admin2"]),
			new("principal-gseb", "principal-gseb@ddvs.com", "Principal", "GSEB", "Principal", ["principal1@ddvs.com", "rajesh.kumar@school.edu", "hiren.vadher@ddvs.com"], ["principal1"]),
			new("principal-cbse", "principal-cbse@ddvs.com", "Principal", "CBSE", "Principal", ["principle1@ddvs.com"], ["principle1"]),
			new("teacher-gseb", "teacher-gseb@ddvs.com", "Teacher", "GSEB", "Teacher", ["teacher1@ddvs.com", "vikram.singh@school.edu", "sweta.badgujar@ddvs.com"], ["teacher1"]),
			new("clerk-office", "clerk-office@ddvs.com", "Clerk", "Office", "Clerk", ["clerk1@ddvs.com", "clerk@school.edu", "raj.baku@ddvs.com"], ["clerk1"]),
			new("finance-officer", "finance-officer@ddvs.com", "Finance", "Officer", "Finance Officer", ["finance1@ddvs.com", "suresh.patil@school.edu", "punam.badgujar@ddvs.com"], ["finance1"])
		};

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

			var userChanged = false;
			if (!managedUser.EmailConfirmed)
			{
				managedUser.SetEmailConfirmed(true);
				userChanged = true;
			}

			if (!string.Equals(managedUser.Name, spec.FirstName, StringComparison.Ordinal))
			{
				managedUser.Name = spec.FirstName;
				userChanged = true;
			}

			if (!string.Equals(managedUser.Surname, spec.LastName, StringComparison.Ordinal))
			{
				managedUser.Surname = spec.LastName;
				userChanged = true;
			}

			if (userChanged)
			{
				var updateResult = await _userManager.UpdateAsync(managedUser);
				if (!updateResult.Succeeded)
				{
					throw new Exception(
						$"Failed to update seeded test user '{spec.Email}': " +
						string.Join(", ", updateResult.Errors));
				}
			}

			await ResetPasswordAsync(managedUser, TempPassword);
		}

		await EnsureRoleMembershipAsync(user, spec.RoleName);
		await LinkEmployeeAliasesToUserAsync(user, spec);
	}

	private async Task LinkEmployeeAliasesToUserAsync(IdentityUser user, TestLoginSpec spec)
	{
		if (spec.EmployeeAliasEmails.Count == 0)
		{
			return;
		}

		var aliasEmails = spec.EmployeeAliasEmails
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (aliasEmails.Count == 0)
		{
			return;
		}

		_employeeAliasCache ??= await _employeeRepository.GetListAsync();
		var employee = _employeeAliasCache.FirstOrDefault(x =>
			!string.IsNullOrWhiteSpace(x.Email) &&
			aliasEmails.Contains(x.Email, StringComparer.OrdinalIgnoreCase));

		if (employee == null)
		{
			_logger.LogInformation(
				"No employee alias match found for seeded login {UserName}. AliasEmails={AliasEmails}",
				spec.UserName,
				string.Join(", ", aliasEmails));
			return;
		}

		if (employee.UserId == user.Id)
		{
			return;
		}

		_logger.LogInformation(
			"Linking employee {EmployeeId} ({EmployeeEmail}) to seeded login {UserName} ({UserId}). PreviousUserId={PreviousUserId}",
			employee.Id,
			employee.Email,
			spec.UserName,
			user.Id,
			employee.UserId);

		employee.UpdateUserId(user.Id);
		await _employeeRepository.UpdateAsync(employee, autoSave: false);
	}

	private async Task<IdentityUser?> FindUserAsync(TestLoginSpec spec)
	{
		var normalizedEmail = _lookupNormalizer.NormalizeEmail(spec.Email);
		var byEmail = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail);
		if (byEmail != null)
		{
			return byEmail;
		}

		// Prefer direct username match before alias lookups to avoid selecting a different
		// alias account and then failing SetUserNameAsync with duplicate-username errors.
		var normalizedUserName = _lookupNormalizer.NormalizeName(spec.UserName);
		var byUserName = await _userRepository.FindByNormalizedUserNameAsync(normalizedUserName);
		if (byUserName != null)
		{
			return byUserName;
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
			// Avoid token-provider dependency in DbMigrator context.
			// Some environments don't register the "Default" token provider, which breaks
			// GeneratePasswordResetTokenAsync during seeding.
			var removeResult = await _userManager.RemovePasswordAsync(managedUser);
			if (!removeResult.Succeeded)
			{
				throw new Exception(
					$"Failed to remove existing password for seeded test user '{managedUser.Email}': " +
					string.Join(", ", removeResult.Errors));
			}

			result = await _userManager.AddPasswordAsync(managedUser, password);
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
			throw new Exception(
				$"Required seeded role '{roleName}' was not found while assigning seeded test user '{user.Email}'.");
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
		IReadOnlyList<string> AliasUserNames)
	{
		public IReadOnlyList<string> EmployeeAliasEmails => AliasEmails;
	}
}
