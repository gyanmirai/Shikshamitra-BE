using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

internal static class PaymentTermTemplateGenerator
{
	public static List<CreatePaymentTermInstallmentDto> Generate(
		DateTime boardStart,
		DateTime boardEnd,
		PreviewPaymentTermTemplateDto input)
	{
		var start = boardStart.Date;
		var end = boardEnd.Date;
		if (start > end)
		{
			throw Invalid("The selected board period is invalid.");
		}

		var periods = input.TemplateKind == PaymentTermTemplateKind.Monthly
			? BuildMonthlyPeriods(start, end)
			: BuildBalancedPeriods(start, end, ResolveCount(input));
		if (input.DemandGenerationRule == DemandGenerationRule.SpecificDate &&
			input.SpecificDemandDates?.Count != periods.Count)
		{
			throw Invalid($"Provide one invoice date for each of the {periods.Count} generated installments.");
		}
		if (input.DemandGenerationRule == DemandGenerationRule.XDaysBeforeDue)
		{
			var maximum = Math.Min(365, periods.Min(period =>
				(ResolveDueDate(period.Start, period.End, input) - period.Start).Days));
			if (input.DemandOffsetDays > maximum)
			{
				throw new BusinessException(PaymentTermErrorCodes.DemandOffsetDaysExceedsMaximum)
					.WithData("Maximum", maximum)
					.WithData("Actual", input.DemandOffsetDays);
			}
		}

		var installments = new List<CreatePaymentTermInstallmentDto>(periods.Count);
		var baseAllocation = Math.Floor(10000m / periods.Count) / 100m;
		for (var index = 0; index < periods.Count; index++)
		{
			var period = periods[index];
			var dueDate = ResolveDueDate(period.Start, period.End, input);
			var specificDemandDate = input.DemandGenerationRule == DemandGenerationRule.SpecificDate
				? input.SpecificDemandDates![index]
				: (DateTime?)null;
			var demandDate = PaymentTermDemandDateCalculator.Calculate(
				input.DemandGenerationRule,
				period.Start,
				dueDate,
				input.DemandOffsetDays,
				specificDemandDate);

			installments.Add(new CreatePaymentTermInstallmentDto
			{
				Sequence = index + 1,
				Name = GetName(input.TemplateKind, index + 1, period.Start),
				CoverageStartDate = period.Start,
				CoverageEndDate = period.End,
				DemandDate = demandDate,
				DueDate = dueDate,
				AllocationPercentage = index == periods.Count - 1
					? 100m - (baseAllocation * index)
					: baseAllocation,
				DemandEnabled = true,
				ReminderEnabled = false
			});
		}

		PaymentTermInstallmentValidator.PrepareAndValidate(
			input.DemandGenerationRule,
			input.DemandOffsetDays,
			installments,
			start,
			end);
		return installments;
	}

	private static int ResolveCount(PreviewPaymentTermTemplateDto input) => input.TemplateKind switch
	{
		PaymentTermTemplateKind.Annual => 1,
		PaymentTermTemplateKind.HalfYearly => 2,
		PaymentTermTemplateKind.Quarterly => 4,
		PaymentTermTemplateKind.Custom when input.CustomInstallmentCount is >= 1 and <= 36 =>
			input.CustomInstallmentCount.Value,
		PaymentTermTemplateKind.Custom => throw Invalid("Custom installment count must be between 1 and 36."),
		_ => throw Invalid("Unsupported payment term template.")
	};

	private static List<(DateTime Start, DateTime End)> BuildMonthlyPeriods(
		DateTime start,
		DateTime end,
		bool enforceMaximum = true)
	{
		var periods = new List<(DateTime Start, DateTime End)>();
		var cursor = start;
		while (cursor <= end)
		{
			if (enforceMaximum && periods.Count == 36)
			{
				throw Invalid("Monthly templates support a maximum of 36 installments.");
			}
			var monthEnd = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
			var periodEnd = monthEnd < end ? monthEnd : end;
			periods.Add((cursor, periodEnd));
			cursor = periodEnd.AddDays(1);
		}
		return periods;
	}

	private static List<(DateTime Start, DateTime End)> BuildBalancedPeriods(DateTime start, DateTime end, int count)
	{
		var totalDays = (end - start).Days + 1;
		if (count > totalDays)
		{
			throw Invalid("The board period is too short for the selected installment count.");
		}

		var monthlyPeriods = BuildMonthlyPeriods(start, end, enforceMaximum: false);
		if (monthlyPeriods.Count >= count)
		{
			var monthAlignedPeriods = new List<(DateTime Start, DateTime End)>(count);
			var baseMonthsPerPeriod = monthlyPeriods.Count / count;
			var remainderMonths = monthlyPeriods.Count % count;
			var monthIndex = 0;
			for (var index = 0; index < count; index++)
			{
				var monthsInPeriod = baseMonthsPerPeriod + (index < remainderMonths ? 1 : 0);
				var periodStart = monthlyPeriods[monthIndex].Start;
				monthIndex += monthsInPeriod;
				monthAlignedPeriods.Add((periodStart, monthlyPeriods[monthIndex - 1].End));
			}
			return monthAlignedPeriods;
		}

		var periods = new List<(DateTime Start, DateTime End)>(count);
		for (var index = 0; index < count; index++)
		{
			var periodStart = start.AddDays((long)totalDays * index / count);
			var nextStart = start.AddDays((long)totalDays * (index + 1) / count);
			periods.Add((periodStart, nextStart.AddDays(-1)));
		}
		return periods;
	}

	private static DateTime ResolveDueDate(
		DateTime coverageStart,
		DateTime coverageEnd,
		PreviewPaymentTermTemplateDto input) => input.DueDateRule switch
	{
		PaymentTermTemplateDueDateRule.CoverageStart => coverageStart,
		PaymentTermTemplateDueDateRule.CoverageEnd => coverageEnd,
		PaymentTermTemplateDueDateRule.DaysAfterCoverageStart
			when input.DueDaysAfterCoverageStart is >= 0 and <= 365 =>
			coverageStart.AddDays(input.DueDaysAfterCoverageStart.Value) <= coverageEnd
				? coverageStart.AddDays(input.DueDaysAfterCoverageStart.Value)
				: coverageEnd,
		PaymentTermTemplateDueDateRule.DaysAfterCoverageStart =>
			throw Invalid("Days after coverage start must be between 0 and 365."),
		_ => throw Invalid("Unsupported template due-date rule.")
	};

	private static string GetName(PaymentTermTemplateKind kind, int sequence, DateTime periodStart) => kind switch
	{
		PaymentTermTemplateKind.Annual => "Annual Payment",
		PaymentTermTemplateKind.HalfYearly => $"Half-Yearly {sequence}",
		PaymentTermTemplateKind.Quarterly => $"Quarter {sequence}",
		PaymentTermTemplateKind.Monthly => periodStart.ToString("MMM yyyy", CultureInfo.InvariantCulture),
		_ => $"Installment {sequence}"
	};

	private static BusinessException Invalid(string reason) =>
		new BusinessException(PaymentTermErrorCodes.InvalidTemplateConfiguration)
			.WithData("Reason", reason);
}
