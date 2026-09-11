using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Eligibility_missing_evaluation_cannot_be_bypassed_with_a_generic_admin_override() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(2021, 4, 1));
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			var blocked = await Should.ThrowAsync<BusinessException>(() => _applicationAppService.AdvanceWorkflowAsync(fixture.ApplicationId,
				new() { ExpectedCurrentStageInstanceId = fixture.StageId, ConcurrencyStamp = application.ConcurrencyStamp,
					IdempotencyKey = "eligibility-admin-bypass", AdminOverride = true, Reason = "An override must not substitute for eligibility evidence." }));
			blocked.Code.ShouldBe(AdmissionEligibilityErrorCodes.EvaluationRequired);
			(await _applicationAppService.GetWorkflowAsync(fixture.ApplicationId)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
			(await GetRequiredService<IAdmissionEligibilityAppService>().GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).ShouldBeEmpty();
		});

	[Fact]
	public Task Eligibility_explicit_negative_route_allows_review_but_cannot_authorize_finalization() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(2022, 4, 1), routeIneligibleToReview: true);
			var state = await EvaluateEligibilityAsync(fixture);
			state.Status.ShouldBe("ineligible");
			state.CanComplete.ShouldBeTrue();
			await SeedEligibilityFormSpoofAsync(fixture.ApplicationId, "eligibility.status", "eligible");
			await AdvanceAssessmentAsync(fixture.ApplicationId, fixture.StageId);
			(await _applicationAppService.GetWorkflowAsync(fixture.ApplicationId)).CurrentStageInstanceId.ShouldBe(fixture.StageIds["PLACEMENT_REVIEW"]);
			await CompleteCertificationWorkflowAsync(fixture.ApplicationId);
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			application = await _applicationAppService.MarkInReviewAsync(application.Id, Decision(application, "Placement review completed."));
			application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Administrative approval does not replace eligibility."));
			application = await _applicationAppService.IssueOfferAsync(application.Id, Decision(application, "Offer issued.", DateTime.UtcNow.AddDays(7)));
			application = await _applicationAppService.AcceptOfferAsync(application.Id, Decision(application, "Offer accepted."));
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeFalse();
			var denied = await Should.ThrowAsync<BusinessException>(() => FinalizeAsync(application, true, "failed-eligibility-route"));
			denied.Code.ShouldBe(AdmissionEligibilityErrorCodes.EvaluationRequired);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id))).ShouldBe(0);
		});

	[Fact]
	public Task Eligibility_finalization_rechecks_external_changes_but_successful_retries_preserve_the_historical_decision() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateEligibilityFixtureAsync(EligibilityPolicyJson([EligibilityAgeCriterion()]), new DateTime(2021, 4, 1));
			var service = GetRequiredService<IAdmissionEligibilityAppService>();
			await EvaluateEligibilityAsync(fixture);
			await CompleteCertificationWorkflowAsync(fixture.ApplicationId);
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			application = await _applicationAppService.MarkInReviewAsync(application.Id, Decision(application, "Reviewed eligibility and identity."));
			application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Approved admission."));
			application = await _applicationAppService.IssueOfferAsync(application.Id, Decision(application, "Offer issued.", DateTime.UtcNow.AddDays(7)));
			application = await _applicationAppService.AcceptOfferAsync(application.Id, Decision(application, "Offer accepted."));
			(await service.GetAsync(fixture.ApplicationId, fixture.StageId)).IsStale.ShouldBeFalse();
			var before = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, new DateTime(2022, 4, 1));
			(await _applicationAppService.GetAsync(fixture.ApplicationId)).ConcurrencyStamp.ShouldBe(application.ConcurrencyStamp);
			(await _applicationAppService.GetFinalizationPreviewAsync(fixture.ApplicationId)).IsReady.ShouldBeFalse();
			var stale = await Should.ThrowAsync<BusinessException>(() => FinalizeAsync(application, true, "eligibility-finalization"));
			stale.Code.ShouldBe(AdmissionEligibilityErrorCodes.StaleEvaluation);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(before);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == fixture.ApplicationId))).ShouldBe(0);
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("ineligible");
			var stillIneligible = await _applicationAppService.GetAsync(fixture.ApplicationId);
			await Should.ThrowAsync<BusinessException>(() => FinalizeAsync(stillIneligible, true, "eligibility-finalization"));
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == fixture.ApplicationId))).ShouldBe(0);
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, new DateTime(2021, 4, 1));
			(await EvaluateEligibilityAsync(fixture)).Status.ShouldBe("eligible");
			application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			var finalized = await FinalizeAsync(application, true, "eligibility-finalization");
			await UpdateEligibilityStudentBirthDateAsync(finalized.StudentId, new DateTimeOffset(2023, 4, 1, 0, 0, 0, TimeSpan.Zero));
			var retry = await FinalizeAsync(application, true, "eligibility-finalization");
			retry.StudentId.ShouldBe(finalized.StudentId);
			retry.OnboardingCaseId.ShouldBe(finalized.OnboardingCaseId);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(before + 1);
			var historical = await service.GetAsync(fixture.ApplicationId, fixture.StageId);
			historical.IsStale.ShouldBeFalse();
			historical.CanEvaluate.ShouldBeFalse();
			historical.LatestEvaluation!.Outcome.ShouldBe("eligible");
			(await service.GetHistoryAsync(fixture.ApplicationId, fixture.StageId)).Count.ShouldBe(3);
		});
}
