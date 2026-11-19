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

	[Theory]
	[InlineData(19)]
	[InlineData(1)]
	public void Calculate_Offset_Within_Start_To_Due_Range_Is_Accepted(int offset)
	{
		Assert.Equal(DueDate.AddDays(-offset), PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.XDaysBeforeDue, StartDate, DueDate, offset, null));
	}

	[Theory]
	[InlineData(20)]
	[InlineData(99)]
	[InlineData(int.MaxValue)]
	public void Calculate_Excessive_Offset_Is_Rejected_Before_Date_Arithmetic(int offset)
	{
		var exception = Assert.Throws<BusinessException>(() => PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.XDaysBeforeDue, StartDate, DueDate, offset, null));
		Assert.Equal(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum, exception.Code);
		Assert.Equal(19, exception.Data["Maximum"]);
	}

	[Theory]
	[InlineData("2026-05-31", PaymentTermErrorCodes.DemandDateBeforeCoverageStart)]
	[InlineData("2026-06-21", PaymentTermErrorCodes.DemandDateAfterDueDate)]
	public void Calculate_Manual_Date_Outside_Allowed_Range_Is_Rejected(string date, string code)
	{
		var exception = Assert.Throws<BusinessException>(() => PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.SpecificDate, StartDate, DueDate, null, DateTime.Parse(date)));
		Assert.Equal(code, exception.Code);
	}

	[Theory]
	[InlineData("2026-06-01")]
	[InlineData("2026-06-20")]
	public void Calculate_Manual_Boundaries_Are_Accepted_Regardless_Of_Today(string date)
	{
		var invoiceDate = DateTime.Parse(date);
		Assert.Equal(invoiceDate, PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.SpecificDate, StartDate, DueDate, null, invoiceDate));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Calculate_Offset_Requires_At_Least_One_Day(int offset)
	{
		var exception = Assert.Throws<BusinessException>(() => PaymentTermDemandDateCalculator.Calculate(
			DemandGenerationRule.XDaysBeforeDue, StartDate, DueDate, offset, null));
		Assert.Equal(PaymentTermErrorCodes.DemandOffsetDaysMustBeNonNegative, exception.Code);
	}
}
