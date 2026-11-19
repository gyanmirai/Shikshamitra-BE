using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.ReportingManager;

public interface IReportingManagerAppService : IApplicationService
{
	Task<System.Collections.Generic.List<EmployeeBasicDto>> GetManagerOptionsAsync(string? search = null);
	/// <summary>
	/// Assigns or updates the reporting manager for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <param name="input">The DTO containing the reporting manager ID.</param>
	/// <returns>The employee reporting response with updated reporting manager details.</returns>
	Task<EmployeeReportingResponseDto> AssignReportingManagerAsync(Guid employeeId, AssignReportingManagerDto input);

	/// <summary>
	/// Gets the reporting manager of an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee.</param>
	/// <returns>The employee reporting response with reporting manager details.</returns>
	Task<EmployeeReportingResponseDto> GetReportingManagerAsync(Guid employeeId);

	/// <summary>
	/// Gets all employees reporting to a specific manager.
	/// </summary>
	/// <param name="managerId">The ID of the manager.</param>
	/// <returns>The response containing manager details and list of employees under them.</returns>
	Task<EmployeesByManagerResponseDto> GetEmployeesByManagerAsync(Guid managerId);

	/// <summary>
	/// Gets all employees reporting to the current signed-in manager.
	/// </summary>
	Task<EmployeesByManagerResponseDto> GetMyEmployeesByManagerAsync();
}
