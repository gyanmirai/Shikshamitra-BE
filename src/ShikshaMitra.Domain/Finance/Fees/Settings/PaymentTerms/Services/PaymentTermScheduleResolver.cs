using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
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

		var version = term.Versions.FirstOrDefault(v => v.Id == detail.PaymentTermVersionId.Value)
			?? throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("VersionId", detail.PaymentTermVersionId);

		if (version.Status != PaymentTermVersionStatus.Published &&
			version.Status != PaymentTermVersionStatus.Superseded)
		{
			throw new BusinessException(PaymentTermErrorCodes.VersionNotPublished)
				.WithData("VersionId", version.Id)
				.WithData("Status", version.Status.ToString());
		}

		var annualAmount = (decimal)detail.Amount;
		var installments = new List<ResolvedInstallment>();
		var allocated = 0m;
		var masterInstallments = version.Installments.OrderBy(i => i.Sequence).ToList();

		for (var index = 0; index < masterInstallments.Count; index++)
		{
			var masterInstallment = masterInstallments[index];
			var coverageStart = masterInstallment.CoverageStartDate;
			var coverageEnd = masterInstallment.CoverageEndDate;
			var demandDate = masterInstallment.DemandDate;
			var dueDate = masterInstallment.DueDate;
			var allocationPercentage = masterInstallment.AllocationPercentage;

			if (detail.ScheduleMode == ScheduleMode.Customized)
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

			decimal calculatedAmount;
			if (index == masterInstallments.Count - 1)
			{
				// Absorb rounding residual on the last installment.
				calculatedAmount = Math.Round(annualAmount - allocated, 2);
			}
			else
			{
				calculatedAmount = Math.Round(annualAmount * allocationPercentage / 100m, 2);
				allocated += calculatedAmount;
			}

			installments.Add(new ResolvedInstallment(
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

		return new ResolvedFeeHeadSchedule(
			detail.Id,
			detail.FeeHeadId,
			detail.FeeHead?.Name,
			annualAmount,
			detail.PaymentTermId.Value,
			term.Name,
			detail.PaymentTermVersionId.Value,
			version.VersionNumber,
			detail.ScheduleMode,
			installments,
			isValid: true);
	}

	public async Task<List<ResolvedFeeHeadSchedule>> ResolveAllAsync(FeeStructure feeStructure)
	{
		var result = new List<ResolvedFeeHeadSchedule>();
		foreach (var detail in feeStructure.FeeStructureDetails)
		{
			result.Add(await ResolveAsync(detail));
		}

		return result;
	}
}

public sealed class ResolvedFeeHeadSchedule
{
	public Guid FeeStructureDetailId { get; }
	public Guid FeeHeadId { get; }
	public string? FeeHeadName { get; }
	public decimal AnnualAmount { get; }
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
		decimal annualAmount,
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
		(decimal)detail.Amount,
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
	public int Sequence { get; }
	public string Name { get; }
	public DateTime CoverageStartDate { get; }
	public DateTime CoverageEndDate { get; }
	public DateTime DemandDate { get; }
	public DateTime DueDate { get; }
	public decimal AllocationPercentage { get; }
	public decimal CalculatedAmount { get; }
	public bool? AllowPartialOverride { get; }
	public int? GracePeriodDaysOverride { get; }

	public ResolvedInstallment(
		int sequence,
		string name,
		DateTime coverageStartDate,
		DateTime coverageEndDate,
		DateTime demandDate,
		DateTime dueDate,
		decimal allocationPercentage,
		decimal calculatedAmount,
		bool? allowPartialOverride,
		int? gracePeriodDaysOverride)
	{
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
