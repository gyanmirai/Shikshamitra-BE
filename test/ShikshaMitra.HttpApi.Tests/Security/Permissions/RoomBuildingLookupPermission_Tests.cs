using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Fact]
	public async Task Timetable_lookup_permission_should_follow_user_overrides_independently_of_building_access()
	{
		const string timetable = ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View;
		const string building = ShikshaMitraSubmodulePermissions.InstituteSetup.BuildingManagement.View;
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("U", user.ToString()), timetable);
		await SetOverridesAsync(user, (timetable, "allow"), (building, "deny"));
		await AssertRealCheckerAsync(user, role, timetable, true);
		await AssertRealCheckerAsync(user, role, building, false);
		await GrantActionVisibilityAsync(Url("R", role), timetable);
		await SetAsync(Url("R", role), (timetable, true));
		await SetOverridesAsync(user, (timetable, "deny"));
		await AssertRealCheckerAsync(user, role, timetable, false);
		await SetOverridesAsync(user, (timetable, "inherit"));
		await AssertRealCheckerAsync(user, role, timetable, true);
	}
}
