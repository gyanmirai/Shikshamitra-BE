using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
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
	private readonly ILogger<SalarySlipManager> _logger;

	public SalarySlipManager(
		ISalarySlipRepository salarySlipRepository,
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		ILogger<SalarySlipManager> logger)
	{
		_salarySlipRepository = salarySlipRepository;
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_logger = logger;
	}

	/// <summary>
	/// Generates or retrieves a salary slip for an employee for a specific month
	/// </summary>
	public async Task<SalarySlip> GenerateOrGetSalarySlipAsync(
		Guid employeeId, 
		string monthYear,
		bool forceRegenerate = false)
	{
		_logger.LogInformation("Generating salary slip for Employee: {EmployeeId}, Month: {MonthYear}", 
			employeeId, monthYear);

		// Validate employee exists
		var employee = await _employeeRepository.GetAsync(employeeId);
		if (employee == null)
		{
			throw new UserFriendlyException($"Employee with ID {employeeId} not found");
		}

		// Check if salary slip already exists
		var existingSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, monthYear);
		
		if (existingSlip != null && !forceRegenerate)
		{
			_logger.LogInformation("Salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}", 
				employeeId, monthYear);
			return existingSlip;
		}

		if (existingSlip != null && existingSlip.Status == SalarySlipStatus.Finalized)
		{
			throw new UserFriendlyException("Cannot regenerate a finalized salary slip");
		}

		// Get payroll components
		var earnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		// Convert to salary slip line items
		var earningLineItems = earnings.Select(e => new SalarySlipLineItem(
			e.ComponentName, 
			e.Amount, 
			e.Type.ToString())).ToList();

		var deductionLineItems = deductions.Select(d => new SalarySlipLineItem(
			d.ComponentName, 
			d.Amount, 
			d.Type.ToString())).ToList();

		// Create or update salary slip
		if (existingSlip == null)
		{
			var salarySlip = new SalarySlip(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employeeId,
				monthYear,
				earningLineItems,
				deductionLineItems);

			await _salarySlipRepository.InsertAsync(salarySlip);
			
			_logger.LogInformation("Salary slip created for Employee: {EmployeeId}, Month: {MonthYear}, " +
				"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
				employeeId, monthYear, salarySlip.TotalEarning, salarySlip.TotalDeduction, salarySlip.NetSalary);

			return salarySlip;
		}

		_logger.LogInformation("Salary slip generated successfully for Employee: {EmployeeId}, Month: {MonthYear}", 
			employeeId, monthYear);

		return existingSlip;
	}

	/// <summary>
	/// Validates month-year format
	/// </summary>
	public void ValidateMonthYearFormat(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
		{
			throw new UserFriendlyException("MonthYear parameter is required");
		}

		if (!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", null,
			System.Globalization.DateTimeStyles.None, out _))
		{
			throw new UserFriendlyException("Invalid monthYear format. Expected format: YYYY-MM (e.g., 2025-01)");
		}
	}
}
