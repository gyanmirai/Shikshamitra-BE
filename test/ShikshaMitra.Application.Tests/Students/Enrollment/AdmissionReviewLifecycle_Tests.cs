using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_draft_and_recommendation_preserve_history_without_recording_an_admission_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(checklist:
				[new { code = "IDENTITY", name = "Identity checked", required = true }]));
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var initial = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			initial.ReviewEnabled.ShouldBeTrue();
			initial.Status.ShouldBe("notStarted");
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).ShouldBeEmpty();
			var draft = await service.SaveDraftAsync(fixture.ApplicationId, fixture.StageId,
				new() { ConcurrencyStamp = initial.ConcurrencyStamp, Notes = "Awaiting independent identity confirmation." });
			draft.Status.ShouldBe("draft");
			draft.Decision.CanApprove.ShouldBeFalse();
			var draftId = draft.LatestRecord!.Id;
			var submitted = await SubmitReviewAsync(fixture);
			submitted.Status.ShouldBe("submitted");
			submitted.LatestRecord!.Id.ShouldNotBe(draftId);
			submitted.CanComplete.ShouldBeFalse();
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			application.ReviewDecision!.CanApprove.ShouldBeTrue();
			(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
			var history = await service.GetHistoryAsync(application.Id, fixture.StageId);
			history.Count.ShouldBe(2);
			history.Single(item => item.Id == draftId).Notes.ShouldBe("Awaiting independent identity confirmation.");
			history.All(item => item.Decision == null).ShouldBeTrue();
			await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Authorized admission decision."));
			var approved = await service.GetAsync(application.Id, fixture.StageId);
			approved.Status.ShouldBe("approved");
			approved.LatestRecord!.Decision!.DecidedBy.ShouldBe(TestUserId);
			approved.LatestRecord.Decision.RecordId.ShouldBe(submitted.LatestRecord.Id);
			approved.CanComplete.ShouldBeTrue();
			var current = await _applicationAppService.GetAsync(application.Id);
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
				new() { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = current.ConcurrencyStamp,
					IdempotencyKey = Guid.NewGuid().ToString("N"), SkipCurrentStage = true, AdminOverride = true,
					Reason = "A completed review must remain available for historical re-review." })))
				.Code.ShouldBe(AdmissionApplicationErrorCodes.ManualSkipNotAllowed);
			(await AdvanceAssessmentAsync(application.Id, fixture.StageId)).CurrentStageInstanceId.ShouldBe(fixture.StageIds["ADMISSION"]);
		});

	[Fact]
	public Task Review_submission_enforces_policy_checklist_and_reason_despite_client_supplied_labels_and_required_flags() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(checklist:
				[new { code = "IDENTITY", name = "Identity checked", required = true }]));
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var state = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			var input = new AdmissionReviewSaveDto
			{
				ConcurrencyStamp = state.ConcurrencyStamp, Recommendation = "approve", Reason = "Reviewed evidence.",
				Checklist = [new() { Code = "IDENTITY", Checked = false, Required = false, Name = "Optional attacker label" }]
			};
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(fixture.ApplicationId, fixture.StageId, input)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.InvalidInput);
			input.Checklist[0].Checked = true;
			input.Reason = " ";
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(fixture.ApplicationId, fixture.StageId, input)))
				.Code.ShouldBe(AdmissionReviewErrorCodes.InvalidInput);
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).ShouldBeEmpty();
			input.Reason = "Identity confirmed against its authoritative source.";
			var result = await service.SubmitAsync(fixture.ApplicationId, fixture.StageId, input);
			result.LatestRecord!.Checklist.Single().Name.ShouldBe("Identity checked");
			result.LatestRecord.Checklist.Single().Required.ShouldBeTrue();
		});

	[Fact]
	public Task Review_new_draft_supersedes_a_submitted_recommendation_and_old_concurrency_cannot_overwrite_it() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync();
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var submitted = await SubmitReviewAsync(fixture);
			var oldStamp = submitted.ConcurrencyStamp;
			var draft = await service.SaveDraftAsync(fixture.ApplicationId, fixture.StageId,
				new() { ConcurrencyStamp = oldStamp, Notes = "Reviewer is reconsidering the recommendation." });
			draft.ConcurrencyStamp.ShouldNotBe(oldStamp);
			draft.Decision.CanApprove.ShouldBeFalse();
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
				Decision(application, "A superseded recommendation must not authorize approval."))))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ReviewRequired);
			(await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(application.Id, fixture.StageId,
				new() { ConcurrencyStamp = oldStamp, Recommendation = "approve", Reason = "Stale submit." })))
				.Code.ShouldBe(AdmissionReviewErrorCodes.ConcurrencyConflict);
			(await service.GetHistoryAsync(application.Id, fixture.StageId)).Count.ShouldBe(2);
			(await service.GetAsync(application.Id, fixture.StageId)).LatestRecord!.Id.ShouldBe(draft.LatestRecord!.Id);
		});

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public Task Review_cannot_be_bypassed_by_completion_skip_or_admin_override(bool skip, bool adminOverride) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync();
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "review.decision", "approve");
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			var denied = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(application.Id,
				new() { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = application.ConcurrencyStamp,
					IdempotencyKey = Guid.NewGuid().ToString("N"), SkipCurrentStage = skip, AdminOverride = adminOverride,
					Reason = "A workflow override must not invent the required review decision." }));
			denied.Code.ShouldBe(skip ? AdmissionApplicationErrorCodes.ManualSkipNotAllowed : AdmissionReviewErrorCodes.ReviewRequired);
			(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
			(await GetRequiredService<IAdmissionReviewAppService>().GetHistoryAsync(application.Id, fixture.StageId)).ShouldBeEmpty();
		});

	[Fact]
	public Task Review_disabled_policy_keeps_basic_completion_and_existing_application_decisions() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(enabled: false));
			var workflow = await _applicationAppService.GetWorkflowAsync(fixture.ApplicationId);
			workflow.Stages.Single(item => item.Id == fixture.StageId).ReviewEnabled.ShouldBeFalse();
			(await AdvanceAssessmentAsync(fixture.ApplicationId, fixture.StageId)).CurrentStageInstanceId.ShouldBe(fixture.StageIds["ADMISSION"]);
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			var approved = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Existing basic approval."));
			approved.Status.ShouldBe(AdmissionApplicationStatus.Approved);
		});
}
