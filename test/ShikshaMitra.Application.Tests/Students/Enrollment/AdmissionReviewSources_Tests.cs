using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_selected_failed_assessment_is_not_replaced_by_a_later_passing_result_or_form_facts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var assessment = JsonSerializer.Serialize(new { assessment = new AdmissionAssessmentPolicy
				{ Version = 1, Enabled = true, ScoringEnabled = true, TotalMarks = 100, PassingMarks = 50, MaxAttempts = 1 } });
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(prerequisites:
				[new { kind = "assessment", sourceStageCode = "EXAM" }]), priorStages:
			[
				new() { Code = "EXAM", Name = "Entrance exam", StageType = AdmissionWorkflowStageType.Assessment, IsRequired = true, ConfigurationJson = assessment },
				new() { Code = "INTERVIEW", Name = "Later interview", StageType = AdmissionWorkflowStageType.Assessment, IsRequired = true, ConfigurationJson = assessment }
			], negativeRoute: "reject", priorExitConditions: new()
				{ ["EXAM"] = "{\"field\":\"assessment.outcome\",\"operator\":\"Equals\",\"value\":\"failed\"}" });
			var assessments = GetRequiredService<IAdmissionAssessmentAppService>();
			foreach (var (code, marks) in new[] { ("EXAM", 20m), ("INTERVIEW", 90m) })
			{
				var state = await assessments.GetAsync(fixture.ApplicationId, fixture.StageIds[code]);
				state = await assessments.BookAsync(fixture.ApplicationId, fixture.StageIds[code], new() { ConcurrencyStamp = state.ConcurrencyStamp });
				var attempt = state.Attempts.Single();
				await assessments.RecordResultAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Marks = marks });
				await AdvanceAssessmentAsync(fixture.ApplicationId, fixture.StageIds[code]);
			}
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "assessment.outcome", "passed");
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "review.decision", "approve");
			var submitted = await SubmitReviewAsync(fixture, "reject");
			submitted.Prerequisites.Single().StageId.ShouldBe(fixture.StageIds["EXAM"]);
			submitted.Prerequisites.Single().Passed.ShouldBeFalse();
			submitted.Prerequisites.Single().Status.ShouldBe("failed");
			submitted.Decision.CanApprove.ShouldBeFalse();
			submitted.Decision.CanReject.ShouldBeTrue();
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.ApproveAsync(application.Id,
				Decision(application, "Later interview cannot replace the selected failed exam."))))
				.Code.ShouldBe(AdmissionReviewErrorCodes.EvidenceRequired);
			await _applicationAppService.RejectAsync(application.Id, Decision(application, "Entrance evidence does not meet the configured requirement."));
			(await AdvanceAssessmentAsync(application.Id, fixture.StageId)).CurrentStageInstanceId.ShouldBe(fixture.StageIds["REVIEW_OUTCOME"]);
		});

	[Fact]
	public Task Review_canonical_change_invalidates_selected_eligibility_and_missing_information_cannot_authorize_any_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(prerequisites:
				[new { kind = "eligibility", sourceStageCode = "ELIGIBILITY" }]), priorStages:
			[
				new() { Code = "ELIGIBILITY", Name = "Age eligibility", StageType = AdmissionWorkflowStageType.Eligibility,
					IsRequired = true, ConfigurationJson = EligibilityPolicyJson([EligibilityAgeCriterion()]) }
			]);
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, new DateTime(2021, 4, 1));
			var eligibility = GetRequiredService<IAdmissionEligibilityAppService>();
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			await eligibility.EvaluateAsync(application.Id, fixture.StageIds["ELIGIBILITY"], new() { ConcurrencyStamp = application.ConcurrencyStamp! });
			await AdvanceAssessmentAsync(application.Id, fixture.StageIds["ELIGIBILITY"]);
			var submitted = await SubmitReviewAsync(fixture);
			submitted.Prerequisites.Single().Passed.ShouldBeTrue();
			var review = GetRequiredService<IAdmissionReviewAppService>();
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, null);
			var stale = await review.GetAsync(application.Id, fixture.StageId);
			stale.ConcurrencyStamp.ShouldBe(submitted.ConcurrencyStamp);
			stale.IsStale.ShouldBeTrue();
			stale.Prerequisites.Single().IsStale.ShouldBeTrue();
			stale.Decision.CanApprove.ShouldBeFalse();
			stale.Decision.CanReject.ShouldBeFalse();
			await eligibility.EvaluateAsync(application.Id, fixture.StageIds["ELIGIBILITY"], new() { ConcurrencyStamp = stale.ConcurrencyStamp });
			var missing = await review.GetAsync(application.Id, fixture.StageId);
			missing.Prerequisites.Single().Passed.ShouldBeFalse();
			missing.Decision.CanApprove.ShouldBeFalse();
			missing.Decision.CanReject.ShouldBeFalse();
			missing.Decision.CanWaitlist.ShouldBeFalse();
			var history = await review.GetHistoryAsync(application.Id, fixture.StageId);
			history.Single().Prerequisites.Single().Passed.ShouldBeTrue();
			history.Single().Id.ShouldBe(submitted.LatestRecord!.Id);
		});

	[Fact]
	public Task Review_document_replacement_invalidates_the_prior_snapshot_without_rewriting_history() =>
		RunCertificationScenarioAsync(async () =>
		{
			var requirement = new AdmissionDocumentRequirement { Code = "BIRTH", Name = "Birth certificate", CollectionMode = "original" };
			var policy = HandoffPolicy(requirement);
			var fixture = await CreateReviewFixtureAsync(ReviewPolicyJson(prerequisites:
				[new { kind = "documents", sourceStageCode = "DOCUMENTS" }]), priorStages:
			[
				new() { Code = "DOCUMENTS", Name = "Original verification", StageType = AdmissionWorkflowStageType.DocumentVerification,
					IsRequired = true, ConfigurationJson = JsonSerializer.Serialize(new { documentVerification = policy }) }
			]);
			var documents = GetRequiredService<IAdmissionDocumentVerificationAppService>();
			var documentStage = fixture.StageIds["DOCUMENTS"];
			var state = await documents.GetAsync(fixture.ApplicationId, documentStage);
			state = await documents.RecordOriginalAsync(fixture.ApplicationId, documentStage, "BIRTH",
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Notes = "Original identity checked." });
			await documents.ReviewAsync(state.Requirements.Single().Evidence.Single().Id,
				new() { ConcurrencyStamp = state.ConcurrencyStamp, Decision = "accepted", Reason = "Original identity verified." });
			await AdvanceAssessmentAsync(fixture.ApplicationId, documentStage);
			var submitted = await SubmitReviewAsync(fixture);
			// Simulate an authoritative external correction; completed document UI does not permit replacement.
			var handoff = new DocumentHandoffFixture(await _applicationAppService.GetAsync(fixture.ApplicationId), fixture.Offering,
				documentStage, policy, null);
			await AddHandoffEvidenceAsync(handoff, requirement, version: 2, kind: "original", decision: null);
			var review = GetRequiredService<IAdmissionReviewAppService>();
			var stale = await review.GetAsync(fixture.ApplicationId, fixture.StageId);
			stale.IsStale.ShouldBeTrue();
			stale.Decision.CanApprove.ShouldBeFalse();
			stale.ConcurrencyStamp.ShouldBe(submitted.ConcurrencyStamp);
			var historical = (await review.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Single();
			historical.Prerequisites.Single().Passed.ShouldBeTrue();
			historical.Decision.ShouldBeNull();
		});
}
