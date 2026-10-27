using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyImpactReferenceReader(
	IRepository<HrPolicyAssignment, Guid> assignments,
	IRepository<EmployeeLeave, Guid> requests,
	IRepository<LeaveYearEndSettlementItem, Guid> settlements,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots) : ApplicationService, ITransientDependency
{
	public async Task<LeaveImpactReferences> ReadAsync(Guid year, Guid? policyId, IReadOnlyCollection<Guid> ruleIds,
		DateTime from, DateTime to)
	{
		if (from.Date > to.Date) throw new BusinessException(HRErrorCodes.PolicyAssignmentRangeInvalid);
		var assignmentRows = !policyId.HasValue ? [] : await AsyncExecuter.ToListAsync((await assignments.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == year && x.LeavePolicyId == policyId)
			.OrderBy(x => x.Id));
		var requestRows = policyId.HasValue ? await AsyncExecuter.ToListAsync((await requests.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.LeavePolicyId == policyId &&
				x.LeavePolicy != null && x.LeavePolicy.AcademicYearId == year).OrderBy(x => x.Id)) : [];
		var ruleArray = ruleIds.ToArray();
		var settlementRows = policyId.HasValue ? await AsyncExecuter.ToListAsync((await settlements.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && ((x.SourceAcademicYearId == year && x.SourceLeavePolicyId == policyId) ||
				(x.TargetAcademicYearId == year && x.TargetLeavePolicyRuleId.HasValue && Enumerable.Contains(ruleArray, x.TargetLeavePolicyRuleId.Value))))
			.OrderBy(x => x.Id)) : [];
		// Snapshots have no leave-policy lineage. Exposure requires a real pinned request or settlement.
		var employeeIds = requestRows.Select(x => x.EmployeeId).Concat(settlementRows.Select(x => x.EmployeeId)).Distinct().ToArray();
		var payrollRows = employeeIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync((await snapshots.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(employeeIds, x.EmployeeId)).OrderBy(x => x.Id));
		var exposed = new List<PayrollRunItemSnapshot>();
		var unresolved = 0;
		foreach (var row in payrollRows)
		{
			if (!TryMonth(row.MonthYear, out var month)) { unresolved++; continue; }
			var monthEnd = month.AddMonths(1).AddDays(-1);
			if (month > to || monthEnd < from) continue;
			if (requestRows.Any(x => x.EmployeeId == row.EmployeeId && x.StartDateOnly <= DateOnly.FromDateTime(monthEnd) && x.EndDateOnly >= DateOnly.FromDateTime(month)) ||
				settlementRows.Any(x => x.EmployeeId == row.EmployeeId && x.PayrollMonth == row.MonthYear)) exposed.Add(row);
		}
		var periodMonths = exposed.Select(x => x.MonthYear).ToHashSet(StringComparer.Ordinal);
		foreach (var item in settlementRows.Where(x => x.PayrollMonth != null))
		{
			if (!TryMonth(item.PayrollMonth!, out var month)) { unresolved++; continue; }
			if (month <= to && month.AddMonths(1).AddDays(-1) >= from) periodMonths.Add(item.PayrollMonth!);
		}
		var periods = periodMonths.OrderBy(x => x, StringComparer.Ordinal).Select(month =>
		{
			var snapshotEmployees = exposed.Where(x => x.MonthYear == month).Select(x => x.EmployeeId).Distinct().Count();
			var lockedEmployees = exposed.Where(x => x.MonthYear == month && x.IsLocked).Select(x => x.EmployeeId).Distinct().Count();
			return new LeavePayrollExposurePeriodDto
			{
				Month = month, SnapshotEmployeeCount = snapshotEmployees, LockedSnapshotEmployeeCount = lockedEmployees,
				SettlementEmployeeCount = settlementRows.Where(x => x.PayrollMonth == month).Select(x => x.EmployeeId).Distinct().Count(),
				LockState = snapshotEmployees == 0 ? "no_snapshot" : lockedEmployees == 0 ? "unlocked" :
					lockedEmployees == snapshotEmployees ? "locked" : "mixed"
			};
		}).ToList();
		return new(assignmentRows, requestRows, settlementRows, exposed, periods, unresolved,
			payrollRows.Select(x => $"{x.Id}:{x.ConcurrencyStamp}:{x.MonthYear}:{x.IsLocked}").ToList());
	}

	private static bool TryMonth(string value, out DateTime month) => DateTime.TryParseExact(value, "yyyy-MM",
		System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out month);
}

public sealed record LeaveImpactReferences(List<HrPolicyAssignment> Assignments, List<EmployeeLeave> Requests,
	List<LeaveYearEndSettlementItem> Settlements, List<PayrollRunItemSnapshot> PayrollSnapshots,
	List<LeavePayrollExposurePeriodDto> PayrollPeriods, int UnresolvedPayrollRecordCount, List<string> PayrollDependencies);
