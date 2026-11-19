using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Enrollment;

public class AdmissionFeeSelectionApiContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionFeeSelectionAppService.SaveAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit)]
	[InlineData(nameof(AdmissionFeeSelectionAppService.ConfirmOnboardingAsync), ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	public void Mutations_enforce_existing_action_permissions_at_both_boundaries(string method, string permission)
	{
		foreach (var boundary in new[] { typeof(AdmissionFeeSelectionAppService), typeof(AdmissionFeeSelectionsController) })
			boundary.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>()
				.Select(attribute => attribute.Policy).ShouldContain(permission);
	}

	[Fact]
	public void Fee_selection_routes_are_application_scoped_and_use_distinct_commands()
	{
		typeof(AdmissionFeeSelectionsController).GetCustomAttribute<RouteAttribute>()!.Template.ShouldBe(
			"api/student/enrollment/admissions/fee-selections/applications/{applicationId:guid}");
		typeof(AdmissionFeeSelectionsController).GetMethod(nameof(AdmissionFeeSelectionsController.GetCandidatesAsync))!
			.GetCustomAttribute<HttpGetAttribute>()!.Template.ShouldBe("candidates");
		typeof(AdmissionFeeSelectionsController).GetMethod(nameof(AdmissionFeeSelectionsController.SaveAsync))!
			.GetCustomAttribute<HttpPutAttribute>().ShouldNotBeNull();
		typeof(AdmissionFeeSelectionsController).GetMethod(nameof(AdmissionFeeSelectionsController.ConfirmOnboardingAsync))!
			.GetCustomAttribute<HttpPostAttribute>()!.Template.ShouldBe("confirm-onboarding");
	}

	[Fact]
	public void Commands_cannot_supply_saved_prices_revision_or_confirmation_actor()
	{
		typeof(SaveAdmissionFeeSelectionDto).GetProperties().Select(property => property.Name)
			.OrderBy(name => name).ShouldBe(new[] { "ConcurrencyStamp", "OptionalHeadIds", "StructureId" });
		typeof(ConfirmAdmissionFeeSelectionDto).GetProperties().Select(property => property.Name)
			.ShouldBe(new[] { "ConcurrencyStamp" });
		typeof(AdmissionFeeSelectionHeadDto).GetProperty(nameof(AdmissionFeeSelectionHeadDto.ConfiguredAmount))!
			.PropertyType.ShouldBe(typeof(decimal));
		typeof(AdmissionFeeSelectionSnapshotDto).GetProperty(nameof(AdmissionFeeSelectionSnapshotDto.SavedAt))!
			.PropertyType.ShouldBe(typeof(DateTimeOffset));
	}
}
