using ShikshaMitra.Seeders;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Seeders;

public class UserSeederContributor_Tests
{
	[Fact]
	public void Student_And_Parent_Baseline_Logins_Should_Be_Seeded_With_Their_Roles()
	{
		var rolesByEmail = UserSeederContributor.BuildSeededLoginRolesByEmail();

		rolesByEmail["student-demo@ddvs.com"].ShouldBe("Student");
		rolesByEmail["parent-demo@ddvs.com"].ShouldBe("Parent");
		UserSeederContributor.DefaultPassword.ShouldBe("Temp@123");
	}

	[Fact]
	public void New_Baseline_Logins_Should_Use_A_New_Seeder_Marker_Version()
	{
		UserSeederContributor.StepName.ShouldBe("UserSeeder");
		UserSeederContributor.StepVersion.ShouldBe("UserSeeder:v3");
		UserSeederContributor.StepOrder.ShouldBe(130);
	}
}
