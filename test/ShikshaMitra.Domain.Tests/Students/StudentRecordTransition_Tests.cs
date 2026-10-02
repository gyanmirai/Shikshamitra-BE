using System;
using System.Text.Json;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRecordTransition_Tests
{
	[Fact] public void Historical_admission_return_has_no_fabricated_source_and_waits_for_records_approval()
	{
		var handoff = Guid.NewGuid();
		var item = StudentRecordTransition.CreateAdmissionReturn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			"Historical profile reviewed", DateTime.UtcNow, "{}", "{}", null, StudentRecordsPolicy.Starter().ToJson(), "{}", Guid.NewGuid(), 1, handoff);
		item.SourceEnrollmentId.ShouldBeNull(); item.ReturnHandoffId.ShouldBe(handoff);
		Should.Throw<BusinessException>(() => item.WaitForAdmission());
		Should.Throw<BusinessException>(() => item.Approve(item.RequestedBy));
		item.Approve(Guid.NewGuid()); item.WaitForAdmission(); item.Status.ShouldBe("AwaitingAdmission"); item.AppliedAt.ShouldBeNull();
		item.Apply(Guid.NewGuid(), "{}", DateTime.UtcNow); item.Status.ShouldBe("Applied");
	}
	[Fact] public void Ordinary_transition_still_requires_a_real_source_enrollment()
	{
		Should.Throw<Exception>(() => new StudentRecordTransition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
			"Withdraw", "Reviewed withdrawal", DateTime.UtcNow, "{}", "{}", null, StudentRecordsPolicy.Starter().ToJson(), "{}", Guid.NewGuid(), 0));
		Should.Throw<BusinessException>(() => Request(0).WaitForAdmission());
	}
	[Fact] public void Typed_admission_handoff_allows_same_link_replay_and_rejects_replacement_or_both_entry()
	{
		var handoff = new StudentRecordReturnHandoff(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionEntryMode.Inquiry, "FreshApplication", "inquiry-return", "{}", "{}", "{}");
		var inquiry = Guid.NewGuid(); var application = Guid.NewGuid(); handoff.LinkInquiry(inquiry); handoff.LinkInquiry(inquiry);
		handoff.LinkApplication(application); handoff.LinkApplication(application);
		Should.Throw<BusinessException>(() => handoff.LinkInquiry(Guid.NewGuid()));
		Should.Throw<BusinessException>(() => handoff.LinkApplication(Guid.NewGuid()));
		Should.Throw<BusinessException>(() => new StudentRecordReturnHandoff(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			ShikshaMitra.Students.StudentEnrollments.Admissions.AdmissionEntryMode.Both, "FreshApplication", "bad-choice", "{}", "{}", "{}"));
	}
	private static StudentRecordTransition Request(int stages = 2) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"Graduate", "Graduation confirmed", DateTime.UtcNow, "{}", "{}", null, StudentRecordsPolicy.Starter().ToJson(), "{}", Guid.NewGuid(), stages);
	[Fact] public void Approval_stages_cannot_be_skipped_or_self_approved()
	{
		var item = Request();
		Should.Throw<BusinessException>(() => item.Apply(null, "{}", DateTime.UtcNow));
		Should.Throw<BusinessException>(() => item.Approve(item.RequestedBy)).Code.ShouldBe("Students:Records:SelfApprovalDenied");
		item.Approve(Guid.NewGuid()); item.Status.ShouldBe("Submitted"); item.CurrentApprovalStage.ShouldBe(1);
		item.Approve(Guid.NewGuid()); item.Status.ShouldBe("Approved");
		item.Schedule(); item.Status.ShouldBe("Scheduled"); item.Apply(null, "{}", DateTime.UtcNow); item.Status.ShouldBe("Applied");
		Should.Throw<BusinessException>(() => item.Cancel());
	}
	[Fact] public void Renewal_never_marks_transition_applied_before_provisioning_completes()
	{
		var item = Request(0); var renewalId = Guid.NewGuid(); item.WaitForRenewal(renewalId);
		item.Status.ShouldBe("AwaitingRenewal"); item.AppliedAt.ShouldBeNull(); item.RenewalCaseId.ShouldBe(renewalId);
		item.Apply(Guid.NewGuid(), "{}", DateTime.UtcNow); item.Status.ShouldBe("Applied"); item.TargetEnrollmentId.ShouldNotBeNull();
	}
	[Fact] public void Failed_apply_requires_explicit_retry_and_preserves_request_snapshot()
	{
		var item = Request(0); var policy = item.PolicyJson;
		item.MarkException("Students:Records:AuthorityChanged"); item.Status.ShouldBe("Exception"); item.AppliedAt.ShouldBeNull();
		Should.Throw<BusinessException>(() => item.Apply(null, "{}", DateTime.UtcNow));
		item.Retry(); item.Status.ShouldBe("Approved"); item.PolicyJson.ShouldBe(policy); item.LastError.ShouldBeNull();
	}
	[Fact] public void Renewal_state_and_placement_are_bound_to_its_target_class()
	{
		var standard = Guid.NewGuid(); var item = new StudentRecordRenewalCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), standard, Guid.NewGuid(), 1, null, "{}", "{}", "{}", "renewal-test");
		Should.Throw<BusinessException>(() => item.Complete());
		Should.Throw<BusinessException>(() => item.ChangeProvisioningPlacement(standard, Guid.NewGuid(), 2));
		item.ConfirmGuardianReview(Guid.NewGuid(), DateTime.UtcNow, "Guardian confirmed contact");
		item.Activate(Guid.NewGuid()); item.Status.ShouldBe("Provisioning");
		Should.Throw<BusinessException>(() => item.ChangeProvisioningPlacement(Guid.NewGuid(), Guid.NewGuid(), 2));
		item.ChangeProvisioningPlacement(standard, Guid.NewGuid(), 2); item.RollNumber.ShouldBe(2);
		item.Complete(); item.Status.ShouldBe("Completed");
	}
	[Fact] public void Owner_roll_rules_resolve_by_real_target_path_without_admission_case()
	{
		var standard = Guid.NewGuid();
		var settings = new OnboardingRuleConfiguration(JsonSerializer.Serialize(new
		{
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
			rollNumber = new { configurations = new object[] {
				new { id = "all", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all", numberingScope = "class-division", startingNumber = 1 },
				new { id = "target", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "selected", standardIds = new[] { standard }, divisionMode = "all", numberingScope = "class", startingNumber = 10, requireApproval = true }
			} }
		}));
		var rule = settings.ResolveRule("rollNumber", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), standard, Guid.NewGuid(), null, DateTime.UtcNow);
		rule.ShouldNotBeNull(); OnboardingRuleConfiguration.Text(rule!.Value, "id").ShouldBe("target");
		OnboardingRuleConfiguration.Number(rule.Value, "startingNumber").ShouldBe(10);
		OnboardingRuleConfiguration.Boolean(rule.Value, "requireApproval").ShouldBeTrue();
		var item = Request(0); item.CaptureOnboardingRollRule(rule.Value.GetRawText());
		Should.Throw<BusinessException>(() => item.CaptureOnboardingRollRule("null"));
		item.Schedule(); item.GetOnboardingRollRule().ShouldContain("target");
	}
	[Fact] public void Pending_renewal_cancellation_requires_coordinator_proof_and_preserves_link()
	{
		var transition = Request(0); var renewal = new StudentRecordRenewalCase(Guid.NewGuid(), Guid.NewGuid(), transition.StudentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, transition.Id, "{}", "{}", "{}", "cancel-renewal");
		transition.WaitForRenewal(renewal.Id);
		Should.Throw<BusinessException>(() => transition.Cancel());
		renewal.Cancel(); renewal.Status.ShouldBe("Cancelled"); transition.Cancel(pendingRenewalCancelled: true);
		transition.Status.ShouldBe("Cancelled"); transition.RenewalCaseId.ShouldBe(renewal.Id); renewal.TransitionId.ShouldBe(transition.Id);
		Should.Throw<BusinessException>(() => renewal.Activate(Guid.NewGuid()));
	}
	[Fact] public void Provisioning_and_completed_renewals_cannot_be_cancelled()
	{
		var renewal = new StudentRecordRenewalCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, null, "{}", "{}", "{}", "closed-renewal");
		renewal.Activate(Guid.NewGuid()); Should.Throw<BusinessException>(() => renewal.Cancel());
		renewal.Complete(); Should.Throw<BusinessException>(() => renewal.Cancel()); renewal.Status.ShouldBe("Completed");
	}
}
