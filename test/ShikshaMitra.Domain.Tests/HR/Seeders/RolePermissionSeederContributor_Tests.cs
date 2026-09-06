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
	private const string UserScopeDefault = "ShikshaMitra.UserScope";
	private const string UserScopeAssign = "ShikshaMitra.UserScope.Assign";
	private const string UserScopeRevoke = "ShikshaMitra.UserScope.Revoke";
	private const string UserScopeView = "ShikshaMitra.UserScope.View";
	private const string UserScopeBulkAssign = "ShikshaMitra.UserScope.BulkAssign";
	private const string UserScopeManage = "ShikshaMitra.UserScope.Manage";

	[Fact]
	public void Permission_Grant_Changes_Should_Use_The_Current_Seeder_Marker_Version()
	{
		RolePermissionSeederContributor.StepName.ShouldBe("RolePermissionSeeder");
		RolePermissionSeederContributor.StepVersion.ShouldBe("RolePermissionSeeder:v18");
		RolePermissionSeederContributor.StepOrder.ShouldBe(120);
	}

	[Fact]
	public void Finance_Administrators_Should_Receive_Concession_Type_Status_Permission()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		foreach (var roleName in new[] { "admin", "Finance Officer" })
		{
			rolePermissions[roleName].ShouldContain(
				ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ConcessionType.ChangeStatus);
		}
	}

	[Fact]
	public void Finance_Roles_Should_Receive_Concession_Policy_Permissions()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		foreach (var roleName in new[] { "admin", "super-admin", "Director", "Principal", "Finance Officer" })
		{
			var permissions = rolePermissions[roleName];

			foreach (var permission in ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.All)
			{
				permissions.ShouldContain(permission);
			}
		}
	}

	[Fact]
	public void Academic_Staff_Should_Receive_Dedicated_Lesson_Plan_Permissions()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		foreach (var roleName in new[] { "admin", "HOD", "Teacher" })
		{
			var permissions = rolePermissions[roleName];

			foreach (var permission in ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.All)
			{
				permissions.ShouldContain(permission);
			}
		}
	}

	[Fact]
	public void Payroll_Salary_Template_Permissions_Should_Be_Granted_To_Payroll_Administrators()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();

		foreach (var roleName in new[] { "admin", "super-admin", "Director", "HR Manager" })
		{
			var permissions = rolePermissions[roleName];

			foreach (var permission in ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.All)
			{
				permissions.ShouldContain(permission);
			}
		}
	}

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
	public void HR_Manager_Should_Have_Academic_Permission_Assignment_Access_Without_Global_Management()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var hrManagerPermissions = rolePermissions["HR Manager"];

		hrManagerPermissions.ShouldContain(UserScopeDefault);
		hrManagerPermissions.ShouldContain(UserScopeView);
		hrManagerPermissions.ShouldContain(UserScopeAssign);
		hrManagerPermissions.ShouldContain(UserScopeRevoke);
		hrManagerPermissions.ShouldContain(UserScopeBulkAssign);
		hrManagerPermissions.ShouldNotContain(UserScopeManage);
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
	public void Principal_Should_Have_Admissions_Crm_Operational_Access()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var principalPermissions = rolePermissions["Principal"];

		foreach (var permission in ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.All)
		{
			principalPermissions.ShouldContain(permission);
		}
	}

	[Fact]
	public void Admissions_Personas_Should_Keep_Least_Privilege_Boundaries()
	{
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var receptionist = rolePermissions["Admissions Receptionist"];
		var counselor = rolePermissions["Admissions Counselor"];
		var approver = rolePermissions["Admissions Approver"];

		receptionist.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View);
		receptionist.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);
		receptionist.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignOwner);
		receptionist.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration);
		receptionist.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult);
		receptionist.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers);

		counselor.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ScheduleExam);
		counselor.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments);
		counselor.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAdmissionRoutes);
		counselor.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignAdmissionRoute);
		counselor.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult);
		counselor.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation);

		approver.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveOffers);
		approver.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation);
		approver.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamResult);
		approver.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveAdmissionRoutes);
		approver.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.EvaluateAdmissionRoute);
		approver.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration);
		approver.ShouldNotContain(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add);
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

	[Fact]
	public void Question_Bank_And_Question_Papers_Should_Include_Standard_Permissions_And_Be_Granted_To_Admin()
	{
		// Verify QuestionBank standard permissions
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View);
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Add);
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Delete);

		// Verify QuestionPapers standard permissions
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View);
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add);
		ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.All
			.ShouldContain(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Delete);

		// Verify Admin role receives all of them
		var rolePermissions = RolePermissionSeederContributor.BuildRolePermissions();
		var adminPermissions = rolePermissions["admin"];

		adminPermissions.ShouldContain("Academics.Examination.QuestionBank.View");
		adminPermissions.ShouldContain("Academics.Examination.QuestionBank.Add");
		adminPermissions.ShouldContain("Academics.Examination.QuestionBank.Delete");

		adminPermissions.ShouldContain("Academics.Examination.QuestionPapers.View");
		adminPermissions.ShouldContain("Academics.Examination.QuestionPapers.Add");
		adminPermissions.ShouldContain("Academics.Examination.QuestionPapers.Delete");
	}
}
