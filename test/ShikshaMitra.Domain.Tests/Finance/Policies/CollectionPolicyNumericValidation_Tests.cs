using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyNumericValidation_Tests
{
	private static CollectionPolicy Create(CollectionPolicyUpdateModel model) => new(Guid.NewGuid(), null, Guid.NewGuid(), model);

	[Theory]
	[InlineData(LateFeeCalculationType.Flat)]
	[InlineData(LateFeeCalculationType.PerDay)]
	[InlineData(LateFeeCalculationType.PerWeek)]
	[InlineData(LateFeeCalculationType.PerMonth)]
	public void Large_And_Overprecision_Amounts_Are_Rejected_Even_In_Draft(LateFeeCalculationType type)
	{
		foreach (var amount in new[] { 1000000m, decimal.MaxValue, 0m, -1m, 1.001m })
		{
			var model = new CollectionPolicyUpdateModelBuilder { Status = CollectionPolicyStatus.Draft, LateFeeType = type }.Build();
			model = type switch
			{
				LateFeeCalculationType.Flat => model with { FlatAmount = amount },
				LateFeeCalculationType.PerDay => model with { AmountPerDay = amount },
				LateFeeCalculationType.PerWeek => model with { AmountPerWeek = amount },
				_ => model with { AmountPerMonth = amount }
			};
			Should.Throw<BusinessException>(() => Create(model)).Code.ShouldBe(CollectionPolicyErrorCodes.InvalidLateFeeRule);
		}
	}

	[Theory]
	[InlineData(LateFeeCalculationType.Percentage)]
	[InlineData(LateFeeCalculationType.Compound)]
	public void Invalid_Percentages_Are_Rejected(LateFeeCalculationType type)
	{
		foreach (var rate in new[] { 0m, 0.001m, 100.01m, decimal.MaxValue })
		{
			var model = new CollectionPolicyUpdateModelBuilder { LateFeeType = type, Percentage = rate }.Build();
			Should.Throw<BusinessException>(() => Create(model)).Code.ShouldBe(CollectionPolicyErrorCodes.InvalidLateFeeRule);
		}
	}

	[Fact]
	public void Bounds_And_No_Limit_Are_Accepted()
	{
		Create(new CollectionPolicyUpdateModelBuilder { FlatAmount = 999999m, GracePeriodDays = 365, MaximumChargingDays = 365 }.Build());
		Create(new CollectionPolicyUpdateModelBuilder { LateFeeType = LateFeeCalculationType.Compound, Percentage = 0.01m, MaximumLateFeeAmount = 0.01m }.Build());
		Create(new CollectionPolicyUpdateModelBuilder { LateFeeType = LateFeeCalculationType.Percentage, Percentage = 100m }.Build());
	}

	[Fact]
	public void Custom_Fee_Head_Rule_Cannot_Bypass_Numeric_Bounds()
	{
		var builder = new CollectionPolicyUpdateModelBuilder();
		var feeHead = builder.LateFeeApplyOnFeeHeads[0];
		builder.FeeHeadOverrides = new[] { new CollectionPolicyFeeHeadOverride(feeHead, FeeHeadOverrideMode.CustomRule,
			new LateFeeRuleOverride(LateFeeCalculationType.PerDay, null, null, PercentageBaseType.OutstandingAmount,
				1000000m, null, null, CompoundFrequencyType.Daily, Array.Empty<CollectionPolicyEscalationRule>(),
				RoundingMethodType.None, null, null)) };
		Should.Throw<BusinessException>(() => Create(builder.Build())).Code.ShouldBe(CollectionPolicyErrorCodes.InvalidLateFeeRule);
	}

	[Fact]
	public void Historical_Snapshot_Is_Preserved_But_Cannot_Be_Resaved_With_Unsafe_Values()
	{
		var snapshot = Create(new CollectionPolicyUpdateModelBuilder().Build()).ToSnapshot();
		snapshot.FlatAmount = 1000000m;
		var restored = CollectionPolicy.FromSnapshot(Guid.NewGuid(), null, Guid.NewGuid(), 7, snapshot);
		restored.FlatAmount.ShouldBe(1000000m);
		restored.Version.ShouldBe(7);
		Should.Throw<BusinessException>(() => restored.Update(snapshot.ToUpdateModel()));
	}

	[Fact]
	public void Days_And_Ceiling_Validate_Regardless_Of_Status()
	{
		var baseline = new CollectionPolicyUpdateModelBuilder { Status = CollectionPolicyStatus.Draft }.Build();
		foreach (var model in new[] { baseline with { GracePeriodDays = 366 }, baseline with { MaximumChargingDays = int.MaxValue },
			baseline with { MaximumChargingDays = 0 }, baseline with { MaximumLateFeeAmount = 1000000m }, baseline with { MaximumLateFeeAmount = 0m } })
			Should.Throw<BusinessException>(() => Create(model));
	}

	[Fact]
	public void Slabs_Reject_Invalid_Single_Ranges_Values_And_Cutoff_Coverage()
	{
		foreach (var rule in new[] { new CollectionPolicyEscalationRule(1, 1000000000, EscalationCalculationType.Flat, 10m, null),
			new(1, 0, EscalationCalculationType.Flat, 10m, null), new(2, 10, EscalationCalculationType.Flat, 10m, null),
			new(1, 10, EscalationCalculationType.Flat, 1000000m, null), new(1, 10, EscalationCalculationType.Percentage, 100.01m, null) })
			Should.Throw<BusinessException>(() => CollectionPolicySlabValidator.EnsureNoOverlapsOrGaps(new[] { rule }));
		Should.Throw<BusinessException>(() => CollectionPolicySlabValidator.EnsureNoOverlapsOrGaps(new[] { new CollectionPolicyEscalationRule(1, 10, EscalationCalculationType.Flat, 10m, null) }, 20));
		CollectionPolicySlabValidator.EnsureNoOverlapsOrGaps(new[] { new CollectionPolicyEscalationRule(1, null, EscalationCalculationType.Flat, 10m, null) }, 20);
	}
}
