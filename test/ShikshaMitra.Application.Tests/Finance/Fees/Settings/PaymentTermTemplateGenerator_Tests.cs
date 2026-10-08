using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Shouldly;
using System;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermTemplateGenerator_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData(0)]
	[InlineData(-1)]
	public void Days_Before_Due_Requires_A_Positive_Offset(int? offset)
	{
		var error = Should.Throw<BusinessException>(() => PaymentTermTemplateGenerator.Generate(
			new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), new()
			{
				TemplateKind = PaymentTermTemplateKind.Annual,
				DemandGenerationRule = DemandGenerationRule.XDaysBeforeDue,
				DemandOffsetDays = offset
			}));
		error.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysMustBeNonNegative);
	}

	[Fact]
	public void Truncated_Last_Month_Determines_The_Offset_Maximum()
	{
		var input = new PreviewPaymentTermTemplateDto
		{
			TemplateKind = PaymentTermTemplateKind.Monthly,
			DueDateRule = PaymentTermTemplateDueDateRule.DaysAfterCoverageStart,
			DueDaysAfterCoverageStart = 13,
			DemandGenerationRule = DemandGenerationRule.XDaysBeforeDue,
			DemandOffsetDays = 10
		};
		var error = Should.Throw<BusinessException>(() => PaymentTermTemplateGenerator.Generate(
			new DateTime(2026, 6, 1), new DateTime(2026, 7, 5), input));
		error.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum);
		error.Data["Maximum"].ShouldBe(4);
		input.DemandOffsetDays = 4;
		var schedule = PaymentTermTemplateGenerator.Generate(new DateTime(2026, 6, 1), new DateTime(2026, 7, 5), input);
		schedule.ShouldAllBe(item => item.DemandDate >= item.CoverageStartDate && item.DemandDate <= item.DueDate);
		schedule[1].DemandDate.ShouldBe(schedule[1].CoverageStartDate);
	}

	[Fact]
	public void Missing_Specific_Dates_Are_Rejected_And_Legacy_Default_Still_Works()
	{
		var input = new PreviewPaymentTermTemplateDto { TemplateKind = PaymentTermTemplateKind.Annual };
		var schedule = PaymentTermTemplateGenerator.Generate(new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), input);
		schedule[0].DemandDate.ShouldBe(schedule[0].CoverageStartDate);
		input.DemandGenerationRule = DemandGenerationRule.SpecificDate;
		Should.Throw<BusinessException>(() => PaymentTermTemplateGenerator.Generate(
			new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), input))
			.Code.ShouldBe(PaymentTermErrorCodes.InvalidTemplateConfiguration);
	}

	[Theory]
	[InlineData("2026-06-01", null)]
	[InlineData("2027-05-31", null)]
	[InlineData("2026-05-31", PaymentTermErrorCodes.DemandDateBeforeCoverageStart)]
	[InlineData("2027-06-01", PaymentTermErrorCodes.DemandDateAfterDueDate)]
	public void Specific_Invoice_Dates_Are_Explicit_And_Within_The_Generated_Range(string date, string? errorCode)
	{
		var input = new PreviewPaymentTermTemplateDto
		{
			TemplateKind = PaymentTermTemplateKind.Annual,
			DemandGenerationRule = DemandGenerationRule.SpecificDate,
			SpecificDemandDates = [DateTime.Parse(date)]
		};
		if (errorCode != null)
		{
			Should.Throw<BusinessException>(() => PaymentTermTemplateGenerator.Generate(
				new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), input)).Code.ShouldBe(errorCode);
			return;
		}
		PaymentTermTemplateGenerator.Generate(new DateTime(2026, 6, 1), new DateTime(2027, 5, 31), input)
			[0].DemandDate.ShouldBe(input.SpecificDemandDates[0]);
	}
}
