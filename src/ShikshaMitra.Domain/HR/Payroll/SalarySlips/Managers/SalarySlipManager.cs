using Microsoft.Extensions.Logging;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Domain service for managing salary slip generation and business logic
/// </summary>
public partial class SalarySlipManager : DomainService
{
	private readonly ISalarySlipRepository _salarySlipRepository;
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly SalarySlipGenerationDataReader _generationDataReader;
	private readonly HrPolicyResolver _hrPolicyResolver;
	private readonly PayrollCalendarResolver _payrollCalendarResolver;
	private readonly PayrollApprovedLeaveResolver _payrollApprovedLeaveResolver;
	private readonly PayrollComponentVersionResolver _componentVersionResolver;
	private readonly SalarySlipSnapshotWriter _snapshotWriter;
	private readonly ILogger<SalarySlipManager> _logger;

	public SalarySlipManager(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		SalarySlipGenerationDataReader generationDataReader,
		HrPolicyResolver hrPolicyResolver,
		PayrollCalendarResolver payrollCalendarResolver,
		PayrollApprovedLeaveResolver payrollApprovedLeaveResolver,
		PayrollComponentVersionResolver componentVersionResolver,
		SalarySlipSnapshotWriter snapshotWriter,
		ILogger<SalarySlipManager> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_generationDataReader = generationDataReader;
		_hrPolicyResolver = hrPolicyResolver;
		_payrollCalendarResolver = payrollCalendarResolver;
		_payrollApprovedLeaveResolver = payrollApprovedLeaveResolver;
		_componentVersionResolver = componentVersionResolver;
		_snapshotWriter = snapshotWriter;
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
		var normalizedMonthYear = SalarySlipGenerationRules.NormalizeMonthYear(monthYear);

		_logger.LogInformation("Generating salary slip for Employee: {EmployeeId}, Month: {MonthYear}",
			employeeId, normalizedMonthYear);

		var employee = await _employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken);

		var existingSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, normalizedMonthYear, cancellationToken);

		if (existingSlip != null && existingSlip.Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid)
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
		SalarySlipGenerationRules.EnsureEmployeeEligibleForPayrollMonth(employee, year, month);
		var calendar = await _payrollCalendarResolver.ResolveAsync(year, month, cancellationToken);
		var resolvedPolicies = await _hrPolicyResolver.ResolveAsync(
			employeeId,
			PayrollRunRules.GetPayrollPolicyResolutionDate(year, month),
			calendar.AcademicYearId,
			cancellationToken);
		var holidayOverrides = resolvedPolicies.PayrollPolicy.GetHolidayOverrides();
		var workCalendarHolidays = await _generationDataReader.GetIncludedWorkCalendarHolidaysAsync(resolvedPolicies.PayrollWorkCalendarPolicy.Id, year, month, cancellationToken);
		var workingUnitsByDate = PayrollMonthCalendar.BuildWorkingUnitsByDate(
			year,
			month,
			workCalendarHolidays,
			resolvedPolicies.PayrollPolicy.SaturdayWorkPolicy,
			holidayOverrides,
			resolvedPolicies.PayrollWorkCalendarPolicy);
		var basis = PayrollBasisDays.ResolvePolicy(
			resolvedPolicies.PayrollPolicy,
			year,
			month,
			workingUnitsByDate.Values.Sum(),
			workingUnitsByDate);

		var startDate = DateOnly.FromDateTime(new DateTime(year, month, 1));
		var endDate = DateOnly.FromDateTime(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
		var attendance = await _generationDataReader.GetAttendanceForEmployeeMonthAsync(employeeId, startDate, endDate);
		var leaveUnitsByEmployee = await _payrollApprovedLeaveResolver.ResolveLeaveUnitsByEmployeeAsync(
			new[] { employeeId },
			year,
			month,
			cancellationToken);
		var components = await _payrollComponentRepository.GetByEmployeeIdAsync(employeeId, cancellationToken);
		components = await _componentVersionResolver.ResolveAsync(components, normalizedMonthYear);
		components = PayrollComponentMonthScope.FilterForMonth(components, year, month);
		var adjustments = await _generationDataReader.GetAdjustmentsForEmployeeMonthAsync(employeeId, normalizedMonthYear, cancellationToken);
		var allComponents = components
			.Concat(adjustments.Select(SalarySlipAdjustmentComponentMapper.ToPayrollComponent))
			.ToList();
		var attendanceSummary = PayrollAttendanceCalculator.CalculatePayableDays(
			attendance,
			basis,
			year,
			month,
			workCalendarHolidays,
			leaveUnitsByEmployee.GetValueOrDefault(employeeId),
			SalarySlipGenerationRules.GetEffectiveEmploymentStartDate(employee));
		var computation = PayrollComputation.Calculate(
			attendanceSummary.PresentDays,
			attendanceSummary.HalfWorkingDays,
			attendanceSummary.PayableDays,
			basis.BasisDays,
			allComponents);

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
				deductionLineItems,
				Clock.Now);

			salarySlip.SetPayrollComputationSnapshot(
				basis.BasisDays,
				computation.PayableDays,
				computation.ProrationFactor,
				basis.EffectiveMode);
			SalarySlipGenerationRules.ApplyPolicySnapshot(salarySlip, resolvedPolicies);

			await _salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);

			_logger.LogInformation("Salary slip created for Employee: {EmployeeId}, Month: {MonthYear}, " +
				"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
				employeeId, normalizedMonthYear, salarySlip.TotalEarning, salarySlip.TotalDeduction, salarySlip.NetSalary);

			return salarySlip;
		}

		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems, Clock.Now);
		existingSlip.SetPayrollComputationSnapshot(
			basis.BasisDays,
			computation.PayableDays,
			computation.ProrationFactor,
			basis.EffectiveMode);
		SalarySlipGenerationRules.ApplyPolicySnapshot(existingSlip, resolvedPolicies);
		await _salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);

		_logger.LogInformation("Salary slip regenerated for Employee: {EmployeeId}, Month: {MonthYear}, " +
			"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
			employeeId, normalizedMonthYear, existingSlip.TotalEarning, existingSlip.TotalDeduction, existingSlip.NetSalary);

		return existingSlip;
	}

}
