using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmFinancialReadiness_Tests
{
	[Fact]
	public void Fee_policy_requires_distinct_approver_and_preserves_versioned_definition()
	{
		var maker = Guid.NewGuid();
		var policy = CreatePolicy(AdmissionCrmPaymentGateType.FinanceCleared);
		policy.Submit(maker, DateTime.UtcNow);

		Should.Throw<BusinessException>(() => policy.Approve(maker, DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:DistinctFeePolicyApproverRequired");
		policy.Approve(Guid.NewGuid(), DateTime.UtcNow);
		policy.Publish(DateTime.UtcNow);

		policy.Status.ShouldBe(AdmissionCrmFeePolicyStatus.Published);
		policy.Version.ShouldBe(1);
		Should.Throw<BusinessException>(() => policy.UpdateDraft(
			"Changed", "{}", "[]", "INR", AdmissionCrmPaymentGateType.None,
			AdmissionCrmPaymentGateMilestone.StudentActivation, 0, 0, 0,
			DateTime.UtcNow, null)).Code.ShouldBe("AdmissionCrm:OnlyDraftFeePolicyCanBeChanged");
	}

	[Fact]
	public void Finance_cleared_gate_requires_authoritative_confirmation()
	{
		var quotation = CreateQuotation(AdmissionCrmPaymentGateType.FinanceCleared, 1000);
		var now = DateTime.UtcNow;
		quotation.Issue(now);
		quotation.IsGateSatisfiedAt(now).ShouldBeFalse();

		quotation.RecordFinanceState(
			AdmissionCrmFinanceState.PendingConfirmation, 1000, "provider-success", "Payment provider", now);
		quotation.IsGateSatisfiedAt(now).ShouldBeFalse();

		quotation.RecordFinanceState(
			AdmissionCrmFinanceState.Confirmed, 1000, "FIN-100", "Finance", now);
		quotation.IsGateSatisfiedAt(now).ShouldBeTrue();
	}

	[Fact]
	public void Temporary_gate_override_expires_deterministically()
	{
		var quotation = CreateQuotation(AdmissionCrmPaymentGateType.RequiredAmountConfirmed, 1000);
		var now = DateTime.UtcNow;
		quotation.Issue(now);
		quotation.OverrideGate("Principal approved a 24-hour reconciliation exception.", Guid.NewGuid(), now, now.AddHours(24));

		quotation.IsGateSatisfiedAt(now.AddHours(23)).ShouldBeTrue();
		quotation.IsGateSatisfiedAt(now.AddHours(25)).ShouldBeFalse();
	}

	[Fact]
	public void Concession_requires_maker_checker_and_finance_acknowledgement()
	{
		var maker = Guid.NewGuid();
		var request = new AdmissionCrmConcessionRequest(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			AdmissionCrmConcessionType.Sibling, 500, 0, "Verified sibling concession.",
			"SIB-100", maker, DateTime.UtcNow);

		Should.Throw<BusinessException>(() => request.Approve(500, "Approved.", maker, DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:DistinctConcessionApproverRequired");
		request.Approve(500, "Principal approved.", Guid.NewGuid(), DateTime.UtcNow);
		request.Status.ShouldBe(AdmissionCrmConcessionStatus.ApprovedPendingFinance);
		request.AcknowledgeFinance("FIN-CON-100", DateTime.UtcNow);
		request.Status.ShouldBe(AdmissionCrmConcessionStatus.FinanceAcknowledged);
	}

	private static AdmissionCrmFeePolicy CreatePolicy(AdmissionCrmPaymentGateType gateType) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GRADE-1", "Grade 1 fees", 1,
		"{}", "[{\"key\":\"deposit\",\"amount\":1000}]", "INR", gateType,
		AdmissionCrmPaymentGateMilestone.StudentActivation, 1000, 0, 24,
		DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30));

	private static AdmissionCrmAdmissionQuotation CreateQuotation(
		AdmissionCrmPaymentGateType gateType, decimal requiredAmount) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
		"AQ-TEST", 1, "INR", "[{\"key\":\"deposit\",\"amount\":1000}]",
		1000, 0, gateType, AdmissionCrmPaymentGateMilestone.StudentActivation,
		requiredAmount, DateTime.UtcNow.AddDays(2));
}
