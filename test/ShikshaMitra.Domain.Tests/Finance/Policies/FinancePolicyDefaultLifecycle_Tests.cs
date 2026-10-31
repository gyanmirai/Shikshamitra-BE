using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class FinancePolicyDefaultLifecycle_Tests
{
	[Theory]
	[InlineData("concession")]
	[InlineData("refund")]
	[InlineData("collection")]
	[InlineData("proration")]
	public void Default_Cannot_Be_Cleared_Deactivated_Or_Archived(string family)
	{
		var policy = Build(family);
		foreach (var change in new[] { policy.Clear, policy.Deactivate, policy.Archive })
		{
			Assert.Throws<BusinessException>(change);
			Assert.True(policy.IsActiveDefault());
		}
		policy.Unarchive(); // Restoring an active policy must be an idempotent no-op.
		Assert.True(policy.IsActiveDefault());
		policy.Demote(); // Allowed only as part of a cross-policy replacement transaction.
		policy.Archive();
		policy.Unarchive();
		Assert.False(policy.IsActiveDefault());
	}

	private static Lifecycle Build(string family)
	{
		var id = Guid.NewGuid();
		var year = Guid.NewGuid();
		switch (family)
		{
			case "concession":
				var concessionModel = new ConcessionPolicyUpdateModel("Concession", "CON", null,
					ConcessionPolicyStatus.Active, true, ConcessionValueSource.PolicyFixed,
					ConcessionCalculationMethod.Percentage, 10m, null, null, null,
					ConcessionApplyOn.AllOutstanding, RoundingMethodType.None);
				var concession = new ConcessionPolicy(id, null, year, concessionModel);
				return new(() => concession.Update(concessionModel with { IsDefault = false }),
					() => concession.Update(concessionModel with { Status = ConcessionPolicyStatus.Draft, IsDefault = false }),
					concession.Archive, concession.Unarchive, () => concession.SetDefault(false),
					() => concession.Status == ConcessionPolicyStatus.Active && concession.IsDefault);
			case "refund":
				var refundModel = new RefundPolicyUpdateModel("Refund", "REF", null,
					RefundPolicyStatus.Active, true, RefundFeeHeadScope.AllFeeHeads, [], false, false, true,
					RefundAmountMode.RemainingPaidAmount, null, null, null, null, RoundingMethodType.None,
					RefundDeductionType.None, null, null, null, [RefundPayoutMethod.BankTransfer], 7);
				var refund = new RefundPolicy(id, null, year, refundModel);
				return new(() => refund.Update(refundModel with { IsDefault = false }),
					() => refund.Update(refundModel with { Status = RefundPolicyStatus.Draft, IsDefault = false }),
					refund.Archive, refund.Unarchive, () => refund.SetDefault(false),
					() => refund.Status == RefundPolicyStatus.Active && refund.IsDefault);
			case "collection":
				var collectionModel = new CollectionPolicyUpdateModelBuilder { IsDefault = true }.Build();
				var collection = new CollectionPolicy(id, null, year, collectionModel);
				return new(() => collection.Update(collectionModel with { IsDefault = false }),
					() => collection.Update(collectionModel with { Status = CollectionPolicyStatus.Draft, IsDefault = false }),
					collection.Archive, collection.Unarchive, () => collection.SetDefault(false),
					() => collection.Status == CollectionPolicyStatus.Active && collection.IsDefault);
			default:
				var prorationModel = new ProrationPolicyUpdateModel("Proration", "PRO", null,
					ProrationPolicyStatus.Active, true,
					[new(Guid.NewGuid(), Guid.NewGuid(), ProrationRuleType.Daily, ProrationRoundingMode.Currency,
						CutoffDay: null, AfterCutoffAction: null)]);
				var proration = new ProrationPolicy(id, null, year, prorationModel);
				return new(() => proration.Update(prorationModel with { IsDefault = false }),
					() => proration.Update(prorationModel with { Status = ProrationPolicyStatus.Draft, IsDefault = false }),
					proration.Archive, proration.Unarchive, () => proration.SetDefault(false),
					() => proration.Status == ProrationPolicyStatus.Active && proration.IsDefault);
		}
	}

	private sealed record Lifecycle(Action Clear, Action Deactivate, Action Archive,
		Action Unarchive, Action Demote, Func<bool> IsActiveDefault);
}
