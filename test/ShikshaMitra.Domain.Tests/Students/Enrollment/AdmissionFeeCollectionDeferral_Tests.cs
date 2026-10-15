using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFeeCollectionDeferral_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Reason_is_mandatory_and_no_deferral_is_written(string? reason)
	{
		var application = Application(); var stage = Guid.NewGuid(); application.SetCurrentStage(stage);
		Should.Throw<BusinessException>(() => application.DeferFeeCollection(stage, reason!, Guid.NewGuid(), DateTimeOffset.UtcNow))
			.Code.ShouldBe(AdmissionFeeSelectionErrorCodes.DeferralReasonRequired);
		application.GetFeeCollectionDeferral().ShouldBeNull(); application.GetFeeSelection().ShouldBeNull();
	}

	[Fact]
	public void Deferral_keeps_payment_selection_and_admission_state_unchanged_and_survives_finalization()
	{
		var application = Application(); var stage = Guid.NewGuid(); var actor = Guid.NewGuid(); application.SetCurrentStage(stage);
		var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(5.5));
		application.DeferFeeCollection(stage, "  Parent requested collection later.  ", actor, now);
		var deferred = application.GetFeeCollectionDeferral()!;
		deferred.Reason.ShouldBe("Parent requested collection later."); deferred.DeferredBy.ShouldBe(actor);
		deferred.DeferredAt.ShouldBe(now.ToUniversalTime()); application.GetFeeSelection().ShouldBeNull();
		application.StudentId.ShouldBeNull(); application.OnboardingCaseId.ShouldBeNull();
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft);
		application.Submit(); application.Approve(actor, null);
		application.FinalizeApprovedDirectAdmission(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
		application.GetFeeCollectionDeferral().ShouldBe(deferred);
	}

	[Fact]
	public void Wrong_stage_missing_actor_oversized_reason_and_closed_journey_cannot_defer()
	{
		var application = Application(); var stage = Guid.NewGuid(); application.SetCurrentStage(stage);
		Should.Throw<BusinessException>(() => application.DeferFeeCollection(Guid.NewGuid(), "Later", Guid.NewGuid(), DateTimeOffset.UtcNow));
		Should.Throw<BusinessException>(() => application.DeferFeeCollection(stage, "Later", Guid.Empty, DateTimeOffset.UtcNow));
		Should.Throw<BusinessException>(() => application.DeferFeeCollection(stage, new string('x', 513), Guid.NewGuid(), DateTimeOffset.UtcNow));
		application.EndJourney(Guid.NewGuid(), "Withdrawn");
		Should.Throw<BusinessException>(() => application.DeferFeeCollection(stage, "Later", Guid.NewGuid(), DateTimeOffset.UtcNow));
		application.GetFeeCollectionDeferral().ShouldBeNull();
	}

	private static AdmissionApplication Application() => new(Guid.NewGuid(), Guid.NewGuid(), "QA-DEFERRAL", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
