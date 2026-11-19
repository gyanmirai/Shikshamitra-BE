using System;
using System.Linq;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Templates;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class GrossEarningAllocation_Tests
{
	[Fact]
	public void Bug6475_fixed_earnings_cannot_be_squeezed_into_a_smaller_gross()
	{
		GrossEarningRule[] rules = [new(Basic, true, SalaryTemplateFormulaType.FixedAmount, 2000m),
			new(Other, false, SalaryTemplateFormulaType.FixedAmount, 50000m)];
		var error = Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(50000m, rules));
		error.Code.ShouldBe(HRErrorCodes.SalaryTemplateAllocationMismatch);
		error.Data["allocated"].ShouldBe(52000m);
		GrossEarningAllocation.Calculate(52000m, rules).Values.Sum().ShouldBe(52000m);
	}

	private static readonly Guid Basic = Guid.Parse("00000000-0000-0000-0000-000000000001");
	private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");
	private static readonly Guid Third = Guid.Parse("00000000-0000-0000-0000-000000000003");

	[Theory]
	[InlineData(20000.01, 10000, 10000.01)]
	[InlineData(0.01, 0, 0.01)]
	[InlineData(0, 0, 0)]
	public void Largest_remainder_is_stable_and_keeps_gross(decimal gross, decimal basic, decimal other)
	{
		GrossEarningRule[] rules = [new(Basic, true, SalaryTemplateFormulaType.PercentageOfGross, 50m),
			new(Other, false, SalaryTemplateFormulaType.PercentageOfGross, 50m)];
		foreach (var order in new[] { rules, rules.Reverse().ToArray() })
		{
			var result = GrossEarningAllocation.Calculate(gross, order);
			result[Basic].ShouldBe(basic);
			result[Other].ShouldBe(other);
			result.Values.Sum().ShouldBe(gross);
		}
	}

	[Fact]
	public void Chained_earnings_use_raw_values_and_ties_use_component_id()
	{
		GrossEarningRule[] rules = [new(Basic, true, SalaryTemplateFormulaType.PercentageOfGross, 50m),
			new(Other, false, SalaryTemplateFormulaType.PercentageOfBasic, 50m),
			new(Third, false, SalaryTemplateFormulaType.PercentageOfComponent, 100m, Other)];
		var result = GrossEarningAllocation.Calculate(0.03m, rules.Reverse().ToArray());
		result[Basic].ShouldBe(0.01m);
		result[Other].ShouldBe(0.01m);
		result[Third].ShouldBe(0.01m);
	}

	[Theory]
	[InlineData(60)]
	[InlineData(110)]
	[InlineData(99.9999)]
	public void Rounding_cannot_conceal_incomplete_allocations(decimal total)
	{
		GrossEarningRule[] rules = [new(Basic, true, SalaryTemplateFormulaType.PercentageOfGross, 50m),
			new(Other, false, SalaryTemplateFormulaType.PercentageOfGross, total - 50m)];
		Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(0.01m, rules)).Code
			.ShouldBe(HRErrorCodes.SalaryTemplateAllocationMismatch);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(0.5)]
	[InlineData(0)]
	[InlineData(0.33333333333333)]
	public void Fixed_monthly_inputs_are_normalized_and_attendance_applied_once(decimal factor)
	{
		GrossEarningRule[] rules = [new(Basic, true, SalaryTemplateFormulaType.FixedAmount, 12000.004m),
			new(Other, false, SalaryTemplateFormulaType.FixedAmount, 8000m)];
		var result = GrossEarningAllocation.Calculate(20000m * factor, rules, factor);
		result.Values.Sum().ShouldBe(GrossEarningAllocation.Money(20000m * factor));
		if (factor > 0m)
			Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(21000m * factor, rules, factor));
	}

	[Fact]
	public void Allocation_errors_preserve_fractional_paise_and_explain_negative_remainders()
	{
		GrossEarningRule[] incomplete = [new(Basic, true, SalaryTemplateFormulaType.PercentageOfGross, 50m)];
		var error = Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(20000.01m, incomplete));
		error.Data["message"].ShouldBe("Gross is INR 20000.01; regular earnings allocate INR 10000.005; missing INR 10000.005. Adjust the earning rules or choose an explicit remainder component.");
		GrossEarningRule[] excess = [new(Basic, true, SalaryTemplateFormulaType.FixedAmount, 21000m),
			new(Other, false, SalaryTemplateFormulaType.RemainderOfGross, null)];
		var remainderError = Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(20000m, excess));
		remainderError.Code.ShouldBe("HR:Payroll:SalaryTemplateEarningsExceedGross");
		remainderError.Data["allocated"].ShouldBe(21000m);
		remainderError.Data["message"].ShouldBeOfType<string>().ShouldContain("excess INR 1000.00");
	}

	[Fact]
	public void Explicit_remainder_and_mixed_formulas_allocate_only_compatible_salaries()
	{
		GrossEarningRule[] mixed = [new(Basic, true, SalaryTemplateFormulaType.FixedAmount, 12000m),
			new(Other, false, SalaryTemplateFormulaType.PercentageOfGross, 40m)];
		GrossEarningAllocation.Calculate(20000m, mixed).Values.Sum().ShouldBe(20000m);
		Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(21000m, mixed));
		var remainder = mixed.Append(new GrossEarningRule(Third, false, SalaryTemplateFormulaType.RemainderOfGross, null)).ToArray();
		GrossEarningAllocation.Calculate(21000.01m, remainder).Values.Sum().ShouldBe(21000.01m);
		Should.Throw<BusinessException>(() => GrossEarningAllocation.Calculate(19000m, remainder));
	}
}
