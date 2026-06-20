using System;
using System.Linq;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Seeders;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class RolePermissionSeederContributor_Tests
{
	[Fact]
	public void HR_Manager_Should_Have_Read_Only_Role_List_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var hrManagerPermissions = rolePermissions["HR Manager"];

		hrManagerPermissions.ShouldContain(IdentityPermissions.Roles.Default);
		hrManagerPermissions.ShouldNotContain(IdentityPermissions.Roles.ManagePermissions);
		hrManagerPermissions.ShouldNotContain(IdentityPermissions.Users.Default);
		hrManagerPermissions.ShouldNotContain(IdentityPermissions.Users.ManagePermissions);
		hrManagerPermissions
			.Where(permission => permission.StartsWith("AbpPermissionManagement.", StringComparison.OrdinalIgnoreCase))
			.ShouldBeEmpty();
	}

	[Fact]
	public void HR_Manager_Should_Have_Communication_Operational_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var hrManagerPermissions = rolePermissions["HR Manager"];

		hrManagerPermissions.ShouldContain(ShikshaMitraModulePermissions.Communication);
		hrManagerPermissions.ShouldContain(ShikshaMitraSubmodulePermissions.Communication.Campaigns.Send);
		hrManagerPermissions.ShouldContain(ShikshaMitraSubmodulePermissions.Communication.Notification_Templates.Add);
		hrManagerPermissions.ShouldContain(ShikshaMitraSubmodulePermissions.Communication.Settings_ProviderSettings.Add);
	}

	[Fact]
	public void Tenant_Admin_Should_Retain_Role_Management_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var adminPermissions = rolePermissions["admin"];

		adminPermissions.ShouldContain(IdentityPermissions.Roles.Default);
		adminPermissions.ShouldContain(IdentityPermissions.Roles.ManagePermissions);
		adminPermissions.ShouldContain(IdentityPermissions.Users.Default);
		adminPermissions.ShouldContain(IdentityPermissions.Users.ManagePermissions);
	}
}
