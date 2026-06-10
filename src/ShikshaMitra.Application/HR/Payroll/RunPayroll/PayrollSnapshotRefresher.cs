using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotRefresher(
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	ISalarySlipRepository salarySlipRepository,
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollAdjustmentReader adjustmentReader,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollBasisPreviewResolver basisPreviewResolver,
	PayrollSnapshotRefreshLookupReader lookupReader,
	ILogger<PayrollSnapshotRefresher> logger)
	: ITransientDependency
{
	public async Task RefreshForEmployeeIdsAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds,
		bool preserveReviewedSnapshots)
	{
		var scopedEmployees = await scopedEmployeeResolver.ResolveScopedEmployeesByIdsAsync(
			year,
			month,
			employeeIds,
			includeUnknownStageEmployees: false);

		await RefreshAsync(internalMonthYear, year, month, academicYearId, scopedEmployees, preserveReviewedSnapshots);
	}
	public async Task RefreshAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		List<Employee> scopedEmployees,
		bool preserveReviewedSnapshots)
	{
		if (scopedEmployees.Count == 0)
		{
			return;
		}

		var calendar = await payrollCalendarResolver.ResolveAsync(year, month, academicYearId);
		var employeeIds = scopedEmployees.Select(e => e.Id).ToList();
		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var previousMonthNetPayByEmployee = (await salarySlipRepository.GetByMonthAsync(PayrollRunRules.GetPreviousMonth(internalMonthYear)))
			.Where(x => x.Status == SalarySlipStatus.Finalized)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(x => x.ProcessedDate).First().NetSalary);
		var reviewByEmployee = await lookupReader.GetReviewsByEmployeeAsync(internalMonthYear);
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceByEmployee = await lookupReader.GetAttendanceRecordsAsync(employeeIds, startDate, endDate);
		var payrollDetailsByEmployee = await payrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
		var componentsByEmployee = await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var resolvedComponentsByEmployee = await componentVersionResolver.ResolveByEmployeeAsync(
			componentsByEmployee,
			internalMonthYear);
		var adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(internalMonthYear, employeeIds);

		var computedItems = new List<RunPayrollItem>();
		var payrollContextByEmployee = new Dictionary<Guid, PayrollEmployeeComputationContext>();
		var policyResolutionDate = PayrollRunRules.GetPayrollPolicyResolutionDate(year, month);
		foreach (var employee in scopedEmployees)
		{
			try
			{
				var resolvedPolicies = await hrPolicyResolver.ResolveAsync(
					employee.Id,
					policyResolutionDate,
					calendar.AcademicYearId);
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
				var payrollPolicyIssueCodes = new List<string>();
				var basis = basisPreviewResolver.Resolve(
					resolvedPolicies.PayrollPolicy,
					year,
					month,
					workingUnitsByDate,
					payrollPolicyIssueCodes);
				payrollContextByEmployee[employee.Id] = new PayrollEmployeeComputationContext(
					resolvedPolicies,
					workCalendarHolidays,
					workingUnitsByDate,
					basis,
					PayrollRunItemBuilder.BuildHolidayImpactJson(year, month, workCalendarHolidays, holidayOverrides),
					payrollPolicyIssueCodes);
			}
			catch (BusinessException ex) when (PayrollRunRules.IsPayrollPolicySetupException(ex))
			{
				logger.LogWarning(
					ex,
					"Payroll preview setup issue for employee {EmployeeId} in {Month}. ErrorCode={ErrorCode}",
					employee.Id,
					internalMonthYear,
					ex.Code);
				computedItems.Add(PayrollRunItemBuilder.BuildPayrollSetupIssueItem(
					employee,
					attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
					payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
					GetResolvedComponents(resolvedComponentsByEmployee, employee.Id, year, month),
					adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
					year,
					month,
					finalizedEmployeeIds.Contains(employee.Id),
					reviewByEmployee.GetValueOrDefault(employee.Id),
					previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
					leaveUnitsByDate: null,
					new[] { IssueCodes.PayrollPolicyConfiguration },
					PayrollRunRules.GetEffectiveEmploymentStartDate(employee)));
			}
		}

		var leaveUnitsByEmployee = await payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			employeeIds,
			year,
			month,
			payrollContextByEmployee.ToDictionary(
				x => x.Key,
				x => (IReadOnlyDictionary<DateOnly, decimal>)x.Value.WorkingUnitsByDate));

		foreach (var employee in scopedEmployees)
		{
			if (!payrollContextByEmployee.TryGetValue(employee.Id, out var payrollContext))
			{
				continue;
			}

			computedItems.Add(PayrollRunItemBuilder.BuildPayrollItem(
				employee,
				attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
				payrollDetailsByEmployee.GetValueOrDefault(employee.Id),
				GetResolvedComponents(resolvedComponentsByEmployee, employee.Id, year, month),
				adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
				payrollContext.Basis,
				year,
				month,
				payrollContext.WorkCalendarHolidays,
				leaveUnitsByEmployee.GetValueOrDefault(employee.Id),
				payrollContext.ResolvedPolicies,
				payrollContext.HolidayImpactJson,
				finalizedEmployeeIds.Contains(employee.Id),
				reviewByEmployee.GetValueOrDefault(employee.Id),
				previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
				payrollContext.PayrollPolicyIssueCodes,
				PayrollRunRules.GetEffectiveEmploymentStartDate(employee)
			));
		}

		await snapshotStore.UpsertAsync(internalMonthYear, computedItems, preserveReviewedSnapshots);
	}

	private static List<EmployeePayrollComponent> GetResolvedComponents(
		ILookup<Guid, EmployeePayrollComponent> resolvedComponentsByEmployee,
		Guid employeeId,
		int year,
		int month) =>
		PayrollComponentMonthScope.FilterForMonth(resolvedComponentsByEmployee[employeeId], year, month);

	private sealed record PayrollEmployeeComputationContext(
		HrResolvedPolicies ResolvedPolicies,
		IReadOnlyList<ShikshaMitra.HR.Calendar.Holidays.Holiday> WorkCalendarHolidays,
		Dictionary<DateOnly, decimal> WorkingUnitsByDate,
		PayrollBasisDaysResult Basis,
		string HolidayImpactJson,
		IReadOnlyList<string> PayrollPolicyIssueCodes);
}
