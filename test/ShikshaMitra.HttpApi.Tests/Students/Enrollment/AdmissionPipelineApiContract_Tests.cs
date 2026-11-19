using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionPipelineApiContract_Tests
{
	[Theory]
	[InlineData("GetPipelineAsync", "pipeline")]
	[InlineData("GetPipelineOptionsAsync", "pipeline/options")]
	[InlineData("GetFollowUpsAsync", "{id:guid}/follow-ups")]
	public void Reads_require_application_view_at_both_boundaries(string method, string route)
	{
		typeof(AdmissionApplicationsController).GetMethod(method)!.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe(route);
		foreach (var boundary in new[] { typeof(AdmissionApplicationsController), typeof(AdmissionApplicationAppService) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.AdmissionApplications.View);
	}

	[Theory]
	[InlineData("ScheduleFollowUpAsync")]
	[InlineData("CompleteFollowUpAsync")]
	[InlineData("CancelFollowUpAsync")]
	public void Followup_writes_require_existing_inquiry_action_and_application_view(string method)
	{
		foreach (var boundary in new[] { typeof(AdmissionApplicationsController), typeof(AdmissionApplicationAppService) })
		{
			var policies = boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ToList();
			policies.ShouldContain(Permissions.AdmissionApplications.View);
			policies.ShouldContain(Permissions.AdmissionInquiries.Manage);
		}
	}
}
