using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

internal static class PaymentTermInstallmentValidator
{
	public static void PrepareAndValidate(
		DemandGenerationRule demandGenerationRule,
		int? demandOffsetDays,
		IReadOnlyList<CreatePaymentTermInstallmentDto> installments,
		DateTime boardStart,
		DateTime boardEnd)
	{
		if (installments == null || installments.Count == 0)
		{
			throw new BusinessException(PaymentTermErrorCodes.NoInstallments);
		}

		var ordered = installments.OrderBy(i => i.Sequence).ToList();
		for (var i = 0; i < ordered.Count; i++)
		{
			if (ordered[i].Sequence != i + 1)
			{
				throw new BusinessException(PaymentTermErrorCodes.DuplicateOrOutOfOrderSequence)
					.WithData("Expected", i + 1)
					.WithData("Actual", ordered[i].Sequence);
			}
		}

		if (ordered.Any(i => string.IsNullOrWhiteSpace(i.Name)))
		{
			throw new BusinessException(PaymentTermErrorCodes.NameRequired);
		}

		var names = ordered.Select(i => i.Name.Trim().ToLowerInvariant()).ToList();
		if (names.Distinct().Count() != names.Count)
		{
			throw new BusinessException(PaymentTermErrorCodes.DuplicateInstallmentName);
		}

		foreach (var installment in ordered)
		{
			if (string.IsNullOrWhiteSpace(installment.Name))
			{
				throw new BusinessException(PaymentTermErrorCodes.NameRequired);
			}

			if (installment.CoverageStartDate.Date > installment.CoverageEndDate.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.CoverageStartAfterEnd)
					.WithData("Installment", installment.Name);
			}

			if (installment.CoverageStartDate.Date < boardStart.Date ||
				installment.CoverageEndDate.Date > boardEnd.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.InstallmentDateOutsideBoardPeriod)
					.WithData("Installment", installment.Name)
					.WithData("BoardStart", boardStart.Date)
					.WithData("BoardEnd", boardEnd.Date);
			}

			try
			{
				installment.DemandDate = PaymentTermDemandDateCalculator.Calculate(
					demandGenerationRule,
					installment.CoverageStartDate,
					installment.DueDate,
					demandOffsetDays,
					installment.DemandDate);
			}
			catch (BusinessException exception)
			{
				exception.WithData("Installment", installment.Name);
				throw;
			}

			// Coverage may intentionally end before a later due date, but every
			// schedule event still belongs to the academic board period. Keeping
			// this invariant on the server prevents API callers from creating dates
			// the date pickers can never represent.
			if (installment.DemandDate.Value.Date < boardStart.Date ||
				installment.DemandDate.Value.Date > boardEnd.Date ||
				installment.DueDate.Date < boardStart.Date ||
				installment.DueDate.Date > boardEnd.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.InstallmentDateOutsideBoardPeriod)
					.WithData("Installment", installment.Name)
					.WithData("BoardStart", boardStart.Date)
					.WithData("BoardEnd", boardEnd.Date);
			}

			if (installment.AllocationPercentage <= 0)
			{
				throw new BusinessException(PaymentTermErrorCodes.AllocationPercentageMustBePositive)
					.WithData("Installment", installment.Name);
			}

			if (installment.AllocationPercentage > 100m)
			{
				throw new BusinessException(PaymentTermErrorCodes.AllocationPercentageExceeds100)
					.WithData("Installment", installment.Name)
					.WithData("Percentage", installment.AllocationPercentage);
			}
		}

		for (var i = 1; i < ordered.Count; i++)
		{
			if (ordered[i].DueDate.Date <= ordered[i - 1].DueDate.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.DuplicateOrOutOfOrderSequence)
					.WithData("Message", "Due dates must be in chronological order.");
			}

			if (ordered[i].CoverageStartDate.Date <= ordered[i - 1].CoverageEndDate.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.OverlappingCoverage)
					.WithData("InstallmentA", ordered[i - 1].Name)
					.WithData("InstallmentB", ordered[i].Name);
			}

			// Coverage gaps (e.g. summer break between terms) are allowed — not an error.
		}

		var total = ordered.Sum(i => i.AllocationPercentage);
		if (Math.Abs(total - 100m) > 0.01m)
		{
			throw new BusinessException(PaymentTermErrorCodes.PercentageTotalNot100)
				.WithData("Total", total);
		}
	}

	/// <summary>
	/// Validates a resolved (master + override) installment schedule for date/percentage rules.
	/// Used when Fee Structure customization can alter demand/due/allocation.
	/// </summary>
	public static void ValidateResolved(
		IReadOnlyList<(string Name, DateTime CoverageStart, DateTime CoverageEnd, DateTime DemandDate, DateTime DueDate, decimal AllocationPercentage)> installments)
	{
		if (installments == null || installments.Count == 0)
		{
			throw new BusinessException(PaymentTermErrorCodes.NoInstallments);
		}

		var ordered = installments.ToList();
		foreach (var installment in ordered)
		{
			if (installment.CoverageStart.Date > installment.CoverageEnd.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.CoverageStartAfterEnd)
					.WithData("Installment", installment.Name);
			}

			try
			{
				PaymentTermDemandDateCalculator.ValidateRange(
					installment.CoverageStart, installment.DueDate, installment.DemandDate);
			}
			catch (BusinessException exception)
			{
				exception.WithData("Installment", installment.Name);
				throw;
			}

			if (installment.AllocationPercentage <= 0)
			{
				throw new BusinessException(PaymentTermErrorCodes.AllocationPercentageMustBePositive)
					.WithData("Installment", installment.Name);
			}

			if (installment.AllocationPercentage > 100m)
			{
				throw new BusinessException(PaymentTermErrorCodes.AllocationPercentageExceeds100)
					.WithData("Installment", installment.Name);
			}
		}

		for (var i = 1; i < ordered.Count; i++)
		{
			if (ordered[i].DueDate.Date <= ordered[i - 1].DueDate.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.DuplicateOrOutOfOrderSequence)
					.WithData("Message", "Due dates must be in chronological order.");
			}

			if (ordered[i].CoverageStart.Date <= ordered[i - 1].CoverageEnd.Date)
			{
				throw new BusinessException(PaymentTermErrorCodes.OverlappingCoverage)
					.WithData("InstallmentA", ordered[i - 1].Name)
					.WithData("InstallmentB", ordered[i].Name);
			}
		}

		var total = ordered.Sum(i => i.AllocationPercentage);
		if (Math.Abs(total - 100m) > 0.01m)
		{
			throw new BusinessException(PaymentTermErrorCodes.PercentageTotalNot100)
				.WithData("Total", total);
		}
	}
}
