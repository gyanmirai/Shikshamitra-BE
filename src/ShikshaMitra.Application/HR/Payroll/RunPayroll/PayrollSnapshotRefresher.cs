using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
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
	PayrollCompensationRevisionResolver compensationRevisionResolver,
	PayrollEsiContextResolver esiContextResolver,
	EsiPayrollConfigurationResolver esiConfigurationResolver,
	IProfessionalTaxRepository professionalTaxRepository,
	IRepository<PayslipCorrection, Guid> payslipCorrections,
	ILogger<PayrollSnapshotRefresher> logger, PfEligibilityResolver pfEligibilityResolver)
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
		bool preserveReviewedSnapshots,
		Func<int, Task>? reportProgress = null,
		Func<Task>? beforePersist = null)
	{
		await ComputeAsync(internalMonthYear, year, month, academicYearId, scopedEmployees,
			preserveReviewedSnapshots, reportProgress, beforePersist);
	}

	/// <summary>Reuses payroll computation without modifying the issued snapshot or system adjustments.</summary>
	internal async Task<RunPayrollItem> CalculateCorrectionAsync(Guid employeeId, string monthYear,
		Guid? academicYearId, IReadOnlyCollection<EmployeeAttendance> attendance)
	{
		var (year, month, internalMonth) = PayrollRunRules.ParseMonth(monthYear);
		if (employeeId == Guid.Empty || attendance == null ||
			attendance.Any(x => x == null || x.EmployeeId != employeeId ||
				x.AttendanceDate.Year != year || x.AttendanceDate.Month != month || !Enum.IsDefined(x.StatusType)) ||
			attendance.Select(x => x.AttendanceDate).Distinct().Count() != attendance.Count)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var scoped = await scopedEmployeeResolver.ResolveScopedEmployeesByIdsAsync(year, month, [employeeId], false);
		if (scoped.Count != 1) throw new BusinessException(HRErrorCodes.PayrollPreviewNotFound);
		if (attendance.Any(x => x.TenantId != scoped[0].TenantId)) throw new BusinessException(HRErrorCodes.PayslipConflict);
		foreach (var day in attendance) PayslipCorrectionAttendanceRules.EnsureValidPunches(day);
		var items = await ComputeAsync(internalMonth, year, month, academicYearId, scoped, false,
			correctionEmployeeId: employeeId, correctionAttendance: attendance);
		var result = items.Single();
		result.PayrollAttendanceSource = "payslip_correction";
		return result;
	}

	private async Task<List<RunPayrollItem>> ComputeAsync(
		string internalMonthYear, int year, int month, Guid? academicYearId, List<Employee> scopedEmployees,
		bool preserveReviewedSnapshots, Func<int, Task>? reportProgress = null, Func<Task>? beforePersist = null,
		Guid? correctionEmployeeId = null, IReadOnlyCollection<EmployeeAttendance>? correctionAttendance = null)
	{
		if (scopedEmployees.Count == 0)
		{
			return [];
		}

		var existingSlips = await salarySlipRepository.GetByMonthAsync(internalMonthYear);
		var finalizedEmployeeIds = existingSlips
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.Select(x => x.EmployeeId)
			.ToHashSet();
		var lockedIds = (await snapshotStore.GetByMonthAsync(internalMonthYear)).Where(x => x.IsLocked)
			.Select(x => x.EmployeeId).ToHashSet();
		var originalCount = scopedEmployees.Count;
		scopedEmployees = scopedEmployees.Where(x => x.Id == correctionEmployeeId ||
			(!finalizedEmployeeIds.Contains(x.Id) && !lockedIds.Contains(x.Id))).ToList();
		var immutableCount = originalCount - scopedEmployees.Count;
		if (scopedEmployees.Count == 0) return [];
		var calendar = await payrollCalendarResolver.ResolveAsync(year, month, academicYearId);
		var employeeIds = scopedEmployees.Select(e => e.Id).ToList();
		var previousMonthNetPayByEmployee = (await salarySlipRepository.GetByMonthAsync(PayrollRunRules.GetPreviousMonth(internalMonthYear)))
			.Where(x => x.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(g => g.Key, g => (decimal?) g.OrderByDescending(x => x.ProcessedDate).First().NetSalary);
		var reviewByEmployee = await lookupReader.GetReviewsByEmployeeAsync(internalMonthYear);
		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendanceByEmployee = await lookupReader.GetAttendanceRecordsAsync(employeeIds, startDate, endDate);
		if (correctionEmployeeId.HasValue)
			attendanceByEmployee[correctionEmployeeId.Value] = correctionAttendance!.ToList();
		// A retroactive exit can leave recorded attendance after employment eligibility ends.
		// Filter calculation inputs while retaining source records and immutable issued payroll.
		foreach (var employee in scopedEmployees)
		{
			if (attendanceByEmployee.TryGetValue(employee.Id, out var attendance))
				attendance.RemoveAll(day => !EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
					employee.EmployeeStatusType, employee.StatusEffectiveDate, employee.StatusLastChangedAt, day.AttendanceDate));
		}
		var payrollDetailsByEmployee = await payrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
		var componentsByEmployee = await payrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var shiftsById = await GetShiftsByIdAsync(scopedEmployees);
		var resolvedComponentsByEmployee = await componentVersionResolver.ResolveByEmployeeAsync(
			componentsByEmployee,
			internalMonthYear);
		var compensationByEmployee = await compensationRevisionResolver.ResolveAsync(
			employeeIds,
			year,
			month,
			resolvedComponentsByEmployee);
		var compensationComponentsByEmployee = compensationByEmployee.Values
			.SelectMany(x => x.Components)
			.ToLookup(x => x.EmployeeId);
		var adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(internalMonthYear, employeeIds);
		var statutoryEffectiveDate = new DateTime(year, month, 1);
		var professionalTaxSlabs = await professionalTaxRepository.GetListAsync(x =>
			x.IsActive &&
			x.JurisdictionCode == "IN-GJ" &&
			x.EffectiveFrom <= statutoryEffectiveDate &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= statutoryEffectiveDate));
		var esiConfiguration = await esiConfigurationResolver.ResolveAsync(new DateTime(year, month, 1));
		var esiContexts = await esiContextResolver.ResolveAsync(
			year,
			month,
			employeeIds,
			compensationComponentsByEmployee);

		var computedItems = new List<RunPayrollItem>();
		using var policyReadBatch = hrPolicyResolver.BeginReadBatch(scopedEmployees);
		using var pfReadBatch = providentFundComponentBuilder.BeginReadBatch();
		var calendarHolidays = new Dictionary<Guid, List<ShikshaMitra.HR.Calendar.Holidays.Holiday>>();
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
				var calendarId = resolvedPolicies.PayrollWorkCalendarPolicy.Id;
				if (!calendarHolidays.TryGetValue(calendarId, out var workCalendarHolidays))
				{
					workCalendarHolidays = await basisPreviewResolver.GetIncludedWorkCalendarHolidaysAsync(calendarId, year, month);
					calendarHolidays[calendarId] = workCalendarHolidays;
				}
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
					"Payroll preview setup issue for staff {EmployeeId} in {Month}. ErrorCode={ErrorCode}",
					employee.Id,
					internalMonthYear,
					ex.Code);
				var payrollDetails = payrollDetailsByEmployee.GetValueOrDefault(employee.Id);
				var payrollComponents = await GetPayrollComponentsAsync(
					compensationByEmployee, employee.Id, year, month);
				var revision = compensationByEmployee.GetValueOrDefault(employee.Id)?.Revision;
				var pfRule = await providentFundComponentBuilder.ResolveRuleAsync(
					revision?.PfRuleId,
					employee.Id,
					year,
					month);
				computedItems.Add(PayrollRunItemBuilder.BuildPayrollSetupIssueItem(
					employee,
					attendanceByEmployee.GetValueOrDefault(employee.Id) ?? new List<EmployeeAttendance>(),
					payrollDetails,
					payrollComponents,
					adjustmentsByEmployee.GetValueOrDefault(employee.Id) ?? new List<PayrollAdjustment>(),
					year,
					month,
					!correctionEmployeeId.HasValue && finalizedEmployeeIds.Contains(employee.Id),
					correctionEmployeeId.HasValue ? null : reviewByEmployee.GetValueOrDefault(employee.Id),
					previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
					leaveUnitsByDate: null,
					revision == null
						? new[] { IssueCodes.PayrollPolicyConfiguration, IssueCodes.CompensationRevisionRequired }
						: new[] { IssueCodes.PayrollPolicyConfiguration },
					PayrollRunRules.GetEffectiveEmploymentStartDate(employee),
					new PayrollStatutoryRuleSet(
						pfRule,
						professionalTaxSlabs,
						IsEmploymentCessationMonth(employee, year, month),
						esiContexts.GetValueOrDefault(employee.Id), esiConfiguration?.Id),
					revision));
			}
		}

		var payrollReadyEmployeeIds = payrollContextByEmployee.Keys.ToList();
		var leaveUnitsByEmployee = await payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			payrollReadyEmployeeIds,
			year,
			month,
			payrollContextByEmployee.ToDictionary(
				x => x.Key,
				x => (IReadOnlyDictionary<DateOnly, decimal>) x.Value.WorkingUnitsByDate));
		foreach (var employee in scopedEmployees)
		{
			if (leaveUnitsByEmployee.TryGetValue(employee.Id, out var leaveUnits))
				leaveUnitsByEmployee[employee.Id] = leaveUnits.Where(day => EmployeeLifecycleEligibility.IsAttendanceEligibleOnDate(
					employee.EmployeeStatusType, employee.StatusEffectiveDate, employee.StatusLastChangedAt, day.Key))
					.ToDictionary(day => day.Key, day => day.Value);
		}
		if (!correctionEmployeeId.HasValue)
		{
			await lateArrivalDeductionAdjustmentService.SyncAsync(
			internalMonthYear,
			year,
			month,
			scopedEmployees,
			attendanceByEmployee,
			shiftsById,
			payrollContextByEmployee.ToDictionary(x => x.Key, x => x.Value.ResolvedPolicies));
			adjustmentsByEmployee = await adjustmentReader.GetByEmployeeAsync(internalMonthYear, employeeIds);
		}
		else
		{
			// Recalculate this derived adjustment in memory. A correction draft must not write attendance penalties.
			var employee = scopedEmployees.Single();
			var adjustments = adjustmentsByEmployee.GetValueOrDefault(employee.Id)?.ToList() ?? [];
			var previous = adjustments.FirstOrDefault(x => x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction);
			adjustments.RemoveAll(x => x.SourceType == PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction);
			if (payrollContextByEmployee.TryGetValue(employee.Id, out var context))
			{
				var desired = PayrollLateArrivalDeductionCalculator.Calculate(internalMonthYear, year, month, employee,
					attendanceByEmployee[employee.Id], ResolveShift(employee, shiftsById), context.ResolvedPolicies.AttendancePolicy);
				if (desired != null) adjustments.Add(new PayrollAdjustment(previous?.Id ?? desired.SourceId, employee.TenantId,
					internalMonthYear, employee.Id, PayrollLateArrivalDeductionCalculator.AdjustmentName, desired.Amount, false,
					PayrollLateArrivalDeductionCalculator.AdjustmentReason, desired.Description,
					PayrollAdjustmentSourceTypes.LateArrivalSalaryDeduction, desired.SourceId, true));
			}
			adjustmentsByEmployee[employee.Id] = adjustments;
		}

		foreach (var employee in scopedEmployees)
		{
			if (!payrollContextByEmployee.TryGetValue(employee.Id, out var payrollContext))
			{
				continue;
			}

			var payrollDetails = payrollDetailsByEmployee.GetValueOrDefault(employee.Id);
			var payrollComponents = await GetPayrollComponentsAsync(
				compensationByEmployee, employee.Id, year, month);
			var revision = compensationByEmployee.GetValueOrDefault(employee.Id)?.Revision;
			var pfRule = await providentFundComponentBuilder.ResolveRuleAsync(
				revision?.PfRuleId,
				employee.Id,
				year,
				month);
			var itemIssueCodes = payrollContext.PayrollPolicyIssueCodes.ToList();
			if (revision == null)
			{
				itemIssueCodes.Add(IssueCodes.CompensationRevisionRequired);
			}
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
				!correctionEmployeeId.HasValue && finalizedEmployeeIds.Contains(employee.Id),
				correctionEmployeeId.HasValue ? null : reviewByEmployee.GetValueOrDefault(employee.Id),
				previousMonthNetPayByEmployee.GetValueOrDefault(employee.Id),
				itemIssueCodes,
				PayrollRunRules.GetEffectiveEmploymentStartDate(employee),
				ResolveShift(employee, shiftsById),
				new PayrollStatutoryRuleSet(
					pfRule,
					professionalTaxSlabs,
					IsEmploymentCessationMonth(employee, year, month),
					esiContexts.GetValueOrDefault(employee.Id), esiConfiguration?.Id),
				revision
			));
			if (reportProgress != null && (computedItems.Count % 25 == 0 || computedItems.Count == scopedEmployees.Count))
				await reportProgress(computedItems.Count + immutableCount);
		}

		var pfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var pfCoverage = await pfEligibilityResolver.GetCoverageAsync(pfMonth);
		var pfMemberHistory = await pfEligibilityResolver.GetMemberHistoryAsync(computedItems.Select(x => x.EmployeeId).ToList(), pfMonth);
		foreach (var item in computedItems.Where(x => !x.IsLocked))
		{
			var resolution = compensationByEmployee.GetValueOrDefault(item.EmployeeId);
			var wages = resolution?.Components.Where(x => x.IsEarning && !x.IsTemp &&
				x.SemanticType is Templates.PayrollComponentSemanticType.Basic or Templates.PayrollComponentSemanticType.DearnessAllowance).Sum(x => x.Amount) ?? 0m;
			var declaration = PfEligibilityResolver.Declaration(resolution?.Revision);
			var decision = PfEligibilityCalculator.Calculate(resolution?.Revision.GrossSalary ?? (decimal) item.FullGrossSalary, wages, pfCoverage, declaration,
				pfMemberHistory.GetValueOrDefault(item.EmployeeId), item.ProvidentFundRuleId.HasValue);
			item.PfEligibilityDetailsJson = System.Text.Json.JsonSerializer.Serialize(decision);
			if (decision.IssueCode != null) item.IssueCodes.Add(decision.IssueCode);
		}
		var settlementEmployees = computedItems.Select(x => x.EmployeeId).ToList();
		var settlements = await payslipCorrections.GetListAsync(x => x.Status == "APPLIED" &&
			x.SettlementMonth == internalMonthYear && settlementEmployees.Contains(x.EmployeeId));
		foreach (var item in computedItems)
			foreach (var settlement in settlements.Where(x => x.EmployeeId == item.EmployeeId).OrderBy(x => x.CompletedAt).ThenBy(x => x.Id))
				PayslipSettlementProjection.Apply(item, settlement);
		if (beforePersist != null) await beforePersist();
		if (!correctionEmployeeId.HasValue)
			await snapshotStore.UpsertAsync(internalMonthYear, computedItems, preserveReviewedSnapshots);
		return computedItems;
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
		IReadOnlyDictionary<Guid, PayrollCompensationRevisionResolution> compensationByEmployee,
		Guid employeeId,
		int year,
		int month)
	{
		var resolution = compensationByEmployee.GetValueOrDefault(employeeId);
		var components = resolution?.Components.ToList() ?? [];
		return await providentFundComponentBuilder.AddProvidentFundDeductionAsync(
			employeeId,
			resolution?.Revision.PfRuleId,
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