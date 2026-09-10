using System.Linq;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountSettingsPermissionContract_Tests
{
	[Theory]
	[InlineData("AccountHead")]
	[InlineData("IncomeCategory")]
	[InlineData("ExpenseCategory")]
	[InlineData("BankAccount")]
	public void Settings_Should_Register_All_Five_Actions_And_Seed_Finance_Roles(string setting)
	{
		var permissionRoot = $"Finance.Accounts.Settings.{setting}";
		var submodule = ShikshaMitraSubmodulePermissions.Finance.Submodules[permissionRoot];
		var expected = new[] { "View", "Add", "Edit", "ChangeStatus", "Delete" }
			.Select(action => $"{permissionRoot}.{action}").ToArray();
		submodule.Permissions.ShouldBe(expected);
		var roles = RolePermissionSeederContributor.BuildRolePermissions();
		foreach (var permission in expected)
		{
			submodule.DisplayNames.ShouldContainKey(permission);
			foreach (var role in new[] { "admin", "super-admin", "Finance Officer" })
				roles[role].ShouldContain(permission);
			foreach (var role in new[] { "Teacher", "Student", "Parent" })
				roles[role].ShouldNotContain(permission);
		}
	}
}
