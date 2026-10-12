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
	public void Counsellor_Lookup_Should_Keep_View_Permission_And_Optional_Role_Filter()
	{
		var method = typeof(AdmissionIntakeProfilesController).GetMethod(nameof(AdmissionIntakeProfilesController.GetCounsellorsAsync))!;
		method.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template.ShouldBe("counsellors");
		method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
		var parameter = method.GetParameters().Single(item => item.Name == "counsellorOnly");
		parameter.ParameterType.ShouldBe(typeof(bool));
		parameter.DefaultValue.ShouldBe(false);
		parameter.GetCustomAttributes(typeof(FromQueryAttribute), true).Length.ShouldBe(1);
		typeof(AdmissionIntakeProfileAppService).GetMethod(nameof(AdmissionIntakeProfileAppService.GetCounsellorsAsync))!
			.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
	}

	[Fact]
	public void Public_Link_Options_Should_Require_Offering_View_And_Bind_The_Period_From_Query()
	{
		var method = typeof(AdmissionPublicInquiryLinksController).GetMethod(nameof(AdmissionPublicInquiryLinksController.GetOptionsAsync))!;
		method.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template.ShouldBe("options");
		method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
		method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).ShouldBeEmpty();
		method.GetParameters().Single().GetCustomAttributes(typeof(FromQueryAttribute), true).Length.ShouldBe(1);
		typeof(AdmissionPublicInquiryAppService).GetMethod(nameof(AdmissionPublicInquiryAppService.GetLinkOptionsAsync))!
			.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View);
	}

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
		controller.GetMethod(nameof(AdmissionIntakeSetupsController.RestoreAsync))!
			.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>()
			.Single().Template.ShouldBe("{id:guid}/restore");
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
		AssertPolicy(nameof(AdmissionIntakeSetupsController.RestoreAsync),
			ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
		typeof(AdmissionIntakeSetupAppService).GetMethod(nameof(AdmissionIntakeSetupAppService.RestoreAsync))!
			.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Add);
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
