using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using System;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;

/// <summary>
/// Single server-side authority for turning a bill-generation rule into an
/// installment demand date. The UI may preview the value, but persisted
/// schedules always use this calculation.
/// </summary>
public static class PaymentTermDemandDateCalculator
{
	public static DateTime Calculate(
		DemandGenerationRule rule,
		DateTime coverageStartDate,
		DateTime dueDate,
		int? offsetDays,
		DateTime? specificDate)
	{
		var maxOffsetDays = (dueDate.Date - coverageStartDate.Date).Days;
		if (rule == DemandGenerationRule.XDaysBeforeDue && offsetDays > maxOffsetDays)
		{
			throw new BusinessException(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum)
				.WithData("Maximum", Math.Max(0, maxOffsetDays))
				.WithData("Actual", offsetDays);
		}

		var demandDate = rule switch
		{
			DemandGenerationRule.OnInstallmentStartDate => coverageStartDate.Date,
			DemandGenerationRule.XDaysBeforeDue when offsetDays is >= 1 => dueDate.Date.AddDays(-offsetDays.Value),
			DemandGenerationRule.OnDueDate => dueDate.Date,
			DemandGenerationRule.SpecificDate when specificDate.HasValue => specificDate.Value.Date,
			DemandGenerationRule.XDaysBeforeDue => throw new BusinessException(
				PaymentTermErrorCodes.DemandOffsetDaysMustBeNonNegative),
			DemandGenerationRule.SpecificDate => throw new BusinessException(
				PaymentTermErrorCodes.DemandDateRequired),
			_ => throw new BusinessException(PaymentTermErrorCodes.DemandDateRequired)
		};
		ValidateRange(coverageStartDate, dueDate, demandDate);
		return demandDate;
	}

	public static void ValidateRange(DateTime coverageStartDate, DateTime dueDate, DateTime demandDate)
	{
		if (demandDate.Date < coverageStartDate.Date)
		{
			throw new BusinessException(PaymentTermErrorCodes.DemandDateBeforeCoverageStart)
				.WithData("CoverageStart", coverageStartDate.Date)
				.WithData("DemandDate", demandDate.Date);
		}

		if (demandDate.Date > dueDate.Date)
		{
			throw new BusinessException(PaymentTermErrorCodes.DemandDateAfterDueDate)
				.WithData("DemandDate", demandDate.Date)
				.WithData("DueDate", dueDate.Date);
		}
	}
}
