using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class RolesSeederContributor_Tests
{
	[Fact]
	public void Employee_Role_Metadata_Changes_Should_Use_A_New_Seeder_Marker_Version()
	{
		RolesSeederContributor.StepName.ShouldBe("RolesSeeder");
		RolesSeederContributor.StepVersion.ShouldBe("RolesSeeder:v6");
		RolesSeederContributor.StepOrder.ShouldBe(110);
	}

	[Fact]
	public void Existing_Role_Audience_Should_Remain_Stable_When_The_Role_Is_Renamed()
	{
		RoleEmployeeAudienceDefaults.ResolveRoleAudience(
				"Learner Support",
				EmployeeRoleAudience.Student)
			.ShouldBe(EmployeeRoleAudience.Student);
	}

	[Theory]
	[InlineData("Student", null, EmployeeRoleAudience.Student)]
	[InlineData("Parent", " ", EmployeeRoleAudience.Parent)]
	[InlineData("Teacher", null, EmployeeRoleAudience.Employee)]
	public void Missing_Role_Audience_Should_Be_Backfilled_From_The_Current_Role_Name(
		string roleName,
		string? existingAudience,
		string expectedAudience)
	{
		RoleEmployeeAudienceDefaults.ResolveRoleAudience(roleName, existingAudience)
			.ShouldBe(expectedAudience);
	}

	[Fact]
	public void Existing_Employee_Group_Audience_Should_Be_Preserved()
	{
		RoleEmployeeAudienceDefaults.ResolveEmployeeGroupAudience(
				"Teacher",
				EmployeeGroupAudience.NonTeaching)
			.ShouldBe(EmployeeGroupAudience.NonTeaching);
	}

	[Theory]
	[InlineData("Teacher", EmployeeGroupAudience.Teaching)]
	[InlineData("Principal", EmployeeGroupAudience.Teaching)]
	[InlineData("HOD", EmployeeGroupAudience.Teaching)]
	[InlineData("Vice Principal", EmployeeGroupAudience.Teaching)]
	[InlineData("Clerk", EmployeeGroupAudience.NonTeaching)]
	[InlineData("HR Manager", EmployeeGroupAudience.NonTeaching)]
	[InlineData("Custom Employee Role", EmployeeGroupAudience.Both)]
	public void Missing_Employee_Group_Audience_Should_Use_The_Explicit_Built_In_Map_Or_Both(
		string roleName,
		string expectedAudience)
	{
		RoleEmployeeAudienceDefaults.ResolveEmployeeGroupAudience(roleName, null)
			.ShouldBe(expectedAudience);
	}

	[Theory]
	[InlineData(EmployeeGroupAudience.Teaching, null, false)]
	[InlineData(EmployeeGroupAudience.NonTeaching, null, false)]
	[InlineData(EmployeeGroupAudience.Both, null, true)]
	[InlineData(EmployeeGroupAudience.Teaching, EmployeeGroupType.Teaching, true)]
	[InlineData(EmployeeGroupAudience.Teaching, EmployeeGroupType.NonTeaching, false)]
	public void Employee_Group_Assignment_Should_Not_Treat_A_Missing_Group_As_Matching_A_Restricted_Role(
		string audience,
		EmployeeGroupType? employeeGroup,
		bool expected)
	{
		EmployeeGroupAudience.IsAssignableTo(audience, employeeGroup).ShouldBe(expected);
	}

	[Theory]
	[InlineData("Teacher", null, true)]
	[InlineData("HR Manager", null, true)]
	[InlineData("Custom Employee Role", null, false)]
	[InlineData("Custom Employee Role", true, true)]
	[InlineData("Custom Employee Role", false, false)]
	[InlineData("admin", true, false)]
	[InlineData("super-admin", true, false)]
	[InlineData("Student", true, false)]
	[InlineData("Parent", true, false)]
	public void Employee_Onboarding_Assignment_Should_Fail_Closed_And_Protect_System_Roles(
		string roleName,
		bool? existingValue,
		bool expected)
	{
		RoleEmployeeAudienceDefaults.ResolveEmployeeOnboardingAssignable(
				roleName,
				existingValue)
			.ShouldBe(expected);
	}
}
