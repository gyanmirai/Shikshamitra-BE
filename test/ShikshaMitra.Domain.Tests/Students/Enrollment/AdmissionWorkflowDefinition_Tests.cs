using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionWorkflowDefinition_Tests
{
	[Fact]
	public void NewWorkflowIsDraftAndNormalizesCode()
	{
		var workflow = new AdmissionWorkflowDefinition(
			Guid.NewGuid(), null, "Standard admission", " standard_v1 ", description: "Grade 5 intake");
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		workflow.IsDeleted.ShouldBeFalse();
		workflow.Code.ShouldBe("STANDARD_V1");
		workflow.Description.ShouldBe("Grade 5 intake");
	}

	[Fact]
	public void PublishedWorkflowCannotBeEdited()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		workflow.Publish(null);
		Should.Throw<BusinessException>(() => workflow.UpdateDetails("Changed", "CHANGED"));
	}

	[Fact]
	public void Inquiry_form_default_can_change_in_draft_but_is_frozen_after_publication()
	{
		var inquiryFormId = Guid.NewGuid();
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1",
			defaultInquiryFormDefinitionId: inquiryFormId);
		workflow.DefaultInquiryFormDefinitionId.ShouldBe(inquiryFormId);

		workflow.SetDefaultInquiryForm(null);
		workflow.DefaultInquiryFormDefinitionId.ShouldBeNull();
		workflow.Publish(null);
		Should.Throw<BusinessException>(() => workflow.SetDefaultInquiryForm(inquiryFormId));
	}

	[Fact]
	public void PublishedWorkflowCanBeArchivedButDraftCannot()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		Should.Throw<BusinessException>(() => workflow.Archive());

		workflow.Publish(null);
		workflow.Archive();

		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Archived);
	}

	[Theory]
	[InlineData(AdmissionWorkflowStatus.Published, false, AdmissionWorkflowStatus.Published)]
	[InlineData(AdmissionWorkflowStatus.Published, true, AdmissionWorkflowStatus.Superseded)]
	[InlineData(AdmissionWorkflowStatus.Superseded, false, AdmissionWorkflowStatus.Superseded)]
	public void Reopen_preserves_identity_publication_and_immutability(AdmissionWorkflowStatus previous, bool anotherPublished, AdmissionWorkflowStatus expected)
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		var publishedAt = DateTime.UtcNow.AddDays(-2);
		var publisher = Guid.NewGuid();
		workflow.Publish(publisher, publishedAt);
		if (previous == AdmissionWorkflowStatus.Superseded) workflow.Supersede();
		workflow.Archive();
		workflow.Reopen(previous, anotherPublished);
		workflow.Status.ShouldBe(expected);
		workflow.Version.ShouldBe(1);
		workflow.PublishedAt.ShouldBe(publishedAt);
		workflow.PublishedBy.ShouldBe(publisher);
		Should.Throw<BusinessException>(() => workflow.UpdateDetails("Changed", "CHANGED"));
		Should.Throw<BusinessException>(() => workflow.Reopen(previous, anotherPublished));
	}

	[Fact]
	public void Reopen_rejects_drafts_and_editable_target_statuses()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		Should.Throw<BusinessException>(() => workflow.Reopen(AdmissionWorkflowStatus.Published, false));
		workflow.Publish(null);
		workflow.Archive();
		Should.Throw<BusinessException>(() => workflow.Reopen(AdmissionWorkflowStatus.Draft, false));
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Archived);
	}

	[Fact]
	public void Maker_checker_lifecycle_requires_an_independent_assigned_approver()
	{
		var maker = Guid.NewGuid();
		var checker = Guid.NewGuid();
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1",
			ownerUserId: maker);

		workflow.MarkValidated(maker, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => workflow.SubmitForApproval(maker, maker, "Initial release", DateTime.UtcNow))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.IndependentApproverRequired);

		workflow.SubmitForApproval(maker, checker, "Initial release", DateTime.UtcNow);
		Should.Throw<BusinessException>(() => workflow.Approve(Guid.NewGuid(), DateTime.UtcNow))
			.Code.ShouldBe(AdmissionWorkflowErrorCodes.AssignedApproverRequired);

		workflow.Approve(checker, DateTime.UtcNow);
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Approved);
		workflow.ApprovedBy.ShouldBe(checker);
	}

	[Fact]
	public void Approved_workflow_can_be_scheduled_published_and_superseded()
	{
		var maker = Guid.NewGuid();
		var checker = Guid.NewGuid();
		var effectiveAt = DateTime.UtcNow.AddHours(1);
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");

		workflow.MarkValidated(maker, DateTime.UtcNow);
		workflow.SubmitForApproval(maker, checker, "Release for next intake", DateTime.UtcNow);
		workflow.Approve(checker, DateTime.UtcNow);
		workflow.Schedule(effectiveAt);
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Scheduled);
		workflow.ScheduledPublishAt.ShouldNotBeNull();
		workflow.ScheduledPublishAt.Value.ShouldBe(effectiveAt, TimeSpan.FromSeconds(1));

		workflow.Publish(checker, effectiveAt, "Release for next intake");
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Published);
		workflow.Supersede();
		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Superseded);
	}

	[Fact]
	public void Rejection_returns_submission_to_editable_draft()
	{
		var maker = Guid.NewGuid();
		var checker = Guid.NewGuid();
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		workflow.MarkValidated(maker, DateTime.UtcNow);
		workflow.SubmitForApproval(maker, checker, "Initial release", DateTime.UtcNow);

		workflow.Reject(checker, "Add the missing exception route");

		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		workflow.ChangeReason.ShouldBe("Add the missing exception route");
		Should.NotThrow(() => workflow.UpdateDetails("Revised admission", "STANDARD_V1"));
	}

	[Fact]
	public void Editing_a_validated_workflow_invalidates_validation_and_returns_it_to_draft()
	{
		var workflow = new AdmissionWorkflowDefinition(Guid.NewGuid(), null, "Standard admission", "STANDARD_V1");
		workflow.MarkValidated(Guid.NewGuid(), DateTime.UtcNow);

		workflow.UpdateDetails("Revised admission", "STANDARD_V1");

		workflow.Status.ShouldBe(AdmissionWorkflowStatus.Draft);
		workflow.ValidatedAt.ShouldBeNull();
		workflow.ValidatedBy.ShouldBeNull();
	}
}
