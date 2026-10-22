using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionWorkflowApiContract_Tests
{
	[Fact]
	public async Task Assessment_catalog_uses_workflow_view_and_preserves_lookup_response()
	{
		var method = typeof(AdmissionWorkflowsController).GetMethod(nameof(AdmissionWorkflowsController.GetAssessmentCatalog))!;
		method.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("assessment-catalog");
		Permission(typeof(AdmissionWorkflowsController), method.Name).ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View);
		Permission(typeof(AdmissionWorkflowAppService), nameof(AdmissionWorkflowAppService.GetAssessmentCatalogAsync)).ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.View);
		var service = Substitute.For<IAdmissionWorkflowAppService>();
		var catalog = new AdmissionAssessmentCatalogDto();
		service.GetAssessmentCatalogAsync().Returns(catalog);
		var response = await new AdmissionWorkflowsController(service).GetAssessmentCatalog();
		response.ShouldNotBeNull();
		await service.Received(1).GetAssessmentCatalogAsync();
	}

	[Fact]
	public async Task Missing_application_detail_returns_not_found_without_throwing()
	{
		var id = System.Guid.NewGuid();
		var service = Substitute.For<IAdmissionApplicationAppService>();
		service.FindAsync(id).Returns((AdmissionApplicationDto?)null);

		var response = await new AdmissionApplicationsController(service).GetAsync(id);

		response.Result.ShouldBeOfType<NotFoundResult>();
	}

	[Fact]
	public void Workflow_controller_exposes_version_validation_usage_and_runtime_routes()
	{
		Route(typeof(AdmissionWorkflowsController)).ShouldBe("api/student/enrollment/admissions/workflows");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Clone)).ShouldBe("{id:guid}/clone");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.CreateNewVersion)).ShouldBe("{id:guid}/new-version");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Validate)).ShouldBe("{id:guid}/validate");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Publish)).ShouldBe("{id:guid}/publish");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.SubmitForApproval)).ShouldBe("{id:guid}/submit-for-approval");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Approve)).ShouldBe("{id:guid}/approve");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Reject)).ShouldBe("{id:guid}/reject");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.PublishWithGovernance)).ShouldBe("{id:guid}/governance/publish");
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.CreateRollbackDraft)).ShouldBe("{id:guid}/rollback-draft");
		Route(typeof(AdmissionApplicationsController)).ShouldBe("api/student/enrollment/admissions/applications");
		Post(typeof(AdmissionApplicationsController), nameof(AdmissionApplicationsController.AdvanceWorkflowAsync))
			.ShouldBe("{id:guid}/workflow/advance");
	}

	[Fact]
	public void Reopen_requires_publish_authority_and_binds_concurrency()
	{
		Post(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Reopen)).ShouldBe("{id:guid}/reopen");
		Permission(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.Reopen)).ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish);
		Permission(typeof(AdmissionWorkflowAppService), nameof(AdmissionWorkflowAppService.ReopenAsync)).ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Publish);
		typeof(AdmissionWorkflowsController).GetMethod(nameof(AdmissionWorkflowsController.Reopen))!.GetParameters()[1].GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}

	[Fact]
	public void Workflow_delete_binds_the_concurrency_stamp_from_the_query_string()
	{
		var method = typeof(AdmissionWorkflowsController).GetMethods()
			.Single(candidate => candidate.Name == nameof(AdmissionWorkflowsController.Delete));
		var parameters = method.GetParameters();

		parameters.Length.ShouldBe(2);
		parameters[1].Name.ShouldBe("concurrencyStamp");
		parameters[1].GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}

	[Fact]
	public void Runtime_advance_contract_requires_concurrency_and_idempotency_fields()
	{
		var properties = typeof(AdvanceAdmissionWorkflowDto).GetProperties().Select(property => property.Name).ToList();
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.ExpectedCurrentStageInstanceId));
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.IdempotencyKey));
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.ConcurrencyStamp));
		properties.ShouldContain(nameof(AdvanceAdmissionWorkflowDto.DeferFeeCollection));
	}

	[Fact]
	public void Runtime_and_version_commands_require_granular_permissions()
	{
		Permission(typeof(AdmissionApplicationAppService), nameof(AdmissionApplicationAppService.AdvanceWorkflowAsync))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Advance);
		Permission(typeof(AdmissionWorkflowAppService), nameof(AdmissionWorkflowAppService.CreateNewVersionAsync))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion);
		Permission(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.CreateNewVersion))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.CreateVersion);
		Permission(typeof(AdmissionWorkflowAppService), nameof(AdmissionWorkflowAppService.ApproveAsync))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Approve);
		Permission(typeof(AdmissionWorkflowsController), nameof(AdmissionWorkflowsController.CreateRollbackDraft))
			.ShouldBe(ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows.Rollback);
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
