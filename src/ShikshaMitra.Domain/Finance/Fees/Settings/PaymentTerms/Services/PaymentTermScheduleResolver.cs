using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;

/// <summary>
/// Sole domain entry point for composing a fee-head schedule from a Payment Term version
/// and optional structure overrides.
/// </summary>
public class PaymentTermScheduleResolver(
	IRepository<PaymentTerm, Guid> paymentTermRepository
) : DomainService
{
	public async Task<ResolvedFeeHeadSchedule> ResolveAsync(FeeStructureDetail detail)
	{
		if (!detail.PaymentTermId.HasValue || !detail.PaymentTermVersionId.HasValue)
		{
			return ResolvedFeeHeadSchedule.Legacy(detail);
		}

		var term = await paymentTermRepository.FindAsync(detail.PaymentTermId.Value);
		if (term == null)
		{
			throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("PaymentTermId", detail.PaymentTermId);
		}
		return Resolve(detail, term);
	}

	/// <summary>Compose a schedule from a term already read in the current operation.</summary>
	public ResolvedFeeHeadSchedule Resolve(FeeStructureDetail detail, PaymentTerm term)
	{
		if (!detail.PaymentTermId.HasValue || !detail.PaymentTermVersionId.HasValue)
			return ResolvedFeeHeadSchedule.Legacy(detail);
		if (term.Id != detail.PaymentTermId.Value)
			throw new BusinessException(PaymentTermErrorCodes.NotFound).WithData("PaymentTermId", detail.PaymentTermId);

		return ResolveVersion(detail, term, detail.PaymentTermVersionId.Value, applyStructureAmounts: true);
	}

	/// <summary>Resolve an explicitly selected service-period term, using overrides only for their pinned version.</summary>
	public ResolvedFeeHeadSchedule ResolveServiceTerm(FeeStructureDetail detail, PaymentTerm term, Guid versionId) =>
		ResolveVersion(detail, term, versionId, applyStructureAmounts: false);

	private static ResolvedFeeHeadSchedule ResolveVersion(FeeStructureDetail detail, PaymentTerm term, Guid versionId, bool applyStructureAmounts)
	{
		var version = term.Versions.FirstOrDefault(v => v.Id == versionId)
			?? throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("VersionId", versionId);

		if (version.Status != PaymentTermVersionStatus.Published &&
			version.Status != PaymentTermVersionStatus.Superseded)
		{
			throw new BusinessException(PaymentTermErrorCodes.VersionNotPublished)
				.WithData("VersionId", version.Id)
				.WithData("Status", version.Status.ToString());
		}

		var annualAmount = applyStructureAmounts && detail.Amount.HasValue ? ShikshaMitra.Finance.WholeRupeeAmounts.RoundUp(detail.Amount.Value) : (decimal?)null;
		var monthlyRate = applyStructureAmounts ? detail.MonthlyRate : null;
		var installments = new List<ResolvedInstallment>();
		var allocated = 0m;
		var masterInstallments = version.Installments.OrderBy(i => i.Sequence).ToList();
		var usesOverrides = detail.PaymentTermId == term.Id && detail.PaymentTermVersionId == versionId &&
			detail.ScheduleMode == ScheduleMode.Customized;

		for (var index = 0; index < masterInstallments.Count; index++)
		{
			var masterInstallment = masterInstallments[index];
			var coverageStart = masterInstallment.CoverageStartDate;
			var coverageEnd = masterInstallment.CoverageEndDate;
			var demandDate = masterInstallment.DemandDate;
			var dueDate = masterInstallment.DueDate;
			var allocationPercentage = masterInstallment.AllocationPercentage;

			if (usesOverrides)
			{
				var overrideEntry = detail.ScheduleOverrides
					.FirstOrDefault(o => o.SourcePaymentTermInstallmentId == masterInstallment.Id);

				if (overrideEntry != null)
				{
					coverageStart = overrideEntry.CoverageStartDate ?? coverageStart;
					coverageEnd = overrideEntry.CoverageEndDate ?? coverageEnd;
					demandDate = overrideEntry.DemandDate ?? demandDate;
					dueDate = overrideEntry.DueDate ?? dueDate;
					allocationPercentage = overrideEntry.AllocationPercentage ?? allocationPercentage;
				}
			}

			// A structure override may move the period or due date. Recalculate all
			// derived bill dates from the owning term rule; only SpecificDate remains
			// an explicit per-installment date.
			if (version.DemandGenerationRule != DemandGenerationRule.SpecificDate)
			{
				demandDate = PaymentTermDemandDateCalculator.Calculate(
					version.DemandGenerationRule,
					coverageStart,
					dueDate,
					version.DemandOffsetDays,
					null);
			}

			decimal? calculatedAmount;
			if (monthlyRate is decimal rate)
			{
				calculatedAmount = MonthlyFeePricing.AmountPerInstallment(rate);
				allocationPercentage = decimal.Floor(100_000_000m / masterInstallments.Count) / 1_000_000m;
				if (index == masterInstallments.Count - 1)
					allocationPercentage = 100m - installments.Sum(row => row.AllocationPercentage);
			}
			else if (!annualAmount.HasValue)
            {
                calculatedAmount = null;
            }
            else if (index == masterInstallments.Count - 1)
			{
				// Absorb rounding residual on the last installment.
				calculatedAmount = annualAmount.Value - allocated;
			}
			else
			{
				calculatedAmount = decimal.Floor(annualAmount.Value * allocationPercentage / 100m);
				allocated += calculatedAmount.Value;
			}

			installments.Add(new ResolvedInstallment(
				masterInstallment.Id,
				masterInstallment.Sequence,
				masterInstallment.Name,
				coverageStart,
				coverageEnd,
				demandDate,
				dueDate,
				allocationPercentage,
				calculatedAmount,
				masterInstallment.AllowPartialOverride,
				masterInstallment.GracePeriodDaysOverride));
		}

		if (monthlyRate.HasValue)
		{
			if (!MonthlyFeePricing.IsMonthlySchedule(installments))
				throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration)
					.WithData("Reason", "Monthly pricing requires one installment per covered calendar month.");
			annualAmount = installments.Sum(row => row.CalculatedAmount);
		}

		return new ResolvedFeeHeadSchedule(
			detail.Id,
			detail.FeeHeadId,
			detail.FeeHead?.Name,
			annualAmount,
			term.Id,
			term.Name,
			versionId,
			version.VersionNumber,
			usesOverrides ? ScheduleMode.Customized : ScheduleMode.Inherited,
			installments,
			isValid: true);
	}

	/// <summary>Read each distinct template once for a calculation, including its pinned versions.</summary>
	public async Task<IReadOnlyDictionary<Guid, PaymentTerm>> LoadTermsAsync(IEnumerable<FeeStructureDetail> details)
	{
		var ids = details.Where(detail => detail.PaymentTermId.HasValue && detail.PaymentTermVersionId.HasValue)
			.Select(detail => detail.PaymentTermId!.Value).Distinct().ToArray();
		if (ids.Length == 0) return new Dictionary<Guid, PaymentTerm>();
		using var tracking = paymentTermRepository.DisableTracking();
		return (await paymentTermRepository.GetListAsync(term => Enumerable.Contains(ids, term.Id)))
			.ToDictionary(term => term.Id);
	}

	public ResolvedFeeHeadSchedule ResolveFromTerms(FeeStructureDetail detail, IReadOnlyDictionary<Guid, PaymentTerm> terms)
	{
		if (!detail.PaymentTermId.HasValue || !detail.PaymentTermVersionId.HasValue)
			return ResolvedFeeHeadSchedule.Legacy(detail);
		if (!terms.TryGetValue(detail.PaymentTermId.Value, out var term))
			throw new BusinessException(PaymentTermErrorCodes.NotFound).WithData("PaymentTermId", detail.PaymentTermId);
		return Resolve(detail, term);
	}

	public async Task<List<ResolvedFeeHeadSchedule>> ResolveAllAsync(FeeStructure feeStructure)
	{
		var terms = await LoadTermsAsync(feeStructure.FeeStructureDetails);
		return feeStructure.FeeStructureDetails.Select(detail => ResolveFromTerms(detail, terms)).ToList();
	}
}

