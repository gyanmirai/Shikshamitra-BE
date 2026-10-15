using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Theory]
	[InlineData("ShiftManagement")]
	[InlineData("AcademicSetup")]
	public async Task Institute_setup_catalogue_actions_support_role_grants_and_user_overrides(string feature)
	{
		var (role, user) = await CreateTargetAsync();
		var actions = feature == "ShiftManagement"
			? ShikshaMitraSubmodulePermissions.InstituteSetup.ShiftManagement.All
			: ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.All;
		var roleUrl = Url("R", role);
		var catalogue = await ReadAsync(roleUrl);
		actions.Length.ShouldBe(4);
		foreach (var name in actions)
		{
			catalogue.Count(entry => entry.Name == name).ShouldBe(1);
			await SetAsync(roleUrl, (name, true));
			(await ReadAsync(roleUrl)).Single(entry => entry.Name == name).IsGranted.ShouldBeTrue();
			await AssertOverrideAsync(user, name, "inherit", true);
			await SetOverridesAsync(user, (name, "deny"));
			await AssertOverrideAsync(user, name, "deny", false);
			await AssertRealCheckerAsync(user, role, name, false);
			await SetOverridesAsync(user, (name, "allow"));
			await AssertOverrideAsync(user, name, "allow", true);
			await AssertRealCheckerAsync(user, role, name, true);
			await SetOverridesAsync(user, (name, "inherit"));
			await AssertOverrideAsync(user, name, "inherit", true);
			await SetAsync(roleUrl, (name, false));
			await AssertOverrideAsync(user, name, "inherit", false);
		}
	}
}
