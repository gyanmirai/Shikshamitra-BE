using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionRefundPolicy_Tests
{
	private static ConcessionPolicyUpdateModel Model(ConcessionApplyOn scope, bool? automatic) => new(
		"Paid concession", "PAID", null, ConcessionPolicyStatus.Active, false,
		ConcessionValueSource.PolicyFixed, ConcessionCalculationMethod.Percentage,
		10m, null, null, null, scope, RoundingMethodType.RoundUp, AutoCreateRefund: automatic);

	[Theory]
	[InlineData(ConcessionApplyOn.AllOutstanding)]
	[InlineData(ConcessionApplyOn.CurrentInstallment)]
	[InlineData(ConcessionApplyOn.FutureInstallments)]
	public void Unpaid_scopes_reject_automatic_refunds(ConcessionApplyOn scope)
	{
		var error = Assert.Throws<BusinessException>(() => new ConcessionPolicy(
			Guid.NewGuid(), null, Guid.NewGuid(), Model(scope, true)));
		Assert.Equal(ConcessionPolicyErrorCodes.RefundRequiresPaidScope, error.Code);
		Assert.False(new ConcessionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), Model(scope, null)).AutoCreateRefund);
	}

	[Fact]
	public void Paid_scope_defaults_to_automatic_and_manual_setting_survives_snapshot_roundtrip()
	{
		var policy = new ConcessionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), Model(ConcessionApplyOn.AllFees, null));
		Assert.True(policy.AutoCreateRefund);
		var oldSnapshot = policy.ToSnapshot() with { AutoCreateRefund = null };
		Assert.True(oldSnapshot.ShouldAutoCreateRefund);
		policy.Update(Model(ConcessionApplyOn.AllFees, false));
		var snapshot = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(JsonSerializer.Serialize(policy.ToSnapshot()))!;
		Assert.False(snapshot.ShouldAutoCreateRefund);
		policy.Update(Model(ConcessionApplyOn.AllFees, true));
		Assert.False(snapshot.ShouldAutoCreateRefund);
	}

	[Fact]
	public void Paid_entitlement_is_immutable_capped_and_reduced_by_reversal()
	{
		var request = new ConcessionRequest(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
		var dueId = Guid.NewGuid();
		var allocation = new ConcessionDueFeeAllocation(Guid.NewGuid(), null, request.Id, dueId, Guid.NewGuid(), 100m);
		Assert.Throws<BusinessException>(() => request.RecordPaidConcessionAmounts(new Dictionary<Guid, decimal> { [dueId] = 60m }, false));
		request.Approve(Guid.NewGuid(), null, [allocation], DateTime.UtcNow);
		Assert.Throws<BusinessException>(() => request.RecordPaidConcessionAmounts(new Dictionary<Guid, decimal> { [dueId] = 101m }, false));
		request.RecordPaidConcessionAmounts(new Dictionary<Guid, decimal> { [dueId] = 60m }, false);
		Assert.False(request.ShouldAutoCreateRefund());
		Assert.Throws<BusinessException>(() => request.RecordPaidConcessionAmounts(new Dictionary<Guid, decimal> { [dueId] = 50m }, false));
		request.RecordReversals([new ConcessionReversal(Guid.NewGuid(), null, request.Id, allocation.Id,
			dueId, allocation.FeeHeadId, 20m, "Approved correction of benefit", Guid.NewGuid(), DateTime.UtcNow)]);
		Assert.Equal(40m, request.GetRemainingPaidConcessionAmounts().Values.Sum());
		request.MarkRefundRequestsCreated();
		Assert.True(request.HaveRefundRequestsBeenCreated());
	}
}
