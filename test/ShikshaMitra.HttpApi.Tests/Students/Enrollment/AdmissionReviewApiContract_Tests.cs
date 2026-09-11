using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionReviewApiContract_Tests
{
	[Theory]
	[InlineData("GetAsync", Permissions.View)]
	[InlineData("GetHistoryAsync", Permissions.View)]
	[InlineData("GetQueueAsync", Permissions.View)]
	[InlineData("SaveDraftAsync", Permissions.Edit)]
	[InlineData("SubmitAsync", Permissions.Submit)]
	[InlineData("AssignAsync", Permissions.Assign)]
	[InlineData("RequestCorrectionAsync", Permissions.Decide)]
	public void Review_controller_and_service_require_the_distinct_action_and_view_permissions(string method, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionReviewsController), typeof(AdmissionReviewAppService) })
		{
			var policies = boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ToList();
			policies.ShouldContain(Permissions.View); policies.ShouldContain(permission);
			if (method == "RequestCorrectionAsync") { policies.ShouldContain(ApplicationPermissions.Reopen); policies.ShouldContain(ApplicationPermissions.Decide); }
		}
	}
	[Fact]
	public void Review_contract_preserves_existing_decision_authority_and_exposes_only_safe_snapshots()
	{
		typeof(AdmissionReviewsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/student/enrollment/admissions/reviews");
		typeof(AdmissionReviewsController).GetMethods().ShouldNotContain(method => method.Name == "ApproveAsync" || method.Name == "RejectAsync" || method.Name == "WaitlistAsync");
		var fields = typeof(AdmissionReviewRecordDto).GetProperties().Select(item => item.Name).ToList();
		fields.ShouldNotContain("InputFingerprint"); fields.ShouldNotContain("PolicySnapshotJson"); fields.ShouldNotContain("EvidenceSnapshotJson");
		typeof(AdmissionApplicationDto).GetProperty("ReviewDecision").ShouldNotBeNull();
		typeof(AdmissionApplicationStageDto).GetProperty("ReviewEnabled").ShouldNotBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Review)!;
		metadata.IsRuntimeReady.ShouldBeTrue(); metadata.SupportedConfiguration.ShouldContain("review");
	}
}
