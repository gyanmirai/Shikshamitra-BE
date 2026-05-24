using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class RolePermissionSeederContributor : BaseSeederContributor
{
	private const string RoleProviderName = "R";

	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IPermissionGrantSeedRepository _permissionGrantSeedRepository;

	public RolePermissionSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IIdentityRoleRepository roleRepository,
		IPermissionGrantSeedRepository permissionGrantSeedRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_roleRepository = roleRepository;
		_permissionGrantSeedRepository = permissionGrantSeedRepository;
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

			await _permissionGrantSeedRepository.InsertRoleGrantsIgnoringDuplicatesAsync(
				role.Name,
				effectivePermissions,
				CurrentTenant.Id);
		}
	}

	private string[] GetEffectivePermissions(string roleName, string[] permissions)
	{
		if (CurrentTenant.Id != null || !IsHostAdministratorRole(roleName))
		{
			return permissions
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
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

		// Normal tenant role grants are configured through the Security module and must not be reset here.
		return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["admin"] = tenantAdminPermissions,
			["super-admin"] = tenantAdminPermissions,
			["Director"] = tenantAdminPermissions
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
			.Concat(TenantAdminPermissionRepairs())
			.Concat(TenantAdminPlatformPermissions())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

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

	private static IEnumerable<string> TenantAdminPermissionRepairs()
	{
		yield return ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View;
		yield return ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.Add;
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

}
