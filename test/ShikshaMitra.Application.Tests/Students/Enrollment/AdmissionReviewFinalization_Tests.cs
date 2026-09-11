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
	public Task Review_historical_reaffirmation_preserves_accepted_offer_and_successful_finalization_retries()
		=> RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync();
			var review = GetRequiredService<IAdmissionReviewAppService>();
			var original = await SubmitReviewAsync(fixture);
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Admission evidence approved."));
			await CompleteCertificationWorkflowAsync(application.Id);
			application = await _applicationAppService.GetAsync(application.Id);
			application = await _applicationAppService.IssueOfferAsync(application.Id, Decision(application, "Offer issued.", DateTime.UtcNow.AddDays(7)));
			application = await _applicationAppService.AcceptOfferAsync(application.Id, Decision(application, "Guardian accepted the offer."));
			(await review.GetAsync(application.Id, fixture.StageId)).IsStale.ShouldBeFalse();
			var count = await WithUnitOfWorkAsync(() => _studentRepository.CountAsync());
			var originalDob = await WithUnitOfWorkAsync(async () => (await _applicantRepository.GetAsync(fixture.ApplicantId)).DateOfBirth);
			await UpdateEligibilityApplicantBirthDateAsync(fixture.ApplicantId, originalDob!.Value.AddDays(-1));
			(await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp.ShouldBe(application.ConcurrencyStamp);
			(await review.GetAsync(application.Id, fixture.StageId)).IsStale.ShouldBeTrue();
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeFalse();
			(await Should.ThrowAsync<BusinessException>(() => FinalizeAsync(application, true, "managed-review-finalization")))
				.Code.ShouldBe(AdmissionReviewErrorCodes.StaleReview);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(count);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id))).ShouldBe(0);
			var fresh = await SubmitReviewAsync(fixture);
			fresh.LatestRecord!.Id.ShouldNotBe(original.LatestRecord!.Id);
			application = await _applicationAppService.GetAsync(application.Id);
			application.Status.ShouldBe(AdmissionApplicationStatus.OfferAccepted);
			application.ReviewDecision!.CanApprove.ShouldBeTrue();
			application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Reaffirmed against the corrected identity."));
			application.Status.ShouldBe(AdmissionApplicationStatus.OfferAccepted);
			(await review.GetAsync(application.Id, fixture.StageId)).IsStale.ShouldBeFalse();
			var finalized = await FinalizeAsync(application, true, "managed-review-finalization");
			await UpdateEligibilityStudentBirthDateAsync(finalized.StudentId, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
			var retry = await FinalizeAsync(application, true, "managed-review-finalization");
			retry.StudentId.ShouldBe(finalized.StudentId);
			retry.OnboardingCaseId.ShouldBe(finalized.OnboardingCaseId);
			(await WithUnitOfWorkAsync(() => _studentRepository.CountAsync())).ShouldBe(count + 1);
			(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.CountAsync(item => item.ApplicationId == application.Id))).ShouldBe(1);
			var finalState = await review.GetAsync(application.Id, fixture.StageId);
			finalState.IsStale.ShouldBeFalse();
			finalState.CanSubmit.ShouldBeFalse();
			var history = await review.GetHistoryAsync(application.Id, fixture.StageId);
			history.Count.ShouldBe(2);
			history.All(item => item.Decision!.Decision == "approve").ShouldBeTrue();
			history.Select(item => item.Decision!.Id).Distinct().Count().ShouldBe(2);
		});
}
