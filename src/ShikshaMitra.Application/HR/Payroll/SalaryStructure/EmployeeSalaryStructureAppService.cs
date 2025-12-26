using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// Application service for managing employee salary structure.
/// </summary>
public class EmployeeSalaryStructureAppService : ApplicationService, IEmployeeSalaryStructureAppService
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeeSalaryStructureAppService(
		IEmployeeRepository employeeRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IGuidGenerator guidGenerator)
	{
		_employeeRepository = employeeRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_guidGenerator = guidGenerator;
	}

	/// <inheritdoc />
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		// Validate employee exists (Repository.GetAsync throws if not found)
		_ = await _employeeRepository.GetAsync(employeeId);

		var earnings = await _payrollComponentRepository.GetEarningsByEmployeeIdAsync(employeeId);
		var deductions = await _payrollComponentRepository.GetDeductionsByEmployeeIdAsync(employeeId);

		return new SalaryStructureResponseDto
		{
			EarningComponents = MapToDto(earnings),
			DeductionComponents = MapToDto(deductions)
		};
	}

	/// <inheritdoc />
	public async Task UpdateAsync(Guid employeeId, UpdateSalaryStructureDto input)
	{
		// Validate employee exists (Repository.GetAsync throws if not found)
		_ = await _employeeRepository.GetAsync(employeeId);

		// Delete existing components
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);

		// Build all components for batch insert
		var componentsToInsert = new List<EmployeePayrollComponent>();

		// Build earning components
		foreach (var earning in input.EarningComponents)
		{
			componentsToInsert.Add(new EmployeePayrollComponent(
				_guidGenerator.Create(),
				employeeId,
				earning.Name,
				earning.Description,
				earning.Amount,
				earning.IsFixed,
				earning.IsTemp,
				earning.ComponentType,
				earning.DependOn,
				isEarning: true
			));
		}

		// Build deduction components
		foreach (var deduction in input.DeductionComponents)
		{
			componentsToInsert.Add(new EmployeePayrollComponent(
				_guidGenerator.Create(),
				employeeId,
				deduction.Name,
				deduction.Description,
				deduction.Amount,
				deduction.IsFixed,
				deduction.IsTemp,
				deduction.ComponentType,
				deduction.DependOn,
				isEarning: false
			));
		}

		// Batch insert all components (single DB round-trip)
		if (componentsToInsert.Count > 0)
		{
			await _payrollComponentRepository.InsertManyAsync(componentsToInsert);
		}
	}

	private static List<SalaryComponentDto> MapToDto(IEnumerable<EmployeePayrollComponent> components)
	{
		return components.Select(c => new SalaryComponentDto
		{
			Name = c.Name,
			Description = c.Description,
			Amount = c.Amount,
			IsFixed = c.IsFixed,
			IsTemp = c.IsTemp,
			ComponentType = c.ComponentType,
			DependOn = c.DependOn
		}).ToList();
	}
}

