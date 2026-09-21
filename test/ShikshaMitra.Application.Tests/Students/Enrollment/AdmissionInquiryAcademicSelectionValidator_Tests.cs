using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryAcademicSelectionValidator_Tests
{
	[Fact]
	public void Academic_selection_is_required()
	{
		var offering = Offering();
		var input = Input(offering);
		input.BoardId = Guid.Empty;

		var error = Should.Throw<BusinessException>(() =>
			AdmissionInquiryAcademicSelectionValidator.EnsureMatches(input, offering));

		error.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	}

	[Fact]
	public void Academic_selection_must_match_the_offering()
	{
		var offering = Offering();
		var input = Input(offering);
		input.StandardId = Guid.NewGuid();

		var error = Should.Throw<BusinessException>(() =>
			AdmissionInquiryAcademicSelectionValidator.EnsureMatches(input, offering));

		error.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	}

	[Fact]
	public void Cycle_is_derived_when_the_client_does_not_supply_one()
	{
		var offering = Offering();
		var input = Input(offering);
		input.AdmissionCycleId = Guid.Empty;

		Should.NotThrow(() => AdmissionInquiryAcademicSelectionValidator.EnsureMatches(input, offering));
	}

	private static CreateAdmissionInquiryDto Input(AdmissionOffering offering) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId,
		AdmissionOfferingId = offering.Id,
		BoardId = offering.BoardId,
		MediumId = offering.MediumId,
		StandardId = offering.StandardId
	};

	private static AdmissionOffering Offering() => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), "ADM-1", true, null, null);
}
