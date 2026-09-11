using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFormStageApiContract_Tests
{
	[Theory]
	[InlineData("GetAsync", Permissions.View)]
	[InlineData("VerifySectionAsync", Permissions.Edit)]
	[InlineData("RequestCorrectionAsync", Permissions.Reopen)]
	public void Controller_and_service_require_action_permissions(string method, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionFormStagesController), typeof(AdmissionFormStageAppService) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(item => item.Policy).ShouldContain(permission);
	}

	[Fact]
	public void Contract_exposes_managed_Form_behavior_and_runtime_metadata()
	{
		typeof(AdmissionFormStagesController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/applications/{applicationId:guid}/form-stages/{stageId:guid}");
		typeof(AdmissionFormStageDto).GetProperty("Policy").ShouldNotBeNull();
		typeof(AdmissionFormStageDto).GetProperty("Blockers").ShouldNotBeNull();
		typeof(AdmissionFormStageDto).GetProperty("Sections").ShouldNotBeNull();
		var metadata = AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Form)!;
		metadata.IsRuntimeReady.ShouldBeTrue();
		metadata.SupportedConfiguration.ShouldContain("formDefinitionId");
		metadata.SupportedConfiguration.ShouldContain("form");
	}
}
