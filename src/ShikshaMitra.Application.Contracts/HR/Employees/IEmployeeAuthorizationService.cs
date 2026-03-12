using System;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Defines employee-specific authorization checks.
/// Prevents IDOR (Insecure Direct Object Reference) vulnerabilities.
/// </summary>
public interface IEmployeeAuthorizationService
{
	/// <summary>
	/// Checks if the current user can read a specific employee's data.
	/// </summary>
	/// <param name="employeeId">The employee ID to check access for.</param>
	/// <param name="requireOwnership">If true, only the employee themselves can access (stricter check).</param>
	Task CheckEmployeeAccessAsync(Guid employeeId, bool requireOwnership = false);

	/// <summary>
	/// Checks if the current user can modify a specific employee's data.
	/// </summary>
	/// <param name="employeeId">The employee ID to check access for.</param>
	Task CheckEmployeeModifyAccessAsync(Guid employeeId);

	/// <summary>
	/// Checks if the current user can modify sensitive fields (salary, role, etc.).
	/// </summary>
	/// <param name="employeeId">The employee ID to check access for.</param>
	Task CheckSensitiveFieldAccessAsync(Guid employeeId);

	/// <summary>
	/// Checks if the current user can perform bulk operations.
	/// </summary>
	Task CheckBulkOperationAccessAsync();
}
