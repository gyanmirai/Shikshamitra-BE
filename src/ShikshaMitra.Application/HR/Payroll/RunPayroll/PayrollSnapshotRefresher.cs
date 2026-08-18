using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotRefresher(
	IEmployeeRepository employeeRepository,
	IEmployeePayrollDetailsRepository payrollDetailsRepository,
	IEmployeePayrollComponentRepository payrollComponentRepository,
	IRepository<ShiftManagement, Guid> shiftRepository,
	ISalarySlipRepository salarySlipRepository,
	HrPolicyResolver hrPolicyResolver,
	PayrollCalendarResolver payrollCalendarResolver,
	PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
	PayrollComponentVersionResolver componentVersionResolver,
	PayrollAdjustmentReader adjustmentReader,
	PayrollLateArrivalDeductionAdjustmentService lateArrivalDeductionAdjustmentService,
	PayrollScopedEmployeeResolver scopedEmployeeResolver,
	PayrollRunSnapshotStore snapshotStore,
	PayrollBasisPreviewResolver basisPreviewResolver,
	PayrollSnapshotRefreshLookupReader lookupReader,
	PayrollProvidentFundComponentBuilder providentFundComponentBuilder,
	PayrollEsiContextResolver esiContextResolver,
	IProfessionalTaxRepository professionalTaxRepository,
	ILogger<PayrollSnapshotRefresher> logger)
	: ITransientDependency
{
	public async Task RefreshForEmployeeIdsAsync(
		string internalMonthYear,
		int year,
		int month,
		Guid? academicYearId,
		IReadOnlyCollection<Guid> employeeIds,
		bool preserveReviewedSnapshots,
		bool enforceUserScope = true)
	{
		var scopedEmployees = enforceUserScope
			? await scopedEmployeeResolver.ResolveScopedEmployeesByIdsAsync(
				year,
				month,
				employeeIds,
				includeUnknownStageEmployees: false)
			: await ResolvePayrollEmployeesByIdsAsync(year, month, employeeIds);

		await RefreshAsync(internalMonthYear, year, month, academicYearId, scopedEmployees, preserveReviewedSnapshots);
	}

	private async Task<List<Employee>> ResolvePayrollEmployeesByIdsAsync(
		int year,
		int month,
		IReadOnlyCollection<Guid> employeeIds)
	{
		var requestedSet = employeeIds.Distinct().ToHashSet();
		if (requestedSet.Count == 0)
		{
			return [];
		}

		var monthStart = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var monthEnd = PayrollRunRules.GetPayrollMonthEndDate(year, month);
		var joinedEndExclusive = monthEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);
		var candidates = await employeeRepository.GetListAsync(employee =>
			requestedSet.Contains(employee.Id) &&
			((employee.DateOfJoining.HasValue && employee.DateOfJoining.Value <= monthEnd) ||
			 (!employee.DateOfJoining.HasValue && employee.CreationTime < joinedEndExclusive)) &&
			((!employee.EmployeeStatusType.HasValue) ||
			 employee.EmployeeStatusType == EmployeeStatusType.Active ||
			 (employee.EmployeeStatusType != EmployeeStatusType.Active &&
			  ((employee.StatusEffectiveDate.HasValue &&
			    employee.StatusEffectiveDate.Value >= monthStart) ||
			   (!employee.StatusEffectiveDate.HasValue &&
			    employee.StatusLastChangedAt.HasValue)))));

		return candidates
			.Where(employee => EmployeeLifecycleEligibility.IsEligibleForPayrollMonth(employee, monthStart, monthEnd))
			.OrderBy(employee => employee.EmployeeName)
			.ThenBy(employee => employee.EmployeeCode)
			.ToList();
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
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var previousMonthNetPayByEmployee = (await salarySlipRepository.GetByMonthAsync(PayrollRunRules.GetPreviousMonth(internalMonthYear)))
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(x => x.ProcessedDate).First().NetSalary);
		var reviewByEmployee = await lookupReader.GetReviewsByEmployeeAsync(internalMonthYear);
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceByEmployee = await lookupReader.GetAttendanceRecordsAsync(employeeIds, startDate, endDate);
		var payrollDetailsByEmployee = await payrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
		var componentsByEmployee = await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var shiftsById = await GetShiftsByIdAsync(scopedEmployees);
		var resolvedComponentsByEmployee = await componentVersionResolver.ResolveByEmployeeAsync(
			componentsByEmployee,
			internalMonthYear);
		var adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(internalMonthYear, employeeIds);
		var statutoryEffectiveDate = new DateTime(year, month, 1);
		var professionalTaxSlabs = await professionalTaxRepository.GetListAsync(x =>
			x.IsActive &&
			x.JurisdictionCode == "IN-GJ" &&
			x.EffectiveFrom <= statutoryEffectiveDate &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= statutoryEffectiveDate));
		var esiContexts = await esiContextResolver.ResolveAsync(
			year,
			month,
			employeeIds,
			resolvedComponentsByEmployee);

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
				var payrollDetails = payrollDetailsByEmployee.GetValueOrDefault(employee.Id);
				var payrollComponents = await GetPayrollComponentsAsync(
					payrollDetailsByEmployee, resolvedComponentsByEmployee, employee.Id, year, month);
				var pfRule = await providentFundComponentBuilder.ResolveRuleAsync(payrollDetails, year, month);
				computedItems.Add(PayrollRunItemBuilder.BuildPayrollSetupIssueItem(
					employee,
					attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
					payrollDetails,
					payrollComponents,
					adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
					year,
					month,
					finalizedEmployeeIds.Contains(employee.Id),
					reviewByEmployee.GetValueOrDefault(employee.Id),
					previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
					leaveUnitsByDate: null,
					new[] { IssueCodes.PayrollPolicyConfiguration },
					PayrollRunRules.GetEffectiveEmploymentStartDate(employee),
					new PayrollStatutoryRuleSet(
						pfRule,
						professionalTaxSlabs,
						IsEmploymentCessationMonth(employee, year, month),
						esiContexts.GetValueOrDefault(employee.Id))));
			}
		}

		var payrollReadyEmployeeIds = payrollContextByEmployee.Keys.ToList();
		var leaveUnitsByEmployee = await payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			payrollReadyEmployeeIds,
			year,
			month,
			payrollContextByEmployee.ToDictionary(
				x => x.Key,
				x => (IReadOnlyDictionary<DateOnly, decimal>)x.Value.WorkingUnitsByDate));
		await lateArrivalDeductionAdjustmentService.SyncAsync(
			internalMonthYear,
			year,
			month,
			scopedEmployees,
			attendanceByEmployee,
			shiftsById,
			payrollContextByEmployee.ToDictionary(x => x.Key, x => x.Value.ResolvedPolicies));
		adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(internalMonthYear, employeeIds);

		foreach (var employee in scopedEmployees)
		{
			if (!payrollContextByEmployee.TryGetValue(employee.Id, out var payrollContext))
			{
				continue;
			}

			var payrollDetails = payrollDetailsByEmployee.GetValueOrDefault(employee.Id);
			var payrollComponents = await GetPayrollComponentsAsync(
				payrollDetailsByEmployee, resolvedComponentsByEmployee, employee.Id, year, month);
			var pfRule = await providentFundComponentBuilder.ResolveRuleAsync(payrollDetails, year, month);
			computedItems.Add(PayrollRunItemBuilder.BuildPayrollItem(
				employee,
				attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
				payrollDetails,
				payrollComponents,
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
				PayrollRunRules.GetEffectiveEmploymentStartDate(employee),
				ResolveShift(employee, shiftsById),
				new PayrollStatutoryRuleSet(
					pfRule,
					professionalTaxSlabs,
					IsEmploymentCessationMonth(employee, year, month),
					esiContexts.GetValueOrDefault(employee.Id))
			));
		}

		await snapshotStore.UpsertAsync(internalMonthYear, computedItems, preserveReviewedSnapshots);
	}

	private static bool IsEmploymentCessationMonth(Employee employee, int year, int month)
	{
		if (employee.EmployeeStatusType is null or EmployeeStatusType.Active || !employee.StatusEffectiveDate.HasValue)
		{
			return false;
		}

		var monthStart = new DateOnly(year, month, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		return employee.StatusEffectiveDate.Value >= monthStart && employee.StatusEffectiveDate.Value <= monthEnd;
	}

	private static List<EmployeePayrollComponent> GetResolvedComponents(
		ILookup<Guid, EmployeePayrollComponent> resolvedComponentsByEmployee,
		Guid employeeId,
		int year,
		int month) =>
		PayrollComponentMonthScope.FilterForMonth(resolvedComponentsByEmployee[employeeId], year, month);

	private async Task<List<EmployeePayrollComponent>> GetPayrollComponentsAsync(
		IReadOnlyDictionary<Guid, EmployeePayrollDetails> payrollDetailsByEmployee,
		ILookup<Guid, EmployeePayrollComponent> resolvedComponentsByEmployee,
		Guid employeeId,
		int year,
		int month)
	{
		var components = GetResolvedComponents(resolvedComponentsByEmployee, employeeId, year, month);
		return await providentFundComponentBuilder.AddProvidentFundDeductionAsync(
			payrollDetailsByEmployee.GetValueOrDefault(employeeId),
			components,
			year,
			month);
	}

	private async Task<Dictionary<Guid, ShiftManagement>> GetShiftsByIdAsync(IEnumerable<Employee> employees)
	{
		var shiftIds = employees
			.Where(employee => employee.ShiftId.HasValue)
			.Select(employee => employee.ShiftId!.Value)
			.Distinct()
			.ToList();
		if (shiftIds.Count == 0)
		{
			return new Dictionary<Guid, ShiftManagement>();
		}

		var shifts = await shiftRepository.GetListAsync(shift => shiftIds.Contains(shift.Id));
		return shifts.ToDictionary(shift => shift.Id);
	}

	private static ShiftManagement? ResolveShift(
		Employee employee,
		IReadOnlyDictionary<Guid, ShiftManagement> shiftsById)
	{
		if (employee.Shift != null)
		{
			return employee.Shift;
		}

		return employee.ShiftId.HasValue && shiftsById.TryGetValue(employee.ShiftId.Value, out var shift)
			? shift
			: null;
	}

	private sealed record PayrollEmployeeComputationContext(
		HrResolvedPolicies ResolvedPolicies,
		IReadOnlyList<ShikshaMitra.HR.Calendar.Holidays.Holiday> WorkCalendarHolidays,
		Dictionary<DateOnly, decimal> WorkingUnitsByDate,
		PayrollBasisDaysResult Basis,
		string HolidayImpactJson,
		IReadOnlyList<string> PayrollPolicyIssueCodes);
}
