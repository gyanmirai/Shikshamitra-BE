using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class StudentFeeDetailsQueryService
{
	public async Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
		Guid studentId, Guid academicYearId, CancellationToken cancellationToken = default)
	{
		EnsureNotEmpty(academicYearId, "academicYearId");
		await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId, cancellationToken);

		var student = await GetStudentSnapshotAsync(studentId, cancellationToken);
		var dueFees = await GetDueFeesAsync(studentId, academicYearId);
		var overdueFee = CalculateOverdueFee(dueFees);
		var feeHeads = await BuildFeeHeadSummariesAsync(studentId, dueFees);
		var charges = await BuildChargeDetailsAsync(studentId, academicYearId);
		var lateBalances = await lateFeeBalanceService.GetBalancesAsync(dueFees, academicYearId, clock.Now.Date);
		var lateDues = dueFees.Where(d => lateBalances[d.Id].Total > 0m).ToList();
		if (lateDues.Count > 0)
		{
			var headIds = lateDues.Select(d => d.FeeHeadId).Distinct().ToList();
			var names = (await feeHeadRepository.GetListAsync(x => headIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
			charges ??= [];
			charges.InsertRange(0, lateDues.Select(d =>
			{
				var balance = lateBalances[d.Id];
				return new FeeChargeDetailDto
				{
					ChargeId = d.Id, Name = $"Late Fee - {names.GetValueOrDefault(d.FeeHeadId, string.Empty)} ({d.GetDisplayInstallmentLabel()})",
					Amount = (double)balance.Total, PaidAmount = (double)balance.PaidAmount,
					PendingAmount = (double)balance.PendingAmount, WaivedAmount = (double)balance.WaivedAmount,
					ReconciliationAmount = balance.ReconciliationAmount,
					CanWaive = balance.PendingAmount > 0m,
					Status = balance.ReconciliationAmount > 0m ? FeeCollectionStatus.InReconciliation
						: balance.PendingAmount <= 0m ? (balance.WaivedAmount > 0m ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
						: (balance.PaidAmount > 0m ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending)
				};
			}));
		}

		var payments = await BuildPaymentHistoryAsync(studentId, academicYearId);
		var concessions = await BuildConcessionDetailsAsync(studentId, academicYearId);
		var feeOverview = BuildFeeOverview(feeHeads, overdueFee);

		return new StudentFeeDetailsDto
		{
			Student = BuildStudentSummary(student),
			FeeOverview = feeOverview, FeeHeads = feeHeads,
			Charges = charges, Payments = payments, Concessions = concessions,
		};
	}

	private async Task<List<DueFee>> GetDueFeesAsync(Guid studentId, Guid academicYearId)
	{
		var dueFeesQ = await dueFeeRepository.GetQueryableAsync();
		var feeHeadsQ = await feeHeadRepository.GetQueryableAsync();
		var academicYearFeeHeadIds = feeHeadsQ
			.Where(fh => fh.AcademicYearId == academicYearId)
			.Select(fh => fh.Id);
		return dueFeesQ
			.Where(d => d.StudentId == studentId && academicYearFeeHeadIds.Contains(d.FeeHeadId))
			.ToList();
	}

	private async Task<IQueryable<Guid>> GetReversedTransactionIdsAsync()
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		return metaQ.Where(m => m.Status == FeeCollectionStatus.Reversed).Select(m => m.Id);
	}

	private async Task<List<FeeHeadSummaryDto>> BuildFeeHeadSummariesAsync(
		Guid studentId, List<DueFee> dueFees)
	{
		if (dueFees.Count == 0) return [];

		var dueFeeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
		var feeHeads = (await feeHeadRepository.GetListAsync(fh => dueFeeHeadIds.Contains(fh.Id)))
			.ToDictionary(fh => fh.Id);
		var reconByFeeHead = await GetInReconciliationByFeeHeadAsync(studentId);
		var today = clock.Now.Date;
		var isOverdue = DueFeeCollectionStatusResolver.OverdueOn(today).Compile();

		return dueFees.GroupBy(d => d.FeeHeadId).Select(group =>
		{
			var feeHead = feeHeads.GetValueOrDefault(group.Key);
			var principal = FeePrincipalOverview.Calculate(group, clock.Now, (decimal)reconByFeeHead.GetValueOrDefault(group.Key, 0d));
			var total = (double)principal.TotalFee;
			var paid = (double)principal.PaidFee;
			var pending = (double)principal.PendingFee;
			var recon = reconByFeeHead.GetValueOrDefault(group.Key, 0d);
			return new FeeHeadSummaryDto
			{
				FeeHeadId = group.Key, FeeHeadName = feeHead?.Name ?? string.Empty,
				Amount = total, PaidAmount = paid, PendingAmount = pending,
				NetFee = principal.NetFee,
				ConcessionAmount = principal.ConcessionAmount,
				GrossPaidAmount = principal.GrossPaidAmount,
				RefundedAmount = principal.RefundedAmount,
				OverpaymentAmount = principal.OverpaymentAmount,
				AdjustmentCreditAmount = principal.AdjustmentCreditAmount,
				Description = feeHead?.Description ?? string.Empty,
				ReconciliationAmount = recon > 0d ? recon : null,
				Status = group.Any(isOverdue) ? FeeCollectionStatus.Overdue
					: pending > 0d
					? (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending)
					: recon > 0d ? FeeCollectionStatus.InReconciliation
					: group.Any(d => d.OutstandingAmount > 0m) ? FeeCollectionStatus.Pending : FeeCollectionStatus.Paid,
			};
		}).ToList();
	}

	private async Task<List<FeeChargeDetailDto>?> BuildChargeDetailsAsync(Guid studentId, Guid academicYearId)
	{
		var chargesQ = await chargeRepository.GetQueryableAsync();
		var studentCharges = chargesQ.Where(c => c.StudentId == studentId
			&& c.AcademicYearId == academicYearId && c.Status == ChargeStatus.Approved).ToList();

		if (studentCharges.Count == 0) return null;
		var chargeIds = studentCharges.Select(c => c.Id).ToList();
		var waivedPerCharge = (await chargeWaiverRepository.GetListAsync(x => chargeIds.Contains(x.ChargeId)))
			.GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(w => w.EffectiveAmount));

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var metadataQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var activeTxIds = metadataQ.Where(x => x.StudentId == studentId && x.AcademicYearId == academicYearId && x.Status != FeeCollectionStatus.Reversed).Select(x => x.Id);
		var pendingTxIds = (await AsyncExecuter.ToListAsync(metadataQ.Where(x => x.StudentId == studentId && x.AcademicYearId == academicYearId && x.Status == FeeCollectionStatus.InReconciliation).Select(x => x.Id))).ToHashSet();
		var allocations = await AsyncExecuter.ToListAsync(chargeCollQ.Where(r => r.StudentId == studentId && chargeIds.Contains(r.ChargeId) && activeTxIds.Contains(r.FeeRecordMetadataId)));
		var paidPerCharge = allocations.GroupBy(x => x.ChargeId).ToDictionary(x => x.Key, x => x.Sum(r => (decimal)r.Amount));
		var reconPerCharge = allocations.Where(x => pendingTxIds.Contains(x.FeeRecordMetadataId)).GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(r => (decimal)r.Amount));

		return studentCharges.Select(c =>
		{
			var net = c.Amount;
			var paid = paidPerCharge.GetValueOrDefault(c.Id, 0m);
			var waived = waivedPerCharge.GetValueOrDefault(c.Id, 0m);
			var pending = Math.Max(0m, net - paid - waived);
			var reconciliation = reconPerCharge.GetValueOrDefault(c.Id, 0m);
			return new FeeChargeDetailDto
			{
				ChargeId = c.Id, Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
				Amount = (double)net, PaidAmount = (double)paid, PendingAmount = (double)pending,
				WaivedAmount = (double)waived, ReconciliationAmount = reconciliation, CanWaive = pending > 0m,
				Status = reconciliation > 0m ? FeeCollectionStatus.InReconciliation
					: pending <= 0m ? (waived > 0m ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
					: (paid > 0m ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
			};
		}).ToList();
	}

	private async Task<List<FeePaymentHistoryDto>?> BuildPaymentHistoryAsync(Guid studentId, Guid academicYearId)
	{
		var metaQ = await feeRecordMetadataRepository.GetQueryableAsync();
		var metadataForStudent = metaQ.Where(m => m.StudentId == studentId && m.AcademicYearId == academicYearId)
			.OrderByDescending(m => m.CreationTime).ToList();

		if (metadataForStudent.Count == 0) return null;

		var paymentMethodQ = await paymentMethodRecordRepository.GetQueryableAsync();
		var metadataIds = metadataForStudent.Select(m => m.Id).ToList();
		var receipts = await paymentReceiptRepository.GetListAsync(r =>
			r.StudentId == studentId && r.AcademicYearId == academicYearId
			&& metadataIds.Contains(r.FeeCollectionTransactionId));
		var receiptsByMetadata = receipts.GroupBy(r => r.FeeCollectionTransactionId)
			.ToDictionary(g => g.Key, g => g
				// A verified provisional document is replaced by its linked official receipt.
				.Where(r => !r.LinkedFinalReceiptId.HasValue)
				.OrderBy(r => r.ReceiptNumber)
				.Select(r => new FeePaymentHistoryReceiptDto
				{
					ReceiptId = r.Id, ReceiptNumber = r.ReceiptNumber,
					Scope = r.ReceiptScope.ToString(), HasPdf = r.PdfBlobName != null,
				}).ToList());
		// Keep the database projection flat. A nested collection inside GroupBy cannot be
		// translated reliably by the production EF provider when a student has payments.
		var methodRows = await AsyncExecuter.ToListAsync(paymentMethodQ
			.Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
			.Select(p => new { p.FeeRecordMetadataId, p.PaymentMethod }));
		var methodsByMetadata = methodRows.GroupBy(p => p.FeeRecordMetadataId)
			.ToDictionary(g => g.Key, g => g.Select(p => p.PaymentMethod).ToList());

		return metadataForStudent.Select(m => new FeePaymentHistoryDto
		{
			PaymentId = m.Id, ReceiptNumber = m.ReceiptNumber,
			Date = DateOnly.FromDateTime(m.CreationTime.Date),
			Amount = m.TotalPaidFee,
			PaymentMethods = methodsByMetadata.TryGetValue(m.Id, out var methods) ? methods : [],
			ReceiptUrl = null, Status = m.Status,
			Receipts = receiptsByMetadata.GetValueOrDefault(m.Id, []),
		}).ToList();
	}

	private async Task<List<ConcessionDetailDto>?> BuildConcessionDetailsAsync(Guid studentId, Guid academicYearId)
	{
		var approvedConcessions = await concessionRequestRepository.GetListAsync(
			r => r.StudentId == studentId
				&& r.AcademicYearId == academicYearId
				&& (r.Status == ConcessionRequestStatus.Approved
					|| r.Status == ConcessionRequestStatus.PartiallyReversed));

		if (approvedConcessions.Count == 0) return null;

		var typeIds = approvedConcessions.Select(r => r.ConcessionTypeId).Distinct();
		var typeMap = await referenceCache.GetConcessionTypeNameMapAsync(typeIds);

		return approvedConcessions.Select(r => new ConcessionDetailDto
		{
			ConcessionRequestConcessionTypeId = r.Id,
			ConcessionTypeName = typeMap.GetValueOrDefault(r.ConcessionTypeId, string.Empty),
			Amount = (double)r.GetNetApprovedConcession() is > 0d and var total ? total : null,
		}).ToList();
	}

	private double CalculateOverdueFee(List<DueFee> dueFees)
	{
		return (double)dueFees.Where(DueFeeCollectionStatusResolver.OverdueOn(clock.Now).Compile())
			.Sum(d => d.OutstandingAmount);
	}

	private static FeeOverviewDto BuildFeeOverview(
		List<FeeHeadSummaryDto> feeHeads, double overdueFee)
	{
		var totalConcession = (double)feeHeads.Sum(fh => fh.ConcessionAmount);
		var totalRecon = feeHeads.Sum(fh => fh.ReconciliationAmount ?? 0d);
		return new FeeOverviewDto
		{
			TotalFee = feeHeads.Sum(fh => fh.Amount),
			NetFee = feeHeads.Sum(fh => fh.NetFee),
			GrossPaidAmount = feeHeads.Sum(fh => fh.GrossPaidAmount),
			RefundedAmount = feeHeads.Sum(fh => fh.RefundedAmount),
			OverpaymentAmount = feeHeads.Sum(fh => fh.OverpaymentAmount),
			AdjustmentCreditAmount = feeHeads.Sum(fh => fh.AdjustmentCreditAmount),
			PaidFee = feeHeads.Sum(fh => fh.PaidAmount),
			PendingFee = feeHeads.Sum(fh => fh.PendingAmount ?? 0d),
			ConcessionAmount = totalConcession > 0d ? totalConcession : null,
			OverdueFee = overdueFee > 0d ? overdueFee : null,
			ReconciliationAmount = totalRecon > 0d ? totalRecon : null,
		};
	}
}
