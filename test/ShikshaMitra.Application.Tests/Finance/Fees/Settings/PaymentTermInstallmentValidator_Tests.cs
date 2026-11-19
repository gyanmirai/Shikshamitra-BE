using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermInstallmentValidator_Tests
{
	private static readonly DateTime BoardStart = new(2026, 6, 2);
	private static readonly DateTime BoardEnd = new(2027, 5, 30);

	[Fact]
	public void Specific_Template_Invoice_Dates_Preserve_Calendar_Dates_In_Time_Zones()
	{
		const string json = """
			{"specificDemandDates":["2026-06-01T00:00:00+05:30","2027-05-31T23:00:00-04:00"]}
			""";
		var input = JsonSerializer.Deserialize<PreviewPaymentTermTemplateDto>(json,
			new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
		input.SpecificDemandDates.ShouldBe([
			new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc)]);
	}

	[Fact]
	public void Offset_Timestamps_Keep_Their_Calendar_Dates_At_Board_Boundaries()
	{
		const string json = """
			{
			  "sequence": 1,
			  "name": "Quarter 1",
			  "coverageStartDate": "2026-06-01T00:00:00+05:30",
			  "coverageEndDate": "2027-05-31T00:00:00+05:30",
			  "demandDate": "2026-06-01T00:00:00+05:30",
			  "dueDate": "2027-05-31T00:00:00+05:30",
			  "reminderDate": "2027-05-30T00:00:00+05:30",
			  "allocationPercentage": 100
			}
			""";
		var installment = JsonSerializer.Deserialize<CreatePaymentTermInstallmentDto>(json,
			new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

		installment.CoverageStartDate.ShouldBe(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		installment.CoverageEndDate.ShouldBe(new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc));
		installment.DemandDate.ShouldBe(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
		installment.DueDate.ShouldBe(new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc));
		installment.ReminderDate.ShouldBe(new DateTime(2027, 5, 30, 0, 0, 0, DateTimeKind.Utc));

		Should.NotThrow(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.OnInstallmentStartDate,
			null,
			[installment],
			new DateTime(2026, 6, 1),
			new DateTime(2027, 5, 31)));
	}

	[Fact]
	public void Installment_Inside_Academic_Year_But_Before_Board_Start_Is_Rejected()
	{
		var installments = BuildInstallments(new DateTime(2026, 5, 1), new DateTime(2026, 6, 30));

		var exception = Should.Throw<BusinessException>(() =>
			PaymentTermInstallmentValidator.PrepareAndValidate(
				DemandGenerationRule.OnInstallmentStartDate,
				null,
				installments,
				BoardStart,
				BoardEnd));

		exception.Code.ShouldBe(PaymentTermErrorCodes.InstallmentDateOutsideBoardPeriod);
	}

	[Fact]
	public void Installment_On_Board_Boundaries_Is_Accepted()
	{
		var installments = BuildInstallments(BoardStart, BoardEnd);

		Should.NotThrow(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.OnInstallmentStartDate,
			null,
			installments,
			BoardStart,
			BoardEnd));
	}

	[Fact]
	public void Due_Date_After_Coverage_End_Is_Accepted()
	{
		var installments = BuildInstallments(
			new DateTime(2026, 9, 1),
			new DateTime(2026, 9, 29));
		installments[0].DueDate = new DateTime(2026, 12, 2);

		Should.NotThrow(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.OnInstallmentStartDate,
			null,
			installments,
			BoardStart,
			BoardEnd));
	}

	[Fact]
	public void Due_Date_Outside_Board_Period_Is_Rejected()
	{
		var installments = BuildInstallments(BoardStart, BoardEnd);
		installments[0].DueDate = BoardEnd.AddDays(1);

		var exception = Should.Throw<BusinessException>(() =>
			PaymentTermInstallmentValidator.PrepareAndValidate(
				DemandGenerationRule.OnInstallmentStartDate,
				null,
				installments,
				BoardStart,
				BoardEnd));

		exception.Code.ShouldBe(PaymentTermErrorCodes.InstallmentDateOutsideBoardPeriod);
	}

	[Fact]
	public void Offset_Uses_The_Most_Restrictive_Installment_And_Names_It()
	{
		var installments = BuildInstallments(BoardStart, new DateTime(2026, 11, 30));
		installments[0].AllocationPercentage = 50m;
		installments[0].DueDate = BoardStart.AddDays(9);
		installments.Add(new()
		{
			Sequence = 2,
			Name = "Half-Yearly 2",
			CoverageStartDate = new DateTime(2026, 12, 1),
			CoverageEndDate = BoardEnd,
			DueDate = new DateTime(2026, 12, 5),
			AllocationPercentage = 50m
		});

		var exception = Should.Throw<BusinessException>(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.XDaysBeforeDue, 5, installments, BoardStart, BoardEnd));
		exception.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum);
		exception.Data["Maximum"].ShouldBe(4);
		exception.Data["Installment"].ShouldBe("Half-Yearly 2");

		Should.NotThrow(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.XDaysBeforeDue, 4, installments, BoardStart, BoardEnd));
		installments[1].DemandDate.ShouldBe(new DateTime(2026, 12, 1));
	}

	[Fact]
	public void Specific_Invoice_Before_Installment_Start_Inside_Board_Period_Is_Rejected()
	{
		var installments = BuildInstallments(new DateTime(2026, 12, 1), BoardEnd);
		installments[0].DemandDate = new DateTime(2026, 10, 1);
		var exception = Should.Throw<BusinessException>(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.SpecificDate, null, installments, BoardStart, BoardEnd));
		exception.Code.ShouldBe(PaymentTermErrorCodes.DemandDateBeforeCoverageStart);
		exception.Data["Installment"].ShouldBe("Installment 1");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Missing_Installment_Name_Returns_Validation_Error(string? name)
	{
		var installments = BuildInstallments(BoardStart, BoardEnd);
		installments[0].Name = name!;
		var exception = Should.Throw<BusinessException>(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.OnInstallmentStartDate, null, installments, BoardStart, BoardEnd));
		exception.Code.ShouldBe(PaymentTermErrorCodes.NameRequired);
	}

	[Fact]
	public void Invalid_Allocation_Total_Is_Rejected()
	{
		var installments = BuildInstallments(BoardStart, BoardEnd);
		installments[0].AllocationPercentage = 61m;
		var exception = Should.Throw<BusinessException>(() => PaymentTermInstallmentValidator.PrepareAndValidate(
			DemandGenerationRule.OnInstallmentStartDate, null, installments, BoardStart, BoardEnd));
		exception.Code.ShouldBe(PaymentTermErrorCodes.PercentageTotalNot100);
	}

	[Fact]
	public void Template_Cannot_Preview_Invoice_Dates_Before_Installment_Start()
	{
		var exception = Should.Throw<BusinessException>(() => PaymentTermTemplateGenerator.Generate(
			BoardStart, BoardEnd, new PreviewPaymentTermTemplateDto
			{
				TemplateKind = PaymentTermTemplateKind.HalfYearly,
				DueDateRule = PaymentTermTemplateDueDateRule.DaysAfterCoverageStart,
				DueDaysAfterCoverageStart = 4,
				DemandGenerationRule = DemandGenerationRule.XDaysBeforeDue,
				DemandOffsetDays = 99
			}));
		exception.Code.ShouldBe(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum);
	}

	[Fact]
	public void Resolved_Customization_Cannot_Bypass_Invoice_Range()
	{
		var exception = Should.Throw<BusinessException>(() => PaymentTermInstallmentValidator.ValidateResolved(
			[("Installment 1", BoardStart, BoardEnd, BoardStart.AddDays(-1), BoardStart.AddDays(4), 100m)]));
		exception.Code.ShouldBe(PaymentTermErrorCodes.DemandDateBeforeCoverageStart);
	}

	private static List<CreatePaymentTermInstallmentDto> BuildInstallments(DateTime start, DateTime end) =>
	[
		new()
		{
			Sequence = 1,
			Name = "Installment 1",
			CoverageStartDate = start,
			CoverageEndDate = end,
			DueDate = end,
			AllocationPercentage = 100m,
			DemandEnabled = true
		}
	];
}
