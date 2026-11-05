using ShikshaMitra.HR.WorkShifts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollLateArrivalDeductionSnapshotInvalidationReader(
	IRepository<Employee, Guid> employeeRepository,
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	EmployeeShiftResolver shiftResolver,
	IAsyncQueryableExecuter asyncExecuter,
	ILogger<PayrollLateArrivalDeductionSnapshotInvalidationReader> logger) : ITransientDependency
{
	public async Task<bool> HasInvalidationsAsync(
		string monthYear,
		int year,
		int month,
		IReadOnlyDictionary<Guid, PayrollRunItemSnapshot> snapshotByEmployee,
		IReadOnlyDictionary<Guid, HrResolvedPolicies> policiesByEmployee)
	{
		var unlockedSnapshots = snapshotByEmployee.Values
			.Where(x => !x.IsLocked)
			.ToList();
		if (unlockedSnapshots.Count == 0)
		{
			return false;
		}

		var employeeIds = unlockedSnapshots.Select(x => x.EmployeeId).Distinct().ToList();
		var employeesById = await GetEmployeesByIdAsync(employeeIds);
		var attendanceByEmployee = await GetAttendanceByEmployeeAsync(employeeIds, year, month);
		var shiftsById = await GetShiftsByIdAsync(employeesById.Values, year, month);

		foreach (var snapshot in unlockedSnapshots)
		{
			if (!employeesById.TryGetValue(snapshot.EmployeeId, out var employee) ||
				!policiesByEmployee.TryGetValue(snapshot.EmployeeId, out var policies))
			{
				continue;
			}

			var attendanceRecords = attendanceByEmployee.GetValueOrDefault(snapshot.EmployeeId) ?? [];
			var shift = ResolveShift(employee, shiftsById);
			if (ConfigurationIssueChanged(snapshot, employee, attendanceRecords, year, month, policies.AttendancePolicy, shift))
			{
				return true;
			}

			var expectedDeduction = PayrollLateArrivalDeductionCalculator.Calculate(
				monthYear,
				year,
				month,
				employee,
				attendanceRecords,
				shift,
				policies.AttendancePolicy);
			if (LateDeductionAdjustmentChanged(snapshot, expectedDeduction))
			{
				return true;
			}
		}

		return false;
	}

	private async Task<Dictionary<Guid, Employee>> GetEmployeesByIdAsync(IReadOnlyCollection<Guid> employeeIds)
	{
		var query = await employeeRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(query.Where(x => employeeIds.Contains(x.Id))))
			.ToDictionary(x => x.Id);
	}

	private async Task<Dictionary<Guid, List<EmployeeAttendance>>> GetAttendanceByEmployeeAsync(
		IReadOnlyCollection<Guid> employeeIds,
		int year,
		int month)
	{
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var query = await attendanceRepository.GetQueryableAsync();
		return (await asyncExecuter.ToListAsync(query.Where(x =>
				employeeIds.Contains(x.EmployeeId) &&
				x.AttendanceDate >= startDate &&
				x.AttendanceDate <= endDate)))
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(x => x.Key, x => x.ToList());
	}

	private async Task<Dictionary<Guid, EmployeeWorkSchedule>> GetShiftsByIdAsync(IEnumerable<Employee> employees, int year, int month)
	{
		var from = new DateOnly(year, month, 1);
		var rows = await shiftResolver.LoadAsync(employees.Select(x => x.Id), from, from.AddMonths(1).AddDays(-1));
		return rows.GroupBy(x => x.EmployeeId).ToDictionary(x => x.Key, x => new EmployeeWorkSchedule(x.ToList()));
	}

	private bool ConfigurationIssueChanged(
		PayrollRunItemSnapshot snapshot,
		Employee employee,
		IReadOnlyCollection<EmployeeAttendance> attendanceRecords,
		int year,
		int month,
		AttendancePolicy attendancePolicy,
		EmployeeWorkSchedule? shift)
	{
		var warning = PayrollLateArrivalDeductionDiagnostics.BuildConfigurationWarning(
			employee,
			attendanceRecords,
			year,
			month,
			attendancePolicy,
			shift);
		var issueCodes = Deserialize<List<string>>(snapshot.IssueCodesJson) ?? [];
		var hasSnapshotIssue = issueCodes.Contains(IssueCodes.AttendanceSalaryDeductionConfiguration);
		var shouldHaveIssue = !string.IsNullOrWhiteSpace(warning);
		if (hasSnapshotIssue == shouldHaveIssue)
		{
			return false;
		}

		logger.LogDebug(
			"Payroll snapshot for staff {EmployeeId} in {MonthYear} is stale after late deduction setup state changed.",
			snapshot.EmployeeId,
			snapshot.MonthYear);
		return true;
	}

	private bool LateDeductionAdjustmentChanged(
		PayrollRunItemSnapshot snapshot,
		PayrollLateArrivalDeductionCalculation? expectedDeduction)
	{
		var snapshotAdjustments = (Deserialize<List<PayrollAdjustmentDto>>(snapshot.PayrollAdjustmentsJson) ?? [])
			.Where(x => x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction)
			.ToList();
		if (expectedDeduction == null)
		{
			if (snapshotAdjustments.Count == 0)
			{
				return false;
			}

			logger.LogDebug(
				"Payroll snapshot for staff {EmployeeId} in {MonthYear} is stale because late deduction is no longer expected.",
				snapshot.EmployeeId,
				snapshot.MonthYear);
			return true;
		}

		var current = snapshotAdjustments.Count == 1 ? snapshotAdjustments[0] : null;
		var hasMatchingAdjustment =
			current != null &&
			current.SourceId == expectedDeduction.SourceId &&
			Math.Round((decimal) current.Amount, 2, MidpointRounding.AwayFromZero) == expectedDeduction.Amount &&
			!current.IsEarning &&
			current.IsSystemGenerated;
		var hasMatchingDeductionComponent = (Deserialize<List<RunPayrollComponentItem>>(snapshot.DeductionComponentsJson) ?? [])
			.Any(x =>
				x.Name == PayrollLateArrivalDeductionCalculator.AdjustmentName &&
				Math.Round((decimal) x.Amount, 2, MidpointRounding.AwayFromZero) == expectedDeduction.Amount);
		if (snapshotAdjustments.Count == 1 && hasMatchingAdjustment && hasMatchingDeductionComponent)
		{
			return false;
		}

		logger.LogDebug(
			"Payroll snapshot for staff {EmployeeId} in {MonthYear} is stale after late deduction amount changed or was missing.",
			snapshot.EmployeeId,
			snapshot.MonthYear);
		return true;
	}

	private static EmployeeWorkSchedule? ResolveShift(
		Employee employee,
		IReadOnlyDictionary<Guid, EmployeeWorkSchedule> shiftsById)
	{
		return shiftsById.GetValueOrDefault(employee.Id);
	}

	private static T? Deserialize<T>(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(json);
		}
		catch (JsonException)
		{
			return default;
		}
	}
}