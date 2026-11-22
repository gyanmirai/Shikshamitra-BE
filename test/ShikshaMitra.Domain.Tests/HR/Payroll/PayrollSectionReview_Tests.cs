using System;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayrollSectionReview_Tests
{
	[Fact]
	public void Section_review_records_its_reviewer_without_issuing_payroll()
	{
		var batch = CreateBatch();
		var reviewer = Guid.NewGuid();
		var reviewedAt = DateTime.UtcNow;
		Should.Throw<ArgumentOutOfRangeException>(() =>
			batch.Resolve(1, PayrollSubmission.Finalized, reviewedAt, "finalize"));

		batch.Resolve(1, PayrollSubmission.Reviewed, reviewedAt, "review", reviewer);

		batch.State.ShouldBe(PayrollSubmission.Reviewed);
		batch.Version.ShouldBe(2);
		batch.ResolvedByUserId.ShouldBe(reviewer);
		batch.ResolvedAt.ShouldBe(reviewedAt);
		Should.Throw<BusinessException>(() =>
			batch.Resolve(1, PayrollSubmission.Returned, reviewedAt, "return"))
			.Code.ShouldBe("PAYROLL_SUBMISSION_VERSION_CONFLICT");
	}

	[Fact]
	public void Section_review_keeps_the_HR_override_reason_and_actor()
	{
		var batch = CreateBatch();
		var issuer = Guid.NewGuid();
		const string reason = "Payroll deadlines require finalization today.";

		batch.Resolve(1, PayrollSubmission.Withdrawn, DateTime.UtcNow, "HrLockOverride", issuer, reason);

		batch.State.ShouldBe(PayrollSubmission.Withdrawn);
		batch.ReasonCode.ShouldBe("HrLockOverride");
		batch.ResolutionReason.ShouldBe(reason);
		batch.ResolvedByUserId.ShouldBe(issuer);
	}

	[Fact]
	public void Section_review_is_opt_in_and_manual_finalization_can_be_restored()
	{
		var settings = new PayrollApprovalSettings(Guid.NewGuid());
		settings.Mode.ShouldBe(PayrollApprovalSettings.Legacy);

		settings.ConfigureSections($$"""[{"StageId":"{{Guid.NewGuid()}}","RoleId":"{{Guid.NewGuid()}}"}]""");
		settings.Mode.ShouldBe(PayrollApprovalSettings.SectionReview);
		Should.Throw<BusinessException>(() => settings.Transition("activate"));

		settings.ConfigureSections(null);
		settings.Mode.ShouldBe(PayrollApprovalSettings.Legacy);
		settings.Version.ShouldBe(2);
	}

	private static PayrollSubmission CreateBatch() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"2026-09", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, false, 1, null, 12_500m, 1,
		DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
}