public sealed class ResolvedFeeHeadSchedule
{
	public Guid FeeStructureDetailId { get; }
	public Guid FeeHeadId { get; }
	public string? FeeHeadName { get; }
	public decimal? AnnualAmount { get; }
	public Guid? PaymentTermId { get; }
	public string? PaymentTermName { get; }
	public Guid? PaymentTermVersionId { get; }
	public int? VersionNumber { get; }
	public ScheduleMode ScheduleMode { get; }
	public IReadOnlyList<ResolvedInstallment> Installments { get; }
	public bool IsValid { get; }

	public ResolvedFeeHeadSchedule(
		Guid feeStructureDetailId,
		Guid feeHeadId,
		string? feeHeadName,
		decimal? annualAmount,
		Guid? paymentTermId,
		string? paymentTermName,
		Guid? paymentTermVersionId,
		int? versionNumber,
		ScheduleMode scheduleMode,
		IReadOnlyList<ResolvedInstallment> installments,
		bool isValid)
	{
		FeeStructureDetailId = feeStructureDetailId;
		FeeHeadId = feeHeadId;
		FeeHeadName = feeHeadName;
		AnnualAmount = annualAmount;
		PaymentTermId = paymentTermId;
		PaymentTermName = paymentTermName;
		PaymentTermVersionId = paymentTermVersionId;
		VersionNumber = versionNumber;
		ScheduleMode = scheduleMode;
		Installments = installments;
		IsValid = isValid;
	}

	public static ResolvedFeeHeadSchedule Legacy(FeeStructureDetail detail) => new(
		detail.Id,
		detail.FeeHeadId,
		detail.FeeHead?.Name,
		detail.Amount,
		null,
		null,
		null,
		null,
		ScheduleMode.Inherited,
		Array.Empty<ResolvedInstallment>(),
		isValid: false);
}

public sealed class ResolvedInstallment
{
	public Guid PaymentTermInstallmentId { get; }
	public int Sequence { get; }
	public string Name { get; }
	public DateTime CoverageStartDate { get; }
	public DateTime CoverageEndDate { get; }
	public DateTime DemandDate { get; }
	public DateTime DueDate { get; }
	public decimal AllocationPercentage { get; }
	public decimal? CalculatedAmount { get; }
	public bool? AllowPartialOverride { get; }
	public int? GracePeriodDaysOverride { get; }

	public ResolvedInstallment(
		Guid paymentTermInstallmentId,
		int sequence,
		string name,
		DateTime coverageStartDate,
		DateTime coverageEndDate,
		DateTime demandDate,
		DateTime dueDate,
		decimal allocationPercentage,
		decimal? calculatedAmount,
		bool? allowPartialOverride,
		int? gracePeriodDaysOverride)
	{
		PaymentTermInstallmentId = paymentTermInstallmentId;
		Sequence = sequence;
		Name = name;
		CoverageStartDate = coverageStartDate;
		CoverageEndDate = coverageEndDate;
		DemandDate = demandDate;
		DueDate = dueDate;
		AllocationPercentage = allocationPercentage;
		CalculatedAmount = calculatedAmount;
		AllowPartialOverride = allowPartialOverride;
		GracePeriodDaysOverride = gracePeriodDaysOverride;
	}
}
