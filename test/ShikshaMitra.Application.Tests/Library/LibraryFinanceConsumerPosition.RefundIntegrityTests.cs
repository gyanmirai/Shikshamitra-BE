using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Canonical_Consumer_Position_Should_Review_A_Redirected_Refund_Line_Before_Counting_And_Paging()
	{
		var original = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = original.Source.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(original.Administrator);
		await SeedApprovedRefundStateAsync(original);
		var processed = await WithUnitOfWorkAsync(() => DownstreamRefundService(original.Administrator)
			.RecordPayoutAsync(original.Submitted.Id, RecordedTypedPayout(original)));
		processed.Status.ShouldBe(FeeRefundStatus.Processed);
		processed.TotalAmount.ShouldBe(15.25m);
		processed.CalculatedAmount.ShouldBe(15m);
		processed.ProcessedAmount.ShouldBe(15m);

		var receiving = await SeedRefundSourceForSamePayerAsync(original.Source);
		var unaffected = await SeedRefundSourceForSamePayerAsync(original.Source);
		var receivingOwner = receiving.Settlement.Owner;
		var unaffectedOwner = unaffected.Settlement.Owner;
		receivingOwner.PersonId.ShouldBe(owner.PersonId);
		unaffectedOwner.PersonId.ShouldBe(owner.PersonId);
		new[] { owner.AssessmentId, receivingOwner.AssessmentId, unaffectedOwner.AssessmentId }.Distinct().Count().ShouldBe(3);
		var originalChargeId = original.Source.Settlement.Posted.FinanceChargeId;
		var receivingChargeId = receiving.Settlement.Posted.FinanceChargeId;
		originalChargeId.ShouldNotBe(receivingChargeId);

		// Positive control uses actual processed payout and original immutable
		// source/receipt/allocation, before any deliberately malformed storage.
		var before = await ReadConsumerRefundIntegrityProofAsync(original, originalChargeId);
		var positive = await ConsumerPositionAsync(owner);
		positive.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		positive.Refunded.ShouldBe(15m);
		positive.FinancialOutstanding.ShouldBe(60.50m);
		await AssertConsumerDetailParityAsync(owner, positive);
		(await ConsumerPositionAsync(receivingOwner)).Refunded.ShouldBe(0m);
		(await ConsumerPositionAsync(unaffectedOwner)).Refunded.ShouldBe(0m);

		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IFeeRefundRepository>();
			var request = await repository.FindWithLinesAsync(original.Submitted.Id);
			request.ShouldNotBeNull();
			var line = request.Lines.ShouldHaveSingleItem();
			line.ChargeId.ShouldBe(originalChargeId);
			// Corrupted-storage input, not an authorized business transition. The
			// replacement is a real same-school Charge, so exact-one-source and
			// existing reference constraints remain satisfied. No SQL guard is
			// disabled and no immutable source-link/command/payout money is edited.
			typeof(FeeRefundLine).GetProperty(nameof(FeeRefundLine.ChargeId))!
				.SetValue(line, (Guid?)receivingChargeId);
			await repository.UpdateAsync(request, true);
		});
		var after = await ReadConsumerRefundIntegrityProofAsync(original, receivingChargeId);
		after.ShouldBe(before);

		// Both claims are now inconsistent: source A owns the request but its
		// line points elsewhere; source B is named by that line but owns neither
		// the source link nor original receipt/allocation. Source C stays valid.
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(original.Administrator).GetQueryAsync(new(
				LibraryChargePositionReadPurpose.ChargeHistory, owner.YearId,
				[owner.AssessmentId, receivingOwner.AssessmentId, unaffectedOwner.AssessmentId]));
			var executor = GetRequiredService<IAsyncQueryableExecuter>();
			(await executor.CountAsync(query)).ShouldBe(3);
			var review = query.Where(row => row.Evidence == FinanceExternalPositionEvidence.NeedsReview);
			(await executor.CountAsync(review)).ShouldBe(2);
			(await executor.ToListAsync(review.Select(row => row.SourceId))).ShouldBe(
				new[] { owner.AssessmentId, receivingOwner.AssessmentId }, ignoreOrder: true);
			var actionable = query.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified && row.Collectible > 0m);
			(await executor.CountAsync(actionable)).ShouldBe(1);
			(await executor.ToListAsync(actionable.OrderBy(row => row.SourceId).Select(row => row.SourceId).Take(1)))
				.ShouldBe([unaffectedOwner.AssessmentId]);
			(await executor.ToListAsync(actionable.OrderBy(row => row.SourceId).Select(row => row.SourceId).Skip(1).Take(1)))
				.ShouldBeEmpty();
		});

		foreach (var implicated in new[] { owner, receivingOwner })
		{
			var row = await ConsumerPositionAsync(implicated);
			row.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
			row.ReviewCode.ShouldNotBeNullOrWhiteSpace();
			row.Refunded.ShouldBeNull();
			row.Collectible.ShouldBeNull();
			row.FinancialOutstanding.ShouldBeNull();
			await AssertConsumerDetailParityAsync(implicated, row);
		}
		var validSibling = await ConsumerPositionAsync(unaffectedOwner);
		validSibling.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		validSibling.Refunded.ShouldBe(0m);
		validSibling.Settled.ShouldBe(40.25m);
		validSibling.FinancialOutstanding.ShouldBe(60.50m);
		validSibling.Collectible.ShouldBe(60.50m);
		await AssertConsumerDetailParityAsync(unaffectedOwner, validSibling);
	}
}
