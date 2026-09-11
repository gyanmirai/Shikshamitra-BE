using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Review_correction_reopens_the_application_form_and_resubmission_stays_at_review_for_a_new_recommendation() =>
		RunCertificationScenarioAsync(async () =>
		{
			var formId = await CreateEligibilityAnswerFormAsync(AdmissionFormFieldType.Number);
			var fixture = await CreateReviewFixtureAsync(applicationFormDefinitionId: formId);
			var service = GetRequiredService<IAdmissionReviewAppService>();
			var original = await SubmitReviewAsync(fixture, "requestCorrection");
			var correction = await service.RequestCorrectionAsync(fixture.ApplicationId, fixture.StageId,
				new() { ConcurrencyStamp = original.ConcurrencyStamp, Reason = "Please provide the corrected previous marks." });
			correction.Status.ShouldBe("correctionRequested");
			correction.CanComplete.ShouldBeFalse();
			correction.CanSubmit.ShouldBeFalse();
			correction.LatestRecord!.Decision!.Decision.ShouldBe("requestCorrection");
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			await _applicationAppService.UpdateFormValuesAsync(application.Id,
				new() { ConcurrencyStamp = application.ConcurrencyStamp, Values = [new() { FieldCode = "ANSWER", ValueJson = "90" }] });
			var resubmitted = await _applicationAppService.SubmitAsync(application.Id);
			resubmitted.ValidationErrors.ShouldBeEmpty();
			(await _applicationAppService.GetWorkflowAsync(application.Id)).CurrentStageInstanceId.ShouldBe(fixture.StageId);
			(await service.GetAsync(application.Id, fixture.StageId)).CanComplete.ShouldBeFalse();
			var newReview = await SubmitReviewAsync(fixture);
			newReview.LatestRecord!.Id.ShouldNotBe(original.LatestRecord!.Id);
			newReview.LatestRecord.Decision.ShouldBeNull();
			application = await _applicationAppService.GetAsync(application.Id);
			(await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Corrected marks reviewed and approved.")))
				.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			var history = await service.GetHistoryAsync(application.Id, fixture.StageId);
			history.Count.ShouldBe(2);
			history.Single(item => item.Id == original.LatestRecord.Id).Decision!.Reason.ShouldBe("Please provide the corrected previous marks.");
		});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Review_waitlist_requires_an_explicit_outgoing_route_and_never_uses_the_approval_default(bool explicitRoute) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateReviewFixtureAsync(negativeRoute: explicitRoute ? "waitlist" : null);
			var service = GetRequiredService<IAdmissionReviewAppService>();
			await SubmitReviewAsync(fixture, "waitlist");
			var application = await _applicationAppService.GetAsync(fixture.ApplicationId);
			application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
			await _applicationAppService.WaitlistAsync(application.Id, Decision(application, "Place the application on the reviewed waiting list."));
			var state = await service.GetAsync(application.Id, fixture.StageId);
			state.Status.ShouldBe("waitlisted");
			state.CanComplete.ShouldBe(explicitRoute);
			if (explicitRoute)
				(await AdvanceAssessmentAsync(application.Id, fixture.StageId)).CurrentStageInstanceId.ShouldBe(fixture.StageIds["REVIEW_OUTCOME"]);
			else
				(await Should.ThrowAsync<BusinessException>(() => AdvanceAssessmentAsync(application.Id, fixture.StageId)))
					.Code.ShouldBe(AdmissionReviewErrorCodes.RouteRequired);
			(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).IsReady.ShouldBeFalse();
		});
}
