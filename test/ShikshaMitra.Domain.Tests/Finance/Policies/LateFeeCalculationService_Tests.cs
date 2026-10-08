using System;
using System.Collections.Generic;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class LateFeeCalculationService_Tests
{
	private readonly LateFeeCalculationService _service = new();

	private static CollectionPolicy BuildPolicy(Action<CollectionPolicyUpdateModelBuilder>? configure = null)
	{
		var builder = new CollectionPolicyUpdateModelBuilder();
		configure?.Invoke(builder);
		return new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), builder.Build());
	}

	private static LateFeeCalculationInput Input(
		int daysLate,
		decimal outstanding = 10000,
		decimal originalFee = 10000,
		decimal remainingBalance = 10000,
		bool isExempt = false,
		int? customGracePeriodDays = null) =>
		new(daysLate, outstanding, originalFee, remainingBalance, null, isExempt, customGracePeriodDays);

	private static LateFeeCalculationInput InputForFeeHead(
		Guid? feeHeadId,
		int daysLate,
		decimal outstanding = 10000,
		decimal originalFee = 10000,
		decimal remainingBalance = 10000) =>
		new(daysLate, outstanding, originalFee, remainingBalance, feeHeadId);

	// ── Grace period ──────────────────────────────────────────────────────────

	[Fact]
	public void Within_Grace_Period_Produces_Zero_Late_Fee()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 7; });
		var result = _service.Calculate(policy, Input(daysLate: 5));
		Assert.Equal(0, result.LateFee);
	}

	[Fact]
	public void Grace_Period_Zero_Charges_From_Day_One()
	{
		// collection-policy-flow.md §7.12
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 0; });
		var result = _service.Calculate(policy, Input(daysLate: 1));
		Assert.Equal(20, result.LateFee);
	}

	[Fact]
	public void Student_Custom_Grace_Period_Overrides_Policy_Grace_Period()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 7; });
		var result = _service.Calculate(policy, Input(daysLate: 10, customGracePeriodDays: 14));
		Assert.Equal(0, result.LateFee); // 10 days late, but student's custom 14-day grace hasn't elapsed
	}

	// ── Exemption / disabled ─────────────────────────────────────────────────

	[Fact]
	public void Student_Level_Exemption_Short_Circuits_To_Zero_Regardless_Of_Policy()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.Compound; b.Percentage = 50; b.GracePeriodDays = 0; });
		var result = _service.Calculate(policy, Input(daysLate: 90, isExempt: true));
		Assert.Equal(0, result.LateFee);
		Assert.True(result.IsExempt);
	}

	[Fact]
	public void Disabled_Late_Fee_Produces_Zero()
	{
		var policy = BuildPolicy(b => { b.EnableLateFee = false; b.EnableWaiver = false; b.LateFeeType = LateFeeCalculationType.Flat; b.FlatAmount = 500; });
		var result = _service.Calculate(policy, Input(daysLate: 30));
		Assert.Equal(0, result.LateFee);
	}

	// ── Calculation types ────────────────────────────────────────────────────

	[Fact]
	public void Flat_Type_Ignores_Days_Late()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.Flat; b.FlatAmount = 250; b.GracePeriodDays = 0; });
		var result = _service.Calculate(policy, Input(daysLate: 1));
		Assert.Equal(250, result.LateFee);

		var resultLater = _service.Calculate(policy, Input(daysLate: 200));
		Assert.Equal(250, resultLater.LateFee);
	}

	[Theory]
	[InlineData(PercentageBaseType.OutstandingAmount, 8000, 10000, 10000, 800)]
	[InlineData(PercentageBaseType.OriginalFee, 8000, 10000, 10000, 1000)]
	[InlineData(PercentageBaseType.RemainingBalance, 8000, 10000, 6000, 600)]
	public void Percentage_Type_Uses_Selected_Base(
		PercentageBaseType baseType, decimal outstanding, decimal originalFee, decimal remainingBalance, decimal expected)
	{
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.Percentage;
			b.Percentage = 10;
			b.PercentageBase = baseType;
			b.GracePeriodDays = 0;
		});
		var result = _service.Calculate(policy, Input(daysLate: 1, outstanding: outstanding, originalFee: originalFee, remainingBalance: remainingBalance));
		Assert.Equal(expected, result.LateFee);
	}

	[Fact]
	public void PerWeek_Type_Ceils_Partial_Weeks()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerWeek; b.AmountPerWeek = 100; b.GracePeriodDays = 0; });
		// 8 days late = 2 weeks (ceil(8/7))
		var result = _service.Calculate(policy, Input(daysLate: 8));
		Assert.Equal(200, result.LateFee);
	}

	[Fact]
	public void PerMonth_Type_Ceils_Partial_Months()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerMonth; b.AmountPerMonth = 300; b.GracePeriodDays = 0; });
		// 31 days late = 2 months (ceil(31/30))
		var result = _service.Calculate(policy, Input(daysLate: 31));
		Assert.Equal(600, result.LateFee);
	}

	[Fact]
	public void Compound_Type_Compounds_Monthly_On_Remaining_Balance()
	{
		// collection-policy-flow.md §7.1
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.Compound;
			b.CompoundFrequency = CompoundFrequencyType.Monthly;
			b.Percentage = 2;
			b.PercentageBase = PercentageBaseType.RemainingBalance;
			b.GracePeriodDays = 7;
		});
		// 37 days late - 7 grace = 30 effective days = 1 monthly period
		var result = _service.Calculate(policy, Input(daysLate: 37, remainingBalance: 10000));
		Assert.Equal(200, result.LateFee); // 10000 * 1.02 - 10000 = 200
	}

	[Fact]
	public void Slab_Type_Sums_Applicable_Slabs_Without_Gap_Or_Overlap()
	{
		// collection-policy-flow.md §7.2 non-overlapping slabs
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.Slab;
			b.GracePeriodDays = 0;
			b.EscalationRules = new List<CollectionPolicyEscalationRule>
			{
				new(1, 10, EscalationCalculationType.Flat, 10, null),
				new(11, 20, EscalationCalculationType.Flat, 20, null),
				new(21, null, EscalationCalculationType.Flat, 30, null)
			};
		});

		// 25 days late: 10 days @10 + 10 days @20 + 5 days @30 = 100+200+150 = 450
		var result = _service.Calculate(policy, Input(daysLate: 25));
		Assert.Equal(450, result.LateFee);
	}

	[Fact]
	public void Slab_Gap_Between_Rules_Is_Rejected_At_Save_Time_Not_Silently_Uncharged()
	{
		// collection-policy-flow.md §7.2 describes a gap (days 11-14 attract no late fee) as a
		// historical bug. CollectionPolicySlabValidator (Phase 1) closes it by rejecting the
		// configuration outright at Update() — so by the time LateFeeCalculationService ever
		// sees a policy, a silent gap can no longer exist. This asserts the rejection, not a
		// calculation outcome, since the gap-at-calculation-time scenario is now unreachable.
		var ex = Assert.Throws<Volo.Abp.BusinessException>(() => BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.Slab;
			b.GracePeriodDays = 0;
			b.EscalationRules = new List<CollectionPolicyEscalationRule>
			{
				new(1, 10, EscalationCalculationType.Flat, 10, null),
				new(15, 20, EscalationCalculationType.Flat, 20, null)
			};
		}));

		Assert.Equal(CollectionPolicyErrorCodes.InvalidEscalationSlabs, ex.Code);
	}

	// ── Caps ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Maximum_Late_Fee_Amount_Caps_The_Result()
	{
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20;
			b.GracePeriodDays = 0;
			b.MaximumLateFeeAmount = 500;
		});
		var result = _service.Calculate(policy, Input(daysLate: 50)); // raw = 1000, capped to 500
		Assert.Equal(500, result.LateFee);
		Assert.Equal("maximumLateFeeAmount", result.CappedBy);
		Assert.Equal(1000, result.AmountBeforeCap);
	}

	[Fact]
	public void Maximum_Charging_Days_Freezes_Accrual_After_Cutoff()
	{
		// Collection_Policy_Design_Recommendations.md §2: "Freeze late fee after 90 days"
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20;
			b.GracePeriodDays = 0;
			b.MaximumChargingDays = 90;
		});
		var at90 = _service.Calculate(policy, Input(daysLate: 90));
		var at150 = _service.Calculate(policy, Input(daysLate: 150));

		Assert.Equal(1800, at90.LateFee); // 90 * 20
		Assert.Equal(1800, at150.LateFee); // frozen at day 90's value
		Assert.Equal("maximumChargingDays", at150.CappedBy);
	}

	// ── Rounding ─────────────────────────────────────────────────────────────

	[Theory]
	[InlineData(RoundingMethodType.RoundUp, 34)]
	[InlineData(RoundingMethodType.RoundDown, 34)]
	[InlineData(RoundingMethodType.NearestRupee, 34)]
	public void Rounding_Method_Is_Applied_To_The_Final_Amount(RoundingMethodType method, decimal expected)
	{
		var result = CalculateWithRoundingFixture(method);
		Assert.Equal(expected, result.LateFee);
	}

	[Fact]
	public void Legacy_rounding_options_use_the_system_whole_rupee_rule()
	{

		var result = CalculateWithRoundingFixture(RoundingMethodType.None);
		Assert.Equal(34m, result.LateFee);
		Assert.Equal("RoundUp", result.RoundingApplied);
	}

	private LateFeeCalculationResult CalculateWithRoundingFixture(RoundingMethodType method)
	{
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.Percentage;
			b.Percentage = 33.33m; // valid two-decimal rate produces a fractional amount to exercise rounding
			b.PercentageBase = PercentageBaseType.OutstandingAmount;
			b.GracePeriodDays = 0;
			b.RoundingMethod = method;
		});
		return _service.Calculate(policy, Input(daysLate: 1, outstanding: 100));
	}

	// ── Per-fee-head overrides (Phase 4a) ────────────────────────────────────

	[Fact]
	public void Fee_Head_With_No_Override_Row_Inherits_The_Policy_Level_Rule()
	{
		var tuitionFeeHeadId = Guid.NewGuid();
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20;
			b.GracePeriodDays = 0;
			b.LateFeeApplyOnFeeHeads = new[] { tuitionFeeHeadId };
		});

		var result = _service.Calculate(policy, InputForFeeHead(tuitionFeeHeadId, daysLate: 3));

		Assert.Equal(60, result.LateFee); // 20/day * 3 days, unaffected by an absent override
		Assert.Equal("Inherit", result.FeeHeadOverrideApplied);
	}

	[Fact]
	public void NoLateFee_Override_Zeroes_The_Fee_For_That_Fee_Head_Only()
	{
		var tuitionFeeHeadId = Guid.NewGuid();
		var transportFeeHeadId = Guid.NewGuid();
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20;
			b.GracePeriodDays = 0;
			b.LateFeeApplyOnFeeHeads = new[] { tuitionFeeHeadId, transportFeeHeadId };
			b.FeeHeadOverrides = new[]
			{
				new CollectionPolicyFeeHeadOverride(transportFeeHeadId, FeeHeadOverrideMode.NoLateFee, null)
			};
		});

		var transportResult = _service.Calculate(policy, InputForFeeHead(transportFeeHeadId, daysLate: 3));
		var tuitionResult = _service.Calculate(policy, InputForFeeHead(tuitionFeeHeadId, daysLate: 3));

		Assert.Equal(0, transportResult.LateFee);
		Assert.Equal("NoLateFee", transportResult.FeeHeadOverrideApplied);
		Assert.Equal(60, tuitionResult.LateFee); // tuition still uses the policy-level rule
		Assert.Equal("Inherit", tuitionResult.FeeHeadOverrideApplied);
	}

	[Fact]
	public void CustomRule_Override_Uses_The_Fee_Head_Level_Rule_Instead_Of_The_Policy_Level_One()
	{
		var libraryFeeHeadId = Guid.NewGuid();
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20; // policy-level rule — should NOT be used for libraryFeeHeadId
			b.GracePeriodDays = 0;
			b.LateFeeApplyOnFeeHeads = new[] { libraryFeeHeadId };
			b.FeeHeadOverrides = new[]
			{
				new CollectionPolicyFeeHeadOverride(
					libraryFeeHeadId,
					FeeHeadOverrideMode.CustomRule,
					new LateFeeRuleOverride(
						LateFeeCalculationType.Flat,
						FlatAmount: 50,
						Percentage: null,
						PercentageBase: PercentageBaseType.OutstandingAmount,
						AmountPerDay: null,
						AmountPerWeek: null,
						AmountPerMonth: null,
						CompoundFrequency: CompoundFrequencyType.Daily,
						EscalationRules: Array.Empty<CollectionPolicyEscalationRule>(),
						RoundingMethod: RoundingMethodType.None,
						MaximumLateFeeAmount: null,
						MaximumChargingDays: null))
			};
		});

		var result = _service.Calculate(policy, InputForFeeHead(libraryFeeHeadId, daysLate: 3));

		Assert.Equal(50, result.LateFee); // flat 50 from the override, not 3 * 20 = 60 from the policy
		Assert.Equal("CustomRule", result.FeeHeadOverrideApplied);
	}

	[Fact]
	public void CustomRule_Override_Respects_Its_Own_Maximum_Late_Fee_Cap()
	{
		var libraryFeeHeadId = Guid.NewGuid();
		var policy = BuildPolicy(b =>
		{
			b.LateFeeType = LateFeeCalculationType.PerDay;
			b.AmountPerDay = 20;
			b.GracePeriodDays = 0;
			b.LateFeeApplyOnFeeHeads = new[] { libraryFeeHeadId };
			b.FeeHeadOverrides = new[]
			{
				new CollectionPolicyFeeHeadOverride(
					libraryFeeHeadId,
					FeeHeadOverrideMode.CustomRule,
					new LateFeeRuleOverride(
						LateFeeCalculationType.PerDay,
						FlatAmount: null,
						Percentage: null,
						PercentageBase: PercentageBaseType.OutstandingAmount,
						AmountPerDay: 20,
						AmountPerWeek: null,
						AmountPerMonth: null,
						CompoundFrequency: CompoundFrequencyType.Daily,
						EscalationRules: Array.Empty<CollectionPolicyEscalationRule>(),
						RoundingMethod: RoundingMethodType.None,
						MaximumLateFeeAmount: 30,
						MaximumChargingDays: null))
			};
		});

		var result = _service.Calculate(policy, InputForFeeHead(libraryFeeHeadId, daysLate: 10));

		Assert.Equal(30, result.LateFee); // 20/day * 10 days = 200, capped by the override's own 30 ceiling
		Assert.Equal("maximumLateFeeAmount", result.CappedBy);
	}

	// ── Exception windows (Phase 4j) ─────────────────────────────────────────

	[Fact]
	public void SuspendLateFeeAccrual_Window_Produces_Zero_Late_Fee()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 0; });
		var input = new LateFeeCalculationInput(
			DaysLate: 30, OutstandingAmount: 5000, OriginalFeeAmount: 5000, RemainingBalance: 5000, FeeHeadId: null,
			ActiveExceptionWindowEffect: ExceptionWindowEffect.SuspendLateFeeAccrual,
			ExceptionWindowLabel: "gateway outage window");

		var result = _service.Calculate(policy, input);

		Assert.Equal(0, result.LateFee);
		Assert.Equal("gateway outage window", result.ExceptionWindowApplied);
	}

	[Fact]
	public void ExtendGracePeriod_Window_Adds_Extension_Days_To_The_Effective_Grace_Period()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 5; });
		var input = new LateFeeCalculationInput(
			DaysLate: 10, OutstandingAmount: 5000, OriginalFeeAmount: 5000, RemainingBalance: 5000, FeeHeadId: null,
			ActiveExceptionWindowEffect: ExceptionWindowEffect.ExtendGracePeriod,
			ExceptionWindowExtensionDays: 10,
			ExceptionWindowLabel: "bank strike window");

		var result = _service.Calculate(policy, input);

		// policy grace (5) + window extension (10) = 15 >= 10 days late -> still within grace
		Assert.Equal(0, result.LateFee);
		Assert.Equal(15, result.EffectiveGracePeriodDays);
	}

	[Fact]
	public void ExtendGracePeriod_Window_Still_Charges_Once_Extended_Grace_Is_Exceeded()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 5; });
		var input = new LateFeeCalculationInput(
			DaysLate: 20, OutstandingAmount: 5000, OriginalFeeAmount: 5000, RemainingBalance: 5000, FeeHeadId: null,
			ActiveExceptionWindowEffect: ExceptionWindowEffect.ExtendGracePeriod,
			ExceptionWindowExtensionDays: 10,
			ExceptionWindowLabel: "bank strike window");

		var result = _service.Calculate(policy, input);

		// effective grace = 5 + 10 = 15; 20 - 15 = 5 days late * 20/day = 100
		Assert.Equal(100, result.LateFee);
		Assert.Equal("bank strike window", result.ExceptionWindowApplied);
	}

	[Fact]
	public void No_Active_Window_Behaves_Exactly_Like_Before_Phase_4j()
	{
		var policy = BuildPolicy(b => { b.LateFeeType = LateFeeCalculationType.PerDay; b.AmountPerDay = 20; b.GracePeriodDays = 0; });
		var result = _service.Calculate(policy, Input(daysLate: 5));

		Assert.Equal(100, result.LateFee);
		Assert.Null(result.ExceptionWindowApplied);
	}
}
