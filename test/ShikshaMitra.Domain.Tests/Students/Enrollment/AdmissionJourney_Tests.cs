using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionJourney_Tests
{
	[Theory]
	[InlineData(AdmissionWorkflowStageType.Form)]
	[InlineData(AdmissionWorkflowStageType.Assessment)]
	[InlineData(AdmissionWorkflowStageType.Payment)]
	[InlineData(AdmissionWorkflowStageType.DocumentVerification)]
	[InlineData(AdmissionWorkflowStageType.Approval)]
	public void Journey_can_end_at_every_active_stage_without_losing_completed_evidence(AdmissionWorkflowStageType type)
	{
		var application = NewApplication();
		var workflow = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), application.TenantId, application.Id, application.WorkflowDefinitionId);
		var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), application.TenantId, workflow.Id, Guid.NewGuid(), "CURRENT", "Current stage", type, 1, true);
		application.SetCurrentStage(stage.Id);
		var actor = Guid.NewGuid();
		Should.Throw<BusinessException>(() => application.EndJourney(actor, " "));
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft);
		application.EndJourney(actor, " Parent withdrew ");
		stage.Cancel(actor, application.DecisionReason!);
		workflow.Cancel();
		application.Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		application.CurrentStageInstanceId.ShouldBeNull();
		stage.CompletionReason.ShouldBe("Parent withdrew");
		stage.Status.ShouldBe(AdmissionRuntimeStageStatus.Cancelled);
		workflow.Status.ShouldBe(AdmissionWorkflowInstanceStatus.Cancelled);
		var at = application.DecisionAt;
		application.EndJourney(actor, "Parent withdrew");
		application.DecisionAt.ShouldBe(at);
	}

	[Fact]
	public void Journey_cannot_end_rejected_declined_finalized_or_enrolled_applications()
	{
		var actor = Guid.NewGuid();
		var rejected = NewApplication(); rejected.Submit(); rejected.Reject(actor, "Rejected");
		Should.Throw<BusinessException>(() => rejected.EndJourney(actor, "Withdrawal"));
		var declined = NewApplication(); declined.Submit(); declined.Approve(actor, null); declined.IssueOffer(DateTime.UtcNow.AddDays(2), actor, null); declined.DeclineOffer(actor, "Declined");
		Should.Throw<BusinessException>(() => declined.EndJourney(actor, "Withdrawal"));
		var finalized = NewApplication(); finalized.Submit(); finalized.Approve(actor, null); finalized.FinalizeApprovedAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
		Should.Throw<BusinessException>(() => finalized.EndJourney(actor, "Withdrawal"));
		var enrolled = NewApplication(); enrolled.Submit(); enrolled.Approve(actor, null); enrolled.IssueOffer(DateTime.UtcNow.AddDays(2), actor, null); enrolled.AcceptOffer(actor, null); enrolled.MarkEnrolled(Guid.NewGuid(), Guid.NewGuid());
		Should.Throw<BusinessException>(() => enrolled.EndJourney(actor, "Withdrawal"));
	}

	[Theory]
	[InlineData(AdmissionApplicationStatus.Submitted)]
	[InlineData(AdmissionApplicationStatus.InReview)]
	[InlineData(AdmissionApplicationStatus.Waitlisted)]
	[InlineData(AdmissionApplicationStatus.Approved)]
	[InlineData(AdmissionApplicationStatus.Offered)]
	[InlineData(AdmissionApplicationStatus.OfferAccepted)]
	public void Journey_can_end_any_pre_finalization_status(AdmissionApplicationStatus target)
	{
		var actor = Guid.NewGuid();
		var application = NewApplication();
		application.Submit();
		if (target == AdmissionApplicationStatus.InReview) application.MarkInReview(actor, null);
		if (target == AdmissionApplicationStatus.Waitlisted) application.Waitlist(actor, "Waitlisted");
		if (target is AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted)
			application.Approve(actor, null);
		if (target is AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted)
			application.IssueOffer(DateTime.UtcNow.AddDays(2), actor, "Issued offer");
		if (target == AdmissionApplicationStatus.OfferAccepted) application.AcceptOffer(actor, null);
		application.Status.ShouldBe(target);
		application.CanEndJourney.ShouldBeTrue();
		application.EndJourney(actor, "Family withdrew before onboarding");
		application.Status.ShouldBe(AdmissionApplicationStatus.Withdrawn);
		application.CanEndJourney.ShouldBeFalse();
	}

	[Fact]
	public void Journey_cancellation_preserves_previously_completed_stage_history()
	{
		var application = NewApplication();
		var workflow = new AdmissionApplicationWorkflowInstance(Guid.NewGuid(), application.TenantId, application.Id, application.WorkflowDefinitionId);
		var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), application.TenantId, workflow.Id, Guid.NewGuid(),
			"FORM", "Completed form", AdmissionWorkflowStageType.Form, 0, true);
		var actor = Guid.NewGuid();
		stage.Complete(actor, "Form submitted");
		var completedAt = stage.CompletedAt;
		stage.Cancel(actor, "Later withdrawal");
		stage.Status.ShouldBe(AdmissionRuntimeStageStatus.Completed);
		stage.CompletedAt.ShouldBe(completedAt);
		stage.CompletionReason.ShouldBe("Form submitted");
	}

	[Fact]
	public void Journey_preparation_is_limited_to_unfinished_stages_in_an_open_workflow()
	{
		var application = NewApplication();
		AdmissionStagePreparationRules.CanPrepare(application, AdmissionWorkflowInstanceStatus.Active, AdmissionRuntimeStageStatus.Pending).ShouldBeTrue();
		AdmissionStagePreparationRules.CanPrepare(application, AdmissionWorkflowInstanceStatus.Active, AdmissionRuntimeStageStatus.Active).ShouldBeTrue();
		foreach (var status in new[] { AdmissionRuntimeStageStatus.Completed, AdmissionRuntimeStageStatus.Skipped, AdmissionRuntimeStageStatus.Cancelled, AdmissionRuntimeStageStatus.Failed })
			AdmissionStagePreparationRules.CanPrepare(application, AdmissionWorkflowInstanceStatus.Active, status).ShouldBeFalse();
		AdmissionStagePreparationRules.CanPrepare(application, AdmissionWorkflowInstanceStatus.Cancelled, AdmissionRuntimeStageStatus.Pending).ShouldBeFalse();
		application.EndJourney(Guid.NewGuid(), "Closed");
		AdmissionStagePreparationRules.CanPrepare(application, AdmissionWorkflowInstanceStatus.Active, AdmissionRuntimeStageStatus.Pending).ShouldBeFalse();
	}

	private static AdmissionApplication NewApplication() => new(Guid.NewGuid(), Guid.NewGuid(), "APP-JOURNEY", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
