using System;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipCorrectionState_Tests
{
	private static PayslipCorrection Create() => new(Guid.NewGuid(), new PayslipDocument(Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2026-08", 1, DateTime.UtcNow, Guid.NewGuid(), "{}", [1]),
		Guid.NewGuid(), " Correct recorded attendance ", Guid.NewGuid(), "{}");

	[Fact]
	public void Recalculation_clears_review_and_stale_review_cannot_apply()
	{
		var draft = Create();
		var revision = Guid.NewGuid();
		var actor = Guid.NewGuid();
		draft.Reason.ShouldBe("Correct recorded attendance");
		Should.Throw<BusinessException>(() => draft.Review(revision, actor, DateTime.UtcNow));
		draft.Calculate("[]", "{}", revision);
		Should.Throw<BusinessException>(() => draft.Review(Guid.NewGuid(), actor, DateTime.UtcNow));
		draft.Review(revision, actor, DateTime.UtcNow);
		draft.ReviewedBy.ShouldBe(actor);
		draft.Calculate("[]", "{}", revision);
		draft.Status.ShouldBe("CALCULATED");
		draft.ReviewedBy.ShouldBeNull();
		Should.Throw<BusinessException>(() => draft.Complete(revision, actor, DateTime.UtcNow, Guid.NewGuid(), null));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("2026-07")]
	[InlineData("2026-08")]
	[InlineData("2026-13")]
	public void Paid_settlement_requires_one_valid_later_period(string? month)
	{
		var draft = Create();
		var revision = Guid.NewGuid();
		var actor = Guid.NewGuid();
		draft.Calculate("[]", "{}", revision);
		draft.Review(revision, actor, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => draft.Complete(revision, actor, DateTime.UtcNow, null, month));
	}

	[Fact]
	public void Cancel_preserves_evidence_and_disallows_later_mutation()
	{
		var draft = Create();
		var revision = Guid.NewGuid();
		draft.Calculate("[]", "{\"net\":100}", revision);
		draft.Cancel(Guid.NewGuid(), DateTime.UtcNow);
		draft.Status.ShouldBe("CANCELLED");
		draft.IsActive.ShouldBeFalse();
		draft.CandidateJson.ShouldBe("{\"net\":100}");
		draft.OriginalSnapshotJson.ShouldBe("{}");
		Should.Throw<BusinessException>(() => draft.Calculate("[]", "{}", revision));
		Should.Throw<BusinessException>(() => draft.Review(revision, Guid.NewGuid(), DateTime.UtcNow));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Applied_correction_retains_exactly_one_result_and_is_terminal(bool paid)
	{
		var draft = Create();
		var revision = Guid.NewGuid();
		var actor = Guid.NewGuid();
		draft.Calculate("[]", "{}", revision);
		draft.Review(revision, actor, DateTime.UtcNow);
		Should.Throw<BusinessException>(() => draft.Complete(revision, actor, DateTime.UtcNow, Guid.NewGuid(), "2026-09"));
		draft.Complete(revision, actor, DateTime.UtcNow, paid ? null : Guid.NewGuid(), paid ? "2026-09" : null);
		draft.Status.ShouldBe("APPLIED");
		draft.IsActive.ShouldBeFalse();
		Should.Throw<BusinessException>(() => draft.Cancel(actor, DateTime.UtcNow));
		Should.Throw<BusinessException>(() => draft.Complete(revision, actor, DateTime.UtcNow, Guid.NewGuid(), null));
	}
}
