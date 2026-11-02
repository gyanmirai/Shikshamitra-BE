using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public class NavigationVisibilityPrerequisiteContract_Tests
{
	[Theory]
	[InlineData("View")]
	[InlineData("Add")]
	[InlineData("Delete")]
	public void House_management_actions_require_existing_student_and_records_visibility(string action)
	{
		var paths = NavigationVisibilityPrerequisites.GetPaths($"Students.Records.HouseManagement.{action}");
		paths.Length.ShouldBe(1);
		paths[0].ShouldBe(new[] { "ShikshaMitra.Module.Students", "ShikshaMitra.Module.Students.Records" });
	}

	[Fact]
	public void Employee_actions_require_module_and_feature_visibility()
	{
		var paths = NavigationVisibilityPrerequisites.GetPaths("HR.Employee.EmployeeList.Edit");
		paths.Length.ShouldBe(1);
		paths[0].ShouldBe(new[] { "ShikshaMitra.Module.HR", "ShikshaMitra.Module.HR.Employee" });
	}

	[Fact]
	public void Institute_setup_action_children_are_distinct_from_exact_visibility_flags()
	{
		const string feature = "ShikshaMitra.Module.InstituteSetup.RoomManagement.Room";
		NavigationVisibilityPrerequisites.GetPaths(feature).ShouldBeEmpty();
		var paths = NavigationVisibilityPrerequisites.GetPaths(feature + ".View");
		paths.Length.ShouldBe(1);
		paths[0].ShouldBe(new[] { "ShikshaMitra.Module.InstituteSetup", "ShikshaMitra.Module.InstituteSetup.RoomManagement", feature });
	}

	[Fact]
	public void Alternative_navigation_paths_remain_separate()
	{
		var paths = NavigationVisibilityPrerequisites.GetPaths("Academics.CurriculumManagement.Settings.SubjectCreation.View");
		paths.Length.ShouldBe(2);
		paths.ShouldContain(path => path.Length == 1 && path[0] == "ShikshaMitra.Module.Academics");
		paths.ShouldContain(path => path.Length == 3 && path[2] == "ShikshaMitra.Module.Academics.Curriculum.Settings");
	}

	[Theory]
	[InlineData("HR.Employee.EmployeeListExtra.Edit")]
	[InlineData("Unknown.Future.Permission")]
	[InlineData("hr.employee.employeelist.edit")]
	[InlineData("AbpTenantManagement.Tenants.Create")]
	public void Unmapped_names_do_not_gain_guessed_prerequisites(string name) =>
		NavigationVisibilityPrerequisites.GetPaths(name).ShouldBeEmpty();
}
