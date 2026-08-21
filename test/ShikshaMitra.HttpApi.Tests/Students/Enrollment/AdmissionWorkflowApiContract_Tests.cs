using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionWorkflowApiContract_Tests
{
	[Fact]
	public void Workflow_controller_exposes_version_validation_usage_and_runtime_routes()
	{
		Route(typeof(AdmissionWorkflowsController)).ShouldBe("api/student/enrollment/admissions/workflows");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Clone)).ShouldBe("{id:guid}/clone");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.CreateNewVersion)).ShouldBe("{id:guid}/new-version");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Validate)).ShouldBe("{id:guid}/validate");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Publish)).ShouldBe("{id:guid}/publish");
		Route(typeof(AdmissionApplicationsController)).ShouldBe("api/student/enrollment/admissions/applications");
		Post(typeof(AdmissionApplicationsController), nameof(AdmissionApplicationsController.AdvanceWorkflowAsync))
			.ShouldBe("{id:guid}/workflow/advance");
	}

	[Fact]
	public void Runtime_advance_contract_requires_concurrency_and_idempotency_fields()
	{
		var properties = typeof(AdvanceAdmissionWorkflowDto).GetProperties().Select(property => property.Name).ToList();
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.ExpectedCurrentStageInstanceId));
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.IdempotencyKey));
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.ConcurrencyStamp));
	}

	[Fact]
	public void Runtime_and_version_commands_require_granular_permissions()
	{
		Permission(typeof(AdmissionApplicationAppService), nameof(AdmissionApplicationAppService.AdvanceWorkflowAsync))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance);
		Permission(typeof(AdmissionWorkflowAppService), nameof(AdmissionWorkflowAppService.CreateNewVersionAsync))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion);
	}

	private static string? Route(System.Type controller) =>
		controller.GetCustomAttribute<RouteAttribute>()?.Template;

	private static string? Post(System.Type controller, string methodName) =>
		controller.GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<HttpPostAttribute>()?.Template;

	private static string? Permission(System.Type service, string methodName) =>
		service.GetMethods().Single(method => method.Name == methodName)
			.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
}
