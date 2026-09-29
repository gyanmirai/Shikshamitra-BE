using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Uow;
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
	[InlineData("EnrollAsync", ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
	[InlineData("GetEnrollmentOptionsAsync", ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize)]
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
	public void Finalization_requires_a_transactional_unit_of_work()
	{
		var method = typeof(AdmissionApplicationAppService).GetMethod(nameof(AdmissionApplicationAppService.FinalizeAsync))!;
		method.GetCustomAttributes(typeof(UnitOfWorkAttribute), inherit: true)
			.Cast<UnitOfWorkAttribute>()
			.ShouldContain(attribute => attribute.IsTransactional == true);
	}

	[Fact]
	public void Reserved_student_identity_is_internal_and_cannot_be_supplied_through_admission_contracts()
	{
		Type[] contracts =
		[
			typeof(CreateAdmissionInquiryDto),
			typeof(UpdateAdmissionInquiryDto),
			typeof(UpdateAdmissionApplicantProfileDto),
			typeof(FinalizeAdmissionApplicationDto),
			typeof(AdmissionApplicantDto)
		];

		foreach (var contract in contracts)
			contract.GetProperty(nameof(AdmissionApplicant.ReservedStudentId)).ShouldBeNull();
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
