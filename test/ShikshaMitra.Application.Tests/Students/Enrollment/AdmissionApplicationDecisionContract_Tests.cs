using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionApplicationDecisionContract_Tests
{
	[Theory]
	[InlineData(nameof(AdmissionApplicationAppService.MarkInReviewAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[InlineData(nameof(AdmissionApplicationAppService.ApproveAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[InlineData(nameof(AdmissionApplicationAppService.RejectAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[InlineData(nameof(AdmissionApplicationAppService.WaitlistAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Decide)]
	[InlineData(nameof(AdmissionApplicationAppService.IssueOfferAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	[InlineData(nameof(AdmissionApplicationAppService.AcceptOfferAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	[InlineData(nameof(AdmissionApplicationAppService.DeclineOfferAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Offer)]
	[InlineData(nameof(AdmissionApplicationAppService.GetFinalizationPreviewAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View)]
	[InlineData(nameof(AdmissionApplicationAppService.FinalizeAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	[InlineData(nameof(AdmissionApplicationAppService.EnrollAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Enroll)]
	[InlineData(nameof(AdmissionApplicationAppService.GetEnrollmentOptionsAsync), ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Enroll)]
	public void Decision_and_finalization_commands_require_the_expected_permission(string methodName, string permission)
	{
		var method = typeof(AdmissionApplicationAppService).GetMethods().Single(value => value.Name == methodName);

		method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
			.Cast<AuthorizeAttribute>()
			.ShouldContain(attribute => attribute.Policy == permission);
	}

	[Fact]
	public void Finalization_contract_stops_before_operational_enrollment()
	{
		var inputProperties = typeof(FinalizeAdmissionApplicationDto).GetProperties().Select(value => value.Name).ToList();
		var resultProperties = typeof(AdmissionFinalizationResultDto).GetProperties().Select(value => value.Name).ToList();

		inputProperties.ShouldContain(nameof(FinalizeAdmissionApplicationDto.IdempotencyKey));
		inputProperties.ShouldContain(nameof(FinalizeAdmissionApplicationDto.ExistingStudentId));
		inputProperties.ShouldNotContain("DivisionId");
		inputProperties.ShouldNotContain("RollNumber");
		resultProperties.ShouldContain(nameof(AdmissionFinalizationResultDto.OnboardingCaseId));
		resultProperties.ShouldNotContain("StudentEnrollmentId");
	}

	[Fact]
	public void Enrollment_options_contract_exposes_capacity_fields()
	{
		var option = new AdmissionEnrollmentDivisionOptionDto
		{
			Id = Guid.NewGuid(),
			Name = "A",
			Strength = 30,
			OccupiedSeats = 29,
			AvailableSeats = 1,
			IsAvailable = true
		};

		option.AvailableSeats.ShouldBe(1);
		option.IsAvailable.ShouldBeTrue();
	}
}
