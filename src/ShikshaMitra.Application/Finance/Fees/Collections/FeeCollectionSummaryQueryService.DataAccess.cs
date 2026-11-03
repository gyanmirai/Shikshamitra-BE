using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.Finance.Reports;
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
		var currentDate = clock.Now.Date;
		var studentQuery = await BuildFilteredStudentQueryAsync(input, cancellationToken);
		var students = await AsyncExecuter.ToListAsync(studentQuery.Select(s => new
		{
			s.Id, s.FirstName, s.MiddleName, s.LastName, s.GrNo, s.RollNo,
			s.StandardId, s.DivisionId, s.Standard, s.Division, s.FeeStructureId
		}), cancellationToken);
		var accounts = await AsyncExecuter.ToListAsync(await BuildFilteredFeeAccountQueryAsync(input, studentQuery.Select(s => s.Id)), cancellationToken);
		if (students.Count == 0 && accounts.Count == 0) return [];
		var studentIds = students.Select(s => s.Id).ToList();
		var accountIds = accounts.Select(a => a.Id).ToList();
		var studentMap = students.ToDictionary(s => s.Id);
		var standardIds = accounts.Select(a => a.StandardId).Distinct().ToList();
		var divisionIds = accounts.Where(a => a.DivisionId.HasValue).Select(a => a.DivisionId!.Value).Distinct().ToList();
		var standardNames = (await standardRepository.GetListAsync(s => standardIds.Contains(s.Id), cancellationToken: cancellationToken))
			.ToDictionary(s => s.Id, s => s.Name);
		var divisionNames = (await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id), cancellationToken: cancellationToken))
			.ToDictionary(d => d.Id, d => d.Name);
		var rows = students.Select(s => new FeeCollectionRawRow
		{
			StudentId = s.Id, FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName,
			GrNo = s.GrNo?.ToString() ?? string.Empty, RollNo = s.RollNo?.ToString() ?? string.Empty,
			StandardId = s.StandardId ?? Guid.Empty, StandardName = s.Standard?.Name ?? string.Empty,
			DivisionId = s.DivisionId ?? Guid.Empty, DivisionName = s.Division?.Name ?? string.Empty,
			IsFeeStructureAssigned = s.FeeStructureId.HasValue,
			TotalFees = 0d, PaidFees = 0d, OutstandingFees = 0d, OverdueFees = 0d,
			TotalCharges = 0d, PendingCharges = 0d
		}).ToList();
		var rowsByPayer = rows.ToDictionary(r => FinanceReportPayerKey.From(r.StudentId, null));
		foreach (var account in accounts)
		{
			var linkedStudent = account.StudentId.HasValue ? studentMap.GetValueOrDefault(account.StudentId.Value) : null;
			var row = new FeeCollectionRawRow
			{
				StudentId = account.StudentId, FinanceFeeAccountId = account.Id, FirstName = account.ApplicantName,
				GrNo = linkedStudent?.GrNo?.ToString() ?? string.Empty, RollNo = linkedStudent?.RollNo?.ToString() ?? string.Empty,
				StandardId = account.StandardId, StandardName = standardNames.GetValueOrDefault(account.StandardId) ?? string.Empty,
				DivisionId = account.DivisionId ?? Guid.Empty,
				DivisionName = account.DivisionId.HasValue ? divisionNames.GetValueOrDefault(account.DivisionId.Value) ?? string.Empty : string.Empty,
				IsFeeStructureAssigned = true,
				TotalFees = 0d, PaidFees = 0d, OutstandingFees = 0d, OverdueFees = 0d,
				TotalCharges = 0d, PendingCharges = 0d
			};
			rows.Add(row);
			rowsByPayer[FinanceReportPayerKey.From(null, account.Id)] = row;
			// One linked account and one enrolled Student are one summary payer, not duplicate balances.
			if (linkedStudent != null && accounts.Count(a => a.StudentId == linkedStudent.Id) == 1)
			{
				var studentKey = FinanceReportPayerKey.From(linkedStudent.Id, null);
				if (rowsByPayer.TryGetValue(studentKey, out var oldRow)) rows.Remove(oldRow);
				rowsByPayer[studentKey] = row;
			}
		}
		var feeHeadQ = await feeHeadRepository.GetQueryableAsync();
		var yearFeeHeads = feeHeadQ.Where(f => f.AcademicYearId == input.AcademicYearId).Select(f => f.Id);
		var dues = await AsyncExecuter.ToListAsync((await dueFeeRepository.GetQueryableAsync())
			.Where(d => yearFeeHeads.Contains(d.FeeHeadId) &&
				((!d.FinanceFeeAccountId.HasValue && d.StudentId.HasValue && studentIds.Contains(d.StudentId.Value)) ||
				 (d.FinanceFeeAccountId.HasValue && accountIds.Contains(d.FinanceFeeAccountId.Value)))), cancellationToken);
		var lateBalances = await lateFeeBalanceService.GetBalancesAsync(dues, input.AcademicYearId, currentDate);
		var isOverdue = DueFeeCollectionStatusResolver.OverdueOn(currentDate).Compile();
		foreach (var due in dues)
		{
			if (!rowsByPayer.TryGetValue(FinanceReportPayerKey.From(due.StudentId, due.FinanceFeeAccountId), out var row)) continue;
			row.TotalFees += (double)due.OriginalAmount;
			row.PaidFees += (double)due.AppliedPaymentAmount;
			// Pending includes every unpaid obligation, including installments not yet demanded.
			row.OutstandingFees += (double)due.OutstandingAmount;
			if (isOverdue(due)) row.OverdueFees += (double)due.OutstandingAmount;
			row.TotalCharges += (double)lateBalances[due.Id].Total;
			row.PendingCharges += (double)lateBalances[due.Id].PendingAmount;
		}
		var charges = await BuildChargeAggregatesByStudentAsync(studentIds, input.AcademicYearId, cancellationToken);
		foreach (var item in charges)
		{
			if (!rowsByPayer.TryGetValue(FinanceReportPayerKey.From(item.Key, null), out var row)) continue;
			row.TotalCharges += item.Value.TotalCharges;
			row.PendingCharges += item.Value.PendingCharges;
		}
		var concessions = await AsyncExecuter.ToListAsync((await concessionRequestRepository.GetQueryableAsync())
			.Where(c => studentIds.Contains(c.StudentId) && c.AcademicYearId == input.AcademicYearId &&
				(c.Status == ConcessionRequestStatus.Approved || c.Status == ConcessionRequestStatus.PartiallyReversed))
			.GroupBy(c => c.StudentId).Select(g => new
			{
				StudentId = g.Key,
				Amount = (g.SelectMany(c => c.ConcessionDueFeeAllocations).Sum(a => (double?)a.ConcessionAmount) ?? 0d) -
					(g.SelectMany(c => c.ConcessionReversals).Sum(r => (double?)r.Amount) ?? 0d)
			}), cancellationToken);
		foreach (var concession in concessions)
			if (rowsByPayer.TryGetValue(FinanceReportPayerKey.From(concession.StudentId, null), out var row))
				row.TotalConcessionAmount = concession.Amount;
		return rows;
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
		var waivedPerCharge = (await chargeWaiverRepository.GetListAsync(x => chargeIds.Contains(x.ChargeId)))
			.GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(w => (double)w.EffectiveAmount));

		foreach (var group in approvedCharges.GroupBy(c => c.StudentId))
		{
			var total   = 0d;
			var pending = 0d;
			foreach (var charge in group)
			{
				total   += charge.Amount;
				pending += Math.Max(0d, charge.Amount - paidDict.GetValueOrDefault(charge.Id, 0d)
					- waivedPerCharge.GetValueOrDefault(charge.Id, 0d));
			}
			result[group.Key] = new StudentChargeAggregate(total, pending);
		}

		return result;
	}
}
