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
	IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
	IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IReadOnlyRepository<FinancePolicyVersion, Guid> financePolicyVersionRepository,
	IRepository<Charge, Guid> chargeRepository,
	IRepository<ChargeWaiverApplication, Guid> chargeWaiverRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeRefundRequest, Guid> feeRefundRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService,
	FinanceReferenceCacheService referenceCache,
	LateFeeBalanceService lateFeeBalanceService,
	CollectionPolicyResolver policyResolver,
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
		var systemLateFeeCharges = new List<ChargeDetailDto>();
		var feeSummary = new FeeSummaryDto();
		var allowOnlineCollection = true;

		if (feeStructureAssignment != null)
		{
			var dueFees = await GetDueFeesAsync(studentId, academicYearId);
			dueFees = dueFees
				.OrderBy(d => d.ScheduleDueDate ?? new DateTime(d.Year, (int) d.Month + 1, 1))
				.ThenBy(d => d.InstallmentSequence ?? 0)
				.ThenBy(d => d.Year).ThenBy(d => d.Month)
				.ToList();
			var today = clock.Now.Date;

			// The collect form exposes the complete assigned schedule so a cashier can accept
			// an advance payment against a specific future installment. DemandDate still controls
			// bill generation and late-fee timing; it is not a collection-eligibility boundary.
			var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
			var prorationVersionIds = dueFees.Where(d => d.ProrationPolicyVersionId.HasValue)
				.Select(d => d.ProrationPolicyVersionId!.Value).Distinct().ToList();
			var prorationVersions = (await financePolicyVersionRepository.GetListAsync(v =>
				prorationVersionIds.Contains(v.Id) && v.PolicyType == "proration"))
				.ToDictionary(v => v.Id);
			var reconByDueFee = await GetInReconciliationByDueFeeAsync(studentId);

			var grouped = dueFees.GroupBy(d => (d.Year, d.Month))
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

			var lateFeeBalances = await lateFeeBalanceService.GetBalancesAsync(dueFees, academicYearId, today);

			foreach (var group in grouped)
			{
				var lines = group.Select(d =>
					{
						var balance = lateFeeBalances[d.Id];
						var lateFee = (Amount: (double) balance.PendingAmount, PaidAmount: (double) balance.PaidAmount,
							WaivedAmount: (double) balance.WaivedAmount, balance.Explanation);
						if (lateFee.Amount > 0 || lateFee.PaidAmount > 0 || lateFee.WaivedAmount > 0)
						{
							var feeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty);
							var installmentLabel = d.GetDisplayInstallmentLabel();
							systemLateFeeCharges.Add(new ChargeDetailDto
							{
								ChargeId = d.Id,
								SourceType = "LateFee",
								SourceId = d.Id,
								IsSystemGenerated = true,
								Name = $"Late Fee - {feeHeadName} ({installmentLabel})",
								Amount = lateFee.Amount + lateFee.PaidAmount + lateFee.WaivedAmount,
								PaidAmount = lateFee.PaidAmount,
								PendingAmount = lateFee.Amount,
								WaivedAmount = lateFee.WaivedAmount,
								CanWaive = lateFee.Amount > 0,
								ReconciliationAmount = balance.ReconciliationAmount,
								Explanation = lateFee.Explanation,
								Status = balance.ReconciliationAmount > 0m ? FeeCollectionStatus.InReconciliation
									: lateFee.Amount <= 0
									? (lateFee.WaivedAmount > 0 ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
									: (lateFee.PaidAmount > 0 ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
							});
						}
						var collectionAmount = (double) Math.Ceiling(d.OutstandingAmount);
						var roundingAmount = Math.Max(0d, collectionAmount - (double) d.OutstandingAmount);
						return new FeeLedgerLineDto
						{
							DueFeeId = d.Id,
							FeeHeadId = d.FeeHeadId,
							FeeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty),
							InstallmentLabel = d.GetDisplayInstallmentLabel(),
							DueDate = d.ScheduleDueDate ?? d.DueDate,
							CoverageStartDate = d.CoverageStartDate,
							CoverageEndDate = d.CoverageEndDate,
							ScheduleSource = d.ScheduleSource.ToString(),
							Total = (double) d.OriginalAmount,
							ScheduledTotal = (double) d.ScheduledAmount,
							Paid = (double) d.PaidAmount,
							Due = (double) d.OutstandingAmount,
							CollectionAmount = collectionAmount,
							RoundingAmount = roundingAmount,
							ProrationPolicyId = d.ProrationPolicyId,
							ProrationPolicyVersionId = d.ProrationPolicyVersionId,
							ProrationPolicyName = d.ProrationPolicyVersionId is { } versionId
								&& prorationVersions.TryGetValue(versionId, out var version)
								&& version.PolicyId == d.ProrationPolicyId ? version.PolicyName : null,
							ProrationPolicyVersionNumber = d.ProrationPolicyVersionId is { } policyVersionId
								&& prorationVersions.TryGetValue(policyVersionId, out var policyVersion)
								&& policyVersion.PolicyId == d.ProrationPolicyId ? policyVersion.VersionNumber : null,
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
							Status = DueFeeCollectionStatusResolver.Resolve(d, today, reconByDueFee.GetValueOrDefault(d.Id, 0d)),
							LateFeeAmount = 0,
							LateFeeExplanation = string.Empty,
							LateFeeWaivedAmount = 0,
							CanWaive = false,
						};
					}).ToList();

				ledger.Add(new FeeLedgerMonthDto
				{
					Month = group.Key.Month.ToString(),
					Year = group.Key.Year,
					TotalDue = lines.Sum(l => l.CollectionAmount > 0 ? l.CollectionAmount : l.Due),
					Items = lines,
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
						ledgerLine.ConcessionAmount += (double) Math.Max(0m, line.ConcessionAmount - reversedAmount);
					}
				}
			}

			feeSummary = new FeeSummaryDto
			{
				TotalFee = (double) dueFees.Sum(d => d.OriginalAmount),
				PaidFee = (double) dueFees.Sum(d => d.PaidAmount),
				PendingFee = (double) dueFees.Sum(d => d.OutstandingAmount),
				ReconciliationAmount = reconByDueFee.Values.Sum(),
				TotalCollectionAmount = (double) dueFees.Sum(d => Math.Ceiling(d.OutstandingAmount)),
				TotalRoundingAdjustment = (double) dueFees.Sum(d => Math.Ceiling(d.OutstandingAmount) - d.OutstandingAmount),
			};
		}

		var chargesQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = chargesQ.Where(c => c.StudentId == studentId
			&& c.AcademicYearId == academicYearId && c.Status == ChargeStatus.Approved).ToList();

		var chargeDtos = new List<ChargeDetailDto>(systemLateFeeCharges);
		if (approvedCharges.Count > 0)
		{
			var chargeIds = approvedCharges.Select(c => c.Id).ToList();
			var waivedPerCharge = (await chargeWaiverRepository.GetListAsync(x => chargeIds.Contains(x.ChargeId)))
				.GroupBy(x => x.ChargeId)
				.ToDictionary(x => x.Key, x => x.Sum(w => (double) w.EffectiveAmount));
			var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
			var chargeMetaQ = await feeRecordMetadataRepository.GetQueryableAsync();
			var reversedTxIds = chargeMetaQ.Where(m => m.Status == FeeCollectionStatus.Reversed).Select(m => m.Id);
			var reconTxIds = chargeMetaQ.Where(m => m.Status == FeeCollectionStatus.InReconciliation).Select(m => m.Id);
			var reconPerCharge = chargeCollQ.Where(r => r.StudentId == studentId && reconTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Amount = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Amount);
			var paidPerCharge = chargeCollQ
				.Where(r => r.StudentId == studentId && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
				.ToDictionary(x => x.ChargeId, x => x.Paid);

			chargeDtos.AddRange(approvedCharges.Select(c =>
			{
				var net = (double) c.Amount;
				var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
				var waived = waivedPerCharge.GetValueOrDefault(c.Id, 0d);
				var pending = Math.Max(0d, net - paid - waived);
				return new ChargeDetailDto
				{
					ChargeId = c.Id,
                    IncomeCategoryId = c.IncomeCategoryId, ReceivingBankAccountId = c.ReceivingBankAccountId,
                    IncomeCategoryName = c.IncomeCategoryName, ReceivingBankLabel = c.ReceivingBankLabel,
					SourceId = c.Id,
					SourceType = "Charge",
					IsSystemGenerated = false,
					Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
					Amount = net,
					PaidAmount = paid,
					PendingAmount = pending,
					WaivedAmount = waived,
					CanWaive = pending > 0,
					ReconciliationAmount = Convert.ToDecimal(reconPerCharge.GetValueOrDefault(c.Id, 0d)),
					Status = reconPerCharge.GetValueOrDefault(c.Id, 0d) > 0d ? FeeCollectionStatus.InReconciliation
						: pending <= 0d ? (waived > 0d ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
						: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
				};
			}));
		}

		return new FeeCollectFormDto
		{
			Student = BuildStudentInfo(student),
			Summary = feeSummary,
			Ledger = ledger,
			Charges = chargeDtos,
			AllowOnlineCollection = allowOnlineCollection,
		};
	}

	private async Task<Dictionary<Guid, (double Amount, string Description)>> GetRefundsByDueFeeAsync(Guid studentId)
	{
		var refunds = await feeRefundRepository.GetListAsync(r => r.StudentId == studentId &&
			(r.Status == FeeRefundStatus.Processed ||
			 (r.Status == FeeRefundStatus.Approved && r.RefundPolicyVersionId == null &&
				r.ConcessionRequestId == null)));
		return refunds.SelectMany(r => r.Lines.Select(l => new
		{ l.DueFeeId, Amount = r.RefundPolicyVersionId == null ? l.Amount : l.ProcessedAmount - l.ReversedAmount, r.Reason }))
			.GroupBy(x => x.DueFeeId)
			.ToDictionary(g => g.Key, g => (
				Amount: Convert.ToDouble(g.Sum(x => x.Amount)),
				Description: string.Join("; ", g.Select(x => x.Reason).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct())
			));
	}

}
