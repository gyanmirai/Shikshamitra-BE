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

	[Fact]
	public void Seeded_Staff_Roles_Should_Have_Leave_Center_Self_Service_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var seededStaffRoles = new[] { "admin", "Principal", "Teacher", "HR Manager", "Clerk", "Finance Officer" };

		foreach (var roleName in seededStaffRoles)
		{
			var permissions = rolePermissions[roleName];

			permissions.ShouldContain(ShikshaMitraModulePermissions.HR);
			permissions.ShouldContain(ShikshaMitraSubModulePermissions.LeaveManagement);
			permissions.ShouldContain(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View);
			permissions.ShouldContain(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Add);
			permissions.ShouldContain(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.Delete);
		}
	}

	[Fact]
	public void Leave_Self_Service_Roles_Should_Not_Get_Payroll_Run_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		rolePermissions["Teacher"].ShouldNotContain(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
		rolePermissions["Finance Officer"].ShouldNotContain(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
	}
}
