using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionEligibility;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionEligibilityApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionEligibilityAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionEligibilityAppService.GetHistoryAsync), Permissions.View)]
	[InlineData(nameof(AdmissionEligibilityAppService.EvaluateAsync), Permissions.Evaluate)]
	[InlineData(nameof(AdmissionEligibilityAppService.RequestExceptionAsync), Permissions.RequestException)]
	[InlineData(nameof(AdmissionEligibilityAppService.ReviewExceptionAsync), Permissions.ReviewException)]
	[InlineData(nameof(AdmissionEligibilityAppService.GetReviewerRolesAsync), WorkflowPermissions.View)]
	public void Eligibility_permissions_are_enforced_at_service_and_controller_boundaries(string method, string permission)
	{
		typeof(AdmissionEligibilityAppService).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().ShouldContain(item => item.Policy == permission);
		typeof(AdmissionEligibilityController).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().ShouldContain(item => item.Policy == permission);
		if (permission is Permissions.Evaluate or Permissions.RequestException or Permissions.ReviewException)
			typeof(AdmissionEligibilityAppService).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().ShouldContain(item => item.Policy == Permissions.View);
	}
	[Fact]
	public void Eligibility_contract_is_additive_and_does_not_expose_private_fact_snapshots()
	{
		typeof(AdmissionEligibilityController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/student/enrollment/admissions/eligibility");
		typeof(AdmissionApplicationStageDto).GetProperty("EligibilityEnabled")!.PropertyType.ShouldBe(typeof(bool));
		typeof(AdmissionEligibilityEvaluationDto).GetProperty("FactsSnapshotJson").ShouldBeNull();
		AdmissionWorkflowStageTypeRegistry.All.Single(item => item.Type == AdmissionWorkflowStageType.Eligibility).SupportedConfiguration.ShouldContain("eligibility");
	}
}
