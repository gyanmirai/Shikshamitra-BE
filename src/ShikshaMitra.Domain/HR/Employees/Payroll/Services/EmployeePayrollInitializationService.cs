using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Service to initialize employee payroll data from assigned payroll structures.
/// Bridges the gap between master payroll templates (HR.Payroll) and
/// employee-specific payroll data (HR.Employees.Payroll).
/// </summary>
public class EmployeePayrollInitializationService : DomainService
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly IPayrollStructureRepository _payrollStructureRepository;
	private readonly IEmployeePayrollDetailsRepository _payrollDetailsRepository;
	private readonly IEmployeePayrollComponentRepository _payrollComponentRepository;
	private readonly IGuidGenerator _guidGenerator;

	public EmployeePayrollInitializationService(
		IEmployeeRepository employeeRepository,
		IPayrollStructureRepository payrollStructureRepository,
		IEmployeePayrollDetailsRepository payrollDetailsRepository,
		IEmployeePayrollComponentRepository payrollComponentRepository,
		IGuidGenerator guidGenerator)
	{
		_employeeRepository = employeeRepository;
		_payrollStructureRepository = payrollStructureRepository;
		_payrollDetailsRepository = payrollDetailsRepository;
		_payrollComponentRepository = payrollComponentRepository;
		_guidGenerator = guidGenerator;
	}

	/// <summary>
	/// Initializes employee payroll data from assigned payroll structure.
	/// Creates EmployeePayrollDetails and clones components from structure.
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	/// <param name="replaceExisting">If true, replaces existing payroll data. If false, only creates if missing.</param>
	/// <exception cref="BusinessException">Thrown when employee has no payroll structure assigned</exception>
	public async Task InitializeFromStructureAsync(Guid employeeId, bool replaceExisting = false)
	{
		// Get employee
		var employee = await _employeeRepository.GetAsync(employeeId);

		// Validate employee has a payroll structure assigned
		if (!employee.PayrollStructureId.HasValue)
		{
			throw new BusinessException(
				"ShikshaMitra:EmployeeHasNoPayrollStructure",
				$"Employee {employeeId} has no payroll structure assigned. Please assign a structure first."
			);
		}

		// Check if employee already has payroll details
		var existingDetails = await _payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);

		if (existingDetails != null && !replaceExisting)
		{
			// Already initialized, skip
			return;
		}

		// Get all payroll structures for this employee
		var structures = await _payrollStructureRepository.GetListAsync(s => s.EmployeeId == employeeId);

		if (!structures.Any())
		{
			throw new BusinessException(
				"ShikshaMitra:PayrollStructureNotFound",
				$"Payroll structures not found for employee {employeeId}"
			);
		}

		// Delete existing data if replacing
		if (replaceExisting && existingDetails != null)
		{
			await DeleteExistingPayrollDataAsync(employeeId);
		}

		// Initialize EmployeePayrollDetails (bank & salary info)
		await InitializePayrollDetailsAsync(employeeId, structures);

		// Initialize EmployeePayrollComponents (earning & deduction components)
		await InitializePayrollComponentsAsync(employeeId, structures);
	}

	/// <summary>
	/// Synchronizes employee payroll components with the latest payroll structure.
	/// Adds new components, updates amounts, and removes components no longer in structure.
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	public async Task SyncComponentsFromStructureAsync(Guid employeeId)
	{
		// Get employee's current payroll structures
		var structures = await _payrollStructureRepository.GetListAsync(s => s.EmployeeId == employeeId);

		if (!structures.Any())
		{
			throw new BusinessException(
				"ShikshaMitra:PayrollStructureNotFound",
				$"Payroll structures not found for employee {employeeId}"
			);
		}

		// Get existing components
		var existingComponents = await _payrollComponentRepository.GetByEmployeeIdAsync(employeeId);

		// Create a dictionary for quick lookup by name (since there's no PayElementId in component)
		var existingComponentsDict = existingComponents.ToDictionary(c => c.Name);

		foreach (var structure in structures)
		{
			// Get pay element name
			var payElementName = structure.PayElement?.Name ?? structure.PayElementId.ToString();

			if (existingComponentsDict.TryGetValue(payElementName, out var existingComponent))
			{
				// Update existing component amount (EmployeePayrollComponent doesn't have UpdateAmount method)
				// We need to update through repository
				existingComponent.Amount = (decimal)structure.Amount;
				await _payrollComponentRepository.UpdateAsync(existingComponent);
			}
			else
			{
				// Add new component
				var newComponent = new EmployeePayrollComponent(
					id: _guidGenerator.Create(),
					employeeId: employeeId,
					name: payElementName,
					description: structure.PayElement?.Code,
					amount: (decimal)structure.Amount,
					isFixed: true,
					isTemp: false,
					componentType: SalaryComponentType.Fixed,
					dependOn: null,
					isEarning: structure.PayElementType == PayElementType.Credit
				);
				await _payrollComponentRepository.InsertAsync(newComponent);
			}
		}

		// Remove components that are no longer in structure
		var structureNames = structures
			.Select(s => s.PayElement?.Name ?? s.PayElementId.ToString())
			.ToHashSet();
		var componentsToRemove = existingComponents.Where(c => !structureNames.Contains(c.Name));

		foreach (var componentToRemove in componentsToRemove)
		{
			await _payrollComponentRepository.DeleteAsync(componentToRemove);
		}
	}

	/// <summary>
	/// Checks if employee payroll data has been initialized
	/// </summary>
	public async Task<bool> IsInitializedAsync(Guid employeeId)
	{
		var details = await _payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		return details != null;
	}

	/// <summary>
	/// Initializes EmployeePayrollDetails with default values from structure
	/// </summary>
	private async Task InitializePayrollDetailsAsync(Guid employeeId, List<PayrollStructure> structures)
	{
		// Calculate basic salary from structure (sum of credit components)
		var creditStructures = structures.Where(s => s.PayElementType == PayElementType.Credit).ToList();
		var basicSalary = creditStructures.Any()
			? (decimal)creditStructures.Sum(s => s.Amount)
			: 0m;

		// Create EmployeePayrollDetails with defaults
		var details = new EmployeePayrollDetails(
			id: _guidGenerator.Create(),
			employeeId: employeeId
		);

		// Set basic salary
		details.UpdateDetails(
			bankName: null,
			branchName: null,
			accountHolderName: null,
			accountNumber: null,
			ifscCode: null,
			pfRule: null,
			epfNumber: null,
			uanNumber: null,
			punchId: null,
			basicSalary: basicSalary,
			tdsPercentage: null,
			tdsAmount: null
		);

		await _payrollDetailsRepository.InsertAsync(details);
	}

	/// <summary>
	/// Initializes EmployeePayrollComponents by cloning from structure
	/// </summary>
	private async Task InitializePayrollComponentsAsync(Guid employeeId, List<PayrollStructure> structures)
	{
		var components = new List<EmployeePayrollComponent>();

		foreach (var structure in structures)
		{
			var payElementName = structure.PayElement?.Name ?? structure.PayElementId.ToString();
			var payElementCode = structure.PayElement?.Code;

			var component = new EmployeePayrollComponent(
				id: _guidGenerator.Create(),
				employeeId: employeeId,
				name: payElementName,
				description: payElementCode,
				amount: (decimal)structure.Amount,
				isFixed: true,
				isTemp: false,
				componentType: SalaryComponentType.Fixed,
				dependOn: null,
				isEarning: structure.PayElementType == PayElementType.Credit
			);

			components.Add(component);
		}

		await _payrollComponentRepository.InsertManyAsync(components);
	}

	/// <summary>
	/// Deletes existing employee payroll data
	/// </summary>
	private async Task DeleteExistingPayrollDataAsync(Guid employeeId)
	{
		// Delete EmployeePayrollDetails
		var details = await _payrollDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (details != null)
		{
			await _payrollDetailsRepository.DeleteAsync(details);
		}

		// Delete EmployeePayrollComponents (both earnings and deductions)
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: true);
		await _payrollComponentRepository.DeleteByEmployeeIdAsync(employeeId, isEarning: false);
	}
}
