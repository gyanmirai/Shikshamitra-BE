using System;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFiveStageModel_Tests
{
	[Fact]
	public void Only_the_five_supported_types_are_selectable_without_renumbering()
	{
		AdmissionWorkflowStageTypeRegistry.All.Where(item => item.IsSelectable).Select(item => (int)item.Type)
			.OrderBy(value => value).ShouldBe(new[] { 1, 2, 4, 5, 7 });
	}

	[Theory]
	[InlineData(AdmissionWorkflowStageType.Eligibility)]
	[InlineData(AdmissionWorkflowStageType.Review)]
	[InlineData(AdmissionWorkflowStageType.Offer)]
	public void Retired_stage_cannot_be_published(AdmissionWorkflowStageType type)
	{
		var stage = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), Guid.NewGuid(), "RETIRED", "Retired", type,
			0, true, false, null, null, true, true);
		new AdmissionWorkflowGraphValidator().Validate([stage], []).ShouldContain(issue => issue.StageId == stage.Id &&
			issue.Severity == AdmissionWorkflowValidationSeverity.Error);
	}

	[Fact]
	public void Payment_settings_do_not_create_a_collection_policy()
	{
		AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Payment,
			"{\"payment\":{\"enabled\":true,\"amount\":500}}") .ShouldBeNull();
	}

	[Fact]
	public void Assessment_has_optional_scheduling_and_no_result_review()
	{
		var json = AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Assessment,
			"{\"assessment\":{\"schedulingEnabled\":true,\"reviewRequired\":true,\"maxAttempts\":3}}");
		var policy = AdmissionAssessmentPolicy.Read(json)!;
		policy.SchedulingEnabled.ShouldBeTrue(); policy.ReviewRequired.ShouldBeFalse(); policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void Unknown_policy_version_is_rejected_instead_of_silently_downgraded()
	{
		var json = AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Assessment,
			"{\"assessment\":{\"version\":2,\"maxAttempts\":1}}");
		Should.Throw<ArgumentException>(() => AdmissionAssessmentPolicy.Read(json));
	}

	[Fact]
	public void Document_codes_are_server_generated_unique_and_stable_on_rename()
	{
		var json = AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.DocumentVerification,
			"{\"documentVerification\":{\"allowWaivers\":true,\"requireIndependentReview\":true,\"collectionFormStageCode\":\"FORM\",\"requirements\":[{\"name\":\"Birth certificate\",\"code\":\"\"},{\"name\":\"Birth certificate\",\"code\":\"\"}]}}");
		var policy = AdmissionDocumentPolicy.Read(json)!;
		policy.Requirements.Select(item => item.Code).Distinct().Count().ShouldBe(2);
		policy.AllowWaivers.ShouldBeFalse(); policy.RequireIndependentReview.ShouldBeFalse(); policy.CollectionFormStageCode.ShouldBeNull();
		var renamed = policy with { Requirements = [policy.Requirements[0] with { Name = "Birth record" }, policy.Requirements[1]] };
		var saved = AdmissionDocumentPolicy.Read(AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.DocumentVerification,
			JsonSerializer.Serialize(new { documentVerification = renamed })))!;
		saved.Requirements[0].Code.ShouldBe(policy.Requirements[0].Code);
	}

	[Fact]
	public void Form_keeps_save_resume_and_the_existing_submission_limit()
	{
		var policy = AdmissionFormStagePolicy.Read(AdmissionStageConfiguration.Normalize(AdmissionWorkflowStageType.Form,
			"{\"form\":{\"allowSaveAndResume\":false,\"maxSubmissionAttempts\":5,\"autoAdvanceOnSubmit\":false}}"))!;
		policy.Enabled.ShouldBeTrue(); policy.AllowSaveAndResume.ShouldBeTrue();
		policy.MaxSubmissionAttempts.ShouldBe(5); policy.AutoAdvanceOnSubmit.ShouldBeTrue();
	}

	[Fact]
	public void Approved_admission_finalizes_without_offer_and_retry_preserves_identifiers()
	{
		var application = Application();
		Should.Throw<BusinessException>(() => application.FinalizeApprovedAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
		application.Submit(); application.Approve(Guid.NewGuid(), null);
		var student = Guid.NewGuid(); var item = Guid.NewGuid(); var now = DateTime.UtcNow;
		application.FinalizeApprovedAdmission(student, item, now);
		application.FinalizeApprovedAdmission(student, item, now.AddHours(1));
		application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
		application.StudentId.ShouldBe(student); application.FinalizedAt.ShouldBe(now); application.OfferAcceptedAt.ShouldBeNull();
	}

	[Fact]
	public async System.Threading.Tasks.Task Waitlisted_decision_stays_pending_until_approved_or_rejected()
	{
		var handler = new ApprovalAdmissionWorkflowStageHandler();
		var context = new AdmissionWorkflowStageRuntimeContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AdmissionApplicationStatus.Waitlisted);
		(await handler.CanCompleteAsync(context)).ShouldBeFalse();
		(await handler.CanCompleteAsync(context with { ApplicationStatus = AdmissionApplicationStatus.Approved })).ShouldBeTrue();
		(await handler.CanCompleteAsync(context with { ApplicationStatus = AdmissionApplicationStatus.Rejected })).ShouldBeTrue();
	}

	private static AdmissionApplication Application() => new(Guid.NewGuid(), null, "FIVE", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
