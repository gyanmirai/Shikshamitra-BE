using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmPolicyGovernance_Tests
{
	[Fact]
	public void Policy_pack_requires_distinct_approver_and_becomes_immutable()
	{
		var maker = Guid.NewGuid();
		var pack = CreatePack();
		pack.Submit(maker, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => pack.Approve(maker, DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:DistinctPolicyPackApproverRequired");
		pack.Approve(Guid.NewGuid(), DateTime.UtcNow);
		pack.Publish(DateTime.UtcNow);
		pack.Status.ShouldBe(AdmissionCrmPolicyPackStatus.Published);
		Should.Throw<BusinessException>(() => pack.UpdateDraft("Changed", "Gujarat", "CBSE", "[]", "[]",
			DateTime.UtcNow, null, DateTime.UtcNow.AddDays(30)))
			.Code.ShouldBe("AdmissionCrm:OnlyDraftPolicyPackCanBeChanged");
	}

	[Fact]
	public void School_override_requires_a_reason_and_preserves_policy_version()
	{
		Should.Throw<BusinessException>(() => new AdmissionCrmPolicyAdoption(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, "{}",
			"[{\"ruleCode\":\"AGE-1\"}]", "{}", null, DateTime.UtcNow,
			DateTime.UtcNow.AddYears(1), Guid.NewGuid(), DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:PolicyOverrideReasonRequired");

		var adoption = new AdmissionCrmPolicyAdoption(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, "{}",
			"[{\"ruleCode\":\"AGE-1\"}]", "{}", "School review approved the local cutoff.",
			DateTime.UtcNow, DateTime.UtcNow.AddYears(1), Guid.NewGuid(), DateTime.UtcNow);
		adoption.PolicyPackVersion.ShouldBe(3);
		adoption.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void Policy_exception_requires_distinct_approver()
	{
		var maker = Guid.NewGuid();
		var exception = new AdmissionCrmPolicyException(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "Late transfer requires authority approval.", "CASE-100",
			maker, DateTime.UtcNow, DateTime.UtcNow.AddDays(10));
		Should.Throw<BusinessException>(() => exception.Decide(true, "Approved.", maker, DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:DistinctPolicyExceptionApproverRequired");
		exception.Decide(true, "Principal approved with supporting authority evidence.", Guid.NewGuid(), DateTime.UtcNow);
		exception.Status.ShouldBe(AdmissionCrmPolicyExceptionStatus.Approved);
	}

	[Fact]
	public void Evaluation_snapshot_is_immutable_and_accepts_only_an_approved_exception()
	{
		var evaluation = new AdmissionCrmPolicyEvaluation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "CBSE-X-TRANSFER", AdmissionCrmPolicyRuleType.AuthorityApproval,
			AdmissionCrmPolicyRuleSeverity.ApprovalRequired, AdmissionCrmPolicyEvaluationStatus.NotSatisfied,
			"{\"desiredClass\":\"Class X\"}", "[\"TC-100\"]", "Board approval is pending.", Guid.NewGuid(), DateTime.UtcNow);
		var exceptionId = Guid.NewGuid();
		evaluation.ApplyApprovedException(exceptionId);
		evaluation.Status.ShouldBe(AdmissionCrmPolicyEvaluationStatus.ExceptionApproved);
		evaluation.ExceptionId.ShouldBe(exceptionId);
		evaluation.InputSnapshotJson.ShouldContain("Class X");
	}

	private static AdmissionCrmPolicyPack CreatePack() => new(
		Guid.NewGuid(), Guid.NewGuid(), "CBSE-GJ", "CBSE Gujarat core", "Gujarat", "CBSE", 1,
		"[{\"authority\":\"CBSE\",\"url\":\"https://www.cbse.gov.in/cbsenew/admission.html\"}]",
		"[{\"code\":\"CBSE-AGE\",\"type\":0}]", DateTime.UtcNow.AddDays(-1),
		DateTime.UtcNow.AddYears(1), DateTime.UtcNow.AddMonths(6));
}
