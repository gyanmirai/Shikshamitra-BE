using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.Employees.ReportingManager;

[RemoteService(false)]
public class ReportingManagerAppService(
	IEmployeeRepository employeeRepository,
	ICurrentUser currentUser)
	: ApplicationService, IReportingManagerAppService
{
	private const int MaxHierarchyDepth = 64;

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
		if (!await IsAdminBypassAsync())
		{
			var actorEmployee = await ResolveCurrentEmployeeAsync();
			if (actorEmployee == null || actorEmployee.Id != managerId)
			{
				throw new AbpAuthorizationException("Access denied: You can only view your own reporting hierarchy.");
			}
		}

		// Validate manager exists
		var manager = await employeeRepository.GetAsync(managerId)
			?? throw new EntityNotFoundException(typeof(Employee), managerId);

		var employees = await GetEmployeesByManagerHierarchyAsync(managerId);

		return new EmployeesByManagerResponseDto
		{
			Manager = MapToEmployeeBasic(manager),
			Employees = employees.Select(MapToEmployeeBasic).ToList()
		};
	}

	public async Task<EmployeesByManagerResponseDto> GetMyEmployeesByManagerAsync()
	{
		var actorEmployee = await ResolveCurrentEmployeeAsync()
			?? throw new AbpAuthorizationException("Access denied: Employee profile not found.");

		return await GetEmployeesByManagerAsync(actorEmployee.Id);
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

	private async Task<List<Employee>> GetEmployeesByManagerHierarchyAsync(Guid managerId)
	{
		var result = new List<Employee>();
		var visited = new HashSet<Guid> { managerId };
		var queue = new Queue<Guid>();
		queue.Enqueue(managerId);
		var depth = 0;

		while (queue.Count > 0 && depth < MaxHierarchyDepth)
		{
			depth++;
			var currentManager = queue.Dequeue();
			var directReports = await employeeRepository.GetEmployeesByManagerAsync(currentManager);

			foreach (var employee in directReports)
			{
				if (!visited.Add(employee.Id))
				{
					continue;
				}

				result.Add(employee);
				queue.Enqueue(employee.Id);
			}
		}

		return result;
	}

	private async Task<Employee?> ResolveCurrentEmployeeAsync()
	{
		var userId = currentUser.Id;
		if (!userId.HasValue)
		{
			return null;
		}

		return await employeeRepository.GetEmployeeByUserIdAsync(userId.Value);
	}

	private Task<bool> IsAdminBypassAsync()
	{
		return Task.FromResult(currentUser.IsInRole("admin") || currentUser.IsInRole("Administrator"));
	}
}
