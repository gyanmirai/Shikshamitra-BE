using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class PaymentTermDemandDateCalculator_Tests
{
	private static readonly DateTime StartDate = new(2026, 6, 1);
	private static readonly DateTime DueDate = new(2026, 6, 20);

	[Theory]
	[InlineData(DemandGenerationRule.OnInstallmentStartDate, null, null, 1)]
	[InlineData(DemandGenerationRule.XDaysBeforeDue, 5, null, 15)]
	[InlineData(DemandGenerationRule.OnDueDate, null, null, 20)]
	[InlineData(DemandGenerationRule.SpecificDate, null, "2026-06-10", 10)]
	public void Calculate_Returns_Expected_Date(
		DemandGenerationRule rule,
		int? offsetDays,
		string? specificDate,
		int expectedDay)
	{
		var result = PaymentTermDemandDateCalculator.Calculate(
			rule,
			StartDate,
			DueDate,
			offsetDays,
			specificDate == null ? null : DateTime.Parse(specificDate));

		Assert.Equal(new DateTime(2026, 6, expectedDay), result);
	}

	[Fact]
	public void Calculate_SpecificDate_Requires_A_Date()
	{
		Assert.Throws<BusinessException>(() => PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.SpecificDate,
			StartDate,
			DueDate,
			null,
			null));
	}
}
