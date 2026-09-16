using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<AcademicYearPermission, Guid> _academicYearPermissionRepository;
	private readonly AcademicSetupPermissionManager _academicSetupPermissionManager;
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
		IAcademicYearRepository academicYearRepository,
		IRepository<AcademicYearPermission, Guid> academicYearPermissionRepository,
		AcademicSetupPermissionManager academicSetupPermissionManager,
		ILogger<TestLoginSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_configuration = configuration;
		_userManager = userManager;
		_userRepository = userRepository;
		_roleRepository = roleRepository;
		_lookupNormalizer = lookupNormalizer;
		_employeeRepository = employeeRepository;
		_academicYearRepository = academicYearRepository;
		_academicYearPermissionRepository = academicYearPermissionRepository;
		_academicSetupPermissionManager = academicSetupPermissionManager;
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

		var credentialSpecs = BuildCredentialSpecs();

		foreach (var spec in credentialSpecs)
		{
			if (await FindUserAsync(spec) != null) continue;
			var user = await CreateTestUserAsync(spec);
			if (spec.RequiresAcademicYearWideScope)
			{
				await EnsureAcademicYearWidePermissionsAsync(user);
			}
			else if (ShouldRemoveAcademicYearWidePermissions(spec.Email, spec.RequiresAcademicYearWideScope))
			{
				await RemoveAcademicYearWidePermissionsAsync(user);
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	internal static IReadOnlyList<string> BuildSeededLoginEmails() =>
		BuildCredentialSpecs()
			.Select(x => x.Email)
			.ToList();

	internal static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSeededLoginEmployeeAliasEmailsByEmail() =>
		BuildCredentialSpecs()
			.ToDictionary(
				x => x.Email,
				x => x.EmployeeAliasEmails,
				StringComparer.OrdinalIgnoreCase);

	internal static IReadOnlyList<string> BuildAcademicYearWideScopeLoginEmails() =>
		BuildCredentialSpecs()
			.Where(x => x.RequiresAcademicYearWideScope)
			.Select(x => x.Email)
			.ToList();

	internal static bool ShouldRemoveAcademicYearWidePermissions(
		string email,
		bool requiresAcademicYearWideScope = false) =>
		!requiresAcademicYearWideScope &&
		SeededEmployeeAcademicScopePolicy.GetRequiredBoard(email).HasValue;

	private static IReadOnlyList<TestLoginSpec> BuildCredentialSpecs() =>
		new List<TestLoginSpec>
		{
			new("admin-ddvs", "admin-ddvs@ddvs.com", "Admin", "DDVS", "admin", ["admin1@gmail.com", "ddvs@gmail.com"], ["admin1"]),
			new("admin-ops", "admin-ops@ddvs.com", "Admin", "Operations", "admin", ["admin2@ddvs.com"], ["admin2"]),
			new("principal-gseb", "principal-gseb@ddvs.com", "Principal", "GSEB", "Principal", ["principal1@ddvs.com", "rajesh.kumar@school.edu", "hiren.vadher@ddvs.com"], ["principal1"]),
			new("principal-cbse", "principal-cbse@ddvs.com", "Principal", "CBSE", "Principal", ["principle1@ddvs.com"], ["principle1"]),
			new("teacher-gseb", "teacher-gseb@ddvs.com", "Teacher", "GSEB", "Teacher", ["teacher1@ddvs.com", "vikram.singh@school.edu", "sweta.badgujar@ddvs.com"], ["teacher1"]),
			new("admissions-reception", "admissions-reception@ddvs.com", "Admissions", "Reception", "Admissions Receptionist", [], []),
			new("admissions-counselor", "admissions-counselor@ddvs.com", "Admissions", "Counselor", "Admissions Counselor", [], []),
			new("admissions-approver", "admissions-approver@ddvs.com", "Admissions", "Approver", "Admissions Approver", [], []),
			new("student-crm-denied", "student-crm-denied@ddvs.com", "CRM", "Restricted", "HR Manager", [], []),
			new("clerk-office", "clerk-office@ddvs.com", "Clerk", "Office", "Clerk", ["clerk1@ddvs.com", "clerk@school.edu", "raj.baku@ddvs.com"], ["clerk1"]),
			new("finance-officer", "finance-officer@ddvs.com", "Finance", "Officer", "Finance Officer", ["finance1@ddvs.com", "suresh.patil@school.edu", "punam.badgujar@ddvs.com"], ["finance1"])
		};

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

	private async Task<IdentityUser> CreateTestUserAsync(TestLoginSpec spec)
	{
		var user = new IdentityUser(GuidGenerator.Create(), spec.UserName, spec.Email, CurrentTenant.Id)
		{ Name = spec.FirstName, Surname = spec.LastName };
		user.SetEmailConfirmed(true);
		var result = await _userManager.CreateAsync(user, TempPassword);
		if (!result.Succeeded)
			throw new Exception("Failed to initialize a synthetic test login: " + string.Join(", ", result.Errors));
		await AssignInitialRoleAsync(user, spec.RoleName);
		await LinkEmployeeAliasesToUserAsync(user, spec);
		return user;
	}

	private async Task EnsureAcademicYearWidePermissionsAsync(IdentityUser user)
	{
		var academicYearIds = (await _academicYearRepository.GetListAsync())
			.Select(x => x.Id)
			.Distinct()
			.ToList();
		if (academicYearIds.Count == 0)
		{
			_logger.LogWarning(
				"No academic years were available while seeding year-wide access for test login {UserName}.",
				user.UserName);
			return;
		}

		var existingYearIds = (await _academicYearPermissionRepository.GetListAsync(
				x => x.UserId == user.Id))
			.Select(x => x.AcademicYearId)
			.ToHashSet();
		var missingYearIds = academicYearIds
			.Where(x => !existingYearIds.Contains(x))
			.ToList();
		if (missingYearIds.Count == 0)
		{
			return;
		}

		await _academicYearPermissionRepository.InsertManyAsync(
			_academicSetupPermissionManager.CreateAcademicYearPermissions(user.Id, missingYearIds),
			autoSave: false);

		_logger.LogInformation(
			"Seeded academic-year-wide permissions for test login {UserName}. AcademicYearCount={AcademicYearCount}",
			user.UserName,
			missingYearIds.Count);
	}

	private async Task RemoveAcademicYearWidePermissionsAsync(IdentityUser user)
	{
		var permissions = await _academicYearPermissionRepository.GetListAsync(x => x.UserId == user.Id);
		if (permissions.Count == 0)
		{
			return;
		}

		await _academicYearPermissionRepository.DeleteManyAsync(permissions, autoSave: false);
		_logger.LogInformation(
			"Removed academic-year-wide permissions from board-scoped test login {UserName}. AcademicYearCount={AcademicYearCount}",
			user.UserName,
			permissions.Count);
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
		Employee? employee = null;
		foreach (var aliasEmail in aliasEmails)
		{
			employee = _employeeAliasCache.FirstOrDefault(x =>
				!string.IsNullOrWhiteSpace(x.Email) &&
				string.Equals(x.Email, aliasEmail, StringComparison.OrdinalIgnoreCase));
			if (employee != null)
			{
				break;
			}
		}

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

	private async Task AssignInitialRoleAsync(IdentityUser user, string roleName)
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
		IReadOnlyList<string> AliasUserNames,
		bool RequiresAcademicYearWideScope = false)
	{
		public IReadOnlyList<string> EmployeeAliasEmails =>
			new[] { Email }
				.Concat(AliasEmails)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
	}
}
