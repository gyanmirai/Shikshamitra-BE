using System;
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

public class AdmissionFormDeleteApiContract_Tests
{
	[Fact]
	public async Task Delete_forwards_the_query_concurrency_stamp_to_the_service()
	{
		var service = Substitute.For<IAdmissionFormAppService>();
		var id = Guid.NewGuid();
		const string stamp = "current-form-stamp";

		await new AdmissionFormsController(service).DeleteAsync(id, stamp);

		await service.Received(1).DeleteAsync(id, stamp);
		var stampParameter = typeof(AdmissionFormsController).GetMethod(nameof(AdmissionFormsController.DeleteAsync))!
			.GetParameters().Single(parameter => parameter.Name == "concurrencyStamp");
		stampParameter.GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}

	[Fact]
	public void Delete_requires_the_Delete_permission_at_both_boundaries()
	{
		foreach (var boundary in new[] { typeof(AdmissionFormsController), typeof(AdmissionFormAppService) })
			boundary.GetMethod(nameof(AdmissionFormsController.DeleteAsync))!
				.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy)
				.ShouldContain(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Delete);
	}

	[Fact]
	public void Form_review_endpoints_and_response_fields_are_removed_without_changing_publish_permission()
	{
		foreach (var boundary in new[] { typeof(AdmissionFormsController), typeof(AdmissionFormAppService) })
			boundary.GetMethods().ShouldNotContain(method =>
				method.Name == "RequestApprovalAsync" || method.Name == "ApproveAsync" || method.Name == "RejectAsync");
		typeof(AdmissionFormDto).GetProperties().ShouldNotContain(property =>
			property.Name == "ApprovalStatus" || property.Name == "ApprovalRequestedBy" || property.Name == "ReviewedBy");
		foreach (var boundary in new[] { typeof(AdmissionFormsController), typeof(AdmissionFormAppService) })
			boundary.GetMethod(nameof(AdmissionFormsController.PublishAsync))!
				.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy)
				.ShouldContain(ShikshaMitraSubmodulePermissions.Students.AdmissionForms.Publish);
	}
}
