using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

/// <summary>
/// Manages payroll structure operations and employee assignments.
/// Automatically initializes employee-specific payroll data when structures are assigned.
/// </summary>
public class PayrollStructureManager : DomainService
{
	private readonly IPayrollStructureRepository _payrollStructureRepository;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly EmployeePayrollInitializationService _initializationService;
	private readonly ILogger<PayrollStructureManager> _logger;

	public PayrollStructureManager(
		IPayrollStructureRepository payrollStructureRepository,
		IRepository<Employee, Guid> employeeRepository,
		EmployeePayrollInitializationService initializationService,
		ILogger<PayrollStructureManager> logger)
	{
		_payrollStructureRepository = payrollStructureRepository;
		_employeeRepository = employeeRepository;
		_initializationService = initializationService;
		_logger = logger;
	}

	/// <summary>
	/// Assigns a payroll structure to an employee and automatically initializes their payroll data.
	/// This creates EmployeePayrollDetails and EmployeePayrollComponent records based on the structure.
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	/// <param name="payrollStructureId">Payroll structure ID to assign</param>
	/// <param name="autoInitialize">If true, automatically initializes employee payroll data</param>
	/// <exception cref="UserFriendlyException">Thrown when employee or structure not found</exception>
	public async Task SetEmployeePayrollStructureAsync(
		Guid employeeId,
		Guid payrollStructureId,
		bool autoInitialize = true)
	{
		_logger.LogInformation(
			"Assigning payroll structure {PayrollStructureId} to employee {EmployeeId}",
			payrollStructureId,
			employeeId
		);

		// Validate employee exists
		var employee = await _employeeRepository.GetAsync(employeeId)
			?? throw new UserFriendlyException("Employee not found");

		// Validate payroll structure exists
		var payrollStructure = await _payrollStructureRepository.GetAsync(payrollStructureId)
			?? throw new UserFriendlyException("Payroll structure not found");

		// Assign structure to employee
		employee.SetPayrollStructure(payrollStructureId);
		await _employeeRepository.UpdateAsync(employee);

		_logger.LogInformation(
			"Payroll structure {PayrollStructureId} assigned to employee {EmployeeId}",
			payrollStructureId,
			employeeId
		);

		// Auto-initialize employee payroll data if requested
		if (autoInitialize)
		{
			try
			{
				_logger.LogInformation(
					"Auto-initializing payroll data for employee {EmployeeId}",
					employeeId
				);

				// Check if already initialized
				var isInitialized = await _initializationService.IsInitializedAsync(employeeId);

				if (isInitialized)
				{
					// Sync existing components with new structure
					_logger.LogInformation(
						"Employee {EmployeeId} payroll already initialized. Syncing components...",
						employeeId
					);
					await _initializationService.SyncComponentsFromStructureAsync(employeeId);
				}
				else
				{
					// Initialize from scratch
					_logger.LogInformation(
						"Initializing payroll data for employee {EmployeeId} from structure",
						employeeId
					);
					await _initializationService.InitializeFromStructureAsync(employeeId);
				}

				_logger.LogInformation(
					"Payroll data initialization completed for employee {EmployeeId}",
					employeeId
				);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Failed to auto-initialize payroll data for employee {EmployeeId}. Structure was assigned but payroll data needs manual setup.",
					employeeId
				);

				// Don't throw - structure is assigned, initialization can be done manually
			}
		}
	}

	/// <summary>
	/// Removes payroll structure assignment from an employee.
	/// Note: This does NOT delete EmployeePayrollDetails/Components.
	/// Use with caution or manually clean up employee payroll data.
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	/// <param name="deletePayrollData">If true, also deletes employee's payroll data</param>
	public async Task RemoveEmployeePayrollStructureAsync(Guid employeeId, bool deletePayrollData = false)
	{
		_logger.LogWarning(
			"Removing payroll structure from employee {EmployeeId}. DeletePayrollData={DeletePayrollData}",
			employeeId,
			deletePayrollData
		);

		var employee = await _employeeRepository.GetAsync(employeeId)
			?? throw new UserFriendlyException("Employee not found");

		employee.RemovePayrollStructure();
		await _employeeRepository.UpdateAsync(employee);

		if (deletePayrollData)
		{
			_logger.LogWarning(
				"Deleting payroll data for employee {EmployeeId}",
				employeeId
			);

			// Use initialization service to clean up
			await _initializationService.InitializeFromStructureAsync(employeeId, replaceExisting: false);
		}

		_logger.LogInformation(
			"Payroll structure removed from employee {EmployeeId}",
			employeeId
		);
	}

	/// <summary>
	/// Re-initializes employee payroll data from current structure.
	/// Useful when structure has been modified and needs to be re-applied.
	/// </summary>
	/// <param name="employeeId">Employee ID</param>
	public async Task ReinitializeEmployeePayrollAsync(Guid employeeId)
	{
		_logger.LogInformation(
			"Re-initializing payroll data for employee {EmployeeId}",
			employeeId
		);

		var employee = await _employeeRepository.GetAsync(employeeId);

		if (!employee.PayrollStructureId.HasValue)
		{
			throw new UserFriendlyException(
				"Cannot re-initialize payroll: Employee has no payroll structure assigned."
			);
		}

		await _initializationService.InitializeFromStructureAsync(employeeId, replaceExisting: true);

		_logger.LogInformation(
			"Payroll data re-initialized for employee {EmployeeId}",
			employeeId
		);
	}
}

