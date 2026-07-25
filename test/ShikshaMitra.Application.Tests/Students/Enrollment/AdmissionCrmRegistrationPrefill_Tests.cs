using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmRegistrationPrefill_Tests
{
	[Fact]
	public void Should_Prefer_Immutable_Route_Name_Snapshot()
	{
		var definitionId = Guid.NewGuid();
		var lead = CreateLead(
			AdmissionCrmRouteType.Fresh,
			definitionId,
			3,
			"Fresh admission 2026");

		var prefill = AdmissionCrmRegistrationAppService.BuildPrefill(lead);

		prefill["admissionRoute"].ShouldBe("Fresh admission 2026");
		prefill["admissionRouteDefinitionId"].ShouldBe(definitionId);
		prefill["admissionRouteDefinitionVersion"].ShouldBe(3);
	}

	[Fact]
	public void Should_Fall_Back_To_Legacy_Route_Type_When_Snapshot_Is_Missing()
	{
		var lead = CreateLead(AdmissionCrmRouteType.SiblingPriority);

		var prefill = AdmissionCrmRegistrationAppService.BuildPrefill(lead);

		prefill["admissionRoute"].ShouldBe(nameof(AdmissionCrmRouteType.SiblingPriority));
		prefill["admissionRouteDefinitionId"].ShouldBeNull();
		prefill["admissionRouteDefinitionVersion"].ShouldBeNull();
	}

	private static AdmissionInquiryLead CreateLead(
		AdmissionCrmRouteType? routeType,
		Guid? routeDefinitionId = null,
		int? routeDefinitionVersion = null,
		string? routeNameSnapshot = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			null,
			null,
			AdmissionInquiryChannel.ManualBackOffice,
			null,
			null,
			null,
			"Legacy",
			null,
			"Applicant",
			null,
			"Parent",
			"9876543210",
			null,
			"Admission inquiry",
			null,
			"Grade 1",
			null,
			DateTimeOffset.UtcNow,
			routeType,
			routeDefinitionId,
			routeDefinitionVersion,
			routeNameSnapshot);
}
