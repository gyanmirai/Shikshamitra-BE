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

	/// <summary>Checks an exact action permission, tenant, and employee scope independently of profile editing.</summary>
	Task CheckEmployeeScopedPermissionAsync(Guid employeeId, string permission);
}
