using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionIntakeSetupApiContract_Tests
{
	[Fact]
	public void Controller_Should_Expose_Explicit_Setup_Lifecycle_Routes()
	{
		var controller = typeof(AdmissionIntakeSetupsController);
		controller.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>()
			.Single().Template.ShouldBe("api/student/enrollment/admissions/intake-setups");
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.GetListAsync)).ShouldNotBeNull();
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.CreateAsync)).ShouldNotBeNull();
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.UpdateAsync)).ShouldNotBeNull();
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.GetUsageAsync))!
			.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>()
			.Single().Template.ShouldBe("{id:guid}/usage");
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.ArchiveAsync))!
			.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>()
			.Single().Template.ShouldBe("{id:guid}/archive");
	}

	[Fact]
	public void Controller_Should_Keep_Admission_Offering_Permissions()
	{
		AssertPolicy(nameof(AdmissionIntakeSetupsController.GetListAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
		AssertPolicy(nameof(AdmissionIntakeSetupsController.GetUsageAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
		AssertPolicy(nameof(AdmissionIntakeSetupsController.CreateAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		AssertPolicy(nameof(AdmissionIntakeSetupsController.UpdateAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		AssertPolicy(nameof(AdmissionIntakeSetupsController.ArchiveAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
	}

	[Fact]
	public void Setup_Contracts_Should_Keep_Existing_Offering_And_Public_Link_References()
	{
		typeof(AdmissionOfferingWriteDto).GetProperty("AdmissionIntakeSetupId").ShouldBeNull();
		typeof(CreateAdmissionPublicInquiryLinkDto).GetProperty("AdmissionIntakeSetupId").ShouldBeNull();
		typeof(AdmissionOfferingWriteDto).GetProperty("InquiryIntakeProfileId").ShouldNotBeNull();
		typeof(AdmissionOfferingWriteDto).GetProperty("CounsellorAssignmentProfileId").ShouldNotBeNull();
		typeof(CreateAdmissionPublicInquiryLinkDto).GetProperty("CounsellorAssignmentProfileId").ShouldNotBeNull();
	}

	private static void AssertPolicy(string methodName, string policy) =>
		typeof(AdmissionIntakeSetupsController).GetMethod(methodName)!
			.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == policy);
}
