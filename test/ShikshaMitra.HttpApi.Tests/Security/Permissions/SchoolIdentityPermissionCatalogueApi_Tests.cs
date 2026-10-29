using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.Permissions;

public partial class PermissionConsistencyApi_Tests
{
	private const string SchoolIdentityDelete = "InstituteSetup.SchoolIdentity.Delete";

	[Fact]
	public async Task School_identity_catalogue_supports_configuration_and_signature_access_without_deletion()
	{
		var (role, user) = await CreateTargetAsync();
		var roleUrl = Url("R", role);
		var expected = new[] { SchoolIdentityPermissions.View, SchoolIdentityPermissions.ManageDefaults,
			SchoolIdentityPermissions.ManageOverrides, SchoolIdentityPermissions.AccessSignature };
		foreach (var url in new[] { roleUrl, OverrideUrl(user) })
		{
			var catalogue = await ReadAsync(url);
			catalogue.Where(entry => entry.Name.StartsWith("InstituteSetup.SchoolIdentity.", StringComparison.Ordinal))
				.Select(entry => entry.Name).OrderBy(name => name).ShouldBe(expected.OrderBy(name => name));
		}
		await GrantActionVisibilityAsync(roleUrl, expected);
		foreach (var name in expected)
		{
			await SetAsync(roleUrl, (name, true));
			await AssertOverrideAsync(user, name, "inherit", true);
			await SetOverridesAsync(user, (name, "deny"));
			await AssertOverrideAsync(user, name, "deny", false);
			await AssertRealCheckerAsync(user, role, name, false);
			await SetOverridesAsync(user, (name, "allow"));
			await AssertOverrideAsync(user, name, "allow", true);
			await SetOverridesAsync(user, (name, "inherit"));
			await AssertOverrideAsync(user, name, "inherit", true);
		}
	}

	[Theory]
	[InlineData("R")]
	[InlineData("U")]
	public async Task Invented_school_identity_delete_grants_are_rejected_atomically(string provider)
	{
		var (role, user) = await CreateTargetAsync();
		var url = provider == "R" ? Url("R", role) : OverrideUrl(user);
		var names = new[] { SchoolIdentityPermissions.View, SchoolIdentityDelete };
		object batch = provider == "R"
			? new { permissions = names.Select(name => new { name, isGranted = true }) }
			: new { permissions = names.Select(name => new { name, state = "allow" }) };
		using var response = await Client.PutAsJsonAsync(url, batch);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
		var catalogue = await ReadAsync(url);
		catalogue.ShouldNotContain(entry => entry.Name == SchoolIdentityDelete);
		catalogue.Single(entry => entry.Name == SchoolIdentityPermissions.View).IsGranted.ShouldBeFalse();
	}

	[Fact]
	public async Task School_identity_has_no_http_delete_operation()
	{
		using var response = await Client.DeleteAsync("/api/institute-setup/school-identity");
		response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
	}
}
