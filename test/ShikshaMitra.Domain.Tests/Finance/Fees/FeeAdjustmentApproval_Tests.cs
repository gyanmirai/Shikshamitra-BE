using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class FeeAdjustmentApproval_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Creator_cannot_apply_own_request_even_after_another_checker_partially_applies_it(bool partiallyApplied)
	{
		var creator = Guid.NewGuid();
		var request = CreateRequest(creator);
		if (partiallyApplied)
			Apply(request, Guid.NewGuid(), 25);
		var previousVersion = request.ApplyVersion;
		var previousApplied = request.AppliedAmount;
		var previousProcessor = request.ProcessedByUserId;

		var exception = Assert.Throws<BusinessException>(() => Apply(request, creator, 10));

		Assert.Equal(FeeAdjustmentErrorCodes.ApproverCannotBeRequestor, exception.Code);
		Assert.Equal(previousVersion, request.ApplyVersion);
		Assert.Equal(previousApplied, request.AppliedAmount);
		Assert.Equal(previousProcessor, request.ProcessedByUserId);
		Assert.Equal(partiallyApplied ? FeeAdjustmentStatus.PartiallyApplied : FeeAdjustmentStatus.Pending, request.Status);
		Assert.Equal(partiallyApplied ? 1 : 0, request.TargetAllocations.Count);
	}

	[Fact]
	public void Same_noncreator_checker_can_apply_successive_batches()
	{
		var request = CreateRequest(Guid.NewGuid());
		var checker = Guid.NewGuid();

		Apply(request, checker, 25);
		Apply(request, checker, 75);

		Assert.Equal(FeeAdjustmentStatus.Applied, request.Status);
		Assert.Equal(100, request.AppliedAmount);
		Assert.Equal(0, request.RemainingAmount);
		Assert.Equal(2, request.ApplyVersion);
		Assert.Equal(checker, request.ProcessedByUserId);
	}

	[Fact]
	public void Administrator_auto_approval_can_apply_the_creator_request()
	{
		var creator = Guid.NewGuid();
		var request = CreateRequest(creator);

		request.RecordAutoApplyBatch(creator, "Automatically approved on administrator submission.",
			[(Guid.NewGuid(), Guid.NewGuid(), (MonthEnum)1, 2026, 100d)]);

		Assert.Equal(FeeAdjustmentStatus.Applied, request.Status);
		Assert.Equal(100d, request.AppliedAmount);
		Assert.Equal(creator, request.ProcessedByUserId);
	}

	[Fact]
	public void Creator_still_cannot_reject_own_request()
	{
		var creator = Guid.NewGuid();
		var request = CreateRequest(creator);

		var exception = Assert.Throws<BusinessException>(() => request.Reject(creator, "No longer needed"));

		Assert.Equal(FeeAdjustmentErrorCodes.ApproverCannotBeRequestor, exception.Code);
		Assert.Equal(FeeAdjustmentStatus.Pending, request.Status);
	}

	private static void Apply(FeeAdjustmentRequest request, Guid actor, double amount) =>
		request.RecordApplyBatch(actor, "Reviewed", [(Guid.NewGuid(), Guid.NewGuid(), (MonthEnum)1, 2026, amount)]);

	private static FeeAdjustmentRequest CreateRequest(Guid creator)
	{
		var request = new FeeAdjustmentRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"Reallocate paid fees", "ADJ-2026-000001", 100);
		typeof(FeeAdjustmentRequest).GetProperty(nameof(FeeAdjustmentRequest.CreatorId))!.SetValue(request, creator);
		return request;
	}
}
