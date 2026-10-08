using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionPipelineApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	[Theory]
	[InlineData("pipeline")]
	[InlineData("pipeline/options")]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Pipeline_reads_should_deny_missing_application_view(string suffix)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, Permissions.AdmissionInquiries.View);
		var response = await client.GetAsync($"/api/student/enrollment/admissions/applications/{suffix}");
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("pipeline", HttpStatusCode.OK)]
	[InlineData("pipeline/options", HttpStatusCode.OK)]
	[InlineData("missing/follow-ups", HttpStatusCode.NotFound)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Application_view_should_allow_reads_without_inquiry_view(string suffix, HttpStatusCode expected)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, Permissions.AdmissionApplications.View);
		if (suffix.StartsWith("missing", StringComparison.Ordinal)) suffix = suffix.Replace("missing", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
		var response = await client.GetAsync($"/api/student/enrollment/admissions/applications/{suffix}");
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("schedule", false)]
	[InlineData("complete", false)]
	[InlineData("cancel", false)]
	[InlineData("schedule", true)]
	[InlineData("complete", true)]
	[InlineData("cancel", true)]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Application_followup_writes_should_require_inquiry_manage(string action, bool grantManage)
	{
		using var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			Permissions.AdmissionApplications.View + (grantManage ? "," + Permissions.AdmissionInquiries.Manage : string.Empty));
		var path = $"/api/student/enrollment/admissions/applications/{Guid.NewGuid():D}/follow-ups";
		object body = action == "schedule"
			? new ScheduleAdmissionInquiryFollowUpDto { ConcurrencyStamp = "current", ScheduledAt = DateTimeOffset.UtcNow.AddDays(2), Purpose = "Confirm visit" }
			: new CompleteAdmissionInquiryFollowUpDto { ConcurrencyStamp = "current", FollowUpConcurrencyStamp = "current", Note = "Recorded contact" };
		if (action != "schedule") path += $"/{Guid.NewGuid():D}/{action}";
		var response = await client.PostAsJsonAsync(path, body, JsonOptions);
		response.StatusCode.ShouldBe(grantManage ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}
}
