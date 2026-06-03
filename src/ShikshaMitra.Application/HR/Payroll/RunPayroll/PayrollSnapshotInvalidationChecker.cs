using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotInvalidationChecker(
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	PayrollBasisPreviewResolver basisPreviewResolver,
	PayrollSnapshotAttendanceInvalidationReader attendanceInvalidationReader,
	ILogger<PayrollSnapshotInvalidationChecker> logger) : ITransientDependency
{
	public async Task<bool> HasInvalidationsAsync(
		string monthYear,
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlySet<Guid> employeeIds,
		List<PayrollRunItemSnapshot> snapshots)
	{
		if (employeeIds.Count == 0)
		{
			return false;
		}

		var snapshotByEmployee = snapshots
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(row => row.LastModificationTime ?? row.CreationTime).First());
		if (employeeIds.Any(id => !snapshotByEmployee.ContainsKey(id)))
		{
			return true;
		}

		if (await HavePolicyChangesAfterSnapshotsAsync(year, month, academicYearId, snapshotByEmployee))
		{
			return true;
		}

		return await attendanceInvalidationReader.HasAttendanceInvalidationsAsync(
			monthYear,
			year,
			month,
			employeeIds,
			snapshotByEmployee);
	}

	private async Task<bool> HavePolicyChangesAfterSnapshotsAsync(
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee)
	{
		var unlockedSnapshots = snapshotByEmployee.Values
			.Where(x => !x.IsLocked)
			.ToList();
		if (unlockedSnapshots.Count == 0)
		{
			return false;
		}

		var calendar = await payrollCalendarResolver.ResolveAsync(year, month, academicYearId);
		var effectiveDate = new DateTime(year, month, 1);
		foreach (var snapshot in unlockedSnapshots)
		{
			HrResolvedPolicies resolvedPolicies;
			try
			{
				resolvedPolicies = await hrPolicyResolver.ResolveAsync(
					snapshot.EmployeeId,
					effectiveDate,
					calendar.AcademicYearId);
			}
			catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
			{
				logger.LogWarning(
					ex,
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale because policy setup cannot be resolved. ErrorCode={ErrorCode}",
					snapshot.EmployeeId,
					snapshot.MonthYear,
					ex.Code);
				return true;
			}

			if (SnapshotPolicyMetadataChanged(snapshot, resolvedPolicies))
			{
				logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale after policy assignment change.",
					snapshot.EmployeeId,
					snapshot.MonthYear);
				return true;
			}

			if (await SnapshotHasInvalidPayrollBasisAsync(snapshot, resolvedPolicies, year, month))
			{
				logger.LogDebug(
					"Payroll snapshot for employee {EmployeeId} in {MonthYear} is stale because basis/payable metadata is invalid.",
					snapshot.EmployeeId,
					snapshot.MonthYear);
				return true;
			}
		}

		return false;
	}

	private async Task<bool> SnapshotHasInvalidPayrollBasisAsync(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies,
		int year,
		int month)
	{
		var holidayOverrides = resolvedPolicies.PayrollPolicy.GetHolidayOverrides();
		var workCalendarHolidays = await basisPreviewResolver.GetIncludedWorkCalendarHolidaysAsync(
			resolvedPolicies.PayrollWorkCalendarPolicy.Id,
			year,
			month);
		var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			year,
			month,
			workCalendarHolidays,
			resolvedPolicies.PayrollPolicy.SaturdayWorkPolicy,
			holidayOverrides,
			resolvedPolicies.PayrollWorkCalendarPolicy);
		var issueCodes = new List<string>();
		var basis = basisPreviewResolver.Resolve(
			resolvedPolicies.PayrollPolicy,
			year,
			month,
			workingUnitsByDate,
			issueCodes);

		return snapshot.BasisDays != basis.BasisDays ||
		       snapshot.TotalWorkingDays != basis.BasisDays ||
		       snapshot.HolidayImpactJson.Contains("\"paid\"", StringComparison.OrdinalIgnoreCase);
	}

	private static bool SnapshotPolicyMetadataChanged(
		PayrollRunItemSnapshot snapshot,
		HrResolvedPolicies resolvedPolicies) =>
		snapshot.PayrollPolicyId != resolvedPolicies.PayrollPolicy.Id ||
		snapshot.PayrollPolicyVersion != resolvedPolicies.PayrollPolicy.Version ||
		snapshot.AttendancePolicyId != resolvedPolicies.AttendancePolicy.Id ||
		snapshot.AttendancePolicyVersion != resolvedPolicies.AttendancePolicy.Version ||
		snapshot.WorkCalendarPolicyId != resolvedPolicies.PayrollWorkCalendarPolicy.Id ||
		snapshot.WorkCalendarPolicyVersion != resolvedPolicies.PayrollWorkCalendarPolicy.Version;
}
