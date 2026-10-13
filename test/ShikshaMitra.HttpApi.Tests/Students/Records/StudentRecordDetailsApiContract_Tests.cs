using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Students.Records;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public class StudentRecordDetailsApiContract_Tests
{
	[Fact]
	public void Details_has_exact_Record_view_at_controller_and_application_boundaries()
	{
		foreach (var boundary in new[] { typeof(StudentRecordDetailsController), typeof(StudentRecordDetailsAppService) })
			boundary.GetCustomAttribute<AuthorizeAttribute>()!.Policy.ShouldBe(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		typeof(StudentRecordDetailsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe("api/students/records/{studentId:guid}/details");
		typeof(StudentRecordDetailsController).GetMethod("GetAsync")!.GetCustomAttribute<HttpGetAttribute>().ShouldNotBeNull();
		StudentRecordPayloadService.ActionFor("custom-details").ShouldBe("Correction");
	}
	[Fact]
	public void Captured_sources_have_server_resolved_fields_and_typed_values_without_account_secrets()
	{
		typeof(StudentRecordRegistrationSourceDto).GetProperty("EditableFieldCodes").ShouldNotBeNull();
		typeof(StudentRecordRegistrationSourceDto).GetProperty("CapturedFieldCodes").ShouldNotBeNull();
		typeof(StudentRecordRegistrationSourceDto).GetProperty("OptionsByField").ShouldNotBeNull();
		typeof(StudentRecordDetailsController).GetMethod("GetFieldOptionsAsync")!.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("options");
		typeof(StudentRecordAdmissionOnboardingDto).GetProperties().Any(x => x.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Token", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
	}
	[Fact]
	public void Inline_form_is_additive_and_requires_existing_record_view_and_change_create_permissions()
	{
		typeof(StudentRecordDetailsController).GetMethod("GetFormSnapshotAsync")!.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("form-snapshot");
		typeof(StudentRecordDetailsController).GetMethod("SaveFormAsync")!.GetCustomAttribute<HttpPutAttribute>()!.Template.ShouldBe("form");
		foreach (var operation in new[] { "GetFormSnapshotAsync", "SaveFormAsync" })
		{
			var policies = typeof(StudentRecordDetailsController).GetMethod(operation)!.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ToList();
			policies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
			policies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
		}
		var appPolicies = typeof(StudentRecordFormAppService).GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).ToList();
		appPolicies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		appPolicies.ShouldContain(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
		typeof(StudentRecordDetailsDto).GetProperty("FormSources").ShouldNotBeNull();
		typeof(StudentRecordRegistrationSourceDto).GetProperty("FormEditableFieldCodes").ShouldNotBeNull();
		typeof(StudentRecordRegistrationSourceDto).GetProperty("FormMaskedFieldCodes").ShouldNotBeNull();
	}
}
