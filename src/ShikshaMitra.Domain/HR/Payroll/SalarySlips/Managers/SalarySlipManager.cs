using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
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
	private readonly IRepository<Holiday, Guid> _holidayRepository;
	private readonly IRepository<EmployeeAttendance, Guid> _attendanceRepository;
	private readonly IDayCalculationSettingRepository _dayCalculationSettingRepository;
	private readonly ILogger<SalarySlipManager> _logger;

	public SalarySlipManager(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IRepository<Holiday, Guid> holidayRepository,
		IRepository<EmployeeAttendance, Guid> attendanceRepository,
		IDayCalculationSettingRepository dayCalculationSettingRepository,
		ILogger<SalarySlipManager> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_holidayRepository = holidayRepository;
		_attendanceRepository = attendanceRepository;
		_dayCalculationSettingRepository = dayCalculationSettingRepository;
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
		ValidateMonthYearFormat(monthYear);

		_logger.LogInformation("Generating salary slip for Employee: {EmployeeId}, Month: {MonthYear}",
			employeeId, monthYear);

		var employee = await _employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken);

		var existingSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, monthYear, cancellationToken);

		if (existingSlip != null && existingSlip.Status == SalarySlipStatus.Finalized)
		{
			if (forceRegenerate)
			{
				throw new BusinessException(HRErrorCodes.SalarySlipAlreadyFinalized);
			}
			_logger.LogInformation("Finalized salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}",
				employeeId, monthYear);
			return existingSlip;
		}

		if (existingSlip != null && !forceRegenerate)
		{
			_logger.LogInformation("Salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}",
				employeeId, monthYear);
			return existingSlip;
		}

		if (!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var monthStart))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		var year = monthStart.Year;
		var month = monthStart.Month;
		var holidays = await GetHolidaysForMonthAsync(year, month);
		var workingWeekdaysMinusHolidays = PayrollMonthCalendar.CountWorkingWeekdaysExcludingHolidayWeekdays(year, month, holidays);
		var daySetting = await _dayCalculationSettingRepository.FindByTenantAsync(CurrentTenant.Id, cancellationToken);
		var basis = PayrollBasisDays.Resolve(daySetting, year, month, workingWeekdaysMinusHolidays);

		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendance = await GetAttendanceForEmployeeMonthAsync(employeeId, startDate, endDate);
		var presentDays = attendance.Count(a => a.StatusType == EmployeeAttendanceStatusType.Present);
		var halfDays = attendance.Count(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave);
		var payableDays = PayrollProrationCalculator.CalculatePayableDays(presentDays, halfDays);
		var factor = PayrollProrationCalculator.CalculateProrationFactor(basis.BasisDays, payableDays);

		var earnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId, cancellationToken);
		var deductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId, cancellationToken);

		if (earnings.Count == 0 && deductions.Count == 0)
		{
			_logger.LogWarning("No payroll components found for Employee: {EmployeeId}. Generating empty salary slip.", employeeId);
		}

		var earningLineItems = earnings.Select(e => new SalarySlipLineItem(
			e.Name,
			PayrollProrationCalculator.ProrateAmount(e.Amount, e.IsTemp, factor),
			e.Description,
			e.IsFixed,
			e.IsTemp,
			e.ComponentType,
			e.DependOn ?? SalaryComponentDependency.None)).ToList();

		var deductionLineItems = deductions.Select(d => new SalarySlipLineItem(
			d.Name,
			PayrollProrationCalculator.ProrateAmount(d.Amount, d.IsTemp, factor),
			d.Description,
			d.IsFixed,
			d.IsTemp,
			d.ComponentType,
			d.DependOn ?? SalaryComponentDependency.None)).ToList();

		if (existingSlip == null)
		{
			var salarySlip = new SalarySlip(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				monthYear,
				earningLineItems,
				deductionLineItems);

			salarySlip.SetPayrollComputationSnapshot(basis.BasisDays, payableDays, factor, basis.EffectiveMode);

			await _salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);

			_logger.LogInformation("Salary slip created for Employee: {EmployeeId}, Month: {MonthYear}, " +
				"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
				employeeId, monthYear, salarySlip.TotalEarning, salarySlip.TotalDeduction, salarySlip.NetSalary);

			return salarySlip;
		}

		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems);
		existingSlip.SetPayrollComputationSnapshot(basis.BasisDays, payableDays, factor, basis.EffectiveMode);
		await _salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);

		_logger.LogInformation("Salary slip regenerated for Employee: {EmployeeId}, Month: {MonthYear}, " +
			"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
			employeeId, monthYear, existingSlip.TotalEarning, existingSlip.TotalDeduction, existingSlip.NetSalary);

		return existingSlip;
	}

	private async Task<List<Holiday>> GetHolidaysForMonthAsync(int year, int month)
	{
		var firstDay = new DateTime(year, month, 1);
		var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
		var startDate = new DateTimeOffset(firstDay);
		var endDate = new DateTimeOffset(lastDay.AddDays(1).AddTicks(-1));

		var holidayQuery = await _holidayRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			holidayQuery = holidayQuery.Where(h => h.TenantId == CurrentTenant.Id);
		}

		return await AsyncExecuter.ToListAsync(
			holidayQuery.Where(h => h.StartDate <= endDate && h.EndDate >= startDate));
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

	/// <summary>
	/// Validates month-year format
	/// </summary>
	public void ValidateMonthYearFormat(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new BusinessException(HRErrorCodes.MonthYearRequired);
		}

		if (!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", null,
			DateTimeStyles.None, out _))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}
	}
}
