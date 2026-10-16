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
	public void Student_Submodules_Should_Expose_Only_Supported_View_Add_And_Delete_Actions()
	{
		foreach (var (submoduleName, submodule) in ShikshaMitraSubmodulePermissions.Students.Submodules)
		{
			var expected = submoduleName is "Students.Records.Overrides" or "Students.Records.GuardianAccess"
				? new[] { $"{submoduleName}.View", $"{submoduleName}.Add" }
				: new[] { $"{submoduleName}.View", $"{submoduleName}.Add", $"{submoduleName}.Delete" };
			submodule.Permissions.ShouldBe(expected);
			submodule.DisplayNames.Keys.ShouldBe(submodule.Permissions);
		}
	}

	[Fact]
	public void Sensitive_Student_Commands_Should_Use_Add_And_Removals_Should_Use_Delete()
	{
		ShikshaMitraSubmodulePermissions.Students.Admissions.Close.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Admissions.Add);
		ShikshaMitraSubmodulePermissions.Students.Admissions.Archive.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Admissions.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Deactivate.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Archive.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Archive.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add);
		ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Cancel.ShouldBe(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Withdraw.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionOffers.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Reverse.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionPayments.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Release.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionSeats.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.ManageGovernance.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Rollback.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments.Waive.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments.Add);
		ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments.Waive.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments.Add);
		ShikshaMitraSubmodulePermissions.Students.Records_Settings.Publish.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_Settings.Add);
		ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add);
		ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add);
		ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add);
		ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Cancel.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add);
	}
}
