using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Application service for managing employee status.
/// </summary>
public class EmployeeStatusAppService : ApplicationService, IEmployeeStatusAppService
{
	private readonly IEmployeeRepository _employeeRepository;
	private readonly EmployeeManager _employeeManager;

	public EmployeeStatusAppService(
		IEmployeeRepository employeeRepository,
		EmployeeManager employeeManager)
	{
		_employeeRepository = employeeRepository;
		_employeeManager = employeeManager;
	}

	/// <inheritdoc />
	public async Task<EmployeeStatusResponseDto> ChangeStatusAsync(Guid employeeId, ChangeEmployeeStatusDto input)
	{
		// Get the employee to capture previous status
		var employee = await _employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var previousStatus = employee.EmployeeStatusType;

		// Update the status using domain manager
		employee = await _employeeManager.UpdateEmployeeStatusAsync(
			employeeId, 
			input.EmployeeStatusType, 
			null);

		// Save the changes
		await _employeeRepository.UpdateAsync(employee);

		return new EmployeeStatusResponseDto
		{
			EmployeeId = employee.Id.ToString(),
			PreviousStatus = previousStatus,
			CurrentStatus = input.EmployeeStatusType,
			UpdatedAt = employee.StatusLastChangedAt ?? DateTimeOffset.UtcNow
		};
	}
}

