using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveBalanceUnitService(
	IEmployeeLeaveRepository employeeLeaveRepository,
	LeaveWorkCalendarUnitService leaveWorkCalendarUnitService) : DomainService
{
	private List<LeaveBalanceUnitRow>? batchRows;
	public async Task<IDisposable> BeginReadBatchAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		var ids = employeeIds.ToArray();
		var query = (await employeeLeaveRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(ids, x.EmployeeId) && (x.Status == EmployeeLeaveStatusType.Approved || x.Status == EmployeeLeaveStatusType.Pending));
		if (await AsyncExecuter.CountAsync(query) > 500000) throw new Volo.Abp.BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		var previous = batchRows;
		batchRows = await AsyncExecuter.ToListAsync(query.Select(x => new LeaveBalanceUnitRow(x.EmployeeId, x.LeavePolicyId, x.LeavePolicyRuleId, x.PaidType, x.Status, x.ApprovedLeaveUnits, x.StartDate, x.EndDate, x.HalfDay, x.LeavePolicy != null ? x.LeavePolicy.AcademicYearId : null)));
		return new Volo.Abp.DisposeAction(() => batchRows = previous);
	}
	public async Task<decimal> GetApprovedUsedByPolicyAndPaidTypeAsync(
		Guid employeeId,
		Guid leavePolicyId,
		LeavePaidType paidType,
		CancellationToken cancellationToken = default)
	{
		var rows = batchRows?.Where(x => x.EmployeeId == employeeId && x.LeavePolicyId == leavePolicyId && x.PaidType == paidType).ToList() ?? await employeeLeaveRepository.GetLeaveBalanceRowsByPolicyAndPaidTypeAsync(
			employeeId,
			leavePolicyId,
			[paidType],
			cancellationToken);

		return SumApprovedUnits(rows);
	}

	public async Task<IReadOnlyDictionary<Guid, LeaveRuleBalanceUnits>> GetBalanceUnitsByRuleAsync(
		Guid employeeId,
		IReadOnlyCollection<Guid> leavePolicyRuleIds,
		CancellationToken cancellationToken = default)
	{
		if (leavePolicyRuleIds.Count == 0)
		{
			return new Dictionary<Guid, LeaveRuleBalanceUnits>();
		}

		var rows = await employeeLeaveRepository.GetLeaveBalanceRowsByRuleAsync(
			employeeId,
			leavePolicyRuleIds,
			cancellationToken);
		var pendingCache = new Dictionary<LeaveUnitCacheKey, decimal>();
		var result = new Dictionary<Guid, LeaveRuleBalanceUnits>();

		foreach (var group in rows.GroupBy(x => x.LeavePolicyRuleId))
		{
			result[group.Key] = new LeaveRuleBalanceUnits(
				group.Key,
				SumApprovedUnits(group),
				await SumPendingUnitsAsync(group, pendingCache, cancellationToken));
		}

		return result;
	}

	public async Task<IReadOnlyDictionary<LeavePaidType, LeavePaidTypeBalanceUnits>> GetBalanceUnitsByPolicyAndPaidTypeAsync(
		Guid employeeId,
		Guid leavePolicyId,
		IReadOnlyCollection<LeavePaidType> paidTypes,
		CancellationToken cancellationToken = default)
	{
		if (paidTypes.Count == 0)
		{
			return new Dictionary<LeavePaidType, LeavePaidTypeBalanceUnits>();
		}

		var rows = batchRows?.Where(x => x.EmployeeId == employeeId && x.LeavePolicyId == leavePolicyId && paidTypes.Contains(x.PaidType)).ToList() ?? await employeeLeaveRepository.GetLeaveBalanceRowsByPolicyAndPaidTypeAsync(
			employeeId,
			leavePolicyId,
			paidTypes,
			cancellationToken);
		var pendingCache = new Dictionary<LeaveUnitCacheKey, decimal>();
		var result = new Dictionary<LeavePaidType, LeavePaidTypeBalanceUnits>();

		foreach (var group in rows.GroupBy(x => x.PaidType))
		{
			result[group.Key] = new LeavePaidTypeBalanceUnits(
				group.Key,
				SumApprovedUnits(group),
				await SumPendingUnitsAsync(group, pendingCache, cancellationToken));
		}

		return result;
	}

	private static decimal SumApprovedUnits(IEnumerable<LeaveBalanceUnitRow> rows) =>
		rows
			.Where(x => x.Status == EmployeeLeaveStatusType.Approved)
			.Sum(x => x.ApprovedLeaveUnits);

	private async Task<decimal> SumPendingUnitsAsync(
		IEnumerable<LeaveBalanceUnitRow> rows,
		Dictionary<LeaveUnitCacheKey, decimal> cache,
		CancellationToken cancellationToken)
	{
		var total = 0m;
		foreach (var row in rows.Where(x => x.Status == EmployeeLeaveStatusType.Pending))
		{
			total += await CalculateRequestedUnitsAsync(row, cache, cancellationToken);
		}

		return total;
	}

	private async Task<decimal> CalculateRequestedUnitsAsync(
		LeaveBalanceUnitRow row,
		Dictionary<LeaveUnitCacheKey, decimal> cache,
		CancellationToken cancellationToken)
	{
		var key = new LeaveUnitCacheKey(
			row.EmployeeId,
			row.StartDate.Date,
			row.EndDate.Date,
			row.HalfDay,
			row.AcademicYearId);
		if (cache.TryGetValue(key, out var units))
		{
			return units;
		}

		units = await leaveWorkCalendarUnitService.CalculateUnitsAsync(
			row.EmployeeId,
			row.StartDate.DateTime,
			row.EndDate.DateTime,
			row.HalfDay,
			row.AcademicYearId,
			cancellationToken);
		cache[key] = units;
		return units;
	}

	private sealed record LeaveUnitCacheKey(
		Guid EmployeeId,
		DateTime StartDate,
		DateTime EndDate,
		bool HalfDay,
		Guid? AcademicYearId);
}