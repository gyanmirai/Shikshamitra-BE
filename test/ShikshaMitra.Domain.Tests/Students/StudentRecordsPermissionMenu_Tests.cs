using System.Linq;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRecordsPermissionMenu_Tests
{
	[Fact]
	public void Profile_workflows_are_registered_under_student_list_without_extra_features()
	{
		var registry = ShikshaMitraSubmodulePermissions.Students.Submodules;
		var workflows = new[] { "Students.Records.Changes", "Students.Records.Transitions",
			"Students.Records.Overrides", "Students.Records.GuardianAccess", "Students.IdentityCompliance" };
		foreach (var feature in workflows) registry.ShouldNotContainKey(feature);
		var expected = ShikshaMitraSubmodulePermissions.Students.Records_StudentList.All
			.Concat(ShikshaMitraSubmodulePermissions.Students.Records_Changes.All)
			.Concat(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.All)
			.Concat(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.All)
			.Concat(ShikshaMitraSubmodulePermissions.Students.Records_GuardianAccess.All)
			.Concat(ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.All).ToArray();
		var bundle = registry["Students.Records.StudentList"];
		bundle.Permissions.ShouldBe(expected);
		bundle.DisplayNames.Keys.ShouldBe(expected);
		var all = registry.Values.SelectMany(item => item.Permissions).ToArray();
		all.Length.ShouldBe(111);
		all.Distinct().Count().ShouldBe(all.Length);
	}
}
