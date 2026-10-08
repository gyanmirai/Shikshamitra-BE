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
					CanWaive = balance.PendingAmount > 0m,
					Status = balance.PendingAmount <= 0m ? (balance.WaivedAmount > 0m ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
						: (balance.PaidAmount > 0m ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending)
				};
			}));
		}

		var payments = await BuildPaymentHistoryAsync(studentId, academicYearId);
		var concessions = await BuildConcessionDetailsAsync(studentId, academicYearId);
		var feeOverview = BuildFeeOverview(feeHeads, concessions, overdueFee);

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
			var total = (double)group.Sum(d => d.OriginalAmount);
			var paid = (double)group.Sum(d => d.PaidAmount);
			var pending = (double)group.Where(d => d.IsBillGeneratedOn(today)).Sum(d => d.OutstandingAmount);
			var recon = reconByFeeHead.GetValueOrDefault(group.Key, 0d);
			return new FeeHeadSummaryDto
			{
				FeeHeadId = group.Key, FeeHeadName = feeHead?.Name ?? string.Empty,
				Amount = total, PaidAmount = paid, PendingAmount = pending,
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
			.ToDictionary(x => x.Key, x => x.Sum(w => (double)w.EffectiveAmount));

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var reversedTxIds = await GetReversedTransactionIdsAsync();
		var paidPerCharge = chargeCollQ
			.Where(r => r.StudentId == studentId && !reversedTxIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.ChargeId)
			.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.ChargeId, x => x.Paid);

		return studentCharges.Select(c =>
		{
			var net = (double)c.Amount;
			var paid = paidPerCharge.GetValueOrDefault(c.Id, 0d);
			var waived = waivedPerCharge.GetValueOrDefault(c.Id, 0d);
			var pending = Math.Max(0d, net - paid - waived);
			return new FeeChargeDetailDto
			{
				ChargeId = c.Id, Name = string.IsNullOrWhiteSpace(c.ChargeName) ? c.PurposeDescription : c.ChargeName,
				Amount = net, PaidAmount = paid, PendingAmount = pending,
				WaivedAmount = waived, CanWaive = pending > 0,
				Status = pending <= 0d ? (waived > 0d ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid)
					: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
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
		List<FeeHeadSummaryDto> feeHeads, List<ConcessionDetailDto>? concessions, double overdueFee)
	{
		var totalConcession = concessions?.Sum(c => c.Amount ?? 0d) ?? 0d;
		var totalRecon = feeHeads.Sum(fh => fh.ReconciliationAmount ?? 0d);
		return new FeeOverviewDto
		{
			TotalFee = feeHeads.Sum(fh => fh.Amount),
			PaidFee = feeHeads.Sum(fh => fh.PaidAmount),
			PendingFee = feeHeads.Sum(fh => fh.PendingAmount ?? 0d),
			ConcessionAmount = totalConcession > 0d ? totalConcession : null,
			OverdueFee = overdueFee > 0d ? overdueFee : null,
			ReconciliationAmount = totalRecon > 0d ? totalRecon : null,
		};
	}
}
