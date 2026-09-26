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
		var feeStructureAssignment = await studentFeeStructureRepository.FirstOrDefaultAsync(
			x => x.StudentId == studentId && x.AcademicYearId == academicYearId);

		var dueFees = await GetDueFeesAsync(studentId, academicYearId);
		var overdueFee = CalculateOverdueFee(dueFees);
		var feeHeads = await BuildFeeHeadSummariesAsync(studentId, feeStructureAssignment, dueFees);
		var charges = await BuildChargeDetailsAsync(studentId, academicYearId);
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
		Guid studentId, StudentFeeStructure? assignment, List<DueFee> dueFees)
	{
		if (assignment is null || dueFees.Count == 0) return [];

		var detailsQ = await feeStructureDetailRepository.GetQueryableAsync();
		var feeHeadsQ = await feeHeadRepository.GetQueryableAsync();
		var dueFeeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();

		var feeHeadDefs = (from detail in detailsQ
						   join fh in feeHeadsQ on detail.FeeHeadId equals fh.Id
						   where detail.FeeStructureId == assignment.FeeStructureStandardId
								 && dueFeeHeadIds.Contains(detail.FeeHeadId)
						   select new
						   {
							   detail.FeeHeadId, FeeHeadName = fh.Name,
							   Description = fh.Description ?? string.Empty, NetAmount = detail.Amount,
						   }).ToList();

		var feeCollQ = await feeCollectionRecordRepository.GetQueryableAsync();
		var reversedTxIds = await GetReversedTransactionIdsAsync();
		var paidPerHead = feeCollQ
			.Where(r => r.StudentId == studentId && !reversedTxIds.Contains(r.FeeRecordMetadataId))
			.GroupBy(r => r.FeeHeadId)
			.Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
			.ToDictionary(x => x.FeeHeadId, x => x.Paid);

		var reconByFeeHead = await GetInReconciliationByFeeHeadAsync(studentId);

		return feeHeadDefs.Select(def =>
		{
			var paid = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
			var netAmount = (double)def.NetAmount;
			var pending = Math.Max(0d, netAmount - paid);
			var recon = reconByFeeHead.GetValueOrDefault(def.FeeHeadId, 0d);
			return new FeeHeadSummaryDto
			{
				FeeHeadId = def.FeeHeadId, FeeHeadName = def.FeeHeadName,
				Amount = netAmount, PaidAmount = paid, PendingAmount = pending,
				Description = def.Description, ReconciliationAmount = recon > 0d ? recon : null,
				Status = pending <= 0d ? FeeCollectionStatus.Paid
					: (paid > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
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
				Status = pending <= 0d ? FeeCollectionStatus.Paid
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
		var methodsByMetadata = paymentMethodQ.Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
			.GroupBy(p => p.FeeRecordMetadataId)
			.Select(g => new { MetadataId = g.Key, PaymentMethods = g.Select(p => p.PaymentMethod).ToList() })
			.ToDictionary(x => x.MetadataId, x => x.PaymentMethods);

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
		var now = clock.Now;
		var currentYear = now.Year;
		var currentMonth = now.Month - 1;
		return (double)dueFees.Where(d => d.OutstandingAmount > 0m
			&& d.IsBillGeneratedOn(now.Date)
			&& (d.Year < currentYear || (d.Year == currentYear && (int)d.Month < currentMonth)))
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
