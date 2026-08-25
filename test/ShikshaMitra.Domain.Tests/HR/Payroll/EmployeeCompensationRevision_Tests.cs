using System;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EmployeeCompensationRevision_Tests
{
	[Fact]
	public void Should_require_a_separate_approver_and_preserve_the_approved_snapshot()
	{
		var submitterId = Guid.NewGuid();
		var approverId = Guid.NewGuid();
		var revision = BuildDraft();

		revision.Submit(submitterId, new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc));

		var selfApproval = Should.Throw<BusinessException>(() =>
			revision.Approve(submitterId, new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc)));
		selfApproval.Code.ShouldBe("HR:Payroll:CompensationRevisionSelfApprovalDenied");

		revision.Approve(approverId, new DateTime(2026, 8, 24, 9, 5, 0, DateTimeKind.Utc));
		revision.Status.ShouldBe(CompensationRevisionStatus.Approved);
		revision.ApprovedByUserId.ShouldBe(approverId);
		Should.Throw<BusinessException>(() => revision.ReplaceDraft(
			new DateTime(2026, 8, 1),
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			20_000m,
			20_000m,
			19_370m,
			19_224m,
			20_000m,
			null,
			null,
			null,
			null,
			"IN-GJ",
			"{}"));
	}

	[Fact]
	public void Should_allow_a_rejected_revision_to_remain_auditable()
	{
		var revision = BuildDraft();
		var submitterId = Guid.NewGuid();
		revision.Submit(submitterId, new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc));

		var selfRejection = Should.Throw<BusinessException>(() => revision.Reject(
			submitterId,
			new DateTime(2026, 8, 24, 8, 30, 0, DateTimeKind.Utc),
			"I changed my mind."));
		selfRejection.Code.ShouldBe("HR:Payroll:CompensationRevisionSelfApprovalDenied");

		revision.Reject(
			Guid.NewGuid(),
			new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc),
			"The effective month is incorrect.");

		revision.Status.ShouldBe(CompensationRevisionStatus.Rejected);
		revision.RejectionReason.ShouldBe("The effective month is incorrect.");
	}

	private static EmployeeCompensationRevision BuildDraft()
	{
		var revision = new EmployeeCompensationRevision(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			1,
			new DateTime(2026, 8, 1));
		revision.ReplaceDraft(
			new DateTime(2026, 8, 1),
			SalaryInputBasis.EmployerCost,
			CompensationProtectionPolicy.ProtectEmployerCost,
			20_000m,
			20_000m,
			19_370m,
			19_224m,
			20_000m,
			null,
			null,
			null,
			null,
			"IN-GJ",
			"{}");
		return revision;
	}
}
