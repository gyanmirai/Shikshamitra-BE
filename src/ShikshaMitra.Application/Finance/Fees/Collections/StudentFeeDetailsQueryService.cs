using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Cache;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public partial class StudentFeeDetailsQueryService(
	IReadOnlyRepository<Student, Guid> studentRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
	IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
	IRepository<FeeCollectionDueFeeAllocation, Guid> dueFeeAllocationRepository,
	IRepository<LateFeeCollectionAllocation, Guid> lateFeeAllocationRepository,
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeRefundRequest, Guid> feeRefundRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService,
	FinanceReferenceCacheService referenceCache,
	LateFeeWaiverLookupService waiverLookupService,
	CollectionPolicyResolver policyResolver,
	LateFeeCalculationService lateFeeCalculationService,
	ExceptionWindowResolver exceptionWindowResolver,
	IClock clock
) : ApplicationService, ITransientDependency
{
	private static void EnsureNotEmpty(Guid value, string fieldName)
	{
		if (value != Guid.Empty) return;
		throw new AbpValidationException("Validation failed",
			[new ValidationResult($"{fieldName} is required.", new[] { fieldName })]);
	}

	public async Task<FeeCollectFormDto> GetCollectFormAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);
		var feeStructureAssignment = await studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == studentId && x.AcademicYearId == academicYearId);

		var ledger = new List<FeeLedgerMonthDto>();
		var feeSummary = new FeeSummaryDto();
		var allowOnlineCollection = true;

		if (feeStructureAssignment != null)
		{
			var dueFees = await GetDueFeesAsync(studentId, academicYearId);
			dueFees = dueFees
				.OrderBy(d => d.ScheduleDueDate ?? new DateTime(d.Year, (int)d.Month + 1, 1))
				.ThenBy(d => d.InstallmentSequence ?? 0)
				.ThenBy(d => d.Year).ThenBy(d => d.Month)
				.ToList();
			var today = clock.Now.Date;
			var generatedDueFees = dueFees.Where(d => d.IsBillGeneratedOn(today)).ToList();

			var feeHeadIds = generatedDueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
			var reconByDueFee = await GetInReconciliationByDueFeeAsync(studentId);

			var grouped = generatedDueFees.GroupBy(d => (d.Year, d.Month))
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

			var concessions = await concessionRequestRepository.GetListAsync(c => c.StudentId == studentId
				&& c.AcademicYearId == academicYearId
				&& (c.Status == ConcessionRequestStatus.Approved
					|| c.Status == ConcessionRequestStatus.PartiallyReversed));

			var refundByDueFee = await GetRefundsByDueFeeAsync(studentId);

			// ── Collection Policy resolution + late fee (Phase 3) ──────────────
			// One resolution for the whole collect-form call, not per due line — the policy
			// doesn't vary by fee head today (per-fee-head override is Phase 4a, not yet built).
			var effectiveRules = await policyResolver.ResolveEffectiveRulesForStudentAsync(
				studentId, feeStructureAssignment.FeeStructureStandardId);
			allowOnlineCollection = effectiveRules.Policy?.AllowOnlineCollection ?? true;

			var waivedByDueFee = await waiverLookupService.GetAppliedWaivedAmountsAsync(generatedDueFees.Select(d => d.Id).ToList());
			var generatedDueFeeIds = generatedDueFees.Select(d => d.Id).ToList();
			var paidLateFeeAllocations = await lateFeeAllocationRepository.GetListAsync(x => generatedDueFeeIds.Contains(x.DueFeeId));
			var lateFeeTransactionIds = paidLateFeeAllocations.Select(x => x.FeeRecordMetadataId).Distinct().ToList();
			var reversedLateFeeTransactionIds = (await feeRecordMetadataRepository.GetListAsync(x =>
				lateFeeTransactionIds.Contains(x.Id) && x.Status == FeeCollectionStatus.Reversed))
				.Select(x => x.Id).ToHashSet();
			var paidLateFeeByDueFee = paidLateFeeAllocations
				.Where(x => !reversedLateFeeTransactionIds.Contains(x.FeeRecordMetadataId))
				.GroupBy(x => x.DueFeeId)
				.ToDictionary(x => x.Key, x => x.Sum(a => (decimal)a.Amount));

			// Resolved once for the whole collect-form call, same rationale as the policy
			// resolution above — an exception window doesn't vary by fee head/due line.
			var activeWindow = await exceptionWindowResolver.ResolveActiveWindowAsync(
				academicYearId, feeStructureAssignment.FeeStructureStandardId, today);

			foreach (var group in grouped)
			{
				var lines = group.Select(d =>
				{
					var lateFee = ComputeLateFeeForLine(
						effectiveRules, d, today, waivedByDueFee, paidLateFeeByDueFee, activeWindow);
					return new FeeLedgerLineDto
					{
						DueFeeId = d.Id, FeeHeadId = d.FeeHeadId,
						FeeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty),
						InstallmentLabel = d.GetDisplayInstallmentLabel(),
						DueDate = d.ScheduleDueDate,
						CoverageStartDate = d.CoverageStartDate,
						CoverageEndDate = d.CoverageEndDate,
						ScheduleSource = d.ScheduleSource.ToString(),
						Total = (double)d.OriginalAmount,
						ScheduledTotal = (double)d.ScheduledAmount,
						Paid = (double)d.PaidAmount, Due = (double)d.OutstandingAmount,
						ProrationPolicyId = d.ProrationPolicyId,
						ProrationPolicyVersionId = d.ProrationPolicyVersionId,
						ProrationFactor = d.ProrationFactor,
						ProrationRuleType = d.ProrationRuleType?.ToString(),
						ProrationRoundingMode = d.ProrationRoundingMode?.ToString(),
						ProrationEligibleStartDate = d.ProrationEligibleStartDate,
						ProrationEligibleEndDate = d.ProrationEligibleEndDate,
						ProrationCoverageDays = d.ProrationCoverageDays,
						ProrationEligibleDays = d.ProrationEligibleDays,
						ProrationChargedDays = d.ProrationChargedDays,
						ProrationExplanation = d.ProrationExplanation,
						//ConcessionAmount = concessions.Where(c => c.DueFeeId == d.Id).Sum(c => c.Amount),
						ReconciliationAmount = reconByDueFee.GetValueOrDefault(d.Id, 0d),
						RefundAmount = refundByDueFee.GetValueOrDefault(d.Id, (Amount: 0d, Description: string.Empty)).Amount,
						RefundDescription = refundByDueFee.GetValueOrDefault(d.Id, (Amount: 0d, Description: string.Empty)).Description,
						Status = d.OutstandingAmount <= 0m ? FeeCollectionStatus.Paid
							: (d.PaidAmount > 0m ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
						LateFeeAmount = lateFee.Amount,
						LateFeeExplanation = lateFee.Explanation,
						LateFeeWaivedAmount = lateFee.WaivedAmount,
					};
				}).ToList();

				ledger.Add(new FeeLedgerMonthDto
				{
					Month = group.Key.Month.ToString(), Year = group.Key.Year,
					TotalDue = lines.Sum(l => l.Due), Items = lines,
				});
			}

			foreach (var concession in concessions)
			{
				foreach (var line in concession.ConcessionDueFeeAllocations)
				{
					var ledgerLine = ledger.SelectMany(l => l.Items)
						.FirstOrDefault(l => l.DueFeeId == line.DueFeeId);
					if (ledgerLine != null)
					{
						var reversedAmount = concession.ConcessionReversals
							.Where(r => r.ConcessionDueFeeAllocationId == line.Id)
							.Sum(r => r.Amount);
						ledgerLine.ConcessionAmount += (double)Math.Max(0m, line.ConcessionAmount - reversedAmount);
					}
				}
			}

			feeSummary = new FeeSummaryDto
			{
				TotalFee = (double)dueFees.Sum(d => d.OriginalAmount),
				PaidFee = (double)dueFees.Sum(d => d.PaidAmount),
				PendingFee = (double)generatedDueFees.Sum(d => d.OutstandingAmount),
				ReconciliationAmount = reconByDueFee.Values.Sum(),
			};
		}

		var chargesQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = chargesQ.Where(c => c.StudentId == studentId
			&& c.AcademicYearId == academicYearId && c.Status == ChargeStatus.Approved).ToList();

		var chargeDtos = new List<ChargeDetailDto>();
		if (approvedCharges.Count > 0)
		{
			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeMetaQ = await feeRecordMetadataRepository.GetQueryableAsync();
			var reversedTxIds = chargeMetaQ.Where(m => m.Status == FeeCollectionStatus.Reversed).Select(m => m.Id);
			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos = approvedCharges.Select(c =>
			{
				var net = (double)c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid);
				return new ChargeDetailDto
				{
					ChargeId = c.Id, Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
					Amount = net, PaidAmount = paid, PendingAmount = pending,
					Status = pending <= 0d ? FeeCollectionStatus.Paid
						: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}).ToList();
		}

		return new FeeCollectFormDto
		{
			Student = BuildStudentInfo(student),
			Summary = feeSummary, Ledger = ledger, Charges = chargeDtos,
			AllowOnlineCollection = allowOnlineCollection,
		};
	}

	private async Task<Dictionary<Guid, (double Amount, string Description)>> GetRefundsByDueFeeAsync(Guid studentId)
	{
		var refunds = await feeRefundRepository.GetListAsync(r => r.StudentId == studentId &&
			(r.Status == FeeRefundStatus.Processed ||
			 (r.Status == FeeRefundStatus.Approved && r.RefundPolicyVersionId == null)));
		return refunds.SelectMany(r => r.Lines.Select(l => new
			{ l.DueFeeId, Amount = r.RefundPolicyVersionId == null ? l.Amount : l.ProcessedAmount - l.ReversedAmount, r.Reason }))
			.GroupBy(x => x.DueFeeId)
			.ToDictionary(g => g.Key, g => (
				Amount: Convert.ToDouble(g.Sum(x => x.Amount)),
				Description: string.Join("; ", g.Select(x => x.Reason).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct())
			));
	}

	private (double Amount, string Explanation, double WaivedAmount) ComputeLateFeeForLine(
		EffectiveCollectionRules effectiveRules, DueFee dueFee, DateTime today, Dictionary<Guid, decimal> waivedByDueFee,
		Dictionary<Guid, decimal> paidByDueFee,
		ActiveExceptionWindow? activeWindow)
	{
		if (effectiveRules.Policy is null || dueFee.OutstandingAmount <= 0)
		{
			return (0, string.Empty, 0);
		}

		var daysLate = (today - dueFee.DueDate.Date).Days;
		var result = lateFeeCalculationService.Calculate(effectiveRules.Policy, new LateFeeCalculationInput(
			daysLate,
			(decimal)dueFee.OutstandingAmount,
			(decimal)dueFee.OriginalAmount,
			(decimal)dueFee.OutstandingAmount,
			dueFee.FeeHeadId,
			effectiveRules.IsLateFeeExempt,
			effectiveRules.CustomGracePeriodDays,
			activeWindow?.Effect,
			activeWindow?.ExtensionDays,
			activeWindow?.Label));

		var waived = waivedByDueFee.GetValueOrDefault(dueFee.Id, 0m);
		var net = Math.Max(0, result.LateFee - waived - paidByDueFee.GetValueOrDefault(dueFee.Id, 0m));

		return ((double)net, net > 0 ? result.Explanation : string.Empty, (double)waived);
	}
}
