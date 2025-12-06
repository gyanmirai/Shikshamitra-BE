using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeRepository : IRepository<Employee, Guid>
{
	/// <summary>
	/// Gets an employee by their associated user ID.
	/// Navigation properties (Role, EducationMediumStage, Shift) are auto-included via EF configuration.
	/// </summary>
	/// <param name="userId">The user ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Employee entity or null if not found</returns>
	Task<Employee?> GetEmployeeByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the last created employee (used for employee code generation).
	/// </summary>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>The most recently created employee or null</returns>
	Task<Employee?> GetLastEmployeeAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets an employee with all related data including PayrollStructure.
	/// Use this method when you need complete employee information.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="includePayrollStructure">Include payroll structure details</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Employee entity with all relations loaded</returns>
	Task<Employee?> GetWithDetailsAsync(
		Guid id, 
		bool includePayrollStructure = false,
		CancellationToken cancellationToken = default);
}