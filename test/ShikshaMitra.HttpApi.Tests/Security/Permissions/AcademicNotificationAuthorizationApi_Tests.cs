using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	[Fact]
	public async Task Academic_notification_send_is_independent_and_explicit_deny_wins_over_role_grants()
	{
		const string send = "Academics.Notification.Send";
		const string manage = "Academics.Notification.Add";
		var (role, user) = await CreateTargetAsync();
		await GrantActionVisibilityAsync(Url("R", role), send, manage);
		await SetAsync(Url("R", role), (send, true));
		await AssertRealCheckerAsync(user, role, send, true);
		await AssertRealCheckerAsync(user, role, manage, false);
		await SetOverridesAsync(user, (send, "deny"));
		await AssertOverrideAsync(user, send, "deny", false);
		await AssertRealCheckerAsync(user, role, send, false);
		await SetAsync(Url("R", role), (send, false), (manage, true));
		await SetAsync(Url("R", role), (send, true));
		await AssertRealCheckerAsync(user, role, send, false);
		await SetOverridesAsync(user, (send, "inherit"));
		await AssertRealCheckerAsync(user, role, send, true);
	}
}
