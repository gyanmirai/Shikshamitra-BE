using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Policies;

[Collection(ApiTestCollection.Name)]
public class HrPolicyResolutionPreviewPermissionApi_Tests(HrOperationalTableApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<HrOperationalTableApiTestFactory>
{
	[Theory]
	[InlineData("HR.Policies.View", HttpStatusCode.Forbidden)]
	[InlineData("HR.Policies.Assign", HttpStatusCode.Forbidden)]
	[InlineData("HR.LeaveManagement.Setting.LeavePolicy.View", HttpStatusCode.Forbidden)]
	[InlineData("HR.Policies.PreviewImpact", HttpStatusCode.BadRequest)]
	public async Task Resolution_preview_requires_its_exact_existing_read_action(string grant, HttpStatusCode expected)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, grant);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		// An authorized caller reaches normal DTO validation; other grants cannot reach the reader.
		var response = await client.PostAsJsonAsync("/api/hr/policy-assignments/resolution-preview", new PolicyAssignmentResolutionPreviewRequestDto());
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
	}
}