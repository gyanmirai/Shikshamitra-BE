using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionAssessments;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionAssessmentApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionAssessmentAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetApplicantsAsync), Permissions.View)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetSessionWorkbenchAsync), Permissions.View)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetSessionAsync), Permissions.View)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetSessionResourcesAsync), Permissions.ManageSessions)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetInterviewersAsync), Permissions.ManageSessions)]
	[InlineData(nameof(AdmissionAssessmentAppService.GetSessionSlotsAsync), Permissions.Book)]
	[InlineData(nameof(AdmissionAssessmentAppService.PreviewSessionAsync), Permissions.ManageSessions)]
	[InlineData(nameof(AdmissionAssessmentAppService.ConfirmSessionAsync), Permissions.ManageSessions)]
	[InlineData(nameof(AdmissionAssessmentAppService.CreateSessionAsync), Permissions.ManageSessions)]
	[InlineData(nameof(AdmissionAssessmentAppService.BookAsync), Permissions.Book)]
	[InlineData(nameof(AdmissionAssessmentAppService.RecordAttendanceAsync), Permissions.RecordAttendance)]
	[InlineData(nameof(AdmissionAssessmentAppService.RecordResultAsync), Permissions.RecordResults)]
	[InlineData(nameof(AdmissionAssessmentAppService.SaveResultDraftAsync), Permissions.RecordResults)]
	[InlineData(nameof(AdmissionAssessmentAppService.ReviewAsync), Permissions.Review)]
	[InlineData(nameof(AdmissionAssessmentAppService.WaiveAsync), Permissions.Waive)]
	public void Assessment_operations_enforce_distinct_permissions_at_both_boundaries(string method, string permission)
	{
		typeof(AdmissionAssessmentAppService).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(AdmissionAssessmentsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
	}

	[Fact]
	public void Managed_api_is_additive_and_application_runtime_has_opt_in_capability()
	{
		typeof(AdmissionAssessmentsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/student/enrollment/admissions/assessments");
		typeof(AdmissionApplicationStageDto).GetProperty("AssessmentEnabled")!.PropertyType.ShouldBe(typeof(bool));
		AdmissionWorkflowStageTypeRegistry.All.Single(item => item.Type == AdmissionWorkflowStageType.Assessment)
			.SupportedConfiguration.ShouldContain("assessment");
	}

	[Fact]
	public void Applicant_roster_is_a_query_bound_additive_endpoint()
	{
		var method = typeof(AdmissionAssessmentsController).GetMethod(nameof(AdmissionAssessmentsController.GetApplicantsAsync))!;
		method.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("applicants");
		method.GetParameters().Single().GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
		typeof(IAdmissionAssessmentAppService).GetMethod(nameof(IAdmissionAssessmentAppService.GetApplicantsAsync))!.ReturnType
			.ShouldBe(typeof(System.Threading.Tasks.Task<AdmissionAssessmentRosterDto>));
	}
}
