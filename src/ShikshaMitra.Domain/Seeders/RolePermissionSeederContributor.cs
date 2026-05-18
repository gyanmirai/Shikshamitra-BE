using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolePermissionSeederContributor : BaseSeederContributor
{
	private const string RoleProviderName = "R";

	private readonly IPermissionDataSeeder _permissionDataSeeder;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;

	public RolePermissionSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IPermissionDataSeeder permissionDataSeeder,
		IIdentityRoleRepository roleRepository,
		IRepository<PermissionGrant, Guid> permissionGrantRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_permissionDataSeeder = permissionDataSeeder;
		_roleRepository = roleRepository;
		_permissionGrantRepository = permissionGrantRepository;
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

			var effectivePermissions = GetEffectivePermissions(roleName, permissions);

			await RemoveObsoletePermissionGrantsAsync(role.Name, effectivePermissions);

			await _permissionDataSeeder.SeedAsync(
				RoleProviderName,
				role.Name,
				effectivePermissions,
				CurrentTenant.Id);
		}
	}

	private async Task RemoveObsoletePermissionGrantsAsync(string roleName, string[] effectivePermissions)
	{
		var effectivePermissionSet = effectivePermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var obsoletePermissions = DangerousPlatformPermissions()
			.Where(permission => !effectivePermissionSet.Contains(permission))
			.ToArray();

		foreach (var obsoletePermission in obsoletePermissions)
		{
			await _permissionGrantRepository.DeleteAsync(
				grant =>
					grant.TenantId == CurrentTenant.Id &&
					grant.Name == obsoletePermission &&
					grant.ProviderName == RoleProviderName &&
					grant.ProviderKey == roleName,
				autoSave: true);
		}
	}

	private string[] GetEffectivePermissions(string roleName, string[] permissions)
	{
		if (CurrentTenant.Id != null || !IsHostAdministratorRole(roleName))
		{
			return permissions;
		}

		return permissions
			.Concat(HostPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static bool IsHostAdministratorRole(string roleName) =>
		roleName.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
		roleName.Equals("super-admin", StringComparison.OrdinalIgnoreCase);

	private static Dictionary<string, string[]> BuildRolePermissions()
	{
		var tenantAdminPermissions = TenantAdminPermissions();
		var schoolOperatorPermissions = SchoolOperatorPermissions();
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
			["admin"] = tenantAdminPermissions,
			["super-admin"] = tenantAdminPermissions,
			["Director"] = schoolOperatorPermissions,
			["Principal"] = schoolOperatorPermissions,
			["Vice Principal"] = schoolOperatorPermissions,
			["Administrative Manager"] = schoolOperatorPermissions,
			["IT Administrator"] = schoolOperatorPermissions,
			["HOD"] = teacherPermissions,
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
			.Concat(AllDetailedModulePermissions())
			.Concat(AllLegacySubModulePermissions())
			.Concat(AllDetailedSubmodulePermissions())
			.Concat(AllPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static string[] TenantAdminPermissions() =>
		AllApplicationPermissions()
			.Concat(TenantAdminPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static string[] SchoolOperatorPermissions() =>
		AllApplicationPermissions()
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

	private static string[] PermissionsForModules(params string[] modulePermissions)
	{
		var moduleSet = modulePermissions
			.SelectMany(ModulePermissionAliases)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

	private static IEnumerable<string> AllDetailedModulePermissions() =>
		ShikshaMitraSubmodulePermissions.AllModules.Keys;

	private static IEnumerable<string> AllLegacySubModulePermissions() =>
		ShikshaMitraSubModulePermissions.All;

	private static IEnumerable<string> AllDetailedSubmodulePermissions() =>
		ShikshaMitraSubmodulePermissions.AllModules
			.SelectMany(module => module.Value.Submodules.Values)
			.SelectMany(submodule => submodule.Permissions);

	private static IEnumerable<string> ModulePermissionAliases(string modulePermission)
	{
		yield return modulePermission;

		var detailedModulePermission = modulePermission switch
		{
			ShikshaMitraModulePermissions.Students => ShikshaMitraSubmodulePermissions.Students.Default,
			ShikshaMitraModulePermissions.Academics => ShikshaMitraSubmodulePermissions.Academics.Default,
			ShikshaMitraModulePermissions.ClassManagement => ShikshaMitraSubmodulePermissions.ClassManagement.Default,
			ShikshaMitraModulePermissions.HR => ShikshaMitraSubmodulePermissions.HR.Default,
			ShikshaMitraModulePermissions.Finance => ShikshaMitraSubmodulePermissions.Finance.Default,
			ShikshaMitraModulePermissions.Communication => ShikshaMitraSubmodulePermissions.Communication.Default,
			ShikshaMitraModulePermissions.Calendar => ShikshaMitraSubmodulePermissions.Calendar.Default,
			ShikshaMitraModulePermissions.Library => ShikshaMitraSubmodulePermissions.Library.Default,
			ShikshaMitraModulePermissions.Transport => ShikshaMitraSubmodulePermissions.Transport.Default,
			ShikshaMitraModulePermissions.InstituteSetup => ShikshaMitraSubmodulePermissions.InstituteSetup.Default,
			_ => null
		};

		if (!string.IsNullOrWhiteSpace(detailedModulePermission))
		{
			yield return detailedModulePermission;
		}
	}

	private static IEnumerable<string> AllPlatformPermissions()
	{
		yield return "ShikshaMitra.Group";
		yield return "ShikshaMitra.Group.List";
		yield return "ShikshaMitra.Group.View";
		yield return "ShikshaMitra.Group.Create";
		yield return "ShikshaMitra.Group.Edit";
		yield return "ShikshaMitra.Group.Delete";
		yield return TenantManagementPermissions.Tenants.Default;
	}

	private static IEnumerable<string> TenantAdminPlatformPermissions()
	{
		yield return "ShikshaMitra.UserScope";
		yield return "ShikshaMitra.UserScope.Assign";
		yield return "ShikshaMitra.UserScope.Revoke";
		yield return "ShikshaMitra.UserScope.View";
		yield return "ShikshaMitra.UserScope.BulkAssign";
		yield return "ShikshaMitra.UserScope.Manage";
		yield return "AbpPermissionManagement.Permissions";
		yield return "AbpPermissionManagement.Permissions.Manage";
		yield return TenantManagementPermissions.Tenants.Update;
		yield return IdentityPermissions.Users.Default;
		yield return IdentityPermissions.Users.ManagePermissions;
		yield return IdentityPermissions.Roles.Default;
		yield return IdentityPermissions.Roles.ManagePermissions;
	}

	private static IEnumerable<string> HostPlatformPermissions()
	{
		yield return TenantManagementPermissions.Tenants.Create;
		yield return TenantManagementPermissions.Tenants.Delete;
		yield return TenantManagementPermissions.Tenants.ManageConnectionStrings;
	}

	private static IEnumerable<string> DangerousPlatformPermissions() =>
		TenantAdminPlatformPermissions()
			.Concat(HostPlatformPermissions());
}
