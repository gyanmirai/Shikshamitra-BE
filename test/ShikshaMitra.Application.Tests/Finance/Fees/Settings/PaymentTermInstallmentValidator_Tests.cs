using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Shouldly;
using System;
using System.Collections.Generic;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermInstallmentValidator_Tests
{
	private static readonly DateTime BoardStart = new(2026, 6, 2);
	private static readonly DateTime BoardEnd = new(2027, 5, 30);

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
