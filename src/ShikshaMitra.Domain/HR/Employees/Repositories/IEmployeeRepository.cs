using System;
using System.Collections.Generic;
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

	/// <summary>
	/// Gets an employee with reporting manager details.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Employee entity with reporting manager loaded</returns>
	Task<Employee?> GetWithReportingManagerAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets an employee with family members (Father, Mother, Spouse) included.
	/// Use this when you need employee data along with family information.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Employee entity with family members loaded</returns>
	Task<Employee?> GetWithFamilyAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets all employees reporting to a specific manager.
	/// </summary>
		/// <param name="managerId">The manager's employee ID</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>List of employees reporting to the manager</returns>
		Task<List<Employee>> GetEmployeesByManagerAsync(Guid managerId, CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets employees by their employee codes (bulk lookup for import operations).
		/// Case-insensitive lookup.
		/// </summary>
		/// <param name="employeeCodes">List of employee codes to lookup</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Dictionary mapping uppercase employee code to employee entity</returns>
		Task<Dictionary<string, Employee>> GetEmployeesByCodesAsync(
			IEnumerable<string> employeeCodes,
			CancellationToken cancellationToken = default);
	}