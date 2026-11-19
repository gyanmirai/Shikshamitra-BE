using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionApplicationCounsellorApiContract_Tests
{
	[Fact]
	public void Application_counsellor_read_and_assignment_are_permissioned_at_both_boundaries()
	{
		var controller = typeof(AdmissionApplicationsController);
		controller.GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/applications");
		var choices = controller.GetMethod(nameof(AdmissionApplicationsController.GetAssignableCounsellorsAsync))!;
		choices.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("counsellors");
		choices.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(ApplicationPermissions.View);
		var assign = controller.GetMethod(nameof(AdmissionApplicationsController.AssignCounsellorAsync))!;
		assign.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("{id:guid}/assign-counsellor");
		assign.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
			.ShouldContain(ApplicationPermissions.Edit);
		foreach (var methodName in new[] { nameof(AdmissionApplicationAppService.GetAssignableCounsellorsAsync),
			nameof(AdmissionApplicationAppService.AssignCounsellorAsync) })
		{
			var permission = methodName == nameof(AdmissionApplicationAppService.AssignCounsellorAsync)
				? ApplicationPermissions.Edit : ApplicationPermissions.View;
			typeof(AdmissionApplicationAppService).GetMethod(methodName)!
				.GetCustomAttributes<AuthorizeAttribute>().Select(item => item.Policy)
				.ShouldContain(permission);
		}
	}

	[Fact]
	public void Counsellor_is_exposed_in_registration_application_and_offer_contracts()
	{
		typeof(StartAdmissionDirectRegistrationDto).GetProperty(nameof(StartAdmissionDirectRegistrationDto.CounsellorId))
			.ShouldNotBeNull();
		typeof(AdmissionApplicationListInput).GetProperty(nameof(AdmissionApplicationListInput.AssignedCounsellorId))
			.ShouldNotBeNull();
		typeof(AdmissionApplicationDto).GetProperty(nameof(AdmissionApplicationDto.AssignedCounsellorName))
			.ShouldNotBeNull();
		typeof(IssueAdmissionOfferDto).GetProperty(nameof(IssueAdmissionOfferDto.CounsellorId))
			.ShouldNotBeNull();
		typeof(AdmissionOfferStageDto).GetProperty(nameof(AdmissionOfferStageDto.AssignedCounsellorId))
			.ShouldNotBeNull();
	}
}
