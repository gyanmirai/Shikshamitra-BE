using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Domain service for managing salary slip generation and business logic
/// </summary>
public class SalarySlipManager : DomainService
{
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IAttendanceConfigurationRepository _attendanceConfigurationRepository;
	private readonly IRepository<AttendancePolicyEvaluationRun, Guid> _attendancePolicyEvaluationRunRepository;
	private readonly IRepository<AttendancePolicyEvaluationResult, Guid> _attendancePolicyEvaluationResultRepository;
	private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;
	private readonly PayrollApprovedLeaveResolver _payrollApprovedLeaveResolver;
	private readonly PayrollComponentVersionResolver _componentVersionResolver;
	private readonly ILogger<SalarySlipManager> _logger;

	public SalarySlipManager(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IAttendanceConfigurationRepository attendanceConfigurationRepository,
		IRepository<AttendancePolicyEvaluationRun, Guid> attendancePolicyEvaluationRunRepository,
		IRepository<AttendancePolicyEvaluationResult, Guid> attendancePolicyEvaluationResultRepository,
		IDayCalculationSettingRepository dayCalculationSettingRepository,
		PayrollCalendarResolver payrollCalendarResolver,
		PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
		PayrollComponentVersionResolver componentVersionResolver,
		ILogger<SalarySlipManager> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_attendanceRepository = attendanceRepository;
		_attendanceConfigurationRepository = attendanceConfigurationRepository;
		_attendancePolicyEvaluationRunRepository = attendancePolicyEvaluationRunRepository;
		_attendancePolicyEvaluationResultRepository = attendancePolicyEvaluationResultRepository;
		_dayCalculationSettingRepository = dayCalculationSettingRepository;
		_payrollCalendarResolver = payrollCalendarResolver;
		_payrollApprovedLeaveResolver = payrollApprovedLeaveResolver;
		_componentVersionResolver = componentVersionResolver;
		_logger = logger;
	}

	/// <summary>
	/// Generates or retrieves a salary slip for an employee for a specific month
	/// </summary>
	public async Task<SalarySlip> GenerateOrGetSalarySlipAsync(
		Guid employeeId,
		string monthYear,
		bool forceRegenerate = false,
		CancellationToken cancellationToken = default)
	{
		var normalizedMonthYear = NormalizeMonthYear(monthYear);

		_logger.LogInformation("Generating salary slip for Employee: {EmployeeId}, Month: {MonthYear}",
			employeeId, normalizedMonthYear);

		await _employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken);

