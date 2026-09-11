using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionDocuments;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionDocumentApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.GetAsync), Permissions.View)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.UploadAsync), Permissions.Upload)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.RecordOriginalAsync), Permissions.RecordOriginal)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.ReviewAsync), Permissions.Review)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.WaiveAsync), Permissions.Waive)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.DownloadAsync), Permissions.Download)]
	[InlineData(nameof(AdmissionDocumentVerificationAppService.GetDocumentTypesAsync), WorkflowPermissions.View)]
	public void Document_actions_have_distinct_permissions_at_both_boundaries(string method, string permission)
	{
		typeof(AdmissionDocumentVerificationAppService).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
		typeof(AdmissionDocumentVerificationController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(permission);
	}
	[Fact]
	public void Document_contract_is_opt_in_private_and_additive()
	{
		typeof(AdmissionDocumentVerificationController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/student/enrollment/admissions/document-verification");
		typeof(AdmissionApplicationStageDto).GetProperty("DocumentVerificationEnabled")!.PropertyType.ShouldBe(typeof(bool));
		typeof(AdmissionDocumentEvidenceDto).GetProperty("BlobName").ShouldBeNull();
		AdmissionWorkflowStageTypeRegistry.All.Single(item => item.Type == AdmissionWorkflowStageType.DocumentVerification).SupportedConfiguration.ShouldContain("documentVerification");
	}
}
