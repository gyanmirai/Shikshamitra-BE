using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolePermissionSeederContributor : BaseSeederContributor
{
	private const string RoleProviderName = "R";

	private readonly IPermissionDataSeeder _permissionDataSeeder;
	private readonly IIdentityRoleRepository _roleRepository;

	public RolePermissionSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IPermissionDataSeeder permissionDataSeeder,
		IIdentityRoleRepository roleRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_permissionDataSeeder = permissionDataSeeder;
		_roleRepository = roleRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var rolePermissions = BuildRolePermissions();

		foreach (var (roleName, permissions) in rolePermissions)
		{
			var role = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
			if (role == null)
			{
				continue;
			}

			await _permissionDataSeeder.SeedAsync(
				RoleProviderName,
				role.Name,
				permissions,
				CurrentTenant.Id);
		}
	}

	private static Dictionary<string, string[]> BuildRolePermissions()
	{
		var allPermissions = AllApplicationPermissions();
		var teacherPermissions = PermissionsForModules(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Academics,
			ShikshaMitraModulePermissions.ClassManagement,
			ShikshaMitraModulePermissions.Calendar);
		var clerkPermissions = PermissionsForModules(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.HR,
			ShikshaMitraModulePermissions.Communication,
			ShikshaMitraModulePermissions.Calendar,
			ShikshaMitraModulePermissions.InstituteSetup);
		var financePermissions = PermissionsForModules(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Finance);
		var studentPermissions = PermissionsForModules(
			ShikshaMitraModulePermissions.Dashboard,
			ShikshaMitraModulePermissions.Students,
			ShikshaMitraModulePermissions.Calendar);

		return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["Director"] = allPermissions,
			["Principal"] = allPermissions,
			["Vice Principal"] = allPermissions,
			["Administrative Manager"] = allPermissions,
			["IT Administrator"] = allPermissions,
			["Head of Department"] = teacherPermissions,
			["Teacher"] = teacherPermissions,
			["Clerk"] = clerkPermissions,
			["Office Assistant"] = clerkPermissions,
			["HR Manager"] = PermissionsForModules(
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.HR,
				ShikshaMitraModulePermissions.Calendar,
				ShikshaMitraModulePermissions.InstituteSetup),
			["Finance Officer"] = financePermissions,
			["Student"] = studentPermissions,
			["Parent"] = studentPermissions
		};
	}

	private static string[] AllApplicationPermissions() =>
		AllModulePermissions()
			.Concat(AllLegacySubModulePermissions())
			.Concat(AllDetailedSubmodulePermissions())
			.Concat(AllPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static string[] PermissionsForModules(params string[] modulePermissions)
	{
		var moduleSet = modulePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var modulePrefixes = moduleSet
			.Select(permission => permission + ".")
			.ToArray();

		return AllApplicationPermissions()
			.Where(permission =>
				moduleSet.Contains(permission) ||
				modulePrefixes.Any(prefix => permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
			.ToArray();
	}

	private static IEnumerable<string> AllModulePermissions() =>
		ShikshaMitraModulePermissions.All;

	private static IEnumerable<string> AllLegacySubModulePermissions() =>
		ShikshaMitraSubModulePermissions.All;

	private static IEnumerable<string> AllDetailedSubmodulePermissions() =>
		ShikshaMitraSubmodulePermissions.AllModules
			.SelectMany(module => module.Value.Submodules.Values)
			.SelectMany(submodule => submodule.Permissions);

	private static IEnumerable<string> AllPlatformPermissions()
	{
		yield return "ShikshaMitra.UserScope";
		yield return "ShikshaMitra.UserScope.Assign";
		yield return "ShikshaMitra.UserScope.Revoke";
		yield return "ShikshaMitra.UserScope.View";
		yield return "ShikshaMitra.UserScope.BulkAssign";
		yield return "ShikshaMitra.UserScope.Manage";
		yield return "ShikshaMitra.Group";
		yield return "ShikshaMitra.Group.List";
		yield return "ShikshaMitra.Group.View";
		yield return "ShikshaMitra.Group.Create";
		yield return "ShikshaMitra.Group.Edit";
		yield return "ShikshaMitra.Group.Delete";
	}
}
