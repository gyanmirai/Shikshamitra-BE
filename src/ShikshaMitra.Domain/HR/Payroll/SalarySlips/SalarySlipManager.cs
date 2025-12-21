using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
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
		bool forceRegenerate = false,
		CancellationToken cancellationToken = default)
	{
		// Validate month-year format
		ValidateMonthYearFormat(monthYear);
		
		_logger.LogInformation("Generating salary slip for Employee: {EmployeeId}, Month: {MonthYear}", 
			employeeId, monthYear);

		// GetAsync already throws EntityNotFoundException if not found, so no null check needed
		var employee = await _employeeRepository.GetAsync(employeeId, cancellationToken: cancellationToken);

		// Check if salary slip already exists
		var existingSlip = await _salarySlipRepository.GetByEmployeeAndMonthAsync(employeeId, monthYear, cancellationToken);
		
		// If slip exists and is finalized, prevent regeneration
		if (existingSlip != null && existingSlip.Status == SalarySlipStatus.Finalized)
		{
			if (forceRegenerate)
			{
				throw new UserFriendlyException("Cannot regenerate a finalized salary slip");
			}
			_logger.LogInformation("Finalized salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}", 
				employeeId, monthYear);
			return existingSlip;
		}

		// If slip exists and not forcing regeneration, return existing draft slip
		if (existingSlip != null && !forceRegenerate)
		{
			_logger.LogInformation("Salary slip already exists for Employee: {EmployeeId}, Month: {MonthYear}", 
				employeeId, monthYear);
			return existingSlip;
		}

		// Get payroll components
		var earnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId, cancellationToken);
		var deductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId, cancellationToken);

		// Log warning if no payroll components found
		if (earnings.Count == 0 && deductions.Count == 0)
		{
			_logger.LogWarning("No payroll components found for Employee: {EmployeeId}. Generating empty salary slip.", employeeId);
		}

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

			await _salarySlipRepository.InsertAsync(salarySlip, cancellationToken: cancellationToken);
			
			_logger.LogInformation("Salary slip created for Employee: {EmployeeId}, Month: {MonthYear}, " +
				"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
				employeeId, monthYear, salarySlip.TotalEarning, salarySlip.TotalDeduction, salarySlip.NetSalary);

			return salarySlip;
		}

		// Update existing draft slip with new line items
		existingSlip.UpdateLineItems(earningLineItems, deductionLineItems);
		await _salarySlipRepository.UpdateAsync(existingSlip, cancellationToken: cancellationToken);

		_logger.LogInformation("Salary slip regenerated for Employee: {EmployeeId}, Month: {MonthYear}, " +
			"TotalEarning: {TotalEarning}, TotalDeduction: {TotalDeduction}, NetSalary: {NetSalary}",
			employeeId, monthYear, existingSlip.TotalEarning, existingSlip.TotalDeduction, existingSlip.NetSalary);

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
