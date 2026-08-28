using System.Linq;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanPermissionContract_Tests
{
	[Fact]
	public void Lesson_Plan_Should_Have_A_Dedicated_Permission_Family()
	{
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.All.ShouldBe(new[]
		{
			"Academics.CurriculumManagement.LessonPlan.View",
			"Academics.CurriculumManagement.LessonPlan.Add",
			"Academics.CurriculumManagement.LessonPlan.Delete"
		});
		ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.All.Intersect(
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement.All)
			.ShouldBeEmpty();
	}

	[Fact]
	public void Lesson_Plan_Permissions_Should_Be_Registered_For_The_Abp_Provider()
	{
		var submodule = ShikshaMitraSubmodulePermissions.Academics.Submodules[
			"Academics.CurriculumManagement.LessonPlan"];

		submodule.Permissions.ShouldBe(
			ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.All);
		foreach (var permission in submodule.Permissions)
		{
			submodule.DisplayNames.ShouldContainKey(permission);
		}
	}
}
