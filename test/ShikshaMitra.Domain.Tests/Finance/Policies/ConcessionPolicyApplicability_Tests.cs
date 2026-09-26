using System;
using System.Text.Json;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class ConcessionPolicyApplicability_Tests
{
	[Fact]
	public void Active_policy_does_not_require_a_concession_type()
	{
		var policy = new ConcessionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), Model());
		Assert.Equal(ConcessionPolicyStatus.Active, policy.Status);
	}

	[Fact]
	public void Active_policy_does_not_require_selected_fee_heads()
	{
		var policy = new ConcessionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), Model() with
			{
				ApplicableConcessionTypeIds = [Guid.NewGuid()],
				FeeHeadScope = ConcessionFeeHeadScope.SelectedFeeHeads
			});
		Assert.Equal(ConcessionPolicyStatus.Active, policy.Status);
	}

	[Fact]
	public void Applicability_is_frozen_in_each_version_snapshot()
	{
		var yearId = Guid.NewGuid();
		var firstType = Guid.NewGuid();
		var secondType = Guid.NewGuid();
		var policy = new ConcessionPolicy(Guid.NewGuid(), null, yearId,
			Model() with { ApplicableConcessionTypeIds = [firstType] });
		var originalJson = JsonSerializer.Serialize(policy.ToSnapshot());
		policy.Update(Model() with { ApplicableConcessionTypeIds = [secondType] });
		var original = JsonSerializer.Deserialize<ConcessionPolicySnapshot>(originalJson)!;
		Assert.Contains(firstType, original.ApplicableConcessionTypeIds!);
		Assert.DoesNotContain(secondType, original.ApplicableConcessionTypeIds!);
		Assert.Contains(secondType, policy.ToSnapshot().ApplicableConcessionTypeIds!);
	}

	private static ConcessionPolicyUpdateModel Model() => new(
		"Scholarship", "SCHOLARSHIP", null, ConcessionPolicyStatus.Active, false,
		ConcessionValueSource.PolicyFixed, ConcessionCalculationMethod.Percentage,
		10m, null, null, null, ConcessionApplyOn.AllOutstanding, RoundingMethodType.None);
}
