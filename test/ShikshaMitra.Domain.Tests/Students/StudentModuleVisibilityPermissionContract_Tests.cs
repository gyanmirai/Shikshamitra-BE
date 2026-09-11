using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentModuleVisibilityPermissionContract_Tests
{
	[Fact]
	public void Student_Module_Should_Register_All_Top_Level_Visibility_Permissions()
	{
		var expected = new[]
		{
			ShikshaMitraSubModulePermissions.StudentDashboard,
			ShikshaMitraSubModulePermissions.Records,
			ShikshaMitraSubModulePermissions.Enrollment,
			ShikshaMitraSubModulePermissions.StudentOnboarding,
			ShikshaMitraSubModulePermissions.Attendance,
			ShikshaMitraSubModulePermissions.Notification
		};

		foreach (var permission in expected)
		{
			ShikshaMitraSubModulePermissions.All.ShouldContain(permission);
			ShikshaMitraSubModulePermissions.DisplayNames.ShouldContainKey(permission);
		}
	}

	[Fact]
	public void Admissions_Roles_Should_Receive_Admissions_And_Onboarding_Visibility()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		foreach (var roleName in new[] { "Admissions Receptionist", "Admissions Counselor", "Admissions Approver" })
		{
			rolePermissions[roleName].ShouldContain(ShikshaMitraSubModulePermissions.Enrollment);
			rolePermissions[roleName].ShouldContain(ShikshaMitraSubModulePermissions.StudentOnboarding);
		}
	}

	[Fact]
	public void Every_Student_Submodule_Should_Expose_View_Add_And_Delete()
	{
		foreach (var (submoduleName, submodule) in ShikshaMitraSubmodulePermissions.Students.Submodules)
		{
			submodule.Permissions.ShouldBe(
			[
				$"{submoduleName}.View",
				$"{submoduleName}.Add",
				$"{submoduleName}.Delete"
			]);
			submodule.DisplayNames.Keys.ShouldBe(submodule.Permissions);
		}
	}
}
