using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
	private async Task<List<FeeCollectionRawRow>> BuildRawRowsAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		EnsureSummaryContextIsValid(input);

		var now          = clock.Now;
		var currentMonth = now.Month - 1;
		var currentYear  = now.Year;

		var studentQ = await BuildFilteredStudentQueryAsync(input, cancellationToken);
		var students = await AsyncExecuter.ToListAsync(
			studentQ.Select(s => new
			{
				s.Id, s.FirstName, s.MiddleName, s.LastName,
				s.GrNo, s.RollNo, s.StandardId, s.DivisionId, s.Standard, s.Division, s.FeeStructureId
			}),
			cancellationToken);

		if (students.Count == 0)
			return [];

		var studentIds = students.Select(s => s.Id).ToList();
		var dueFeeQ = await dueFeeRepository.GetQueryableAsync();
		var feeHeadQ = await feeHeadRepository.GetQueryableAsync();
		var academicYearFeeHeadIds = feeHeadQ
			.Where(fh => fh.AcademicYearId == input.AcademicYearId)
			.Select(fh => fh.Id);
		dueFeeQ = dueFeeQ.Where(d => academicYearFeeHeadIds.Contains(d.FeeHeadId));

		var (feeAggs, overdueAggs) = await GetStudentFeeAggregatesAsync(
			dueFeeQ, studentIds, now.Date, currentYear, currentMonth, cancellationToken);

		var chargeAggDict = await BuildChargeAggregatesByStudentAsync(
			studentIds, input.AcademicYearId, cancellationToken);

		var concessionRequestQ = await concessionRequestRepository.GetQueryableAsync();
		var concessionAggs = await AsyncExecuter.ToListAsync(
			concessionRequestQ
				.Where(cr => studentIds.Contains(cr.StudentId)
					&& cr.AcademicYearId == input.AcademicYearId
					&& (cr.Status == ConcessionRequestStatus.Approved
						|| cr.Status == ConcessionRequestStatus.PartiallyReversed))
				.GroupBy(cr => cr.StudentId)
				.Select(g => new
				{
					StudentId = g.Key,
					TotalApprovedConcession =
						(g.SelectMany(cr => cr.ConcessionDueFeeAllocations)
							.Sum(a => (double?)a.ConcessionAmount) ?? 0d)
						- (g.SelectMany(cr => cr.ConcessionReversals)
							.Sum(r => (double?)r.Amount) ?? 0d)
				}),
			cancellationToken);

		var feeDict           = feeAggs.ToDictionary(fa => fa.StudentId);
		var overdueDict       = overdueAggs.ToDictionary(ov => ov.StudentId);
		var concessionAggDict = concessionAggs.ToDictionary(ca => ca.StudentId);

		return students.Select(s =>
		{
			feeDict.TryGetValue(s.Id, out var fa);
			overdueDict.TryGetValue(s.Id, out var ov);
			chargeAggDict.TryGetValue(s.Id, out var ca);
			concessionAggDict.TryGetValue(s.Id, out var coa);
			return new FeeCollectionRawRow
			{
				StudentId       = s.Id,
				FirstName       = s.FirstName,
				MiddleName      = s.MiddleName,
				LastName        = s.LastName,
				GrNo            = s.GrNo?.ToString() ?? string.Empty,
				RollNo          = s.RollNo?.ToString() ?? string.Empty,
				StandardName    = s.Standard?.Name ?? string.Empty,
				StandardId      = s.StandardId ?? Guid.Empty,
				DivisionName    = s.Division?.Name ?? string.Empty,
				DivisionId      = s.DivisionId ?? Guid.Empty,
				TotalFees       = fa.Total,
				PaidFees        = fa.Paid,
				OutstandingFees = fa.Outstanding,
				OverdueFees     = ov.Overdue,
				TotalCharges    = ca.TotalCharges,
				PendingCharges  = ca.PendingCharges,
				TotalConcessionAmount   = coa?.TotalApprovedConcession ?? 0d,
				IsFeeStructureAssigned  = s.FeeStructureId != null,
			};
		}).ToList();
	}

	private async Task<(
		List<(Guid StudentId, double Total, double Paid, double Outstanding)> FeeAggs,
		List<(Guid StudentId, double? Overdue)> OverdueAggs
	)> GetStudentFeeAggregatesAsync(
		IQueryable<DueFee> dueFeeQ,
		ICollection<Guid> studentIds,
		DateTime currentDate,
		int currentYear,
		int currentMonth,
		CancellationToken cancellationToken)
	{
		var feeAggs = await AsyncExecuter.ToListAsync(
			dueFeeQ
				.Where(d => studentIds.Contains(d.StudentId))
				.GroupBy(d => d.StudentId)
				.Select(g => new
				{
					StudentId   = g.Key,
					Total       = (double?)g.Sum(d => (double?)d.OriginalAmount) ?? 0d,
					Paid        = (double?)g.Sum(d => (double?)d.PaidAmount) ?? 0d,
					Outstanding = (double?)g.Sum(d =>
						d.ScheduleSource == ScheduleSource.Legacy || !d.DemandDate.HasValue || d.DemandDate.Value <= currentDate
							? (double?)d.OutstandingAmount
							: 0d) ?? 0d,
				}),
			cancellationToken);

		var overdueAggs = await AsyncExecuter.ToListAsync(
			dueFeeQ
				.Where(d => studentIds.Contains(d.StudentId)
						 && d.OutstandingAmount > 0m
						 && (d.ScheduleSource == ScheduleSource.Legacy || !d.DemandDate.HasValue || d.DemandDate.Value <= currentDate)
						 && (d.Year < currentYear
						 || (d.Year == currentYear && (int)d.Month < currentMonth)))
				.GroupBy(d => d.StudentId)
				.Select(g => new
				{
					StudentId = g.Key,
					Overdue   = (double?)g.Sum(d => d.OutstandingAmount),
				}),
			cancellationToken);

		return (
			feeAggs.Select(x    => (x.StudentId, x.Total, x.Paid, x.Outstanding)).ToList(),
			overdueAggs.Select(x => (x.StudentId, x.Overdue)).ToList()
		);
	}

	private async Task<Dictionary<Guid, StudentChargeAggregate>> BuildChargeAggregatesByStudentAsync(
		List<Guid> studentIds,
		Guid academicYearId,
		CancellationToken cancellationToken = default)
	{
		var result = studentIds.ToDictionary(id => id, _ => StudentChargeAggregate.Empty);

		if (studentIds.Count == 0)
			return result;

		var chargeQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = await AsyncExecuter.ToListAsync(
			chargeQ
				.Where(c => studentIds.Contains(c.StudentId)
						 && c.AcademicYearId == academicYearId
						 && c.Status == ChargeStatus.Approved)
				.Select(c => new { c.Id, c.StudentId, Amount = (double)c.Amount }),
			cancellationToken);

		if (approvedCharges.Count == 0)
			return result;

		var chargeIds = approvedCharges.Select(c => c.Id).Distinct().ToList();
		var metaQ     = await feeRecordMetadataRepository.GetQueryableAsync();
		var reversedTxIds = await AsyncExecuter.ToListAsync(
			metaQ
				.Where(m => m.Status == FeeCollectionStatus.Reversed)
				.Select(m => m.Id),
			cancellationToken);

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var paidPerCharge = await AsyncExecuter.ToListAsync(
			chargeCollQ
				.Where(r => studentIds.Contains(r.StudentId)
						 && chargeIds.Contains(r.ChargeId)
						 && !reversedTxIds.Contains(r.FeeRecordMetadataId))
				.GroupBy(r => r.ChargeId)
				.Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) }),
			cancellationToken);

		var paidDict = paidPerCharge.ToDictionary(x => x.ChargeId, x => x.Paid);

		foreach (var group in approvedCharges.GroupBy(c => c.StudentId))
		{
			var total   = 0d;
			var pending = 0d;
			foreach (var charge in group)
			{
				total   += charge.Amount;
				pending += Math.Max(0d, charge.Amount - paidDict.GetValueOrDefault(charge.Id, 0d));
			}
			result[group.Key] = new StudentChargeAggregate(total, pending);
		}

		return result;
	}
}
