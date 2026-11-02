using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFormCorrectionApiContract_Tests
{
	[Fact]
	public void Form_correction_requires_existing_edit_permission_at_both_boundaries_and_posts_to_corrections()
	{
		var controller = typeof(AdmissionApplicationsController).GetMethod("CorrectFormAsync");
		controller.ShouldNotBeNull();
		controller.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("{id:guid}/form/corrections");
		controller.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(ApplicationPermissions.Edit);
		var service = typeof(AdmissionApplicationAppService).GetMethod("CorrectFormAsync");
		service.ShouldNotBeNull();
		service.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy).ShouldContain(ApplicationPermissions.Edit);
	}

	[Fact]
	public void Form_correction_conflicts_are_safe_http_409()
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode("Students.Admissions.FormCorrection.Conflict").ShouldBe(409);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode("Students.Admissions.FormCorrection.Invalid").ShouldBe(400);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode("Students.Admissions.FormCorrection.Blocked").ShouldBe(400);
	}
}
