using ShikshaMitra.Domain.Finance.Fees.Settings;
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
	IRepository<Charge, Guid> chargeRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
	IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
	IRepository<FeeRefundRequest, Guid> feeRefundRepository,
	IConcessionRequestRepository concessionRequestRepository,
	FinanceScopeAccessService financeScopeAccessService,
	FinanceReferenceCacheService referenceCache,
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
		var feeStructureAssignment = await studentFeeStructureRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);

		var ledger = new List<FeeLedgerMonthDto>();
		var feeSummary = new FeeSummaryDto();

		if (feeStructureAssignment != null)
		{
			var dueFeeQ = await dueFeeRepository.GetQueryableAsync();
			var dueFees = dueFeeQ.Where(d => d.StudentId == studentId)
				.OrderBy(d => d.Year).ThenBy(d => d.Month).ToList();

			var feeHeadIds = dueFees.Select(d => d.FeeHeadId).Distinct().ToList();
			var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
			var feeHeadNameMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);
			var reconByDueFee = await GetInReconciliationByDueFeeAsync(studentId);

			var grouped = dueFees.GroupBy(d => (d.Year, d.Month))
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

			var concessions = await concessionRequestRepository.GetListAsync(c => c.StudentId == studentId
				&& c.Status == ConcessionRequestStatus.Approved);

			var refundByDueFee = await GetRefundsByDueFeeAsync(studentId);

			foreach (var group in grouped)
			{
				var lines = group.Select(d => new FeeLedgerLineDto
				{
					DueFeeId = d.Id, FeeHeadId = d.FeeHeadId,
					FeeHeadName = feeHeadNameMap.GetValueOrDefault(d.FeeHeadId, string.Empty),
					Total = d.OriginalAmount, Paid = d.PaidAmount, Due = d.OutstandingAmount,
					//ConcessionAmount = concessions.Where(c => c.DueFeeId == d.Id).Sum(c => c.Amount),
					ReconciliationAmount = reconByDueFee.GetValueOrDefault(d.Id, 0d),
					RefundAmount = refundByDueFee.GetValueOrDefault(d.Id, (Amount: 0d, Description: string.Empty)).Amount,
					RefundDescription = refundByDueFee.GetValueOrDefault(d.Id, (Amount: 0d, Description: string.Empty)).Description,
					Status = d.OutstandingAmount <= 0d ? FeeCollectionStatus.Paid
						: (d.PaidAmount > 0d ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending),
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
						ledgerLine.ConcessionAmount += (double)line.ConcessionAmount;
					}
				}
			}

			feeSummary = new FeeSummaryDto
			{
				TotalFee = dueFees.Sum(d => d.OriginalAmount),
				PaidFee = dueFees.Sum(d => d.PaidAmount),
				PendingFee = dueFees.Sum(d => d.OutstandingAmount),
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
		};
	}

	private async Task<Dictionary<Guid, (double Amount, string Description)>> GetRefundsByDueFeeAsync(Guid studentId)
	{
		var refunds = await feeRefundRepository.GetListAsync(r => r.StudentId == studentId && r.Status == FeeRefundStatus.Approved);
		return refunds.SelectMany(r => r.Lines.Select(l => new { l.DueFeeId, l.Amount, r.Reason }))
			.GroupBy(x => x.DueFeeId)
			.ToDictionary(g => g.Key, g => (
				Amount: g.Sum(x => x.Amount),
				Description: string.Join("; ", g.Select(x => x.Reason).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct())
			));
	}
}
