using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOffering_Tests
{
	[Fact]
	public void Offering_normalizes_code_and_defaults_active()
	{
		var offering = CreateOffering(useCycleDates: false,
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(5.5)),
			new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.FromHours(5.5)));

		offering.Code.ShouldBe("GRADE-5");
		offering.Name.ShouldBe("GRADE-5");
		offering.IsActive.ShouldBeTrue();
		offering.Track.ShouldBe(AdmissionTrack.General);
		offering.EntryMode.ShouldBe(AdmissionEntryMode.Inquiry);
	}

	[Fact]
	public void Direct_registration_track_is_independent_of_workflow_configuration()
	{
		var offering = CreateOffering(useCycleDates: true, null, null);

		offering.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration, identityLocked: false);

		offering.Track.ShouldBe(AdmissionTrack.Rte);
		offering.EntryMode.ShouldBe(AdmissionEntryMode.DirectRegistration);
		Should.Throw<BusinessException>(() => offering.UpdateRouting(
			AdmissionTrack.General, AdmissionEntryMode.Inquiry, identityLocked: true))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingIdentityLocked);
	}

	[Fact]
	public void Offering_persists_a_user_friendly_name()
	{
		var offering = new AdmissionOffering(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " grade-5 ",
			true, null, null, "  Grade 5 General Admission  ");

		offering.Name.ShouldBe("Grade 5 General Admission");
		offering.Code.ShouldBe("GRADE-5");
	}

	[Fact]
	public void Offering_rejects_reverse_date_range()
	{
		Should.Throw<BusinessException>(() => CreateOffering(useCycleDates: false,
			new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingDateRangeInvalid);
	}

	[Fact]
	public void Cycle_dates_are_inherited_without_copying_overrides()
	{
		var offering = CreateOffering(useCycleDates: true, null, null);

		offering.OpenDate.ShouldBeNull();
		offering.CloseDate.ShouldBeNull();
		offering.GetEffectiveOpenDate(new DateTime(2026, 10, 1)).Date.ShouldBe(new DateTime(2026, 10, 1));
		offering.GetEffectiveCloseDate(new DateTime(2027, 3, 31)).Date.ShouldBe(new DateTime(2027, 3, 31));
	}

	[Fact]
	public void Identity_cannot_change_after_runtime_usage()
	{
		var offering = CreateOffering(useCycleDates: true, null, null);

		Should.Throw<BusinessException>(() => offering.UpdateIdentity(
			offering.CampusId, offering.BoardId, offering.MediumId, offering.StageId, Guid.NewGuid(), identityLocked: true))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingIdentityLocked);
	}

	[Fact]
	public void Availability_is_open_only_for_ready_active_published_offering_inside_window()
	{
		var offering = CreateOffering(useCycleDates: true, null, null);
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "2026-27", "2026-27", Guid.NewGuid(),
			new DateTime(2026, 8, 1), new DateTime(2027, 3, 31));
		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 7, 1));

		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, cycle, configurationReady: true,
			new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero));

		availability.IsAvailable.ShouldBeTrue();
		availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.Open);
		availability.CanStartApplication.ShouldBeTrue();
	}

	[Fact]
	public void Missing_configuration_has_deterministic_availability_reason()
	{
		var offering = CreateOffering(useCycleDates: true, null, null);
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "2026-27", "2026-27", Guid.NewGuid(),
			new DateTime(2026, 8, 1), new DateTime(2027, 3, 31));
		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 7, 1));

		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, cycle, configurationReady: false,
			new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero));

		availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.ConfigurationIncomplete);
		availability.ReasonCode.ShouldBe(AdmissionErrorCodes.OfferingConfigurationIncomplete);
	}

	[Fact]
	public void Availability_is_not_open_before_effective_window()
	{
		var offering = CreateOffering(false,
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2027, 3, 31, 0, 0, 0, TimeSpan.Zero));
		var cycle = CreatePublishedCycle();

		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, cycle, true, new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(5.5)));

		availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.NotOpenYet);
		availability.CanCreateInquiry.ShouldBeFalse();
	}

	[Fact]
	public void Availability_is_closed_after_effective_window()
	{
		var offering = CreateOffering(false,
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero));
		var cycle = CreatePublishedCycle();

		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, cycle, true, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

		availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.Closed);
		availability.CanStartApplication.ShouldBeFalse();
	}

	[Fact]
	public void Availability_is_inactive_even_when_configuration_is_ready()
	{
		var offering = CreateOffering(true, null, null);
		offering.Deactivate();

		var availability = AdmissionOfferingAvailabilityEvaluator.Evaluate(
			offering, CreatePublishedCycle(), true,
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

		availability.Status.ShouldBe(AdmissionOfferingAvailabilityStatus.Inactive);
		availability.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void Availability_includes_both_open_and_close_boundary_dates()
	{
		var offering = CreateOffering(false,
			new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(5.5)),
			new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.FromHours(5.5)));
		var cycle = CreatePublishedCycle();

		AdmissionOfferingAvailabilityEvaluator.Evaluate(offering, cycle, true,
			new DateTimeOffset(2026, 10, 1, 23, 59, 59, TimeSpan.FromHours(5.5))).IsAvailable.ShouldBeTrue();
		AdmissionOfferingAvailabilityEvaluator.Evaluate(offering, cycle, true,
			new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(5.5))).IsAvailable.ShouldBeTrue();
	}

	[Fact]
	public void Future_workflow_defaults_can_change_without_rewriting_identity()
	{
		var offering = CreateOffering(true, null, null);
		var workflowV2 = Guid.NewGuid();

		offering.ConfigureReferences(workflowV2, Guid.NewGuid(), Guid.NewGuid());

		offering.WorkflowDefinitionId.ShouldBe(workflowV2);
		offering.StandardId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Existing_application_keeps_frozen_workflow_and_form_when_offering_defaults_change()
	{
		var offering = CreateOffering(true, null, null);
		var workflowV1 = Guid.NewGuid();
		var applicationFormV1 = Guid.NewGuid();
		offering.ConfigureReferences(workflowV1, Guid.NewGuid(), applicationFormV1);
		var application = new AdmissionApplication(
			Guid.NewGuid(), offering.TenantId, "APP/2026/00001", Guid.NewGuid(), null,
			offering.AdmissionCycleId, offering.Id, workflowV1, applicationFormV1);

		offering.ConfigureReferences(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

		application.AdmissionOfferingId.ShouldBe(offering.Id);
		application.WorkflowDefinitionId.ShouldBe(workflowV1);
		application.FormDefinitionId.ShouldBe(applicationFormV1);
	}

	private static AdmissionCycle CreatePublishedCycle()
	{
		var cycle = new AdmissionCycle(Guid.NewGuid(), Guid.NewGuid(), "2026-27", "2026-27", Guid.NewGuid(),
			new DateTime(2026, 8, 1), new DateTime(2027, 3, 31));
		cycle.Publish(Guid.NewGuid(), new DateTime(2026, 7, 1));
		return cycle;
	}

	private static AdmissionOffering CreateOffering(
		bool useCycleDates,
		DateTimeOffset? openDate,
		DateTimeOffset? closeDate) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " grade-5 ",
		useCycleDates, openDate, closeDate);
}
