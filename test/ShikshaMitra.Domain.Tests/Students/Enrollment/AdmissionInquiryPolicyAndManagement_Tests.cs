using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryPolicyAndManagement_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{}")] 
	[InlineData("{\"inquiry\":{\"enabled\":false}}")]
	public void Basic_or_disabled_configuration_preserves_the_existing_inquiry_flow(string? json) =>
		AdmissionInquiryPolicy.Read(json).ShouldBeNull();

	[Fact]
	public void Managed_policy_accepts_supported_operational_controls()
	{
		var policy = Policy() with
		{
			AssignmentMode = AdmissionCounsellorAssignmentMode.RoundRobin,
			CounsellorPoolUserIds = [Guid.NewGuid(), Guid.NewGuid()],
			FirstContactDueInHours = 24,
			QualificationDueInDays = 7,
			AllowedSources = [AdmissionInquirySource.Website, AdmissionInquirySource.Referral],
			RequireSourceDetailFor = [AdmissionInquirySource.Referral],
			QualificationChecklist = [new() { Code = "FAMILY_INTEREST", Name = "Family confirmed interest", Required = true }]
		};
		policy.Validate();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Unknown_policy_versions_are_rejected(int version) =>
		Should.Throw<ArgumentException>(() => (Policy() with { Version = version }).Validate());

	[Fact]
	public void Policy_rejects_invalid_assignment_sources_slas_and_checklist()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { AssignmentMode = AdmissionCounsellorAssignmentMode.RoundRobin }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { FirstContactDueInHours = 0 }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { QualificationDueInDays = 366 }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AllowedSources = [AdmissionInquirySource.Website], RequireSourceDetailFor = [AdmissionInquirySource.Other] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { QualificationChecklist = [new() { Code = "bad code", Name = "Bad" }] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { QualificationChecklist = [new() { Code = "A", Name = "One" }, new() { Code = "a", Name = "Two" }] }).Validate());
	}

	[Fact]
	public void Management_records_contact_and_outcome_without_exposing_public_setters()
	{
		var now = DateTime.UtcNow;
		var state = new AdmissionInquiryManagement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"{\"version\":1}", now, 24, 7);
		state.FirstContactDueAt.ShouldBe(now.AddHours(24));
		state.QualificationDueAt.ShouldBe(now.AddDays(7));
		state.RecordContact(now.AddHours(1));
		state.ContactCount.ShouldBe(1);
		state.RecordOutcome(AdmissionInquiryQualificationOutcome.Qualified, "[]", null,
			"  Ready to apply  ", Guid.NewGuid(), now.AddHours(2));
		state.Outcome.ShouldBe(AdmissionInquiryQualificationOutcome.Qualified);
		state.OutcomeReason.ShouldBe("Ready to apply");
		typeof(AdmissionInquiryManagement).GetProperties().Where(item => item.DeclaringType == typeof(AdmissionInquiryManagement))
			.ShouldAllBe(item => item.SetMethod == null || !item.SetMethod.IsPublic);
	}

	[Fact]
	public void Follow_up_has_guarded_one_way_completion()
	{
		var item = new AdmissionInquiryFollowUp(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), DateTime.UtcNow.AddDays(1), "Discuss the application route");
		item.Status.ShouldBe(AdmissionInquiryFollowUpStatus.Scheduled);
		item.Complete(Guid.NewGuid(), DateTime.UtcNow, "Guardian confirmed the next step.");
		item.Status.ShouldBe(AdmissionInquiryFollowUpStatus.Completed);
		Should.Throw<BusinessException>(() => item.Cancel(Guid.NewGuid(), DateTime.UtcNow, "Too late"));
	}

	[Fact]
	public void Managed_inquiry_must_be_the_non_skippable_entry_stage()
	{
		var workflowId = Guid.NewGuid();
		var managed = Stage(workflowId, false, false);
		AdmissionInquiryGraphRules.Validate([managed]).ShouldContain(issue =>
			issue.Code == AdmissionInquiryManagementErrorCodes.InvalidPolicy);
		managed = Stage(workflowId, true, true);
		AdmissionInquiryGraphRules.Validate([managed]).ShouldContain(issue =>
			issue.Code == AdmissionInquiryManagementErrorCodes.InvalidPolicy);
		AdmissionInquiryGraphRules.Validate([Stage(workflowId, true, false)]).ShouldBeEmpty();
	}

	private static AdmissionInquiryPolicy Policy() => new() { Version = 1, Enabled = true };

	private static AdmissionWorkflowStageDefinition Stage(Guid workflowId, bool entry, bool allowSkip) => new(
		Guid.NewGuid(), workflowId, "INQUIRY", "Inquiry", AdmissionWorkflowStageType.Inquiry, 0, true, allowSkip,
		null, "{\"inquiry\":{\"version\":1,\"enabled\":true}}", entry, false);
}