		var existingSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, normalizedMonthYear, cancellationToken);

		if (existingSlip != null && existingSlip.Status == SalarySlipStatus.Finalized)
		{
			if (forceRegenerate)
			{
				throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
			}
			_logger.LogInformation("Finalized salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}",
				employeeId, normalizedMonthYear);
			return existingSlip;
		}

		if (existingSlip != null && !forceRegenerate)
		{
			_logger.LogInformation("Salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}",
				employeeId, normalizedMonthYear);
			return existingSlip;
		}

		var year = int.Parse(normalizedMonthYear[..4]);
		var month = int.Parse(normalizedMonthYear[5..7]);
		var calendar = await _payrollCalendarResolver.ResolveAsync(year, month, cancellationToken);
		var daySetting = await _dayCalculationSettingRepository.FindEffectiveAsync(CurrentTenant.Id, calendar.AcademicYearId, cancellationToken);
		var saturdayWorkPolicy = daySetting?.GetEffectiveSaturdayWorkPolicyForMonth(year, month) ?? SaturdayWorkPolicy.FullDay;
		var holidayOverrides = daySetting?.GetEffectiveHolidayOverridesForMonth(year, month) ?? Array.Empty<PayrollHolidayOverride>();
		var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			year,
			month,
			calendar.Holidays,
			saturdayWorkPolicy,
			holidayOverrides);
		var basis = PayrollBasisDays.Resolve(daySetting, year, month, workingUnitsByDate.Values.Sum(), workingUnitsByDate);

		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendance = await GetAttendanceForEmployeeMonthAsync(employeeId, startDate, endDate);
		var approvedAttendance = await GetApprovedAttendanceForEmployeeMonthAsync(
			employeeId,
			calendar.AcademicYearId,
			year,
			month,
			cancellationToken);
		var leaveUnitsByEmployee = await _payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			new[] { employeeId },
			year,
			month,
			cancellationToken);
		var components = await _payrollComponentRepository.GetByEmployeeIdAsync(employeeId, cancellationToken);
		components = await _componentVersionResolver.ResolveAsync(components, normalizedMonthYear);
		var attendanceSummary = approvedAttendance.Count > 0
			? PayrollAttendanceCalculator.CalculatePayableDays(
				approvedAttendance,
				basis,
				year,
				month,
				calendar.Holidays,
				leaveUnitsByEmployee.GetValueOrDefault(employeeId))
			: PayrollAttendanceCalculator.CalculatePayableDays(
				attendance,
				basis,
				year,
				month,
				calendar.Holidays,
				leaveUnitsByEmployee.GetValueOrDefault(employeeId));
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			components);

		if (components.Count == 0)
		{
			_logger.LogWarning("No payroll components found for Employee: {EmployeeId}. Generating empty salary slip.", employeeId);
		}

		var earningLineItems = computation.Earnings.Select(PayrollComputation.ToSalarySlipLineItem).ToList();
		var deductionLineItems = computation.Deductions.Select(PayrollComputation.ToSalarySlipLineItem).ToList();

		if (existingSlip == null)
		{
			var salarySlip = new SalarySlip(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				normalizedMonthYear,
				earningLineItems,
				deductionLineItems);

			salarySlip.SetPayrollComputationSnapshot(
				basis.BasisDays,
				computation.PayableDays,
				computation.ProrationFactor,
				basis.EffectiveMode);

			await _salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);

			_logger.LogInformation("Salary slip created for Employee: {EmployeeId}, Month: {MonthYear}, " +
				"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
				employeeId, normalizedMonthYear, salarySlip.TotalEarning, salarySlip.TotalDeduction, salarySlip.NetSalary);

			return salarySlip;
		}

		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems);
		existingSlip.SetPayrollComputationSnapshot(
			basis.BasisDays,
			computation.PayableDays,
			computation.ProrationFactor,
			basis.EffectiveMode);
		await _salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);

		_logger.LogInformation("Salary slip regenerated for Employee: {EmployeeId}, Month: {MonthYear}, " +
			"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
			employeeId, normalizedMonthYear, existingSlip.TotalEarning, existingSlip.TotalDeduction, existingSlip.NetSalary);

		return existingSlip;
	}

	private async Task<List<EmployeeAttendance>> GetAttendanceForEmployeeMonthAsync(
		Guid employeeId,
		DateOnly startDate,
		DateOnly endDate)
	{
		var attendanceQuery = await _attendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		return await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a =>
				a.EmployeeId == employeeId &&
				a.AttendanceDate >= startDate &&
				a.AttendanceDate <= endDate));
	}

	private async Task<List<PayrollApprovedAttendanceDay>> GetApprovedAttendanceForEmployeeMonthAsync(
		Guid employeeId,
		Guid? academicYearId,
		int year,
		int month,
		CancellationToken cancellationToken)
	{
		if (!academicYearId.HasValue)
		{
			return new List<PayrollApprovedAttendanceDay>();
		}

		var configuration = await _attendanceConfigurationRepository.FindByAcademicYearIdAsync(
			academicYearId.Value,
			cancellationToken);
		if (configuration?.PayrollIntegrationMode != AttendancePayrollIntegrationMode.ApprovedResults)
		{
			return new List<PayrollApprovedAttendanceDay>();
		}

		var runQuery = await _attendancePolicyEvaluationRunRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			runQuery = runQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			runQuery = runQuery.Where(x => x.TenantId == null);
		}

		var publishedRun = (await AsyncExecuter.ToListAsync(
				runQuery
					.Where(x =>
						x.AcademicYearId == academicYearId.Value &&
						x.Year == year &&
						x.Month == month &&
						x.Status == AttendancePolicyEvaluationRunStatus.Published)
					.OrderByDescending(x => x.PublishedAt ?? x.CreationTime),
				cancellationToken))
			.FirstOrDefault();
		if (publishedRun == null)
		{
			return new List<PayrollApprovedAttendanceDay>();
		}

		var resultQuery = await _attendancePolicyEvaluationResultRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			resultQuery = resultQuery.Where(x => x.TenantId == CurrentTenant.Id);
		}
		else
		{
			resultQuery = resultQuery.Where(x => x.TenantId == null);
		}

		var rows = await AsyncExecuter.ToListAsync(
			resultQuery.Where(x =>
				x.AttendancePolicyEvaluationRunId == publishedRun.Id &&
				x.EmployeeId == employeeId),
			cancellationToken);
		if (rows.Count == 0)
		{
			throw new BusinessException("ShikshaMitra:PublishedAttendanceEvaluationResultsIncomplete")
				.WithData("employeeId", employeeId)
				.WithData("year", year)
				.WithData("month", month);
		}

		return rows
			.Select(x => new PayrollApprovedAttendanceDay(x.AttendanceDate, x.EvaluatedStatus, x.EvaluatedPayableUnits))
			.ToList();
	}

	/// <summary>
	/// Validates month-year format
	/// </summary>
	public void ValidateMonthYearFormat(string monthYear)
	{
		NormalizeMonthYear(monthYear);
	}

	private static string NormalizeMonthYear(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new BusinessException(HRErrorCodes.MonthYearRequired);
		}

		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var normalizedMonthYear))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		return normalizedMonthYear;
	}
}
