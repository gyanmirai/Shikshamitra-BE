using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionInquiryManagementApiContract_Tests
{
	[Theory]
	[InlineData("GetAsync", Permissions.View)]
	[InlineData("RecordContactAsync", Permissions.Manage)]
	[InlineData("ScheduleFollowUpAsync", Permissions.Manage)]
	[InlineData("CompleteFollowUpAsync", Permissions.Manage)]
	[InlineData("CancelFollowUpAsync", Permissions.Manage)]
	[InlineData("QualifyAsync", Permissions.Manage)]
	[InlineData("ReopenAsync", Permissions.Manage)]
	public void Management_controller_and_service_require_action_permissions(string method, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionInquiryManagementController), typeof(AdmissionInquiryManagementAppService) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
				.ShouldContain(permission);
	}

	[Fact]
	public void Inquiry_contract_exposes_managed_configuration_without_internal_snapshots()
	{
		typeof(AdmissionInquiryManagementController).GetCustomAttribute<RouteAttribute>()!.Template
			.ShouldBe("api/student/enrollment/admissions/inquiries/{inquiryId:guid}/management");
		typeof(AdmissionInquiryManagementDto).GetProperty("PolicySnapshotJson").ShouldBeNull();
		typeof(AdmissionInquiryManagementDto).GetProperty("QualificationAnswersJson").ShouldBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Inquiry)!;
		metadata.IsRuntimeReady.ShouldBeTrue();
		metadata.IsSelectable.ShouldBeFalse();
		metadata.IsDeprecated.ShouldBeTrue();
		metadata.SupportedConfiguration.ShouldContain("inquiry");
	}

	[Fact]
	public void Counsellor_choices_require_workflow_view_permission()
	{
		typeof(AdmissionInquirySettingsController).GetMethod("GetCounsellorsAsync")!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(WorkflowPermissions.View);
		typeof(AdmissionInquirySettingsAppService).GetMethod("GetCounsellorsAsync")!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(WorkflowPermissions.View);
	}

	[Fact]
	public void Runtime_assignment_uses_the_dedicated_inquiry_permission_and_queue_contract()
	{
		var counsellors = typeof(AdmissionInquiriesController).GetMethod("GetAssignableCounsellorsAsync")!;
		counsellors.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("counsellors");
		counsellors.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(Permissions.Assign);
		typeof(AdmissionInquiryAppService).GetMethod("GetAssignableCounsellorsAsync")!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.Assign);

		var assign = typeof(AdmissionInquiriesController).GetMethod("AssignCounsellorAsync")!;
		assign.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(Permissions.Assign);
		typeof(AdmissionInquiryAppService).GetMethod("AssignCounsellorAsync")!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(Permissions.Assign);

		typeof(AdmissionInquiryListInput).GetProperty("IsAssigned").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("WorkQueue").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("InquiryNumber").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("ApplicantName").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("Phone").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("Email").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("PipelineGroup").ShouldNotBeNull();
		typeof(AdmissionInquiryListInput).GetProperty("ExcludePostApplication").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("NextFollowUpAt").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("NeedsFirstContact").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("NeedsQualification").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("LastContactAt").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("QualificationOutcome").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("IsOverdue").ShouldNotBeNull();
	}

	[Fact]
	public void Inquiry_communication_is_a_managed_CRM_action_with_an_explicit_result_contract()
	{
		var controllerMethod = typeof(AdmissionInquiriesController).GetMethod("CreateCommunicationAsync")!;
		controllerMethod.GetCustomAttribute<HttpPostAttribute>()!.Template
			.ShouldBe("{id:guid}/communications");
		controllerMethod.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(Permissions.Manage);
		typeof(AdmissionInquiryAppService).GetMethod("CreateCommunicationAsync")!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(Permissions.Manage);
		typeof(IAdmissionInquiryAppService).GetMethod("CreateCommunicationAsync").ShouldNotBeNull();

		typeof(CreateAdmissionInquiryCommunicationDto).GetProperty("Channels").ShouldNotBeNull();
		typeof(CreateAdmissionInquiryCommunicationDto).GetProperty("PublishImmediately").ShouldNotBeNull();
		typeof(AdmissionInquiryCommunicationResultDto).GetProperty("CampaignId").ShouldNotBeNull();
		typeof(AdmissionInquiryCommunicationResultDto).GetProperty("DeliveryStarted").ShouldNotBeNull();
		typeof(AdmissionInquiryCommunicationResultDto).GetProperty("DeliveryErrorCode").ShouldNotBeNull();
	}

	[Theory]
	[InlineData("GetAsync", "{id:guid}", Permissions.View)]
	[InlineData("GetTimelineAsync", "{id:guid}/timeline", Permissions.View)]
	[InlineData("ExportCsvAsync", "export", Permissions.View)]
	[InlineData("UpdateAsync", "{id:guid}", Permissions.Manage)]
	[InlineData("BulkAssignCounsellorAsync", "bulk/assign", Permissions.Assign)]
	public void Inquiry_operational_routes_require_the_expected_permission(
		string method,
		string route,
		string permission)
	{
		var controllerMethod = typeof(AdmissionInquiriesController).GetMethod(method)!;
		var routeAttribute = controllerMethod.GetCustomAttributes<HttpMethodAttribute>()
			.Single();
		routeAttribute.Template.ShouldBe(route);
		controllerMethod.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(permission);
		typeof(AdmissionInquiryAppService).GetMethod(method)!
			.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(permission);
	}

	[Fact]
	public void Inquiry_contract_exposes_language_concurrency_timeline_and_bulk_results()
	{
		typeof(CreateAdmissionInquiryDto).GetProperty("BoardId").ShouldNotBeNull();
		typeof(CreateAdmissionInquiryDto).GetProperty("MediumId").ShouldNotBeNull();
		typeof(CreateAdmissionInquiryDto).GetProperty("StandardId").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("PreferredLanguageCode").ShouldNotBeNull();
		typeof(AdmissionInquiryDto).GetProperty("ConcurrencyStamp").ShouldNotBeNull();
		typeof(UpdateAdmissionInquiryDto).GetProperty("ConcurrencyStamp").ShouldNotBeNull();
		typeof(AdmissionInquiryTimelineItemDto).GetProperty("ActivityType").ShouldNotBeNull();
		typeof(BulkAdmissionInquiryResultDto).GetProperty("UpdatedCount").ShouldNotBeNull();
	}
}
