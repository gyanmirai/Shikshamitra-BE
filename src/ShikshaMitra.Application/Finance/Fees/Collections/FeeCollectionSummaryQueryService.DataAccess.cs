using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
	private async Task<(List<FeeCollectionRawRow> Rows, HashSet<Guid> SingleAccountStudents, int TotalCount)>
		BuildPagedPayerRowsAsync(GetFeeCollectionSummaryInput input, CancellationToken cancellationToken)
	{
		EnsureSummaryContextIsValid(input);
		var studentQuery = await BuildFilteredStudentQueryAsync(input, cancellationToken);
		var accountQuery = await BuildFilteredFeeAccountQueryAsync(input, studentQuery.Select(s => s.Id));
		var singleAccountStudentIds = accountQuery.Where(a => a.StudentId.HasValue)
			.GroupBy(a => a.StudentId!.Value).Where(group => group.Count() == 1).Select(group => group.Key);
		var payerKeys = studentQuery.Where(s => !singleAccountStudentIds.Contains(s.Id))
			.Select(s => new { StudentId = (Guid?)s.Id, FinanceFeeAccountId = (Guid?)null,
				FirstName = (string?)s.FirstName, LastName = (string?)s.LastName })
			.Concat(accountQuery.Select(a => new { a.StudentId, FinanceFeeAccountId = (Guid?)a.Id,
				FirstName = (string?)a.ApplicantName, LastName = (string?)null }));
		var totalCount = await AsyncExecuter.CountAsync(payerKeys, cancellationToken);
		if (totalCount == 0) return ([], [], 0);
		var pageKeys = await AsyncExecuter.ToListAsync(payerKeys
			.OrderBy(key => key.LastName ?? string.Empty).ThenBy(key => key.FirstName ?? string.Empty)
			.ThenBy(key => key.StudentId).ThenBy(key => key.FinanceFeeAccountId)
			.Skip(input.SkipCount).Take(input.MaxResultCount), cancellationToken);
		var studentIds = pageKeys.Where(key => !key.FinanceFeeAccountId.HasValue && key.StudentId.HasValue)
			.Select(key => key.StudentId!.Value).ToArray();
		var accountIds = pageKeys.Where(key => key.FinanceFeeAccountId.HasValue)
			.Select(key => key.FinanceFeeAccountId!.Value).ToArray();
		var (rows, singleAccountStudents) = await BuildPayerRowsAsync(input, cancellationToken,
			studentIds, accountIds);
		var byKey = rows.ToDictionary(row => (row.StudentId, row.FinanceFeeAccountId));
		return (pageKeys.Select(key => byKey[(key.StudentId, key.FinanceFeeAccountId)]).ToList(),
			singleAccountStudents, totalCount);
	}

	private async Task<List<FeeCollectionRawRow>> BuildRawRowsAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken = default)
	{
		var (rows, singleAccountStudents) = await BuildPayerRowsAsync(input, cancellationToken);
		await PopulateBalancesAsync(input, rows, singleAccountStudents, cancellationToken);
		return rows;
	}

	private async Task<(List<FeeCollectionRawRow> Rows, HashSet<Guid> SingleAccountStudents)> BuildPayerRowsAsync(
		GetFeeCollectionSummaryInput input,
		CancellationToken cancellationToken,
		Guid[]? pageStudentIds = null,
		Guid[]? pageAccountIds = null)
	{
		EnsureSummaryContextIsValid(input);
		var studentQuery = await BuildFilteredStudentQueryAsync(input, cancellationToken);
		var allAccountQuery = await BuildFilteredFeeAccountQueryAsync(input, studentQuery.Select(s => s.Id));
		var accountQuery = allAccountQuery;
		if (pageAccountIds is not null)
			accountQuery = accountQuery.Where(a => Enumerable.Contains(pageAccountIds, a.Id));
		var accounts = await AsyncExecuter.ToListAsync(accountQuery, cancellationToken);
		if (pageStudentIds is not null)
		{
			var linkedIds = accounts.Where(a => a.StudentId.HasValue).Select(a => a.StudentId!.Value).ToArray();
			studentQuery = studentQuery.Where(s => Enumerable.Contains(pageStudentIds, s.Id) || Enumerable.Contains(linkedIds, s.Id));
		}
		var students = await AsyncExecuter.ToListAsync(studentQuery.Select(s => new
		{
			s.Id, s.FirstName, s.MiddleName, s.LastName, s.GrNo, s.RollNo,
			s.StandardId, s.DivisionId, s.Standard, s.Division, s.FeeStructureId,
			s.AcademicYearId
		}), cancellationToken);
		if (students.Count == 0 && accounts.Count == 0) return ([], []);
		var studentMap = students.ToDictionary(s => s.Id);
		var historicalIds = students.Where(s => s.AcademicYearId != input.AcademicYearId)
			.Select(s => s.Id).ToArray();
		var historicalPlacements = await AsyncExecuter.ToListAsync(
			(await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentEnrollment, Guid>>()
				.GetQueryableAsync()).Where(enrollment => enrollment.AcademicYearId == input.AcademicYearId &&
					Enumerable.Contains(historicalIds, enrollment.StudentId))
				.Select(enrollment => new { enrollment.StudentId, enrollment.StandardId,
					enrollment.DivisionId, enrollment.RollNumber }), cancellationToken);
		var placementMap = historicalPlacements.GroupBy(p => p.StudentId)
			.ToDictionary(group => group.Key, group => group.First());
		var assignedHistoricalIds = await AsyncExecuter.ToListAsync(
			(await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.GetQueryableAsync()).Where(assignment => assignment.AcademicYearId == input.AcademicYearId &&
					Enumerable.Contains(historicalIds, assignment.StudentId)).Select(assignment => assignment.StudentId),
			cancellationToken);
		var assignedHistoricalSet = assignedHistoricalIds.ToHashSet();
		var standardIds = accounts.Select(a => a.StandardId)
			.Concat(historicalPlacements.Where(p => p.StandardId.HasValue).Select(p => p.StandardId!.Value))
			.Distinct().ToList();
		var divisionIds = accounts.Where(a => a.DivisionId.HasValue).Select(a => a.DivisionId!.Value)
			.Concat(historicalPlacements.Where(p => p.DivisionId.HasValue).Select(p => p.DivisionId!.Value))
			.Distinct().ToList();
		var standardNames = (await standardRepository.GetListAsync(s => standardIds.Contains(s.Id), cancellationToken: cancellationToken))
			.ToDictionary(s => s.Id, s => s.Name);
		var divisionNames = (await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id), cancellationToken: cancellationToken))
			.ToDictionary(d => d.Id, d => d.Name);
		var linkedStudentIds = accounts.Where(a => a.StudentId.HasValue).Select(a => a.StudentId!.Value).Distinct().ToArray();
		var accountCounts = pageAccountIds is null ? accounts.Select(a => a.StudentId).ToList()
			: await AsyncExecuter.ToListAsync(allAccountQuery
				.Where(a => a.StudentId.HasValue && Enumerable.Contains(linkedStudentIds, a.StudentId.Value))
				.Select(a => a.StudentId), cancellationToken);
		var singleAccountStudents = accountCounts.Where(id => id.HasValue).GroupBy(id => id!.Value)
			.Where(g => g.Count() == 1).Select(g => g.Key).ToHashSet();
		var rows = students.Where(s => (pageStudentIds is null || pageStudentIds.Contains(s.Id))
			&& !singleAccountStudents.Contains(s.Id)).Select(s => new FeeCollectionRawRow
		{
			StudentId = s.Id, FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName,
			GrNo = s.GrNo?.ToString() ?? string.Empty,
			RollNo = placementMap.TryGetValue(s.Id, out var placement) ? placement.RollNumber?.ToString() ?? string.Empty : s.RollNo?.ToString() ?? string.Empty,
			StandardId = placementMap.TryGetValue(s.Id, out var placed) ? placed.StandardId ?? Guid.Empty : s.StandardId ?? Guid.Empty,
			StandardName = placementMap.TryGetValue(s.Id, out var named) && named.StandardId.HasValue
				? standardNames.GetValueOrDefault(named.StandardId.Value) ?? string.Empty : s.Standard?.Name ?? string.Empty,
			DivisionId = placementMap.TryGetValue(s.Id, out var divided) ? divided.DivisionId ?? Guid.Empty : s.DivisionId ?? Guid.Empty,
			DivisionName = placementMap.TryGetValue(s.Id, out var division) && division.DivisionId.HasValue
				? divisionNames.GetValueOrDefault(division.DivisionId.Value) ?? string.Empty : s.Division?.Name ?? string.Empty,
			IsFeeStructureAssigned = s.AcademicYearId == input.AcademicYearId ? s.FeeStructureId.HasValue
				: assignedHistoricalSet.Contains(s.Id),
			TotalFees = 0d, PaidFees = 0d, OutstandingFees = 0d, OverdueFees = 0d,
			TotalCharges = 0d, PaidCharges = 0m, PendingCharges = 0d
		}).ToList();
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
				TotalCharges = 0d, PaidCharges = 0m, PendingCharges = 0d
			};
			rows.Add(row);
		}
		return (rows, singleAccountStudents);
	}

	private async Task PopulateBalancesAsync(
		GetFeeCollectionSummaryInput input,
		List<FeeCollectionRawRow> rows,
		HashSet<Guid> singleAccountStudents,
		CancellationToken cancellationToken)
	{
		if (rows.Count == 0) return;
		var currentDate = clock.Now.Date;
		var studentIds = rows.Where(row => row.StudentId.HasValue &&
			(!row.FinanceFeeAccountId.HasValue || singleAccountStudents.Contains(row.StudentId.Value)))
			.Select(row => row.StudentId!.Value).Distinct().ToList();
		var accountIds = rows.Where(row => row.FinanceFeeAccountId.HasValue)
			.Select(row => row.FinanceFeeAccountId!.Value).ToList();
		var rowsByPayer = rows.Where(row => !row.FinanceFeeAccountId.HasValue)
			.ToDictionary(row => FinanceReportPayerKey.From(row.StudentId, null));
		foreach (var row in rows.Where(row => row.FinanceFeeAccountId.HasValue))
		{
			rowsByPayer[FinanceReportPayerKey.From(null, row.FinanceFeeAccountId)] = row;
			// A single linked account owns the enrolled student's legacy obligations too.
			if (row.StudentId.HasValue && singleAccountStudents.Contains(row.StudentId.Value))
				rowsByPayer[FinanceReportPayerKey.From(row.StudentId, null)] = row;
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
			// Collection history stays gross; cash returned is reported independently.
			row.PaidFees += (double)due.PaidAmount;
			row.RefundAmount += due.RefundedAmount;
			// Pending includes every unpaid obligation, including installments not yet demanded.
			row.OutstandingFees += (double)due.OutstandingAmount;
			if (isOverdue(due)) row.OverdueFees += (double)due.OutstandingAmount;
			var lateBalance = lateBalances[due.Id];
			row.TotalCharges += (double)Math.Max(0m, lateBalance.Total - lateBalance.WaivedAmount);
			row.PaidCharges += lateBalance.PaidAmount;
			row.PendingCharges += (double)lateBalance.PendingAmount;
		}
		var charges = await BuildChargeAggregatesByStudentAsync(studentIds, input.AcademicYearId, cancellationToken);
		foreach (var item in charges)
		{
			if (!rowsByPayer.TryGetValue(FinanceReportPayerKey.From(item.Key, null), out var row)) continue;
			row.TotalCharges += (double)item.Value.TotalCharges;
			row.PaidCharges += item.Value.PaidCharges;
			row.PendingCharges += (double)item.Value.PendingCharges;
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
	}
	private async Task<Dictionary<Guid, StudentChargeAggregate>> BuildChargeAggregatesByStudentAsync(
		List<Guid> studentIds,
		Guid academicYearId,
		CancellationToken cancellationToken = default,
		(DateTime StartUtc, DateTime EndUtc)? duePeriod = null)
	{
		var result = studentIds.ToDictionary(id => id, _ => StudentChargeAggregate.Empty);

		if (studentIds.Count == 0)
			return result;

		var chargeQ = await chargeRepository.GetQueryableAsync();
		var approvedCharges = await AsyncExecuter.ToListAsync(
			chargeQ
				.Where(c => c.StudentId.HasValue && studentIds.Contains(c.StudentId.Value)
						 && c.AcademicYearId == academicYearId
						 && c.Status == ChargeStatus.Approved)
				.Select(c => new { c.Id, StudentId = c.StudentId!.Value, c.Amount, c.DueDate }),
			cancellationToken);

		if (duePeriod is { } period)
			approvedCharges.RemoveAll(charge =>
				charge.DueDate is not { } dueDate ||
				dueDate.UtcDateTime < period.StartUtc || dueDate.UtcDateTime >= period.EndUtc);

		if (approvedCharges.Count == 0)
			return result;

		var chargeIds = approvedCharges.Select(c => c.Id).Distinct().ToList();
		var metaQ     = await feeRecordMetadataRepository.GetQueryableAsync();

		var chargeCollQ = await chargeCollectionRecordRepository.GetQueryableAsync();
		var paymentFacts = await AsyncExecuter.ToListAsync(
			chargeCollQ
				.Where(r => r.StudentId.HasValue && studentIds.Contains(r.StudentId.Value)
						 && chargeIds.Contains(r.ChargeId)
						 && !metaQ.Any(m => m.Id == r.FeeRecordMetadataId && m.Status == FeeCollectionStatus.Reversed))
				.Select(r => new { r.ChargeId, r.Amount }),
			cancellationToken);

		// Convert each legacy allocation before adding it: the new paid-money projection is decimal.
		var paidDict = paymentFacts.GroupBy(x => x.ChargeId)
			.ToDictionary(g => g.Key, g => g.Sum(x => Convert.ToDecimal(x.Amount)));
		var waivedPerCharge = (await chargeWaiverRepository.GetListAsync(x => chargeIds.Contains(x.ChargeId)))
			.GroupBy(x => x.ChargeId)
			.ToDictionary(x => x.Key, x => x.Sum(w => w.EffectiveAmount));

		foreach (var group in approvedCharges.GroupBy(c => c.StudentId))
		{
			var total   = 0m;
			var paid = 0m;
			var pending = 0m;
			foreach (var charge in group)
			{
				var netAmount = Math.Max(0m, charge.Amount - waivedPerCharge.GetValueOrDefault(charge.Id));
				var collected = paidDict.GetValueOrDefault(charge.Id, 0m);
				total += netAmount;
				paid += collected;
				pending += Math.Max(0m, netAmount - collected);
			}
			result[group.Key] = new StudentChargeAggregate(total, paid, pending);
		}

		return result;
	}
}
