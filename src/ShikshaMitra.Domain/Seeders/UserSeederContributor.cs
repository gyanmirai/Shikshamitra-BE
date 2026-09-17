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
	internal const string StepName = "UserSeeder";
	internal const string StepVersion = "UserSeeder:v3";
	internal const int StepOrder = 130;
	internal const string DefaultPassword = "Temp@123";

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
				// Clean-database initialization never repairs existing identities or memberships.
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

			var result = await _userManager.CreateAsync(user, DefaultPassword);
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

	internal static IReadOnlyDictionary<string, string> BuildSeededLoginRolesByEmail() =>
		BuildUserSpecs()
			.ToDictionary(x => x.Email, x => x.RoleName, System.StringComparer.OrdinalIgnoreCase);

	private static IReadOnlyList<BaselineUserSpec> BuildUserSpecs() =>
		new List<BaselineUserSpec>
		{
			new("admin-ddvs", "admin-ddvs@ddvs.com", "Admin", "DDVS", "admin", ["admin1"], ["admin1@gmail.com", "ddvs@gmail.com"]),
			new("admin-ops", "admin-ops@ddvs.com", "Admin", "Operations", "admin", ["admin2"], ["admin2@ddvs.com"]),
			new("teacher-demo-1", "teacher-demo-1@ddvs.com", "Teacher", "Demo 1", "Teacher", ["teacher1"], ["teacher1@school.com"]),
			new("teacher-demo-2", "teacher-demo-2@ddvs.com", "Teacher", "Demo 2", "Teacher", ["teacher2"], ["teacher2@school.com"]),
			new("principal-demo-gseb", "principal-demo-gseb@ddvs.com", "Principal", "Demo GSEB", "Principal", ["principal"], ["principal@school.com"]),
			new("hr-manager", "hr-manager@ddvs.com", "HR", "Manager", "HR Manager", ["hr"], ["hr@school.com"]),
			new("student-demo", "student-demo@ddvs.com", "Student", "Demo", "Student", ["student1"], ["student1@ddvs.com"]),
			new("parent-demo", "parent-demo@ddvs.com", "Parent", "Demo", "Parent", ["parent1"], ["parent1@ddvs.com"])
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
