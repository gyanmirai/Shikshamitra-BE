using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
