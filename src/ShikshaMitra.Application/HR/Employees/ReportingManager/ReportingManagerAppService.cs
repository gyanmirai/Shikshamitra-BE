using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public class ReportingManagerAppService(IEmployeeRepository employeeRepository)
	: ApplicationService, IReportingManagerAppService
{
	public async Task<EmployeeReportingResponseDto> AssignReportingManagerAsync(
		Guid employeeId,
		AssignReportingManagerDto input)
	{
		// Validate employee exists
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		// Validate reporting manager exists
		var reportingManager = await employeeRepository.GetAsync(input.ReportingManagerId)
			?? throw new EntityNotFoundException(typeof(Employee), input.ReportingManagerId);

		// Business rule: Employee cannot report to themselves
		if (employeeId == input.ReportingManagerId)
		{
			throw new BusinessException(ShikshaMitraDomainErrorCodes.EmployeeCannotReportToSelf)
				.WithData("EmployeeId", employeeId);
		}

		// Business rule: Prevent circular reporting relationships
		await ValidateNoCircularReportingAsync(employeeId, input.ReportingManagerId);

		// Assign reporting manager
		employee.AssignReportingManager(input.ReportingManagerId);
		await employeeRepository.UpdateAsync(employee);

		// Get updated employee with reporting manager details
		var updatedEmployee = await employeeRepository.GetWithReportingManagerAsync(employeeId);

		return MapToEmployeeReportingResponse(updatedEmployee!);
	}

	public async Task<EmployeeReportingResponseDto> GetReportingManagerAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetWithReportingManagerAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		return MapToEmployeeReportingResponse(employee);
	}

	public async Task<EmployeesByManagerResponseDto> GetEmployeesByManagerAsync(Guid managerId)
	{
		// Validate manager exists
		var manager = await employeeRepository.GetAsync(managerId)
			?? throw new EntityNotFoundException(typeof(Employee), managerId);

		// Get employees under this manager
		var employees = await employeeRepository.GetEmployeesByManagerAsync(managerId);

		return new EmployeesByManagerResponseDto
		{
			Manager = MapToEmployeeBasic(manager),
			Employees = employees.Select(MapToEmployeeBasic).ToList()
		};
	}

	private async Task ValidateNoCircularReportingAsync(Guid employeeId, Guid newManagerId)
	{
		// Check if the new manager reports to this employee (directly or indirectly)
		var currentManagerId = newManagerId;
		var visited = new System.Collections.Generic.HashSet<Guid> { employeeId };

		while (currentManagerId != Guid.Empty)
		{
			if (visited.Contains(currentManagerId))
			{
				throw new BusinessException(ShikshaMitraDomainErrorCodes.CircularReportingNotAllowed)
					.WithData("EmployeeId", employeeId)
					.WithData("ManagerId", newManagerId);
			}

			visited.Add(currentManagerId);

			var manager = await employeeRepository.GetWithReportingManagerAsync(currentManagerId);
			currentManagerId = manager?.ReportingManagerId ?? Guid.Empty;
		}
	}

	private EmployeeReportingResponseDto MapToEmployeeReportingResponse(Employee employee)
	{
		return new EmployeeReportingResponseDto
		{
			EmployeeId = employee.Id,
			ReportingManager = employee.ReportingManager != null
				? MapToEmployeeBasic(employee.ReportingManager)
				: null
		};
	}

	private EmployeeBasicDto MapToEmployeeBasic(Employee employee)
	{
		return new EmployeeBasicDto
		{
			Id = employee.Id,
			EmployeeCode = employee.EmployeeCode,
			FullName = employee.EmployeeName,
			Designation = employee.Designation
		};
	}
}
